using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class AuthenticationProtocolTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Server : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public Server()
        {
            _listener.Start();
            Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        }

        public Uri Address { get; }

        public async Task<string> ReplyAsync(CurrentUser? user, string? cookie)
        {
            using var connection = await _listener.AcceptTcpClientAsync(Ct);
            var headers = await SessionBoundaryTests.ReadHeadersAsync(connection);
            var body = user is null ? "" : JsonSerializer.Serialize(user, ApiClient.Json);
            await SessionBoundaryTests.RespondAsync(connection, body, cookie);
            return headers;
        }

        public void Dispose() => _listener.Dispose();
    }

    [Theory]
    [InlineData("fehlend")]
    [InlineData("anderer Name")]
    [InlineData("abgelaufen")]
    [InlineData("anderer Pfad")]
    [InlineData("keine Person")]
    [InlineData("fehlende Personenkennung")]
    public async Task Unvollstaendige_Antwort_bestaetigt_keine_Anmeldung(string reason)
    {
        using var server = new Server();
        var session = new MemorySessionStore();
        var disk = new MemoryOfflineStore();
        var active = Guid.NewGuid().ToString("N");
        session.Save(active);
        var cache = new OfflineCache(disk);
        var user = TestData.Reception();
        cache.Put("api/auth/me", JsonSerializer.Serialize(user, ApiClient.Json));
        cache.Flush();
        var api = new ApiClient(server.Address, session, offline: cache);
        var issued = Guid.NewGuid().ToString("N");
        var header = reason switch
        {
            "fehlend" => null,
            "anderer Name" => $"different={issued}; Path=/",
            "abgelaufen" => "aispa.auth=; Max-Age=0; Path=/",
            "anderer Pfad" => $"aispa.auth={issued}; Path=/other",
            "keine Person" or "fehlende Personenkennung" => $"aispa.auth={issued}; Path=/",
            _ => throw new ArgumentOutOfRangeException(nameof(reason)),
        };
        var login = api.SignInWithCodeAsync("test", Ct);
        var reply = reason switch
        {
            "keine Person" => null,
            "fehlende Personenkennung" => user with { Id = "" },
            _ => user with { Id = "angefragte-person" },
        };
        var request = await server.ReplyAsync(reply, header);

        Assert.False((await login).Ok);
        Assert.False(request.Contains($"aispa.auth={active}", StringComparison.Ordinal));
        Assert.True(api.SessionCookie == active && session.Load() == active);
        Assert.Equal(user.Id, new OfflineCache(disk).Read<CurrentUser>("api/auth/me")?.Value?.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Nach_Startpasswort_gilt_die_Merken_Absicht_der_Anmeldung(bool pin)
    {
        using var server = new Server();
        var device = new MemorySessionStore();
        device.Save(Guid.NewGuid().ToString("N"));
        var session = new MemorySessionStore();
        var disk = new MemoryOfflineStore();
        var api = new ApiClient(server.Address, session, offline: new OfflineCache(disk), deviceStore: device);
        var user = TestData.Reception();
        var login = pin ? api.QuickSwitchAsync(user.Id, "1234", Ct) : api.SignInWithPasswordAsync("test", "test", Ct);
        var first = Guid.NewGuid().ToString("N");
        var request = await server.ReplyAsync(user with { MustChangePassword = true }, $"aispa.auth={first}; Path=/");
        Assert.True(request.Contains($"aispa.device={device.Load()}", StringComparison.Ordinal));
        Assert.True((await login).Ok);
        Assert.True(api.SessionCookie == first);
        Assert.Null(session.Load());
        Assert.Null(new OfflineCache(disk).Get("api/auth/me"));

        var change = api.ChangePasswordAsync("test", "updated", Ct);
        var second = Guid.NewGuid().ToString("N");
        var renewal = await server.ReplyAsync(user, $"aispa.auth={second}; Path=/");
        Assert.True(renewal.Contains($"aispa.auth={first}", StringComparison.Ordinal));
        Assert.True((await change).Ok);
        Assert.True(api.SessionCookie == second);
        Assert.True(pin ? session.Load() is null : session.Load() == second);
        Assert.Equal(!pin, new OfflineCache(disk).Get("api/auth/me") is not null);

        using var offline = new StubApi { Down = true };
        var restarted = new MainViewModel(offline.Client(new OfflineCache(disk), session)) { ProbeWindow = TimeSpan.Zero };
        await restarted.StartAsync();
        Assert.Equal(!pin, restarted.IsSignedIn);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Passworterneuerung_verlangt_Antwortcookie_und_erhaelt_gemerkte_Sitzung(bool issued)
    {
        using var server = new Server();
        var session = new MemorySessionStore();
        var first = Guid.NewGuid().ToString("N");
        session.Save(first);
        var api = new ApiClient(server.Address, session);
        var change = api.ChangePasswordAsync("test", "updated", Ct);
        var second = Guid.NewGuid().ToString("N");
        var request = await server.ReplyAsync(TestData.Reception(), issued ? $"aispa.auth={second}; Path=/" : null);
        Assert.True(request.Contains($"aispa.auth={first}", StringComparison.Ordinal));
        Assert.Equal(issued, (await change).Ok);
        Assert.True(api.SessionCookie == (issued ? second : first));
        Assert.True(session.Load() == api.SessionCookie);
    }
}
