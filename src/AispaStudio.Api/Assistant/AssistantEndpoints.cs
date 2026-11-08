using AispaStudio.Api.Access;
using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Assistant;

/// <summary>KI-Assistent für Mitarbeitende (je nach Rolle) und für Kunden in der Online-Buchung. Er antwortet und schlägt vor, bucht aber nie selbst.</summary>
internal static class AssistantEndpoints
{
    private const int MaxStaffQuestion = 1000;
    private const int MaxCustomerQuestion = 500;

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/assistant", async Task<Results<Ok<AssistantAnswer>, ValidationProblem>> (
                AssistantQuestion question, AccessService accessService, RoleAssistant assistant, CancellationToken ct) =>
            {
                if (Invalid(question.Question, MaxStaffQuestion) is { } problem)
                {
                    return problem;
                }

                var access = (await accessService.CurrentAsync(ct))!;
                return TypedResults.Ok(await assistant.AskStaffAsync(access, question with { Question = question.Question.Trim() }, ct));
            })
            .RequirePermission(AccessRight.ViewCalendar)
            // Das Backoffice hat keinen Kalender, aber Auswertungen (ADR 0070, 0072): der Assistent antwortet ihm nur dazu.
            .AlsoAllow(AccessRight.ViewFinance, AccessRight.ViewCalendar)
            .WithTags("Assistent")
            .WithName("AskAssistant")
            .WithSummary("Frage an den KI-Assistenten: freie Zeiten, Kunden, Tagesüberblick, Betriebsfragen (Öffnungszeiten, Team, Angebot, Aktionen) und Auswertungen (je nach Rolle). Liefert Antwort, Quelle und Vorschläge, bucht nichts.");

        routes.MapPost("/api/locations/{locationId:guid}/calendar-request", async Task<Results<Ok<CalendarRequestResult>, NotFound, ValidationProblem>> (
                Guid locationId, CalendarRequestInput input, CalendarRequestService requests, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input.Text) && input.Wish is null)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["text"] = ["Bitte den Wunsch eintippen."] });
                }

                if (input.Text?.Length > MaxStaffQuestion)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["text"] = [$"Höchstens {MaxStaffQuestion} Zeichen."] });
                }

                return await requests.ProposeAsync(locationId, input, ct) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound();
            })
            .RequirePermission(AccessRight.Book)
            .WithTags("Assistent")
            .WithName("ProposeFromCalendarRequest")
            .WithSummary("KI-Anfrage im Kalender (Strg+K): Wunsch in eigenen Worten oder geänderte Bausteine; liefert die Bausteine und die drei "
                + "besten freien Zeiten mit Person, Raum und Preis. Die KI liest nur den Text, die Zeiten rechnet der Planer; gebucht wird nichts.");

        // Standort in der Route: daran erkennt der Kundenbereich das Spa (Mandant).
        routes.MapPost("/api/online/locations/{locationId:guid}/assistant", async Task<Results<Ok<OnlineAssistantAnswer>, NotFound, ValidationProblem>> (
                Guid locationId, OnlineAssistantQuestion question, RoleAssistant assistant, OnlinePortal portal, CancellationToken ct) =>
            {
                if (Invalid(question.Question, MaxCustomerQuestion) is { } problem)
                {
                    return problem;
                }

                var answer = await assistant.AskCustomerAsync(locationId, question.Question.Trim(), portal, ct);
                return answer is null ? TypedResults.NotFound() : TypedResults.Ok(answer);
            })
            .AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit)
            .WithTags("Kundenbereich")
            .WithName("AskOnlineAssistant")
            .WithSummary("Terminwunsch in eigenen Worten: bis zu drei freie Zeiten. Gebucht wird über die normale Online-Buchung.");

        return routes;
    }

    private static ValidationProblem? Invalid(string? question, int max) =>
        string.IsNullOrWhiteSpace(question) ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["question"] = ["Bitte eine Frage eingeben."] })
        : question.Length > max ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["question"] = [$"Höchstens {max} Zeichen."] })
        : null;
}
