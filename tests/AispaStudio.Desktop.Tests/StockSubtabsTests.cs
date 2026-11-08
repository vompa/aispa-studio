using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R07 Slice 143 (Ä351): Das Lager teilt sich in Übersicht, Bestand, Bestellen, Chargen und Bewegungen. Die Übersicht sagt, was zu tun ist; der Bestand hat vier Spalten, die übrigen Werte
/// stehen im Seitenpanel der Zeile. Das ViewModel prüft Inhalt, Links, Rechte und die Suche; das Aussehen steht in <c>ViewTests.StockSubtabs</c>.
/// </summary>
public sealed class StockSubtabsTests
{
    private const string Ean = "9001234567890";

    private static readonly Guid CandleId = Guid.Parse("00000000-0000-0000-0000-0000000c1181");

    private static async Task<StockViewModel> Open(StubApi api, CurrentUser? user = null)
    {
        var stock = new StockViewModel(api.Client(), user ?? TestData.Owner(), new FixedTime());
        await stock.EnsureLoadedAsync();
        return stock;
    }

    /// <summary>Vier Positionen im Bestellvorschlag und eine Charge Massageöl, die am 10.3.2027 abläuft (die Uhr der ViewModel-Tests steht auf dem 1.3.2027).</summary>
    private static StubApi BusyApi() => Api()
        .On($"GET {Route("/reorder")}", Enumerable.Range(1, 4).Select(n => OilSuggestion with { ItemId = Guid.NewGuid(), ItemName = $"Material {n}" }).ToList())
        .On($"GET {Route("/batches")}", new StockBatchesDto(Vienna.Id, new DateOnly(2027, 3, 1), 14,
            [new StockBatchDto(OilId, "Massageöl", "Flasche", "L-9", new DateOnly(2027, 3, 10), 3m, default, false, true, MainId, "Hauptlager")]));

    /// <summary>Kosmetiktücher: 50 Stück bei 10 Mindestbestand, aber nur noch für 2 Behandlungen; Öl: 10 Flaschen bei 2 Mindestbestand.</summary>
    private static StubApi CoverageApi() => Api().On($"GET {Route()}", new StockOverviewDto(Vienna.Id, 14, 100m,
    [
        new StockLevelDto(OilId, "Massageöl", "Flasche", 10, 2, 125m, 3, 0, false, TreatmentsCovered: 80),
        new StockLevelDto(CandleId, "Einweg-Kosmetiktücher", "Stück", 50, 10, 15m, 0, 0, false, TreatmentsCovered: 2),
    ]));

    [Fact]
    public async Task Given_das_Lager_oeffnet_When_geladen_Then_ist_die_Uebersicht_der_erste_Reiter()
    {
        var stock = await Open(Api());

        Assert.Equal(StockTab.Overview, stock.Tab);
        Assert.True(stock.IsOverviewTab);
    }

    [Fact]
    public async Task Given_4_Positionen_im_Bestellvorschlag_und_eine_ablaufende_Charge_When_das_Lager_oeffnet_Then_zeigt_die_Uebersicht_beide_Punkte_mit_Link()
    {
        var stock = await Open(BusyApi());

        var reorder = Assert.Single(stock.Todo.Items, i => i.Title == "4 Positionen nachbestellen");
        Assert.Equal("Bestellen öffnen", reorder.LinkText);
        var expiring = Assert.Single(stock.Todo.Items, i => i.Title == "1 Charge läuft bald ab");
        Assert.Contains("Massageöl", expiring.Detail, StringComparison.Ordinal);
        Assert.Contains("10.3.2027", expiring.Detail, StringComparison.Ordinal);
        Assert.Equal("Chargen öffnen", expiring.LinkText);

        reorder.LinkCommand!.Execute(null);
        Assert.Equal(StockTab.Orders, stock.Tab);
        stock.SetTab(StockTab.Overview);
        expiring.LinkCommand!.Execute(null);
        Assert.Equal(StockTab.Tasks, stock.Tab);
    }

