using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Tägliche Sicherung im Einzelplatz (ADR 0041, Schritt 5): Ordner wählen, Wiederherstellungsschlüssel, täglich sichern, erinnern.</summary>
public sealed class BackupViewTests
{
    private sealed class FakeLocal : ILocalServer
    {
        public NetworkShareInfo? Network { get; set; }

        public Task<bool> SetSharedAsync(bool share, CancellationToken ct = default) => Task.FromResult(false);

        public List<string> Backups { get; } = [];

        public bool Fails { get; set; }

        public Uri ApiAddress { get; } = new("http://127.0.0.1:55086/");

        public string? CentralDescription => null;

        public bool HasRecoveryKey { get; private set; }

        public Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public List<(string Dump, string Code)> Restored { get; } = [];

        public Task RestoreAsync(string dumpFile, string recoveryCode, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            if (recoveryCode.StartsWith("FALS", StringComparison.Ordinal))
            {
                throw new LocalServerException("Der Wiederherstellungsschlüssel passt nicht zu dieser Sicherung.");
            }

            progress?.Report("Spiele die Sicherung ein …");
            Restored.Add((dumpFile, recoveryCode));
            return Task.CompletedTask;
        }

        public Task<bool> SetCentralAsync(string? connectionString, CancellationToken ct = default) => Task.FromResult(true);

        public string? ShareCode => null;

        public string SetShareCode(string? code) => code ?? "";

        public string CreateRecoveryKey()
        {
            HasRecoveryKey = true;
            return "ABCD-EFGH-JKMN-PQRS-TUVW-XYZ2";
        }

