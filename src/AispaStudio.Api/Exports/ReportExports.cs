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
                CancellationToken ct, string compare = ManagementReports.CompareModes.Previous) =>
            {
                if (TableFormat(format) is not { } kind)
                {
                    return Problem("format", TableFormatsOnly);
                }

                if (RangeError(from, until) is { } rangeError)
                {
                    return Problem("from", rangeError);
                }

                if (!ManagementReports.CompareModes.IsKnown(compare))
                {
                    return Problem("compare", "Vergleich: previous (Zeitraum davor), month (Vormonat) oder year (Vorjahr).");
                }

                var amounts = (await accessService.CurrentAsync(ct))!.Has(AccessRight.ViewAmountReports);
                if (await reports.ManagementAsync(locationId, from!.Value, until!.Value, amounts, ct, compare) is not { } report)
                {
                    return TypedResults.NotFound();
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
                + "ohne das Recht „Berichte mit Beträgen“ ohne Geldbeträge.");

        return routes;
    }

    /// <summary>Euro-Kennzahlen als Betrag mit zwei Nachkommastellen, alle anderen als Zahl.</summary>
    private static object? Value(decimal? value, string unit) =>
        value is { } number && unit == "€" ? new ExportCell(number, ColumnKind.Money) : value;
}