    [Fact]
    public async Task Given_nichts_zu_tun_When_das_Lager_oeffnet_Then_sagt_die_Uebersicht_es_und_der_Lagerwert_steht_klein_darunter()
    {
        var api = Api()
            .On($"GET {Route("/reorder")}", new List<ReorderSuggestionDto>())
            .On($"GET {Route()}", new StockOverviewDto(Vienna.Id, 14, 987.5m, [new StockLevelDto(OilId, "Massageöl", "Flasche", 10, 2, 125m, 3, 0, false, TreatmentsCovered: 80)]));

        var stock = await Open(api);

        Assert.Empty(stock.Todo.Items);
        Assert.True(stock.Todo.IsEmpty);
        Assert.Contains("987,50", stock.Todo.ValueText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_Einweg_Kosmetiktuecher_reichen_fuer_2_Behandlungen_When_Bestand_oeffnet_Then_steht_Reicht_fuer_2_Behandlungen_und_der_Chip_nachbestellen()
    {
        var stock = await Open(CoverageApi());
        stock.SetTab(StockTab.Stock);

        var candles = stock.Overview.Rows.Single(r => r.ItemId == CandleId);
        Assert.Equal("2 Behandlungen", candles.ReachText);
        Assert.Equal("nachbestellen", candles.StatusText);
        Assert.Contains("reicht für 2 Behandlungen", candles.Summary, StringComparison.Ordinal);
        var oil = stock.Overview.Rows.Single(r => r.ItemId == OilId);
        Assert.Equal("80 Behandlungen", oil.ReachText);
        Assert.Equal("", oil.StatusText);
        var todo = Assert.Single(stock.Todo.Items, i => i.Title.StartsWith("1 Material reicht", StringComparison.Ordinal));
        Assert.Contains("Einweg-Kosmetiktücher (2)", todo.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_Lagerort_ist_gewaehlt_When_Kosmetiktuecher_dort_fuer_2_Behandlungen_reichen_Then_steht_Reicht_fuer_und_der_Chip_nachbestellen()
    {
        var api = Api().On($"GET {Route($"/storage-places/{ShopId}/levels")}", new StoragePlaceLevelsDto(ShopId, 20m,
        [
            new StoragePlaceLevelDto(CandleId, "Einweg-Kosmetiktücher", "Stück", 50, null, false, 15m, null, [], TreatmentsCovered: 2),
            new StoragePlaceLevelDto(OilId, "Massageöl", "Flasche", 1, null, false, 5m, null, []),
        ]));
        var stock = await Open(api);
        stock.SetTab(StockTab.Stock);

        stock.SelectedPlace = stock.PlaceFilter.Single(p => p.Id == ShopId);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        var candles = stock.Overview.Rows.Single(r => r.ItemId == CandleId);
        Assert.Equal("2 Behandlungen", candles.ReachText);
        Assert.Equal("nachbestellen", candles.StatusText);
        Assert.Equal("", stock.Overview.Rows.Single(r => r.ItemId == OilId).StatusText);
    }

    [Fact]
    public async Task Given_ein_Material_unter_dem_Mindestbestand_When_Bestand_oeffnet_Then_zeigt_der_Balken_den_Anteil_am_doppelten_Mindestbestand_und_der_Chip_nachbestellen_steht()
    {
        var stock = await Open(Api());

        var towel = stock.Overview.Rows.Single(r => r.ItemId == TowelId);
        Assert.True(towel.HasBar);
        Assert.Equal(0.25, towel.Fill, 3);
        Assert.Equal("nachbestellen", towel.StatusText);
        var oil = stock.Overview.Rows.Single(r => r.ItemId == OilId);
        Assert.Equal(1.0, oil.Fill, 3);
    }

    [Fact]
    public async Task Given_eine_Zeile_gewaehlt_When_das_Seitenpanel_zeigt_Then_stehen_die_uebrigen_Werte_dort()
    {
        var stock = await Open(Api());

        stock.Overview.Select(OilId);

        var details = stock.Overview.SelectedRow!.Details.ToDictionary(d => d.Label, d => d.Value);
        Assert.Equal("Flasche", details["Einheit"]);
        Assert.Equal("2", details["Mindestbestand"]);
        Assert.Equal("3", details["Verbrauch geplant"]);
        Assert.True(details.ContainsKey("Wert"));
        Assert.False(details.ContainsKey("Bestellt"));
        Assert.True(stock.Overview.HasSelection);
    }

    [Fact]
    public async Task Given_man_tippt_in_das_Feld_Material_suchen_oder_scannen_When_kein_Enter_kommt_Then_filtert_der_Bestand()
    {
        var stock = await Open(Api());

        stock.ScanText = "hand";

        Assert.Equal(TowelId, Assert.Single(stock.Overview.Rows).ItemId);
    }

    [Fact]
    public async Task Given_die_Uebersicht_ist_offen_When_man_in_das_Suchfeld_tippt_Then_wechselt_das_Lager_zum_Bestand_damit_man_den_Filter_sieht()
    {
        var stock = await Open(Api());
        Assert.True(stock.IsOverviewTab);

        stock.ScanText = "ö";

        Assert.True(stock.IsStockTab);
        Assert.Equal(OilId, Assert.Single(stock.Overview.Rows).ItemId);
    }

    [Fact]
    public async Task Given_man_scannt_einen_Materialcode_in_das_Suchfeld_When_das_Material_existiert_Then_ist_seine_Zeile_gewaehlt()
    {
        var api = Api().On($"GET /api/stock/items/by-barcode?code={Ean}", Oil with { Barcode = Ean });
        var stock = await Open(api);

        stock.ScanText = Ean;
        await stock.ScanCommand.ExecuteAsync(null);

        Assert.Equal(StockTab.Stock, stock.Tab);
        Assert.Equal(OilId, stock.Overview.SelectedRow?.ItemId);
        Assert.Equal("", stock.ScanText);
        Assert.Equal(2, stock.Overview.Rows.Count);
    }

    [Fact]
    // T-025 (Auftraggeber, 05.11.2026, Ä373): Die Rezeption öffnet „Bestellen“ für die Bedarfsprüfung; Bestellungen bleiben beim Lagerrecht.
    public async Task Given_eine_Rezeptionistin_ohne_Lagerrecht_When_sie_das_Lager_oeffnet_Then_oeffnet_sie_Bestellen_fuer_den_Bedarf_sieht_aber_Chargen_nicht_und_in_der_Uebersicht_nichts_zum_Bestellen()
    {
        var api = BusyApi();
        var stock = await Open(api, TestData.Reception());

        Assert.False(stock.CanManage);
        Assert.True(stock.CanBook);
        stock.SetTab(StockTab.Orders);
        Assert.Equal(StockTab.Orders, stock.Tab);
        stock.SetTab(StockTab.Overview);
        stock.SetTab(StockTab.Tasks);
        Assert.Equal(StockTab.Overview, stock.Tab);
        stock.SetTab(StockTab.Stock);
        Assert.Equal(StockTab.Stock, stock.Tab);
        stock.SetTab(StockTab.Movements);
        Assert.Equal(StockTab.Movements, stock.Tab);
        Assert.DoesNotContain(stock.Todo.Items, i => i.Title.Contains("nachbestellen", StringComparison.Ordinal));
        var expiring = Assert.Single(stock.Todo.Items, i => i.Title == "1 Charge läuft bald ab");
        Assert.Null(expiring.LinkCommand);
        Assert.False(expiring.HasLink);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"GET {Route("/reorder")}");
    }

    [Fact]
    public async Task Given_nur_das_Recht_zu_sehen_When_ein_Code_gescannt_wird_Then_bleibt_die_Eingabe_als_Suche_stehen_und_die_API_wird_nicht_gefragt()
    {
        var api = Api();
        var stock = await Open(api, WithRights(AccessRight.ViewCalendar));

        stock.ScanText = "öl";
        await stock.ScanCommand.ExecuteAsync(null);

        Assert.Equal("öl", stock.ScanText);
        Assert.Equal(OilId, Assert.Single(stock.Overview.Rows).ItemId);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("by-barcode", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_die_Bedarfspruefung_meldet_Hinweise_When_Bestellen_offen_ist_Then_zeigt_sie_eine_Zeile_mit_der_Zahl_statt_der_Textwand()
    {
        var stock = await Open(Api());

        Assert.Equal("1 Hinweis zur Bedarfsprüfung", stock.Demand.WarningTitle);
        Assert.Contains("Massageöl: Bestand 10 Flasche reicht nicht", stock.Demand.WarningText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_Name_im_Suchfeld_der_eine_Zeile_trifft_When_Enter_kommt_Then_bleibt_die_Suche_stehen_und_nichts_wird_gefragt()
    {
        var api = Api().On("GET /api/stock/items/by-barcode?code=hand", null, HttpStatusCode.NotFound);
        var stock = await Open(api);

        stock.ScanText = "hand";
        await stock.ScanCommand.ExecuteAsync(null);

        Assert.Equal("hand", stock.ScanText);
        Assert.Equal(TowelId, Assert.Single(stock.Overview.Rows).ItemId);
        Assert.Null(stock.Error);
    }

    [Fact]
    public async Task Given_ein_Material_mit_abgelaufener_Charge_When_der_Name_der_Zeile_gebildet_wird_Then_steht_abgelaufen_nur_einmal_darin()
    {
        var api = Api().On($"GET {Route("/batches")}", new StockBatchesDto(Vienna.Id, new DateOnly(2027, 3, 1), 14,
            [new StockBatchDto(OilId, "Massageöl", "Flasche", "L-0", new DateOnly(2027, 2, 1), 3m, default, true, true, MainId, "Hauptlager")]));
        var stock = await Open(api);

        var summary = stock.Overview.Rows.Single(r => r.ItemId == OilId).Summary;

        Assert.Equal("abgelaufen", stock.Overview.Rows.Single(r => r.ItemId == OilId).StatusText);
        Assert.Equal(1, summary.Split("abgelaufen").Length - 1);
    }

    [Fact]
    public async Task Given_im_Bestand_ist_ein_Lagerort_gewaehlt_When_der_Link_der_Uebersicht_folgt_Then_zeigt_der_Bestand_den_ganzen_Standort()
    {
        var stock = await Open(Api());
        stock.SetTab(StockTab.Stock);
        stock.SelectedPlace = stock.PlaceFilter.Single(p => p.Id == ShopId);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        stock.SetTab(StockTab.Overview);

        Assert.Single(stock.Todo.Items, i => i.Title.Contains("unter dem Mindestbestand", StringComparison.Ordinal)).LinkCommand!.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Null(stock.SelectedPlace?.Id);
        Assert.Equal(TowelId, Assert.Single(stock.Overview.Rows).ItemId);
    }

    [Fact]
    public async Task Given_die_Chargen_sind_nicht_geladen_When_nichts_anderes_zu_tun_ist_Then_meldet_die_Uebersicht_keine_Ordnung()
    {
        var api = Api()
            .On($"GET {Route("/batches")}", new { }, HttpStatusCode.InternalServerError)
            .On($"GET {Route("/reorder")}", new List<ReorderSuggestionDto>())
            .On($"GET {Route()}", new StockOverviewDto(Vienna.Id, 14, 10m, [new StockLevelDto(OilId, "Massageöl", "Flasche", 10, 2, 125m, 3, 0, false, TreatmentsCovered: 80)]));

        var stock = await Open(api);

        Assert.Empty(stock.Todo.Items);
        Assert.True(stock.Todo.HasGaps);
        Assert.False(stock.Todo.IsEmpty);
    }

    [Fact]
    public async Task Given_ein_Sprung_ohne_Material_When_Open_Then_zeigt_das_Lager_die_Uebersicht()
    {
        var stock = await Open(Api());
        stock.SetTab(StockTab.Movements);

        stock.Open(null, null, null);

        Assert.Equal(StockTab.Overview, stock.Tab);
    }

    private static CurrentUser WithRights(params AccessRight[] rights) =>
        new("u9", "recht@aispa.test", "Rolf", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, true, []), null, rights);
}
