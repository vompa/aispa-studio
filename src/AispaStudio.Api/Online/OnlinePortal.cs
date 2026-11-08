using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>Ergebnis einer Aktion im Kundenbereich; die Endpunkte machen daraus die passende HTTP-Antwort.</summary>
internal abstract record PortalOutcome
{
    public sealed record Done(OnlineAppointment Appointment) : PortalOutcome;

    public sealed record NotFound : PortalOutcome;

    /// <summary>Fachlich nicht möglich, z. B. Stornofrist vorbei.</summary>
    public sealed record Refused(string Message) : PortalOutcome;
}

/// <summary>
/// Gemeinsame Logik für Online-Buchung, „Meine Termine“ und Links aus Mails. Termine bucht und storniert weiterhin der
/// <see cref="BookingService"/> des Kalenders; hier kommen nur die Regeln des Kundenbereichs dazu (Vorlauf, Stornofrist).
/// </summary>
internal sealed class OnlinePortal(
    AispaDbContext db,
    OnlineDbContext online,
    BookingService booking,
    AccessLinks links,
    IMessageBus bus,
    TenantContext tenants,
    TimeProvider time,
    OnlineRules rules,
    CardGuarantees guarantees,
    Deposits deposits,
    IOptions<OnlineBookingOptions> options)
{
    private OnlineBookingOptions Options => options.Value;

    /// <summary>
    /// Erster und letzter online buchbarer Kalendertag am Standort nach seinen Regeln (Vorlauf, Buchungshorizont).
    /// Ist die Online-Buchung am Standort ausgeschaltet, liegt der letzte Tag vor dem ersten.
    /// </summary>
    public async Task<(DateOnly First, DateOnly Last, DateTimeOffset Earliest)> BookableRangeAsync(Guid locationId, TimeZoneInfo zone, CancellationToken ct)
    {
        var rule = await rules.ForAsync(locationId, ct);
        var now = time.GetUtcNow();
        var earliest = now.AddMinutes(rule.MinLeadMinutes);
        var first = LocalTime.LocalDate(earliest, zone);
        return (first, rule.BookingEnabled ? LocalTime.LocalDate(now, zone).AddDays(rule.MaxDaysAhead) : first.AddDays(-1), earliest);
    }

    /// <summary>
    /// Freie Startzeiten eines Tages ab <paramref name="earliest"/>. Je Startzeit genügt eine freie Kombination; welche Person
    /// und welcher Raum, entscheidet die Buchung. <c>null</c>, wenn Standort oder Behandlung nicht passen.
    /// </summary>
    public async Task<IReadOnlyList<DateTimeOffset>?> FreeTimesAsync(
        Guid locationId, Guid treatmentId, DateOnly date, DateTimeOffset earliest, CancellationToken ct)
    {
        var context = await booking.LoadContextAsync(locationId, treatmentId, date, null, ct);
        if (context is null)
        {
            return null;
        }

        var step = TimeSpan.FromMinutes((await rules.ForAsync(locationId, ct)).SlotStepMinutes);
        return new ResourcePlanner(context).FreeSlots(date, step, null)
            .Select(s => s.Start).Where(s => s >= earliest).Distinct().Order().ToList();
    }

    public async Task<IReadOnlyList<OnlineAppointment>> ToOnlineAsync(IReadOnlyList<Appointment> appointments, CancellationToken ct)
    {
        if (appointments.Count == 0)
        {
            return [];
        }

        var locationIds = appointments.Select(a => a.LocationId).Distinct().ToList();
        var locations = await db.Locations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var customerIds = appointments.Select(a => a.CustomerId).Distinct().ToList();
        var validFrom = time.GetUtcNow().AddMonths(-Options.AnamnesisValidMonths);
        var deadlines = await rules.ForAsync(locationIds, ct);
        var withAnamnesis = (await online.AnamnesisRecords.AsNoTracking()
            .Where(r => customerIds.Contains(r.CustomerId) && r.SubmittedAt >= validFrom)
            .Select(r => r.CustomerId).Distinct().ToListAsync(ct)).ToHashSet();
        var ids = appointments.Select(a => a.Id).ToList();
        var cards = await guarantees.ForAppointmentsAsync(ids, ct);
        var paid = await deposits.ForAppointmentsAsync(ids, ct);

        return [.. appointments.Select(a => ToOnline(a, locations[a.LocationId], deadlines[a.LocationId].CancellationDeadlineHours,
            withAnamnesis.Contains(a.CustomerId), cards.GetValueOrDefault(a.Id), paid.GetValueOrDefault(a.Id)))];
    }

    public async Task<OnlineAppointment> ToOnlineAsync(Appointment appointment, CancellationToken ct) =>
        (await ToOnlineAsync([appointment], ct))[0];

    private OnlineAppointment ToOnline(Appointment a, Location location, int cancellationDeadlineHours, bool anamnesis, OnlineCardGuarantee? card,
        OnlineDeposit? deposit)
    {
        var now = time.GetUtcNow();
        var state = a.Status switch
        {
            AppointmentStatus.Cancelled => OnlineAppointmentState.Cancelled,
            AppointmentStatus.Booked when a.End > now => OnlineAppointmentState.Booked,
            _ => OnlineAppointmentState.Past,
        };
        // Mit hinterlegter Karte gilt die Frist, der der Gast zugestimmt hat, wie bei der Gebühr selbst (CardGuarantees.ToStaff).
        var savedCard = card is { Status: CardGuaranteeStatus.Saved };
        var cancellableUntil = a.Start.AddHours(-(savedCard ? card!.DeadlineHours : cancellationDeadlineHours));
        var canCancel = state == OnlineAppointmentState.Booked && now <= cancellableUntil;
        // Nach der Frist darf der Gast nur selbst absagen, wenn eine hinterlegte Karte die Gebühr deckt; sonst bleibt der Anruf.
        // Auch mit bezahlter Anzahlung: Sie bliebe beim Spa, und die Gebühr käme dazu.
        var lateFee = savedCard && card!.FeeAmount > 0 ? card.FeeAmount : (decimal?)null;
        var depositPaid = deposit is { Status: OnlineDepositStatus.Paid or OnlineDepositStatus.Receipted };
        var canCancelLate = state == OnlineAppointmentState.Booked && !canCancel && now < a.Start && lateFee is not null && !depositPaid;
        return new OnlineAppointment(
            a.Id, a.LocationId, location.Name, location.TimeZone, a.Treatment.Name, a.Start, a.End, a.Price, location.Currency,
            state, cancellableUntil, canCancel, anamnesis,
            MasterDataEndpoints.ContactOf(location), card, deposit, a.ConfirmedAt, a.Treatment.GuestHint, a.TreatmentId,
            canCancel && a.GroupId is null && a.GuestMoves < Options.MaxGuestMoves, lateFee, canCancelLate);
    }

    public Task<Appointment?> LoadAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);

    /// <summary>
    /// Nach einer erfolgreichen Buchung: Herkunft festhalten, Links anlegen, Bestätigung über die Queue verschicken. Verlangt der
    /// Standort eine Anzahlung, wird sie angelegt und die Zahlung begonnen; sonst bei einer Ausfallgebühr die Kartenhinterlegung
    /// (der Gast hat beim Buchen zugestimmt).
    /// </summary>
    public async Task<OnlineBookingConfirmation> ConfirmAsync(
        Guid appointmentId, Guid? accountId, string email, string language, CancellationToken ct)
    {
        var appointment = await LoadAppointmentAsync(appointmentId, ct)
            ?? throw new InvalidOperationException("Gebuchter Termin nicht gefunden.");
        var now = time.GetUtcNow();
        online.OnlineBookings.Add(new OnlineBooking
        {
            AppointmentId = appointment.Id,
            CustomerId = appointment.CustomerId,
            CustomerAccountId = accountId,
            CreatedAt = now,
            PrivacyAcceptedAt = now,
            PrivacyNoticeVersion = Options.PrivacyNoticeVersion,
            Language = language,
        });
        var manageToken = links.Create(AccessLinkPurpose.ManageAppointment, appointment.CustomerId, appointment.Id, appointment.End.AddDays(30));
        var anamnesisToken = links.Create(AccessLinkPurpose.Anamnesis, appointment.CustomerId, appointment.Id, appointment.End.AddDays(1));
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
        var locationRules = await rules.ForAsync(location.Id, ct);
        var deposit = await deposits.AddAsync(appointment, location.Currency, locationRules, email, language, ct);
        var card = deposit is null ? await guarantees.AddAsync(appointment, location.Currency, locationRules, email, language, ct) : null;
        await online.SaveChangesAsync(ct);
        if (deposit is not null)
        {
            await deposits.StartAsync(deposit, appointment.Treatment.Name, location.Name, Url($"termin/{manageToken}"), ct);
        }
        else if (card is not null)
        {
            await guarantees.StartAsync(card, appointment.Treatment.Name, location.Name, Url($"termin/{manageToken}"), ct);
        }

        var dto = await ToOnlineAsync(appointment, ct);
        await bus.PublishGuestMailAsync(tenants, OnlineMails.Confirmation(
            email, GuestName.Of(appointment.Customer, dto.TimeZone), dto, Url($"termin/{manageToken}"), Url($"anamnese/{anamnesisToken}"), language));
        return new OnlineBookingConfirmation(dto, manageToken, anamnesisToken);
    }

    /// <summary>
    /// Storno durch die Kundin oder den Kunden selbst: bis zur Stornofrist frei, danach nur mit hinterlegter Karte und ausdrücklicher
    /// Zustimmung zur Gebühr. Belastet wird nicht hier, sondern erst durch die Kassenaufsicht (<see cref="CardGuarantees.ChargeAsync"/>).
    /// </summary>
    public async Task<PortalOutcome> CancelAsync(Guid appointmentId, Guid customerId, bool acceptFee, CancellationToken ct)
    {
        var appointment = await LoadAppointmentAsync(appointmentId, ct);
        if (appointment is null || appointment.CustomerId != customerId)
        {
            return new PortalOutcome.NotFound();
        }

        var current = await ToOnlineAsync(appointment, ct);
        if (current.State == OnlineAppointmentState.Cancelled)
        {
            return new PortalOutcome.Done(current);
        }

        if (!current.CanCancel && !(current.CanCancelLate && acceptFee))
        {
            return new PortalOutcome.Refused(current.CanCancelLate
                ? "Nach der Stornofrist fällt eine Gebühr an. Bitte stimmen Sie ihr zu oder rufen Sie uns an."
                : "Dieser Termin kann online nicht mehr storniert werden. Bitte rufen Sie uns an.");
        }

        var reason = current.CanCancel ? "Online durch Kundin/Kunden storniert" : "Online nach der Stornofrist storniert, Gebühr bestätigt";
        if (await booking.CancelAsync(appointmentId, reason, ct) is not BookingOutcome.Success)
        {
            return new PortalOutcome.Refused("Der Termin wurde gerade geändert. Bitte laden Sie die Seite neu.");
        }

        var record = await online.OnlineBookings.FirstOrDefaultAsync(b => b.AppointmentId == appointmentId, ct);
        if (record is not null)
        {
            record.CancelledAt = time.GetUtcNow();
            await online.SaveChangesAsync(ct);
        }

        // Rechtzeitig abgesagt: eine bezahlte Anzahlung geht über Storno des Anzahlungsbelegs zurück.
        await deposits.RefundAfterCancellationAsync(appointmentId, ct);

        var cancelled = await ToOnlineAsync((await LoadAppointmentAsync(appointmentId, ct))!, ct);
        if (appointment.Customer.Email is { } email)
        {
            await bus.PublishGuestMailAsync(tenants, OnlineMails.Cancelled(email, GuestName.Of(appointment.Customer, cancelled.TimeZone), cancelled, record?.Language));
        }

        return new PortalOutcome.Done(cancelled);
    }

    /// <summary>
    /// Der Gast sagt zu, dass er kommt (Link aus Bestätigung oder Erinnerung). Nur für bevorstehende gebuchte Termine; eine zweite
    /// Zusage ändert nichts. Der Kalender bekommt den neuen Stand live.
    /// </summary>
    public async Task<PortalOutcome> ConfirmAttendanceAsync(Guid appointmentId, Guid customerId, CancellationToken ct)
    {
        var appointment = await db.Appointments.Include(a => a.Treatment).Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment is null || appointment.CustomerId != customerId)
        {
            return new PortalOutcome.NotFound();
        }

        var now = time.GetUtcNow();
        if (appointment.Status != AppointmentStatus.Booked || appointment.Start <= now)
        {
            return new PortalOutcome.Refused("Dieser Termin lässt sich nicht mehr bestätigen.");
        }

        if (appointment.ConfirmedAt is null)
        {
            // Auf Mikrosekunden wie in PostgreSQL, damit die Antwort denselben Zeitpunkt nennt wie jeder spätere Abruf.
            appointment.ConfirmedAt = now.AddTicks(-(now.Ticks % 10));
            appointment.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            await bus.PublishAsync(new AppointmentChangedMessage(BookingService.ToDto(appointment, appointment.Treatment.Name)));
        }

        return new PortalOutcome.Done(await ToOnlineAsync(appointment, ct));
    }

    /// <summary>
    /// Der Gast verschiebt selbst, innerhalb der Stornofrist und höchstens <see cref="OnlineBookingOptions.MaxGuestMoves"/>-mal. Die
    /// neue Zeit muss eine der online angebotenen freien Zeiten sein. Person, Raum und Gerät bleiben, wenn sie frei sind; Preis,
    /// Karte und Anzahlung bleiben beim Termin. Paar und Spa-Tag nur über die Rezeption, weil alle Teile zusammen wandern.
    /// </summary>
    public async Task<PortalOutcome> MoveAsync(Guid appointmentId, Guid customerId, DateTimeOffset start, CancellationToken ct)
    {
        var appointment = await LoadAppointmentAsync(appointmentId, ct);
        if (appointment is null || appointment.CustomerId != customerId)
        {
            return new PortalOutcome.NotFound();
        }

        var current = await ToOnlineAsync(appointment, ct);
        if (!current.CanMove)
        {
            return new PortalOutcome.Refused(current.CanCancel && appointment.GroupId is null && Options.MaxGuestMoves > 0
                ? "Dieser Termin wurde schon so oft verschoben, wie es online geht. Bitte rufen Sie uns an."
                : "Dieser Termin kann online nicht verschoben werden. Bitte rufen Sie uns an.");
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
        var zone = LocalTime.Zone(location.TimeZone);
        var (first, last, earliest) = await BookableRangeAsync(location.Id, zone, ct);
        var date = LocalTime.LocalDate(start, zone);
        if (date < first || date > last
            || await FreeTimesAsync(location.Id, appointment.TreatmentId, date, earliest, ct) is not { } free || !free.Contains(start))
        {
            return new PortalOutcome.Refused("Diese Zeit ist nicht mehr frei. Bitte wählen Sie eine andere.");
        }

        if (await booking.MoveAsync(appointmentId, new MoveAppointmentRequest(start, null, null, null), ct) is not BookingOutcome.Success)
        {
            return new PortalOutcome.Refused("Diese Zeit ist nicht mehr frei. Bitte wählen Sie eine andere.");
        }

        await db.Appointments.Where(a => a.Id == appointmentId).ExecuteUpdateAsync(u => u.SetProperty(a => a.GuestMoves, a => a.GuestMoves + 1), ct);
        var moved = await ToOnlineAsync((await LoadAppointmentAsync(appointmentId, ct))!, ct);
        if (appointment.Customer.Email is { } email)
        {
            var language = await online.OnlineBookings.AsNoTracking().Where(b => b.AppointmentId == appointmentId).Select(b => b.Language)
                .FirstOrDefaultAsync(ct);
            var token = links.Create(AccessLinkPurpose.ManageAppointment, appointment.CustomerId, appointment.Id, moved.End.AddDays(30));
            await online.SaveChangesAsync(ct);
            await bus.PublishGuestMailAsync(tenants, OnlineMails.Moved(email, GuestName.Of(appointment.Customer, moved.TimeZone), moved, Url($"termin/{token}"), language));
        }

        return new PortalOutcome.Done(moved);
    }

    /// <summary>Eigener Termin als .ics; jeder fremde Termin ist „nicht gefunden“.</summary>
    public async Task<Results<FileContentHttpResult, NotFound>> CalendarFileAsync(Guid appointmentId, Guid customerId, DateTimeOffset now, CancellationToken ct)
    {
        var appointment = await LoadAppointmentAsync(appointmentId, ct);
        if (appointment is null || appointment.CustomerId != customerId)
        {
            return TypedResults.NotFound();
        }

        var file = AppointmentCalendarFile.Create(await ToOnlineAsync(appointment, ct), now);
        return TypedResults.File(Encoding.UTF8.GetBytes(file), AppointmentCalendarFile.ContentType, AppointmentCalendarFile.FileName);
    }

    /// <summary>Eigene Gutscheine: Pakete des Gastes und online bestellte; an der Kasse anonym verkaufte kennt das Konto nicht.</summary>
    public async Task<IReadOnlyList<OnlineOwnedVoucher>> OwnedVouchersAsync(Guid customerId, CancellationToken ct)
    {
        var ordered = await online.VoucherOrders.AsNoTracking()
            .Where(o => o.CustomerId == customerId && o.VoucherId != null).Select(o => o.VoucherId!.Value).ToListAsync(ct);
        return await (
            from v in db.Vouchers.AsNoTracking()
            where v.CustomerId == customerId || ordered.Contains(v.Id)
            join r in db.Receipts.AsNoTracking() on v.SoldOnReceiptId equals r.Id
            join c in db.CashRegisters.AsNoTracking() on r.RegisterId equals c.Id
            join l in db.Locations.AsNoTracking() on c.LocationId equals l.Id
            join t in db.Treatments.AsNoTracking() on v.TreatmentId equals t.Id into treatments
            from t in treatments.DefaultIfEmpty()
            orderby v.CreatedAt descending
            select new OnlineOwnedVoucher(v.Code, v.Kind, v.InitialValue, v.Balance, l.Currency, v.Status, v.ValidUntil, t == null ? null : t.Name, v.Units, v.UnitsLeft))
            .ToListAsync(ct);
    }

    /// <summary>Link in den Kundenbereich (Hash-Route, damit der Code nicht in Server-Logs landet).</summary>
    /// <param name="spa">Mandant für Links, die ohne Kundin funktionieren müssen (z. B. Buchung aus der Warteliste).</param>
    public string Url(string route, Guid? spa = null) =>
        $"{Options.PublicUrl.TrimEnd('/')}/buchen/{(spa is { } id ? $"?{OnlineTenant.Query}={id}" : "")}#/{route}";
}
