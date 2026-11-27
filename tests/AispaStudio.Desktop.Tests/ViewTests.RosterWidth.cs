using AispaStudio.Contracts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>T-100 (Paket A, Befund 5): Der Wochenplan bleibt bei langen Standortnamen innerhalb der Planfläche, ohne waagrechten Rollbalken und ohne dass die Zeile „Besetzung“ verdeckt wird.</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData(1280, "hell")]
    [InlineData(1100, "hell")]
    [InlineData(1100, "dunkel")]
    public Task Wochenplan_mit_langen_Standortnamen_passt_in_die_Planflaeche_ohne_Rollbalken(double windowWidth, string theme)
    {
        var (api, roster) = RosterApi();
        string[] names =
        [
            "Therme Bad Waltersdorf Zentrum", "Wellnesshotel Seeblick am Wörthersee", "Gesundheitsresort Altaussee Nord",
            "Day-Spa Innsbruck Maria-Theresien-Straße", "Thermenlandschaft Loipersdorf Süd", "Alpenhof Kitzbühel Kurzentrum", "Stadtspa Salzburg Getreidegasse",
        ];
        var others = names.Select(n => new LocationDto(Guid.NewGuid(), n, "Europe/Vienna", "EUR", [])).ToList();
        api.On("GET /api/locations", new List<LocationDto>([SpaSample.Vienna, .. others]));
        for (var day = 0; day < 7; day++)
        {
            roster.Plan(RosterLena, RosterTuesday.AddDays(day - 1), false, new RosterEntry(RosterKind.Work, others[day].Id, new(11, 0), new(19, 0)));
        }

        return Render(theme, $"verwaltung-team-wochenplan-breite-lang-{windowWidth:0}", async (window, main) =>
        {
            await SetSize(window, windowWidth, 900);
            var team = await OpenWeekPlan(window, main);
            await Pump(Settling);

            var table = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "RosterTable" && c.IsEffectivelyVisible);
            var scroll = table.GetVisualAncestors().OfType<ScrollViewer>().First();
            var outer = Named<ScrollViewer>(window, "RosterScroll");
            var coverage = Named<Border>(window, "RosterCoverage");
            var coverageBottom = coverage.TranslatePoint(new Point(0, coverage.Bounds.Height), scroll)!.Value.Y;
            var bar = scroll.GetVisualDescendants().OfType<ScrollBar>().First(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal);
            var barTop = bar.TranslatePoint(new Point(0, 0), scroll)!.Value.Y;
            var grid = table.GetVisualDescendants().OfType<UniformGrid>().First();
            var chip = table.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("chip") && b.IsEffectivelyVisible).Select(b => b.Bounds.Width).ToList();
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Fenster {windowWidth:0}: Tabelle {table.Bounds.Width:0}, Viewport innen {scroll.Viewport.Width:0}, Extent {scroll.Extent.Width:0}, außen {outer.Viewport.Width:0}x{outer.Viewport.Height:0}, "
                + $"Spaltenraster {grid.Bounds.Width:0} ({grid.Bounds.Width / 7:0} je Spalte), Chips {string.Join("/", chip.Select(w => (int)w))}, Besetzung unten {coverageBottom:0} von {scroll.Viewport.Height:0}, ScrollViewer {scroll.Bounds.Height:0} hoch, Rollbalken sichtbar {bar.IsEffectivelyVisible}, Oberkante {barTop:0}, Höhe {bar.Bounds.Height:0}");

            Assert.True(table.Bounds.Width <= scroll.Viewport.Width, $"Tabelle {table.Bounds.Width:0} breiter als sichtbar {scroll.Viewport.Width:0}");
            var visibleBottom = bar.IsEffectivelyVisible ? barTop : scroll.Viewport.Height;
            Assert.True(coverageBottom <= visibleBottom, $"Besetzung reicht bis {coverageBottom:0}, frei sichtbar nur bis {visibleBottom:0} (Rollbalken darüber)");
            return team;
        }, api);
    }
}
