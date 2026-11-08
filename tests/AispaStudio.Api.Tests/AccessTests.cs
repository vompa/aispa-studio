using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Rollen und Standortzugang: was welche Rolle darf und dass der Server es bei jedem Aufruf prüft.</summary>
public sealed class AccessTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Inhaber_sieht_seine_Rolle_und_alle_Rechte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var me = await spa.GetAsync<CurrentUser>("/api/auth/me", Ct);

        Assert.Equal(StaffRole.Owner, me.Access?.Role);
        Assert.True(me.Access?.AllLocations);
        Assert.Equal(Enum.GetValues<AccessRight>(), me.Rights);
    }

    [Fact]
    public async Task Rezeption_bucht_aber_pflegt_keine_Stammdaten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var booked = await reception.PostAsJsonAsync("/api/appointments", new AppointmentRequest(
            spa.Location.Id, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(10), null, null, null, null), TestSpa.Json, Ct);
        var room = await reception.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Raum C", 0), TestSpa.Json, Ct);
        var location = await reception.PostAsJsonAsync("/api/locations",
            new LocationInput("Graz", "Europe/Vienna", "EUR", []), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, room.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, location.StatusCode);
    }

    [Fact]
    public async Task Standortleitung_sieht_nur_eigene_Standorte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var salzburg = await spa.CreateLocationAsync("Salzburg", [DayOfWeek.Monday], Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [salzburg.Id]);

        var locations = await manager.GetFromJsonAsync<List<LocationDto>>("/api/locations", TestSpa.Json, Ct);
        var ownCalendar = await manager.GetAsync($"/api/locations/{salzburg.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var otherCalendar = await manager.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var otherRoom = await manager.PutAsJsonAsync($"/api/rooms/{spa.RoomA.Id}", new RoomInput("Umbenannt", 0), TestSpa.Json, Ct);

        Assert.Equal([salzburg.Id], locations!.Select(l => l.Id));
        Assert.Equal(HttpStatusCode.OK, ownCalendar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherCalendar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherRoom.StatusCode);
    }

    [Fact]
    public async Task Standortleitung_aendert_Person_ohne_Arbeitszeiten_anderer_Standorte_zu_verlieren()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var salzburg = await spa.CreateLocationAsync("Salzburg", [DayOfWeek.Tuesday], Ct);
        var anna = await spa.PutAsync<EmployeeDto>($"/api/employees/{spa.Anna.Id}", new EmployeeInput("Anna", "#2f6f5e", [spa.Massage.Id],
        [
            new WorkingHours(spa.Location.Id, DayOfWeek.Monday, new(9, 0), new(17, 0)),
            new WorkingHours(salzburg.Id, DayOfWeek.Tuesday, new(9, 0), new(17, 0)),
        ]), Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [salzburg.Id]);

        // Die Standortleitung Salzburg schickt nur ihre eigenen Zeiten (wie die Oberfläche, die Wien nicht kennt).
        var response = await manager.PutAsJsonAsync($"/api/employees/{anna.Id}", new EmployeeInput("Anna M.", "#2f6f5e", [spa.Massage.Id],
            [new WorkingHours(salzburg.Id, DayOfWeek.Tuesday, new(10, 0), new(18, 0))]), TestSpa.Json, Ct);
        var updated = await TestSpa.ReadAsync<EmployeeDto>(response, Ct);

        Assert.Equal("Anna M.", updated.DisplayName);
        Assert.Contains(new WorkingHours(spa.Location.Id, DayOfWeek.Monday, new(9, 0), new(17, 0)), updated.WorkingHours);
        Assert.Contains(new WorkingHours(salzburg.Id, DayOfWeek.Tuesday, new(10, 0), new(18, 0)), updated.WorkingHours);
    }

    [Fact]
    public async Task Therapeutin_sieht_Kalender_und_setzt_nur_eigene_Termine_auf_eingecheckt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var own = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var other = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Ben.Id);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var calendar = await anna.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var book = await anna.PostAsJsonAsync("/api/appointments", new AppointmentRequest(
            spa.Location.Id, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(14), null, null, null, null), TestSpa.Json, Ct);
        var ownStatus = await anna.PostAsJsonAsync($"/api/appointments/{own.Id}/status",
            new AppointmentStatusChange(AppointmentStatus.CheckedIn), TestSpa.Json, Ct);
        var otherStatus = await anna.PostAsJsonAsync($"/api/appointments/{other.Id}/status",
            new AppointmentStatusChange(AppointmentStatus.CheckedIn), TestSpa.Json, Ct);
        var me = await anna.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, calendar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, book.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownStatus.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherStatus.StatusCode);
        Assert.Equal(spa.Anna.Id, me!.EmployeeId);
    }

    [Fact]
    public async Task Kassa_nur_fuer_Rollen_mit_Buchungsrecht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var receptionRegisters = await reception.GetAsync($"/api/pos/registers?locationId={spa.Location.Id}", Ct);
        var therapistRegisters = await therapist.GetAsync($"/api/pos/registers?locationId={spa.Location.Id}", Ct);
        var otherLocation = await reception.GetAsync($"/api/pos/registers?locationId={Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.OK, receptionRegisters.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, therapistRegisters.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherLocation.StatusCode);
    }

    [Fact]
    public async Task Einladung_annehmen_legt_Konto_mit_Rolle_an_und_gilt_nur_einmal()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = $"rezeption-{Guid.NewGuid():N}@aispa.test";
        var created = await spa.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput(email, "Rita Rezeption", StaffRole.Reception, false, [spa.Location.Id], null), Ct);
        using var anonymous = fixture.CreateClient();

        var preview = await anonymous.GetFromJsonAsync<InvitationPreview>($"/api/auth/invitations/{created.Token}", TestSpa.Json, Ct);
        var accepted = await anonymous.PostAsync($"/api/auth/invitations/{created.Token}/accept", null, Ct);
        var user = await TestSpa.ReadAsync<CurrentUser>(accepted, Ct);
        var again = await anonymous.PostAsync($"/api/auth/invitations/{created.Token}/accept", null, Ct);
        var staff = await spa.GetAsync<List<StaffMemberDto>>("/api/staff", Ct);

        Assert.Equal(["Wien"], preview!.LocationNames);
        Assert.Equal(StaffRole.Reception, user.Access?.Role);
        Assert.Equal([spa.Location.Id], user.Access?.LocationIds);
        Assert.Contains(AccessRight.Book, user.Rights);
        Assert.DoesNotContain(AccessRight.ManageCatalog, user.Rights);
        Assert.True(accepted.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Contains(staff, s => s.Email == email && s.Role == StaffRole.Reception);
    }

    [Fact]
    public async Task Standortleitung_laedt_keine_Inhaber_und_nicht_fuer_fremde_Standorte_ein()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var salzburg = await spa.CreateLocationAsync("Salzburg", [DayOfWeek.Monday], Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [salzburg.Id]);

        var owner = await manager.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"x-{Guid.NewGuid():N}@aispa.test", "X", StaffRole.Owner, true, [], null), TestSpa.Json, Ct);
        var otherLocation = await manager.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"y-{Guid.NewGuid():N}@aispa.test", "Y", StaffRole.Reception, false, [spa.Location.Id], null), TestSpa.Json, Ct);
        var ownLocation = await manager.PostAsJsonAsync("/api/staff/invitations",
            new InvitationInput($"z-{Guid.NewGuid():N}@aispa.test", "Z", StaffRole.Reception, false, [salzburg.Id], null), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, owner.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, otherLocation.StatusCode);
        Assert.Equal(HttpStatusCode.Created, ownLocation.StatusCode);
    }

    [Fact]
    public async Task Gesperrter_Zugang_wirkt_sofort_trotz_Anmeldung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var me = await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);

        var updated = await spa.Client.PutAsJsonAsync($"/api/staff/{me!.Id}",
            new StaffAccessInput(StaffRole.Reception, false, [spa.Location.Id], null, IsActive: false), TestSpa.Json, Ct);
        var calendar = await reception.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var meAfter = await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, calendar.StatusCode);
        Assert.Null(meAfter!.Access);
        Assert.Empty(meAfter.Rights);
    }

    [Fact]
    public async Task Eigenen_Zugang_aendert_man_nicht_selbst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var me = await spa.GetAsync<CurrentUser>("/api/auth/me", Ct);

        var response = await spa.Client.PutAsJsonAsync($"/api/staff/{me.Id}",
            new StaffAccessInput(StaffRole.Reception, true, [], null, true), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Live_Kanal_nur_fuer_Standorte_mit_Zugang()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var salzburg = await spa.CreateLocationAsync("Salzburg", [DayOfWeek.Monday], Ct);
        var cookie = await TestUsers.RegisterAsync(fixture, Ct, StaffRole.Reception, [salzburg.Id]);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, LiveHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .Build();
        await connection.StartAsync(Ct);

        await connection.InvokeAsync(LiveHubContract.WatchLocation, salzburg.Id, Ct);
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, Ct));
    }
    [Fact]
    public async Task Befristeter_Zugang_gilt_bis_zum_Ende_und_wirkt_danach_wie_gesperrt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var me = await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        var until = DateTimeOffset.UtcNow.AddDays(30);

        var limited = await spa.PutAsync<StaffMemberDto>($"/api/staff/{me!.Id}",
            new StaffAccessInput(StaffRole.Reception, false, [spa.Location.Id], null, true, until), Ct);
        var before = await reception.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AispaDbContext>().StaffAccess.IgnoreQueryFilters()
                .Where(a => a.UserId == me.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
        }

        var after = await reception.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var meAfter = await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        var listed = (await spa.GetAsync<List<StaffMemberDto>>("/api/staff", Ct)).Single(m => m.UserId == me.Id);

        Assert.True(Math.Abs((limited.ExpiresAt!.Value - until).TotalSeconds) < 1);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        Assert.Null(meAfter!.Access);
        Assert.True(listed.ExpiresAt < DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Befristung_nicht_fuer_Inhaber_und_nicht_in_der_Vergangenheit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        var me = await manager.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);

        var owner = await spa.Client.PutAsJsonAsync($"/api/staff/{me!.Id}",
            new StaffAccessInput(StaffRole.Owner, true, [], null, true, DateTimeOffset.UtcNow.AddDays(5)), TestSpa.Json, Ct);
        var past = await spa.Client.PutAsJsonAsync($"/api/staff/{me.Id}",
            new StaffAccessInput(StaffRole.Manager, false, [spa.Location.Id], null, true, DateTimeOffset.UtcNow.AddDays(-1)), TestSpa.Json, Ct);
        var tooLong = await spa.Client.PutAsJsonAsync($"/api/staff/{me.Id}",
            new StaffAccessInput(StaffRole.Manager, false, [spa.Location.Id], null, true, DateTimeOffset.UtcNow.AddYears(3)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, owner.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Einladung_traegt_die_Befristung_in_den_Zugang()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var until = DateTimeOffset.UtcNow.AddDays(60);
        var created = await spa.PostAsync<InvitationCreated>("/api/staff/invitations",
            new InvitationInput($"aushilfe-{Guid.NewGuid():N}@aispa.test", "Sommer Aushilfe", StaffRole.Reception, false, [spa.Location.Id], null, until), Ct);
        using var anonymous = fixture.CreateClient();

        var user = await TestSpa.ReadAsync<CurrentUser>(await anonymous.PostAsync($"/api/auth/invitations/{created.Token}/accept", null, Ct), Ct);
        var listed = (await spa.GetAsync<List<StaffMemberDto>>("/api/staff", Ct)).Single(m => m.UserId == user.Id);

        Assert.True(Math.Abs((created.Invitation.AccessExpiresAt!.Value - until).TotalSeconds) < 1);
        Assert.True(Math.Abs((listed.ExpiresAt!.Value - until).TotalSeconds) < 1);
    }
}
