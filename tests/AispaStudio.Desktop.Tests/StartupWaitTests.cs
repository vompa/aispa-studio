using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ä68 (Runde 6, Auftraggeber): beim Start der Windows-App, während die API noch hochfährt, standen drei Zeitüberschreitungen von je 20 Sekunden im Ausgabefenster.
/// Der Start fragt jetzt mit kurzem Zeitlimit, wiederholt es alle paar Sekunden, zeigt „Verbindung wird aufgebaut …“ und fragt die Datenbank erst, wenn der Server bereit ist.
/// </summary>
public sealed class StartupWaitTests
{
    /// <summary>Antwortet je Aufruf von „api/system/status“ der Reihe nach: <c>null</c> hängt bis zum Abbruch (der Server nimmt die Verbindung an und schweigt), ein Stand wird ausgeliefert; der letzte gilt weiter.</summary>
    private sealed class ScriptedServer(params SystemStatus?[] statuses) : HttpMessageHandler
    {
        private int _statusCalls;

        public int StatusCalls => _statusCalls;

        public List<string> Routes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            lock (Routes)
            {
                Routes.Add(path);
            }

            if (path != "/api/v1/system/status")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var status = statuses[Math.Min(Interlocked.Increment(ref _statusCalls) - 1, statuses.Length - 1)];
            if (status is null)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(status, options: ApiClient.Json) };
        }
    }

    private static SystemStatus Ready => new("1.0.0+abc", "Test", DateTimeOffset.UtcNow);

    private static SystemStatus Starting(StartupPhase phase, string text) => new("1.0.0+abc", "Test", DateTimeOffset.UtcNow, new StartupInfo(phase, text));

    // Das Zeitlimit ist groß genug für den ersten, noch ungewärmten Aufruf (JSON, auch unter Last der Gesamtsuite) und klein gegen die 20 Sekunden des ApiClient.
    private static MainViewModel Create(ScriptedServer server, TimeSpan? window = null, TimeSpan? timeout = null) =>
        new(new ApiClient(new Uri("http://localhost/"), new MemorySessionStore(), server))
        {
            ProbeTimeout = timeout ?? TimeSpan.FromMilliseconds(1500),
            ProbeInterval = TimeSpan.FromMilliseconds(10),
            ProbeWindow = window ?? TimeSpan.FromSeconds(10),
        };

    /// <summary>Merkt sich jede Überschrift und jeden Statustext, die das Fenster zu sehen bekam.</summary>
    private static List<string> Watch(MainViewModel vm)
    {
        var seen = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.ConnectionTitle) or nameof(MainViewModel.StatusText))
            {
                seen.Add($"{vm.ConnectionTitle} | {vm.StatusText}");
            }
        };
        return seen;
    }

    [Fact]
    public async Task Antwortet_der_Server_erst_beim_dritten_Mal_wartet_der_Start_mit_kurzem_Zeitlimit_und_zeigt_den_Aufbau()
    {
        var server = new ScriptedServer(null, null, Ready);
        var vm = Create(server);
        var seen = Watch(vm);

        var watch = Stopwatch.StartNew();
        await vm.CheckStatusCommand.ExecuteAsync(null);

        Assert.Equal(3, server.StatusCalls);
        Assert.True(vm.IsConnected);
        Assert.Equal("Verbunden · Version 1.0.0 · Test", vm.StatusText);
        // Zwei hängende Versuche mit je 1,5 s Zeitlimit statt zweimal 20 Sekunden.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Der Start brauchte {watch.Elapsed}.");
        Assert.Contains(seen, s => s.StartsWith("Verbindung wird aufgebaut …", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Antwortet_der_Server_nie_zeigt_der_Start_nach_der_Wartezeit_keine_Verbindung_statt_zu_haengen()
    {
        var server = new ScriptedServer((SystemStatus?)null);
        var vm = Create(server, window: TimeSpan.FromMilliseconds(600), timeout: TimeSpan.FromMilliseconds(100));

        await vm.CheckStatusCommand.ExecuteAsync(null);

        Assert.True(server.StatusCalls >= 3);
        Assert.False(vm.IsConnected);
        Assert.Equal("Keine Verbindung zum Server", vm.ConnectionTitle);
        Assert.Equal("Der Server antwortet nicht.", vm.StatusText);
    }

    [Fact]
    public async Task Migriert_der_Server_noch_steht_sein_Stand_da_und_die_Datenbank_wird_erst_danach_gefragt()
    {
        var server = new ScriptedServer(
            Starting(StartupPhase.Migrating, "Datenbank wird migriert (seit 3 s)"),
            Starting(StartupPhase.Seeding, "Beispiel-Spa wird eingerichtet (seit 5 s)"),
            Ready);
        var vm = Create(server);
        var seen = Watch(vm);

        await vm.StartAsync();

        Assert.Contains("Verbindung wird aufgebaut … | Datenbank wird migriert (seit 3 s)", seen);
        Assert.Contains("Verbindung wird aufgebaut … | Beispiel-Spa wird eingerichtet (seit 5 s)", seen);
        Assert.True(vm.IsConnected);
        // Alle Abfragen der Anmeldung kamen nach dem dritten Stand „bereit“, keine in das Zeitlimit.
        lock (server.Routes)
        {
            Assert.Equal(3, server.Routes.Take(3).Count(r => r == "/api/v1/system/status"));
            Assert.Contains("/api/v1/auth/me", server.Routes);
        }
    }

    [Fact]
    public async Task Startbildschirm_nennt_je_Phase_einen_kurzen_Stand_und_die_Phase_fuer_die_technischen_Angaben()
    {
        var server = new ScriptedServer(
            Starting(StartupPhase.Migrating, "Datenbank wird migriert (seit 3 s)"),
            Starting(StartupPhase.Seeding, "Beispiel-Spa wird eingerichtet (seit 5 s)"),
            Ready);
        var vm = Create(server);
        var seen = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.StartupStatus))
            {
                seen.Add($"{vm.StartupStatus} | {vm.StartupStep}");
            }
        };

        Assert.Equal("Server wird gestartet …", vm.StartupStatus);
        await vm.StartAsync();

        Assert.Equal(
            ["Datenbank wird vorbereitet … | Migrating", "Beispiel-Spa wird eingerichtet (seit 5 s) … | Seeding", "Anmeldung wird geladen … | Anmeldung"],
            seen);
        Assert.False(vm.IsStarting);
        Assert.False(vm.IsOffline);
    }

    [Fact]
    public async Task Startbildschirm_bleibt_beim_Verbindungsaufbau_bei_Server_wird_gestartet_und_endet_ohne_Verbindung_als_Karte()
    {
        var vm = new MainViewModel(new StubApi { Down = true }.Client())
        {
            ProbeInterval = TimeSpan.FromMilliseconds(10),
            ProbeWindow = TimeSpan.FromMilliseconds(100),
        };
        var starting = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsStarting))
            {
                starting.Add(vm.IsStarting);
            }
        };

        await vm.StartAsync();

        Assert.Equal([true, false], starting);
        Assert.Equal("Server wird gestartet …", vm.StartupStatus);
        Assert.Equal("Verbinden", vm.StartupStep);
        Assert.True(vm.IsOffline);
        // Die Karte steht mit Logo und Namen auf der Petrolfläche, auch wenn der Startbildschirm vorher nicht zu sehen war.
        Assert.True(vm.IsStartupScreenShown);
    }

    [Fact]
    public async Task Ein_gescheiterter_Start_haelt_den_Startbildschirm_nicht_auf()
    {
        var server = new ScriptedServer(Starting(StartupPhase.Failed, "Migration gescheitert"));
        var vm = Create(server);

        await vm.CheckStatusCommand.ExecuteAsync(null);

        Assert.Equal(1, server.StatusCalls);
        Assert.True(vm.IsConnected);
    }

    [Fact]
    public async Task Ein_nicht_erreichbarer_Server_wird_eine_Weile_versucht_und_dann_gemeldet()
    {
        var vm = new MainViewModel(new StubApi { Down = true }.Client())
        {
            ProbeInterval = TimeSpan.FromMilliseconds(10),
            ProbeWindow = TimeSpan.FromMilliseconds(100),
        };
        var seen = Watch(vm);

        await vm.CheckStatusCommand.ExecuteAsync(null);

        Assert.Contains(seen, s => s.StartsWith("Verbindung wird aufgebaut …", StringComparison.Ordinal));
        Assert.False(vm.IsConnected);
        Assert.Equal("Keine Verbindung zum Server", vm.ConnectionTitle);
        Assert.StartsWith("Server nicht erreichbar", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mit_lokalem_Stand_wartet_der_Start_nicht_auf_den_Server()
    {
        var offline = new OfflineCache(new MemoryOfflineStore(), TimeProvider.System);
        offline.Put("api/auth/me", "{}");
        var vm = new MainViewModel(new StubApi { Down = true }.Client(offline))
        {
            ProbeInterval = TimeSpan.FromSeconds(30),
            ProbeWindow = TimeSpan.FromMinutes(5),
        };

        var watch = Stopwatch.StartNew();
        await vm.CheckStatusCommand.ExecuteAsync(null);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Der Start wartete {watch.Elapsed}.");
        Assert.False(vm.IsConnected);
        Assert.StartsWith("Server nicht erreichbar", vm.StatusText, StringComparison.Ordinal);
    }
}
