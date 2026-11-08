using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Online;

/// <summary>Kartenhinterlegung: für Gäste über den Link aus der Mail oder das Konto, für das Team im Termin.</summary>
internal static class CardGuaranteeEndpoints
{
    public static IEndpointRouteBuilder MapCardGuaranteeEndpoints(this IEndpointRouteBuilder routes)
    {
        var guest = routes.MapGroup("/api/online/bookings/{token}/card").WithTags("Kundenbereich").AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit);

        guest.MapPost("/test", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                string token, TestCardResult result, AccessLinks links, OnlinePortal portal, CardGuarantees cards, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                return link?.AppointmentId is { } id && await cards.TestAsync(id, result.Saved, ct)
                    ? await AppointmentAsync(portal, id, link.CustomerId, ct)
                    : TypedResults.NotFound();
            })
            .WithName("CompleteTestCard")
            .WithSummary("Nur im Testbetrieb: Hinterlegen der Karte simulieren oder abbrechen.");

        guest.MapPost("/retry", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                string token, AccessLinks links, OnlinePortal portal, CardGuarantees cards, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                var appointment = link?.AppointmentId is { } id ? await portal.LoadAppointmentAsync(id, ct) : null;
                return appointment is not null && appointment.CustomerId == link!.CustomerId
                       && await cards.RetryAsync(appointment, portal.Url($"termin/{token}"), ct)
                    ? await AppointmentAsync(portal, appointment.Id, link.CustomerId, ct)
                    : TypedResults.NotFound();
            })
            .WithName("RetryCardGuarantee")
            .WithSummary("Hinterlegen der Karte nach Abbruch oder Ablauf neu beginnen.");

        guest.MapPost("/accept", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                string token, AccessLinks links, OnlinePortal portal, CardGuarantees cards, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                var appointment = link?.AppointmentId is { } id ? await portal.LoadAppointmentAsync(id, ct) : null;
                return appointment is not null && appointment.CustomerId == link!.CustomerId
                       && await cards.AcceptAsync(appointment, portal.Url($"termin/{token}"), ct)
                    ? await AppointmentAsync(portal, appointment.Id, link.CustomerId, ct)
                    : TypedResults.NotFound();
            })
            .WithName("AcceptCardGuarantee")
            .WithSummary("Der Ausfallgebühr zustimmen, die die Rezeption per Link angefordert hat, und das Hinterlegen beginnen.");

        var mine = routes.MapGroup("/api/online/me/appointments/{appointmentId:guid}/card").WithTags("Kundenbereich: Mein Konto")
            .RequireCustomer().RequireRateLimiting(OnlineSetup.SensitiveRateLimit);

        mine.MapPost("/test", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                Guid appointmentId, TestCardResult result, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal,
                CardGuarantees cards, CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                && await portal.LoadAppointmentAsync(appointmentId, ct) is { } appointment && appointment.CustomerId == customerId
                && await cards.TestAsync(appointmentId, result.Saved, ct)
                    ? await AppointmentAsync(portal, appointmentId, customerId, ct)
                    : TypedResults.NotFound())
            .WithName("CompleteMyTestCard")
            .WithSummary("Nur im Testbetrieb: Hinterlegen der Karte für einen eigenen Termin simulieren oder abbrechen.");

        mine.MapPost("/retry", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                Guid appointmentId, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal, CardGuarantees cards,
                CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                && await portal.LoadAppointmentAsync(appointmentId, ct) is { } appointment && appointment.CustomerId == customerId
                && await cards.RetryAsync(appointment, portal.Url("termine"), ct)
                    ? await AppointmentAsync(portal, appointmentId, customerId, ct)
                    : TypedResults.NotFound())
            .WithName("RetryMyCardGuarantee")
            .WithSummary("Hinterlegen der Karte für einen eigenen Termin neu beginnen.");

        mine.MapPost("/accept", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                Guid appointmentId, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal, CardGuarantees cards,
                CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                && await portal.LoadAppointmentAsync(appointmentId, ct) is { } appointment && appointment.CustomerId == customerId
                && await cards.AcceptAsync(appointment, portal.Url("termine"), ct)
                    ? await AppointmentAsync(portal, appointmentId, customerId, ct)
                    : TypedResults.NotFound())
            .WithName("AcceptMyCardGuarantee")
            .WithSummary("Der angeforderten Ausfallgebühr für einen eigenen Termin zustimmen und das Hinterlegen beginnen.");

        var staff = routes.MapGroup("/api/appointments/{appointmentId:guid}/card-guarantee").WithTags("Kalender");

        staff.MapGet("", async Task<Results<Ok<CardGuaranteeStaffDto>, NoContent, NotFound>> (Guid appointmentId, CardGuarantees cards, CancellationToken ct) =>
                await cards.StaffAsync(appointmentId, ct) switch
                {
                    (false, _) => TypedResults.NotFound(),
                    (true, null) => TypedResults.NoContent(),
                    (true, { } guarantee) => TypedResults.Ok(guarantee),
                })
            .RequirePermission(AccessRight.ViewCalendar)
            .WithName("GetCardGuarantee")
            .WithSummary("Hinterlegte Karte und Ausfallgebühr eines Online-Termins; 204, wenn keine verlangt wurde.");

        staff.MapPost("/charge", async Task<Results<Ok<CardGuaranteeStaffDto>, NotFound, ProblemHttpResult>> (
                Guid appointmentId, ClaimsPrincipal user, CardGuarantees cards, CancellationToken ct) =>
                await cards.ChargeAsync(appointmentId, user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", ct) switch
                {
                    CardGuarantees.ChargeOutcome.Started s => TypedResults.Ok(s.Guarantee),
                    CardGuarantees.ChargeOutcome.Refused r => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: r.Message),
                    _ => TypedResults.NotFound(),
                })
            .RequirePermission(AccessRight.SuperviseCash)
            .WithName("ChargeNoShowFee")
            .WithSummary("Ausfallgebühr von der hinterlegten Karte belasten und an der Kasse verbuchen (nur bei Nichterscheinen oder später Absage).");

        staff.MapGet("/offer", async Task<Results<Ok<CardRequestOffer>, NotFound>> (Guid appointmentId, CardRequests requests, CancellationToken ct) =>
                await requests.OfferAsync(appointmentId, ct) is { } offer ? TypedResults.Ok(offer) : TypedResults.NotFound())
            .RequirePermission(AccessRight.ViewCalendar)
            .WithName("GetCardRequestOffer")
            .WithSummary("Ob sich für einen Termin der Rezeption eine Karte per Link anfordern lässt, und mit welcher Ausfallgebühr.");

        staff.MapPost("/request", async Task<Results<Ok<CardGuaranteeStaffDto>, NotFound, ProblemHttpResult>> (
                Guid appointmentId, CardRequests requests, CancellationToken ct) =>
                await requests.RequestAsync(appointmentId, ct) switch
                {
                    CardRequests.RequestOutcome.Sent s => TypedResults.Ok(s.Guarantee),
                    CardRequests.RequestOutcome.Refused r => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: r.Message),
                    _ => TypedResults.NotFound(),
                })
            .RequirePermission(AccessRight.Book)
            .WithName("RequestCardGuarantee")
            .WithSummary("Dem Gast einen Link schicken, um der Ausfallgebühr zuzustimmen und eine Karte zu hinterlegen; erneut aufgerufen ein neuer Link.");

        return routes;
    }

    private static async Task<Results<Ok<OnlineAppointment>, NotFound>> AppointmentAsync(OnlinePortal portal, Guid appointmentId, Guid customerId, CancellationToken ct) =>
        await portal.LoadAppointmentAsync(appointmentId, ct) is { } appointment && appointment.CustomerId == customerId
            ? TypedResults.Ok(await portal.ToOnlineAsync(appointment, ct))
            : TypedResults.NotFound();
}
