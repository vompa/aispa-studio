using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Das Muster „Löschen oder nicht“ (ADR 0099): <c>deletion-check</c> zählt, was an Raum, Gerät, Qualifikation, Lieferant und Lagerort hängt; Löschen geht nur, wenn nichts daran hängt,
/// sonst antwortet es mit 409 und derselben Liste (Qualifikation, Lieferant) oder gar nicht (Raum, Gerät: nur „Nicht mehr verwenden“).
/// </summary>
public sealed class DeletionCheckTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<DeletionCheckDto> CheckAsync(TestSpa spa, string path) => spa.GetAsync<DeletionCheckDto>(path, Ct);

    [Fact]
    public async Task Given_Qualifikation_ohne_Zuordnung_When_geprueft_und_geloescht_Then_ist_sie_weg()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var unused = await spa.PostAsync<QualificationDto>("/api/qualifications", new QualificationInput($"Fango {Guid.NewGuid():N}"), Ct);

        var check = await CheckAsync(spa, $"/api/qualifications/{unused.Id}/deletion-check");
        var delete = await spa.Client.DeleteAsync($"/api/qualifications/{unused.Id}", Ct);
        var left = await spa.GetAsync<List<QualificationDto>>("/api/qualifications", Ct);

        Assert.True(check.CanDelete);
        Assert.Empty(check.Dependents);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(left, q => q.Id == unused.Id);
    }

    [Fact]
    public async Task Given_Qualifikation_bei_Personen_und_Behandlung_When_geloescht_Then_409_mit_der_Liste_und_sie_bleibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var check = await CheckAsync(spa, $"/api/qualifications/{spa.Massage.Id}/deletion-check");
        var delete = await spa.Client.DeleteAsync($"/api/qualifications/{spa.Massage.Id}", Ct);
        var conflict = await delete.Content.ReadFromJsonAsync<DeletionCheckDto>(TestSpa.Json, Ct);

        Assert.False(check.CanDelete);
        Assert.Equal(["2 Personen im Team", "1 Behandlung"], check.Dependents.Select(d => d.Label));
        Assert.Equal(["employees", "treatments"], check.Dependents.Select(d => d.Kind));
        Assert.Null(check.Alternative);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal(check.Dependents.Select(d => d.Label), conflict!.Dependents.Select(d => d.Label));
        Assert.Contains(await spa.GetAsync<List<QualificationDto>>("/api/qualifications", Ct), q => q.Id == spa.Massage.Id);
    }

    [Fact]
    public async Task Given_Raum_mit_Behandlung_und_kuenftigem_Termin_When_geprueft_Then_zaehlt_er_beides_und_bietet_Nicht_mehr_verwenden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, room: spa.RoomA.Id);

        var check = await CheckAsync(spa, $"/api/rooms/{spa.RoomA.Id}/deletion-check");
        var rejected = await spa.Client.DeleteAsync($"/api/rooms/{spa.RoomA.Id}", Ct);

        Assert.False(check.CanDelete);
        Assert.Equal(["1 künftiger Termin", "1 Behandlung"], check.Dependents.Select(d => d.Label));
        Assert.Equal(new DeletionAlternative("deactivate", "Nicht mehr verwenden"), check.Alternative);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    [Fact]
    public async Task Given_Raum_und_Geraet_ohne_Verweise_When_geprueft_Then_lassen_sie_sich_nicht_loeschen_aber_ausser_Betrieb_nehmen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var room = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Ruheraum", 0), Ct);
        var equipment = await spa.PostAsync<EquipmentDto>($"/api/locations/{spa.Location.Id}/equipment", new EquipmentInput("Heizstein"), Ct);

        var roomCheck = await CheckAsync(spa, $"/api/rooms/{room.Id}/deletion-check");
        var equipmentCheck = await CheckAsync(spa, $"/api/equipment/{equipment.Id}/deletion-check");
        var deactivated = await spa.Client.DeleteAsync($"/api/equipment/{equipment.Id}", Ct);
        var afterwards = await spa.Client.GetAsync($"/api/equipment/{equipment.Id}/deletion-check", Ct);

        Assert.False(roomCheck.CanDelete);
        Assert.Empty(roomCheck.Dependents);
        Assert.False(equipmentCheck.CanDelete);
        Assert.NotNull(equipmentCheck.Alternative);
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task Given_Lieferant_ohne_und_mit_Material_When_geloescht_Then_nur_der_erste_geht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var unused = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Ohne Material"), Ct);
        var used = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Mit Material"), Ct);
        await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.02m, 0, null, SupplierId: used.Id), Ct);

        var free = await CheckAsync(spa, $"/api/stock/suppliers/{unused.Id}/deletion-check");
        var tied = await CheckAsync(spa, $"/api/stock/suppliers/{used.Id}/deletion-check");
        var deleteFree = await spa.Client.DeleteAsync($"/api/stock/suppliers/{unused.Id}", Ct);
        var deleteTied = await spa.Client.DeleteAsync($"/api/stock/suppliers/{used.Id}", Ct);
        var conflict = await deleteTied.Content.ReadFromJsonAsync<DeletionCheckDto>(TestSpa.Json, Ct);
        var left = await spa.GetAsync<List<StockSupplierDto>>("/api/stock/suppliers", Ct);

        Assert.True(free.CanDelete);
        Assert.False(tied.CanDelete);
        Assert.Equal("1 Material", Assert.Single(tied.Dependents).Label);
        Assert.Equal("Nicht mehr verwenden", tied.Alternative?.Label);
        Assert.Equal(HttpStatusCode.NoContent, deleteFree.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteTied.StatusCode);
        Assert.Equal("1 Material", Assert.Single(conflict!.Dependents).Label);
        Assert.Equal([used.Id], left.Select(s => s.Id));
    }

    [Fact]
    public async Task Given_deaktivierter_Lieferant_mit_Material_When_geprueft_Then_gibt_es_keine_Alternative_mehr()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Alt GmbH"), Ct);
        await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Maske", "Stück", 1m, 0, null, SupplierId: supplier.Id), Ct);
        await spa.PutAsync<StockSupplierDto>($"/api/stock/suppliers/{supplier.Id}", new StockSupplierInput("Alt GmbH", IsActive: false), Ct);

        var check = await CheckAsync(spa, $"/api/stock/suppliers/{supplier.Id}/deletion-check");

        Assert.False(check.CanDelete);
        Assert.Null(check.Alternative);
    }

    [Fact]
    public async Task Given_Lagerorte_When_geprueft_Then_geht_Loeschen_nur_ohne_Bewegungen_und_nie_beim_Hauptlager()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = $"/api/stock/locations/{spa.Location.Id}";
        var places = await spa.GetAsync<List<StoragePlaceDto>>($"{url}/storage-places", Ct);
        var main = places.Single(p => p.Kind == StoragePlaceKind.Main);
        var shelf = await spa.PostAsync<StoragePlaceDto>($"{url}/storage-places", new StoragePlaceInput("Regal", null, StoragePlaceKind.Other, null), Ct);
        var used = await spa.PostAsync<StoragePlaceDto>($"{url}/storage-places", new StoragePlaceInput("Schrank", null, StoragePlaceKind.Other, null), Ct);
        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Handtuch", "Stück", 2m, 0, null), Ct);
        await spa.PostAsync<List<StockMovementDto>>($"{url}/movements",
            new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, 5m, 2m, StoragePlaceId: used.Id), Ct);

        var free = await CheckAsync(spa, $"{url}/storage-places/{shelf.Id}/deletion-check");
        var tied = await CheckAsync(spa, $"{url}/storage-places/{used.Id}/deletion-check");
        var hub = await CheckAsync(spa, $"{url}/storage-places/{main.Id}/deletion-check");
        var elsewhere = await spa.Client.GetAsync($"/api/stock/locations/{Guid.NewGuid()}/storage-places/{shelf.Id}/deletion-check", Ct);

        Assert.True(free.CanDelete);
        Assert.False(tied.CanDelete);
        Assert.Equal("1 Lagerbewegung", Assert.Single(tied.Dependents).Label);
        Assert.Equal("Nicht mehr verwenden", tied.Alternative?.Label);
        Assert.False(hub.CanDelete);
        Assert.Null(hub.Alternative);
        Assert.Equal(HttpStatusCode.BadRequest, elsewhere.StatusCode);
    }

    [Fact]
    public async Task Given_Rezeption_When_geprueft_oder_geloescht_Then_verboten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Nur Inhaber"), Ct);

        var checkQualification = await reception.GetAsync($"/api/qualifications/{spa.Massage.Id}/deletion-check", Ct);
        var deleteQualification = await reception.DeleteAsync($"/api/qualifications/{spa.Massage.Id}", Ct);
        var checkRoom = await reception.GetAsync($"/api/rooms/{spa.RoomA.Id}/deletion-check", Ct);
        var deleteSupplier = await reception.DeleteAsync($"/api/stock/suppliers/{supplier.Id}", Ct);

        Assert.All([checkQualification, deleteQualification, checkRoom, deleteSupplier], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }
}
