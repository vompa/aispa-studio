using System.Net;
using ClosedXML.Excel;

namespace AispaStudio.Api.Tests;

/// <summary>Antworten der Exporte im Test: Dateiname aus dem Content-Disposition, das eine Tabellenblatt einer Excel-Datei (als Beispiel gespeichert) und seine Spaltenköpfe.</summary>
internal static class ExportResponses
{
    public static string FileName(HttpResponseMessage response) =>
        (response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? "").Trim('"');

    public static async Task<IXLWorksheet> SheetAsync(HttpResponseMessage response, CancellationToken ct)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        SampleFiles.Save(FileName(response), bytes);
        return Assert.Single(new XLWorkbook(new MemoryStream(bytes)).Worksheets);
    }

    public static List<string> Headers(IXLWorksheet sheet) => [.. sheet.Row(1).CellsUsed().Select(c => c.GetString())];
}
