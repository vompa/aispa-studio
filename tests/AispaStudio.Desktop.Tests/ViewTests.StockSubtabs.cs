using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>R07 Slice 143 (Ä351): Das Lager im Fenster, Reiter, „Was ist zu tun“, der Bestand mit vier Spalten und sein Seitenpanel.</summary>
public sealed partial class ViewTests
{
    private static Button LinkButton(Window window, string text) =>
        window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == text);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Lager_Uebersicht_sagt_was_zu_tun_ist_und_die_Reiter_stehen_in_der_Reihenfolge(string theme) => Render(theme, "verwaltung-lager-uebersicht", async (window, main) =>
    {
        var (shell, _) = await OpenStockOverview(window, main);

        Assert.True(Shows(window, "Was ist zu tun"));
        Assert.True(Shows(window, "1 Position nachbestellen"));
        Assert.True(Shows(window, "1 Material unter dem Mindestbestand"));
        Assert.True(Shows(window, "Lagerwert"));
        Assert.Equal("Übersicht", Named<TabButton>(window, "OverviewTabButton").Content);
        Assert.Equal(["Übersicht", "Bestand", "Bestellen", "Chargen (2)", "Bewegungen", "Inventur", "Lagerorte", "Lieferanten", "Verbrauch"],
            Named<Border>(window, "Tabs").GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).Select(t => (string)t.Content!));
        Assert.True(Named<Border>(window, "AdminDivider").IsEffectivelyVisible);
        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Lager_Uebersicht_Link_fuehrt_ins_Bestellen() => Render("hell", "verwaltung-lager-uebersicht-link", async (window, main) =>
    {
        var (shell, stock) = await OpenStockOverview(window, main);

        LinkButton(window, "Bestellen öffnen").Command!.Execute(null);
        await Pump(Settling);

        Assert.Equal(StockTab.Orders, stock.Tab);
        Assert.True(Shows(window, "Bestellvorschlag"));
        return shell.AdminPage;
    }, StockApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Lager_Bestand_hat_vier_Spalten_und_die_uebrigen_Werte_im_Seitenpanel(string theme) => Render(theme, "verwaltung-lager-bestand", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);

        Assert.True(Shows(window, "Reicht für"));
        Assert.True(Shows(window, "Status"));
        Assert.False(Shows(window, "Verbrauch geplant"));
        Assert.True(Shows(window, "nachbestellen"));
        Assert.Null(Named<ContentControl>(window, "RowDetail").Content);

        stock.Overview.Select(OilId);
        await Pump(Settling);

        Assert.True(Shows(window, "Mindestbestand"));
        Assert.True(Shows(window, "Verbrauch geplant"));
        var closeButton = Named<Button>(window, "CloseRowDetailButton");
        Assert.True(closeButton.IsEffectivelyVisible);
        ClickCenter(window, closeButton);
        await Pump(Settling);
        Assert.Null(stock.Overview.SelectedRow);

        // das Bild für das Handbuch endet mit offenem Seitenpanel
        stock.Overview.Select(TowelId);
        await Pump(Settling);
        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Lager_Suchfeld_filtert_beim_Tippen() => Render("hell", "verwaltung-lager-suche", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);

        Assert.True(Shows(window, "Material suchen oder scannen"));
        await TypeInto(window, "ScanField", "hand");
        Assert.Equal(TowelId, Assert.Single(stock.Overview.Rows).ItemId);
        return shell.AdminPage;
    }, StockApi());

    /// <summary>T-025 (Auftraggeber, 05.11.2026, Ä373): Die Rezeption sieht „Bestellen“ nur mit der Bedarfsprüfung; Bestellvorschlag und Bestellungen bleiben beim Lagerrecht.</summary>
    [Fact]
    public Task Given_die_Rezeption_When_sie_im_Lager_Bestellen_oeffnet_Then_sieht_sie_die_Bedarfspruefung_aber_keinen_Bestellvorschlag() => Render("hell", "verwaltung-lager-bestellen-rezeption", async (window, main) =>
    {
        var (shell, _) = await OpenStockOverview(window, main);

        Assert.Equal(["Übersicht", "Bestand", "Bestellen", "Bewegungen"],
            Named<Border>(window, "Tabs").GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).Select(t => (string)t.Content!));
        await ClickScrolled(window, "OrdersTabButton");
        await Pump(Settling);

        Assert.True(Shows(window, "Bedarfsprüfung"));
        Assert.False(Shows(window, "Bestellvorschlag"));
        Assert.False(Shows(window, "Gerade ist nichts zu bestellen."));
        return shell.AdminPage;
    }, StockApi().On("GET /api/auth/me", Reception()));
}
