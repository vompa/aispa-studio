using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Live;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AispaStudio.Api.Tests;

public sealed class SystemEndpointTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Status_liefert_Umgebung_und_Version()
    {
        using var client = fixture.CreateClient();

        var status = await client.GetFromJsonAsync<SystemStatus>("/api/system/status", TestSpa.Json, Ct);

        Assert.NotNull(status);
        Assert.Equal(StartupPhase.Ready, status.Startup?.Phase);
        Assert.Equal("Development", status.Environment);
        Assert.False(string.IsNullOrWhiteSpace(status.Version));
    }

    [Fact]
    public async Task Startseite_leitet_in_der_Entwicklung_zur_Anmeldeseite_von_Vite_weiter()
    {
        using var client = fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/?einladung=tok-1", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("http://localhost:5173/anmelden/?einladung=tok-1", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Adresse_der_Team_Oberflaeche_gibt_es_ohne_Anmeldung()
    {
        using var client = fixture.Factory.CreateClient();

        var web = await client.GetFromJsonAsync<TeamWebInfo>("/api/system/team-web", Ct);

        Assert.Equal("http://localhost:5173/", web?.Url);
    }

    [Fact]
    public void Health_Checks_laufen_ohne_Anmeldung_und_Mandanten_Middleware()
    {
        // Ein abgebrochener Health-Check (Aspire, Debugger angehalten) soll nicht als Ausnahme durch TenantLock und OnlineTenant laufen.
        var endpoints = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is "/health" or "/alive").ToList();

        Assert.Equal(2, endpoints.Count);
        // ShortCircuitMetadata ist intern; ShortCircuit() hängt sie an.
        Assert.All(endpoints, e => Assert.Contains(e.Metadata, m => m.GetType().Name == "ShortCircuitMetadata"));
    }

    [Fact]
    public async Task Ohne_Datenbankverbindung_nennt_der_Start_das_richtige_Startprojekt()
    {
        // Direktstart der API in Visual Studio statt über den AppHost: früher eine englische Meldung von Wolverine.
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:aispadb", ""));

        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());

        Assert.Contains("AispaStudio.AppHost", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_ist_gruen()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_Dokument_ist_Version_3_1()
    {
        using var client = fixture.CreateClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", Ct));

        Assert.StartsWith("3.1", doc.RootElement.GetProperty("openapi").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Datenbankmodell_hat_keine_fehlende_Migration()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();

        Assert.False(db.Database.HasPendingModelChanges(), "Modell geändert: 'dotnet ef migrations add <Name> -p src/AispaStudio.Api -o Data/Migrations' ausführen.");
    }

    [Fact]
    public async Task Ping_ohne_Anmeldung_wird_abgelehnt()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/system/ping", new PingRequest("hallo"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ping_laeuft_ueber_die_Queue_und_kommt_per_SignalR_zurueck()
    {
        var cookie = await TestUsers.RegisterAsync(fixture, Ct);
        var pong = new TaskCompletionSource<PongEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, LiveHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .Build();
        connection.On<PongEvent>(LiveHubContract.Pong, p => pong.TrySetResult(p));
        await connection.StartAsync(Ct);

        using var client = fixture.CreateClient(cookie);
        var response = await client.PostAsJsonAsync("/api/system/ping", new PingRequest("hallo"), Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<PingAccepted>(Ct);
        var received = await pong.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(accepted!.PingId, received.PingId);
        Assert.Equal("hallo", received.Message);
    }

    [Fact]
    public async Task Ping_erreicht_nur_den_Aufrufer_nicht_andere_Mandanten()
    {
        var cookieA = await TestUsers.RegisterAsync(fixture, Ct);
        var cookieB = await TestUsers.RegisterAsync(fixture, Ct);
        var text = $"nur-fuer-a-{Guid.NewGuid():N}";
        var pongA = new TaskCompletionSource<PongEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pongB = new TaskCompletionSource<PongEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connectionA = LiveConnection(cookieA);
        await using var connectionB = LiveConnection(cookieB);
        connectionA.On<PongEvent>(LiveHubContract.Pong, p => pongA.TrySetResult(p));
        connectionB.On<PongEvent>(LiveHubContract.Pong, p => pongB.TrySetResult(p));
        await connectionA.StartAsync(Ct);
        await connectionB.StartAsync(Ct);

        using var client = fixture.CreateClient(cookieA);
        var response = await client.PostAsJsonAsync("/api/system/ping", new PingRequest(text), Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // Positivkontrolle: Der Aufrufer bekommt das Pong, erst danach zählt das Ausbleiben bei B.
        var received = await pongA.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        Assert.Equal(text, received.Message);
        await Task.Delay(TimeSpan.FromSeconds(3), Ct);
        Assert.False(pongB.Task.IsCompleted, "Ein anderer Mandant hat das Pong des Aufrufers erhalten");
    }

    [Fact]
    public async Task Ping_ohne_Aufrufer_aus_der_Queue_sendet_nichts_und_wirft_nicht()
    {
        var cookie = await TestUsers.RegisterAsync(fixture, Ct);
        var pong = new TaskCompletionSource<PongEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = LiveConnection(cookie);
        connection.On<PongEvent>(LiveHubContract.Pong, p => pong.TrySetResult(p));
        await connection.StartAsync(Ct);

        // Eine Nachricht aus der Zeit vor dem Feld UserId: Clients.User(null) würde werfen, und Wolverine wiederholte sie bis zur Fehlerablage.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await PingRequestedHandler.Handle(new PingRequested(Guid.NewGuid(), "alt", null),
            services.GetRequiredService<IHubContext<LiveHub, ILiveClient>>(), TimeProvider.System, services.GetRequiredService<ILogger<LiveHub>>());

        await Task.Delay(TimeSpan.FromSeconds(2), Ct);
        Assert.False(pong.Task.IsCompleted);
    }

    private HubConnection LiveConnection(string cookie) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, LiveHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .Build();
}
