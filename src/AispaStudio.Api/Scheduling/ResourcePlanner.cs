using AispaStudio.Api.Closures;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Alles, was der Planer für eine Behandlung an einem Standort kennen muss. Wird aus der Datenbank geladen.
/// <c>UsefulGapMinutes</c>: kürzeste Bindung einer Person durch eine Behandlung des Standorts; kürzere Lücken bleiben ungenutzt.
/// <c>DurationMinutes</c>: verkürzter Termin; ohne Angabe die Dauer der Behandlung.
/// <c>PreparationMinutes</c>, <c>FollowUpMinutes</c>, <c>HandsOnMinutes</c>, <c>ProcessingMinutes</c>: Zeiten eines bestehenden Termins; ohne Angabe die der Behandlung.
/// <c>Closed</c>: Schließzeiten des Standorts im Zeitraum des Tages (ADR 0073); in ihnen ist der Standort nicht geöffnet.
/// <c>ClosedRooms</c>: Raumsperren der Räume des Standorts im Zeitraum des Tages (ADR 0076); ein gesperrter Raum wird in der Zeit nicht gewählt.
/// <c>House</c>: die Rezeption trägt ein (ADR 0073, Nachtrag R03); an einem Schließtag oder einem Tag ohne Dienst plant sie trotzdem und bekommt nur einen Hinweis.
/// <c>OutsideHours</c>: die Rezeption hat „Trotzdem buchen“ bestätigt (Slice 85, ADR 0073, Nachtrag R04); wirkt nur mit <c>House</c> und übergeht Öffnungszeit und Dienst.
/// </summary>
internal sealed record PlanningContext(
    TimeZoneInfo Zone,
    IReadOnlyList<LocationOpeningHours> OpeningHours,
    Treatment Treatment,
    IReadOnlyList<Employee> Employees,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Equipment> Equipment,
    IReadOnlyList<ResourceBooking> Bookings,
    Guid LocationId,
    IReadOnlyList<EmployeeShift> Shifts,
    int UsefulGapMinutes = 30,
    int ExtraFollowUpMinutes = 0,
    int? DurationMinutes = null,
    int? PreparationMinutes = null,
    int? FollowUpMinutes = null,
    int? HandsOnMinutes = null,
    int? ProcessingMinutes = null,
    IReadOnlyList<ClosedSpan>? Closed = null,
    IReadOnlyList<ClosedRoomSpan>? ClosedRooms = null,
    bool House = false,
    bool OutsideHours = false);

/// <summary>Eine Schließzeit als Zeitraum in UTC (<c>Until</c> ausschließlich) mit ihrer Art, damit Meldungen den Grund nennen können.</summary>
internal sealed record ClosedSpan(DateTimeOffset From, DateTimeOffset Until, ClosureKind Kind);

/// <summary>
/// Zeiten eines Termins: Behandlung, Bindung der Person (mit Vor-/Nachbereitung) und des Raums (zusätzlich Raumpflege).
/// Mit Einwirkzeit (<c>ProcessingMinutes</c> &gt; 0) ist die Person von <c>Start + HandsOnMinutes</c> für diese Zeit frei, der Raum bleibt belegt.
/// </summary>
internal sealed record PlannedTimes(
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset BlockedFrom,
    DateTimeOffset BlockedUntil,
    DateTimeOffset RoomUntil,
    int PreparationMinutes,
    int FollowUpMinutes,
    int ExtraFollowUpMinutes,
    int HandsOnMinutes = 0,
    int ProcessingMinutes = 0)
{
    /// <summary>Zeiten, in denen die Person gebunden ist: ein Fenster, mit Einwirkzeit davor und danach.</summary>
    public IEnumerable<(DateTimeOffset From, DateTimeOffset Until)> EmployeeWindows()
    {
        if (ProcessingMinutes == 0)
        {
            yield return (BlockedFrom, BlockedUntil);
            yield break;
        }

        var freeFrom = Start.AddMinutes(HandsOnMinutes);
        var freeUntil = freeFrom.AddMinutes(ProcessingMinutes);
        if (BlockedFrom < freeFrom)
        {
            yield return (BlockedFrom, freeFrom);
        }

        if (freeUntil < BlockedUntil)
        {
            yield return (freeUntil, BlockedUntil);
        }
    }
}

