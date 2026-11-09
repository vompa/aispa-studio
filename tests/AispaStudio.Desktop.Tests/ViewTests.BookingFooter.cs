using AispaStudio.Desktop.Controls;

using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>A4 (B7, B13): „Neuer Termin“ hat die Meldung und „Termin buchen“ fest im Fuß der Seitenleiste (design.md 5.1) und legt den Knopf unter den Text.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Neuer_Termin_zeigt_nach_Zeitraum_ist_belegt_Meldung_und_Termin_buchen_ganz_im_sichtbaren_Bereich() => Render("hell", "kalender-termin-fuss", async (window, main) =>
    {
        window.Height = 700;
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        calendar.Booking!.ShowNewCustomer = true;
        calendar.Booking.Error = "Zeitraum ist belegt.";
        await Pump();

        var panel = SidePanelOf(window);
        var bar = panel.GetVisualDescendants().OfType<InfoBar>().Single(b => b.IsEffectivelyVisible && b.Message == "Zeitraum ist belegt.");
        var book = panel.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Content as string == "Termin buchen");
        var rail = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PanelRail");
        var railTop = BoundsIn(rail, window).Top;

        // Beide liegen ganz im Fenster und über der Reiterleiste, ohne dass der Inhalt gescrollt wurde.
        Assert.True(BoundsIn(bar, window).Bottom <= railTop + 0.5, $"Meldung endet bei {BoundsIn(bar, window).Bottom}, Reiterleiste beginnt bei {railTop}");
        Assert.True(BoundsIn(book, window).Bottom <= railTop + 0.5, $"Knopf endet bei {BoundsIn(book, window).Bottom}, Reiterleiste beginnt bei {railTop}");
        Assert.True(BoundsIn(bar, window).Top >= 0 && BoundsIn(book, window).Top >= 0);
        Assert.True(BoundsIn(bar, window).Bottom <= BoundsIn(book, window).Top + 0.5, "Die Meldung steht über dem Knopf");
        // Fest im Fuß: nicht im scrollenden Inhalt.
        Assert.DoesNotContain(book.GetVisualAncestors(), a => a is ScrollViewer);
        Assert.DoesNotContain(bar.GetVisualAncestors(), a => a is ScrollViewer);
        return calendar;
    });

    [Fact]
    public Task Neuer_Termin_legt_Freie_Zeit_suchen_unter_den_Text_der_Spalte() => Render("hell", "kalender-termin-spalte-knopf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump();

        var panel = SidePanelOf(window);
        var text = panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible && t.Text == calendar.Booking!.ColumnScopeText);
        var search = panel.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && Equals(b.Content, "Freie Zeit suchen"));

        Assert.True(BoundsIn(search, window).Top >= BoundsIn(text, window).Bottom - 0.5, "Der Knopf steht unter dem Text, nicht daneben");
        Assert.True(BoundsIn(text, window).Width >= BoundsIn(search, window).Width, "Der Text hat die ganze Breite");
        return calendar;
    });
}
