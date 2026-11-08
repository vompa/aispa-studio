using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Lager: Verbrauch laut Rezeptur, Verkauf an der Kassa, Wareneingang, Inventur, Umbuchung und Vollkosten mit Einstandspreis.</summary>
public sealed class StockTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Abgeschlossene_Behandlung_bucht_Material_laut_Rezeptur_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.04m, 500m, null), Ct);
        await Move(spa, new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 1000m, 0.05m));
        var lines = await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials",
            new[] { new MaterialLine("", 30m, "", 0, oil.Id), new MaterialLine("Laken", 1m, "Stück", 0.30m) }, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        var used = await WaitAsync(spa, oil.Id, q => q == 970m);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.NoShow), Ct);
        var reverted = await WaitAsync(spa, oil.Id, q => q == 1000m);
        var movements = await spa.GetAsync<List<StockMovementDto>>(
            $"/api/stock/locations/{spa.Location.Id}/movements?from=2000-01-01&until=2000-12-31", Ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var recent = await spa.GetAsync<List<StockMovementDto>>(
            $"/api/stock/locations/{spa.Location.Id}/movements?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}", Ct);

        Assert.Equal(("Massageöl", "ml", 0.05m, oil.Id), (lines[0].Name, lines[0].Unit, lines[0].UnitCost, lines[0].StockItemId));
        Assert.Equal(970m, used.Quantity);
        Assert.Equal(48.50m, used.Value);
        Assert.Equal(1000m, reverted.Quantity);
        Assert.Empty(movements);
        Assert.Equal(2, recent.Count(m => m.AppointmentId == appointment.Id && m.Kind == StockMovementKind.Consumption));
        Assert.Equal(0m, recent.Where(m => m.AppointmentId == appointment.Id).Sum(m => m.Quantity));
    }

    [Fact]
    public async Task Verkauf_an_der_Kassa_bucht_ab_und_Storno_zurueck()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"L-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien", "Test-Spa GmbH", null, null), Ct);
        var accounts = await spa.GetAsync<List<RevenueAccountDto>>("/api/pos/revenue-accounts", Ct);
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Körperlotion", 29m, TaxCategory.Normal, accounts[0].Id), Ct);
        var lotion = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Körperlotion", "Stück", 11m, 2m, article.Id), Ct);
        await Move(spa, new StockMovementInput(lotion.Id, StockMovementKind.GoodsReceipt, 5m));
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Article, null, article.Id, 2, null, 0, null, null, null, null, null), Ct);

        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 58m, null, null)]), Ct);
        var sold = await WaitAsync(spa, lotion.Id, q => q == 3m);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Retour"), Ct);
        var back = await WaitAsync(spa, lotion.Id, q => q == 5m);
        var duplicate = await spa.Client.PostAsJsonAsync("/api/stock/items", new StockItemInput("Lotion 2", "Stück", 1m, 0m, article.Id), TestSpa.Json, Ct);

        Assert.Equal((3m, false, 0m), (sold.Quantity, sold.BelowMinimum, sold.Suggested));
        Assert.Equal(5m, back.Quantity);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Inventur_Korrektur_Umbuchung_und_Bestellvorschlag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var masks = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Tuchmaske", "Stück", 2m, 10m, null), Ct);
        await Move(spa, new StockMovementInput(masks.Id, StockMovementKind.GoodsReceipt, 20m, 3m));
        var main = await MainAsync(spa);
        var counted = await Move(spa, new StockMovementInput(masks.Id, StockMovementKind.Count, 18m));
        var noReason = await spa.Client.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(masks.Id, StockMovementKind.Correction, -1m, StoragePlaceId: main), TestSpa.Json, Ct);
        var noPlace = await spa.Client.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(masks.Id, StockMovementKind.Count, 5m), TestSpa.Json, Ct);
        await Move(spa, new StockMovementInput(masks.Id, StockMovementKind.Correction, -1m, Reason: "eingerissen"));
        var transfer = await Move(spa, new StockMovementInput(masks.Id, StockMovementKind.Transfer, 9m, TargetLocationId: other.Id));
        var manual = await spa.Client.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(masks.Id, StockMovementKind.Consumption, -1m, StoragePlaceId: main), TestSpa.Json, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var receptionMoves = await reception.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(masks.Id, StockMovementKind.Count, 0m, StoragePlaceId: main), TestSpa.Json, Ct);
        var receptionCorrects = await reception.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(masks.Id, StockMovementKind.Correction, 1m, StoragePlaceId: main, Reason: "gefunden"), TestSpa.Json, Ct);
        var receptionSees = await reception.GetFromJsonAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", TestSpa.Json, Ct);

        var here = await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct);
        var there = await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{other.Id}", Ct);
        var item = Assert.Single(here.Items, i => i.ItemId == masks.Id);

        Assert.Equal(-2m, Assert.Single(counted).Quantity);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noPlace.StatusCode);
        Assert.Equal([-9m, 9m], transfer.Select(m => m.Quantity));
        Assert.Equal(HttpStatusCode.BadRequest, manual.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, receptionMoves.StatusCode);
        Assert.Equal(HttpStatusCode.OK, receptionCorrects.StatusCode);
        Assert.NotNull(receptionSees);
        Assert.Equal((9m, true, 1m, 27m), (item.Quantity, item.BelowMinimum, item.Suggested, item.Value));
        Assert.Equal(9m, Assert.Single(there.Items, i => i.ItemId == masks.Id).Quantity);
    }

    [Fact]
    public async Task Bestellvorschlag_wird_Bestellung_und_Lieferung_bucht_den_Wareneingang()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items",
            new StockItemInput("Arganöl", "ml", 0.10m, 1000m, null, Supplier: "Aroma GmbH", SupplierArticleNumber: "AR-500"), Ct);
        var salt = await spa.PostAsync<StockItemDto>("/api/stock/items",
            new StockItemInput("Meersalz", "g", 0.01m, 2000m, null, Supplier: "aroma gmbh "), Ct);
        var loose = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Kerzen", "Stück", 1m, 5m, null), Ct);
        var url = $"/api/stock/locations/{spa.Location.Id}/orders";

        var created = await spa.PostAsync<List<PurchaseOrderDto>>($"{url}/from-suggestions", new { }, Ct);
        var again = await spa.PostAsync<List<PurchaseOrderDto>>($"{url}/from-suggestions", new { }, Ct);
        var draft = Assert.Single(created);
        var overview = await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct);
        var edited = await spa.PutAsync<PurchaseOrderDto>($"/api/stock/orders/{draft.Id}", new PurchaseOrderInput("Aroma GmbH",
            [new PurchaseOrderLineInput(oil.Id, 1000m, 0.12m), new PurchaseOrderLineInput(salt.Id, 2000m)], "Bitte bis Freitag"), Ct);
        var text = await spa.Client.GetStringAsync($"/api/stock/orders/{draft.Id}/text", Ct);
        var early = await spa.Client.PostAsJsonAsync($"/api/stock/orders/{draft.Id}/deliveries",
            new PurchaseOrderDeliveryInput([new(edited.Lines[0].Id, 1m)]), TestSpa.Json, Ct);
        var ordered = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{draft.Id}/ordered", new { }, Ct);
        var lateEdit = await spa.Client.PutAsJsonAsync($"/api/stock/orders/{draft.Id}",
            new PurchaseOrderInput("Aroma GmbH", [new PurchaseOrderLineInput(oil.Id, 1m)]), TestSpa.Json, Ct);
        var oilLine = ordered.Lines.Single(l => l.ItemId == oil.Id);
        var saltLine = ordered.Lines.Single(l => l.ItemId == salt.Id);
        var partial = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{draft.Id}/deliveries",
            new PurchaseOrderDeliveryInput([new(oilLine.Id, 1000m, 0.14m), new(saltLine.Id, 500m)], "LS-77"), Ct);
        var afterPartial = await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct);
        var complete = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{draft.Id}/deliveries",
            new PurchaseOrderDeliveryInput([new(saltLine.Id, 1500m)]), Ct);
        var tooMuch = await spa.Client.PostAsJsonAsync($"/api/stock/orders/{draft.Id}/deliveries",
            new PurchaseOrderDeliveryInput([new(saltLine.Id, 1m)]), TestSpa.Json, Ct);
        var other = await spa.PostAsync<PurchaseOrderDto>(url, new PurchaseOrderInput("Kerzenhof", [new PurchaseOrderLineInput(loose.Id, 5m)]), Ct);
        var cancelled = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{other.Id}/close", new { }, Ct);
        var list = await spa.GetAsync<List<PurchaseOrderDto>>(url, Ct);
        var items = await spa.GetAsync<List<StockItemDto>>("/api/stock/items", Ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movements = await spa.GetAsync<List<StockMovementDto>>(
            $"/api/stock/locations/{spa.Location.Id}/movements?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}", Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var receptionList = await reception.GetAsync(url, Ct);

        Assert.Equal(("Aroma GmbH", PurchaseOrderStatus.Draft, 2), (draft.Supplier, draft.Status, draft.Lines.Count));
        Assert.Equal(120m, draft.Total);
        Assert.Empty(again);
        var oilLevel = overview.Items.Single(i => i.ItemId == oil.Id);
        Assert.Equal((1000m, 0m), (oilLevel.OnOrder, oilLevel.Suggested));
        Assert.Equal(5m, overview.Items.Single(i => i.ItemId == loose.Id).Suggested);
        Assert.Contains("AR-500  Arganöl", text, StringComparison.Ordinal);
        Assert.Contains("Bitte bis Freitag", text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, lateEdit.StatusCode);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, partial.Status);
        Assert.Equal((1000m, 1500m), (afterPartial.Items.Single(i => i.ItemId == oil.Id).Quantity, afterPartial.Items.Single(i => i.ItemId == salt.Id).OnOrder));
        Assert.Equal(PurchaseOrderStatus.Received, complete.Status);
        Assert.NotNull(complete.ClosedAt);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(2, list.Count);
        Assert.Equal(0.14m, items.Single(i => i.Id == oil.Id).UnitCost);
        Assert.Equal(3, movements.Count(m => m.PurchaseOrderId == draft.Id && m.Kind == StockMovementKind.GoodsReceipt));
        Assert.Contains(movements, m => m.PurchaseOrderId == draft.Id && m.Reason == $"Bestellung {draft.Number}, Lieferschein LS-77");
        Assert.Equal(HttpStatusCode.Forbidden, receptionList.StatusCode);
    }

    [Fact]
    public async Task Chargen_mit_Ablaufdatum_zuerst_ablaufend_zuerst_verbraucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var cream = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Gesichtscreme", "Tiegel", 8m, 0m, null), Ct);
        var url = $"/api/stock/locations/{spa.Location.Id}/batches";
        var today = (await spa.GetAsync<StockBatchesDto>(url, Ct)).Today;
        await Move(spa, new StockMovementInput(cream.Id, StockMovementKind.GoodsReceipt, 5m, Batch: "C0", ExpiresOn: today.AddDays(-5)));
        var receipt = await Move(spa, new StockMovementInput(cream.Id, StockMovementKind.GoodsReceipt, 10m, Batch: " A1 ", ExpiresOn: today.AddDays(10)));
        await Move(spa, new StockMovementInput(cream.Id, StockMovementKind.GoodsReceipt, 10m, Batch: "B2", ExpiresOn: today.AddDays(200)));
        var withBefore = await spa.GetAsync<StockBatchesDto>(url, Ct);
        await Move(spa, new StockMovementInput(cream.Id, StockMovementKind.Correction, -12m, Reason: "verbraucht"));
        var countWithBatch = await spa.Client.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(cream.Id, StockMovementKind.Count, 1m, Batch: "X", StoragePlaceId: await MainAsync(spa)), TestSpa.Json, Ct);

        var after = await spa.GetAsync<StockBatchesDto>(url, Ct);
        var level = Assert.Single((await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct)).Items, i => i.ItemId == cream.Id);

        Assert.Equal(("A1", today.AddDays(10)), (receipt[0].Batch, receipt[0].ExpiresOn));
        Assert.Equal([("C0", 5m, true, true), ("A1", 10m, false, true), ("B2", 10m, false, false)],
            withBefore.Batches.Select(b => (b.Batch, b.Quantity, b.Expired, b.ExpiresSoon)));
        Assert.Equal([("A1", 3m), ("B2", 10m)], after.Batches.Select(b => (b.Batch, b.Quantity)));
        Assert.Equal(HttpStatusCode.BadRequest, countWithBatch.StatusCode);
        Assert.Equal(today.AddDays(10), level.NextExpiry);
    }

    [Fact]
    public async Task Verbrauch_und_Verkauf_tragen_die_Charge_und_lassen_sich_zurueckverfolgen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"L-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien", "Test-Spa GmbH", null, null), Ct);
        var accounts = await spa.GetAsync<List<RevenueAccountDto>>("/api/pos/revenue-accounts", Ct);
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Gesichtscreme", 39m, TaxCategory.Normal, accounts[0].Id), Ct);
        var cream = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Gesichtscreme", "Tiegel", 8m, 0m, article.Id), Ct);
        var today = (await spa.GetAsync<StockBatchesDto>($"/api/stock/locations/{spa.Location.Id}/batches", Ct)).Today;
        await Move(spa, new StockMovementInput(cream.Id, StockMovementKind.GoodsReceipt, 5m, Batch: "C0", ExpiresOn: today.AddDays(-5)));
        await Move(spa, new StockMovementInput(cream.Id, StockMovementKind.GoodsReceipt, 10m, Batch: "A1", ExpiresOn: today.AddDays(10)));
        await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials",
            new[] { new MaterialLine("", 7m, "", 0, cream.Id) }, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        // Behandlung: 7 Tiegel, zuerst die 5 der ablaufenden Charge C0, dann 2 aus A1.
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        await WaitAsync(spa, cream.Id, q => q == 8m);
        // Verkauf von 3 aus A1, Storno zurück nach A1.
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, spa.Customer.Id, []), Ct);
        await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", new BillLineInput(BillLineKind.Article, null, article.Id, 3, null, 0, null, null, null, null, null), Ct);
        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 117m, null, null)]), Ct);
        await WaitAsync(spa, cream.Id, q => q == 5m);
        var usedC0 = await spa.GetAsync<StockBatchUsageDto>($"/api/stock/batch-usage?itemId={cream.Id}&batch=C0", Ct);
        var usedA1 = await spa.GetAsync<StockBatchUsageDto>($"/api/stock/batch-usage?itemId={cream.Id}&batch=A1", Ct);
        await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Retour"), Ct);
        await WaitAsync(spa, cream.Id, q => q == 8m);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.NoShow), Ct);
        await WaitAsync(spa, cream.Id, q => q == 15m);
        var afterA1 = await spa.GetAsync<StockBatchUsageDto>($"/api/stock/batch-usage?itemId={cream.Id}&batch=A1", Ct);
        var batches = await spa.GetAsync<StockBatchesDto>($"/api/stock/locations/{spa.Location.Id}/batches", Ct);
        var unknown = await spa.Client.GetAsync($"/api/stock/batch-usage?itemId={Guid.NewGuid()}&batch=A1", Ct);

        Assert.Equal((5m, 5m), (usedC0.Received, usedC0.Used));
        var treated = Assert.Single(usedC0.Entries, e => e.Kind == StockMovementKind.Consumption);
        Assert.Equal((-5m, spa.Customer.Id, appointment.Id, spa.MassageTreatment.Name), (treated.Quantity, treated.CustomerId, treated.AppointmentId, treated.Detail));
        Assert.Equal((10m, 5m, today.AddDays(10)), (usedA1.Received, usedA1.Used, usedA1.ExpiresOn));
        Assert.Equal([(StockMovementKind.GoodsReceipt, 10m), (StockMovementKind.Consumption, -2m), (StockMovementKind.Sale, -3m)],
            usedA1.Entries.Select(e => (e.Kind, e.Quantity)));
        var sold = usedA1.Entries.Single(e => e.Kind == StockMovementKind.Sale);
        Assert.Equal((spa.Customer.Id, receipt.Id, $"Beleg {receipt.Number}"), (sold.CustomerId, sold.ReceiptId, sold.Detail));
        Assert.False(string.IsNullOrWhiteSpace(sold.CustomerName));
        Assert.Equal((1, 0), (usedA1.Customers, afterA1.Customers));
        Assert.Equal((10m, 0m), (afterA1.Received, afterA1.Used));
        Assert.Equal([(StockMovementKind.GoodsReceipt, 10m), (StockMovementKind.Sale, -3m), (StockMovementKind.Sale, 3m)],
            afterA1.Entries.Select(e => (e.Kind, e.Quantity)));
        Assert.Equal([("C0", 5m), ("A1", 10m)], batches.Batches.Where(b => b.ItemId == cream.Id).Select(b => (b.Batch, b.Quantity)));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    /// <summary>Bucht am Hauptlager, wenn die Bewegung keinen Lagerort nennt (die Maske schickt immer einen).</summary>
    private static async Task<List<StockMovementDto>> Move(TestSpa spa, StockMovementInput input) =>
        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            input.StoragePlaceId is null && input.Kind != StockMovementKind.GoodsReceipt ? input with { StoragePlaceId = await MainAsync(spa) } : input, Ct);

    private static async Task<Guid> MainAsync(TestSpa spa) =>
        (await spa.GetAsync<List<StoragePlaceDto>>($"/api/stock/locations/{spa.Location.Id}/storage-places", Ct)).Single(p => p.Kind == StoragePlaceKind.Main).Id;

    private static async Task<StockLevelDto> WaitAsync(TestSpa spa, Guid itemId, Func<decimal, bool> done)
    {
        for (var i = 0; ; i++)
        {
            var level = (await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct)).Items.Single(l => l.ItemId == itemId);
            if (done(level.Quantity) || i == 100)
            {
                return level;
            }

            await Task.Delay(100, Ct);
        }
    }
}
