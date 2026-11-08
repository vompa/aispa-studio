using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Raumansicht (Slice 77, Ä91, Ä92): Räume nach Kategorie, der Umschalter „Alle Räume“ / „Nur verfügbare Räume“, Sauna und Ruheraum als Spalten mit Belegung
/// und Vorschau, und das Umschalten aus der Teamansicht mit angefangenem Termin.
/// </summary>
public sealed class RoomViewTests
{
    private static readonly RoomDto Cedar = TestData.Room;
    private static readonly RoomDto Hamam = new(Guid.NewGuid(), TestData.LocationId, "Hamam", 20);
    private static readonly RoomDto Sauna = new(Guid.NewGuid(), TestData.LocationId, "Finnische Sauna", 0, Places: 12, IsArea: true);
    private static readonly RoomDto Relax = new(Guid.NewGuid(), TestData.LocationId, "Ruheraum", 0, Places: 20, IsArea: true);
    private static readonly TreatmentDto HamamRitual = TestData.MassageTreatment with { Id = Guid.NewGuid(), Name = "Hamam-Ritual", RoomIds = [Hamam.Id] };

    /// <summary>Stammdaten absichtlich durcheinander: die Bereiche stehen zuerst.</summary>
    private static readonly LocationResources Resources = TestData.Resources with
    {
        Rooms = [Sauna, Cedar, Relax, Hamam],
        Treatments = [TestData.MassageTreatment, HamamRitual],
    };

    private static readonly string Areas = $"GET /api/locations/{TestData.LocationId}/areas?date=2027-03-01";

