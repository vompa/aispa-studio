using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// „Wieder verwenden“ bei Räumen und Geräten (ADR 0099): ein aus der Planung genommener Raum oder ein Gerät steht in <c>…/former</c>, nicht mehr im Standort,
/// und lässt sich per <c>POST …/reactivate</c> zurückholen. Beides braucht <c>ManageLocation</c>.
/// </summary>
public sealed class FormerRoomsAndEquipmentTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_entfernter_Raum_When_Liste_der_nicht_mehr_verwendeten_gelesen_Then_steht_er_dort_und_nicht_im_Standort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var room = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Ruheraum", 0), Ct);

        var deactivated = await spa.Client.DeleteAsync($"/api/rooms/{room.Id}", Ct);
        var former = await spa.GetAsync<List<RoomDto>>($"/api/locations/{spa.Location.Id}/rooms/former", Ct);
        var resources = await spa.GetAsync<LocationResources>($"/api/locations/{spa.Location.Id}/resources", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.Equal([room.Id], former.Select(r => r.Id));
        Assert.DoesNotContain(resources.Rooms, r => r.Id == room.Id);
    }

    [Fact]
    public async Task Given_nicht_mehr_verwendete_Raeume_When_gelesen_Then_nach_Name_sortiert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var zeta = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Zeta", 0), Ct);
        var alpha = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Alpha", 0), Ct);
        await spa.Client.DeleteAsync($"/api/rooms/{zeta.Id}", Ct);
        await spa.Client.DeleteAsync($"/api/rooms/{alpha.Id}", Ct);

        var former = await spa.GetAsync<List<RoomDto>>($"/api/locations/{spa.Location.Id}/rooms/former", Ct);

        Assert.Equal(["Alpha", "Zeta"], former.Select(r => r.Name));
    }

    [Fact]
    public async Task Given_nicht_mehr_verwendeter_Raum_When_wieder_verwendet_Then_steht_er_im_Standort_und_ist_buchbar()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.Client.DeleteAsync($"/api/rooms/{spa.RoomB.Id}", Ct);

        var reactivated = await spa.Client.PostAsync($"/api/rooms/{spa.RoomB.Id}/reactivate", null, Ct);
        var former = await spa.GetAsync<List<RoomDto>>($"/api/locations/{spa.Location.Id}/rooms/former", Ct);
        var resources = await spa.GetAsync<LocationResources>($"/api/locations/{spa.Location.Id}/resources", Ct);
        var booked = await spa.BookAsync(TestSpa.At(10), Ct, room: spa.RoomB.Id);

        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal(spa.RoomB.Id, (await TestSpa.ReadAsync<RoomDto>(reactivated, Ct)).Id);
        Assert.Empty(former);
        Assert.Contains(resources.Rooms, r => r.Id == spa.RoomB.Id);
        Assert.True(booked.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Given_aktiver_Raum_When_wieder_verwendet_Then_aendert_sich_nichts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var reactivated = await spa.Client.PostAsync($"/api/rooms/{spa.RoomA.Id}/reactivate", null, Ct);
        var resources = await spa.GetAsync<LocationResources>($"/api/locations/{spa.Location.Id}/resources", Ct);

        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal(1, resources.Rooms.Count(r => r.Id == spa.RoomA.Id));
    }

    [Fact]
    public async Task Given_entferntes_Geraet_When_Liste_gelesen_und_wieder_verwendet_Then_wandert_es_zwischen_den_Listen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var equipment = await spa.PostAsync<EquipmentDto>($"/api/locations/{spa.Location.Id}/equipment", new EquipmentInput("Heizstein"), Ct);

        await spa.Client.DeleteAsync($"/api/equipment/{equipment.Id}", Ct);
        var former = await spa.GetAsync<List<EquipmentDto>>($"/api/locations/{spa.Location.Id}/equipment/former", Ct);
        var inLocation = await spa.GetAsync<LocationResources>($"/api/locations/{spa.Location.Id}/resources", Ct);
        var reactivated = await spa.Client.PostAsync($"/api/equipment/{equipment.Id}/reactivate", null, Ct);
        var formerAfter = await spa.GetAsync<List<EquipmentDto>>($"/api/locations/{spa.Location.Id}/equipment/former", Ct);
        var back = await spa.GetAsync<LocationResources>($"/api/locations/{spa.Location.Id}/resources", Ct);

        Assert.Equal([equipment.Id], former.Select(e => e.Id));
        Assert.DoesNotContain(inLocation.Equipment, e => e.Id == equipment.Id);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal(equipment.Id, (await TestSpa.ReadAsync<EquipmentDto>(reactivated, Ct)).Id);
        Assert.Empty(formerAfter);
        Assert.Contains(back.Equipment, e => e.Id == equipment.Id);
    }

    [Fact]
    public async Task Given_unbekannte_Ids_und_fremder_Mandant_When_wieder_verwendet_Then_404()
    {
        var a = await TestSpa.CreateAsync(fixture, Ct);
        var b = await TestSpa.CreateAsync(fixture, Ct);
        var room = await a.PostAsync<RoomDto>($"/api/locations/{a.Location.Id}/rooms", new RoomInput("Ruheraum", 0), Ct);
        var equipment = await a.PostAsync<EquipmentDto>($"/api/locations/{a.Location.Id}/equipment", new EquipmentInput("Heizstein"), Ct);
        await a.Client.DeleteAsync($"/api/rooms/{room.Id}", Ct);
        await a.Client.DeleteAsync($"/api/equipment/{equipment.Id}", Ct);

        var unknownRoom = await a.Client.PostAsync($"/api/rooms/{Guid.NewGuid()}/reactivate", null, Ct);
        var unknownEquipment = await a.Client.PostAsync($"/api/equipment/{Guid.NewGuid()}/reactivate", null, Ct);
        var foreignRoom = await b.Client.PostAsync($"/api/rooms/{room.Id}/reactivate", null, Ct);
        var foreignEquipment = await b.Client.PostAsync($"/api/equipment/{equipment.Id}/reactivate", null, Ct);
        var foreignRooms = await b.Client.GetAsync($"/api/locations/{a.Location.Id}/rooms/former", Ct);
        var foreignEquipmentList = await b.Client.GetAsync($"/api/locations/{a.Location.Id}/equipment/former", Ct);

        Assert.Equal(HttpStatusCode.NotFound, unknownRoom.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownEquipment.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignRoom.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignEquipment.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignRooms.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignEquipmentList.StatusCode);
    }

    [Fact]
    public async Task Given_Empfang_ohne_Recht_zum_Verwalten_When_Liste_oder_Wieder_verwenden_Then_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var rooms = await reception.GetAsync($"/api/locations/{spa.Location.Id}/rooms/former", Ct);
        var equipment = await reception.GetAsync($"/api/locations/{spa.Location.Id}/equipment/former", Ct);
        var room = await reception.PostAsync($"/api/rooms/{spa.RoomA.Id}/reactivate", null, Ct);
        var device = await reception.PostAsync($"/api/equipment/{Guid.NewGuid()}/reactivate", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, rooms.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, equipment.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, room.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, device.StatusCode);
    }
}
