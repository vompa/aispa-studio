using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Mandantentrennung: zwei Spas in derselben Datenbank sehen und ändern nie die Daten des anderen.</summary>
public sealed class TenantTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ein_Spa_sieht_die_Daten_eines_anderen_nicht()
    {
        var a = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await a.BookOkAsync(TestSpa.At(10), Ct);
        var b = await TestSpa.CreateAsync(fixture, Ct);

        var locations = await b.GetAsync<List<LocationDto>>("/api/locations", Ct);
        var employees = await b.GetAsync<List<EmployeeDto>>("/api/employees", Ct);
        var treatments = await b.GetAsync<List<TreatmentDto>>("/api/treatments", Ct);
        var customers = await b.GetAsync<List<CustomerDto>>($"/api/customers?search={a.Customer.LastName}", Ct);

        Assert.Equal([b.Location.Id], locations.Select(l => l.Id));
        Assert.DoesNotContain(employees, e => e.Id == a.Anna.Id);
        Assert.DoesNotContain(treatments, t => t.Id == a.MassageTreatment.Id);
        Assert.Empty(customers);
        Assert.False((await b.Client.GetAsync($"/api/locations/{a.Location.Id}/resources", Ct)).IsSuccessStatusCode);
        Assert.False((await b.Client.GetAsync($"/api/customers/{a.Customer.Id}", Ct)).IsSuccessStatusCode);
        Assert.False((await b.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest("Test"), TestSpa.Json, Ct)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Ein_Spa_kann_fremde_Daten_nicht_aendern_oder_verwenden()
    {
        var a = await TestSpa.CreateAsync(fixture, Ct);
        var b = await TestSpa.CreateAsync(fixture, Ct);

        var room = await b.Client.PutAsJsonAsync($"/api/rooms/{a.RoomA.Id}", new RoomInput("Übernommen", 0), TestSpa.Json, Ct);
        var booking = await b.BookAsync(TestSpa.At(10), Ct, customer: a.Customer.Id);
        var foreignTreatment = await b.BookAsync(TestSpa.At(11), Ct, treatment: a.MassageTreatment);

        Assert.False(room.IsSuccessStatusCode);
        Assert.False(booking.IsSuccessStatusCode);
        Assert.False(foreignTreatment.IsSuccessStatusCode);
        var resources = await a.GetAsync<LocationResources>($"/api/locations/{a.Location.Id}/resources", Ct);
        Assert.Contains(resources.Rooms, r => r.Name == "Raum A");
    }

    [Fact]
    public async Task Gleiche_Namen_sind_in_verschiedenen_Spas_erlaubt()
    {
        var a = await TestSpa.CreateAsync(fixture, Ct);
        var b = await TestSpa.CreateAsync(fixture, Ct);
        var name = $"Shiatsu {Guid.NewGuid():N}";

        var first = await a.Client.PostAsJsonAsync("/api/qualifications", new QualificationInput(name), TestSpa.Json, Ct);
        var second = await b.Client.PostAsJsonAsync("/api/qualifications", new QualificationInput(name), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Neues_Spa_registrieren_legt_Inhaber_an_und_startet_leer()
    {
        using var anonymous = fixture.CreateClient();
        var email = $"inhaberin-{Guid.NewGuid():N}@aispa.test";

        var response = await anonymous.PostAsJsonAsync("/api/tenant/signup",
            new TenantSignup("Day Spa Linz", "AT", email, "Maria Inhaberin"), TestSpa.Json, Ct);
        var user = await TestSpa.ReadAsync<CurrentUser>(response, Ct);
        using var owner = fixture.CreateClient(string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));

        Assert.Equal(StaffRole.Owner, user.Access?.Role);
        Assert.Equal("Day Spa Linz", user.Access?.TenantName);
        Assert.Empty((await owner.GetFromJsonAsync<List<LocationDto>>("/api/locations", TestSpa.Json, Ct))!);
        Assert.Equal("Day Spa Linz", (await owner.GetFromJsonAsync<TenantDto>("/api/tenant", TestSpa.Json, Ct))!.Name);

        var again = await anonymous.PostAsJsonAsync("/api/tenant/signup",
            new TenantSignup("Noch ein Spa", "AT", email, "Maria Inhaberin"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Eingeladene_Person_landet_im_Spa_der_Einladung()
    {
        var a = await TestSpa.CreateAsync(fixture, Ct);
        var created = await a.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput($"neu-{Guid.NewGuid():N}@aispa.test", "Neu", StaffRole.Reception, false, [a.Location.Id], null), Ct);
        using var anonymous = fixture.CreateClient();

        var accepted = await anonymous.PostAsync($"/api/auth/invitations/{created.Token}/accept", null, Ct);
        var user = await TestSpa.ReadAsync<CurrentUser>(accepted, Ct);
        using var reception = fixture.CreateClient(string.Join("; ", accepted.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));

        Assert.Equal(a.Location.Id, Assert.Single((await reception.GetFromJsonAsync<List<LocationDto>>("/api/locations", TestSpa.Json, Ct))!).Id);
        Assert.Equal(user.Access?.TenantId, (await a.GetAsync<CurrentUser>("/api/auth/me", Ct)).Access?.TenantId);
    }

    [Fact]
    public async Task Buchungsseite_zeigt_nur_das_eigene_Spa()
    {
        var a = await TestSpa.CreateAsync(fixture, Ct);
        var b = await TestSpa.CreateAsync(fixture, Ct);
        using var page = b.OnlineClient();

        var locations = await page.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct);
        var foreignTreatments = await page.GetAsync($"/api/online/locations/{a.Location.Id}/treatments", Ct);

        Assert.Equal([b.Location.Id], locations!.Select(l => l.Id));
        Assert.Equal(HttpStatusCode.NotFound, foreignTreatments.StatusCode);
    }
}
