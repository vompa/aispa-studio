using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>Ertrag und Verkauf im Management-Bericht: Deckungsbeitrag, Umsatz je Dienst- und Behandlungsstunde, Produktanteil, Umsatz je Gast.</summary>
internal sealed partial class ManagementReports
{
    /// <summary>Kennzahlen in Prozent, die aus Geldbeträgen entstehen; sie fehlen ohne das Recht „Berichte mit Beträgen“ wie die Beträge selbst.</summary>
    private static readonly HashSet<string> AmountShares = ["contributionMarginRate", "productShare"];

    /// <summary>
    /// Deckungsbeitrag der Termine: je Termin der netto kassierte Betrag (<see cref="NetByAppointment"/>) minus die Vollkosten seines Monats; <c>Basis</c> ist der Umsatz
    /// derselben Termine. Termine, für deren Behandlung keine Vollkosten vorliegen, zählen nicht mit (wie bei der Marge je Termin).
    /// </summary>
    private async Task<(decimal Margin, decimal Basis)> ContributionAsync(Guid locationId, TimeZoneInfo zone, IReadOnlyCollection<(Guid Id, Guid TreatmentId, DateTimeOffset Start)> held,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var registerIds = await db.CashRegisters.Where(r => r.LocationId == locationId).Select(r => r.Id).ToListAsync(ct);
        var lines = await db.Receipts.AsNoTracking()
            .Where(r => registerIds.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
            .SelectMany(r => r.Lines)
            .ToListAsync(ct);
        var net = NetByAppointment(lines);
        var unitCosts = await UnitCostsAsync(locationId, held.Select(a => LocalTime.LocalDate(a.Start, zone)), ct);
        var costed = held
            .Select(a => (Net: net.GetValueOrDefault(a.Id), Cost: CostOf(unitCosts, LocalTime.LocalDate(a.Start, zone), a.TreatmentId)))
            .Where(a => a.Cost is not null)
            .ToList();
        return (Math.Round(costed.Sum(a => a.Net - a.Cost!.Total), 2), costed.Sum(a => a.Net));
    }

    /// <summary>Vollkosten je Behandlung für jeden Monat, in dem einer der Tage liegt: Ein Zeitraum über zwei Monate rechnet jeden Termin mit den Stückkosten seines Monats.</summary>
    private async Task<Dictionary<(int Year, int Month), Dictionary<Guid, TreatmentCostDto>>> UnitCostsAsync(Guid locationId, IEnumerable<DateOnly> days, CancellationToken ct)
    {
        var unitCosts = new Dictionary<(int Year, int Month), Dictionary<Guid, TreatmentCostDto>>();
        foreach (var (year, month) in days.Select(d => (d.Year, d.Month)).Distinct())
        {
            unitCosts[(year, month)] = (await costs.AnalyzeAsync(locationId, year, month, ct))?.Treatments.ToDictionary(t => t.TreatmentId) ?? [];
        }

        return unitCosts;
    }

    private static TreatmentCostDto? CostOf(Dictionary<(int Year, int Month), Dictionary<Guid, TreatmentCostDto>> unitCosts, DateOnly day, Guid treatmentId) =>
        unitCosts[(day.Year, day.Month)].GetValueOrDefault(treatmentId);

    /// <summary>
    /// Ertrag und Verkauf eines Zeitraums. Umsatz heißt hier Umsatz ohne Wertgutscheinverkauf netto (<c>serviceRevenueNet</c>); der Deckungsbeitrag in Prozent bezieht sich auf
    /// den Umsatz der Termine, die er enthält, der Produktanteil auf den ganzen Umsatz ohne Gutscheinverkauf.
    /// </summary>
    private static IEnumerable<Kpi> EarningsKpis(Period current, Period previous)
    {
        static decimal Per(decimal amount, decimal basis) => basis == 0 ? 0 : Math.Round(amount / basis, 2);
        static decimal Share(decimal part, decimal whole) => whole == 0 ? 0 : Math.Round(part * 100 / whole, 1);

        yield return new("contributionMargin", "Deckungsbeitrag", current.ContributionMargin, previous.ContributionMargin, "€", true);
        yield return new("contributionMarginRate", "Deckungsbeitrag in Prozent", Share(current.ContributionMargin, current.ContributionBasis),
            Share(previous.ContributionMargin, previous.ContributionBasis), "%", true);
        yield return new("serviceRevenuePerShiftHour", "Umsatz je Dienststunde", Per(current.ServiceNet, current.ShiftHours), Per(previous.ServiceNet, previous.ShiftHours), "€", true);
        yield return new("serviceRevenuePerTreatmentHour", "Umsatz je Behandlungsstunde", Per(current.ServiceNet, current.TreatmentHours),
            Per(previous.ServiceNet, previous.TreatmentHours), "€", true);
        yield return new("productShare", "Produktanteil am Umsatz", Share(current.ProductNet, current.ServiceNet), Share(previous.ProductNet, previous.ServiceNet), "%", true);
        yield return new("productRevenuePerAppointment", "Produktumsatz je Behandlung", Per(current.ProductNet, current.Appointments),
            Per(previous.ProductNet, previous.Appointments), "€", true);
        yield return new("revenuePerGuest", "Umsatz je Gast", Per(current.ServiceNet, current.Customers), Per(previous.ServiceNet, previous.Customers), "€", true);
    }
}
