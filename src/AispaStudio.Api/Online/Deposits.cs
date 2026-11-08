using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>
/// Anzahlung und Vorauszahlung beim Online-Buchen (ADR 0047): Verlangt der Standort oder die Behandlung eine Anzahlung, stimmt der
/// Gast beim Buchen zu und bezahlt sie beim Zahlungsanbieter. Die Kasse des Standorts stellt den Anzahlungsbeleg aus und
/// verrechnet den Betrag beim Kassieren der Behandlung (<see cref="PosService"/>). Eine Anzahlung ersetzt die Kartenhinterlegung.
/// </summary>
internal sealed class Deposits(
    AispaDbContext db,
    OnlineDbContext online,
    IMessageBus bus,
    TimeProvider time,
    IEnumerable<IOnlinePaymentProvider> providers,
    PaymentAccounts payments)
{
    /// <summary>Die Testzahlung, falls der Server sie anbietet; welcher Anbieter für neue Anzahlungen gilt, sagt <see cref="PaymentAccounts"/>.</summary>
    private readonly IOnlinePaymentProvider? _test = providers.LastOrDefault(p => p.IsTest);
    private readonly Dictionary<Guid, bool> _registers = [];

    /// <summary>Prozent am Standort, ohne zu prüfen, ob Zahlungskonto und Kasse bereit sind (<see cref="AvailableAsync"/>).</summary>
    public static int EffectivePercent(LocationOnlineRules rules) => Math.Clamp(rules.DepositPercent, 0, 100);

    /// <summary>Prozent für eine Behandlung: eigene Prozent der Behandlung vor der Regel des Standorts.</summary>
    public static int EffectivePercent(LocationOnlineRules rules, Treatment treatment) =>
        Math.Clamp(treatment.DepositPercent ?? rules.DepositPercent, 0, 100);

    /// <summary>
    /// Die Anzahlung braucht ein bereites Zahlungskonto des Spas (ADR 0054) und für den Anzahlungsbeleg eine aktive Kasse am Standort;
    /// sonst verlangt der Kundenbereich keine Anzahlung (und fällt auf die Kartenhinterlegung zurück, falls eingestellt).
    /// </summary>
    public async Task<bool> AvailableAsync(Guid locationId, CancellationToken ct)
    {
        if (!_registers.TryGetValue(locationId, out var available))
        {
            _registers[locationId] = available = await payments.ActiveProviderAsync(ct) is not null
                && await db.CashRegisters.AnyAsync(r => r.LocationId == locationId && r.IsActive, ct);
        }

        return available;
    }

    /// <summary>Wirksame Prozent am Standort für die Behandlung; 0 ohne Kasse.</summary>
    public async Task<int> PercentAtAsync(LocationOnlineRules rules, Treatment treatment, Guid locationId, CancellationToken ct) =>
        await AvailableAsync(locationId, ct) ? EffectivePercent(rules, treatment) : 0;

    /// <summary>
    /// Prozent für eine Buchung: wie <see cref="PercentAtAsync"/>, aber 0 für Stammgäste, wenn der Standort Gebühr und Anzahlung nur
    /// von neuen Gästen verlangt. Neu ist, wer im Spa noch keine abgeschlossene Behandlung hatte
    /// oder wiederholt nicht erschienen ist (<see cref="NoShowHistory"/>).
    /// </summary>
    public async Task<int> PercentForAsync(LocationOnlineRules rules, Treatment treatment, Guid locationId, Guid customerId, CancellationToken ct)
    {
        var percent = await PercentAtAsync(rules, treatment, locationId, ct);
        if (percent > 0 && rules.NoShowFeeNewCustomersOnly
            && await db.Appointments.AnyAsync(a => a.CustomerId == customerId && a.Status == AppointmentStatus.Completed, ct)
            && !await NoShowHistory.IsRepeatAsync(db, customerId, time.GetUtcNow(), ct))
        {
            return 0;
        }

        return percent;
    }

    /// <summary>Legt die Anzahlung zur Buchung an (gespeichert wird mit der Buchung); ohne Anzahlung nichts. Die Behandlung muss geladen sein.</summary>
    public async Task<DepositOrder?> AddAsync(Appointment appointment, string currency, LocationOnlineRules rules, string email, string language, CancellationToken ct)
    {
        var percent = await PercentForAsync(rules, appointment.Treatment, appointment.LocationId, appointment.CustomerId, ct);
        var amount = CardGuarantees.Fee(appointment.Price, percent);
        if (percent <= 0 || amount <= 0)
        {
            return null;
        }

        DateTimeOffset? releaseAt = rules.CardHoldHours > 0 ? time.GetUtcNow().AddHours(rules.CardHoldHours) : null;
        var deposit = new DepositOrder
        {
            AppointmentId = appointment.Id,
            LocationId = appointment.LocationId,
            CustomerId = appointment.CustomerId,
            Email = email,
            Language = language,
            Percent = percent,
            Amount = amount,
            Currency = currency,
            AcceptedAt = time.GetUtcNow(),
            DeadlineHours = rules.CancellationDeadlineHours,
            Status = OnlineDepositStatus.Pending,
            ReleaseAt = releaseAt < appointment.Start ? releaseAt : null,
            Provider = (await payments.ActiveProviderAsync(ct))!.Name,
        };
        online.Deposits.Add(deposit);
        return deposit;
    }

    /// <summary>Beginnt die Zahlung. Im Testbetrieb sofort, sonst fragt die Queue den Anbieter nach seiner Zahlungsseite.</summary>
    public async Task StartAsync(DepositOrder deposit, string treatmentName, string locationName, string returnUrl, CancellationToken ct)
    {
        deposit.Attempts++;
        deposit.ProviderReference = null;
        deposit.PaymentUrl = null;
        var tenantId = await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == deposit.LocationId), ct) ?? Guid.Empty;
        var payment = new PaymentStart(tenantId, deposit.Id, deposit.Amount, deposit.Currency, $"Anzahlung {treatmentName}, {locationName}", deposit.Email,
            returnUrl, deposit.Attempts);
        if (_test is { } test && test.Name == deposit.Provider)
        {
            deposit.ProviderReference = (await test.StartAsync(payment, ct)).Reference;
            await online.SaveChangesAsync(ct);
            return;
        }

        await online.SaveChangesAsync(ct);
        await bus.PublishAsync(new StartOnlinePayment(payment));
    }

    /// <summary>
    /// Der Anbieter hat die Zahlung bestätigt. Idempotent; der Anzahlungsbeleg entsteht entkoppelt über die Queue. Auch wenn der Termin
    /// inzwischen freigegeben oder abgesagt ist: das Geld ist da, bekommt seinen Beleg und wird danach erstattet.
    /// </summary>
    public async Task ConfirmPaymentAsync(Guid depositId, string providerReference, CancellationToken ct)
    {
        var updated = await online.Deposits
            .Where(d => d.Id == depositId && d.ProviderReference == providerReference
                && (d.Status == OnlineDepositStatus.Pending || d.Status == OnlineDepositStatus.Failed
                    || d.Status == OnlineDepositStatus.Cancelled || d.Status == OnlineDepositStatus.Released))
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, OnlineDepositStatus.Paid).SetProperty(d => d.PaidAt, time.GetUtcNow())
                .SetProperty(d => d.PaymentUrl, (string?)null), ct);
        if (updated == 1)
        {
            await bus.PublishAsync(new ReceiptOnlineDeposit(depositId));
        }
    }

    /// <summary>Abgebrochen oder abgelaufen: der Gast kann neu beginnen, solange der Termin bevorsteht.</summary>
    public Task PaymentFailedAsync(Guid depositId, string providerReference, CancellationToken ct) =>
        online.Deposits
            .Where(d => d.Id == depositId && d.Status == OnlineDepositStatus.Pending && d.ProviderReference == providerReference)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, OnlineDepositStatus.Failed).SetProperty(d => d.PaymentUrl, (string?)null), ct);

    /// <summary>Nur im Testbetrieb: Zahlung simulieren oder abbrechen.</summary>
    public async Task<bool> TestAsync(Guid appointmentId, bool paid, CancellationToken ct)
    {
        var deposit = await online.Deposits.AsNoTracking().FirstOrDefaultAsync(d => d.AppointmentId == appointmentId, ct);
        if (deposit is not { Status: OnlineDepositStatus.Pending, ProviderReference: { } reference } || !IsTest(deposit))
        {
            return false;
        }

        if (paid)
        {
            await ConfirmPaymentAsync(deposit.Id, reference, ct);
        }
        else
        {
            await PaymentFailedAsync(deposit.Id, reference, ct);
        }

        return true;
    }

    /// <summary>Nach Abbruch oder Ablauf neu beginnen, solange der Termin noch bevorsteht.</summary>
    public async Task<bool> RetryAsync(Appointment appointment, string returnUrl, CancellationToken ct)
    {
        var deposit = await online.Deposits.FirstOrDefaultAsync(d => d.AppointmentId == appointment.Id, ct);
        if (deposit is not { Status: OnlineDepositStatus.Failed } || appointment.Status != AppointmentStatus.Booked || appointment.Start <= time.GetUtcNow())
        {
            return false;
        }

        deposit.Status = OnlineDepositStatus.Pending;
        var location = await db.Locations.AsNoTracking().Where(l => l.Id == appointment.LocationId).Select(l => l.Name).FirstAsync(ct);
        await StartAsync(deposit, appointment.Treatment.Name, location, returnUrl, ct);
        return true;
    }

    /// <summary>
    /// Nach einer rechtzeitigen Absage durch den Gast oder einer Absage durch den Betrieb: Anzahlungsbeleg stornieren und erstatten, über die Queue
    /// (<paramref name="reason"/>: Stornogrund, sonst der des Gastes). Eine noch nicht bezahlte Anzahlung wird nur abgebrochen.
    /// </summary>
    public async Task RefundAfterCancellationAsync(Guid appointmentId, CancellationToken ct, string? reason = null)
    {
        var deposit = await online.Deposits.FirstOrDefaultAsync(d => d.AppointmentId == appointmentId, ct);
        switch (deposit?.Status)
        {
            case OnlineDepositStatus.Pending or OnlineDepositStatus.Failed:
                deposit.Status = OnlineDepositStatus.Cancelled;
                deposit.PaymentUrl = null;
                await online.SaveChangesAsync(ct);
                break;
            case OnlineDepositStatus.Receipted:
                await bus.PublishAsync(new RefundOnlineDeposit(deposit.Id, reason));
                break;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, OnlineDeposit>> ForAppointmentsAsync(IReadOnlyCollection<Guid> appointmentIds, CancellationToken ct)
    {
        var found = await online.Deposits.AsNoTracking().Where(d => appointmentIds.Contains(d.AppointmentId)).ToListAsync(ct);
        var receiptIds = found.Where(d => d.ReceiptId is not null).Select(d => d.ReceiptId!.Value).ToList();
        var receipts = receiptIds.Count == 0
            ? []
            : await db.Receipts.AsNoTracking().Where(r => receiptIds.Contains(r.Id))
                .Join(db.CashRegisters.AsNoTracking(), r => r.RegisterId, c => c.Id, (r, c) => new { r.Id, c.Code, r.Number, r.IssuedAt })
                .ToDictionaryAsync(r => r.Id, r => $"{r.Code} Nr. {r.Number}, {r.IssuedAt:dd.MM.yyyy}", ct);
        return found.ToDictionary(d => d.AppointmentId, d => ToOnline(d, d.ReceiptId is { } id ? receipts.GetValueOrDefault(id) : null));
    }

    /// <summary>Anzahlung eines Termins für das Team; <c>Found</c> ist falsch, wenn es den Termin (im eigenen Spa) nicht gibt.</summary>
    public async Task<(bool Found, DepositStaffDto? Deposit)> StaffAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment is null)
        {
            return (false, null);
        }

        var deposit = await online.Deposits.AsNoTracking().FirstOrDefaultAsync(d => d.AppointmentId == appointmentId, ct);
        if (deposit is null)
        {
            return (true, null);
        }

        var booked = deposit.ReceiptId is { } receiptId
            ? await db.AppointmentDeposits.AsNoTracking().FirstOrDefaultAsync(d => d.ReceiptId == receiptId, ct)
            : null;
        return (true, ToStaff(deposit, appointment, booked, booked?.VoidedAt is null ? null : await VoidNoteAsync(booked.ReceiptId, ct)));
    }

    /// <summary>
    /// Wie der Anzahlungsbeleg storniert wurde: „Storno-Beleg K1 Nr. 12 vom 08.03.2027, zurück: Bar. Grund“. Das ist das Protokoll der Rückzahlung am Termin und nennt den Zahlungsweg
    /// (Ä53): bei „Bar“ bekommt der Gast das Geld an der Rezeption.
    /// </summary>
    private async Task<string?> VoidNoteAsync(Guid receiptId, CancellationToken ct)
    {
        var voided = await db.Receipts.AsNoTracking().Include(r => r.Payments).FirstOrDefaultAsync(r => r.VoidsReceiptId == receiptId, ct);
        if (voided is null)
        {
            return null;
        }

        var code = await db.CashRegisters.AsNoTracking().Where(c => c.Id == voided.RegisterId).Select(c => c.Code).FirstAsync(ct);
        return $"Storno-Beleg {code} Nr. {voided.Number} vom {voided.IssuedAt:dd.MM.yyyy}, zurück: {PaymentMethods.Way(voided.Payments.Select(p => p.Method))}. {voided.VoidReason}";
    }

    private bool IsTest(DepositOrder d) => _test is { } p && p.Name == d.Provider;

    private OnlineDeposit ToOnline(DepositOrder d, string? receipt) =>
        new(d.Amount, d.Currency, d.Percent, d.Status, d.Status == OnlineDepositStatus.Pending && !IsTest(d) ? d.PaymentUrl : null, IsTest(d),
            d.DeadlineHours, d.Status is OnlineDepositStatus.Pending or OnlineDepositStatus.Failed ? d.ReleaseAt : null, receipt);

    internal static DepositStaffDto ToStaff(DepositOrder d, Appointment a, AppointmentDeposit? booked, string? voidNote = null)
    {
        // Sagt der Betrieb ab (Schließzeit, ADR 0073), ist das nie eine späte Absage des Gastes: die Anzahlung bleibt nicht beim Spa.
        var late = a.Status == AppointmentStatus.Cancelled && !a.CancelledByBusiness && a.UpdatedAt > a.Start.AddHours(-d.DeadlineHours);
        var settled = booked?.SettledOnReceiptId is not null;
        var voided = booked?.VoidedAt is not null;
        var kept = d.Status == OnlineDepositStatus.Receipted && !settled && !voided && (a.Status == AppointmentStatus.NoShow || late);
        string? hint = d.Status switch
        {
            OnlineDepositStatus.Pending when d.ReleaseAt is not null && a.Status == AppointmentStatus.Booked =>
                "Noch nicht bezahlt; ohne Zahlung wird der Termin zum angezeigten Zeitpunkt automatisch freigegeben.",
            OnlineDepositStatus.Pending => "Der Gast hat die Anzahlung noch nicht bezahlt.",
            OnlineDepositStatus.Failed => "Die Zahlung wurde abgebrochen; der Gast kann sie im Kundenbereich neu beginnen.",
            OnlineDepositStatus.Paid when d.Error is not null => "Bezahlt, aber der Anzahlungsbeleg konnte nicht erstellt werden.",
            OnlineDepositStatus.Paid => "Bezahlt; der Anzahlungsbeleg wird gerade an der Kassa erstellt.",
            OnlineDepositStatus.Receipted when settled => "Beim Kassieren der Behandlung verrechnet.",
            OnlineDepositStatus.Receipted when voided => voidNote is null ? "Der Anzahlungsbeleg wurde an der Kassa storniert." : $"Der Anzahlungsbeleg wurde an der Kassa storniert. {voidNote}",
            OnlineDepositStatus.Refunded when voidNote is not null => $"Nach dem Storno des Anzahlungsbelegs zurückerstattet. {voidNote}",
            OnlineDepositStatus.Receipted when kept => "Einbehalten: nicht erschienen oder nach der Stornofrist abgesagt. Zum Erstatten „Anzahlung retournieren“ wählen (Inhaber und Manager).",
            OnlineDepositStatus.Receipted when a.CancelledByBusiness => "Vom Betrieb abgesagt; zum Erstatten die Anzahlung an der Kassa retournieren (Inhaber und Manager).",
            OnlineDepositStatus.Receipted when a.Status == AppointmentStatus.Cancelled => "Rechtzeitig storniert; die Erstattung läuft über das Storno des Anzahlungsbelegs.",
            OnlineDepositStatus.Receipted => "Bezahlt mit Anzahlungsbeleg; wird beim Kassieren der Behandlung abgezogen.",
            OnlineDepositStatus.Cancelled => "Abgebrochen, es wurde nichts bezahlt.",
            OnlineDepositStatus.Refunded => "Nach dem Storno des Anzahlungsbelegs zurückerstattet.",
            OnlineDepositStatus.Released => "Termin freigegeben, weil die Anzahlung in der Frist nicht bezahlt wurde.",
            _ => null,
        };
        return new DepositStaffDto(d.Id, d.Status, d.Amount, d.Currency, d.Percent, d.AcceptedAt, d.PaidAt, d.ReceiptId, settled,
            booked?.SettledOnReceiptId, kept, voided, hint, d.Error,
            d.Status is OnlineDepositStatus.Pending or OnlineDepositStatus.Failed && a.Status == AppointmentStatus.Booked ? d.ReleaseAt : null);
    }
}
