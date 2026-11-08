using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Abgleich in der Windows-App (ADR 0041, Schritt 4): Stand, abgelehnte Zeilen und die zentrale Datenbank eintragen.</summary>
public sealed class SyncViewTests
{
    private sealed class FakeLocal : ILocalServer
    {
        public NetworkShareInfo? Network { get; set; }

        public Task<bool> SetSharedAsync(bool share, CancellationToken ct = default) => Task.FromResult(false);

        public List<string?> Applied { get; } = [];

        public Uri ApiAddress { get; } = new("http://127.0.0.1:55086/");

        public string? CentralDescription { get; private set; }

        public string? ShareCode { get; private set; }

        public string SetShareCode(string? code)
        {
            if (code is not null && !KeySeal.IsComplete(code))
            {
                throw new LocalServerException("Der Schlüsselcode des Spas hat 24 Zeichen.");
            }

            ShareCode = code ?? "NEUE-STEL-LECO-DE23-4567-89AB";
            return ShareCode;
        }

        public Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task RestoreAsync(string dumpFile, string recoveryCode, IProgress<string>? progress = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public bool HasRecoveryKey => false;

        public string CreateRecoveryKey() => "";

        public Task<string> BackupAsync(string folder, CancellationToken ct = default) => Task.FromResult("");

        public Task<bool> SetCentralAsync(string? connectionString, CancellationToken ct = default)
        {
            if (connectionString == "kaputt")
            {
                throw new LocalServerException("Die Verbindungsangabe ist nicht lesbar.");
            }

            Applied.Add(connectionString);
            CentralDescription = connectionString is null ? null : "db.spa.at · aispadb";
            return Task.FromResult(true);
        }
    }

    private static readonly DateTimeOffset Detected = new(2026, 10, 19, 8, 30, 0, TimeSpan.Zero);

    private static SyncStatus Status(params SyncConflict[] conflicts) =>
        new(true, Detected.AddMinutes(5), null, 3, conflicts);

    private static CurrentUser Owner() => TestData.Reception() with
    {
        Rights = [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ManageLocation],
    };

    [Fact]
    public async Task Stand_und_abgelehnte_Zeilen_mit_Zahl_in_der_Navigation()
    {
        var conflict = new SyncConflict(7, "Termine", "Appointments", "{\"Id\": \"x\"}",
            "Zentral abgelehnt: Raum, Person oder Gerät ist zur selben Zeit schon gebucht.", Detected);
        var api = new StubApi()
            .On("GET /api/system/sync", Status(conflict))
            .On("POST /api/system/sync/conflicts/7/resolve", null, HttpStatusCode.NoContent);
        var shell = new ShellViewModel(api.Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()),
            "", () => Task.CompletedTask, local: new FakeLocal());

        var nav = Assert.Single(shell.Pages, p => p.Title == "Abgleich");
        var sync = Assert.IsType<SyncViewModel>(nav.Page);
        await sync.RefreshAsync();

        Assert.Equal("1", nav.Badge);
        Assert.True(sync.HasConflicts);
        Assert.Equal("Termine", sync.Conflicts[0].Area);
        Assert.Equal("3 Änderung(en) warten auf den nächsten Abgleich.", sync.PendingText);
        Assert.StartsWith("Zuletzt abgeglichen", sync.StateText, StringComparison.Ordinal);
        Assert.False(sync.CanConfigure);
        Assert.False(sync.ConnectCommand.CanExecute(null));

        api.On("GET /api/system/sync", Status());
        await sync.ResolveCommand.ExecuteAsync(sync.Conflicts[0]);

        Assert.Contains(api.Calls, c => c.Route == "POST /api/system/sync/conflicts/7/resolve");
        Assert.Null(nav.Badge);
        Assert.False(sync.HasConflicts);
        shell.Dispose();
    }

    [Fact]
    public void Ohne_Einzelplatz_gibt_es_keinen_Abgleich_in_der_Navigation()
    {
        var shell = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);

