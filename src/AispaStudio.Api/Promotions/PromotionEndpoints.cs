using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Promotions;

/// <summary>Aktionen (ADR 0071): verwalten für Inhaber und Standortleitung, „laufende Aktionen je Standort“ für jeden Zugang (Kassa, Assistent).</summary>
internal static class PromotionEndpoints
{
    public static IEndpointRouteBuilder MapPromotionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/promotions").WithTags("Aktionen");

        group.MapGet("/active", async Task<Results<Ok<IReadOnlyList<ActivePromotionDto>>, NotFound>> (
                Guid locationId, DateOnly? date, PromotionService promotions, CancellationToken ct) =>
            {
                if (await promotions.TodayAsync(locationId, ct) is not { } today)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(await promotions.GetActiveAsync(locationId, date ?? today, ct));
            })
            .WithName("ListActivePromotions")
            .WithSummary("Aktionen, die an einem Standort an einem Tag laufen (ohne Angabe heute am Standort), mit Gästetext, Rabatt und Gültigkeit.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/", async (bool? includeArchived, PromotionService promotions, AccessService access, CancellationToken ct) =>
                await promotions.ListAsync(includeArchived == true, (await access.CurrentAsync(ct))!, ct))
            .WithName("ListPromotions")
            .WithSummary("Alle Aktionen des Spas (laufende, geplante, pausierte, abgelaufene); archivierte nur auf Wunsch.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapGet("/{promotionId:guid}", async Task<Results<Ok<PromotionDto>, NotFound>> (
                Guid promotionId, PromotionService promotions, AccessService access, CancellationToken ct) =>
                await promotions.GetAsync(promotionId, (await access.CurrentAsync(ct))!, ct) is { } promotion ? TypedResults.Ok(promotion) : TypedResults.NotFound())
            .WithName("GetPromotion")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPost("/", async Task<Results<Created<PromotionDto>, ValidationProblem>> (
                PromotionInput input, PromotionService promotions, AccessService access, CancellationToken ct) =>
            {
                var (promotion, errors) = await promotions.CreateAsync(input, (await access.CurrentAsync(ct))!, ct);
                return promotion is null ? errors!.Problem() : TypedResults.Created($"/api/promotions/{promotion.Id}", promotion);
            })
            .WithName("CreatePromotion")
            .WithSummary("Legt eine Aktion an: Zeitraum, Standorte, Rabatt in Prozent oder als Betrag und wofür sie gilt. Die Kassa schlägt sie vor, den Rabatt rechnet die Kassa.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPut("/{promotionId:guid}", async Task<Results<Ok<PromotionDto>, NotFound, ValidationProblem>> (
                Guid promotionId, PromotionInput input, PromotionService promotions, AccessService access, CancellationToken ct) =>
            {
                var (promotion, errors) = await promotions.UpdateAsync(promotionId, input, (await access.CurrentAsync(ct))!, ct);
                return promotion is not null ? TypedResults.Ok(promotion) : errors is not null ? errors.Problem() : TypedResults.NotFound();
            })
            .WithName("UpdatePromotion")
            .WithSummary("Ändert eine Aktion, auch Pausieren und Fortsetzen. Bereits gegebene Rabatte auf Belegen bleiben unverändert.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPost("/{promotionId:guid}/archive", async Task<Results<Ok<PromotionDto>, NotFound>> (
                Guid promotionId, PromotionService promotions, AccessService access, CancellationToken ct) =>
                await promotions.SetArchivedAsync(promotionId, true, (await access.CurrentAsync(ct))!, ct) is { } promotion
                    ? TypedResults.Ok(promotion)
                    : TypedResults.NotFound())
            .WithName("ArchivePromotion")
            .WithSummary("Archiviert eine Aktion: sie bleibt im Verlauf, läuft aber nie mehr und steht nicht mehr in der Liste.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPost("/{promotionId:guid}/unarchive", async Task<Results<Ok<PromotionDto>, NotFound>> (
                Guid promotionId, PromotionService promotions, AccessService access, CancellationToken ct) =>
                await promotions.SetArchivedAsync(promotionId, false, (await access.CurrentAsync(ct))!, ct) is { } promotion
                    ? TypedResults.Ok(promotion)
                    : TypedResults.NotFound())
            .WithName("UnarchivePromotion")
            .WithSummary("Holt eine archivierte Aktion zurück in die Liste.")
            .RequirePermission(AccessRight.ManageCatalog);

        return routes;
    }
}
