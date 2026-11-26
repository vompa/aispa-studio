using AispaStudio.Contracts;

namespace AispaStudio.Api.Reporting;

/// <summary>Muster im Management-Bericht: Auslastung nach Wochentag und Tageszeit und Termine nach Herkunft.</summary>
internal sealed partial class ManagementReports
{
    /// <summary>
    /// Beginn von Mittag, Nachmittag und Abend (Ortszeit); davor ist Vormittag. 11 bis 14 Uhr fasst die Mittagspause der Gäste (Lunch-Angebote),
    /// 14 bis 17 Uhr den Nachmittag bis zum Arbeitsschluss, ab 17 Uhr kommen die Gäste nach der Arbeit.
    /// </summary>
    internal static readonly TimeOnly[] DaypartStarts = [new(11, 0), new(14, 0), new(17, 0)];

    /// <summary>Unter dieser Auslastung (Prozent) gilt ein Feld als schwach; dieselbe Schwelle wie die Warnung zur Auslastung Personal.</summary>
    internal const decimal WeakLoadBelow = 50;

    private static readonly DayOfWeek[] WeekOrder =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>
    /// Auslastung Personal je Wochentag und Tageszeit aus Behandlungs- und Dienstzeiten in Ortszeit, wie die Kennzahl: Behandlungsminuten ÷ Dienstminuten.
    /// Ein Termin oder Dienst über eine Grenze zählt anteilig in beiden Abschnitten. Wochentage ohne Dienst fehlen; ein Abschnitt ohne Dienst bleibt leer.
    /// </summary>
    private static List<WeekdayLoad> LoadByWeekday(IEnumerable<(DateTime Start, DateTime End)> treatments, IEnumerable<(DateTime Start, DateTime End)> shifts)
    {
        static Dictionary<(DayOfWeek Day, int Part), double> Minutes(IEnumerable<(DateTime Start, DateTime End)> spans) =>
            spans.SelectMany(s => SplitByDaypart(s.Start, s.End)).GroupBy(m => (m.Day, m.Part)).ToDictionary(g => g.Key, g => g.Sum(m => m.Minutes));

        var treated = Minutes(treatments);
        var staffed = Minutes(shifts);
        decimal? Cell(DayOfWeek day, int part) => staffed.GetValueOrDefault((day, part)) is var minutes && minutes > 0
            ? Math.Round((decimal)(treated.GetValueOrDefault((day, part)) / minutes * 100), 1)
            : null;

        return [.. WeekOrder.Where(day => Enumerable.Range(0, DaypartStarts.Length + 1).Any(part => staffed.GetValueOrDefault((day, part)) > 0))
            .Select(day => new WeekdayLoad(day, Cell(day, 0), Cell(day, 1), Cell(day, 2), Cell(day, 3)))];
    }

    /// <summary>Teilt eine Zeitspanne an den Grenzen der Tageszeiten und an Mitternacht: Wochentag, Abschnitt (0 Vormittag bis 3 Abend) und Minuten.</summary>
    private static IEnumerable<(DayOfWeek Day, int Part, double Minutes)> SplitByDaypart(DateTime start, DateTime end)
    {
        for (var cursor = start; cursor < end;)
        {
            var part = DaypartStarts.Count(s => s <= TimeOnly.FromDateTime(cursor));
            var boundary = part < DaypartStarts.Length ? cursor.Date + DaypartStarts[part].ToTimeSpan() : cursor.Date.AddDays(1);
            var stop = boundary < end ? boundary : end;
            yield return (cursor.DayOfWeek, part, (stop - cursor).TotalMinutes);
            cursor = stop;
        }
    }

    /// <summary>Stattfindende Termine nach ihrer gespeicherten Herkunft, die häufigste zuerst. „Rezeption“ umfasst alles, was das Team einträgt, auch Anrufe.</summary>
    private static List<ChannelKpi> Channels(IEnumerable<AppointmentSource> sources) =>
        [.. sources.GroupBy(s => s).Select(g => new ChannelKpi(g.Key, ChannelLabel(g.Key), g.Count()))
            .OrderByDescending(c => c.Appointments).ThenBy(c => c.Source)];

    private static string ChannelLabel(AppointmentSource source) => source switch
    {
        AppointmentSource.Reception => "Rezeption",
        AppointmentSource.Online => "Online-Buchung",
        AppointmentSource.Email => "Mail",
        AppointmentSource.Sms => "SMS",
        AppointmentSource.WhatsApp => "WhatsApp",
        AppointmentSource.Phone => "Telefon (Terminannahme)",
        AppointmentSource.Treatwell => "Treatwell",
        _ => source.ToString(),
    };
}
