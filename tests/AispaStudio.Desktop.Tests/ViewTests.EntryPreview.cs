using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ä67: Vorschau des geplanten Eintrags im Kalender, solange das Fenster „Neuer Termin“ offen ist.</summary>
public sealed partial class ViewTests
{
    /// <summary>Der Text der gestrichelten Vorschau-Karte in der angegebenen Farbe (Akzent bzw. Warnung); sichtbar nur, wenn sie wirklich gezeichnet wird.</summary>
    private static TextBlock? PreviewText(Window window, string text, string brush) =>
        window.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == text && t.IsEffectivelyVisible && ColorOf(t.Foreground) == ColorOf(window, brush) && t.FontWeight == Avalonia.Media.FontWeight.SemiBold);

    [Fact]
    public Task Termin_Vorschau_zeigt_den_geplanten_Termin_in_der_Spalte_mit_Vor_und_Nachbereitung() => Render("hell", "termin-vorschau", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        // Lena: Lücke von 12:00 bis 13:00; die Massage passt genau hinein.
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump(Settling);

        var ghost = calendar.Ghost;
        Assert.NotNull(ghost);
        Assert.Equal("Klassische Massage", ghost.Text);
        Assert.False(ghost.IsWarning);
        Assert.True(ghost.Height > 0);
        Assert.NotNull(PreviewText(window, "Klassische Massage", "AccentBrush"));
        return calendar;
    });

    [Fact]
    public Task Termin_Vorschau_bricht_einen_langen_Namen_um_statt_ihn_abzuschneiden() => Render("hell", "termin-vorschau-umbruch", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump(Settling);

        var preview = PreviewText(window, calendar.Ghost!.Text, "AccentBrush");
        Assert.NotNull(preview);
        Assert.Equal(Avalonia.Media.TextWrapping.Wrap, preview.TextWrapping);
        return calendar;
    });

    [Fact]
    public Task Termin_Vorschau_steht_in_Warnfarbe_wenn_die_Zeit_belegt_ist() => Render("hell", "termin-vorschau-belegt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        // Eine längere Behandlung als die freie Zeit.
        calendar.Booking!.Treatment = calendar.Booking.Treatments[^1];
        calendar.Booking.StartText = "12:30";
        await Pump(Settling);

        Assert.True(calendar.Ghost!.IsWarning);
        Assert.NotNull(PreviewText(window, calendar.Ghost.Text, "WarnBrush"));
        Assert.Null(PreviewText(window, calendar.Ghost.Text, "AccentBrush"));
        return calendar;
    });
}
