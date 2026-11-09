using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Ablaufliste als PDF (PDF-Ausgaben F, ADR 0091): die Chargen, die in den nächsten <see cref="Data.Days"/> Tagen ablaufen, zuerst ablaufend zuerst, mit Material, Charge, Lagerort, Ablaufdatum, Frist
/// und Menge. Schon Abgelaufenes steht nicht darauf; das ist die Aufgabe „Abgelaufen“ der Lagerseite.
/// </summary>
internal static class ExpiringStockDocument
{
    public const int DefaultDays = 30;

    public const int MaxDays = 365;

    public sealed record Data(string LocationName, DateOnly Today, int Days, IReadOnlyList<StockBatchDto> Batches);

    public static string FileName(Data data) => DocumentFiles.PdfName("Ablaufliste", data.LocationName, data.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>Die Chargen von heute bis heute plus <paramref name="days"/> Tage; die Reihenfolge der Eingabe bleibt.</summary>
    public static IReadOnlyList<StockBatchDto> Within(StockBatchesDto batches, int days) =>
        [.. batches.Batches.Where(b => b.ExpiresOn >= batches.Today && b.ExpiresOn <= batches.Today.AddDays(days))];

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var pdf = new DocumentBuilder(DocumentFormat.A4Portrait, header);
        pdf.Title(data.Days == 1 ? "Ablaufliste: nächster Tag" : $"Ablaufliste: nächste {data.Days} Tage");
        pdf.Paragraph($"{data.LocationName} · Stand {DocumentBuilder.Date(data.Today)}", muted: true);
        if (data.Batches.Count == 0)
        {
            pdf.Paragraph("In diesem Zeitraum läuft keine Charge ab.");
            return pdf.Build();
        }

        pdf.Table(
            [new("Material", 3.5), new("Charge", 1.6), new("Lagerort", 2), new("Läuft ab", 1.6), new("Frist", 1.6), new("Menge", 1.5, DocumentAlign.Right)],
            data.Batches.Select(b => DocumentRow.Of(
                b.ItemName, b.Batch ?? "", b.StoragePlaceName, DocumentBuilder.Date(b.ExpiresOn), Due(b.ExpiresOn.DayNumber - data.Today.DayNumber), $"{DocumentBuilder.Number(b.Quantity)} {b.Unit}")));
        return pdf.Build();
    }

    private static string Due(int daysLeft) => daysLeft switch
    {
        0 => "heute",
        1 => "noch 1 Tag",
        _ => $"noch {daysLeft} Tage",
    };
}
