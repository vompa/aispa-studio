using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Closures;

/// <summary>Übersicht von Schließzeiten und Raumsperren eines Standorts in einer Liste (ADR 0098): lesen für jeden mit Kalender; verwaltet wird über die Wege der jeweiligen Art.</summary>
internal static class ClosureOverviewEndpoints
{
    public static IEndpointRouteBuilder MapClosureOverviewEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/locations/{locationId:guid}/closure-overview", async Task<Results<Ok<ClosureOverview>, NotFound>> (
                Guid locationId, ClosureOverviewService overview, CancellationToken ct, ClosureScope scope = ClosureScope.Upcoming) =>
                await overview.GetAsync(locationId, scope, ct) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound())
            .WithTags("Schließzeiten")
            .WithName("GetClosureOverview")
            .WithSummary("Schließzeiten des ganzen Standorts und Raumsperren in einer Liste, je mit Art, Zeitraum, Raum, Grund, Zustand und der Zahl der noch offenen Termine. "
                + "Mit scope=Upcoming (Standard) die laufenden und geplanten nach Beginn aufsteigend, mit Past die beendeten, neueste zuerst, mit All beide.")
            .RequirePermission(AccessRight.ViewCalendar);

        return routes;
    }
}
