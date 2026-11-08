using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Vorschlag für die Vorlieben aus den erledigten Besuchen: Person, Wochentag und Tageszeit, wenn mindestens drei Besuche da sind und
/// mindestens 60 % davon gleich sind. Eine gemeinsame Buchung (Spa-Tag) zählt einmal, mit ihrem ersten Teil.
/// </summary>
internal static class CustomerPreferenceHistory
{
    private const int MinVisits = 3;
    private const double Share = 0.6;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private static readonly (string Name, TimeOnly From, TimeOnly Until)[] DayParts =
    [
        ("vormittags", new(8, 0), new(12, 0)),
        ("nachmittags", new(12, 0), new(17, 0)),
        ("abends", new(17, 0), new(22, 0)),
    ];

    public static async Task<CustomerPreferenceSuggestion> SuggestAsync(AispaDbContext db, Guid customerId, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddYears(-2);
        var rows = await db.Appointments.AsNoTracking()
            .Where(a => a.CustomerId == customerId && a.Status == AppointmentStatus.Completed && a.Start >= since)
            .Join(db.Locations, a => a.LocationId, l => l.Id, (a, l) => new { a.Id, a.GroupId, a.Start, a.EmployeeId, l.TimeZone })
            .ToListAsync(ct);
        var visits = rows.GroupBy(r => r.GroupId ?? r.Id).Select(g => g.OrderBy(r => r.Start).First()).ToList();
        if (visits.Count < MinVisits)
        {
            return new(visits.Count, null, null, 0, null, 0, null, null, null);
        }

        var parts = new List<string>();
        var count = visits.Count;
        var employee = visits.GroupBy(v => v.EmployeeId).OrderByDescending(g => g.Count()).First();
        string? employeeName = null;
        Guid? employeeId = null;
        if (employee.Count() >= count * Share)
        {
            employeeId = employee.Key;
            employeeName = await db.Employees.AsNoTracking().Where(e => e.Id == employee.Key).Select(e => e.DisplayName).FirstOrDefaultAsync(ct);
            parts.Add($"{employee.Count()} von {count} Besuchen bei {employeeName}");
        }

        var local = visits.Select(v => TimeZoneInfo.ConvertTime(v.Start, TimeZoneInfo.FindSystemTimeZoneById(v.TimeZone)).DateTime).ToList();
        var weekday = local.GroupBy(t => t.DayOfWeek).OrderByDescending(g => g.Count()).First();
        DayOfWeek? day = weekday.Count() >= count * Share ? weekday.Key : null;
        if (day is { } d)
        {
            parts.Add($"meist {German.DateTimeFormat.GetDayName(d)}");
        }

        var part = DayParts.Select(p => (p, n: local.Count(t => TimeOnly.FromDateTime(t) is var at && at >= p.From && at < p.Until)))
            .OrderByDescending(x => x.n).First();
        var hasPart = part.n >= count * Share;
        if (hasPart)
        {
            parts.Add($"meist {part.p.Name}");
        }

        return new(count, employeeId, employeeName, employeeId is null ? 0 : employee.Count(), day, day is null ? 0 : weekday.Count(),
            hasPart ? part.p.From : null, hasPart ? part.p.Until : null, parts.Count == 0 ? null : string.Join(" · ", parts));
    }
}
