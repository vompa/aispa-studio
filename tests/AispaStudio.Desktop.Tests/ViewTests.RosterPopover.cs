using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>T-017 (Ä370): Ein Klick auf eine Zelle im Dienstplan öffnet den Tag als Popover direkt an der Zelle, im Wochenplan und auf der Personenkarte.</summary>
public sealed partial class ViewTests
{
    private static Rect BoundsIn(Control control, Visual root) => new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

    private static Control PlanRootOf(Window window) => VisibleNamed<Control>(window, "PlanRoot");

    [Fact]
    public Task Given_der_Wochenplan_When_eine_Zelle_geklickt_wird_Then_oeffnet_der_Tag_als_Popover_direkt_unter_der_Zelle()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-popover", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");

            var root = PlanRootOf(window);
            var cell = BoundsIn(RosterCellButton(window, "Lena Huber", "Di."), root);
            var editor = BoundsIn(VisibleNamed<Control>(window, "DayEditor"), root);
            Assert.InRange(editor.Top, cell.Bottom, cell.Bottom + 8);
            Assert.InRange(editor.Left, cell.Left - 8, cell.Left + 8);
            Assert.True(editor.Top < BoundsIn(VisibleNamed<Control>(window, "RosterTable"), root).Bottom, "Der Tag liegt als Popover über der Tabelle, nicht darunter.");
            var scroll = VisibleNamed<ScrollViewer>(window, "RosterScroll");
            var inView = BoundsIn(VisibleNamed<Control>(window, "DayEditor"), scroll);
            Assert.True(inView.Top >= -1 && inView.Bottom <= scroll.Viewport.Height + 1, $"Popover {inView}, sichtbar {scroll.Viewport}");

            await ClickCell(window, "Lena Huber", "Mi.");
            var next = BoundsIn(RosterCellButton(window, "Lena Huber", "Mi."), root);
            Assert.InRange(BoundsIn(VisibleNamed<Control>(window, "DayEditor"), root).Left, next.Left - 8, next.Left + 8);

            await ClickButton(window, VisibleNamed<Button>(window, "CloseDayButton"));
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "DayEditor" && c.IsEffectivelyVisible);
            return team;
        }, api);
    }

    [Fact]
    public Task Given_die_Karte_When_der_Sonntag_geklickt_und_die_Tabelle_gescrollt_wird_Then_bleibt_das_Popover_ganz_in_der_Karte()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-karte-dienstplan-popover", async (window, main) =>
        {
            var team = await OpenTeam(window, main);
            await ClickRow(window, "Lena Huber");
            await ClickSection(window, "Dienstplan und Abwesenheit");
            await Pump(Settling);

            await ClickCell(window, "Lena Huber", "So.");
            var root = PlanRootOf(window);
            var editor = BoundsIn(VisibleNamed<Control>(window, "DayEditor"), root);
            Assert.True(editor.Left >= 0 && editor.Right <= root.Bounds.Width + 0.5, $"Popover {editor}, Karte {root.Bounds.Width}");

            // Die Tabelle scrollt auf der Karte waagrecht: ragt die offene Zelle links hinaus, bleibt das Popover in der Karte.
            await ClickCell(window, "Lena Huber", "Di.");
            var tableScroll = VisibleNamed<Control>(window, "RosterTable").GetVisualAncestors().OfType<ScrollViewer>().First();
            tableScroll.Offset = new Vector(Math.Max(0, tableScroll.Extent.Width - tableScroll.Viewport.Width), 0);
            await Pump(Settling);
            editor = BoundsIn(VisibleNamed<Control>(window, "DayEditor"), root);
            Assert.True(editor.Left >= 0 && editor.Right <= root.Bounds.Width + 0.5, $"Popover {editor}, Karte {root.Bounds.Width}");
            return team;
        }, api);
    }

    [Fact]
    public Task Given_ein_Entwurf_When_er_gespeichert_ist_Then_stehen_Meldung_und_Knoepfe_im_Popover()
    {
        var (api, _) = RosterApi();
        return Render("hell", "verwaltung-team-wochenplan-popover-entwurf", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            await ClickCell(window, "Lena Huber", "Di.");
            await TypeIntoTime(window, "Dienst 1 Beginn", "12:00");
            await TypeIntoTime(window, "Dienst 1 Ende", "20:00");
            PressSave(window);
            await Pump(Settling);

            Assert.True(Shows(window, RosterDayEditorViewModel.DraftSaved));
            var editor = VisibleNamed<Control>(window, "DayEditor");
            Assert.True(editor.Bounds.Width <= 560.5, $"Popover {editor.Bounds.Width} breit");
            // Texte und Knöpfe; Symbole (Viewbox, Path) messen ihre ungeschnittene Geometrie und zählen nicht.
            var outside = editor.GetVisualDescendants().OfType<Control>()
                .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && c is TextBlock or Button)
                .Select(c => (Control: c, Bounds: BoundsIn(c, editor)))
                .Where(x => x.Bounds.Left < -0.5 || x.Bounds.Right > editor.Bounds.Width + 0.5)
                .Select(x => $"{x.Control.GetType().Name} {x.Control.Name} {x.Bounds}")
                .ToList();
            Assert.Empty(outside);
            return team;
        }, api);
    }
}
