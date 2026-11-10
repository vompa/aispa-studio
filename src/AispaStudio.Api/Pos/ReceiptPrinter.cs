using System.Globalization;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Beleg für Bondrucker: als Text in fester Zeichenbreite (42 Zeichen bei 80 mm mit Schrift B, 48 mit Schrift A) und als ESC/POS-Befehle
/// mit QR-Code und Schnitt. Inhalt wie in der Weboberfläche; der QR-Code trägt den signierten Belegcode (AT) unverändert.
/// Mit Rechnungsempfänger wird der Beleg zur Rechnung: Empfänger, Leistungsdatum und UID-Nummern stehen dann darauf.
/// </summary>
internal static class ReceiptPrinter
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public static string Text(ReceiptDto receipt, TimeZoneInfo zone, string currency, int width)
    {
        var text = new StringBuilder();
        void Line(string value = "") => text.Append(value.Length > width ? value[..width] : value).Append('\n');
        void Center(string value) => Line(value.Length >= width ? value : new string(' ', (width - value.Length) / 2) + value);
        void Columns(string left, string right)
        {
            var room = width - right.Length - 1;
            Line(left.Length > room ? $"{left[..room]} {right}" : left.PadRight(width - right.Length) + right);
        }

        string Money(decimal amount) => amount.ToString("#,##0.00", German);

        foreach (var header in receipt.ReceiptHeader.Split('\n', StringSplitOptions.TrimEntries))
        {
            Center(header);
        }

        // UID und Firmenbuch des Standorts, sofern der Belegkopf sie nicht schon nennt.
        foreach (var company in CompanyLines(receipt.Company).Where(c => !receipt.ReceiptHeader.Contains(c, StringComparison.OrdinalIgnoreCase)))
        {
            Center(company);
        }

        var issued = TimeZoneInfo.ConvertTime(receipt.IssuedAt, zone);
        Line();
        if (receipt.Recipient is { } recipient)
        {
            Line("Rechnung an:");
            Line(recipient.Name);
            foreach (var part in recipient.Address.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                Line(part);
            }

            if (recipient.VatId is { } vatId)
            {
                Line($"UID {vatId}");
            }

            Line();
            Line($"Rechnung Nr. {receipt.RegisterCode}-{receipt.Number}");
            Line($"Rechnungs- und Leistungsdatum {issued.ToString("dd.MM.yyyy", German)}");
        }
        else
        {
            Columns($"{KindLabel(receipt.Kind)} Nr. {receipt.Number}", $"Kasse {receipt.RegisterCode}");
        }

        Line(issued.ToString("dd.MM.yyyy HH:mm:ss", German));
        if (receipt.VoidReason is { } reason)
        {
            Line($"Storno: {reason}");
        }

        Line(new string('-', width));
        foreach (var l in receipt.Lines)
        {
            // Rabatt sichtbar: Preis davor, darunter der Rabatt; die Spalte ergibt so weiter die Summe.
            Columns(l.Quantity > 1 ? $"{l.Quantity} x {l.Description}" : l.Description, Money(l.Amount + l.Discount));
            if (l.Discount != 0)
            {
                Columns($"  {l.DiscountNote ?? "Rabatt"}", Money(-l.Discount));
            }
        }

        Line(new string('-', width));
        Columns($"Summe {currency}", Money(receipt.Total));
        foreach (var t in receipt.Taxes)
        {
            Columns($"{t.Percent.ToString("0.#", German)} % USt auf {Money(t.Net)}", Money(t.Tax));
        }

        Line();
        foreach (var p in receipt.Payments)
        {
            Columns(PaymentMethods.Label(p.Method) + (p.VoucherCode is { } code ? $" {code}" : ""), Money(p.Amount));
        }

        if (receipt.Change > 0)
        {
            Columns("Rückgeld", Money(receipt.Change));
        }

        foreach (var v in receipt.IssuedVouchers)
        {
            Line();
            Center($"Gutschein {v.Code}");
            Center($"Wert {currency} {Money(v.InitialValue)}");
        }

        if (receipt.SignatureDeviceFailed)
        {
            Line();
            Center("Sicherheitseinrichtung ausgefallen");
        }

        return text.ToString();
    }

    /// <summary>ESC/POS: Initialisieren, Zeichensatz Windows-1252, Text, QR-Code (Modell 2, Fehlerkorrektur M), Vorschub und Teilschnitt.</summary>
    public static byte[] EscPos(ReceiptDto receipt, TimeZoneInfo zone, string currency, int width)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Encoding.GetEncoding(1252);
        var bytes = new List<byte> { 0x1B, 0x40, 0x1B, 0x74, 16 };
        bytes.AddRange(ansi.GetBytes(Text(receipt, zone, currency, width)));
        if (receipt.FiscalCode is { } code)
        {
            var data = Encoding.UTF8.GetBytes(code);
            var length = data.Length + 3;
            bytes.AddRange([0x1B, 0x61, 1]);
            bytes.AddRange([0x1D, 0x28, 0x6B, 4, 0, 0x31, 0x41, 0x32, 0]);
            bytes.AddRange([0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x43, 4]);
            bytes.AddRange([0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x45, 0x31]);
            bytes.AddRange([0x1D, 0x28, 0x6B, (byte)(length % 256), (byte)(length / 256), 0x31, 0x50, 0x30]);
            bytes.AddRange(data);
            bytes.AddRange([0x1D, 0x28, 0x6B, 3, 0, 0x31, 0x51, 0x30]);
            bytes.AddRange([0x1B, 0x61, 0]);
        }

        bytes.AddRange([0x1B, 0x64, 4, 0x1D, 0x56, 0x42, 0]);
        return [.. bytes];
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

    /// <summary>Bezeichnung der Belegart, wie sie auf Bon und PDF steht.</summary>
    internal static string KindLabel(ReceiptKind kind) => kind switch
    {
        ReceiptKind.Sale => "Beleg",
        ReceiptKind.Void => "Stornobeleg",
        ReceiptKind.Start => "Startbeleg",
        ReceiptKind.Monthly => "Monatsbeleg",
        ReceiptKind.Yearly => "Jahresbeleg",
        ReceiptKind.Null => "Nullbeleg",
        _ => "Schlussbeleg",
    };
}
