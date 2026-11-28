using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

public sealed class SessionBoundaryTests
{
    private const string Identity = "api/auth/me";
    private const string Customer = "api/customers/probe";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FailingStore : IOfflineStore
    {
        private readonly MemoryOfflineStore _inner = new();
        public bool Fail { get; set; }
        public OfflineState Load() => _inner.Load();
        public void Save(OfflineState state)
        {
            if (Fail)
            {
                throw new IOException("Test-Schreibfehler");
            }

            _inner.Save(state);
        }
        public void Clear()
        {
            if (Fail)
            {
                throw new IOException("Test-Schreibfehler");
            }

            _inner.Clear();
        }
    }

    private static (MemorySessionStore Session, OfflineCache Cache) Seed(IOfflineStore store)
    {
        var session = new MemorySessionStore();
        session.Save("first");
        var cache = new OfflineCache(store) { Tenant = TestData.Reception().Access!.TenantId, SharedDevice = QuickSwitchTests.Device };
        cache.Put(Identity, JsonSerializer.Serialize(TestData.Reception(), ApiClient.Json));
        cache.Put(Customer, "{}");
        cache.Flush();
        return (session, cache);
    }

    [Theory]
    [InlineData("device-code/redeem")]
    [InlineData("dev/login")]
    [InlineData("standalone/login")]
    [InlineData("standalone/setup")]
    [InlineData("standalone/recover")]
    [InlineData("quick-switch")]
    public async Task Bestaetigte_neue_Anmeldung_loescht_alte_Lesedaten(string route)
    {
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var api = new StubApi().On("POST /api/auth/" + route, TestData.Reception() with { Id = "next" }).Client(cache, session);
        var result = route switch
        {
            "device-code/redeem" => await api.SignInWithCodeAsync("test", Ct),
            "dev/login" => await api.SignInAsTestAccountAsync("test", Ct),
            "standalone/login" => await api.SignInWithPasswordAsync("test", "test", Ct),
            "standalone/setup" => await api.CompleteStandaloneSetupAsync(new("Test", "AT", "Test", "Test", "Test", "test"), Ct),
            "standalone/recover" => await api.RecoverPasswordAsync("test", "test", "test", Ct),
            _ => await api.QuickSwitchAsync("next", "1234", Ct),
        };
        Assert.True(result.Ok);
        Assert.Null(new OfflineCache(store).Get(Customer));
        Assert.Equal(QuickSwitchTests.Device, store.Load().SharedDevice);
        if (route == "quick-switch")
        {
            Assert.Null(session.Load());
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    public async Task Definitive_Ablehnung_sperrt_neuen_Client_auch_bei_Schreibfehler(HttpStatusCode denial, bool fail)
    {
        var store = new FailingStore();
        var (session, cache) = Seed(store);
        store.Fail = fail;
        var server = new StubApi().On("GET /" + Identity, null, denial);
        var result = await server.Client(cache, session).GetAsync<CurrentUser>(Identity, Ct);
        Assert.Equal(denial, result.Status);
        Assert.Null(session.Load());
        if (!fail)
        {
            Assert.Null(new OfflineCache(store).Get(Customer));
        }

        server.Down = true;
        var restarted = new MainViewModel(server.Client(new OfflineCache(store), session)) { ProbeWindow = TimeSpan.Zero };
        await restarted.StartAsync();
        Assert.False(restarted.IsSignedIn);
    }

    private sealed class FailedSessionClear(ISessionStore original, Exception? failure = null) : ISessionStore
    {
        public Exception Failure { get; } = failure ?? new IOException("Sitzungsspeicher ist gesperrt.");
        public string? Load() => original.Load();
        public void Save(string cookie) => original.Save(cookie);
        public void Clear() => throw Failure;
    }

    [Fact]
    public async Task Abmelden_loescht_den_Lesestand_auch_wenn_der_Sitzungsspeicher_gesperrt_ist()
    {
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var locked = new FailedSessionClear(session);
        var api = new StubApi().Client(cache, locked);

        var error = await Assert.ThrowsAsync<IOException>(() => api.SignOutAsync(Ct));

        Assert.Same(locked.Failure, error);
        var reopened = new OfflineCache(store);
        Assert.Null(reopened.Get(Identity));
        Assert.Null(reopened.Get(Customer));
        Assert.Null(api.SessionCookie);
        Assert.Equal(QuickSwitchTests.Device, reopened.SharedDevice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Fehlendes_Loeschrecht_laesst_den_unabhaengigen_Lesestand_widerrufen(int status)
    {
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var locked = new FailedSessionClear(session, new UnauthorizedAccessException());
        var server = new StubApi();
        var api = server.Client(cache, locked);
        if (status == 0)
        {
            Assert.Same(locked.Failure, await Assert.ThrowsAsync<UnauthorizedAccessException>(() => api.SignOutAsync(Ct)));
        }
        else
        {
            server.On("GET /" + Identity, null, (HttpStatusCode)status);
            var result = await api.GetAsync<CurrentUser>(Identity, Ct);
            Assert.Equal((HttpStatusCode)status, result.Status);
            Assert.Contains("lokal", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        var reopened = new OfflineCache(store);
        Assert.Null(reopened.Get(Identity));
        Assert.Null(reopened.Get(Customer));
        Assert.Equal(QuickSwitchTests.Device, reopened.SharedDevice);
        Assert.Null(api.SessionCookie);
    }

    [Fact]
    public async Task Zwei_gesperrte_Speicher_melden_beide_Fehler()
    {
        var store = new FailingStore();
        var (session, cache) = Seed(store);
        var locked = new FailedSessionClear(session, new UnauthorizedAccessException());
        store.Fail = true;
        var api = new StubApi().Client(cache, locked);

        var error = await Assert.ThrowsAsync<IOException>(() => api.SignOutAsync(Ct));

        var failures = Assert.IsType<AggregateException>(error.InnerException);
        Assert.Collection(failures.InnerExceptions, first => Assert.Same(locked.Failure, first), second => Assert.IsType<IOException>(second));
        Assert.Null(api.SessionCookie);
    }

    [Fact]
    public async Task Fehler_beim_Wechsel_laesst_keine_gemerkte_Anmeldung_zurueck()
    {
        var store = new FailingStore();
        var (session, cache) = Seed(store);
        store.Fail = true;
        var api = new StubApi().On("POST /api/auth/dev/login", TestData.Reception()).Client(cache, session);
        await Assert.ThrowsAsync<IOException>(() => api.SignInAsTestAccountAsync("test", Ct));
        Assert.Null(session.Load());
    }

    [Fact]
    public async Task Startpasswort_eroeffnet_keine_gemerkte_Arbeitsflaeche()
    {
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var user = TestData.Reception() with { MustChangePassword = true };
        var server = new StubApi().On("POST /api/auth/standalone/login", user).On("GET /" + Identity, user);
        var api = server.Client(cache, session);
        await api.SignInWithPasswordAsync("test", "test", Ct);
        await api.GetAsync<CurrentUser>(Identity, Ct);
        cache.Flush();
        server.Down = true;
        var restarted = new MainViewModel(server.Client(new OfflineCache(store), session)) { ProbeWindow = TimeSpan.Zero };
        await restarted.StartAsync();
        Assert.False(restarted.IsSignedIn);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Spaeter_Leseabschluss_der_vorigen_Anmeldung_veraendert_die_neue_nicht(bool denied)
    {
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var path = denied ? Identity : "api/customers/late";
        var server = new StubApi().On("GET /" + path, denied ? null : TestData.Reception(), denied ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            .Hold("GET /" + path, gate.Task).On("POST /api/auth/dev/login", TestData.Reception() with { Id = "next" });
        var api = server.Client(cache, session);
        var pending = api.GetAsync<CurrentUser>(path, Ct);
        await api.SignInAsTestAccountAsync("test", Ct);
        cache.Put(Identity, JsonSerializer.Serialize(TestData.Reception() with { Id = "next" }, ApiClient.Json));
        gate.SetResult();
        await pending;
        Assert.NotNull(cache.Get(Identity));
        Assert.Null(cache.Get("api/customers/late"));
    }

    [Fact]
    public async Task Begonnene_Kundenanlage_behaelt_ihr_Spa_beim_Wechsel()
    {
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var original = cache.Tenant;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new StubApi().Hold("POST /api/customers", gate.Task).On("POST /api/auth/dev/login", TestData.Reception()).Client(cache, session);
        var id = Guid.NewGuid();
        var create = api.CreateCustomerAsync(new("Test", "Test", null, null, null, null, ClientId: id), Ct);
        await api.SignInAsTestAccountAsync("test", Ct);
        cache.Tenant = Guid.NewGuid();
        gate.SetException(new HttpRequestException("Offline"));
        await create;
        Assert.Equal(original, Assert.Single(store.Load().Pending).TenantId);
        Assert.Null(cache.Get($"api/customers/{id}"));
    }

    [Fact]
    public async Task Abmelden_widerruft_lokal_vor_der_Antwort_und_die_naechste_Anmeldung_behaelt_ihr_Cookie()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var address = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var api = new ApiClient(address, session, offline: cache);
        var logout = api.SignOutAsync(Ct);
        using var first = await listener.AcceptTcpClientAsync(Ct);
        var headers = await ReadHeadersAsync(first);
        var sent = headers.Contains("aispa.auth=first", StringComparison.Ordinal);
        var persisted = session.Load() is not null || new OfflineCache(store).Get(Identity) is not null;
        var login = api.SignInWithCodeAsync("test", Ct);
        var nextConnection = listener.AcceptTcpClientAsync(Ct).AsTask();
        var early = await Task.WhenAny(nextConnection, Task.Delay(TimeSpan.FromMilliseconds(250), Ct)) == nextConnection;
        await RespondAsync(first, "", "aispa.auth=; Max-Age=0; Path=/");
        await logout;
        using var next = await nextConnection;
        await ReadHeadersAsync(next);
        await RespondAsync(next, JsonSerializer.Serialize(TestData.Reception() with { Id = "next" }, ApiClient.Json), "aispa.auth=next; Path=/");
        Assert.True((await login).Ok);
        Assert.True(sent);
        Assert.False(persisted);
        Assert.False(early);
        Assert.True(api.SessionCookie == "next" && session.Load() == "next");
        Assert.Equal("next", new OfflineCache(store).Read<CurrentUser>(Identity)?.Value?.Id);
    }

    [Fact]
    public async Task Geraeteanmeldung_am_Empfang_bleibt_beim_globalen_Abmelden_erhalten()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var address = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        cache.SharedDevice = null;
        var deviceStore = new MemorySessionStore();
        var api = new ApiClient(address, session, offline: cache, deviceStore: deviceStore);
        using var shell = new ShellViewModel(api, TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()),
            "", () => api.SignOutAsync(Ct));
        shell.Devices.DeviceLocation = TestData.Location;
        Assert.True(shell.Devices.RegisterDeviceCommand.CanExecute(null));
        var registration = shell.Devices.RegisterDeviceCommand.ExecuteAsync(null);
        using var registering = await listener.AcceptTcpClientAsync(Ct);
        await ReadHeadersAsync(registering);

        Assert.True(shell.SignOutCommand.CanExecute(null));
        var logout = shell.SignOutCommand.ExecuteAsync(null);
        var persistedSignIn = session.Load() is not null;
        await RespondAsync(registering, JsonSerializer.Serialize(QuickSwitchTests.Device, ApiClient.Json), "aispa.device=registered; Path=/");
        await registration;
        using var ending = await listener.AcceptTcpClientAsync(Ct);
        await ReadHeadersAsync(ending);
        await RespondAsync(ending, "", "aispa.auth=; Max-Age=0; Path=/");
        await logout;

        Assert.False(persistedSignIn);
        Assert.Null(shell.Devices.DeviceError);
        Assert.True(api.HasDeviceCookie);
        Assert.NotNull(deviceStore.Load());
        Assert.Equal(QuickSwitchTests.Device, new OfflineCache(store).SharedDevice);
    }

    [Fact]
    public async Task Geraet_neu_anmelden_uebersteht_die_Antwort_des_vorigen_Widerrufs()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var address = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        var deviceStore = new MemorySessionStore();
        deviceStore.Save("old-device");
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var api = new ApiClient(address, session, offline: cache, deviceStore: deviceStore);
        var revoke = api.RevokeDeviceAsync(Ct);
        using var previous = await listener.AcceptTcpClientAsync(Ct);
        await ReadHeadersAsync(previous);
        var replacement = QuickSwitchTests.Device with { IdleMinutes = 10 };
        var register = api.RegisterDeviceAsync(new("Empfang", TestData.Location.Id, replacement.IdleMinutes), Ct);
        var arrival = listener.AcceptTcpClientAsync(Ct).AsTask();
        var overlap = await Task.WhenAny(arrival, Task.Delay(TimeSpan.FromMilliseconds(250), Ct)) == arrival;
        if (!overlap)
        {
            await RespondAsync(previous, "", "aispa.device=; Max-Age=0; Path=/");
            await revoke;
        }

        using var latest = await arrival;
        await ReadHeadersAsync(latest);
        await RespondAsync(latest, JsonSerializer.Serialize(replacement, ApiClient.Json), "aispa.device=current-device; Path=/");
        Assert.True((await register).Ok);
        if (overlap)
        {
            await RespondAsync(previous, "", "aispa.device=; Max-Age=0; Path=/");
        }

        Assert.True((await revoke).Ok);
        Assert.True(api.HasDeviceCookie);
        Assert.True(deviceStore.Load() == "current-device");
        Assert.Equal(replacement, new OfflineCache(store).SharedDevice);
    }

    private sealed class HeldTerminalPoll(StubApi server) : DelegatingHandler(server)
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/jobs", StringComparison.Ordinal))
            {
                Started.TrySetResult();
                await Release.Task.ConfigureAwait(false);
            }
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Eine_spaete_Cookieerneuerung_aendert_die_neue_Anmeldung_nicht()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var address = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        var (session, cache) = Seed(new MemoryOfflineStore());
        var api = new ApiClient(address, session, offline: cache);
        var read = api.GetAsync<object>("api/locations", Ct);
        using var old = await listener.AcceptTcpClientAsync(Ct);
        await ReadHeadersAsync(old);

        var signin = api.SignInWithCodeAsync("test", Ct);
        using var next = await listener.AcceptTcpClientAsync(Ct);
        await ReadHeadersAsync(next);
        await RespondAsync(next, JsonSerializer.Serialize(TestData.Reception() with { Id = "next" }, ApiClient.Json), "aispa.auth=next; Path=/");
        Assert.True((await signin).Ok);
        await RespondAsync(old, "{}", "aispa.auth=previous-renewal; Path=/");
        await read;

        Assert.True(api.SessionCookie == "next" && session.Load() == "next");
    }

    [Fact]
    public async Task Lokaler_Widerruf_ist_vor_dem_Geraeteabbau_dauerhaft()
    {
        var store = new MemoryOfflineStore();
        var (session, cache) = Seed(store);
        var terminal = new PaymentTerminalDto(Guid.NewGuid(), TestData.LocationId, "Test", TerminalProvider.Local, "127.0.0.1:9", true, TerminalProtocol.Opi);
        var settings = new MemorySettingsStore();
        settings.Save(settings.Load() with { TerminalId = terminal.Id });
        var server = new StubApi().On("GET /api/system/status", new SystemStatus("1", "Test", DateTimeOffset.UtcNow))
            .On("GET /" + Identity, TestData.Reception()).On("GET /api/locations", new[] { TestData.Location })
            .On($"GET /api/pos/terminals?locationId={TestData.LocationId}", new[] { terminal });
        using var handler = new HeldTerminalPoll(server);
        var main = new MainViewModel(new ApiClient(new Uri("http://localhost/"), session, handler, cache), theme: new ThemeController(settings));
        await main.StartAsync();
        var shell = Assert.IsType<ShellViewModel>(main.Content);
        await shell.Devices.InitializeAsync();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        var logout = shell.SignOutCommand.ExecuteAsync(null);
        try
        {
            Assert.False(logout.IsCompleted);
            Assert.Null(session.Load());
            Assert.Null(new OfflineCache(store).Get(Identity));
        }
        finally
        {
            handler.Release.TrySetResult();
            await logout;
        }
    }

    private static async Task<string> ReadHeadersAsync(TcpClient client)
    {
        using var reader = new StreamReader(client.GetStream(), Encoding.ASCII, false, 1024, leaveOpen: true);
        var headers = new StringBuilder();
        while (await reader.ReadLineAsync(Ct) is { Length: > 0 } line)
        {
            headers.AppendLine(line);
        }

        return headers.ToString();
    }

    [Fact]
    public async Task Unabhaengige_Leseanfragen_warten_nicht_aufeinander()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new StubApi().Hold("GET /api/locations", gate.Task).On("GET /api/customers/probe", new { }).Client();
        var first = api.GetAsync<object>("api/locations", Ct);
        try
        {
            Assert.True((await api.GetAsync<object>(Customer, Ct).WaitAsync(TimeSpan.FromSeconds(3), Ct)).Ok);
            Assert.False(first.IsCompleted);
        }
        finally
        {
            gate.TrySetResult();
            await first;
        }
    }

    private static async Task RespondAsync(TcpClient client, string body, string cookie)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nSet-Cookie: {cookie}\r\n\r\n";
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(header), Ct);
        await client.GetStream().WriteAsync(bytes, Ct);
    }
}
