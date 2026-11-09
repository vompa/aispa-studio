using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Gutschein als PDF und per Mail (PDF-Ausgaben E, ADR 0091): A4 als Geschenk mit Spa-Name, Wert oder Behandlung, Code, QR-Code mit dem Code, „Gültig bis“ und „Einlösbar in …“;
/// Senden an den Käufer oder eine eingetippte Adresse mit Vermerk (<see cref="SentDocument"/>). Recht wie der Beleg (<c>ViewReceipts</c>), fremder Mandant 404.
/// </summary>
public sealed class VoucherPdfTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Verkauft einen Gutschein bar an der Kassa; ohne Gast (<paramref name="withBuyer"/> false) ist der Käufer unbekannt.</summary>
    private static async Task<(ReceiptDto Receipt, VoucherDto Voucher)> SellVoucherAsync(
        TestSpa spa, VoucherKind kind = VoucherKind.MultiPurpose, decimal? value = 50m, Guid? treatmentId = null, bool withBuyer = true)
    {
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers",
            new CashRegisterInput(spa.Location.Id, $"G-{Guid.NewGuid():N}"[..14], FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678",
                "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, withBuyer ? spa.Customer.Id : null, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Voucher, null, null, 1, value, 0, null, null, null, kind, treatmentId), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, bill.Open, null, null)]), Ct);
        return (receipt, Assert.Single(receipt.IssuedVouchers));
    }

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid voucherId)
    {
        var response = await client.GetAsync($"/api/pos/vouchers/{voucherId}/pdf", Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    private static async Task<List<SentDocument>> SentAsync(TestSpa spa, Guid voucherId)
    {
        await using var scope = spa.Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().SentDocuments.AsNoTracking()
            .Where(d => d.Kind == SentDocument.VoucherKind && d.TargetId == voucherId).ToListAsync(Ct);
    }

    [Fact]
    public async Task Der_Wertgutschein_nennt_Wert_Code_QR_Gueltigkeit_und_wo_er_gilt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (_, voucher) = await SellVoucherAsync(spa);

        var (response, pdf) = await PdfAsync(spa.Client, voucher.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"Gutschein-{voucher.Code}.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        PdfText.Keep(pdf, "gutschein-wert");
        using var document = PdfText.Open(pdf);
        Assert.Equal(1, document.PageCount);
        Assert.Equal("Geschenkgutschein", document.Info.Title);
        var text = PdfText.Text(pdf);
        Assert.Contains("Geschenkgutschein", text, StringComparison.Ordinal);
        Assert.Contains("50,00 €", text, StringComparison.Ordinal);
        Assert.Contains(voucher.Code, text, StringComparison.Ordinal);
        Assert.Contains("Gültig bis", text, StringComparison.Ordinal);
        Assert.Contains("ohne Befristung", text, StringComparison.Ordinal);
        Assert.Contains("Einlösbar in", text, StringComparison.Ordinal);
        Assert.Contains("Wien", text, StringComparison.Ordinal);
        Assert.DoesNotContain("storniert", text, StringComparison.Ordinal);
        Assert.True(PdfText.CountOperators(pdf, "re") > 100, "Der Code steht als QR-Code auf dem Gutschein.");
    }

    [Fact]
    public async Task Der_Behandlungsgutschein_nennt_die_Behandlung_statt_des_Betrags()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (_, voucher) = await SellVoucherAsync(spa, VoucherKind.SinglePurpose, null, spa.MassageTreatment.Id);

        var (_, pdf) = await PdfAsync(spa.Client, voucher.Id);

        PdfText.Keep(pdf, "gutschein-behandlung");
        var text = PdfText.Text(pdf);
        Assert.Contains("Massage", text, StringComparison.Ordinal);
        Assert.Contains("Gutschein für eine Behandlung", text, StringComparison.Ordinal);
        Assert.Contains(voucher.Code, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_stornierter_Gutschein_traegt_den_Hinweis_und_ist_nicht_einloesbar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (receipt, voucher) = await SellVoucherAsync(spa);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Irrtum"), Ct);

        var (_, pdf) = await PdfAsync(spa.Client, voucher.Id);

        Assert.Contains("Dieser Gutschein ist storniert und nicht mehr einlösbar.", PdfText.Text(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Vorbelegung_nennt_die_Adresse_des_Kaeufers_Betreff_und_Text()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (_, voucher) = await SellVoucherAsync(spa);

        var draft = await spa.GetAsync<DocumentMailDraft>($"/api/pos/vouchers/{voucher.Id}/send", Ct);

        Assert.Equal("eva@example.at", draft.To);
        Assert.StartsWith("Ihr Gutschein von ", draft.Subject, StringComparison.Ordinal);
        Assert.Contains("Ihren Gutschein über 50,00 €", draft.Text, StringComparison.Ordinal);
        Assert.Contains("als PDF", draft.Text, StringComparison.Ordinal);
        Assert.Contains("Freundliche Grüße", draft.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_bekannten_Kaeufer_ist_die_Adresse_der_Vorbelegung_leer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (_, voucher) = await SellVoucherAsync(spa, withBuyer: false);

        var draft = await spa.GetAsync<DocumentMailDraft>($"/api/pos/vouchers/{voucher.Id}/send", Ct);

        Assert.Null(draft.To);
    }

    [Fact]
    public async Task Senden_legt_die_Mail_mit_dem_PDF_in_die_Queue_und_vermerkt_es()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (_, voucher) = await SellVoucherAsync(spa);
        var to = $"beschenkte-{Guid.NewGuid():N}@example.at";
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id], spa.Anna.Id);

        var response = await reception.PostAsJsonAsync($"/api/pos/vouchers/{voucher.Id}/send", new SendDocumentRequest($" {to} "), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(to, m => m.Attachments is { Count: > 0 }, Ct);
        var attachment = Assert.Single(mail.Attachments!);
        Assert.Equal($"Gutschein-{voucher.Code}.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Contains(voucher.Code, PdfText.Text(attachment.Content), StringComparison.Ordinal);
        Assert.StartsWith("Ihr Gutschein von ", mail.Subject, StringComparison.Ordinal);
        var note = Assert.Single(await SentAsync(spa, voucher.Id));
        Assert.Equal(to, note.To);
        Assert.Equal("Anna", note.SentBy);
        Assert.InRange(note.SentAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Eine_ungueltige_Adresse_ist_400_und_vermerkt_nichts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (_, voucher) = await SellVoucherAsync(spa);

        var response = await spa.Client.PostAsJsonAsync($"/api/pos/vouchers/{voucher.Id}/send", new SendDocumentRequest("eva@"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await SentAsync(spa, voucher.Id));
    }

    [Fact]
    public async Task Das_Backoffice_darf_den_Gutschein_wie_den_Beleg_ansehen_der_Therapeut_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (_, voucher) = await SellVoucherAsync(spa);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (allowed, _) = await PdfAsync(backoffice, voucher.Id);
        var (forbidden, _) = await PdfAsync(therapist, voucher.Id);
        var (foreign, _) = await PdfAsync(other.Client, voucher.Id);
        var draft = await other.Client.GetAsync($"/api/pos/vouchers/{voucher.Id}/send", Ct);
        var send = await other.Client.PostAsJsonAsync($"/api/pos/vouchers/{voucher.Id}/send", new SendDocumentRequest("eva@example.at"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
    }
}
