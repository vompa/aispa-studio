using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Slice 136 (Ä318): offene Bestellungen mit dem voraussichtlichen Liefertermin. Das System speichert keinen zugesagten Termin: Er ist das Bestelldatum plus die Lieferzeit des
/// Lieferanten. „Jetzt“ ist Mittwoch, der 3. März 2027, 11:00 Uhr in Wien. Bestellungen sieht, wer das Lager des Standorts verwalten darf.
/// </summary>
public sealed class OpenOrderTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly AnswerKit _kit = new(fixture);

    private static readonly DateTimeOffset Monday8 = new(2027, 3, 1, 7, 0, 0, TimeSpan.Zero);

    private async Task<PurchaseOrderDto> OrderedAsync(TestSpa spa, DateTimeOffset orderedAt, int? leadTimeDays, bool markOrdered = true)
    {
        _ = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Aroma GmbH", LeadTimeDays: leadTimeDays), Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Arganöl", "Stück", 4m, 5m, null, Supplier: "Aroma GmbH"), Ct);
        var order = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/locations/{spa.Location.Id}/orders",
            new PurchaseOrderInput("Aroma GmbH", [new PurchaseOrderLineInput(oil.Id, 5m)]), Ct);
        if (!markOrdered)
        {
            return order;
        }

        var ordered = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{order.Id}/ordered", new { }, Ct);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        await db.PurchaseOrders.Where(o => o.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderedAt, orderedAt), Ct);
        return ordered;
    }

    [Fact]
    public async Task Bestellung_vor_zwei_Tagen_mit_drei_Tagen_Lieferzeit_kommt_morgen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await OrderedAsync(spa, Monday8, 3);

        var block = await _kit.AnswerAsync(spa, "Wann kommt die Lieferung?");

        Assert.Equal("1 offene Bestellung. Die nächste Lieferung kommt voraussichtlich am Donnerstag, 04.03.2027.", block.Text);
        Assert.Equal(["Lieferant", "Bestellt am", "Voraussichtlich", "Offen"], block.Table!.Columns);
        Assert.Equal([["Aroma GmbH", "01.03.2027", "Donnerstag, 04.03.2027", "Arganöl: 5 Stück"]], block.Table.Rows);
        Assert.Equal("Bestellungen des Standorts Wien (Bestelldatum plus Lieferzeit des Lieferanten)", block.Source);
    }

    [Fact]
    public async Task Ohne_Lieferzeit_sagt_die_Antwort_Lieferzeit_nicht_hinterlegt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await OrderedAsync(spa, Monday8, null);

        var block = await _kit.AnswerAsync(spa, "Was ist bestellt?");

        Assert.Equal("1 offene Bestellung.", block.Text);
        Assert.Equal([["Aroma GmbH", "01.03.2027", "Lieferzeit nicht hinterlegt", "Arganöl: 5 Stück"]], block.Table!.Rows);
    }

    [Fact]
    public async Task Ueberfaellige_Lieferung_wird_als_ueberfaellig_genannt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await OrderedAsync(spa, new DateTimeOffset(2027, 2, 15, 7, 0, 0, TimeSpan.Zero), 3);

        var block = await _kit.AnswerAsync(spa, "Wann kommt die Lieferung?");

        Assert.Equal("1 offene Bestellung.", block.Text);
        Assert.Equal([["Aroma GmbH", "15.02.2027", "überfällig seit 18.02.2027", "Arganöl: 5 Stück"]], block.Table!.Rows);
    }

    [Fact]
    public async Task Entwurf_und_geschlossene_Bestellungen_fehlen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await OrderedAsync(spa, Monday8, 3, markOrdered: false);
        var oil = await spa.GetAsync<List<StockItemDto>>("/api/stock/items", Ct);
        var closed = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/locations/{spa.Location.Id}/orders",
            new PurchaseOrderInput("Aroma GmbH", [new PurchaseOrderLineInput(oil[0].Id, 2m)]), Ct);
        await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{closed.Id}/close", new { }, Ct);

        var block = await _kit.AnswerAsync(spa, "Wann kommt die Lieferung?");

        Assert.Equal("Es gibt keine offene Bestellung.", block.Text);
        Assert.Null(block.Table);
    }

    [Fact]
    public async Task Ohne_Recht_zum_Verwalten_des_Lagers_fehlt_die_Berechtigung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await OrderedAsync(spa, Monday8, 3);

        var block = await _kit.AnswerAsync(spa, "Wann kommt die Lieferung?", role: StaffRole.Reception);

        Assert.Equal(AssistantBlockKind.Denied, block.Kind);
        Assert.Equal("Dafür fehlt die Berechtigung. Bestellungen sieht, wer das Lager des Standorts verwaltet.", block.Text);
    }
}