    private static StubApi Api(CalendarDay? day = null, params AreaVisitDto[] visits) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day ?? TestData.Day())
        .On(Areas, new List<AreaDay>
        {
            new(Sauna.Id, Sauna.Name, 12, [.. visits.Where(v => v.RoomId == Sauna.Id)], []),
            new(Relax.Id, Relax.Name, 20, [], []),
        });

    private static Task<CalendarViewModel> OpenAsync(StubApi api) => ClosureTests.OpenAsync(api, TestData.Owner());

    private static RoomClosureBand Closure(RoomDto room, DateTimeOffset from, DateTimeOffset until, RoomClosureKind kind = RoomClosureKind.Maintenance) =>
        new(Guid.NewGuid(), room.Id, room.Name, from, until, kind, "Reinigung", "Grundreinigung", 0, false);

    private static CalendarDay DayClosed(RoomDto room, DateTimeOffset from, DateTimeOffset until)
    {
        var band = Closure(room, from, until);
        return TestData.Day() with { RoomClosures = [band], RoomClosuresDue = [band] };
    }

    [Fact]
    public async Task Given_zwei_Kategorien_von_Raeumen_When_die_Raumansicht_oeffnet_Then_stehen_die_Raeume_nach_Kategorie_sortiert()
    {
        var vm = await OpenAsync(Api());

        vm.ByRoom = true;

        Assert.Equal(["Raum Zeder", "Hamam", "Finnische Sauna", "Ruheraum"], vm.Columns.Select(c => c.Title));
        Assert.Equal(["Behandlungsräume", "Behandlungsräume", "Sauna und Ruhe", "Sauna und Ruhe"], vm.Columns.Select(c => c.Category));
    }

    [Fact]
    public async Task Given_ein_gesperrter_Raum_When_Nur_verfuegbare_Raeume_gewaehlt_wird_Then_fehlt_er_in_der_Ansicht()
    {
        var vm = await OpenAsync(Api(DayClosed(Hamam, TestData.At(8), TestData.At(21))));
        vm.ByRoom = true;
        Assert.Contains("Hamam", vm.Columns.Select(c => c.Title));
        Assert.True(vm.CanToggleRooms);
        Assert.Equal("Nur verfügbare Räume", vm.RoomFilterText);

        vm.ToggleRoomsCommand.Execute(null);

        Assert.DoesNotContain("Hamam", vm.Columns.Select(c => c.Title));
        Assert.Equal("Alle Räume", vm.RoomFilterText);

        vm.ToggleRoomsCommand.Execute(null);
        Assert.Contains("Hamam", vm.Columns.Select(c => c.Title));
    }

    [Fact]
    public async Task Given_eine_Reinigung_am_Vormittag_When_Nur_verfuegbare_Raeume_gewaehlt_wird_Then_bleibt_der_Raum_sichtbar()
    {
        var vm = await OpenAsync(Api(DayClosed(Hamam, TestData.At(10), TestData.At(11))));
        vm.ByRoom = true;

        Assert.False(vm.CanToggleRooms);
        vm.OnlyAvailableRooms = true;

        Assert.Contains("Hamam", vm.Columns.Select(c => c.Title));
    }

    [Fact]
    public async Task Given_die_Teamansicht_When_Raeume_gesperrt_sind_Then_gibt_es_den_Umschalter_nicht()
    {
        var vm = await OpenAsync(Api(DayClosed(Hamam, TestData.At(8), TestData.At(21))));

        Assert.False(vm.CanToggleRooms);
    }

    [Fact]
    public async Task Given_die_Raumansicht_When_sie_oeffnet_Then_stehen_Finnische_Sauna_und_Ruheraum_als_Spalten_da()
    {
        var visit = new AreaVisitDto(Guid.NewGuid(), TestData.LocationId, Sauna.Id, Guid.NewGuid(), "Eva Muster", 2, TestData.At(10), TestData.At(12), false, null);
        var vm = await OpenAsync(Api(null, visit));

        vm.ByRoom = true;

        var sauna = Assert.Single(vm.Columns, c => c.Title == "Finnische Sauna");
        Assert.Single(vm.Columns, c => c.Title == "Ruheraum");
        Assert.True(sauna.IsArea);
        Assert.Equal("12 Plätze", sauna.Subtitle);
        var block = Assert.Single(sauna.Items);
        Assert.Equal("Eva Muster", block.Title);
        Assert.Equal("2 Personen", block.Subtitle);
        Assert.Equal("10:00–12:00", block.TimeText);
    }

    [Fact]
    public async Task Given_offene_Bearbeitung_eines_Termins_im_Hamam_When_zur_Raumansicht_gewechselt_wird_Then_ist_der_Hamam_sichtbar_und_die_Vorschau_steht_dort()
    {
        var vm = await OpenAsync(Api());
        vm.SetViewport(1300, 800);
        var revealed = new List<double>();
        vm.ColumnRevealRequested += revealed.Add;
        vm.StartBooking(vm.Columns[0], 100);
        vm.Booking!.Treatment = HamamRitual;
        Assert.False(vm.Booking.ColumnIsRoom);

        vm.ByRoom = true;

        var index = vm.Columns.ToList().FindIndex(c => c.Title == "Hamam");
        Assert.True(index >= 0);
        Assert.True(vm.Booking.ColumnIsRoom);
        Assert.Equal(Hamam.Id, vm.Booking.ColumnId);
        Assert.NotNull(vm.Ghost);
        Assert.Equal(DayLayout.ColumnLeft(index, vm.Scale), vm.Ghost!.Left);
        Assert.Equal([DayLayout.ColumnLeft(index, vm.Scale)], revealed);
        Assert.True(vm.IsOnPreview(vm.Ghost.Left + 5, vm.Ghost.Top + 5));
    }

    [Fact]
    public async Task Given_ein_Sauna_Eintritt_in_Arbeit_When_die_Zeiten_stehen_Then_zeigt_die_Spalte_des_Bereichs_die_Vorschau()
    {
        var vm = await OpenAsync(Api());
        vm.ByRoom = true;
        var sauna = vm.Columns.ToList().FindIndex(c => c.Title == "Finnische Sauna");

        vm.StartBooking(vm.Columns[sauna], 100);
        Assert.NotNull(vm.Areas);
        await vm.Areas.LoadAsync();
        vm.Areas.FromText = "11:00";
        vm.Areas.UntilText = "12:30";

        Assert.NotNull(vm.Ghost);
        Assert.Equal(DayLayout.ColumnLeft(sauna, vm.Scale), vm.Ghost!.Left);
        Assert.Equal(90 * vm.Scale.PixelsPerMinute, vm.Ghost.Height, 0.5);
    }
}
