using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Online;

/// <summary>Warteliste: eintragen (als Gast oder angemeldet), per Link oder im Konto ansehen und austragen, Liste für das Team.</summary>
internal static class WaitlistEndpoints
{
    public static IEndpointRouteBuilder MapWaitlistEndpoints(this IEndpointRouteBuilder routes)
    {
        var open = routes.MapGroup("/api/online/waitlist").WithTags("Kundenbereich").AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit);

        open.MapPost("", async Task<Results<Created<WaitlistConfirmation>, ValidationProblem, NotFound>> (
                WaitlistRequest request, HttpContext http, Waitlist waitlist, CancellationToken ct) =>
                await waitlist.JoinAsync(request, await CustomerAuth.TryGetAccountIdAsync(http), ct) switch
                {
                    Waitlist.JoinOutcome.Joined j => TypedResults.Created($"/api/online/waitlist/{j.Confirmation.ManageToken}", j.Confirmation),
                    Waitlist.JoinOutcome.Invalid i => i.Errors.Problem(),
                    _ => TypedResults.NotFound(),
                })
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("JoinWaitlist")
            .WithSummary("Trägt auf die Warteliste ein; Bestätigung per Mail. Höchstens 60 Tage und 5 offene Einträge je Kunde.");

        open.MapGet("/{token}", async Task<Results<Ok<WaitlistEntryDto>, NotFound>> (
                string token, AccessLinks links, Waitlist waitlist, CancellationToken ct) =>
                await links.FindAsync(token, AccessLinkPurpose.ManageWaitlist, ct) is { WaitlistEntryId: { } id } link
                && await waitlist.FindAsync(id, link.CustomerId, ct) is { } entry
                    ? TypedResults.Ok(entry)
                    : TypedResults.NotFound())
            .WithName("GetWaitlistEntry")
            .WithSummary("Eintrag über den Link aus der Mail.");

        open.MapPost("/{token}/cancel", async Task<Results<Ok<WaitlistEntryDto>, NotFound>> (
                string token, AccessLinks links, Waitlist waitlist, CancellationToken ct) =>
                await links.FindAsync(token, AccessLinkPurpose.ManageWaitlist, ct) is { WaitlistEntryId: { } id } link
                && await waitlist.CancelAsync(id, link.CustomerId, ct) is { } entry
                    ? TypedResults.Ok(entry)
                    : TypedResults.NotFound())
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("LeaveWaitlist")
            .WithSummary("Trägt über den Link aus der Mail von der Warteliste aus.");

        var me = routes.MapGroup("/api/online/me/waitlist").WithTags("Kundenbereich: Mein Konto").RequireCustomer()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit);

        me.MapGet("", async Task<Results<Ok<IReadOnlyList<WaitlistEntryDto>>, UnauthorizedHttpResult>> (
                ClaimsPrincipal principal, OnlineDbContext online, Waitlist waitlist, CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, online, ct) switch
                {
                    null => TypedResults.Unauthorized(),
                    { CustomerId: { } customerId } => TypedResults.Ok(await waitlist.ForCustomerAsync(customerId, ct)),
                    _ => TypedResults.Ok<IReadOnlyList<WaitlistEntryDto>>([]),
                })
            .WithName("ListMyWaitlist")
            .WithSummary("Eigene Einträge auf der Warteliste, neueste zuerst.");

        me.MapPost("/{entryId:guid}/cancel", async Task<Results<Ok<WaitlistEntryDto>, NotFound>> (
                Guid entryId, ClaimsPrincipal principal, OnlineDbContext online, Waitlist waitlist, CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                && await waitlist.CancelAsync(entryId, customerId, ct) is { } entry
                    ? TypedResults.Ok(entry)
                    : TypedResults.NotFound())
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("LeaveMyWaitlist")
            .WithSummary("Trägt einen eigenen Eintrag von der Warteliste aus.");

        routes.MapGet("/api/locations/{locationId:guid}/waitlist", async Task<Results<Ok<IReadOnlyList<WaitlistStaffEntry>>, NotFound>> (
                Guid locationId, Waitlist waitlist, CancellationToken ct) =>
                await waitlist.ForLocationAsync(locationId, ct) is { } entries ? TypedResults.Ok(entries) : TypedResults.NotFound())
            .RequirePermission(AccessRight.ViewCalendar)
            .WithTags("Kalender")
            .WithName("ListLocationWaitlist")
            .WithSummary("Offene Einträge der Warteliste eines Standorts, nach gewünschtem Beginn.");

        routes.MapPost("/api/waitlist/{entryId:guid}/close", async Task<Results<Ok<WaitlistEntryDto>, NotFound, ValidationProblem>> (
                Guid entryId, WaitlistCloseInput input, AccessService accessService, Waitlist waitlist, CancellationToken ct) =>
            {
                if (!Enum.IsDefined(input.Reason))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Unbekannter Grund."] });
                }

                var access = (await accessService.CurrentAsync(ct))!;
                return await waitlist.CloseAsync(entryId, input.Reason, access.CanSee, ct) is { } entry ? TypedResults.Ok(entry) : TypedResults.NotFound();
            })
            .RequirePermission(AccessRight.Book)
            .WithTags("Kalender")
            .WithName("CloseWaitlistEntry")
            .WithSummary("Nimmt einen Wunsch von der Warteliste: „vereinbart“ gilt als gebucht, „braucht keinen mehr“ wie ausgetragen. Der Gast bekommt dann keine Mails mehr.");

        return routes;
    }
}
