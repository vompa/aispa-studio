using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using static AispaStudio.Api.Exports.ExportRequests;

namespace AispaStudio.Api.Exports;

/// <summary>
/// Kennzahlen des Management-Berichts als eine Tabelle (Export/Import, Aufgabe D): Kennzahl, Wert, Vorperiode und Einheit, mit dem Recht der Seite „Berichte“;
/// ohne das Recht „Berichte mit Beträgen“ fehlen die Geldbeträge wie auf der Seite.
/// </summary>
internal static class ReportExports
{
    public static IEndpointRouteBuilder MapReportExportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/reports/locations/{locationId:guid}/management/export", async Task<Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>> (
                Guid locationId, DateOnly? from, DateOnly? until, string? format, ManagementReports reports, AccessService accessService, ExportLog log,
                ManagementDocumentSources sources, ManagementSummaries summaries, ManagementAnalyses analyses, ClaimsPrincipal user, CancellationToken ct,
                string compare = ManagementReports.CompareModes.Previous, string content = Contents.Kpis, bool ai = false, Guid? analysisId = null) =>
            {
                if (ExportFiles.Parse(format) == ExportFormat.Docx)
                {
                    return await WordAsync(locationId, from, until, compare, ai, reports, summaries, accessService, sources, log, analyses, analysisId, user, ct);
                }

                if (TableFormat(format) is not { } kind)
                {
                    return Problem("format", TableFormatsOnly);
                }

                if (content is not (Contents.Kpis or Contents.Report))
                {
                    return Problem("content", "Inhalt: kpis (Kennzahlen) oder report (ganzer Bericht).");
                }

                if (content == Contents.Report && kind != ExportFormat.Xlsx)
                {
                    return Problem("format", "Den ganzen Bericht gibt es als Excel (xlsx).");
                }

                if (RangeError(from, until) is { } rangeError)
                {
                    return Problem("from", rangeError);
                }

                if (!ManagementReports.CompareModes.IsKnown(compare))
                {
                    return Problem("compare", "Vergleich: previous (Zeitraum davor), month (Vormonat) oder year (Vorjahr).");
                }

                var access = (await accessService.CurrentAsync(ct))!;
                var amounts = access.Has(AccessRight.ViewAmountReports);
                // Gästenamen nur im ganzen Bericht und nur mit dem Recht der Kundenliste, wie auf der Seite.
                var guests = content == Contents.Report && access.Has(AccessRight.ViewCalendar);
                if (await reports.ManagementAsync(locationId, from!.Value, until!.Value, amounts, ct, compare, guests) is not { } report)
                {
                    return TypedResults.NotFound();
                }

                if (content == Contents.Report)
                {
                    var analysis = analysisId is { } id ? await analyses.DocumentBlockAsync(id, locationId, from.Value, until.Value, compare, amounts, ct) : null;
                    if (analysisId is not null && analysis is null)
                    {
                        return Problem("analysisId", ManagementAnalyses.NotForThisReport);
                    }

                    return await WorkbookAsync(report, compare, guests, amounts, analysis, sources, user, log, ct);
                }

                var columns = new ExportColumn[]
                {
                    new("Kennzahl", ColumnKind.Text), new("Wert", ColumnKind.Number), new(compare switch { ManagementReports.CompareModes.Month => "Vormonat", ManagementReports.CompareModes.Year => "Vorjahr", _ => "Vorperiode" }, ColumnKind.Number), new("Einheit", ColumnKind.Text),
                };
                var rows = report.Kpis.Select(k => new object?[] { k.Label, Value(k.Value, k.Unit), Value(k.Previous, k.Unit), k.Unit }).ToList();
                var table = new ExportTable("Kennzahlen", columns, rows);
                // Dateiname mit Von und Bis: „Kennzahlen_2026-11-01_bis_2026-11-26.xlsx“.
                var title = FormattableString.Invariant($"Kennzahlen {from.Value:yyyy-MM-dd} bis");
                return await FileAsync(table, kind, "Kennzahlen", $"{Day(from.Value)} bis {Day(until.Value)}", title, until, log, ct);
            })
            .WithTags("Berichte")
            .RequirePermission(AccessRight.ViewFinance)
            .WithName("ExportManagementKpis")
            .WithSummary("Kennzahlen des Management-Berichts für from bis until (höchstens ein Jahr) als Excel oder CSV: Kennzahl, Wert, Vorperiode, Einheit; "
                + "ohne das Recht „Berichte mit Beträgen“ ohne Geldbeträge. Mit content=report der ganze Bericht als Excel-Arbeitsmappe (ein Blatt je Abschnitt, "
                + "Gäste nur mit dem Recht der Kundenliste, Vergleich mit anderen Spas, wenn das Modul eingeschaltet ist). Mit format=docx der ganze Bericht als Word-Datei "
                + "(A4 hoch, Inhalt wie das PDF, der Verlauf als Tabelle, nach jeder Kennzahlen-Gruppe „Anmerkungen:“); content entfällt, ai=true nimmt den schon geschriebenen KI-Kommentar mit Hinweis auf. "
                + "Mit analysisId stehen Arbeitsmappe und Word-Datei mit der übernommenen KI-Auswertung und Hinweis da (fertig, gleicher Standort, Zeitraum, Vergleich und Sicht, sonst ein Fehler).");

        return routes;
    }

    /// <summary>Was der Export enthält: <c>kpis</c> (Standard) die Kennzahlen als eine Tabelle, <c>report</c> den ganzen Bericht als Arbeitsmappe.</summary>
    internal static class Contents
    {
        public const string Kpis = "kpis";
        public const string Report = "report";
    }

    /// <summary>Der ganze Bericht als Arbeitsmappe: Inhalt aus <see cref="ManagementReportDocument"/>, Hinweise als letztes Blatt, ein Eintrag im Export-Protokoll.</summary>
    private static async Task<Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>> WorkbookAsync(ManagementReportDto report, string compare,
        bool guests, bool amounts, ReportTextBlock? analysis, ManagementDocumentSources sources, ClaimsPrincipal user, ExportLog log, CancellationToken ct)
    {
        var header = await sources.Headers.ForAsync(report.LocationId, user, ct);
        var benchmark = await sources.BenchmarkAsync(report, amounts, ct);
        var document = ManagementReportDocument.Build(report, header, compare, guests, benchmark, analysis: analysis);
        IReadOnlyList<ExportTable> tables = [.. document.Sections, document.NotesTable()];
        await log.RecordAsync("Bericht", ExportFormat.Xlsx, tables.Sum(t => t.Rows.Count), $"{Day(report.From)} bis {Day(report.Until)}", ct);
        var title = FormattableString.Invariant($"Bericht {report.From:yyyy-MM-dd} bis");
        return TypedResults.File(TableExport.ToWorkbook(tables, document.Facts), ExportFiles.ContentType(ExportFormat.Xlsx),
            ExportFiles.Name(title, ExportFormat.Xlsx, report.Until));
    }

    /// <summary>Der ganze Bericht als Word-Datei: gleicher Inhalt und gleiche Rechte wie das PDF, ein Eintrag im Export-Protokoll.</summary>
    private static async Task<Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>> WordAsync(Guid locationId, DateOnly? from, DateOnly? until,
        string compare, bool ai, ManagementReports reports, ManagementSummaries summaries, AccessService access, ManagementDocumentSources sources, ExportLog log,
        ManagementAnalyses analyses, Guid? analysisId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (ManagementReportPdfEndpoints.Invalid(from, until, compare) is { } problem)
        {
            return problem;
        }

        if (await ManagementReportPdfEndpoints.LoadAsync(locationId, from!.Value, until!.Value, compare, ai, reports, summaries, access, sources, user, ct, analyses, analysisId)
            is not { } loaded)
        {
            return TypedResults.NotFound();
        }

        if (loaded.AnalysisRejected)
        {
            return Problem("analysisId", ManagementAnalyses.NotForThisReport);
        }

        var word = ManagementReportWord.Build(loaded.Document);
        await log.RecordAsync(ManagementReportPdfEndpoints.Kind, ExportFormat.Docx, ManagementReportPdfEndpoints.RowCount(loaded.Document), loaded.Document.Period, ct);
        return TypedResults.File(word, WordDocumentBuilder.ContentType, ManagementReportWord.FileName(loaded.Document, loaded.Report));
    }

    /// <summary>Euro-Kennzahlen als Betrag mit zwei Nachkommastellen, alle anderen als Zahl.</summary>
    private static object? Value(decimal? value, string unit) =>
        value is { } number && unit == "€" ? new ExportCell(number, ColumnKind.Money) : value;
}
