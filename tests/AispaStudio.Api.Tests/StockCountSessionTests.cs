using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Lager: Zählung eines Lagerorts vom Handy, Abschluss durch die Leitung (Slice 116).</summary>
public sealed class StockCountSessionTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_Lena_zaehlt_den_Raumschrank_2_When_sie_die_Zaehlung_abschickt_Then_sieht_die_Managerin_sie_zum_Abschluss()
    {
        var shop = await ShopAsync();
        using var lena = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [shop.Spa.Location.Id]);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [shop.Spa.Location.Id]);

        var started = await StartAsync(lena, shop);
        await SetAsync(lena, shop, started.Id, shop.Masks, 7m);
        await SetAsync(lena, shop, started.Id, shop.Oil, 400m);
        var submitted = await PostAsync<StockCountSessionDto>(lena, $"{Base(shop)}/count-sessions/{started.Id}/submit");
        var waiting = await manager.GetFromJsonAsync<List<StockCountSessionDto>>($"{Base(shop)}/count-sessions?status=Submitted", TestSpa.Json, Ct);

        var session = Assert.Single(waiting!);
        Assert.Equal((started.Id, StockCountSessionStatus.Submitted, shop.Cabinet.Name), (session.Id, session.Status, session.StoragePlaceName));
        Assert.Equal([("Massageöl", 400m, 500m), ("Tuchmaske", 7m, 10m)], session.Lines.Select(l => (l.Name, l.Counted, l.Expected!.Value)).ToList());
        Assert.All(submitted.Lines, l => Assert.Null(l.Expected));
    }

    [Fact]
    public async Task Given_eine_abgeschickte_Zaehlung_When_die_Leitung_sie_schliesst_Then_steht_der_gezaehlte_Bestand_und_die_Bewegung_ist_eine_Inventur()
    {
        var shop = await ShopAsync();
        var submitted = await SubmittedAsync(shop);

        var closed = await PostAsync<StockCountSessionDto>(shop.Spa.Client, $"{Base(shop)}/count-sessions/{submitted.Id}/close");
        var levels = await shop.Spa.GetAsync<StoragePlaceLevelsDto>($"{Base(shop)}/storage-places/{shop.Cabinet.Id}/levels", Ct);
        var counts = (await Movements(shop)).Where(m => m.Kind == StockMovementKind.Count).ToList();

        Assert.Equal(StockCountSessionStatus.Closed, closed.Status);
        Assert.Equal(7m, levels.Items.Single(l => l.ItemId == shop.Masks).Quantity);
        Assert.Equal(500m, levels.Items.Single(l => l.ItemId == shop.Oil).Quantity);
        var count = Assert.Single(counts);
        Assert.Equal((shop.Masks, -3m, shop.Cabinet.Id), (count.ItemId, count.Quantity, count.StoragePlaceId));
    }

    [Fact]
    public async Task Given_eine_abgeschickte_Zaehlung_When_Lena_close_ruft_Then_403()
    {
        var shop = await ShopAsync();
        var submitted = await SubmittedAsync(shop);
        using var lena = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [shop.Spa.Location.Id]);

        var response = await lena.PostAsync($"{Base(shop)}/count-sessions/{submitted.Id}/close", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(await Movements(shop), m => m.Kind == StockMovementKind.Count);
    }

    [Fact]
    public async Task Given_eine_abgeschickte_Zaehlung_When_close_zweimal_laeuft_Then_wird_nur_einmal_gebucht()
    {
        var shop = await ShopAsync();
        var submitted = await SubmittedAsync(shop);

        var first = await shop.Spa.Client.PostAsync($"{Base(shop)}/count-sessions/{submitted.Id}/close", null, Ct);
        var second = await shop.Spa.Client.PostAsync($"{Base(shop)}/count-sessions/{submitted.Id}/close", null, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Single(await Movements(shop), m => m.Kind == StockMovementKind.Count);
    }

    [Fact]
    public async Task Given_eine_abgeschickte_Zaehlung_When_zwei_Leitungen_gleichzeitig_schliessen_Then_wird_nur_einmal_gebucht()
    {
        var shop = await ShopAsync();
        var submitted = await SubmittedAsync(shop);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [shop.Spa.Location.Id]);
        var url = $"{Base(shop)}/count-sessions/{submitted.Id}/close";

        var responses = await Task.WhenAll(shop.Spa.Client.PostAsync(url, null, Ct), manager.PostAsync(url, null, Ct));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Single(await Movements(shop), m => m.Kind == StockMovementKind.Count);
    }

    [Fact]
    public async Task Given_eine_offene_Zaehlung_When_ein_zweites_Handy_startet_Then_bekommt_es_dieselbe_Zaehlung()
    {
        var shop = await ShopAsync();
        using var lena = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [shop.Spa.Location.Id]);
        using var other = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [shop.Spa.Location.Id]);
        var first = await StartAsync(lena, shop);
        await SetAsync(lena, shop, first.Id, shop.Masks, 7m);

        var second = await StartAsync(other, shop);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(7m, Assert.Single(second.Lines).Counted);
    }

    [Fact]
    public async Task Given_eine_abgeschickte_Zaehlung_When_Lena_eine_Zeile_aendert_Then_400()
    {
        var shop = await ShopAsync();
        var submitted = await SubmittedAsync(shop);

        var response = await shop.Spa.Client.PutAsJsonAsync($"{Base(shop)}/count-sessions/{submitted.Id}/lines/{shop.Masks}", new StockCountLineInput(9m), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Given_keine_gezaehlte_Zeile_When_abgeschickt_Then_400()
    {
        var shop = await ShopAsync();
        var started = await StartAsync(shop.Spa.Client, shop);

        var response = await shop.Spa.Client.PostAsync($"{Base(shop)}/count-sessions/{started.Id}/submit", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2.0001")]
    [InlineData("1000000")]
    public async Task Given_eine_ungueltige_Menge_When_die_Zeile_gespeichert_wird_Then_400_mit_Feldmeldung(string counted)
    {
        var shop = await ShopAsync();
        var started = await StartAsync(shop.Spa.Client, shop);
        using var body = new StringContent($"{{\"counted\":{counted}}}", global::System.Text.Encoding.UTF8, "application/json");

        var response = await shop.Spa.Client.PutAsync($"{Base(shop)}/count-sessions/{started.Id}/lines/{shop.Masks}", body, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("counted", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_Lagerort_eines_anderen_Standorts_When_die_Zaehlung_startet_Then_400()
    {
        var shop = await ShopAsync();
        var other = await shop.Spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var otherPlace = (await shop.Spa.GetAsync<List<StoragePlaceDto>>($"/api/stock/locations/{other.Id}/storage-places", Ct)).Single(p => p.Kind == StoragePlaceKind.Main);

        var response = await shop.Spa.Client.PostAsync($"{Base(shop)}/storage-places/{otherPlace.Id}/count-sessions", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Given_an_einem_Lagerort_des_zweiten_Standorts_laeuft_eine_Zaehlung_When_der_erste_Standort_sie_startet_Then_400_und_nichts_wird_herausgegeben()
    {
        var shop = await ShopAsync();
        var other = await shop.Spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var otherPlace = (await shop.Spa.GetAsync<List<StoragePlaceDto>>($"/api/stock/locations/{other.Id}/storage-places", Ct)).Single(p => p.Kind == StoragePlaceKind.Main);
        var running = await shop.Spa.Client.PostAsync($"/api/stock/locations/{other.Id}/storage-places/{otherPlace.Id}/count-sessions", null, Ct);

        var response = await shop.Spa.Client.PostAsync($"{Base(shop)}/storage-places/{otherPlace.Id}/count-sessions", null, Ct);

        Assert.Equal(HttpStatusCode.OK, running.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Given_Lena_in_der_Mitarbeiter_App_When_sie_Material_sucht_zaehlt_und_abschickt_Then_lassen_die_Kanalregeln_alle_Aufrufe_durch()
    {
        var shop = await ShopAsync();
        var lena = await shop.Spa.CreateEmployeeAsync("Lena", [shop.Spa.Massage.Id], shop.Spa.Location.Id, Ct);
        using var app = await ChannelTests.StaffAppAsync(shop.Spa, StaffRole.Therapist, lena.Id, Ct);

        var items = await app.GetAsync("/api/stock/items", Ct);
        var places = await app.GetAsync($"{Base(shop)}/storage-places", Ct);
        var started = await StartAsync(app, shop);
        await SetAsync(app, shop, started.Id, shop.Masks, 7m);
        var submitted = await app.PostAsync($"{Base(shop)}/count-sessions/{started.Id}/submit", null, Ct);

        Assert.Equal(HttpStatusCode.OK, items.StatusCode);
        Assert.Equal(HttpStatusCode.OK, places.StatusCode);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
    }

    [Fact]
    public async Task Given_eine_verworfene_Zaehlung_When_sie_geschlossen_werden_soll_Then_400()
    {
        var shop = await ShopAsync();
        var submitted = await SubmittedAsync(shop);

        var discarded = await PostAsync<StockCountSessionDto>(shop.Spa.Client, $"{Base(shop)}/count-sessions/{submitted.Id}/discard");
        var response = await shop.Spa.Client.PostAsync($"{Base(shop)}/count-sessions/{submitted.Id}/close", null, Ct);

        Assert.Equal(StockCountSessionStatus.Discarded, discarded.Status);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(await Movements(shop), m => m.Kind == StockMovementKind.Count);
    }

    private sealed record Shop(TestSpa Spa, StoragePlaceDto Cabinet, Guid Masks, Guid Oil);

    private static string Base(Shop shop) => $"/api/stock/locations/{shop.Spa.Location.Id}";

    /// <summary>Raumschrank 2 mit 10 Tuchmasken und 500 ml Öl.</summary>
    private async Task<Shop> ShopAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var cabinet = await spa.PostAsync<StoragePlaceDto>($"/api/stock/locations/{spa.Location.Id}/storage-places",
            new StoragePlaceInput("Raumschrank 2", null, StoragePlaceKind.Room, spa.RoomB.Id), Ct);
        var masks = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Tuchmaske", "Stück", 2m, 0m, null), Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.05m, 0m, null), Ct);
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
        var started = await StartAsync(lena, shop);
        await SetAsync(lena, shop, started.Id, shop.Masks, 7m);
        await SetAsync(lena, shop, started.Id, shop.Oil, 500m);
        return await PostAsync<StockCountSessionDto>(lena, $"{Base(shop)}/count-sessions/{started.Id}/submit");
    }

    private static Task<StockCountSessionDto> StartAsync(HttpClient client, Shop shop) =>
        PostAsync<StockCountSessionDto>(client, $"{Base(shop)}/storage-places/{shop.Cabinet.Id}/count-sessions");

    private static async Task SetAsync(HttpClient client, Shop shop, Guid sessionId, Guid itemId, decimal counted)
    {
        var response = await client.PutAsJsonAsync($"{Base(shop)}/count-sessions/{sessionId}/lines/{itemId}", new StockCountLineInput(counted), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url) =>
        await TestSpa.ReadAsync<T>(await client.PostAsync(url, null, Ct), Ct);

    private static async Task<List<StockMovementDto>> Movements(Shop shop)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await shop.Spa.GetAsync<List<StockMovementDto>>(
            $"{Base(shop)}/movements?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}", Ct);
    }
}
