using AispaStudio.Api.Access;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>
/// Kalender-Abos (ADR 0075). Lesen, „Im Kalender zeigen“ und „Falsch melden“ mit dem Kalenderrecht; alles andere mit <see cref="AccessRight.ManageLocation"/>,
/// innerhalb der eigenen Standorte. Der Abruf selbst läuft in der Queue: „Jetzt abrufen“ antwortet sofort mit 202.
/// </summary>
internal static class CalendarSubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapCalendarSubscriptionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/calendar-subscriptions").WithTags("Kalender-Abos");

        group.MapGet("/", async (Guid? locationId, CalendarSubscriptionService subscriptions, AccessService access, CancellationToken ct) =>
                await subscriptions.ListAsync((await access.CurrentAsync(ct))!, locationId, ct))
            .WithName("ListCalendarSubscriptions")
            .WithSummary("Kalender-Abos mit Stand des Abrufs; mit Standort nur die dort geltenden. „ShowInCalendar“ gilt für den eigenen Zugang.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/sources", () => CalendarFeedSources.Suggestions(DateTime.UtcNow.Year))
            .WithName("ListCalendarSubscriptionSources")
            .WithSummary("Quellenvorschläge je Land und Region (Feiertage, Schulferien); der Betrieb bestätigt oder tauscht die Quelle.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/", async Task<Results<Created<CalendarSubscriptionDto>, ValidationProblem>> (
                CalendarSubscriptionInput input, CalendarSubscriptionService subscriptions, AccessService access, IMessageBus bus, CancellationToken ct) =>
            {
                var (subscription, errors) = await subscriptions.CreateAsync(input, (await access.CurrentAsync(ct))!, ct);
                if (subscription is null)
                {
                    return errors!.Problem();
                }

                await PublishIfDueAsync(bus, subscription);
                return TypedResults.Created($"/api/calendar-subscriptions/{subscription.Id}", subscription);
            })
            .WithName("CreateCalendarSubscription")
            .WithSummary("Legt ein Abo an. Die Adresse muss mit https:// oder webcal:// beginnen und darf keine Zugangsdaten enthalten; ist das Abo aktiv, wird es sofort im Hintergrund abgerufen.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/{subscriptionId:guid}", async Task<Results<Ok<CalendarSubscriptionDto>, NotFound, ValidationProblem>> (
                Guid subscriptionId, CalendarSubscriptionInput input, CalendarSubscriptionService subscriptions, AccessService access, IMessageBus bus, CancellationToken ct) =>
            {
                var (subscription, errors) = await subscriptions.UpdateAsync(subscriptionId, input, (await access.CurrentAsync(ct))!, ct);
                if (subscription is null)
                {
                    return errors is null ? TypedResults.NotFound() : errors.Problem();
                }

                await PublishIfDueAsync(bus, subscription);
                return TypedResults.Ok(subscription);
            })
            .WithName("UpdateCalendarSubscription")
            .WithSummary("Ändert ein Abo. Eine neue Adresse wird sofort im Hintergrund abgerufen; die bisherige Kopie gilt bis dahin.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("/{subscriptionId:guid}", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                Guid subscriptionId, CalendarSubscriptionService subscriptions, AccessService access, CancellationToken ct) =>
            {
                var (found, error) = await subscriptions.DeleteAsync(subscriptionId, (await access.CurrentAsync(ct))!, ct);
                return !found ? TypedResults.NotFound()
                    : error is not null ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["subscription"] = [error] })
                    : TypedResults.NoContent();
            })
            .WithName("DeleteCalendarSubscription")
            .WithSummary("Löscht ein Abo samt gespeicherter Kopie. Die vorab eingetragenen Abos nach Land lassen sich nur ausschalten.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/{subscriptionId:guid}/active", async Task<Results<Ok<CalendarSubscriptionDto>, NotFound>> (
                Guid subscriptionId, SubscriptionActiveInput input, CalendarSubscriptionService subscriptions, AccessService access, IMessageBus bus, CancellationToken ct) =>
            {
                if (await subscriptions.SetActiveAsync(subscriptionId, input.IsActive, (await access.CurrentAsync(ct))!, ct) is not { } subscription)
                {
                    return TypedResults.NotFound();
                }

                await PublishIfDueAsync(bus, subscription);
                return TypedResults.Ok(subscription);
            })
            .WithName("SetCalendarSubscriptionActive")
            .WithSummary("Schaltet ein Abo für alle ein oder aus: aus heißt kein Abruf und keine Wirkung (Öffnungszeiten, Buchung, Assistent, Kalender). Nur die Leitung.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/{subscriptionId:guid}/show", async Task<Results<Ok<CalendarSubscriptionDto>, NotFound>> (
                Guid subscriptionId, SubscriptionShowInput input, CalendarSubscriptionService subscriptions, AccessService access, CancellationToken ct) =>
                await subscriptions.SetShowAsync(subscriptionId, input.Show, (await access.CurrentAsync(ct))!, ct) is { } subscription
                    ? TypedResults.Ok(subscription)
                    : TypedResults.NotFound())
            .WithName("SetCalendarSubscriptionShow")
            .WithSummary("„Im Kalender zeigen“ für den eigenen Zugang. Ändert nur die Anzeige im Kalender, nicht Öffnungszeiten, Buchung oder Assistent.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPost("/{subscriptionId:guid}/fetch", async Task<Results<Accepted, NotFound, ValidationProblem>> (
                Guid subscriptionId, CalendarSubscriptionService subscriptions, AccessService access, IMessageBus bus, CancellationToken ct) =>
            {
                var (found, error) = await subscriptions.MarkForFetchAsync(subscriptionId, (await access.CurrentAsync(ct))!, ct);
                if (!found)
                {
                    return TypedResults.NotFound();
                }

                if (error is not null)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["subscription"] = [error] });
                }

                await bus.PublishAsync(new FetchCalendarSubscription(subscriptionId));
                return TypedResults.Accepted((string?)null);
            })
            .WithName("FetchCalendarSubscription")
            .WithSummary("Ruft das Abo jetzt ab. Der Abruf läuft im Hintergrund; das Ergebnis steht danach am Abo (Stand, Fehler) und als Meldung im Kalender.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/{subscriptionId:guid}/import", async Task<Results<Ok<SubscriptionRefreshResult>, NotFound, ValidationProblem>> (
                Guid subscriptionId, SubscriptionImportInput input, CalendarSubscriptionService subscriptions, CalendarSubscriptionFetcher fetcher,
                AccessService access, CancellationToken ct) =>
            {
                if (!await subscriptions.CanManageAsync(subscriptionId, (await access.CurrentAsync(ct))!, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(!string.IsNullOrWhiteSpace(input.Content), nameof(input.Content), "Die Datei ist leer.");
                errors.Check((input.Content?.Length ?? 0) <= FeedReader.MaxBytes, nameof(input.Content), $"Die Datei ist größer als {FeedReader.MaxBytes / (1024 * 1024)} MB.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var (result, error) = await fetcher.ImportAsync(subscriptionId, input.Content!, ct);
                if (result is null)
                {
                    errors.Add(nameof(input.Content), error ?? "Die Datei konnte nicht gelesen werden.");
                    return errors.Problem();
                }

                return TypedResults.Ok(result);
            })
            .WithName("ImportCalendarSubscriptionFile")
            .WithSummary("Übernimmt eine Datei (ICS oder CSV mit Datum und Titel) als Kopie des Abos. Derselbe Leser wie beim Abruf, Horizont laufendes Jahr plus zwei.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/{subscriptionId:guid}/conflicts", async Task<Results<Ok<List<SubscriptionConflictDto>>, NotFound>> (
                Guid subscriptionId, CalendarSubscriptionService subscriptions, AccessService access, CancellationToken ct) =>
                await subscriptions.ConflictsAsync(subscriptionId, (await access.CurrentAsync(ct))!, ct) is { } conflicts
                    ? TypedResults.Ok(conflicts)
                    : TypedResults.NotFound())
            .WithName("ListCalendarSubscriptionConflicts")
            .WithSummary("Tage, an denen Abo und Regel-Feiertage sich widersprechen, ab heute, mit der Entscheidung der Leitung. Nichts wird stumm überschrieben.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/{subscriptionId:guid}/decisions", async Task<Results<NoContent, NotFound>> (
                Guid subscriptionId, SubscriptionDecisionInput input, CalendarSubscriptionService subscriptions, AccessService access, CancellationToken ct) =>
                await subscriptions.DecideAsync(subscriptionId, input.Date, input.Decision, (await access.CurrentAsync(ct))!, ct) ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("DecideCalendarSubscriptionConflict")
            .WithSummary("Übernehmen oder ignorieren für einen Tag mit Widerspruch; ohne Entscheidung wird sie zurückgenommen. „Ignorieren“ bei einem Tag nur im Abo: der Tag gilt nicht.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/{subscriptionId:guid}/report", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                Guid subscriptionId, SubscriptionReportInput input, CalendarSubscriptionService subscriptions, AccessService access, CancellationToken ct) =>
            {
                var (found, errors) = await subscriptions.ReportAsync(subscriptionId, input, (await access.CurrentAsync(ct))!, ct);
                return !found ? TypedResults.NotFound() : errors is not null ? errors.Problem() : TypedResults.NoContent();
            })
            .WithName("ReportCalendarSubscriptionDay")
            .WithSummary("„Falsch melden“: erzeugt eine Meldung an die Leitung mit Tag und Grund. Das Abo ändert sich nicht.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/notices", async (Guid? locationId, CalendarSubscriptionService subscriptions, AccessService access, CancellationToken ct) =>
                await subscriptions.NoticesAsync((await access.CurrentAsync(ct))!, locationId, ct))
            .WithName("ListCalendarSubscriptionNotices")
            .WithSummary("Offene Meldungen zu Abos für die Seitenleiste „Meldungen“: Änderung nach dem Abruf, Fehler, Erinnerung im November, „Falsch melden“ (nur Leitung).")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPost("/notices/{noticeId:guid}/resolve", async Task<Results<NoContent, NotFound>> (
                Guid noticeId, CalendarSubscriptionService subscriptions, AccessService access, CancellationToken ct) =>
                await subscriptions.ResolveNoticeAsync(noticeId, (await access.CurrentAsync(ct))!, ct) ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("ResolveCalendarSubscriptionNotice")
            .WithSummary("Markiert eine Meldung zu Abos als erledigt (für alle).")
            .RequirePermission(AccessRight.ViewCalendar);

        return routes;
    }

    /// <summary>Ein aktives Abo mit Adresse und ohne Abruftermin wird sofort in die Queue gestellt (neu angelegt, eingeschaltet, Adresse geändert).</summary>
    private static async Task PublishIfDueAsync(IMessageBus bus, CalendarSubscriptionDto subscription)
    {
        if (subscription.IsActive && subscription.Url.Length > 0 && subscription.NextCheckAt is null)
        {
            await bus.PublishAsync(new FetchCalendarSubscription(subscription.Id));
        }
    }
}
