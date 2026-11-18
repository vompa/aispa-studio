using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.ReportSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>„Mein Tag“ (Masken-Überarbeitung P9): einspaltige Karte bis 760 breit, Kopf mit Kennzeichen, Statuszeile, Vorlieben im eigenen Abschnitt. Geprüft wird als Inhaberin.</summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell", 1100, "1100")]
    [InlineData("dunkel", 1100, "1100")]
    [InlineData("hell", 1900, "breit")]
    [InlineData("dunkel", 1900, "breit")]
    public Task Mein_Tag_zeigt_die_Karte_einspaltig_mit_Kopf_Status_und_Wuenschen(string theme, int width, string label) =>
        Render(theme, $"verwaltung-mein-tag-{label}", async (window, main) =>
        {
            // When die Inhaberin „Mein Tag“ bei dieser Fensterbreite öffnet
            window.Width = width;
            var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);
            await Pump(Settling);

            // Then steht im Kopf der Name ohne die doppelte Dauer, die Dauer und der Raum stehen als Kennzeichen daneben, und die Statuszeile nennt die offene Vorbereitung.
            Assert.True(Shows(window, "10:00 Lava-Hot-Stone-Massage"));
            Assert.False(Shows(window, "(60 Min.)"));
            Assert.True(Shows(window, "60 Min."));
            Assert.True(Shows(window, "2 Vorbereitungen offen, erste bis 09:30"));
            Assert.True(Shows(window, "Steinwärmer bereitstellen"));
            Assert.True(Shows(window, "Das wünscht sich Anna"));
            Assert.True(Shows(window, "Duft: Lavendel"));

            // Und die Vorlieben stehen nicht noch einmal als Zeile der Vorbereitung; der Inhalt der Karte ist höchstens 760 breit.
            Assert.False(Shows(window, "Duft: Lavendel (Vorliebe)"));
            var heading = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Vorbereitung");
            var column = heading.GetVisualAncestors().OfType<StackPanel>().First(p => p.MaxWidth == 760);
            Assert.True(column.Bounds.Width <= 760.5, $"Die Karte ist {column.Bounds.Width} breit.");
            return shell.AdminPage;
        }, ReportsApi().On(DayPlanRoute, LavaDay()));
}
