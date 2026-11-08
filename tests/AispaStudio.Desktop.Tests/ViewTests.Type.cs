using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F23: die Schriftstufen des Briefs (Abschnitt 3) mit Größe und Zeilenhöhe, als Muster zum Ansehen (hell und dunkel).</summary>
public sealed partial class ViewTests
{
    private static Control TypeSample() => new StackPanel
    {
        Margin = new Thickness(24),
        Spacing = 8,
        Children =
        {
            new TextBlock { Text = "Titel 22/28 Semibold", Classes = { "h1" } },
            new TextBlock { Text = "Überschrift 18/24 Semibold", Classes = { "h2" } },
            new TextBlock { Text = "Zwischentitel 16/22 Semibold", Classes = { "h3" } },
            new TextBlock { Text = "Text 14/20 Regular: Der Termin von Mia Muster beginnt um 10:30." },
            new TextBlock { Text = "Klein 12/16 Regular: Hilfetext unter einem Feld.", Classes = { "small" } },
            new TextBlock { Text = "Beschriftung 12/16 Medium", Classes = { "label" } },
            new TextBlock { Text = "Gruppentitel in Satzschreibweise", Classes = { "group" } },
            new Border { Classes = { "pill", "accent" }, Child = new TextBlock { Text = "Mini 12/14 Medium" } },
        },
    };

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Schriftstufen_Muster_hat_die_Groessen_und_Zeilenhoehen_des_Briefs(string theme) => InWindow(theme, TypeSample, 420, 340, async window =>
    {
        await Pump();
        var blocks = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text is { Length: > 0 }).ToList();
        (double Size, double Line, FontWeight Weight) Of(string start) => blocks.Where(t => t.Text!.StartsWith(start, StringComparison.Ordinal)).Select(t => (t.FontSize, t.LineHeight, t.FontWeight)).Single();
        Assert.Equal((22, 28, FontWeight.SemiBold), Of("Titel"));
        Assert.Equal((18, 24, FontWeight.SemiBold), Of("Überschrift"));
        Assert.Equal((16, 22, FontWeight.SemiBold), Of("Zwischentitel"));
        Assert.Equal((14, double.NaN, FontWeight.Normal), Of("Text 14"));
        Assert.Equal((12, 16, FontWeight.Normal), Of("Klein"));
        Assert.Equal((12, 16, FontWeight.Medium), Of("Beschriftung"));
        Assert.Equal((12, 16, FontWeight.Medium), Of("Gruppentitel"));
        Assert.Equal((12, 14, FontWeight.Medium), Of("Mini"));
    }, "schriftstufen");
}
