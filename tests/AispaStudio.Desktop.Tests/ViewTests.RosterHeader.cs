using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>T-101 (Aufgabe 4): Die Kopfzeile des Wochenplans mit dem Menü „Ausgabe“ steht in einer Zeile, nichts springt beim Blättern, das Menü und die Meldung schieben den Plan nicht, und das Menü gehört der Tastatur.</summary>
public sealed partial class ViewTests
{
    private static Point PositionIn(Window window, Control control) => control.TranslatePoint(default, window) ?? throw new InvalidOperationException(control.Name);

    [Fact]
    public Task Wochenplan_Kopfzeile_bleibt_beim_Blaettern_ueber_vier_Wochen_an_derselben_Stelle()
    {
        // Ohne Entwürfe: das Band „Veröffentlichen · Verwerfen“ verschiebt den Plan in Wochen mit Entwurf um seine Höhe und ist bewusst nicht Teil dieser Aufgabe (PR-Protokoll).
        var (api, _) = RosterApi();
        _ = new RosterSample(api, SpaSample.Vienna, SpaSample.Day.AddDays(21), RosterLena, SpaSample.Marco);
        return Render("hell", "verwaltung-team-wochenplan-kopf-1100", async (window, main) =>
        {
            await SetSize(window, 1100, 900);
            var team = await OpenWeekPlan(window, main);
            var plan = team.Plan!;
            var output = VisibleNamed<Button>(window, "RosterOutputButton");
            var requests = VisibleNamed<Button>(window, "RequestsButton");
            var table = VisibleNamed<StackPanel>(window, "RosterTable");
            var header = VisibleNamed<Grid>(window, "RosterHeader");
            var title = VisibleNamed<TextBlock>(window, "WeekTitleText");
            var start = (Output: PositionIn(window, output), Requests: PositionIn(window, requests), Table: PositionIn(window, table).Y);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"1100 px: Kopfzeile {header.Bounds.Width:0}x{header.Bounds.Height:0}, Ausgabe x={start.Output.X:0} y={start.Output.Y:0} ({output.Bounds.Width:0} breit), Anträge x={start.Requests.X:0} ({requests.Bounds.Width:0} breit), "
                + $"Wochentitel {title.Bounds.Width:0} (Text {title.DesiredSize.Width:0}), Tabelle y={start.Table:0}");
            Assert.True(header.Bounds.Height <= output.Bounds.Height + 8, $"Kopfzeile {header.Bounds.Height:0} hoch: sie bricht um (Knopf {output.Bounds.Height:0})");

            var titles = new List<string> { plan.WeekTitle };
            for (var week = 0; week < 4; week++)
            {
                // Einmal mit der Bestätigung „Gespeichert: …“ (Toast der Shell), einmal ohne: sie belegt keinen Platz in der Zeile.
                if (week % 2 == 0)
                {
                    team.Toast.Show("Gespeichert: Dienstplan_2026-10-19.xlsx");
                }

                var toastShown = team.Toast.IsShown;
                plan.NextWeekCommand.Execute(null);
                await Pump(Settling);
                titles.Add(plan.WeekTitle);
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"{plan.WeekTitle}: Toast {toastShown}, Ausgabe {PositionIn(window, output)}, Anträge {PositionIn(window, requests)}, Tabelle y={PositionIn(window, table).Y:0}, Kopfzeile y={PositionIn(window, header).Y:0}, Titel {title.Bounds.Width:0}");
                Assert.Equal(start.Output, PositionIn(window, output));
                Assert.Equal(start.Requests, PositionIn(window, requests));
                Assert.Equal(start.Table, PositionIn(window, table).Y);
                Assert.True(title.DesiredSize.Width <= title.Bounds.Width + 0.5, $"Wochentitel {plan.WeekTitle} ({title.DesiredSize.Width:0}) breiter als sein Platz ({title.Bounds.Width:0})");
            }

            Assert.Equal(5, titles.Distinct().Count());
            plan.ThisWeekCommand.Execute(null);
            await Pump(Settling);
            return team;
        }, api);
    }

    [Fact]
    public Task Wochenplan_Menue_Ausgabe_liegt_ueber_dem_Plan_und_schiebt_ihn_nicht()
    {
        var (api, _) = RosterWeekApi();
        return Render("hell", "verwaltung-team-wochenplan-menue-1100", async (window, main) =>
        {
            await SetSize(window, 1100, 900);
            var team = await OpenWeekPlan(window, main);
            var table = VisibleNamed<StackPanel>(window, "RosterTable");
            var output = VisibleNamed<Button>(window, "RosterOutputButton");
            var before = PositionIn(window, table);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "RosterDocumentCard" || c.Name == "RosterExportSaved" || c.Name == "RosterDocumentButton");

            var header = VisibleNamed<Grid>(window, "RosterHeader");
            var left = (Panel)header.Children[0];
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"1100 px: linke Seite braucht {left.DesiredSize.Width:0} von {left.Bounds.Width:0} (frei {left.Bounds.Width - left.DesiredSize.Width:0}), Kopfzeile {header.Bounds.Height:0} hoch, Ausgabe rechts bei {PositionIn(window, output).X + output.Bounds.Width:0} von {PositionIn(window, header).X + header.Bounds.Width:0}");
            output.Flyout!.ShowAt(output);
            await Pump(Settling);

            Assert.True(output.Flyout.IsOpen);
            Assert.Equal(before, PositionIn(window, table));
            var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).Select(i => i.Header as string).ToList();
            Assert.Equal(
            [
                "Aushang · veröffentlichter Stand", "Drucken …", "Als PDF speichern", "Liste · mit Entwürfen",
                "Dienstplan der Woche als Excel", "Dienstplan der Woche als CSV", "Abwesenheiten als Excel", "Abwesenheiten als CSV",
            ], items);

            // Die Gruppenüberschrift ist ein abgeschalteter Eintrag, bleibt aber lesbar (fg-2 auf der Menüfläche, kein halbes Grau).
            var heading = window.GetVisualDescendants().OfType<MenuItem>().First(i => i.IsEffectivelyVisible && i.Header as string == "Liste · mit Entwürfen");
            var headingText = heading.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == "Liste · mit Entwürfen");
            var headingContrast = Contrast(SolidColor(headingText.Foreground), ColorOf(output, "SurfaceBrush"));
            Assert.True(headingContrast >= 4.5 && heading.Opacity >= 1, $"Gruppenüberschrift {headingContrast:0.00}:1, Deckkraft {heading.Opacity}");
            return team;
        }, api);
    }

    [Fact]
    public Task Wochenplan_Menue_Ausgabe_ist_mit_der_Tastatur_allein_erreichbar_und_Esc_gibt_den_Fokus_zurueck()
    {
        var (api, _) = RosterWeekApi();
        return Render("hell", "verwaltung-team-wochenplan-menue-tastatur", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            var output = VisibleNamed<Button>(window, "RosterOutputButton");
            output.Focus();
            await Pump(Settling);
            Assert.Same(output, Focused(window));

            Press(window, PhysicalKey.Enter);
            await Pump(Settling);
            Assert.True(output.Flyout!.IsOpen);

            // Gruppenüberschriften sind abgeschaltet: die Pfeile überspringen sie und erreichen jeden Befehl.
            var reached = new List<string?>();
            if (Focused(window) is not MenuItem)
            {
                Press(window, PhysicalKey.ArrowDown);
                await Pump();
            }

            for (var step = 0; step < 6; step++)
            {
                reached.Add((Focused(window) as Control)?.Name);
                Press(window, PhysicalKey.ArrowDown);
                await Pump();
            }

            Assert.Equal(["PrintItem", "SavePdfItem", "RosterExcelItem", "RosterCsvItem", "AbsencesExcelItem", "AbsencesCsvItem"], reached);

            Press(window, PhysicalKey.Escape);
            await Pump(Settling);
            Assert.False(output.Flyout.IsOpen);
            Assert.Same(output, Focused(window));
            return team;
        }, api);
    }
}
