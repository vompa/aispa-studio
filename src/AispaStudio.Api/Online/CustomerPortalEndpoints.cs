using System.Security.Claims;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>Angemeldete Kundinnen und Kunden: eigenes Profil und eigene Termine über alle Standorte.</summary>
internal static class CustomerPortalEndpoints
{
    public static IEndpointRouteBuilder MapCustomerPortalEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/online/me").WithTags("Kundenbereich: Mein Konto").RequireCustomer()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit);

        group.MapGet("/", async Task<Results<Ok<CustomerSession>, UnauthorizedHttpResult>> (
                ClaimsPrincipal principal, CustomerAccounts accounts, CancellationToken ct) =>
                CustomerAuth.AccountId(principal) is { } id && await accounts.SessionAsync(id, ct) is { } session
                    ? TypedResults.Ok(session)
                    : TypedResults.Unauthorized())
            .WithName("GetCustomerSession");

        group.MapPut("/profile", async Task<Results<Ok<CustomerSession>, ValidationProblem, UnauthorizedHttpResult>> (
                CustomerProfileInput input, ClaimsPrincipal principal, OnlineDbContext online, AispaDbContext db,
                CustomerAccounts accounts, CancellationToken ct) =>
            {
                var account = await FindAccountAsync(principal, online, ct);
                if (account is null)
                {
                    return TypedResults.Unauthorized();
                }

                var errors = new Errors();
                CustomerAccounts.ValidateContact(errors, new OnlineContact(input.FirstName, input.LastName, account.Email!, input.Phone), requireEmail: false);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                if (account.CustomerId is { } customerId && await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) is { } customer)
                {
                    customer.FirstName = input.FirstName.Trim();
                    customer.LastName = input.LastName.Trim();
                    customer.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    account.CustomerId = (await accounts.CreateCustomerAsync(input.FirstName, input.LastName, account.Email!, input.Phone, ct)).Id;
                    await online.SaveChangesAsync(ct);
                }

                return TypedResults.Ok((await accounts.SessionAsync(account.Id, ct))!);
            })
            .WithName("UpdateCustomerProfile")
            .WithSummary("Name und Telefon im gemeinsamen Kundenstamm; legt den Kunden an, falls es ihn noch nicht gibt.");

        group.MapGet("/appointments", async Task<Results<Ok<IReadOnlyList<OnlineAppointment>>, UnauthorizedHttpResult>> (
                ClaimsPrincipal principal, OnlineDbContext online, AispaDbContext db, OnlinePortal portal, CancellationToken ct) =>
            {
                var account = await FindAccountAsync(principal, online, ct);
                if (account is null)
                {
                    return TypedResults.Unauthorized();
                }

                if (account.CustomerId is not { } customerId)
                {
                    return TypedResults.Ok<IReadOnlyList<OnlineAppointment>>([]);
                }

                var appointments = await db.Appointments.AsNoTracking().Include(a => a.Treatment)
                    .Where(a => a.CustomerId == customerId)
                    .OrderByDescending(a => a.Start)
                    .Take(100)
                    .ToListAsync(ct);
                return TypedResults.Ok(await portal.ToOnlineAsync(appointments, ct));
            })
            .WithName("ListMyAppointments")
            .WithSummary("Eigene Termine über alle Standorte, neueste zuerst (auch von der Rezeption gebuchte).");

        group.MapPost("/appointments/{appointmentId:guid}/cancel", async Task<Results<Ok<OnlineAppointment>, NotFound, ProblemHttpResult>> (
                Guid appointmentId, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal, CancellationToken ct, bool acceptFee = false) =>
                await FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                    ? OnlineBookingEndpoints.Respond(await portal.CancelAsync(appointmentId, customerId, acceptFee, ct))
                    : TypedResults.NotFound())
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("CancelMyAppointment")
            .WithSummary("Storniert einen eigenen Termin, bis zur Stornofrist; danach nur mit hinterlegter Karte und „acceptFee“.");

        group.MapGet("/appointments/{appointmentId:guid}/calendar.ics", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid appointmentId, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal, TimeProvider time, CancellationToken ct) =>
                await FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                    ? await portal.CalendarFileAsync(appointmentId, customerId, time.GetUtcNow(), ct)
                    : TypedResults.NotFound())
            .WithName("GetMyAppointmentCalendarFile")
            .WithSummary("Eigener Termin als .ics für den eigenen Kalender.");

        group.MapGet("/vouchers", async Task<Results<Ok<IReadOnlyList<OnlineOwnedVoucher>>, UnauthorizedHttpResult>> (
                ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal, CancellationToken ct) =>
            {
                var account = await FindAccountAsync(principal, online, ct);
                if (account is null)
                {
                    return TypedResults.Unauthorized();
                }

                return TypedResults.Ok(account.CustomerId is { } customerId ? await portal.OwnedVouchersAsync(customerId, ct) : []);
            })
            .WithName("ListMyVouchers")
            .WithSummary("Eigene Gutscheine mit Restwert.");

        group.MapPost("/appointments/{appointmentId:guid}/move", async Task<Results<Ok<OnlineAppointment>, NotFound, ProblemHttpResult>> (
                Guid appointmentId, OnlineMoveRequest request, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal,
                CancellationToken ct) =>
                await FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                    ? OnlineBookingEndpoints.Respond(await portal.MoveAsync(appointmentId, customerId, request.Start, ct))
                    : TypedResults.NotFound())
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("MoveMyAppointment")
            .WithSummary("Verschiebt einen eigenen Termin auf eine freie Zeit, bis zur Stornofrist und höchstens zweimal.");

        group.MapPost("/appointments/{appointmentId:guid}/confirm", async Task<Results<Ok<OnlineAppointment>, NotFound, ProblemHttpResult>> (
                Guid appointmentId, ClaimsPrincipal principal, OnlineDbContext online, OnlinePortal portal, CancellationToken ct) =>
                await FindAccountAsync(principal, online, ct) is { CustomerId: { } customerId }
                    ? OnlineBookingEndpoints.Respond(await portal.ConfirmAttendanceAsync(appointmentId, customerId, ct))
                    : TypedResults.NotFound())
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("ConfirmMyAppointment")
            .WithSummary("Sagt für einen eigenen bevorstehenden Termin zu, dass man kommt.");

        return routes;
    }

    public static async Task<CustomerAccount?> FindAccountAsync(ClaimsPrincipal principal, OnlineDbContext online, CancellationToken ct) =>
        CustomerAuth.AccountId(principal) is { } id ? await online.Users.FirstOrDefaultAsync(a => a.Id == id, ct) : null;
}
