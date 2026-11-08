using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Serientermine in der Windows-App: Vorschau und Anlegen im Termin, ↻ im Kalender, ab hier stornieren.</summary>
public sealed class SeriesTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static DateTimeOffset Weeks(int weeks, int hour = 10) => TestData.At(hour).AddDays(7 * weeks);

    private static async Task<(CalendarViewModel Vm, StubApi Api)> OpenAsync(CalendarDay day)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day);
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return (vm, api);
    }

    [Fact]
    public async Task Wiederholen_zeigt_Konflikte_bucht_nur_die_gewaehlten_und_zeigt_die_Serie()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var (vm, api) = await OpenAsync(TestData.Day(appointment));
        var seriesId = Guid.NewGuid();
        api.On($"POST /api/appointments/{appointment.Id}/series/preview", new List<SeriesOccurrence>
        {
            new(TestData.Monday.AddDays(28), Weeks(4), TestData.Anna.Id, "Anna", SeriesOccurrenceState.Free, null),
            new(TestData.Monday.AddDays(56), Weeks(8, 14), TestData.Anna.Id, "Anna", SeriesOccurrenceState.OtherTime, "Anna: belegt"),
            new(TestData.Monday.AddDays(84), Weeks(12), null, null, SeriesOccurrenceState.Conflict, "Anna: Urlaub"),
        });
        var starts = new List<DateTimeOffset> { appointment.Start, Weeks(4), Weeks(8, 14) };
        api.On($"POST /api/appointments/{appointment.Id}/series", new SeriesResult(
            new SeriesInfo(seriesId, 4, SeriesMode.Weekly, 4, null, starts, null, false),
            [appointment with { SeriesId = seriesId, UpdatedAt = TestData.At(10, 5) }], []));

        vm.OpenAppointment(appointment);
        var panel = vm.Selected!;
        Assert.True(panel.ShowRepeat);
        panel.EditSeriesCommand.Execute(null);
        panel.SeriesCount = "4";
        await panel.PreviewSeriesCommand.ExecuteAsync(null);

        Assert.Equal([true, true, false], panel.SeriesRows.Select(r => r.Take));
        Assert.False(panel.SeriesRows[2].CanTake);
        Assert.Equal("andere Uhrzeit · Anna: belegt", panel.SeriesRows[1].StateText);
        Assert.Equal("2 Termine buchen", panel.CreateSeriesText);

        await panel.CreateSeriesCommand.ExecuteAsync(null);
        var sent = JsonSerializer.Deserialize<SeriesCreate>(api.Calls.Last(c => c.Body is not null).Body!, ApiClient.Json)!;
        Assert.Equal(new SeriesInput(4, SeriesMode.Weekly, 4, null), sent.Rule);
        Assert.Equal([Weeks(4), Weeks(8, 14)], sent.Picks.Select(p => p.Start));
        Assert.Equal("Serie angelegt, 0 weitere Termine gebucht", vm.Toast);

        var item = vm.Columns[0].Items.Single();
        Assert.Equal("↻ 1/3", item.SeriesLabel);
        Assert.Contains("↻ alle 4 Wochen · Termin 1 von 3 · nächster Mo. 29.3.", item.HoverText, StringComparison.Ordinal);
        Assert.True(vm.Selected!.IsSeries);
        Assert.False(vm.Selected.ShowRepeat);
        Assert.Equal("Nur diesen stornieren", vm.Selected.CancelOneText);
    }

    [Fact]
    public async Task Verschieben_eines_Serientermins_laesst_die_folgenden_stehen_und_ab_hier_stornieren_beendet_die_Serie()
    {
        var seriesId = Guid.NewGuid();
        var appointment = TestData.Appointment(10, TestData.Anna) with { SeriesId = seriesId };
        var info = new SeriesInfo(seriesId, 2, SeriesMode.Weekly, null, TestData.Monday.AddDays(42), [appointment.Start, Weeks(2), Weeks(4)], null, false);
        var (vm, api) = await OpenAsync(TestData.Day(appointment) with { Series = [info] });
        var moved = appointment with { Start = TestData.At(14), End = TestData.At(14, 50), UpdatedAt = TestData.At(9) };
        api.On($"POST /api/appointments/{appointment.Id}/move", moved);

        var item = vm.Columns[0].Items[0];
        vm.BeginDrag(item, grabOffset: 0);
        vm.DragTo(10, item.Top + 240 * DayLayout.PixelsPerMinute);
        await vm.DropAsync();
        Assert.True(vm.CanUndo);
        Assert.Null(vm.Problem);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/series/follow", StringComparison.Ordinal));

        api.On($"POST /api/appointments/{appointment.Id}/series/cancel-following", new SeriesResult(
            info with { Starts = [], Ended = true }, [moved with { Status = AppointmentStatus.Cancelled, UpdatedAt = TestData.At(9, 30) }],
            [new SeriesFailure(Weeks(4), "Anna: Urlaub")]));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Alternative, "Serie beendet");
        vm.Dialogs = dialogs;
        vm.OpenAppointment(moved);
        Assert.True(vm.Selected!.CanCancelFollowing);
        await vm.Selected.AskCancelCommand.ExecuteAsync(null);
        Assert.Equal("Diesen und alle folgenden", dialogs.Asked.Single().AlternativeText);
        Assert.Equal("1 Termin storniert, Serie beendet", vm.Toast);
        Assert.Equal(AispaStudio.Desktop.Controls.InfoBarSeverity.Warning, vm.Problem!.Severity);
        Assert.Equal("Nicht frei: Mo. 29.3. (Anna: Urlaub)", vm.Problem.Message);
        Assert.Empty(vm.Columns[0].Items);
    }
}
