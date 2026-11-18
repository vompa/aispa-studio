using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Beispiel-Spa in der eigenen Datenbank (ADR 0100): Schalter <c>--demo</c>, Anmeldehinweis, Leiste mit Löschen, keine Sicherung.</summary>
public sealed class DemoSpaTests
{
    private static readonly SystemModules DemoStandalone = new(true, false, false, false, false, false, false, false, Demo: true);
    private static readonly SystemModules DemoServer = new(false, true, true, true, true, true, true, true, Demo: true);

    /// <summary>Einzelplatz-Server ohne Prozesse; der Test liest nur, wie das Beispiel-Spa die Datenbank und das Backup betrifft.</summary>
    private sealed class FakeLocal : ILocalServer
    {
        public NetworkShareInfo? Network => null;

        public Uri ApiAddress { get; } = new("http://127.0.0.1:55086/");

        public string? CentralDescription => null;

        public bool HasRecoveryKey => false;

        public string? ShareCode => null;

        public int Backups { get; private set; }

        public Task<bool> SetSharedAsync(bool share, CancellationToken ct = default) => Task.FromResult(false);

        public Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task<bool> SetCentralAsync(string? connectionString, CancellationToken ct = default) => Task.FromResult(true);

        public string SetShareCode(string? code) => code ?? "";

        public string CreateRecoveryKey() => "ABCD-EFGH-JKMN-PQRS-TUVW-XYZ2";

        public Task<string> BackupAsync(string folder, CancellationToken ct = default)
        {
            Backups++;
            return Task.FromResult(Path.Combine(folder, "aispa.dump"));
        }

        public Task RestoreAsync(string dumpFile, string recoveryCode, IProgress<string>? progress = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ShellViewModel Shell(StubApi api, CurrentUser user, SystemModules modules, FakeDialogs dialogs, Action? closeApp = null, bool local = true) =>
        new(api.Client(), user, null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask,
            modules: modules, local: local ? new FakeLocal() : null, dialogs: dialogs, closeApp: closeApp);

    [Fact]
    public void Schalter_demo_schaltet_den_Einzelplatz_nur_fuer_diesen_Start_ein_und_merkt_nichts()
    {
        var settings = new MemorySettingsStore();

        Assert.True(StandaloneMode.IsDemo(["--DEMO"]));
        Assert.False(StandaloneMode.IsDemo(["--standalone"]));
        Assert.True(StandaloneMode.Resolve(["--demo"], settings));
        Assert.False(settings.Load().Standalone);
        Assert.False(StandaloneMode.Resolve([], settings));

        // Auch gegen die gemerkte Betriebsart „Server“ und gegen „--server“: der Schalter gewinnt, gespeichert wird nichts.
        Assert.True(StandaloneMode.Resolve(["--server", "--demo"], settings));
        Assert.False(settings.Load().Standalone);

        // Die gemerkte Betriebsart bleibt bestehen: wer vorher Einzelplatz hatte, hat ihn danach noch.
        Assert.True(StandaloneMode.Resolve(["--standalone"], settings));
        Assert.True(StandaloneMode.Resolve(["--demo"], settings));
        Assert.True(StandaloneMode.Resolve([], settings));
    }

    [Fact]
    public async Task Anmeldemaske_zeigt_im_Beispiel_Spa_den_Hinweis_und_nur_im_Einzelplatz()
    {
        var status = new SystemStatus("1", "Production", DateTimeOffset.UtcNow, new StartupInfo(StartupPhase.Ready, "Bereit"));
        var standalone = new LoginViewModel(new StubApi().On("GET /api/system/modules", DemoStandalone).On("GET /api/system/status", status)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(false)).Client(), _ => Task.CompletedTask);
        var server = new LoginViewModel(new StubApi().On("GET /api/system/modules", DemoServer).On("GET /api/system/status", status).Client(),
            _ => Task.CompletedTask);
        var plain = new LoginViewModel(new StubApi().On("GET /api/system/modules", DemoStandalone with { Demo = false }).On("GET /api/system/status", status)
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(false)).Client(), _ => Task.CompletedTask);

        await standalone.LoadAsync();
        await server.LoadAsync();
        await plain.LoadAsync();

        Assert.True(standalone.HasDemoHint);
        Assert.Equal("Beispiel-Spa: Anmelden z. B. mit inhaberin@demo.aispa.test, Passwort demo", LoginViewModel.DemoHint);
        Assert.False(server.HasDemoHint);
        Assert.False(plain.HasDemoHint);
    }

    [Fact]
    public async Task Im_Beispiel_Spa_zeigt_die_Anmeldung_das_Einspielen_auch_in_Production()
    {
        var api = new StubApi().On("GET /api/system/modules", DemoStandalone)
            .On("GET /api/system/status", new SystemStatus("1", "Production", DateTimeOffset.UtcNow,
                new StartupInfo(StartupPhase.Seeding, "Beispiel-Spa wird eingerichtet (seit 12 s)")))
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(false));
        var login = new LoginViewModel(api.Client(), _ => Task.CompletedTask);

        await login.LoadAsync();

