using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Staff;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wolverine;

namespace AispaStudio.Api.Scheduling;

/// <summary>Ergebnis einer Buchungsaktion; die Endpunkte machen daraus die passende HTTP-Antwort.</summary>
internal abstract record BookingOutcome
{
    public sealed record Success(AppointmentDto Appointment) : BookingOutcome;

    public sealed record NotFound(string What) : BookingOutcome;

    public sealed record Invalid(string Field, string Message) : BookingOutcome;

    public sealed record Rejected(BookingRejected Reason) : BookingOutcome;

    public sealed record GroupBooked(GroupBookingResult Result) : BookingOutcome;

    public sealed record GroupSlots(List<GroupSlot> Slots) : BookingOutcome;

    public sealed record AreaVisitBooked(AreaVisitDto Visit) : BookingOutcome;

    public sealed record SeriesPreviewed(List<SeriesOccurrence> Occurrences) : BookingOutcome;

    public sealed record SeriesChanged(SeriesResult Result) : BookingOutcome;
}

/// <summary>
/// Bucht, verschiebt und storniert Termine. Prüft vorab mit dem <see cref="ResourcePlanner"/> (für verständliche Meldungen);
/// die Ausschlussbedingung in PostgreSQL ist die letzte Sicherung gegen gleichzeitige Doppelbuchungen.
/// </summary>
internal sealed partial class BookingService(AispaDbContext db, RosterService roster, TimeProvider time, IMessageBus bus, OpeningCache openingCache)
{
    private const string ExclusionViolation = "23P01";

    private const string StaleMessage = "Der Termin wurde inzwischen an einem anderen Arbeitsplatz geändert. Bitte den aktuellen Stand prüfen.";

    private static readonly BookingRejected TakenMeanwhile = new(
        "Diese Zeit wurde gerade anderweitig vergeben. Bitte eine andere Zeit wählen.", []);

    /// <param name="house">Die Rezeption trägt ein: an einem Schließtag oder Tag ohne Dienst plant sie trotzdem (ADR 0073, Nachtrag R03).</param>
    public Task<PlanningContext?> LoadContextAsync(
        Guid locationId, Guid treatmentId, DateOnly date, Guid? ignoreAppointmentId, CancellationToken ct, bool house = false) =>
        LoadContextIgnoringAsync(locationId, treatmentId, date, ignoreAppointmentId is { } id ? [id] : [], ct, house);