/// <summary>
/// Geplanter Termin. <c>RoomSlot</c> und <c>RoomFrom</c> setzt nur die gemeinsame Buchung im Mehrplatzraum: jeder Gast einen eigenen Platz,
/// alle für die ganze Zeit, in der die Gruppe den Raum braucht.
/// </summary>
internal sealed record PlannedAppointment(
    PlannedTimes Times, Employee Employee, Room Room, Equipment? Equipment, int RoomSlot = 0, DateTimeOffset? RoomFrom = null)
{
    public IEnumerable<(ResourceKind Kind, Guid Id, int Slot, DateTimeOffset From, DateTimeOffset Until)> Bookings()
    {
        foreach (var (from, until) in Times.EmployeeWindows())
        {
            yield return (ResourceKind.Employee, Employee.Id, 0, from, until);
        }

        yield return (ResourceKind.Room, Room.Id, RoomSlot, RoomFrom ?? Times.BlockedFrom, Times.RoomUntil);
        if (Equipment is not null)
        {
            yield return (ResourceKind.Equipment, Equipment.Id, 0, Times.Start, Times.End);
        }
    }
}

/// <summary><c>OutsideHours</c>: die Ablehnung ließe sich für die Rezeption mit „Trotzdem buchen“ aufheben (nur Öffnungszeit oder Dienst im Weg).</summary>
internal sealed record PlanResult(PlannedAppointment? Plan, string? Message, IReadOnlyList<BookingConflict> Conflicts, string? Notice = null, bool OutsideHours = false)
{
    public static PlanResult Rejected(string message, IReadOnlyList<BookingConflict> conflicts, bool outsideHours = false) => new(null, message, conflicts, OutsideHours: outsideHours);
}

/// <summary>
/// Findet für einen Startzeitpunkt eine Kombination aus Person, Raum und Gerät, die gleichzeitig frei ist.
/// Reine Logik ohne Datenbank: Buchung und Suche nach freien Zeiten verwenden denselben Weg.
/// </summary>
internal sealed class ResourcePlanner(PlanningContext context)
{
    private int Preparation => context.PreparationMinutes ?? context.Treatment.PreparationMinutes;

    private int FollowUp => context.FollowUpMinutes ?? context.Treatment.FollowUpMinutes;

    public PlannedTimes TimesFor(DateTimeOffset start)
    {
        var duration = context.DurationMinutes ?? context.Treatment.DurationMinutes;
        var end = start.AddMinutes(duration);
        // Verkürzte Termine behalten die Einwirkzeit nur, soweit sie in die Dauer passt; ohne Einwirkzeit gibt es keine Minuten am Gast.
        var handsOn = Math.Min(context.HandsOnMinutes ?? context.Treatment.HandsOnMinutes, duration);
        var processing = Math.Min(context.ProcessingMinutes ?? context.Treatment.ProcessingMinutes, duration - handsOn);
        if (processing <= 0)
        {
            (handsOn, processing) = (0, 0);
        }

        var blockedFrom = start.AddMinutes(-Preparation);
        // Kennzeichen „Braucht mehr Zeit“: Puffer nach der Behandlung (CustomerRules).
        var blockedUntil = end.AddMinutes(FollowUp + context.ExtraFollowUpMinutes);
        return new PlannedTimes(start, end, blockedFrom, blockedUntil, blockedUntil, Preparation, FollowUp, context.ExtraFollowUpMinutes,
            handsOn, processing);
    }

