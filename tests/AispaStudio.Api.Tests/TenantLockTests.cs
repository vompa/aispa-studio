using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Der Betreiber sperrt ein Spa: Lesen und Exportieren geht weiter, Ändern und neue Online-Buchungen nicht.</summary>
public sealed class TenantLockTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gesperrtes_Spa_sieht_alles_aendert_aber_nichts_und_nimmt_online_nichts_Neues_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var operatorClient = fixture.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-Aispa-Operator-Key", ApiFixture.OperatorKey);
        var lockPath = $"/api/operator/spas/{spa.TenantId}/lock";

        Assert.Equal(HttpStatusCode.BadRequest, (await operatorClient.PostAsJsonAsync(lockPath, new OperatorLockInput(" "), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await operatorClient.PostAsJsonAsync($"/api/operator/spas/{Guid.NewGuid()}/lock", new OperatorLockInput("Test"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await spa.Client.PostAsJsonAsync(lockPath, new OperatorLockInput("Selbst"), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await operatorClient.PostAsJsonAsync(lockPath, new OperatorLockInput("Vertrag beendet."), TestSpa.Json, Ct)).StatusCode);

        var access = (await spa.GetAsync<CurrentUser>("/api/auth/me", Ct)).Access!;
        Assert.NotNull(access.LockedAt);
        Assert.Equal("Vertrag beendet.", access.LockReason);
        var listed = Assert.Single(await operatorClient.GetFromJsonAsync<List<OperatorSpaDto>>("/api/operator/spas", TestSpa.Json, Ct) ?? [],
            s => s.Id == spa.TenantId);
        Assert.Equal("Vertrag beendet.", listed.LockReason);

        // Lesen geht, Ändern nicht; der Grund steht in der Antwort.
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.GetAsync("/api/customers", Ct)).StatusCode);
        var write = await spa.Client.PostAsJsonAsync("/api/customers", new { firstName = "Eva", lastName = "Sommer" }, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Contains("Vertrag beendet.", await write.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        // Online: Standorte ansehen ja, neu buchen nein.
        using var online = spa.OnlineClient();
        Assert.Equal(HttpStatusCode.OK, (await online.GetAsync("/api/online/locations", Ct)).StatusCode);
        var booking = await online.PostAsJsonAsync("/api/online/bookings", new { }, TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, booking.StatusCode);
        Assert.Contains("keine Online-Buchungen", await booking.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent, (await operatorClient.DeleteAsync(lockPath, Ct)).StatusCode);
        Assert.Null((await spa.GetAsync<CurrentUser>("/api/auth/me", Ct)).Access!.LockedAt);
        Assert.NotEqual(HttpStatusCode.Forbidden,
            (await spa.Client.PostAsJsonAsync("/api/customers", new { firstName = "Eva", lastName = "Sommer" }, TestSpa.Json, Ct)).StatusCode);
    }
}
