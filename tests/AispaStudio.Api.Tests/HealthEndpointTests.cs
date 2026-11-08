using System.Net;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Health-Endpunkte im Betrieb (<c>Hosting:HealthEndpoints</c>, ADR 0087): ohne den Schalter gibt es sie nur in der Entwicklung; mit ihm
/// melden <c>/health</c> (bereit) und <c>/alive</c> (lebt) in jeder Umgebung nur den Status, ohne Namen und Beschreibungen der Checks.
/// </summary>
public sealed partial class HealthEndpointTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Switch = "Hosting:HealthEndpoints";

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Im_Betrieb_mit_Schalter_meldet_der_Endpunkt_nur_den_Status(string path)
    {
        await using var host = Production(true);
        using var client = host.CreateClient();

        var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Im_Betrieb_ohne_Schalter_gibt_es_den_Endpunkt_nicht(string path)
    {
        await using var host = Production(null);
        using var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, Ct)).StatusCode);
    }

    [Fact]
    public async Task Im_Betrieb_mit_ausgeschaltetem_Schalter_gibt_es_den_Endpunkt_nicht()
    {
        await using var host = Production(false);
        using var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/health", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/alive", Ct)).StatusCode);
    }

    [Fact]
    public async Task Im_Betrieb_ist_die_Instanz_waehrend_des_Starts_nicht_bereit_aber_am_Leben()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        // Über die Dienste (langsam), damit der Zustand „wird eingerichtet“ sicher zu sehen ist.
        await using var host = Production(true).WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:aispadb", connection);
            b.UseSetting("Database:SeedDemoData", "true");
            b.UseSetting("DemoData:Mode", "Services");
            b.UseSetting("DemoData:WeeksBack", "1");
            b.UseSetting("DemoData:WeeksAhead", "1");
        });
        using var client = host.CreateClient();

        var health = await client.GetAsync("/health", Ct);
        var alive = await client.GetAsync("/alive", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Equal("Unhealthy", await health.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        Assert.Equal("Healthy", await alive.Content.ReadAsStringAsync(Ct));
        // Der 503 kommt vom laufenden Einspielen, nicht von einem anderen Fehler.
        Assert.Equal(StartupPhase.Seeding, host.Services.GetRequiredService<StartupState>().Snapshot().Phase);
    }

    [Fact]
    public async Task Der_Worker_meldet_im_Betrieb_mit_Schalter_ebenfalls()
    {
        await using var host = Production(true).WithWebHostBuilder(b => b.UseSetting("Hosting:Role", "Worker"));
        using var client = host.CreateClient();

        var response = await client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task In_der_Entwicklung_bleibt_der_Bericht_ausfuehrlich_und_der_Schalter_unnoetig()
    {
        using var client = fixture.CreateClient();

        var health = await (await client.GetAsync("/health", Ct)).Content.ReadAsStringAsync(Ct);

        Assert.Contains("startup: Healthy", health, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development", "1")]
    [InlineData("Development", "yes")]
    [InlineData("Production", "1")]
    [InlineData("Production", "yes")]
    public async Task Ungueltiger_Schalter_bricht_in_jeder_Umgebung_den_Start_ab(string environment, string value)
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment(environment);
            b.UseSetting(Switch, value);
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.Equal($"Hosting:HealthEndpoints „{value}“ ist ungültig; erlaubt sind true und false.", error.Message);
    }

    [Fact]
    public async Task Einzelplatz_mit_ungueltigem_Schalter_nennt_zuerst_den_ungueltigen_Wert()
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting(Switch, "yes");
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("Hosting:HealthEndpoints „yes“ ist ungültig", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leerer_Schalter_gilt_als_aus_wie_bei_Hosting_ForwardedHeaders()
    {
        // Compose setzt unbelegte Werte als leeren Text (${VAR:-}).
        await using var host = Production(null).WithWebHostBuilder(b => b.UseSetting(Switch, ""));
        using var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/health", Ct)).StatusCode);
    }

    [Fact]
    public async Task Einzelplatz_mit_Hosting_HealthEndpoints_bricht_den_Start_ab()
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting(Switch, "true");
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("Der Einzelplatz hat keine Health-Endpunkte im Betrieb", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_nginx_conf_When_health_und_alive_aufgeloest_werden_Then_antwortet_nginx_selbst_mit_404()
    {
        var config = File.ReadAllText(Path.Combine(SolutionRoot(), "docker", "nginx.conf"));
        var locations = LocationPattern().Matches(config).Select(m => (Modifier: m.Groups[1].Value, Path: m.Groups[2].Value)).ToList();

        foreach (var path in new[] { "/health", "/alive" })
        {
            // Regex-Locations gibt es nicht; die Prüfung kennt nur exakte und Präfix-Locations.
            Assert.DoesNotContain(locations, l => l.Modifier.Contains('~', StringComparison.Ordinal));
            Assert.DoesNotContain(locations, l => l.Modifier == "=" && l.Path == path);
            var prefixes = locations.Where(l => l.Modifier is "" or "^~" && path.StartsWith(l.Path, StringComparison.Ordinal)).ToList();
            // Es trifft nur die Wurzel „/“, und die liefert Dateien oder 404.
            Assert.Equal(["/"], prefixes.Select(l => l.Path));
        }

        var start = config.IndexOf("location / {", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var block = config[start..config.IndexOf('}', start)];
        Assert.Contains("try_files $uri =404;", block, StringComparison.Ordinal);
        Assert.DoesNotContain("proxy_pass", block, StringComparison.Ordinal);
    }

    /// <summary>Produktionsumgebung im Testserver; <paramref name="healthEndpoints"/> <c>null</c> lässt den Schalter weg.</summary>
    private WebApplicationFactory<Program> Production(bool? healthEndpoints) => fixture.Factory.WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Production");
        b.UseSetting("Ai:Local:Url", "");
        if (healthEndpoints is { } on)
        {
            b.UseSetting(Switch, on ? "true" : "false");
        }
    });

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AispaStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    [GeneratedRegex(@"^\s*location\s+(?:(=|\^~|~\*|~)\s+)?(\S+)\s*\{", RegexOptions.Multiline)]
    private static partial Regex LocationPattern();
}
