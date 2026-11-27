using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// ADR 0098 (T-061): Das Lager hat vier Reiter. Chargen und Bewegungen sind Sichten im Bestand, Lagerorte und Lieferanten stehen in der Einrichtung, der Verbrauch ist ein Abschnitt der
/// Übersicht. Jeder alte Wert von <see cref="StockTab"/> bleibt ein Sprungziel und landet am neuen Ort; die Rechte je Inhalt sind dieselben wie vorher. Das Aussehen steht in <c>ViewTests.StockLayout</c>.
/// </summary>
public sealed class StockLayoutTests
{
    private static readonly DateOnly Today = new(2027, 3, 1);

    private static async Task<StockViewModel> Open(StubApi api, CurrentUser? user = null)
    {
        var stock = new StockViewModel(api.Client(), user ?? TestData.Owner(), new FixedTime());
        await stock.EnsureLoadedAsync();
        return stock;
    }

    /// <summary>Vier Positionen im Bestellvorschlag und eine Charge Massageöl, die am 10.3.2027 abläuft.</summary>
    private static StubApi BusyApi() => Api()
        .On($"GET {Route("/reorder")}", Enumerable.Range(1, 4).Select(n => OilSuggestion with { ItemId = Guid.NewGuid(), ItemName = $"Material {n}" }).ToList())
        .On($"GET {Route("/batches")}", new StockBatchesDto(Vienna.Id, Today, 14,
            [new StockBatchDto(OilId, "Massageöl", "Flasche", "L-9", new DateOnly(2027, 3, 10), 3m, default, false, true, MainId, "Hauptlager")]));

