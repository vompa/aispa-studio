using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ä58/Ä70 (Runde 6, Auftraggeber): die beiden Knöpfe unten in der Navigationsleiste (Farbschema, Benutzer) waren sichtbar breiter als die Einträge oben.
/// Der Test misst Breite und linke Kante der Einträge und der Knöpfe in der Leiste sowie Rundung der Hover-Fläche.
/// </summary>
public sealed partial class ViewTests
{
    private const double RailTolerance = 0.5;

    private sealed record RailBox(double Left, double Width, CornerRadius Radius);

    /// <summary>Lage eines Elements in der Leiste (Linke Kante relativ zur Leiste) und Rundung seiner Fläche.</summary>
    private static RailBox MeasureInRail(Visual rail, Control control)
    {
        var left = control.TranslatePoint(new Point(0, 0), rail)!.Value.X;
        var surface = control.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
        return new RailBox(left, control.Bounds.Width, surface.CornerRadius);
    }

    [Fact]
    public Task Untere_Knoepfe_der_Navigationsleiste_sind_so_breit_wie_die_Eintraege() => Render("hell", "navigation-knoepfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump(Settling);

        var rail = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("rail"));
        var navigation = rail.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("nav"));
        var item = MeasureInRail(rail, (Control)navigation.ContainerFromIndex(0)!);
        var buttons = rail.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("navbtn")).ToList();

        Assert.Equal(2, buttons.Count);
        var report = string.Join("; ", buttons.Select(b =>
        {
            var box = MeasureInRail(rail, b);
            return $"Knopf links {box.Left:0.##}, Breite {box.Width:0.##}, Rundung {box.Radius.TopLeft:0.##}";
        }));
        foreach (var button in buttons)
        {
            var box = MeasureInRail(rail, button);
            Assert.True(Math.Abs(box.Width - item.Width) <= RailTolerance, $"Eintrag: links {item.Left:0.##}, Breite {item.Width:0.##}, Rundung {item.Radius.TopLeft:0.##}; {report}");
            Assert.True(Math.Abs(box.Left - item.Left) <= RailTolerance, $"Eintrag: links {item.Left:0.##}, Breite {item.Width:0.##}; {report}");
            Assert.Equal(item.Radius, box.Radius);
        }

        return calendar;
    });
}
