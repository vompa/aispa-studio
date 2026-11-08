using System.Net;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Verwalten von zu Hause (Runde R05, Slice 118: Ä196): die Windows-App verbindet sich ohne Umgebungsvariable mit einem Server.</summary>
public sealed class ServerConnectTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("spa.example.at", "https://spa.example.at/")]
    [InlineData(" https://Spa.Example.at ", "https://spa.example.at/")]
    [InlineData("https://spa.example.at:8443/", "https://spa.example.at:8443/")]
    [InlineData("http://spa.example.at", null)]
    [InlineData("", null)]
    [InlineData("kein Name", null)]
    public void Adresse_bekommt_https_und_den_Standardport_des_Servers(string input, string? expected) =>
        Assert.Equal(expected, ServerChoice.Normalize(input)?.ToString());

    [Fact]
    public async Task Erreichbarer_Server_wird_geprueft_gemerkt_und_die_App_startet_neu()
    {
        var settings = new MemorySettingsStore();
        var restarts = 0;
        var probed = new List<Uri>();
        var connect = new ServerConnectViewModel(settings, () => restarts++, (address, _) =>
        {
            probed.Add(address);
            return Task.FromResult<string?>(null);
        });
        Assert.False(connect.ConnectCommand.CanExecute(null));

        connect.Address = "spa.example.at";
        await connect.ConnectCommand.ExecuteAsync(null);

        Assert.Null(connect.Error);
        Assert.Equal([new Uri("https://spa.example.at/")], probed);
        Assert.Equal(("https://spa.example.at/", null), (settings.Load().ServerAddress, settings.Load().ServerCode));
        Assert.Equal(1, restarts);
    }

    [Fact]
    public async Task Nicht_erreichbarer_Server_meldet_am_Feld_und_merkt_nichts()
    {
        var settings = new MemorySettingsStore();
        var restarts = 0;
        var connect = new ServerConnectViewModel(settings, () => restarts++,
            (address, _) => Task.FromResult<string?>($"{address.Host} ist nicht erreichbar."))
        {
            Address = "spa.example.at",
        };

        await connect.ConnectCommand.ExecuteAsync(null);

        Assert.Equal("spa.example.at ist nicht erreichbar.", connect.Error);
        Assert.Equal((null, null, 0), (settings.Load().ServerAddress, settings.Load().ServerCode, restarts));
    }

    [Fact]
    public async Task Adresse_ohne_https_wird_abgelehnt_und_nicht_geprueft()
    {
        var settings = new MemorySettingsStore();
        var probed = 0;
        var connect = new ServerConnectViewModel(settings, () => { }, (_, _) =>
        {
            probed++;
            return Task.FromResult<string?>(null);
        })
        {
            Address = "http://spa.example.at",
        };

        await connect.ConnectCommand.ExecuteAsync(null);

        Assert.Contains("https://", connect.Error, StringComparison.Ordinal);
        Assert.Equal((0, null), (probed, settings.Load().ServerAddress));
    }

    [Fact]
    public async Task Echte_Pruefung_antwortet_ohne_Fehler_wenn_der_Server_den_Status_liefert()
    {
        using var handler = new FixedHandler(HttpStatusCode.OK, """{"version":"1.0","environment":"Production","serverTime":"2026-10-29T10:00:00+00:00"}""");

        Assert.Null(await ServerConnectViewModel.ProbeAsync(new Uri("https://spa.example.at/"), Ct, handler));
    }

    [Fact]
    public async Task Echte_Pruefung_meldet_Nichterreichbarkeit_mit_dem_Namen_des_Servers()
    {
        using var handler = new ThrowingHandler();

        var message = await ServerConnectViewModel.ProbeAsync(new Uri("https://spa.example.at/"), Ct, handler);

        Assert.StartsWith("spa.example.at ist nicht erreichbar", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.OK, "<!doctype html><title>Webseite des Spas</title>")]
    public async Task Echte_Pruefung_erkennt_eine_Adresse_die_antwortet_aber_kein_Server_ist_ohne_Absturz(HttpStatusCode status, string body)
    {
        using var handler = new FixedHandler(status, body);

        var message = await ServerConnectViewModel.ProbeAsync(new Uri("https://spa.example.at/"), Ct, handler);

        Assert.Equal("spa.example.at antwortet, ist aber kein AISPA-Studio-Server. Stimmt die Adresse?", message);
    }

    [Fact]
    public async Task Ungueltige_Eingaben_nennen_ihre_Ursache_und_werden_nicht_geprueft()
    {
        var probed = 0;
        var connect = new ServerConnectViewModel(new MemorySettingsStore(), () => { }, (_, _) =>
        {
            probed++;
            return Task.FromResult<string?>(null);
        });

        connect.Address = "http://spa.example.at";
        await connect.ConnectCommand.ExecuteAsync(null);
        Assert.StartsWith("http:// ist unverschlüsselt", connect.Error, StringComparison.Ordinal);

        connect.Address = "kein Name";
        await connect.ConnectCommand.ExecuteAsync(null);
        Assert.StartsWith("Das ist keine gültige Adresse", connect.Error, StringComparison.Ordinal);
        Assert.Equal(0, probed);
    }

    [Fact]
    public void Gemerkter_Server_laesst_sich_vergessen_und_erscheint_nicht_als_Haupt_PC()
    {
        var settings = new MemorySettingsStore();
        settings.Save(new DesktopSettings(ServerAddress: "https://spa.example.at/"));
        var restarts = 0;
        var server = new ServerConnectViewModel(settings, () => restarts++);
        var mainPc = new NetworkConnectViewModel(settings, () => { });

        Assert.True(server.HasSaved);
        Assert.False(mainPc.HasSaved);

        server.ForgetCommand.Execute(null);

        Assert.Equal((null, 1), (settings.Load().ServerAddress, restarts));
    }

    [Fact]
    public void Von_Hand_eingetragenes_http_ohne_Code_gilt_nicht_als_Server()
    {
        var choice = ServerChoice.Resolve(new DesktopSettings(ServerAddress: "http://spa.example.at/"), null);

        Assert.Equal(ServerSource.Default, choice.Source);
    }

    [Fact]
    public async Task Echte_Pruefung_lehnt_ein_selbst_ausgestelltes_Zertifikat_ab()
    {
        using var certificate = NetworkShare.CreateCertificate("empfang-pc", DateTimeOffset.UtcNow);
        await using var host = NetworkShareTests.TlsHost.Start(certificate);

        var message = await ServerConnectViewModel.ProbeAsync(host.Address, Ct);

        Assert.StartsWith("Das Zertifikat von 127.0.0.1 ist nicht gültig", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Umgebungsvariable_hat_Vorrang_vor_dem_eingetragenen_Server()
    {
        var settings = new DesktopSettings(ServerAddress: "https://spa.example.at/");

        var choice = ServerChoice.Resolve(settings, "http://localhost:6000");

        Assert.Equal((new Uri("http://localhost:6000"), null, ServerSource.Environment), (choice.Address, choice.Code, choice.Source));
    }

    [Fact]
    public void Eingetragener_Server_ohne_Code_ist_ein_Server_und_mit_Code_ein_Haupt_PC()
    {
        var server = ServerChoice.Resolve(new DesktopSettings(ServerAddress: "https://spa.example.at/"), null);
        var mainPc = ServerChoice.Resolve(new DesktopSettings(ServerAddress: "https://empfang-pc:55443/", ServerCode: "3F9A-12C4-77B0-E1D2-9A0B"), null);

        Assert.Equal((new Uri("https://spa.example.at/"), null, ServerSource.Server), (server.Address, server.Code, server.Source));
        Assert.Equal((new Uri("https://empfang-pc:55443/"), "3F9A-12C4-77B0-E1D2-9A0B", ServerSource.MainPc), (mainPc.Address, mainPc.Code, mainPc.Source));
    }

    [Fact]
    public void Ohne_Eintrag_gilt_localhost()
    {
        var choice = ServerChoice.Resolve(new DesktopSettings(), null);

        Assert.Equal((new Uri("http://localhost:5086/"), null, ServerSource.Default), (choice.Address, choice.Code, choice.Source));
    }

    [Fact]
    public void Seite_Netz_zeigt_den_Server_und_loest_ihn()
    {
        var settings = new MemorySettingsStore();
        settings.Save(new DesktopSettings(ServerAddress: "https://spa.example.at/"));
        var restarts = 0;
        var link = new ServerLinkViewModel(settings, ServerChoice.Resolve(settings.Load(), null), () => restarts++);

        Assert.Contains("https://spa.example.at/", link.Text, StringComparison.Ordinal);
        Assert.True(link.ReleaseCommand.CanExecute(null));

        link.ReleaseCommand.Execute(null);

        Assert.Equal((null, null, 1), (settings.Load().ServerAddress, settings.Load().ServerCode, restarts));
    }

    [Fact]
    public void Seite_Netz_loest_nicht_die_Umgebungsvariable()
    {
        var settings = new MemorySettingsStore();
        var link = new ServerLinkViewModel(settings, ServerChoice.Resolve(settings.Load(), "http://localhost:6000"), () => { });

        Assert.Contains("AISPA_API_URL", link.Text, StringComparison.Ordinal);
        Assert.False(link.ReleaseCommand.CanExecute(null));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Verbindung abgelehnt.");
    }

    private sealed class FixedHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }
}
