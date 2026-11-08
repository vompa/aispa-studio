using AispaStudio.Api.Roster;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Pausen im Kalender nach dem Arbeitszeitrecht des Landes. Eine Pause ist eine Sperrzeit der Person, deren Grund mit „Pause“ beginnt;
/// die Lücke zwischen zwei Diensten am selben Tag zählt ebenfalls. Der Kalender warnt, wenn die verlangte Pause fehlt und zwischen
/// Terminen und Sperrzeiten keine zusammenhängende Zeit mehr dafür frei ist.
/// </summary>
internal static class CalendarBreaks
{
    public static bool IsBreak(string? reason) => reason?.TrimStart().StartsWith("Pause", StringComparison.OrdinalIgnoreCase) == true;

    /// <param name="busy">Was die Person belegt: Termine mit Vor- und Nachbereitung und alle Sperrzeiten (auch Pausen).</param>
    /// <param name="breaks">Sperrzeiten, die Pausen sind.</param>
    public static List<CalendarBreak> For(
        IReadOnlyList<EmployeeShift> shifts,
        IReadOnlyList<(Guid EmployeeId, DateTimeOffset From, DateTimeOffset Until)> busy,
        IReadOnlyList<(Guid EmployeeId, DateTimeOffset From, DateTimeOffset Until)> breaks,
        WorkingTimeRules rules)
    {
        var result = new List<CalendarBreak>();
        foreach (var own in shifts.GroupBy(s => s.EmployeeId))
        {
            var spans = own.OrderBy(s => s.Start).ToList();
            var shiftMinutes = spans.Sum(s => (s.End - s.Start).TotalMinutes);
            var pauseMinutes = breaks.Where(b => b.EmployeeId == own.Key).Sum(b => OverlapWithShifts(spans, b.From, b.Until));
            var gapMinutes = spans.Zip(spans.Skip(1)).Sum(p => Math.Max(0, (p.Second.Start - p.First.End).TotalMinutes));
            var worked = shiftMinutes - pauseMinutes;
            var required = rules.RequiredBreakMinutes(worked / 60);
            if (required == 0)
            {
                continue;
            }

            var planned = (int)Math.Round(pauseMinutes + gapMinutes);
            string? warning = null;
            if (planned < required)
            {
                var missing = required - planned;
                var taken = busy.Where(b => b.EmployeeId == own.Key).OrderBy(b => b.From).ToList();
                if (LongestFree(spans, taken) < missing)
                {
                    warning = $"Keine Zeit mehr für die gesetzliche Pause: {required} Min. bei {worked / 60:0.#} Std. Dienst"
                        + (planned > 0 ? $", {planned} Min. eingetragen." : ".");
                }
            }

            result.Add(new CalendarBreak(own.Key, required, planned, warning));
        }

        return result;
    }

    private static double OverlapWithShifts(List<EmployeeShift> spans, DateTimeOffset from, DateTimeOffset until) =>
        spans.Sum(s => Overlap(s.Start, s.End, from, until));

    private static double Overlap(DateTimeOffset aFrom, DateTimeOffset aUntil, DateTimeOffset bFrom, DateTimeOffset bUntil) =>
        Math.Max(0, ((aUntil < bUntil ? aUntil : bUntil) - (aFrom > bFrom ? aFrom : bFrom)).TotalMinutes);

    /// <summary>Längste zusammenhängende freie Zeit in Minuten innerhalb der Dienste.</summary>
    private static double LongestFree(List<EmployeeShift> shifts, List<(Guid EmployeeId, DateTimeOffset From, DateTimeOffset Until)> taken)
    {
        var longest = 0.0;
        foreach (var shift in shifts)
        {
            var cursor = shift.Start;
            foreach (var (_, from, until) in taken.Where(t => t.From < shift.End && shift.Start < t.Until))
            {
                if (from > cursor)
                {
                    longest = Math.Max(longest, (from - cursor).TotalMinutes);
                }

                if (until > cursor)
                {
                    cursor = until;
                }
            }

            if (shift.End > cursor)
            {
                longest = Math.Max(longest, (shift.End - cursor).TotalMinutes);
            }
        }

        return longest;
    }
}
