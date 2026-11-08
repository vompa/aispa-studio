using System.Text;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>Einzelplatz-Betrieb (ADR 0041): Die App legt Datenbank und API auf diesem PC an und startet sie.</summary>
public sealed class LocalServerTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aispa-local-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LocalServerPaths Paths => new(Path.Combine(_root.FullName, "app"), Path.Combine(_root.FullName, "daten"));

    public void Dispose() => _root.Delete(recursive: true);

    private void InstallPackage()
    {
        foreach (var file in new[] { Paths.Tool("pg_ctl"), Paths.Tool("initdb"), Paths.Tool("psql"), Paths.Tool("createdb"), Paths.Api })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "");
        }
    }

    [Fact]
    public async Task Erster_Start_legt_Datenbank_an_startet_sie_und_dann_die_API()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths);
        var ready = 0;
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted && ++ready > 1));
        var steps = new List<string>();

        await server.StartAsync(new SyncProgress(steps), Ct);

        Assert.Equal(["initdb", "pg_ctl status", "pg_ctl start", "psql", "createdb", "api"], runner.Calls);
        Assert.Equal(["Lege die lokale Datenbank an …", "Starte die lokale Datenbank …", "Starte AISPA Studio auf diesem PC …"], steps);
        Assert.Equal(new Uri($"http://127.0.0.1:{server.Config.ApiPort}/"), server.ApiAddress);
        Assert.False(File.Exists(Path.Combine(Paths.DataDirectory, "initdb.tmp")));

        var env = runner.ApiEnvironment!;
        Assert.Equal("true", env["Hosting__Standalone"]);
        Assert.Equal(Paths.WebRoot, env["Hosting__WebRoot"]);
        Assert.Equal($"http://127.0.0.1:{server.Config.ApiPort}", env["ASPNETCORE_URLS"]);
        Assert.Contains($"Port={server.Config.DatabasePort};Database=aispadb;Username=aispa_app;Password=", env["ConnectionStrings__aispadb"], StringComparison.Ordinal);
        var keyId = env["HealthData__CurrentKeyId"];
        Assert.Matches("^lokal-[0-9a-f]{12}$", keyId);
        Assert.Equal(32, Convert.FromBase64String(env[$"HealthData__Keys__{keyId}"]).Length);
        Assert.Contains($"-p {server.Config.DatabasePort} -c listen_addresses=127.0.0.1", runner.StartArgs, StringComparison.Ordinal);
        Assert.Equal(runner.InitPassword, runner.PsqlPassword);
    }

    [Fact]
    public async Task Zweiter_Start_behaelt_Ports_Passwort_und_Schluessel_und_legt_nichts_neu_an()
    {
        InstallPackage();
        var first = new FakeRunner(Paths);
        var server = new LocalServer(Paths, first, new PlainProtector(), (_, _) => Task.FromResult(first.ApiStarted));
        await server.StartAsync(ct: Ct);

        var again = new FakeRunner(Paths) { DatabaseRunning = true, DatabaseExists = true };
        var restarted = new LocalServer(Paths, again, new PlainProtector(), (_, _) => Task.FromResult(again.ApiStarted));
        await restarted.StartAsync(ct: Ct);

        Assert.Equal(server.Config, restarted.Config);
        Assert.Equal(["pg_ctl status", "psql", "api"], again.Calls);
        Assert.Equal(first.ApiEnvironment!["ConnectionStrings__aispadb"], again.ApiEnvironment!["ConnectionStrings__aispadb"]);
        Assert.Equal(first.ApiEnvironment["HealthData__CurrentKeyId"], again.ApiEnvironment["HealthData__CurrentKeyId"]);
        var keyId = first.ApiEnvironment["HealthData__CurrentKeyId"];
        Assert.Equal(first.ApiEnvironment[$"HealthData__Keys__{keyId}"], again.ApiEnvironment[$"HealthData__Keys__{keyId}"]);
    }

    [Fact]
    public async Task Laeuft_die_API_schon_wird_sie_nicht_doppelt_gestartet()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths) { DatabaseRunning = true, DatabaseExists = true };
        Directory.CreateDirectory(Paths.Cluster);
        File.WriteAllText(Path.Combine(Paths.Cluster, "PG_VERSION"), "17");
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(true));

        await server.StartAsync(ct: Ct);

        Assert.Equal(["pg_ctl status", "psql"], runner.Calls);
    }

    [Fact]
    public async Task Fehlt_das_Paket_erklaert_die_App_was_fehlt()
    {
        var server = new LocalServer(Paths, new FakeRunner(Paths), new PlainProtector(), (_, _) => Task.FromResult(false));

        var error = await Assert.ThrowsAsync<LocalServerException>(() => server.StartAsync(ct: Ct));

        Assert.Contains("vollständige Windows-Paket", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startet_die_API_nicht_zeigt_die_App_das_Protokoll()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths) { ApiExitsImmediately = true };
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(false), TimeSpan.FromSeconds(5));

        var error = await Assert.ThrowsAsync<LocalServerException>(() => server.StartAsync(ct: Ct));

        Assert.Contains("api.log", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unlesbare_Zugangsdaten_werden_nie_still_ersetzt()
    {
        InstallPackage();
        Directory.CreateDirectory(Paths.DataDirectory);
        File.WriteAllBytes(Paths.Secrets, Encoding.UTF8.GetBytes("kaputt"));
        var server = new LocalServer(Paths, new FakeRunner(Paths), new PlainProtector(), (_, _) => Task.FromResult(false));

        var error = await Assert.ThrowsAsync<LocalServerException>(() => server.StartAsync(ct: Ct));

        Assert.Contains("Sicherung", error.Message, StringComparison.Ordinal);
        Assert.Equal("kaputt", File.ReadAllText(Paths.Secrets));
    }

    [Fact]
    public async Task Beenden_stoppt_API_und_Datenbank()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths);
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);
        File.WriteAllText(Path.Combine(Paths.Cluster, "postmaster.pid"), "1");

        await server.StopAsync();

        // Geordnet über die Stopp-Datei, nicht hart; danach ist die Datei wieder weg.
        Assert.True(runner.ApiStoppedGracefully);
        Assert.False(runner.ApiKilled);
        Assert.False(File.Exists(Paths.StopFile));
        Assert.Equal(Paths.StopFile, runner.ApiEnvironment!["Hosting__StopFile"]);
        Assert.Equal("pg_ctl stop", runner.Calls[^1]);
    }

    [Fact]
    public async Task API_die_nicht_auf_die_Stopp_Datei_reagiert_wird_nach_der_Frist_hart_beendet()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths) { ApiIgnoresStop = true };
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted),
            stopTimeout: TimeSpan.FromMilliseconds(300));
        await server.StartAsync(ct: Ct);

        await server.StopAsync();

        Assert.True(runner.ApiKilled);
        Assert.False(File.Exists(Paths.StopFile));
    }

    [Fact]
    public async Task Zentrale_Datenbank_wird_geschuetzt_gemerkt_und_die_API_neu_gestartet()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths);
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);
        var password = runner.ApiEnvironment!["ConnectionStrings__aispadb"];
        Assert.False(runner.ApiEnvironment.ContainsKey("Sync__CentralConnectionString"));
        Assert.Null(server.CentralDescription);

        const string Central = "Host=db.spa-am-see.at;Database=aispadb;Username=gmunden;Password=geheim;SSL Mode=Require";
        Assert.Throws<LocalServerException>(() => server.SetShareCode("K7QM-2XRT"));
        Assert.Null(server.ShareCode);
        Assert.Equal("K7QM-2XRT-H9PW-4CNE-YB3D-ZF6A", server.SetShareCode("k7qm 2xrt h9pw 4cne yb3d zf6a"));
        Assert.True(await server.SetCentralAsync($"  {Central} ", Ct));

        Assert.Equal(["api", "api"], runner.Calls.Where(c => c == "api"));
        Assert.Equal(Central, runner.ApiEnvironment!["Sync__CentralConnectionString"]);
        Assert.Equal(password, runner.ApiEnvironment["ConnectionStrings__aispadb"]);
        Assert.Equal("K7QM-2XRT-H9PW-4CNE-YB3D-ZF6A", runner.ApiEnvironment["HealthData__ShareCode"]);
        Assert.Equal("db.spa-am-see.at · aispadb", server.CentralDescription);
        Assert.Contains("geheim", File.ReadAllText(Paths.Secrets), StringComparison.Ordinal);
        Assert.False(File.Exists(Paths.Secrets + ".neu"));

        // Nächster Start der App: die Angabe ist noch da.
        var again = new FakeRunner(Paths) { DatabaseRunning = true, DatabaseExists = true };
        var restarted = new LocalServer(Paths, again, new PlainProtector(), (_, _) => Task.FromResult(again.ApiStarted));
        await restarted.StartAsync(ct: Ct);
        Assert.Equal(Central, again.ApiEnvironment!["Sync__CentralConnectionString"]);
        Assert.Equal("K7QM-2XRT-H9PW-4CNE-YB3D-ZF6A", again.ApiEnvironment["HealthData__ShareCode"]);
        Assert.Equal(29, restarted.SetShareCode(null).Length);

        Assert.True(await restarted.SetCentralAsync(null, Ct));
        Assert.False(again.ApiEnvironment!.ContainsKey("Sync__CentralConnectionString"));
        Assert.Null(restarted.CentralDescription);
    }

    [Fact]
    public async Task Freigabe_im_Netz_startet_die_API_mit_HTTPS_und_behaelt_Zertifikat_und_Port()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths);
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);
        Assert.Null(server.Network);
        Assert.False(runner.ApiEnvironment!.ContainsKey("Kestrel__Endpoints__Netz__Url"));

        Assert.True(await server.SetSharedAsync(true, Ct));

        Assert.Equal(["api", "api"], runner.Calls.Where(c => c == "api"));
        var env = runner.ApiEnvironment!;
        var port = server.Config.NetworkPort;
        Assert.True(port > 0);
        Assert.False(env.ContainsKey("ASPNETCORE_URLS"));
        Assert.Equal($"http://127.0.0.1:{server.Config.ApiPort}", env["Kestrel__Endpoints__Lokal__Url"]);
        Assert.Equal($"https://*:{port}", env["Kestrel__Endpoints__Netz__Url"]);
        Assert.Equal(Paths.NetworkCertificate, env["Kestrel__Endpoints__Netz__Certificate__Path"]);
        using (var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
            Paths.NetworkCertificate, env["Kestrel__Endpoints__Netz__Certificate__Password"]))
        {
            Assert.True(certificate.HasPrivateKey);
            Assert.Equal(NetworkShare.Code(certificate), server.Network!.Code);
        }

        var code = server.Network.Code;
        Assert.Matches("^[0-9A-F]{4}(-[0-9A-F]{4}){4}$", code);
        Assert.Contains(new Uri($"https://{Environment.MachineName}:{port}/"), server.Network.Addresses);
        Assert.Contains(env["Kestrel__Endpoints__Netz__Certificate__Password"], File.ReadAllText(Paths.Secrets), StringComparison.Ordinal);

        // Zurücknehmen: wieder nur lokal. Erneut freigeben behält Zertifikat und Port, damit die anderen PCs nichts neu eintragen.
        Assert.True(await server.SetSharedAsync(false, Ct));
        Assert.Null(server.Network);
        Assert.Equal($"http://127.0.0.1:{server.Config.ApiPort}", runner.ApiEnvironment!["ASPNETCORE_URLS"]);
        Assert.False(runner.ApiEnvironment.ContainsKey("Kestrel__Endpoints__Netz__Url"));

        var again = new FakeRunner(Paths) { DatabaseRunning = true, DatabaseExists = true };
        var restarted = new LocalServer(Paths, again, new PlainProtector(), (_, _) => Task.FromResult(again.ApiStarted));
        Assert.False(await restarted.SetSharedAsync(true, Ct));
        await restarted.StartAsync(ct: Ct);
        Assert.Equal((port, code), (restarted.Config.NetworkPort, restarted.Network!.Code));
        Assert.Equal($"https://*:{port}", again.ApiEnvironment!["Kestrel__Endpoints__Netz__Url"]);
    }

    [Theory]
    [InlineData("Host=db.spa.at;Username=gmunden", "Server (Host), Datenbank")]
    [InlineData("Host=127.0.0.1;Port={0};Database=aispadb;Username=aispa_app", "Datenbank dieses PCs")]
    [InlineData("Host=db.spa.at;Database", "nicht lesbar")]
    public async Task Falsche_Angabe_fuer_die_Zentrale_wird_erklaert_und_nicht_gespeichert(string connection, string message)
    {
        InstallPackage();
        var runner = new FakeRunner(Paths);
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);
        var before = File.ReadAllText(Paths.Secrets);

        var error = await Assert.ThrowsAsync<LocalServerException>(() =>
            server.SetCentralAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, connection, server.Config.DatabasePort), Ct));

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(Paths.Secrets));
        Assert.Single(runner.Calls, c => c == "api");
    }

    [Fact]
    public async Task Sicherung_mit_Schluessel_daneben_und_nur_die_letzten_14()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths);
        var time = new ManualTime(new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero));
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted), time: time);
        await server.StartAsync(ct: Ct);
        var folder = Path.Combine(_root.FullName, "sicherung");

        var missing = await Assert.ThrowsAsync<LocalServerException>(() => server.BackupAsync(folder, Ct));
        Assert.Contains("Wiederherstellungsschlüssel", missing.Message, StringComparison.Ordinal);
        Assert.False(server.HasRecoveryKey);
        Assert.False(File.Exists(Paths.RecoveryFile));

        var code = server.CreateRecoveryKey();
        Assert.Matches("^([A-HJKMNP-Z2-9]{4}-){5}[A-HJKMNP-Z2-9]{4}$", code);
        Assert.True(server.HasRecoveryKey);

        // Für „Passwort vergessen“ liest die laufende API dieselbe Datei; ein neuer Schlüssel gilt ohne Neustart.
        Assert.Equal(Paths.RecoveryFile, runner.ApiEnvironment!["Hosting__RecoveryFile"]);
        Assert.Equal(runner.ApiEnvironment[$"HealthData__Keys__{runner.ApiEnvironment["HealthData__CurrentKeyId"]}"],
            RecoveryKey.Open(File.ReadAllText(Paths.RecoveryFile), code).Key);
        for (var day = 0; day < 16; day++)
        {
            await server.BackupAsync(folder, Ct);
            time.Advance(TimeSpan.FromDays(1));
        }

        var dumps = Directory.GetFiles(folder, "*.dump").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(LocalServer.BackupsKept, dumps.Count);
        Assert.Equal("aispa-20261021-080000.dump", dumps[0]);
        Assert.Equal(LocalServer.BackupsKept, Directory.GetFiles(folder, "*.schluessel.json").Length);
        Assert.Contains("-Fc", runner.DumpArgs, StringComparison.Ordinal);
        Assert.Equal(runner.InitPassword, runner.DumpPassword);

        // Auf einem anderen PC: mit dem Code kommt der Schlüssel für Gesundheitsdaten zurück, mit einem falschen nicht.
        var env = runner.ApiEnvironment!;
        var key = RecoveryKey.Open(File.ReadAllText(Path.Combine(folder, "aispa-20261021-080000.schluessel.json")), code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal));
        Assert.Equal(env["HealthData__CurrentKeyId"], key.KeyId);
        Assert.Equal(env[$"HealthData__Keys__{key.KeyId}"], key.Key);
        Assert.Throws<LocalServerException>(() => RecoveryKey.Open(File.ReadAllText(Path.Combine(folder, "aispa-20261021-080000.schluessel.json")),
            RecoveryKey.NewCode()));
    }

    [Fact]
    public async Task Fehlgeschlagene_Sicherung_hinterlaesst_keine_halbe_Datei()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths) { DumpFails = true };
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);
        server.CreateRecoveryKey();
        var folder = Path.Combine(_root.FullName, "sicherung");

        var error = await Assert.ThrowsAsync<LocalServerException>(() => server.BackupAsync(folder, Ct));

        Assert.Contains("Platte voll", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(folder));
    }

    /// <summary>Einzelplatz „Gmunden“ mit einer Sicherung; gibt Code, Pfad der Sicherung und die Umgebung der API zurück.</summary>
    private async Task<(string Code, string Dump, IReadOnlyDictionary<string, string> Env)> BackedUpAsync()
    {
        InstallPackage();
        var runner = new FakeRunner(Paths);
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);
        var code = server.CreateRecoveryKey();
        var dump = await server.BackupAsync(Path.Combine(_root.FullName, "sicherung"), Ct);
        await server.StopAsync();
        return (code, dump, runner.ApiEnvironment!);
    }

    [Fact]
    public async Task Sicherung_ohne_laufende_App_startet_die_Datenbank_nur_dafuer()
    {
        var (_, _, _) = await BackedUpAsync();
        var folder = Path.Combine(_root.FullName, "abends");

        // Datenbank aus, App zu: starten, sichern, wieder beenden; die API startet nicht.
        var runner = new FakeRunner(Paths) { DatabaseExists = true };
        var task = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(false));
        var dump = await task.BackupUnattendedAsync(folder, () => false, Ct);
        Assert.True(File.Exists(dump));
        Assert.Equal(["pg_ctl status", "pg_ctl status", "pg_ctl start", "psql", "pg_dump", "pg_ctl stop"], runner.Calls);

        // Ist die App inzwischen gestartet, bleibt die Datenbank an.
        runner.Calls.Clear();
        await task.BackupUnattendedAsync(folder, () => true, Ct);
        Assert.DoesNotContain("pg_ctl stop", runner.Calls);

        // Lief die Datenbank schon (App offen), wird sie weder gestartet noch beendet.
        runner.Calls.Clear();
        await task.BackupUnattendedAsync(folder, () => false, Ct);
        Assert.Equal(["pg_ctl status", "pg_dump"], runner.Calls);
    }

    [Fact]
    public async Task Sicherung_ohne_lokale_Datenbank_meldet_den_Grund()
    {
        InstallPackage();
        var server = new LocalServer(Paths, new FakeRunner(Paths), new PlainProtector(), (_, _) => Task.FromResult(false));
        var error = await Assert.ThrowsAsync<LocalServerException>(() => server.BackupUnattendedAsync(_root.FullName, () => false, Ct));
        Assert.Contains("keine lokale Datenbank", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Windows_Aufgabe_sichert_nur_wenn_eingerichtet_und_faellig()
    {
        var (_, _, _) = await BackedUpAsync();
        var runner = new FakeRunner(Paths) { DatabaseExists = true };
        var time = new ManualTime(new DateTimeOffset(2026, 10, 19, 21, 0, 0, TimeSpan.Zero));
        var server = new LocalServer(Paths, runner, new PlainProtector(), (_, _) => Task.FromResult(false), time: time);
        var settings = new MemorySettingsStore();
        var log = new StringWriter();

        // Kein Einzelplatz oder kein Ordner: nichts zu tun.
        Assert.Equal(0, await ScheduledBackup.RunAsync(server, settings, time, log, () => false, Ct));
        Assert.DoesNotContain("pg_dump", runner.Calls);

        var folder = Path.Combine(_root.FullName, "abends");
        settings.Save(new DesktopSettings(Standalone: true, BackupFolder: folder, LastBackupAt: time.GetUtcNow().AddHours(-3)));
        Assert.Equal(0, await ScheduledBackup.RunAsync(server, settings, time, log, () => false, Ct));
        Assert.DoesNotContain("pg_dump", runner.Calls);
        Assert.Contains("Nicht fällig", log.ToString(), StringComparison.Ordinal);

        time.Advance(TimeSpan.FromHours(10));
        Assert.Equal(0, await ScheduledBackup.RunAsync(server, settings, time, log, () => false, Ct));
        Assert.Contains("pg_dump", runner.Calls);
        Assert.Equal(time.GetUtcNow(), settings.Load().LastBackupAt);
        Assert.Single(Directory.GetFiles(folder, "*.dump"));

        // Fehler: Exit-Code 1, Grund im Protokoll, Zeitpunkt der letzten Sicherung bleibt.
        runner.DumpFails = true;
        var last = settings.Load().LastBackupAt;
        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, await ScheduledBackup.RunAsync(server, settings, time, log, () => false, Ct));
        Assert.Contains("Fehlgeschlagen: Die Sicherung ist fehlgeschlagen", log.ToString(), StringComparison.Ordinal);
        Assert.Equal(last, settings.Load().LastBackupAt);
    }

    [Fact]
    public async Task Windows_Aufgabe_wird_mit_schtasks_angelegt()
    {
        var runner = new FakeRunner(Paths);
        var app = @"C:\Programme\AISPA & Co\AispaStudio.Desktop.exe";
        var scheduler = new WindowsBackupScheduler(runner, app, _root.FullName);

        Assert.Null(await scheduler.RegisterAsync(Ct));
        Assert.Equal(["schtasks"], runner.Calls);
        Assert.Equal(["/Create", "/TN", WindowsBackupScheduler.TaskName, "/XML"], runner.LastArgs.Take(4));
        Assert.Equal("/F", runner.LastArgs[^1]);
        Assert.Contains(@"<Command>C:\Programme\AISPA &amp; Co\AispaStudio.Desktop.exe</Command>", runner.LastXml, StringComparison.Ordinal);
        Assert.Contains("<Arguments>--sichern</Arguments>", runner.LastXml, StringComparison.Ordinal);
        Assert.Contains("<StartWhenAvailable>true</StartWhenAvailable>", runner.LastXml, StringComparison.Ordinal);
        Assert.Contains("<LogonType>InteractiveToken</LogonType>", runner.LastXml, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_root.FullName, "aispa-sicherung-*.xml"));
        System.Xml.Linq.XDocument.Parse(runner.LastXml);

        runner.SchtasksFails = true;
        Assert.Equal("FEHLER: Zugriff verweigert", await scheduler.RegisterAsync(Ct));
    }

    private LocalServerPaths NewPc => new(Path.Combine(_root.FullName, "app"), Path.Combine(_root.FullName, "neuer-pc"));

    [Fact]
    public async Task Neuer_PC_stellt_die_Sicherung_mit_Schluessel_wieder_her()
    {
        var (code, dump, before) = await BackedUpAsync();
        var runner = new FakeRunner(NewPc) { TenantCount = 0 };
        var server = new LocalServer(NewPc, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);
        var steps = new List<string>();

        await server.RestoreAsync(dump, code.ToLowerInvariant(), new SyncProgress(steps), Ct);

        Assert.EndsWith($"-d aispadb --no-owner --no-privileges --exit-on-error {dump}", runner.RestoreArgs, StringComparison.Ordinal);
        Assert.Equal(["Lege die bisherigen Daten beiseite …", "Lege die lokale Datenbank neu an …", "Spiele die Sicherung ein …",
            "Starte AISPA Studio auf diesem PC …"], steps);
        var keyId = before["HealthData__CurrentKeyId"];
        Assert.Equal(keyId, runner.ApiEnvironment!["HealthData__CurrentKeyId"]);
        Assert.Equal(before[$"HealthData__Keys__{keyId}"], runner.ApiEnvironment[$"HealthData__Keys__{keyId}"]);
        Assert.NotEqual(before["ConnectionStrings__aispadb"], runner.ApiEnvironment["ConnectionStrings__aispadb"]);
        Assert.Equal(runner.InitPassword, runner.RestorePassword);
        Assert.True(server.HasRecoveryKey);
        Assert.Single(Directory.GetDirectories(NewPc.DataDirectory, "pg-vor-wiederherstellung-*"));
        Assert.Single(Directory.GetFiles(NewPc.DataDirectory, "zugang-vor-wiederherstellung-*.bin"));
    }

    [Fact]
    public async Task Wiederherstellen_ueberschreibt_nie_ein_eingerichtetes_Spa()
    {
        var (code, dump, _) = await BackedUpAsync();
        var runner = new FakeRunner(NewPc) { TenantCount = 1 };
        var server = new LocalServer(NewPc, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);

        var error = await Assert.ThrowsAsync<LocalServerException>(() => server.RestoreAsync(dump, code, ct: Ct));

        Assert.Contains("schon ein Spa eingerichtet", error.Message, StringComparison.Ordinal);
        Assert.Equal("", runner.RestoreArgs);
        Assert.Empty(Directory.GetDirectories(NewPc.DataDirectory, "pg-*"));
    }

    [Fact]
    public async Task Unlesbare_Zugangsdaten_werden_beiseitegelegt_und_die_Sicherung_eingespielt()
    {
        var (code, dump, _) = await BackedUpAsync();
        Directory.CreateDirectory(Path.Combine(NewPc.Cluster));
        File.WriteAllText(Path.Combine(NewPc.Cluster, "PG_VERSION"), "17");
        File.WriteAllText(NewPc.Secrets, "kaputt");
        var runner = new FakeRunner(NewPc);
        var server = new LocalServer(NewPc, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await Assert.ThrowsAsync<LocalServerException>(() => server.StartAsync(ct: Ct));

        await server.RestoreAsync(dump, code, ct: Ct);

        Assert.NotEqual("", runner.RestoreArgs);
        Assert.Equal("kaputt", File.ReadAllText(Assert.Single(Directory.GetFiles(NewPc.DataDirectory, "zugang-vor-wiederherstellung-*.bin"))));
        Assert.True(runner.ApiStarted);
    }

    [Fact]
    public async Task Falscher_Code_oder_kaputte_Sicherung_aendern_nichts_Bleibendes()
    {
        var (_, dump, _) = await BackedUpAsync();
        var runner = new FakeRunner(NewPc) { TenantCount = 0 };
        var server = new LocalServer(NewPc, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);

        var wrong = await Assert.ThrowsAsync<LocalServerException>(() => server.RestoreAsync(dump, RecoveryKey.NewCode(), ct: Ct));
        Assert.Contains("passt nicht", wrong.Message, StringComparison.Ordinal);
        var missing = await Assert.ThrowsAsync<LocalServerException>(() => server.RestoreAsync(dump + ".fehlt", "x", ct: Ct));
        Assert.Contains("nicht gefunden", missing.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(NewPc.DataDirectory, "pg-*"));
    }

    [Fact]
    public async Task Abgebrochenes_Einspielen_wird_beiseitegelegt()
    {
        var (code, dump, _) = await BackedUpAsync();
        var runner = new FakeRunner(NewPc) { TenantCount = 0, RestoreFails = true };
        var server = new LocalServer(NewPc, runner, new PlainProtector(), (_, _) => Task.FromResult(runner.ApiStarted));
        await server.StartAsync(ct: Ct);

        var error = await Assert.ThrowsAsync<LocalServerException>(() => server.RestoreAsync(dump, code, ct: Ct));

        Assert.Contains("kaputte Sicherung", error.Message, StringComparison.Ordinal);
        Assert.Single(Directory.GetDirectories(NewPc.DataDirectory, "pg-abgebrochen-*"));
        Assert.False(Directory.Exists(NewPc.Cluster));
    }

    [Fact]
    public void Schalter_standalone_wird_gemerkt_und_server_schaltet_zurueck()
    {
        var settings = new MemorySettingsStore();

        Assert.False(StandaloneMode.Resolve([], settings));
        Assert.True(StandaloneMode.Resolve(["--standalone"], settings));
        Assert.True(StandaloneMode.Resolve([], settings));
        Assert.False(StandaloneMode.Resolve(["--server"], settings));
        Assert.False(settings.Load().Standalone);
    }

    private sealed class SyncProgress(List<string> steps) : IProgress<string>
    {
        public void Report(string value) => steps.Add(value);
    }

    /// <summary>Spielt PostgreSQL und API nach: initdb legt PG_VERSION an, psql meldet, ob die Datenbank schon da ist.</summary>
    private sealed class FakeRunner(LocalServerPaths paths) : IProcessRunner
    {
        public List<string> Calls { get; } = [];

        public bool DatabaseRunning { get; set; }

        public string StopFile => paths.StopFile;

        public bool DatabaseExists { get; set; }

        public bool ApiExitsImmediately { get; set; }

        public bool ApiStarted { get; set; }

        public bool ApiKilled { get; private set; }

        /// <summary>Die API hat die Stopp-Datei gesehen und sich selbst beendet.</summary>
        public bool ApiStoppedGracefully { get; private set; }

        /// <summary>Wie eine ältere API ohne Stopp-Datei.</summary>
        public bool ApiIgnoresStop { get; set; }

        public IReadOnlyDictionary<string, string>? ApiEnvironment { get; private set; }

        public string StartArgs { get; private set; } = "";

        public string? InitPassword { get; private set; }

        public string? PsqlPassword { get; private set; }

        public bool DumpFails { get; set; }

        /// <summary>Spas in der lokalen Datenbank; <c>null</c>, wenn es die Tabelle noch nicht gibt.</summary>
        public int? TenantCount { get; set; }

        public bool RestoreFails { get; set; }

        public string RestoreArgs { get; private set; } = "";

        public string? RestorePassword { get; private set; }

        public string DumpArgs { get; private set; } = "";

        public string? DumpPassword { get; private set; }

        public bool SchtasksFails { get; set; }

        public IReadOnlyList<string> LastArgs { get; private set; } = [];

        public string LastXml { get; private set; } = "";

        public Task<ProcessResult> RunAsync(string file, IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? env = null,
            CancellationToken ct = default)
        {
            var tool = Path.GetFileNameWithoutExtension(file);
            var call = tool == "pg_ctl" ? $"pg_ctl {args[0]}" : tool;
            Calls.Add(call);
            LastArgs = args;
            switch (call)
            {
                case "initdb":
                    InitPassword = File.ReadAllText(args[args.ToList().IndexOf("--pwfile") + 1]);
                    Directory.CreateDirectory(paths.Cluster);
                    File.WriteAllText(Path.Combine(paths.Cluster, "PG_VERSION"), "17");
                    return Done(0);
                case "pg_ctl status":
                    return Done(DatabaseRunning ? 0 : 3);
                case "pg_ctl start":
                    StartArgs = args[^1];
                    DatabaseRunning = true;
                    File.WriteAllText(Path.Combine(paths.Cluster, "postmaster.pid"), "1");
                    return Done(0);
                case "psql" when args[^1].Contains("pg_tables", StringComparison.Ordinal):
                    return Done(0, TenantCount is null ? "0\n" : "1\n");
                case "psql" when args[^1].Contains("Tenants", StringComparison.Ordinal):
                    return Done(0, $"{TenantCount}\n");
                case "psql":
                    PsqlPassword = env!["PGPASSWORD"];
                    return Done(0, DatabaseExists ? "1\n" : "");
                case "pg_ctl stop":
                    DatabaseRunning = false;
                    File.Delete(Path.Combine(paths.Cluster, "postmaster.pid"));
                    return Done(0);
                case "pg_restore":
                    RestoreArgs = string.Join(' ', args);
                    RestorePassword = env!["PGPASSWORD"];
                    return Done(RestoreFails ? 1 : 0, RestoreFails ? "pg_restore: Fehler: kaputte Sicherung" : "");
                case "createdb":
                    DatabaseExists = true;
                    return Done(0);
                case "schtasks":
                    LastXml = File.ReadAllText(args[args.ToList().IndexOf("/XML") + 1], Encoding.Unicode);
                    return Done(SchtasksFails ? 1 : 0, SchtasksFails ? "FEHLER: Zugriff verweigert\n" : "");
                case "pg_dump":
                    DumpArgs = string.Join(' ', args);
                    DumpPassword = env!["PGPASSWORD"];
                    File.WriteAllText(args[args.ToList().IndexOf("-f") + 1], "PGDMP");
                    return Done(DumpFails ? 1 : 0, DumpFails ? "pg_dump: Fehler: Platte voll" : "");
                default:
                    return Done(0);
            }
        }

        public IRunningProcess Start(string file, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, string logFile)
        {
            Calls.Add("api");
            ApiEnvironment = env;
            ApiStarted = !ApiExitsImmediately;
            ApiKilled = false;
            ApiStoppedGracefully = false;
            return new Process(this);
        }

        private static Task<ProcessResult> Done(int code, string output = "") => Task.FromResult(new ProcessResult(code, output));

        private sealed class Process(FakeRunner runner) : IRunningProcess
        {
            public bool HasExited
            {
                get
                {
                    if (!runner.ApiIgnoresStop && File.Exists(runner.StopFile))
                    {
                        runner.ApiStoppedGracefully = true;
                        runner.ApiStarted = false;
                    }

                    return runner.ApiExitsImmediately || runner.ApiKilled || runner.ApiStoppedGracefully;
                }
            }

            public Task KillAsync()
            {
                runner.ApiKilled = true;
                runner.ApiStarted = false;
                return Task.CompletedTask;
            }
        }
    }
}

