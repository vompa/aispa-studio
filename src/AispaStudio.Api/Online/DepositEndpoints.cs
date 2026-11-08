using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>Anzahlung: für Gäste über den Link aus der Mail oder das Konto (Testzahlung, neu beginnen), für das Team im Termin.</summary>
internal static class DepositEndpoints
{
    public static IEndpointRouteBuilder MapDepositEndpoints(this IEndpointRouteBuilder routes)
    {
        var guest = routes.MapGroup("/api/online/bookings/{token}/deposit").WithTags("Kundenbereich").AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit);

        guest.MapPost("/test", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                string token, TestPaymentResult result, AccessLinks links, OnlinePortal portal, Deposits deposits, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                return link?.AppointmentId is { } id && await deposits.TestAsync(id, result.Paid, ct)
                    ? await AppointmentAsync(portal, id, link.CustomerId, ct)
                    : TypedResults.NotFound();
            })
            .WithName("CompleteTestDeposit")
            .WithSummary("Nur im Testbetrieb: Zahlung der Anzahlung simulieren oder abbrechen.");

        guest.MapPost("/retry", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                string token, AccessLinks links, OnlinePortal portal, Deposits deposits, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, AccessLinkPurpose.ManageAppointment, ct);
                var appointment = link?.AppointmentId is { } id ? await portal.LoadAppointmentAsync(id, ct) : null;
                return appointment is not null && appointment.CustomerId == link!.CustomerId
                       && await deposits.RetryAsync(appointment, portal.Url($"termin/{token}"), ct)
                    ? await AppointmentAsync(portal, appointment.Id, link.CustomerId, ct)
                    : TypedResults.NotFound();
            })
            .WithName("RetryDeposit")
            .WithSummary("Zahlung der Anzahlung nach Abbruch oder Ablauf neu beginnen.");

        var mine = routes.MapGroup("/api/online/me/appointments/{appointmentId:guid}/deposit").WithTags("Kundenbereich: Mein Konto")
            .RequireCustomer().RequireRateLimiting(OnlineSetup.SensitiveRateLimit);

        mine.MapPost("/test", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                Guid appointmentId, TestPaymentResult result, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal,
                Deposits deposits, CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                && await portal.LoadAppointmentAsync(appointmentId, ct) is { } appointment && appointment.CustomerId == customerId
                && await deposits.TestAsync(appointmentId, result.Paid, ct)
                    ? await AppointmentAsync(portal, appointmentId, customerId, ct)
                    : TypedResults.NotFound())
            .WithName("CompleteMyTestDeposit")
            .WithSummary("Nur im Testbetrieb: Zahlung der Anzahlung für einen eigenen Termin simulieren oder abbrechen.");

        mine.MapPost("/retry", async Task<Results<Ok<OnlineAppointment>, NotFound>> (
                Guid appointmentId, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal, Deposits deposits,
                CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                && await portal.LoadAppointmentAsync(appointmentId, ct) is { } appointment && appointment.CustomerId == customerId
                && await deposits.RetryAsync(appointment, portal.Url("termine"), ct)
                    ? await AppointmentAsync(portal, appointmentId, customerId, ct)
                    : TypedResults.NotFound())
            .WithName("RetryMyDeposit")
            .WithSummary("Zahlung der Anzahlung für einen eigenen Termin neu beginnen.");

        routes.MapGet("/api/appointments/{appointmentId:guid}/deposit", async Task<Results<Ok<DepositStaffDto>, NoContent, NotFound>> (
                Guid appointmentId, Deposits deposits, CancellationToken ct) =>
                await deposits.StaffAsync(appointmentId, ct) switch
                {
                    (false, _) => TypedResults.NotFound(),
                    (true, null) => TypedResults.NoContent(),
                    (true, { } deposit) => TypedResults.Ok(deposit),
                })
            .WithTags("Kalender")
            .RequirePermission(AccessRight.ViewCalendar)
            .WithName("GetDeposit")
            .WithSummary("Anzahlung eines Online-Termins mit Stand des Anzahlungsbelegs; 204, wenn keine verlangt wurde.");

        routes.MapGet("/api/appointments/{appointmentId:guid}/deposit-return", async Task<Results<Ok<DepositReturnInfo>, NoContent, NotFound>> (
                Guid appointmentId, DepositReturns returns, AccessService access, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Appointments.AnyAsync(a => a.Id == appointmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var canReturn = (await access.CurrentAsync(ct))?.IsOwnerLevel == true;
                return await returns.InfoAsync(appointmentId, canReturn, ct) is { } info ? TypedResults.Ok(info) : TypedResults.NoContent();
            })
            .WithTags("Kalender")
            .RequirePermission(AccessRight.ViewCalendar)
            .WithName("GetDepositReturn")
            .WithSummary("Die offene Anzahlung eines Termins (Betrag, Zahlungsweg, Anzahlungsbeleg), die sich retournieren lässt; 204, wenn es keine gibt. "
                + "„CanReturn“ sagt, ob die angemeldete Person sie retournieren darf (Inhaber und Manager).");

        routes.MapPost("/api/appointments/{appointmentId:guid}/deposit-return", async Task<Results<Ok<ReceiptDto>, ValidationProblem, ProblemHttpResult, NotFound>> (
                Guid appointmentId, DepositReturnRequest request, ClaimsPrincipal user, DepositReturns returns, AccessService access, AispaDbContext db, CancellationToken ct) =>
            {
                if ((await access.CurrentAsync(ct))?.IsOwnerLevel != true)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Keine Berechtigung.",
                        detail: "Eine Anzahlung retournieren nur Inhaber und Manager.");
                }

                if (!await db.Appointments.AnyAsync(a => a.Id == appointmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var reason = request.Reason?.Trim() ?? "";
                var errors = new Errors();
                errors.Require(nameof(request.Reason), reason);
                errors.Check(reason.Length <= DepositReturnReasonLength, nameof(request.Reason), $"Höchstens {DepositReturnReasonLength} Zeichen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var result = await returns.ReturnAsync(appointmentId, $"Anzahlung retourniert: {reason}", user.FindFirstValue(ClaimTypes.NameIdentifier), ct);
                if (result.Value is { } returned)
                {
                    return TypedResults.Ok(returned.VoidReceipt);
                }

                return result.Error is { Kind: PosErrorKind.NotFound } ? TypedResults.NotFound() : ReturnProblem(result.Error!);
            })
            .WithTags("Kalender")
            .RequirePermission(AccessRight.Book)
            .WithName("ReturnDeposit")
            .WithSummary("Retourniert die offene Anzahlung eines Termins: Storno-Beleg über die Kassa mit dem Grund (Pflicht), das Geld geht auf dem Zahlungsweg der Einzahlung zurück. "
                + "Nur Inhaber und Manager (403 sonst). Ohne offene Anzahlung 404.");

        return routes;
    }

    /// <summary>Höchstlänge des Grundes; mit dem vorangestellten Text bleibt er unter den 500 Zeichen des Stornogrundes am Beleg.</summary>
    private const int DepositReturnReasonLength = 300;

    private static ProblemHttpResult ReturnProblem(PosError error) => TypedResults.Problem(
        statusCode: error.Kind == PosErrorKind.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status409Conflict, title: error.Message);

    private static async Task<Results<Ok<OnlineAppointment>, NotFound>> AppointmentAsync(OnlinePortal portal, Guid appointmentId, Guid customerId, CancellationToken ct) =>
        await portal.LoadAppointmentAsync(appointmentId, ct) is { } appointment && appointment.CustomerId == customerId
            ? TypedResults.Ok(await portal.ToOnlineAsync(appointment, ct))
            : TypedResults.NotFound();
}
