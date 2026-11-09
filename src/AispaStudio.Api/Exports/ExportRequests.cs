using System.Globalization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Exports;

/// <summary>Antwort eines Listen-Exports: die Datei, ein Hinweis zur Abfrage, ein unbekannter Standort oder fehlendes Recht.</summary>
internal sealed record ListExportRequest(Location Location, ExportFormat Format, string List);

/// <summary>
/// Gemeinsame Prüfungen und Antworten der Exporte: Format, Zeitraum, Recht der Leitung, Zeitzone des Standorts, Datum im Filtertext und die Datei selbst
/// (höchstens <see cref="TableExport.MaxRows"/> Zeilen, Eintrag im Export-Protokoll).
/// </summary>
internal static class ExportRequests
{
    /// <summary>Längster Zeitraum einer Datei: ein Jahr.</summary>
    public const int MaxDays = 366;

    public const string TableFormatsOnly = "Diese Liste gibt es als Excel (xlsx) oder CSV.";

    public const string LeadershipOnly = "Diese Liste exportiert nur die Leitung.";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Excel oder CSV; andere Formate und Unbekanntes ergeben null.</summary>
    public static ExportFormat? TableFormat(string? text) => ExportFiles.Parse(text) is { } format && format is ExportFormat.Xlsx or ExportFormat.Csv ? format : null;

    /// <summary>Hinweis zum Zeitraum (fehlt, Ende vor Beginn, länger als ein Jahr) oder null.</summary>
    public static string? RangeError(DateOnly? from, DateOnly? until) => (from, until) switch
    {
        ({ } f, { } u) when u < f => "Das Ende liegt vor dem Beginn.",
        ({ } f, { } u) when u.DayNumber - f.DayNumber >= MaxDays => "Höchstens ein Jahr auf einmal.",
        ({ }, { }) => null,
        _ => "Zeitraum fehlt: from und until angeben (JJJJ-MM-TT).",
    };

    public static ValidationProblem Problem(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    public static TimeZoneInfo ZoneOf(Location location) => LocalTime.IsKnownZone(location.TimeZone) ? LocalTime.Zone(location.TimeZone) : TimeZoneInfo.Utc;

    public static string Day(DateOnly date) => date.ToString("d.M.yyyy", German);

    /// <summary>
    /// Prüft eine Abfrage an die Listen von Kassa, Lager und Personal: Format Excel oder CSV, bekannte Liste, Recht der Leitung (Inhaberin, Geschäftsführung), Standort des Mandanten.
    /// </summary>
    public static async Task<(ListExportRequest? Request, Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>? Failure)> LeadershipListAsync(
        Guid locationId, string? list, IReadOnlyCollection<string> lists, string? format, AispaDbContext db, AccessService accessService, CancellationToken ct)
    {
        if (!(await accessService.CurrentAsync(ct))!.IsOwnerLevel)
        {
            return (null, (ProblemHttpResult)PermissionExtensions.Forbidden(LeadershipOnly));
        }

        if (TableFormat(format) is not { } kind)
        {
            return (null, Problem("format", TableFormatsOnly));
        }

        var name = list?.Trim().ToLowerInvariant();
        if (name is null || !lists.Contains(name))
        {
            return (null, Problem("list", $"Liste unbekannt. Möglich sind {string.Join(", ", lists)}."));
        }

        return await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct) is { } location
            ? (new ListExportRequest(location, kind, name), null)
            : (null, TypedResults.NotFound());
    }

    /// <summary>Die fertige Tabelle als Datei; zu viele Zeilen ergeben einen Hinweis auf engere Filter, sonst steht der Export im Protokoll.</summary>
    public static async Task<Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>> FileAsync(ExportTable table, ExportFormat format, string kind,
        string? filter, string title, DateOnly? date, ExportLog log, CancellationToken ct)
    {
        if (table.Rows.Count > TableExport.MaxRows)
        {
            return Problem("from", TableExport.TooLargeMessage);
        }

        await log.RecordAsync(kind, format, table.Rows.Count, filter, ct);
        return TypedResults.File(format == ExportFormat.Xlsx ? TableExport.ToExcel(table) : TableExport.ToCsv(table), ExportFiles.ContentType(format),
            ExportFiles.Name(title, format, date));
    }
}
