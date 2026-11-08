using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ä63, Ä71: „Kommt 15 Min. später“ im Kontextmenü; das Menü schließt, der Termin bleibt an seiner Zeit, es öffnet sich kein Fenster.</summary>
public sealed partial class ViewTests
{
    private static StubApi LateApi() => Api().On($"PUT /api/appointments/{SpaSample.Selected.Id}/late",
        SpaSample.Selected with { LateMinutes = 15, UpdatedAt = SpaSample.At(9, 50) });

    private static async Task ChooseFromContextMenuAsync(Window window, string header)
    {
        var at = CenterOf(window, SpaSample.Selected.Id);
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        await Pump(Settling);

        var menu = window.GetVisualDescendants().OfType<ContextMenu>().Single();
        var item = menu.GetVisualDescendants().OfType<MenuItem>().Single(i => HeaderOf(i) == header);
        var click = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;
        window.MouseDown(click, MouseButton.Left);
        window.MouseUp(click, MouseButton.Left);
        await Pump(Settling);
    }

    [Fact]
    public Task Kommt_spaeter_per_Maus_laesst_den_Termin_stehen_und_Doch_puenktlich_nimmt_es_zurueck()
    {
        var api = LateApi();
        return Render("hell", "kalender-verspaetung-menue", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            var current = () => calendar.Columns.SelectMany(c => c.Items).First(x => x.Appointment?.Id == SpaSample.Selected.Id);

            // Die Frist des Toasts läuft mit echter Zeit (SampleTime stellt nur die Uhr); unter Last schloss er sich vor der Prüfung.
            // Festgehalten wie mit der Maus darüber, damit der Test nur den Text prüft, nicht die Frist.
            calendar.HoldToast(true);
            await ChooseFromContextMenuAsync(window, "Kommt 15 Min. später");

            // Das Menü ist zu und bleibt zu, der Termin steht an seiner Zeit mit der Markierung, der Toast bestätigt.
            Assert.Empty(window.GetVisualDescendants().OfType<ContextMenu>());
            Assert.Equal(SpaSample.Selected.Start, current().Appointment!.Start);
            Assert.Contains("kommt 10:45", current().MetaText, StringComparison.Ordinal);
            Assert.StartsWith("Brigitte Müller kommt 15 Min. später (", calendar.Toast, StringComparison.Ordinal);
            Assert.Null(calendar.Problem);

            // Der Server meldet die eigene Änderung auch an diesen Arbeitsplatz zurück (Live); derselbe Stand ändert nichts.
            calendar.ApplyLive(current().Appointment! with { Warnings = current().Appointment!.Warnings?.ToList() });
            await Pump(Settling);
            Assert.Empty(window.GetVisualDescendants().OfType<ContextMenu>());
            Assert.Contains("kommt 10:45", current().MetaText, StringComparison.Ordinal);

            // „Doch pünktlich“ löscht die Markierung.
            api.On($"PUT /api/appointments/{SpaSample.Selected.Id}/late", SpaSample.Selected with { UpdatedAt = SpaSample.At(9, 55) });
            await ChooseFromContextMenuAsync(window, "Doch pünktlich");
            Assert.Equal("Brigitte Müller kommt doch pünktlich", calendar.Toast);
            Assert.DoesNotContain("kommt ", current().MetaText, StringComparison.Ordinal);
            return calendar;
        }, api);
    }
}
