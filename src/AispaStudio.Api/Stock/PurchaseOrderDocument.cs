using System.Globalization;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Bestellung an den Lieferanten als PDF (PDF-Ausgaben F, ADR 0092): A4 mit Lieferant, Bestelldatum, Lieferadresse (der Standort), Positionen mit Menge, Einheit, Artikelnummer, Bezeichnung,
/// Einzelpreis und Summe, Gesamtsumme netto und dem Hinweis der Bestellung. „Erwartete Lieferung“ steht nur, wenn der Lieferant eine Lieferzeit hat: Bestelldatum plus Lieferzeit. Die Bestellung hat
/// keinen eigenen Liefertermin; wer einen verlangt, schreibt ihn in den Hinweis.
/// </summary>
internal static class PurchaseOrderDocument
{
    public sealed record Data(PurchaseOrderDto Order, Location Location, StockSupplier? Supplier);

    public static string FileName(PurchaseOrderDto order) => DocumentFiles.PdfName("Bestellung", order.Number.ToString(CultureInfo.InvariantCulture), order.Supplier);

    /// <summary>Vorbelegung der Mail: Adresse des Lieferanten (leer, wenn keine eingetragen ist), Betreff „Bestellung … vom …“, kurzer Text mit Gruß.</summary>
    public static DocumentMailDraft Draft(Data data, DocumentHeader header)
    {
        var order = data.Order;
        return new DocumentMailDraft(
            string.IsNullOrWhiteSpace(data.Supplier?.Email) ? null : data.Supplier.Email.Trim(),
            $"Bestellung {order.Number} vom {DocumentBuilder.Date(order.CreatedAt, header.Zone)}",
            $"Guten Tag,\n\nim Anhang finden Sie unsere Bestellung {order.Number} als PDF. Bitte bestätigen Sie den Erhalt und nennen Sie uns den Liefertermin.{DocumentMails.Closing(header.SpaName)}");
    }

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var order = data.Order;
        var currency = data.Location.Currency;
        var pdf = new DocumentBuilder(PaperFormat.A4Portrait, header);
        pdf.Title($"Bestellung {order.Number}");
        pdf.KeyValues(Facts(data, header));
        pdf.Space();
        pdf.Table(
            [new("Menge", 1, DocumentAlign.Right), new("Einheit", 1.3), new("Artikelnr.", 1.4), new("Bezeichnung", 4), new("Einzelpreis", 1.5, DocumentAlign.Right), new("Summe", 1.5, DocumentAlign.Right)],
            order.Lines.Select(l => DocumentRow.Of(
                DocumentBuilder.Number(l.Quantity), l.Unit, l.SupplierArticleNumber ?? "", Description(l), DocumentBuilder.Amount(l.UnitCost, currency), DocumentBuilder.Amount(l.Quantity * l.UnitCost, currency))),
            DocumentRow.Of("", "", "", "Gesamtsumme netto", "", DocumentBuilder.Amount(order.Total, currency)));
        if (order.Note is { } note)
        {
            pdf.Heading("Hinweis");
            pdf.Paragraph(note);
        }

        return pdf.Build();
    }

    private static string Description(PurchaseOrderLineDto line) => line.PackageText is { } package ? $"{line.ItemName}\n{package}" : line.ItemName;

    private static IEnumerable<(string, string)> Facts(Data data, DocumentHeader header)
    {
        var order = data.Order;
        yield return ("Lieferant", order.Supplier);
        if (!string.IsNullOrWhiteSpace(data.Supplier?.ContactPerson))
        {
            yield return ("Ansprechperson", data.Supplier.ContactPerson.Trim());
        }

        yield return ("Bestelldatum", DocumentBuilder.Date(order.CreatedAt, header.Zone));
        yield return ("Lieferung an", string.Join(", ", header.LocationLines.Take(3)));
        if (data.Supplier?.LeadTimeDays is { } days)
        {
            yield return ("Erwartete Lieferung", $"{DocumentBuilder.Date(order.CreatedAt.AddDays(days), header.Zone)} (Lieferzeit {days} {(days == 1 ? "Tag" : "Tage")})");
        }
    }
}
