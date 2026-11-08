using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Spa-Tag als Baukasten: Sauna oder Ruheraum als Teil der Buchung, Reihenfolge mit den wenigsten Wartezeiten vom Planer.</summary>
public sealed class SpaDayBuilderTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<RoomDto> SaunaAsync(TestSpa spa, int places) =>
        spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Sauna", 0, places, IsArea: true), Ct);

    private static async Task<CustomerDto> GuestAsync(TestSpa spa) =>
        await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Gabi", $"Gast{Guid.NewGuid():N}", null, null, null, null), Ct);

    /// <summary>Anna und Ben sind um 10 Uhr belegt (bis 11:00 samt Nachbereitung).</summary>
    private static async Task BusyAtTenAsync(TestSpa spa)
    {
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Ben.Id);
    }

    private static Task<List<GroupSlot>> SlotsAsync(TestSpa spa, GroupAvailabilityRequest request) =>
        spa.PostAsync<List<GroupSlot>>($"/api/locations/{spa.Location.Id}/appointment-groups/availability", request, Ct);

    [Fact]
    public async Task Planer_stellt_die_Sauna_vor_die_Massage_wenn_der_Tag_so_frueher_endet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await SaunaAsync(spa, 4);
        var guest = await GuestAsync(spa);
        await BusyAtTenAsync(spa);
        GroupBookingItem[] items = [new(spa.MassageTreatment.Id, guest.Id, 0)];
        GroupAreaItem[] areas = [new(sauna.Id, guest.Id, 1, 60)];

        var fixedOrder = await SlotsAsync(spa, new GroupAvailabilityRequest(TestSpa.Monday, items, 0, areas));
        var flexible = await SlotsAsync(spa, new GroupAvailabilityRequest(TestSpa.Monday, items, 0, areas, FlexibleOrder: true));

        Assert.DoesNotContain(fixedOrder, s => s.Start == TestSpa.At(10));
        var ten = Assert.Single(flexible, s => s.Start == TestSpa.At(10));
        Assert.Equal(new GroupSlotArea(TestSpa.At(10), TestSpa.At(11), sauna.Id, guest.Id), Assert.Single(ten.Areas!));
        // Nach der Sauna fünf Minuten warten, bis Anna oder Ben samt Vorbereitung frei ist.
        Assert.Equal(TestSpa.At(11, 5), Assert.Single(ten.Items).Start);
        Assert.Equal(TestSpa.At(11, 55), ten.End);

        var response = await spa.Client.PostAsJsonAsync("/api/appointment-groups",
            new GroupBookingRequest(spa.Location.Id, TestSpa.At(10), items, 0, "Geburtstag", null, areas, FlexibleOrder: true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booked = await TestSpa.ReadAsync<GroupBookingResult>(response, Ct);
        var visit = Assert.Single(booked.AreaVisits!);
        Assert.Equal(booked.GroupId, visit.GroupId);
        Assert.Equal(TestSpa.At(10), visit.From);
        Assert.Equal(TestSpa.At(11, 5), Assert.Single(booked.Appointments).Start);

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Equal(visit.Id, Assert.Single(day.GroupAreas!).Id);
        var areaDay = await spa.GetAsync<List<AreaDay>>($"/api/locations/{spa.Location.Id}/areas?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Single(Assert.Single(areaDay).Visits);

        // Ganze Buchung verschieben: Sauna und Massage behalten Reihenfolge und Abstand (die fünf Minuten Warten bleiben).
        var moved = await spa.PostAsync<GroupBookingResult>($"/api/locations/{spa.Location.Id}/appointment-groups/{booked.GroupId}/move",
            new MoveGroupRequest(TestSpa.At(13)), Ct);
        Assert.Equal(TestSpa.At(13), Assert.Single(moved.AreaVisits!).From);
        Assert.Equal(TestSpa.At(14, 5), Assert.Single(moved.Appointments).Start);

        var cancelled = await spa.PostAsync<GroupBookingResult>($"/api/locations/{spa.Location.Id}/appointment-groups/{booked.GroupId}/cancel",
            new CancelAppointmentRequest("Gast krank"), Ct);
        Assert.True(Assert.Single(cancelled.AreaVisits!).IsCancelled);
        var after = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Empty(after.GroupAreas!);
    }

    [Fact]
    public async Task Volle_Sauna_und_falsche_Angaben_werden_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await SaunaAsync(spa, 1);
        var guest = await GuestAsync(spa);
        await spa.PostAsync<AreaVisitDto>("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(9), TestSpa.At(13)), Ct);
        GroupBookingItem[] items = [new(spa.MassageTreatment.Id, guest.Id, 0)];

        var slots = await SlotsAsync(spa, new GroupAvailabilityRequest(TestSpa.Monday, items, 0, [new(sauna.Id, guest.Id, 1, 60)]));
        Assert.All(slots, s => Assert.True(s.Areas![0].From >= TestSpa.At(13)));

        foreach (var wrong in new[]
        {
            new GroupAvailabilityRequest(TestSpa.Monday, items, 0, [new(sauna.Id, guest.Id, 1, 5)]),
            new GroupAvailabilityRequest(TestSpa.Monday, items, 0, [new(sauna.Id, guest.Id, 0, 60)]),
            new GroupAvailabilityRequest(TestSpa.Monday, items, 0, [new(spa.RoomA.Id, guest.Id, 1, 60)]),
            new GroupAvailabilityRequest(TestSpa.Monday, items, 0,
                [new(sauna.Id, guest.Id, 1, 30), new(sauna.Id, guest.Id, 2, 30), new(sauna.Id, guest.Id, 3, 30), new(sauna.Id, guest.Id, 4, 30)],
                FlexibleOrder: true),
        })
        {
            var response = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/appointment-groups/availability", wrong, TestSpa.Json, Ct);
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, $"{response.StatusCode} für {wrong}");
        }
    }
}
