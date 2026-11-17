using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Scheitert die Parameterbindung (Pflichtwert fehlt, Enum unbekannt), läuft der Handler nicht; die Antwort braucht trotzdem einen deutschen Satz,
/// den die App anzeigt (<c>HttpApiClient.ErrorAsync</c> liest <c>detail</c>). Antworten mit eigenem Text bleiben unverändert.
/// </summary>
public sealed class BadRequestTextTests(ApiFixture fixture)
{
    private const string Sentence = "Die Anfrage ist unvollständig, oder ein Wert hat das falsche Format.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/api/v1/accounting/export?format=csv&from=2026-11-01&until=2026-11-30")]
    [InlineData("/api/v1/accounting/export?from=2026-11-01&until=2026-11-30")]
    [InlineData("/api/v1/payroll/export?year=2026&month=11")]
    [InlineData("/api/v1/payroll/export?format=DatevLodas&year=zwei&month=11")]
    public async Task Ungueltige_Parameter_ergeben_400_mit_deutschem_Satz(string path)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestSpa.Json, Ct);
        Assert.Equal(Sentence, problem!.Detail);
    }

    [Fact]
    public async Task Eigene_Pruefungen_behalten_ihren_Text_ohne_den_allgemeinen_Satz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.GetAsync("/api/v1/accounting/export?format=Datev&from=2026-01-01&until=2027-06-30", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct);
        Assert.Equal(["Zeitraum von höchstens einem Jahr, Ende nach Beginn."], problem!.Errors["until"]);
        Assert.Null(problem.Detail);
    }

    [Fact]
    public async Task Andere_Fehler_bekommen_den_Satz_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.GetAsync($"/api/v1/rooms/{Guid.NewGuid()}/deletion-check", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(Sentence, text, StringComparison.Ordinal);
    }
}
