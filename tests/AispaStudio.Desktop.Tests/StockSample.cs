using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 101: Beispieldaten und Antworten der Lager-API für die native Maske „Lager“ (ViewModel- und Fenstertests teilen sie).</summary>
internal static class StockSample
{
    /// <summary>Dieselben Kennungen wie die Standorte der Fenstertests (<see cref="SpaSample"/>), damit deren Kalender und das Lager am selben Standort hängen.</summary>
    public static readonly LocationDto Vienna = new(SpaSample.LocationId, "Wien", "Europe/Vienna", "EUR", [], null, LocationContact.Empty);
    public static readonly LocationDto Salzburg = new(SpaSample.Salzburg.Id, "Salzburg", "Europe/Vienna", "EUR", [], null, LocationContact.Empty);

    public static readonly Guid OilId = Guid.Parse("00000000-0000-0000-0000-0000000c1111");
    public static readonly Guid TowelId = Guid.Parse("00000000-0000-0000-0000-0000000c1112");
    public static readonly Guid ArticleId = Guid.Parse("00000000-0000-0000-0000-0000000c1121");
    public static readonly Guid RoomId = Guid.Parse("00000000-0000-0000-0000-0000000c1131");
    public static readonly Guid MainId = Guid.Parse("00000000-0000-0000-0000-0000000c1141");
    public static readonly Guid ShopId = Guid.Parse("00000000-0000-0000-0000-0000000c1142");
    public static readonly Guid CabinetId = Guid.Parse("00000000-0000-0000-0000-0000000c1143");

    public static readonly Guid SupplierId = Guid.Parse("00000000-0000-0000-0000-0000000c1151");

    public static readonly StockSupplierDto Lavendelhof = new(SupplierId, "Lavendelhof", "Eva Muster", "bestellung@lavendelhof.example", null, 5, 150m, SupplierOrderChannel.Email, true);

    public static readonly StockItemDto Oil = new(OilId, "Massageöl", "Flasche", 12.5m, 2, ArticleId, true, "Lavendelhof", "ML-100", SupplierId);
    public static readonly StockItemDto Towel = new(TowelId, "Handtuch", "Stück", 3m, 10, null, true, PackUnit: "Packung", PackSize: 10m, CaseUnit: "Karton", PacksPerCase: 5,
        Packages: [new(StockPackageLevel.Unit, "Stück", "Stück", 1m), new(StockPackageLevel.Pack, "Packung", "Packung (10 Stück)", 10m), new(StockPackageLevel.Case, "Karton", "Karton (5 × 10 Stück)", 50m)]);

    public static readonly Guid OrderId = Guid.Parse("00000000-0000-0000-0000-0000000c1161");

