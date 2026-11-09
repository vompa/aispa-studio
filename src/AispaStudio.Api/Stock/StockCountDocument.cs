using System.Globalization;
using AispaStudio.Api.Documents;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Inventurliste und Zählliste als PDF (PDF-Ausgaben F, ADR 0091). Die Inventurliste zeigt je gezähltem Material Einheit, Soll, Gezählt, Differenz und deren Wert (Differenz mal Einstandspreis) und die
/// Summe der Differenzen, wie die Inventur der App. Die Zählliste (<see cref="Data.Blank"/>) ist für vor dem Zählen: alle aktiven Materialien mit dem Soll und einer leeren Spalte „Gezählt“, dazu Zeilen für
/// die Unterschriften.
/// </summary>
internal static class StockCountDocument
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Eine leere Zelle, zwei Zeilen hoch: Platz zum Hinschreiben der gezählten Menge.</summary>
    private const string WritingSpace = "\n";

    /// <summary><paramref name="Counted"/> ist bei der Zählliste leer.</summary>
    public sealed record Line(string Name, string Unit, decimal Target, decimal? Counted, decimal UnitCost);

    public sealed record Data(string PlaceName, DateTimeOffset At, string? CountedBy, bool Blank, IReadOnlyList<Line> Lines, string Currency);

    public static string FileName(Data data, TimeZoneInfo zone) =>
        DocumentFiles.PdfName(Name(data), data.PlaceName, TimeZoneInfo.ConvertTime(data.At, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var pdf = new DocumentBuilder(DocumentFormat.A4Portrait, header);
        pdf.Title($"{Name(data)} {data.PlaceName}");
        pdf.Paragraph(data.CountedBy is { } by
            ? $"Lagerort {data.PlaceName} · gezählt von {by} · Stand {DocumentBuilder.DateTime(data.At, header.Zone)}"
            : $"Lagerort {data.PlaceName} · Stand {DocumentBuilder.DateTime(data.At, header.Zone)}", muted: true);
        if (data.Lines.Count == 0)
        {
            pdf.Paragraph("Kein Material angelegt.");
        }
        else if (data.Blank)
        {
            pdf.Table(
                [new("Material", 4), new("Einheit", 1.2), new("Soll", 1.2, DocumentAlign.Right), new("Gezählt", 2, DocumentAlign.Right)],
                data.Lines.Select(l => DocumentRow.Of(l.Name, l.Unit, Quantity(l.Target), WritingSpace)));
        }
        else
        {
            var changes = data.Lines.Where(l => Difference(l) != 0).ToList();
            pdf.Table(
                [new("Material", 4), new("Einheit", 1.2), new("Soll", 1.2, DocumentAlign.Right), new("Gezählt", 1.4, DocumentAlign.Right), new("Differenz", 1.4, DocumentAlign.Right), new("Wert", 1.8, DocumentAlign.Right)],
                data.Lines.Select(l => DocumentRow.Of(l.Name, l.Unit, Quantity(l.Target), Quantity(l.Counted ?? 0), Signed(Difference(l)), Worth(l, data.Currency))),
                DocumentRow.Of($"Summe der Differenzen ({(changes.Count == 1 ? "1 Position" : $"{changes.Count} Positionen")})", "", "", "", "", DocumentBuilder.Amount(changes.Sum(Value), data.Currency)));
        }

        if (data.Blank)
        {
            pdf.SignatureLines("Gezählt von", "Geprüft von");
        }

        return pdf.Build();
    }

    private static string Name(Data data) => data.Blank ? "Zählliste" : "Inventurliste";

    private static decimal Difference(Line line) => (line.Counted ?? 0) - line.Target;

    private static decimal Value(Line line) => Difference(line) * line.UnitCost;

    private static string Worth(Line line, string currency) => Difference(line) == 0 ? "" : DocumentBuilder.Amount(Value(line), currency);

    private static string Quantity(decimal value) => value.ToString("#,##0.###", German);

    private static string Signed(decimal value) => value.ToString("+#,##0.###;-#,##0.###;0", German);
}
