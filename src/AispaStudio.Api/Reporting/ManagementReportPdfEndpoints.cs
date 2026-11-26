using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Exports;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using static AispaStudio.Api.Exports.ExportRequests;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Management-Bericht als PDF und per Mail (T-095, Paket D2, ADR 0092): Recht wie der Bericht (<c>ViewFinance</c>), Beträge nur mit „Berichte mit Beträgen“, Gästenamen nur mit
/// dem Recht der Kundenliste, wie die Arbeitsmappe. Mit <c>ai=true</c> kommt der schon geschriebene KI-Kommentar zu genau diesen Zahlen mit Hinweis dazu, ein neuer wird
/// nicht angestoßen. Gesendet wird nur an die eingetippte Adresse (vorbelegt ist die eigene); jede Datei und jede Sendung steht im Export-Protokoll.
/// </summary>
internal static class ManagementReportPdfEndpoints
{
    public const string Kind = "Bericht";
    public const string MailKind = "Bericht per Mail";

    public static IEndpointRouteBuilder MapManagementReportPdfEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/reports/locations/{locationId:guid}/management").WithTags("Berichte").RequirePermission(AccessRight.ViewFinance);

        group.MapGet("/pdf", async Task<Results<FileContentHttpResult, ValidationProblem, NotFound>> (
                Guid locationId, DateOnly? from, DateOnly? until, ManagementReports reports, ManagementSummaries summaries, AccessService access,
                ManagementDocumentSources sources, ExportLog log, ClaimsPrincipal user, CancellationToken ct, string compare = ManagementReports.CompareModes.Previous, bool ai = false) =>
            {
                if (Invalid(from, until, compare) is { } problem)
                {
                    return problem;
                }

                if (await LoadAsync(locationId, from!.Value, until!.Value, compare, ai, reports, summaries, access, sources, user, ct) is not { } loaded)
                {
                    return TypedResults.NotFound();
                }

                var pdf = ManagementReportPdf.Build(loaded.Document);
                await log.RecordAsync(Kind, ExportFormat.Pdf, RowCount(loaded.Document), loaded.Document.Period, ct);
                return DocumentFiles.Pdf(pdf, ManagementReportPdf.FileName(loaded.Document, loaded.Report));
            })
            .WithName("ManagementReportPdf")
            .WithSummary("Management-Bericht für from bis until (höchstens ein Jahr) als PDF (A4 hoch): Kennzahlen je Gruppe mit Vergleich (compare wie beim Bericht) und Veränderung, "
                + "Verlauf als Balken, danach die Tabellen des Berichts. Ohne das Recht „Berichte mit Beträgen“ ohne Geldbeträge, Gäste nur mit dem Recht der Kundenliste; "
                + "mit ai=true der schon geschriebene KI-Kommentar mit Hinweis. Steht im Export-Protokoll.");

        group.MapGet("/send", async Task<Results<Ok<DocumentMailDraft>, ValidationProblem, NotFound>> (
                Guid locationId, DateOnly? from, DateOnly? until, ManagementReports reports, ManagementSummaries summaries, AccessService access,
                ManagementDocumentSources sources, AispaDbContext db, ClaimsPrincipal user, CancellationToken ct, string compare = ManagementReports.CompareModes.Previous, bool ai = false) =>
            {
                if (Invalid(from, until, compare) is { } problem)
                {
                    return problem;
                }

                if (await LoadAsync(locationId, from!.Value, until!.Value, compare, ai, reports, summaries, access, sources, user, ct) is not { } loaded)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(ManagementReportPdf.Draft(loaded.Document, await OwnAddressAsync(db, user, ct)));
            })
            .WithName("ManagementReportMailDraft")
            .WithSummary("Vorbelegung für „Per Mail senden“: die eigene Mailadresse der angemeldeten Person, Betreff mit Standort und Zeitraum, kurzer Text.");

