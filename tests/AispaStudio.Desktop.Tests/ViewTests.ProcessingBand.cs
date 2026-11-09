using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Das Einwirkzeit-Band im Kalender ist eine flache Fläche im Ton der Vor- und Nachbereitungsstreifen mit feinen Linien oben und unten
/// (vorher schraffiert); sein Text bleibt in Hell und Dunkel lesbar.
/// </summary>
public sealed partial class ViewTests
{
    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Einwirkzeit_Band_ist_flach_im_Ton_der_Streifen_mit_lesbarem_Text(string theme)
    {
        var day = SpaSample.ProcessingDay();
        return Render(theme, "kalender-einwirkzeit-band", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            while (calendar.Scale.PixelsPerMinute < 1.2)
            {
                calendar.ZoomInCommand.Execute(null);
            }

            await Pump();

            var band = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("processing") && b.IsEffectivelyVisible);
            var stripe = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("stripe") && b.IsEffectivelyVisible);
            var text = Assert.Single(band.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible);

            // Flach statt schraffiert: einfarbig, im Flächenton der Streifen, mit Linien oben und unten.
            var fill = ColorOf(band.Background);
            Assert.Equal(ColorOf(window, "Surface3Brush"), fill);
            Assert.Equal(ColorOf(stripe.Background), fill);
            Assert.Equal(1.0, band.Opacity);
            Assert.Equal(new Avalonia.Thickness(0, 1, 0, 1), band.BorderThickness);
            Assert.Equal(ColorOf(window, "LineStrongBrush"), ColorOf(band.BorderBrush));
            Assert.False(band.IsHitTestVisible);

            // Text klein am unteren Rand, lesbar auf der Fläche.
            Assert.Equal(12, text.FontSize);
            Assert.Equal(VerticalAlignment.Bottom, text.VerticalAlignment);
            Assert.True(Contrast(ColorOf(text.Foreground), fill) >= 4.5, $"Text {ColorOf(text.Foreground)} auf {fill}: {Contrast(ColorOf(text.Foreground), fill):0.00}:1");
            return calendar;
        }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", day));
    }
}
