using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Soll-Ist-Verbrauch je Artikel (Slice 116). Soll: gebuchter Verbrauch laut Rezept und Extras (Art Verbrauch). Mehrverbrauch: was die Inventuren
/// darüber hinaus fehlen sahen (Differenzen der Art Inventur mit umgekehrtem Vorzeichen). Ist = Soll + Mehrverbrauch. Korrekturen (Bruch mit Grund)
/// und Verkäufe zählen nicht: sie sind erklärt bzw. kein Verbrauch.
/// </summary>
internal sealed class StockUsageReport(AispaDbContext db)
{
    public const int MaxDays = 366;

    /// <summary>Eine Bewegung, wie die Rechnung sie braucht.</summary>
    internal sealed record Row(Guid ItemId, StockMovementKind Kind, decimal Quantity, Guid? AppointmentId);

    internal sealed record ItemInfo(string Name, string Unit, decimal UnitCost);

    public async Task<StockUsageReportDto?> ReportAsync(Guid locationId, DateOnly from, DateOnly until, bool amounts, CancellationToken ct)
    {
        if (await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is not { } timeZone)
        {
            return null;
        }

        var zone = LocalTime.Zone(timeZone);
        var (start, _) = LocalTime.Day(from, zone);
        var (_, end) = LocalTime.Day(until, zone);
        var rows = await db.StockMovements.AsNoTracking()
            .Where(m => m.LocationId == locationId && m.At >= start && m.At < end
                && (m.Kind == StockMovementKind.Consumption || m.Kind == StockMovementKind.Count))
            .Select(m => new Row(m.ItemId, m.Kind, m.Quantity, m.AppointmentId)).ToListAsync(ct);
        var itemIds = rows.Select(r => r.ItemId).Distinct().ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => new ItemInfo(i.Name, i.Unit, i.UnitCost), ct);
        var appointmentIds = rows.Where(r => r.AppointmentId != null).Select(r => r.AppointmentId!.Value).Distinct().ToList();
        var treatmentOf = await db.Appointments.AsNoTracking().Where(a => appointmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.TreatmentId, ct);
        var treatmentIds = treatmentOf.Values.Distinct().ToList();
        var treatmentNames = await db.Treatments.AsNoTracking().Where(t => treatmentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var lines = Compute(rows, items, treatmentOf, treatmentNames);
        return new(locationId, from, until, amounts ? lines : [.. lines.Select(l => l with { ExcessValue = null })],
            amounts ? decimal.Round(lines.Sum(l => l.ExcessValue ?? 0), 2) : null, !amounts);
    }

    /// <summary>Reine Rechnung; Artikel ohne Soll und ohne Mehrverbrauch erscheinen nicht.</summary>
    internal static List<StockUsageLineDto> Compute(IReadOnlyList<Row> rows, IReadOnlyDictionary<Guid, ItemInfo> items,
        IReadOnlyDictionary<Guid, Guid> treatmentOf, IReadOnlyDictionary<Guid, string> treatmentNames)
    {
        var lines = new List<StockUsageLineDto>();
        foreach (var group in rows.GroupBy(r => r.ItemId).Where(g => items.ContainsKey(g.Key)))
        {
            var item = items[group.Key];
            var planned = -group.Where(r => r.Kind == StockMovementKind.Consumption).Sum(r => r.Quantity);
            var excess = -group.Where(r => r.Kind == StockMovementKind.Count).Sum(r => r.Quantity);
            if (planned == 0 && excess == 0)
            {
                continue;
            }

            lines.Add(new(group.Key, item.Name, item.Unit, planned, planned + excess, excess, decimal.Round(excess * item.UnitCost, 2),
                Treatments(group, treatmentOf, treatmentNames)));
        }

        return [.. lines.OrderBy(l => l.Name, StringComparer.Ordinal).ThenBy(l => l.ItemId)];
    }

    /// <summary>Behandlungen, die den Artikel netto verbraucht haben: je Termin Soll (Stornos heben den Verbrauch auf und zählen nicht), je Behandlung Summe und Terminzahl.</summary>
    private static List<StockUsageTreatmentDto> Treatments(IEnumerable<Row> rows, IReadOnlyDictionary<Guid, Guid> treatmentOf,
        IReadOnlyDictionary<Guid, string> treatmentNames) =>
        [.. rows.Where(r => r.Kind == StockMovementKind.Consumption && r.AppointmentId is { } id && treatmentOf.ContainsKey(id))
            .GroupBy(r => r.AppointmentId!.Value)
            .Select(g => (TreatmentId: treatmentOf[g.Key], Planned: -g.Sum(r => r.Quantity)))
            .Where(a => a.Planned > 0)
            .GroupBy(a => a.TreatmentId)
            .Select(g => new StockUsageTreatmentDto(g.Key, treatmentNames.GetValueOrDefault(g.Key, ""), g.Sum(a => a.Planned), g.Count()))
            .OrderBy(t => t.Name, StringComparer.Ordinal).ThenBy(t => t.TreatmentId)];
}
