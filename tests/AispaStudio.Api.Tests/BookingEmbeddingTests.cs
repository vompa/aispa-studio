using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AispaStudio.Api.Tests;

/// <summary>Kundenbereich auf der eigenen Website einbetten (ADR 0080, Nachtrag): Liste je Spa, <c>frame-ancestors</c> von <c>/buchen/</c>.</summary>
public sealed class BookingEmbeddingTests(ApiFixture fixture)
{
    private const string Policy = "Content-Security-Policy";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string?> FrameAncestorsAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Contains("Buchen", await response.Content.ReadAsStringAsync(Ct));
        return response.Headers.TryGetValues(Policy, out var values) ? Assert.Single(values) : null;
    }

    private (WebApplicationFactory<Program> Host, DirectoryInfo Root) HostWithWebRoot()
    {
        var root = Directory.CreateTempSubdirectory("aispa-web-");
        Directory.CreateDirectory(Path.Combine(root.FullName, "help"));
        File.WriteAllText(Path.Combine(root.FullName, "help", "index.html"), "<p>Hilfe</p>");
        Directory.CreateDirectory(Path.Combine(root.FullName, "buchen"));
        File.WriteAllText(Path.Combine(root.FullName, "buchen", "index.html"), "<p>Buchen</p>");
        return (fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:WebRoot", root.FullName)), root);
    }

    [Fact]
    public async Task Given_keine_eingetragene_Website_When_buchen_abgerufen_wird_Then_enthaelt_die_Policy_frame_ancestors_self()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var (host, root) = HostWithWebRoot();
        try
        {
            await using var _ = host;
            using var client = host.CreateClient();

            Assert.Equal("frame-ancestors 'self'", await FrameAncestorsAsync(client, $"/buchen/?spa={spa.TenantId}"));
            Assert.Equal("frame-ancestors 'self'", await FrameAncestorsAsync(client, "/buchen/spa-am-see?spa=gibt-es-nicht"));
            // Auch die Datei selbst darf nicht ohne Richtlinie ausgeliefert werden.
            Assert.Equal("frame-ancestors 'self'", await FrameAncestorsAsync(client, $"/buchen/index.html?spa={spa.TenantId}"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Given_Spa_When_Einbetten_gelesen_Then_BookingUrl_mit_spa_Kennung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var read = await spa.GetAsync<EmbeddingSettings>("/api/tenant/embedding", Ct);
        var written = await spa.PutAsync<EmbeddingSettings>("/api/tenant/embedding", new EmbeddingSettings(["https://www.beispiel-spa.at"], "https://fremd.example/"), Ct);

        // Ohne eigene Einstellung gilt die Standardadresse des Testservers (OnlineBooking:PublicUrl); jedes Spa bekommt seine eigene Kennung.
        Assert.Equal($"http://localhost:5173/buchen/?spa={spa.TenantId}", read.BookingUrl);
        Assert.Equal(read.BookingUrl, written.BookingUrl);
        Assert.Equal($"http://localhost:5173/buchen/?spa={other.TenantId}", (await other.GetAsync<EmbeddingSettings>("/api/tenant/embedding", Ct)).BookingUrl);
    }

    [Fact]
    public async Task Given_eingetragen_beispiel_spa_When_buchen_abgerufen_wird_Then_enthaelt_die_Policy_frame_ancestors_self_und_die_Website()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<EmbeddingSettings>("/api/tenant/embedding", new EmbeddingSettings(["https://www.beispiel-spa.at"]), Ct);
        var (host, root) = HostWithWebRoot();
        try
        {
            await using var _ = host;
            using var client = host.CreateClient();

            Assert.Equal("frame-ancestors 'self' https://www.beispiel-spa.at", await FrameAncestorsAsync(client, $"/buchen/?spa={spa.TenantId}"));
            Assert.Equal("frame-ancestors 'self' https://www.beispiel-spa.at", await FrameAncestorsAsync(client, $"/buchen/spa-am-see?spa={spa.TenantId}"));
            // Die Liste eines Spas gilt nie für ein anderes.
            Assert.Equal("frame-ancestors 'self'", await FrameAncestorsAsync(client, $"/buchen/?spa={other.TenantId}"));

            // Der Endpunkt, den nginx per auth_request fragt, liefert denselben Wert.
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/online/embedding-policy");
            request.Headers.Add("X-Original-URI", $"/buchen/spa-am-see?spa={spa.TenantId}");
            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("'self' https://www.beispiel-spa.at", Assert.Single(response.Headers.GetValues("X-Frame-Ancestors")));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("http://www.beispiel-spa.at", "https://")]
    [InlineData("https://www.beispiel-spa.at/buchen", "Pfad")]
    [InlineData("https://www.beispiel-spa.at?x=1", "Pfad")]
    [InlineData("https://*.beispiel-spa.at", "Platzhalter")]
    [InlineData("https://nutzer:geheim@www.beispiel-spa.at", "Zugangsdaten")]
    [InlineData("www.beispiel-spa.at", "https://")]
    [InlineData("https://wellness-höhe.at", "Punycode")]
    [InlineData("https://www.beispiel-spa.at; script-src *", "Adresse")]
    public async Task Given_eine_ungueltige_Adresse_When_gespeichert_wird_Then_nennt_ein_Fehler_am_Feld_den_Grund_und_nichts_wird_gespeichert(
        string input, string reason)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<EmbeddingSettings>("/api/tenant/embedding", new EmbeddingSettings(["https://alt.beispiel-spa.at"]), Ct);

        var response = await spa.Client.PutAsJsonAsync("/api/tenant/embedding", new EmbeddingSettings(["https://neu.beispiel-spa.at", input]), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct);
        var message = Assert.Single(problem!.Errors["allowedOrigins"]);
        Assert.Contains(reason, message);
        Assert.Contains(input, message);
        Assert.Equal(["https://alt.beispiel-spa.at"], (await spa.GetAsync<EmbeddingSettings>("/api/tenant/embedding", Ct)).AllowedOrigins);
    }

    [Fact]
    public async Task Adressen_werden_vereinheitlicht_und_nicht_doppelt_gespeichert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var saved = await spa.PutAsync<EmbeddingSettings>("/api/tenant/embedding", new EmbeddingSettings(
            ["  HTTPS://WWW.Beispiel-Spa.at/ ", "https://www.beispiel-spa.at", "https://beispiel-spa.at:08443", ""]), Ct);

        Assert.Equal(["https://www.beispiel-spa.at", "https://beispiel-spa.at:8443"], saved.AllowedOrigins);
        Assert.Equal(saved.AllowedOrigins, (await spa.GetAsync<EmbeddingSettings>("/api/tenant/embedding", Ct)).AllowedOrigins);
    }

    [Fact]
    public async Task Mehr_als_zehn_Adressen_werden_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PutAsJsonAsync("/api/tenant/embedding",
            new EmbeddingSettings([.. Enumerable.Range(1, 11).Select(i => $"https://spa{i}.beispiel.at")]), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("allowedOrigins", (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!.Errors.Keys);
    }

    [Fact]
    public async Task Die_Liste_pflegt_nur_wer_die_Organisation_verwalten_darf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var put = await reception.PutAsJsonAsync("/api/tenant/embedding", new EmbeddingSettings(["https://www.beispiel-spa.at"]), TestSpa.Json, Ct);
        var get = await reception.GetAsync("/api/tenant/embedding", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
    }

    [Fact]
    public void Given_nginx_conf_When_location_buchen_gelesen_wird_Then_fragt_sie_die_API_nach_frame_ancestors()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var config = File.ReadAllText(Path.Combine(directory.FullName, "docker", "nginx.conf"));
        var start = config.IndexOf("location /buchen/ {", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var block = config[start..config.IndexOf('}', start)];

        Assert.Contains("auth_request /embedding-policy;", block, StringComparison.Ordinal);
        Assert.Contains("auth_request_set $frame_ancestors $upstream_http_x_frame_ancestors;", block, StringComparison.Ordinal);
        Assert.Contains("add_header Content-Security-Policy \"frame-ancestors $frame_ancestors\" always;", block, StringComparison.Ordinal);
        Assert.Contains("location = /embedding-policy {", config, StringComparison.Ordinal);
        Assert.Contains("proxy_pass http://api:8080/api/online/embedding-policy;", config, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header X-Original-URI $request_uri;", config, StringComparison.Ordinal);
    }
}
