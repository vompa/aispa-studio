using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Tagesabschluss als PDF (PDF-Ausgaben D, ADR 0092): A4 zum Abheften. Kasse, Zeitraum, Belege von–bis, Summen je Zahlart, Steuern, Kassenlade mit Soll, gezähltem Bestand und
/// Differenz (mit Notiz), Einlagen und Entnahmen des Zeitraums, Unterschriftenzeilen „Gezählt von“ und „Geprüft von“ (dokumente.md). Der Abschluss ist unveränderlich gespeichert; das PDF zeigt genau diese Zahlen.
/// </summary>
internal static class DayClosingDocument
{
    /// <summary>Belegnummern des Abschlusses: erste und letzte Nummer der Verkäufe und Stornos im Zeitraum; null, wenn es keine gab.</summary>
    public sealed record ReceiptRange(long First, long Last);

    public static string FileName(string registerCode, DayClosingDto closing, TimeZoneInfo zone) =>
        DocumentFiles.PdfName("Tagesabschluss", registerCode, TimeZoneInfo.ConvertTime(closing.Until, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public static byte[] Build(DayClosingDto closing, string registerCode, ReceiptRange? range, IReadOnlyList<CashMovement> movements, DocumentHeader header, TimeZoneInfo zone, string currency)
    {
        var pdf = new DocumentBuilder(PaperFormat.A4Portrait, header);
        pdf.Title($"Tagesabschluss {registerCode}");
        pdf.KeyValues(
        [
            ("Kasse", registerCode),
            ("Zeitraum", $"{DocumentBuilder.DateTime(closing.From, zone)} bis {DocumentBuilder.DateTime(closing.Until, zone)}"),
            ("Belege", Receipts(closing, range)),
        ]);

        pdf.Heading("Umsatz");
        pdf.Table(
            [new("Zahlart", 3), new($"Betrag {currency}", 1, DocumentAlign.Right)],
            closing.Payments.Select(p => DocumentRow.Of(PaymentMethods.Label(p.Method), DocumentBuilder.Money(p.Amount))),
            DocumentRow.Of("Umsatz gesamt", DocumentBuilder.Money(closing.Revenue)));
        if (closing.Taxes.Count > 0)
        {
            pdf.Space();
            pdf.Table(
                [new("Steuersatz", 2), new("Netto", 1, DocumentAlign.Right), new("USt", 1, DocumentAlign.Right), new("Brutto", 1, DocumentAlign.Right)],
                closing.Taxes.Select(t => DocumentRow.Of($"{DocumentBuilder.Number(t.Percent)} % USt", DocumentBuilder.Money(t.Net), DocumentBuilder.Money(t.Tax), DocumentBuilder.Money(t.Gross))));
        }

        pdf.Heading($"Kassenlade ({currency})");
        pdf.KeyValues(
        [
            ("Anfangsbestand", DocumentBuilder.Money(closing.OpeningCash)),
            ("Bareinnahmen", DocumentBuilder.Money(closing.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount))),
            ("Einlagen", DocumentBuilder.Money(closing.CashDeposits)),
            ("Entnahmen", DocumentBuilder.Money(-closing.CashWithdrawals)),
            ("Soll-Bestand", DocumentBuilder.Money(closing.ExpectedCash)),
            ("Gezählter Bestand", DocumentBuilder.Money(closing.CountedCash)),
            ("Differenz", DocumentBuilder.Money(closing.Difference)),
        ]);
        if (closing.Note is { } note)
        {
            pdf.Paragraph($"Notiz: {note}");
        }

        if (movements.Count > 0)
        {
            pdf.Heading("Einlagen und Entnahmen");
            pdf.Table(
                [new("Zeit", 2), new("Art", 1.2), new("Grund", 4), new($"Betrag {currency}", 1.5, DocumentAlign.Right)],
                movements.OrderBy(m => m.At).Select(m => DocumentRow.Of(
                    DocumentBuilder.DateTime(m.At, zone),
                    m.Kind == CashMovementKind.Deposit ? "Einlage" : "Entnahme",
                    m.Reason,
                    DocumentBuilder.Money(m.Kind == CashMovementKind.Deposit ? m.Amount : -m.Amount))));
        }

        pdf.SignatureLines("Gezählt von", "Geprüft von");
        return pdf.Build();
    }

    private static string Receipts(DayClosingDto closing, ReceiptRange? range)
    {
        var counts = $"{closing.ReceiptCount} {(closing.ReceiptCount == 1 ? "Verkauf" : "Verkäufe")}, {closing.VoidCount} {(closing.VoidCount == 1 ? "Storno" : "Stornos")}";
        return range is null ? $"keine ({counts})" : $"Nr. {range.First} bis Nr. {range.Last} ({counts})";
    }
}
