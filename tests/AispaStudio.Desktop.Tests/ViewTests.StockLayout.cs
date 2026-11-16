using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// ADR 0098 (T-061): Das Lager im Fenster nach masken.md R15. Eine Schiene mit höchstens fünf Einträgen, die beim Mindestfenster 1100 × 680 nicht umbricht; die Werkzeuge daneben oder in
/// Zeile 2; Chargen und Bewegungen als Ansichten im Bestand; „Einrichtung“ als eigene Seite mit „Zurück“; jedes alte Sprungziel zeigt seinen Inhalt am neuen Ort.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly string[] StockHeaderTools = ["ScanField", "LocationField", "SetupButton", "BookMovementButton"];

    private static List<TabButton> StockRailTabs(Window window) => [.. Named<Border>(window, "Tabs").GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible)];

    /// <summary>Alle sichtbaren Reiterschienen der Lager-Maske (ohne die Leiste „Bewegung buchen“, die ihre vier Arten behält).</summary>
    private static List<Border> StockRails(Window window) =>
    [
        .. window.GetVisualDescendants().OfType<StockView>().Single().GetVisualDescendants().OfType<Border>()
            .Where(b => b.Classes.Contains("segmented") && b.IsEffectivelyVisible && b.FindAncestorOfType<MovementBookingPanel>() is null),
    ];

    private static List<RadioButton> StockViewOptions(Window window) =>
        [.. Named<StackPanel>(window, "StockViews").GetVisualDescendants().OfType<RadioButton>().Where(r => r.IsEffectivelyVisible)];

    /// <summary>Das Mindestfenster der App (design.md Abschnitt 6), ohne die Seitenleiste „KI und Hilfe“.</summary>
    private static async Task SetMinimumWindow(Window window, ShellViewModel shell)
    {
        window.MinHeight = 0;
        window.Height = 680;
        await SetHeaderWidth(window, shell, 1100, companion: false);
        Assert.Equal(680, window.Bounds.Height, 0.5);
    }

    private static bool InsideHorizontally(Window window, Control inner, Control outer) =>
        LeftOf(window, inner) >= LeftOf(window, outer) - 0.5 && RightOf(window, inner) <= RightOf(window, outer) + 0.5;

    [Fact]
    public Task Lager_Schiene_hat_hoechstens_fuenf_Eintraege_und_bricht_bei_1100_mal_680_nicht_um() => Render("hell", "probe-lager-kopf-mindestfenster", async (window, main) =>
    {
        // Given das Lager als Inhaberin mit mehreren Standorten und Lagerorten (breitester Kopf) im Mindestfenster
        var (shell, stock) = await OpenStockOverview(window, main);
        await SetMinimumWindow(window, shell);
        Assert.True(stock.HasSeveralLocations);

        foreach (var place in new[] { StockTab.Overview, StockTab.Stock, StockTab.Tasks, StockTab.Movements, StockTab.Orders, StockTab.Count, StockTab.Places, StockTab.Suppliers })
        {
            // When jeder Ort des Lagers gezeigt wird
            stock.SetTab(place);
            await Pump(Settling);

            // Then steht höchstens eine Schiene da, mit höchstens fünf Einträgen in einer Zeile und innerhalb des Kopfes
            var header = Named<ReflowRow>(window, "StockHeader");
            var rails = StockRails(window);
            Assert.True(rails.Count == 1, $"{place}: {rails.Count} Schienen statt einer");
            var tabs = rails[0].GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).ToList();
            Assert.InRange(tabs.Count, 2, 5);
            Assert.All(tabs, t => Assert.Equal(TopOf(window, tabs[0]), TopOf(window, t), 0.5));
            Assert.All(tabs, t => Assert.True(InsideHorizontally(window, t, header), $"{place}: Reiter {t.Content} ragt über den Kopf"));
            Assert.Empty(rails[0].GetVisualDescendants().OfType<ScrollViewer>());

            // And die Werkzeuge stehen vollständig im Kopf: neben der Schiene oder rechtsbündig in Zeile 2, nie darüber
            var tools = Named<StackPanel>(window, "StockTools");
            Assert.True(InsideHorizontally(window, tools, header), $"{place}: Werkzeuge ragen über den Kopf ({LeftOf(window, tools):F0} bis {RightOf(window, tools):F0}, Kopf {LeftOf(window, header):F0} bis {RightOf(window, header):F0})");
            var left = place is StockTab.Places or StockTab.Suppliers ? Named<StackPanel>(window, "SetupHead") : (Control)rails[0];
            Assert.True(LeftOf(window, tools) >= RightOf(window, left) - 0.5 || TopOf(window, tools) >= BottomOf(window, left) - 0.5, $"{place}: Werkzeuge überdecken die Schiene");

            if (stock.IsStockTab)
            {
                // And die Zeile des Bestands zeigt die Ansichten in einer Zeile und den Lagerort, wo er gilt
                var row = Named<Grid>(window, "StockViewRow");
                var options = StockViewOptions(window);
                Assert.Equal(["Material", "Chargen (2)", "Bewegungen"], options.Select(o => (string)o.Content!));
                Assert.All(options, o => Assert.Equal(TopOf(window, options[0]), TopOf(window, o), 0.5));
                Assert.All(options, o => Assert.True(InsideHorizontally(window, o, row), $"{place}: Ansicht {o.Content} ragt über die Zeile"));
                var filter = Named<ComboBox>(window, "PlaceFilterField");
                Assert.Equal(place != StockTab.Tasks, filter.IsEffectivelyVisible);
                Assert.True(!filter.IsEffectivelyVisible || (InsideHorizontally(window, filter, row) && LeftOf(window, filter) >= RightOf(window, options[^1])), $"{place}: Lagerort überdeckt die Ansichten");
            }
        }

        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Lager_Werkzeuge_stehen_neben_der_Schiene_wenn_die_Breite_reicht() => Render("hell", "probe-lager-kopf-breit", async (window, main) =>
    {
        // Given das Lager in einem breiten Fenster
        var (shell, _) = await OpenStockOverview(window, main);
        await SetHeaderWidth(window, shell, 1440, companion: false);

        // Then stehen Suche, Standort, „Einrichtung“, Hilfe und „Bewegung buchen“ in der Zeile der Schiene, rechtsbündig
        var header = Named<ReflowRow>(window, "StockHeader");
        var rail = Named<Border>(window, "Tabs");
        var tools = Named<StackPanel>(window, "StockTools");
        Assert.Equal(BottomOf(window, rail), BottomOf(window, tools), 0.5);
        Assert.Equal(RightOf(window, header), RightOf(window, tools), 0.5);
        Assert.True(LeftOf(window, tools) >= RightOf(window, rail));
        foreach (var name in StockHeaderTools)
        {
            Assert.True(Named<Control>(window, name).IsEffectivelyVisible, name);
        }

        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Lager_alte_Sprungziele_zeigen_ihren_Inhalt_am_neuen_Ort() => Render("hell", "probe-lager-sprungziele", async (window, main) =>
    {
        var (shell, stock) = await OpenStockOverview(window, main);
        await SetMinimumWindow(window, shell);
        (StockTab Target, string SelectedTab, string Content)[] targets =
        [
            (StockTab.Stock, "StockTabButton", "StockList"),
            (StockTab.Tasks, "StockTabButton", "ExpiringDocumentButton"),
            (StockTab.Movements, "StockTabButton", "MovementsFromField"),
            (StockTab.Orders, "OrdersTabButton", "DemandPeriodField"),
            (StockTab.Count, "CountTabButton", "StartCountButton"),
            (StockTab.Places, "PlacesTabButton", "PlaceList"),
            (StockTab.Suppliers, "SuppliersTabButton", "SupplierList"),
            (StockTab.Overview, "OverviewTabButton", "TodoList"),
            (StockTab.Usage, "OverviewTabButton", "UsagePeriodField"),
        ];

        foreach (var (target, selectedTab, content) in targets)
        {
            // Given ein anderer Ort; When das alte Sprungziel gesetzt wird
            stock.SetTab(target == StockTab.Count ? StockTab.Orders : StockTab.Count);
            await Pump(Settling);
            stock.SetTab(target);
            await Pump(Settling);
            await Pump(Settling);

            // Then ist sein Reiter gewählt und sein Inhalt steht im sichtbaren Teil der Seite
            var tab = Named<TabButton>(window, selectedTab);
            Assert.True(tab.IsEffectivelyVisible && tab.IsSelected, $"{target}: Reiter {selectedTab} ist nicht gewählt");
            var shown = Named<Control>(window, content);
            var scroll = Named<ScrollViewer>(window, "StockScroll");
            Assert.True(shown.IsEffectivelyVisible, $"{target}: {content} ist nicht sichtbar");
            Assert.True(TopOf(window, shown) >= TopOf(window, scroll) - 0.5 && TopOf(window, shown) < BottomOf(window, scroll),
                $"{target}: {content} liegt außerhalb des sichtbaren Teils ({TopOf(window, shown):F0}, sichtbar {TopOf(window, scroll):F0} bis {BottomOf(window, scroll):F0})");
        }

        // And die Sichten des Bestands zeigen die gewählte als Optionsfeld
        stock.SetTab(StockTab.Tasks);
        await Pump(Settling);
        Assert.True(Named<RadioButton>(window, "BatchesViewButton").IsChecked);
        Assert.False(Named<RadioButton>(window, "ItemsViewButton").IsChecked);
        stock.SetTab(StockTab.Movements);
        await Pump(Settling);
        Assert.True(Named<RadioButton>(window, "MovementsViewButton").IsChecked);
        Assert.False(Named<RadioButton>(window, "BatchesViewButton").IsChecked);
        return shell.AdminPage;
    }, StockApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Lager_Bestand_wechselt_die_Ansicht_ueber_Optionsfelder(string theme) => Render(theme, "verwaltung-lager-chargen", async (window, main) =>
    {
        // Given der Bestand in der Ansicht „Material“
        var (shell, stock) = await OpenStock(window, main);
        Assert.Equal(["Material", "Chargen (2)", "Bewegungen"], StockViewOptions(window).Select(o => (string)o.Content!));
        Assert.True(Named<RadioButton>(window, "ItemsViewButton").IsChecked);
        Assert.True(Named<TabButton>(window, "StockTabButton").IsSelected);

        // When die Ansicht „Bewegungen“ gewählt wird; Then bleibt der Reiter „Bestand“ gewählt und die Zahl offener Chargen sichtbar
        await ClickScrolled(window, "MovementsViewButton");
        await Pump(Settling);
        Assert.Equal(StockTab.Movements, stock.Tab);
        Assert.True(Named<TabButton>(window, "StockTabButton").IsSelected);
        Assert.True(Named<Control>(window, "MovementsFromField").IsEffectivelyVisible);
        Assert.False(Named<Control>(window, "StockList").IsEffectivelyVisible);
        Assert.Equal("Chargen (2)", Named<RadioButton>(window, "BatchesViewButton").Content);

        // When die Ansicht „Chargen“ gewählt wird; Then stehen die Aufgaben da (das Bild für das Handbuch endet hier)
        await ClickScrolled(window, "BatchesViewButton");
        await Pump(Settling);
        Assert.Equal(StockTab.Tasks, stock.Tab);
        Assert.True(Shows(window, "Bald ablaufend"));
        Assert.False(Named<ComboBox>(window, "PlaceFilterField").IsEffectivelyVisible);
        return shell.AdminPage;
    }, StockApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Lager_Einrichtung_ist_eine_eigene_Seite_mit_Zurueck(string theme) => Render(theme, "verwaltung-lager-einrichtung", async (window, main) =>
    {
        // Given der Bestand; When „Einrichtung“ geklickt wird
        var (shell, stock) = await OpenStock(window, main);
        await ClickScrolled(window, "SetupButton");
        await Pump(Settling);

        // Then ersetzt die Seite die Schiene: „Zurück“, der Titel und die Wahl zwischen Lagerorten und Lieferanten; Suche und „Bewegung buchen“ fehlen
        Assert.True(stock.IsPlacesSetup);
        Assert.False(Named<Border>(window, "Tabs").IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "SetupBackButton").IsEffectivelyVisible);
        Assert.True(Shows(window, "Einrichtung"));
        Assert.Equal(["Lagerorte", "Lieferanten"], Named<Border>(window, "SetupTabs").GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).Select(t => (string)t.Content!));
        Assert.True(Named<Control>(window, "PlaceList").IsEffectivelyVisible);
        Assert.False(Named<Button>(window, "SetupButton").IsEffectivelyVisible);
        Assert.False(Named<TextBox>(window, "ScanField").IsEffectivelyVisible);
        Assert.False(Named<Button>(window, "BookMovementButton").IsEffectivelyVisible);
        Assert.True(Named<ComboBox>(window, "LocationField").IsEffectivelyVisible);

        // When „Lieferanten“ und danach „Zurück“ geklickt werden; Then steht wieder der Bestand da
        await ClickScrolled(window, "SuppliersTabButton");
        await Pump(Settling);
        Assert.True(Named<Control>(window, "SupplierList").IsEffectivelyVisible);
        Assert.False(Named<Control>(window, "PlaceList").IsEffectivelyVisible);
        await ClickScrolled(window, "SetupBackButton");
        await Pump(Settling);
        Assert.Equal(StockTab.Stock, stock.Tab);
        Assert.True(Named<Border>(window, "Tabs").IsEffectivelyVisible);
        Assert.True(Named<Control>(window, "StockList").IsEffectivelyVisible);

        // das Bild für das Handbuch zeigt die Einrichtung mit den Lagerorten
        await ClickScrolled(window, "SetupButton");
        await Pump(Settling);
        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Given_die_Rezeption_When_sie_den_Bestand_oeffnet_Then_fehlen_Chargen_und_Einrichtung() => Render("hell", "verwaltung-lager-bestand-rezeption", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);

        Assert.Equal(["Material", "Bewegungen"], StockViewOptions(window).Select(o => (string)o.Content!));
        Assert.False(Named<Button>(window, "SetupButton").IsEffectivelyVisible);
        stock.SetTab(StockTab.Places);
        stock.SetTab(StockTab.Suppliers);
        stock.SetTab(StockTab.Tasks);
        await Pump(Settling);
        Assert.Equal(StockTab.Stock, stock.Tab);
        Assert.Single(StockRails(window));
        return shell.AdminPage;
    }, StockApi().On("GET /api/auth/me", Reception()));
}
