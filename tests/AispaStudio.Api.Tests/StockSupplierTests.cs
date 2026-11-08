using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>Lager: Lieferantenstamm, Packungen am Material und Aufgaben zu Ablaufdaten (Slice 112).</summary>
public sealed class StockSupplierTests(ApiFixture fixture)
{
    private const string MigrationBeforeSuppliers = "20261030204958_StaffAppAbsenceRequests";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_Material_mit_Text_Haslauer_When_die_Migration_laeuft_Then_gibt_es_den_Lieferanten_und_das_Material_zeigt_auf_ihn()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        await using var db = new AispaDbContextDesignTimeFactory().CreateDbContext([]);
        db.Database.SetConnectionString(connection);
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(MigrationBeforeSuppliers, Ct);
        await using var sql = new NpgsqlConnection(connection);
        await sql.OpenAsync(Ct);
        var first = await InsertTenantAsync(db, "Erstes Spa");
        var second = await InsertTenantAsync(db, "Zweites Spa");
        await InsertItemAsync(sql, first, "Massageöl", "Haslauer");
        await InsertItemAsync(sql, first, "Körperlotion", " haslauer ");
        await InsertItemAsync(sql, first, "Tuchmaske", "  ");
        await InsertItemAsync(sql, first, "Handschuhe", null);
        await InsertItemAsync(sql, second, "Massageöl", "Haslauer");

        await migrator.MigrateAsync(null, Ct);

