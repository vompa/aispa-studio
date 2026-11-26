using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Management-Bericht als PDF (T-095, Paket D2): A4 hoch aus demselben Berichtskern wie die Arbeitsmappe, Kennzahlen in Gruppen, Verlauf als Balken; ohne das Recht
/// „Berichte mit Beträgen“ ohne Beträge. Senden nur an die eingetippte Adresse (vorbelegt ist die eigene), jede Sendung im Export-Protokoll.
/// </summary>
public sealed class ManagementReportPdfTests(ApiFixture fixture)
{
    private static readonly TimeSpan MailWait = TimeSpan.FromSeconds(60);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Path(Guid locationId, string tail = "pdf") => $"/api/reports/locations/{locationId}/management/{tail}?from=2027-03-01&until=2027-03-07";

    [Fact]
    public async Task Bericht_als_PDF_hoch_mit_Titel_Zeitraum_Kennzahlen_in_Gruppen_und_Verlauf_im_Export_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct);

        using var response = await spa.Client.GetAsync(Path(spa.Location.Id), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Management-Bericht-Wien-2027-03-01-bis-2027-03-07.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        var pdf = PdfText.Keep(await response.Content.ReadAsByteArrayAsync(Ct), "management-bericht");
        using (var document = PdfText.Open(pdf))
        {
            Assert.True(document.Pages[0].Height.Point > document.Pages[0].Width.Point, "A4 hoch");
        }

        var text = PdfText.Text(pdf);
        foreach (var expected in new[] { "Management-Bericht", "1.3.2027 bis 7.3.2027", "Umsatz und Ertrag", "Auslastung", "Umsatz brutto", "EUR", "Verlauf" })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.StartsWith("Export Bericht (PDF, ", Assert.Single(history).Text);
    }

    [Fact]
    public async Task Bericht_als_PDF_ohne_Recht_Berichte_mit_Betraegen_ohne_Betraege()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct);
        // Die Standortleitung sieht den Bericht ohne Beträge, bis ihr das Recht vergeben wird (das Backoffice hat es von Haus aus).
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);

        using var response = await manager.GetAsync(Path(spa.Location.Id), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = PdfText.Text(PdfText.Keep(await response.Content.ReadAsByteArrayAsync(Ct), "management-bericht-ohne-betraege"));
        Assert.Contains("Auslastung", text, StringComparison.Ordinal);
        Assert.DoesNotContain("EUR", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Umsatz brutto", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Senden_nur_an_die_eingetippte_Adresse_vorbelegt_die_eigene_und_im_Export_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var me = await spa.GetAsync<CurrentUser>("/api/auth/me", Ct);
        var to = $"leitung-{Guid.NewGuid():N}@example.at";

        var draft = await spa.GetAsync<DocumentMailDraft>(Path(spa.Location.Id, "send"), Ct);
        using var invalid = await spa.Client.PostAsJsonAsync(Path(spa.Location.Id, "send"), new SendDocumentRequest(""), TestSpa.Json, Ct);
        using var sent = await spa.Client.PostAsJsonAsync(Path(spa.Location.Id, "send"), new SendDocumentRequest(to), TestSpa.Json, Ct);

        Assert.Equal(me.Email, draft.To);
        Assert.Contains("Management-Bericht", draft.Subject, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(to, m => m.Attachments is { Count: > 0 }, Ct, MailWait);
        var attachment = Assert.Single(mail.Attachments!);
        Assert.Equal("Management-Bericht-Wien-2027-03-01-bis-2027-03-07.pdf", attachment.FileName);
        Assert.Contains("Management-Bericht", PdfText.Text(attachment.Content), StringComparison.Ordinal);
        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        var entry = Assert.Single(history).Text;
        Assert.StartsWith("Export Bericht per Mail (PDF, ", entry);
        Assert.Contains(to, entry, StringComparison.Ordinal);
    }
}
