using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Tagesabschluss als PDF (PDF-Ausgaben D, ADR 0092): Kasse, Zeitraum, Belege von–bis, Summen je Zahlart, Steuern, Soll, gezählter Bestand, Differenz mit Notiz,
/// Einlagen und Entnahmen, Unterschriftenzeilen. Recht wie das Lesen der Abschlüsse (<c>ViewAmountReports</c>), fremder Mandant 404. Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class DayClosingPdfTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Verkauf über 25,00 € bar, Einlage 100,00 €, Entnahme 20,00 €, gezählt 103,50 € (Soll 105,00 €): Differenz −1,50 €.</summary>
    private static async Task<(ReceiptDto Sale, DayClosingDto Closing)> CloseDayAsync(TestSpa spa)
    {
        var sale = await ReceiptPdfTests.SellTeaAsync(spa, paid: 25m);
        await spa.PostAsync<CashMovementDto>($"/api/pos/registers/{sale.RegisterId}/cash-movements", new CashMovementInput(CashMovementKind.Deposit, 100m, "Wechselgeld"), Ct);
        await spa.PostAsync<CashMovementDto>($"/api/pos/registers/{sale.RegisterId}/cash-movements", new CashMovementInput(CashMovementKind.Withdrawal, 20m, "Abschöpfung Bank"), Ct);
        var closing = await spa.PostAsync<DayClosingDto>($"/api/pos/registers/{sale.RegisterId}/day-closings", new DayClosingInput(103.50m, "Zwei Münzen verlegt"), Ct);
        return (sale, closing);
    }

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid registerId, Guid closingId)
    {
        var response = await client.GetAsync($"/api/pos/registers/{registerId}/day-closings/{closingId}/pdf", Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    [Fact]
    public async Task Tagesabschluss_als_PDF_nennt_Summen_Soll_gezaehlt_und_Differenz_mit_Notiz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (sale, closing) = await CloseDayAsync(spa);

        var (response, pdf) = await PdfAsync(spa.Client, sale.RegisterId, closing.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Matches($@"^Tagesabschluss-{sale.RegisterCode}-\d{{4}}-\d{{2}}-\d{{2}}\.pdf$", response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "");
        PdfText.Keep(pdf, "tagesabschluss");
        var text = PdfText.Text(pdf);
        Assert.Contains($"Tagesabschluss {sale.RegisterCode}", text, StringComparison.Ordinal);
        Assert.Contains("Test-Spa", text, StringComparison.Ordinal);
        Assert.Contains($"Nr. {sale.Number} bis Nr. {sale.Number}", text, StringComparison.Ordinal);
        Assert.Contains("1 Verkauf, 0 Stornos", text, StringComparison.Ordinal);
        Assert.Contains("Bar", text, StringComparison.Ordinal);
        Assert.Contains("Umsatz gesamt", text, StringComparison.Ordinal);
        Assert.Contains("20 % USt", text, StringComparison.Ordinal);
        Assert.Contains("Soll-Bestand", text, StringComparison.Ordinal);
        Assert.Contains("105,00", text, StringComparison.Ordinal);
        Assert.Contains("Gezählter Bestand", text, StringComparison.Ordinal);
        Assert.Contains("103,50", text, StringComparison.Ordinal);
        Assert.Contains("Differenz", text, StringComparison.Ordinal);
        Assert.Contains("-1,50", text, StringComparison.Ordinal);
        Assert.Contains("Zwei Münzen verlegt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Einlagen_und_Entnahmen_des_Tages_stehen_mit_Grund_im_Abschluss()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (sale, closing) = await CloseDayAsync(spa);

        var (_, pdf) = await PdfAsync(spa.Client, sale.RegisterId, closing.Id);

        var text = PdfText.Text(pdf);
        Assert.Contains("Einlagen und Entnahmen", text, StringComparison.Ordinal);
        Assert.Contains("Wechselgeld", text, StringComparison.Ordinal);
        Assert.Contains("Abschöpfung Bank", text, StringComparison.Ordinal);
        Assert.Contains("100,00", text, StringComparison.Ordinal);
        Assert.Contains("-20,00", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Das_Dokument_traegt_Unterschriftenzeilen_Gezaehlt_von_und_Geprueft_von()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (sale, closing) = await CloseDayAsync(spa);

        var (_, pdf) = await PdfAsync(spa.Client, sale.RegisterId, closing.Id);

        var text = PdfText.Text(pdf);
        Assert.Contains("Gezählt von", text, StringComparison.Ordinal);
        Assert.Contains("Geprüft von", text, StringComparison.Ordinal);
        using var document = PdfText.Open(pdf);
        Assert.Equal(595.3, document.Pages[0].Width.Point, 0.5);
    }

    [Fact]
    public async Task Ohne_Recht_Berichte_mit_Betraegen_403_fremder_Mandant_und_fremde_Kasse_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (sale, closing) = await CloseDayAsync(spa);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (forbidden, _) = await PdfAsync(reception, sale.RegisterId, closing.Id);
        var (foreign, _) = await PdfAsync(other.Client, sale.RegisterId, closing.Id);
        var (wrongRegister, _) = await PdfAsync(spa.Client, Guid.NewGuid(), closing.Id);
        var (unknown, _) = await PdfAsync(spa.Client, sale.RegisterId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongRegister.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
