using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Inventurliste als PDF (PDF-Ausgaben F, ADR 0091): Material, Einheit, Soll, Gezählt, Differenz, Wert und die Summe der Differenzen für eine Zählung vom Handy; vor dem Zählen als
/// Zählliste mit leerer Spalte „Gezählt“ (<c>blank=true</c>, auch ohne Zählung für einen Lagerort). Recht wie die Inventur (<c>ManageLocation</c>), fremder Mandant und fremder Standort 404.
/// </summary>
public sealed class StockCountPdfTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Shop(TestSpa Spa, StoragePlaceDto Cabinet, Guid Masks, Guid Oil);

    private static string Base(Shop shop) => $"/api/stock/locations/{shop.Spa.Location.Id}";

    private static string Url(Shop shop, Guid sessionId, string query = "") => $"/api/locations/{shop.Spa.Location.Id}/stock-counts/{sessionId}/pdf{query}";

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    /// <summary>Raumschrank 2 mit 10 Tuchmasken (je 2,00 €), 500 ml Öl (je 0,05 €) und Duftkerzen ohne Bestand.</summary>
    private async Task<Shop> ShopAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var cabinet = await spa.PostAsync<StoragePlaceDto>($"/api/stock/locations/{spa.Location.Id}/storage-places",
            new StoragePlaceInput("Raumschrank 2", null, StoragePlaceKind.Room, spa.RoomB.Id), Ct);
        var masks = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Tuchmaske", "Stück", 2m, 0m, null), Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.05m, 0m, null), Ct);
        await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Duftkerze", "Stück", 4m, 0m, null), Ct);
        foreach (var (item, quantity) in new[] { (masks, 10m), (oil, 500m) })
        {
            await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
                new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, quantity, StoragePlaceId: cabinet.Id), Ct);
        }

        return new(spa, cabinet, masks.Id, oil.Id);
    }

    /// <summary>Lena hat im Raumschrank 7 Tuchmasken (Soll 10) und 500 ml Öl (Soll 500) gezählt und abgeschickt.</summary>
    private async Task<StockCountSessionDto> SubmittedAsync(Shop shop)
    {
        using var lena = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [shop.Spa.Location.Id]);
        var started = await TestSpa.ReadAsync<StockCountSessionDto>(await lena.PostAsync($"{Base(shop)}/storage-places/{shop.Cabinet.Id}/count-sessions", null, Ct), Ct);
        foreach (var (item, counted) in new[] { (shop.Masks, 7m), (shop.Oil, 500m) })
        {
            (await lena.PutAsJsonAsync($"{Base(shop)}/count-sessions/{started.Id}/lines/{item}", new StockCountLineInput(counted), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        }

        return await TestSpa.ReadAsync<StockCountSessionDto>(await lena.PostAsync($"{Base(shop)}/count-sessions/{started.Id}/submit", null, Ct), Ct);
    }

    [Fact]
    public async Task Die_Inventurliste_nennt_Material_Einheit_Soll_Gezaehlt_Differenz_Wert_und_die_Summe()
    {
        var shop = await ShopAsync();
        var session = await SubmittedAsync(shop);

        var (response, pdf) = await PdfAsync(shop.Spa.Client, Url(shop, session.Id));

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Inventurliste-Raumschrank-2-", response.Content.Headers.ContentDisposition?.FileNameStar, StringComparison.Ordinal);
        PdfText.Keep(pdf, "inventurliste");
        var text = PdfText.Text(pdf);
        Assert.Contains("Inventurliste Raumschrank 2", text, StringComparison.Ordinal);
        foreach (var heading in new[] { "Material", "Einheit", "Soll", "Gezählt", "Differenz", "Wert" })
        {
            Assert.Contains(heading, text, StringComparison.Ordinal);
        }

        Assert.Contains("Tuchmaske", text, StringComparison.Ordinal);
        Assert.Contains("Stück", text, StringComparison.Ordinal);
        Assert.Contains("Massageöl", text, StringComparison.Ordinal);
        Assert.Contains("-3", text, StringComparison.Ordinal);
        Assert.Contains("-6,00 €", text, StringComparison.Ordinal);
        Assert.Contains("Summe der Differenzen", text, StringComparison.Ordinal);
        Assert.Contains("gezählt von Test Person", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Duftkerze", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mit_blank_ist_es_die_Zaehlliste_mit_leerer_Spalte_Gezaehlt_ohne_die_Zaehlwerte_und_mit_allem_Material()
    {
        var shop = await ShopAsync();
        var session = await SubmittedAsync(shop);

        var (response, pdf) = await PdfAsync(shop.Spa.Client, Url(shop, session.Id, "?blank=true"));

        response.EnsureSuccessStatusCode();
        Assert.StartsWith("Zählliste-Raumschrank-2-", response.Content.Headers.ContentDisposition?.FileNameStar, StringComparison.Ordinal);
        PdfText.Keep(pdf, "zaehlliste");
        var text = PdfText.Text(pdf);
        Assert.Contains("Zählliste Raumschrank 2", text, StringComparison.Ordinal);
        Assert.Contains("Gezählt", text, StringComparison.Ordinal);
        Assert.Contains("Tuchmaske", text, StringComparison.Ordinal);
        Assert.Contains("Duftkerze", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Differenz", text, StringComparison.Ordinal);
        Assert.DoesNotContain("-6,00", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auch_ohne_Zaehlung_gibt_es_die_Zaehlliste_eines_Lagerorts()
    {
        var shop = await ShopAsync();

        var (response, pdf) = await PdfAsync(shop.Spa.Client, $"/api/locations/{shop.Spa.Location.Id}/storage-places/{shop.Cabinet.Id}/count-sheet/pdf");

        response.EnsureSuccessStatusCode();
        var text = PdfText.Text(pdf);
        Assert.Contains("Zählliste Raumschrank 2", text, StringComparison.Ordinal);
        Assert.Contains("Tuchmaske", text, StringComparison.Ordinal);
        Assert.Contains("Duftkerze", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_unbekannte_Zaehlung_ein_fremder_Lagerort_und_eine_Zaehlung_eines_anderen_Standorts_sind_404()
    {
        var shop = await ShopAsync();
        var session = await SubmittedAsync(shop);
        var second = await shop.Spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);

        var (unknown, _) = await PdfAsync(shop.Spa.Client, Url(shop, Guid.NewGuid()));
        var (otherLocation, _) = await PdfAsync(shop.Spa.Client, $"/api/locations/{second.Id}/stock-counts/{session.Id}/pdf");
        var (otherPlace, _) = await PdfAsync(shop.Spa.Client, $"/api/locations/{second.Id}/storage-places/{shop.Cabinet.Id}/count-sheet/pdf");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherLocation.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherPlace.StatusCode);
    }

    [Fact]
    public async Task Die_Leitung_darf_die_Inventurliste_drucken_die_Rezeption_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var shop = await ShopAsync();
        var session = await SubmittedAsync(shop);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [shop.Spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [shop.Spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (allowed, _) = await PdfAsync(manager, Url(shop, session.Id));
        var (forbidden, _) = await PdfAsync(reception, Url(shop, session.Id));
        var (forbiddenSheet, _) = await PdfAsync(reception, $"/api/locations/{shop.Spa.Location.Id}/storage-places/{shop.Cabinet.Id}/count-sheet/pdf");
        var (foreign, _) = await PdfAsync(other.Client, Url(shop, session.Id));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenSheet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
