using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Stock;

/// <summary>„Passt dazu“: ein Verkaufsartikel, den eine Behandlung empfiehlt (Slice 115); die Reihenfolge ist die der Behandlungsmaske.</summary>
public sealed class TreatmentRecommendedArticle
{
    public Guid TreatmentId { get; init; }
    public Guid ArticleId { get; init; }
    public int Position { get; init; }
}

/// <summary>
/// Vorschläge „Passt dazu“ für Termin-Panel und Kassa: die empfohlenen Artikel der Behandlungen, aber nur mit Bestand am Verkaufsort des Standorts.
/// Der Verkaufsort ist derselbe, von dem die Kassa beim Bezahlen abbucht (<see cref="StockService.SalesPlace"/>); ohne Lagerartikel ist kein
/// Bestand belegt, dann gibt es keinen Vorschlag. Eine Provision auf Produktverkauf gibt es nicht (E12).
/// </summary>
internal static class ProductRecommendations
{
    public const int MaxPerTreatment = 5;

    public static async Task<List<RecommendedArticleDto>> ForAsync(AispaDbContext db, Guid locationId, IReadOnlyList<Guid> treatmentIds,
        IReadOnlyCollection<Guid> excludedArticleIds, CancellationToken ct)
    {
        var mappings = await db.TreatmentRecommendedArticles.AsNoTracking().Where(r => treatmentIds.Contains(r.TreatmentId)).ToListAsync(ct);
        var articleIds = mappings
            .OrderBy(r => IndexOf(treatmentIds, r.TreatmentId)).ThenBy(r => r.Position)
            .Select(r => r.ArticleId).Where(id => !excludedArticleIds.Contains(id)).Distinct().ToList();
        if (articleIds.Count == 0)
        {
            return [];
        }

        var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId).ToListAsync(ct);
        var salesPlaceId = StockService.SalesPlace(places).Id;
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id) && a.IsActive).ToDictionaryAsync(a => a.Id, ct);
        var items = await db.StockItems.AsNoTracking().Where(i => i.ArticleId != null && articleIds.Contains(i.ArticleId.Value) && i.IsActive)
            .ToDictionaryAsync(i => i.ArticleId!.Value, ct);
        var itemIds = items.Values.Select(i => i.Id).ToList();
        var onShelf = await db.StockMovements.AsNoTracking().Where(m => m.StoragePlaceId == salesPlaceId && itemIds.Contains(m.ItemId))
            .GroupBy(m => m.ItemId).Select(g => new { ItemId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Quantity, ct);

        return [.. articleIds
            .Where(id => articles.ContainsKey(id) && items.ContainsKey(id) && onShelf.GetValueOrDefault(items[id].Id) > 0)
            .Select(id => new RecommendedArticleDto(id, articles[id].Name, articles[id].Price, onShelf[items[id].Id], items[id].Unit))];
    }

    private static int IndexOf(IReadOnlyList<Guid> ids, Guid id)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return i;
            }
        }

        return ids.Count;
    }
}

internal static class ProductRecommendationEndpoints
{
    public static IEndpointRouteBuilder MapProductRecommendationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Lager");

        group.MapGet("/treatments/{treatmentId:guid}/recommended-articles", async (Guid treatmentId, AispaDbContext db, CancellationToken ct) =>
                await db.TreatmentRecommendedArticles.AsNoTracking().Where(r => r.TreatmentId == treatmentId).OrderBy(r => r.Position)
                    .Select(r => r.ArticleId).ToListAsync(ct))
            .WithName("ListTreatmentRecommendedArticles")
            .WithSummary("„Passt dazu“: Verkaufsartikel, die Termin-Panel und Kassa zu dieser Behandlung vorschlagen, in ihrer Reihenfolge.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("/treatments/{treatmentId:guid}/recommended-articles", async Task<Results<Ok<List<Guid>>, NotFound, ValidationProblem>> (
                Guid treatmentId, List<Guid> articleIds, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Treatments.AnyAsync(t => t.Id == treatmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var known = await db.Articles.Where(a => articleIds.Contains(a.Id) && a.IsActive).Select(a => a.Id).ToListAsync(ct);
                var errors = new Errors();
                errors.Check(articleIds.Count <= ProductRecommendations.MaxPerTreatment, "articleIds",
                    $"Höchstens {ProductRecommendations.MaxPerTreatment} Artikel.");
                errors.Check(articleIds.Distinct().Count() == articleIds.Count, "articleIds", "Jeden Artikel nur einmal wählen.");
                errors.Check(articleIds.All(known.Contains), "articleIds", "Artikel nicht gefunden.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                db.TreatmentRecommendedArticles.RemoveRange(await db.TreatmentRecommendedArticles.Where(r => r.TreatmentId == treatmentId).ToListAsync(ct));
                db.TreatmentRecommendedArticles.AddRange(articleIds.Select((id, i) =>
                    new TreatmentRecommendedArticle { TreatmentId = treatmentId, ArticleId = id, Position = i }));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(articleIds);
            })
            .WithName("SetTreatmentRecommendedArticles")
            .WithSummary("„Passt dazu“ einer Behandlung ersetzen: höchstens fünf aktive Kassa-Artikel, jeder einmal.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapGet("/appointments/{appointmentId:guid}/recommendations", async Task<Results<Ok<List<RecommendedArticleDto>>, NotFound>> (
                Guid appointmentId, AispaDbContext db, CancellationToken ct) =>
            {
                var appointment = await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId)
                    .Select(a => new { a.LocationId, a.TreatmentId }).FirstOrDefaultAsync(ct);
                return appointment is null
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(await ProductRecommendations.ForAsync(db, appointment.LocationId, [appointment.TreatmentId], [], ct));
            })
            .WithName("ListAppointmentRecommendations")
            .WithSummary("„Passt dazu“ im Termin-Panel: empfohlene Verkaufsartikel der Behandlung mit Bestand am Verkaufsregal des Standorts.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/pos/bills/{billId:guid}/recommendations", async Task<Results<Ok<List<RecommendedArticleDto>>, NotFound>> (
                Guid billId, AispaDbContext db, CancellationToken ct) =>
            {
                var bill = await db.Bills.AsNoTracking().Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == billId, ct);
                if (bill is null)
                {
                    return TypedResults.NotFound();
                }

                var locationId = await db.CashRegisters.Where(r => r.Id == bill.RegisterId).Select(r => r.LocationId).FirstAsync(ct);
                var lines = bill.Lines.OrderBy(l => l.Position).ToList();
                List<Guid> treatmentIds = [.. lines.Where(l => l.TreatmentId != null).Select(l => l.TreatmentId!.Value).Distinct()];
                HashSet<Guid> onBill = [.. lines.Where(l => l.ArticleId != null).Select(l => l.ArticleId!.Value)];
                return TypedResults.Ok(await ProductRecommendations.ForAsync(db, locationId, treatmentIds, onBill, ct));
            })
            .WithName("ListBillRecommendations")
            .WithSummary("„Passt dazu“ an der Kassa: empfohlene Verkaufsartikel der Behandlungen auf der Rechnung, die noch nicht darauf stehen und am Verkaufsregal liegen.")
            .RequirePermission(AccessRight.Book);

        return routes;
    }
}
