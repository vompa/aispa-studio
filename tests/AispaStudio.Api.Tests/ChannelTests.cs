using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Tests;

/// <summary>Rechte je Kanal (Slice 104): Anzeigegerät nur lesend, Mitarbeiter-App nur eigene Daten, Kunden-App nur eigene Termine.</summary>
public sealed partial class ChannelTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gegeben_eine_gekoppelte_Anzeige_Wenn_sie_einen_Termin_verschieben_will_Dann_antwortet_die_API_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        using var display = await PairDisplayAsync(spa, Ct);

        var move = await display.PostAsJsonAsync($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(12), null, null, null), TestSpa.Json, Ct);
        var calendar = await display.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        // Nur mit Anmeldung, ohne Rechteprüfung: hier sperrt allein der Kanal.
        var me = await display.GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, move.StatusCode);
        Assert.Contains("Über diesen Zugang nicht freigegeben.", await move.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, calendar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, me.StatusCode);
    }

    [Fact]
    public async Task Gegeben_eine_gekoppelte_Anzeige_Wenn_die_Inhaberin_sie_widerruft_Dann_meldet_sich_die_Anzeige_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var display = await PairDisplayAsync(spa, Ct);

        var session = await TestSpa.ReadAsync<DisplaySession>(await display.GetAsync("/api/display/session", Ct), Ct);
        var listed = await spa.GetAsync<List<DisplayDeviceDto>>($"/api/locations/{spa.Location.Id}/displays", Ct);
        var revoke = await spa.Client.DeleteAsync($"/api/displays/{session.DeviceId}", Ct);
        var after = await display.GetAsync("/api/display/session", Ct);
        var listedAfter = await spa.GetAsync<List<DisplayDeviceDto>>($"/api/locations/{spa.Location.Id}/displays", Ct);

        Assert.Equal("Pausenraum", session.Name);
        Assert.Equal(spa.Location.Id, session.LocationId);
        Assert.Equal([session.DeviceId], listed.Select(d => d.Id));
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Empty(listedAfter);
    }

    [Fact]
    public async Task Kopplung_mit_falschem_Code_oder_ohne_Recht_am_Standort_scheitert_und_vor_dem_Koppeln_wartet_die_Anzeige()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var device = fixture.CreateClient();
        var start = await TestSpa.ReadAsync<DisplayPairingStart>(await device.PostAsync("/api/display/pairing", null, Ct), Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var waiting = await device.PostAsJsonAsync("/api/display/pairing/redeem", new DisplayPairingRedeem(start.Secret), TestSpa.Json, Ct);
        var wrongCode = await spa.Client.PostAsJsonAsync("/api/displays", new DisplayPairInput("ABCD-EFGH", "Pausenraum", spa.Location.Id), TestSpa.Json, Ct);
        var noRight = await reception.PostAsJsonAsync("/api/displays", new DisplayPairInput(start.Code, "Pausenraum", spa.Location.Id), TestSpa.Json, Ct);
        var wrongSecret = await device.PostAsJsonAsync("/api/display/pairing/redeem", new DisplayPairingRedeem("falsch"), TestSpa.Json, Ct);

        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", start.Code);
        Assert.Equal(HttpStatusCode.Accepted, waiting.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, noRight.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.StatusCode);
    }

    [Fact]
    public async Task Gegeben_Lena_in_der_Mitarbeiter_App_Wenn_sie_den_Plan_von_Sophie_abruft_Dann_antwortet_die_API_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        var sophie = await spa.CreateEmployeeAsync("Sophie", [spa.Massage.Id], spa.Location.Id, Ct);
        // Rezeption darf am Arbeitsplatz fremde Pläne sehen (Recht Book); in der Mitarbeiter-App zählt nur die eigene Person.
        using var app = await StaffAppAsync(spa, StaffRole.Reception, lena.Id, Ct);
        var dayPlan = $"/api/reports/locations/{spa.Location.Id}/day-plan?date={TestSpa.Monday:yyyy-MM-dd}";

        var own = await app.GetAsync($"{dayPlan}&employeeId={lena.Id}", Ct);
        var others = await app.GetAsync($"{dayPlan}&employeeId={sophie.Id}", Ct);
        var me = await TestSpa.ReadAsync<CurrentUser>(await app.GetAsync("/api/auth/me", Ct), Ct);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, others.StatusCode);
        Assert.Equal(AccessChannel.StaffApp, me.Channel);
    }

    [Fact]
    public async Task Mitarbeiter_App_erreicht_nur_freigegebene_Endpunkte_auch_wenn_die_Rolle_mehr_darf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        using var app = await StaffAppAsync(spa, StaffRole.Reception, lena.Id, Ct);

        var calendar = await app.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var customers = await app.GetAsync("/api/customers?search=Eva", Ct);
        var registers = await app.GetAsync($"/api/pos/registers?locationId={spa.Location.Id}", Ct);
        var again = await app.PostAsync("/api/auth/channel/staff-app", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, calendar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customers.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, registers.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
    }

    /// <summary>T-015 (Auftraggeber, 05.11.2026): keine Zeiterfassung per Handy; die Mitarbeiter-App erreicht die Stempeluhr nicht mehr.</summary>
    [Fact]
    public async Task Mitarbeiter_App_kann_nicht_stempeln()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        using var app = await StaffAppAsync(spa, StaffRole.Therapist, lena.Id, Ct);

        var status = await app.GetAsync("/api/time/me", Ct);
        var clockIn = await app.PostAsJsonAsync("/api/time/me/clock-in", new { locationId = spa.Location.Id }, Ct);
        var clockOut = await app.PostAsync("/api/time/me/clock-out", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clockIn.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clockOut.StatusCode);
    }

    [Fact]
    public async Task Mitarbeiter_App_setzt_den_Status_nur_eigener_Termine_und_der_Verlauf_nennt_die_App()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        var own = await spa.BookOkAsync(TestSpa.At(10), Ct, lena.Id);
        var others = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        using var app = await StaffAppAsync(spa, StaffRole.Reception, lena.Id, Ct);

        var started = await app.PostAsJsonAsync($"/api/appointments/{own.Id}/status", new AppointmentStatusChange(AppointmentStatus.CheckedIn), TestSpa.Json, Ct);
        var foreign = await app.PostAsJsonAsync($"/api/appointments/{others.Id}/status", new AppointmentStatusChange(AppointmentStatus.CheckedIn), TestSpa.Json, Ct);
        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/appointments/{own.Id}/history", Ct);

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.StartsWith("Eingecheckt in der Mitarbeiter-App von Test Person", history[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mitarbeiter_App_bleibt_es_auch_wenn_Identity_das_Cookie_nach_dem_Sicherheitsstempel_neu_ausstellt()
    {
        var options = fixture.Factory.Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;
        var current = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Channels.ClaimType, nameof(AccessChannel.StaffApp))], IdentityConstants.ApplicationScheme));
        var renewed = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "benutzer-1")], IdentityConstants.ApplicationScheme));

        await options.OnRefreshingPrincipal!(new SecurityStampRefreshingPrincipalContext { CurrentPrincipal = current, NewPrincipal = renewed });

        Assert.Equal(AccessChannel.StaffApp, Channels.Of(renewed));
    }

    [Fact]
    public async Task Am_Arbeitsplatz_bleibt_alles_wie_bisher()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var me = await spa.GetAsync<CurrentUser>("/api/auth/me", Ct);
        var calendar = await spa.Client.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(AccessChannel.Workstation, me.Channel);
        Assert.Equal(HttpStatusCode.OK, calendar.StatusCode);
    }

    [Fact]
    public async Task Kunden_App_sagt_keinen_fremden_Termin_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, customer: spa.Customer.Id);
        using var stranger = spa.OnlineClient(await CustomerCookieAsync(spa, $"fremd-{Guid.NewGuid():N}@example.at", Ct));

        var cancel = await stranger.PostAsync($"/api/online/me/appointments/{appointment.Id}/cancel", null, Ct);
        var mine = await TestSpa.ReadAsync<List<OnlineAppointment>>(await stranger.GetAsync("/api/online/me/appointments", Ct), Ct);

        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Empty(mine);
    }

    /// <summary>Wie das Dashboard: Code holen, die Inhaberin koppelt ihn in der Windows-App, dann holt die Anzeige ihr Cookie.</summary>
    internal static async Task<HttpClient> PairDisplayAsync(TestSpa spa, CancellationToken ct)
    {
        using var device = spa.Fixture.CreateClient();
        var start = await TestSpa.ReadAsync<DisplayPairingStart>(await device.PostAsync("/api/display/pairing", null, ct), ct);
        _ = await spa.PostAsync<DisplayDeviceDto>("/api/displays", new DisplayPairInput(start.Code.ToLowerInvariant(), "Pausenraum", spa.Location.Id), ct);
        var redeemed = await device.PostAsJsonAsync("/api/display/pairing/redeem", new DisplayPairingRedeem(start.Secret), TestSpa.Json, ct);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        return spa.Fixture.CreateClient(Cookies(redeemed));
    }

    /// <summary>Anmeldung als Person, danach der Wechsel in den Kanal der Mitarbeiter-App (wie nach der Passkey-Anmeldung am Handy).</summary>
    internal static async Task<HttpClient> StaffAppAsync(TestSpa spa, StaffRole role, Guid employeeId, CancellationToken ct)
    {
        using var signedIn = await TestUsers.ClientAsync(spa.Fixture, ct, role, [spa.Location.Id], employeeId);
        var narrowed = await signedIn.PostAsync("/api/auth/channel/staff-app", null, ct);
        Assert.Equal(HttpStatusCode.OK, narrowed.StatusCode);
        return spa.Fixture.CreateClient(Cookies(narrowed));
    }

    private async Task<string> CustomerCookieAsync(TestSpa spa, string email, CancellationToken ct)
    {
        using var client = spa.OnlineClient();
        var request = await client.PostAsJsonAsync("/api/online/auth/code", new SignInCodeRequest(email, "de"), TestSpa.Json, ct);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Anmeldecode", StringComparison.Ordinal), ct);
        var verify = await client.PostAsJsonAsync("/api/online/auth/code/verify",
            new SignInCodeVerification(email, SixDigits().Match(mail.Subject).Value), TestSpa.Json, ct);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return Cookies(verify);
    }

    private static string Cookies(HttpResponseMessage response) =>
        string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex SixDigits();
}
