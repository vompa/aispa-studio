using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Termine als Datei in der App (Export/Import, Aufgabe C): „Mehr“ › „Kalenderdatei …“ im Termin-Panel, „+ Termin“ › „Terminliste exportieren …“ mit Zeitraum und Format,
/// „Kalenderdatei …“ in der Personenkarte nur für die Leitung und die Person selbst. Inhalt und Format der Dateien prüfen die API-Tests.
/// </summary>
public sealed class AppointmentFilesTests
{
    private static readonly byte[] Ics = "BEGIN:VCALENDAR"u8.ToArray();

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static StubApi Api(params AppointmentDto[] appointments) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointments));

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, FakeFileSaver files, FakeDialogs? dialogs = null)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime()) { FileSaver = files, Dialogs = dialogs };
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Mehr_Kalenderdatei_speichert_den_Termin_und_meldet_Gespeichert()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var route = $"GET /api/appointments/{appointment.Id}/ics";
        var api = Api(appointment).OnFile(route, Ics, "Termin_2027-03-01_1000.ics");
        var files = new FakeFileSaver();
        var vm = await OpenAsync(api, files);
        vm.OpenAppointment(appointment);
        var panel = vm.Selected!;

        Assert.True(panel.HasMore);
        Assert.True(panel.CanSaveCalendarFile);
        Assert.Equal("Kalenderdatei …", panel.CalendarFileOption.Label);
        await panel.CalendarFile.ExportCommand.ExecuteAsync(panel.CalendarFileOption);

        Assert.Contains(api.Calls, c => c.Route == route);
        var (name, content) = Assert.Single(files.Saved);
        Assert.Equal("Termin_2027-03-01_1000.ics", name);
        Assert.Equal(Ics, content);
    }

    [Fact]
    public async Task Stornierter_Termin_hat_keine_Kalenderdatei()
    {
        var appointment = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Cancelled);
        var vm = await OpenAsync(Api(appointment), new FakeFileSaver());
        vm.OpenAppointment(appointment);

        Assert.False(vm.Selected!.CanSaveCalendarFile);
    }

    [Fact]
    public async Task Terminliste_fragt_Zeitraum_und_Format_und_holt_die_Woche_als_CSV()
    {
        var route = $"GET /api/locations/{TestData.LocationId}/appointments/export?from=2027-03-01&until=2027-03-07&format=csv";
        var api = Api().OnFile(route, [1, 2, 3], "Termine_2027-03-01.csv");
        var files = new FakeFileSaver();
        var dialogs = new FakeDialogs();
        var vm = await OpenAsync(api, files, dialogs);

        var running = vm.ExportAppointmentListCommand.ExecuteAsync(null);
        var dialog = Assert.IsType<AppointmentListDialogViewModel>(dialogs.Open);
        FakeDialogs.AssertRules(dialog.Request);
        Assert.Equal((new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 7), true), (dialog.From, dialog.Until, dialog.IsExcel));
        Assert.False(dialog.IsDestructive);

        dialog.Until = new DateOnly(2027, 2, 27);
        dialog.ExportCommand.Execute(null);
        Assert.Equal(AppointmentListDialogViewModel.EndBeforeStart, dialog.Error);
        Assert.Same(dialog, dialogs.Open);

        dialog.Until = new DateOnly(2027, 3, 7);
        Assert.Null(dialog.Error);
        dialog.IsCsv = true;
        dialog.ExportCommand.Execute(null);
        await running;

        Assert.Contains(api.Calls, c => c.Route == route);
        Assert.Equal("Termine_2027-03-01.csv", Assert.Single(files.Saved).Name);
    }

    [Fact]
    public async Task Terminliste_abbrechen_holt_nichts()
    {
        var api = Api();
        var dialogs = new FakeDialogs();
        var vm = await OpenAsync(api, new FakeFileSaver(), dialogs);

        var running = vm.ExportAppointmentListCommand.ExecuteAsync(null);
        dialogs.Open!.CancelCommand.Execute(null);
        await running;

        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/appointments/export", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("2027-03-01", "2027-03-01", null)]
    [InlineData("2027-03-01", "2028-02-29", null)]
    [InlineData("2027-03-01", "2028-03-01", AppointmentListDialogViewModel.TooLong)]
    [InlineData("2027-03-02", "2027-03-01", AppointmentListDialogViewModel.EndBeforeStart)]
    [InlineData(null, "2027-03-01", AppointmentListDialogViewModel.MissingRange)]
    public void Zeitraum_der_Terminliste_wird_geprueft(string? from, string? until, string? error) =>
        Assert.Equal(error, AppointmentListDialogViewModel.RangeError(from is null ? null : DateOnly.Parse(from, CultureInfo.InvariantCulture), until is null ? null : DateOnly.Parse(until, CultureInfo.InvariantCulture)));

    [Fact]
    public async Task Personenkarte_Kalenderdatei_holt_90_Tage_ab_heute()
    {
        var (api, _) = RosterTests.Setup();
        var today = DateOnly.FromDateTime(new RosterTests.RosterClock().GetLocalNow().DateTime);
        var route = $"GET /api/employees/{RosterSample.Lena.Id}/ics?from={today:yyyy-MM-dd}&until={today.AddDays(90):yyyy-MM-dd}";
        api.OnFile(route, Ics, "Kalender_Lena.ics");
        var team = await RosterTests.Loaded(api);
        team.Open(RosterSample.Lena.Id, PersonCardViewModel.RosterTab, null);
        var card = team.Card!;
        var files = new FakeFileSaver();
        card.CalendarFile!.Files = files;

        Assert.True(card.HasCalendarFile);
        await card.CalendarFile.ExportCommand.ExecuteAsync(card.CalendarFileOption);

        Assert.Contains(api.Calls, c => c.Route == route);
        Assert.Equal("Gespeichert: Kalender_Lena.ics", card.CalendarFileSaved);
    }

    [Fact]
    public void Personenkarte_Kalenderdatei_nur_fuer_die_Leitung_und_die_Person_selbst()
    {
        var (api, _) = RosterTests.Setup();
        var lena = RosterSample.Lena;
        PersonCardViewModel Card(CurrentUser user) => new(new TeamContext(api.Client(), user, new RosterTests.RosterClock(), standalone: false),
            new TeamEntry(TeamEntryKind.Person, lena.DisplayName, "", lena.Id), lena, null, null);

        Assert.False(Card(TestData.Reception(RosterSample.Marco.Id)).HasCalendarFile);
        Assert.False(Card(TestData.Reception()).HasCalendarFile);
        Assert.True(Card(TestData.Reception(lena.Id)).HasCalendarFile);
        Assert.True(Card(TestData.Manager()).HasCalendarFile);
    }
}
