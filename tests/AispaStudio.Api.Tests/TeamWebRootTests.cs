using System.Net;
using AispaStudio.Api.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AispaStudio.Api.Tests;

/// <summary>Web-Apps (Anmelden, Hilfe, Mein Tag …) direkt aus der API im Einzelplatz-Betrieb (ADR 0041).</summary>
public sealed class TeamWebRootTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DirectoryInfo CreateWebRoot(bool withRootPage)
    {
        var root = Directory.CreateTempSubdirectory("aispa-web-");
        if (withRootPage)
        {
            // Rest einer früheren Auslieferung: darf nie mehr geliefert werden.
            File.WriteAllText(Path.Combine(root.FullName, "index.html"), "<p>Team</p>");
        }

        foreach (var app in new[] { "anmelden", "buchen", "mein-tag", "help", "dashboard", "betreiber" })
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, app));
            File.WriteAllText(Path.Combine(root.FullName, app, "index.html"), $"<p>App {app}</p>");
        }

        Directory.CreateDirectory(Path.Combine(root.FullName, "assets"));
        File.WriteAllText(Path.Combine(root.FullName, "assets", "app.js"), "console.log(1)");
        return root;
    }

    [Fact]
    public async Task Hosting_WebRoot_liefert_Apps_aus_ohne_die_API_zu_verdecken()
    {
        var root = CreateWebRoot(withRootPage: false);
        try
        {
            await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:WebRoot", root.FullName));
            using var client = host.CreateClient();

            Assert.Contains("App buchen", await client.GetStringAsync("/buchen/spa-am-see", Ct));
            Assert.Equal("console.log(1)", await client.GetStringAsync("/assets/app.js", Ct));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/assets/fehlt.js", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/gibt-es-nicht", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/system/status", Ct)).StatusCode);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Anmelden_und_die_Apps_werden_ausgeliefert()
    {
        var root = CreateWebRoot(withRootPage: false);
        try
        {
            await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:WebRoot", root.FullName));
            using var client = host.CreateClient();

            foreach (var app in new[] { "anmelden", "buchen", "mein-tag", "help", "dashboard", "betreiber" })
            {
                Assert.Contains($"App {app}", await client.GetStringAsync($"/{app}/", Ct));
                Assert.Contains($"App {app}", await client.GetStringAsync($"/{app}/unterseite", Ct));
            }
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Wurzel_leitet_nach_anmelden_weiter()
    {
        var root = CreateWebRoot(withRootPage: true);
        try
        {
            await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:WebRoot", root.FullName));
            using var client = host.CreateClient(new() { AllowAutoRedirect = false });

            using var response = await client.GetAsync("/", Ct);
            using var invited = await client.GetAsync("/?einladung=tok-1", Ct);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/anmelden/", response.Headers.Location?.OriginalString);
            Assert.Equal(HttpStatusCode.Redirect, invited.StatusCode);
            Assert.Equal("/anmelden/?einladung=tok-1", invited.Headers.Location?.OriginalString);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Einzelplatz_Anfrage_auf_team_antwortet_404()
    {
        var root = CreateWebRoot(withRootPage: true);
        try
        {
            await using var host = fixture.Factory.WithWebHostBuilder(b =>
            {
                b.UseSetting("Hosting:Standalone", "true");
                b.UseSetting("Hosting:WebRoot", root.FullName);
            });
            using var client = host.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/team", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/team/kalender", Ct)).StatusCode);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Einzelplatz_Anfrage_auf_einen_unbekannten_Pfad_antwortet_404()
    {
        var root = CreateWebRoot(withRootPage: true);
        try
        {
            await using var host = fixture.Factory.WithWebHostBuilder(b =>
            {
                b.UseSetting("Hosting:Standalone", "true");
                b.UseSetting("Hosting:WebRoot", root.FullName);
            });
            using var client = host.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/kalender/heute", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/einstellungen", Ct)).StatusCode);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Given_eine_Anfrage_an_buchen_When_die_Antwort_geprueft_wird_Then_traegt_sie_CSP_Report_Only_mit_default_src_self()
    {
        var root = CreateWebRoot(withRootPage: false);
        try
        {
            await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:WebRoot", root.FullName));
            using var client = host.CreateClient();

            foreach (var path in new[] { "/buchen/", "/buchen/spa-am-see" })
            {
                using var response = await client.GetAsync(path, Ct);
                Assert.Contains("App buchen", await response.Content.ReadAsStringAsync(Ct));
                Assert.True(response.Headers.TryGetValues("Content-Security-Policy-Report-Only", out var values), path);
                Assert.Contains("default-src 'self'", Assert.Single(values));
                // Der durchsetzende Header trägt nur frame-ancestors (BookingEmbeddingTests); die Richtlinie für Inhalte bleibt Report-Only.
                Assert.Equal("frame-ancestors 'self'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
            }

            // Die Anmeldeseite bekommt in dieser Slice keine Richtlinie.
            using var signIn = await client.GetAsync("/anmelden/", Ct);
            Assert.False(signIn.Headers.Contains("Content-Security-Policy-Report-Only"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Given_nginx_conf_When_location_buchen_gelesen_wird_Then_setzt_sie_dieselbe_Richtlinie_wie_die_API()
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

        Assert.Contains(
            $"add_header {BookingContentSecurityPolicy.HeaderName} \"{BookingContentSecurityPolicy.Value}\" always;",
            block,
            StringComparison.Ordinal);

        // Slice 103: die Wurzel leitet wie die API mit Abfrage (Einladungslink) zur Anmeldeseite weiter.
        Assert.Contains("return 302 /anmelden/$is_args$args;", config, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stopp_Datei_beendet_die_API_geordnet_und_wird_entfernt()
    {
        var folder = Directory.CreateTempSubdirectory("aispa-stopp-");
        try
        {
            var stopFile = Path.Combine(folder.FullName, "api.stop");
            // Rest eines früheren Laufs: wird beim Start entfernt, beendet nichts.
            File.WriteAllText(stopFile, "");
            await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("Hosting:StopFile", stopFile));
            using var client = host.CreateClient();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            await WaitAsync(() => !File.Exists(stopFile));
            await Task.Delay(StopFileWatcher.Interval * 3, Ct);
            Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);

            File.WriteAllText(stopFile, "");
            await WaitAsync(() => lifetime.ApplicationStopping.IsCancellationRequested);
            Assert.False(File.Exists(stopFile));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(100, Ct);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Ohne_WebRoot_liefert_die_API_keine_Oberflaeche()
    {
        using var client = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/kalender", Ct)).StatusCode);
    }
}
