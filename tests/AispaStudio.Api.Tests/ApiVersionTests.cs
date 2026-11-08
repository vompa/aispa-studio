using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Pfadversion der Team-API (Slice 121, ADR 0085): <c>/api/v1/…</c> und der alte Pfad <c>/api/…</c> treffen denselben Endpunkt mit denselben Regeln;
/// Kundenbereich, Webhooks und Betreiber bleiben ohne Version.
/// </summary>
public sealed partial class ApiVersionTests(ApiFixture fixture)
{
    private static readonly Guid SomeId = Guid.Parse("6f1c2a4e-9b3d-4c7a-8e21-5d0f3b9a7c11");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ohne_Recht_lehnen_v1_und_alter_Pfad_gleich_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var versioned = await reception.GetAsync("/api/v1/payroll/settings", Ct);
        var old = await reception.GetAsync("/api/payroll/settings", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, versioned.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, old.StatusCode);
        // Die traceId unterscheidet sich je Anfrage; Titel und Text der Ablehnung sind gleich.
        var oldProblem = await old.Content.ReadFromJsonAsync<ProblemText>(Ct);
        var versionedProblem = await versioned.Content.ReadFromJsonAsync<ProblemText>(Ct);
        Assert.Equal(oldProblem, versionedProblem);
        Assert.False(string.IsNullOrEmpty(oldProblem?.Detail));
    }

    [Fact]
    public async Task Jeder_Endpunkt_mit_Anmeldung_lehnt_unter_v1_wie_unter_dem_alten_Pfad_ab()
    {
        var endpoints = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } raw && raw.StartsWith("/api/", StringComparison.Ordinal)
                && !raw.StartsWith("/api/online", StringComparison.Ordinal) && !raw.StartsWith("/api/hooks", StringComparison.Ordinal)
                && !raw.StartsWith("/api/operator", StringComparison.Ordinal)
                && e.Metadata.GetMetadata<IAuthorizeData>() is not null && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"]).Select(m => (Method: m, Path: Filled(e.RoutePattern.RawText!),
                ContentType: e.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes is [var accepted, ..] ? accepted : "application/json")))
            .Distinct()
            .ToList();
        using var anonymous = fixture.CreateClient();
        var differences = new List<string>();

        foreach (var (method, path, contentType) in endpoints)
        {
            var old = await SendAsync(anonymous, method, path, contentType);
            var versioned = await SendAsync(anonymous, method, "/api/v1" + path["/api".Length..], contentType);
            // Ohne Anmeldung muss der alte Pfad 401 sein; sonst traf der gefüllte Pfad keinen Endpunkt und der Vergleich sagte nichts.
            if (old != HttpStatusCode.Unauthorized || old != versioned)
            {
                differences.Add($"{method} {path}: alt {(int)old}, v1 {(int)versioned}");
            }
        }

        Assert.True(endpoints.Count > 300, $"Nur {endpoints.Count} Endpunkte gefunden.");
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    [Fact]
    public async Task Anzeige_erreicht_unter_v1_nur_ihre_Endpunkte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var display = await ChannelTests.PairDisplayAsync(spa, Ct);

        var session = await display.GetAsync("/api/v1/display/session", Ct);
        var customers = await display.GetAsync("/api/v1/customers", Ct);

        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customers.StatusCode);
        Assert.Contains("Über diesen Zugang nicht freigegeben.", await customers.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alter_Pfad_liefert_denselben_Termin_wie_v1()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var day = $"locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}";

        var versioned = await spa.Client.GetAsync($"/api/v1/{day}", Ct);
        var old = await spa.Client.GetAsync($"/api/{day}", Ct);

        Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
        var json = await versioned.Content.ReadAsStringAsync(Ct);
        Assert.Contains(appointment.Id.ToString(), json, StringComparison.Ordinal);
        Assert.Equal(await old.Content.ReadAsStringAsync(Ct), json);
    }

    [Theory]
    [InlineData("GET", "/api/v1/online/locations")]
    [InlineData("POST", "/api/v1/hooks/payments/stripe")]
    [InlineData("GET", "/api/v1/operator/spas")]
    public async Task Kundenbereich_Webhooks_und_Betreiber_haben_keine_v1(string method, string path)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var online = spa.OnlineClient();

        var response = await SendAsync(online, method, path);

        Assert.Equal(HttpStatusCode.NotFound, response);
    }

    [Fact]
    public async Task Kundenbereich_antwortet_unter_dem_bisherigen_Pfad()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var online = spa.OnlineClient();

        var response = await online.GetAsync("/api/online/locations", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Das_Dokument_nennt_Team_Pfade_mit_v1_und_den_Kundenbereich_ohne()
    {
        var provider = fixture.Factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");

        var document = await provider.GetOpenApiDocumentAsync(Ct);
        var paths = document.Paths.Keys.ToList();

        Assert.Contains("/api/v1/locations/{locationId}/calendar", paths);
        Assert.Contains("/api/v1/payroll/settings", paths);
        Assert.Contains("/api/online/locations", paths);
        Assert.Contains(paths, p => p.StartsWith("/api/operator/", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, p => p.StartsWith("/api/", StringComparison.Ordinal) && !p.StartsWith("/api/v1/", StringComparison.Ordinal)
            && !p.StartsWith("/api/online", StringComparison.Ordinal) && !p.StartsWith("/api/hooks", StringComparison.Ordinal)
            && !p.StartsWith("/api/operator", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, p => p.StartsWith("/api/v1/online", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mandantensperre_gilt_unter_v1()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var operatorClient = fixture.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-Aispa-Operator-Key", ApiFixture.OperatorKey);
        var locked = await operatorClient.PostAsJsonAsync($"/api/operator/spas/{spa.TenantId}/lock", new OperatorLockInput("Vertrag beendet."), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, locked.StatusCode);

        var write = await spa.Client.PostAsJsonAsync("/api/v1/customers", new { firstName = "Eva", lastName = "Sommer" }, TestSpa.Json, Ct);
        var old = await spa.Client.PostAsJsonAsync("/api/customers", new { firstName = "Eva", lastName = "Sommer" }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, old.StatusCode);
        Assert.Contains("Vertrag beendet.", await write.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(await old.Content.ReadFromJsonAsync<ProblemText>(Ct), await write.Content.ReadFromJsonAsync<ProblemText>(Ct));
    }

    private sealed record ProblemText(string? Title, string? Detail);

    /// <summary>Ein leerer Körper im Inhaltstyp, den der Endpunkt annimmt; sonst antwortet das Routing schon vor der Anmeldung mit 415.</summary>
    private static async Task<HttpStatusCode> SendAsync(HttpClient client, string method, string path, string contentType = "application/json")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent(contentType == "application/json" ? "{}" : "", null, contentType);
        }

        using var response = await client.SendAsync(request, Ct);
        return response.StatusCode;
    }

    /// <summary>Füllt Routenparameter mit festen Werten: Guid, Zahl, Datum oder Text je nach Einschränkung.</summary>
    private static string Filled(string raw) => Parameter().Replace(raw, m => m.Groups["constraint"].Value switch
    {
        var c when c.Contains("guid", StringComparison.Ordinal) => SomeId.ToString(),
        var c when c.Contains("int", StringComparison.Ordinal) || c.Contains("long", StringComparison.Ordinal) => "1",
        var c when c.Contains("datetime", StringComparison.Ordinal) => "2027-03-01",
        _ => "x",
    });

    [GeneratedRegex(@"\{\*{0,2}[A-Za-z]+(?<constraint>(:[^}]+)?)\??\}")]
    private static partial Regex Parameter();
}
