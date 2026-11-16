using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// ADR 0098, T-066: fünf Ansichten im Team, „Anträge“ als Seitenleiste des Wochenplans und „Rechte und Zuständigkeiten“ als eine Seite mit zwei Abschnitten,
/// mit echter Eingabe als Inhaberin; Bilder hell und dunkel, auch beim Mindestfenster.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Team mit zwei offenen Anträgen (Lena Urlaub mit Terminen, Marco Schulung).</summary>
    private static StubApi RequestsApi()
    {
        var (api, _) = RosterApi();
        return api.On("GET /api/absence-requests", new List<AbsenceRequestDto>
        {
            RosterSample.Request(RosterLena, RosterKind.Vacation, 8, 12, affected: 2, note: "Familienfeier"),
            RosterSample.Request(SpaSample.Marco, RosterKind.Training, 4, 4),
        });
    }

    private static TextBlock RequestsCountText(Window window) => Named<Border>(window, "RequestsCount").GetVisualDescendants().OfType<TextBlock>().Single();

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Antraege_oeffnen_als_Seitenleiste_im_Wochenplan_und_Esc_bringt_den_Fokus_zurueck(string theme) =>
        Render(theme, "verwaltung-team-wochenplan-antraege", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            var button = Named<Button>(window, "RequestsButton");
            Assert.True(button.IsEffectivelyVisible);
            Assert.True(Named<Border>(window, "RequestsCount").IsEffectivelyVisible);
            Assert.Equal("2", RequestsCountText(window).Text);
            Assert.Equal("Anträge, 2 offene Anträge", Avalonia.Automation.AutomationProperties.GetName(button));
            var panel = Named<SidePanel>(window, "RequestsPanel");
            Assert.False(panel.IsEffectivelyVisible);

            await ClickButton(window, button);
            Assert.True(team.Plan!.IsRequestsOpen);
            Assert.True(panel.IsEffectivelyVisible);
            Assert.Equal(360, panel.Bounds.Width);
            Assert.True(RegionNavigation.GetIsRegion(panel));
            Assert.Equal("Anträge", Avalonia.Automation.AutomationProperties.GetName(panel));
            Assert.True(Shows(window, "Urlaub, 8.3.2027 bis 12.3.2027"));
            Assert.True(Shows(window, "In dieser Zeit liegen 2 Termine."));
            var focused = window.FocusManager?.GetFocusedElement() as Visual;
            Assert.True(focused is not null && panel.IsVisualAncestorOf(focused), $"Fokus auf {focused}");
            // Der Fokus steht auf dem ersten Antrag, nicht auf dem Fragezeichen im Kopf.
            Assert.Equal("Ablehnen", (focused as Button)?.Content as string);
            // Seitenleiste nach Brief 5.12: Fläche surface, Rahmen links 1 px.
            Assert.NotNull(panel.Background);
            Assert.Equal(new Thickness(1, 0, 0, 0), panel.BorderThickness);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            await Pump(Settling);
            Assert.False(team.Plan.IsRequestsOpen);
            Assert.False(panel.IsEffectivelyVisible);
            Assert.Same(button, window.FocusManager?.GetFocusedElement());

            // Für das Bild bleibt die Leiste offen.
            await ClickButton(window, button);
            window.MouseMove(new Point(1, 1));
            await Pump(Settling);
            return team;
        }, RequestsApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Antraege_beim_Mindestfenster_liegt_die_Leiste_ueber_dem_Wochenraster(string theme) =>
        Render(theme, "verwaltung-team-wochenplan-antraege-mindestfenster", async (window, main) =>
        {
            await SetSize(window, 1100, 680);
            var team = await OpenWeekPlan(window, main);
            var table = Named<Control>(window, "RosterTable");
            var scroll = Named<ScrollViewer>(window, "RosterScroll");
            var tableWidth = table.Bounds.Width;

            await ClickButton(window, Named<Button>(window, "RequestsButton"));
            var panel = Named<SidePanel>(window, "RequestsPanel");
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"1100×680: Wochenplan {scroll.Bounds.Width:0}, Raster {tableWidth:0}, Leiste {panel.Bounds.Width:0}, neben der Leiste {scroll.Bounds.Width - panel.Bounds.Width:0}");

            // Neben der Leiste als eigener Spalte bliebe für das Raster zu wenig; deshalb liegt sie darüber, und das Raster behält seine Breite.
            Assert.True(tableWidth > scroll.Bounds.Width - panel.Bounds.Width, $"Raster {tableWidth:0} passt neben die Leiste ({scroll.Bounds.Width - panel.Bounds.Width:0})");
            Assert.Equal(tableWidth, table.Bounds.Width);
            Assert.Equal(LeftOf(window, scroll) + scroll.Bounds.Width, LeftOf(window, panel) + panel.Bounds.Width, 1);
            Assert.Equal(TopOf(window, scroll), TopOf(window, panel), 1);
            Assert.True(TopOf(window, panel) + panel.Bounds.Height <= window.Height + 1, $"Leiste reicht bis {TopOf(window, panel) + panel.Bounds.Height:0}");
            // Beide Anträge mit ihren Knöpfen stehen in der Leiste, nichts ragt über ihre Breite hinaus.
            var approve = panel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Content as string == "Genehmigen").ToList();
            Assert.Equal(2, approve.Count);
            Assert.All(approve, b => Assert.True(LeftOf(window, b) + b.Bounds.Width <= LeftOf(window, panel) + panel.Bounds.Width, "Genehmigen ragt über die Leiste"));
            window.MouseMove(new Point(1, 1));
            await Pump(Settling);
            return team;
        }, RequestsApi());

    [Fact]
    public Task Ohne_offene_Antraege_steht_am_Knopf_keine_Zahl() => Render("hell", "verwaltung-team-antraege-ohne-zahl", async (window, main) =>
    {
        var team = await OpenWeekPlan(window, main);

        Assert.True(Named<Button>(window, "RequestsButton").IsEffectivelyVisible);
        Assert.False(Named<Border>(window, "RequestsCount").IsEffectivelyVisible);
        return team;
    }, RosterApi().Api);

    [Fact]
    public Task Die_Kachel_Offene_Antraege_der_Personenkarte_oeffnet_den_Wochenplan_mit_der_Leiste() =>
        Render("hell", "verwaltung-team-kachel-antraege", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            var tile = Named<Control>(window, "OverviewTiles").GetVisualDescendants().OfType<StatTile>().First(t => t.Label == "Offene Anträge");

            ClickCenter(window, tile);
            await Pump(Settling);

            Assert.True(team.ShowsRoster);
            Assert.True(Named<SidePanel>(window, "RequestsPanel").IsEffectivelyVisible);
            Assert.True(Shows(window, "Schulung, 4.3.2027"));
            return team;
        }, RequestsApi());

    [Fact]
    public Task Die_Rezeption_sieht_im_Wochenplan_keinen_Knopf_Antraege() => Render("hell", "verwaltung-team-antraege-rezeption", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await Pump(Settling);

        Assert.True(team.ShowsRoster);
        Assert.False(Named<Button>(window, "RequestsButton").IsEffectivelyVisible);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TabButton>(), t => t.IsEffectivelyVisible && t.Content as string == "Rechte und Zuständigkeiten");
        return team;
    }, RequestsApi().On("GET /api/auth/me", Reception()));

    [Theory]
    [InlineData("hell", false)]
    [InlineData("dunkel", false)]
    [InlineData("hell", true)]
    [InlineData("dunkel", true)]
    public Task Verwaltung_Team_Rechte_und_Zustaendigkeiten_zeigt_beide_Abschnitte_untereinander(string theme, bool smallest) =>
        Render(theme, smallest ? "verwaltung-team-rechte-mindestfenster" : "verwaltung-team-rechte", async (window, main) =>
        {
            if (smallest)
            {
                await SetSize(window, 1100, 680);
            }

            var team = await OpenTeam(window, main);
            await ClickSection(window, "Rechte und Zuständigkeiten");
            await Pump(Settling);

            Assert.Equal(OwnerModes, window.GetVisualDescendants().OfType<TabButton>().Where(t => t.IsEffectivelyVisible).Select(t => t.Content as string).Where(OwnerModes.Contains));
            var powers = Named<Control>(window, "PowersSection");
            var duties = Named<Control>(window, "DutiesSection");
            Assert.True(powers.IsEffectivelyVisible);
            Assert.True(duties.IsEffectivelyVisible);
            Assert.Equal(20, TopOf(window, duties) - (TopOf(window, powers) + powers.Bounds.Height), 1);
            foreach (var (section, title, topic) in new[] { (powers, "Wer darf was?", "regelungen-vollmachten"), (duties, "Zuständigkeiten", "zustaendigkeiten-zertifikate") })
            {
                var heading = section.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == title);
                Assert.True(heading.Classes.Contains("h3"), $"{title}: keine Zwischenüberschrift");
                Assert.Equal(topic, section.GetVisualDescendants().OfType<HelpButton>().First().Topic);
            }

            Assert.True(Shows(window, "Lena, bis 31.12.2026, erteilt von Katrin"));
            Assert.True(Shows(window, "Lena Huber, Vertretung Marco Berger, gültig ab 1.1.2026"));
            Assert.False(Named<Control>(window, "TeamList").IsEffectivelyVisible);
            Assert.False(team.HasUnsavedChanges);
            return team;
        }, DutiesApi());

    [Fact]
    public Task Die_fruehere_Ansicht_Zustaendigkeiten_rollt_ihren_Abschnitt_an_den_Anfang() => Render("hell", "verwaltung-team-rechte-sprung", async (window, main) =>
    {
        await SetSize(window, 1100, 680);
        var team = await OpenTeam(window, main);
        var shell = (ShellViewModel)main.Content!;

        // Sprung über die Verwaltung wie aus dem Assistenten oder dem Kalender, mit der früheren Ansicht als Abschnitt.
        shell.AdminPage.Show("masterData", "team", null, TeamViewModel.DutiesMode);
        await Pump(Settling);
        Assert.True(team.ShowsRights);

        // Ohne Person: „zustaendigkeiten“ war früher auch ein Reiter der Karte; der Sprung mit Person bleibt dort (PersonCardTests).
        var scroll = Named<ScrollViewer>(window, "RightsScroll");
        var duties = Named<Control>(window, "DutiesSection");
        var expected = Math.Min(duties.TranslatePoint(default, (Visual)scroll.Content!)!.Value.Y, scroll.Extent.Height - scroll.Viewport.Height);
        Assert.True(expected > 0, "Die Seite ist beim Mindestfenster nicht höher als der sichtbare Bereich");
        Assert.Equal(expected, scroll.Offset.Y, 1);

        Assert.True(await team.SwitchToAsync(TeamViewModel.PowersMode));
        await Pump(Settling);
        Assert.Equal(0, scroll.Offset.Y, 1);
        return team;
    }, DutiesApi());
}
