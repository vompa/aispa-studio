using System.Net;
using System.Net.Http.Json;
using System.Text;
using AispaStudio.Contracts;
using ClosedXML.Excel;

namespace AispaStudio.Api.Tests;

/// <summary>Kundenliste als Excel, CSV, Portal-CSV und vCard (Export/Import, Aufgabe B): Filter der Liste, Vermerk nur für die Leitung, nie Gesundheitsfelder, Protokoll im Verlauf.</summary>
public sealed class CustomerExportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] ForbiddenHeaders = ["Gesundheit", "Anamnese", "Wünsche", "Wunsch", "Rabatt", "Rechnung", "Notiz"];

    private async Task<(TestSpa Spa, CustomerDto Huber)> SpaWithGuestsAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var huber = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Anna", "Huber", "anna.huber@beispiel.at", "+43 664 1234567", new DateOnly(1985, 3, 14),
            "mag Lavendelöl", Salutation: CustomerSalutation.Female, TitlesBefore: "Mag.", Address: new CustomerAddress("Ringstraße 1", "1010", "Wien", "AT")), Ct);
        await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Max", "Maier", null, "+43 676 5550102", null, null), Ct);
        await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ödön", "Öhlinger", "oe@beispiel.at", null, null, null, Language: "en"), Ct);
        (await spa.Client.PutAsJsonAsync($"/api/customers/{huber.Id}/reception-note", new { text = "Zahlt mit Firmenkarte" }, TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        return (spa, huber);
    }

    private static string FileName(HttpResponseMessage response) =>
        (response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? "").Trim('"');

    /// <summary>Liest die Datei und legt sie mit <c>AISPA_EXPORT_SAMPLES</c> unter ihrem Servernamen zum Ansehen ab.</summary>
    private static async Task<byte[]> SavedAsync(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        SampleFiles.Save(FileName(response), bytes);
        return bytes;
    }

    private static async Task<string[]> CsvLinesAsync(HttpResponseMessage response)
    {
        var bytes = await SavedAsync(response);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        return Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public async Task Excel_mit_Suchfilter_Vermerk_fuer_die_Leitung_und_Eintrag_im_Verlauf()
    {
        var (spa, _) = await SpaWithGuestsAsync();

        using var response = await spa.Client.GetAsync("/api/customers/export?format=xlsx&search=huber", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.Matches(@"^Kunden_\d{4}-\d{2}-\d{2}\.xlsx$", FileName(response));
        using var workbook = new XLWorkbook(new MemoryStream(await SavedAsync(response)));
        var sheet = Assert.Single(workbook.Worksheets);
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(["Anrede", "Titel", "Vorname", "Nachname", "Rufname", "E-Mail", "Telefon", "Geburtstag", "Sprache", "Straße", "PLZ", "Ort", "Land",
            "Werbe-Einwilligung", "Einwilligung am", "Angelegt am", "Kanal", "Letzter Besuch", "Besuche", "Vermerk"], headers);
        Assert.All(ForbiddenHeaders, word => Assert.DoesNotContain(headers, h => h.Contains(word, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber());
        var row = sheet.Row(2);
        Assert.Equal(("Frau", "Mag.", "Anna", "Huber", "anna.huber@beispiel.at"), (row.Cell(1).GetString(), row.Cell(2).GetString(), row.Cell(3).GetString(), row.Cell(4).GetString(), row.Cell(6).GetString()));
        Assert.Equal(new DateTime(1985, 3, 14), row.Cell(8).GetDateTime());
        Assert.Equal("Deutsch", row.Cell(9).GetString());
        Assert.Equal("offen", row.Cell(14).GetString());
        Assert.True(row.Cell(15).IsEmpty());
        Assert.Equal("Rezeption", row.Cell(17).GetString());
        Assert.Equal(0, row.Cell(19).GetDouble());
        Assert.Equal("Zahlt mit Firmenkarte", row.Cell(20).GetString());

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Equal("Export Kundenliste (Excel, 1 Zeile, Suche „huber“) von Test Person", Assert.Single(history).Text);
    }

    [Fact]
    public async Task CSV_fuer_die_Rezeption_ohne_Vermerk_mit_allen_Gaesten()
    {
        var (spa, huber) = await SpaWithGuestsAsync();
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var maier = (await spa.GetAsync<List<CustomerDto>>("/api/customers?search=maier", Ct)).Single();
        (await reception.PutAsJsonAsync($"/api/customers/{huber.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Confirmed), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        (await reception.PutAsJsonAsync($"/api/customers/{maier.Id}/marketing-consent", new MarketingConsentDecision(MarketingConsentState.Refused), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        using var response = await reception.GetAsync("/api/customers/export?format=csv", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = await CsvLinesAsync(response);
        Assert.Equal("Anrede;Titel;Vorname;Nachname;Rufname;E-Mail;Telefon;Geburtstag;Sprache;Straße;PLZ;Ort;Land;Werbe-Einwilligung;Einwilligung am;Angelegt am;Kanal;Letzter Besuch;Besuche", lines[0]);
        Assert.Equal(5, lines.Length);
        Assert.Contains(lines, l => l.StartsWith("Frau;Mag.;Anna;Huber;;anna.huber@beispiel.at;+43 664 1234567;14.03.1985;Deutsch;Ringstraße 1;1010;Wien;AT;Ja;", StringComparison.Ordinal));
        Assert.Matches(@";Huber;.*;Ja;\d{2}\.\d{2}\.\d{4};", lines.Single(l => l.Contains(";Huber;", StringComparison.Ordinal)));
        Assert.Matches(@";Maier;.*;Nein;\d{2}\.\d{2}\.\d{4};", lines.Single(l => l.Contains(";Maier;", StringComparison.Ordinal)));
        Assert.Contains(lines, l => l.Contains(";Ödön;Öhlinger;;oe@beispiel.at;;;Englisch;;;;;offen;;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Portal_CSV_hat_englische_Spalten_mit_Komma_und_ISO_Datum()
    {
        var (spa, _) = await SpaWithGuestsAsync();

        using var response = await spa.Client.GetAsync("/api/customers/export?format=portal&search=huber", Ct);

        var lines = await CsvLinesAsync(response);
        Assert.Equal("First name,Last name,Email,Mobile,Date of birth,Notes,Marketing consent", lines[0]);
        Assert.Equal("Anna,Huber,anna.huber@beispiel.at,+43 664 1234567,1985-03-14,,no", lines[1]);
        Assert.Matches(@"^Kunden_Portal_\d{4}-\d{2}-\d{2}\.csv$", FileName(response));
    }

    [Fact]
    public async Task VCard_fuer_die_Liste_und_fuer_einen_Kontakt_mit_Vermerk_nur_fuer_die_Leitung()
    {
        var (spa, huber) = await SpaWithGuestsAsync();

        using var all = await spa.Client.GetAsync("/api/customers/export?format=vcf", Ct);
        var text = Encoding.UTF8.GetString(await SavedAsync(all));
        Assert.Equal("text/vcard", all.Content.Headers.ContentType?.MediaType);
        Assert.Equal(4, text.Split("BEGIN:VCARD").Length - 1);
        Assert.Matches(@"^Kontakte_\d{4}-\d{2}-\d{2}\.vcf$", FileName(all));

        using var one = await spa.Client.GetAsync($"/api/customers/{huber.Id}/vcard", Ct);
        var card = Encoding.UTF8.GetString(await SavedAsync(one));
        Assert.Equal("Kontakt_Huber.vcf", FileName(one));
        Assert.Contains("FN:Mag. Anna Huber", card, StringComparison.Ordinal);
        Assert.Contains("TEL;TYPE=cell;VALUE=uri:tel:+436641234567", card, StringComparison.Ordinal);
        Assert.Contains("NOTE:Zahlt mit Firmenkarte", card, StringComparison.Ordinal);
        Assert.DoesNotContain("Lavendel", card, StringComparison.Ordinal);

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var plain = await reception.GetAsync($"/api/customers/{huber.Id}/vcard", Ct);
        Assert.DoesNotContain("NOTE:", await plain.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Contains(history, h => h.Text == "Export Kundenliste (vCard, 4 Zeilen) von Test Person");
        Assert.Contains(history, h => h.Text == "Export Kontakt (vCard, 1 Zeile, Anna Huber) von Test Person");
    }

    [Fact]
    public async Task Ein_fremdes_Spa_sieht_nur_seine_eigenen_Gaeste()
    {
        await SpaWithGuestsAsync();
        var other = await TestSpa.CreateAsync(fixture, Ct);

        using var response = await other.Client.GetAsync("/api/customers/export?format=csv", Ct);

        Assert.Equal(2, (await CsvLinesAsync(response)).Length);
    }

    [Theory]
    [InlineData("ics")]
    [InlineData("pdf")]
    [InlineData("")]
    public async Task Kalenderdatei_und_unbekanntes_Format_ergeben_einen_Hinweis(string format)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        using var response = await spa.Client.GetAsync($"/api/customers/export?format={format}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("format", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }
}