    public PlanResult Plan(DateTimeOffset start, Guid? employeeId = null, Guid? roomId = null, Guid? equipmentId = null)
    {
        var times = TimesFor(start);

        // Die Rezeption trägt an einem Schließtag oder an einem Tag ohne Dienst trotzdem ein; Gäste (online, KI-Annahme) nie.
        // Mit „Trotzdem buchen“ (nur die Rezeption) auch außerhalb der Öffnungszeit und des Dienstes.
        var confirmed = context.House && context.OutsideHours;
        var opening = OpeningRules.Check(DayWindows(times.Start), context.Shifts.Count > 0, context.House, times.Start, times.End);
        if (opening == OpeningState.Closed && !confirmed)
        {
            return PlanResult.Rejected(ClosedMessage(times.Start, times.End), [], outsideHours: context.House);
        }

        var closedDay = opening == OpeningState.ClosedDay;

        var conflicts = new List<BookingConflict>();
        var employee = PickEmployee(times, employeeId, conflicts, ignoreDuty: closedDay || confirmed);
        var room = PickRoom(times, roomId, conflicts);
        var equipment = PickEquipment(times, equipmentId, conflicts, out var equipmentOk);

        if (employee is null || room is null || !equipmentOk)
        {
            // Steht nur der Dienst im Weg, kann die Rezeption trotzdem buchen.
            var onlyDuty = context.House && !confirmed && room is not null && equipmentOk
                && employee is null && PickEmployee(times, employeeId, [], ignoreDuty: true) is not null;
            return PlanResult.Rejected("Zu dieser Zeit ist nicht alles frei, was die Behandlung braucht.", conflicts, onlyDuty);
        }

        var notice = closedDay ? ClosedDayNotice
            : opening == OpeningState.Closed || !IsWorking(employee, times.BlockedFrom, times.BlockedUntil) ? BookingNotices.OutsideHours
            : null;
        return new PlanResult(
            new PlannedAppointment(times with { RoomUntil = times.BlockedUntil.AddMinutes(room.CleaningMinutes) }, employee, room, equipment),
            null,
            [],
            notice);
    }

    /// <summary>Der Hinweis an die Rezeption, wenn sie an einem Schließtag oder an einem Tag ohne Dienst einträgt.</summary>
    public const string ClosedDayNotice = BookingNotices.ClosedDay;

    /// <summary>Die Öffnungsfenster des Tages, an dem <paramref name="at"/> liegt (Schließtag und Ablehnung rechnet <see cref="OpeningRules.Check"/>).</summary>
    private IReadOnlyList<TimeRange> DayWindows(DateTimeOffset at) =>
        [.. OpeningWindows(LocalTime.LocalDate(at, context.Zone)).Select(w => new TimeRange(w.Start, w.End))];

    /// <summary>
    /// Freie Startzeiten eines Tages im angegebenen Raster, je Person eine Kombination, mit Bewertung der Lücken.
    /// <c>snug</c>: zusätzlich Zeiten außerhalb des Rasters, die lückenlos an eine Belegung oder an Dienstbeginn bzw. -ende anschließen
    /// (z. B. 11:05, wenn der Termin davor samt Nachbereitung um 11:00 endet und die Behandlung 5 Minuten Vorbereitung braucht).
    /// </summary>
    public IEnumerable<AvailableSlot> FreeSlots(DateOnly date, TimeSpan step, Guid? employeeId, bool snug = false)
    {
        var employees = (employeeId is { } id ? context.Employees.Where(e => e.Id == id) : context.Employees).ToList();
        var extra = snug ? SnugStarts(employees) : [];
        foreach (var window in OpeningWindows(date))
        {
            var grid = new List<DateTimeOffset>();
            for (var start = window.Start; start.AddMinutes(context.Treatment.DurationMinutes) <= window.End; start += step)
            {
                grid.Add(start);
            }

            var onGrid = grid.ToHashSet();
            var starts = grid.Concat(extra.Where(e => window.Start <= e.Start && e.Start < window.End).Select(e => e.Start))
                .Distinct().Order();
            foreach (var start in starts)
            {
                foreach (var employee in employees)
                {
                    if (!onGrid.Contains(start) && !extra.Contains((employee.Id, start)))
                    {
                        continue;
                    }

                    var result = Plan(start, employee.Id);
                    if (result.Plan is not { } plan)
                    {
                        continue;
                    }

                    var (fit, reason) = Fit(plan);
                    if (onGrid.Contains(start) || fit == SlotFit.Snug)
                    {
                        yield return new AvailableSlot(plan.Times.Start, plan.Times.End, plan.Employee.Id, plan.Room.Id, plan.Equipment?.Id, fit, reason);
                    }
                }
            }
        }
    }

