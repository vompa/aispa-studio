using AispaStudio.Api.Pos;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Reporting;

/// <summary>Verlauf im Management-Bericht: Umsatz ohne Gutscheinverkauf netto, Behandlungen und Auslastung Personal je Tag, bei langen Zeiträumen je Woche.</summary>
internal sealed partial class ManagementReports
{
    /// <summary>Bis zu dieser Länge zeigt der Verlauf jeden Tag, darüber jede Woche (zwei Monate bleiben täglich lesbar).</summary>
    internal const int DailyTrendDays = 62;

    /// <summary>Abschnitte des Verlaufs: einzelne Tage oder Wochen von Montag bis Sonntag, am Anfang und Ende auf den Zeitraum gekürzt.</summary>
    private static IEnumerable<(DateOnly From, DateOnly Until)> TrendBuckets(DateOnly from, DateOnly until)
    {
        var byWeek = until.DayNumber - from.DayNumber + 1 > DailyTrendDays;
        for (var start = from; start <= until;)
        {
            var sunday = start.AddDays(6 - ((int)start.DayOfWeek + 6) % 7);
            var end = !byWeek ? start : sunday < until ? sunday : until;
            yield return (start, end);
            start = end.AddDays(1);
        }
    }

    /// <summary>
    /// Verlauf aus schon geladenen Daten des Zeitraums: Belegzeilen ohne Wertgutscheinverkauf (Ortstag des Belegs, Bruttobetrag, Steuersatz), stattfindende Termine
    /// (Ortstag des Beginns, Minuten) und Dienste des Standorts (Tag, Minuten). Auslastung wie in der Kennzahl: Behandlungsminuten durch Dienstminuten.
    /// </summary>
    private static List<TrendPoint> Trend(DateOnly from, DateOnly until, IReadOnlyCollection<(DateOnly Day, decimal Gross, decimal TaxPercent)> serviceLines,
        IReadOnlyCollection<(DateOnly Day, double Minutes)> appointments, IReadOnlyCollection<(DateOnly Day, double Minutes)> shifts)
    {
        static bool In(DateOnly day, (DateOnly From, DateOnly Until) bucket) => day >= bucket.From && day <= bucket.Until;

        return [.. TrendBuckets(from, until).Select(bucket =>
        {
            var held = appointments.Where(a => In(a.Day, bucket)).ToList();
            var shiftMinutes = shifts.Where(s => In(s.Day, bucket)).Sum(s => s.Minutes);
            var utilization = shiftMinutes > 0 ? Math.Round((decimal)(held.Sum(a => a.Minutes) / shiftMinutes * 100), 1) : 0;
            var revenue = TaxRates.NetSum(serviceLines.Where(l => In(l.Day, bucket)), l => l.Gross, l => l.TaxPercent);
            return new TrendPoint(bucket.From, bucket.Until, revenue, held.Count, utilization);
        })];
    }
}
