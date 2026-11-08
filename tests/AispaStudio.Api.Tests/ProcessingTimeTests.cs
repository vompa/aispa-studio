using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Schritt 13: Einwirkzeit. Der Gast bleibt im Raum, die Person ist in der Zeit frei und kann einen anderen Gast behandeln.</summary>
public sealed class ProcessingTimeTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Packung: 60 Minuten, 5 Vorbereitung, 10 Nachbereitung, 20 Minuten am Gast, dann 20 Minuten Einwirkzeit.</summary>
    private static Task<TreatmentDto> CreatePackAsync(TestSpa spa, int handsOn = 20, int processing = 20) =>
        spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Packung", 60, 5, 10, 85m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], [],
                HandsOnMinutes: handsOn, ProcessingMinutes: processing), Ct);

    /// <summary>Kurze Behandlung ohne Vor- und Nachbereitung, füllt die Einwirkzeit genau.</summary>
    private static Task<TreatmentDto> CreateShortAsync(TestSpa spa) =>
        spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Kurz", 20, 0, 0, 25m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), Ct);

    private async Task<List<ResourceBooking>> BookingsOfAsync(TestSpa spa, Guid appointmentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().ResourceBookings.AsNoTracking()
            .Where(b => b.AppointmentId == appointmentId).OrderBy(b => b.From).ToListAsync(Ct);
    }

    [Fact]
    public async Task Die_Person_bekommt_zwei_Belegungen_der_Raum_eine()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var pack = await CreatePackAsync(spa);

        var response = await spa.BookAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomB.Id, pack);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(response, Ct);

        Assert.Equal((20, 20), (appointment.HandsOnMinutes, appointment.ProcessingMinutes));
        Assert.Equal((TestSpa.At(9, 55), TestSpa.At(11, 10)), (appointment.BlockedFrom, appointment.BlockedUntil));
        var bookings = await BookingsOfAsync(spa, appointment.Id);
        var person = bookings.Where(b => b.Kind == ResourceKind.Employee).ToList();
        Assert.Equal(2, person.Count);
        Assert.Equal((TestSpa.At(9, 55), TestSpa.At(10, 20)), (person[0].From, person[0].Until));
        Assert.Equal((TestSpa.At(10, 40), TestSpa.At(11, 10)), (person[1].From, person[1].Until));
        var room = Assert.Single(bookings, b => b.Kind == ResourceKind.Room);
        Assert.Equal((TestSpa.At(9, 55), TestSpa.At(11, 10), spa.RoomB.Id), (room.From, room.Until, room.ResourceId));
    }

    [Fact]
    public async Task Ein_zweiter_Termin_der_Person_in_der_Einwirkzeit_in_einem_anderen_Raum_wird_angenommen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var pack = await CreatePackAsync(spa);
        var shortTreatment = await CreateShortAsync(spa);
        await spa.BookAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomB.Id, pack);

        var response = await spa.BookAsync(TestSpa.At(10, 20), Ct, spa.Anna.Id, spa.RoomA.Id, shortTreatment);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var second = await TestSpa.ReadAsync<AppointmentDto>(response, Ct);
        Assert.Equal((spa.Anna.Id, spa.RoomA.Id), (second.EmployeeId, second.RoomId));
    }

    [Fact]
    public async Task Derselbe_Termin_in_den_ersten_Minuten_am_Gast_wird_mit_Begruendung_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var pack = await CreatePackAsync(spa);
        var shortTreatment = await CreateShortAsync(spa);
        await spa.BookAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomB.Id, pack);

        var response = await spa.BookAsync(TestSpa.At(10, 10), Ct, spa.Anna.Id, spa.RoomA.Id, shortTreatment);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rejected = (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        Assert.Contains(rejected.Conflicts, c => c.Kind == ResourceKind.Employee && c.ResourceId == spa.Anna.Id);

        // Auch der Raum bleibt in der Einwirkzeit belegt.
        var sameRoom = await spa.BookAsync(TestSpa.At(10, 20), Ct, spa.Anna.Id, spa.RoomB.Id, shortTreatment);
        Assert.Equal(HttpStatusCode.Conflict, sameRoom.StatusCode);
        var roomRejected = (await sameRoom.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        Assert.Contains(roomRejected.Conflicts, c => c.Kind == ResourceKind.Room && c.ResourceId == spa.RoomB.Id);
    }

    [Fact]
    public async Task Verschieben_und_Aendern_der_Zeiten_behalten_die_Einwirkzeit_des_Termins_auch_nach_Aenderung_der_Behandlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var pack = await CreatePackAsync(spa);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomB.Id, pack), Ct);
        await spa.PutAsync<TreatmentDto>($"/api/treatments/{pack.Id}",
            new TreatmentInput("Packung", 60, 5, 10, 85m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], [], HandsOnMinutes: 10, ProcessingMinutes: 10), Ct);

        var moved = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(13), spa.Anna.Id, null, null), Ct);

        Assert.Equal((20, 20), (moved.HandsOnMinutes, moved.ProcessingMinutes));
        var person = (await BookingsOfAsync(spa, appointment.Id)).Where(b => b.Kind == ResourceKind.Employee).ToList();
        Assert.Equal([(TestSpa.At(12, 55), TestSpa.At(13, 20)), (TestSpa.At(13, 40), TestSpa.At(14, 10))], person.Select(b => (b.From, b.Until)));

        var changed = await spa.PutAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/times", new AppointmentTimesRequest(10, 10), Ct);

        Assert.Equal((20, 20, 10, 10), (changed.HandsOnMinutes, changed.ProcessingMinutes, changed.PreparationMinutes, changed.FollowUpMinutes));
        person = [.. (await BookingsOfAsync(spa, appointment.Id)).Where(b => b.Kind == ResourceKind.Employee)];
        Assert.Equal([(TestSpa.At(12, 50), TestSpa.At(13, 20)), (TestSpa.At(13, 40), TestSpa.At(14, 10))], person.Select(b => (b.From, b.Until)));
    }

    [Fact]
    public async Task Vollkosten_zaehlen_die_Einwirkzeit_nicht_als_Personalzeit_wohl_aber_als_Raumzeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreatePackAsync(spa);

        var analysis = await spa.GetAsync<CostAnalysisDto>($"/api/costs/locations/{spa.Location.Id}/analysis?month=2027-03", Ct);

        var massage = Assert.Single(analysis.Treatments, t => t.TreatmentName == "Massage");
        var pack = Assert.Single(analysis.Treatments, t => t.TreatmentName == "Packung");
        Assert.Equal((65, 73), (massage.StaffMinutes, massage.RoomMinutes));
        // 5 + 60 + 10 Minuten gebunden, davon 20 Einwirkzeit; der Raum bleibt für 75 Minuten plus durchschnittlich 7,5 Minuten Raumpflege belegt.
        Assert.Equal((55, 83), (pack.StaffMinutes, pack.RoomMinutes));
    }

    [Fact]
    public async Task Eingaben_werden_geprueft()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        async Task<string> RejectAsync(int duration, int handsOn, int processing)
        {
            var response = await spa.Client.PostAsJsonAsync("/api/treatments",
                new TreatmentInput("Packung", duration, 5, 10, 85m, [spa.Massage.Id], [spa.RoomA.Id], [], HandsOnMinutes: handsOn, ProcessingMinutes: processing),
                TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return await response.Content.ReadAsStringAsync(Ct);
        }

        Assert.Contains("dürfen zusammen die Dauer nicht übersteigen", await RejectAsync(60, 30, 40), StringComparison.Ordinal);
        Assert.Contains("Minuten am Gast davor", await RejectAsync(60, 0, 20), StringComparison.Ordinal);
        Assert.Contains("Zwischen 0 und 120", await RejectAsync(300, 10, 121), StringComparison.Ordinal);
        Assert.Contains("Zwischen 0 und 120", await RejectAsync(300, 121, 10), StringComparison.Ordinal);

        // Genau die Dauer ist erlaubt (keine Ausarbeitung danach).
        var exact = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Maske", 60, 5, 10, 85m, [spa.Massage.Id], [spa.RoomA.Id], [], HandsOnMinutes: 30, ProcessingMinutes: 30), Ct);
        Assert.Equal((30, 30), (exact.HandsOnMinutes, exact.ProcessingMinutes));
    }

    [Fact]
    public async Task Freie_Zeit_suchen_findet_eine_Zeit_in_der_Einwirkzeit_der_Person()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var pack = await CreatePackAsync(spa);
        var shortTreatment = await CreateShortAsync(spa);
        await spa.BookAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomB.Id, pack);

        var slots = await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={shortTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}", Ct);

        var inside = Assert.Single(slots, s => s.Start == TestSpa.At(10, 20));
        Assert.Equal((spa.Anna.Id, spa.RoomA.Id), (inside.EmployeeId, inside.RoomId));
        // Während der ersten 20 Minuten am Gast ist die Person nicht frei.
        Assert.DoesNotContain(slots, s => s.Start == TestSpa.At(10, 10));
    }
}
