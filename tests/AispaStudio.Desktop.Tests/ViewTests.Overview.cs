using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde 5 Ä30: Tagesüberblick unter dem Chip „n Termine · n da · n offen“ (Design-Brief 5.22, 7): 420 breit, einzeilig, Klick und Tastatur in „Als Nächstes“.</summary>
public sealed partial class ViewTests
{
    private static Task<bool> WithOverview(string theme, string shot, Func<Window, CalendarViewModel, Panel, Task> act, StubApi? stub = null) =>
        Render(theme, shot, async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            var chip = Named<Button>(window, "OverviewChip");
            chip.Flyout!.ShowAt(chip);
            await Pump(Settling);
            var content = (Panel)((Flyout)chip.Flyout).Content!;
            await act(window, calendar, content);
            return calendar;
        }, stub);

    private static List<Button> UpcomingEntries(Panel content) =>
        [.. content.GetVisualDescendants().OfType<ItemsControl>().Single(l => l.Name == "UpcomingList").GetVisualDescendants().OfType<Button>()];

    private static void RaiseKey(Control target, Key key) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = KeyModifiers.None });

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Tagesueberblick_ist_420_breit_und_zeigt_Behandlung_und_Person_einzeilig_ungekuerzt(string theme) => WithOverview(theme, "kalender-tagesueberblick", (window, calendar, content) =>
    {
        Assert.Equal(420, content.Bounds.Width, 0.5);
        var entries = UpcomingEntries(content);
        Assert.Equal(calendar.Upcoming.Count, entries.Count);
        Assert.NotEmpty(entries);

        foreach (var entry in entries)
        {
            var lines = entry.GetVisualDescendants().OfType<TextBlock>().ToList();
            Assert.All(lines, line =>
            {
                // Einzeilig und nicht abgeschnitten: die natürliche Breite des Textes passt in die Breite des Textfelds.
                var arranged = line.Bounds.Width;
                line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(line.DesiredSize.Width <= arranged + 0.5, $"„{line.Text}“ ist gekürzt ({line.DesiredSize.Width:0} > {arranged:0})");
                Assert.True(line.Bounds.Height <= 20, $"„{line.Text}“ bricht um");
            });
            // Nichts Gekürztes, also kein Hinweis, der nur Sichtbares wiederholt.
            Assert.Null(ToolTip.GetTip(entry));
        }

        return Task.CompletedTask;
    });

    [Fact]
    public void Lange_Zeilen_enden_nach_44_Zeichen_mit_Ellipsis_und_der_volle_Text_steht_im_Hinweis()
    {
        var appointment = SpaSample.Selected;
        var kurz = new UpcomingItem("13:00", "Eva Muster", "Massage · Lena Huber", "#2f6f5e", appointment);
        Assert.Equal("Eva Muster", kurz.CustomerShown);
        Assert.Equal("Massage · Lena Huber", kurz.DetailShown);
        Assert.Null(kurz.FullText);

        var genau = new UpcomingItem("13:00", new string('A', 44), new string('B', 44), "#2f6f5e", appointment);
        Assert.Equal(new string('A', 44), genau.CustomerShown);
        Assert.Null(genau.FullText);

        var lang = new UpcomingItem("13:00", "Dr. Wolfgang Bauer-Niederegger und Begleitung (Gruppe Mayr)", "Aromaöl-Massage mit Kräuterstempel und Fußreflex · Katharina Huber-Aigner", "#2f6f5e", appointment);
        Assert.Equal(44, lang.CustomerShown.Length);
        Assert.EndsWith("…", lang.CustomerShown, StringComparison.Ordinal);
        Assert.Equal(44, lang.DetailShown.Length);
        Assert.EndsWith("…", lang.DetailShown, StringComparison.Ordinal);
        Assert.Equal($"{lang.Customer} · {lang.Detail}", lang.FullText);
    }

    [Fact]
    public Task Klick_auf_einen_Eintrag_markiert_den_Termin_oeffnet_das_Panel_und_schliesst_das_Fenster() => WithOverview("hell", "kalender-tagesueberblick-klick", async (window, calendar, content) =>
    {
        var chip = Named<Button>(window, "OverviewChip");
        var entries = UpcomingEntries(content);
        var target = calendar.Upcoming[1].Appointment;
        Assert.True(chip.Flyout!.IsOpen);

        entries[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Pump(Settling);

        Assert.Equal(target.Id, calendar.MarkedId);
        Assert.Equal(target.Id, calendar.Selected?.Appointment.Id);
        Assert.False(chip.Flyout.IsOpen);
        // Das Termin-Panel steht rechts und der Reiter nennt die Seite.
        Assert.Contains(calendar.OpenPanels, t => t.IsSelected && t.Text == "Termin");
    });

    [Fact]
    public Task Pfeile_Pos1_und_Ende_bewegen_den_Fokus_in_der_Liste_Enter_oeffnet_den_Termin() => WithOverview("hell", "kalender-tagesueberblick-tasten", async (window, calendar, content) =>
    {
        var chip = Named<Button>(window, "OverviewChip");
        var entries = UpcomingEntries(content);
        Assert.True(entries.Count >= 3, $"{entries.Count} Einträge");

        entries[0].Focus(NavigationMethod.Tab);
        await Pump();
        Assert.True(entries[0].IsFocused);

        RaiseKey(entries[0], Key.Down);
        Assert.True(entries[1].IsFocused);
        RaiseKey(entries[1], Key.Down);
        Assert.True(entries[2].IsFocused);
        RaiseKey(entries[2], Key.Up);
        Assert.True(entries[1].IsFocused);
        RaiseKey(entries[1], Key.End);
        Assert.True(entries[^1].IsFocused);
        // Am Ende bleibt der Fokus stehen, es gibt keinen Umlauf.
        RaiseKey(entries[^1], Key.Down);
        Assert.True(entries[^1].IsFocused);
        RaiseKey(entries[^1], Key.Home);
        Assert.True(entries[0].IsFocused);
        RaiseKey(entries[0], Key.Up);
        Assert.True(entries[0].IsFocused);

        // Enter auf dem fokussierten Eintrag öffnet den Termin und schließt das Fenster.
        RaiseKey(entries[0], Key.Down);
        RaiseKey(entries[1], Key.Enter);
        await Pump(Settling);
        Assert.Equal(calendar.Upcoming[1].Appointment.Id, calendar.Selected?.Appointment.Id);
        Assert.False(chip.Flyout!.IsOpen);
    });
}
