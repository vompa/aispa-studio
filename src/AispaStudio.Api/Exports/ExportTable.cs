using System.Globalization;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Exports;

/// <summary>Art einer Spalte; bestimmt Zellentyp und Zahlenformat in Excel und die Schreibweise in CSV.</summary>
internal enum ColumnKind
{
    Text,
    Number,
    Money,
    Date,
    Time,
    YesNo,
}

/// <summary>Spalte einer Exporttabelle mit dem Spaltenkopf, wie er in der Datei steht.</summary>
internal sealed record ExportColumn(string Name, ColumnKind Kind);

/// <summary>
/// Eine Liste der App als Tabelle für Excel oder CSV: <paramref name="Name"/> ist Blattname und Listenname, jede Zeile hat je Spalte einen Wert
/// (<c>string</c>, <c>decimal</c>/<c>int</c>, <c>DateOnly</c>, <c>TimeOnly</c>, <c>bool</c> oder <c>null</c>).
/// </summary>
internal sealed record ExportTable(string Name, IReadOnlyList<ExportColumn> Columns, IReadOnlyList<object?[]> Rows);

/// <summary>Dateinamen, Inhaltstypen und Bezeichnungen der Formate, gemeinsam für alle Exporte.</summary>
internal static class ExportFiles
{
    public static string Label(ExportFormat format) => format switch
    {
        ExportFormat.Xlsx => "Excel",
        ExportFormat.Csv => "CSV",
        ExportFormat.Portal => "Portal-CSV",
        ExportFormat.Vcf => "vCard",
        ExportFormat.Ics => "iCal",
        _ => format.ToString(),
    };

    public static string ContentType(ExportFormat format) => format switch
    {
        ExportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ExportFormat.Vcf => "text/vcard; charset=utf-8",
        ExportFormat.Ics => "text/calendar; charset=utf-8",
        _ => "text/csv; charset=utf-8",
    };

    public static string Extension(ExportFormat format) => format switch
    {
        ExportFormat.Xlsx => "xlsx",
        ExportFormat.Vcf => "vcf",
        ExportFormat.Ics => "ics",
        _ => "csv",
    };

    /// <summary>„Kunden_2026-11-09.xlsx“, „Termin_2026-11-12_1400.ics“: sprechend, ohne Umlaute und Sonderzeichen im Dateisystem-Teil.</summary>
    public static string Name(string title, ExportFormat format, DateOnly? date = null, TimeOnly? time = null)
    {
        var parts = new List<string> { Ascii(title) };
        if (date is { } d)
        {
            parts.Add(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (time is { } t)
        {
            parts.Add(t.ToString("HHmm", CultureInfo.InvariantCulture));
        }

        return $"{string.Join('_', parts)}.{Extension(format)}";
    }

    /// <summary>Umlaute und ß umschreiben, Leerzeichen zu Unterstrichen, alles andere außer Buchstaben, Ziffern, Bindestrich und Unterstrich weglassen.</summary>
    private static string Ascii(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim())
        {
            switch (c)
            {
                case 'ä': builder.Append("ae"); break;
                case 'ö': builder.Append("oe"); break;
                case 'ü': builder.Append("ue"); break;
                case 'Ä': builder.Append("Ae"); break;
                case 'Ö': builder.Append("Oe"); break;
                case 'Ü': builder.Append("Ue"); break;
                case 'ß': builder.Append("ss"); break;
                case ' ': builder.Append('_'); break;
                default:
                    if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Length == 0 ? "Export" : builder.ToString();
    }
}
