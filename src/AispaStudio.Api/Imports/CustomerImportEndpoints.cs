using System.Text.Json;
using AispaStudio.Api.Access;
using AispaStudio.Api.Exports;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Importing;

/// <summary>
/// Kunden-Import (Export/Import, Aufgabe E): Vorschau mit Zuordnung, Zählern und Dubletten, Import oder Probelauf, Fehlerzeilen als Excel. Nur für Inhaberin und Geschäftsführung.
/// Die Datei kommt bei jedem Schritt neu mit (<c>multipart/form-data</c>), die API hält nichts zwischen den Schritten fest.
/// </summary>
internal static class CustomerImportEndpoints
{
    private const string LeadershipOnly = "Kunden importiert nur die Leitung.";
    private const int Samples = 3;

    private sealed record Upload(string FileName, ImportSheet Sheet, CustomerImportMapping Mapping);

    public static IEndpointRouteBuilder MapCustomerImportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/customers/import").WithTags("Kunden").RequirePermission(AccessRight.ViewCalendar).DisableAntiforgery();

        group.MapPost("/preview", async Task<Results<Ok<CustomerImportPreview>, ValidationProblem, ProblemHttpResult>> (
                IFormFile file, [FromForm] string? mapping, AccessService access, CustomerImport import, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
                CancellationToken ct) =>
            {
                if (await access.CurrentAsync(ct) is not { IsOwnerLevel: true })
                {
                    return (ProblemHttpResult)PermissionExtensions.Forbidden(LeadershipOnly);
                }

                var (upload, problem) = await ReadAsync(file, mapping, json.Value.SerializerOptions, ct);
                if (upload is null)
                {
                    return problem!;
                }

                var rows = await import.AnalyzeAsync(upload.Sheet, upload.Mapping, ct);
                var resolved = upload.Mapping with
                {
                    DateFormat = upload.Mapping.DateFormat == ImportDateFormat.Auto
                        ? CustomerImport.DetectedDateFormat(upload.Sheet, upload.Mapping)
                        : upload.Mapping.DateFormat,
                };
                return TypedResults.Ok(new CustomerImportPreview(upload.FileName, rows.Count, Columns(upload.Sheet), resolved,
                    rows.Count(r => r.Status == ImportRowStatus.New), rows.Count(r => r.Status == ImportRowStatus.Duplicate), rows.Count(r => r.Status == ImportRowStatus.Invalid),
                    rows.Take(CustomerImport.PreviewRows).Select(r => Preview(r, upload.Mapping)).ToList(),
                    rows.Where(r => r.Status == ImportRowStatus.Duplicate).Select(r => Preview(r, upload.Mapping)).ToList(),
                    rows.Where(r => r.Status == ImportRowStatus.Invalid).Select(r => Preview(r, upload.Mapping)).ToList()));
            })
            .WithName("PreviewCustomerImport")
            .WithSummary("Liest eine Excel- oder CSV-Datei (bis 10 MB, 20 000 Zeilen, erste Zeile Spaltenköpfe) und zeigt Spalten mit Beispielen, die Zuordnung (ohne Angabe nach den "
                + "Spaltenköpfen vorgeschlagen), Zähler neu, Dubletten und fehlerhaft, die ersten 20 Zeilen sowie alle Dubletten und Fehler. Ändert nichts.");

        group.MapPost("/", async Task<Results<Ok<CustomerImportResult>, ValidationProblem, ProblemHttpResult>> (
                IFormFile file, [FromForm] string? mapping, [FromForm] string? decisions, [FromForm] bool? dryRun, AccessService access, CustomerImport import,
                IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, CancellationToken ct) =>
            {
                if (await access.CurrentAsync(ct) is not { IsOwnerLevel: true } current)
                {
                    return (ProblemHttpResult)PermissionExtensions.Forbidden(LeadershipOnly);
                }

                var (upload, problem) = await ReadAsync(file, mapping, json.Value.SerializerOptions, ct);
                if (upload is null)
                {
                    return problem!;
                }

                if (!TryDecisions(decisions, json.Value.SerializerOptions, out var chosen))
                {
                    return ExportRequests.Problem("decisions", "Die Entscheidungen zu den Dubletten sind nicht lesbar.");
                }

                var rows = await import.AnalyzeAsync(upload.Sheet, upload.Mapping, ct);
                return TypedResults.Ok(await import.RunAsync(rows, chosen, dryRun ?? false, upload.FileName, current.UserId, ct));
            })
            .WithName("ImportCustomers")
            .WithSummary("Importiert die Gäste der Datei mit derselben Zuordnung wie die Vorschau, in Blöcken zu 200 und mit dem Kanal „Import“. Dubletten werden übersprungen, außer "
                + "die Entscheidung je Zeile sagt „Merge“ (leere Felder ergänzen, nie überschreiben). Mit dryRun=true nur zählen (Probelauf). Jeder Import steht im Verlauf.");