    private static CurrentUser WithRights(params AccessRight[] rights) =>
        new("u9", "recht@aispa.test", "Rolf", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, true, []), null, rights);

    /// <summary>Der Ort, den die Maske zeigt: Reiter der Schiene, im Bestand mit der Sicht, oder die Einrichtung mit ihrem Inhalt.</summary>
    private static string Place(StockViewModel stock) => stock switch
    {
        { IsSetup: true } => stock.IsPlacesSetup ? "Einrichtung/Lagerorte" : stock.IsSuppliersSetup ? "Einrichtung/Lieferanten" : "Einrichtung",
        { IsOverviewTab: true } => "Zu tun",
        { IsStockTab: true } => stock.IsItemsView ? "Bestand/Material" : stock.IsBatchesView ? "Bestand/Chargen" : stock.IsMovementsView ? "Bestand/Bewegungen" : "Bestand",
        { IsOrdersTab: true } => "Bestellen",
        { IsCountTab: true } => "Inventur",
        _ => "nirgends",
    };

    [Theory]
    [InlineData(StockTab.Overview, "Zu tun")]
    [InlineData(StockTab.Usage, "Zu tun")]
    [InlineData(StockTab.Stock, "Bestand/Material")]
    [InlineData(StockTab.Tasks, "Bestand/Chargen")]
    [InlineData(StockTab.Movements, "Bestand/Bewegungen")]
    [InlineData(StockTab.Orders, "Bestellen")]
    [InlineData(StockTab.Count, "Inventur")]
    [InlineData(StockTab.Places, "Einrichtung/Lagerorte")]
    [InlineData(StockTab.Suppliers, "Einrichtung/Lieferanten")]
    public async Task Given_ein_altes_Sprungziel_When_die_Inhaberin_es_anspringt_Then_landet_sie_am_neuen_Ort(StockTab target, string expected)
    {
        var stock = await Open(Api());
        stock.SetTab(target == StockTab.Orders ? StockTab.Count : StockTab.Orders);

        stock.SetTab(target);

        Assert.Equal(expected, Place(stock));
        Assert.Equal(1, new[] { stock.IsOverviewTab, stock.IsStockTab, stock.IsOrdersTab, stock.IsCountTab, stock.IsSetup }.Count(shown => shown));
        Assert.Equal(1, new[] { stock.IsItemsView, stock.IsBatchesView, stock.IsMovementsView }.Count(shown => shown) + (stock.IsStockTab ? 0 : 1));
    }

    [Fact]
    public void Given_die_Orte_des_Lagers_When_gezaehlt_Then_bleiben_alle_neun_Sprungadressen_bestehen()
    {
        Assert.Equal(
            ["Count", "Movements", "Orders", "Overview", "Places", "Stock", "Suppliers", "Tasks", "Usage"],
            Enum.GetNames<StockTab>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_das_Sprungziel_Verbrauch_When_angesprungen_Then_zeigt_das_Lager_die_Uebersicht_und_verlangt_den_Abschnitt_einmal()
    {
        var stock = await Open(Api());
        stock.SetTab(StockTab.Stock);
        var requested = 0;
        stock.UsageRequested += () => requested++;

        stock.SetTab(StockTab.Usage);

        Assert.Equal(StockTab.Overview, stock.Tab);
        Assert.Equal(1, requested);
        Assert.True(stock.TakeUsageRequest());
        Assert.False(stock.TakeUsageRequest());
    }

    [Fact]
    public async Task Given_die_Inhaberin_When_das_Lager_oeffnet_Then_laedt_die_Uebersicht_den_Verbrauch_einmal_und_zeigt_den_Mehrverbrauch_als_Kachel()
    {
        var api = Api();

        var stock = await Open(api);

        Assert.True(stock.CanViewUsage);
        Assert.Equal(1, api.Calls.Count(c => c.Route == $"GET {UsageRoute(Today, 30)}"));
        Assert.Equal("Massageöl", Assert.Single(stock.Usage.Rows).Material);
        var tile = Assert.Single(stock.Todo.Stats, s => s.Label == "Mehrverbrauch");
        Assert.Equal(("25,00 €", "Letzte 30 Tage"), (tile.Value, tile.Hint));

        stock.SetTab(StockTab.Stock);
        tile.Command!.Execute(null);
        Assert.Equal(StockTab.Overview, stock.Tab);
        Assert.True(stock.TakeUsageRequest());
    }

    [Fact]
    public async Task Given_ein_anderer_Zeitraum_When_der_Verbrauch_neu_laedt_Then_nennt_die_Kachel_den_Zeitraum()
    {
        var stock = await Open(Api());

        stock.Usage.SelectedPeriod = stock.Usage.Periods[2];
        await stock.Usage.LoadAsync();

        Assert.Equal("Letzte 90 Tage", stock.Todo.Stats.Single(s => s.Label == "Mehrverbrauch").Hint);
    }

    [Fact]
    public async Task Given_ohne_das_Recht_ViewFinance_When_das_Lager_oeffnet_und_Verbrauch_angesprungen_wird_Then_bleibt_der_Ort_und_nichts_wird_geladen()
    {
        var api = Api();
        var stock = await Open(api, WithRights(AccessRight.Book, AccessRight.ManageLocation, AccessRight.ManageCatalog));
        stock.SetTab(StockTab.Stock);

        stock.SetTab(StockTab.Usage);
        stock.SetTab(StockTab.Overview);

        Assert.False(stock.CanViewUsage);
        Assert.Equal(StockTab.Overview, stock.Tab);
        Assert.False(stock.TakeUsageRequest());
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("stock-usage", StringComparison.Ordinal));
        Assert.DoesNotContain(stock.Todo.Stats, s => s.Label == "Mehrverbrauch");
    }

    [Fact]
    public async Task Given_vier_Positionen_und_eine_ablaufende_Charge_When_das_Lager_oeffnet_Then_nennen_die_Kacheln_die_Zahlen_und_fuehren_an_ihren_Ort()
    {
        var stock = await Open(BusyApi());

        Assert.Equal(["Lagerwert", "Nachbestellen", "Unter Minimum", "Chargen laufen ab", "Mehrverbrauch"], stock.Todo.Stats.Select(s => s.Label));
        var value = stock.Todo.Stats[0];
        Assert.Equal(("140,00 €", "am Standort", StatTileState.None), (value.Value, value.Hint, value.State));
        Assert.Null(value.Command);
        var reorder = stock.Todo.Stats[1];
        var below = stock.Todo.Stats[2];
        var batches = stock.Todo.Stats[3];
        Assert.Equal(("4", StatTileState.Warn), (reorder.Value, reorder.State));
        Assert.Equal(("1", StatTileState.Warn), (below.Value, below.State));
        Assert.Equal(("1", StatTileState.Warn), (batches.Value, batches.State));

        reorder.Command!.Execute(null);
        Assert.Equal(StockTab.Orders, stock.Tab);
        below.Command!.Execute(null);
        Assert.Equal(StockTab.Stock, stock.Tab);
        Assert.True(stock.Overview.OnlyBelow);
        Assert.Equal(TowelId, Assert.Single(stock.Overview.Rows).ItemId);
        batches.Command!.Execute(null);
        Assert.Equal(StockTab.Tasks, stock.Tab);
    }

    [Fact]
    public async Task Given_nichts_laeuft_ab_When_das_Lager_oeffnet_Then_steht_die_Null_ohne_Warnzeichen()
    {
        var stock = await Open(Api());

        var batches = stock.Todo.Stats.Single(s => s.Label == "Chargen laufen ab");

        Assert.Equal(("0", StatTileState.None), (batches.Value, batches.State));
    }

    [Fact]
    public async Task Given_die_Rezeption_When_das_Lager_oeffnet_Then_fehlen_Bestellkachel_und_Einrichtung_und_die_Chargen_Kachel_fuehrt_nirgends_hin()
    {
        var stock = await Open(BusyApi(), TestData.Reception());

        Assert.DoesNotContain(stock.Todo.Stats, s => s.Label is "Nachbestellen" or "Mehrverbrauch");
        Assert.Null(stock.Todo.Stats.Single(s => s.Label == "Chargen laufen ab").Command);
        Assert.False(stock.CanSetup);
        Assert.False(stock.ShowSetupButton);
        stock.OpenSetupCommand.Execute(null);
        Assert.False(stock.IsSetup);
    }

    [Fact]
    public async Task Given_die_Inhaberin_im_Bestand_When_sie_die_Einrichtung_oeffnet_und_Zurueck_waehlt_Then_steht_sie_wieder_im_Bestand()
    {
        var stock = await Open(Api());
        stock.SetTab(StockTab.Movements);
        Assert.True(stock.ShowSetupButton);

        stock.OpenSetupCommand.Execute(null);

        Assert.Equal("Einrichtung/Lagerorte", Place(stock));
        Assert.True(stock.HasSetupChoice);
        Assert.False(stock.ShowSetupButton);
        Assert.False(stock.ShowSearch);
        Assert.False(stock.ShowBookButton);
        stock.ShowSuppliersCommand.Execute(null);
        Assert.Equal("Einrichtung/Lieferanten", Place(stock));

        stock.CloseSetupCommand.Execute(null);

        Assert.Equal("Bestand/Bewegungen", Place(stock));
        Assert.True(stock.ShowSearch);
    }

    [Fact]
    public async Task Given_nur_das_Katalogrecht_When_die_Einrichtung_oeffnet_Then_zeigt_sie_die_Lieferanten_ohne_Auswahl_und_Lagerorte_bleiben_gesperrt()
    {
        var stock = await Open(Api(), WithRights(AccessRight.Book, AccessRight.ManageCatalog));

        stock.OpenSetupCommand.Execute(null);

        Assert.Equal("Einrichtung/Lieferanten", Place(stock));
        Assert.False(stock.HasSetupChoice);
        stock.SetTab(StockTab.Places);
        Assert.Equal("Einrichtung/Lieferanten", Place(stock));
    }

    [Fact]
    public async Task Given_nur_das_Recht_fuer_den_Standort_When_die_Einrichtung_oeffnet_Then_zeigt_sie_die_Lagerorte_und_Lieferanten_bleiben_gesperrt()
    {
        var stock = await Open(Api(), WithRights(AccessRight.Book, AccessRight.ManageLocation));

        stock.OpenSetupCommand.Execute(null);

        Assert.Equal("Einrichtung/Lagerorte", Place(stock));
        Assert.False(stock.HasSetupChoice);
        stock.SetTab(StockTab.Suppliers);
        Assert.Equal("Einrichtung/Lagerorte", Place(stock));
        stock.CloseSetupCommand.Execute(null);
        Assert.Equal("Zu tun", Place(stock));
    }

    [Fact]
    public async Task Given_die_Zahl_offener_Chargen_When_eine_andere_Sicht_gewaehlt_ist_Then_nennt_die_Sicht_Chargen_sie_weiter()
    {
        var stock = await Open(Api());

        stock.SetTab(StockTab.Movements);

        Assert.Equal("Chargen (2)", stock.BatchesViewLabel);
    }
}