        group.MapPost("/send", async Task<Results<Accepted, ValidationProblem, NotFound>> (
                Guid locationId, DateOnly? from, DateOnly? until, SendDocumentRequest request, ManagementReports reports, ManagementSummaries summaries, AccessService access,
                ManagementDocumentSources sources, SentDocuments sent, ExportLog log, TenantContext tenants, IMessageBus bus, ClaimsPrincipal user, CancellationToken ct,
                string compare = ManagementReports.CompareModes.Previous, bool ai = false) =>
            {
                if (Invalid(from, until, compare) is { } problem)
                {
                    return problem;
                }

                if (DocumentMails.Validate(request) is { } errors)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                if (await LoadAsync(locationId, from!.Value, until!.Value, compare, ai, reports, summaries, access, sources, user, ct) is not { } loaded)
                {
                    return TypedResults.NotFound();
                }

                var document = loaded.Document;
                var to = request.To.Trim();
                var (subject, text) = DocumentMails.Resolve(ManagementReportPdf.Draft(document, null), request);
                var fileName = ManagementReportPdf.FileName(document, loaded.Report);
                await bus.PublishAsync(DocumentMails.Message(tenants.TenantId!.Value, to, subject, text, fileName, ManagementReportPdf.Build(document)));
                await sent.RecordAsync(SentDocument.ManagementReportKind, locationId, to, subject, user, ct);
                await log.RecordAsync(MailKind, ExportFormat.Pdf, RowCount(document), $"{document.Period}, an {to}", ct);
                return TypedResults.Accepted($"/api/reports/locations/{locationId}/management");
            })
            .WithName("SendManagementReportPdf")
            .WithSummary("Management-Bericht als PDF per Mail an die eingetippte Adresse (über die Queue); Inhalt wie GET …/management/pdf. Vermerk im Export-Protokoll mit Empfänger.");

        return routes;
    }

    internal sealed record Loaded(ManagementReportDocument Document, ManagementReportDto Report);

    internal static ValidationProblem? Invalid(DateOnly? from, DateOnly? until, string compare) =>
        RangeError(from, until) is { } rangeError ? Problem("from", rangeError)
        : !ManagementReports.CompareModes.IsKnown(compare) ? Problem("compare", "Vergleich: previous (Zeitraum davor), month (Vormonat) oder year (Vorjahr).")
        : null;

    /// <summary>Bericht nach den Rechten der Person, daraus der Berichtskern mit Kopf, Vergleich mit anderen Spas (wenn eingeschaltet) und, mit <paramref name="ai"/>, dem gespeicherten KI-Kommentar.</summary>
    internal static async Task<Loaded?> LoadAsync(Guid locationId, DateOnly from, DateOnly until, string compare, bool ai, ManagementReports reports, ManagementSummaries summaries,
        AccessService accessService, ManagementDocumentSources sources, ClaimsPrincipal user, CancellationToken ct)
    {
        var access = (await accessService.CurrentAsync(ct))!;
        var amounts = access.Has(AccessRight.ViewAmountReports);
        var guests = access.Has(AccessRight.ViewCalendar);
        if (await reports.ManagementAsync(locationId, from, until, amounts, ct, compare, guests) is not { } report)
        {
            return null;
        }

        var header = await sources.Headers.ForAsync(locationId, user, ct);
        var benchmark = await sources.BenchmarkAsync(report, amounts, ct);
        var summary = ai ? await summaries.StoredAsync(report, ct) : null;
        return new Loaded(ManagementReportDocument.Build(report, header, compare, guests, benchmark, summary, summary is null ? null : ManagementSummaries.Notice), report);
    }

    /// <summary>Die Mailadresse des angemeldeten Kontos; null ohne Anmeldung oder Adresse.</summary>
    private static async Task<string?> OwnAddressAsync(AispaDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Email).FirstOrDefaultAsync(ct) : null;

    internal static int RowCount(ManagementReportDocument document) => document.Sections.Sum(s => s.Rows.Count);
}
