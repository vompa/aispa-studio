using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Beim Start lädt die App die gemerkte Anmeldung (`api/auth/me`). Antwortet der Server darauf noch nicht (Debug-Build, erster Aufruf, API im Debugger),
/// versucht es die App sichtbar weiter, statt nach einem Versuch zur Anmeldeseite zu springen.
/// </summary>
public sealed class StartupSignInTests
{
    /// <summary>Status „bereit“; `api/auth/me` hängt die ersten <paramref name="hangs"/> Male bis zum Abbruch und antwortet danach mit 401.</summary>
    private sealed class SlowSignIn(int hangs) : HttpMessageHandler
    {
        private int _meCalls;

        public int MeCalls => _meCalls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (path == "/api/v1/system/status")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new SystemStatus("1.0.0+abc", "Test", DateTimeOffset.UtcNow), options: ApiClient.Json),
                };
            }

            if (path == "/api/v1/auth/me" && Interlocked.Increment(ref _meCalls) <= hangs)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }
    }

    private static MainViewModel Create(SlowSignIn server, TimeSpan window) =>
        new(new ApiClient(new Uri("http://localhost/"), new MemorySessionStore(), server))
        {
            ProbeTimeout = TimeSpan.FromMilliseconds(1500),
            ProbeInterval = TimeSpan.FromMilliseconds(10),
            StartupWindow = window,
            SignInTimeout = TimeSpan.FromMilliseconds(200),
        };

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Nach_Verbindungsausfall_und_Zeitlimit_der_Anmeldung_startet_die_gemerkte_Person_offline(HttpStatusCode status)
    {
        var server = new StubApi().On("GET /api/auth/me", TestData.Reception());
        var cache = new OfflineCache(new MemoryOfflineStore());
        var session = new MemorySessionStore();
        session.Save("test-session");
        Assert.NotNull((await server.Client(cache, session).GetAsync<CurrentUser>("api/auth/me", TestContext.Current.CancellationToken)).Value);
        server.On("GET /api/system/status", new SystemStatus("1", "Test", DateTimeOffset.UtcNow), status);
        server.Hold("GET /api/auth/me", new TaskCompletionSource().Task);
        var vm = new MainViewModel(server.Client(cache, session))
        {
            SignInTimeout = TimeSpan.FromMilliseconds(100),
            StartupWindow = TimeSpan.Zero,
        };

        await vm.StartAsync();

        Assert.False(vm.IsConnected);
        Assert.IsType<ShellViewModel>(vm.Content);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Eine_abgelehnte_Anmeldung_hat_Vorrang_vor_dem_gemerkten_Zugriff(HttpStatusCode status)
    {
        var server = new StubApi().On("GET /api/auth/me", TestData.Reception());
        var store = new MemoryOfflineStore();
        var cache = new OfflineCache(store);
        var session = new MemorySessionStore();
        session.Save("test-session");
        var api = server.Client(cache, session);
        Assert.NotNull((await api.GetAsync<CurrentUser>("api/auth/me", TestContext.Current.CancellationToken)).Value);
        server.On("GET /api/system/status", null, HttpStatusCode.ServiceUnavailable);
        server.On("GET /api/auth/me", null, status);
        var vm = new MainViewModel(api);

        await vm.StartAsync();

        Assert.False(vm.IsSignedIn);
        Assert.IsNotType<ShellViewModel>(vm.Content);

        cache.Flush();
        server.Down = true;
        var reloaded = new OfflineCache(store);
        var restarted = new MainViewModel(server.Client(reloaded, session)) { ProbeWindow = TimeSpan.Zero };
        await restarted.StartAsync();

        Assert.False(restarted.IsSignedIn);
        Assert.Null(restarted.Content);
        Assert.Null(reloaded.Get("api/auth/me"));
    }

    [Fact]
    public async Task Antwortet_die_Anmeldung_erst_beim_dritten_Mal_wartet_der_Start_sichtbar_statt_zur_Anmeldeseite_zu_springen()
    {
        var server = new SlowSignIn(hangs: 2);
        var vm = Create(server, TimeSpan.FromSeconds(10));
        var seen = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.StatusText))
            {
                seen.Add(vm.StatusText);
            }
        };

        await vm.StartAsync();

        Assert.Equal(3, server.MeCalls);
        Assert.Contains("Die Anmeldung wird geladen. AISPA Studio antwortet noch nicht, die App versucht es weiter.", seen);
        Assert.IsType<LoginViewModel>(vm.Content);
    }

    [Fact]
    public async Task Antwortet_die_Anmeldung_nie_zeigt_der_Start_keine_Verbindung_statt_der_Anmeldeseite()
    {
        var server = new SlowSignIn(hangs: int.MaxValue);
        var vm = Create(server, TimeSpan.FromMilliseconds(800));

        await vm.StartAsync();

        Assert.True(server.MeCalls >= 2);
        Assert.Null(vm.Content);
        Assert.False(vm.IsConnected);
        Assert.Equal("Keine Verbindung", vm.ConnectionTitle);
        Assert.Equal("AISPA Studio antwortet nicht.", vm.StatusText);
    }
}
