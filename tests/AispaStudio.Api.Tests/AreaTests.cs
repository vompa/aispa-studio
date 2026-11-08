using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Bereiche mit Kapazität (Sauna, Ruheraum): Eintritte belegen Plätze, nie mehr als vorhanden.</summary>
public sealed class AreaTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<RoomDto> SaunaAsync(TestSpa spa, int places) =>
        spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Sauna", 0, places, IsArea: true), Ct);

    private static Task<HttpResponseMessage> VisitAsync(TestSpa spa, RoomDto area, DateTimeOffset from, DateTimeOffset until, int persons = 1) =>
        spa.Client.PostAsJsonAsync("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, area.Id, spa.Customer.Id, from, until, persons), TestSpa.Json, Ct);

    private static Task<List<AreaDay>> DayAsync(TestSpa spa) =>
        spa.GetAsync<List<AreaDay>>($"/api/locations/{spa.Location.Id}/areas?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

    [Fact]
    public async Task Belegt_Plaetze_bis_zur_Kapazitaet_und_zeigt_den_Verlauf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await SaunaAsync(spa, 3);

        var couple = await VisitAsync(spa, sauna, TestSpa.At(10), TestSpa.At(13), persons: 2);
        var third = await VisitAsync(spa, sauna, TestSpa.At(11), TestSpa.At(12));
        var full = await VisitAsync(spa, sauna, TestSpa.At(11, 30), TestSpa.At(12, 30));
        var later = await VisitAsync(spa, sauna, TestSpa.At(12), TestSpa.At(14));
        var rejected = await full.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        var day = Assert.Single(await DayAsync(spa));

        Assert.Equal(HttpStatusCode.Created, couple.StatusCode);
        Assert.Equal(HttpStatusCode.Created, third.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        Assert.Equal(HttpStatusCode.Created, later.StatusCode);
        Assert.Contains("voll (3 von 3", Assert.Single(rejected!.Conflicts).Reason, StringComparison.Ordinal);
        Assert.Equal(3, day.Places);
        Assert.Equal(3, day.Visits.Count);
        Assert.Equal(
            [(TestSpa.At(10), 2), (TestSpa.At(11), 3), (TestSpa.At(13), 1), (TestSpa.At(14), 0)],
            day.Load.Select(l => (l.Start, l.Occupied)));
    }

    [Fact]
    public async Task Ohne_Verbindung_vorgemerkter_Eintritt_wird_mit_seiner_Id_genau_einmal_gebucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await SaunaAsync(spa, 1);
        var request = new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(10), TestSpa.At(12), ClientId: Guid.NewGuid());

        var first = await TestSpa.ReadAsync<AreaVisitDto>(await spa.Client.PostAsJsonAsync("/api/area-visits", request, TestSpa.Json, Ct), Ct);
        var again = await spa.Client.PostAsJsonAsync("/api/area-visits", request, TestSpa.Json, Ct);
        var otherCustomer = await spa.Client.PostAsJsonAsync("/api/area-visits",
            request with { CustomerId = (await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Ida", "Anders", null, null, null, null), Ct)).Id },
            TestSpa.Json, Ct);
        var foreign = await other.Client.PostAsJsonAsync("/api/area-visits",
            request with { LocationId = other.Location.Id, CustomerId = other.Customer.Id }, TestSpa.Json, Ct);

        Assert.Equal(request.ClientId, first.Id);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(first, await TestSpa.ReadAsync<AreaVisitDto>(again, Ct));
        Assert.Equal(HttpStatusCode.BadRequest, otherCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Single(Assert.Single(await DayAsync(spa)).Visits);
    }

    [Fact]
    public async Task Stornieren_gibt_die_Plaetze_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await SaunaAsync(spa, 1);
        var visit = await TestSpa.ReadAsync<AreaVisitDto>(await VisitAsync(spa, sauna, TestSpa.At(10), TestSpa.At(12)), Ct);

        var cancelled = await spa.PostAsync<AreaVisitDto>($"/api/locations/{spa.Location.Id}/area-visits/{visit.Id}/cancel", new { }, Ct);
        var again = await VisitAsync(spa, sauna, TestSpa.At(10), TestSpa.At(12));

        Assert.True(cancelled.IsCancelled);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Geschlossen_gesperrt_oder_als_Behandlungsraum_geht_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await SaunaAsync(spa, 10);
        await spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Room, sauna.Id, TestSpa.At(15), TestSpa.At(16), "Aufguss-Ofen Wartung"), Ct);

        var closed = await VisitAsync(spa, sauna, TestSpa.At(19), TestSpa.At(21));
        var blocked = await VisitAsync(spa, sauna, TestSpa.At(14), TestSpa.At(15, 30));
        var asRoom = await spa.Client.PostAsJsonAsync("/api/treatments",
            new TreatmentInput("Saunamassage", 30, 0, 0, 40m, [], [sauna.Id], []), TestSpa.Json, Ct);
        var tooMany = await VisitAsync(spa, sauna, TestSpa.At(10), TestSpa.At(11), persons: 21);

        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, asRoom.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task Therapeutin_darf_keinen_Eintritt_buchen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await SaunaAsync(spa, 5);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var response = await anna.PostAsJsonAsync("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(10), TestSpa.At(11)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Planer_vergibt_kleinste_freie_Plaetze_und_achtet_auf_Sperren()
    {
        var from = TestSpa.At(10);
        var until = TestSpa.At(12);
        var visit = Guid.NewGuid();
        ResourceBooking Taken(int slot) => new() { Slot = slot, From = from, Until = until, AreaVisitId = visit };

        Assert.Equal([1, 3], AreaPlanner.FreePlaces(4, [Taken(0), Taken(2)], from, until, 2));
        Assert.Null(AreaPlanner.FreePlaces(4, [Taken(0), Taken(2)], from, until, 3));
        Assert.Null(AreaPlanner.FreePlaces(4, [new ResourceBooking { From = from, Until = until, Reason = "Wartung" }], from, until, 1));
        Assert.Equal([0], AreaPlanner.FreePlaces(1, [Taken(0)], until, until.AddHours(1), 1));
    }
}
