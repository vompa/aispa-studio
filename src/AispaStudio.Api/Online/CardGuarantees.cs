using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>
/// Kartenhinterlegung als No-Show-Schutz: Verlangt der Standort eine Ausfallgebühr, stimmt der Gast beim Online-Buchen zu und
/// hinterlegt danach eine Karte beim Zahlungsanbieter. Belastet wird nie automatisch, sondern nur, wenn die Kassenaufsicht es
/// bei „nicht erschienen“ oder einer Absage nach der Stornofrist auslöst; die Gebühr wird dann wie jeder Umsatz an der Kasse
/// verbucht (<see cref="ChargeNoShowFeeHandler"/>).
/// </summary>
internal sealed class CardGuarantees(
    AispaDbContext db,
    OnlineDbContext online,
    IMessageBus bus,
    TimeProvider time,
    IEnumerable<IOnlinePaymentProvider> providers,
    PaymentAccounts payments)
{
    /// <summary>Die Testzahlung, falls der Server sie anbietet; welcher Anbieter für neue Hinterlegungen gilt, sagt <see cref="PaymentAccounts"/>.</summary>
    private readonly IOnlinePaymentProvider? _test = providers.LastOrDefault(p => p.IsTest);

    public abstract record ChargeOutcome
    {
        public sealed record Started(CardGuaranteeStaffDto Guarantee) : ChargeOutcome;

        public sealed record NotFound : ChargeOutcome;

        public sealed record Refused(string Message) : ChargeOutcome;
    }

    /// <summary>Ob das Spa Karten hinterlegen lassen kann: nur mit bereitem Zahlungskonto (oder Testzahlung).</summary>
    public async Task<bool> AvailableAsync(CancellationToken ct) => await payments.ActiveProviderAsync(ct) is not null;

    /// <summary>Ohne Zahlungsanbieter lässt sich keine Karte hinterlegen; dann gilt die Regel des Standorts nicht.</summary>
    public static int EffectivePercent(bool available, LocationOnlineRules rules) => available ? rules.NoShowFeePercent : 0;

    /// <summary>Prozent für eine Behandlung: eigene Prozent der Behandlung vor der Regel des Standorts.</summary>
    public static int EffectivePercent(bool available, LocationOnlineRules rules, Treatment treatment) =>
        available ? Math.Clamp(treatment.NoShowFeePercent ?? rules.NoShowFeePercent, 0, 100) : 0;

    /// <summary>
    /// Prozent für eine Buchung: wie <see cref="EffectivePercent(bool, LocationOnlineRules, Treatment)"/>, aber 0 für Stammgäste, wenn der
    /// Standort die Gebühr nur von neuen Gästen verlangt. Neu ist, wer im Spa noch keine abgeschlossene Behandlung hatte
    /// oder wiederholt nicht erschienen ist (<see cref="NoShowHistory"/>).
    /// </summary>
    public async Task<int> PercentForAsync(LocationOnlineRules rules, Treatment treatment, Guid customerId, CancellationToken ct)
    {
        var percent = EffectivePercent(await AvailableAsync(ct), rules, treatment);
        if (percent > 0 && rules.NoShowFeeNewCustomersOnly
            && await db.Appointments.AnyAsync(a => a.CustomerId == customerId && a.Status == AppointmentStatus.Completed, ct)
            && !await NoShowHistory.IsRepeatAsync(db, customerId, time.GetUtcNow(), ct))
        {
            return 0;
        }

        return percent;
    }

    public static decimal Fee(decimal price, int percent) => decimal.Round(price * percent / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Legt die Hinterlegung zur Buchung an (gespeichert wird mit der Buchung); ohne Gebühr nichts.</summary>
    public Task<CardGuarantee?> AddAsync(Appointment appointment, string currency, LocationOnlineRules rules, string email, string language, CancellationToken ct) =>
        AddAsync(appointment, currency, rules, email, language, consented: true, ct);

    /// <summary>
    /// Wie <see cref="AddAsync(Appointment, string, LocationOnlineRules, string, string, CancellationToken)"/>; ohne
    /// <paramref name="consented"/> wartet die Hinterlegung auf die Zustimmung des Gastes (Link der Rezeption) und kann bis dahin
    /// weder beginnen noch belastet werden. <see cref="Appointment.Treatment"/> muss geladen sein.
    /// </summary>
    public async Task<CardGuarantee?> AddAsync(Appointment appointment, string currency, LocationOnlineRules rules, string email, string language,
        bool consented, CancellationToken ct)
    {
        var percent = await PercentForAsync(rules, appointment.Treatment, appointment.CustomerId, ct);
        var fee = Fee(appointment.Price, percent);
        if (percent <= 0 || fee <= 0)
        {
            return null;
        }

        DateTimeOffset? releaseAt = consented && rules.CardHoldHours > 0 ? time.GetUtcNow().AddHours(rules.CardHoldHours) : null;
        var guarantee = new CardGuarantee
        {
            AppointmentId = appointment.Id,
            LocationId = appointment.LocationId,
            CustomerId = appointment.CustomerId,
            Email = email,
            Language = language,
            FeePercent = percent,
            FeeAmount = fee,
            Currency = currency,
            AcceptedAt = consented ? time.GetUtcNow() : null,
            DeadlineHours = rules.CancellationDeadlineHours,
            Status = consented ? CardGuaranteeStatus.Pending : CardGuaranteeStatus.AwaitingConsent,
            ReleaseAt = releaseAt < appointment.Start ? releaseAt : null,
            Provider = (await payments.ActiveProviderAsync(ct))!.Name,
        };
        online.CardGuarantees.Add(guarantee);
        return guarantee;
    }

    /// <summary>Beginnt das Hinterlegen. Im Testbetrieb sofort, sonst fragt die Queue den Anbieter nach seiner Seite.</summary>
    public async Task StartAsync(CardGuarantee guarantee, string treatmentName, string locationName, string returnUrl, CancellationToken ct)
    {
        guarantee.Attempts++;
        guarantee.SessionReference = null;
        guarantee.SetupUrl = null;
        var tenantId = await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == guarantee.LocationId), ct) ?? Guid.Empty;
        var setup = new CardSetupStart(tenantId, guarantee.Id, guarantee.Attempts, guarantee.Currency, $"{treatmentName}, {locationName}", guarantee.Email, returnUrl);
        if (_test is { } test && test.Name == guarantee.Provider)
        {
            guarantee.SessionReference = (await test.StartCardSetupAsync(setup, ct)).Reference;
            await online.SaveChangesAsync(ct);
            return;
        }

        await online.SaveChangesAsync(ct);
        await bus.PublishAsync(new StartCardGuarantee(setup));
    }

    public async Task<IReadOnlyDictionary<Guid, OnlineCardGuarantee>> ForAppointmentsAsync(IReadOnlyCollection<Guid> appointmentIds, CancellationToken ct)
    {
        var found = await online.CardGuarantees.AsNoTracking().Where(g => appointmentIds.Contains(g.AppointmentId)).ToListAsync(ct);
        return found.ToDictionary(g => g.AppointmentId, ToOnline);
    }

    /// <summary>Nur im Testbetrieb: Hinterlegen simulieren oder abbrechen.</summary>
    public async Task<bool> TestAsync(Guid appointmentId, bool saved, CancellationToken ct)
    {
        var guarantee = await online.CardGuarantees.AsNoTracking().FirstOrDefaultAsync(g => g.AppointmentId == appointmentId, ct);
        if (guarantee is not { Status: CardGuaranteeStatus.Pending, SessionReference: { } session } || !IsTest(guarantee))
        {
            return false;
        }

        if (saved)
        {
            await CardSavedAsync(guarantee.Id, session, "test-card", "test-customer", ct);
        }
        else
        {
            await SetupFailedAsync(guarantee.Id, session, ct);
        }

        return true;
    }

    /// <summary>Nach Abbruch oder Ablauf neu beginnen, solange der Termin noch bevorsteht.</summary>
    public async Task<bool> RetryAsync(Appointment appointment, string returnUrl, CancellationToken ct)
    {
        var guarantee = await online.CardGuarantees.FirstOrDefaultAsync(g => g.AppointmentId == appointment.Id, ct);
        if (guarantee is not { Status: CardGuaranteeStatus.Failed } || appointment.Status != AppointmentStatus.Booked || appointment.Start <= time.GetUtcNow())
        {
            return false;
        }

        guarantee.Status = CardGuaranteeStatus.Pending;
        var location = await db.Locations.AsNoTracking().Where(l => l.Id == appointment.LocationId).Select(l => l.Name).FirstAsync(ct);
        await StartAsync(guarantee, appointment.Treatment.Name, location, returnUrl, ct);
        return true;
    }

    /// <summary>
    /// Der Gast stimmt der Ausfallgebühr zu, die die Rezeption per Link angefordert hat; danach beginnt das Hinterlegen wie bei
    /// einer Online-Buchung. Nur solange der Termin bevorsteht.
    /// </summary>
    public async Task<bool> AcceptAsync(Appointment appointment, string returnUrl, CancellationToken ct)
    {
        var guarantee = await online.CardGuarantees.FirstOrDefaultAsync(g => g.AppointmentId == appointment.Id, ct);
        if (guarantee is not { Status: CardGuaranteeStatus.AwaitingConsent } || appointment.Status != AppointmentStatus.Booked
            || appointment.Start <= time.GetUtcNow())
        {
            return false;
        }

        guarantee.AcceptedAt = time.GetUtcNow();
        guarantee.Status = CardGuaranteeStatus.Pending;
        var location = await db.Locations.AsNoTracking().Where(l => l.Id == appointment.LocationId).Select(l => l.Name).FirstAsync(ct);
        await StartAsync(guarantee, appointment.Treatment.Name, location, returnUrl, ct);
        return true;
    }

    /// <summary>Der Anbieter meldet die hinterlegte Karte. Idempotent; zählt nur für die zuletzt begonnene Sitzung.</summary>
    public Task CardSavedAsync(Guid guaranteeId, string session, string? cardReference, string? providerCustomer, CancellationToken ct) =>
        online.CardGuarantees
            .Where(g => g.Id == guaranteeId && g.Status == CardGuaranteeStatus.Pending && g.SessionReference == session)
            .ExecuteUpdateAsync(u => u
                .SetProperty(g => g.Status, CardGuaranteeStatus.Saved)
                .SetProperty(g => g.SavedAt, time.GetUtcNow())
                .SetProperty(g => g.CardReference, cardReference)
                .SetProperty(g => g.ProviderCustomer, providerCustomer)
                .SetProperty(g => g.SetupUrl, (string?)null), ct);

    public Task SetupFailedAsync(Guid guaranteeId, string session, CancellationToken ct) =>
        online.CardGuarantees
            .Where(g => g.Id == guaranteeId && g.Status == CardGuaranteeStatus.Pending && g.SessionReference == session)
            .ExecuteUpdateAsync(u => u.SetProperty(g => g.Status, CardGuaranteeStatus.Failed).SetProperty(g => g.SetupUrl, (string?)null), ct);

    /// <summary>Hinterlegung eines Termins für das Team; <c>null</c>, wenn es den Termin (im eigenen Spa) nicht gibt.</summary>
    public async Task<(bool Found, CardGuaranteeStaffDto? Guarantee)> StaffAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment is null)
        {
            return (false, null);
        }

        var guarantee = await online.CardGuarantees.AsNoTracking().FirstOrDefaultAsync(g => g.AppointmentId == appointmentId, ct);
        return (true, guarantee is null ? null : ToStaff(guarantee, appointment));
    }

    /// <summary>
    /// Die Kassenaufsicht löst die Ausfallgebühr aus. Belastet und verbucht wird im Hintergrund; vorher wird geprüft, dass die Gebühr
    /// fällig ist und eine Kasse sie verbuchen kann, damit nie Geld ohne Beleg eingezogen wird.
    /// </summary>
    public async Task<ChargeOutcome> ChargeAsync(Guid appointmentId, string userId, CancellationToken ct)
    {
        var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        var guarantee = appointment is null ? null : await online.CardGuarantees.FirstOrDefaultAsync(g => g.AppointmentId == appointmentId, ct);
        if (appointment is null || guarantee is null)
        {
            return new ChargeOutcome.NotFound();
        }

        if (ToStaff(guarantee, appointment) is { CanCharge: false } blocked)
        {
            return new ChargeOutcome.Refused(blocked.Hint ?? "Die Ausfallgebühr kann nicht belastet werden.");
        }

        if (!await db.CashRegisters.AnyAsync(r => r.LocationId == appointment.LocationId && r.IsActive, ct))
        {
            return new ChargeOutcome.Refused("An diesem Standort gibt es keine aktive Kasse, die die Gebühr verbuchen kann.");
        }

        guarantee.Status = CardGuaranteeStatus.Charging;
        guarantee.ChargedBy = userId;
        guarantee.Error = null;
        await online.SaveChangesAsync(ct);
        await bus.PublishAsync(new ChargeNoShowFee(guarantee.Id));
        return new ChargeOutcome.Started(ToStaff(guarantee, appointment));
    }

    private bool IsTest(CardGuarantee g) => _test is { } p && p.Name == g.Provider;

    private OnlineCardGuarantee ToOnline(CardGuarantee g) =>
        new(g.FeeAmount, g.Currency, g.FeePercent, g.Status, g.Status == CardGuaranteeStatus.Pending && !IsTest(g) ? g.SetupUrl : null, IsTest(g),
            g.DeadlineHours, g.Status is CardGuaranteeStatus.Pending or CardGuaranteeStatus.Failed ? g.ReleaseAt : null);

    internal static CardGuaranteeStaffDto ToStaff(CardGuarantee g, Appointment a)
    {
        // Sagt der Betrieb ab (Schließzeit, ADR 0073), ist das nie eine späte Absage des Gastes.
        var late = a.Status == AppointmentStatus.Cancelled && !a.CancelledByBusiness && a.UpdatedAt > a.Start.AddHours(-g.DeadlineHours);
        var removed = g.CardRemovedAt is not null;
        string? hint = g.Status switch
        {
            CardGuaranteeStatus.Saved or CardGuaranteeStatus.ChargeFailed when removed =>
                "Die Frist ist abgelaufen; die Karte wurde beim Zahlungsanbieter gelöscht und lässt sich nicht mehr belasten.",
            CardGuaranteeStatus.AwaitingConsent => "Link verschickt; der Gast hat der Ausfallgebühr noch nicht zugestimmt.",
            CardGuaranteeStatus.Pending or CardGuaranteeStatus.Failed when g.ReleaseAt is not null && a.Status == AppointmentStatus.Booked =>
                "Noch keine Karte hinterlegt; ohne Karte wird der Termin zum angezeigten Zeitpunkt automatisch freigegeben.",
            CardGuaranteeStatus.Pending => "Der Gast hat die Karte noch nicht hinterlegt.",
            CardGuaranteeStatus.Failed => "Das Hinterlegen der Karte wurde abgebrochen; es gibt keine Karte.",
            CardGuaranteeStatus.Charging => "Die Gebühr wird gerade belastet.",
            CardGuaranteeStatus.Charged => null,
            CardGuaranteeStatus.Refunded => "Nach dem Storno an der Kassa zurückerstattet.",
            CardGuaranteeStatus.Released => "Termin freigegeben, weil in der Frist keine Karte hinterlegt wurde.",
            _ when a.Status == AppointmentStatus.NoShow || late => null,
            _ when a.CancelledByBusiness => "Vom Betrieb abgesagt: keine Ausfallgebühr.",
            _ when a.Status == AppointmentStatus.Cancelled => "Rechtzeitig storniert: keine Ausfallgebühr.",
            _ => "Fällig erst bei „Nicht erschienen“ oder einer Absage nach der Stornofrist.",
        };
        var canCharge = g.Status is CardGuaranteeStatus.Saved or CardGuaranteeStatus.ChargeFailed && !removed
            && (a.Status == AppointmentStatus.NoShow || late);
        return new CardGuaranteeStaffDto(g.Id, g.Status, g.FeeAmount, g.Currency, g.FeePercent, g.AcceptedAt, g.SavedAt, canCharge,
            hint, g.ChargedAt, g.ReceiptId, g.Error, g.CardRemovedAt,
            g.Status is CardGuaranteeStatus.Pending or CardGuaranteeStatus.Failed && a.Status == AppointmentStatus.Booked ? g.ReleaseAt : null);
    }
}
