using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Runde 5 Ä28: Der Hinweis am Streifen „Vorb. 15 Min.“ war ein dunkler Kasten ohne lesbaren Text. Ursache: Der Tooltip hängt logisch unter dem Streifen,
/// und die Regel „Border.stripe TextBlock“ (Text fg) färbte auch den Text im Tooltip, der auf der dunklen Meldungsfläche stand.
/// </summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Hinweis_am_Streifen_ist_lesbar_mit_Kontrast_ab_4_5(string theme) => Render(theme, "tooltip-streifen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var stripe = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("stripe") && b.IsEffectivelyVisible && b.DataContext is CalendarStripe { Tip.Length: > 0 });
        var tip = Assert.IsType<string>(ToolTip.GetTip(stripe));

        ToolTip.SetIsOpen(stripe, true);
        await Pump();
        var popup = window.GetVisualDescendants().OfType<ToolTip>().Single(t => t.Content as string == tip);
        var text = popup.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(Contrast(ColorOf(popup.Background), ColorOf(text.Foreground)) >= 4.5,
            $"Text {ColorOf(text.Foreground)} auf {ColorOf(popup.Background)}");
        Assert.Equal(12, text.FontSize);
        Assert.Equal(new Thickness(0), text.Margin);
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Jeder_Texthinweis_im_Kalender_ist_lesbar(string theme) => Render(theme, "tooltip-kalender", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        var targets = window.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && ToolTip.GetTip(c) is string { Length: > 0 })
            .ToList();
        Assert.True(targets.Count > 20, $"{targets.Count} Hinweise gefunden");

        var failures = new List<string>();
        foreach (var target in targets)
        {
            ToolTip.SetIsOpen(target, true);
            await Pump(2);
            var popup = window.GetVisualDescendants().OfType<ToolTip>().FirstOrDefault(t => ReferenceEquals(t.Content, ToolTip.GetTip(target)));
            if (popup is null)
            {
                failures.Add($"{Describe(target)}: Hinweis erscheint nicht");
            }
            else
            {
                var text = popup.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
                var contrast = text?.Foreground is null ? 0 : Contrast(ColorOf(popup.Background), ColorOf(text.Foreground));
                if (contrast < 4.5)
                {
                    failures.Add($"{Describe(target)}: Kontrast {contrast:0.0}");
                }
            }

            ToolTip.SetIsOpen(target, false);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Jede_Hover_Karte_im_Kalender_ist_lesbar_Spaltenkoepfe_und_Termine(string theme) => Render(theme, "tooltip-karten", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        // Reiche Hinweise (Karte mit Zeilen, Symbol und Text): Spaltenköpfe, Termine, Streifen mit Karte.
        var targets = window.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && ToolTip.GetTip(c) is not (null or string))
            .ToList();
        Assert.True(targets.Count > 10, $"{targets.Count} Karten gefunden");

        var failures = new List<string>();
        foreach (var target in targets)
        {
            ToolTip.SetIsOpen(target, true);
            await Pump(2);
            var popup = window.GetVisualDescendants().OfType<ToolTip>().FirstOrDefault(t => ReferenceEquals(t.Content, ToolTip.GetTip(target)));
            if (popup is null)
            {
                failures.Add($"{Describe(target)}: Karte erscheint nicht");
            }
            else
            {
                var background = ColorOf(popup.Background);
                foreach (var text in popup.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text) && t.Foreground is ISolidColorBrush))
                {
                    var contrast = Contrast(background, ColorOf(text.Foreground));
                    if (contrast < 4.5)
                    {
                        failures.Add($"{target.GetType().Name} „{text.Text}“: Kontrast {contrast:0.0}");
                    }
                }
            }

            ToolTip.SetIsOpen(target, false);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct()));
        return calendar;
    });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Die_Hover_Karte_des_Termins_zeigt_Behandlung_Tag_Zeit_und_Raum_in_Grundschrift_fett_und_nichts_ueberlappt(string theme) => Render(theme, "hover-karte-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        var target = window.GetVisualDescendants().OfType<Control>()
            .First(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && ToolTip.GetTip(c) is HoverCard { HasParts: true });
        var card = (HoverCard)ToolTip.GetTip(target)!;
        ToolTip.SetIsOpen(target, true);
        await Pump(2);
        var popup = window.GetVisualDescendants().OfType<ToolTip>().First(t => ReferenceEquals(t.Content, card));
        var texts = popup.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();

        foreach (var expected in new[] { card.Treatment!, card.DayTime!, card.Room! })
        {
            var text = texts.First(t => t.Text == expected);
            Assert.Equal(14, text.FontSize);
            Assert.Equal(Avalonia.Media.FontWeight.SemiBold, text.FontWeight);
        }

        // Etikett und Wert einer Zeile greifen nicht ineinander: die Etikett-Spalte ist breit genug für das längste Etikett.
        foreach (var label in popup.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("muted") && t.IsEffectivelyVisible && t.Parent is Grid))
        {
            Assert.True(label.DesiredSize.Width <= label.Bounds.Width + 0.5, $"Etikett „{label.Text}“ ist abgeschnitten");
        }

        ToolTip.SetIsOpen(target, false);
        return calendar;
    });

    private static string Describe(Control c) => $"{c.GetType().Name}.{string.Join('.', c.Classes)} „{ToolTip.GetTip(c)}“";
}
