using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Inventurliste und Zählliste als PDF (PDF-Ausgaben F, ADR 0091). Recht wie die Inventur (<c>ManageLocation</c>); Standort und Zählung prüft der Mandantenfilter, eine Zählung eines anderen
/// Standorts oder Mandanten ist „nicht gefunden“. Die Inventur der Windows-App zählt, ohne dass eine Zählung gespeichert wird; ihre Zählliste kommt deshalb auch je Lagerort, ohne Zählung.
/// </summary>
internal static class StockCountDocumentEndpoints
{
    public static IEndpointRouteBuilder MapStockCountDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/locations/{locationId:guid}").WithTags("Lager").RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/stock-counts/{countId:guid}/pdf", async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> (
                Guid locationId, Guid countId, string? blank, ClaimsPrincipal user, AispaDbContext db, StockCountSessions sessions, StockService stock, DocumentHeaders headers,
                CancellationToken ct) =>
            {
                if (!bool.TryParse(blank ?? "false", out var asSheet))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["blank"] = ["Bitte „true“ oder „false“ angeben."] });
                }

                if (await sessions.FindAsync(locationId, countId, ct) is not { } session)
                {
                    return TypedResults.NotFound();
                }

                var header = await headers.ForAsync(locationId, user, ct);
                var data = asSheet
                    ? await SheetAsync(locationId, session.StoragePlaceId, header.CreatedAt, db, stock, ct)
                    : await CountedAsync(session, sessions, db, ct);
                return data is null ? TypedResults.NotFound() : DocumentFiles.Pdf(StockCountDocument.Build(data, header), StockCountDocument.FileName(data, header.Zone));
            })
            .WithName("StockCountPdf")
            .WithSummary("Inventurliste einer Zählung vom Handy als PDF (A4): Material, Einheit, Soll, Gezählt, Differenz, Wert und die Summe der Differenzen; mit blank=true die Zählliste mit leerer Spalte „Gezählt“.");

        group.MapGet("/storage-places/{placeId:guid}/count-sheet/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid locationId, Guid placeId, ClaimsPrincipal user, AispaDbContext db, StockService stock, DocumentHeaders headers, CancellationToken ct) =>
            {
                var header = await headers.ForAsync(locationId, user, ct);
                if (await SheetAsync(locationId, placeId, header.CreatedAt, db, stock, ct) is not { } data)
                {
                    return TypedResults.NotFound();
                }

                return DocumentFiles.Pdf(StockCountDocument.Build(data, header), StockCountDocument.FileName(data, header.Zone));
            })
            .WithName("StockCountSheetPdf")
            .WithSummary("Zählliste eines Lagerorts als PDF vor dem Zählen (A4): alle aktiven Materialien mit dem Soll und einer leeren Spalte „Gezählt“.");

        return routes;
    }

    /// <summary>Was die Zählung gezählt hat, mit dem Bestand des Lagerorts als Soll und dem Einstandspreis für den Wert.</summary>
    private static async Task<StockCountDocument.Data> CountedAsync(StockCountSession session, StockCountSessions sessions, AispaDbContext db, CancellationToken ct)
    {
        var dto = await sessions.ToDtoAsync(session, withExpected: true, ct);
        var costs = await db.StockItems.AsNoTracking().Where(i => session.Lines.Select(l => l.ItemId).Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.UnitCost, ct);
        var currency = await db.Locations.AsNoTracking().Where(l => l.Id == session.LocationId).Select(l => l.Currency).FirstAsync(ct);
        return new StockCountDocument.Data(dto.StoragePlaceName, dto.SubmittedAt ?? dto.StartedAt, dto.StartedBy, false,
            [.. dto.Lines.Select(l => new StockCountDocument.Line(l.Name, l.Unit, l.Expected ?? 0, l.Counted, costs.GetValueOrDefault(l.ItemId)))], currency);
    }

    /// <summary>Alle aktiven Materialien mit dem Bestand des Lagerorts; null, wenn es den Lagerort an diesem Standort nicht gibt.</summary>
    private static async Task<StockCountDocument.Data?> SheetAsync(Guid locationId, Guid placeId, DateTimeOffset at, AispaDbContext db, StockService stock, CancellationToken ct)
    {
        if (await db.StoragePlaces.AsNoTracking().FirstOrDefaultAsync(p => p.Id == placeId && p.LocationId == locationId, ct) is not { } place)
        {
            return null;
        }

        var target = (await stock.LevelsAsync(locationId, placeId, ct)).Items.ToDictionary(l => l.ItemId, l => l.Quantity);
        var items = (await db.StockItems.AsNoTracking().Where(i => i.IsActive).ToListAsync(ct)).OrderBy(i => i.Name, StringComparer.CurrentCulture).ToList();
        var currency = await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.Currency).FirstAsync(ct);
        return new StockCountDocument.Data(place.Name, at, null, true, [.. items.Select(i => new StockCountDocument.Line(i.Name, i.Unit, target.GetValueOrDefault(i.Id), null, i.UnitCost))], currency);
    }
}
