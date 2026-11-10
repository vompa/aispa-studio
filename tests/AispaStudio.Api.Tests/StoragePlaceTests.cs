using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Lagerorte je Standort (ADR 0068): Hauptlager mit dem Standort, höchstens zehn, Bestand und Chargen je Lagerort, Nachfüllen, Verbrauch aus dem
/// Raumschrank mit Rückfall auf das Hauptlager, Verkauf vom Verkaufsregal und Inventur je Lagerort.
/// </summary>
public sealed class StoragePlaceTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(TestSpa spa) => $"/api/stock/locations/{spa.Location.Id}";

    private static Task<List<StoragePlaceDto>> PlacesAsync(TestSpa spa, Guid? locationId = null) =>
        spa.GetAsync<List<StoragePlaceDto>>($"/api/stock/locations/{locationId ?? spa.Location.Id}/storage-places", Ct);

    private static async Task<StoragePlaceDto> MainAsync(TestSpa spa, Guid? locationId = null) =>
        (await PlacesAsync(spa, locationId)).Single(p => p.Kind == StoragePlaceKind.Main);

    private static StoragePlaceInput Input(string name, StoragePlaceKind kind = StoragePlaceKind.Other, Guid? roomId = null, bool active = true,
        IReadOnlyList<StoragePlaceMinimumDto>? minimums = null) =>
        new(name, null, kind, roomId, active, minimums);

    private static Task<StoragePlaceDto> CreateAsync(TestSpa spa, string name, StoragePlaceKind kind = StoragePlaceKind.Other, Guid? roomId = null,
        IReadOnlyList<StoragePlaceMinimumDto>? minimums = null) =>
        spa.PostAsync<StoragePlaceDto>($"{Url(spa)}/storage-places", Input(name, kind, roomId, true, minimums), Ct);

    private static Task<List<StockMovementDto>> MoveAsync(TestSpa spa, StockMovementInput input) =>
        spa.PostAsync<List<StockMovementDto>>($"{Url(spa)}/movements", input, Ct);

    private static Task<StockItemDto> ItemAsync(TestSpa spa, string name, decimal minimum = 0, Guid? articleId = null) =>
        spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput(name, "Stück", 2m, minimum, articleId), Ct);

    private static async Task<decimal> QuantityAsync(TestSpa spa, Guid placeId, Guid itemId, Guid? locationId = null) =>
        (await spa.GetAsync<StoragePlaceLevelsDto>($"/api/stock/locations/{locationId ?? spa.Location.Id}/storage-places/{placeId}/levels", Ct)).Items
            .SingleOrDefault(i => i.ItemId == itemId)?.Quantity ?? 0;

    private static async Task<decimal> WaitForQuantityAsync(TestSpa spa, Guid placeId, Guid itemId, decimal expected)
    {
        for (var i = 0; ; i++)
        {
            var quantity = await QuantityAsync(spa, placeId, itemId);
            if (quantity == expected || i == 100)
            {
                return quantity;
            }

            await Task.Delay(100, Ct);
        }
    }

    private static async Task<List<StockMovementDto>> RecentMovementsAsync(TestSpa spa)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await spa.GetAsync<List<StockMovementDto>>($"{Url(spa)}/movements?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}", Ct);
    }

    /// <summary>Schließt den Termin ab und wartet, bis die Queue den Verbrauch gebucht hat.</summary>
    private static async Task<List<StockMovementDto>> CompleteAsync(TestSpa spa, AppointmentDto appointment)
    {
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);
        for (var i = 0; ; i++)
        {
            var movements = (await RecentMovementsAsync(spa)).Where(m => m.AppointmentId == appointment.Id).ToList();
            if (movements.Count > 0 || i == 100)
            {
                return movements;
            }

            await Task.Delay(100, Ct);
        }
    }

    [Fact]
    public async Task Hauptlager_entsteht_mit_dem_Standort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);

        var here = await PlacesAsync(spa);
        var there = await PlacesAsync(spa, other.Id);

        var main = Assert.Single(here);
        Assert.Equal(("Hauptlager", StoragePlaceKind.Main, 0, true, spa.Location.Id), (main.Name, main.Kind, main.Position, main.IsActive, main.LocationId));
        Assert.Equal(other.Id, Assert.Single(there).LocationId);
        Assert.NotEqual(main.Id, there[0].Id);
    }

    [Fact]
    public async Task Bestand_am_Lagerort_nennt_die_Behandlungen_laut_Richtwert_mit_der_Menge_dieses_Orts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var main = await MainAsync(spa);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.05m, 0m, null, TreatmentDose: 15m), Ct);
        var towel = await ItemAsync(spa, "Handtuch");
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 100m, StoragePlaceId: main.Id));
        await MoveAsync(spa, new StockMovementInput(towel.Id, StockMovementKind.GoodsReceipt, 100m, StoragePlaceId: main.Id));

        var levels = await spa.GetAsync<StoragePlaceLevelsDto>($"{Url(spa)}/storage-places/{main.Id}/levels", Ct);

        Assert.Equal(6, levels.Items.Single(i => i.ItemId == oil.Id).TreatmentsCovered);
        Assert.Null(levels.Items.Single(i => i.ItemId == towel.Id).TreatmentsCovered);
    }

    [Fact]
    public async Task Hoechstens_zehn_Lagerorte_je_Standort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        for (var i = 1; i <= 8; i++)
        {
            await CreateAsync(spa, $"Regal {i}");
        }

        var duplicate = await spa.Client.PostAsJsonAsync($"{Url(spa)}/storage-places", Input("regal 1"), TestSpa.Json, Ct);
        await CreateAsync(spa, "Regal 9");
        var eleventh = await spa.Client.PostAsJsonAsync($"{Url(spa)}/storage-places", Input("Regal 10"), TestSpa.Json, Ct);
        var second = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var elsewhere = await spa.PostAsync<StoragePlaceDto>($"/api/stock/locations/{second.Id}/storage-places", Input("Regal 1"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, eleventh.StatusCode);
        var problem = await eleventh.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct);
        Assert.Equal("Höchstens 10 Lagerorte je Standort.", problem!.Errors["name"].Single());
        Assert.Equal(10, (await PlacesAsync(spa)).Count);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(second.Id, elsewhere.LocationId);
    }

    [Fact]
    public async Task Hauptlager_laesst_sich_umbenennen_aber_nicht_loeschen_oder_deaktivieren()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var main = await MainAsync(spa);
        var url = $"{Url(spa)}/storage-places/{main.Id}";

        var delete = await spa.Client.DeleteAsync(url, Ct);
        var deactivate = await spa.Client.PutAsJsonAsync(url, Input("Hauptlager", StoragePlaceKind.Main, active: false), TestSpa.Json, Ct);
        var otherKind = await spa.Client.PutAsJsonAsync(url, Input("Hauptlager", StoragePlaceKind.Other), TestSpa.Json, Ct);
        var second = await spa.Client.PostAsJsonAsync($"{Url(spa)}/storage-places", Input("Noch eines", StoragePlaceKind.Main), TestSpa.Json, Ct);
        var renamed = await spa.PutAsync<StoragePlaceDto>(url,
            new StoragePlaceInput("Lager im Keller", "Schlüssel bei der Rezeption", StoragePlaceKind.Main, null), Ct);
        var shelf = await CreateAsync(spa, "Regal");
        var deleteShelf = await spa.Client.DeleteAsync($"{Url(spa)}/storage-places/{shelf.Id}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, otherKind.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(("Lager im Keller", "Schlüssel bei der Rezeption"), (renamed.Name, renamed.Description));
        Assert.Equal(HttpStatusCode.NoContent, deleteShelf.StatusCode);
        Assert.Equal(main.Id, Assert.Single(await PlacesAsync(spa)).Id);
    }

    [Fact]
    public async Task Raum_nur_bei_der_Art_Raum_und_nur_aus_demselben_Standort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var foreignRoom = await spa.PostAsync<RoomDto>($"/api/locations/{other.Id}/rooms", new RoomInput("Fremder Raum", 0), Ct);
        var url = $"{Url(spa)}/storage-places";

        var cabinet = await CreateAsync(spa, "Schrank A", StoragePlaceKind.Room, spa.RoomA.Id);
        var onShelf = await spa.Client.PostAsJsonAsync(url, Input("Regal", StoragePlaceKind.Other, spa.RoomB.Id), TestSpa.Json, Ct);
        var foreign = await spa.Client.PostAsJsonAsync(url, Input("Schrank fremd", StoragePlaceKind.Room, foreignRoom.Id), TestSpa.Json, Ct);
        var again = await spa.Client.PostAsJsonAsync(url, Input("Schrank A 2", StoragePlaceKind.Room, spa.RoomA.Id), TestSpa.Json, Ct);
        var long60 = await spa.Client.PostAsJsonAsync(url, Input(new string('x', 61)), TestSpa.Json, Ct);
        var longText = await spa.Client.PostAsJsonAsync(url, new StoragePlaceInput("Regal", new string('x', 161), StoragePlaceKind.Other, null), TestSpa.Json, Ct);

        Assert.Equal(spa.RoomA.Id, cabinet.RoomId);
        Assert.Equal(HttpStatusCode.BadRequest, onShelf.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, long60.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longText.StatusCode);
    }

    [Fact]
    public async Task Lagerort_eines_anderen_Standorts_wird_in_Bewegung_und_Pflege_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var foreignMain = await MainAsync(spa, other.Id);
        var item = await ItemAsync(spa, "Tuchmaske");
        await MoveAsync(spa, new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, 10m));

        var count = await spa.Client.PostAsJsonAsync($"{Url(spa)}/movements",
            new StockMovementInput(item.Id, StockMovementKind.Count, 5m, StoragePlaceId: foreignMain.Id), TestSpa.Json, Ct);
        var receipt = await spa.Client.PostAsJsonAsync($"{Url(spa)}/movements",
            new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, 5m, StoragePlaceId: foreignMain.Id), TestSpa.Json, Ct);
        var refill = await spa.Client.PostAsJsonAsync($"{Url(spa)}/movements",
            new StockMovementInput(item.Id, StockMovementKind.Refill, 1m, StoragePlaceId: (await MainAsync(spa)).Id, TargetStoragePlaceId: foreignMain.Id),
            TestSpa.Json, Ct);
        var update = await spa.Client.PutAsJsonAsync($"{Url(spa)}/storage-places/{foreignMain.Id}", Input("Hauptlager", StoragePlaceKind.Main), TestSpa.Json, Ct);
        var levels = await spa.Client.GetAsync($"{Url(spa)}/storage-places/{foreignMain.Id}/levels", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, count.StatusCode);
        var problem = await count.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct);
        Assert.Equal("Der Lagerort gehört nicht zu diesem Standort.", problem!.Errors["storagePlaceId"].Single());
        Assert.Equal(HttpStatusCode.BadRequest, receipt.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refill.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, levels.StatusCode);
        Assert.Equal(0m, await QuantityAsync(spa, foreignMain.Id, item.Id, other.Id));
        Assert.Equal(10m, await QuantityAsync(spa, (await MainAsync(spa)).Id, item.Id));
    }

    [Fact]
    public async Task Nachfuellen_bucht_zwei_Bewegungen_und_die_Charge_wandert_mit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var main = await MainAsync(spa);
        var cabinet = await CreateAsync(spa, "Schrank Raum A", StoragePlaceKind.Room, spa.RoomA.Id);
        var cream = await ItemAsync(spa, "Gesichtscreme");
        var expires = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90);
        await MoveAsync(spa, new StockMovementInput(cream.Id, StockMovementKind.GoodsReceipt, 10m, Batch: "A1", ExpiresOn: expires));
        var before = await spa.GetAsync<StockOverviewDto>(Url(spa), Ct);

        var moves = await MoveAsync(spa, new StockMovementInput(cream.Id, StockMovementKind.Refill, 4m, StoragePlaceId: main.Id, TargetStoragePlaceId: cabinet.Id));
        var after = await spa.GetAsync<StockOverviewDto>(Url(spa), Ct);
        var batches = await spa.GetAsync<StockBatchesDto>($"{Url(spa)}/batches", Ct);
        var cabinetLevels = await spa.GetAsync<StoragePlaceLevelsDto>($"{Url(spa)}/storage-places/{cabinet.Id}/levels", Ct);
        var sameTarget = await spa.Client.PostAsJsonAsync($"{Url(spa)}/movements",
            new StockMovementInput(cream.Id, StockMovementKind.Refill, 1m, StoragePlaceId: main.Id, TargetStoragePlaceId: main.Id), TestSpa.Json, Ct);
        var noTarget = await spa.Client.PostAsJsonAsync($"{Url(spa)}/movements",
            new StockMovementInput(cream.Id, StockMovementKind.Refill, 1m, StoragePlaceId: main.Id), TestSpa.Json, Ct);
        var zero = await spa.Client.PostAsJsonAsync($"{Url(spa)}/movements",
            new StockMovementInput(cream.Id, StockMovementKind.Refill, 0m, StoragePlaceId: main.Id, TargetStoragePlaceId: cabinet.Id), TestSpa.Json, Ct);

        Assert.Equal([(main.Id, -4m, "A1"), (cabinet.Id, 4m, "A1")], moves.Select(m => (m.StoragePlaceId, m.Quantity, m.Batch)));
        Assert.All(moves, m => Assert.Equal((StockMovementKind.Refill, expires), (m.Kind, m.ExpiresOn)));
        Assert.Equal(["Hauptlager", "Schrank Raum A"], moves.Select(m => m.StoragePlaceName));
        Assert.Equal([("A1", main.Id, 6m), ("A1", cabinet.Id, 4m)],
            batches.Batches.Select(b => (b.Batch, b.StoragePlaceId, b.Quantity)).OrderByDescending(b => b.Item3));
        var level = Assert.Single(cabinetLevels.Items);
        Assert.Equal((4m, expires), (level.Quantity, level.NextExpiry));
        Assert.Equal("A1", Assert.Single(level.Batches).Batch);
        Assert.Equal(before.Items.Single().Quantity, after.Items.Single().Quantity);
        Assert.Equal(before.TotalValue, after.TotalValue);
        Assert.Equal(HttpStatusCode.BadRequest, sameTarget.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noTarget.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
    }

    [Fact]
    public async Task Uebersicht_liefert_Places_und_die_Standortsumme_unveraendert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var main = await MainAsync(spa);
        var shelf = await CreateAsync(spa, "Regal");
        var salt = await ItemAsync(spa, "Meersalz", minimum: 10);
        await MoveAsync(spa, new StockMovementInput(salt.Id, StockMovementKind.GoodsReceipt, 20m, 1m));
        await MoveAsync(spa, new StockMovementInput(salt.Id, StockMovementKind.Refill, 5m, StoragePlaceId: main.Id, TargetStoragePlaceId: shelf.Id));

        var overview = await spa.GetAsync<StockOverviewDto>(Url(spa), Ct);

        var level = Assert.Single(overview.Items);
        Assert.Equal((20m, false, 20m), (level.Quantity, level.BelowMinimum, overview.TotalValue));
        Assert.Equal([(main.Id, 15m), (shelf.Id, 5m)], level.Places!.Select(p => (p.StoragePlaceId, p.Quantity)));
    }

    [Fact]
    public async Task Verbrauch_kommt_aus_dem_Raumschrank_dann_aus_dem_Hauptlager_und_sonst_ins_Minus_mit_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var main = await MainAsync(spa);
        var cabinet = await CreateAsync(spa, "Schrank Raum A", StoragePlaceKind.Room, spa.RoomA.Id);
        var oil = await ItemAsync(spa, "Massageöl", minimum: 500);
        await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials", new[] { new MaterialLine("", 30m, "", 0, oil.Id) }, Ct);
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 1000m, 0.05m));
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.Refill, 100m, StoragePlaceId: main.Id, TargetStoragePlaceId: cabinet.Id));

        // Reicht im Raum: der Raumschrank trägt den Verbrauch.
        var first = await CompleteAsync(spa, await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id));
        var mainAfterFirst = await QuantityAsync(spa, main.Id, oil.Id);
        var cabinetAfterFirst = await QuantityAsync(spa, cabinet.Id, oil.Id);

        // Reicht nur im Hauptlager: im Schrank sind nur noch 10 ml, also bucht das Hauptlager.
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.Count, 10m, StoragePlaceId: cabinet.Id));
        var second = await CompleteAsync(spa, await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Anna.Id, spa.RoomA.Id));

        // Reicht nirgends: beide unter 30 ml, das Hauptlager geht ins Minus und der Grund sagt es.
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.Count, 5m, StoragePlaceId: main.Id));
        var third = await CompleteAsync(spa, await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id, spa.RoomA.Id));
        var overview = await spa.GetAsync<StockOverviewDto>(Url(spa), Ct);

        var firstMove = Assert.Single(first);
        Assert.Equal((cabinet.Id, -30m, "Behandlung abgeschlossen"), (firstMove.StoragePlaceId, firstMove.Quantity, firstMove.Reason));
        Assert.Equal((900m, 70m), (mainAfterFirst, cabinetAfterFirst));
        var secondMove = Assert.Single(second);
        Assert.Equal((main.Id, -30m, "Behandlung abgeschlossen"), (secondMove.StoragePlaceId, secondMove.Quantity, secondMove.Reason));
        Assert.Equal(10m, await QuantityAsync(spa, cabinet.Id, oil.Id));
        var thirdMove = Assert.Single(third);
        Assert.Equal((main.Id, -30m), (thirdMove.StoragePlaceId, thirdMove.Quantity));
        Assert.Contains("Bestand reichte nicht", thirdMove.Reason, StringComparison.Ordinal);
        Assert.Equal(-25m, await QuantityAsync(spa, main.Id, oil.Id));
        Assert.Equal((-15m, true), (overview.Items.Single().Quantity, overview.Items.Single().BelowMinimum));
    }

    [Fact]
    public async Task Verbrauch_ohne_Raumschrank_kommt_aus_dem_Hauptlager()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var main = await MainAsync(spa);
        var oil = await ItemAsync(spa, "Massageöl");
        await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials", new[] { new MaterialLine("", 30m, "", 0, oil.Id) }, Ct);
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 100m));

        var moves = await CompleteAsync(spa, await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id));

        Assert.Equal((main.Id, -30m), (Assert.Single(moves).StoragePlaceId, Assert.Single(moves).Quantity));
    }

    [Fact]
    public async Task Verkauf_geht_vom_Verkaufsregal_sonst_vom_Hauptlager_und_Storno_an_denselben_Ort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var main = await MainAsync(spa);
        var shelf = await CreateAsync(spa, "Verkaufsregal Rezeption", StoragePlaceKind.Sales);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"L-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien", "Test-Spa GmbH", null, null), Ct);
        var accounts = await spa.GetAsync<List<RevenueAccountDto>>("/api/pos/revenue-accounts", Ct);
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Körperlotion", 29m, TaxCategory.Normal, accounts[0].Id), Ct);
        var lotion = await ItemAsync(spa, "Körperlotion", articleId: article.Id);
        await MoveAsync(spa, new StockMovementInput(lotion.Id, StockMovementKind.GoodsReceipt, 10m));
        await MoveAsync(spa, new StockMovementInput(lotion.Id, StockMovementKind.Refill, 5m, StoragePlaceId: main.Id, TargetStoragePlaceId: shelf.Id));

        var receipt = await SellAsync(spa, register, article, 2);
        var shelfAfterSale = await WaitForQuantityAsync(spa, shelf.Id, lotion.Id, 3m);
        var mainAfterSale = await QuantityAsync(spa, main.Id, lotion.Id);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Retour"), Ct);
        var shelfAfterVoid = await WaitForQuantityAsync(spa, shelf.Id, lotion.Id, 5m);

        // Ohne aktives Verkaufsregal kommt der Verkauf aus dem Hauptlager.
        await spa.PutAsync<StoragePlaceDto>($"{Url(spa)}/storage-places/{shelf.Id}", Input("Verkaufsregal Rezeption", StoragePlaceKind.Sales, active: false), Ct);
        await SellAsync(spa, register, article, 1);
        var mainAfterSecondSale = await WaitForQuantityAsync(spa, main.Id, lotion.Id, 4m);

        Assert.Equal((3m, 5m), (shelfAfterSale, mainAfterSale));
        Assert.Equal(5m, shelfAfterVoid);
        Assert.Equal(4m, mainAfterSecondSale);
        Assert.Equal(5m, await QuantityAsync(spa, shelf.Id, lotion.Id));
    }

    [Fact]
    public async Task Inventur_je_Lagerort_bucht_die_Differenz_nur_dort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var main = await MainAsync(spa);
        var shelf = await CreateAsync(spa, "Regal");
        var masks = await ItemAsync(spa, "Tuchmaske");
        await MoveAsync(spa, new StockMovementInput(masks.Id, StockMovementKind.GoodsReceipt, 20m));
        await MoveAsync(spa, new StockMovementInput(masks.Id, StockMovementKind.Refill, 5m, StoragePlaceId: main.Id, TargetStoragePlaceId: shelf.Id));

        var counted = await MoveAsync(spa, new StockMovementInput(masks.Id, StockMovementKind.Count, 3m, StoragePlaceId: shelf.Id));
        var unchanged = await MoveAsync(spa, new StockMovementInput(masks.Id, StockMovementKind.Count, 15m, StoragePlaceId: main.Id));

        var move = Assert.Single(counted);
        Assert.Equal((shelf.Id, -2m, StockMovementKind.Count), (move.StoragePlaceId, move.Quantity, move.Kind));
        Assert.Empty(unchanged);
        Assert.Equal((15m, 3m), (await QuantityAsync(spa, main.Id, masks.Id), await QuantityAsync(spa, shelf.Id, masks.Id)));
    }

    [Fact]
    public async Task Umbuchung_zwischen_Standorten_landet_im_Hauptlager_des_Ziels_und_Mindestmengen_gelten_je_Lagerort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var main = await MainAsync(spa);
        var otherMain = await MainAsync(spa, other.Id);
        var oil = await ItemAsync(spa, "Massageöl");
        var cabinet = await CreateAsync(spa, "Schrank Raum A", StoragePlaceKind.Room, spa.RoomA.Id, [new StoragePlaceMinimumDto(oil.Id, 100m)]);
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 300m));
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.Refill, 60m, StoragePlaceId: main.Id, TargetStoragePlaceId: cabinet.Id));

        var moves = await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.Transfer, 50m, StoragePlaceId: main.Id, TargetLocationId: other.Id));
        var levels = await spa.GetAsync<StoragePlaceLevelsDto>($"{Url(spa)}/storage-places/{cabinet.Id}/levels", Ct);
        var places = await PlacesAsync(spa);
        var cleared = await spa.PutAsync<StoragePlaceDto>($"{Url(spa)}/storage-places/{cabinet.Id}",
            new StoragePlaceInput("Schrank Raum A", null, StoragePlaceKind.Room, spa.RoomA.Id, true, []), Ct);

        Assert.Equal([(main.Id, -50m), (otherMain.Id, 50m)], moves.Select(m => (m.StoragePlaceId, m.Quantity)));
        Assert.Equal(50m, await QuantityAsync(spa, otherMain.Id, oil.Id, other.Id));
        var level = Assert.Single(levels.Items);
        Assert.Equal((60m, 100m, true), (level.Quantity, level.MinimumQuantity, level.BelowMinimum));
        Assert.Equal(100m, Assert.Single(places.Single(p => p.Id == cabinet.Id).Minimums).MinimumQuantity);
        Assert.Empty(cleared.Minimums);
    }

    private static async Task<ReceiptDto> SellAsync(TestSpa spa, CashRegisterDto register, ArticleDto article, int quantity)
    {
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Article, null, article.Id, quantity, null, 0, null, null, null, null, null), Ct);
        return await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 29m * quantity, null, null)]), Ct);
    }
}
