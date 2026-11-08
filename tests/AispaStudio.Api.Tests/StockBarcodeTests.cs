using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Lager: Barcode am Material, Suche per Code im Lager und an der Kassa (Slice 114).</summary>
public sealed class StockBarcodeTests(ApiFixture fixture)
{
    private const string Ean = "9001234567890";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_ein_Material_mit_EAN_9001234567890_und_Kassa_Artikel_When_der_Code_an_der_Kassa_gesucht_wird_Then_liefert_die_API_den_Artikel()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var article = await CreateArticleAsync(spa, "Körperlotion");
        await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Körperlotion", article.Id, Ean), Ct);

        var found = await spa.GetAsync<ArticleDto>($"/api/pos/articles/by-barcode?code={Ean}", Ct);

        Assert.Equal(article.Id, found.Id);
    }

    [Fact]
    public async Task Given_ein_Material_mit_Code_When_der_Code_im_Lager_gesucht_wird_Then_liefert_die_API_das_Material_mit_Code()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var created = await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Massageöl", null, Ean), Ct);

        var found = await spa.GetAsync<StockItemDto>($"/api/stock/items/by-barcode?code={Ean}", Ct);

        Assert.Equal(created.Id, found.Id);
        Assert.Equal(Ean, found.Barcode);
    }

    [Fact]
    public async Task Given_zwei_Materialien_When_das_zweite_denselben_Code_bekommt_Then_lehnt_die_API_ab_und_nennt_das_erste_Material()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Körperlotion", null, Ean), Ct);

        var created = await spa.Client.PostAsJsonAsync("/api/stock/items", Input("Handcreme", null, Ean), TestSpa.Json, Ct);
        var other = await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Fußcreme", null, "4006381333931"), Ct);
        var changed = await spa.Client.PutAsJsonAsync($"/api/stock/items/{other.Id}", Input("Fußcreme", null, Ean), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains("Körperlotion", await created.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
    }

    [Fact]
    public async Task Given_ein_Material_mit_Code_When_es_mit_demselben_Code_gespeichert_wird_Then_geht_es()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Körperlotion", null, Ean), Ct);

        var saved = await spa.PutAsync<StockItemDto>($"/api/stock/items/{item.Id}", Input("Körperlotion neu", null, Ean), Ct);

        Assert.Equal(Ean, saved.Barcode);
    }

    [Fact]
    public async Task Given_ein_unbekannter_Code_When_er_gesucht_wird_Then_antwortet_die_API_mit_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var stock = await spa.Client.GetAsync("/api/stock/items/by-barcode?code=123456", Ct);
        var pos = await spa.Client.GetAsync("/api/pos/articles/by-barcode?code=123456", Ct);

        Assert.Equal(HttpStatusCode.NotFound, stock.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pos.StatusCode);
    }

    [Fact]
    public async Task Given_ein_deaktiviertes_Material_When_sein_Code_gesucht_wird_Then_wird_es_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var article = await CreateArticleAsync(spa, "Körperlotion");
        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Körperlotion", article.Id, Ean), Ct);
        await spa.PutAsync<StockItemDto>($"/api/stock/items/{item.Id}", Input("Körperlotion", article.Id, Ean) with { IsActive = false }, Ct);

        var stock = await spa.Client.GetAsync($"/api/stock/items/by-barcode?code={Ean}", Ct);
        var pos = await spa.Client.GetAsync($"/api/pos/articles/by-barcode?code={Ean}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, stock.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pos.StatusCode);
    }

    [Fact]
    public async Task Given_derselbe_Code_in_zwei_Mandanten_When_beide_ihn_speichern_Then_geht_es_und_jeder_findet_nur_sein_Material()
    {
        var first = await TestSpa.CreateAsync(fixture, Ct);
        var second = await TestSpa.CreateAsync(fixture, Ct);

        var a = await first.PostAsync<StockItemDto>("/api/stock/items", Input("Körperlotion", null, Ean), Ct);
        var b = await second.PostAsync<StockItemDto>("/api/stock/items", Input("Handcreme", null, Ean), Ct);

        Assert.Equal(a.Id, (await first.GetAsync<StockItemDto>($"/api/stock/items/by-barcode?code={Ean}", Ct)).Id);
        Assert.Equal(b.Id, (await second.GetAsync<StockItemDto>($"/api/stock/items/by-barcode?code={Ean}", Ct)).Id);
    }

    [Fact]
    public async Task Given_viele_gleichzeitige_Speicherungen_mit_demselben_Code_When_sie_laufen_Then_gewinnt_genau_eine_und_keine_endet_im_Serverfehler()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => spa.Client.PostAsJsonAsync("/api/stock/items", Input($"Körperlotion {i}", null, Ean), TestSpa.Json, Ct)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
    }

    [Theory]
    [InlineData("12 34")]
    [InlineData("12")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public async Task Given_ein_ungueltiger_Code_When_gespeichert_Then_lehnt_die_API_ab(string code)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/stock/items", Input("Körperlotion", null, code), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Given_ein_Code_mit_Randleerzeichen_When_gespeichert_Then_steht_er_getrimmt_und_ein_leerer_Code_entfernt_ihn()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Körperlotion", null, $"  {Ean} "), Ct);
        var cleared = await spa.PutAsync<StockItemDto>($"/api/stock/items/{item.Id}", Input("Körperlotion", null, "  "), Ct);

        Assert.Equal(Ean, item.Barcode);
        Assert.Null(cleared.Barcode);
    }

    [Fact]
    public async Task Given_die_Rezeption_When_sie_den_Code_an_der_Kassa_sucht_Then_findet_sie_den_Artikel()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var article = await CreateArticleAsync(spa, "Körperlotion");
        await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Körperlotion", article.Id, Ean), Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var found = await reception.GetFromJsonAsync<ArticleDto>($"/api/pos/articles/by-barcode?code={Ean}", TestSpa.Json, Ct);

        Assert.Equal(article.Id, found!.Id);
    }

    [Fact]
    public async Task Given_ein_Material_ohne_Kassa_Artikel_When_der_Code_an_der_Kassa_gesucht_wird_Then_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StockItemDto>("/api/stock/items", Input("Massageöl", null, Ean), Ct);

        var response = await spa.Client.GetAsync($"/api/pos/articles/by-barcode?code={Ean}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static StockItemInput Input(string name, Guid? articleId, string? barcode) =>
        new(name, "Stück", 2m, 0m, articleId, Barcode: barcode);

    private static async Task<ArticleDto> CreateArticleAsync(TestSpa spa, string name)
    {
        var accounts = await spa.GetAsync<List<RevenueAccountDto>>("/api/pos/revenue-accounts", Ct);
        return await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput(name, 29m, TaxCategory.Normal, accounts[0].Id), Ct);
    }
}
