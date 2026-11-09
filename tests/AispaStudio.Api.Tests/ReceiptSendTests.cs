using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Beleg per Mail (PDF-Ausgaben B, ADR 0091): Vorbelegung mit der Adresse des Gastes, Betreff und Text; der Versand legt die Mail mit dem PDF in die Queue,
/// vermerkt es am Beleg („Per Mail gesendet an …“), und eine ungültige Adresse ist 400.
/// </summary>
public sealed class ReceiptSendTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Die_Vorbelegung_nennt_die_Adresse_des_Gastes_Betreff_und_Text()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await ReceiptPdfTests.SellTeaAsync(spa);

        var draft = await spa.GetAsync<DocumentMailDraft>($"/api/pos/receipts/{receipt.Id}/send", Ct);

        Assert.Equal("eva@example.at", draft.To);
        Assert.StartsWith($"Ihr Beleg Nr. {receipt.Number} von ", draft.Subject, StringComparison.Ordinal);
        Assert.Contains($"Ihren Beleg Nr. {receipt.Number} vom ", draft.Text, StringComparison.Ordinal);
        Assert.Contains("als PDF", draft.Text, StringComparison.Ordinal);
        Assert.Contains("Freundliche Grüße", draft.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Rechnung_heisst_im_Betreff_Ihre_Rechnung_mit_Kassen_und_Belegnummer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await ReceiptPdfTests.SellTeaAsync(spa);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/recipient", new InvoiceRecipient("Muster GmbH", "Graben 1\n1010 Wien", "ATU99999999"), Ct);

        var draft = await spa.GetAsync<DocumentMailDraft>($"/api/pos/receipts/{receipt.Id}/send", Ct);

        Assert.StartsWith($"Ihre Rechnung Nr. {receipt.RegisterCode}-{receipt.Number} von ", draft.Subject, StringComparison.Ordinal);
        Assert.Contains($"Ihre Rechnung Nr. {receipt.RegisterCode}-{receipt.Number}", draft.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Senden_legt_die_Mail_mit_dem_PDF_in_die_Queue_und_vermerkt_es_am_Beleg()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await ReceiptPdfTests.SellTeaAsync(spa);
        var to = $"anna-{Guid.NewGuid():N}@example.at";

        var response = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/send", new SendDocumentRequest(to), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(to, m => m.Attachments is { Count: > 0 }, Ct);
        var attachment = Assert.Single(mail.Attachments!);
        Assert.Equal($"Beleg-{receipt.RegisterCode}-{receipt.Number}.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Contains($"Beleg Nr. {receipt.Number}", PdfText.Text(attachment.Content), StringComparison.Ordinal);
        Assert.StartsWith($"Ihr Beleg Nr. {receipt.Number} von ", mail.Subject, StringComparison.Ordinal);
        Assert.NotNull(mail.FromName);

        var updated = await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}", Ct);
        var note = Assert.Single(updated.SentMails!);
        Assert.Equal(to, note.To);
        Assert.InRange(note.SentAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Eigener_Betreff_und_Text_werden_uebernommen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await ReceiptPdfTests.SellTeaAsync(spa);
        var to = $"anna-{Guid.NewGuid():N}@example.at";

        var response = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/send",
            new SendDocumentRequest($" {to} ", "Danke für Ihren Besuch", "Guten Tag,\n\nwie besprochen der Beleg.\n"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(to, m => m.Attachments is { Count: > 0 }, Ct);
        Assert.Equal("Danke für Ihren Besuch", mail.Subject);
        Assert.Equal("Guten Tag,\n\nwie besprochen der Beleg.", mail.Text);
    }

    [Fact]
    public async Task Eine_ungueltige_Adresse_ist_400_und_sendet_nichts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var receipt = await ReceiptPdfTests.SellTeaAsync(spa);

        var response = await spa.Client.PostAsJsonAsync($"/api/pos/receipts/{receipt.Id}/send", new SendDocumentRequest("anna@"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Mailadresse", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Empty((await spa.GetAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}", Ct)).SentMails ?? []);
    }
}
