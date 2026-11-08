using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Slice 71 (Ä83, Ä88, Ä93): Vorschau ziehen, Notizrand ziehen, Abbruch beim Verlassen der Kalenderfläche, alles mit der Maus.</summary>
public sealed partial class ViewTests
{
    /// <summary>Zieht in fünf Schritten von <paramref name="from"/> nach <paramref name="to"/>, ohne loszulassen.</summary>
    private static async Task DragAsync(Window window, Point from, Point to)
    {
        window.MouseDown(from, MouseButton.Left);
        for (var step = 1; step <= 5; step++)
        {
            window.MouseMove(from + (to - from) * (step / 5.0));
            await Pump();
        }
    }

    [Fact]
    public Task Gefilterte_Woche_Vorschau_auf_Mittwoch_10_Uhr_ziehen_setzt_Tag_und_Beginn_im_Formular() => Render("hell", "kalender-vorschau-ziehen", async (window, main) =>
    {
        var calendar = await FilterToLenaAsync(window, main);
        var at = InHost(window, DayLayout.ColumnLeft(0, calendar.Scale) + 20, FreeY(calendar, 0));
        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump(Settling);
        // Eine kurze Lücke schlägt Pause vor; die Vorschau zum Ziehen ist die des Termins.
        calendar.SelectNewEntry(1);
        await Pump();
        var customer = calendar.Booking!.Customer;
        var treatment = calendar.Booking.Treatment;
        var ghost = calendar.Ghost!;

        // Die Vorschau 5 Pixel unter ihrem oberen Rand greifen und mit dem oberen Rand auf Mittwoch (dritte Spalte) 10:00 ziehen.
        var ten = calendar.Hours.Single(h => h.Label == "10:00").Top;
        await DragAsync(window, InHost(window, ghost.Left + 20, ghost.Top + 5), InHost(window, DayLayout.ColumnLeft(2, calendar.Scale) + 20, ten + 5));
        Assert.True(calendar.IsDragging);
        window.MouseUp(InHost(window, DayLayout.ColumnLeft(2, calendar.Scale) + 20, ten + 5), MouseButton.Left);
        await Pump(Settling);

        Assert.False(calendar.IsDragging);
        Assert.Equal(new DateOnly(2026, 10, 21), calendar.Booking!.Date);
        Assert.Equal("10:00", calendar.Booking.StartText);
        Assert.Equal("Lena Huber", calendar.Booking.ColumnTitle);
        Assert.Equal(customer, calendar.Booking.Customer);
        Assert.Equal(treatment, calendar.Booking.Treatment);
        Assert.Equal(DayLayout.ColumnLeft(2, calendar.Scale), calendar.Ghost!.Left);
        return calendar;
    }, WeekApi());

    [Fact]
    public Task Kalender_Notiz_unteres_Ende_30_Minuten_ziehen_setzt_Bis_30_Minuten_spaeter()
    {
        var note = new CalendarNoteDto(Guid.NewGuid(), SpaSample.Day, "Lieferung Öle", ResourceKind.Employee, SpaSample.Marco.Id, SpaSample.At(10), false,
            SpaSample.At(8), SpaSample.At(10, 30));
        var stub = Api()
            .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.CalendarDay() with { Notes = [note] })
            .On($"PATCH /api/calendar-notes/{note.Id}", note with { Until = SpaSample.At(11) });
        return Render("hell", "kalender-notiz-ziehen", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            await Pump();
            var view = window.GetVisualDescendants().OfType<CalendarView>().Single();
            var sticky = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("sticky") && b.DataContext is CalendarNoteItem { Id: var id } && id == note.Id);
            var edge = sticky.TranslatePoint(new Point(sticky.Bounds.Width / 2, sticky.Bounds.Height - 2), window)!.Value;

            // Über dem Rand zeigt der Zeiger, dass sich die Höhe ziehen lässt.
            window.MouseMove(edge);
            await Pump();
            Assert.Equal(StandardCursorType.SizeNorthSouth, view.PointerShape);

            var to = edge + new Point(0, 30 * calendar.Scale.PixelsPerMinute);
            await DragAsync(window, edge, to);
            Assert.True(calendar.IsDragging);
            window.MouseUp(to, MouseButton.Left);
            await Pump(Settling);

            var sent = System.Text.Json.JsonSerializer.Deserialize<CalendarNoteChange>(
                stub.Calls.Last(c => c.Route == $"PATCH /api/calendar-notes/{note.Id}").Body!, AispaStudio.Desktop.Services.ApiClient.Json)!;
            Assert.Equal(SpaSample.At(11), sent.Until);
            Assert.Null(sent.At);
            // Ein Ziehen am Rand öffnet die Bearbeitung nicht; ein Klick in die Mitte schon, und dort steht das neue Ende.
            Assert.Null(calendar.NewEntry);
            sticky = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("sticky") && b.DataContext is CalendarNoteItem { Id: var id } && id == note.Id);
            var middle = sticky.TranslatePoint(new Point(sticky.Bounds.Width / 2, sticky.Bounds.Height / 2), window)!.Value;
            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);
            await Pump(Settling);
            Assert.Equal("11:00", calendar.Entry!.EndText);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Kalender_Ziehen_bricht_ab_wenn_die_Maus_die_Kalenderflaeche_verlaesst()
    {
        var stub = Api();
        return Render("hell", "kalender-ziehen-verlassen", async (window, main) =>
        {
            var calendar = await SignedInCalendar(main);
            calendar.ByRoom = true;
            await Pump();
            var view = window.GetVisualDescendants().OfType<CalendarView>().Single();
            var body = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "BodyScroll");
            var from = CenterOf(window, SpaSample.Selected.Id);
            var inside = new Point(from.X + calendar.Scale.ColumnWidth + 8, from.Y - 30 * calendar.Scale.PixelsPerMinute);
            await DragAsync(window, from, inside);
            Assert.True(calendar.IsDragging);

            // Links über die Zeitleiste hinaus, klar außerhalb der Kalenderfläche.
            var outside = body.TranslatePoint(new Point(-30, body.Bounds.Height / 2), window)!.Value;
            window.MouseMove(outside);
            await Pump();

            Assert.False(calendar.IsDragging);
            Assert.Null(calendar.Ghost);
            Assert.Equal(StandardCursorType.Arrow, view.PointerShape);

            // Zurück in den Kalender und loslassen: nichts wird verschoben, nichts geöffnet.
            window.MouseMove(inside);
            window.MouseUp(inside, MouseButton.Left);
            await Pump();
            Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/move", StringComparison.Ordinal));
            Assert.Null(calendar.Selected);
            Assert.Equal(SpaSample.Selected.Start, calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id).Appointment!.Start);
            return calendar;
        }, stub);
    }
}
