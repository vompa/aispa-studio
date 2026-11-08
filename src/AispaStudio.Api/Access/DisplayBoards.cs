using System.Globalization;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Access;

/// <summary>
/// Die Tafel im Pausenraum (Slice 105): rechnet aus Dienstplan, Terminen und Raumsperren, was die Anzeige zeigt. Sie bekommt nie einen Gastnamen,
/// keine Behandlung, keine Notiz und keinen Preis, nur Zeiten, Initialen, Personen im Haus und Räume.
/// </summary>
internal sealed class DisplayBoards(AispaDbContext db, RosterService roster, TimeProvider time)
{
    private const int NextCount = 8;

    private static readonly AppointmentStatus[] Open = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn];

    public async Task<DisplayBoard?> BuildAsync(Guid locationId, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var now = time.GetUtcNow();
        var date = LocalTime.LocalDate(now, zone);
        var (from, until) = LocalTime.Day(date, zone);

        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer)
            .Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until && Open.Concat(new[] { AppointmentStatus.Completed }).Contains(a.Status))
            .OrderBy(a => a.Start).ToListAsync(ct);
        var employees = await roster.EmployeesAt(locationId, date).AsNoTracking().Include(e => e.WorkingHours).ToListAsync(ct);
        var shifts = RosterService.ShiftsOf(employees, await roster.PlansAsync(employees, date, date, ct), locationId, date, zone);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive && !r.IsArea).OrderBy(r => r.Name).ToListAsync(ct);
        var closures = await db.RoomClosures.AsNoTracking().Where(c => rooms.Select(r => r.Id).Contains(c.RoomId) && c.FromDate <= date && c.UntilDate >= date).ToListAsync(ct);

        var names = employees.ToDictionary(e => e.Id, e => e.InHouseName);
        var people = employees
            .Select(e => Person(e, shifts, appointments, now))
            .Where(p => p.Shifts.Count > 0 || p.Appointments.Count > 0)
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var roomNames = rooms.ToDictionary(r => r.Id, r => r.Name);
        var next = appointments
            .Where(a => Open.Contains(a.Status) && a.Start > now)
            .Take(NextCount)
            .Select(a => new DisplayNextAppointment(a.Start, GuestInitials(a.Customer.FirstName, a.Customer.LastName, a.PlaceholderGuest),
                names.GetValueOrDefault(a.EmployeeId, ""), roomNames.GetValueOrDefault(a.RoomId, "")))
            .ToList();

        return new DisplayBoard(location.Name, location.TimeZone, now, from, until, people,
            [.. rooms.Select(r => Room(r, appointments, closures, zone, now, until))], next);
    }

    /// <summary>„A. H.“: erster Buchstabe von Vor- und Nachname; fehlt einer, bleibt der andere; ein Gast ohne Namen in einer Gruppe heißt „Gast“.</summary>
    public static string GuestInitials(string firstName, string lastName, int? placeholder)
    {
        if (placeholder is not null)
        {
            return "Gast";
        }

        var letters = new[] { firstName, lastName }.Select(FirstLetter).Where(l => l.Length > 0).ToList();
        return letters.Count == 0 ? "Gast" : string.Join(' ', letters.Select(l => l + "."));
    }

    private static string FirstLetter(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length == 0 ? "" : new StringInfo(trimmed).SubstringByTextElements(0, 1).ToUpperInvariant();
    }

    private static DisplayPerson Person(Employee employee, List<EmployeeShift> shifts, List<Appointment> appointments, DateTimeOffset now)
    {
        var own = shifts.Where(s => s.EmployeeId == employee.Id).Select(s => new DisplaySpan(s.Start, s.End)).ToList();
        var mine = appointments.Where(a => a.EmployeeId == employee.Id).Select(a => new DisplaySpan(a.Start, a.End)).ToList();
        var state = mine.Any(a => a.Start <= now && now < a.End) ? DisplayPersonState.InTreatment
            : own.Any(s => s.Start <= now && now < s.End) ? DisplayPersonState.Free
            : DisplayPersonState.OffShift;
        return new(employee.InHouseName, employee.Color, state, own, mine);
    }

    private static DisplayRoom Room(Room room, List<Appointment> appointments, List<RoomClosure> closures, TimeZoneInfo zone, DateTimeOffset now, DateTimeOffset dayEnd)
    {
        var blocks = appointments.Where(a => a.RoomId == room.Id && Open.Contains(a.Status)).Select(a => (From: a.BlockedFrom, Until: a.BlockedUntil))
            .Concat(closures.Where(c => c.RoomId == room.Id).Select(c => (From: c.StartsAt(zone), Until: c.EndsAt(zone))))
            .ToList();
        var covering = blocks.Where(b => b.From <= now && now < b.Until).ToList();
        if (covering.Count > 0)
        {
            // Reicht die Belegung über das Tagesende hinaus, steht kein Ende da (die Tafel zeigt „Tagesende“).
            var busyUntil = covering.Max(b => b.Until);
            return new(room.Name, false, busyUntil >= dayEnd ? null : busyUntil);
        }

        var upcoming = blocks.Where(b => b.From > now && b.From < dayEnd).Select(b => (DateTimeOffset?)b.From).Min();
        return new(room.Name, true, upcoming);
    }
}
