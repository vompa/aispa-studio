using AispaStudio.Contracts;

namespace AispaStudio.Api.Reporting;

/// <summary>Ausfälle im Management-Bericht: entgangener Umsatz durch No-Show und durch Storno, getrennt ausgewiesen.</summary>
internal sealed partial class ManagementReports
{
    /// <summary>Entgangener Umsatz ist die Summe der Terminpreise (brutto) der nicht erschienenen bzw. stornierten Termine; weniger ist besser.</summary>
    private static IEnumerable<Kpi> LossKpis(Period current, Period previous)
    {
        yield return new("lostRevenueNoShow", "Entgangen durch No-Show", current.LostNoShow, previous.LostNoShow, "€", false);
        yield return new("lostRevenueCancellation", "Entgangen durch Storno", current.LostCancellation, previous.LostCancellation, "€", false);
    }
}
