using AispaStudio.Contracts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Schritt 17: Kopfzeile des Kalenders in zwei Zeilen mit einheitlichen Kopfzeilen-Chips (Design-Brief 5.8 und 7, Ä16 und Ä17).</summary>
public sealed partial class ViewTests
{
    private static readonly string[] HeadChipNames = ["HeadTeam", "HeadRooms", "OverviewChip", "HeadSauna", "HeadWaitlist", "HeadMessages", "HeadStaffFilter"];

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static double TopOf(Window window, Control control) => control.TranslatePoint(new Point(0, 0), window)!.Value.Y;

    private static double LeftOf(Window window, Control control) => control.TranslatePoint(new Point(0, 0), window)!.Value.X;

    private static bool ShowsText(Control chip, string text) =>
        chip.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text?.Contains(text, StringComparison.Ordinal) == true);

    /// <summary>Beispieltag mit einer Person ohne Dienst, damit der Personenfilter („Alle zeigen“) in der Kopfzeile steht.</summary>
    private static StubApi HiddenStaffApi() => Api().On($"GET /api/locations/{SpaSample.LocationId}/resources",
        SpaSample.Resources with { Employees = [.. SpaSample.Resources.Employees, new EmployeeDto(Guid.NewGuid(), "Clara", "#c27a2c", [], [])] });

    [Fact]
    public Task Kopfzeile_hat_zwei_Zeilen_Warteliste_und_Filter_stehen_in_Zeile_zwei_und_alle_Elemente_sind_Kopfzeilen_Chips() => Render("hell", "kalender-kopfzeile-zwei-zeilen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        Assert.True(calendar.HasWaitlist);
        Assert.True(calendar.CanToggleAllStaff);
        Assert.False(calendar.IsCompactHeader);

        // Alle Elemente beider Zeilen (außer Datum, Pfeile, Suche, Aktualisieren und dem primären Knopf) tragen den Stil „headchip“, Höhe 34.
        foreach (var name in HeadChipNames)
        {
            var chip = Named<Button>(window, name);
            Assert.Contains("headchip", chip.Classes);
            if (chip.IsEffectivelyVisible)
            {
                Assert.Equal(34, chip.Bounds.Height, 0.5);
            }
        }

        var row2 = Named<Control>(window, "HeaderRow2");
        var row2Buttons = row2.GetVisualDescendants().OfType<Button>().Where(b => b is not RepeatButton && b.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(row2Buttons);
        Assert.All(row2Buttons, b => Assert.Contains("headchip", b.Classes));
        Assert.All(row2Buttons, b => Assert.Equal(34, b.Bounds.Height, 0.5));

        // Zeile 1: „Meldungen“ steht rechts oben; Zeile 2: Team/Räume, Zähler, Warteliste links und der Personenfilter rechts auf derselben Linie.
        var messages = Named<Button>(window, "HeadMessages");
        var team = Named<Button>(window, "HeadTeam");
        var waitlist = Named<Button>(window, "HeadWaitlist");
        var filter = Named<Button>(window, "HeadStaffFilter");
        var counter = Named<Button>(window, "OverviewChip");
        Assert.True(waitlist.IsEffectivelyVisible && filter.IsEffectivelyVisible);
        Assert.True(TopOf(window, team) > TopOf(window, messages) + 20, "Zeile 2 steht unter Zeile 1");
        foreach (var chip in new[] { Named<Button>(window, "HeadRooms"), counter, waitlist, filter })
        {
            Assert.Equal(TopOf(window, team), TopOf(window, chip), 0.5);
        }

        Assert.True(LeftOf(window, filter) > LeftOf(window, waitlist), "Der Filter steht rechts von der Warteliste");
        Assert.True(LeftOf(window, filter) + filter.Bounds.Width > window.Bounds.Width - 120, "Der Filter steht am rechten Rand");

        // Der Umschalter Team/Räume sind zwei Chips, der gewählte `on`.
        Assert.Contains("on", team.Classes);
        Assert.DoesNotContain("on", Named<Button>(window, "HeadRooms").Classes);
        return calendar;
    }, HiddenStaffApi());

    [Fact]
    public Task Bei_schmaler_Breite_werden_die_Chips_kompakt_und_die_Kopfzeile_bleibt_gleich_hoch() => Render("hell", "kalender-kopfzeile-kompakt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var header = Named<Control>(window, "CalendarHeader");
        var wideHeight = header.Bounds.Height;
        var waitlist = Named<Button>(window, "HeadWaitlist");
        Assert.True(ShowsText(waitlist, "Warteliste"));

        window.Width = 1000;
        await Pump();
        await Pump();

        Assert.True(calendar.IsCompactHeader);
        // Symbol und Zahl statt Text; der Text steht im Hinweis.
        Assert.False(ShowsText(waitlist, "Warteliste"));
        Assert.Contains("Warteliste", ToolTip.GetTip(waitlist) as string, StringComparison.Ordinal);
        Assert.Equal(wideHeight, header.Bounds.Height, 0.5);

        // Kein Umbruch: alle Chips der Zeile 2 stehen weiter auf einer Linie.
        var line = TopOf(window, Named<Button>(window, "HeadTeam"));
        foreach (var name in HeadChipNames.Where(n => n != "HeadMessages"))
        {
            var chip = Named<Button>(window, name);
            if (chip.IsEffectivelyVisible)
            {
                Assert.Equal(line, TopOf(window, chip), 0.5);
            }
        }

        return calendar;
    }, HiddenStaffApi());

    [Fact]
    public Task Kopfzeile_der_Raumansicht_hat_keinen_Personenfilter() => Render("hell", "kalender-kopfzeile-raeume", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump();

        Assert.False(Named<Button>(window, "HeadStaffFilter").IsEffectivelyVisible);
        Assert.Contains("on", Named<Button>(window, "HeadRooms").Classes);
        Assert.DoesNotContain("on", Named<Button>(window, "HeadTeam").Classes);
        return calendar;
    }, HiddenStaffApi());
}