        Assert.DoesNotContain(shell.Pages, p => p.Title == "Abgleich");
        Assert.Null(shell.Sync);
        shell.Dispose();
    }

    [Fact]
    public async Task Leitung_traegt_die_zentrale_Datenbank_ein_und_wieder_aus()
    {
        var api = new StubApi().On("GET /api/system/sync", new SyncStatus(false, null, null, 0, []));
        var local = new FakeLocal();
        var sync = new SyncViewModel(api.Client(), local, TimeProvider.System) { CanConfigure = true };
        await sync.RefreshAsync();
        Assert.False(sync.HasCentral);
        Assert.StartsWith("Keine zentrale Datenbank", sync.CentralText, StringComparison.Ordinal);

        sync.ConnectionInput = "kaputt";
        await sync.ConnectCommand.ExecuteAsync(null);
        Assert.Equal("Die Verbindungsangabe ist nicht lesbar.", sync.Message);
        Assert.Equal("kaputt", sync.ConnectionInput);

        // Erste Stelle: ohne Code entsteht einer und wird einmal gezeigt.
        Assert.Equal("NEUE-STEL-LECO-DE23-4567-89AB", sync.ShownCode);
        sync.CodeNotedCommand.Execute(null);
        Assert.False(sync.HasShownCode);

        api.On("GET /api/system/sync", new SyncStatus(true, null, null, 12, []));
        sync.ConnectionInput = "Host=db.spa.at;Database=aispadb;Username=gmunden;Password=geheim";
        sync.ShareCodeInput = "K7QM-2XRT";
        await sync.ConnectCommand.ExecuteAsync(null);
        Assert.Equal("Der Schlüsselcode des Spas hat 24 Zeichen.", sync.Message);
        Assert.Empty(local.Applied);

        // Der Code von vorhin bleibt; kein neuer, aber auf Wunsch noch einmal sichtbar für die nächste Stelle.
        sync.ShareCodeInput = "";
        await sync.ConnectCommand.ExecuteAsync(null);
        Assert.Null(sync.ShownCode);
        sync.ShowCodeCommand.Execute(null);
        Assert.Equal("NEUE-STEL-LECO-DE23-4567-89AB", sync.ShownCode);

        Assert.Equal(["Host=db.spa.at;Database=aispadb;Username=gmunden;Password=geheim"], local.Applied);
        Assert.True(sync.HasShareCode);
        Assert.Equal("", sync.ConnectionInput);
        Assert.True(sync.HasCentral);
        Assert.Equal("Zentrale Datenbank: db.spa.at · aispadb", sync.CentralText);
        Assert.StartsWith("Der erste Abgleich läuft gleich", sync.StateText, StringComparison.Ordinal);
        Assert.StartsWith("Gespeichert. Der Abgleich startet jetzt", sync.Message, StringComparison.Ordinal);

        await sync.DisconnectCommand.ExecuteAsync(null);
        Assert.Null(local.Applied[^1]);
        Assert.False(sync.HasCentral);
        Assert.StartsWith("Abgleich ausgeschaltet", sync.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Neuer_PC_verbindet_sich_mit_zentraler_Datenbank_und_Schluesselcode()
    {
        var local = new FakeLocal();
        var reloaded = 0;
        var join = new JoinViewModel(local, () =>
        {
            reloaded++;
            return Task.CompletedTask;
        });
        join.OpenCommand.Execute(null);
        Assert.True(join.IsOpen);

        join.ConnectionInput = "Host=db.spa.at;Database=aispadb;Username=ischl;Password=geheim";
        join.ShareCodeInput = "K7QM-2XRT";
        Assert.False(join.JoinCommand.CanExecute(null));

        join.ShareCodeInput = "k7qm 2xrt h9pw 4cne yb3d zf6a";
        await join.JoinCommand.ExecuteAsync(null);
        Assert.Equal("k7qm 2xrt h9pw 4cne yb3d zf6a", local.ShareCode);
        Assert.Equal(["Host=db.spa.at;Database=aispadb;Username=ischl;Password=geheim"], local.Applied);
        Assert.Equal(1, reloaded);
        Assert.Equal("", join.ShareCodeInput);

        join.ConnectionInput = "kaputt";
        join.ShareCodeInput = "K7QM-2XRT-H9PW-4CNE-YB3D-ZF6A";
        await join.JoinCommand.ExecuteAsync(null);
        Assert.Equal("Die Verbindungsangabe ist nicht lesbar.", join.ErrorText);
        Assert.Equal(1, reloaded);
    }

    [Fact]
    public void Leitung_darf_eintragen_Rezeption_nicht()
    {
        var owner = new ShellViewModel(new StubApi().Client(), Owner(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask, local: new FakeLocal());

        Assert.True(owner.Sync!.CanConfigure);
        Assert.True(owner.Sync.CanResolve);
        owner.Dispose();
    }
}
