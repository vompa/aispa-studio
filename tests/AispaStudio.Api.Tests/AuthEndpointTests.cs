using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class AuthEndpointTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Me_ohne_Anmeldung_liefert_401()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Passkey_Anmeldeoptionen_enthalten_Challenge()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsync("/api/auth/passkey/request-options", null, Ct);

        response.EnsureSuccessStatusCode();
        using var options = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.False(string.IsNullOrEmpty(options.RootElement.GetProperty("challenge").GetString()));
    }

    [Fact]
    public async Task Angemeldete_Person_bekommt_Optionen_fuer_neuen_Passkey()
    {
        var cookie = await TestUsers.RegisterAsync(fixture, Ct);
        using var client = fixture.CreateClient(cookie);

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me", TestSpa.Json, Ct);
        var response = await client.PostAsync("/api/auth/passkey/creation-options", null, Ct);

        Assert.NotNull(me);
        Assert.Equal(0, me.PasskeyCount);
        response.EnsureSuccessStatusCode();
        using var options = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Test Person", options.RootElement.GetProperty("user").GetProperty("displayName").GetString());
        Assert.False(string.IsNullOrEmpty(options.RootElement.GetProperty("challenge").GetString()));
    }

    [Fact]
    public async Task Anmeldung_ohne_vorherige_Optionen_wird_abgelehnt()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/passkey/login", new PasskeyCredential("{}"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ungueltiger_Passkey_wird_bei_Anmeldung_abgelehnt()
    {
        using var optionsClient = fixture.CreateClient();
        var options = await optionsClient.PostAsync("/api/auth/passkey/request-options", null, Ct);
        var state = string.Join("; ", options.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        using var client = fixture.CreateClient(state);

        var response = await client.PostAsJsonAsync("/api/auth/passkey/login", new PasskeyCredential("{}"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
