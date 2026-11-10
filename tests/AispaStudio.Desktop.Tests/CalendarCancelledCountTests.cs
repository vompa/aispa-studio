using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Chip „Storniert n“ in der Kalender-Kopfzeile (Verlauf, Aufgabe C): zählt die stornierten Termine des Tages, blendet sich bei 0 aus und öffnet den Verlauf mit Tag und Art.</summary>
public sealed class CalendarCancelledCountTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static StubApi Api(params AppointmentDto[] appointments) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointments))
        .On($"GET /api/locations/{TestData.LocationId}/history?from=2027-03-01&until=2027-03-01&kinds=Cancelled&kinds=Restored", new ActivityPage([], null))
        .On($"GET /api/locations/{TestData.LocationId}/history?from=2027-03-01&until=2027-03-01", new ActivityPage([], null));

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static AppointmentDto Cancelled(int hour) => TestData.Appointment(hour, TestData.Anna, AppointmentStatus.Cancelled);

    [Fact]
    public async Task Der_Zaehler_zaehlt_die_stornierten_Termine_des_Tages_und_der_Chip_heisst_Storniert_2()
    {
        var calendar = await OpenAsync(Api(Cancelled(10), Cancelled(14), TestData.Appointment(11, TestData.Anna)));

        Assert.Equal(2, calendar.CancelledCount);
        Assert.True(calendar.HasCancelled);
        Assert.Equal("Storniert 2", calendar.CancelledLabel);
    }

    [Fact]
    public async Task Termine_eines_anderen_Standorts_zaehlen_nicht()
    {
        var elsewhere = Cancelled(10) with { LocationId = Guid.NewGuid() };
        var calendar = await OpenAsync(Api(elsewhere, Cancelled(11)));

        Assert.Equal(1, calendar.CancelledCount);
    }

    [Fact]
    public async Task Ohne_stornierte_Termine_ist_der_Chip_ausgeblendet()
    {
        var calendar = await OpenAsync(Api(TestData.Appointment(10, TestData.Anna)));

        Assert.Equal(0, calendar.CancelledCount);
        Assert.False(calendar.HasCancelled);
    }

    [Fact]
    public async Task Ein_Klick_uebergibt_den_gezeigten_Tag()
    {
        var calendar = await OpenAsync(Api(Cancelled(10)));
        DateOnly? requested = null;
        calendar.CancelledRequested = day => requested = day;

        calendar.OpenCancelledCommand.Execute(null);

        Assert.Equal(TestData.Monday, requested);
    }

    [Fact]
    public async Task Die_Shell_wechselt_zur_Seite_Verlauf_mit_dem_Tag_und_der_Art_Stornos()
    {
        var api = Api(Cancelled(10));
        var shell = new ShellViewModel(api.Client(), TestData.Owner(), null, new FixedTime(), new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);
        await shell.Calendar.InitializeAsync();
        await shell.Calendar.LoadAsync();
        Assert.Same(shell.Calendar, shell.SelectedPage?.Page);

        shell.Calendar.OpenCancelledCommand.Execute(null);
        for (var i = 0; i < 200 && shell.Activity.Rows.Count == 0 && !api.Calls.Any(c => c.Route.EndsWith("kinds=Restored", StringComparison.Ordinal)); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Same(shell.Activity, shell.SelectedPage?.Page);
        Assert.Equal((ActivityPeriod.Custom, TestData.Monday, TestData.Monday), (shell.Activity.Period, shell.Activity.CustomFrom, shell.Activity.CustomUntil));
        Assert.Equal(["Stornos"], shell.Activity.KindChips.Where(c => c.IsOn).Select(c => c.Label));
        Assert.Contains(api.Calls, c => c.Route.EndsWith("history?from=2027-03-01&until=2027-03-01&kinds=Cancelled&kinds=Restored", StringComparison.Ordinal));
    }
}
