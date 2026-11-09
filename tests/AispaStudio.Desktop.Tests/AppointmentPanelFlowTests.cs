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

    private sealed class RecordingFileSaver : IFileSaver
    {
        public string? SavedName { get; private set; }

        public Task<bool> SaveAsync(string suggestedName, string typeName, IReadOnlyList<string> patterns, byte[] content)
        {
            SavedName = suggestedName;
            return Task.FromResult(true);
        }
    }

    [Theory]
    [InlineData(AppointmentStatus.Booked, true)]
    [InlineData(AppointmentStatus.CheckedIn, true)]
    [InlineData(AppointmentStatus.Completed, true)]
    [InlineData(AppointmentStatus.Cancelled, false)]
    public async Task Given_ein_Termin_When_sein_Panel_geoeffnet_wird_Then_steht_Bestaetigung_hinter_Mehr_ausser_beim_stornierten(AppointmentStatus status, bool offered)
    {
        var panel = await PanelOfAsync(status);

        Assert.Equal(offered, panel.CanConfirm);
        Assert.True(!offered || panel.HasMore, "Hinter „Mehr“ steht die Bestätigung.");
    }

    [Fact]
    public async Task Given_ein_gebuchter_Termin_When_Bestaetigung_geoeffnet_und_gespeichert_wird_Then_kommt_das_PDF_der_API_mit_deren_Dateinamen_und_dem_Toast_des_Kalenders()
    {
        var panel = await PanelOfAsync(AppointmentStatus.Booked);
        var id = panel.Appointment.Id;
        var api = new StubApi()
            .OnFile($"GET /api/appointments/{id}/confirmation/pdf", [0x25, 0x50, 0x44, 0x46], "Terminbestätigung-01-03-2027.pdf")
            .On($"GET /api/appointments/{id}/history", new List<HistoryEntry>());
        var toasts = new List<string>();
        var saver = new RecordingFileSaver();
        var shown = new AppointmentViewModel(api.Client(), panel.Appointment, "Anna", "Raum Zeder", Vienna, true, true, _ => Task.CompletedTask, () => { }) { ShowToast = toasts.Add };
        Assert.False(shown.IsConfirmationOpen);

        shown.ToggleConfirmationCommand.Execute(null);
        shown.ConfirmationDocument!.Files = saver;
        await shown.ConfirmationDocument.SaveCommand.ExecuteAsync(null);

        Assert.True(shown.IsConfirmationOpen);
        Assert.Equal($"api/appointments/{id}/confirmation/send", shown.ConfirmationDocument.Source.SendPath);
        Assert.Equal("Terminbestätigung-01-03-2027.pdf", saver.SavedName);
        Assert.Equal(["Gespeichert: Terminbestätigung-01-03-2027.pdf"], toasts);

        // Nach dem Senden steht der Vermerk gleich im geöffneten Verlauf des Termins.
        await shown.HistorySection.LoadAsync();
        shown.HistorySection.IsOpen = true;
        var loads = api.Calls.Count(c => c.Route == $"GET /api/appointments/{id}/history");
        shown.ConfirmationDocument.ShowToast!("Gesendet an eva@example.at");
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(loads + 1, api.Calls.Count(c => c.Route == $"GET /api/appointments/{id}/history"));

        shown.ToggleConfirmationCommand.Execute(null);
        Assert.False(shown.IsConfirmationOpen);
    }
}
