using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F17: Tooltip nach Design-Brief 5.22 (250 ms, Höchstbreite 240, Radius 8, Esc blendet aus, Tastaturfokus zeigt ihn).</summary>
public sealed partial class ViewTests
{
    private const string LongTip = "Ein ziemlich langer Hinweis, der umbrechen soll, wenn die Höchstbreite erreicht ist, und der nicht abgeschnitten werden darf.";

    [Fact]
    public Task Tooltip_Standardwerte_250_ms_Abstand_4_unter_dem_Ziel() => Session.Dispatch(() =>
    {
        var button = new Button();
        ToolTip.SetTip(button, "Hinweis");
        Assert.Equal(250, ToolTip.GetShowDelay(button));
        Assert.Equal(250, ToolTip.GetBetweenShowDelay(button));
        Assert.Equal(PlacementMode.Bottom, ToolTip.GetPlacement(button));
        Assert.Equal(4, ToolTip.GetVerticalOffset(button));

        // Ein am Steuerelement gesetzter Wert gewinnt (Hover-Karten im Kalender: 500 ms).
        var card = new Border();
        ToolTip.SetShowDelay(card, 500);
        ToolTip.SetTip(card, "Karte");
        Assert.Equal(500, ToolTip.GetShowDelay(card));
        return true;
    }, CancellationToken.None);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Tooltip_hat_die_Flaeche_der_Meldung_Radius_8_und_bricht_bei_240_um(string theme) =>
        InWindow(theme, () =>
        {
            var button = new Button { Content = "Speichern" };
            ToolTip.SetTip(button, LongTip);
            var disabled = new Button { Content = "Gesperrt", IsEnabled = false };
            ToolTip.SetTip(disabled, "Verfügbar, sobald ein Name eingetragen ist.");
            ToolTip.SetShowOnDisabled(disabled, true);
            return new StackPanel { Margin = new Thickness(40, 20), Spacing = 70, Children = { button, disabled } };
        }, 520, 260, async window =>
        {
            var panel = (StackPanel)window.Content!;
            ToolTip.SetIsOpen(panel.Children[0], true);
            ToolTip.SetIsOpen(panel.Children[1], true);
            await Pump();

            var tips = window.GetVisualDescendants().OfType<ToolTip>().ToList();
            Assert.Equal(2, tips.Count);
            var tip = tips.Single(t => t.Content as string == LongTip);
            Assert.Equal(ColorOf(window, "ToastBrush"), ColorOf(tip.Background));
            Assert.Equal(ColorOf(window, "ToastFgBrush"), ColorOf(tip.Foreground));
            Assert.Equal(new CornerRadius(8), tip.CornerRadius);
            Assert.Equal(new Thickness(10, 6), tip.Padding);
            Assert.Equal(12, tip.FontSize);
            Assert.Equal(240, tip.MaxWidth);
            Assert.True(tip.Bounds.Width <= 240.5, $"Breite {tip.Bounds.Width}");
            Assert.True(tip.Bounds.Height > 40, "Der lange Text bricht um, statt abgeschnitten zu werden");
            // Text auf der Fläche: 4,5:1 (Brief 2.6).
            Assert.True(Contrast(ColorOf(tip.Background), ColorOf(tip.Foreground)) >= 4.5);
        }, "tooltip");

    [Fact]
    public Task Esc_und_Strg_blenden_den_Tooltip_aus() => InWindow("hell", () => new Button { Content = "Anker" }, 300, 200, async window =>
    {
        var anchor = (Button)window.Content!;
        ToolTip.SetTip(anchor, "Hinweis");
        ToolTip.SetIsOpen(anchor, true);
        await Pump();
        Assert.True(ToolTip.GetIsOpen(anchor));

        Press(window, PhysicalKey.Escape);
        await Pump();
        Assert.False(ToolTip.GetIsOpen(anchor));

        ToolTip.SetIsOpen(anchor, true);
        await Pump();
        Press(window, PhysicalKey.ControlLeft);
        await Pump();
        Assert.False(ToolTip.GetIsOpen(anchor));
    });

    [Fact]
    public Task Tastaturfokus_zeigt_den_Tooltip_nach_250_ms_und_der_Fokusverlust_blendet_ihn_aus() =>
        InWindow("hell", () => new StackPanel { Children = { new Button { Content = "Vorher" }, new Button { Content = "Ziel" }, new Button { Content = "Nachher" } } }, 300, 200, async window =>
        {
            var panel = (StackPanel)window.Content!;
            var before = (Button)panel.Children[0];
            var target = (Button)panel.Children[1];
            ToolTip.SetTip(target, "Hinweis zum Ziel");
            before.Focus(NavigationMethod.Pointer);
            await Pump();

            // Mausfokus zeigt nichts.
            target.Focus(NavigationMethod.Pointer);
            await Pump(20);
            Assert.False(ToolTip.GetIsOpen(target));

            before.Focus(NavigationMethod.Pointer);
            target.Focus(NavigationMethod.Tab);
            Assert.False(ToolTip.GetIsOpen(target), "Nicht sofort, erst nach der Verzögerung");
            await Task.Delay(400);
            await Pump();
            Assert.True(ToolTip.GetIsOpen(target));

            before.Focus(NavigationMethod.Tab);
            await Pump();
            Assert.False(ToolTip.GetIsOpen(target));
        });

    [Fact]
    public Task Reicher_Inhalt_wird_zur_hellen_Hover_Karte_Text_bleibt_der_schlichte_Tooltip() => InWindow("hell", () => new StackPanel
    {
        Children = { new Button { Content = "Termin" }, new Button { Content = "Text" } },
    }, 600, 300, async window =>
    {
        var panel = (StackPanel)window.Content!;
        var rich = (Button)panel.Children[0];
        var plain = (Button)panel.Children[1];
        ToolTip.SetTip(rich, new TextBlock { Text = LongTip, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Width = 400 });
        ToolTip.SetTip(plain, "Nur Text");
        ToolTip.SetIsOpen(rich, true);
        ToolTip.SetIsOpen(plain, true);
        await Pump();

        var tips = window.GetVisualDescendants().OfType<ToolTip>().ToList();
        var card = tips.Single(t => t.Content is TextBlock);
        var text = tips.Single(t => t.Content is string);
        Assert.Contains("hovercard", card.Classes);
        Assert.DoesNotContain("hovercard", text.Classes);
        Assert.Equal(420, card.MaxWidth);
        Assert.Equal(ColorOf(window, "SurfaceBrush"), ColorOf(card.Background));
        Assert.Equal(ColorOf(window, "FgBrush"), ColorOf(card.Foreground));
        Assert.Equal(240, text.MaxWidth);
    });
}
