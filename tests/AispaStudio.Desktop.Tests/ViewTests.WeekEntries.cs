using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ä64: In der gefilterten 7-Tage-Ansicht sind die Tagesspalten vollwertige Spalten der Person: Klick, Rechtsklick und Ziehen mit der Maus.</summary>
public sealed partial class ViewTests
{
    private static Point InHost(Window window, double x, double y)
    {
        var host = window.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "Host");
        return host.TranslatePoint(new Point(x, y), window)!.Value;
    }

    private static async Task<CalendarViewModel> FilterToLenaAsync(Window window, MainViewModel main)
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var at = HeaderOf(window, "Lena Huber");
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump();
        await Pump();
        Assert.Equal(7, calendar.Columns.Count);
        return calendar;
    }

    /// <summary>Eine freie Stelle in der Spalte: dort zeigt die Vorschau unter der Maus, dass gebucht werden kann.</summary>
    private static double FreeY(CalendarViewModel calendar, int column)
    {
        var x = DayLayout.ColumnLeft(column, calendar.Scale) + 20;
        var span = calendar.Columns[column].Available.First();
        for (var y = span.Top + 20; y < span.Top + span.Height; y += 15)
        {
            calendar.Hover(x, y);
            if (calendar.Ghost is not null)
            {
                calendar.ClearHover();
                return y;
            }
        }

        throw new InvalidOperationException("Keine freie Stelle in der Spalte");
    }

    [Fact]
    public Task Gefilterte_Woche_Klick_auf_freie_Zeit_oeffnet_Neuer_Termin_fuer_die_Person_am_Tag_der_Spalte() => Render("hell", "kalender-woche-neuer-termin", async (window, main) =>
    {
        var calendar = await FilterToLenaAsync(window, main);
        var y = FreeY(calendar, 2);
        var at = InHost(window, DayLayout.ColumnLeft(2, calendar.Scale) + 20, y);

        window.MouseMove(at);
        await Pump();
        Assert.NotNull(calendar.Ghost);
        Assert.Equal(DayLayout.ColumnLeft(2, calendar.Scale), calendar.Ghost.Left);

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        await Pump(Settling);

        Assert.NotNull(calendar.NewEntry);
        Assert.Equal("Lena Huber", calendar.Booking!.ColumnTitle);
        Assert.True(calendar.IsFiltered);
        return calendar;
    }, WeekApi());

    [Fact]
    public Task Gefilterte_Woche_Rechtsklick_auf_freie_Zeit_zeigt_das_Menue_mit_Tag_und_Person() => Render("hell", "kalender-woche-menue", async (window, main) =>
    {
        var calendar = await FilterToLenaAsync(window, main);
        var at = InHost(window, DayLayout.ColumnLeft(3, calendar.Scale) + 20, FreeY(calendar, 3));

        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        await Pump(Settling);

        var menu = window.GetVisualDescendants().OfType<ContextMenu>().Single();
        var headers = menu.GetVisualDescendants().OfType<MenuItem>().Select(HeaderOf).ToList();
        Assert.StartsWith("Termin hier (Do. 22.10.", headers[0], StringComparison.Ordinal);
        Assert.EndsWith(", Lena Huber)", headers[0], StringComparison.Ordinal);
        Assert.Contains(headers, h => h == "Sperrzeit hier");
        return calendar;
    }, WeekApi());

    [Fact]
    public Task Gefilterte_Woche_Ziehen_ueber_Tage_verschiebt_den_Termin_auf_den_Tag_der_Zielspalte() => Render("hell", "kalender-woche-ziehen", async (window, main) =>
    {
        var calendar = await FilterToLenaAsync(window, main);
        var item = calendar.Columns[0].Items.First(i => i.Appointment is not null && calendar.CanMove(i));
        var start = CenterOf(window, item.Appointment!.Id);
        var target = InHost(window, DayLayout.ColumnLeft(4, calendar.Scale) + 60, item.Top + 12);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(30, 0));
        window.MouseMove(target);
        await Pump();
        Assert.True(calendar.IsDragging);
        Assert.Equal(DayLayout.ColumnLeft(4, calendar.Scale), calendar.Ghost!.Left);
        Assert.EndsWith("· Fr. 23.10.", calendar.Ghost.Text, StringComparison.Ordinal);
        window.MouseUp(target, MouseButton.Left);
        await Pump();
        return calendar;
    }, WeekApi());
}
