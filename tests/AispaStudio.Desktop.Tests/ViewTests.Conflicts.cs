using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

public sealed partial class ViewTests
{
    [Fact]
    public Task Kalender_Konfliktmeldung_nennt_die_Belegung_in_Ortszeit() => Render("hell", "kalender-konflikt", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        // Marco hat um 12:00 einen Termin (Dr. Bauer) und eine Pause; die Sperrzeit von 11:30 bis 13:30 passt nicht.
        calendar.OpenNoteAt(DayLayout.ColumnLeft(1, calendar.Scale) + 20, calendar.Hours.Single(h => h.Label == "11:00").Top + 4);
        Assert.True(calendar.SelectNewEntry(4));
        calendar.Entry!.Text = "Schulung";
        calendar.Entry.StartText = "11:30";
        calendar.Entry.EndText = "13:30";
        await calendar.Entry.SaveAsync();
        await Pump();

        var notice = Assert.Single(window.GetVisualDescendants().OfType<ConflictNoticeView>(), v => v.IsEffectivelyVisible);
        var texts = notice.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Zeitraum ist belegt.", texts);
        Assert.Contains("Termin · 11:55–13:00 · Dr. Wolfgang Bauer", texts);
        Assert.Contains("Sperrzeit · 13:00–13:30 · Mittagspause", texts);
        Assert.Contains("Zuerst verschieben oder einen anderen Zeitraum wählen.", texts);
        return calendar;
    }, ConflictingBlockApi());

    private static StubApi ConflictingBlockApi() => Api().On("POST /api/blocks", new BookingRejected("Zeitraum ist belegt.",
    [
        new BookingConflict(ResourceKind.Employee, SpaSample.Marco.Id, "Dr. Wolfgang Bauer", "hat hier einen Termin", SpaSample.At(11, 55), SpaSample.At(13),
            SpaSample.CalendarDay().Appointments.Single(a => a.CustomerName == "Dr. Wolfgang Bauer").Id),
        new BookingConflict(ResourceKind.Employee, SpaSample.Marco.Id, "Mittagspause", "ist gesperrt", SpaSample.At(13), SpaSample.At(13, 30), IsBlock: true),
    ]), HttpStatusCode.Conflict);

    [Fact]
    public Task Kalender_Konfliktmeldung_ist_eine_InfoBar_mit_Fehler_und_einem_Aktionsknopf() => Render("hell", "kalender-konflikt-infobar", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenNoteAt(DayLayout.ColumnLeft(1, calendar.Scale) + 20, calendar.Hours.Single(h => h.Label == "11:00").Top + 4);
        Assert.True(calendar.SelectNewEntry(4));
        calendar.Entry!.Text = "Schulung";
        calendar.Entry.StartText = "11:30";
        calendar.Entry.EndText = "13:30";
        await calendar.Entry.SaveAsync();
        await Pump();

        var notice = Assert.Single(window.GetVisualDescendants().OfType<ConflictNoticeView>(), v => v.IsEffectivelyVisible);
        var bar = Assert.Single(notice.GetVisualDescendants().OfType<InfoBar>());
        Assert.Equal(InfoBarSeverity.Error, bar.Severity);
        Assert.Equal("Zeitraum ist belegt.", bar.Title);
        Assert.DoesNotContain(notice.GetVisualDescendants().OfType<Border>(), b => b is not InfoBar && b.Classes.Contains("infobar"));
        var action = Assert.Single(notice.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("infobar-action") && b.IsEffectivelyVisible);
        Assert.Equal("Zum Konflikt", action.Content);
        Assert.NotNull(action.Command);
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(bar));

        // In der schmalen Seitenleiste steht die Aktion unter dem Text; daneben bliebe für den Titel eine Spalte von etwa 110 px.
        var title = notice.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Zeitraum ist belegt.");
        Assert.True(title.Bounds.Height < 24, $"Der Titel bricht um (Höhe {title.Bounds.Height}).");
        Assert.True(action.TranslatePoint(new Point(0, 0), window)!.Value.Y > title.TranslatePoint(new Point(0, title.Bounds.Height), window)!.Value.Y - 1);
        return calendar;
    }, ConflictingBlockApi());

    [Fact]
    public Task Kalender_Notiz_mit_Ende_steht_neben_dem_Termin_statt_darueber() => Render("hell", "kalender-notiz-neben-termin", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var appointment = calendar.Columns.SelectMany(c => c.Items).Single(i => i.Appointment?.Id == SpaSample.Selected.Id);
        var column = calendar.Columns.Single(c => c.Items.Contains(appointment));
        var sticky = Assert.Single(column.Notes);

        // Der Termin steht links, der Zettel mit Ende in der schmalen Bahn rechts; nichts liegt übereinander.
        Assert.True(appointment.Place.HasSide);
        Assert.True(sticky.Place.InSide);
        var cards = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("appt") && b.DataContext is CalendarItem { Place.HasSide: true }).ToList();
        var notes = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("sticky") && b.DataContext is CalendarNoteItem { Place.InSide: true }).ToList();
        var card = Assert.Single(cards);
        var note = Assert.Single(notes);
        Assert.True(card.TranslatePoint(new Point(card.Bounds.Width, 0), window)!.Value.X <= note.TranslatePoint(new Point(0, 0), window)!.Value.X + 0.5);
        Assert.Equal(sticky.Height, note.Bounds.Height, 1.0);
        return calendar;
    }, Api().On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with
    {
        Notes =
        [
            new CalendarNoteDto(Guid.NewGuid(), SpaSample.Day, "Herr Lang ruft wegen des Gutscheins zurück", ResourceKind.Employee, SpaSample.Lena.Id,
                SpaSample.At(10, 40), false, SpaSample.At(9), SpaSample.At(11, 10)),
        ],
    }));
}
