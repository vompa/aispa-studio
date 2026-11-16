using System.Windows.Input;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Gemeinsame Bausteine der Verwaltungsmasken: Kennzahl-Kachel (Design-Brief 5.5) und Seitenleiste „Verlauf“ (5.12).</summary>
public sealed partial class ViewTests
{
    private static readonly TimeZoneInfo AdminBlocksZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static StatTile Tile(Control root) => root.GetVisualDescendants().OfType<StatTile>().First();

    private static string[] VisibleTextsOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text!)];

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Kennzahl_Kachel_zeigt_Beschriftung_Wert_und_Hinweis(string theme) => InWindow(theme,
        () => new StatTile { Label = "Bestand", Value = "128", Hint = "Vormonat 120", Width = 200 }, 320, 200, window =>
    {
        Assert.Equal(["Bestand", "128", "Vormonat 120"], VisibleTextsOf(window));
        Assert.DoesNotContain(Tile(window).GetVisualDescendants().OfType<Icon>(), i => i.IsEffectivelyVisible);
        return Task.CompletedTask;
    }, "stat-tile");

    [Fact]
    public Task Kennzahl_Kachel_ohne_Hinweis_blendet_die_Zeile_aus() => InWindow("hell", () => new StatTile { Label = "Bestand", Value = "128" }, 320, 200, window =>
    {
        Assert.Equal(["Bestand", "128"], VisibleTextsOf(window));
        return Task.CompletedTask;
    });

    [Fact]
    public Task Kennzahl_Kachel_im_Zustand_Warn_zeigt_ein_Symbol_nicht_nur_Farbe() => InWindow("hell",
        () => new StatTile { Label = "Unter Mindestbestand", Value = "3", State = StatTileState.Warn }, 320, 200, window =>
    {
        var tile = Tile(window);
        Assert.Contains("warn", tile.Classes);
        Assert.Equal("Unter Mindestbestand: 3, Warnung", AutomationProperties.GetName(tile));
        Assert.Contains(tile.GetVisualDescendants().OfType<Icon>(), i => i.IsEffectivelyVisible && i.Data == Icons.Warning);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Kennzahl_Kachel_mit_Command_ist_fokussierbar_und_Enter_und_Leertaste_loesen_aus() => InWindow("hell", () =>
    {
        var count = 0;
        return new StatTile { Label = "Offen", Value = "7", Command = new RelayCommand<object?>(p => count += p is int step ? step : 100), CommandParameter = 1, Tag = (Func<int>)(() => count) };
    }, 320, 200, async window =>
    {
        var tile = Tile(window);
        var count = (Func<int>)tile.Tag!;
        Assert.True(tile.Focusable);
        Assert.True(tile.IsTabStop);
        Assert.True(tile.Focus(NavigationMethod.Tab));
        Press(window, PhysicalKey.Enter, RawInputModifiers.None);
        await Pump();
        Assert.Equal(1, count());
        Press(window, PhysicalKey.Space, RawInputModifiers.None);
        await Pump();
        Assert.Equal(2, count());
    });

    [Fact]
    public Task Kennzahl_Kachel_ohne_Command_ist_reine_Anzeige_und_kein_Tab_Stopp() => InWindow("hell",
        () => new StackPanel { Children = { new StatTile { Label = "Bestand", Value = "128" }, new Button { Content = "Weiter", Name = "After" } } }, 320, 200, async window =>
    {
        var tile = Tile(window);
        Assert.False(tile.Focusable);
        Assert.False(tile.IsTabStop);
        Assert.False(tile.Focus(NavigationMethod.Tab));
        Press(window, PhysicalKey.Tab, RawInputModifiers.None);
        await Pump();
        Assert.Same(Named<Button>(window, "After"), Focused(window));
    });

    [Fact]
    public Task Kennzahl_Kachel_nennt_dem_Screenreader_Beschriftung_Wert_und_Hinweis() => InWindow("hell",
        () => new StackPanel { Children = { new StatTile { Label = "Bestand", Value = "128", Hint = "Vormonat 120" }, new StatTile { Label = "Offen", Value = "7" } } }, 320, 300, window =>
    {
        var tiles = window.GetVisualDescendants().OfType<StatTile>().ToList();
        Assert.Equal("Bestand: 128, Vormonat 120", AutomationProperties.GetName(tiles[0]));
        Assert.Equal("Offen: 7", AutomationProperties.GetName(tiles[1]));
        return Task.CompletedTask;
    });

    private static HistoryPanel NewHistoryPanel(bool open, ICommand? close = null) => new()
    {
        Subtitle = "Kundin Anna Muster",
        IsOpen = open,
        CloseCommand = close,
        Sections =
        [
            new HistorySectionViewModel(new StubApi().Client(), "api/x/history", AdminBlocksZone, "Erster Tipp", "Verlauf"),
            new HistorySectionViewModel(new StubApi().Client(), "api/y/history", AdminBlocksZone, "Zweiter Tipp", "Änderungen"),
        ],
    };

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verlauf_Leiste_geoeffnet_zeigt_Titel_und_alle_Abschnitte(string theme) => InWindow(theme, () => NewHistoryPanel(true), 600, 500, window =>
    {
        var panel = window.GetVisualDescendants().OfType<HistoryPanel>().First();
        Assert.True(panel.IsEffectivelyVisible);
        Assert.Equal(360, panel.Bounds.Width, 0.5);
        Assert.Contains("sidepanel", panel.Classes);
        Assert.True(RegionNavigation.GetIsRegion(panel));
        var texts = VisibleTextsOf(panel);
        Assert.Contains("Verlauf", texts);
        Assert.Contains("Kundin Anna Muster", texts);
        Assert.Contains("Änderungen", texts);
        Assert.Equal(2, panel.GetVisualDescendants().OfType<AccordionHeader>().Count());
        Assert.Equal("Schließen (Esc)", ToolTip.GetTip(panel.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Schließen")));
        return Task.CompletedTask;
    }, "history-panel");

    [Fact]
    public Task Verlauf_Leiste_ist_geschlossen_nicht_sichtbar() => InWindow("hell", () => new StackPanel { Children = { NewHistoryPanel(false) } }, 600, 500, window =>
    {
        Assert.False(window.GetVisualDescendants().OfType<HistoryPanel>().First().IsEffectivelyVisible);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Verlauf_Leiste_schliesst_mit_Esc_und_gibt_den_Fokus_zurueck() => InWindow("hell", () =>
        new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { new Button { Name = "Opener", Content = "Verlauf" }, NewHistoryPanel(false) } }, 700, 500, async window =>
    {
        var opener = Named<Button>(window, "Opener");
        var panel = window.GetVisualDescendants().OfType<HistoryPanel>().First();
        opener.Focus(NavigationMethod.Tab);
        panel.IsOpen = true;
        await Pump(Settling);
        Assert.True(panel.IsEffectivelyVisible);
        Assert.True(Focused(window) is Visual inside && panel.IsVisualAncestorOf(inside));
        Press(window, PhysicalKey.Escape, RawInputModifiers.None);
        await Pump(Settling);
        Assert.False(panel.IsOpen);
        Assert.False(panel.IsEffectivelyVisible);
        Assert.Same(opener, Focused(window));
    });

    [Fact]
    public Task Verlauf_Leiste_schliesst_mit_dem_Schliessen_Knopf_und_ruft_den_Befehl() => InWindow("hell", () =>
    {
        var closed = 0;
        var panel = NewHistoryPanel(true, new RelayCommand(() => closed++));
        panel.Tag = (Func<int>)(() => closed);
        return panel;
    }, 600, 500, async window =>
    {
        var panel = window.GetVisualDescendants().OfType<HistoryPanel>().First();
        ClickCenter(window, panel.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Schließen"));
        await Pump(Settling);
        Assert.False(panel.IsOpen);
        Assert.Equal(1, ((Func<int>)panel.Tag!)());
    });
}
