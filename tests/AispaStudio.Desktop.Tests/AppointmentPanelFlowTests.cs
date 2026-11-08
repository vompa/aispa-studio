using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ablauf der Aktionen im Termin-Panel je Status (Ä81): Stornieren ist ohne Einchecken erreichbar. Alle Tests laufen mit Inhaber-Rechten.</summary>
public sealed class AppointmentPanelFlowTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(7);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    /// <summary>Der Tag vor dem Testkalender: Der Termin am Montag liegt dann morgen.</summary>
    private sealed class DayBeforeTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(7).AddDays(-1);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static async Task<AppointmentViewModel> PanelOfAsync(AppointmentStatus status, TimeProvider? time = null)
    {
        var appointment = TestData.Appointment(10, TestData.Anna, status);
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-02-28", TestData.Day() with { Date = new DateOnly(2027, 2, 28) })
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointment));
        var vm = new CalendarViewModel(api.Client(), TestData.Owner(), null, time ?? new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.OpenAppointment(appointment);
        return vm.Selected!;
    }

    [Fact]
    public async Task Given_ein_Termin_von_morgen_When_sein_Panel_geoeffnet_wird_Then_fehlt_Einchecken_und_Erledigt_nennt_den_Tag_als_Grund()
    {
        var panel = await PanelOfAsync(AppointmentStatus.Booked, new DayBeforeTime());

        Assert.False(panel.IsOnToday);
        Assert.False(panel.ShowCheckIn);
        Assert.False(panel.ShowComplete);
        Assert.False(panel.ShowNoShow);
        Assert.True(panel.HasMore);
        Assert.Equal("Verfügbar ab dem Tag des Termins.", panel.CompleteHint);
        Assert.Equal("Verfügbar ab dem Tag des Termins.", panel.NoShowHint);
        Assert.True(panel.CanCancel);
    }

    [Fact]
    public async Task Given_ein_gebuchter_Termin_von_heute_When_sein_Panel_geoeffnet_wird_Then_nennen_die_Hinweise_den_Status_als_Grund()
    {
        var panel = await PanelOfAsync(AppointmentStatus.Booked);

        Assert.True(panel.IsOnToday);
        Assert.True(panel.ShowComplete);
        Assert.Equal("Verfügbar bei gebuchten und eingecheckten Terminen.", panel.CompleteHint);
        Assert.Equal("Verfügbar bei gebuchten Terminen.", panel.NoShowHint);
    }

    [Fact]
    public async Task Given_ein_gebuchter_Termin_When_sein_Panel_geoeffnet_wird_Then_steht_Stornieren_ohne_vorheriges_Einchecken_zur_Verfuegung()
    {
        var panel = await PanelOfAsync(AppointmentStatus.Booked);

        Assert.True(panel.ShowCheckIn);
        Assert.True(panel.CanCancel);
        Assert.False(panel.IsCheckedIn);
    }

    [Fact]
    public async Task Given_ein_eingecheckter_Termin_When_sein_Panel_geoeffnet_wird_Then_stehen_Kassieren_Einchecken_zuruecknehmen_und_Stornieren_oben()
    {
        var panel = await PanelOfAsync(AppointmentStatus.CheckedIn);

        Assert.False(panel.ShowCheckIn);
        Assert.True(panel.ShowUndoCheckIn);
        Assert.True(panel.CanCancel);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public async Task Given_ein_erledigter_oder_stornierter_Termin_When_sein_Panel_geoeffnet_wird_Then_fehlt_Stornieren(AppointmentStatus status)
    {
        var panel = await PanelOfAsync(status);

        Assert.False(panel.CanCancel);
        Assert.False(panel.ShowUndoCheckIn);
    }
}