        Assert.Equal("Beispiel-Spa wird eingerichtet (seit 12 s)", login.StartupNotice);
        Assert.Equal("Beispiel-Spa wird eingerichtet", login.StartupTitle);
    }

    [Fact]
    public void Leiste_steht_nur_im_Beispiel_Spa_und_nur_mit_dem_Recht_den_Standort_zu_verwalten()
    {
        var api = new StubApi();

        Assert.True(Shell(api, TestData.Owner(), DemoStandalone, new FakeDialogs()).ShowDemoBar);
        Assert.False(Shell(api, TestData.Reception(), DemoStandalone, new FakeDialogs()).ShowDemoBar);
        Assert.False(Shell(api, TestData.Owner(), DemoStandalone with { Demo = false }, new FakeDialogs()).ShowDemoBar);
        Assert.Equal("Sie sehen Beispieldaten.", ShellViewModel.DemoBarText);
    }

    [Fact]
    public async Task Abbrechen_in_der_Rueckfrage_loescht_nichts_und_schliesst_die_App_nicht()
    {
        var api = new StubApi().On("DELETE /api/system/demo", null, HttpStatusCode.Accepted);
        var dialogs = new FakeDialogs();
        var closed = 0;
        var shell = Shell(api, TestData.Owner(), DemoStandalone, dialogs, () => closed++);

        await shell.DeleteDemoCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Asked);
        dialogs.AssertRules();
        Assert.Equal("Beispieldaten löschen?", dialogs.Asked[0].Title);
        Assert.Equal("Löschen", dialogs.Asked[0].ConfirmText);
        Assert.StartsWith("Das Beispiel-Spa mit allen Terminen, Gästen und Belegen ist danach gelöscht", dialogs.Asked[0].Message, StringComparison.Ordinal);
        Assert.Empty(api.Calls);
        Assert.Equal(0, closed);
    }

    [Fact]
    public async Task Im_Einzelplatz_loescht_die_Bestaetigung_die_Beispieldaten_und_schliesst_die_App()
    {
        var api = new StubApi().On("DELETE /api/system/demo", null, HttpStatusCode.Accepted);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var closed = 0;
        var shell = Shell(api, TestData.Owner(), DemoStandalone, dialogs, () => closed++);

        await shell.DeleteDemoCommand.ExecuteAsync(null);

        Assert.Equal(["DELETE /api/system/demo"], api.Calls.Select(c => c.Route));
        Assert.Equal(1, closed);
        Assert.Single(dialogs.Asked);
        Assert.EndsWith("Die App schließt sich danach.", dialogs.Asked[0].Message, StringComparison.Ordinal);
        Assert.Null(shell.Toast);
    }

    [Fact]
    public async Task Am_Server_nennt_die_Meldung_nach_dem_Loeschen_den_Weg_und_die_App_bleibt_offen()
    {
        var api = new StubApi().On("DELETE /api/system/demo", null, HttpStatusCode.Accepted);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var closed = 0;
        var shell = Shell(api, TestData.Owner(), DemoServer, dialogs, () => closed++, local: false);

        await shell.DeleteDemoCommand.ExecuteAsync(null);

        Assert.Equal(0, closed);
        Assert.Equal(2, dialogs.Asked.Count);
        Assert.Contains("AISPA_DEMO entfernen", dialogs.Asked[1].Message, StringComparison.Ordinal);
        Assert.Contains("sonst legt er neue Beispieldaten an", dialogs.Asked[1].Message, StringComparison.Ordinal);
        Assert.Equal(0, dialogs.Asked[1].ConfirmText.Length);
    }

    [Fact]
    public async Task Scheitert_das_Loeschen_bleibt_die_App_offen_und_eine_Meldung_steht_unten()
    {
        var api = new StubApi().On("DELETE /api/system/demo", null, HttpStatusCode.NotFound);
        var closed = 0;
        var shell = Shell(api, TestData.Owner(), DemoStandalone, new FakeDialogs().Answer(ConfirmChoice.Confirmed), () => closed++);

        await shell.DeleteDemoCommand.ExecuteAsync(null);

        Assert.Equal(0, closed);
        Assert.StartsWith("Die Beispieldaten ließen sich nicht löschen.", shell.Toast, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_das_Recht_fragt_Loeschen_nicht_einmal_nach()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var shell = Shell(new StubApi(), TestData.Reception(), DemoStandalone, dialogs);

        await shell.DeleteDemoCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Asked);
    }

    [Fact]
    public async Task Sicherung_im_Beispiel_Spa_sichert_nicht_und_erinnert_nicht()
    {
        var local = new FakeLocal();
        var settings = new MemorySettingsStore();
        settings.Save(settings.Load() with { BackupFolder = Path.Combine(Path.GetTempPath(), "aispa-demo-sicherung") });
        var backup = new BackupViewModel(local, settings, TimeProvider.System) { CanConfigure = true, IsDemo = true };

        await backup.RunIfDueAsync();

        Assert.False(backup.HasReminder);
        Assert.False(backup.BackupNowCommand.CanExecute(null));
        Assert.False(backup.SaveFolderCommand.CanExecute(null));
        Assert.False(backup.ShowFolderSettings);
        Assert.Equal("Im Beispiel-Spa wird nicht gesichert.", BackupViewModel.DemoText);
        Assert.Equal(BackupViewModel.DemoText, backup.BackupNowHint);
        Assert.Equal(0, local.Backups);
    }
}