        var rows = await ReadRowsAsync(sql, """
            SELECT i."TenantId"::text || '|' || i."Name" || '|' || coalesce(s."Name", '-')
            FROM aispa."StockItems" i LEFT JOIN aispa."StockSuppliers" s ON s."Id" = i."SupplierId" AND s."TenantId" = i."TenantId"
            """);
        var suppliers = await ReadRowsAsync(sql, """SELECT "TenantId"::text FROM aispa."StockSuppliers" """);
        Assert.Equal(
            new[] { $"{first}|Handschuhe|-", $"{first}|Körperlotion|Haslauer", $"{first}|Massageöl|Haslauer", $"{first}|Tuchmaske|-", $"{second}|Massageöl|Haslauer" }
                .Order(StringComparer.Ordinal),
            rows.Order(StringComparer.Ordinal));
        Assert.Equal(1, suppliers.Count(t => t == first.ToString()));
        Assert.Equal(1, suppliers.Count(t => t == second.ToString()));
    }

    [Fact]
    public async Task Given_Massageoel_in_Flaschen_und_Kartons_When_ein_Karton_eingeht_Then_steigt_der_Bestand_um_2000_ml()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(), Ct);

        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 1m, 80m, PackageLevel: StockPackageLevel.Case), Ct);

        var level = await LevelAsync(spa, oil.Id);
        var items = await spa.GetAsync<List<StockItemDto>>("/api/stock/items", Ct);
        Assert.Equal(2000m, level.Quantity);
        Assert.Equal(0.04m, items.Single(i => i.Id == oil.Id).UnitCost);
        Assert.Equal(133, level.TreatmentsCovered);
    }

    [Fact]
    public async Task Packungen_nennen_Menge_und_Bezeichnung_je_Ebene()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(), Ct);

        Assert.Equal(
            [(StockPackageLevel.Unit, "ml", 1m), (StockPackageLevel.Pack, "Flasche (500 ml)", 500m), (StockPackageLevel.Case, "Karton (4 × 500 ml)", 2000m)],
            oil.Packages!.Select(p => (p.Level, p.Label, p.BaseQuantity)));
    }

    [Fact]
    public async Task Given_Material_mit_Lieferant_When_ohne_Lieferant_gespeichert_Then_ist_die_Zuordnung_weg()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var created = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Tuchmaske", "Stück", 2m, 0m, null, Supplier: "Haslauer"), Ct);
        Assert.NotNull(created.SupplierId);

        await spa.PutAsync<StockItemDto>($"/api/stock/items/{created.Id}", new StockItemInput("Tuchmaske", "Stück", 2m, 0m, null), Ct);

        var item = (await spa.GetAsync<List<StockItemDto>>("/api/stock/items", Ct)).Single(i => i.Id == created.Id);
        Assert.Null(item.SupplierId);
        Assert.Null(item.Supplier);
    }

    [Fact]
    public async Task Given_deaktivierter_Lieferant_When_einem_anderen_Material_zugeordnet_Then_abgelehnt_und_bestehende_Zuordnung_bleibt_speicherbar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Haslauer"), Ct);
        var kept = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Tuchmaske", "Stück", 2m, 0m, null, SupplierId: supplier.Id), Ct);
        await spa.PutAsync<StockSupplierDto>($"/api/stock/suppliers/{supplier.Id}", new StockSupplierInput("Haslauer", IsActive: false), Ct);

        var added = await spa.Client.PostAsJsonAsync("/api/stock/items", new StockItemInput("Kerze", "Stück", 2m, 0m, null, SupplierId: supplier.Id), TestSpa.Json, Ct);
        var saved = await spa.PutAsync<StockItemDto>($"/api/stock/items/{kept.Id}", new StockItemInput("Tuchmaske neu", "Stück", 2m, 0m, null, SupplierId: supplier.Id), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        Assert.Equal(supplier.Id, saved.SupplierId);
    }

    [Fact]
    public async Task Given_Kartonpreis_der_nicht_aufgeht_When_gebucht_Then_gilt_der_gerundete_Preis_je_Einheit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(), Ct);

        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 1m, UnitCost: 7.77m, PackageLevel: StockPackageLevel.Case), Ct);

        // 7,77 € je Karton zu 2000 ml sind 0,003885 € je ml, auf vier Stellen 0,0039 €.
        var items = await spa.GetAsync<List<StockItemDto>>("/api/stock/items", Ct);
        Assert.Equal(0.0039m, items.Single(i => i.Id == oil.Id).UnitCost);
    }

    [Fact]
    public async Task Given_Kartons_ueber_der_Mengengrenze_When_gebucht_Then_abgelehnt_statt_Serverfehler()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(), Ct);

        var tooMany = await spa.Client.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 600_000m, PackageLevel: StockPackageLevel.Case), TestSpa.Json, Ct);
        var hugeCase = await spa.Client.PostAsJsonAsync("/api/stock/items",
            new StockItemInput("Fass", "ml", 1m, 0m, null, PackUnit: "Fass", PackSize: 999_999m, CaseUnit: "Palette", PacksPerCase: 10_000), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, hugeCase.StatusCode);
    }

    [Fact]
    public async Task Packungsebene_gilt_nur_beim_Wareneingang_und_nur_wenn_das_Material_sie_kennt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(), Ct);
        var plain = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Tuchmaske", "Stück", 2m, 0m, null), Ct);
        var path = $"/api/stock/locations/{spa.Location.Id}/movements";
        var places = await spa.GetAsync<List<StoragePlaceDto>>($"/api/stock/locations/{spa.Location.Id}/storage-places", Ct);
        var main = places.Single(p => p.Kind == StoragePlaceKind.Main).Id;

        var correction = await spa.Client.PostAsJsonAsync(path,
            new StockMovementInput(oil.Id, StockMovementKind.Correction, 1m, Reason: "Fund", StoragePlaceId: main, PackageLevel: StockPackageLevel.Case), TestSpa.Json, Ct);
        var unknown = await spa.Client.PostAsJsonAsync(path, new StockMovementInput(plain.Id, StockMovementKind.GoodsReceipt, 1m, PackageLevel: StockPackageLevel.Case), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, correction.StatusCode);
        Assert.Contains("packageLevel", await correction.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Unvollstaendige_Packung_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var noSize = await spa.Client.PostAsJsonAsync("/api/stock/items", new StockItemInput("A", "ml", 1m, 0m, null, PackUnit: "Flasche"), TestSpa.Json, Ct);
        var caseOnly = await spa.Client.PostAsJsonAsync("/api/stock/items", new StockItemInput("B", "ml", 1m, 0m, null, CaseUnit: "Karton", PacksPerCase: 4), TestSpa.Json, Ct);
        var warning = await spa.Client.PostAsJsonAsync("/api/stock/items", new StockItemInput("C", "ml", 1m, 0m, null, ExpiryWarningDays: 0), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, noSize.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, caseOnly.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, warning.StatusCode);
    }

    [Fact]
    public async Task Lieferant_anlegen_aendern_und_Material_zuordnen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers",
            new StockSupplierInput("Haslauer", "Eva Muster", "bestellung@haslauer.example", "+43 1 234", 5, 150m, SupplierOrderChannel.Portal), Ct);
        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.04m, 0m, null, SupplierId: supplier.Id), Ct);
        var changed = await spa.PutAsync<StockSupplierDto>($"/api/stock/suppliers/{supplier.Id}",
            new StockSupplierInput("Haslauer KG", "Eva Muster", "bestellung@haslauer.example", null, 7, null, SupplierOrderChannel.Phone, false), Ct);
        var items = await spa.GetAsync<List<StockItemDto>>("/api/stock/items", Ct);

        Assert.Equal(("Haslauer", 5, 150m, SupplierOrderChannel.Portal, true), (supplier.Name, supplier.LeadTimeDays, supplier.MinimumOrderValue, supplier.OrderChannel, supplier.IsActive));
        Assert.Equal(("Haslauer", supplier.Id), (item.Supplier, item.SupplierId));
        Assert.Equal(("Haslauer KG", 7, false), (changed.Name, changed.LeadTimeDays, changed.IsActive));
        Assert.Equal("Haslauer KG", items.Single(i => i.Id == item.Id).Supplier);
    }

    [Fact]
    public async Task Name_als_Text_findet_den_Lieferanten_ohne_Gross_und_Kleinschreibung_oder_legt_ihn_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var first = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Öl", "ml", 1m, 0m, null, Supplier: "Aroma GmbH"), Ct);
        var second = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Salz", "g", 1m, 0m, null, Supplier: " aroma gmbh "), Ct);
        var suppliers = await spa.GetAsync<List<StockSupplierDto>>("/api/stock/suppliers", Ct);

        var only = Assert.Single(suppliers);
        Assert.Equal("Aroma GmbH", only.Name);
        Assert.Equal((only.Id, only.Id), (first.SupplierId, second.SupplierId));
    }

    [Fact]
    public async Task Doppelter_Lieferant_und_falsche_Angaben_werden_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Haslauer"), Ct);

        var duplicate = await spa.Client.PostAsJsonAsync("/api/stock/suppliers", new StockSupplierInput(" HASLAUER "), TestSpa.Json, Ct);
        var mail = await spa.Client.PostAsJsonAsync("/api/stock/suppliers", new StockSupplierInput("Neu", Email: "keine-adresse"), TestSpa.Json, Ct);
        var leadTime = await spa.Client.PostAsJsonAsync("/api/stock/suppliers", new StockSupplierInput("Neu", LeadTimeDays: -1), TestSpa.Json, Ct);
        var unknown = await spa.Client.PostAsJsonAsync("/api/stock/items", new StockItemInput("Öl", "ml", 1m, 0m, null, SupplierId: Guid.NewGuid()), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, leadTime.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Given_Charge_laeuft_in_20_Tagen_ab_When_die_Leitung_die_Aufgaben_oeffnet_Then_steht_sie_mit_Menge_und_Lagerort_in_Bald_ablaufend()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(), Ct);
        await ReceiveAsync(spa, oil.Id, 300m, "L-20", 20);

        var tasks = await TasksAsync(spa);

        var task = Assert.Single(tasks.Expiring);
        Assert.Equal(("Massageöl", "L-20", 300m, "ml"), (task.ItemName, task.Batch, task.Quantity, task.Unit));
        Assert.False(string.IsNullOrEmpty(task.StoragePlaceName));
        Assert.InRange(task.DaysLeft, 19, 21);
    }

    [Fact]
    public async Task Frist_gilt_je_Material()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var shortWarning = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Kurz", "Stück", 1m, 0m, null, ExpiryWarningDays: 10), Ct);
        var longWarning = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Lang", "Stück", 1m, 0m, null, ExpiryWarningDays: 60), Ct);
        await ReceiveAsync(spa, shortWarning.Id, 1m, "K", 20);
        await ReceiveAsync(spa, longWarning.Id, 1m, "L", 45);

        var tasks = await TasksAsync(spa);

        Assert.Equal(["Lang"], tasks.Expiring.Select(t => t.ItemName));
    }

    [Fact]
    public async Task Abverkaufsliste_nennt_nur_aktive_Kassa_Artikel_und_nie_Abgelaufenes()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var accounts = await spa.GetAsync<List<RevenueAccountDto>>("/api/pos/revenue-accounts", Ct);
        var article = await spa.PostAsync<ArticleDto>("/api/pos/articles", new ArticleInput("Körperlotion", 29m, TaxCategory.Normal, accounts[0].Id), Ct);
        var lotion = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Lotion", "Stück", 11m, 0m, article.Id), Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", Oil(), Ct);
        await ReceiveAsync(spa, lotion.Id, 4m, "L-1", 20);
        await ReceiveAsync(spa, lotion.Id, 2m, "L-0", -3);
        await ReceiveAsync(spa, oil.Id, 100m, "O-1", 20);

        var tasks = await TasksAsync(spa);

        var sell = Assert.Single(tasks.SellOff);
        Assert.Equal(("L-1", "Körperlotion", 29m), (sell.Batch, sell.ArticleName, sell.SalePrice));
        Assert.Equal(["L-0"], tasks.Expired.Select(t => t.Batch));
        Assert.Equal(2, tasks.Expiring.Count);
    }

    [Fact]
    public async Task Rezeption_sieht_weder_Lieferanten_noch_Aufgaben()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var list = await reception.GetAsync("/api/stock/suppliers", Ct);
        var create = await reception.PostAsJsonAsync("/api/stock/suppliers", new StockSupplierInput("Haslauer"), TestSpa.Json, Ct);
        var tasks = await reception.GetAsync($"/api/stock/locations/{spa.Location.Id}/expiry-tasks", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tasks.StatusCode);
    }

    private static StockItemInput Oil() => new("Massageöl", "ml", 0.05m, 0m, null, PackUnit: "Flasche", PackSize: 500m, CaseUnit: "Karton", PacksPerCase: 4, TreatmentDose: 15m);

    private static Task<List<StockMovementDto>> ReceiveAsync(TestSpa spa, Guid itemId, decimal quantity, string batch, int daysFromToday) =>
        spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(itemId, StockMovementKind.GoodsReceipt, quantity, Batch: batch, ExpiresOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromToday)), Ct);

    private static Task<StockExpiryTasksDto> TasksAsync(TestSpa spa) =>
        spa.GetAsync<StockExpiryTasksDto>($"/api/stock/locations/{spa.Location.Id}/expiry-tasks", Ct);

    private static async Task<StockLevelDto> LevelAsync(TestSpa spa, Guid itemId)
    {
        var overview = await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct);
        return overview.Items.Single(l => l.ItemId == itemId);
    }

    private static async Task<Guid> InsertTenantAsync(AispaDbContext db, string name)
    {
        var tenant = new Tenant { Name = name, Country = "AT" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(Ct);
        return tenant.Id;
    }

    private static async Task InsertItemAsync(NpgsqlConnection sql, Guid tenantId, string name, string? supplier)
    {
        await using var insert = new NpgsqlCommand("""
            INSERT INTO aispa."StockItems" ("Id", "Name", "Unit", "UnitCost", "MinimumQuantity", "IsActive", "TenantId", "Supplier")
            VALUES (@id, @name, 'ml', 0, 0, TRUE, @tenant, @supplier)
            """, sql);
        insert.Parameters.AddWithValue("id", Guid.CreateVersion7());
        insert.Parameters.AddWithValue("name", name);
        insert.Parameters.AddWithValue("tenant", tenantId);
        insert.Parameters.AddWithValue("supplier", (object?)supplier ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<List<string>> ReadRowsAsync(NpgsqlConnection sql, string query)
    {
        await using var command = new NpgsqlCommand(query, sql);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
