using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Roster;

/// <summary>Liest, wer wann wo Dienst hat: Dienstplan für einzelne Tage vor Wochenarbeitszeit.</summary>
internal sealed class RosterService(AispaDbContext db, TenantContext tenants)
{
    /// <summary>Arbeitszeitgrenzen nach dem Land des eigenen Unternehmens.</summary>
    public async Task<WorkingTimeRules> RulesAsync(CancellationToken ct)
    {
        var tenantId = tenants.TenantId;
        var country = tenantId is null ? null : await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Country).FirstOrDefaultAsync(ct);
        return WorkingTimeRules.For(country);
    }

    /// <summary>
    /// Aktive Personen, die am Standort regelmäßig arbeiten oder im Dienstplan dort eingeteilt sind (ab <paramref name="since"/>);
    /// mit <paramref name="drafts"/> auch die, die nur ein Entwurf dort einteilt.
    /// </summary>
    public IQueryable<Employee> EmployeesAt(Guid locationId, DateOnly since, bool drafts = false) =>
        db.Employees.Where(e => e.IsActive
            && (e.WorkingHours.Any(w => w.LocationId == locationId)
                || db.RosterDays.Any(d => d.EmployeeId == e.Id && d.Date >= since && d.Entries.Any(x => x.LocationId == locationId))
                || (drafts && db.RosterDrafts.Any(d => d.EmployeeId == e.Id && d.Date >= since && d.Entries.Any(x => x.LocationId == locationId)))));

    /// <summary>
    /// Geltende Pläne je Person und Tag im Zeitraum [from, until]. Mit <paramref name="drafts"/> so, wie sie nach dem Veröffentlichen
    /// aller Entwürfe wären (für die Planung); Kalender und Buchung verwenden immer den veröffentlichten Stand.
    /// </summary>
    public async Task<Dictionary<(Guid EmployeeId, DateOnly Date), DayPlan>> PlansAsync(
        IReadOnlyCollection<Employee> employees, DateOnly from, DateOnly until, CancellationToken ct, bool drafts = false)
    {
        var ids = employees.Select(e => e.Id).ToList();
        var planned = await db.RosterDays.AsNoTracking().Include(d => d.Entries)
            .Where(d => ids.Contains(d.EmployeeId) && d.Date >= from && d.Date <= until)
            .ToDictionaryAsync(d => (d.EmployeeId, d.Date), ct);
        var drafted = drafts
            ? await db.RosterDrafts.AsNoTracking().Include(d => d.Entries)
                .Where(d => ids.Contains(d.EmployeeId) && d.Date >= from && d.Date <= until)
                .ToDictionaryAsync(d => (d.EmployeeId, d.Date), ct)
            : [];

        var plans = new Dictionary<(Guid, DateOnly), DayPlan>();
        foreach (var employee in employees)
        {
            for (var date = from; date <= until; date = date.AddDays(1))
            {
                plans[(employee.Id, date)] = drafted.TryGetValue((employee.Id, date), out var draft)
                    ? DayPlan.For(employee, draft)
                    : DayPlan.For(employee, planned.GetValueOrDefault((employee.Id, date)), date);
            }
        }

        return plans;
    }

    /// <summary>Dienste am Standort an einem Tag, in UTC. Grundlage für Kalender und Buchung.</summary>
    public async Task<List<EmployeeShift>> ShiftsAsync(Guid locationId, DateOnly date, TimeZoneInfo zone, CancellationToken ct)
    {
        var employees = await EmployeesAt(locationId, date).AsNoTracking().Include(e => e.WorkingHours).ToListAsync(ct);
        var plans = await PlansAsync(employees, date, date, ct);
        return ShiftsOf(employees, plans, locationId, date, zone);
    }

    /// <summary>Die Dienste am Standort aus den geltenden Plänen der Personen (reine Rechnung, auch für das Einspielen der Beispieldaten).</summary>
    internal static List<EmployeeShift> ShiftsOf(IEnumerable<Employee> employees, IReadOnlyDictionary<(Guid EmployeeId, DateOnly Date), DayPlan> plans,
        Guid locationId, DateOnly date, TimeZoneInfo zone) =>
        [.. employees
            .SelectMany(e => plans[(e.Id, date)].Shifts.Where(s => s.LocationId == locationId)
                .Select(s => new EmployeeShift(e.Id, LocalTime.ToUtc(date, s.Start!.Value, zone), LocalTime.ToUtc(date, s.End!.Value, zone))))
            .OrderBy(s => s.Start)];
}