        public Task<string> BackupAsync(string folder, CancellationToken ct = default)
        {
            if (Fails)
            {
                throw new LocalServerException($"Der Ordner für die Sicherung ist nicht erreichbar ({folder}).");
            }

            Backups.Add(folder);
            return Task.FromResult(Path.Combine(folder, $"aispa-{Backups.Count}.dump"));
        }
    }

    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "aispa-sicherung");

    private sealed class FakeScheduler(string? error = null) : IBackupScheduler
    {
        public int Registered { get; private set; }

        public Task<string?> RegisterAsync(CancellationToken ct = default)
        {
            Registered++;
            return Task.FromResult(error);
        }
    }

    [Fact]
    public async Task Mit_dem_Ordner_entsteht_die_Windows_Aufgabe_auch_fuer_schon_gewaehlte_Ordner()
    {
        var local = new FakeLocal();
        var time = new ManualTime(new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero));
        var scheduler = new FakeScheduler();
        var backup = new BackupViewModel(local, new MemorySettingsStore(), time, scheduler) { CanConfigure = true, FolderInput = Folder };
        await backup.SaveFolderCommand.ExecuteAsync(null);
        Assert.Equal(1, scheduler.Registered);
        Assert.StartsWith("Auch wenn die App geschlossen ist, sichert Windows jeden Abend um 23:00 Uhr", backup.ScheduleText, StringComparison.Ordinal);

        // Ordner aus einer älteren Version: die Aufgabe kommt beim ersten Lauf im Hintergrund dazu, einmal.
        var settings = new MemorySettingsStore();
        settings.Save(new DesktopSettings(BackupFolder: Folder, LastBackupAt: time.GetUtcNow()));
        var failing = new FakeScheduler("Zugriff verweigert");
        var older = new BackupViewModel(local, settings, time, failing);
        await older.RunIfDueAsync();
        await older.RunIfDueAsync();
        Assert.Equal(1, failing.Registered);
        Assert.Contains("ließ sich nicht anlegen (Zugriff verweigert)", older.ScheduleText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leitung_waehlt_den_Ordner_bekommt_den_Schluessel_einmal_und_es_wird_gleich_gesichert()
    {
        var local = new FakeLocal();
        var settings = new MemorySettingsStore();
        var time = new ManualTime(new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero));
        var backup = new BackupViewModel(local, settings, time) { CanConfigure = true };
        Assert.True(backup.HasReminder);
        Assert.StartsWith("Noch keine tägliche Sicherung", backup.ReminderText, StringComparison.Ordinal);

        backup.FolderInput = "sicherung";
        await backup.SaveFolderCommand.ExecuteAsync(null);
        Assert.StartsWith("Bitte den vollständigen Pfad", backup.ErrorText, StringComparison.Ordinal);
        Assert.Empty(local.Backups);

        backup.FolderInput = Folder;
        await backup.SaveFolderCommand.ExecuteAsync(null);

        Assert.Equal("ABCD-EFGH-JKMN-PQRS-TUVW-XYZ2", backup.NewCode);
        Assert.Equal([Folder], local.Backups);
        Assert.Null(backup.ErrorText);
        Assert.False(backup.HasReminder);
        Assert.Equal("Letzte Sicherung: 19.10.2026 08:00.", backup.LastBackupText);
        Assert.Equal(Folder, settings.Load().BackupFolder);
        Assert.Equal(time.GetUtcNow(), settings.Load().LastBackupAt);

        backup.CodeNotedCommand.Execute(null);
        Assert.False(backup.HasNewCode);

        // Zweiter Ordner: der Schlüssel bleibt derselbe, es kommt kein neuer Code.
        backup.FolderInput = Folder + "2";
        await backup.SaveFolderCommand.ExecuteAsync(null);
        Assert.Null(backup.NewCode);
    }

    [Fact]
    public async Task Einmal_am_Tag_nach_Fehler_erst_nach_30_Minuten_wieder_und_Erinnerung_nach_sieben_Tagen()
    {
        var local = new FakeLocal();
        local.CreateRecoveryKey();
        var start = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);
        var settings = new MemorySettingsStore();
        settings.Save(new DesktopSettings(BackupFolder: Folder, LastBackupAt: start));
        var time = new ManualTime(start.AddHours(23));
        var backup = new BackupViewModel(local, settings, time);

        await backup.RunIfDueAsync();
        Assert.Empty(local.Backups);

        time.Advance(TimeSpan.FromHours(1));
        await backup.RunIfDueAsync();
        Assert.Single(local.Backups);
        await backup.RunIfDueAsync();
        Assert.Single(local.Backups);

        // Laufwerk ab: nicht alle 15 Sekunden neu versuchen, nach sieben Tagen erinnern.
        local.Fails = true;
        time.Advance(TimeSpan.FromDays(1));
        await backup.RunIfDueAsync();
        Assert.Contains("nicht erreichbar", backup.ErrorText, StringComparison.Ordinal);
        time.Advance(TimeSpan.FromMinutes(10));
        await backup.RunIfDueAsync();
        Assert.Single(local.Backups);
        Assert.False(backup.HasReminder);

        time.Advance(TimeSpan.FromDays(7));
        await backup.RunIfDueAsync();
        Assert.True(backup.HasReminder);
        Assert.Contains("älter als sieben Tage", backup.ReminderText, StringComparison.Ordinal);

        local.Fails = false;
        time.Advance(TimeSpan.FromMinutes(30));
        await backup.RunIfDueAsync();
        Assert.Equal(2, local.Backups.Count);
        Assert.False(backup.HasReminder);
        Assert.Null(backup.ErrorText);
    }

    [Fact]
    public void Nur_im_Einzelplatz_und_nur_die_Leitung_waehlt_den_Ordner()
    {
        var reception = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask, local: new FakeLocal());
        Assert.Contains(reception.Pages, p => p.Title == "Sicherung");
        Assert.False(reception.Backup!.CanConfigure);
        reception.Dispose();

        var server = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask);
        Assert.Null(server.Backup);
        Assert.DoesNotContain(server.Pages, p => p.Title == "Sicherung");
        server.Dispose();

        var owner = TestData.Reception() with { Rights = [AccessRight.ViewCalendar, AccessRight.ManageLocation] };
        var shell = new ShellViewModel(new StubApi().Client(), owner, null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask, local: new FakeLocal());
        Assert.True(shell.Backup!.CanConfigure);
        shell.Dispose();
    }

    [Fact]
    public void Sicherungs_Banner_steht_ohne_Sicherung_und_Sicherung_oeffnen_waehlt_die_Seite()
    {
        var shell = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System,
            new ThemeController(new MemorySettingsStore()), "", () => Task.CompletedTask, local: new FakeLocal());
        Assert.True(shell.Banners.Backup);
        Assert.NotEqual("Sicherung", shell.SelectedPage?.Title);

        shell.OpenBackupCommand.Execute(null);

        Assert.Equal("Sicherung", shell.SelectedPage?.Title);
        shell.Dispose();
    }

    [Fact]
    public async Task Wiederherstellen_braucht_Sicherung_und_vollen_Schluessel_und_verbindet_danach_neu()
    {
        var local = new FakeLocal();
        var reconnected = 0;
        var restore = new RestoreViewModel(local, () =>
        {
            reconnected++;
            return Task.CompletedTask;
        });
        Assert.False(restore.IsOpen);
        restore.OpenCommand.Execute(null);
        Assert.True(restore.IsOpen);

        restore.DumpFile = "\"D:\\Sicherung\\aispa-20261019-080000.dump\"";
        restore.Code = "K7QM-2XRT-H9PW";
        Assert.False(restore.RestoreCommand.CanExecute(null));

        restore.Code = "FALS-2XRT-H9PW-4CNE-YB3D-ZF6A";
        await restore.RestoreCommand.ExecuteAsync(null);
        Assert.Equal("Der Wiederherstellungsschlüssel passt nicht zu dieser Sicherung.", restore.ErrorText);
        Assert.Equal(0, reconnected);

        restore.Code = "k7qm 2xrt h9pw 4cne yb3d zf6a";
        await restore.RestoreCommand.ExecuteAsync(null);
        Assert.Equal([("D:\\Sicherung\\aispa-20261019-080000.dump", "k7qm 2xrt h9pw 4cne yb3d zf6a")], local.Restored);
        Assert.Null(restore.ErrorText);
        Assert.Equal("", restore.Code);
        Assert.Equal(1, reconnected);
    }
}
