using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Raumcheck nach der Behandlung (ADR 0068): Mindestmengen des Raumschranks als Raumausstattung, Zeilen in der Raumpflege-Aufgabe, „Aufgefüllt“ und „Reicht noch“,
/// Raumpflege erst nach allen Antworten, Tagesstart-Punkt „Ausstattung vollständig“, Zähler der Betriebsleiste und „Von Raum … übernehmen“.
/// </summary>
public sealed class RoomRestockTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(TestSpa spa) => $"/api/stock/locations/{spa.Location.Id}";

    private static string Iso(DateOnly date) => $"{date:yyyy-MM-dd}";

    private sealed record Setup(TestSpa Spa, StoragePlaceDto Main, StoragePlaceDto Cabinet, StockItemDto Oil, StockItemDto Towels);

    /// <summary>Hauptlager mit 1000 ml Öl und 50 Handtüchern; Raumschrank von Raum A mit 200 ml Öl und 10 Handtüchern, Raumausstattung 500 ml Öl und 6 Handtücher.</summary>
    private async Task<Setup> SetupAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var places = await spa.GetAsync<List<StoragePlaceDto>>($"{Url(spa)}/storage-places", Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl neutral", "ml", 0.05m, 0, null), Ct);
        var towels = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Handtuch", "Stk", 1m, 0, null), Ct);
        var cabinet = await spa.PostAsync<StoragePlaceDto>($"{Url(spa)}/storage-places", new StoragePlaceInput("Schrank Raum A", null, StoragePlaceKind.Room, spa.RoomA.Id, true,
            [new StoragePlaceMinimumDto(oil.Id, 500m), new StoragePlaceMinimumDto(towels.Id, 6m)]), Ct);
        var main = places.Single(p => p.Kind == StoragePlaceKind.Main);
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, 1000m, 0.05m));
        await MoveAsync(spa, new StockMovementInput(towels.Id, StockMovementKind.GoodsReceipt, 50m, 1m));
        await MoveAsync(spa, new StockMovementInput(oil.Id, StockMovementKind.Refill, 200m, StoragePlaceId: main.Id, TargetStoragePlaceId: cabinet.Id));
        await MoveAsync(spa, new StockMovementInput(towels.Id, StockMovementKind.Refill, 10m, StoragePlaceId: main.Id, TargetStoragePlaceId: cabinet.Id));
        return new(spa, main, cabinet, oil, towels);
    }

    private static Task<List<StockMovementDto>> MoveAsync(TestSpa spa, StockMovementInput input) =>
        spa.PostAsync<List<StockMovementDto>>($"{Url(spa)}/movements", input, Ct);

    private static async Task<decimal> QuantityAsync(TestSpa spa, Guid placeId, Guid itemId) =>
        (await spa.GetAsync<StoragePlaceLevelsDto>($"{Url(spa)}/storage-places/{placeId}/levels", Ct)).Items.SingleOrDefault(i => i.ItemId == itemId)?.Quantity ?? 0;

    private static async Task<AppointmentDto> CompletedAsync(TestSpa spa, int hour)
    {
        var appointment = await spa.BookOkAsync(TestSpa.At(hour), Ct, spa.Anna.Id, spa.RoomA.Id);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);
        return appointment;
    }

    private static async Task<RoomCareDay> CareAsync(TestSpa spa, DateOnly? date = null) =>
        await spa.GetAsync<RoomCareDay>($"/api/locations/{spa.Location.Id}/room-care?date={Iso(date ?? TestSpa.Monday)}", Ct);

    private static string AnswerUrl(AppointmentDto appointment, StockItemDto item) => $"/api/appointments/{appointment.Id}/room-care/restock/{item.Id}";

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient client, AppointmentDto appointment, StockItemDto item, RestockState state, decimal? quantity = null) =>
        client.PutAsJsonAsync(AnswerUrl(appointment, item), new RestockAnswer(state, quantity), TestSpa.Json, Ct);

    [Fact]
    public async Task Raumpflege_nach_dem_Termin_hat_Zeilen_fuer_Material_unter_der_Mindestmenge()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);

        var booked = Assert.Single((await CareAsync(spa)).Tasks);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);
        var completed = Assert.Single((await CareAsync(spa)).Tasks);

        Assert.Empty(booked.Restock!);
        var line = Assert.Single(completed.Restock!);
        Assert.Equal(("Massageöl neutral", "ml", 200m, 500m, 300m, RestockState.Open), (line.ItemName, line.Unit, line.Quantity, line.Minimum, line.Suggested, line.State));
        Assert.Equal(setup.Oil.Id, line.ItemId);
    }

    [Fact]
    public async Task Ohne_Raumschrank_oder_bei_vollem_Raumschrank_gibt_es_keine_Zeilen()
    {
        var plain = await TestSpa.CreateAsync(fixture, Ct);
        await CompletedAsync(plain, 10);
        var full = await SetupAsync();
        await MoveAsync(full.Spa, new StockMovementInput(full.Oil.Id, StockMovementKind.Refill, 300m, StoragePlaceId: full.Main.Id, TargetStoragePlaceId: full.Cabinet.Id));
        await CompletedAsync(full.Spa, 10);

        var withoutCabinet = await CareAsync(plain);
        var enough = await CareAsync(full.Spa);

        Assert.Empty(Assert.Single(withoutCabinet.Tasks).Restock!);
        Assert.Equal(0, withoutCabinet.RoomsBelowMinimum);
        Assert.Empty(Assert.Single(enough.Tasks).Restock!);
        Assert.Equal(0, enough.RoomsBelowMinimum);
    }

    [Fact]
    public async Task Aufgefuellt_bucht_Nachfuellen_vom_Hauptlager_in_den_Raumschrank_und_die_Antwort_bleibt()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        var appointment = await CompletedAsync(spa, 10);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var response = await AnswerAsync(anna, appointment, setup.Oil, RestockState.Refilled);
        var task = await TestSpa.ReadAsync<RoomCareTaskDto>(response, Ct);
        var reloaded = Assert.Single((await CareAsync(spa)).Tasks);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movements = await spa.GetAsync<List<StockMovementDto>>($"{Url(spa)}/movements?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var line = Assert.Single(task.Restock!);
        Assert.Equal((RestockState.Refilled, 500m, 0m), (line.State, line.Quantity, line.Suggested));
        Assert.Equal(RestockState.Refilled, Assert.Single(reloaded.Restock!).State);
        Assert.Equal(500m, await QuantityAsync(spa, setup.Cabinet.Id, setup.Oil.Id));
        Assert.Equal(500m, await QuantityAsync(spa, setup.Main.Id, setup.Oil.Id));
        Assert.Contains(movements, m => m is { Kind: StockMovementKind.Refill, Quantity: -300m, Reason: "Raumcheck nach Termin" } && m.StoragePlaceId == setup.Main.Id);
        Assert.Contains(movements, m => m is { Kind: StockMovementKind.Refill, Quantity: 300m, Reason: "Raumcheck nach Termin" } && m.StoragePlaceId == setup.Cabinet.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await AnswerAsync(spa.Client, appointment, setup.Oil, RestockState.Refilled)).StatusCode);
    }

    [Fact]
    public async Task Aufgefuellt_nimmt_eine_geaenderte_Menge()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        var appointment = await CompletedAsync(spa, 10);

        var task = await TestSpa.ReadAsync<RoomCareTaskDto>(await AnswerAsync(spa.Client, appointment, setup.Oil, RestockState.Refilled, 150m), Ct);
        var zero = await AnswerAsync(spa.Client, appointment, setup.Towels, RestockState.Refilled, 0m);

        Assert.Equal(350m, task.Restock!.Single(l => l.ItemId == setup.Oil.Id).Quantity);
        Assert.Equal(350m, await QuantityAsync(spa, setup.Cabinet.Id, setup.Oil.Id));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
    }

    [Fact]
    public async Task Reicht_noch_bucht_nichts_und_bleibt_stehen()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        var appointment = await CompletedAsync(spa, 10);

        var task = await TestSpa.ReadAsync<RoomCareTaskDto>(await AnswerAsync(spa.Client, appointment, setup.Oil, RestockState.Enough), Ct);
        var reloaded = Assert.Single((await CareAsync(spa)).Tasks);
        var open = await AnswerAsync(spa.Client, appointment, setup.Oil, RestockState.Open);
        var unknown = await AnswerAsync(spa.Client, appointment, setup.Towels, RestockState.Enough);

        var line = Assert.Single(task.Restock!);
        Assert.Equal((RestockState.Enough, 200m, 300m), (line.State, line.Quantity, line.Suggested));
        Assert.Equal(RestockState.Enough, Assert.Single(reloaded.Restock!).State);
        Assert.Equal(200m, await QuantityAsync(spa, setup.Cabinet.Id, setup.Oil.Id));
        Assert.Equal(800m, await QuantityAsync(spa, setup.Main.Id, setup.Oil.Id));
        Assert.Equal(HttpStatusCode.BadRequest, open.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PutAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/room-care/restock/{setup.Oil.Id}",
            new RestockAnswer(RestockState.Enough), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Raumpflege_ist_erst_erledigt_wenn_jede_Zeile_beantwortet_ist()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        await MoveAsync(spa, new StockMovementInput(setup.Towels.Id, StockMovementKind.Count, 2m, StoragePlaceId: setup.Cabinet.Id));
        var appointment = await CompletedAsync(spa, 10);
        var check = (bool done) => spa.Client.PutAsJsonAsync($"/api/appointments/{appointment.Id}/room-care", new RoomCareCheckInput(done), TestSpa.Json, Ct);

        var twoOpen = await check(true);
        await AnswerAsync(spa.Client, appointment, setup.Oil, RestockState.Refilled);
        var oneOpen = await check(true);
        await AnswerAsync(spa.Client, appointment, setup.Towels, RestockState.Enough);
        var done = await check(true);
        var undo = await check(false);
        var care = Assert.Single((await CareAsync(spa)).Tasks);

        Assert.Equal(HttpStatusCode.BadRequest, twoOpen.StatusCode);
        Assert.Contains("Erst die Ausstattung prüfen: 2 Zeilen offen.", await twoOpen.Content.ReadAsStringAsync(Ct));
        Assert.Contains("Erst die Ausstattung prüfen: 1 Zeile offen.", await oneOpen.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, undo.StatusCode);
        Assert.Equal(RoomCareState.Open, care.State);
        Assert.Equal(2, care.Restock!.Count(l => l.State != RestockState.Open));
    }

    [Fact]
    public async Task Raumpflege_ohne_Zeilen_wird_wie_bisher_abgehakt()
    {
        var plain = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await CompletedAsync(plain, 10);

        var done = await plain.Client.PutAsJsonAsync($"/api/appointments/{appointment.Id}/room-care", new RoomCareCheckInput(true), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        Assert.Equal(RoomCareState.Done, Assert.Single((await CareAsync(plain)).Tasks).State);
    }

    [Fact]
    public async Task Tagesstart_zeigt_die_Ausstattung_je_Raum_vollstaendig_oder_mit_den_fehlenden_Materialien()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        var url = $"/api/locations/{spa.Location.Id}/room-start?date={Iso(TestSpa.Monday)}";

        var missing = Assert.Single((await spa.GetAsync<RoomStartDayDto>(url, Ct)).Rooms);
        await MoveAsync(spa, new StockMovementInput(setup.Oil.Id, StockMovementKind.Refill, 300m, StoragePlaceId: setup.Main.Id, TargetStoragePlaceId: setup.Cabinet.Id));
        var complete = Assert.Single((await spa.GetAsync<RoomStartDayDto>(url, Ct)).Rooms);

        Assert.Equal(("Raum A", RoomStartState.NoChecklist), (missing.RoomName, missing.State));
        Assert.False(missing.Equipment!.Complete);
        var line = Assert.Single(missing.Equipment.Below);
        Assert.Equal(("Massageöl neutral", 200m, 500m, 300m), (line.ItemName, line.Quantity, line.Minimum, line.Suggested));
        Assert.True(complete.Equipment!.Complete);
        Assert.Empty(complete.Equipment.Below);
    }

    [Fact]
    public async Task Raum_ohne_Ausstattung_und_ohne_Checkliste_fehlt_im_Tagesstart()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var day = await spa.GetAsync<RoomStartDayDto>($"/api/locations/{spa.Location.Id}/room-start?date={Iso(TestSpa.Monday)}", Ct);

        Assert.Empty(day.Rooms);
    }

    [Fact]
    public async Task Betriebsleiste_zaehlt_die_Raeume_unter_Mindestbestand()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        var cabinetB = await spa.PostAsync<StoragePlaceDto>($"{Url(spa)}/storage-places", new StoragePlaceInput("Schrank Raum B", null, StoragePlaceKind.Room, spa.RoomB.Id, true,
            [new StoragePlaceMinimumDto(setup.Towels.Id, 4m)]), Ct);

        var both = await CareAsync(spa);
        var inCalendar = (await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Iso(TestSpa.Monday)}", Ct)).RoomCare!;
        await MoveAsync(spa, new StockMovementInput(setup.Oil.Id, StockMovementKind.Refill, 300m, StoragePlaceId: setup.Main.Id, TargetStoragePlaceId: setup.Cabinet.Id));
        var one = await CareAsync(spa);
        await MoveAsync(spa, new StockMovementInput(setup.Towels.Id, StockMovementKind.Refill, 4m, StoragePlaceId: setup.Main.Id, TargetStoragePlaceId: cabinetB.Id));
        var none = await CareAsync(spa);

        Assert.Equal((2, 2, 1, 0), (both.RoomsBelowMinimum, inCalendar.RoomsBelowMinimum, one.RoomsBelowMinimum, none.RoomsBelowMinimum));
    }

    [Fact]
    public async Task Behandlungskarte_traegt_die_Zeilen_erst_nach_dem_Abschluss()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var card = () => anna.GetFromJsonAsync<TreatmentCard>($"/api/reports/appointments/{appointment.Id}/card", TestSpa.Json, Ct);

        var before = await card();
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);
        var after = await card();
        var plan = await anna.GetFromJsonAsync<DayPlanDto>($"/api/reports/locations/{spa.Location.Id}/day-plan?date={Iso(TestSpa.Monday)}", TestSpa.Json, Ct);

        Assert.Null(before!.Restock);
        Assert.Equal(setup.Oil.Id, Assert.Single(after!.Restock!).ItemId);
        Assert.Equal(setup.Oil.Id, Assert.Single(Assert.Single(plan!.Cards).Restock!).ItemId);
    }

    [Fact]
    public async Task Von_Raum_uebernehmen_ersetzt_die_Mindestmengen_durch_die_des_Quell_Lagerorts()
    {
        var setup = await SetupAsync();
        var spa = setup.Spa;
        var other = await spa.PostAsync<StoragePlaceDto>($"{Url(spa)}/storage-places", new StoragePlaceInput("Schrank Raum B", null, StoragePlaceKind.Room, spa.RoomB.Id, true,
            [new StoragePlaceMinimumDto(setup.Towels.Id, 99m)]), Ct);
        var empty = await spa.PostAsync<StoragePlaceDto>($"{Url(spa)}/storage-places", new StoragePlaceInput("Leer", null, StoragePlaceKind.Other, null), Ct);
        var copy = (Guid target, Guid source) => spa.Client.PostAsync($"{Url(spa)}/storage-places/{target}/minimums/copy-from/{source}", null, Ct);

        var copied = await TestSpa.ReadAsync<StoragePlaceDto>(await copy(other.Id, setup.Cabinet.Id), Ct);
        var fromEmpty = await copy(other.Id, empty.Id);
        var self = await copy(other.Id, other.Id);
        var unknown = await copy(other.Id, Guid.NewGuid());
        var second = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var foreign = (await spa.GetAsync<List<StoragePlaceDto>>($"/api/stock/locations/{second.Id}/storage-places", Ct)).Single();
        var otherLocation = await copy(other.Id, foreign.Id);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var forbidden = await reception.PostAsync($"{Url(spa)}/storage-places/{other.Id}/minimums/copy-from/{setup.Cabinet.Id}", null, Ct);

        Assert.Equal([(setup.Oil.Id, 500m), (setup.Towels.Id, 6m)], copied.Minimums.OrderByDescending(m => m.MinimumQuantity).Select(m => (m.ItemId, m.MinimumQuantity)));
        Assert.Equal(HttpStatusCode.BadRequest, fromEmpty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, otherLocation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(2, (await spa.GetAsync<List<StoragePlaceDto>>($"{Url(spa)}/storage-places", Ct)).Single(p => p.Id == other.Id).Minimums.Count);
    }
}
