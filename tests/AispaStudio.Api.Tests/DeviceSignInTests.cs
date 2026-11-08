using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Anmeldung der Windows-App mit einem Einmal-Code aus dem Browser.</summary>
public sealed class DeviceSignInTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Code_meldet_die_App_als_dieselbe_Person_an_und_gilt_nur_einmal()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var code = await TestSpa.ReadAsync<DeviceCode>(await reception.PostAsync("/api/auth/device-code", null, Ct), Ct);
        using var app = fixture.CreateClient();

        // Kleinbuchstaben und ohne Bindestrich, wie beim Abtippen.
        var redeemed = await app.PostAsJsonAsync("/api/auth/device-code/redeem",
            new DeviceCodeRedeem(code.Code.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant()), TestSpa.Json, Ct);
        var user = await TestSpa.ReadAsync<CurrentUser>(redeemed, Ct);
        using var signedIn = fixture.CreateClient(string.Join("; ", redeemed.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        var again = await app.PostAsJsonAsync("/api/auth/device-code/redeem", new DeviceCodeRedeem(code.Code), TestSpa.Json, Ct);

        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", code.Code);
        Assert.Equal(StaffRole.Reception, user.Access?.Role);
        Assert.Contains(redeemed.Headers.GetValues("Set-Cookie"), c => c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await signedIn.GetAsync($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [Fact]
    public async Task Falscher_Code_und_Code_ohne_Anmeldung_werden_abgelehnt()
    {
        using var anonymous = fixture.CreateClient();

        var wrong = await anonymous.PostAsJsonAsync("/api/auth/device-code/redeem", new DeviceCodeRedeem("ABCD-EFGH"), TestSpa.Json, Ct);
        var create = await anonymous.PostAsync("/api/auth/device-code", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }
}
