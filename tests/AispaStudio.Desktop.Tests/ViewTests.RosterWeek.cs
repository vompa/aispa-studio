using AispaStudio.Contracts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>R07 Slice 142 (Ä350): die lesbare Woche im Dienstplan, gemessen im gezeichneten Bild beider Farbschemata: Kontrast von Chip und Balken, gestrichelter Entwurfsrand, Band, Stunden beim Namen, Klick durch den Balken.</summary>
public sealed partial class ViewTests
{
    private static (StubApi Api, RosterSample Roster) RosterWeekApi()
    {
        var (api, roster) = RosterApi();
        roster.Plan(RosterLena, RosterTuesday, true, new RosterEntry(RosterKind.Work, SpaSample.Vienna.Id, new(9, 0), new(17, 0)));
        roster.PlanVacation(RosterLena, RosterTuesday.AddDays(1), RosterTuesday.AddDays(3));
        return (api, roster);
    }

    private static Color SolidColor(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Team_Wochenplan_Chip_und_Balken_haben_mindestens_4_5_zu_1_Kontrast(string theme)
    {
        var (api, _) = RosterWeekApi();
        return Render(theme, "verwaltung-team-wochenplan-woche", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);

            var time = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == "09:00–17:00");
            var chip = time.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("chip"));
            var chipContrast = Contrast(SolidColor(time.Foreground), SolidColor(chip.Background));
            Assert.True(chipContrast >= 4.5, $"Zeit im Chip: {chipContrast:0.00}:1");

            var label = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == "Urlaub Mi–Fr");
            var bar = label.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("bar"));
            var barContrast = Contrast(SolidColor(label.Foreground), SolidColor(bar.Background));
            Assert.True(barContrast >= 4.5, $"Text im Balken: {barContrast:0.00}:1");
            return team;
        }, api);
    }

    // T-039 (Ä372): der Chip rechnet seine Fläche aus Personenfarbe und Farbschema; beim Umschalten zur Laufzeit (auch „System“) muss sie mitgehen.
    [Fact]
    public Task Verwaltung_Team_Wochenplan_Chip_folgt_dem_Farbschema_beim_Umschalten()
    {
        var (api, _) = RosterWeekApi();
        return Render("hell", "verwaltung-team-wochenplan-umschalten", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            var time = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == "09:00–17:00");
            var chip = time.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("chip"));
            var light = SolidColor(chip.Background);

            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            await Pump(Settling);
            var dark = SolidColor(chip.Background);
            var ink = SolidColor(time.Foreground);
            Assert.NotEqual(light, dark);
            Assert.True(Contrast(ink, dark) >= 4.5, $"Zeit im dunklen Chip: {Contrast(ink, dark):0.00}:1");
            Assert.True(Contrast(dark, Colors.White) > Contrast(light, Colors.White), $"Chipfläche im Dunkel {dark} nicht dunkler als im Hellen {light}");

            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            await Pump(Settling);
            Assert.Equal(light, SolidColor(chip.Background));
            return team;
        }, api);
    }

    [Fact]
    public Task Verwaltung_Team_Wochenplan_zeigt_Entwurfsband_gestrichelten_Rand_und_Stunden_beim_Namen()
    {
        var (api, _) = RosterWeekApi();
        return Render("hell", "verwaltung-team-wochenplan-entwurf", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);

            Assert.True(Shows(window, "Entwurf: 1 Änderung nicht veröffentlicht"));
            var publish = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PublishButton");
            Assert.True(publish.IsEffectivelyVisible);
            Assert.Equal("Veröffentlichen", publish.Content);
            var edge = Assert.Single(window.GetVisualDescendants().OfType<Rectangle>(), r => r.Classes.Contains("draft") && r.IsEffectivelyVisible);
            Assert.NotEmpty(edge.StrokeDashArray!);
            var hours = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Name == "RosterHours").Text!;
            Assert.Matches(@"^8 .*h$", hours);
            Assert.False(Shows(window, "Grau: Wochenarbeitszeit"));
            return team;
        }, api);
    }

    [Fact]
    public Task Verwaltung_Team_Wochenplan_Klick_auf_einen_Urlaubstag_oeffnet_trotz_Balken_genau_diesen_Tag()
    {
        var (api, _) = RosterWeekApi();
        return Render("hell", "verwaltung-team-wochenplan-balken-klick", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);

            await ClickCell(window, "Lena Huber", "Do.");

            var day = Assert.IsType<ViewModels.RosterDayEditorViewModel>(team.Plan!.Day);
            Assert.Equal(RosterTuesday.AddDays(2), day.Date);
            return team;
        }, api);
    }
}