    /// <summary>Startzeiten, bei denen die Bindung der Person genau an eine Belegung oder an den Dienst anschließt.</summary>
    private HashSet<(Guid EmployeeId, DateTimeOffset Start)> SnugStarts(IEnumerable<Employee> employees)
    {
        var starts = new HashSet<(Guid, DateTimeOffset)>();
        foreach (var employee in employees)
        {
            var edges = context.Shifts.Where(s => s.EmployeeId == employee.Id).Select(s => (After: s.Start, Before: s.End))
                .Concat(context.Bookings.Where(b => b.ResourceId == employee.Id).Select(b => (After: b.Until, Before: b.From)));
            foreach (var (after, before) in edges)
            {
                starts.Add((employee.Id, after.AddMinutes(Preparation)));
                starts.Add((employee.Id, before.AddMinutes(-FollowUp - context.ExtraFollowUpMinutes - context.Treatment.DurationMinutes)));
            }
        }

        return starts;
    }

    /// <summary>
    /// Bewertet einen geplanten Termin nach den Lücken, die er im Dienst der Person lässt: davor bis zur vorigen Belegung oder zum Dienstbeginn,
    /// danach bis zur nächsten Belegung oder zum Dienstende. Eine Lücke, die kürzer ist als jede Behandlung des Standorts, ist verlorene Arbeitszeit.
    /// </summary>
    public (SlotFit Fit, string Reason) Fit(PlannedAppointment plan)
    {
        var (from, until) = (plan.Times.BlockedFrom, plan.Times.BlockedUntil);
        var shift = context.Shifts.FirstOrDefault(s => s.EmployeeId == plan.Employee.Id && s.Start <= from && until <= s.End);
        if (shift is null)
        {
            return (SlotFit.Neutral, "Außerhalb eines Dienstes geplant.");
        }

        var own = context.Bookings.Where(b => b.ResourceId == plan.Employee.Id && b.From < shift.End && shift.Start < b.Until).ToList();
        var previous = own.Where(b => b.Until <= from).MaxBy(b => b.Until);
        var next = own.Where(b => b.From >= until).MinBy(b => b.From);
        var before = (int)(from - (previous is null ? shift.Start : Max(previous.Until, shift.Start))).TotalMinutes;
        var after = (int)((next is null ? shift.End : Min(next.From, shift.End)) - until).TotalMinutes;
        var useful = context.UsefulGapMinutes;
        var name = plan.Employee.DisplayName;
        var deadBefore = before > 0 && before < useful;
        var deadAfter = after > 0 && after < useful;

        if (deadBefore || deadAfter)
        {
            var gap = (deadBefore, deadAfter) switch
            {
                (true, true) => $"{before} Minuten davor und {after} Minuten danach",
                (true, false) => $"{before} Minuten davor",
                _ => $"{after} Minuten danach",
            };

            return (SlotFit.LeavesGap, $"Lässt bei {name} {gap} frei; dort passt keine Behandlung (kürzeste {useful} Minuten).");
        }

        if (before == 0 && after == 0)
        {
            return (SlotFit.Snug, $"Füllt die Lücke bei {name} genau.");
        }

        if (before == 0)
        {
            return (SlotFit.Snug, previous is not null && previous.Until == from
                ? $"Schließt bei {name} direkt an {(previous.AppointmentId is null ? "die Sperrzeit" : "den Termin")} davor an."
                : $"Beginnt bei {name} mit dem Dienst.");
        }

        if (after == 0)
        {
            return (SlotFit.Snug, next is not null && next.From == until
                ? $"Endet bei {name} direkt vor {(next.AppointmentId is null ? "der Sperrzeit" : "dem nächsten Termin")}."
                : $"Endet bei {name} mit dem Dienst.");
        }

        return (SlotFit.Neutral, $"Lässt bei {name} davor {before} und danach {after} Minuten für weitere Behandlungen.");

        static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
        static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    }

