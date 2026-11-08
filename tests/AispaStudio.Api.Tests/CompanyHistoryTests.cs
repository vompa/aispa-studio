using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>R05 Slice 93 (Ä191, ADR 0082): Verlauf der Firmendaten für die native Maske in der Windows-App.</summary>
public sealed class CompanyHistoryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_Inhaberin_aendert_Name_und_Website_When_Verlauf_der_Firmendaten_Then_alt_neu_Person_und_Zeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var tenant = await spa.GetAsync<TenantDto>("/api/tenant", Ct);
        var company = tenant.Company ?? CompanyProfile.Empty;
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        _ = await spa.PutAsync<TenantDto>("/api/tenant",
            new TenantInput("Day Spa am Ring", tenant.Country, company with { Website = "https://spa-am-ring.at" }), Ct);

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/tenant/history", Ct);
        Assert.Equal($"Geändert von Test Person: Name {tenant.Name} → Day Spa am Ring; Website {company.Website ?? "–"} → https://spa-am-ring.at", history[0].Text);
        Assert.InRange(history[0].At, before, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Given_nur_die_Einbettung_geaendert_When_Verlauf_der_Firmendaten_Then_kein_neuer_Eintrag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var tenant = await spa.GetAsync<TenantDto>("/api/tenant", Ct);
        _ = await spa.PutAsync<TenantDto>("/api/tenant", new TenantInput("Day Spa am Ring", tenant.Country, tenant.Company), Ct);
        var counted = (await spa.GetAsync<List<HistoryEntry>>("/api/tenant/history", Ct)).Count;

        var changed = await spa.Client.PutAsJsonAsync("/api/tenant/embedding", new EmbeddingSettings(["https://www.spa-am-ring.at"]), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/tenant/history", Ct);
        Assert.Equal(counted, history.Count);
        Assert.StartsWith("Geändert von Test Person: Name", history[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_Rezeption_ohne_ManageOrganization_When_Verlauf_der_Firmendaten_Then_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = fixture.CreateClient(spa.Cookie);
        var email = $"rita-{Guid.NewGuid():N}@aispa.test";
        var registered = await client.PostAsJsonAsync("/api/auth/dev/register", new DevRegistration(email, "Rita", StaffRole.Reception, [spa.Location.Id]), TestSpa.Json, Ct);
        registered.EnsureSuccessStatusCode();
        using var rita = fixture.CreateClient(string.Join("; ", registered.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));

        Assert.Equal(HttpStatusCode.Forbidden, (await rita.GetAsync("/api/tenant/history", Ct)).StatusCode);
    }
}
