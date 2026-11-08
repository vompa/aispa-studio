using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Stock;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Lager: Bestellvorschlag aus Verbrauch, Terminen, Lieferzeit und Höchstmenge; Senden an den Lieferanten (Slice 113).</summary>
public sealed class StockReorderTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_30_ml_Tagesverbrauch_5_Tage_Lieferzeit_100_ml_Bestand_und_90_ml_Termine_When_gerechnet_Then_so_viele_Flaschen_bis_zur_Hoechstmenge()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilWithSupplierAsync(spa, leadTimeDays: 5);
        await SetMaximumAsync(spa, oil.Id, 1000m);
        await SeedStockAsync(spa, oil.Id, onHand: 100m, usedInNinetyDays: 2700m);
        await BookOilAsync(spa, oil.Id, milliliters: 90m, daysAhead: 2);

        var line = await SuggestionAsync(spa, oil.Id);

        // 100 − (30 × 5 + 90) = −140; bis 1000 fehlen 1140; das sind 3 Flaschen zu je 500 ml.
        Assert.Equal((30m, 5, 90m, 1000m, 1500m, "3 × Flasche (500 ml)"), (line.DailyUse, line.LeadTimeDays, line.BookedUse, line.Target, line.Suggested, line.PackageText));
        Assert.Equal(1500m, (await LevelAsync(spa, oil.Id)).Suggested);
    }

    [Fact]
    public async Task Given_Termin_nach_der_Lieferung_When_gerechnet_Then_zaehlt_er_nicht_zum_Bedarf_bis_Lieferung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilWithSupplierAsync(spa, leadTimeDays: 2);
        await SetMaximumAsync(spa, oil.Id, 500m);
        await BookOilAsync(spa, oil.Id, milliliters: 90m, daysAhead: 6);

        var line = await SuggestionAsync(spa, oil.Id);

        Assert.Equal((0m, 500m), (line.BookedUse, line.Suggested));
    }

    [Fact]
    public async Task Given_Hoechstmengen_an_zwei_Lagerorten_When_gerechnet_Then_ist_das_Bestellziel_ihre_Summe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilWithSupplierAsync(spa, leadTimeDays: 1);
        var shelf = await spa.PostAsync<StoragePlaceDto>($"/api/stock/locations/{spa.Location.Id}/storage-places",
            new StoragePlaceInput("Regal", null, StoragePlaceKind.Other, null, true, [new StoragePlaceMinimumDto(oil.Id, 0m, 300m)]), Ct);
        await SetMaximumAsync(spa, oil.Id, 700m);

        var line = await SuggestionAsync(spa, oil.Id);

        Assert.Equal(300m, shelf.Minimums.Single().MaximumQuantity);
        Assert.Equal((1000m, 1000m), (line.Target, line.Suggested));
    }

    [Fact]
    public async Task Given_erwarteter_Stand_ueber_der_Mindestmenge_When_gerechnet_Then_kein_Vorschlag_trotz_Hoechstmenge()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Haslauer", LeadTimeDays: 5), Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(supplier.Id) with { MinimumQuantity = 100m }, Ct);
        await SetMaximumAsync(spa, oil.Id, 1000m);
        await SeedStockAsync(spa, oil.Id, onHand: 400m, usedInNinetyDays: 0m);

        Assert.Equal(0m, (await LevelAsync(spa, oil.Id)).Suggested);
        Assert.DoesNotContain(await SuggestionsAsync(spa), l => l.ItemId == oil.Id);
    }

    [Fact]
    public async Task Given_keine_Lieferzeit_und_keine_Hoechstmenge_When_gerechnet_Then_bleibt_es_beim_Mindestbestand_ohne_Tagesverbrauch()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Tuchmaske", "Stück", 2m, 10m, null, Supplier: "Haslauer"), Ct);
        await SeedStockAsync(spa, item.Id, onHand: 4m, usedInNinetyDays: 90m);

        var line = await SuggestionAsync(spa, item.Id);

        Assert.Equal(((int?)null, 6m, 10m, (string?)null), (line.LeadTimeDays, line.Suggested, line.Target, line.PackageText));
    }

    [Fact]
    public async Task Given_Vorschlag_unter_dem_Mindestbestellwert_When_der_Entwurf_entsteht_Then_nennt_er_den_fehlenden_Betrag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Haslauer", Email: "bestellung@haslauer.example", LeadTimeDays: 1, MinimumOrderValue: 150m), Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(supplier.Id), Ct);
        await SetMaximumAsync(spa, oil.Id, 500m);

        var draft = Assert.Single(await DraftsAsync(spa));

        // 500 ml × 0,05 € = 25 €; bis 150 € fehlen 125 €.
        Assert.Equal((25m, 150m, 125m, "bestellung@haslauer.example", "1 × Flasche (500 ml)"),
            (draft.Total, draft.MinimumOrderValue, draft.MissingToMinimum, draft.SupplierEmail, draft.Lines.Single().PackageText));
    }

    [Fact]
    public async Task Given_Bestellen_und_senden_When_die_Leitung_bestaetigt_Then_geht_die_Mail_ueber_die_Queue_und_die_Bestellung_steht_auf_bestellt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = $"bestellung-{Guid.NewGuid():N}@haslauer.example";
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Haslauer", Email: email, LeadTimeDays: 1), Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(supplier.Id) with { SupplierArticleNumber = "ML-100" }, Ct);
        await SetMaximumAsync(spa, oil.Id, 500m);
        var draft = Assert.Single(await DraftsAsync(spa));

        var sent = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{draft.Id}/send", new { }, Ct);
        var mail = await fixture.Mailbox.WaitForAsync(email, _ => true, Ct);
        var again = await spa.Client.PostAsJsonAsync($"/api/stock/orders/{draft.Id}/send", new { }, TestSpa.Json, Ct);

        Assert.Equal(PurchaseOrderStatus.Ordered, sent.Status);
        Assert.NotNull(sent.OrderedAt);
        Assert.Contains($"Bestellung {draft.Number}", mail.Subject, StringComparison.Ordinal);
        Assert.Contains("1 × Flasche (500 ml)  ML-100  Massageöl", mail.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Single(fixture.Mailbox.To(email));
    }

    [Fact]
    public async Task Given_Lieferant_ohne_Mailadresse_When_gesendet_Then_abgelehnt_und_der_Entwurf_bleibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilWithSupplierAsync(spa, leadTimeDays: 1);
        await SetMaximumAsync(spa, oil.Id, 500m);
        var draft = Assert.Single(await DraftsAsync(spa));

        var response = await spa.Client.PostAsJsonAsync($"/api/stock/orders/{draft.Id}/send", new { }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PurchaseOrderStatus.Draft, (await spa.GetAsync<PurchaseOrderDto>($"/api/stock/orders/{draft.Id}", Ct)).Status);
    }

    [Fact]
    public async Task Given_Hoechstmenge_unter_der_Mindestmenge_When_gespeichert_Then_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilWithSupplierAsync(spa, leadTimeDays: 1);

        var response = await spa.Client.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/storage-places",
            new StoragePlaceInput("Regal", null, StoragePlaceKind.Other, null, true, [new StoragePlaceMinimumDto(oil.Id, 100m, 50m)]), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rezeption_darf_den_Vorschlag_nicht_lesen_und_nichts_senden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var list = await reception.GetAsync($"/api/stock/locations/{spa.Location.Id}/reorder", Ct);
        var send = await reception.PostAsJsonAsync($"/api/stock/orders/{Guid.NewGuid()}/send", new { }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode);
    }

    [Fact]
    public void Given_Hoechstmenge_unter_dem_Mindestbestand_When_gerechnet_Then_gilt_der_Mindestbestand_als_Ziel() =>
        Assert.Equal(50m, PurchaseOrders.ReorderQuantity(onHand: 50m, dailyUse: 0m, leadTimeDays: null, booked: 0m, onOrder: 0m, minimum: 100m, maximum: 20m, packSize: null));

    [Fact]
    public async Task Given_Hoechstmenge_an_einem_deaktivierten_Lagerort_When_gerechnet_Then_zaehlt_sie_nicht_zum_Ziel()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilWithSupplierAsync(spa, leadTimeDays: 1);
        await SetMaximumAsync(spa, oil.Id, 500m);
        await spa.PostAsync<StoragePlaceDto>($"/api/stock/locations/{spa.Location.Id}/storage-places",
            new StoragePlaceInput("Keller", null, StoragePlaceKind.Other, null, false, [new StoragePlaceMinimumDto(oil.Id, 0m, 300m)]), Ct);

        Assert.Equal(500m, (await SuggestionAsync(spa, oil.Id)).Target);
    }

    [Fact]
    public void Given_nur_der_Log_Versand_When_gesendet_werden_soll_Then_kommt_die_Mail_nur_mit_eigenem_Postfach_hinaus()
    {
        var logging = ActivatorUtilities.CreateInstance<LoggingMailSender>(fixture.Factory.Services);
        var sending = new SpaMailbox { Enabled = true, SendReplies = true, ProtectedSecret = "x", Kind = MailboxKind.Graph, Address = "spa@example.test" };

        Assert.False(SpaMailSender.CanDeliver(logging, []));
        Assert.False(SpaMailSender.CanDeliver(logging, [new SpaMailbox { Enabled = false, Address = "spa@example.test" }]));
        Assert.True(SpaMailSender.CanDeliver(logging, [sending]));
        Assert.True(SpaMailSender.CanDeliver(fixture.Mailbox, []));
    }

    private static StockItemInput Oil(Guid supplierId) => new("Massageöl", "ml", 0.05m, 0m, null, SupplierId: supplierId, PackUnit: "Flasche", PackSize: 500m);

    private static async Task<StockItemDto> OilWithSupplierAsync(TestSpa spa, int leadTimeDays)
    {
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Haslauer", LeadTimeDays: leadTimeDays), Ct);
        return await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(supplier.Id), Ct);
    }

    private static Task<List<PurchaseOrderDto>> DraftsAsync(TestSpa spa) =>
        spa.PostAsync<List<PurchaseOrderDto>>($"/api/stock/locations/{spa.Location.Id}/orders/from-suggestions", new { }, Ct);

    private static async Task SetMaximumAsync(TestSpa spa, Guid itemId, decimal maximum)
    {
        var path = $"/api/stock/locations/{spa.Location.Id}/storage-places";
        var main = (await spa.GetAsync<List<StoragePlaceDto>>(path, Ct)).Single(p => p.Kind == StoragePlaceKind.Main);
        await spa.PutAsync<StoragePlaceDto>($"{path}/{main.Id}",
            new StoragePlaceInput(main.Name, main.Description, main.Kind, null, true, [new StoragePlaceMinimumDto(itemId, 0m, maximum)]), Ct);
    }

    /// <summary>Bestand und Verbrauch direkt als Bewegungen: 90 Tage Verbrauch über Termine bräuchten viele Abschlüsse in der Vergangenheit.</summary>
    private async Task SeedStockAsync(TestSpa spa, Guid itemId, decimal onHand, decimal usedInNinetyDays)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var main = await db.StoragePlaces.FirstAsync(p => p.LocationId == spa.Location.Id && p.Kind == StoragePlaceKind.Main, Ct);
        var now = DateTimeOffset.UtcNow;
        StockMovement Movement(StockMovementKind kind, decimal quantity, int daysAgo) =>
            new() { ItemId = itemId, LocationId = spa.Location.Id, StoragePlaceId = main.Id, Kind = kind, Quantity = quantity, At = now.AddDays(-daysAgo) };
        db.StockMovements.AddRange(
            Movement(StockMovementKind.GoodsReceipt, onHand + usedInNinetyDays + 500m, 200),

            // Vor dem 90-Tage-Fenster: zählt nicht zum Tagesverbrauch.
            Movement(StockMovementKind.Consumption, -500m, 120),
            Movement(StockMovementKind.Consumption, -usedInNinetyDays * 2 / 3, 10),
            Movement(StockMovementKind.Sale, -usedInNinetyDays / 3, 20));
        await db.SaveChangesAsync(Ct);
    }

    private static async Task BookOilAsync(TestSpa spa, Guid itemId, decimal milliliters, int daysAhead)
    {
        await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials", new[] { new MaterialLine("", milliliters, "", 0, itemId) }, Ct);
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysAhead);
        await spa.BookOkAsync(TestSpa.At(10, date: date.DayOfWeek == DayOfWeek.Sunday ? date.AddDays(1) : date), Ct);
    }

    private static Task<List<ReorderSuggestionDto>> SuggestionsAsync(TestSpa spa) =>
        spa.GetAsync<List<ReorderSuggestionDto>>($"/api/stock/locations/{spa.Location.Id}/reorder", Ct);

    private static async Task<ReorderSuggestionDto> SuggestionAsync(TestSpa spa, Guid itemId) =>
        (await SuggestionsAsync(spa)).Single(l => l.ItemId == itemId);

    private static async Task<StockLevelDto> LevelAsync(TestSpa spa, Guid itemId) =>
        (await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct)).Items.Single(l => l.ItemId == itemId);
}
