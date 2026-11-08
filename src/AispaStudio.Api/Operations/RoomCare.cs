using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Operations;

/// <summary>
/// Raumpflege nach jedem Termin (abhakbare Aufgabe mit Zustand offen, erledigt, überfällig), Tagesstart-Kontrolle je Raum mit Checkliste und
/// Abnahme durch die Standortleitung, dazu das Hygieneprotokoll je Tag und Raum. Alles sind Hinweise und Aufzeichnungen: nichts blockiert eine Buchung
/// oder ein Einchecken.
/// </summary>
internal sealed class RoomCareService(AispaDbContext db, TimeProvider time, RoomRestockService restock)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");
    private static readonly AppointmentStatus[] Gone = [AppointmentStatus.Cancelled, AppointmentStatus.NoShow];

    public const int MaxLogDays = 92;

    /// <summary>Aufgaben der Raumpflege und Zustand der Räume für den Kalender eines Tages; <paramref name="appointments"/> sind die Termine des Tages.</summary>
    public async Task<RoomCareDay> DayAsync(Guid locationId, DateOnly date, TimeZoneInfo zone, IReadOnlyList<Appointment> appointments, CancellationToken ct)
    {
        var (from, until) = LocalTime.Day(date, zone);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId).ToDictionaryAsync(r => r.Id, ct);
        var live = appointments.Where(a => a.Start >= from && a.Start < until && !Gone.Contains(a.Status)
            && rooms.TryGetValue(a.RoomId, out var room) && room.CleaningMinutes > 0).ToList();
        var liveIds = live.Select(a => a.Id).ToList();
        var careChecks = (await db.RoomCareChecks.AsNoTracking().Where(c => liveIds.Contains(c.AppointmentId)).ToListAsync(ct)).ToDictionary(c => c.AppointmentId);
        var careNames = await NamesAsync(careChecks.Values.Select(c => c.UserId), ct);
        var roomIds = rooms.Keys.ToList();
        var equipment = await restock.EquipmentAsync(roomIds, ct);
        var tasks = await restock.WithRestockAsync(Tasks(live, rooms, zone, careChecks, careNames), live, equipment, ct);

        var items = await db.RoomStartItems.AsNoTracking().Where(i => i.IsActive && roomIds.Contains(i.RoomId)).ToListAsync(ct);
        var ticks = await db.RoomStartChecks.AsNoTracking().Where(c => c.Date == date && roomIds.Contains(c.RoomId)).ToListAsync(ct);
        var approvals = await db.RoomApprovals.AsNoTracking().Where(a => a.Date == date && roomIds.Contains(a.RoomId)).ToListAsync(ct);
        var names = await NamesAsync(approvals.Select(a => a.ApprovedByUserId), ct);

        var states = new List<RoomDayStateDto>();
        foreach (var room in rooms.Values.Where(r => r.IsActive).OrderBy(r => r.Name, StringComparer.Ordinal))
        {
            var roomItems = items.Where(i => i.RoomId == room.Id).ToList();
            var roomTasks = tasks.Where(t => t.RoomId == room.Id).ToList();
            if (roomItems.Count == 0 && roomTasks.Count == 0)
            {
                continue;
            }

            var checkedCount = ticks.Count(c => c.RoomId == room.Id && roomItems.Any(i => i.Id == c.ItemId));
            var start = approvals.FirstOrDefault(a => a.RoomId == room.Id && a.Kind == RoomApprovalKind.DayStart);
            var care = approvals.FirstOrDefault(a => a.RoomId == room.Id && a.Kind == RoomApprovalKind.RoomCare);
            states.Add(new(room.Id, room.Name, StartState(roomItems.Count, checkedCount, start is not null), roomItems.Count, checkedCount,
                start?.ApprovedAt, Name(names, start?.ApprovedByUserId), roomTasks.Count(t => t.State == RoomCareState.Open),
                roomTasks.Count(t => t.State == RoomCareState.Overdue), care?.ApprovedAt, Name(names, care?.ApprovedByUserId)));
        }

        return new(tasks, states, RoomRestockService.RoomsBelowMinimum(equipment));
    }

    /// <summary>Die Raumpflege-Aufgabe, an der der Termin hängt (mit den Zeilen des Raumchecks); <c>null</c>, wenn es nach dem Termin keine Raumpflege gibt.</summary>
    public async Task<RoomCareTaskDto?> TaskOfAsync(Appointment appointment, CancellationToken ct)
    {
        var zone = await ZoneAsync(appointment.LocationId, ct);
        var day = await DayOfAsync(appointment.LocationId, LocalTime.LocalDate(appointment.Start, zone), zone, ct);
        return day.Tasks.FirstOrDefault(t => t.AppointmentIds.Contains(appointment.Id));
    }

    /// <summary>Zeilen des Raumchecks je Termin (für die Behandlungskarten): ein Aufruf je Standort und Tag; Termine ohne Raumpflege-Aufgabe fehlen.</summary>
    public async Task<Dictionary<Guid, IReadOnlyList<RestockLine>>> RestockOfAsync(IEnumerable<Appointment> appointments, CancellationToken ct)
    {
        var result = new Dictionary<Guid, IReadOnlyList<RestockLine>>();
        foreach (var location in appointments.GroupBy(a => a.LocationId))
        {
            var zone = await ZoneAsync(location.Key, ct);
            foreach (var day in location.GroupBy(a => LocalTime.LocalDate(a.Start, zone)))
            {
                var tasks = (await DayOfAsync(location.Key, day.Key, zone, ct)).Tasks;
                foreach (var a in day)
                {
                    if (tasks.FirstOrDefault(t => t.AppointmentIds.Contains(a.Id)) is { Restock: { } lines })
                    {
                        result[a.Id] = lines;
                    }
                }
            }
        }

        return result;
    }

    private async Task<RoomCareDay> DayOfAsync(Guid locationId, DateOnly date, TimeZoneInfo zone, CancellationToken ct)
    {
        var (from, until) = LocalTime.Day(date, zone);
        var appointments = await db.Appointments.AsNoTracking().Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until).ToListAsync(ct);
        return await DayAsync(locationId, date, zone, appointments, ct);
    }

    public async Task<TimeZoneInfo> ZoneAsync(Guid locationId, CancellationToken ct) =>
        LocalTime.Zone(await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstAsync(ct));

    /// <summary>Hinweise beim Einchecken: Tagesstart des Raums nicht abgeschlossen, Raumpflege nach dem vorigen Termin nicht abgehakt.</summary>
    public async Task<List<string>> CheckInHintsAsync(AppointmentDto appointment, CancellationToken ct)
    {
        var hints = new List<string>();
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == appointment.RoomId, ct);
        var timeZone = await db.Locations.Where(l => l.Id == appointment.LocationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (room is null || timeZone is null)
        {
            return hints;
        }

        var zone = LocalTime.Zone(timeZone);
        var date = LocalTime.LocalDate(appointment.Start, zone);

        var itemIds = await db.RoomStartItems.AsNoTracking().Where(i => i.RoomId == room.Id && i.IsActive).Select(i => i.Id).ToListAsync(ct);
        if (itemIds.Count > 0 && !await db.RoomApprovals.AnyAsync(a => a.RoomId == room.Id && a.Date == date && a.Kind == RoomApprovalKind.DayStart, ct))
        {
            var done = await db.RoomStartChecks.CountAsync(c => c.RoomId == room.Id && c.Date == date && itemIds.Contains(c.ItemId), ct);
            if (done < itemIds.Count)
            {
                hints.Add($"Tagesstart-Kontrolle für {room.Name} ist noch nicht abgeschlossen ({done} von {itemIds.Count} Punkten abgehakt).");
            }
        }

        if (room.CleaningMinutes > 0)
        {
            var (dayStart, _) = LocalTime.Day(date, zone);
            var previous = await db.Appointments.AsNoTracking()
                .Where(p => p.RoomId == room.Id && p.Id != appointment.Id && !Gone.Contains(p.Status) && p.Start >= dayStart && p.BlockedUntil <= appointment.BlockedFrom)
                .OrderByDescending(p => p.BlockedUntil).FirstOrDefaultAsync(ct);
            if (previous is not null)
            {
                var group = db.Appointments.Where(p => p.RoomId == room.Id && p.BlockedUntil == previous.BlockedUntil && !Gone.Contains(p.Status)).Select(p => p.Id);
                if (!await db.RoomCareChecks.AnyAsync(c => group.Contains(c.AppointmentId), ct))
                {
                    hints.Add($"Raumpflege in {room.Name} nach dem Termin um {Clock(previous.End, zone)} ist noch nicht abgehakt.");
                }
            }
        }

        return hints;
    }

    /// <summary>Tagesstart-Kontrolle aller Räume eines Standorts mit Checkliste für einen Tag.</summary>
    public async Task<RoomStartDayDto?> StartDayAsync(Guid locationId, DateOnly date, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
        {
            return null;
        }

        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive).OrderBy(r => r.Name).ToListAsync(ct);
        var roomIds = rooms.Select(r => r.Id).ToList();
        var items = await db.RoomStartItems.AsNoTracking().Where(i => i.IsActive && roomIds.Contains(i.RoomId)).OrderBy(i => i.Position).ToListAsync(ct);
        var ticks = (await db.RoomStartChecks.AsNoTracking().Where(c => c.Date == date && roomIds.Contains(c.RoomId)).ToListAsync(ct)).ToDictionary(c => c.ItemId);
        var approvals = (await db.RoomApprovals.AsNoTracking().Where(a => a.Date == date && a.Kind == RoomApprovalKind.DayStart && roomIds.Contains(a.RoomId))
            .ToListAsync(ct)).ToDictionary(a => a.RoomId);
        var names = await NamesAsync(ticks.Values.Select(c => c.UserId).Concat(approvals.Values.Select(a => a.ApprovedByUserId)), ct);
        var equipment = await restock.EquipmentAsync(roomIds, ct);

        var result = new List<RoomStartRoomDto>();
        foreach (var room in rooms.OrderBy(r => r.Name, StringComparer.Ordinal))
        {
            var roomItems = items.Where(i => i.RoomId == room.Id).ToList();
            var levels = equipment.GetValueOrDefault(room.Id);
            if (roomItems.Count == 0 && levels is null)
            {
                continue;
            }

            approvals.TryGetValue(room.Id, out var approval);
            var states = roomItems.Select(i => ticks.TryGetValue(i.Id, out var tick)
                ? new RoomStartItemStateDto(i.Id, i.Text, true, tick.CheckedAt, Name(names, tick.UserId))
                : new RoomStartItemStateDto(i.Id, i.Text, false, null, null)).ToList();
            var below = levels?.Where(l => l.IsBelow).Select(l => l.ToLine(RestockState.Open)).ToList();
            result.Add(new(room.Id, room.Name, StartState(states.Count, states.Count(s => s.Done), approval is not null), states,
                approval?.ApprovedAt, Name(names, approval?.ApprovedByUserId), below is null ? null : new RoomEquipmentDto(below.Count == 0, below)));
        }

        return new(locationId, date, result);
    }

    /// <summary>
    /// Nimmt den Tagesstart oder die Raumpflege eines Raums an einem Tag ab. Der Tagesstart braucht alle Punkte abgehakt, die Raumpflege alle Aufgaben
    /// des Raums an diesem Tag. Liefert die Abnahme oder den Grund der Ablehnung.
    /// </summary>
    public async Task<(RoomApprovalDto? Approval, string? Error)> ApproveAsync(Guid roomId, DateOnly date, RoomApprovalKind kind, string approvedBy,
        string? recordedBy, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId && r.IsActive, ct);
        if (room is null)
        {
            return (null, "Raum nicht gefunden.");
        }

        var zone = LocalTime.Zone(await db.Locations.Where(l => l.Id == room.LocationId).Select(l => l.TimeZone).FirstAsync(ct));
        var now = time.GetUtcNow();
        if (date > LocalTime.LocalDate(now, zone))
        {
            return (null, "Abgenommen wird heute oder rückwirkend, nicht im Voraus.");
        }

        if (await db.RoomApprovals.AnyAsync(a => a.RoomId == roomId && a.Date == date && a.Kind == kind, ct))
        {
            return (null, $"{(kind == RoomApprovalKind.DayStart ? "Der Tagesstart" : "Die Raumpflege")} von {room.Name} ist an diesem Tag schon abgenommen.");
        }

        if (kind == RoomApprovalKind.DayStart)
        {
            var items = await db.RoomStartItems.AsNoTracking().Where(i => i.RoomId == roomId && i.IsActive).OrderBy(i => i.Position).ToListAsync(ct);
            if (items.Count == 0)
            {
                return (null, $"{room.Name} hat keine Checkliste für den Tagesstart.");
            }

            var done = await db.RoomStartChecks.Where(c => c.RoomId == roomId && c.Date == date).Select(c => c.ItemId).ToListAsync(ct);
            var open = items.Where(i => !done.Contains(i.Id)).Select(i => i.Text).ToList();
            if (open.Count > 0)
            {
                return (null, $"Noch offen: {string.Join(", ", open)}.");
            }
        }
        else
        {
            var (from, until) = LocalTime.Day(date, zone);
            var appointments = await db.Appointments.AsNoTracking()
                .Where(a => a.RoomId == roomId && a.Start >= from && a.Start < until && !Gone.Contains(a.Status)).ToListAsync(ct);
            if (room.CleaningMinutes == 0 || appointments.Count == 0)
            {
                return (null, $"An diesem Tag gibt es in {room.Name} keine Raumpflege.");
            }

            var checks = await db.RoomCareChecks.AsNoTracking().Where(c => c.RoomId == roomId).Select(c => c.AppointmentId).ToListAsync(ct);
            var open = appointments.Where(a => !checks.Contains(a.Id)).Select(a => Clock(a.End, zone)).Distinct().Order().ToList();
            if (open.Count > 0)
            {
                return (null, $"Raumpflege noch nicht abgehakt nach den Terminen um {string.Join(", ", open)}.");
            }
        }

        db.RoomApprovals.Add(new RoomApproval { RoomId = roomId, Date = date, Kind = kind, ApprovedAt = now, ApprovedByUserId = approvedBy, RecordedByUserId = recordedBy });
        await db.SaveChangesAsync(ct);
        var names = await NamesAsync([approvedBy], ct);
        return (new RoomApprovalDto(roomId, date, kind, now, Name(names, approvedBy)), null);
    }

    /// <summary>Hygieneprotokoll: je Tag und Raum die Punkte der Tagesstart-Kontrolle und die Raumpflege-Aufgaben, mit Erledigung und Abnahme; nur bis heute.</summary>
    public async Task<HygieneLogDto?> LogAsync(Guid locationId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        if (await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is not { } timeZone)
        {
            return null;
        }

        var zone = LocalTime.Zone(timeZone);
        var today = LocalTime.LocalDate(time.GetUtcNow(), zone);
        if (until > today)
        {
            until = today;
        }

        var rows = new List<HygieneRowDto>();
        if (from > until)
        {
            return new(locationId, from, until, rows);
        }

        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId).ToDictionaryAsync(r => r.Id, ct);
        var roomIds = rooms.Keys.ToList();
        var items = await db.RoomStartItems.AsNoTracking().Where(i => roomIds.Contains(i.RoomId)).ToDictionaryAsync(i => i.Id, ct);
        var ticks = await db.RoomStartChecks.AsNoTracking().Where(c => c.Date >= from && c.Date <= until && roomIds.Contains(c.RoomId)).ToListAsync(ct);
        var approvals = await db.RoomApprovals.AsNoTracking().Where(a => a.Date >= from && a.Date <= until && roomIds.Contains(a.RoomId)).ToListAsync(ct);
        var (start, _) = LocalTime.Day(from, zone);
        var (_, end) = LocalTime.Day(until, zone);
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= start && a.Start < end && !Gone.Contains(a.Status)).OrderBy(a => a.Start).ToListAsync(ct);
        var careChecks = (await db.RoomCareChecks.AsNoTracking().Where(c => roomIds.Contains(c.RoomId)).ToListAsync(ct)).ToDictionary(c => c.AppointmentId);
        var names = await NamesAsync(ticks.Select(c => c.UserId).Concat(approvals.Select(a => a.ApprovedByUserId)).Concat(careChecks.Values.Select(c => c.UserId)), ct);

        for (var date = from; date <= until; date = date.AddDays(1))
        {
            foreach (var room in rooms.Values.OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                var startApproval = approvals.FirstOrDefault(a => a.RoomId == room.Id && a.Date == date && a.Kind == RoomApprovalKind.DayStart);
                var roomTicks = ticks.Where(c => c.RoomId == room.Id && c.Date == date).ToDictionary(c => c.ItemId);
                var active = items.Values.Where(i => i.RoomId == room.Id && i.IsActive).OrderBy(i => i.Position).ToList();
                // Entfernte Punkte stehen im Protokoll, wenn sie an dem Tag abgehakt wurden.
                foreach (var item in active.Concat(items.Values.Where(i => i.RoomId == room.Id && !i.IsActive && roomTicks.ContainsKey(i.Id))))
                {
                    roomTicks.TryGetValue(item.Id, out var tick);
                    rows.Add(new(date, room.Id, room.Name, RoomApprovalKind.DayStart, item.Text, null, tick is not null, tick?.CheckedAt, Name(names, tick?.UserId),
                        startApproval?.ApprovedAt, Name(names, startApproval?.ApprovedByUserId)));
                }

                var careApproval = approvals.FirstOrDefault(a => a.RoomId == room.Id && a.Date == date && a.Kind == RoomApprovalKind.RoomCare);
                var (dayFrom, dayUntil) = LocalTime.Day(date, zone);
                var live = appointments.Where(a => a.RoomId == room.Id && a.Start >= dayFrom && a.Start < dayUntil && room.CleaningMinutes > 0).ToList();
                foreach (var task in Tasks(live, rooms, zone, careChecks, names))
                {
                    rows.Add(new(date, room.Id, room.Name, RoomApprovalKind.RoomCare, task.Text, task.Until, task.State == RoomCareState.Done, task.DoneAt, task.DoneBy,
                        careApproval?.ApprovedAt, Name(names, careApproval?.ApprovedByUserId)));
                }
            }
        }

        return new(locationId, from, until, rows);
    }

    /// <summary>Aufgaben der Raumpflege der Termine: gleicher Raum und gleiches Ende der Belegung sind eine Aufgabe.</summary>
    private List<RoomCareTaskDto> Tasks(IReadOnlyList<Appointment> live, Dictionary<Guid, Room> rooms, TimeZoneInfo zone,
        IReadOnlyDictionary<Guid, RoomCareCheck> checks, Dictionary<string, string> names)
    {
        var now = time.GetUtcNow();
        return [.. live.GroupBy(a => (a.RoomId, a.BlockedUntil)).Select(g =>
            {
                var first = g.OrderBy(a => a.Start).ThenBy(a => a.Id).First();
                var room = rooms[g.Key.RoomId];
                var careUntil = g.Key.BlockedUntil.AddMinutes(room.CleaningMinutes);
                var done = g.Select(a => checks.GetValueOrDefault(a.Id)).ToList();
                var allDone = done.All(c => c is not null);
                var last = allDone ? done.OfType<RoomCareCheck>().MaxBy(c => c.CheckedAt) : null;
                var state = allDone ? RoomCareState.Done : now > careUntil ? RoomCareState.Overdue : RoomCareState.Open;
                return new RoomCareTaskDto(first.Id, [.. g.Select(a => a.Id).Order()], room.Id, room.Name, $"{room.Name} pflegen bis {Clock(careUntil, zone)}",
                    g.Key.BlockedUntil, careUntil, first.EmployeeId, state, last?.CheckedAt, Name(names, last?.UserId));
            }).OrderBy(t => t.Until).ThenBy(t => t.RoomName, StringComparer.Ordinal)];
    }

    internal static RoomStartState StartState(int items, int checkedCount, bool approved) =>
        items == 0 ? RoomStartState.NoChecklist
        : approved ? RoomStartState.Approved
        : checkedCount >= items ? RoomStartState.Checked
        : checkedCount == 0 ? RoomStartState.Open
        : RoomStartState.Partial;

    private async Task<Dictionary<string, string>> NamesAsync(IEnumerable<string?> userIds, CancellationToken ct)
    {
        var ids = userIds.OfType<string>().Distinct().ToList();
        return ids.Count == 0 ? [] : await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    private static string? Name(Dictionary<string, string> names, string? userId) => userId is not null && names.TryGetValue(userId, out var name) ? name : null;

    private static string Clock(DateTimeOffset instant, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(instant, zone).ToString("HH:mm", German);
}