        group.MapPost("/errors", async Task<Results<FileContentHttpResult, ValidationProblem, ProblemHttpResult>> (
                IFormFile file, [FromForm] string? mapping, AccessService access, CustomerImport import, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
                TimeProvider time, CancellationToken ct) =>
            {
                if (await access.CurrentAsync(ct) is not { IsOwnerLevel: true })
                {
                    return (ProblemHttpResult)PermissionExtensions.Forbidden(LeadershipOnly);
                }

                var (upload, problem) = await ReadAsync(file, mapping, json.Value.SerializerOptions, ct);
                if (upload is null)
                {
                    return problem!;
                }

                var rows = await import.AnalyzeAsync(upload.Sheet, upload.Mapping, ct);
                var table = new ExportTable("Importfehler",
                    [new ExportColumn("Zeile", ColumnKind.Number), new ExportColumn("Grund", ColumnKind.Text), .. upload.Sheet.Headers.Select(h => new ExportColumn(h, ColumnKind.Text))],
                    rows.Where(r => r.Status == ImportRowStatus.Invalid)
                        .Select(r => (object?[])[r.Line.Number, r.Reason, .. upload.Sheet.Headers.Select((_, i) => r.Line.Cell(i))]).ToList());
                return TypedResults.File(TableExport.ToExcel(table), ExportFiles.ContentType(ExportFormat.Xlsx),
                    ExportFiles.Name("Importfehler", ExportFormat.Xlsx, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime)));
            })
            .WithName("GetCustomerImportErrors")
            .WithSummary("Die fehlerhaften Zeilen der Datei als Excel: Zeile, Grund und die Spalten der Datei, zum Berichtigen und erneuten Importieren.");

        return routes;
    }

    /// <summary>Liest Datei und Zuordnung; ohne Zuordnung gilt der Vorschlag nach den Spaltenköpfen.</summary>
    private static async Task<(Upload? Upload, ValidationProblem? Problem)> ReadAsync(IFormFile file, string? mappingJson, JsonSerializerOptions options, CancellationToken ct)
    {
        var name = Path.GetFileName(file.FileName ?? "");
        if (!ImportFile.IsExcel(name) && !ImportFile.IsCsv(name))
        {
            return (null, ExportRequests.Problem("file", "Bitte eine Excel-Datei (.xlsx) oder CSV-Datei wählen."));
        }

        if (file.Length is 0 or > CustomerImport.MaxBytes)
        {
            return (null, ExportRequests.Problem("file", file.Length == 0 ? "Die Datei ist leer." : "Die Datei ist größer als 10 MB. Bitte in mehrere Dateien aufteilen."));
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        var sheet = ImportFile.Read(buffer.ToArray(), name);
        if (sheet is null || sheet.Headers.All(h => h.Length == 0))
        {
            return (null, ExportRequests.Problem("file", "Die Datei lässt sich nicht lesen. In der ersten Zeile müssen die Spaltenköpfe stehen."));
        }

        if (sheet.Lines.Count > CustomerImport.MaxRows)
        {
            return (null, ExportRequests.Problem("file", $"Die Datei hat {sheet.Lines.Count} Zeilen; höchstens 20 000 je Import. Bitte in mehrere Dateien aufteilen."));
        }

        if (string.IsNullOrWhiteSpace(mappingJson))
        {
            return (new Upload(name, sheet, new CustomerImportMapping(CustomerImportFields.SuggestAll(sheet.Headers))), null);
        }

        CustomerImportMapping? mapping;
        try
        {
            mapping = JsonSerializer.Deserialize<CustomerImportMapping>(mappingJson, options);
        }
        catch (JsonException)
        {
            mapping = null;
        }

        return MappingError(mapping, sheet) is { } error
            ? (null, ExportRequests.Problem("mapping", error))
            : (new Upload(name, sheet, mapping!), null);
    }

    private static string? MappingError(CustomerImportMapping? mapping, ImportSheet sheet)
    {
        if (mapping?.Columns is not { } columns || !Enum.IsDefined(mapping.DateFormat) || columns.Any(c => !Enum.IsDefined(c.Field)))
        {
            return "Die Zuordnung der Spalten ist nicht lesbar.";
        }

        if (columns.Any(c => c.Column < 0 || c.Column >= sheet.Headers.Count))
        {
            return "Die Zuordnung nennt eine Spalte, die es in der Datei nicht gibt.";
        }

        if (columns.GroupBy(c => c.Field).Any(g => g.Count() > 1) || columns.GroupBy(c => c.Column).Any(g => g.Count() > 1))
        {
            return "Jedes Feld und jede Spalte nur einmal zuordnen.";
        }

        return columns.Any(c => c.Field == CustomerImportField.LastName) ? null : "Eine Spalte dem Nachnamen zuordnen.";
    }

    private static bool TryDecisions(string? json, JsonSerializerOptions options, out Dictionary<int, ImportDuplicateAction> decisions)
    {
        decisions = [];
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<ImportDuplicateDecision>>(json, options) ?? [];
            if (list.Any(d => !Enum.IsDefined(d.Action)))
            {
                return false;
            }

            decisions = list.GroupBy(d => d.Row).ToDictionary(g => g.Key, g => g.Last().Action);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<ImportColumn> Columns(ImportSheet sheet) =>
        sheet.Headers.Select((header, i) => new ImportColumn(i, header,
            sheet.Lines.Select(l => l.Cell(i)).Where(v => v.Length > 0).Take(Samples).ToList(), CustomerImportFields.Suggest(header))).ToList();

    private static ImportRowPreview Preview(CustomerImportRow row, CustomerImportMapping mapping)
    {
        string Raw(CustomerImportField field) => mapping.Columns.FirstOrDefault(c => c.Field == field) is { } column ? row.Line.Cell(column.Column) : "";

        var name = row.Input is { } input ? $"{input.FirstName} {input.LastName}" : $"{Raw(CustomerImportField.FirstName)} {Raw(CustomerImportField.LastName)}";
        return new ImportRowPreview(row.Line.Number, row.Status, name.Trim(), row.Input?.Email ?? Blank(Raw(CustomerImportField.Email)),
            row.StoredPhone ?? Blank(Raw(CustomerImportField.Phone)), row.Reason, row.ExistingCustomerId);
    }

    private static string? Blank(string value) => value.Length == 0 ? null : value;
}
