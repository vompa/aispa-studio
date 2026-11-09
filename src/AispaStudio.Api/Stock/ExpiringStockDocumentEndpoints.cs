using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Ablaufliste eines Standorts als PDF (PDF-Ausgaben F, ADR 0091). Recht wie die „Ablauf-Aufgaben“ (<c>ManageLocation</c>); den Standort prüft der Mandantenfilter, ein fremder ist „nicht gefunden“.
/// Der Zeitraum <c>days</c> reicht von 1 bis 365 Tage und ist ohne Angabe 30.
/// </summary>
internal static class ExpiringStockDocumentEndpoints
{
    public static IEndpointRouteBuilder MapExpiringStockDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/locations/{locationId:guid}").WithTags("Lager").RequirePermission(AccessRight.ManageLocation)
            .MapGet("/stock/expiring/pdf", async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> (
                Guid locationId, int? days, ClaimsPrincipal user, AispaDbContext db, StockService stock, DocumentHeaders headers, CancellationToken ct) =>
            {
                var period = days ?? ExpiringStockDocument.DefaultDays;
                if (period is < 1 or > ExpiringStockDocument.MaxDays)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["days"] = [$"Bitte einen Zeitraum von 1 bis {ExpiringStockDocument.MaxDays} Tagen angeben."] });
                }

                if (await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct) is not { } location)
                {
                    return TypedResults.NotFound();
                }

                var batches = await stock.BatchesAsync(locationId, ct);
                var data = new ExpiringStockDocument.Data(location.Name, batches.Today, period, ExpiringStockDocument.Within(batches, period));
                return DocumentFiles.Pdf(ExpiringStockDocument.Build(data, await headers.ForAsync(locationId, user, ct)), ExpiringStockDocument.FileName(data));
            })
            .WithName("ExpiringStockPdf")
            .WithSummary("Ablaufliste als PDF (A4): Chargen, die in den nächsten days Tagen (1 bis 365, Standard 30) ablaufen, mit Material, Charge, Lagerort, Ablaufdatum, Frist und Menge.");
        return routes;
    }
}
