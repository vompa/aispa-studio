using AispaStudio.Desktop.Controls;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F18: Zeile 2 der Kalender-Kopfzeile hat ein Überlauf-Menü „⋯“ statt waagrechten Scrollens (Design-Brief 7, 5.21).</summary>
public sealed partial class ViewTests
{
    private static readonly string[] RowTwoChipNames = ["HeadTeam", "HeadRooms", "OverviewChip", "HeadSauna", "HeadWaitlist", "HeadStaffFilter"];

    private static OverflowStrip RowTwo(Window window) => Named<OverflowStrip>(window, "HeaderRow2");

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task Kopfzeile_zeigt_bei_1280_und_1000_alle_Chips_einzeilig_ohne_Scrollen(int width) => Render("hell", $"kalender-kopfzeile-{width}", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        window.Width = width;
        await Pump();
        await Pump();

        var strip = RowTwo(window);
        Assert.Empty(strip.GetVisualDescendants().OfType<ScrollViewer>());
        // Das Fenster ist mindestens 1100 breit; die Chips werden kompakt, sobald die Spalten unter 1100 liegen (7).
        Assert.True(calendar.IsCompactHeader);
        var line = TopOf(window, Named<Button>(window, "HeadTeam"));
        foreach (var name in RowTwoChipNames)
        {
            var chip = Named<Button>(window, name);
            if (!chip.IsEffectivelyVisible)
            {
                continue;
            }

            // Jedes sichtbare Chip steht auf einer Linie und ist im Rahmen der Zeile; nichts bricht um.
            Assert.Equal(line, TopOf(window, chip), 0.5);
            Assert.True(LeftOf(window, chip) + chip.Bounds.Width <= LeftOf(window, strip) + strip.Bounds.Width + 0.5, name);
        }

        // Bei diesen Breiten passt alles: kein Überlauf-Menü, alle Chips bedienbar.
        Assert.False(strip.OverflowButton.IsEffectivelyVisible);
        Assert.Empty(strip.Overflowed);
        return calendar;
    }, HiddenStaffApi());

    [Fact]
    public Task Wird_es_zu_schmal_wandern_die_Chips_von_rechts_in_das_Menue_und_die_Tastatur_erreicht_es() => Render("hell", "kalender-kopfzeile-ueberlauf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        window.Width = 1000;
        await Pump();

        // Ein sehr schmales Fenster oder eine breite Seitenleiste lassen der Zeile wenig Platz; hier legt der Test die Breite der Zeile fest (330).
        await Pump(Settling);
        var strip = RowTwo(window);
        strip.MaxWidth = 330;
        await Pump();
        var more = strip.OverflowButton;
        Assert.True(more.IsEffectivelyVisible, "Überlauf-Menü erscheint");
        Assert.Equal(34, more.Bounds.Height, 0.5);
        Assert.Contains("headchip", more.Classes);
        Assert.Equal("⋯", more.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Empty(strip.GetVisualDescendants().OfType<ScrollViewer>());

        // Das Menü steht am rechten Ende der Zeile, die verbleibenden Chips links davon auf einer Linie, nichts ragt hinaus.
        Assert.Equal(LeftOf(window, strip) + strip.Bounds.Width, LeftOf(window, more) + more.Bounds.Width, 0.5);
        var overflowed = strip.Overflowed;
        Assert.NotEmpty(overflowed);
        var line = TopOf(window, Named<Button>(window, "HeadTeam"));
        foreach (var chip in strip.Children.OfType<Border>().Where(c => !OverflowStrip.GetIsOverflowed(c) && c.Bounds.Width > 0))
        {
            Assert.Equal(line, TopOf(window, chip.Child!), 0.5);
            Assert.True(LeftOf(window, chip) + chip.Bounds.Width <= LeftOf(window, more) + 0.5);
        }

        // Von rechts nach links: zuerst der rechts angeheftete Personenfilter, dann vom Ende der Reihe; der Chip ganz links (Team) bleibt am längsten.
        Assert.Contains(overflowed, c => (c as Border)?.Child is Button { Name: "HeadStaffFilter" } || (c as Border)?.Child is Button { Name: "HeadWaitlist" });
        Assert.DoesNotContain(overflowed, c => (c as Border)?.Child is Button { Name: "HeadTeam" });
        // Chips im Menü nehmen keinen Platz, sind unsichtbar und nicht per Tab erreichbar.
        Assert.All(overflowed, c =>
        {
            Assert.Equal(0, c.Bounds.Width);
            Assert.Equal(0, c.Opacity);
            Assert.False(c.IsEffectivelyEnabled);
        });

        // Tab erreicht jedes sichtbare Chip und das Menü, nie ein verborgenes.
        var visibleButtons = strip.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled && b.Focusable).ToList();
        Assert.Contains(more, visibleButtons);
        Assert.DoesNotContain(visibleButtons, b => overflowed.Any(o => o.IsVisualAncestorOf(b)));

        // Das Menü öffnet mit Enter vom Fokus aus; die Einträge tragen Symbol und vollen Text in der Reihenfolge der Zeile; Pfeil ab wählt, Enter löst den Chip aus.
        more.Focus(NavigationMethod.Tab);
        await Pump();
        Press(window, PhysicalKey.Enter);
        await Pump();
        var menu = (MenuFlyout)more.Flyout!;
        Assert.True(menu.IsOpen);
        var entries = menu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(overflowed.Count, entries.Count);
        Assert.Equal(overflowed.Select(c => OverflowStrip.GetHeader(c)), entries.Select(e => e.Header as string));
        Assert.All(entries, e => Assert.NotNull(e.Header));
        var waitlist = entries.SingleOrDefault(e => (e.Header as string)?.StartsWith("Warteliste", StringComparison.Ordinal) == true);
        Assert.NotNull(waitlist);
        Assert.True(waitlist!.Icon is Icon { Width: 16 });
        await Pump(Settling);
        Assert.All(entries, e => Assert.True(e.Bounds.Height > 0, $"{e.Header} ist nicht zu sehen"));
        SaveShot(window, "kopfzeile-ueberlauf-menue", "hell");

        // Ein Eintrag löst dasselbe aus wie das Chip: Warteliste öffnet ihre Seitenleiste.
        Assert.False(calendar.HasPanel);
        waitlist.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        await Pump();
        Assert.NotNull(calendar.WaitlistPanel);
        return calendar;
    }, HiddenStaffApi());
}
