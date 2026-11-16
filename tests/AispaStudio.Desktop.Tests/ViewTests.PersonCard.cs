using System.Net;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>ADR 0098: Personenkarte mit vier Bereichen, Übersicht mit Kacheln und Verlauf als Seitenleiste, mit echter Eingabe als Inhaberin.</summary>
public sealed partial class ViewTests
{
    private static Task SetSize(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        return Pump(Settling);
    }

    [Fact]
    public Task Personenkarte_beim_Mindestfenster_hat_die_Schiene_eine_Zeile_und_der_Verlauf_liegt_ueber_der_Karte() =>
        Render("hell", "verwaltung-team-mindestfenster", async (window, main) =>
        {
            await SetSize(window, 1100, 680);
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickCardArea(window, "Profil");

            var rail = Named<Border>(window, "CardTabs");
            var tabs = rail.GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).ToList();
            var host = Named<ContentControl>(window, "CardHost");
            var details = Named<Control>(window, "DetailsBlock");
            var view = window.GetVisualDescendants().OfType<TeamView>().Single();
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"1100×680: Team {view.Bounds.Width:0}, Karte {host.Bounds.Width:0}, Schiene {rail.Bounds.Width:0}, Stammdaten {details.Bounds.Width:0}, Rest neben Leiste {host.Bounds.Width - 360:0}");

            Assert.Equal(4, tabs.Count);
            Assert.All(tabs, t => Assert.Equal(TopOf(window, tabs[0]), TopOf(window, t), 1));
            Assert.True(LeftOf(window, rail) + rail.Bounds.Width <= LeftOf(window, host) + host.Bounds.Width, $"Schiene {rail.Bounds.Width:0}, Karte {host.Bounds.Width:0}");

            // Neben einer Leiste von 360 als eigener Spalte bliebe für das Profil zu wenig; deshalb liegt sie über der Karte, und die Karte behält ihre Breite.
            var cardWidth = host.Bounds.Width;
            ClickCenter(window, Named<Button>(window, "HistoryButton"));
            await Pump(Settling);
            var panel = Named<HistoryPanel>(window, "CardHistory");
            Assert.True(panel.IsEffectivelyVisible);
            Assert.Equal(cardWidth, host.Bounds.Width);
            Assert.Equal(360, panel.Bounds.Width);
            Assert.Equal(LeftOf(window, host) + host.Bounds.Width, LeftOf(window, panel) + panel.Bounds.Width, 1);            return team;
        }, TeamApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Verlauf_oeffnet_per_Klick_als_Seitenleiste_und_Esc_bringt_den_Fokus_zurueck(string theme) =>
        Render(theme, "verwaltung-team-verlauf", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            var button = Named<Button>(window, "HistoryButton");
            Assert.Equal("Wer was wann geändert hat", ToolTip.GetTip(button));

            ClickCenter(window, button);
            await Pump(Settling);
            var panel = Named<HistoryPanel>(window, "CardHistory");
            Assert.True(team.Card!.IsHistoryOpen);
            Assert.True(panel.IsEffectivelyVisible);
            Assert.Equal("Lena Huber", panel.Subtitle);
            Assert.Equal(team.Card.HistorySections, panel.Sections);
            Assert.Equal(PersonCardViewModel.OverviewArea, team.Card.SelectedTab);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            await Pump(Settling);
            Assert.False(team.Card.IsHistoryOpen);
            Assert.False(panel.IsEffectivelyVisible);
            Assert.Same(button, window.FocusManager?.GetFocusedElement());

            // Für das Handbuchbild bleibt der Verlauf offen.
            ClickCenter(window, button);
            await Pump(Settling);
            window.MouseMove(new Point(1, 1));
            await Pump(Settling);
            return team;
        }, TeamApi());

    [Fact]
    public Task Personenkarte_Wechsel_der_Person_schliesst_den_Verlauf() => Render("hell", "verwaltung-team-verlauf-wechsel", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        ClickCenter(window, Named<Button>(window, "HistoryButton"));
        await Pump(Settling);
        Assert.True(Named<HistoryPanel>(window, "CardHistory").IsEffectivelyVisible);

        await ClickRow(window, SpaSample.Marco.DisplayName);

        Assert.Equal(SpaSample.Marco.DisplayName, team.Card!.Title);
        Assert.False(team.Card.IsHistoryOpen);
        Assert.False(Named<HistoryPanel>(window, "CardHistory").IsEffectivelyVisible);
        return team;
    }, TeamApi());

    [Fact]
    public Task Personenkarte_Klick_auf_die_Kachel_Naechster_Dienst_springt_in_den_Dienstplan_mit_dem_Fokus_darin() =>
        Render("hell", "verwaltung-team-kachel", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            var tile = Named<Control>(window, "OverviewTiles").GetVisualDescendants().OfType<StatTile>().First(t => t.Label == "Nächster Dienst");
            Assert.Equal("Di. 20.10., 09:00–17:00", tile.Value);

            ClickCenter(window, tile);
            await Pump(Settling);

            Assert.Equal(PersonCardViewModel.WorkTimeArea, team.Card!.SelectedTab);
            var block = Named<Control>(window, "RosterSection");
            Assert.True(block.IsEffectivelyVisible);
            Assert.False(Named<Control>(window, "OverviewTiles").IsEffectivelyVisible);
            var focused = window.FocusManager?.GetFocusedElement() as Visual;
            Assert.True(focused is not null && block.IsVisualAncestorOf(focused), $"Fokus auf {focused}");
            return team;
        }, TeamApi());

    [Fact]
    public Task Personenkarte_Ladefehler_der_Uebersicht_zeigt_den_Hinweis_mit_Erneut_laden()
    {
        var api = TeamApi().On($"GET /api/employees/{SpaSample.Lena.Id}/overview", new { title = "Die Übersicht ist gerade nicht erreichbar." }, HttpStatusCode.InternalServerError);
        return Render("hell", "verwaltung-team-uebersicht-fehler", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");

            Assert.True(Shows(window, "Übersicht nicht geladen"));
            Assert.DoesNotContain(Named<Control>(window, "OverviewSection").GetVisualDescendants().OfType<StatTile>(), t => t.IsEffectivelyVisible);
            var retry = Named<Control>(window, "OverviewSection").GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == "Erneut laden");

            api.On($"GET /api/employees/{SpaSample.Lena.Id}/overview", TeamSample.Overview(SpaSample.Lena.Id));
            ClickCenter(window, retry);
            await Pump(Settling);

            Assert.False(Shows(window, "Übersicht nicht geladen"));
            Assert.Equal(5, Named<Control>(window, "OverviewTiles").GetVisualDescendants().OfType<StatTile>().Count(t => t.IsEffectivelyVisible));
            return team;
        }, api);
    }

    [Fact]
    public Task Personenkarte_Arbeitszeit_zeigt_den_Wochenplan_in_voller_Breite() => Render("hell", "verwaltung-team-arbeitszeit-breite", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        await ClickCardArea(window, "Arbeitszeit");

        var roster = Named<Control>(window, "RosterHost");
        var host = Named<ContentControl>(window, "CardHost");
        Assert.True(roster.Bounds.Width > 760, $"Wochenplan {roster.Bounds.Width:0}");
        var sunday = roster.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && (t.Text ?? "").StartsWith("So.", StringComparison.Ordinal));
        Assert.True(LeftOf(window, sunday) + sunday.Bounds.Width <= LeftOf(window, host) + host.Bounds.Width, "„So.“ liegt außerhalb der Karte");
        Assert.True(Named<Control>(window, "LeaveHost").Bounds.Width <= 760);
        return team;
    }, TeamApi());
}
