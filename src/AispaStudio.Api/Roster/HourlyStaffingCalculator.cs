using AispaStudio.Contracts;

namespace AispaStudio.Api.Roster;

/// <summary>Zeitspanne eines Tages in Minuten ab Mitternacht der Ortszeit, Ende exklusiv.</summary>
internal readonly record struct MinuteSpan(int From, int Until);

/// <summary>Ein Tag für die Besetzung: Öffnungsfenster und je Person ihre Dienste an diesem Standort.</summary>
internal sealed record StaffingDay(DateOnly Date, IReadOnlyList<MinuteSpan> Open, IReadOnlyList<IReadOnlyList<MinuteSpan>> PeopleShifts);

/// <summary>Besetzung je Tag und voller Stunde (reine Rechnung, ohne Datenbank).</summary>
internal static class HourlyStaffingCalculator
{
    /// <summary>Mindestdauer des Dienstes in einer Stunde, damit die Person dort zählt.</summary>
    private const int MinimumMinutes = 30;

    /// <summary>
    /// Stundenbereich: von der frühesten Öffnung bis zum spätesten Schluss der Woche, erweitert um Dienste außerhalb.
    /// Leer, wenn kein Tag geöffnet ist oder niemand Dienst hat.
    /// </summary>
    public static IReadOnlyList<HourlyStaffing> Compute(IReadOnlyList<StaffingDay> days)
    {
        var open = days.SelectMany(d => d.Open).Where(Valid).ToList();
        var shifts = days.SelectMany(d => d.PeopleShifts.SelectMany(p => p)).Where(Valid).ToList();
        if (open.Count == 0 || shifts.Count == 0)
        {
            return [];
        }

        var spans = open.Concat(shifts).ToList();
        var first = spans.Min(s => s.From / 60);
        var last = spans.Max(s => (s.Until - 1) / 60);

        return
        [
            .. days.SelectMany(day => Enumerable.Range(first, last - first + 1).Select(hour =>
                new HourlyStaffing(day.Date, hour, day.Open.Where(Valid).Any(w => Overlap(w, hour) > 0), day.PeopleShifts.Count(p => p.Where(Valid).Sum(s => Overlap(s, hour)) >= MinimumMinutes)))),
        ];
    }

    private static bool Valid(MinuteSpan span) => span.From < span.Until;

    private static int Overlap(MinuteSpan span, int hour) => Math.Max(0, Math.Min(span.Until, (hour + 1) * 60) - Math.Max(span.From, hour * 60));
}
