using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>
/// Warteliste eines Tages als PDF (PDF-Ausgaben E, ADR 0092). Recht wie die Liste im Kalender; den Standort prüft der Mandantenfilter, ein fremder ist „nicht gefunden“. Kein Versand.
/// </summary>
internal static class WaitlistDocumentEndpoints
{
    public static IEndpointRouteBuilder MapWaitlistDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/locations/{locationId:guid}").WithTags("Kalender").RequirePermission(AccessRight.ViewCalendar)
            .MapGet("/waitlist/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid locationId, DateOnly? date, ClaimsPrincipal user, AispaDbContext db, Waitlist waitlist, DocumentHeaders headers, TimeProvider time, CancellationToken ct) =>
            {
                if (await waitlist.ForLocationAsync(locationId, ct) is not { } entries
                    || await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.Name).FirstOrDefaultAsync(ct) is not { } locationName)
                {
                    return TypedResults.NotFound();
                }

                var header = await headers.ForAsync(locationId, user, ct);
                var day = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), header.Zone).DateTime);
                return DocumentFiles.Pdf(WaitlistDocument.Build(WaitlistDocument.ValidOn(entries, day), day, header), WaitlistDocument.FileName(locationName, day));
            })
            .WithName("WaitlistPdf")
            .WithSummary("Warteliste eines Tages als PDF (A4): die offenen Wünsche, die am Tag (date, Standard heute) gelten, mit Gast, Telefon, Wunsch, Zeitraum und Stand.");
        return routes;
    }
}
