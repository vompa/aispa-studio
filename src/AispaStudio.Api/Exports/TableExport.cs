using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace AispaStudio.Api.Exports;

/// <summary>
/// Schreibt eine <see cref="ExportTable"/> als Excel (ClosedXML: Kopfzeile fett, Autofilter, Spaltenbreite nach Inhalt, Beträge und Datum als echte Zellen) oder als
/// CSV (UTF-8 mit BOM, Strichpunkt, Anführungszeichen bei Bedarf, Datum TT.MM.JJJJ, Beträge mit Komma), wie Excel in Österreich und Deutschland sie direkt öffnet.
/// </summary>
internal static class TableExport
{
    /// <summary>Höchstens so viele Zeilen je Datei; darüber muss der Filter enger werden.</summary>
    public const int MaxRows = 50_000;

    public const string TooLargeMessage = "Mehr als 50.000 Zeilen. Zeitraum oder Filter enger wählen.";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static bool TooLarge(ExportTable table) => table.Rows.Count > MaxRows;

    public static byte[] ToExcel(ExportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName(table.Name));
        for (var column = 0; column < table.Columns.Count; column++)
        {
            var header = sheet.Cell(1, column + 1);
            header.Value = table.Columns[column].Name;
            header.Style.Font.Bold = true;
        }

        for (var row = 0; row < table.Rows.Count; row++)
        {
            for (var column = 0; column < table.Columns.Count; column++)
            {
                Fill(sheet.Cell(row + 2, column + 1), table.Columns[column].Kind, table.Rows[row][column]);
            }
        }

        // Breite nach dem längsten Inhalt der Spalte (ohne Schriftvermessung, damit es auf jedem Server gleich aussieht).
        for (var column = 0; column < table.Columns.Count; column++)
        {
            var longest = table.Rows.Select(r => CsvText(table.Columns[column].Kind, r[column]).Length).Prepend(table.Columns[column].Name.Length).Max();
            sheet.Column(column + 1).Width = Math.Clamp(longest * 1.1 + 2, 8, 60);
        }

        sheet.RangeUsed()?.SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] ToCsv(ExportTable table)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(';', table.Columns.Select(c => Quote(c.Name)))).Append("\r\n");
        foreach (var row in table.Rows)
        {
            builder.Append(string.Join(';', table.Columns.Select((c, i) => Quote(CsvText(c.Kind, row[i]))))).Append("\r\n");
        }

        return [.. Utf8WithBom.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    private static void Fill(IXLCell cell, ColumnKind kind, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.Format = "DD.MM.YYYY";
                return;
            case TimeOnly time:
                cell.Value = time.ToTimeSpan();
                cell.Style.NumberFormat.Format = "HH:MM";
                return;
            case bool yes:
                cell.Value = yes ? "Ja" : "Nein";
                return;
            case decimal amount:
                cell.Value = amount;
                cell.Style.NumberFormat.Format = kind == ColumnKind.Money ? "#,##0.00" : "0.####";
                return;
            case int number:
                cell.Value = number;
                return;
            case long number:
                cell.Value = number;
                return;
            case double number:
                cell.Value = number;
                return;
            default:
                cell.Value = value.ToString();
                return;
        }
    }

    /// <summary>Schreibweise eines Werts in der CSV-Datei (und für die Spaltenbreite in Excel).</summary>
    private static string CsvText(ColumnKind kind, object? value) => value switch
    {
        null => "",
        DateOnly date => date.ToString("dd.MM.yyyy", German),
        TimeOnly time => time.ToString("HH:mm", German),
        bool yes => yes ? "Ja" : "Nein",
        decimal amount => amount.ToString(kind == ColumnKind.Money ? "0.00" : "0.####", German),
        int number => number.ToString(German),
        long number => number.ToString(German),
        double number => number.ToString("0.####", German),
        _ => value.ToString() ?? "",
    };

    private static string Quote(string text) =>
        text.AsSpan().IndexOfAny(";\"\r\n") >= 0 ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;

    /// <summary>Excel erlaubt 31 Zeichen und keines von <c>[]:*?/\</c> im Blattnamen.</summary>
    private static string SheetName(string name)
    {
        var cleaned = new string([.. name.Where(c => c is not ('[' or ']' or ':' or '*' or '?' or '/' or '\\'))]).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = "Export";
        }

        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
