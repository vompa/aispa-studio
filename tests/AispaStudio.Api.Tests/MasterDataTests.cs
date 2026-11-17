using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class MasterDataTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Arbeitszeiten_aendern_wirkt_sofort_auf_freie_Zeiten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var updated = await spa.PutAsync<EmployeeDto>($"/api/employees/{spa.Anna.Id}", new EmployeeInput("Anna", "#123456", [spa.Massage.Id],
            [new WorkingHours(spa.Location.Id, DayOfWeek.Monday, new(12, 0), new(20, 0))]), Ct);
        var slots = await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}", Ct);

        Assert.Equal("#123456", updated.Color);
        Assert.DoesNotContain(TestSpa.At(10), slots.Select(s => s.Start));
        Assert.Contains(TestSpa.At(18), slots.Select(s => s.Start));
    }

    [Fact]
    public async Task Ueberschneidende_Arbeitszeiten_werden_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PutAsJsonAsync($"/api/employees/{spa.Anna.Id}", new EmployeeInput("Anna", "#123456", [],
            [
                new WorkingHours(spa.Location.Id, DayOfWeek.Monday, new(9, 0), new(13, 0)),
                new WorkingHours(spa.Location.Id, DayOfWeek.Monday, new(12, 0), new(17, 0)),
            ]), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Oeffnungszeiten_aendern_schliesst_den_Standort_fuer_Gaeste_und_die_Rezeption_traegt_mit_Hinweis_ein()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        await spa.PutAsync<LocationDto>($"/api/locations/{spa.Location.Id}",
            new LocationInput("Wien neu", "Europe/Vienna", "EUR", [new OpeningHours(DayOfWeek.Tuesday, new(9, 0), new(18, 0))]), Ct);
        var slots = await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var response = await spa.BookAsync(TestSpa.At(10), Ct);

        // Der Montag ist jetzt ein Schließtag (ADR 0073, Nachtrag R03): keine freien Zeiten, aber die Rezeption trägt ein.
        Assert.Empty(slots);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["Der Standort hat an diesem Tag geschlossen."], (await TestSpa.ReadAsync<AppointmentDto>(response, Ct)).Warnings);
    }

    [Fact]
    public async Task Behandlung_aendern_laesst_gebuchte_Termine_unveraendert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10), Ct);

        var changed = await spa.PutAsync<TreatmentDto>($"/api/treatments/{spa.MassageTreatment.Id}",
            new TreatmentInput("Massage lang", 80, 5, 10, 99m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), Ct);
        var after = await spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.Id}", Ct);

        Assert.Equal(80, changed.DurationMinutes);
        Assert.Equal(booked.End, after.End);
        Assert.Equal(79m, after.Price);
    }

    [Fact]
    public async Task Raum_mit_kuenftigem_Termin_kann_nicht_deaktiviert_werden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, room: spa.RoomA.Id);

        var busy = await spa.Client.DeleteAsync($"/api/rooms/{spa.RoomA.Id}", Ct);
        var free = await spa.Client.DeleteAsync($"/api/rooms/{spa.RoomB.Id}", Ct);
        var resources = await spa.GetAsync<LocationResources>($"/api/locations/{spa.Location.Id}/resources", Ct);

        Assert.Equal(HttpStatusCode.Conflict, busy.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, free.StatusCode);
        Assert.Equal([spa.RoomA.Id], resources.Rooms.Select(r => r.Id));
        Assert.Equal([spa.RoomA.Id], Assert.Single(resources.Treatments).RoomIds);
    }

    [Fact]
    public async Task Deaktivierte_Person_wird_nicht_mehr_eingeplant()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.DeleteAsync($"/api/employees/{spa.Ben.Id}", Ct);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var second = await spa.BookAsync(TestSpa.At(10), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(spa.Anna.Id, first.EmployeeId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Nicht_mehr_angebotene_Behandlung_steht_nur_mit_includeInactive_in_der_Liste_und_wird_wieder_angeboten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var id = spa.MassageTreatment.Id;

        var deactivated = await spa.Client.DeleteAsync($"/api/treatments/{id}", Ct);
        var offered = await spa.GetAsync<List<TreatmentDto>>("/api/treatments", Ct);
        var all = await spa.GetAsync<List<TreatmentDto>>("/api/treatments?includeInactive=true", Ct);
        var reactivated = await spa.Client.PostAsync($"/api/treatments/{id}/reactivate", null, Ct);
        var again = await spa.GetAsync<List<TreatmentDto>>("/api/treatments", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.DoesNotContain(offered, t => t.Id == id);
        Assert.False(all.Single(t => t.Id == id).IsActive);
        Assert.All(all.Where(t => t.Id != id), t => Assert.True(t.IsActive));
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.True((await TestSpa.ReadAsync<TreatmentDto>(reactivated, Ct)).IsActive);
        Assert.Contains(again, t => t.Id == id);
    }

    [Fact]
    public async Task Wieder_anbieten_einer_unbekannten_Behandlung_ist_404_und_includeInactive_braucht_das_Recht_zum_Bearbeiten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var unknown = await spa.Client.PostAsync($"/api/treatments/{Guid.NewGuid()}/reactivate", null, Ct);
        var receptionAll = await reception.GetAsync("/api/treatments?includeInactive=true", Ct);
        var receptionOffered = await reception.GetAsync("/api/treatments", Ct);
        var receptionReactivate = await reception.PostAsync($"/api/treatments/{spa.MassageTreatment.Id}/reactivate", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, receptionAll.StatusCode);
        Assert.Equal(HttpStatusCode.OK, receptionOffered.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, receptionReactivate.StatusCode);
    }
}
