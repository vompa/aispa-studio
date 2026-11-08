using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kopfzeile des Kalenders: „Alle zeigen“ nur bei verborgenen Personen, stabile Spaltenbreite, Standort im Kopf (Schritt 14, Ä1/Ä11).</summary>
public sealed class CalendarHeaderTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    private static readonly EmployeeDto Clara = new(Guid.NewGuid(), "Clara", "#c27a2c", [], []);
    private static readonly EmployeeDto Dora = new(Guid.NewGuid(), "Dora", "#3f7fbf", [], []);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static async Task<CalendarViewModel> OpenAsync(LocationResources resources, CalendarDay day, ISettingsStore? settings = null)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day);
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime(), settings);
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static LocationResources TeamWith(params EmployeeDto[] extra) =>
        new(TestData.Location, [TestData.Room], [], [TestData.Anna, TestData.Ben, .. extra], [TestData.Facial, TestData.MassageTreatment], []);

    [Fact]
    public async Task Ohne_verborgene_Personen_gibt_es_keine_Schaltflaeche()
    {
        var vm = await OpenAsync(TestData.Resources, TestData.Day());

        Assert.Equal(0, vm.HiddenStaffCount);
        Assert.False(vm.CanToggleAllStaff);
    }

    [Fact]
    public async Task Eine_Person_ohne_Dienst_zeigt_Alle_zeigen_1_und_nach_dem_Umschalten_Nur_mit_Dienst()
    {
        var vm = await OpenAsync(TeamWith(Clara), TestData.Day());

        Assert.True(vm.CanToggleAllStaff);
        Assert.Equal("Alle zeigen (1)", vm.AllStaffText);
        Assert.Equal(["Anna", "Ben"], vm.Columns.Select(c => c.Title));

        vm.ShowAllStaff = true;

        // Die Schaltfläche bleibt (sonst käme man nicht zurück) und die Zahl der verborgenen Personen bleibt gleich.
        Assert.True(vm.CanToggleAllStaff);
        Assert.Equal("Nur mit Dienst", vm.AllStaffText);
        Assert.Equal(["Anna", "Ben", "Clara"], vm.Columns.Select(c => c.Title));
    }

    [Fact]
    public async Task Auch_Dienst_an_einem_anderen_Standort_zaehlt_als_verborgen_und_ein_Termin_nicht()
    {
        var day = TestData.Day() with { Elsewhere = [new CalendarElsewhere(Dora.Id, "Graz", TestData.At(10), TestData.At(18))] };

        var vm = await OpenAsync(TeamWith(Clara, Dora), day);

        Assert.Equal(2, vm.HiddenStaffCount);
        Assert.Equal("Alle zeigen (2)", vm.AllStaffText);

        // Claras Termin (z. B. nach einer Planänderung) gibt ihr eine Spalte, also ist sie nicht mehr verborgen.
        var withAppointment = await OpenAsync(TeamWith(Clara, Dora), day with { Appointments = [TestData.Appointment(10, Clara)] });
        Assert.Equal(["Anna", "Ben", "Clara"], withAppointment.Columns.Select(c => c.Title));
        Assert.Equal(1, withAppointment.HiddenStaffCount);
    }

    [Fact]
    public async Task In_der_Raumansicht_gibt_es_keine_Schaltflaeche()
    {
        var vm = await OpenAsync(TeamWith(Clara), TestData.Day());

        vm.ShowRoomsCommand.Execute(null);

        Assert.False(vm.CanToggleAllStaff);
    }

    [Fact]
    public async Task Die_Spaltenbreite_bleibt_beim_Zuschalten_gleich_und_der_Kalender_scrollt_waagrecht()
    {
        var vm = await OpenAsync(TeamWith(Clara, Dora), TestData.Day());
        vm.SetViewport(1000, 800);
        var before = (vm.Scale.ColumnWidth, vm.ColumnsWidth);

        vm.ShowAllStaff = true;

        Assert.Equal(4, vm.Columns.Count);
        Assert.Equal(before.ColumnWidth, vm.Scale.ColumnWidth);
        Assert.True(vm.ColumnsWidth > before.ColumnsWidth);
        Assert.True(vm.ColumnsWidth > 1000);

        vm.ShowAllStaff = false;

        Assert.Equal(before, (vm.Scale.ColumnWidth, vm.ColumnsWidth));
    }

    [Fact]
    public async Task Die_Spaltenbreite_bleibt_auch_nach_Groessenaenderung_des_Fensters_stabil()
    {
        var vm = await OpenAsync(TeamWith(Clara, Dora), TestData.Day());
        vm.ShowAllStaff = true;

        vm.SetViewport(1000, 800);
        var on = vm.Scale.ColumnWidth;
        vm.ShowAllStaff = false;

        Assert.Equal(on, vm.Scale.ColumnWidth);
    }

    [Fact]
    public async Task Der_Kopf_einer_Person_von_einem_anderen_Standort_trennt_Standort_und_Dienstzeit()
    {
        var day = TestData.Day() with { Elsewhere = [new CalendarElsewhere(Dora.Id, "Graz", TestData.At(10), TestData.At(18))] };
        var vm = await OpenAsync(TeamWith(Dora), day);

        vm.ShowAllStaff = true;

        var dora = vm.Columns.Single(c => c.Title == "Dora");
        Assert.Equal("Graz", dora.Place);
        Assert.Equal("10:00–18:00", dora.ShiftText);
        // Der volle Text bleibt für die Rückfrage beim Buchen und Hinweise.
        Assert.Equal("Graz 10:00–18:00", dora.Subtitle);
        // Der Kopf hat genau zwei Zeilen: Standort und Dienstzeit stehen mit „·“ in der zweiten.
        Assert.Equal("Graz · 10:00–18:00", dora.SecondLine);
        var anna = vm.Columns.Single(c => c.Title == "Anna");
        Assert.Null(anna.Place);
        Assert.Equal(anna.Subtitle, anna.ShiftText);
        Assert.Equal(anna.Subtitle, anna.SecondLine);
    }

    [Fact]
    public async Task Der_Hinweis_am_Kopf_einer_Person_nennt_Dienstzeit_Standort_und_Sprachen_in_hoechstens_fuenf_Zeilen()
    {
        var day = TestData.Day() with { Elsewhere = [new CalendarElsewhere(Dora.Id, "Graz", TestData.At(10), TestData.At(18))] };
        var speaking = Dora with { Languages = ["en", "fr"] };
        var vm = await OpenAsync(TeamWith(speaking), day);
        vm.ShowAllStaff = true;

        var dora = vm.Columns.Single(c => c.Title == "Dora");
        Assert.Equal(
            [(Icons.Clock, "10:00–18:00"), (Icons.Pin, "Dienst in Graz"), (Icons.Chat, "Sprachen: Deutsch, Englisch, Französisch"), (Icons.Cursor, dora.FilterTip!)],
            dora.HeaderTipLines.Select(l => (l.Icon, l.Text)));

        // Ohne Dienst an einem anderen Standort und ohne Sprachen bleiben Dienstzeit und Klick-Hinweis.
        var anna = vm.Columns.Single(c => c.Title == "Anna");
        Assert.Equal([Icons.Clock, Icons.Cursor], anna.HeaderTipLines.Select(l => l.Icon));

        dora.Alert = new ColumnAlert(MessageSeverity.Warning, "Pause fehlt");
        Assert.Equal(5, dora.HeaderTipLines.Count);
    }

    [Fact]
    public async Task Die_Kopfzeile_wird_unter_1100_Pixel_Breite_kompakt()
    {
        var vm = await OpenAsync(TeamWith(Clara), TestData.Day());

        // Vor der ersten Meldung der Größe bleibt es bei der vollen Form.
        Assert.False(vm.IsCompactHeader);
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.SetViewport(1200, 800);
        Assert.False(vm.IsCompactHeader);
        Assert.DoesNotContain(nameof(CalendarViewModel.IsCompactHeader), changes);

        vm.SetViewport(1000, 800);
        Assert.True(vm.IsCompactHeader);
        Assert.Contains(nameof(CalendarViewModel.IsCompactHeader), changes);
        // Kompakt: Symbol und Zahl, der Text steht im Hinweis.
        Assert.Equal(vm.Overview!.Appointments.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.OverviewCompact);
        Assert.Equal(vm.HiddenStaffCount.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.AllStaffCompact);

        vm.SetViewport(1100, 800);
        Assert.False(vm.IsCompactHeader);
    }
}
