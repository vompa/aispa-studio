using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Listen des Lagers als Excel (Export/Import, Aufgabe D): Bestand je Lagerort, Inventur mit Differenzen, Bewegungen eines Zeitraums, Bestellungen und Ablaufliste;
/// nur für die Leitung, ein fremder Mandant sieht nichts. Lagerbewegungen entstehen mit der echten Uhr, daher filtern die Tests auf heute.
/// </summary>
public sealed class StockExportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly ViennaToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    /// <summary>500 ml Massageöl, Charge A1 mit Ablauf in 30 Tagen, im Hauptlager; eine eingereichte Zählung mit 480 ml und eine Bestellung über 1000 ml.</summary>
    private async Task<TestSpa> StockAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var basePath = $"/api/stock/locations/{spa.Location.Id}";
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.04m, 100m, null), Ct);
        var main = (await spa.GetAsync<List<StoragePlaceDto>>($"{basePath}/storage-places", Ct)).Single(p => p.Kind == StoragePlaceKind.Main);
        _ = await spa.PostAsync<List<StockMovementDto>>($"{basePath}/movements",
            new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 500m, Batch: "A1", ExpiresOn: ViennaToday.AddDays(30)), Ct);
        var session = await TestSpa.ReadAsync<StockCountSessionDto>(await spa.Client.PostAsync($"{basePath}/storage-places/{main.Id}/count-sessions", null, Ct), Ct);
        (await spa.Client.PutAsJsonAsync($"{basePath}/count-sessions/{session.Id}/lines/{oil.Id}", new StockCountLineInput(480m), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        _ = await TestSpa.ReadAsync<StockCountSessionDto>(await spa.Client.PostAsync($"{basePath}/count-sessions/{session.Id}/submit", null, Ct), Ct);
        _ = await spa.PostAsync<PurchaseOrderDto>($"{basePath}/orders", new PurchaseOrderInput("Lieferant Nord", [new PurchaseOrderLineInput(oil.Id, 1000m, 0.04m)]), Ct);
        return spa;
    }

    private static string Path(TestSpa spa, string list) =>
        $"/api/stock/locations/{spa.Location.Id}/export?list={list}&from={ViennaToday:yyyy-MM-dd}&until={ViennaToday:yyyy-MM-dd}&format=xlsx";

    [Fact]
    public async Task Bestand_je_Lagerort_Inventur_und_Ablaufliste()
    {
        var spa = await StockAsync();

        using var levels = await spa.Client.GetAsync(Path(spa, "levels"), Ct);
        using var count = await spa.Client.GetAsync(Path(spa, "count"), Ct);
        using var expiry = await spa.Client.GetAsync(Path(spa, "expiry"), Ct);
        var levelSheet = await ExportResponses.SheetAsync(levels, Ct);
        var countSheet = await ExportResponses.SheetAsync(count, Ct);
        var expirySheet = await ExportResponses.SheetAsync(expiry, Ct);

        Assert.Equal($"Lagerbestand_{ViennaToday:yyyy-MM-dd}.xlsx", ExportResponses.FileName(levels));
        Assert.Equal(["Material", "Lagerort", "Menge", "Einheit", "Mindestbestand", "Unter Mindestbestand"], ExportResponses.Headers(levelSheet));
        var level = levelSheet.Row(2);
        Assert.Equal(("Massageöl", 500d, "ml", 100d, "Nein"), (level.Cell(1).GetString(), level.Cell(3).GetDouble(), level.Cell(4).GetString(), level.Cell(5).GetDouble(),
            level.Cell(6).GetString()));
        Assert.False(string.IsNullOrWhiteSpace(level.Cell(2).GetString()));

        Assert.Equal(["Material", "Lagerort", "Einheit", "Bestand", "Gezählt", "Differenz"], ExportResponses.Headers(countSheet));
        var line = countSheet.Row(2);
        Assert.Equal(("Massageöl", 500d, 480d, -20d), (line.Cell(1).GetString(), line.Cell(4).GetDouble(), line.Cell(5).GetDouble(), line.Cell(6).GetDouble()));

        Assert.Equal(["Material", "Charge", "Ablaufdatum", "Menge", "Einheit", "Lagerort", "Abgelaufen"], ExportResponses.Headers(expirySheet));
        var batch = expirySheet.Row(2);
        Assert.Equal(("A1", ViennaToday.AddDays(30).ToDateTime(TimeOnly.MinValue), 500d, "Nein"),
            (batch.Cell(2).GetString(), batch.Cell(3).GetDateTime(), batch.Cell(4).GetDouble(), batch.Cell(7).GetString()));
    }

    [Fact]
    public async Task Bewegungen_des_Zeitraums_und_Bestellungen()
    {
        var spa = await StockAsync();

        using var movements = await spa.Client.GetAsync(Path(spa, "movements"), Ct);
        using var yesterday = await spa.Client.GetAsync(
            $"/api/stock/locations/{spa.Location.Id}/export?list=movements&from={ViennaToday.AddDays(-2):yyyy-MM-dd}&until={ViennaToday.AddDays(-1):yyyy-MM-dd}&format=xlsx", Ct);
        using var orders = await spa.Client.GetAsync(Path(spa, "orders"), Ct);
        var movementSheet = await ExportResponses.SheetAsync(movements, Ct);
        var emptySheet = await ExportResponses.SheetAsync(yesterday, Ct);
        var orderSheet = await ExportResponses.SheetAsync(orders, Ct);

        Assert.Equal(["Tag", "Zeit", "Material", "Lagerort", "Art", "Menge", "Einheit", "Charge", "Grund"], ExportResponses.Headers(movementSheet));
        var receipt = movementSheet.Row(2);
        Assert.Equal(("Massageöl", "Wareneingang", 500d, "A1"), (receipt.Cell(3).GetString(), receipt.Cell(5).GetString(), receipt.Cell(6).GetDouble(), receipt.Cell(8).GetString()));
        Assert.Equal(1, emptySheet.LastRowUsed()!.RowNumber());

        Assert.Equal(["Nummer", "Lieferant", "Status", "Angelegt", "Bestellt", "Positionen", "Summe netto"], ExportResponses.Headers(orderSheet));
        var order = orderSheet.Row(2);
        Assert.Equal(("Lieferant Nord", "Entwurf", 1d, 40d), (order.Cell(2).GetString(), order.Cell(3).GetString(), order.Cell(6).GetDouble(), order.Cell(7).GetDouble()));
        Assert.Equal(2, orderSheet.LastRowUsed()!.RowNumber());

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Contains(history, h => h.Text == $"Export Lagerbewegungen (Excel, 1 Zeile, {ViennaToday:d.M.yyyy} bis {ViennaToday:d.M.yyyy}) von Test Person");
    }

    [Fact]
    public async Task Nur_die_Leitung_und_nur_der_eigene_Mandant()
    {
        var spa = await StockAsync();
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var stranger = await TestUsers.ClientAsync(fixture, Ct);
        var fresh = await TestSpa.CreateAsync(fixture, Ct);

        using var forManager = await manager.GetAsync(Path(spa, "levels"), Ct);
        using var forStranger = await stranger.GetAsync(Path(spa, "levels"), Ct);
        using var noCount = await fresh.Client.GetAsync(Path(fresh, "count"), Ct);
        using var noRange = await spa.Client.GetAsync($"/api/stock/locations/{spa.Location.Id}/export?list=movements&format=csv", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forManager.StatusCode);
        Assert.Contains(forStranger.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        Assert.Equal(HttpStatusCode.BadRequest, noCount.StatusCode);
        Assert.Contains("Keine eingereichte", await noCount.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, noRange.StatusCode);
    }
}