    /// <summary>Die Öffnungszeiten des Tages in UTC, ohne die Schließzeiten des Standorts (ADR 0073): ein halber Tag Betriebsurlaub lässt nur den Rest offen.</summary>
    public IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> OpeningWindows(DateOnly date) =>
        OpeningWindows(context.OpeningHours, context.Zone, date, context.Closed);

    /// <summary>Wie oben, ohne Planer; auch der Assistent rechnet damit, wann ein Tag geöffnet ist. Die Regel steht in <see cref="OpeningRules"/> (auch für die Windows-App).</summary>
    public static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> OpeningWindows(
        IEnumerable<LocationOpeningHours> openingHours, TimeZoneInfo zone, DateOnly date, IReadOnlyList<ClosedSpan>? closed) =>
        OpeningRules.Windows(
                openingHours.Where(o => o.Day == date.DayOfWeek).Select(o => new TimeRange(LocalTime.ToUtc(date, o.Opens, zone), LocalTime.ToUtc(date, o.Closes, zone))),
                ClosedRanges(closed))
            .Select(w => (w.Start, w.End));

    /// <summary>Die Schließzeiten für <see cref="OpeningRules"/>, mit der Art in Worten für die Meldung.</summary>
    public static List<ClosedRange> ClosedRanges(IReadOnlyList<ClosedSpan>? closed) =>
        [.. (closed ?? []).Select(c => new ClosedRange(c.From, c.Until, ClosureText.Title(c.Kind)))];

    /// <summary>Warum ein Termin nicht passt, wenn der Standort geschlossen ist: wegen einer Schließzeit mit ihrer Art, sonst wegen der Öffnungszeiten.</summary>
    private string ClosedMessage(DateTimeOffset from, DateTimeOffset until) => OpeningRules.ClosedMessage(ClosedRanges(context.Closed), from, until);

    private bool IsOpen(DateTimeOffset from, DateTimeOffset until) => OpeningRules.IsOpen(DayWindows(from), from, until);

    private Employee? PickEmployee(PlannedTimes times, Guid? employeeId, List<BookingConflict> conflicts, bool ignoreDuty = false)
    {
        var required = context.Treatment.RequiredQualifications.Select(q => q.Id).ToHashSet();

        string? Reason(Employee e)
        {
            if (!required.IsSubsetOf(e.Qualifications.Select(q => q.Id)))
            {
                return "hat nicht die nötige Qualifikation";
            }

            if (!ignoreDuty && !IsWorking(e, times.BlockedFrom, times.BlockedUntil))
            {
                return "ist zu dieser Zeit nicht im Dienst";
            }

            return times.EmployeeWindows().Any(w => IsBusy(e.Id, w.From, w.Until)) ? BusyReason : null;
        }

        if (employeeId is { } id)
        {
            var wanted = context.Employees.FirstOrDefault(e => e.Id == id);
            var reason = wanted is null ? "ist an diesem Tag hier nicht im Dienst" : Reason(wanted);
            if (reason is null)
            {
                return wanted;
            }

            var busy = wanted is null || reason != BusyReason
                ? null
                : times.EmployeeWindows().Select(w => Overlapping(wanted.Id, w.From, w.Until)).FirstOrDefault(b => b is not null);
            conflicts.Add(Conflict(ResourceKind.Employee, id, wanted?.DisplayName ?? "Person", reason, busy));
            return null;
        }

        // Gleichmäßig auslasten: zuerst die Person, die an diesem Tag am wenigsten gebucht ist.
        var free = context.Employees
            .Where(e => Reason(e) is null)
            .OrderBy(e => BookedMinutes(e.Id))
            .ThenBy(e => e.DisplayName, StringComparer.CurrentCulture)
            .FirstOrDefault();
        if (free is null)
        {
            conflicts.Add(new BookingConflict(ResourceKind.Employee, Guid.Empty, "Team",
                "Niemand mit der nötigen Qualifikation ist zu dieser Zeit im Dienst und frei"));
        }

        return free;
    }

