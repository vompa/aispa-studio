using System.Globalization;
using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Ablaufliste als PDF (PDF-Ausgaben F, ADR 0091): Chargen, die in den nächsten n Tagen ablaufen, mit Material, Charge, Lagerort, Ablaufdatum und Menge. Recht wie die „Ablauf-Aufgaben“
/// (<c>ManageLocation</c>), fremder Mandant 404, Zeitraum 1 bis 365 Tage (Standard 30).
/// </summary>
public sealed class ExpiringStockPdfTests(ApiFixture fixture)
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    private static string Format(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string Url(Guid locationId, string query = "?days=30") => $"/api/locations/{locationId}/stock/expiring/pdf{query}";

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    /// <summary>Gesichtscreme (Charge CH-A) läuft in 5 Tagen ab, Maske (CH-B) in 20, Öl (CH-C) in 60 Tagen.</summary>
    private static async Task<TestSpa> BatchesAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        foreach (var (name, batch, days, quantity) in new[] { ("Gesichtscreme", "CH-A", 5, 12m), ("Gesichtsmaske", "CH-B", 20, 30m), ("Massageöl", "CH-C", 60, 4m) })
        {
            var item = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput(name, "Stück", 2m, 0m, null), Ct);
            await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
                new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, quantity, Batch: batch, ExpiresOn: Today.AddDays(days)), Ct);
        }

        return spa;
    }

    [Fact]
    public async Task Die_Ablaufliste_nennt_die_Chargen_der_naechsten_Tage_mit_Material_Charge_Lagerort_Datum_und_Menge()
    {
        var spa = await BatchesAsync(fixture);

        var (response, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id));

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Ablaufliste-Wien-", response.Content.Headers.ContentDisposition?.FileNameStar, StringComparison.Ordinal);
        PdfText.Keep(pdf, "ablaufliste");
        var text = PdfText.Text(pdf);
        Assert.Contains("Ablaufliste: nächste 30 Tage", text, StringComparison.Ordinal);
        foreach (var heading in new[] { "Material", "Charge", "Lagerort", "Läuft ab", "Menge" })
        {
            Assert.Contains(heading, text, StringComparison.Ordinal);
        }

        Assert.Contains("Gesichtscreme", text, StringComparison.Ordinal);
        Assert.Contains("CH-A", text, StringComparison.Ordinal);
        Assert.Contains(Format(Today.AddDays(5)), text, StringComparison.Ordinal);
        Assert.Contains("noch 5 Tage", text, StringComparison.Ordinal);
        Assert.Contains("Hauptlager", text, StringComparison.Ordinal);
        Assert.Contains("12 Stück", text, StringComparison.Ordinal);
        Assert.Contains("Gesichtsmaske", text, StringComparison.Ordinal);
        Assert.Contains("30 Stück", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Massageöl", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CH-C", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_laengerer_Zeitraum_nimmt_spaetere_Chargen_mit_und_die_zuerst_ablaufende_steht_oben()
    {
        var spa = await BatchesAsync(fixture);

        var (_, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id, "?days=90"));

        var lines = PdfText.Text(pdf).Split('\n').ToList();
        Assert.Contains("Ablaufliste: nächste 90 Tage", lines);
        Assert.Contains("Massageöl", lines);
        Assert.True(lines.IndexOf("Gesichtscreme") < lines.IndexOf("Gesichtsmaske") && lines.IndexOf("Gesichtsmaske") < lines.IndexOf("Massageöl"), "zuerst ablaufend zuerst");
    }

    [Fact]
    public async Task Ohne_days_gilt_der_Standard_30_und_ohne_Chargen_steht_ein_Hinweis_statt_der_Tabelle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var (response, pdf) = await PdfAsync(spa.Client, Url(spa.Location.Id, ""));

        response.EnsureSuccessStatusCode();
        var text = PdfText.Text(pdf);
        Assert.Contains("Ablaufliste: nächste 30 Tage", text, StringComparison.Ordinal);
        Assert.Contains("In diesem Zeitraum läuft keine Charge ab.", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("?days=0")]
    [InlineData("?days=366")]
    [InlineData("?days=-5")]
    public async Task Ein_Zeitraum_ausserhalb_von_1_bis_365_Tagen_ist_400(string query)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var (response, _) = await PdfAsync(spa.Client, Url(spa.Location.Id, query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Die_Leitung_darf_die_Ablaufliste_drucken_die_Rezeption_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await BatchesAsync(fixture);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (allowed, _) = await PdfAsync(manager, Url(spa.Location.Id));
        var (forbidden, _) = await PdfAsync(reception, Url(spa.Location.Id));
        var (foreign, _) = await PdfAsync(other.Client, Url(spa.Location.Id));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
