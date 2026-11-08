using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Slice 136 (Ä318): Bestand, Mindestbestand und Verkaufspreis im Assistenten. Das Lager sehen alle mit dem Recht „Kalender ansehen“, aber nie Einstandspreise oder Werte.
/// Der Verkaufspreis kommt aus dem Verkaufsartikel der Kassa, nicht aus dem Material.
/// </summary>
public sealed class StockAnswerTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    private static Task<StockItemDto> ItemAsync(TestSpa spa, string name, decimal minimum, Guid? articleId = null) =>
        spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput(name, "Stück", 4m, minimum, articleId), Ct);

    private static Task<StoragePlaceDto> SalesAsync(TestSpa spa) =>
        spa.PostAsync<StoragePlaceDto>($"/api/stock/locations/{spa.Location.Id}/storage-places",
            new StoragePlaceInput("Verkaufslager", null, StoragePlaceKind.Sales, null, true, []), Ct);

    private static Task<List<StockMovementDto>> ReceiveAsync(TestSpa spa, StockItemDto item, decimal quantity, Guid? place = null) =>
        spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, quantity, StoragePlaceId: place), Ct);

    private static async Task<ArticleDto> ArticleAsync(TestSpa spa, string name, decimal price)
    {
        _ = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"L-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien", "Test-Spa GmbH", null, null), Ct);
        var accounts = await spa.GetAsync<List<RevenueAccountDto>>("/api/pos/revenue-accounts", Ct);
        return await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput(name, price, TaxCategory.Normal, accounts[0].Id), Ct);
    }

    [Fact]
    public async Task Arganoel_mit_2_Stueck_unter_Mindestbestand_nennt_beides()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await ItemAsync(spa, "Arganöl", 5m);
        var sales = await SalesAsync(spa);
        await ReceiveAsync(spa, oil, 2m, sales.Id);

        var block = await _kit.AnswerAsync(spa, "Haben wir das Arganöl noch?");

        Assert.Equal("Arganöl: 2 Stück im Verkaufslager; gesamt 2 Stück. Unter dem Mindestbestand von 5 Stück.", block.Text);
        Assert.Equal("Lagerbestand des Standorts Wien", block.Source);
    }

    [Fact]
    public async Task Bestand_nennt_jeden_Lagerort_und_die_Summe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await ItemAsync(spa, "Arganöl", 5m);
        var sales = await SalesAsync(spa);
        await ReceiveAsync(spa, oil, 3m);
        await ReceiveAsync(spa, oil, 2m, sales.Id);

        var block = await _kit.AnswerAsync(spa, "Wie viel Arganöl haben wir noch?");

        Assert.Equal("Arganöl: 3 Stück im Hauptlager, 2 Stück im Verkaufslager; gesamt 5 Stück.", block.Text);
    }

    [Fact]
    public async Task Unbekanntes_Material_wird_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ItemAsync(spa, "Arganöl", 5m);

        var block = await _kit.AnswerAsync(spa, "Haben wir das Zauberpulver noch?");

        Assert.Equal(AssistantBlockKind.NotFound, block.Kind);
    }

    [Fact]
    public async Task Unter_Mindestbestand_listet_nur_die_zu_kleinen_Bestaende()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await ItemAsync(spa, "Arganöl", 5m);
        _ = await ItemAsync(spa, "Gesichtscreme", 3m);
        var salt = await ItemAsync(spa, "Meersalz", 5m);
        await ReceiveAsync(spa, oil, 2m);
        await ReceiveAsync(spa, salt, 10m);

        var block = await _kit.AnswerAsync(spa, "Was ist bald aus?");

        Assert.Equal("2 Materialien liegen unter dem Mindestbestand.", block.Text);
        Assert.Equal([["Arganöl", "2 Stück", "5 Stück", "–"], ["Gesichtscreme", "0 Stück", "3 Stück", "–"]], block.Table!.Rows);
    }

    [Fact]
    public async Task Ohne_zu_kleine_Bestaende_sagt_die_Antwort_dass_alles_reicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var salt = await ItemAsync(spa, "Meersalz", 5m);
        await ReceiveAsync(spa, salt, 10m);

        var block = await _kit.AnswerAsync(spa, "Was muss bestellt werden?");

        Assert.Equal("Kein Material liegt unter dem Mindestbestand.", block.Text);
    }

    [Fact]
    public async Task Verkaufspreis_kommt_aus_dem_Artikel()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var article = await ArticleAsync(spa, "Gesichtscreme", 39m);
        await ItemAsync(spa, "Gesichtscreme", 3m, article.Id);

        var block = await _kit.AnswerAsync(spa, "Was kostet die Gesichtscreme?");

        Assert.Equal("Gesichtscreme kostet 39,00 EUR.", block.Text);
    }

    [Fact]
    public async Task Teiltreffer_auf_einen_Artikel_verdraengt_die_Behandlung_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ArticleAsync(spa, "Massageöl", 12m);

        var block = await _kit.AnswerAsync(spa, "Wie lange dauert die Massage?");

        Assert.Contains("Massage kostet 79,00 EUR und dauert 50 Minuten", block.Text, StringComparison.Ordinal);
        Assert.Null(block.Table);
    }

    [Fact]
    public async Task Behandlung_und_Artikel_gleichen_Namens_stehen_beide_in_der_Antwort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ArticleAsync(spa, "Massage", 25m);

        var block = await _kit.AnswerAsync(spa, "Was kostet die Massage?");

        Assert.Equal([["Massage", "Behandlung", "79,00 EUR"], ["Massage", "Artikel", "25,00 EUR"]], block.Table!.Rows);
    }
}
