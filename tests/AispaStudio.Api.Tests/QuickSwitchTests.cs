using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Schnellwechsel am Empfangsgerät: PIN gilt nur auf einem angemeldeten Gerät, nur am dessen Standort, und sperrt nach Fehlversuchen.</summary>
public sealed class QuickSwitchTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Wechsel_per_PIN_nur_am_angemeldeten_Empfangsgeraet_und_nur_fuer_Personen_mit_PIN()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ownerCookie = await TestUsers.RegisterAsync(fixture, Ct, StaffRole.Owner, [spa.Location.Id]);
        using var owner = fixture.CreateClient(ownerCookie);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);
        var me = (await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!;

        // Ohne Gerät: kein Wechsel, auch nicht mit richtiger PIN.
        var tooShort = await reception.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("12"), TestSpa.Json, Ct);
        var sameDigits = await reception.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("1111"), TestSpa.Json, Ct);
        var set = await reception.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("2580"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sameDigits.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.True((await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.HasPin);

        using var stranger = fixture.CreateClient();
        var noDevice = await stranger.GetFromJsonAsync<QuickSwitchState>("/api/auth/quick-switch", TestSpa.Json, Ct);
        var noDeviceSwitch = await stranger.PostAsJsonAsync("/api/auth/quick-switch", new QuickSwitchRequest(me.Id, "2580"), TestSpa.Json, Ct);
        Assert.Null(noDevice!.Device);
        Assert.Equal(HttpStatusCode.Unauthorized, noDeviceSwitch.StatusCode);

        // Die Leitung meldet das Gerät an; die Therapeutin darf das nicht.
        var denied = await therapist.PostAsJsonAsync("/api/auth/shared-device", new SharedDeviceInput("Empfang", spa.Location.Id), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var tooLong = await owner.PostAsJsonAsync("/api/auth/shared-device", new SharedDeviceInput("Empfang", spa.Location.Id, 600), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var registered = await owner.PostAsJsonAsync("/api/auth/shared-device", new SharedDeviceInput("Empfang", spa.Location.Id, 3), TestSpa.Json, Ct);
        registered.EnsureSuccessStatusCode();
        var deviceCookie = registered.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).Single(c => c.StartsWith("aispa.device=", StringComparison.Ordinal));

        using var device = fixture.CreateClient(deviceCookie);
        var state = await device.GetFromJsonAsync<QuickSwitchState>("/api/auth/quick-switch", TestSpa.Json, Ct);
        Assert.Equal("Empfang", state!.Device!.Name);
        Assert.Equal(spa.Location.Name, state.Device.LocationName);
        Assert.Equal(3, state.Device.IdleMinutes);
        // Nur wer eine PIN hat, steht zur Auswahl.
        Assert.Equal([me.Id], state.People.Select(p => p.UserId));

        var wrong = await device.PostAsJsonAsync("/api/auth/quick-switch", new QuickSwitchRequest(me.Id, "0000"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var switched = await device.PostAsJsonAsync("/api/auth/quick-switch", new QuickSwitchRequest(me.Id, "2580"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        var authCookie = switched.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).Single(c => c.StartsWith("aispa.auth=", StringComparison.Ordinal));
        using var asReception = fixture.CreateClient(authCookie);
        Assert.Equal(me.Id, (await asReception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!.Id);

        // Gerät abmelden: danach gilt die PIN dort nicht mehr.
        using var ownerOnDevice = fixture.CreateClient($"{ownerCookie}; {deviceCookie}");
        var revoked = await ownerOnDevice.DeleteAsync("/api/auth/shared-device", Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        var afterRevoke = await device.PostAsJsonAsync("/api/auth/quick-switch", new QuickSwitchRequest(me.Id, "2580"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task Fuenf_Fehlversuche_sperren_die_PIN_und_ein_anderer_Standort_zaehlt_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweigstelle", [DayOfWeek.Monday], Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var elsewhere = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [other.Id]);
        var me = (await reception.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!;
        var away = (await elsewhere.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct))!;
        (await reception.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("4711"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        (await elsewhere.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("4711"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        var registered = await spa.Client.PostAsJsonAsync("/api/auth/shared-device", new SharedDeviceInput("Tablet Empfang", spa.Location.Id), TestSpa.Json, Ct);
        registered.EnsureSuccessStatusCode();
        using var device = fixture.CreateClient(registered.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).Single(c => c.StartsWith("aispa.device=", StringComparison.Ordinal)));

        // Die Person des anderen Standorts steht nicht zur Auswahl und kommt auch mit richtiger PIN nicht hinein.
        var state = await device.GetFromJsonAsync<QuickSwitchState>("/api/auth/quick-switch", TestSpa.Json, Ct);
        Assert.DoesNotContain(state!.People, p => p.UserId == away.Id);
        var foreign = await device.PostAsJsonAsync("/api/auth/quick-switch", new QuickSwitchRequest(away.Id, "4711"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, foreign.StatusCode);

        for (var i = 0; i < 5; i++)
        {
            var wrong = await device.PostAsJsonAsync("/api/auth/quick-switch", new QuickSwitchRequest(me.Id, "9999"), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var locked = await device.PostAsJsonAsync("/api/auth/quick-switch", new QuickSwitchRequest(me.Id, "4711"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        Assert.Contains("Fehlversuche", await locked.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        // Eine neue PIN hebt die Sperre auf.
        (await reception.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("1357"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        var again = await device.PostAsJsonAsync("/api/auth/quick-switch", new QuickSwitchRequest(me.Id, "1357"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        // Inhaber brauchen sechs Ziffern.
        var ownerShort = await spa.Client.PutAsJsonAsync("/api/auth/pin", new QuickSwitchPin("1234"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, ownerShort.StatusCode);
    }
}
