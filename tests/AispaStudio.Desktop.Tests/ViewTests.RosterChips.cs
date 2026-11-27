using AispaStudio.Contracts;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>T-100 (Paket B): Ein Dienst am gewählten Standort und einer an einem anderen Standort sind ohne Lesen erkennbar (Stilklasse, zweite Zeile); der Screenreader-Name der Zelle nennt Zeit und Standort wie bisher.</summary>
public sealed partial class ViewTests
{
    private static (StubApi Api, RosterSample Roster) RosterElsewhereApi(Func<StubApi, StubApi>? configure = null)
    {
        var (api, roster) = RosterApi();
        api.On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna, SpaSample.Salzburg });
        configure?.Invoke(api);
        roster.Plan(RosterLena, RosterTuesday.AddDays(2), false, new RosterEntry(RosterKind.Work, SpaSample.Salzburg.Id, new(10, 0), new(14, 0)));
        return (api, roster);
    }

    private static ContentControl RosterCell(Window window, string person, string day) =>
        window.GetVisualDescendants().OfType<ContentControl>().First(c => c.Classes.Contains("rcell") && c.IsEffectivelyVisible
            && (AutomationProperties.GetName(c) ?? "").StartsWith($"{person}, {day}", StringComparison.Ordinal));

    private static void AssertHereAndElsewhere(Window window)
    {
        var here = RosterCell(window, "Lena Huber", "Di.");
        var elsewhere = RosterCell(window, "Lena Huber", "Do.");

        var hereChip = here.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("chip") && b.IsEffectivelyVisible);
        var elsewhereChip = elsewhere.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("chip") && b.IsEffectivelyVisible);
        Assert.DoesNotContain("elsewhere", hereChip.Classes);
        Assert.Contains("elsewhere", elsewhereChip.Classes);

        var hereTexts = hereChip.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        var elsewhereTexts = elsewhereChip.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Equal(["09:00–17:00"], hereTexts);
        Assert.Equal(["10:00–14:00", "Salzburg"], elsewhereTexts);

        Assert.Equal("Lena Huber, Di. 20.10.: 09:00–17:00", AutomationProperties.GetName(here));
        Assert.Equal("Lena Huber, Do. 22.10.: 10:00–14:00 Salzburg", AutomationProperties.GetName(elsewhere));
    }

    [Fact]
    public Task Wochenplan_Dienst_hier_und_Dienst_an_anderem_Standort_unterscheiden_sich_in_Stilklasse_und_zweiter_Zeile()
    {
        var (api, _) = RosterElsewhereApi();
        return Render("hell", "verwaltung-team-wochenplan-anderer-standort", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            AssertHereAndElsewhere(window);
            return team;
        }, api);
    }

    [Fact]
    public Task Wochenplan_Zelle_ohne_Planungsrecht_zeigt_dieselben_Chips_wie_der_Knopf()
    {
        var (api, _) = RosterElsewhereApi(a => a.On("GET /api/auth/me", Reception()));
        return Render("hell", "verwaltung-team-wochenplan-rezeption-anderer-standort", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await Pump(Settling);
            Assert.True(team.ShowsRoster);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("rcell") && b.IsEffectivelyVisible);
            AssertHereAndElsewhere(window);
            return team;
        }, api);
    }
}
