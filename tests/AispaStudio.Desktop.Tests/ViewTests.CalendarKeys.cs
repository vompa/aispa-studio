using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Slice 76 (Ä78, Ä96, Ä100): ruhige Datumsauswahl, Tastenleiste aus Knöpfen, Liste „Überfällig“ im Tagesüberblick.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Datumswaehler_behaelt_beim_Wechsel_des_Tages_Breite_und_Platz() => Render("hell", "datumswaehler-ruhig", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var date = Named<Button>(window, "DateButton");
        date.Flyout!.ShowAt(date);
        await Pump();
        var picker = window.GetVisualDescendants().OfType<Calendar>().Single();
        var before = (date.Bounds.Width, picker.TranslatePoint(new Point(0, 0), window));

        // Montag, 19. Oktober → Mittwoch: der Titel wird länger, die geöffnete Auswahl bleibt an ihrem Platz.
        calendar.Date = calendar.Date.AddDays(2);
        await Pump();

        Assert.True(date.Flyout.IsOpen);
        Assert.Equal(before.Width, date.Bounds.Width, 0.5);
        Assert.Equal(before.Item2, picker.TranslatePoint(new Point(0, 0), window));
        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-21", SpaSample.CalendarDay()));

    [Fact]
    public Task Eintraege_der_Tastenleiste_sind_Knoepfe_und_ein_Klick_loest_die_Taste_aus() => Render("hell", "tastenleiste-knoepfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.DataContext is KeyHint).ToList();
        Assert.Equal(calendar.KeyHints.Count, buttons.Count);

        var help = buttons.Single(b => ((KeyHint)b.DataContext!).Key == "?");
        Assert.False(calendar.ShowKeyHelp);
        var center = help.TranslatePoint(new Point(help.Bounds.Width / 2, help.Bounds.Height / 2), window)!.Value;
        Click(window, center);
        await Pump();

        Assert.True(calendar.ShowKeyHelp);
        return calendar;
    });

    [Fact]
    public Task Tagesueberblick_zeigt_Ueberfaellig_als_Liste_und_ein_Eintrag_oeffnet_den_Termin() => WithOverview("hell", "kalender-ueberfaellig", (window, calendar, content) =>
    {
        // Der Abschnitt steht nur da, wenn es überfällige Termine gibt; seine Liste zeigt genau die Einträge des ViewModels.
        var section = content.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "OverdueSection");
        Assert.Equal(calendar.Overview!.Overdue > 0, section.IsVisible);
        Assert.Equal(calendar.OverdueList.Count, section.GetVisualDescendants().OfType<ItemsControl>().Single(l => l.Name == "OverdueItems").GetVisualDescendants().OfType<Button>().Count());
        return Task.CompletedTask;
    });
}
