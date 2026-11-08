using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Prüft, welche der Angaben, die ein Antwortgeber braucht (<see cref="IAnswerGiver.Fields"/>), im Spa leer sind (ADR 0072, Nachtrag Runde 5b). Die Prüfung steht für alle
/// Themen an einer Stelle und liest nur, was die gefragten Felder brauchen. Felder einer Behandlung kennt nur der Antwortgeber, der die Behandlung gefunden hat.
/// </summary>
internal sealed class FieldGaps(AispaDbContext db)
{
    public async Task<IReadOnlyList<FieldGap>> FindAsync(IReadOnlyCollection<string> ids, AnswerContext context, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var locationId = context.Location.Id;
        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).FirstAsync(l => l.Id == locationId, ct);
        var gaps = new List<FieldGap>();
        foreach (var id in ids)
        {
            if (await IsEmptyAsync(id, location, ct))
            {
                gaps.Add(new FieldGap(id));
            }
        }

        return gaps;
    }

    private async Task<bool> IsEmptyAsync(string id, Location location, CancellationToken ct) => id switch
    {
        FieldIds.OpeningHours => location.OpeningHours.Count == 0,
        FieldIds.Region => string.IsNullOrWhiteSpace(location.Region),
        FieldIds.Phone => string.IsNullOrWhiteSpace(location.Phone),
        FieldIds.Email => string.IsNullOrWhiteSpace(location.Email),
        FieldIds.Street => string.IsNullOrWhiteSpace(location.Street),
        FieldIds.Directions => string.IsNullOrWhiteSpace(location.Directions),
        FieldIds.CancellationPolicy => !await db.LocationOnlineSettings.AsNoTracking()
            .AnyAsync(s => s.LocationId == location.Id && s.CancellationPolicy != null && s.CancellationPolicy.Trim() != "", ct),
        FieldIds.Rooms => !await db.Rooms.AsNoTracking().AnyAsync(r => r.LocationId == location.Id && r.IsActive && !r.IsArea, ct),
        FieldIds.Areas => !await db.Rooms.AsNoTracking().AnyAsync(r => r.LocationId == location.Id && r.IsActive && r.IsArea, ct),
        FieldIds.Website => string.IsNullOrWhiteSpace(await db.Tenants.AsNoTracking().Select(t => t.Website).FirstOrDefaultAsync(ct)),
        FieldIds.Promotions => !await db.Promotions.AsNoTracking().AnyAsync(p => p.ArchivedAt == null, ct),
        _ => throw new ArgumentException($"Feldkennung {id} lässt sich nicht allgemein prüfen.", nameof(id)),
    };
}
