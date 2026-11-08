using AispaStudio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Was eine Behandlung je Termin im Lager verbraucht: das Material im Rezept (Kosten) plus die Extras von „Zum Termin bereitstellen“, jeweils nur
/// mit Lagerartikel. Eine einzige Quelle für die Abbuchung, den geplanten Verbrauch und die Bedarfsprüfung, damit nichts doppelt gebucht wird.
/// </summary>
internal static class StockRecipes
{
    public static async Task<Dictionary<Guid, Dictionary<Guid, decimal>>> ForAsync(AispaDbContext db, IReadOnlyCollection<Guid> treatmentIds, CancellationToken ct)
    {
        var materials = await db.TreatmentMaterials.AsNoTracking().Where(m => treatmentIds.Contains(m.TreatmentId) && m.StockItemId != null)
            .Select(m => new { m.TreatmentId, ItemId = m.StockItemId!.Value, m.Quantity }).ToListAsync(ct);
        var extras = await db.TreatmentExtras.AsNoTracking().Where(x => treatmentIds.Contains(x.TreatmentId) && x.StockItemId != null)
            .Select(x => new { x.TreatmentId, ItemId = x.StockItemId!.Value, x.Quantity }).ToListAsync(ct);
        return materials.Concat(extras).GroupBy(r => r.TreatmentId)
            .ToDictionary(g => g.Key, g => g.GroupBy(r => r.ItemId).ToDictionary(x => x.Key, x => x.Sum(r => r.Quantity)));
    }

    public static async Task<Dictionary<Guid, decimal>> ForAsync(AispaDbContext db, Guid treatmentId, CancellationToken ct) =>
        (await ForAsync(db, [treatmentId], ct)).GetValueOrDefault(treatmentId) ?? [];
}
