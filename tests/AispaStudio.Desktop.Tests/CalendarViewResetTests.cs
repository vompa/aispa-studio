using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Slice 76: Tastenleiste mit Knöpfen (Ä96), „Ansicht zurücksetzen“ (Ä99) und die Liste „Überfällig“ im Tagesüberblick (Ä100).</summary>
public sealed class CalendarViewResetTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10, 30);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static StubApi Api(params AppointmentDto[] appointments)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources);
        for (var i = 0; i < 7; i++)
        {
            api.On($"GET /api/locations/{TestData.LocationId}/calendar?date={TestData.Monday.AddDays(i):yyyy-MM-dd}", i == 0 ? TestData.Day(appointments) : TestData.Day());
        }

        return api;
    }

    private static async Task<CalendarViewModel> OpenAsync(params AppointmentDto[] appointments)
    {
        var vm = new CalendarViewModel(Api(appointments).Client(), TestData.Owner(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Given_die_Tastenleiste_When_Esc_Markierung_aufheben_angeklickt_wird_Then_ist_die_Markierung_weg()
    {
        var appointment = TestData.Appointment(13, TestData.Anna);
        var vm = await OpenAsync(appointment);
        vm.Mark(vm.Columns[0].Items.Single(i => i.Id == appointment.Id));
        Assert.Equal(appointment.Id, vm.MarkedId);

        var hint = Assert.Single(vm.KeyHints, h => h.Text == "Markierung aufheben");
        Assert.True(hint.IsEnabled);
        vm.PressKeyHintCommand.Execute(hint);

        Assert.Null(vm.MarkedId);
    }

    [Fact]
    public async Task Jeder_Eintrag_der_Tastenleiste_ist_gesperrt_mit_Grund_oder_loest_seine_Taste_aus()
    {
        var empty = await OpenAsync();
        var down = Assert.Single(empty.KeyHints, h => h.Key == "↓");
        Assert.False(down.IsEnabled);
        Assert.Equal("Heute gibt es keine Termine", down.Tip);

        var withAppointment = await OpenAsync(TestData.Appointment(13, TestData.Anna));
        Assert.True(withAppointment.KeyHints.Single(h => h.Key == "↓").IsEnabled);
        // Eine Taste mit Pfeilen ohne eindeutige Richtung bleibt gesperrt und nennt den Grund, statt still nichts zu tun.
        var days = withAppointment.KeyHints.Single(h => h.Key == "← →");
        Assert.False(days.IsEnabled);
        Assert.False(string.IsNullOrEmpty(days.BlockedReason));
        Assert.All(withAppointment.KeyHints, h => Assert.True(h.IsEnabled || h.BlockedReason is not null));
    }

    [Fact]
    public async Task Given_ein_gefilterter_Kalender_When_das_Kuerzel_zum_Zuruecksetzen_gedrueckt_wird_Then_zeigt_er_die_Ansicht_des_ersten_Oeffnens()
    {
        var vm = await OpenAsync(TestData.Appointment(13, TestData.Anna));
        Assert.DoesNotContain(vm.KeyHints, h => h.Text == "Ansicht zurücksetzen");

        vm.FilterBy(vm.Columns[0]);
        await Until(() => vm.IsFiltered && vm.Columns.Count == 7);
        Assert.Contains(vm.KeyHints, h => h.Text == "Ansicht zurücksetzen" && h.Key == "Strg+R");

        Assert.True(vm.HandleKey(Key.R, KeyModifiers.Control));
        await Until(() => !vm.IsFiltered && vm.Columns.Count == 2);

        Assert.Equal(TestData.Monday, vm.Date);
        Assert.False(vm.ByRoom);
        Assert.DoesNotContain(vm.KeyHints, h => h.Text == "Ansicht zurücksetzen");
        Assert.False(vm.HandleKey(Key.R, KeyModifiers.Control));
    }

    [Fact]
    public async Task Das_Zuruecksetzen_stellt_auch_Raumansicht_und_Hervorhebung_her_und_laesst_offene_Leisten_stehen()
    {
        var appointment = TestData.Appointment(13, TestData.Anna);
        var vm = await OpenAsync(appointment);
        vm.ShowRoomsCommand.Execute(null);
        vm.FocusOnCommand.Execute(CalendarFocus.Appointments);
        vm.OpenAppointment(appointment);
        Assert.NotNull(vm.Selected);
        Assert.True(vm.CanResetView);

        vm.HandleKey(Key.R, KeyModifiers.Control);
        await Until(() => !vm.ByRoom && !vm.HasFocus);

        Assert.False(vm.CanResetView);
        Assert.Equal(appointment.Id, vm.Selected?.Appointment.Id);
    }

    [Fact]
    public async Task Given_sieben_ueberfaellige_Termine_When_der_Tagesueberblick_oeffnet_Then_stehen_fuenf_Eintraege_und_und_2_weitere_da()
    {
        // Jetzt ist 10:30; Termine ab 9:40 (bis 10:50) sind begonnen und nicht eingecheckt.
        var overdue = Enumerable.Range(0, 7)
            .Select(i => TestData.Appointment(10, i % 2 == 0 ? TestData.Anna : TestData.Ben) with { Start = TestData.At(9, 40 + i), End = TestData.At(10, 50) })
            .ToArray();
        var vm = await OpenAsync(overdue);

        Assert.Equal(7, vm.Overview!.Overdue);
        Assert.Equal(5, vm.OverdueList.Count);
        Assert.Equal("und 2 weitere", vm.OverdueMoreText);
        Assert.True(vm.HasOverdueMore);
        // Die ältesten zuerst; jeder Eintrag trägt den Termin, den ein Klick öffnet.
        Assert.Equal(overdue.Take(5).Select(a => a.Id), vm.OverdueList.Select(i => i.Appointment.Id));

        vm.OpenAppointment(vm.OverdueList[0].Appointment);
        Assert.Equal(overdue[0].Id, vm.Selected?.Appointment.Id);
    }

    [Fact]
    public async Task Bei_drei_ueberfaelligen_Terminen_steht_keine_Zeile_und_n_weitere_da()
    {
        var overdue = Enumerable.Range(0, 3)
            .Select(i => TestData.Appointment(10, TestData.Anna) with { Start = TestData.At(10, i), End = TestData.At(10, 50) })
            .ToArray();
        var vm = await OpenAsync(overdue);

        Assert.Equal(3, vm.OverdueList.Count);
        Assert.False(vm.HasOverdueMore);
    }
}