/// <summary>Rahmen der App im Einzelplatz: erst Datenbank und API starten, dann wie gewohnt verbinden.</summary>
public sealed class LocalStartTests
{
    private sealed class FakeLocal(Exception? failure = null) : ILocalServer
    {
        public NetworkShareInfo? Network { get; set; }

        public Task<bool> SetSharedAsync(bool share, CancellationToken ct = default) => Task.FromResult(false);

        public List<string> Steps { get; } = [];

        public Uri ApiAddress { get; } = new("http://127.0.0.1:55086/");

        public Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            Steps.Add("start");
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }

        public Task StopAsync() => Task.CompletedTask;

        public string? CentralDescription => null;

        public Task<bool> SetCentralAsync(string? connectionString, CancellationToken ct = default) => Task.FromResult(false);

        public string? ShareCode => null;

        public string SetShareCode(string? code) => code ?? "";

        public bool HasRecoveryKey => false;

        public string CreateRecoveryKey() => "";

        public Task<string> BackupAsync(string folder, CancellationToken ct = default) => Task.FromResult("");

        public Task RestoreAsync(string dumpFile, string recoveryCode, IProgress<string>? progress = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task Startet_den_Einzelplatz_vor_der_Verbindung()
    {
        var api = new StubApi()
            .On("GET /api/system/status", new AispaStudio.Contracts.SystemStatus("1", "Production", DateTimeOffset.UtcNow))
            .On("GET /api/auth/me", null, System.Net.HttpStatusCode.Unauthorized);
        var local = new FakeLocal();
        var vm = new AispaStudio.Desktop.ViewModels.MainViewModel(api.Client(), local: local);

        await vm.StartAsync();

        Assert.Equal(["start"], local.Steps);
        Assert.True(vm.IsConnected);
        Assert.True(vm.IsLocal);
    }

    [Fact]
    public async Task Startet_der_Einzelplatz_nicht_steht_der_Grund_auf_dem_Startbildschirm()
    {
        var api = new StubApi();
        var vm = new AispaStudio.Desktop.ViewModels.MainViewModel(api.Client(),
            local: new FakeLocal(new LocalServerException("Für den Einzelplatz-Betrieb fehlt pg_ctl.")));

        await vm.StartAsync();

        Assert.False(vm.IsConnected);
        Assert.Equal("Einzelplatz startet nicht", vm.ConnectionTitle);
        Assert.Equal("Für den Einzelplatz-Betrieb fehlt pg_ctl.", vm.StatusText);
        Assert.Empty(api.Calls);
    }
}