    private Room? PickRoom(PlannedTimes times, Guid? roomId, List<BookingConflict> conflicts)
    {
        bool IsFree(Room r) => ClosedAt(r, times) is null && !IsBusy(r.Id, times.BlockedFrom, times.BlockedUntil.AddMinutes(r.CleaningMinutes));

        if (roomId is { } id)
        {
            var wanted = context.Rooms.FirstOrDefault(r => r.Id == id);
            if (wanted is not null && IsFree(wanted))
            {
                return wanted;
            }

            conflicts.Add(wanted is not null && ClosedAt(wanted, times) is { } closed
                ? ClosedConflict(wanted, closed)
                : Conflict(ResourceKind.Room, id, wanted?.Name ?? "Raum", wanted is null ? "ist für diese Behandlung nicht geeignet" : BusyReason,
                    wanted is null ? null : Overlapping(wanted.Id, times.BlockedFrom, times.BlockedUntil.AddMinutes(wanted.CleaningMinutes))));
            return null;
        }

        // Mehrplatzräume (Paarsuite) zuletzt, damit sie für gemeinsame Buchungen frei bleiben.
        var free = context.Rooms.OrderBy(r => r.Places).FirstOrDefault(IsFree);
        if (free is null)
        {
            // Gesperrte Räume nennen ihren Grund, damit „kein Raum frei“ nicht rätselhaft bleibt.
            foreach (var room in context.Rooms)
            {
                conflicts.Add(ClosedAt(room, times) is { } closed
                    ? ClosedConflict(room, closed)
                    : Conflict(ResourceKind.Room, room.Id, room.Name, BusyReason, Overlapping(room.Id, times.BlockedFrom, times.BlockedUntil.AddMinutes(room.CleaningMinutes))));
            }

            conflicts.Add(new BookingConflict(ResourceKind.Room, Guid.Empty, "Räume",
                context.Rooms.Count == 0
                    ? "An diesem Standort gibt es keinen geeigneten Raum"
                    : "Kein geeigneter Raum ist zu dieser Zeit frei",
                NextFree: context.Rooms.Count == 0 ? null : NextFreeRoomStart(times)));
        }

        return free;
    }

    /// <summary>
    /// Frühester Beginn nach dem gewünschten, zu dem ein passender Raum für die ganze Bindung (Vorbereitung bis Raumpflege) frei ist: der Beginn rückt
    /// jeweils an das früheste Ende einer Belegung oder Raumsperre vor, die noch im Weg steht. <c>null</c>, wenn das nicht in den Öffnungszeiten liegt.
    /// </summary>
    private DateTimeOffset? NextFreeRoomStart(PlannedTimes times)
    {
        var length = times.BlockedUntil - times.BlockedFrom;
        var from = times.BlockedFrom;
        for (var step = 0; step < 100; step++)
        {
            var blockers = context.Rooms.Select(r => BlockerEnd(r, from, from + length)).ToList();
            if (blockers.Any(b => b is null))
            {
                var start = from.AddMinutes(times.PreparationMinutes);
                return IsOpen(start, start + (times.End - times.Start)) ? start : null;
            }

            from = blockers.Min()!.Value;
        }

        return null;
    }

    /// <summary>Wann die Belegung oder Raumsperre endet, die den Raum im Zeitraum (mit Raumpflege) blockiert; <c>null</c>, wenn er frei ist.</summary>
    private DateTimeOffset? BlockerEnd(Room room, DateTimeOffset from, DateTimeOffset until)
    {
        var withCleaning = until.AddMinutes(room.CleaningMinutes);
        var booking = Overlapping(room.Id, from, withCleaning);
        var closed = (context.ClosedRooms ?? []).Where(c => c.RoomId == room.Id && c.From < withCleaning && from < c.Until).MinBy(c => c.From);
        return (booking?.Until, closed?.Until) switch
        {
            ({ } b, { } c) => b < c ? b : c,
            ({ } b, null) => b,
            (null, { } c) => c,
            _ => null,
        };
    }

