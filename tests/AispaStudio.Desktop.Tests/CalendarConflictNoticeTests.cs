using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Meldungen im Kalender (Slice 72, Ä79, Ä80, Ä94): eine Ortsregel (über dem Knopf der Aktion), die belegende Person und der Raum stehen in der Meldung,
/// „Zum Konflikt“ führt in die Woche der Person, „Kein Raum frei“ nennt Räume und eine nächste freie Zeit.
/// </summary>
public sealed class CalendarConflictNoticeTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static StubApi WeekApi(params AppointmentDto[] appointments)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources);
        for (var i = 0; i < 7; i++)
        {
            var date = TestData.Monday.AddDays(i);
            api.On($"GET /api/locations/{TestData.LocationId}/calendar?date={date:yyyy-MM-dd}", (i == 0 ? TestData.Day(appointments) : TestData.Day()) with { Date = date });
        }

        return api;
    }

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static (StubApi Api, AppointmentDto Moving, AppointmentDto Occupying) Rejection()
    {
        var moving = TestData.Appointment(10, TestData.Anna);
        var occupying = TestData.Appointment(11, TestData.Anna);
        var conflict = new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Anna", "ist zu dieser Zeit schon belegt", occupying.Start, occupying.End, occupying.Id);
        var api = WeekApi(moving, occupying)
            .On($"POST /api/appointments/{moving.Id}/move", new BookingRejected("Zeitraum ist belegt.", [conflict]), HttpStatusCode.Conflict);
        return (api, moving, occupying);
    }

    private static async Task RejectedDragAsync(CalendarViewModel vm)
    {
        vm.BeginDrag(vm.Columns[0].Items[0], grabOffset: 0);
        vm.DragTo(10, vm.Columns[0].Items[0].Top + 60 * DayLayout.PixelsPerMinute);
        await vm.DropAsync();
    }

    [Fact]
    public async Task Given_ein_belegter_Zeitraum_When_eine_Verschiebung_abgelehnt_wird_Then_nennt_die_Meldung_Person_und_Raum()
    {
        var (api, _, _) = Rejection();
        var vm = await OpenAsync(api);

        await RejectedDragAsync(vm);

        Assert.Equal("Nicht verschoben", vm.Problem!.Title);
        Assert.Equal("Zeitraum ist belegt. Termin · 11:00–11:50 · Anna · Raum Zeder", vm.Problem.Message);
    }

    [Fact]
    public async Task Given_Nicht_verschoben_When_Zum_Konflikt_gedrueckt_wird_Then_zeigt_der_Kalender_die_Woche_der_Person_mit_dem_belegenden_Termin()
    {
        var (api, _, occupying) = Rejection();
        var vm = await OpenAsync(api);
        await RejectedDragAsync(vm);
        Assert.Equal("Zum Konflikt", vm.Problem!.ActionText);

        vm.Problem.Action!.Execute(null);
        await Until(() => vm.MarkedId == occupying.Id);

        var filter = vm.Filter!;
        Assert.Equal((CalendarFilterKind.Employee, TestData.Anna.Id), (filter.Kind, filter.Id));
        Assert.Equal(TestData.Monday, vm.Date);
        Assert.Contains(vm.Columns.SelectMany(c => c.Items), i => i.Appointment?.Id == occupying.Id);
    }

    [Fact]
    public async Task Given_ein_Raum_im_Weg_When_Zum_Konflikt_Then_zeigt_der_Kalender_die_Woche_des_Raums()
    {
        var moving = TestData.Appointment(10, TestData.Anna);
        var occupying = TestData.Appointment(11, TestData.Ben);
        var conflict = new BookingConflict(ResourceKind.Room, TestData.Room.Id, TestData.Room.Name, "ist zu dieser Zeit schon belegt", occupying.Start, occupying.End, occupying.Id);
        var api = WeekApi(moving, occupying).On($"POST /api/appointments/{moving.Id}/move", new BookingRejected("Zeitraum ist belegt.", [conflict]), HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);

        await RejectedDragAsync(vm);
        Assert.Equal("Zeitraum ist belegt. Termin · 11:00–11:50 · Raum Zeder · Ben", vm.Problem!.Message);
        vm.Problem.Action!.Execute(null);
        await Until(() => vm.MarkedId == occupying.Id);

        Assert.Equal((CalendarFilterKind.Room, TestData.Room.Id), (vm.Filter!.Kind, vm.Filter.Id));
    }
}
