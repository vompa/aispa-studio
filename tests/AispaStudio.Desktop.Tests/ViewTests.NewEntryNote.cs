using AispaStudio.Desktop.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R04 Slice 82 (Ä148, Ä149): Die Notiz in „Neuer Termin“, Paar/Spa, Sauna und in der Art „Notiz“ ist mehrzeilig und wächst mit dem Text bis zur Höchsthöhe,
/// der Reiter unten trägt den Gast und das Kennzeichen „in Bearbeitung“.
/// </summary>
public sealed partial class ViewTests
{
    private const string ThreeLines = "Lavendelöl\nwenig Gespräch\nDruck eher fest";

    private static TextBox NoteBox(Visual root, string name) =>
        root.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == name && t.IsEffectivelyVisible);

    /// <summary>Nichts ist abgeschnitten: der Inhalt ist nicht höher als die sichtbare Fläche des Felds.</summary>
    private static void AssertShowsAllLines(TextBox box)
    {
        Assert.True(box.AcceptsReturn);
        var scroller = box.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Height <= scroller.Viewport.Height + 0.5,
            $"Inhalt {scroller.Extent.Height:0.#} px, sichtbar {scroller.Viewport.Height:0.#} px");
        Assert.True(box.Bounds.Height >= 68, $"Höhe {box.Bounds.Height:0.#}");
    }

    [Fact]
    public Task Notiz_mit_drei_Zeilen_ist_drei_Zeilen_hoch_und_nichts_ist_abgeschnitten() => Render("hell", "kalender-termin-notiz-mehrzeilig", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        var booking = calendar.Booking!;
        await Pump();
        var box = NoteBox(SidePanelOf(window), "BookingNoteBox");
        var empty = box.Bounds.Height;

        booking.Note = ThreeLines;
        await Pump();
        AssertShowsAllLines(box);
        Assert.Equal(68, empty, 0.5);

        // Lange Wünsche: das Feld wächst bis 200 und scrollt dann.
        booking.Note = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"Zeile {i}"));
        await Pump();
        Assert.Equal(200, box.Bounds.Height, 0.5);
        return calendar;
    });

    [Fact]
    public Task Notiz_in_Paar_Spa_Sauna_und_Notiz_ist_mehrzeilig_und_schneidet_nichts_ab() => Render("hell", "kalender-eintrag-notiz-mehrzeilig", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump();

        Assert.True(calendar.SelectNewEntry(2));
        calendar.GroupBooking!.Note = ThreeLines;
        await Pump();
        AssertShowsAllLines(NoteBox(SidePanelOf(window), "GroupNoteBox"));

        Assert.True(calendar.SelectNewEntry(6));
        await PumpUntil(() => calendar.Areas!.ShowForm);
        calendar.Areas!.Note = ThreeLines;
        await Pump();
        AssertShowsAllLines(NoteBox(SidePanelOf(window), "AreaNoteBox"));

        Assert.True(calendar.SelectNewEntry(5));
        calendar.Entry!.Text = ThreeLines;
        await Pump();
        AssertShowsAllLines(NoteBox(SidePanelOf(window), "EntryTextBox"));

        // Der Grund einer Sperrzeit bleibt eine Zeile.
        Assert.True(calendar.SelectNewEntry(4));
        await Pump();
        Assert.False(NoteBox(SidePanelOf(window), "EntryTextBox").AcceptsReturn);
        return calendar;
    });

    [Fact]
    public Task Reiter_unten_nennt_den_Gast_und_zeigt_den_Punkt_in_Bearbeitung() => Render("hell", "kalender-reiter-neuer-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        var guest = SpaSample.Customers[0];
        calendar.Booking!.Customers.Add(guest);
        calendar.Booking.Customer = guest;
        await Pump();

        var rail = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PanelRail");
        var tab = rail.GetVisualDescendants().OfType<TabButton>().Single(t => t.IsSelected);
        Assert.Equal($"Neuer Termin · {guest.InHouseName}, in Bearbeitung", AutomationProperties.GetName(tab));
        Assert.Contains(tab.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == $"Neuer Termin · {guest.InHouseName}");
        Assert.Contains(tab.GetVisualDescendants().OfType<Ellipse>(), e => e.Name == "InProgressDot" && e.IsEffectivelyVisible);
        return calendar;
    });
}
