using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using static AispaStudio.Api.Exports.ExportRequests;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// „Mit KI auswerten“ (T-096, Paket E1): anlegen und nachfragen. Recht wie der Bericht (<c>ViewFinance</c>); die Sicht mit oder ohne Beträge folgt dem Recht
/// „Berichte mit Beträgen“ der Person, eine Auswertung mit Beträgen sieht nur, wer dieses Recht hat. Ohne Claude-Schlüssel lehnt der Server ab
/// (<see cref="ManagementAnalysisDto.UnavailableHint"/>); die App sperrt den Knopf schon vorher über <see cref="ManagementReportDto.AiAnalysisAvailable"/>.
/// </summary>
internal static class ManagementAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapManagementAnalysisEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/reports/locations/{locationId:guid}/management/analysis").WithTags("Berichte").RequirePermission(AccessRight.ViewFinance);

        group.MapPost("", async Task<Results<Ok<ManagementAnalysisDto>, ValidationProblem, NotFound>> (
                Guid locationId, ManagementAnalysisRequest request, ManagementAnalyses analyses, AccessService accessService, CancellationToken ct) =>
            {
                if (Invalid(request, analyses.Enabled) is { } problem)
                {
                    return problem;
                }

                var access = (await accessService.CurrentAsync(ct))!;
                return await analyses.RequestAsync(locationId, request, access.Has(AccessRight.ViewAmountReports), access.UserId, ct) is { } analysis
                    ? TypedResults.Ok(analysis)
                    : TypedResults.NotFound();
            })
            .WithName("RequestManagementAnalysis")
            .WithSummary("KI-Auswertung des Management-Berichts anlegen (Zeitraum und Vergleich wie beim Bericht, Schwerpunkt, eigene Frage bis 300 Zeichen); "
                + "geschrieben wird über die Queue, Status mit GET …/analysis/{id}. Zu denselben Zahlen, Schwerpunkt und Frage kommt die gespeicherte Auswertung zurück. "
                + "Ohne Claude-Schlüssel ein Fehler mit Hinweis.");

        group.MapGet("/{id:guid}", async Task<Results<Ok<ManagementAnalysisDto>, NotFound>> (
                Guid locationId, Guid id, ManagementAnalyses analyses, AccessService accessService, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                return await analyses.GetAsync(locationId, id, access.Has(AccessRight.ViewAmountReports), ct) is { } analysis
                    ? TypedResults.Ok(analysis)
                    : TypedResults.NotFound();
            })
            .WithName("GetManagementAnalysis")
            .WithSummary("Stand einer KI-Auswertung: Pending (läuft), Done mit Ergebnis oder Failed (nichts anzeigen, erneut anlegen). Immer mit Hinweis auf die KI.");

        return routes;
    }

    private static ValidationProblem? Invalid(ManagementAnalysisRequest request, bool enabled) =>
        !enabled ? Problem("analysis", ManagementAnalysisDto.UnavailableHint)
        : RangeError(request.From, request.Until) is { } rangeError ? Problem("from", rangeError)
        : !ManagementReports.CompareModes.IsKnown(request.Compare) ? Problem("compare", "Vergleich: previous (Zeitraum davor), month (Vormonat) oder year (Vorjahr).")
        : !Enum.IsDefined(request.Focus) ? Problem("focus", "Unbekannter Schwerpunkt.")
        : request.Question?.Trim().Length > ManagementAnalyses.MaxQuestionLength ? Problem("question", $"Die eigene Frage hat höchstens {ManagementAnalyses.MaxQuestionLength} Zeichen.")
        : null;
}
