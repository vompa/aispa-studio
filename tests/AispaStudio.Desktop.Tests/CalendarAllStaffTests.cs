using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>„Alle Mitarbeitenden“ im Kalender: Personen ohne Dienst an diesem Standort als schraffierte Spalten mit Grund; Buchen nur nach Bestätigung.</summary>
public sealed class CalendarAllStaffTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly EmployeeDto Clara = new(Guid.NewGuid(), "Clara", "#c27a2c", [], []);
    private static readonly EmployeeDto Dora = new(Guid.NewGuid(), "Dora", "#3f7fbf", [], []);
    private static readonly EmployeeDto Erik = new(Guid.NewGuid(), "Erik", "#5b8c3a", [], []);

    private static readonly LocationResources Team = new(TestData.Location, [TestData.Room], [], [TestData.Anna, TestData.Ben, Clara, Dora, Erik],
        [TestData.Facial, TestData.MassageTreatment], []);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    /// <summary>Anna und Ben arbeiten; Clara hat Urlaub bis Donnerstag, Dora arbeitet in Graz, Erik hat frei.</summary>
    private static CalendarDay Day() => TestData.Day() with
    {
        Absences = [new CalendarAbsence(Clara.Id, RosterKind.Vacation, TestData.Monday.AddDays(3), null)],
        Elsewhere = [new CalendarElsewhere(Dora.Id, "Graz", TestData.At(10), TestData.At(18))],
    };

    private static async Task<CalendarViewModel> OpenAsync(ISettingsStore? settings = null)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", Team)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", Day());
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime(), settings);
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Ohne_den_Schalter_zeigt_der_Kalender_nur_Personen_mit_Dienst()
    {
        var vm = await OpenAsync();

        Assert.False(vm.ShowAllStaff);
        Assert.Equal(["Anna", "Ben"], vm.Columns.Select(c => c.Title));
        Assert.All(vm.Columns, c => Assert.False(c.OffDuty));
    }

    [Fact]
    public async Task Mit_dem_Schalter_kommen_Personen_ohne_Dienst_mit_Grund_dazu()
    {
        var vm = await OpenAsync();

        vm.ShowAllStaff = true;

        Assert.Equal(["Anna", "Ben", "Clara", "Dora", "Erik"], vm.Columns.Select(c => c.Title));
        Assert.Equal(["09:00–17:00", "12:00–20:00", "Urlaub bis 4.3.", "Graz 10:00–18:00", "frei"], vm.Columns.Select(c => c.Subtitle));
        Assert.Equal([false, false, true, true, true], vm.Columns.Select(c => c.OffDuty));
        Assert.Equal(RosterKind.Vacation, vm.Columns[2].Absence);
        // Ohne Dienst gibt es keine helle Fläche: die ganze Spalte ist schraffiert.
        Assert.All(vm.Columns.Where(c => c.OffDuty), c => Assert.Empty(c.Available));
    }

    [Fact]
    public async Task Die_Einstellung_gilt_je_Geraet_und_bleibt_nach_dem_Neustart()
    {
        var store = new MemorySettingsStore();
        var vm = await OpenAsync(store);
        vm.ShowAllStaff = true;

        Assert.True(store.Load().CalendarAllStaff);
        var again = await OpenAsync(store);
        Assert.True(again.ShowAllStaff);
        Assert.Equal(5, again.Columns.Count);

        again.ShowAllStaff = false;
        Assert.False(store.Load().CalendarAllStaff);
    }

    [Fact]
    public async Task In_der_Raumansicht_gibt_es_keine_Personenspalten_dazu()
    {
        var vm = await OpenAsync();
        vm.ShowAllStaff = true;

        vm.ShowRoomsCommand.Execute(null);

        Assert.Equal(["Raum Zeder"], vm.Columns.Select(c => c.Title));
    }

    [Fact]
    public async Task Buchen_in_einer_Spalte_ohne_Dienst_fragt_erst_nach()
    {
        var vm = await OpenAsync();
        vm.ShowAllStaff = true;

        // Mit Dienst: das Fenster geht gleich auf.
        vm.StartBooking(vm.Columns[0], 100);
        Assert.NotNull(vm.NewEntry);
        vm.CloseAllPanels();

        // Ohne Dienst: zuerst die Rückfrage über dem Kalender (keine Meldung der Seitenleiste), noch kein Fenster.
        vm.StartBooking(vm.Columns[3], 100);
        Assert.Null(vm.NewEntry);
        var ask = vm.OffDuty!;
        Assert.Equal("Trotzdem buchen", ask.ActionText);
        Assert.Contains("Dora hat an diesem Tag keinen Dienst hier (Graz 10:00–18:00)", ask.Message, StringComparison.Ordinal);
        Assert.Empty(vm.Messages);

        ask.Action.Execute(null);
        Assert.NotNull(vm.NewEntry);
        Assert.Null(vm.OffDuty);
    }

    [Fact]
    public async Task Doppelklick_in_einer_Spalte_ohne_Dienst_bucht_ohne_Rueckfrage()
    {
        // Auftraggeber, 23.10.2026: der Doppelklick ist das „Trotzdem buchen“.
        var vm = await OpenAsync();
        vm.ShowAllStaff = true;
        var x = Enumerable.Range(0, 4000).First(px => DayLayout.ColumnAt(px, vm.Columns.Count, vm.Scale) == 3);

        vm.StartBookingAt(x, 100, suggest: false, offDutyConfirmed: true);

        Assert.Null(vm.OffDuty);
        Assert.NotNull(vm.NewEntry);
    }

    [Fact]
    public async Task Die_Rueckfrage_laesst_sich_schliessen_und_bucht_dann_nicht()
    {
        var vm = await OpenAsync();
        vm.ShowAllStaff = true;
        vm.StartBooking(vm.Columns[4], 100);

        vm.DismissOffDutyCommand.Execute(null);

        Assert.Null(vm.NewEntry);
        Assert.Null(vm.OffDuty);
    }
}
