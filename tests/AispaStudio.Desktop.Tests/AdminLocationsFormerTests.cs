using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>„Wieder verwenden“ bei Räumen und Geräten (ADR 0099): „Nicht mehr verwendete zeigen“ unter den Listen, je Zeile nur „Wieder verwenden“.</summary>
public sealed class AdminLocationsFormerTests
{
    private static readonly RoomDto Sauna = new(Guid.NewGuid(), TestData.LocationId, "Sauna", 15, 12, true, null);
    private static readonly RoomDto OldRoom = new(Guid.NewGuid(), TestData.LocationId, "Alter Raum", 10);
    private static readonly EquipmentDto Bench = new(Guid.NewGuid(), TestData.LocationId, "Liege");
    private static readonly EquipmentDto OldDevice = new(Guid.NewGuid(), TestData.LocationId, "Altes Gerät");

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", new LocationResources(TestData.Location, [Sauna], [Bench], [], [], []))
        .On("GET /api/holidays/regions", new List<HolidayRegionDto>())
        .On("GET /api/pos/articles", new List<ArticleDto>())
        .On($"GET /api/locations/{TestData.LocationId}/online-rules", new LocationOnlineRules(true, 120, 60, 24, 15, null, true))
        .On($"GET /api/locations/{TestData.LocationId}/rooms/former", new List<RoomDto> { OldRoom })
        .On($"GET /api/locations/{TestData.LocationId}/equipment/former", new List<EquipmentDto> { OldDevice });

    private static async Task<LocationsViewModel> Open(StubApi api, CurrentUser? user = null)
    {
        var locations = new LocationsViewModel(api.Client(), user ?? TestData.Owner());
        await locations.EnsureLoadedAsync();
        return locations;
    }

    [Fact]
    public async Task Given_Inhaberin_When_Haken_Nicht_mehr_verwendete_Raeume_zeigen_gesetzt_wird_Then_erscheinen_sie_neben_den_aktiven()
    {
        var api = Api();
        var locations = await Open(api);
        Assert.True(locations.CanShowFormer);
        Assert.Empty(locations.FormerRooms);

        locations.ShowFormerRooms = true;
        await locations.FormerLoading;

        var row = Assert.Single(locations.FormerRooms);
        Assert.Equal("Alter Raum", row.Name);
        Assert.Equal(1, api.Calls.Count(c => c.Route == $"GET /api/locations/{TestData.LocationId}/rooms/former"));
        Assert.DoesNotContain(locations.Rooms, r => r.Id == OldRoom.Id);

        locations.ShowFormerRooms = false;
        Assert.Empty(locations.FormerRooms);
    }

    [Fact]
    public async Task Given_ohne_Recht_zum_Verwalten_When_Maske_offen_Then_gibt_es_den_Haken_nicht()
    {
        var locations = await Open(Api(), TestData.Reception());

        Assert.False(locations.CanShowFormer);
    }

    [Fact]
    public async Task Given_nicht_mehr_verwendeter_Raum_When_Wieder_verwenden_Then_POST_und_der_Raum_steht_in_der_normalen_Liste_mit_Meldung()
    {
        var api = Api().On($"POST /api/rooms/{OldRoom.Id}/reactivate", OldRoom);
        var locations = await Open(api);
        locations.ShowFormerRooms = true;
        await locations.FormerLoading;

        await locations.FormerRooms.Single().ReactivateCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"POST /api/rooms/{OldRoom.Id}/reactivate"));
        Assert.Empty(locations.FormerRooms);
        Assert.Contains(locations.Rooms, r => r.Id == OldRoom.Id);
        Assert.Equal("Raum Alter Raum wird wieder verwendet", locations.RoomNotice);
        Assert.Null(locations.RoomMessage);
    }

    [Fact]
    public async Task Given_Raum_When_er_bei_gesetztem_Haken_entfernt_wird_Then_steht_er_sofort_bei_den_nicht_mehr_verwendeten()
    {
        var api = Api().On($"DELETE /api/rooms/{Sauna.Id}", null, HttpStatusCode.NoContent);
        var locations = await Open(api);
        locations.ShowFormerRooms = true;
        await locations.FormerLoading;

        await locations.Rooms.Single().DeactivateCommand.ExecuteAsync(null);

        Assert.Empty(locations.Rooms);
        Assert.Equal(["Alter Raum", "Sauna"], locations.FormerRooms.Select(r => r.Name));
    }

    [Fact]
    public async Task Given_Wieder_verwenden_scheitert_When_ausgefuehrt_Then_bleibt_die_Zeile_und_die_Meldung_steht_am_Raum()
    {
        var api = Api().On($"POST /api/rooms/{OldRoom.Id}/reactivate", new { }, HttpStatusCode.InternalServerError);
        var locations = await Open(api);
        locations.ShowFormerRooms = true;
        await locations.FormerLoading;

        await locations.FormerRooms.Single().ReactivateCommand.ExecuteAsync(null);

        Assert.Single(locations.FormerRooms);
        Assert.DoesNotContain(locations.Rooms, r => r.Id == OldRoom.Id);
        Assert.NotNull(locations.RoomMessage);
        Assert.Null(locations.RoomNotice);
    }

    [Fact]
    public async Task Given_Laden_der_nicht_mehr_verwendeten_scheitert_When_Haken_gesetzt_Then_schaltet_er_zurueck_und_meldet()
    {
        var api = Api().On($"GET /api/locations/{TestData.LocationId}/equipment/former", new { }, HttpStatusCode.InternalServerError);
        var locations = await Open(api);

        locations.ShowFormerEquipment = true;
        await locations.FormerLoading;

        Assert.False(locations.ShowFormerEquipment);
        Assert.Empty(locations.FormerEquipment);
        Assert.NotNull(locations.EquipmentMessage);
    }

    [Fact]
    public async Task Given_nicht_mehr_verwendetes_Geraet_When_Wieder_verwenden_Then_steht_es_in_der_normalen_Liste()
    {
        var api = Api().On($"POST /api/equipment/{OldDevice.Id}/reactivate", OldDevice);
        var locations = await Open(api);
        locations.ShowFormerEquipment = true;
        await locations.FormerLoading;
        Assert.Equal("Altes Gerät", locations.FormerEquipment.Single().Name);

        await locations.FormerEquipment.Single().ReactivateCommand.ExecuteAsync(null);

        Assert.Empty(locations.FormerEquipment);
        Assert.Contains(locations.Equipment, e => e.Id == OldDevice.Id);
        Assert.Equal("Gerät Altes Gerät wird wieder verwendet", locations.EquipmentNotice);
    }

    [Fact]
    public async Task Given_Geraet_When_es_bei_gesetztem_Haken_entfernt_wird_Then_steht_es_bei_den_nicht_mehr_verwendeten()
    {
        var api = Api().On($"DELETE /api/equipment/{Bench.Id}", null, HttpStatusCode.NoContent);
        var locations = await Open(api);
        locations.ShowFormerEquipment = true;
        await locations.FormerLoading;

        await locations.Equipment.Single().RemoveCommand.ExecuteAsync(null);

        Assert.Empty(locations.Equipment);
        Assert.Equal(["Altes Gerät", "Liege"], locations.FormerEquipment.Select(e => e.Name));
    }
}
