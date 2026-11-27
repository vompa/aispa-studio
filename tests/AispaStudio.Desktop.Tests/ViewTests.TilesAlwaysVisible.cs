using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// T-109 (Auftraggeber, 26.11.2026): Es gibt keinen Reiter „Übersicht“ mehr; die Kennzahl-Kacheln stehen dauerhaft über der Schiene und bleiben beim Wechsel des Reiters sichtbar.
/// Namen in den Listen der Verwaltung stehen fett und eine Stufe größer.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly string[] StockRailButtons = ["StockTabButton", "OrdersTabButton", "CountTabButton"];

    private static List<string> RailLabels(Window window, string rail) =>
        [.. Named<Border>(window, rail).GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).Select(t => t.Content as string ?? "")];

    private static void AssertTilesAboveRail(Window window, string tiles, string rail)
    {
        var grid = Named<Control>(window, tiles);
        Assert.True(grid.IsEffectivelyVisible, $"{tiles} ist nicht sichtbar");
        Assert.Contains(grid.GetVisualDescendants().OfType<StatTile>(), t => t.IsEffectivelyVisible);
        Assert.True(BottomOf(window, grid) <= TopOf(window, Named<Control>(window, rail)) + 0.5, $"{tiles} steht nicht über {rail}");
    }

    [Fact]
    public Task Kacheln_der_Personenkarte_stehen_ohne_Reiter_Uebersicht_ueber_jedem_Bereich() => Render("hell", "probe-t109-personenkarte", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");

        Assert.DoesNotContain("Übersicht", RailLabels(window, "CardTabs"));
        Assert.Equal(PersonCardViewModel.ProfileArea, team.Card!.SelectedTab);
        AssertTilesAboveRail(window, "OverviewTiles", "CardTabs");

        await ClickCardArea(window, "Arbeitszeit");
        Assert.Equal(PersonCardViewModel.WorkTimeArea, team.Card.SelectedTab);
        AssertTilesAboveRail(window, "OverviewTiles", "CardTabs");
        return team;
    }, TeamApi());

    [Fact]
    public Task Kacheln_der_Behandlung_stehen_ohne_Reiter_Uebersicht_ueber_jedem_Bereich() => Render("hell", "probe-t109-behandlung", async (window, main) =>
    {
        var treatments = await OpenTreatments(window, main);

        Assert.DoesNotContain("Übersicht", RailLabels(window, "SectionTabs"));
        Assert.Equal(TreatmentEditorViewModel.Basics, treatments.Editor!.SelectedSection);
        AssertTilesAboveRail(window, "OverviewTiles", "SectionTabs");

        await ClickSection(window, "Durchführung");
        Assert.Equal(TreatmentEditorViewModel.Execution, treatments.Editor.SelectedSection);
        AssertTilesAboveRail(window, "OverviewTiles", "SectionTabs");
        return treatments;
    }, TreatmentApi());

    [Fact]
    public Task Kacheln_des_Lagers_stehen_ohne_Reiter_Uebersicht_ueber_jedem_Reiter() => Render("hell", "probe-t109-lager", async (window, main) =>
    {
        var (shell, stock) = await OpenStockOverview(window, main);

        Assert.DoesNotContain("Übersicht", RailLabels(window, "Tabs"));
        AssertTilesAboveRail(window, "StockStats", "Tabs");

        foreach (var tab in StockRailButtons)
        {
            ClickCenter(window, Named<TabButton>(window, tab));
            await Pump(Settling);
            Assert.True(Named<TabButton>(window, tab).IsSelected, $"{tab} ist nicht gewählt");
            AssertTilesAboveRail(window, "StockStats", "Tabs");
        }

        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Listenname_in_der_Verwaltung_steht_fett_und_eine_Stufe_groesser() => Render("hell", "probe-t109-listenname", async (window, main) =>
    {
        var team = await OpenTeam(window, main);

        var name = Named<ListBox>(window, "TeamList").GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Lena Huber");
        Assert.Equal(FontWeight.SemiBold, name.FontWeight);
        Assert.Equal(16, name.FontSize);
        return team;
    }, TeamApi());
}
