using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

internal static class TestUsers
{
    /// <summary>
    /// Legt über den Entwicklungs-Endpunkt ein Konto an und liefert das Anmelde-Cookie.
    /// Ohne Angaben Inhaber für alle Standorte eines neuen Mandanten (jeder Test hat so sein eigenes Spa),
    /// sonst die gewünschte Rolle an den genannten Standorten in deren Mandanten.
    /// </summary>
    public static async Task<string> RegisterAsync(ApiFixture fixture, CancellationToken ct,
        StaffRole role = StaffRole.Owner, Guid[]? locations = null, Guid? employee = null)
    {
        using var client = fixture.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@aispa.test";
        var response = await client.PostAsJsonAsync("/api/auth/dev/register",
            new DevRegistration(email, "Test Person", role, locations, employee, NewTenant: locations is null), TestSpa.Json, ct);
        response.EnsureSuccessStatusCode();

        var cookies = response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]);
        return string.Join("; ", cookies);
    }

    public static async Task<HttpClient> ClientAsync(ApiFixture fixture, CancellationToken ct,
        StaffRole role = StaffRole.Owner, Guid[]? locations = null, Guid? employee = null) =>
        fixture.CreateClient(await RegisterAsync(fixture, ct, role, locations, employee));
}
