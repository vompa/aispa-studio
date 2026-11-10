using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Paar- und Spa-Tag-Buchung: mehrere Behandlungen gemeinsam, alle oder keine, Paare bevorzugt in der Paarsuite.</summary>
public sealed class GroupBookingTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<CustomerDto> GuestAsync(TestSpa spa, string name) =>
        await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput(name, $"Gast{Guid.NewGuid():N}", null, null, null, null), Ct);

    private static async Task<RoomDto> CoupleSuiteAsync(TestSpa spa)
    {
        var suite = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Paarsuite", 10, 2), Ct);
        var t = spa.MassageTreatment;
        await spa.PutAsync<TreatmentDto>($"/api/treatments/{t.Id}", new TreatmentInput(t.Name, t.DurationMinutes, t.PreparationMinutes,
            t.FollowUpMinutes, t.Price, t.RequiredQualificationIds, [.. t.RoomIds, suite.Id], t.EquipmentIds), Ct);
        return suite;
    }

    private static Task<HttpResponseMessage> BookGroupAsync(TestSpa spa, DateTimeOffset start, int gap, params GroupBookingItem[] items) =>
        spa.Client.PostAsJsonAsync("/api/appointment-groups", new GroupBookingRequest(spa.Location.Id, start, items, gap), TestSpa.Json, Ct);

    private static async Task<GroupBookingResult> BookGroupOkAsync(TestSpa spa, DateTimeOffset start, int gap, params GroupBookingItem[] items)
    {
        var response = await BookGroupAsync(spa, start, gap, items);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<GroupBookingResult>(response, Ct);
    }

    [Fact]
    public async Task Paar_kommt_gemeinsam_in_die_Paarsuite_einzelne_Gaeste_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suite = await CoupleSuiteAsync(spa);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;

        var couple = await BookGroupOkAsync(spa, TestSpa.At(10), 0,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, partner.Id, 0));
        var single = await spa.BookOkAsync(TestSpa.At(12), Ct);

        Assert.Equal(2, couple.Appointments.Count);
        Assert.All(couple.Appointments, a => Assert.Equal(suite.Id, a.RoomId));
        Assert.All(couple.Appointments, a => Assert.Equal(couple.GroupId, a.GroupId));
        Assert.Equal(TestSpa.At(10), Assert.Single(couple.Appointments.Select(a => a.Start).Distinct()));
        Assert.NotEqual(couple.Appointments[0].EmployeeId, couple.Appointments[1].EmployeeId);
        Assert.NotEqual(suite.Id, single.RoomId);
        Assert.Equal(2, suite.Places);
    }

    [Fact]
    public async Task Gaeste_ohne_Namen_buchen_und_spaeter_nachtragen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CoupleSuiteAsync(spa);
        var paul = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;
        var booker = spa.Customer.Id;

        // Gast 1 und Gast 2 ohne Namen zuerst, danach die buchende Person und noch einmal Gast 1.
        var group = await BookGroupOkAsync(spa, TestSpa.At(10), 30,
            new GroupBookingItem(massage, booker, 0, PlaceholderGuest: 1), new GroupBookingItem(massage, booker, 0, PlaceholderGuest: 2),
            new GroupBookingItem(massage, booker, 1), new GroupBookingItem(massage, booker, 1, PlaceholderGuest: 1));
        Assert.Equal([1, 2, null, 1], group.Appointments.Select(a => a.PlaceholderGuest));
        Assert.All(group.Appointments, a => Assert.Equal(booker, a.CustomerId));

        var named = await spa.PutAsync<GroupBookingResult>($"/api/appointments/{group.Appointments[0].Id}/guest", new AssignGuestRequest(paul.Id), Ct);
        Assert.Equal([group.Appointments[0].Id, group.Appointments[3].Id], named.Appointments.Select(a => a.Id));
        Assert.All(named.Appointments, a => Assert.Equal((paul.Id, (int?)null), (a.CustomerId, a.PlaceholderGuest)));
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Equal(2, day.Appointments.Single(a => a.Id == group.Appointments[1].Id).PlaceholderGuest);

        var again = await spa.Client.PutAsJsonAsync($"/api/appointments/{group.Appointments[0].Id}/guest", new AssignGuestRequest(paul.Id), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        var twice = await BookGroupAsync(spa, TestSpa.At(14), 0,
            new GroupBookingItem(massage, booker, 0, PlaceholderGuest: 1), new GroupBookingItem(massage, booker, 0, PlaceholderGuest: 1));
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
    }

    [Fact]
    public async Task Ohne_Verbindung_vorgemerkte_Paarbuchung_wird_mit_ihrer_Id_genau_einmal_gebucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;
        var request = new GroupBookingRequest(spa.Location.Id, TestSpa.At(10),
            [new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, partner.Id, 0)], ClientId: Guid.NewGuid());

        var first = await TestSpa.ReadAsync<GroupBookingResult>(
            await spa.Client.PostAsJsonAsync("/api/appointment-groups", request, TestSpa.Json, Ct), Ct);
        var again = await spa.Client.PostAsJsonAsync("/api/appointment-groups", request, TestSpa.Json, Ct);
        var foreign = await other.Client.PostAsJsonAsync("/api/appointment-groups", request with
        {
            LocationId = other.Location.Id,
            Items = [new GroupBookingItem(other.MassageTreatment.Id, other.Customer.Id, 0), new GroupBookingItem(other.MassageTreatment.Id, other.Customer.Id, 1)],
        }, TestSpa.Json, Ct);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(request.ClientId, first.GroupId);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var repeated = await TestSpa.ReadAsync<GroupBookingResult>(again, Ct);
        Assert.Equal(first.GroupId, repeated.GroupId);
        Assert.Equal(first.Appointments.Select(a => a.Id).Order(), repeated.Appointments.Select(a => a.Id).Order());
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(2, day.Appointments.Count);
    }

    [Fact]
    public async Task Ohne_Paarsuite_gehen_getrennte_Raeume()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;

        var couple = await BookGroupOkAsync(spa, TestSpa.At(10), 0,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, partner.Id, 0));

        Assert.Equal([spa.RoomA.Id, spa.RoomB.Id], couple.Appointments.Select(a => a.RoomId).Order());
    }

    [Fact]
    public async Task Spa_Tag_folgt_nach_Ende_und_Pause()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var massage = spa.MassageTreatment.Id;

        var day = await BookGroupOkAsync(spa, TestSpa.At(10), 15,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, spa.Customer.Id, 1));

        Assert.Equal(TestSpa.At(10), day.Appointments[0].Start);
        Assert.Equal(day.Appointments[0].End.AddMinutes(15), day.Appointments[1].Start);
    }

    [Fact]
    public async Task Bucht_alles_oder_nichts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;
        await spa.BookOkAsync(TestSpa.At(11), Ct, employee: spa.Ben.Id);

        var response = await BookGroupAsync(spa, TestSpa.At(11), 0,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, partner.Id, 0));
        var rejected = await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.StartsWith("Massage:", rejected!.Message, StringComparison.Ordinal);
        Assert.Single(calendar.Appointments);
    }

    [Fact]
    public async Task Ein_Gast_kann_nicht_zwei_Behandlungen_gleichzeitig_haben()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var massage = spa.MassageTreatment.Id;

        var same = await BookGroupAsync(spa, TestSpa.At(10), 0,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, spa.Customer.Id, 0));
        var gap = await BookGroupAsync(spa, TestSpa.At(10), 0,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, spa.Customer.Id, 2));

        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, gap.StatusCode);
    }

    [Fact]
    public async Task Freie_Zeiten_passen_fuer_alle_und_lassen_sich_buchen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;
        await spa.BookOkAsync(TestSpa.At(9, 15), Ct, employee: spa.Anna.Id);
        GroupBookingItem[] items = [new(massage, spa.Customer.Id, 0), new(massage, partner.Id, 0)];

        var slots = await spa.PostAsync<List<GroupSlot>>($"/api/locations/{spa.Location.Id}/appointment-groups/availability",
            new GroupAvailabilityRequest(TestSpa.Monday, items), Ct);
        var first = slots[0];
        var booked = await BookGroupOkAsync(spa, first.Start, 0, items);

        // Anna ist mit Nachbereitung bis 10:15 gebunden, Raum A mit Raumpflege bis 10:30; mit 5 min Vorbereitung geht es erst um 10:45.
        Assert.Equal(TestSpa.At(10, 45), first.Start);
        Assert.All(slots, s => Assert.NotEqual(s.Items[0].EmployeeId, s.Items[1].EmployeeId));
        Assert.Equal(first.Items.Select(i => i.EmployeeId), booked.Appointments.Select(a => a.EmployeeId));
    }

    [Fact]
    public async Task Gemeinsam_stornieren_gibt_alles_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suite = await CoupleSuiteAsync(spa);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;
        GroupBookingItem[] items = [new(massage, spa.Customer.Id, 0), new(massage, partner.Id, 0)];
        var couple = await BookGroupOkAsync(spa, TestSpa.At(10), 0, items);

        var cancelled = await spa.PostAsync<GroupBookingResult>(
            $"/api/locations/{spa.Location.Id}/appointment-groups/{couple.GroupId}/cancel", new CancelAppointmentRequest("Krank"), Ct);
        var again = await BookGroupOkAsync(spa, TestSpa.At(10), 0, items);

        Assert.All(cancelled.Appointments, a => Assert.Equal(AppointmentStatus.Cancelled, a.Status));
        Assert.All(again.Appointments, a => Assert.Equal(suite.Id, a.RoomId));
    }

    [Fact]
    public async Task Therapeutin_darf_keine_gemeinsame_Buchung_anlegen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var massage = spa.MassageTreatment.Id;

        var response = await anna.PostAsJsonAsync("/api/appointment-groups", new GroupBookingRequest(spa.Location.Id, TestSpa.At(10),
            [new(massage, spa.Customer.Id, 0), new(massage, spa.Customer.Id, 1)]), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    private static Task<HttpResponseMessage> MoveGroupAsync(TestSpa spa, Guid groupId, DateTimeOffset start) =>
        spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/appointment-groups/{groupId}/move", new MoveGroupRequest(start), TestSpa.Json, Ct);

    [Fact]
    public async Task Paar_wird_gemeinsam_verschoben_und_behaelt_Paarsuite_und_Personen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suite = await CoupleSuiteAsync(spa);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;
        var couple = await BookGroupOkAsync(spa, TestSpa.At(10), 0,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, partner.Id, 0));

        var moved = await TestSpa.ReadAsync<GroupBookingResult>(await MoveGroupAsync(spa, couple.GroupId, TestSpa.At(14)), Ct);
        var oldTimeFree = await spa.BookAsync(TestSpa.At(10), Ct, employee: couple.Appointments[0].EmployeeId);

        Assert.Equal(couple.GroupId, moved.GroupId);
        Assert.Equal(couple.Appointments.Select(a => a.Id).Order(), moved.Appointments.Select(a => a.Id).Order());
        Assert.All(moved.Appointments, a => Assert.Equal(TestSpa.At(14), a.Start));
        Assert.All(moved.Appointments, a => Assert.Equal(suite.Id, a.RoomId));
        Assert.Equal(couple.Appointments.ToDictionary(a => a.Id, a => a.EmployeeId), moved.Appointments.ToDictionary(a => a.Id, a => a.EmployeeId));
        Assert.Equal(HttpStatusCode.Created, oldTimeFree.StatusCode);
    }

    [Fact]
    public async Task Spa_Tag_behaelt_beim_Verschieben_Ablauf_und_Pause()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var massage = spa.MassageTreatment.Id;
        var day = await BookGroupOkAsync(spa, TestSpa.At(10), 15,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, spa.Customer.Id, 1));

        var moved = await TestSpa.ReadAsync<GroupBookingResult>(await MoveGroupAsync(spa, day.GroupId, TestSpa.At(13)), Ct);

        Assert.Equal(TestSpa.At(13), moved.Appointments[0].Start);
        Assert.Equal(moved.Appointments[0].End.AddMinutes(15), moved.Appointments[1].Start);
    }

    [Fact]
    public async Task Verschieben_gelingt_ganz_oder_gar_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;
        var couple = await BookGroupOkAsync(spa, TestSpa.At(10), 0,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, partner.Id, 0));
        await spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Employee, spa.Ben.Id, TestSpa.At(14), TestSpa.At(15), "Schulung"), Ct);

        var response = await MoveGroupAsync(spa, couple.GroupId, TestSpa.At(14));
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.All(day.Appointments.Where(a => a.GroupId == couple.GroupId), a => Assert.Equal(TestSpa.At(10), a.Start));
    }

    [Fact]
    public async Task Eingecheckte_Buchung_wird_nicht_gemeinsam_verschoben()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var partner = await GuestAsync(spa, "Paul");
        var massage = spa.MassageTreatment.Id;
        var couple = await BookGroupOkAsync(spa, TestSpa.At(10), 0,
            new GroupBookingItem(massage, spa.Customer.Id, 0), new GroupBookingItem(massage, partner.Id, 0));
        await spa.SetStatusAsync(couple.Appointments[0].Id, AppointmentStatus.CheckedIn, Ct);

        var response = await MoveGroupAsync(spa, couple.GroupId, TestSpa.At(14));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
