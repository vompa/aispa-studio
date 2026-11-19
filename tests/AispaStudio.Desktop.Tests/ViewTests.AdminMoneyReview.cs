using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Durchsicht der Verwaltung „Kassa &amp; Geld“, „Kontakt“ und „Zahlen“ (Befunde 106 bis 119): Zahlenspalten, Beschriftungen, Knopfformen.</summary>
public sealed partial class ViewTests
{
    private static TextBlock TextBlockShowing(Window window, string text) =>
        window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == text);

    private static double RightEdge(Window window, Control control) =>
        (control.TranslatePoint(new Point(control.Bounds.Width, 0), window) ?? throw new InvalidOperationException("Nicht im Fenster.")).X;

    [Fact]
    public Task Pflichtberichte_Zahlenspalten_der_Arbeitszeit_stehen_samt_Kopf_rechtsbuendig() => Render("hell", "geld-pflichtberichte-zahlen", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.ComplianceTab);

        // Die Werte „160,5“ (Stunden) und der Kopf „Stunden“ teilen dieselbe rechte Kante (design.md 5.7).
        var head = TextBlockShowing(window, "Stunden");
        var value = TextBlockShowing(window, "160,5");
        Assert.Equal(HorizontalAlignment.Right, head.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Right, value.HorizontalAlignment);
        Assert.Equal(RightEdge(window, head), RightEdge(window, value), 0.5);

        // Die Zahl der Pakete steht in der richtigen Mehrzahl.
        Assert.True(Shows(window, "1 offenes Paket mit 4 Behandlungen"));
        return shell.AdminPage;
    }, ReportsApi());
}
