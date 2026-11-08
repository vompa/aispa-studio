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
        Assert.Contains("Die Anmeldung wird geladen. Der Server antwortet noch nicht, die App versucht es weiter.", seen);
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
        Assert.Equal("Keine Verbindung zum Server", vm.ConnectionTitle);
        Assert.Equal("Der Server antwortet nicht.", vm.StatusText);
    }
}