    /// <summary>Wie oben; die Belegungen der Termine in <paramref name="ignoreAppointmentIds"/> zählen als frei (Verschieben einer ganzen Buchung).</summary>
    public async Task<PlanningContext?> LoadContextIgnoringAsync(
        Guid locationId, Guid treatmentId, DateOnly date, IReadOnlyCollection<Guid> ignoreAppointmentIds, CancellationToken ct, bool house = false)
    {
        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours)
            .FirstOrDefaultAsync(l => l.Id == locationId, ct);
        var treatment = await db.Treatments.AsNoTracking()
            .Include(t => t.RequiredQualifications).Include(t => t.Rooms).Include(t => t.Equipment).Include(t => t.LocationPrices).Include(t => t.PriceRules)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == treatmentId && t.IsActive, ct);
        if (location is null || treatment is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var (rooms, equipment) = ResourcesAt(treatment, locationId);
        var shifts = await roster.ShiftsAsync(locationId, date, zone, ct);
        var working = shifts.Select(s => s.EmployeeId).Distinct().ToList();
        // Hat am Tag niemand Dienst, wählt die Rezeption aus allen Personen des Standorts (Tag ohne Dienst).
        var employees = await (house && working.Count == 0 ? roster.EmployeesAt(locationId, date) : db.Employees.Where(e => working.Contains(e.Id)))
            .AsNoTracking()
            .Include(e => e.Qualifications)
            .Include(e => e.WorkingHours)
            .AsSplitQuery()
            .ToListAsync(ct);
        if (house && working.Count == 0)
        {
            // Wer an dem Tag krank, im Urlaub oder sonst abwesend eingeplant ist, wird nicht vorgeschlagen.
            var plans = await roster.PlansAsync(employees, date, date, ct);
            employees = [.. employees.Where(e => plans[(e.Id, date)].Entries.All(x => x.Kind == RosterKind.Work))];
        }

        // Belegungen des ganzen Tages (mit Rand für Vor- und Nachbereitung), auch an anderen Standorten.
        var (from, until) = BookingWindow(date, zone);
        var resourceIds = employees.Select(e => e.Id).Concat(rooms.Select(r => r.Id)).Concat(equipment.Select(e => e.Id)).ToList();
        var bookings = await db.ResourceBookings.AsNoTracking()
            .Where(b => resourceIds.Contains(b.ResourceId) && b.From < until && from < b.Until)
            .Where(b => b.AppointmentId == null || !ignoreAppointmentIds.Contains(b.AppointmentId.Value))
            .ToListAsync(ct);

        // Kürzeste Behandlung des Standorts (mit Vor- und Nachbereitung): Lücken darunter bleiben ungenutzt.
        var shortest = await db.Treatments.AsNoTracking()
            .Where(t => t.IsActive && t.Rooms.Any(r => r.LocationId == locationId && r.IsActive))
            .Select(t => (int?)(t.DurationMinutes + t.PreparationMinutes + t.FollowUpMinutes))
            .MinAsync(ct);

        // Schließzeiten des Standorts (ADR 0073): in ihnen ist nicht geöffnet.
        var closed = await ClosureService.SpansAsync(db, locationId, from, until, zone, ct);
        // Raumsperren (ADR 0076): ein gesperrter Raum wird in der Zeit nicht gewählt.
        var closedRooms = await RoomClosureService.SpansAsync(db, locationId, from, until, zone, ct);
        return new PlanningContext(zone, location.OpeningHours, treatment, employees, rooms, equipment, bookings, locationId, shifts, shortest ?? 30, Closed: closed, ClosedRooms: closedRooms, House: house);
    }

    /// <summary>Räume und Geräte, die die Behandlung am Standort nutzen kann, nach Name geordnet.</summary>
    internal static (List<Room> Rooms, List<Equipment> Equipment) ResourcesAt(Treatment treatment, Guid locationId) =>
        ([.. treatment.Rooms.Where(r => r.LocationId == locationId && r.IsActive).OrderBy(r => r.Name)],
         [.. treatment.Equipment.Where(e => e.LocationId == locationId && e.IsActive).OrderBy(e => e.Name)]);

    /// <summary>Zeitraum, dessen Belegungen der Planer für einen Tag kennen muss: der ganze Tag mit Rand für Vor- und Nachbereitung.</summary>
    internal static (DateTimeOffset From, DateTimeOffset Until) BookingWindow(DateOnly date, TimeZoneInfo zone)
    {
        var (dayFrom, dayUntil) = LocalTime.Day(date, zone);
        return (dayFrom.AddHours(-3), dayUntil.AddHours(3));
    }

    /// <summary>Ausdrücklich gewählte Person, die für diesen Gast „Nicht bei mir“ gesetzt hat: abgelehnt mit Grund.</summary>
    internal static BookingOutcome.Rejected? NotWithMe(CustomerRules rules, PlanningContext context, Guid? employeeId) =>
        employeeId is { } id && rules.ExcludedEmployees.Contains(id)
            ? new BookingOutcome.Rejected(new BookingRejected("Diese Person bekommt den Gast nicht mehr vorgeschlagen („Nicht bei mir“).",
                [new BookingConflict(ResourceKind.Employee, id, context.Employees.FirstOrDefault(e => e.Id == id)?.DisplayName ?? "Person",
                    "hat für diesen Gast „Nicht bei mir“ gesetzt")]))
            : null;

    /// <param name="source">Wer bucht; nur der Server setzt die Quelle, Aufrufe aus dem Team buchen immer als Rezeption.</param>
    /// <param name="platform">Angaben einer Buchungsplattform (Treatwell): wie bezahlt wird und ob die Buchung dort noch anzunehmen ist.</param>
    public async Task<BookingOutcome> BookAsync(AppointmentRequest request, CancellationToken ct, Guid? seriesId = null,
        AppointmentSource source = AppointmentSource.Reception, PlatformBooking? platform = null)
    {
        // Wiederholte Buchung mit derselben Id (z. B. nach einem Verbindungsabbruch): den bestehenden Termin zurückgeben.
        if (request.ClientId is { } clientId
            && await LoadAsync(clientId, ct) is { } existing)
        {
            return existing.CustomerId == request.CustomerId && existing.LocationId == request.LocationId
                ? new BookingOutcome.Success(ToDto(existing, existing.Treatment.Name))
                : new BookingOutcome.Invalid(nameof(request.ClientId), "Diese Id gehört schon zu einem anderen Termin.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (customer is null)
        {
            return new BookingOutcome.NotFound("Kunde");
        }

        if (customer.DeletedAt is not null)
        {
            return new BookingOutcome.Invalid(nameof(request.CustomerId), CustomerAnonymizer.NotSelectableMessage);
        }

        var start = request.Start.ToUniversalTime();
        var zoneId = await db.Locations.Where(l => l.Id == request.LocationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null)
        {
            return new BookingOutcome.NotFound("Standort");
        }

        var zone = LocalTime.Zone(zoneId);
        var house = source == AppointmentSource.Reception;
        // „Trotzdem buchen“ gilt nur für die Rezeption (Slice 85); Gäste, KI und Plattformen bauen ihre Anfrage selbst und können es nicht setzen.
        var confirmed = house && request.OutsideHoursConfirmed;
        if (!confirmed && await ClosedBeforePlanningAsync(request, start, zone, house, ct) is { } closed)
        {
            return new BookingOutcome.Rejected(new BookingRejected(closed, [], OutsideHours: house));
        }

        var context = await LoadContextAsync(request.LocationId, request.TreatmentId, LocalTime.LocalDate(start, zone), null, ct, house: house);
        if (context is null)
        {
            return new BookingOutcome.NotFound("Behandlung");
        }

        var rules = await CustomerRules.LoadAsync(db, customer.Id, ct);
        if (NotWithMe(rules, context, request.EmployeeId) is { } excluded)
        {
            return excluded;
        }

        context = rules.Apply(context, request.EmployeeId) with { OutsideHours = confirmed };
        var result = new ResourcePlanner(context).Plan(start, request.EmployeeId, request.RoomId, request.EquipmentId);
        if (result.Plan is not { } plan)
        {
            return new BookingOutcome.Rejected(new BookingRejected(result.Message!, result.Conflicts, result.OutsideHours));
        }

        // Sonderregelung (R05 Slice 111, E10): warnt nur die Rezeption, sperrt nie; Online, KI und Serien bleiben ohne Prüfung.
        if (house && seriesId is null && !request.RulesConfirmed
            && await StaffRuleCheck.AppointmentWarningAsync(db, plan.Employee, plan.Times.Start, plan.Times.End, zone, null, ct) is { } ruleWarning)
        {
            return new BookingOutcome.Rejected(new BookingRejected(ruleWarning, [], StaffRule: true));
        }

        var now = time.GetUtcNow();
        var appointment = NewAppointment(request, customer, context, plan, now, seriesId, source, platform);
        db.Appointments.Add(appointment);

        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(TakenMeanwhile);
        }

        return WithNotice(await PublishAsync(appointment, context.Treatment.Name), result.Notice);
    }

    /// <summary>
    /// Schnelle Ablehnung vor dem Planungskontext (Slice 78, Ä84): liegt die Zeit außerhalb der Öffnungszeit oder in einer Schließzeit, dieselbe Meldung wie der Raumplaner,
    /// aus dem <see cref="OpeningCache"/> und mit derselben Regel (<see cref="OpeningRules"/>); sonst <c>null</c>, dann entscheidet der Planer mit frischen Daten.
    /// Ob jemand Dienst hat, spielt für die Ablehnung keine Rolle: ein Tag ohne Dienst macht nur eine offene Zeit zum Schließtag.
    /// </summary>
    private async Task<string?> ClosedBeforePlanningAsync(AppointmentRequest request, DateTimeOffset start, TimeZoneInfo zone, bool house, CancellationToken ct)
    {
        var duration = await db.Treatments.AsNoTracking().Where(t => t.Id == request.TreatmentId && t.IsActive).Select(t => (int?)t.DurationMinutes).FirstOrDefaultAsync(ct);
        if (duration is not { } minutes)
        {
            return null;
        }

        var date = LocalTime.LocalDate(start, zone);
        var week = await openingCache.WeekAsync(db, request.LocationId, date, zone, ct);
        var windows = ResourcePlanner.OpeningWindows(week.OpeningHours, zone, date, week.Closed).Select(w => new TimeRange(w.Start, w.End)).ToList();
        var end = start.AddMinutes(minutes);
        return OpeningRules.Check(windows, anyShift: true, house, start, end) == OpeningState.Closed
            ? OpeningRules.ClosedMessage(ResourcePlanner.ClosedRanges(week.Closed), start, end)
            : null;
    }

    /// <summary>Hängt den Hinweis (Schließtag) an die Antwort auf das Buchen oder Verschieben; er blockiert nichts und wird nicht gespeichert.</summary>
    private static BookingOutcome WithNotice(BookingOutcome outcome, string? notice) =>
        notice is not null && outcome is BookingOutcome.Success success
            ? new BookingOutcome.Success(success.Appointment with { Warnings = [notice] })
            : outcome;

    /// <summary>Plant mit Vor- und Nachbereitung und Einwirkzeit des Termins statt mit denen der Behandlung (Schritt 12 und 13).</summary>
    internal static PlanningContext WithTimesOf(Appointment appointment, PlanningContext context) =>
        context with
        {
            PreparationMinutes = appointment.PreparationMinutes,
            FollowUpMinutes = appointment.FollowUpMinutes,
            HandsOnMinutes = appointment.HandsOnMinutes,
            ProcessingMinutes = appointment.ProcessingMinutes,
        };

    /// <param name="house">Die Rezeption verschiebt im Kalender: an einem Schließtag trägt sie trotzdem ein; Gäste, Plattformen und der Ausfall-Assistent planen streng.</param>
    /// <param name="toLocationId">Anderer Standort desselben Spas (nur Ausfall-Assistent); sonst bleibt der Termin, wo er ist.</param>
    /// <param name="restore">Nimmt eine Stornierung zurück (Ä95): derselbe Weg wie das Verschieben, aber für einen stornierten Termin, der danach wieder „Gebucht“ ist.</param>
    /// <param name="check">Nur prüfen, ob die Zeit geht: die Antwort ist der unveränderte Termin oder die Ablehnung, gespeichert wird nichts.</param>
    public async Task<BookingOutcome> MoveAsync(Guid id, MoveAppointmentRequest request, CancellationToken ct, Guid? toLocationId = null, bool restore = false, bool house = false,
        bool check = false)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (StaleOf(appointment, request.ExpectedVersion) is { } stale)
        {
            return stale;
        }

        if (restore && appointment.Status != AppointmentStatus.Cancelled)
        {
            return new BookingOutcome.Invalid(nameof(appointment.Status), "Nur ein stornierter Termin lässt sich zurücknehmen.");
        }

        if (!restore && appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed or AppointmentStatus.NoShow)
        {
            return new BookingOutcome.Invalid(nameof(appointment.Status), "Abgeschlossene oder stornierte Termine können nicht verschoben werden.");
        }

        var locationId = toLocationId ?? appointment.LocationId;
        if (locationId != appointment.LocationId && (appointment.Status != AppointmentStatus.Booked || appointment.GroupId is not null))
        {
            return new BookingOutcome.Invalid(nameof(toLocationId),
                "An einen anderen Standort lassen sich nur einzeln gebuchte Termine verlegen, die noch nicht eingecheckt sind.");
        }

        var start = request.Start.ToUniversalTime();
        var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null)
        {
            return new BookingOutcome.NotFound("Standort");
        }

        var context = await LoadContextAsync(
            locationId, appointment.TreatmentId, LocalTime.LocalDate(start, LocalTime.Zone(zoneId)), appointment.Id, ct, house);
        if (context is null)
        {
            return new BookingOutcome.NotFound("Behandlung");
        }

        var rules = await CustomerRules.LoadAsync(db, appointment.CustomerId, ct);
        if (NotWithMe(rules, context, request.EmployeeId) is { } excluded)
        {
            return excluded;
        }

        // Dauer: die bisherige (ein Termin mit eigener Dauer behält sie).
        var full = context.Treatment.DurationMinutes;
        var duration = (int)(appointment.End - appointment.Start).TotalMinutes;
        context = WithTimesOf(appointment, rules.Apply(context, request.EmployeeId ?? appointment.EmployeeId));

        // Bisherige Ressourcen behalten, wenn sie zur neuen Zeit frei sind; sonst passende andere wählen.
        var planner = new ResourcePlanner(context with { DurationMinutes = duration == full ? null : duration });
        var result = planner.Plan(start,
            request.EmployeeId ?? appointment.EmployeeId,
            request.RoomId ?? appointment.RoomId,
            request.EquipmentId ?? appointment.EquipmentId);
        // Zurücknehmen zur selben Zeit: Person, Raum und Gerät müssen die alten sein, sonst meldet der Server den Konflikt und das Team verschiebt bewusst.
        var sameSpot = restore && start == appointment.Start;
        if (result.Plan is null && !sameSpot && (request.EmployeeId is null || request.RoomId is null || request.EquipmentId is null))
        {
            var fallback = planner.Plan(start, request.EmployeeId, request.RoomId, request.EquipmentId);
            if (fallback.Plan is not null)
            {
                result = fallback;
            }
        }

        if (result.Plan is not { } plan)
        {
            return new BookingOutcome.Rejected(new BookingRejected(result.Message!, result.Conflicts));
        }

        if (check)
        {
            return new BookingOutcome.Success(ToDto(appointment, appointment.Treatment.Name));
        }

        // Verschieben fragt nicht nach: Der Termin zieht um, die Antwort nennt die Sonderregelung (R05 Slice 111).
        var ruleWarning = house
            ? await StaffRuleCheck.AppointmentWarningAsync(db, plan.Employee, plan.Times.Start, plan.Times.End, LocalTime.Zone(zoneId), appointment.Id, ct)
            : null;
        var previousStart = appointment.Start;
        var previousLocation = appointment.LocationId;
        db.ResourceBookings.RemoveRange(appointment.Bookings);
        appointment.LocationId = locationId;
        Apply(appointment, plan, time.GetUtcNow());
        if (restore)
        {
            appointment.Status = AppointmentStatus.Booked;
            appointment.StartedAt = null;
            appointment.CancelReason = null;
            appointment.CancelledByBusiness = false;
        }

        if (appointment.Start != previousStart)
        {
            appointment.LateMinutes = null;
        }

        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(TakenMeanwhile);
        }

        var moved = WithNotice(await PublishAsync(appointment, appointment.Treatment.Name, previousStart == appointment.Start ? null : previousStart,
            previousLocation == appointment.LocationId ? null : previousLocation), result.Notice);
        return ruleWarning is not null && moved is BookingOutcome.Success success
            ? new BookingOutcome.Success(success.Appointment with { Warnings = [.. success.Appointment.Warnings ?? [], ruleWarning] })
            : moved;
    }

    /// <summary>Stornierung zurücknehmen (Ä95): ohne <paramref name="start"/> zur selben Zeit mit denselben Ressourcen, sonst zum neuen Beginn mit freier Wahl wie beim Verschieben.</summary>
    public Task<BookingOutcome> RestoreAsync(Guid id, DateTimeOffset? start, CancellationToken ct, uint? expectedVersion = null) =>
        RestoreAsync(id, start, expectedVersion, check: false, ct);

    /// <summary>Ob die Stornierung zur alten Zeit zurückgenommen werden könnte: die Ablehnung mit den Belegungen, sonst <c>null</c>; speichert nichts (Verlauf, Aufgabe E).</summary>
    public async Task<BookingRejected?> RestoreConflictAsync(Guid id, CancellationToken ct) =>
        await RestoreAsync(id, null, null, check: true, ct) is BookingOutcome.Rejected rejected ? rejected.Reason : null;

    private async Task<BookingOutcome> RestoreAsync(Guid id, DateTimeOffset? start, uint? expectedVersion, bool check, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (StaleOf(appointment, expectedVersion) is { } stale)
        {
            return stale;
        }

        var move = start is { } at
            ? new MoveAppointmentRequest(at, null, null, null)
            : new MoveAppointmentRequest(appointment.Start, appointment.EmployeeId, appointment.RoomId, appointment.EquipmentId);
        return await MoveAsync(id, move, ct, restore: true, check: check);
    }

    /// <param name="byBusiness">Der Betrieb sagt ab (Schließzeit, ADR 0073): nie eine Ausfallgebühr, die Anzahlung ist zu erstatten.</param>
    public async Task<BookingOutcome> CancelAsync(Guid id, string? reason, CancellationToken ct, bool byBusiness = false, uint? expectedVersion = null)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (StaleOf(appointment, expectedVersion) is { } stale)
        {
            return stale;
        }

        if (appointment.Status != AppointmentStatus.Cancelled)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelReason = reason;
            appointment.CancelledByBusiness = byBusiness;
            appointment.UpdatedAt = time.GetUtcNow();
            db.ResourceBookings.RemoveRange(appointment.Bookings);
            if (!await TrySaveAsync(ct))
            {
                return new BookingOutcome.Rejected(TakenMeanwhile);
            }

            return await PublishAsync(appointment, appointment.Treatment.Name);
        }

        return new BookingOutcome.Success(ToDto(appointment, appointment.Treatment.Name));
    }

    public async Task<BookingOutcome> ChangeStatusAsync(Guid id, AppointmentStatus status, CancellationToken ct, uint? expectedVersion = null)
    {
        if (status == AppointmentStatus.Cancelled)
        {
            return new BookingOutcome.Invalid(nameof(status), "Zum Stornieren den Endpunkt „cancel“ verwenden.");
        }

        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (StaleOf(appointment, expectedVersion) is { } stale)
        {
            return stale;
        }

        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            return new BookingOutcome.Invalid(nameof(status), "Der Termin ist storniert.");
        }

        if (status == AppointmentStatus.Booked && await UndoCheckInBlockAsync(appointment, ct) is { } block)
        {
            return new BookingOutcome.Invalid(nameof(status), block);
        }

        appointment.Status = status;
        if (status == AppointmentStatus.Booked)
        {
            appointment.StartedAt = null;
        }

        appointment.UpdatedAt = time.GetUtcNow();
        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
        }

        return await PublishAsync(appointment, appointment.Treatment.Name);
    }

    /// <summary>Handy-App: Behandlung begonnen. Nur am Tag des Termins; ein noch nicht eingecheckter Gast gilt damit als eingecheckt. Doppeltes Melden ändert nichts.</summary>
    public async Task<BookingOutcome> BeginAsync(Guid id, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        if (appointment.Status is not (AppointmentStatus.Booked or AppointmentStatus.CheckedIn))
        {
            return new BookingOutcome.Invalid("status", "Dieser Termin lässt sich nicht mehr beginnen.");
        }

        if (appointment is { StartedAt: not null, Status: AppointmentStatus.CheckedIn })
        {
            return new BookingOutcome.Success(ToDto(appointment, appointment.Treatment.Name));
        }

        var now = time.GetUtcNow();
        var zone = LocalTime.Zone(await db.Locations.Where(l => l.Id == appointment.LocationId).Select(l => l.TimeZone).FirstAsync(ct));
        if (LocalTime.LocalDate(now, zone) != LocalTime.LocalDate(appointment.Start, zone))
        {
            return new BookingOutcome.Invalid("status", "Begonnen lässt sich nur am Tag des Termins melden.");
        }

        appointment.Status = AppointmentStatus.CheckedIn;
        appointment.StartedAt = now;
        appointment.UpdatedAt = now;
        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Termin wurde gerade von jemand anderem geändert.", []));
        }

        return await PublishAsync(appointment, appointment.Treatment.Name);
    }

    /// <summary>Handy-App: Behandlung beendet. Nur nach „begonnen“; danach wie „abgeschlossen“ an der Rezeption.</summary>
    public async Task<BookingOutcome> FinishAsync(Guid id, CancellationToken ct)
    {
        var running = await db.Appointments.AsNoTracking().Where(a => a.Id == id).Select(a => new { a.Status, a.StartedAt }).FirstOrDefaultAsync(ct);
        if (running is null)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        return running is { Status: AppointmentStatus.CheckedIn, StartedAt: not null }
            ? await ChangeStatusAsync(id, AppointmentStatus.Completed, ct)
            : new BookingOutcome.Invalid("status", "Bitte zuerst „begonnen“ melden.");
    }

    /// <summary>
    /// „Einchecken zurücknehmen“ (Ä97) geht nur aus „Eingecheckt“ und solange nicht kassiert ist: eine Kassenzeile zum Termin, auch auf einer offenen Rechnung,
    /// gilt als kassiert. Danach führt der Weg über den Beleg an der Kassa (ADR 0010).
    /// </summary>
    private async Task<string?> UndoCheckInBlockAsync(Appointment appointment, CancellationToken ct)
    {
        if (appointment.Status != AppointmentStatus.CheckedIn)
        {
            return "Nur ein eingecheckter Termin lässt sich auf „Gebucht“ zurücksetzen.";
        }

        return await db.BillLines.AnyAsync(l => l.AppointmentId == appointment.Id && l.Kind == BillLineKind.Appointment, ct)
            ? "Der Termin ist schon kassiert. Das Einchecken lässt sich nur noch über den Beleg an der Kassa zurücknehmen."
            : null;
    }

    /// <summary>Sperrzeit (Urlaub, Wartung) anlegen. Überschneidet sie sich mit Terminen, wird sie abgelehnt.</summary>
    public async Task<(ResourceBooking? Block, BookingRejected? Rejected)> AddBlockAsync(BlockInput input, CancellationToken ct)
    {
        var block = new ResourceBooking
        {
            Kind = input.Kind,
            ResourceId = input.ResourceId,
            From = input.From.ToUniversalTime(),
            Until = input.Until.ToUniversalTime(),
            Reason = input.Reason,
        };

        if (await BlockOverlapAsync(block, ct) is { } rejected)
        {
            return (null, rejected);
        }

        db.ResourceBookings.Add(block);
        return await TrySaveAsync(ct) ? (block, null) : (null, TakenMeanwhile);
    }

    /// <summary>
    /// Beginn und Ende einer Sperrzeit oder Pause ändern (Rand im Kalender gezogen, Slice 71). Geprüft wird wie beim Anlegen, nur nicht gegen die Sperrzeit selbst.
    /// </summary>
    /// <returns>Die geänderte Sperrzeit, die Ablehnung bei einer Überschneidung, oder beides leer, wenn es die Sperrzeit nicht gibt.</returns>
    public async Task<(ResourceBooking? Block, BookingRejected? Rejected)> ChangeBlockAsync(Guid blockId, BlockChange change, CancellationToken ct)
    {
        var stored = await db.ResourceBookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == blockId && b.AppointmentId == null && b.AreaVisitId == null, ct);
        if (stored is null)
        {
            return (null, null);
        }

        var block = new ResourceBooking
        {
            Id = stored.Id,
            Kind = stored.Kind,
            ResourceId = stored.ResourceId,
            Slot = stored.Slot,
            From = change.From.ToUniversalTime(),
            Until = change.Until.ToUniversalTime(),
            Reason = stored.Reason,
        };
        if (await BlockOverlapAsync(block, ct) is { } rejected)
        {
            return (null, rejected);
        }

        // Die Zeiten einer Belegung sind unveränderlich (init); die Ausschlussbedingung der Datenbank fängt ein gleichzeitiges Belegen ab.
        int changed;
        try
        {
            changed = await db.ResourceBookings.Where(b => b.Id == blockId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.From, block.From).SetProperty(b => b.Until, block.Until), ct);
        }
        catch (Exception ex) when (IsBookingRace(ex))
        {
            return (null, TakenMeanwhile);
        }

        // Inzwischen an einem anderen Arbeitsplatz gelöscht: wie unbekannt.
        return changed == 0 ? (null, null) : (block, null);
    }

    /// <summary>Was die Zeit der Sperrzeit schon belegt (Termine, Bereiche, andere Sperrzeiten), als Ablehnung; sonst null.</summary>
    private async Task<BookingRejected?> BlockOverlapAsync(ResourceBooking block, CancellationToken ct)
    {
        var overlapping = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.Id != block.Id && b.ResourceId == block.ResourceId && b.From < block.Until && block.From < b.Until)
            .ToListAsync(ct);
        if (overlapping.Count == 0)
        {
            return null;
        }

        var appointmentIds = overlapping.Where(b => b.AppointmentId is not null).Select(b => b.AppointmentId!.Value).ToList();
        var guests = (await db.Appointments.AsNoTracking().Include(a => a.Customer).Where(a => appointmentIds.Contains(a.Id)).ToListAsync(ct))
            .ToDictionary(a => a.Id, a => a.Customer.InHouseName);
        return new BookingRejected("Zeitraum ist belegt.",
            [.. overlapping.OrderBy(b => b.From).Select(b => b.AppointmentId is { } appointmentId
                ? new BookingConflict(b.Kind, b.ResourceId, guests.GetValueOrDefault(appointmentId, "Termin"), "hat hier einen Termin", b.From, b.Until, appointmentId)
                : b.AreaVisitId is not null
                    ? new BookingConflict(b.Kind, b.ResourceId, "Bereich", "ist belegt", b.From, b.Until)
                    : new BookingConflict(b.Kind, b.ResourceId, b.Reason ?? "Sperrzeit", "ist gesperrt", b.From, b.Until, IsBlock: true))]);
    }

    public static AppointmentDto ToDto(Appointment a, string treatmentName) => new(
        a.Id, a.LocationId, a.TreatmentId, treatmentName, a.CustomerId,
        a.Customer.InHouseName,
        a.EmployeeId, a.RoomId, a.EquipmentId, a.Start, a.End, a.BlockedFrom, a.BlockedUntil,
        a.Status, a.Price, a.Note, a.UpdatedAt, a.GroupId, a.SeriesId, a.Source, a.LateMinutes, a.PlaceholderGuest, a.ConfirmedAt,
        a.PlatformPayment, a.PrepaidAmount, a.AcceptInPlatformPending, a.Customer.AssignmentOpen,
        PreparationMinutes: a.PreparationMinutes, FollowUpMinutes: a.FollowUpMinutes, ExtraFollowUpMinutes: a.ExtraFollowUpMinutes,
        HandsOnMinutes: a.HandsOnMinutes, ProcessingMinutes: a.ProcessingMinutes,
        HeldSince: a.HeldSince, HoldRemindAt: a.HoldRemindAt, HoldAnsweredAt: a.HoldAnsweredAt, HoldMailId: a.HoldMailId, StartedAt: a.StartedAt, Version: a.Version);

    /// <summary>
    /// Ä281: Kommt die Änderung von einem veralteten Stand (<paramref name="expected"/> ist nicht die Version des Termins), lehnt der Server mit dem
    /// aktuellen Termin ab, statt die Änderung eines anderen Arbeitsplatzes still zu überschreiben. Ohne Angabe gilt das bisherige Verhalten.
    /// Ändert jemand den Termin zwischen dieser Prüfung und dem Speichern, fällt das über die xmin-Sperre auf (<see cref="TrySaveAsync"/>).
    /// 0 heißt „Stand unbekannt“ (offline gebuchter Termin, zwischengespeicherter Stand von vor Ä281) und wird nicht geprüft; xmin ist für eine
    /// gespeicherte Zeile nie 0.
    /// </summary>
    private static BookingOutcome.Rejected? StaleOf(Appointment appointment, uint? expected) =>
        expected is { } version and not 0 && version != appointment.Version
            ? new BookingOutcome.Rejected(new BookingRejected(StaleMessage, [], Current: ToDto(appointment, appointment.Treatment.Name)))
            : null;

    private Task<Appointment?> LoadAsync(Guid id, CancellationToken ct) =>
        db.Appointments.Include(a => a.Bookings).Include(a => a.Customer).Include(a => a.Treatment)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>Der neue Termin zu einem geplanten Ablauf samt seinen Belegungen (noch nicht gespeichert).</summary>
    internal static Appointment NewAppointment(AppointmentRequest request, Customer customer, PlanningContext context, PlannedAppointment plan,
        DateTimeOffset now, Guid? seriesId, AppointmentSource source, PlatformBooking? platform)
    {
        var price = context.Treatment.PriceAt(request.LocationId, plan.Times.Start, context.Zone);
        var appointment = new Appointment
        {
            Id = request.ClientId ?? Guid.CreateVersion7(),
            LocationId = request.LocationId,
            TreatmentId = context.Treatment.Id,
            CustomerId = customer.Id,
            Customer = customer,
            Price = price,
            Note = request.Note,
            SeriesId = seriesId,
            Source = source,
            PlatformPayment = platform?.Payment ?? PlatformPayment.None,
            PrepaidAmount = platform?.PrepaidAmount is { } prepaid ? Math.Min(prepaid, price) : null,
            AcceptInPlatformPending = platform?.AcceptPending ?? false,
            CreatedAt = now,
        };
        ApplyPlan(appointment, plan, now);
        return appointment;
    }

    private void Apply(Appointment appointment, PlannedAppointment plan, DateTimeOffset now) =>
        db.ResourceBookings.AddRange(ApplyPlan(appointment, plan, now));

    /// <summary>Überträgt den Plan auf den Termin und legt seine Belegungen an (am Termin, noch nicht gespeichert).</summary>
    internal static List<ResourceBooking> ApplyPlan(Appointment appointment, PlannedAppointment plan, DateTimeOffset now)
    {
        appointment.EmployeeId = plan.Employee.Id;
        appointment.RoomId = plan.Room.Id;
        appointment.EquipmentId = plan.Equipment?.Id;
        // Eine Zusage gilt für die Zeit, zu der der Gast sie gegeben hat.
        if (appointment.Start != plan.Times.Start)
        {
            appointment.ConfirmedAt = null;
        }

        appointment.Start = plan.Times.Start;
        appointment.End = plan.Times.End;
        appointment.BlockedFrom = plan.Times.BlockedFrom;
        appointment.BlockedUntil = plan.Times.BlockedUntil;
        appointment.PreparationMinutes = plan.Times.PreparationMinutes;
        appointment.FollowUpMinutes = plan.Times.FollowUpMinutes;
        appointment.ExtraFollowUpMinutes = plan.Times.ExtraFollowUpMinutes;
        appointment.HandsOnMinutes = plan.Times.HandsOnMinutes;
        appointment.ProcessingMinutes = plan.Times.ProcessingMinutes;
        appointment.UpdatedAt = now;
        var created = new List<ResourceBooking>();
        foreach (var (kind, resourceId, slot, from, until) in plan.Bookings())
        {
            // Ausdrücklich als neu markieren: die Id wird hier vergeben, EF würde sonst ein Update annehmen.
            var booking = new ResourceBooking
            {
                Kind = kind,
                ResourceId = resourceId,
                Slot = slot,
                From = from,
                Until = until,
                AppointmentId = appointment.Id,
            };
            created.Add(booking);
            appointment.Bookings.Add(booking);
        }

        return created;
    }

    /// <summary>Speichert; <c>false</c>, wenn inzwischen jemand anderes die Zeit belegt oder den Termin geändert hat.</summary>
    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (IsBookingRace(ex))
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private static bool IsBookingRace(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException => true,
        PostgresException { SqlState: ExclusionViolation } => true,
        { InnerException: { } inner } => IsBookingRace(inner),
        _ => false,
    };

    /// <summary>Meldet die Änderung über die Queue an alle Kalender des Standorts und liefert den neuen Stand.</summary>
    private async Task<BookingOutcome> PublishAsync(
        Appointment appointment, string treatmentName, DateTimeOffset? previousStart = null, Guid? previousLocationId = null)
    {
        var dto = ToDto(appointment, treatmentName);
        await bus.PublishAsync(new AppointmentChangedMessage(dto, previousStart, previousLocationId));
        return new BookingOutcome.Success(dto);
    }
}
