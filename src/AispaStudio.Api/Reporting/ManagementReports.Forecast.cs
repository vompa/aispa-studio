using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>Vorschau im Management-Bericht: gebuchter Stand der nächsten vier Wochen, rein gerechnet aus Kalender und Dienstplan, ohne KI.</summary>
internal sealed partial class ManagementReports
{
    /// <summary>So viele Wochen zeigt die Vorschau.</summary>
    internal const int ForecastWeeks = 4;

    /// <summary>
    /// Je sieben Tage ab <paramref name="today"/> (gleich lange Abschnitte, damit die Wochen vergleichbar sind): stattfindende Termine, ihr Terminpreis brutto und
    /// Behandlungsminuten ÷ geplante Dienstminuten des Standorts. Heute zählt ganz, auch schon vergangene Termine des Tages.
    /// </summary>
    private async Task<List<ForecastWeek>> ForecastAsync(Guid locationId, TimeZoneInfo zone, DateOnly today, CancellationToken ct)
    {
        var last = today.AddDays(ForecastWeeks * 7 - 1);
        var (start, _) = LocalTime.Day(today, zone);
        var (_, end) = LocalTime.Day(last, zone);
        var booked = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= start && a.Start < end && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .Select(a => new { a.Start, a.End, a.Price })
            .ToListAsync(ct);
        var employees = await roster.EmployeesAt(locationId, today).AsNoTracking().Include(e => e.WorkingHours).ToListAsync(ct);
        var plans = await roster.PlansAsync(employees, today, last, ct);

        return [.. Enumerable.Range(0, ForecastWeeks).Select(week =>
        {
            var from = today.AddDays(week * 7);
            var until = from.AddDays(6);
            var held = booked.Where(a => LocalTime.LocalDate(a.Start, zone) is var day && day >= from && day <= until).ToList();
            var shiftMinutes = plans.Where(p => p.Key.Item2 >= from && p.Key.Item2 <= until)
                .SelectMany(p => p.Value.Shifts.Where(s => s.LocationId == locationId)).Sum(s => (s.End!.Value - s.Start!.Value).TotalMinutes);
            var utilization = shiftMinutes > 0 ? Math.Round((decimal)(held.Sum(a => (a.End - a.Start).TotalMinutes) / shiftMinutes * 100), 1) : 0;
            return new ForecastWeek(from, until, held.Count, held.Sum(a => a.Price), utilization);
        })];
    }
}
