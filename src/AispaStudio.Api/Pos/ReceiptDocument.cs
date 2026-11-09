using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Beleg oder Rechnung als PDF (PDF-Ausgaben B, ADR 0091): A4 für Ablage und Versand, Bon-Format als Abbild des Bondrucks. Inhalt wie <see cref="ReceiptPrinter"/>
/// (Positionen mit Rabatt, Summe, Steuern, Zahlungen, Gutscheine, RKSV-Code als QR), mit Rechnungsempfänger wird der Beleg zur Rechnung; der Nachdruck trägt „Kopie“.
/// </summary>
internal static class ReceiptDocument
{
    public static bool IsInvoice(ReceiptDto receipt) => receipt.Recipient is not null;

    public static string Label(ReceiptDto receipt) => IsInvoice(receipt) ? "Rechnung" : ReceiptPrinter.KindLabel(receipt.Kind);

    public static string Title(ReceiptDto receipt) =>
        IsInvoice(receipt) ? $"Rechnung Nr. {receipt.RegisterCode}-{receipt.Number}" : $"{Label(receipt)} Nr. {receipt.Number}";

    public static string FileName(ReceiptDto receipt) => DocumentFiles.PdfName(Label(receipt), $"{receipt.RegisterCode}-{receipt.Number}");

    /// <summary>Kopf: der Belegkopf der Kasse (Firmenname, Anschrift, UID) ersetzt die Firmendaten, dazu UID und Firmenbuch des Standorts, wenn sie dort fehlen; der Standort steht nicht noch einmal.</summary>
    public static DocumentHeader Header(DocumentHeader standard, ReceiptDto receipt)
    {
        var lines = receipt.ReceiptHeader.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var company in CompanyLines(receipt.Company))
        {
            if (!lines.Any(l => l.Contains(company, StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(company);
            }
        }

        return standard with { CompanyLines = lines, LocationLines = [] };
    }

    /// <summary>Vorbelegung der Mail: Adresse des Gastes aus der Kundenkarte, Betreff „Ihre Rechnung Nr. … von …“, kurzer Text mit Gruß.</summary>
    public static DocumentMailDraft Draft(ReceiptDto receipt, string spaName, string? customerEmail, TimeZoneInfo zone)
    {
        var (nominative, accusative) = IsInvoice(receipt)
            ? ($"Ihre Rechnung Nr. {receipt.RegisterCode}-{receipt.Number}", $"Ihre Rechnung Nr. {receipt.RegisterCode}-{receipt.Number}")
            : ($"Ihr {Label(receipt)} Nr. {receipt.Number}", $"Ihren {Label(receipt)} Nr. {receipt.Number}");
        return new DocumentMailDraft(
            customerEmail,
            $"{nominative} von {spaName}",
            $"Guten Tag,\n\nanbei erhalten Sie {accusative} vom {DocumentBuilder.Date(receipt.IssuedAt, zone)} als PDF.{DocumentMails.Closing(spaName)}");
    }

    public static byte[] Build(ReceiptDto receipt, DocumentHeader header, TimeZoneInfo zone, string currency, DocumentPageFormat format, bool copy)
    {
        var narrow = format == DocumentPageFormat.Receipt;
        var pdf = new DocumentBuilder(narrow ? DocumentFormat.Receipt80 : DocumentFormat.A4Portrait, header);
        pdf.Title(Title(receipt));
        if (copy)
        {
            pdf.Paragraph("Kopie", bold: true);
        }

        pdf.KeyValues(Facts(receipt, zone));
        pdf.Heading("Positionen");
        pdf.Table([new("Position", 4), new($"Betrag {currency}", 1, DocumentAlign.Right)], Lines(receipt), DocumentRow.Of("Summe", DocumentBuilder.Money(receipt.Total)));
        if (receipt.Taxes.Count > 0)
        {
            pdf.Table(
                [new("Steuersatz", 2), new("Netto", 1, DocumentAlign.Right), new("USt", 1, DocumentAlign.Right), new("Brutto", 1, DocumentAlign.Right)],
                receipt.Taxes.Select(t => DocumentRow.Of($"{DocumentBuilder.Number(t.Percent)} % USt", DocumentBuilder.Money(t.Net), DocumentBuilder.Money(t.Tax), DocumentBuilder.Money(t.Gross))));
        }

        pdf.Heading("Zahlung");
        var payments = receipt.Payments
            .Select(p => (PaymentMethods.Label(p.Method) + (p.VoucherCode is { } code ? $" {code}" : ""), DocumentBuilder.Money(p.Amount)))
            .ToList();
        if (receipt.Change > 0)
        {
            payments.Add(("Rückgeld", DocumentBuilder.Money(receipt.Change)));
        }

        pdf.KeyValues(payments);
        if (receipt.IssuedVouchers.Count > 0)
        {
            pdf.Heading("Gutscheine");
            pdf.KeyValues(receipt.IssuedVouchers.Select(v => ($"Gutschein {v.Code}", $"{currency} {DocumentBuilder.Money(v.InitialValue)}")));
        }

        if (receipt.SignatureDeviceFailed)
        {
            pdf.Paragraph("Sicherheitseinrichtung ausgefallen", bold: true, align: DocumentAlign.Center);
        }

        if (receipt.FiscalCode is { } fiscalCode)
        {
            pdf.Space();
            pdf.QrCode(fiscalCode, narrow ? 25 : 30);
            pdf.Small("Signierter Belegcode (RKSV)", DocumentAlign.Center);
        }

        return pdf.Build();
    }

    private static List<(string Key, string Value)> Facts(ReceiptDto receipt, TimeZoneInfo zone)
    {
        var facts = new List<(string, string)>();
        if (receipt.Recipient is { } recipient)
        {
            facts.Add(("Rechnung an", string.Join('\n', RecipientLines(recipient))));
            facts.Add(("Rechnungs- und Leistungsdatum", DocumentBuilder.Date(receipt.IssuedAt, zone)));
            facts.Add(("Beleg", $"{ReceiptPrinter.KindLabel(receipt.Kind)} Nr. {receipt.Number}, Kasse {receipt.RegisterCode}"));
        }
        else
        {
            facts.Add(("Kasse", receipt.RegisterCode));
        }

        facts.Add(("Ausgestellt", DocumentBuilder.DateTime(receipt.IssuedAt, zone)));
        if (receipt.VoidReason is { } reason)
        {
            facts.Add(("Storno", reason));
        }

        return facts;
    }

    /// <summary>Positionen wie auf dem Bon: Preis vor dem Rabatt, darunter der Rabatt grau, damit die Spalte die Summe ergibt.</summary>
    private static IEnumerable<DocumentRow> Lines(ReceiptDto receipt)
    {
        foreach (var line in receipt.Lines)
        {
            yield return DocumentRow.Of(line.Quantity > 1 ? $"{line.Quantity} × {line.Description}" : line.Description, DocumentBuilder.Money(line.Amount + line.Discount));
            if (line.Discount != 0)
            {
                yield return new DocumentRow([$"   {line.DiscountNote ?? "Rabatt"}", DocumentBuilder.Money(-line.Discount)], Muted: true);
            }
        }
    }

    private static IEnumerable<string> RecipientLines(InvoiceRecipient recipient)
    {
        yield return recipient.Name;
        foreach (var part in recipient.Address.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return part;
        }

        if (recipient.VatId is { } vatId)
        {
            yield return $"UID {vatId}";
        }
    }

    private static IEnumerable<string> CompanyLines(LocationCompany? company)
    {
        if (company?.LegalName is { } legalName)
        {
            yield return legalName;
        }

        if (company?.VatId is { } vatId)
        {
            yield return $"UID {vatId}";
        }

        if (company?.CompanyRegisterNumber is { } register)
        {
            yield return company.CompanyRegisterCourt is { } court ? $"{register}, {court}" : register;
        }
    }
}
