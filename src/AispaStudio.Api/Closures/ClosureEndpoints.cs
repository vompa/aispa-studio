using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Schließzeiten je Standort (ADR 0073): lesen für jeden mit Kalender, verwalten mit <see cref="AccessRight.ManageLocation"/>; die Absage der Termine
/// zusätzlich mit <see cref="AccessRight.Book"/>. Der Standort steht in der Route, der Zugriffsfilter prüft ihn.
/// </summary>
internal static class ClosureEndpoints
{
    public static IEndpointRouteBuilder MapClosureEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/locations/{locationId:guid}/closures").WithTags("Schließzeiten");

        group.MapGet("/", async Task<Results<Ok<List<ClosureDto>>, NotFound>> (Guid locationId, ClosureService closures, CancellationToken ct) =>
                await closures.ListAsync(locationId, ct) is { } list ? TypedResults.Ok(list) : TypedResults.NotFound())
            .WithName("ListClosures")
            .WithSummary("Alle Schließzeiten des Standorts mit Zustand (geplant, aktiv, beendet), neueste zuerst, und der Zahl der noch offenen Termine darin.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/{closureId:guid}", async Task<Results<Ok<ClosureDto>, NotFound>> (Guid locationId, Guid closureId, ClosureService closures, CancellationToken ct) =>
                await closures.GetAsync(locationId, closureId, ct) is { } closure ? TypedResults.Ok(closure) : TypedResults.NotFound())
            .WithName("GetClosure")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPost("/", async Task<Results<Created<ClosureDto>, NotFound, ValidationProblem>> (
                Guid locationId, ClosureInput input, ClosureService closures, AccessService access, CancellationToken ct) =>
            {
                var (closure, errors) = await closures.CreateAsync(locationId, input, (await access.CurrentAsync(ct))!, ct);
                return closure is not null ? TypedResults.Created($"/api/locations/{locationId}/closures/{closure.Id}", closure)
                    : errors is not null ? errors.Problem() : TypedResults.NotFound();
            })
            .WithName("CreateClosure")
            .WithSummary("Legt eine Schließzeit an: Zeitraum (mit Uhrzeit für halbe Tage), Art, interner Grund und Text für Gäste. "
                + "Danach gibt es in der Zeit keine freien Zeiten mehr; bestehende Termine bleiben, bis sie abgesagt werden.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/{closureId:guid}", async Task<Results<Ok<ClosureDto>, NotFound, ValidationProblem>> (
                Guid locationId, Guid closureId, ClosureInput input, ClosureService closures, CancellationToken ct) =>
            {
                var (closure, errors) = await closures.UpdateAsync(locationId, closureId, input, ct);
                return closure is not null ? TypedResults.Ok(closure) : errors is not null ? errors.Problem() : TypedResults.NotFound();
            })
            .WithName("UpdateClosure")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("/{closureId:guid}", async Task<Results<NoContent, NotFound, Conflict<string>>> (Guid locationId, Guid closureId, ClosureService closures, CancellationToken ct) =>
            {
                var (notFound, kept) = await closures.DeleteAsync(locationId, closureId, ct);
                return notFound ? TypedResults.NotFound()
                    : kept ? TypedResults.Conflict("Wegen dieser Schließzeit wurden schon Termine abgesagt; sie bleibt als Verlauf stehen.")
                    : TypedResults.NoContent();
            })
            .WithName("DeleteClosure")
            .WithSummary("Löscht eine Schließzeit. Wurden wegen ihr schon Termine abgesagt, bleibt sie als Verlauf stehen (409).")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/{closureId:guid}/cancellation", async Task<Results<Ok<ClosureCancellationPreview>, NotFound>> (
                Guid locationId, Guid closureId, ClosureCancellations cancellations, CancellationToken ct) =>
                await cancellations.PreviewAsync(locationId, closureId, ct) is { } preview ? TypedResults.Ok(preview) : TypedResults.NotFound())
            .WithName("PreviewClosureCancellation")
            .WithSummary("Die Rückfrage zum Absagen: die offenen Termine in der Schließzeit (Gast, Zeit, Behandlung, Kanal) und der Vorschlag für Betreff und Text. Ändert nichts.")
            .RequirePermission(AccessRight.ManageLocation)
            .AlsoRequire(AccessRight.Book);

        group.MapPost("/{closureId:guid}/cancellation", async Task<Results<Ok<CancelClosureAppointmentsResult>, NotFound, ValidationProblem>> (
                Guid locationId, Guid closureId, CancelClosureAppointmentsRequest request, ClosureCancellations cancellations, AccessService access, CancellationToken ct) =>
            {
                var (result, errors) = await cancellations.CancelAsync(locationId, closureId, request, (await access.CurrentAsync(ct))!, ct);
                return result is not null ? TypedResults.Ok(result) : errors is not null ? errors.Problem() : TypedResults.NotFound();
            })
            .WithName("CancelClosureAppointments")
            .WithSummary("Sagt die gewählten Termine der Schließzeit ab (storniert durch den Betrieb, kein Ausfallhonorar) und stellt je Termin genau eine Nachricht an den Gast in die Queue. "
                + "Geschieht nur mit dieser Anfrage, nie automatisch.")
            .RequirePermission(AccessRight.ManageLocation)
            .AlsoRequire(AccessRight.Book);

        group.MapPost("/{closureId:guid}/cancellation/revise", async Task<Results<Ok<ReviseClosureTextResult>, NotFound, ValidationProblem>> (
                Guid locationId, Guid closureId, ReviseClosureTextRequest request, ClosureService closures, ClosureCancellations cancellations, CancellationToken ct) =>
            {
                if (await closures.FindByIdAsync(locationId, closureId, ct) is null)
                {
                    return TypedResults.NotFound();
                }

                var (text, problem) = await cancellations.ReviseAsync(request, ct);
                if (text is not null)
                {
                    return TypedResults.Ok(new ReviseClosureTextResult(text));
                }

                var errors = new Errors();
                errors.Add(nameof(request.Text), problem!);
                return errors.Problem();
            })
            .WithName("ReviseClosureCancellationText")
            .WithSummary("Lässt die KI den Text der Absage umformulieren, auf Knopfdruck. Neue Zahlen oder verlorene Platzhalter verwirft der Server; der Text trägt danach den Hinweis auf die KI.")
            .RequirePermission(AccessRight.ManageLocation)
            .AlsoRequire(AccessRight.Book);

        group.MapGet("/{closureId:guid}/notices", async Task<Results<Ok<List<ClosureNoticeDto>>, NotFound>> (
                Guid locationId, Guid closureId, ClosureCancellations cancellations, CancellationToken ct) =>
                await cancellations.NoticesAsync(locationId, closureId, ct) is { } notices ? TypedResults.Ok(notices) : TypedResults.NotFound())
            .WithName("ListClosureNotices")
            .WithSummary("Protokoll der Absagen dieser Schließzeit: je Termin wer, wann, über welchen Kanal und mit welchem Text.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/{closureId:guid}/notices/{noticeId:guid}/send-now", async Task<Results<Ok<ClosureNoticeDto>, NotFound, Conflict<string>>> (
                Guid locationId, Guid closureId, Guid noticeId, ClosureCancellations cancellations, CancellationToken ct) =>
            {
                var (notice, change) = await cancellations.SendNowAsync(locationId, closureId, noticeId, ct);
                return change == NoticeChange.NotFound ? TypedResults.NotFound()
                    : change == NoticeChange.TooLate ? TypedResults.Conflict(ClosureCancellations.NoticeTooLate) : TypedResults.Ok(notice!);
            })
            .WithName("SendClosureNoticeNow")
            .WithSummary("Schickt eine Nachricht, die noch auf ihre Wartezeit wartet, sofort. Ist sie schon gesendet oder zurückgenommen, 409.")
            .RequirePermission(AccessRight.ManageLocation)
            .AlsoRequire(AccessRight.Book);

        group.MapPost("/{closureId:guid}/notices/{noticeId:guid}/cancel", async Task<Results<Ok<ClosureNoticeDto>, NotFound, Conflict<string>>> (
                Guid locationId, Guid closureId, Guid noticeId, ClosureCancellations cancellations, CancellationToken ct) =>
            {
                var (notice, change) = await cancellations.WithdrawAsync(locationId, closureId, noticeId, ct);
                return change == NoticeChange.NotFound ? TypedResults.NotFound()
                    : change == NoticeChange.TooLate ? TypedResults.Conflict(ClosureCancellations.NoticeTooLate) : TypedResults.Ok(notice!);
            })
            .WithName("WithdrawClosureNotice")
            .WithSummary("Nimmt eine Nachricht zurück, die noch auf ihre Wartezeit wartet („Nicht senden“): sie geht nie raus. Der Termin bleibt abgesagt. Ist sie schon gesendet, 409.")
            .RequirePermission(AccessRight.ManageLocation)
            .AlsoRequire(AccessRight.Book);

        var delay = routes.MapGroup("/api/locations/{locationId:guid}/guest-message-delay").WithTags("Schließzeiten");

        delay.MapGet("/", async Task<Results<Ok<GuestMessageDelayDto>, NotFound>> (Guid locationId, AispaDbContext db, CancellationToken ct) =>
                await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => (int?)l.GuestMessageDelayMinutes).FirstOrDefaultAsync(ct) is { } minutes
                    ? TypedResults.Ok(new GuestMessageDelayDto(minutes)) : TypedResults.NotFound())
            .WithName("GetGuestMessageDelay")
            .WithSummary("Wartezeit in Minuten, die eine Nachricht an den Gast nach einer Absage des Teams wartet (Standard 10).")
            .RequirePermission(AccessRight.ManageLocation);

        delay.MapPut("/", async Task<Results<Ok<GuestMessageDelayDto>, NotFound, ValidationProblem>> (Guid locationId, GuestMessageDelayDto input, AispaDbContext db, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Check(input.Minutes is >= 0 and <= GuestMessageTiming.MaxDelayMinutes, nameof(input.Minutes),
                    $"Die Wartezeit liegt zwischen 0 und {GuestMessageTiming.MaxDelayMinutes} Minuten.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                if (await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct) is not { } location)
                {
                    return TypedResults.NotFound();
                }

                location.GuestMessageDelayMinutes = input.Minutes;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(input);
            })
            .WithName("SetGuestMessageDelay")
            .WithSummary("Stellt die Wartezeit vor der Nachricht an den Gast ein (0 bis 60 Minuten). Gäste, die selbst auslösen, bekommen ihre Nachricht sofort.")
            .RequirePermission(AccessRight.ManageLocation);

        return routes;
    }

    /// <summary>Verlangt zusätzlich zum Recht des Endpunkts ein zweites (hier: Absagen braucht auch das Recht zu buchen).</summary>
    private static RouteHandlerBuilder AlsoRequire(this RouteHandlerBuilder builder, AccessRight right) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var access = await context.HttpContext.RequestServices.GetRequiredService<AccessService>().CurrentAsync(context.HttpContext.RequestAborted);
            return access is { } granted && granted.Has(right)
                ? await next(context)
                : PermissionExtensions.Forbidden("Dafür braucht es auch das Recht zu buchen.");
        });
}
