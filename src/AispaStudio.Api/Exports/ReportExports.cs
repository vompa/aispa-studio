using AispaStudio.Api.Access;
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
                CancellationToken ct) =>
            {
                if (TableFormat(format) is not { } kind)
                {
                    return Problem("format", TableFormatsOnly);
                }

                if (RangeError(from, until) is { } rangeError)
                {
                    return Problem("from", rangeError);
                }

                var amounts = (await accessService.CurrentAsync(ct))!.Has(AccessRight.ViewAmountReports);
                if (await reports.ManagementAsync(locationId, from!.Value, until!.Value, amounts, ct) is not { } report)
                {
                    return TypedResults.NotFound();
                }

                var columns = new ExportColumn[]
                {
                    new("Kennzahl", ColumnKind.Text), new("Wert", ColumnKind.Number), new("Vorperiode", ColumnKind.Number), new("Einheit", ColumnKind.Text),
                };
                var rows = report.Kpis.Select(k => new object?[] { k.Label, k.Value, k.Previous, k.Unit }).ToList();
                var table = new ExportTable("Kennzahlen", columns, rows);
                return await FileAsync(table, kind, "Kennzahlen", $"{Day(from.Value)} bis {Day(until.Value)}", "Kennzahlen", from, log, ct);
            })
            .WithTags("Berichte")
            .RequirePermission(AccessRight.ViewFinance)
            .WithName("ExportManagementKpis")
            .WithSummary("Kennzahlen des Management-Berichts für from bis until (höchstens ein Jahr) als Excel oder CSV: Kennzahl, Wert, Vorperiode, Einheit; "
                + "ohne das Recht „Berichte mit Beträgen“ ohne Geldbeträge.");

        return routes;
    }
}
