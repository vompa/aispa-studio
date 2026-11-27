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
        WriteSheet(workbook, table);
        return Save(workbook);
    }

    /// <summary>
    /// Arbeitsmappe mit einem Blatt je Tabelle, druckfertig: A4 quer, Kopfzeile wiederholt, „Seite n von m“ im Fuß, Datenbalken in den Spalten mit
    /// <see cref="ColumnHint.Bar"/>, graue Pfeile in den Spalten mit <see cref="ColumnHint.Change"/>. <paramref name="facts"/> steht als Kopfblock rechts
    /// neben der ersten Tabelle (Betrieb, Zeitraum …), damit deren Spalten und Kopfzeile in Zeile 1 bleiben.
    /// </summary>
    public static byte[] ToWorkbook(IReadOnlyList<ExportTable> tables, IReadOnlyList<KeyValuePair<string, string>> facts)
    {
        using var workbook = new XLWorkbook();
        foreach (var table in tables)
        {
            var sheet = WriteSheet(workbook, table);
            Emphasize(sheet, table);
            PrintSetup(sheet);
        }

        if (tables.Count > 0)
        {
            WriteFacts(workbook.Worksheet(1), tables[0].Columns.Count + 2, facts);
        }

        return Save(workbook);
    }

    private static IXLWorksheet WriteSheet(XLWorkbook workbook, ExportTable table)
    {
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
        return sheet;
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Datenbalken in Grau für Größen, der graue Pfeil-Symbolsatz für Veränderungen (unter 0 ab, 0 waagrecht, über 0 auf).</summary>
    private static void Emphasize(IXLWorksheet sheet, ExportTable table)
    {
        if (table.Rows.Count == 0)
        {
            return;
        }

        for (var column = 0; column < table.Columns.Count; column++)
        {
            var cells = sheet.Range(2, column + 1, table.Rows.Count + 1, column + 1);
            switch (table.Columns[column].Hint)
            {
                case ColumnHint.Bar when table.Columns[column].Kind == ColumnKind.Percent:
                    // Prozent von 0 bis 100 %, damit 5 % nicht wie ein voller Balken aussehen (Zellen tragen Wert/100).
                    cells.AddConditionalFormat().DataBar(XLColor.FromHtml("#A6A6A6")).Minimum(XLCFContentType.Number, 0).Maximum(XLCFContentType.Number, 1);
                    break;
                case ColumnHint.Bar:
                    cells.AddConditionalFormat().DataBar(XLColor.FromHtml("#A6A6A6")).Minimum(XLCFContentType.Number, 0).HighestValue();
                    break;
                case ColumnHint.Change:
                    cells.AddConditionalFormat().IconSet(XLIconSetStyle.ThreeArrowsGray)
                        .AddValue(XLCFIconSetOperator.EqualOrGreaterThan, "0", XLCFContentType.Percent)
                        .AddValue(XLCFIconSetOperator.EqualOrGreaterThan, "0", XLCFContentType.Number)
                        .AddValue(XLCFIconSetOperator.GreaterThan, "0", XLCFContentType.Number);
                    break;
            }
        }
    }

    private static void PrintSetup(IXLWorksheet sheet)
    {
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 1);
        sheet.PageSetup.FitToPages(1, 0);
        var footer = sheet.PageSetup.Footer.Right;
        footer.AddText("Seite ");
        footer.AddText(XLHFPredefinedText.PageNumber);
        footer.AddText(" von ");
        footer.AddText(XLHFPredefinedText.NumberOfPages);
    }

    private static void WriteFacts(IXLWorksheet sheet, int firstColumn, IReadOnlyList<KeyValuePair<string, string>> facts)
    {
        for (var row = 0; row < facts.Count; row++)
        {
            var label = sheet.Cell(row + 1, firstColumn);
            label.Value = facts[row].Key;
            label.Style.Font.Bold = true;
            sheet.Cell(row + 1, firstColumn + 1).Value = facts[row].Value;
        }

        if (facts.Count > 0)
        {
            sheet.Column(firstColumn).Width = Math.Clamp(facts.Max(f => f.Key.Length) * 1.1 + 2, 8, 30);
            sheet.Column(firstColumn + 1).Width = Math.Clamp(facts.Max(f => f.Value.Length) * 1.1 + 2, 8, 60);
        }
    }

    /// <summary>Strichpunkt für Excel in Österreich und Deutschland; Komma nur für die Portal-CSV mit englischen Spalten.</summary>
    public static byte[] ToCsv(ExportTable table, char separator = ';')
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(separator, table.Columns.Select(c => Quote(c.Name, separator)))).Append("\r\n");
        foreach (var row in table.Rows)
        {
            builder.Append(string.Join(separator, table.Columns.Select((c, i) => Quote(CsvText(c.Kind, row[i]), separator)))).Append("\r\n");
        }

        return [.. Utf8WithBom.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    private static void Fill(IXLCell cell, ColumnKind kind, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case ExportCell typed:
                Fill(cell, typed.Kind, typed.Value);
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
            case decimal share when kind == ColumnKind.Percent:
                cell.Value = share / 100m;
                cell.Style.NumberFormat.Format = "0.0%";
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
        ExportCell typed => CsvText(typed.Kind, typed.Value),
        DateOnly date => date.ToString("dd.MM.yyyy", German),
        TimeOnly time => time.ToString("HH:mm", German),
        bool yes => yes ? "Ja" : "Nein",
        decimal share when kind == ColumnKind.Percent => share.ToString("0.0", German) + " %",
        decimal amount => amount.ToString(kind == ColumnKind.Money ? "0.00" : "0.####", German),
        int number => number.ToString(German),
        long number => number.ToString(German),
        double number => number.ToString("0.####", German),
        _ => value.ToString() ?? "",
    };

    private static string Quote(string text, char separator) =>
        text.AsSpan().IndexOfAny("\"\r\n") >= 0 || text.Contains(separator, StringComparison.Ordinal) ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;

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