    /// <summary>Entwurf bei Lavendelhof: 5 Flaschen zu 12,50 € sind 62,50 €, bis zum Mindestbestellwert von 150 € fehlen 87,50 €.</summary>
    public static readonly PurchaseOrderDto DraftOrder = new(OrderId, 7, Vienna.Id, "Lavendelhof", PurchaseOrderStatus.Draft, null, new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero), null, null, 62.5m,
        [new PurchaseOrderLineDto(Guid.Parse("00000000-0000-0000-0000-0000000c1162"), OilId, "Massageöl", "Flasche", "ML-100", 5m, 12.5m, 0m)], 150m, 87.5m, "bestellung@lavendelhof.example");

    /// <summary>Dieselbe Bestellung, bestellt: 5 Flaschen stehen aus.</summary>
    public static readonly PurchaseOrderDto OrderedOrder = DraftOrder with { Status = PurchaseOrderStatus.Ordered, OrderedAt = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero) };

    public const string OrderText = "Bestellung 7 an Lavendelhof\nLieferung an: Wien\n\n5 Flasche  ML-100  Massageöl\n";

    private static readonly StockDemandLine OilShort = new(OilId, "Massageöl", "Flasche", 10m, 12m, 2m, 4, 0, StockWarningKind.Insufficient, new DateTimeOffset(2027, 3, 2, 15, 0, 0, TimeSpan.Zero),
        "Massageöl: Bestand 10 Flasche reicht nicht für alle gebuchten Termine (Bedarf 12 Flasche).", 2m);

    /// <summary>Gebuchte Termine brauchen 12 Flaschen, 10 sind da: es fehlen 2. Handtücher reichen.</summary>
    public static readonly StockDemandDto Demand = new(Vienna.Id, new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 7), 4,
        [OilShort, new StockDemandLine(TowelId, "Handtuch", "Stück", 5m, 1m, 0m, 1, 4, null, null, null)], [OilShort]);

    /// <summary>Charge L-9: 12 Flaschen kamen herein, 1 ging in einen Termin von Maria Gruber, 1 an der Kassa ohne Kunde.</summary>
    public static readonly StockBatchUsageDto BatchUsage = new(OilId, "Massageöl", "Flasche", "L-9", new DateOnly(2027, 9, 1), 12m, 2m, Customers: 1, Entries:
    [
        new StockBatchUsageEntry(new DateTimeOffset(2027, 2, 1, 9, 0, 0, TimeSpan.Zero), StockMovementKind.GoodsReceipt, Vienna.Id, "Wien", 12m, null, null, "Bestellung 7", null, null),
        new StockBatchUsageEntry(new DateTimeOffset(2027, 2, 10, 14, 0, 0, TimeSpan.Zero), StockMovementKind.Consumption, Vienna.Id, "Wien", -1m, Guid.Parse("00000000-0000-0000-0000-0000000c1171"), "Maria Gruber", "Aromaöl-Massage",
            Guid.Parse("00000000-0000-0000-0000-0000000c1172"), null),
        new StockBatchUsageEntry(new DateTimeOffset(2027, 2, 12, 11, 0, 0, TimeSpan.Zero), StockMovementKind.Sale, Vienna.Id, "Wien", -1m, null, null, "Beleg 42", null, Guid.Parse("00000000-0000-0000-0000-0000000c1173")),
    ]);

    public static readonly ReorderSuggestionDto OilSuggestion = new(OilId, "Massageöl", "Flasche", "Lavendelhof", 10m, 3m, 5, 9m, 0m, 20m, 25m, null, 312.5m);

    /// <summary>Die Aufgaben am 1. März 2027: eine Charge läuft in 20 Tagen ab und ist verkäuflich, eine ist abgelaufen.</summary>
    public static readonly StockExpiryTasksDto ExpiryTasks = ExpiryTaskSample();

    private static StockExpiryTasksDto ExpiryTaskSample()
    {
        var soon = new StockExpiryTaskDto(OilId, "Massageöl", "Flasche", "L-9", new DateOnly(2027, 3, 21), 20, 3m, MainId, "Hauptlager", "Massageöl 100 ml", 24m);
        var late = new StockExpiryTaskDto(TowelId, "Handtuch", "Stück", "H-1", new DateOnly(2027, 2, 20), -9, 2m, ShopId, "Verkaufsregal");
        return new(Vienna.Id, new DateOnly(2027, 3, 1), [soon], [soon], [late]);
    }

    public static readonly StoragePlaceDto Main = new(MainId, Vienna.Id, "Hauptlager", null, StoragePlaceKind.Main, null, true, 0, []);
    public static readonly StoragePlaceDto Shop = new(ShopId, Vienna.Id, "Verkaufsregal", "Im Empfang", StoragePlaceKind.Sales, null, true, 1, []);
    public static readonly StoragePlaceDto Cabinet = new(CabinetId, Vienna.Id, "Schrank Zeder", null, StoragePlaceKind.Room, RoomId, true, 2,
        [new StoragePlaceMinimumDto(OilId, 1), new StoragePlaceMinimumDto(TowelId, 4)]);

    public static readonly LocationResources Resources = new(Vienna, [new RoomDto(RoomId, Vienna.Id, "Raum Zeder", 10)], [], [], [], []);

    public static string Route(string rest = "") => $"/api/stock/locations/{Vienna.Id}{rest}";

    public static StockOverviewDto Overview(decimal oil) => new(Vienna.Id, 14, oil * 12.5m + 15m,
    [
        new StockLevelDto(OilId, "Massageöl", "Flasche", oil, 2, oil * 12.5m, 3, 0, oil < 2, 0, "Lavendelhof", null,
            [new StockPlaceQuantityDto(MainId, oil - 1), new StockPlaceQuantityDto(ShopId, 1)]),
        new StockLevelDto(TowelId, "Handtuch", "Stück", 5, 10, 15m, 0, 5, true, 0, null, null, [new StockPlaceQuantityDto(MainId, 5)]),
    ]);

    public static StoragePlaceLevelsDto Levels(Guid placeId) => placeId switch
    {
        _ when placeId == MainId => new(MainId, 77.5m, [Level(Oil, 10, 2), Level(Towel, 5, 10)]),
        _ when placeId == ShopId => new(ShopId, 12.5m, [Level(Oil, 1, 1)]),
        _ => new(placeId, 0, []),
    };

    private static StoragePlaceLevelDto Level(StockItemDto item, decimal quantity, decimal minimum) =>
        new(item.Id, item.Name, item.Unit, quantity, minimum, quantity < minimum, quantity * item.UnitCost, null, []);

    public static StockMovementDto Movement(StockMovementKind kind, decimal quantity, Guid? receipt = null) =>
        new(Guid.NewGuid(), OilId, "Massageöl", Vienna.Id, MainId, "Hauptlager", kind, quantity, 12.5m, new DateTimeOffset(2027, 2, 20, 10, 30, 0, TimeSpan.Zero),
            kind == StockMovementKind.Correction ? "Bruch" : null, null, receipt, Batch: kind == StockMovementKind.GoodsReceipt ? "L-1" : null);

    /// <summary>
    /// Die Lager-API mit festen Antworten. <paramref name="oil"/> liefert die aktuelle Menge Massageöl im Bestand des Standorts (ändert sich mit dem Verkauf an der Kassa);
    /// <paramref name="two"/> fügt den Standort Salzburg hinzu; <paramref name="locationList"/> aus lässt die Standorte und Räume der Fenstertests stehen. Schreibende Aufrufe antworten wie die API und werden in <see cref="StubApi.Calls"/> festgehalten.
    /// </summary>
    public static StubApi Api(StubApi? api = null, Func<decimal>? oil = null, bool two = false, StockMovementDto[]? movements = null,
        string period = "from=2027-01-30&until=2027-03-01", bool locationList = true, PurchaseOrderDto? startOrder = null)
    {
        oil ??= () => 10m;
        var delivered = 0m;
        var locations = two ? new List<LocationDto> { Vienna, Salzburg } : [Vienna];
        var items = new List<StockItemDto> { Oil, Towel };
        var places = new List<StoragePlaceDto> { Main, Shop, Cabinet };
        var suppliers = new List<StockSupplierDto> { Lavendelhof };
        var order = startOrder ?? DraftOrder;
        api ??= new StubApi();
        // Die Uhr der ViewModel-Tests (1.3.2027) und der Bildtests (19.10.2026), je Zeitraum der Auswahl.
        foreach (var today in new[] { new DateOnly(2027, 3, 1), new DateOnly(2026, 10, 19) })
        {
            foreach (var days in new[] { 1, 7, 14 })
            {
                api.On($"GET {Route(string.Create(CultureInfo.InvariantCulture, $"/demand?from={today:yyyy-MM-dd}&until={today.AddDays(days - 1):yyyy-MM-dd}"))}", Demand);
            }
        }

        if (locationList)
        {
            api.On("GET /api/locations", locations).On($"GET /api/locations/{Vienna.Id}/resources", Resources);
        }

        return api
            .OnJson("GET /api/stock/items", _ => items.ToList())
            .On("GET /api/pos/articles", new List<ArticleDto> { new(ArticleId, "Massageöl 100 ml", 24m, TaxCategory.Normal, Guid.NewGuid()) })
            .OnJson($"GET {Route()}", _ => Overview(oil() + delivered))
            .On($"GET {Route("/batches")}", new StockBatchesDto(Vienna.Id, new DateOnly(2027, 3, 1), 14, []))
            .OnJson($"GET {Route("/storage-places")}", _ => places.ToList())
            .On($"GET {Route($"/storage-places/{MainId}/levels")}", Levels(MainId))
            .On($"GET {Route($"/storage-places/{ShopId}/levels")}", Levels(ShopId))
            .On($"GET {Route($"/storage-places/{CabinetId}/levels")}", Levels(CabinetId))
            .On($"GET {Route($"/movements?{period}")}", movements ?? [Movement(StockMovementKind.GoodsReceipt, 12), Movement(StockMovementKind.Correction, -2)])
            .OnJson("POST /api/stock/items", body =>
            {
                var input = JsonSerializer.Deserialize<StockItemInput>(body!, ApiClient.Json)!;
                var created = new StockItemDto(Guid.NewGuid(), input.Name, input.Unit, input.UnitCost, input.MinimumQuantity, input.ArticleId, input.IsActive, input.Supplier, input.SupplierArticleNumber);
                items.Add(created);
                return created;
            }, HttpStatusCode.Created)
            .OnJson($"PUT /api/stock/items/{TowelId}", body =>
            {
                var input = JsonSerializer.Deserialize<StockItemInput>(body!, ApiClient.Json)!;
                var changed = Towel with { SupplierId = input.SupplierId, Supplier = suppliers.FirstOrDefault(s => s.Id == input.SupplierId)?.Name };
                items[items.FindIndex(i => i.Id == TowelId)] = changed;
                return changed;
            })
            .OnJson("GET /api/stock/suppliers", _ => suppliers.ToList())
            .OnJson("POST /api/stock/suppliers", body =>
            {
                var input = JsonSerializer.Deserialize<StockSupplierInput>(body!, ApiClient.Json)!;
                var created = new StockSupplierDto(Guid.NewGuid(), input.Name, input.ContactPerson, input.Email, input.Phone, input.LeadTimeDays, input.MinimumOrderValue, input.OrderChannel, input.IsActive);
                suppliers.Add(created);
                return created;
            }, HttpStatusCode.Created)
            .OnJson($"PUT /api/stock/suppliers/{SupplierId}", body =>
            {
                var input = JsonSerializer.Deserialize<StockSupplierInput>(body!, ApiClient.Json)!;
                var changed = Lavendelhof with { Name = input.Name, IsActive = input.IsActive, LeadTimeDays = input.LeadTimeDays };
                suppliers[suppliers.FindIndex(s => s.Id == SupplierId)] = changed;
                return changed;
            })
            .On($"GET {Route("/expiry-tasks")}", ExpiryTasks)
            .OnJson($"GET {Route("/orders")}", _ => new List<PurchaseOrderDto> { order })
            .On($"GET {Route("/reorder")}", new List<ReorderSuggestionDto> { OilSuggestion })
            .OnJson($"POST {Route("/orders/from-suggestions")}", _ => new List<PurchaseOrderDto> { order })
            .OnJson($"PUT /api/stock/orders/{OrderId}", body =>
            {
                var input = JsonSerializer.Deserialize<PurchaseOrderInput>(body!, ApiClient.Json)!;
                var quantity = input.Lines.Single().Quantity;
                return order = order with { Total = quantity * 12.5m, MissingToMinimum = Math.Max(0m, 150m - quantity * 12.5m), Lines = [order.Lines[0] with { Quantity = quantity }] };
            })
            .OnJson($"POST /api/stock/orders/{OrderId}/send", _ => order = order with { Status = PurchaseOrderStatus.Ordered })
            .OnJson($"POST /api/stock/orders/{OrderId}/close", _ => order = order with { Status = order.Lines.Any(l => l.Received > 0) ? PurchaseOrderStatus.Received : PurchaseOrderStatus.Cancelled })
            .OnJson($"POST /api/stock/orders/{OrderId}/ordered", _ => order = order with { Status = PurchaseOrderStatus.Ordered })
            .OnBytes($"GET /api/stock/orders/{OrderId}/text", Encoding.UTF8.GetBytes(OrderText))
            .OnJson($"POST /api/stock/orders/{OrderId}/deliveries", body =>
            {
                var input = JsonSerializer.Deserialize<PurchaseOrderDeliveryInput>(body!, ApiClient.Json)!;
                var amount = input.Lines.Sum(l => l.Quantity);
                delivered += amount;
                var line = order.Lines[0] with { Received = order.Lines[0].Received + amount };
                return order = order with { Lines = [line], Status = line.Received >= line.Quantity ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived };
            })
            .On($"GET /api/stock/batch-usage?itemId={OilId}&batch=L-9", BatchUsage)
            .On($"POST {Route("/movements")}", new List<StockMovementDto> { Movement(StockMovementKind.GoodsReceipt, 1) })
            .OnJson($"POST {Route("/storage-places")}", body =>
            {
                var input = JsonSerializer.Deserialize<StoragePlaceInput>(body!, ApiClient.Json)!;
                var created = new StoragePlaceDto(Guid.NewGuid(), Vienna.Id, input.Name, input.Description, input.Kind, input.RoomId, input.IsActive, places.Count, input.Minimums ?? []);
                places.Add(created);
                return created;
            }, HttpStatusCode.Created)
            .OnJson($"PUT {Route($"/storage-places/{ShopId}")}", body =>
            {
                var input = JsonSerializer.Deserialize<StoragePlaceInput>(body!, ApiClient.Json)!;
                var changed = Shop with { Name = input.Name, Description = input.Description, IsActive = input.IsActive, Minimums = input.Minimums ?? [] };
                places[places.FindIndex(p => p.Id == ShopId)] = changed;
                return changed;
            })
            .OnJson($"PUT {Route($"/storage-places/{MainId}")}", body =>
            {
                var input = JsonSerializer.Deserialize<StoragePlaceInput>(body!, ApiClient.Json)!;
                var changed = Main with { Name = input.Name };
                places[places.FindIndex(p => p.Id == MainId)] = changed;
                return changed;
            })
            .OnJson($"DELETE {Route($"/storage-places/{ShopId}")}", _ =>
            {
                places.RemoveAll(p => p.Id == ShopId);
                return new { };
            })
            .OnJson($"POST {Route($"/storage-places/{ShopId}/minimums/copy-from/{CabinetId}")}", _ => Shop with { Minimums = Cabinet.Minimums });
    }

    public static StockMovementInput SentMovement(StubApi api) =>
        JsonSerializer.Deserialize<StockMovementInput>(api.Calls.Single(c => c.Route == $"POST {Route("/movements")}").Body!, ApiClient.Json)!;

    public static IEnumerable<StockMovementInput> SentMovements(StubApi api) =>
        api.Calls.Where(c => c.Route == $"POST {Route("/movements")}").Select(c => JsonSerializer.Deserialize<StockMovementInput>(c.Body!, ApiClient.Json)!);

    /// <summary>Die Uhr der Lager-Tests: 1. März 2027, damit „die letzten 30 Tage“ im Aufruf der Bewegungen feststehen.</summary>
    public sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
