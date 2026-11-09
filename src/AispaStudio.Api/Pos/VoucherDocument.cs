using AispaStudio.Api.Documents;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

/// <summary>
/// Gutschein als PDF zum Verschenken (PDF-Ausgaben E, ADR 0091): A4 mit dem Spa-Namen groß, Wert oder Behandlung, Code und QR-Code mit dem Code, „Gültig bis“ und „Einlösbar in …“.
/// Ein stornierter oder schon eingelöster Gutschein trägt einen Hinweis, damit ein Nachdruck niemanden täuscht. Ein Gutschein gilt am Standort, an dem er verkauft wurde, und an
/// allen anderen Standorten des Spas; „Einlösbar in“ nennt deshalb alle Standorte.
/// </summary>
internal static class VoucherDocument
{
    public sealed record Data(Voucher Voucher, string? TreatmentName, IReadOnlyList<string> LocationNames, string Currency);

    public static string FileName(Voucher voucher) => DocumentFiles.PdfName("Gutschein", voucher.Code);

    /// <summary>Was der Gutschein wert ist: der Betrag, die Behandlung oder die Zahl der Behandlungen eines Pakets.</summary>
    public static string Worth(Data data)
    {
        var voucher = data.Voucher;
        if (voucher.Units is { } units)
        {
            return $"{units} {(units == 1 ? "Behandlung" : "Behandlungen")}{(data.TreatmentName is null ? "" : $" {data.TreatmentName}")}";
        }

        return voucher.Kind == VoucherKind.SinglePurpose && data.TreatmentName is { } name ? name : DocumentBuilder.Amount(voucher.InitialValue, data.Currency);
    }

    private static string Kind(Data data) => data.Voucher switch
    {
        { Units: not null } => "Behandlungspaket",
        { Kind: VoucherKind.SinglePurpose } => "Gutschein für eine Behandlung",
        _ => "Wertgutschein, einlösbar für alle Leistungen",
    };

    /// <summary>Vorbelegung der Mail: Adresse des Käufers aus der Kundenkarte, Betreff „Ihr Gutschein von …“, kurzer Text mit Gruß.</summary>
    public static DocumentMailDraft Draft(Data data, string spaName, string? buyerEmail)
    {
        var about = data.Voucher.Units is null && data.Voucher.Kind == VoucherKind.SinglePurpose && data.TreatmentName is { } name ? $"für {name}" : $"über {Worth(data)}";
        return new DocumentMailDraft(
            buyerEmail,
            $"Ihr Gutschein von {spaName}",
            $"Guten Tag,\n\nanbei erhalten Sie Ihren Gutschein {about} als PDF.{DocumentMails.Closing(spaName)}");
    }

    public static byte[] Build(Data data, DocumentHeader header)
    {
        var voucher = data.Voucher;
        var pdf = new DocumentBuilder(DocumentFormat.A4Portrait, header);
        pdf.Space(24);
        pdf.Display(header.SpaName, documentTitle: "Geschenkgutschein");
        pdf.Paragraph("Geschenkgutschein", bold: true, align: DocumentAlign.Center);
        pdf.Space(28);
        pdf.Display(Worth(data));
        pdf.Paragraph(Kind(data), muted: true, align: DocumentAlign.Center);
        pdf.Space(20);
        pdf.QrCode(voucher.Code, 42);
        pdf.Paragraph(voucher.Code, bold: true, align: DocumentAlign.Center);
        pdf.Space(12);
        pdf.KeyValues(
        [
            ("Gültig bis", voucher.ValidUntil is { } until ? DocumentBuilder.Date(until) : "ohne Befristung"),
            ("Einlösbar in", string.Join(", ", data.LocationNames)),
            ("Ausgestellt am", DocumentBuilder.Date(voucher.CreatedAt, header.Zone)),
        ]);
        if (Warning(voucher.Status) is { } warning)
        {
            pdf.Paragraph(warning, bold: true);
        }

        pdf.Small("Den Gutschein lösen Sie an der Kassa ein: Zeigen Sie den Code oder den QR-Code vor.", DocumentAlign.Center);
        return pdf.Build();
    }

    private static string? Warning(VoucherStatus status) => status switch
    {
        VoucherStatus.Voided => "Dieser Gutschein ist storniert und nicht mehr einlösbar.",
        VoucherStatus.Redeemed => "Dieser Gutschein ist bereits eingelöst.",
        _ => null,
    };
}
