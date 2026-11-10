using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Beleg und Rechnung als PDF (PDF-Ausgaben B, ADR 0092): A4 mit Positionen, Summe, Zahlart und RKSV-QR, Bon-Format 80 mm, Rechnung mit Empfängerzeile,
/// Nachdruck als Kopie, Stornobeleg gekennzeichnet; Recht <c>ViewReceipts</c>, fremder Mandant 404. Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class ReceiptPdfTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static async Task<ReceiptDto> SellTeaAsync(TestSpa spa, int quantity = 2, decimal paid = 30m)
    {
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, spa.Customer.Id, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, quantity, 12.50m, 0, "Kräutertee „Bergblüte“", TaxCategory.Normal, null, null, null), Ct);
        return await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, paid, null, null)]), Ct);
    }

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid receiptId, string query = "")
    {
        var response = await client.GetAsync($"/api/pos/receipts/{receiptId}/pdf{query}", Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    [Fact]
    public async Task Beleg_als_PDF_A4_mit_Positionen_Summe_Zahlart_Rueckgeld_und_QR()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await SellTeaAsync(spa);

        var (response, pdf) = await PdfAsync(spa.Client, receipt.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"Beleg-{receipt.RegisterCode}-{receipt.Number}.pdf", response.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        PdfText.Keep(pdf, "beleg-a4");
        var text = PdfText.Text(pdf);
        Assert.Contains($"Beleg Nr. {receipt.Number}", text, StringComparison.Ordinal);
        Assert.Contains("Test-Spa GmbH", text, StringComparison.Ordinal);
        Assert.Contains("2 × Kräutertee „Bergblüte“", text, StringComparison.Ordinal);
        Assert.Contains("25,00", text, StringComparison.Ordinal);
        Assert.Contains("Bar", text, StringComparison.Ordinal);
        Assert.Contains("Rückgeld", text, StringComparison.Ordinal);
        Assert.Contains("5,00", text, StringComparison.Ordinal);
        Assert.Contains("20 % USt", text, StringComparison.Ordinal);
        Assert.Contains("Signierter Belegcode (RKSV)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Kopie", text, StringComparison.Ordinal);
        Assert.True(PdfText.CountOperators(pdf, "re") > 100, "Der RKSV-Code steht als QR-Code auf dem Beleg.");
        using var document = PdfText.Open(pdf);
        Assert.Equal(595.3, document.Pages[0].Width.Point, 0.5);
    }

    [Fact]
    public async Task Bon_Format_ist_80_mm_breit_und_der_Nachdruck_traegt_Kopie()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await SellTeaAsync(spa);

        var (response, pdf) = await PdfAsync(spa.Client, receipt.Id, "?format=Receipt&copy=true");

        response.EnsureSuccessStatusCode();
        PdfText.Keep(pdf, "beleg-bon");
        using var document = PdfText.Open(pdf);
        Assert.Equal(1, document.PageCount);
        Assert.Equal(226.8, document.Pages[0].Width.Point, 0.5);
        var text = PdfText.Text(pdf);
        Assert.Contains($"Beleg Nr. {receipt.Number}", text, StringComparison.Ordinal);
        Assert.Contains("Kopie", text, StringComparison.Ordinal);
        Assert.Contains("Kräutertee", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rechnung_mit_Empfaenger_traegt_Empfaengerzeile_Leistungsdatum_und_Rechnungstitel()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await SellTeaAsync(spa);
        var invoice = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/recipient", new InvoiceRecipient("Muster GmbH", "Graben 1\n1010 Wien", "ATU99999999"), Ct);

        var (response, pdf) = await PdfAsync(spa.Client, invoice.Id);

        response.EnsureSuccessStatusCode();
        Assert.Contains($"Rechnung-{receipt.RegisterCode}-{receipt.Number}.pdf", response.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        PdfText.Keep(pdf, "rechnung-a4");
        var text = PdfText.Text(pdf);
        Assert.Contains($"Rechnung Nr. {receipt.RegisterCode}-{receipt.Number}", text, StringComparison.Ordinal);
        Assert.Contains("Muster GmbH", text, StringComparison.Ordinal);
        Assert.Contains("1010 Wien", text, StringComparison.Ordinal);
        Assert.Contains("UID ATU99999999", text, StringComparison.Ordinal);
        Assert.Contains("Rechnungs- und Leistungsdatum", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Stornobeleg_ist_gekennzeichnet_und_nennt_den_Grund()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await SellTeaAsync(spa);
        var voided = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), Ct);

        var (response, pdf) = await PdfAsync(spa.Client, voided.Id);

        response.EnsureSuccessStatusCode();
        var text = PdfText.Text(pdf);
        Assert.Contains($"Stornobeleg Nr. {voided.Number}", text, StringComparison.Ordinal);
        Assert.Contains("Falsch kassiert", text, StringComparison.Ordinal);
        Assert.Contains("-25,00", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Recht_Belege_ansehen_403_und_fremder_Mandant_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await SellTeaAsync(spa);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (forbidden, _) = await PdfAsync(therapist, receipt.Id);
        var (foreign, _) = await PdfAsync(other.Client, receipt.Id);
        var draft = await other.Client.GetAsync($"/api/pos/receipts/{receipt.Id}/send", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
    }
}
