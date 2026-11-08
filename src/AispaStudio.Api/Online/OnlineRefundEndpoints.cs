using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Online;

/// <summary>Erstattung online bezahlter Belege, im Beleg an der Kassa.</summary>
internal static class OnlineRefundEndpoints
{
    public static IEndpointRouteBuilder MapOnlineRefundEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/pos/receipts/{receiptId:guid}/online-refund").WithTags("Kassa");

        group.MapGet("", async Task<Results<Ok<OnlineRefundDto>, NoContent, NotFound>> (Guid receiptId, OnlineRefunds refunds, CancellationToken ct) =>
                await refunds.StatusAsync(receiptId, ct) switch
                {
                    (false, _) => TypedResults.NotFound(),
                    (true, null) => TypedResults.NoContent(),
                    (true, { } refund) => TypedResults.Ok(refund),
                })
            .RequirePermission(AccessRight.Book)
            .WithName("GetOnlineRefund")
            .WithSummary("Online-Zahlung hinter einem Beleg und Stand der Erstattung; 204, wenn der Beleg nicht online bezahlt wurde.");

        group.MapPost("", async Task<Results<Ok<OnlineRefundDto>, NotFound, ProblemHttpResult>> (
                Guid receiptId, ClaimsPrincipal user, OnlineRefunds refunds, CancellationToken ct) =>
                await refunds.RetryAsync(receiptId, user.FindFirstValue(ClaimTypes.NameIdentifier), ct) switch
                {
                    OnlineRefunds.RequestOutcome.Started s => TypedResults.Ok(s.Refund),
                    OnlineRefunds.RequestOutcome.Refused r => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: r.Message),
                    _ => TypedResults.NotFound(),
                })
            .RequirePermission(AccessRight.SuperviseCash)
            .WithName("RetryOnlineRefund")
            .WithSummary("Erstattung eines stornierten, online bezahlten Belegs erneut beim Zahlungsanbieter anstoßen.");

        return routes;
    }
}
