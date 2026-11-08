using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>
/// Prüft Sonderregelungen (R05 Slice 111, E10): Die Regel selbst ist rein, nur das Laden braucht die Datenbank. Der Text für die Warnung nennt den Grund nie, denn den sehen nur Inhaber und Manager.
/// </summary>
internal static class StaffRuleCheck
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <param name="spans">Die neu eingetragenen Zeiten: Ein schon gebuchter später Termin löst beim nächsten nichts aus.</param>
    /// <param name="hoursThatDay">Die Stunden des ganzen Tages samt der neuen Zeiten.</param>
    public static bool Violated(StaffRule rule, DateOnly day, IReadOnlyList<(TimeOnly Start, TimeOnly End)> spans, decimal hoursThatDay)
    {
        if (spans.Count == 0 || !StaffRegulationModel.IsValidOn(day, rule.ValidFrom, rule.ValidUntil))
        {
            return false;
        }

        return rule.Kind switch
        {
            StaffRuleKind.NoAppointmentsAfter => spans.Any(s => s.End > rule.LatestEnd),
            StaffRuleKind.MaxHoursPerDay => hoursThatDay > rule.MaxHours,
            StaffRuleKind.FixedDaysOff => rule.DaysOff.Contains(day.DayOfWeek),
            _ => true,
        };
    }

    /// <summary>Die Regelung in Worten; steht in der Warnung, auf der Karte und im Reiter.</summary>
    public static string Summary(StaffRule rule) => rule.Kind switch
    {
        StaffRuleKind.NoAppointmentsAfter => $"keine Termine nach {rule.LatestEnd:HH\\:mm}",
        StaffRuleKind.MaxHoursPerDay => $"höchstens {rule.MaxHours?.ToString("0.##", German)} Stunden pro Tag",
        StaffRuleKind.FixedDaysOff => $"frei am {Days(rule.DaysOff)}",
        _ => "Schutzfrist",
    };

    public static string Message(string person, IReadOnlyList<StaffRule> rules) =>
        $"{person}: {string.Join("; ", rules.Select(r => $"Sonderregelung „{Summary(r)}“{(r.ValidUntil is { } until ? $", gültig bis {until:dd.MM.yyyy}" : "")}"))}.";

    /// <summary>Warnung für diese Zeiten oder <c>null</c>, wenn keine Regelung betroffen ist.</summary>
    public static async Task<string?> WarningAsync(
        AispaDbContext db, Guid employeeId, string person, DateOnly day, IReadOnlyList<(TimeOnly Start, TimeOnly End)> spans, decimal hoursThatDay, CancellationToken ct)
    {
        var rules = await RulesOnAsync(db, employeeId, day, ct);
        var violated = rules.Where(r => Violated(r, day, spans, hoursThatDay)).ToList();
        return violated.Count == 0 ? null : Message(person, violated);
    }

    /// <summary>Warnung für einen Termin; zählt die offenen Termine des Tages mit, ohne den verschobenen selbst.</summary>
    public static async Task<string?> AppointmentWarningAsync(
        AispaDbContext db, Employee employee, DateTimeOffset start, DateTimeOffset end, TimeZoneInfo zone, Guid? movedId, CancellationToken ct)
    {
        var day = LocalTime.LocalDate(start, zone);
        var rules = await RulesOnAsync(db, employee.Id, day, ct);
        if (rules.Count == 0)
        {
            return null;
        }

        var around = await db.Appointments.AsNoTracking()
            .Where(a => a.EmployeeId == employee.Id && a.Id != movedId && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow
                && a.Start >= start.AddDays(-1) && a.Start <= start.AddDays(1))
            .Select(a => new { a.Start, a.End })
            .ToListAsync(ct);
        var minutes = around.Where(a => LocalTime.LocalDate(a.Start, zone) == day).Sum(a => (a.End - a.Start).TotalMinutes) + (end - start).TotalMinutes;
        var span = (TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(end, zone).DateTime));
        var violated = rules.Where(r => Violated(r, day, [span], (decimal)minutes / 60)).ToList();
        return violated.Count == 0 ? null : Message(employee.PreferredName ?? employee.DisplayName, violated);
    }

    private static Task<List<StaffRule>> RulesOnAsync(AispaDbContext db, Guid employeeId, DateOnly day, CancellationToken ct) =>
        db.Set<StaffRule>().AsNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.ValidFrom <= day && (r.ValidUntil == null || r.ValidUntil >= day))
            .OrderBy(r => r.Kind).ThenBy(r => r.Id)
            .ToListAsync(ct);

    private static string Days(IReadOnlyList<DayOfWeek> days)
    {
        var names = days.OrderBy(d => ((int)d + 6) % 7).Select(d => German.DateTimeFormat.GetDayName(d)).ToList();
        return names.Count < 2 ? string.Concat(names) : $"{string.Join(", ", names[..^1])} und {names[^1]}";
    }
}
