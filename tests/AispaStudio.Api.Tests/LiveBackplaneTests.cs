using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using AispaStudio.Api.Live;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.Redis;
using Wolverine;
using PingRequested = AispaStudio.Api.System.PingRequested;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Backplane für den Live-Kanal (<c>Live:Backplane</c>, ADR 0087): Mit <c>Redis</c> erreicht eine Meldung, die eine Instanz auslöst, auch die Clients an einer
/// anderen Instanz, ob die auslösende Instanz die API oder der Worker ist. Ohne den Schalter bleibt es bei den Clients der eigenen Instanz.
/// </summary>
public sealed class LiveBackplaneTests(ApiFixture fixture, LiveBackplaneTests.RedisServer redis) : IClassFixture<LiveBackplaneTests.RedisServer>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Switch = "Live:Backplane";

    private const string RedisConnection = "ConnectionStrings:redis";

    /// <summary>Ein Redis für die ganze Testklasse. Ohne Docker: <c>AISPA_TEST_REDIS</c> auf einen vorhandenen Server setzen (z. B. <c>localhost:6379</c>).</summary>
    public sealed class RedisServer : IAsyncLifetime
    {
        private static readonly string? ExternalServer = Environment.GetEnvironmentVariable("AISPA_TEST_REDIS");

        private readonly RedisContainer? _container = ExternalServer is null ? new RedisBuilder("redis:8-alpine").Build() : null;

        public string ConnectionString { get; private set; } = "";

        public async ValueTask InitializeAsync()
        {
            if (_container is null)
            {
                ConnectionString = ExternalServer!;
                return;
            }

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        public async ValueTask DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }
    }

    private WebApplicationFactory<Program> Host(string role, string? backplane, string? connection = null, LogCapture? log = null) =>
        fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Role", role);
            b.UseSetting("Logging:LogLevel:Default", "Information");
            if (backplane is not null)
            {
                b.UseSetting(Switch, backplane);
                b.UseSetting(RedisConnection, connection ?? redis.ConnectionString);
            }

            if (log is not null)
            {
                b.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(log));
            }
        });

    private static HubConnection Live(WebApplicationFactory<Program> host, string cookie) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(host.Server.BaseAddress, LiveHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => host.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions = TestSpa.Json)
            .Build();

    /// <summary>Löst die Meldung auf dem Sender aus (wie die Buchung über die Queue) und meldet, ob der Client an der anderen Instanz sie bekommt.</summary>
    private async Task<bool> ArrivesAtOtherInstanceAsync(string senderRole, string? backplane)
    {
        await using var sender = Host(senderRole, backplane);
        await using var receiver = Host("Api", backplane);
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var received = new TaskCompletionSource<AppointmentDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = Live(receiver, spa.Cookie);
        connection.On<AppointmentDto>(LiveHubContract.AppointmentChanged, a =>
        {
            if (a.Id == appointment.Id)
            {
                received.TrySetResult(a);
            }
        });
        await connection.StartAsync(Ct);
        await connection.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, Ct);

        await using (var scope = sender.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new AppointmentChangedMessage(appointment));
        }

        // Ohne Backplane gibt es nichts abzuwarten außer der Zeit, die eine Meldung bis zum Client höchstens braucht.
        return await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(backplane is null ? 4 : 30), Ct)) == received.Task;
    }

    [Theory]
    [InlineData("Api")]
    [InlineData("Worker")]
    public async Task Mit_Backplane_erreicht_eine_Meldung_den_Client_an_der_anderen_Instanz(string senderRole) =>
        Assert.True(await ArrivesAtOtherInstanceAsync(senderRole, "Redis"));

    [Theory]
    [InlineData("Api")]
    [InlineData("Worker")]
    public async Task Ohne_Backplane_kommt_eine_Meldung_an_der_anderen_Instanz_nicht_an(string senderRole) =>
        Assert.False(await ArrivesAtOtherInstanceAsync(senderRole, null));

    [Fact]
    public async Task Die_Schreibweise_Redis_gilt_ohne_Ruecksicht_auf_Groß_und_Kleinschreibung()
    {
        Assert.True(await ArrivesAtOtherInstanceAsync("Api", "redis"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("NONE")]
    public async Task Ohne_Schluessel_oder_mit_None_bleibt_der_Live_Kanal_wie_heute(string value)
    {
        var log = new LogCapture();
        await using var host = Host("Api", value, log: log);

        var manager = host.Services.GetRequiredService<HubLifetimeManager<LiveHub>>();

        Assert.Equal("DefaultHubLifetimeManager`1", manager.GetType().Name);
        Assert.Contains(log.Lines, l => l.Level == LogLevel.Information && l.Message.Contains("Live-Backplane: aus", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("Memcached")]
    [InlineData("Redis,None")]
    public async Task Ein_ungueltiger_Wert_bricht_den_Start_ab(string value)
    {
        await using var host = Host("Api", value);

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.Equal($"Live:Backplane „{value}“ ist ungültig; erlaubt sind None (Standard) und Redis.", error.Message);
    }

    [Theory]
    [InlineData("Redis")]
    [InlineData("redis")]
    public async Task Redis_ohne_Verbindungszeichenfolge_bricht_den_Start_ab(string value)
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b => b.UseSetting(Switch, value));

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("Live:Backplane=Redis braucht die Verbindungszeichenfolge ConnectionStrings:redis", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redis_mit_leerer_Verbindungszeichenfolge_bricht_den_Start_ab()
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting(Switch, "Redis");
            b.UseSetting(RedisConnection, "  ");
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("Live:Backplane=Redis braucht die Verbindungszeichenfolge ConnectionStrings:redis", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_ungueltige_Verbindungszeichenfolge_bricht_den_Start_ab_ohne_sie_zu_nennen()
    {
        await using var host = Host("Api", "Redis", "127.0.0.1:1,geheimes-Kennwort-4711=1");

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("ConnectionStrings:redis ist keine gültige Redis-Verbindungszeichenfolge", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("geheimes-Kennwort-4711", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Einzelplatz_mit_Redis_bricht_den_Start_ab()
    {
        await using var host = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Hosting:Standalone", "true");
            b.UseSetting(Switch, "Redis");
            b.UseSetting(RedisConnection, redis.ConnectionString);
        });

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("Der Einzelplatz hat nur eine Instanz und keinen Redis", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_Start_meldet_die_Backplane_ohne_die_Verbindungszeichenfolge()
    {
        var log = new LogCapture();
        // Das Kennwort darf in keiner Zeile stehen; der Server ist nicht erreichbar, der Start meldet trotzdem.
        await using var host = Host("Api", "Redis", "127.0.0.1:1,password=geheimes-Kennwort-4711", log);

        _ = host.Server;

        Assert.Contains(log.Lines, l => l.Level == LogLevel.Information && l.Message.Contains("Live-Backplane: Redis", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Lines, l => l.Message.Contains("geheimes-Kennwort-4711", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Faellt_Redis_aus_wird_nur_gewarnt_ohne_Wiederholung_und_die_Anfrage_gelingt()
    {
        var log = new LogCapture();
        // Nichts lauscht auf Port 1: Redis ist von Anfang an nicht erreichbar.
        await using var host = Host("Api", "Redis", "127.0.0.1:1,password=geheimes-Kennwort-4711", log);
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", spa.Cookie);

        // Eine Meldung über die Queue und eine Buchung über die API der Instanz mit dem toten Redis.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new AppointmentChangedMessage(appointment));
        }

        var response = await client.PostAsJsonAsync("/api/appointments", new AppointmentRequest(
            spa.Location.Id, spa.MassageTreatment.Id, spa.Customer.Id, TestSpa.At(14), spa.Anna.Id, spa.RoomA.Id, null, null), TestSpa.Json, Ct);

        Assert.True(response.IsSuccessStatusCode, $"Buchung bei Redis-Ausfall: {(int)response.StatusCode}");
        int Warnings() => log.Lines.Count(l => l.Level == LogLevel.Warning && l.Message.Contains("Terminänderung", StringComparison.Ordinal));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        while (Warnings() < 2)
        {
            await Task.Delay(100, timeout.Token);
        }

        // Die Wiederholregel der Queue (Queue:RetrySeconds) wartet in den Tests eine Sekunde; nach dem Warten wäre eine dritte Warnung zu sehen.
        await Task.Delay(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal(2, Warnings());
        Assert.DoesNotContain(log.Lines, l => l.Message.Contains("geheimes-Kennwort-4711", StringComparison.Ordinal));
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Eigener Redis nur für diesen Test (Anhalten, Pausieren, späterer Start), mit festem Port, damit die Instanz ihn nach dem Neustart wiederfindet.</summary>
    private static RedisContainer OwnRedis(int? port = null) =>
        port is null ? new RedisBuilder("redis:8-alpine").Build() : new RedisBuilder("redis:8-alpine").WithPortBinding(port.Value, 6379).Build();

    private static async Task PublishAsync(WebApplicationFactory<Program> host, object message)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(message);
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan limit)
    {
        using var timeout = new CancellationTokenSource(limit);
        try
        {
            while (!condition())
            {
                await Task.Delay(100, timeout.Token);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return condition();
        }
    }

    private static int Delivered(LogCapture log, string what = "nicht zugestellt") =>
        log.Lines.Count(l => l.Level == LogLevel.Warning && l.Message.Contains(what, StringComparison.Ordinal));

    [Fact]
    public async Task Haengt_Redis_statt_abzulehnen_wird_nur_gewarnt_ohne_Wiederholung_und_Fehlerablage()
    {
        await using var redisContainer = OwnRedis();
        await redisContainer.StartAsync(Ct);
        var log = new LogCapture();
        await using var host = Host("Api", "Redis", redisContainer.GetConnectionString(), log);
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        await using var connection = Live(host, spa.Cookie);
        await connection.StartAsync(Ct);
        await connection.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, Ct);

        // Pausiert antwortet Redis nicht und lehnt auch nicht ab: Der Befehl endet mit RedisTimeoutException (erbt von TimeoutException).
        await redisContainer.PauseAsync(Ct);
        try
        {
            await PublishAsync(host, new AppointmentChangedMessage(appointment));
            Assert.True(await WaitForAsync(() => Delivered(log, "„Terminänderung“") >= 1, TimeSpan.FromSeconds(60)), "keine Warnung bei hängendem Redis");
            await Task.Delay(TimeSpan.FromSeconds(4), Ct);
        }
        finally
        {
            await redisContainer.UnpauseAsync(CancellationToken.None);
        }

        Assert.Equal(1, Delivered(log, "„Terminänderung“"));
        Assert.DoesNotContain(log.Lines, l => l.Message.Contains("rescheduled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Startet_die_Instanz_im_Ausfall_erreicht_der_Ping_des_Aufrufers_ihn_nach_der_Rueckkehr_von_Redis()
    {
        var port = FreePort();
        var log = new LogCapture();
        await using var host = Host("Api", "Redis", $"127.0.0.1:{port}", log);
        _ = host.Server;
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var userId = (await spa.GetAsync<CurrentUser>("/api/auth/me", Ct)).Id;
        await PublishAsync(host, new PingRequested(Guid.NewGuid(), "im-ausfall", userId));
        Assert.True(await WaitForAsync(() => Delivered(log) >= 1, TimeSpan.FromSeconds(60)), "keine Warnung im Ausfall");

        await using var redisContainer = OwnRedis(port);
        await redisContainer.StartAsync(Ct);
        var pongs = 0;
        HubConnection? connection = null;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limit.CancelAfter(TimeSpan.FromSeconds(90));
        while (connection is null)
        {
            var candidate = Live(host, spa.Cookie);
            candidate.On<PongEvent>(LiveHubContract.Pong, _ => Interlocked.Increment(ref pongs));
            try
            {
                await candidate.StartAsync(limit.Token);
                connection = candidate;
            }
            catch (HttpRequestException)
            {
                await candidate.DisposeAsync();
                await Task.Delay(1000, limit.Token);
            }
            catch (HubException)
            {
                await candidate.DisposeAsync();
                await Task.Delay(1000, limit.Token);
            }
        }

        await using var live = connection;
        for (var attempt = 0; attempt < 15 && Volatile.Read(ref pongs) == 0; attempt++)
        {
            await PublishAsync(host, new PingRequested(Guid.NewGuid(), "nach-der-rueckkehr", userId));
            await Task.Delay(2000, Ct);
        }

        Assert.True(Volatile.Read(ref pongs) > 0, "Der Benutzerkanal erreicht die Instanz nach der Rückkehr von Redis nie mehr");
    }

    [Fact]
    public async Task Ein_Aussetzer_waehrend_WatchLocation_laesst_die_Gruppe_nicht_dauerhaft_stumm()
    {
        var port = FreePort();
        await using var redisContainer = OwnRedis(port);
        await redisContainer.StartAsync(Ct);
        await using var host = Host("Api", "Redis", redisContainer.GetConnectionString());
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(12), Ct);
        int first = 0, later = 0;
        await using var c1 = Live(host, spa.Cookie);
        c1.On<AppointmentDto>(LiveHubContract.AppointmentChanged, a => { if (a.Id == appointment.Id) { Interlocked.Increment(ref first); } });
        await c1.StartAsync(Ct);

        await redisContainer.StopAsync(Ct);
        await Task.Delay(1500, Ct);
        var duringBlip = Task.Run(async () =>
        {
            try
            {
                await c1.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, Ct);
            }
            catch (HubException)
            {
                // Erwartet, wenn der Aussetzer länger dauert als das Zeitlimit; der Client meldet sich danach erneut an.
            }
        }, Ct);
        await redisContainer.StartAsync(Ct);
        await duringBlip;

        // Wie die Oberfläche: Scheitert die Anmeldung am Standort, versucht der Client es erneut.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limit.CancelAfter(TimeSpan.FromSeconds(90));
        while (true)
        {
            try
            {
                await c1.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, limit.Token);
                break;
            }
            catch (HubException)
            {
                await Task.Delay(1000, limit.Token);
            }
        }

        await using var c2 = Live(host, spa.Cookie);
        c2.On<AppointmentDto>(LiveHubContract.AppointmentChanged, a => { if (a.Id == appointment.Id) { Interlocked.Increment(ref later); } });
        await c2.StartAsync(limit.Token);
        await c2.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, limit.Token);

        await PublishAsync(host, new AppointmentChangedMessage(appointment));

        Assert.True(await WaitForAsync(() => Volatile.Read(ref first) > 0 && Volatile.Read(ref later) > 0, TimeSpan.FromSeconds(30)),
            $"nach dem Aussetzer bekommen nicht alle Clients die Meldung (erster {first}, später verbunden {later})");
    }

    [Fact]
    public async Task Ein_channelPrefix_in_der_Verbindungszeichenfolge_trennt_die_Installationen()
    {
        await using var own = Host("Api", "Redis", $"{redis.ConnectionString},channelPrefix=kunde2");
        await using var same = Host("Worker", "Redis", $"{redis.ConnectionString},channelPrefix=kunde2");
        await using var other = Host("Worker", "Redis");
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var received = 0;
        await using var connection = Live(own, spa.Cookie);
        connection.On<AppointmentDto>(LiveHubContract.AppointmentChanged, a => { if (a.Id == appointment.Id) { Interlocked.Increment(ref received); } });
        await connection.StartAsync(Ct);
        await connection.InvokeAsync(LiveHubContract.WatchLocation, spa.Location.Id, Ct);

        await PublishAsync(other, new AppointmentChangedMessage(appointment));
        await Task.Delay(TimeSpan.FromSeconds(4), Ct);
        var fromOtherInstallation = Volatile.Read(ref received);
        await PublishAsync(same, new AppointmentChangedMessage(appointment));
        var fromSame = await WaitForAsync(() => Volatile.Read(ref received) > fromOtherInstallation, TimeSpan.FromSeconds(30));

        Assert.Equal(0, fromOtherInstallation);
        Assert.True(fromSame, "die Meldung der Installation mit demselben Präfix kommt nicht an");
    }

    [Theory]
    [InlineData("127.0.0.1:1,tunnel=geheimes-Kennwort-4711")]
    [InlineData("127.0.0.1:1,ssl=geheimes-Kennwort-4711")]
    public async Task Auch_eine_Zeichenfolge_die_FormatException_wirft_bricht_deutsch_und_ohne_den_Wert_ab(string connection)
    {
        await using var host = Host("Api", "Redis", connection);

        var error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.StartsWith("ConnectionStrings:redis ist keine gültige Redis-Verbindungszeichenfolge", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("geheimes-Kennwort-4711", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", 1000, 2000)]
    [InlineData(",connectTimeout=4000,asyncTimeout=6000", 4000, 6000)]
    [InlineData(",CONNECTTIMEOUT=4000", 4000, 2000)]
    [InlineData(",AsyncTimeout=6000", 1000, 6000)]
    public async Task Zeitlimits_gelten_standardmaessig_und_lassen_sich_ueber_die_Verbindungszeichenfolge_anheben(string suffix, int connect, int command)
    {
        await using var host = Host("Api", "Redis", $"127.0.0.1:1{suffix}");

        var configuration = host.Services.GetRequiredService<IOptions<RedisOptions>>().Value.Configuration;

        Assert.Equal(connect, configuration.ConnectTimeout);
        Assert.Equal(command, configuration.AsyncTimeout);
    }
}
