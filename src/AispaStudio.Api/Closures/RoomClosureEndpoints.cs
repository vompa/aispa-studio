using AispaStudio.Api.Access;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Raumsperren je Standort (ADR 0076): lesen für jeden mit Kalender, verwalten mit <see cref="AccessRight.ManageLocation"/> (wie Räume anlegen). Die Umbuchungshilfe
/// verlangt <see cref="AccessRight.Book"/>. Der Standort steht in der Route, der Zugriffsfilter prüft ihn.
/// </summary>
internal static class RoomClosureEndpoints
{
    public static IEndpointRouteBuilder MapRoomClosureEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/locations/{locationId:guid}/room-closures").WithTags("Raumsperren");

        group.MapGet("/", async Task<Results<Ok<List<RoomClosureDto>>, NotFound>> (Guid locationId, RoomClosureService closures, CancellationToken ct) =>
                await closures.ListAsync(locationId, ct) is { } list ? TypedResults.Ok(list) : TypedResults.NotFound())
            .WithName("ListRoomClosures")
            .WithSummary("Alle Raumsperren des Standorts mit Zustand (geplant, aktiv, beendet), neueste zuerst, und der Zahl der noch offenen Termine im gesperrten Raum.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/{closureId:guid}", async Task<Results<Ok<RoomClosureDto>, NotFound>> (Guid locationId, Guid closureId, RoomClosureService closures, CancellationToken ct) =>
                await closures.GetAsync(locationId, closureId, ct) is { } closure ? TypedResults.Ok(closure) : TypedResults.NotFound())
            .WithName("GetRoomClosure")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPost("/", async Task<Results<Created<RoomClosureDto>, NotFound, ValidationProblem>> (
                Guid locationId, RoomClosureInput input, RoomClosureService closures, AccessService access, CancellationToken ct) =>
            {
                var (closure, errors) = await closures.CreateAsync(locationId, input, (await access.CurrentAsync(ct))!, ct);
                return closure is not null ? TypedResults.Created($"/api/locations/{locationId}/room-closures/{closure.Id}", closure)
                    : errors is not null ? errors.Problem() : TypedResults.NotFound();
            })
            .WithName("CreateRoomClosure")
            .WithSummary("Sperrt einen Raum für einen Zeitraum (ganze Tage oder mit Uhrzeit für halbe Tage) mit Art und Grund. Danach bucht der Planer den Raum in der Zeit nicht mehr; "
                + "bestehende Termine im Raum bleiben, bis sie umgebucht werden.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/{closureId:guid}", async Task<Results<Ok<RoomClosureDto>, NotFound, ValidationProblem>> (
                Guid locationId, Guid closureId, RoomClosureInput input, RoomClosureService closures, CancellationToken ct) =>
            {
                var (closure, errors) = await closures.UpdateAsync(locationId, closureId, input, ct);
                return closure is not null ? TypedResults.Ok(closure) : errors is not null ? errors.Problem() : TypedResults.NotFound();
            })
            .WithName("UpdateRoomClosure")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("/{closureId:guid}", async Task<Results<NoContent, NotFound>> (Guid locationId, Guid closureId, RoomClosureService closures, CancellationToken ct) =>
                await closures.DeleteAsync(locationId, closureId, ct) ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("DeleteRoomClosure")
            .WithSummary("Hebt eine Raumsperre auf; der Raum ist sofort wieder buchbar.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/{closureId:guid}/rebooking", async Task<Results<Ok<AbsenceProposal>, NotFound>> (
                Guid locationId, Guid closureId, AbsenceAssistant assistant, CancellationToken ct) =>
                await assistant.ProposeForRoomClosureAsync(locationId, closureId, ct) is { } proposal ? TypedResults.Ok(proposal) : TypedResults.NotFound())
            .WithName("ProposeRoomClosureRebooking")
            .WithSummary("Umbuchungshilfe: für jeden offenen Termin im gesperrten Raum einen anderen Raum zur selben Zeit vorschlagen, sonst die nächste freie Zeit am selben Tag (ändert nichts). "
                + "Übernommen wird mit „ApplyAbsenceCover“.")
            .RequirePermission(AccessRight.Book);

        return routes;
    }
}