    private Equipment? PickEquipment(PlannedTimes times, Guid? equipmentId, List<BookingConflict> conflicts, out bool ok)
    {
        ok = true;
        if (context.Treatment.Equipment.Count == 0)
        {
            return null;
        }

        bool IsFree(Equipment e) => !IsBusy(e.Id, times.Start, times.End);

        if (equipmentId is { } id)
        {
            var wanted = context.Equipment.FirstOrDefault(e => e.Id == id);
            if (wanted is not null && IsFree(wanted))
            {
                return wanted;
            }

            ok = false;
            conflicts.Add(Conflict(ResourceKind.Equipment, id, wanted?.Name ?? "Gerät", wanted is null ? "ist für diese Behandlung nicht geeignet" : BusyReason,
                wanted is null ? null : Overlapping(wanted.Id, times.Start, times.End)));
            return null;
        }

        var free = context.Equipment.FirstOrDefault(IsFree);
        if (free is null)
        {
            ok = false;
            conflicts.Add(new BookingConflict(ResourceKind.Equipment, Guid.Empty, "Geräte",
                context.Equipment.Count == 0
                    ? "An diesem Standort gibt es kein geeignetes Gerät"
                    : "Kein geeignetes Gerät ist zu dieser Zeit frei"));
        }

        return free;
    }

    /// <summary>Die Raumsperre, die den Raum für die Belegung des Termins (mit Vor- und Nachbereitung und Raumpflege) sperrt; sonst <c>null</c>.</summary>
    private ClosedRoomSpan? ClosedAt(Room room, PlannedTimes times) =>
        (context.ClosedRooms ?? []).FirstOrDefault(c => c.RoomId == room.Id && c.From < times.BlockedUntil.AddMinutes(room.CleaningMinutes) && times.BlockedFrom < c.Until);

    private static BookingConflict ClosedConflict(Room room, ClosedRoomSpan closed) =>
        new(ResourceKind.Room, room.Id, room.Name, RoomClosureText.Reason(closed.Kind, closed.Reason), closed.From, closed.Until, null, true);

    /// <summary>Im Dienst laut Dienstplan des Tages (oder Wochenarbeitszeit, wenn für den Tag nichts geplant ist).</summary>
    private bool IsWorking(Employee employee, DateTimeOffset from, DateTimeOffset until) =>
        context.Shifts.Any(s => s.EmployeeId == employee.Id && s.Start <= from && until <= s.End);

    private const string BusyReason = "ist zu dieser Zeit schon belegt";

    private bool IsBusy(Guid resourceId, DateTimeOffset from, DateTimeOffset until) => Overlapping(resourceId, from, until) is not null;

    /// <summary>Die früheste Belegung der Ressource, die den Zeitraum schneidet (Termin oder Sperrzeit).</summary>
    private ResourceBooking? Overlapping(Guid resourceId, DateTimeOffset from, DateTimeOffset until) =>
        context.Bookings.Where(b => b.ResourceId == resourceId && b.From < until && from < b.Until).OrderBy(b => b.From).FirstOrDefault();

    /// <summary>Ein Konflikt mit der Belegung dahinter (Zeitraum, Termin oder Sperrzeit), damit die Oberfläche ihn in Ortszeit nennen kann.</summary>
    private static BookingConflict Conflict(ResourceKind kind, Guid id, string name, string reason, ResourceBooking? booking) =>
        new(kind, id, name, reason, booking?.From, booking?.Until, booking?.AppointmentId, booking is { AppointmentId: null, AreaVisitId: null });

    private double BookedMinutes(Guid resourceId) =>
        context.Bookings.Where(b => b.ResourceId == resourceId && b.AppointmentId != null).Sum(b => (b.Until - b.From).TotalMinutes);
}
