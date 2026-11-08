using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Weitergeleitete Kopfzeilen hinter Proxy und Ingress (<c>Hosting:ForwardedHeaders</c>, ADR 0087): Client-Adresse und Schema
/// kommen nur von vertrauten Proxys, und der Einzelplatz (Prüfung „nur vom eigenen PC“) lässt sich nie damit täuschen.
/// </summary>
public sealed class ForwardedHeadersTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string On = "Hosting:ForwardedHeaders:Enabled";
    private const string Networks = "Hosting:ForwardedHeaders:KnownNetworks";
    private const string Proxies = "Hosting:ForwardedHeaders:KnownProxies";
    private const string Limit = "Hosting:ForwardedHeaders:ForwardLimit";

    [Fact]
    public async Task Eingeschaltet_gilt_Adresse_und_Schema_vom_vertrauten_Netz()
    {
        await using var host = Host("10.0.0.5", (On, "true"), ($"{Networks}:0", "10.0.0.0/8"));

        var seen = await host.SendAsync("203.0.113.7", "https", Ct);

        Assert.Equal("203.0.113.7|True", $"{seen.Address}|{seen.IsHttps}");
    }

    [Fact]
    public async Task Ausgeschaltet_bleiben_Adresse_und_Schema_wie_sie_sind()
    {
        await using var host = Host("10.0.0.5", ($"{Networks}:0", "10.0.0.0/8"));

        var seen = await host.SendAsync("203.0.113.7", "https", Ct);

        Assert.Equal("10.0.0.5", seen.Address);
        Assert.False(seen.IsHttps);
    }

    [Fact]
    public async Task Eingeschaltet_ignoriert_die_Kopfzeilen_aus_einem_nicht_vertrauten_Netz()
    {
        await using var host = Host("192.0.2.9", (On, "true"), ($"{Networks}:0", "10.0.0.0/8"));

        var seen = await host.SendAsync("127.0.0.1", "https", Ct);

        Assert.Equal("192.0.2.9", seen.Address);
        Assert.False(seen.IsHttps);
    }

    [Fact]
    public async Task Bekannter_Proxy_wird_einzeln_vertraut_und_ein_anderer_nicht()
    {
        await using var trusted = Host("10.0.0.5", (On, "true"), ($"{Proxies}:0", "10.0.0.5"));
        await using var other = Host("10.0.0.6", (On, "true"), ($"{Proxies}:0", "10.0.0.5"));

        Assert.Equal("203.0.113.7", (await trusted.SendAsync("203.0.113.7", "https", Ct)).Address);
        Assert.Equal("10.0.0.6", (await other.SendAsync("203.0.113.7", "https", Ct)).Address);
    }

    [Fact]
    public async Task Ohne_Angaben_vertraut_nur_Loopback_und_nie_jeder()
    {
        await using var lan = Host("10.0.0.5", (On, "true"));
        await using var loopback = Host("127.0.0.1", (On, "true"));

        Assert.Equal("10.0.0.5", (await lan.SendAsync("203.0.113.7", "https", Ct)).Address);
        Assert.Equal("203.0.113.7", (await loopback.SendAsync("203.0.113.7", "https", Ct)).Address);
    }

    [Fact]
    public async Task Eine_Liste_als_kommagetrennter_Text_gilt_wie_mehrere_Eintraege()
    {
        await using var host = Host("172.16.0.4", (On, "true"), (Networks, "10.0.0.0/8, 172.16.0.0/12"));

        Assert.Equal("203.0.113.7", (await host.SendAsync("203.0.113.7", "https", Ct)).Address);
    }

    [Theory]
    [InlineData(1, "10.1.0.4")]
    [InlineData(2, "203.0.113.7")]
    public async Task ForwardLimit_bestimmt_wie_viele_Proxys_in_der_Kette_zaehlen(int limit, string expected)
    {
        await using var host = Host("10.0.0.5", (On, "true"), ($"{Networks}:0", "10.0.0.0/8"), (Limit, limit.ToString(CultureInfo.InvariantCulture)));

        var seen = await host.SendAsync("203.0.113.7, 10.1.0.4", "https", Ct);

        Assert.Equal(expected, seen.Address);
        Assert.True(seen.IsHttps);
    }

    [Fact]
    public async Task Rate_Limit_zaehlt_je_Client_hinter_demselben_Proxy()
    {
        await using var host = Host("10.0.0.5", (On, "true"), ($"{Networks}:0", "10.0.0.0/8"), ("OnlineBooking:PublicRequestsPerMinute", "2"));
        await using var plain = Host("10.0.0.5", ($"{Networks}:0", "10.0.0.0/8"), ("OnlineBooking:PublicRequestsPerMinute", "2"));

        var first = new[] { await host.StatusAsync("203.0.113.7", Ct), await host.StatusAsync("203.0.113.7", Ct), await host.StatusAsync("203.0.113.7", Ct) };
        var second = await host.StatusAsync("203.0.113.8", Ct);
        var withoutSwitch = new[] { await plain.StatusAsync("203.0.113.7", Ct), await plain.StatusAsync("203.0.113.8", Ct), await plain.StatusAsync("203.0.113.9", Ct) };

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], first);
        Assert.Equal(HttpStatusCode.OK, second);
        // Ohne Schalter teilen sich alle Kunden den Topf des Proxys.
        Assert.Equal(HttpStatusCode.TooManyRequests, withoutSwitch[2]);
    }

    [Fact]
    public async Task Gerätecookie_ist_hinter_dem_Proxy_mit_X_Forwarded_Proto_https_Secure()
    {
        await using var on = Host("10.0.0.5", (On, "true"), ($"{Networks}:0", "10.0.0.0/8"));
        await using var off = Host("10.0.0.5", ($"{Networks}:0", "10.0.0.0/8"));
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ownerCookie = await TestUsers.RegisterAsync(fixture, Ct, StaffRole.Owner, [spa.Location.Id]);

        var withSwitch = await RegisterDeviceAsync(on, ownerCookie, spa.Location.Id);
        var withoutSwitch = await RegisterDeviceAsync(off, ownerCookie, spa.Location.Id);

        Assert.Contains("secure", withSwitch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", withoutSwitch, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Einzelplatz_mit_Hosting_ForwardedHeaders_bricht_den_Start_ab()
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting(On, "true");
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("Der Einzelplatz läuft nie hinter einem Proxy", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    public async Task Einzelplatz_mit_dem_ASP_NET_Schluessel_ForwardedHeaders_Enabled_bricht_den_Start_ab(string value)
    {
        // ASP.NET Core vertraut mit diesem Schlüssel (auch als Umgebungsvariable ASPNETCORE_FORWARDEDHEADERS_ENABLED) jedem Absender.
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting("ForwardedHeaders_Enabled", value);
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("Der Einzelplatz läuft nie hinter einem Proxy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Einzelplatz_lehnt_Einrichtung_und_Wiederherstellung_trotz_gefaelschtem_X_Forwarded_For_ab()
    {
        await using var host = Host("192.168.1.50", ("Hosting:Standalone", "true"));

        var setup = await host.PostAsync("/api/auth/standalone/setup", "127.0.0.1", Ct);
        var recover = await host.PostAsync("/api/auth/standalone/recover", "127.0.0.1", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, setup);
        Assert.Equal(HttpStatusCode.Forbidden, recover);
    }

    [Fact]
    public async Task Schalter_und_ASP_NET_Schluessel_zusammen_werten_die_Kopfzeilen_nur_einmal_aus()
    {
        await using var host = Host("10.0.0.5", (On, "true"), ($"{Networks}:0", "10.0.0.0/8"), ("ForwardedHeaders_Enabled", "true"));

        var seen = await host.SendAsync("203.0.113.7, 10.1.0.4", "https", Ct);

        // ForwardLimit 1: nur der rechte Eintrag zählt, ein zweiter Durchlauf käme bis 203.0.113.7.
        Assert.Equal("10.1.0.4", seen.Address);
    }

    [Fact]
    public async Task ASP_NET_Schluessel_vertraut_neben_dem_eigenen_Schalter_nicht_jedem()
    {
        await using var host = Host("192.0.2.9", (On, "true"), ($"{Networks}:0", "10.0.0.0/8"), ("ForwardedHeaders_Enabled", "true"));

        var seen = await host.SendAsync("127.0.0.1", "https", Ct);

        Assert.Equal("192.0.2.9", seen.Address);
        Assert.False(seen.IsHttps);
    }

    [Theory]
    [InlineData(Networks + ":0", "10.0.0.0")]
    [InlineData(Networks + ":0", "10.0.0.0/40")]
    [InlineData(Networks + ":0", "intern")]
    [InlineData(Networks + ":0", "0.0.0.0/0")]
    [InlineData(Networks + ":0", "::/0")]
    [InlineData(Networks + ":0", "10.0.0.0/7")]
    [InlineData(Networks + ":0", "8000::/1")]
    [InlineData(Proxies + ":0", "10.0.0.0/8")]
    [InlineData(Proxies + ":0", "nginx")]
    [InlineData(Limit, "0")]
    [InlineData(Limit, "viele")]
    public async Task Ungueltige_Angabe_bricht_den_Start_ab_und_nennt_den_Wert(string key, string value)
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting(On, "true");
            b.UseSetting(key, value);
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.Contains(value, error.Message, StringComparison.Ordinal);
        Assert.StartsWith("Hosting:ForwardedHeaders", error.Message, StringComparison.Ordinal);
    }

    private static async Task<string> RegisterDeviceAsync(TestHost host, string ownerCookie, Guid locationId)
    {
        using var client = host.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/shared-device") { Content = JsonContent.Create(new SharedDeviceInput("Empfang", locationId, 3), options: TestSpa.Json) };
        request.Headers.Add("Cookie", ownerCookie);
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request, Ct);
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("aispa.device=", StringComparison.Ordinal));
    }

    /// <summary>
    /// Im TestServer ist die Adresse des Absenders null; ein Startfilter setzt sie auf die des „Proxys“, noch vor der Pipeline der API.
    /// Was die API daraus macht, hält er beim Antworten fest, weil die Middleware die Werte am Anfrageobjekt ändert.
    /// </summary>
    private TestHost Host(string proxy, params (string Key, string Value)[] settings)
    {
        var probe = new Probe(IPAddress.Parse(proxy));
        var factory = fixture.Factory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }

            b.ConfigureTestServices(services => services.Insert(0, ServiceDescriptor.Singleton<IStartupFilter>(probe)));
        });
        return new TestHost(factory, probe);
    }

    private sealed class TestHost(WebApplicationFactory<Program> factory, Probe probe) : IAsyncDisposable
    {
        public WebApplicationFactory<Program> Factory => factory;

        public async Task<Seen> SendAsync(string forwardedFor, string proto, CancellationToken ct)
        {
            using var client = factory.CreateClient();
            var id = Guid.NewGuid().ToString("N");
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/modules");
            request.Headers.Add("X-Test-Id", id);
            request.Headers.Add("X-Forwarded-For", forwardedFor);
            request.Headers.Add("X-Forwarded-Proto", proto);
            using var response = await client.SendAsync(request, ct);
            return probe.Seen[id];
        }

        /// <summary>Statuscode eines POST ohne Inhalt, den der Absender mit gefälschtem <c>X-Forwarded-For</c> schickt.</summary>
        public async Task<HttpStatusCode> PostAsync(string path, string forwardedFor, CancellationToken ct)
        {
            using var http = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
            request.Headers.Add("X-Forwarded-For", forwardedFor);
            using var response = await http.SendAsync(request, ct);
            return response.StatusCode;
        }

        /// <summary>Statuscode einer öffentlichen, begrenzten Anfrage von einem Client hinter dem Proxy.</summary>
        public async Task<HttpStatusCode> StatusAsync(string client, CancellationToken ct)
        {
            using var http = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/online/anamnesis/form");
            request.Headers.Add("X-Forwarded-For", client);
            using var response = await http.SendAsync(request, ct);
            return response.StatusCode;
        }

        public ValueTask DisposeAsync() => factory.DisposeAsync();
    }

    private sealed record Seen(string? Address, bool IsHttps);

    private sealed class Probe(IPAddress proxy) : IStartupFilter
    {
        public ConcurrentDictionary<string, Seen> Seen { get; } = new();

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                context.Connection.RemoteIpAddress = proxy;
                if (context.Request.Headers.TryGetValue("X-Test-Id", out var id))
                {
                    context.Response.OnStarting(() =>
                    {
                        Seen[id.ToString()] = new Seen(context.Connection.RemoteIpAddress?.ToString(), context.Request.IsHttps);
                        return Task.CompletedTask;
                    });
                }

                return continuation(context);
            });
            next(app);
        };
    }
}
