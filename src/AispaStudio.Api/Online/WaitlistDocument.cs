using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Online;

/// <summary>
/// Warteliste eines Tages als PDF (PDF-Ausgaben E, ADR 0091): die offenen Wünsche, die an dem Tag gelten, mit Gast, Telefon, Wunsch (Behandlung), Zeitraum und Stand
/// („Wartet“ oder „2× freien Termin gemeldet“), zum Nachtelefonieren am Empfang.
/// </summary>
internal static class WaitlistDocument
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public static string FileName(string locationName, DateOnly date) =>
        DocumentFiles.PdfName("Warteliste", locationName, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>Die offenen Wünsche, in deren Zeitraum der Tag liegt, in der Reihenfolge der Liste (gewünschter Beginn, dann Eintragung).</summary>
    public static List<WaitlistStaffEntry> ValidOn(IEnumerable<WaitlistStaffEntry> entries, DateOnly date) =>
        [.. entries.Where(e => e.Status == WaitlistStatus.Waiting && e.From <= date && date <= e.Until)];

    public static byte[] Build(IReadOnlyList<WaitlistStaffEntry> entries, DateOnly date, DocumentHeader header)
    {
        var pdf = new DocumentBuilder(DocumentFormat.A4Portrait, header);
        pdf.Title($"Warteliste {date.ToString("dddd, dd.MM.yyyy", German)}");
        pdf.Paragraph($"Offene Wünsche, die am {DocumentBuilder.Date(date)} gelten · {Count(entries.Count)}", muted: true);
        if (entries.Count == 0)
        {
            pdf.Paragraph("Keine offenen Wünsche an diesem Tag.");
            return pdf.Build();
        }

        pdf.Table(
            [new("Gast", 3), new("Telefon", 2.5), new("Wunsch", 2.5), new("Zeitraum", 4), new("Stand", 2.5)],
            entries.Select(e => DocumentRow.Of(e.CustomerName, string.IsNullOrWhiteSpace(e.Phone) ? "–" : e.Phone.Trim(), e.TreatmentName, Period(e), State(e))));
        return pdf.Build();
    }

    /// <summary>„1 offener Wunsch“, „3 offene Wünsche“.</summary>
    public static string Count(int count) => $"{count} {(count == 1 ? "offener Wunsch" : "offene Wünsche")}";

    public static string State(WaitlistStaffEntry entry) => entry.OfferCount == 0 ? "Wartet" : $"{entry.OfferCount}× freien Termin gemeldet";

    /// <summary>„02.03.2027 bis 06.03.2027“ und darunter „nachmittags“; bei einem einzigen Tag ohne „bis“.</summary>
    public static string Period(WaitlistStaffEntry entry) =>
        $"{DocumentBuilder.Date(entry.From)}{(entry.Until == entry.From ? "" : $" bis {DocumentBuilder.Date(entry.Until)}")}\n{DayPartName(entry.DayPart)}";

    private static string DayPartName(DayPart part) => part switch
    {
        DayPart.Morning => "vormittags",
        DayPart.Afternoon => "nachmittags",
        DayPart.Evening => "abends",
        _ => "ganztags",
    };
}
