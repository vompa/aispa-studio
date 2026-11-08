using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Fluent 2 F21: Fehler und Warnungen einer Aktion stehen als InfoBar oben im Kalender (Fehler und Warnung assertive, Toast polite), der Toast ist eine Zeile.</summary>
public sealed partial class ViewTests
{
    /// <summary>Uhr wie <see cref="SampleTime"/>, deren Timer nur mit <see cref="Advance"/> feuern (Vorbild <c>CalendarNoticeTests.TimerTime</c>).</summary>
    private sealed class StoppedTime : TimeProvider
    {
        private readonly List<StoppedTimer> _timers = [];
        private DateTimeOffset _now = SpaSample.At(11, 40);

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new StoppedTimer(callback, state, _now + dueTime);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            foreach (var timer in _timers.Where(t => !t.Fired && t.Due <= _now).ToList())
            {
                timer.Fire();
            }
        }

        private sealed class StoppedTimer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            public DateTimeOffset Due { get; } = due;

            public bool Fired { get; private set; }

            public void Fire()
            {
                Fired = true;
                callback(state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static StubApi RejectedMove() => Api().On($"POST /api/appointments/{SpaSample.Selected.Id}/move",
        new BookingRejected("Zu dieser Zeit ist nicht alles frei.", [new BookingConflict(ResourceKind.Room, SpaSample.Lotus.Id, "Raum Lotus", "belegt")]), HttpStatusCode.Conflict);

    /// <summary>Der Gast hat eine feste Therapeut:in (Lena); der Server verschiebt den Termin zu Marco: Der Toast bestätigt, die Warnung bleibt als InfoBar.</summary>
    private static StubApi MovedAwayFromFixedTherapist()
    {
        var day = SpaSample.CalendarDay();
        var fixedToLena = new CustomerBookingPreferences(SpaSample.Lena.Id, true);
        return Api()
            .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19",
                day with { Guests = [.. day.Guests!.Select(g => g.CustomerId == SpaSample.Selected.CustomerId ? g with { Booking = fixedToLena } : g)] })
            .On($"POST /api/appointments/{SpaSample.Selected.Id}/move", SpaSample.Selected with { EmployeeId = SpaSample.Marco.Id, UpdatedAt = SpaSample.At(9, 50) });
    }

    private static async Task DragSelectedToSecondColumnAsync(CalendarViewModel calendar)
    {
        var item = calendar.Columns.SelectMany(c => c.Items).First(i => i.Appointment?.Id == SpaSample.Selected.Id);
        calendar.BeginDrag(item, 20);
        calendar.DragTo(DayLayout.ColumnLeft(1) + 40, item.Top + 60 * DayLayout.PixelsPerMinute);
        await calendar.DropAsync();
    }

    private static InfoBar ProblemBar(Window window) => window.GetVisualDescendants().OfType<InfoBar>().First(b => b.IsEffectivelyVisible);

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Fehler_einer_Aktion_steht_als_InfoBar_oben_assertive_und_nicht_als_Toast(string theme) => Render(theme, "meldung-fehler", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await DragSelectedToSecondColumnAsync(calendar);
        await Pump(Settling);

        var bar = ProblemBar(window);
        Assert.Equal(InfoBarSeverity.Error, bar.Severity);
        Assert.Equal("Nicht verschoben", bar.Title);
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(bar));
        Assert.NotNull(bar.CloseCommand);
        // Die Meldung steht über dem Kalender, nicht unten über dem Raster, und der Toast bleibt leer.
        var grid = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ToastBar");
        Assert.True(TopOf(window, bar) < TopOf(window, grid));
        Assert.False(calendar.HasToast);
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(grid));
        return calendar;
    }, RejectedMove());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Warnung_einer_Aktion_steht_als_InfoBar_mit_dem_Toast_als_Bestaetigung(string theme) => Render(theme, "meldung-warnung", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await DragSelectedToSecondColumnAsync(calendar);
        await Pump(Settling);

        var bar = ProblemBar(window);
        Assert.Equal(InfoBarSeverity.Warning, bar.Severity);
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(bar));
        Assert.True(calendar.HasToast);
        Assert.True(calendar.Toast!.Length <= CalendarViewModel.ToastMaxLength);
        return calendar;
    }, MovedAwayFromFixedTherapist(), time: new StoppedTime());

    [Fact]
    public Task Schliessen_blendet_die_InfoBar_aus_und_der_Toast_bleibt()
    {
        // Die Frist des Toasts läuft über die Uhr der App; mit echter Zeit ist sie unter Last schon vor dem Schließen abgelaufen.
        var clock = new StoppedTime();
        return Render("hell", "meldung-geschlossen", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await DragSelectedToSecondColumnAsync(calendar);
        await Pump(Settling);
        var bar = ProblemBar(window);
        Assert.True(calendar.HasToast);

        bar.CloseCommand!.Execute(null);
        await Pump(Settling);

        Assert.False(bar.IsEffectivelyVisible);
        Assert.True(calendar.HasToast);
        // Der Toast geht erst mit seiner eigenen Frist zu, nicht mit der Meldung: mit „Rückgängig“ 8 s.
        Assert.True(calendar.CanUndo);
        clock.Advance(TimeSpan.FromSeconds(7));
        await Pump();
        Assert.True(calendar.HasToast);
        clock.Advance(TimeSpan.FromSeconds(1));
        await Pump();
        Assert.False(calendar.HasToast);
        return calendar;
    }, MovedAwayFromFixedTherapist(), time: clock);
    }
}
