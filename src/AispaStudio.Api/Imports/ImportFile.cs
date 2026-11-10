using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace AispaStudio.Api.Importing;

/// <summary>Eine gelesene Importdatei: Spaltenköpfe aus der ersten Zeile, danach je Zeile die Zellen als Text. <see cref="ImportLine.Number"/> zählt wie in Excel (Kopfzeile ist 1).</summary>
public sealed record ImportSheet(IReadOnlyList<string> Headers, IReadOnlyList<ImportLine> Lines);

public sealed record ImportLine(int Number, IReadOnlyList<string> Cells)
{
    public string Cell(int column) => column >= 0 && column < Cells.Count ? Cells[column] : "";
}

/// <summary>
/// Liest Excel (erstes Blatt, ClosedXML) und CSV (Strichpunkt, Komma oder Tabulator, nach der Kopfzeile erkannt; Anführungszeichen nach RFC 4180; UTF-8 mit oder ohne BOM, sonst Latin-1).
/// Datumszellen in Excel werden zu „JJJJ-MM-TT“, damit das Datum unabhängig vom Format der Zelle erkannt wird. Leere Zeilen fallen weg.
/// </summary>
public static class ImportFile
{
    private static readonly char[] Separators = [';', ',', '\t'];

    public static bool IsExcel(string fileName) => fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

    public static bool IsCsv(string fileName) =>
        fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    /// <summary>Liest die Datei; <c>null</c>, wenn sie sich nicht als Tabelle lesen lässt.</summary>
    public static ImportSheet? Read(byte[] data, string fileName)
    {
        try
        {
            return IsExcel(fileName) ? ReadExcel(data) : ReadCsv(Decode(data));
        }
        catch (Exception e) when (e is InvalidDataException or FormatException or ArgumentException or InvalidOperationException
            or global::System.IO.FileFormatException or NotSupportedException)
        {
            return null;
        }
    }

    private static ImportSheet ReadExcel(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        if (sheet.RangeUsed() is not { } used)
        {
            return new ImportSheet([], []);
        }

        var width = used.LastColumn().ColumnNumber();
        var headers = Enumerable.Range(1, width).Select(c => CellText(sheet.Cell(1, c))).ToList();
        var lines = new List<ImportLine>();
        for (var row = 2; row <= used.LastRow().RowNumber(); row++)
        {
            var cells = Enumerable.Range(1, width).Select(c => CellText(sheet.Cell(row, c))).ToList();
            if (cells.Any(c => c.Length > 0))
            {
                lines.Add(new ImportLine(row, cells));
            }
        }

        return new ImportSheet(headers, lines);
    }

    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Number => cell.GetDouble().ToString("0.##########", CultureInfo.InvariantCulture),
        XLDataType.Blank => "",
        _ => cell.GetFormattedString().Trim(),
    };

    private static string Decode(byte[] data)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(data).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            // Excel speichert „CSV (Trennzeichen-getrennt)“ in Österreich und Deutschland in Windows-1252; Latin-1 trifft alle Umlaute.
            return Encoding.Latin1.GetString(data);
        }
    }

    private static ImportSheet ReadCsv(string text)
    {
        var records = Records(text, Separator(text)).ToList();
        if (records.Count == 0)
        {
            return new ImportSheet([], []);
        }

        var headers = records[0].Cells.Select(c => c.Trim()).ToList();
        var lines = records.Skip(1)
            .Where(r => r.Cells.Any(c => c.Trim().Length > 0))
            .Select(r => new ImportLine(r.Number, r.Cells.Select(c => c.Trim()).ToList()))
            .ToList();
        return new ImportSheet(headers, lines);
    }

    /// <summary>Das Trennzeichen, das in der Kopfzeile (außerhalb von Anführungszeichen) am häufigsten vorkommt.</summary>
    private static char Separator(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        var header = end < 0 ? text : text[..end];
        return Separators.OrderByDescending(s => header.Count(c => c == s)).First();
    }

    private sealed record CsvRecord(int Number, List<string> Cells);

    private static IEnumerable<CsvRecord> Records(string text, char separator)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var line = 1;
        var start = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    line += c == '\n' ? 1 : 0;
                    cell.Append(c);
                }

                continue;
            }

            if (c == '"' && cell.Length == 0)
            {
                quoted = true;
            }
            else if (c == separator)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                cells.Add(cell.ToString());
                cell.Clear();
                yield return new CsvRecord(start, cells);
                cells = [];
                line++;
                start = line;
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            yield return new CsvRecord(start, cells);
        }
    }
}
