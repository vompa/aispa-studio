using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using AispaStudio.Desktop.Controls;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

public sealed class SettingsTransactionTests
{
    private static DesktopSettings Original() => new(ServerAddress: "https://bisher/", CompanionPages: new Dictionary<string, bool> { ["kalender"] = true },
        AssistantHistory: new("test", new DateOnly(2026, 1, 1), ["Frage"]));

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(false, null, null)]
    [InlineData(true, FileAccess.Read, FileShare.Read)]
    [InlineData(true, FileAccess.Read, FileShare.ReadWrite)]
    [InlineData(true, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)]
    [InlineData(true, FileAccess.ReadWrite, FileShare.ReadWrite)]
    [InlineData(true, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)]
    public async Task Verbindung_loesen_prueft_die_bestehende_Ersetzungsfreigabe_vor_dem_Widerruf(bool existing, FileAccess? access, FileShare? share)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows-Freigabe für das Ersetzen einer geöffneten Datei.");
        var directory = Directory.CreateTempSubdirectory("aispa-ersetzungsfreigabe-");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            var settings = new FileSettingsStore(path);
            if (existing)
            {
                settings.Save(Original());
            }

            var before = existing ? File.ReadAllBytes(path) : null;
            var canReplace = access is null;

            var session = new MemorySessionStore();
            var disk = new MemoryOfflineStore();
            var user = TestData.Reception();
            var current = ServerChoice.Resolve(settings.Load(), null);
            using var server = new StubApi().OnSignIn($"POST {current.Address.Authority}/api/auth/dev/login", user);
            var api = new ApiClient(current.Address, session, server, new OfflineCache(disk));
            Assert.True((await api.SignInAsTestAccountAsync("test", TestContext.Current.CancellationToken)).Ok);
            var cookie = api.SessionCookie;
            Assert.NotNull(cookie);
            var ends = 0;
            var restarts = 0;
            void Revoke()
            {
                ends++;
                api.ForgetSignIn();
            }

            string? error;
            using (var holder = access.HasValue && share.HasValue ? File.Open(path, FileMode.Open, access.Value, share.Value) : null)
            {
                if (existing)
                {
                    var form = new ServerLinkViewModel(settings, current, () => restarts++, Revoke);
                    Assert.True(form.ReleaseCommand.CanExecute(null));
                    await form.ReleaseCommand.ExecuteAsync(null);
                    error = form.Error;
                }
                else
                {
                    var form = new ServerConnectViewModel(settings, () => restarts++, (_, _) => Task.FromResult<string?>(null), Revoke) { Address = "https://neu/" };
                    await form.ConnectCommand.ExecuteAsync(null);
                    error = form.Error;
                }
            }

            Assert.Equal(canReplace ? 1 : 0, ends);
            Assert.Equal(canReplace ? 1 : 0, restarts);
            if (canReplace)
            {
                Assert.Null(error);
                Assert.True(File.Exists(path));
                Assert.Equal(existing ? null : "https://neu/", settings.Load().ServerAddress);
                Assert.Null(api.SessionCookie);
                Assert.Null(session.Load());
            }
            else
            {
                Assert.Contains("vorbereitet", error, StringComparison.Ordinal);
                Assert.Equal(before, File.ReadAllBytes(path));
                Assert.True(api.SessionCookie == cookie && session.Load() == cookie);
            }

            server.Down = true;
            var next = ServerChoice.Resolve(settings.Load(), null);
            var reopened = new MainViewModel(new ApiClient(next.Address, session, server, new OfflineCache(disk))) { ProbeWindow = TimeSpan.Zero };
            await reopened.StartAsync();
            Assert.Equal(!canReplace, reopened.IsSignedIn && reopened.CanShowContent);
            Assert.Equal(canReplace ? null : user.Id, reopened.User?.Id);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Ungueltiges_Speicherziel_belaesst_die_bisherige_Offlineanmeldung()
    {
        // NUL ist unter Windows ein Gerät. Auf anderen Plattformen ist das Verzeichnis ebenfalls kein Dateiziel.
        var invalid = OperatingSystem.IsWindows() ? Path.Combine(Path.GetTempPath(), "NUL") : Path.GetTempPath();
        var settings = new FileSettingsStore(invalid);
        var session = new MemorySessionStore();
        var disk = new MemoryOfflineStore();
        using var server = new StubApi().OnSignIn("POST /api/auth/dev/login", TestData.Reception());
        var api = server.Client(new OfflineCache(disk), session);
        Assert.True((await api.SignInAsTestAccountAsync("test", TestContext.Current.CancellationToken)).Ok);
        var restarts = 0;
        var form = new ServerConnectViewModel(settings, () => restarts++, (_, _) => Task.FromResult<string?>(null), api.ForgetSignIn)
        {
            Address = "https://anderes-spa/",
        };
        await form.ConnectCommand.ExecuteAsync(null);
        Assert.NotNull(form.Error);
        Assert.Equal(0, restarts);
        server.Down = true;
        var restarted = new MainViewModel(server.Client(new OfflineCache(disk), session)) { ProbeWindow = TimeSpan.Zero };
        await restarted.StartAsync();
        Assert.True(restarted.IsSignedIn && restarted.CanShowContent);
    }

    [Fact]
    public void Vorbereitete_Datei_bleibt_bis_zur_bestaetigten_Veroeffentlichung_inaktiv()
    {
        var directory = Directory.CreateTempSubdirectory("aispa-einstellungen-");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            var store = new FileSettingsStore(path);
            var strict = (IPreparedSettingsStore)store;
            using (var first = strict.Prepare(_ => Original()))
            {
                Assert.False(File.Exists(path));
                first.CommitAndVerify();
            }

            using (strict.Prepare(old => old with { ServerAddress = "https://verworfen/" }))
            {
                Assert.Equal(Original().ServerAddress, new FileSettingsStore(path).Load().ServerAddress);
            }

            Assert.Single(directory.GetFiles());
            using (var accepted = strict.Prepare(old => old with { ServerAddress = "https://neu/" }))
            {
                accepted.CommitAndVerify();
            }

            var actual = new FileSettingsStore(path).Load();
            Assert.Equal("https://neu/", actual.ServerAddress);
            Assert.True(actual.CompanionPages?["kalender"]);
            Assert.Equal("Frage", Assert.Single(actual.AssistantHistory!.Questions));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("beschädigt")]
    [InlineData("gesperrt")]
    [InlineData("Verzeichnis")]
    public async Task Endpfadfehler_wird_vor_dem_Widerruf_sichtbar(string obstacle)
    {
        var directory = Directory.CreateTempSubdirectory("aispa-endpfad-");
        try
        {
            var path = Path.Combine(directory.FullName, "einstellungen.json");
            if (obstacle == "Verzeichnis")
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                File.WriteAllText(path, obstacle == "beschädigt" ? "{" : "{}");
            }

            using var locked = obstacle == "gesperrt" ? File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
            var ends = 0;
            var restarts = 0;
            var link = new ServerLinkViewModel(new FileSettingsStore(path), ServerChoice.Resolve(Original(), null), () => restarts++, () => ends++);
            await link.ReleaseCommand.ExecuteAsync(null);
            Assert.Contains("vorbereitet", link.Error, StringComparison.Ordinal);
            Assert.Equal((0, 0), (ends, restarts));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("Umgebung", false)]
    [InlineData("gleichwertiger Code", false)]
    [InlineData("anderer Code", true)]
    [InlineData("ohne Code", true)]
    public async Task Nur_ein_anderes_wirksames_Ziel_verwirft_Zugang_und_Assistentenverlauf(string scenario, bool changed)
    {
        var store = new MemorySettingsStore();
        var previous = Original() with { ServerCode = "abcdeabcdeabcdeabcde" };
        store.Save(previous);
        var ends = 0;
        var starts = 0;
        var coordinator = new ConnectionSettings(store, () => starts++, scenario == "Umgebung" ? "https://vorgegeben/" : null,
            () => { ends++; return Task.CompletedTask; }, null, null);
        var uri = new Uri(scenario == "Umgebung" ? "https://reserve/" : previous.ServerAddress!);
        var pin = scenario switch
        {
            "gleichwertiger Code" => NetworkShare.NormalizeCode(previous.ServerCode!),
            "anderer Code" => "1111-2222-3333-4444-5555",
            _ => null,
        };
        await coordinator.ChangeAsync(uri, pin, _ => { });
        Assert.Equal(uri.ToString(), store.Load().ServerAddress);
        Assert.Equal(1, starts);
        Assert.Equal(changed ? 1 : 0, ends);
        Assert.Equal(!changed, store.Load().AssistantHistory is not null);
    }

    private sealed class FaultStore(string phase) : ISettingsStore, IPreparedSettingsStore
    {
        private DesktopSettings _value = Original();
        public IOException Failure { get; } = new("Speicherfehler");
        public DesktopSettings Load() => _value;
        public void Save(DesktopSettings value) => _value = value;

        public PreparedSettingsWrite Prepare(Func<DesktopSettings, DesktopSettings> update)
        {
            var desired = update(Load());
            if (phase == "Vorbereitung")
            {
                throw Failure;
            }

            return new(() =>
            {
                if (phase == "Veröffentlichung")
                {
                    throw Failure;
                }

                Save(desired);
            }, () => throw Failure);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileSettingsStore_bestaetigt_nur_den_tatsaechlichen_Endinhalt(bool changedContent)
    {
        var directory = Directory.CreateTempSubdirectory("aispa-publikation-");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            var store = new FileSettingsStore(path);
            var restarts = 0;
            var ended = false;
            string? notice = null;
            Exception? cause = null;
            Task End()
            {
                ended = true;
                if (changedContent)
                {
                    File.WriteAllText(Assert.Single(directory.GetFiles("*.tmp")).FullName, "{}");
                }
                else
                {
                    Directory.CreateDirectory(path);
                }

                return Task.CompletedTask;
            }

            var change = new ConnectionSettings(store, () => restarts++, null, End, (_, message) => notice = message, error => cause = error);
            await change.ChangeAsync(new Uri("https://neu/"), null, _ => { });
            Assert.True(ended);
            Assert.Equal(0, restarts);
            Assert.NotNull(cause);
            Assert.Contains("nicht bestätigt", notice, StringComparison.Ordinal);
            Assert.Empty(directory.GetFiles("*.tmp"));
            if (changedContent)
            {
                Assert.Equal("{}", File.ReadAllText(path));
            }
            else
            {
                Assert.True(Directory.Exists(path));
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("Vorbereitung")]
    [InlineData("Veröffentlichung")]
    [InlineData("Bestätigung")]
    public async Task Speicherfehler_verhindert_Neustart_und_erhaelt_die_urspruengliche_Ursache(string phase)
    {
        var store = new FaultStore(phase);
        var restarts = 0;
        var closed = false;
        string? formError = null;
        string? closedError = null;
        Exception? cause = null;
        var coordinator = new ConnectionSettings(store, () => restarts++, null, () => { closed = true; return Task.CompletedTask; },
            (_, message) => closedError = message, error => cause = error);
        await coordinator.ChangeAsync(new Uri("https://neu/"), null, message => formError = message);
        Assert.Equal(0, restarts);
        Assert.Same(store.Failure, cause);
        Assert.Equal(phase != "Vorbereitung", closed);
        Assert.Equal(phase == "Bestätigung" ? "https://neu/" : Original().ServerAddress, store.Load().ServerAddress);
        Assert.Contains(phase == "Vorbereitung" ? "vorbereitet" : "nicht bestätigt", formError ?? closedError, StringComparison.Ordinal);
        Assert.Equal(closed, closedError is not null);
    }

    [Fact]
    public async Task Gemeinsamer_Widerrufs_und_Abbaufehler_bleibt_als_Ursache_der_gesperrten_Karte_erhalten()
    {
        var store = new MemorySettingsStore();
        store.Save(Original());
        var failures = new AggregateException(new IOException("Widerruf"), new IOException("Abbau"));
        var restarts = 0;
        string? notice = null;
        Exception? cause = null;
        var coordinator = new ConnectionSettings(store, () => restarts++, null, () => Task.FromException(failures),
            (_, message) => notice = message, error => cause = error);
        await coordinator.ChangeAsync(null, null, _ => { });
        Assert.Same(failures, cause);
        Assert.Contains("Anmeldung", notice, StringComparison.Ordinal);
        Assert.Equal(Original().ServerAddress, store.Load().ServerAddress);
        Assert.Equal(0, restarts);
    }

    [Fact]
    public async Task Gemeinsamer_Wechsel_wartet_auf_den_Abbau_und_sperrt_die_zweite_Karte()
    {
        var store = new MemorySettingsStore();
        store.Save(Original());
        var teardown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restarts = 0;
        var coordinator = new ConnectionSettings(store, () => restarts++, null, () => teardown.Task, null, null);
        var first = coordinator.ChangeAsync(new Uri("https://neu/"), null, _ => { });
        string? error = null;
        await coordinator.ChangeAsync(null, null, message => error = message);
        Assert.Contains("bereits", error, StringComparison.Ordinal);
        Assert.Equal(Original().ServerAddress, store.Load().ServerAddress);
        Assert.Equal(0, restarts);
        teardown.SetResult();
        await first;
        Assert.Equal("https://neu/", store.Load().ServerAddress);
        Assert.Equal(1, restarts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Serverende_sperrt_laufende_und_angestellte_Anmelde_und_Geraeteantworten(bool loginFirst)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var user = TestData.Reception();
        using var server = new StubApi().OnSignIn("POST /api/auth/dev/login", user)
            .On("POST /api/auth/shared-device", QuickSwitchTests.Device)
            .Hold(loginFirst ? "POST /api/auth/dev/login" : "POST /api/auth/shared-device", gate.Task);
        var device = new MemorySessionStore();
        device.Save(Guid.NewGuid().ToString("N"));
        var person = new MemorySessionStore();
        person.Save(Guid.NewGuid().ToString("N"));
        var disk = new MemoryOfflineStore();
        var cache = new OfflineCache(disk) { SharedDevice = QuickSwitchTests.Device };
        var api = new ApiClient(new Uri("http://localhost"), person, server, cache, device);
        var registration = new AispaStudio.Contracts.SharedDeviceInput("Empfang", TestData.LocationId, 10);
        var ct = TestContext.Current.CancellationToken;
        var first = loginFirst ? (Task)api.SignInAsTestAccountAsync("test", ct) : api.RegisterDeviceAsync(registration, ct);
        Assert.Single(server.Calls);
        var queuedLogin = api.SignInAsTestAccountAsync("test", ct);
        var queuedDevice = api.RegisterDeviceAsync(registration, ct);
        api.ForgetSignIn();
        gate.SetResult();
        await Task.WhenAll(first, queuedLogin, queuedDevice);

        Assert.Single(server.Calls);
        Assert.False((await queuedLogin).Ok);
        Assert.False((await queuedDevice).Ok);
        Assert.False(api.HasDeviceCookie);
        Assert.Null(device.Load());
        Assert.Null(api.SessionCookie);
        Assert.Null(person.Load());
        Assert.Null(new OfflineCache(disk).Get("api/auth/me"));
        Assert.Null(new OfflineCache(disk).SharedDevice);
    }

    [Fact]
    public async Task Neu_bestaetigte_Person_startet_offline_ohne_das_Empfangsgeraet_des_bisherigen_Servers()
    {
        var device = new MemorySessionStore();
        device.Save(Guid.NewGuid().ToString("N"));
        var person = new MemorySessionStore();
        var disk = new MemoryOfflineStore();
        var old = new OfflineCache(disk) { SharedDevice = QuickSwitchTests.Device };
        using var server = new StubApi().OnSignIn("POST /api/auth/dev/login", TestData.Reception() with { Id = "neu" });
        var prior = new ApiClient(new Uri("https://bisher/"), person, server, old, device);
        var settings = new MemorySettingsStore();
        settings.Save(Original());
        var connect = new ServerConnectViewModel(settings, () => { }, (_, _) => Task.FromResult<string?>(null), prior.ForgetSignIn)
        {
            Address = "https://localhost/",
        };
        await connect.ConnectCommand.ExecuteAsync(null);
        Assert.Null(connect.Error);
        var next = new ApiClient(ServerChoice.Resolve(settings.Load(), null).Address, person, server, new OfflineCache(disk), device);
        Assert.True((await next.SignInAsTestAccountAsync("test", TestContext.Current.CancellationToken)).Ok);
        server.Down = true;
        var offline = new ApiClient(next.BaseAddress, person, server, new OfflineCache(disk), device);
        var main = new MainViewModel(offline) { ProbeWindow = TimeSpan.Zero };
        await main.StartAsync();
        Assert.True(main.IsSignedIn && main.CanShowContent);
        Assert.Equal("neu", main.User?.Id);
        Assert.False(offline.HasDeviceCookie);
        Assert.Null(main.Device);
        Assert.Null(new OfflineCache(disk).SharedDevice);
    }

    [Fact]
    public async Task Personenabmeldung_am_selben_Server_behaelt_Geraet_und_Standort()
    {
        using var server = new StubApi().On("POST /api/auth/logout", null);
        var device = new MemorySessionStore();
        device.Save(Guid.NewGuid().ToString("N"));
        var disk = new MemoryOfflineStore();
        var cache = new OfflineCache(disk) { SharedDevice = QuickSwitchTests.Device };
        var api = new ApiClient(new Uri("http://localhost"), new MemorySessionStore(), server, cache, device);
        await api.SignOutAsync(TestContext.Current.CancellationToken);
        Assert.True(api.HasDeviceCookie && device.Load() is not null);
        Assert.Equal(QuickSwitchTests.Device, new OfflineCache(disk).SharedDevice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Vorbereitungsfehler_erscheint_auf_der_aktiven_Verbindungskarte(bool inShell) => ViewTests.Session.Dispatch(async () =>
    {
        var store = new FaultStore("Vorbereitung");
        var shell = new ServerLinkViewModel(store, ServerChoice.Resolve(Original(), null), () => { });
        var start = new ServerConnectViewModel(store, () => { }, (_, _) => Task.FromResult<string?>(null)) { Address = "https://neu/" };
        var window = new Window { Content = inShell ? new ServerLinkView { DataContext = shell } : new ServerConnectView { DataContext = start } };
        try
        {
            window.Show();
            await (inShell ? shell.ReleaseCommand : start.ConnectCommand).ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(window.GetVisualDescendants().OfType<InfoBar>(), bar =>
                bar.IsEffectivelyVisible && bar.Message?.Contains("vorbereitet", StringComparison.Ordinal) == true);
        }
        finally
        {
            window.Close();
        }

        return true;
    }, TestContext.Current.CancellationToken);
}
