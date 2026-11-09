using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// „Passt dazu“ (Slice 115, Ä251): Verkaufsartikel je Behandlung, vorgeschlagen im Termin und an der Kassa, nur mit Bestand am Verkaufsregal.
/// Eine Provision auf Produktverkauf gibt es nicht (E12).
/// </summary>
public sealed class ProductRecommendationTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string StockUrl(TestSpa spa) => $"/api/stock/locations/{spa.Location.Id}";

    private static string MappingUrl(TestSpa spa) => $"/api/treatments/{spa.MassageTreatment.Id}/recommended-articles";

    [Fact]
    public async Task Regal_mit_3_Stueck_Kassa_schlaegt_das_Oel_nach_dem_Abschliessen_vor()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var shop = await ShopAsync(spa);
        var oil = await ArticleAsync(spa, shop, "Massageöl Lavendel", onShelf: 3m);
        await spa.PutAsync<List<Guid>>(MappingUrl(spa), new[] { oil.Id }, Ct);
        var appointment = await CompleteAsync(spa, await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id));
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(shop.Register.Id, null, [appointment.Id]), Ct);

        var atPos = await spa.GetAsync<List<RecommendedArticleDto>>($"/api/pos/bills/{bill.Id}/recommendations", Ct);
        var inPanel = await spa.GetAsync<List<RecommendedArticleDto>>($"/api/appointments/{appointment.Id}/recommendations", Ct);

        var suggestion = Assert.Single(atPos);
        Assert.Equal((oil.Id, "Massageöl Lavendel", 24.90m, 3m, "Stück"),
            (suggestion.ArticleId, suggestion.Name, suggestion.Price, suggestion.ShelfQuantity, suggestion.Unit));
        Assert.Equal(oil.Id, Assert.Single(inPanel).ArticleId);
    }

    [Fact]
    public async Task Leeres_Regal_kein_Vorschlag_im_Termin_und_an_der_Kassa()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var shop = await ShopAsync(spa);
        var oil = await ArticleAsync(spa, shop, "Massageöl Lavendel", onShelf: 0m, inMain: 10m);
        await spa.PutAsync<List<Guid>>(MappingUrl(spa), new[] { oil.Id }, Ct);
        var appointment = await CompleteAsync(spa, await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id));
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(shop.Register.Id, null, [appointment.Id]), Ct);

        var atPos = await spa.GetAsync<List<RecommendedArticleDto>>($"/api/pos/bills/{bill.Id}/recommendations", Ct);
        var inPanel = await spa.GetAsync<List<RecommendedArticleDto>>($"/api/appointments/{appointment.Id}/recommendations", Ct);

        Assert.Empty(atPos);
        Assert.Empty(inPanel);
    }

    [Fact]
    public async Task Artikel_schon_auf_der_Rechnung_wird_nicht_mehr_vorgeschlagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var shop = await ShopAsync(spa);
        var oil = await ArticleAsync(spa, shop, "Massageöl Lavendel", onShelf: 3m);
        await spa.PutAsync<List<Guid>>(MappingUrl(spa), new[] { oil.Id }, Ct);
        var appointment = await CompleteAsync(spa, await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id));
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(shop.Register.Id, null, [appointment.Id]), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Article, null, oil.Id, 1, null, 0, null, null, null, null, null), Ct);

        var atPos = await spa.GetAsync<List<RecommendedArticleDto>>($"/api/pos/bills/{bill.Id}/recommendations", Ct);

        Assert.Empty(atPos);
    }

    [Fact]
    public async Task Artikel_ohne_Lagerartikel_wird_nie_vorgeschlagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var shop = await ShopAsync(spa);
        var voucherBook = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Gutscheinheft", 10m, TaxCategory.Normal, shop.AccountId), Ct);
        var cream = await ArticleAsync(spa, shop, "Gesichtscreme", onShelf: 2m);
        await spa.PutAsync<List<Guid>>(MappingUrl(spa), new[] { voucherBook.Id, cream.Id }, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);

        var inPanel = await spa.GetAsync<List<RecommendedArticleDto>>($"/api/appointments/{appointment.Id}/recommendations", Ct);

        Assert.Equal([cream.Id], inPanel.Select(a => a.ArticleId));
    }

    [Fact]
    public async Task Zuordnung_kommt_in_der_gespeicherten_Reihenfolge_zurueck()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var shop = await ShopAsync(spa);
        var cream = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Gesichtscreme", 39m, TaxCategory.Normal, shop.AccountId), Ct);
        var oil = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Massageöl", 24.90m, TaxCategory.Normal, shop.AccountId), Ct);

        await spa.PutAsync<List<Guid>>(MappingUrl(spa), new[] { oil.Id, cream.Id }, Ct);
        var saved = await spa.GetAsync<List<Guid>>(MappingUrl(spa), Ct);

        Assert.Equal([oil.Id, cream.Id], saved);
    }

    [Theory]
    [InlineData("unbekannt")]
    [InlineData("doppelt")]
    [InlineData("zu viele")]
    public async Task Ungueltige_Zuordnung_wird_mit_Feldmeldung_abgelehnt(string fault)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var shop = await ShopAsync(spa);
        var articles = await Task.WhenAll(Enumerable.Range(0, 6).Select(async i =>
            (await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput($"Artikel {i}", 10m, TaxCategory.Normal, shop.AccountId), Ct)).Id));
        Guid[] input = fault switch
        {
            "unbekannt" => [Guid.NewGuid()],
            "doppelt" => [articles[0], articles[0]],
            _ => articles,
        };

        var response = await spa.Client.PutAsJsonAsync(MappingUrl(spa), input, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("articleIds", (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!.Errors.Keys);
    }

    [Fact]
    public async Task Rezeption_darf_die_Zuordnung_nicht_aendern()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var response = await reception.PutAsJsonAsync(MappingUrl(spa), Array.Empty<Guid>(), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record Shop(CashRegisterDto Register, StoragePlaceDto Main, StoragePlaceDto Shelf, Guid AccountId);

    private static async Task<Shop> ShopAsync(TestSpa spa)
    {
        var main = (await spa.GetAsync<List<StoragePlaceDto>>($"{StockUrl(spa)}/storage-places", Ct)).Single(p => p.Kind == StoragePlaceKind.Main);
        var shelf = await spa.PostAsync<StoragePlaceDto>($"{StockUrl(spa)}/storage-places",
            new StoragePlaceInput("Verkaufsregal Rezeption", null, StoragePlaceKind.Sales, null, true, null), Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"L-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien", "Test-Spa GmbH", null, null), Ct);
        var accounts = await spa.GetAsync<List<RevenueAccountDto>>("/api/pos/revenue-accounts", Ct);
        return new Shop(register, main, shelf, accounts[0].Id);
    }

    /// <summary>Kassa-Artikel mit Lagerartikel: Wareneingang ins Hauptlager, davon <paramref name="onShelf"/> ins Verkaufsregal nachgefüllt.</summary>
    private static async Task<ArticleDto> ArticleAsync(TestSpa spa, Shop shop, string name, decimal onShelf, decimal inMain = 0m)
    {
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput(name, 24.90m, TaxCategory.Normal, shop.AccountId), Ct);
        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput(name, "Stück", 8m, 0, article.Id), Ct);
        await spa.PostAsync<List<StockMovementDto>>($"{StockUrl(spa)}/movements", new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, onShelf + inMain), Ct);
        if (onShelf > 0)
        {
            await spa.PostAsync<List<StockMovementDto>>($"{StockUrl(spa)}/movements",
                new StockMovementInput(item.Id, StockMovementKind.Refill, onShelf, StoragePlaceId: shop.Main.Id, TargetStoragePlaceId: shop.Shelf.Id), Ct);
        }

        return article;
    }

    private static Task<AppointmentDto> CompleteAsync(TestSpa spa, AppointmentDto appointment) =>
        spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);
}
