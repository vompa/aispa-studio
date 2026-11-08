using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Einzelplatz-Betrieb (ADR 0041): Die Windows-App startet die mitgelieferte PostgreSQL und die API selbst, beide nur an
/// 127.0.0.1. Beim ersten Start legt sie die Datenbank unter <c>%LOCALAPPDATA%\AispaStudio\daten</c> an, mit eigenem Passwort
/// und eigenem Schlüssel für Gesundheitsdaten, beide DPAPI-geschützt. Die Web-Seiten im Browser (Anmelden, Hilfe) liefert dieselbe API aus.
/// </summary>
public interface ILocalServer
{
    /// <summary>Adresse der lokalen API, schon vor dem Start bekannt.</summary>
    Uri ApiAddress { get; }

    /// <summary>Startet Datenbank und API, falls sie nicht schon laufen, und wartet, bis die API antwortet.</summary>
    Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Beendet API und Datenbank geordnet.</summary>
    Task StopAsync();

    /// <summary>Zentrale Datenbank für den Abgleich als „Server · Datenbank“, ohne Passwort; <c>null</c> ohne Abgleich.</summary>
    string? CentralDescription { get; }

    /// <summary>
    /// Trägt die zentrale Datenbank ein (<c>null</c> schaltet den Abgleich aus), speichert sie geschützt und startet die API neu,
    /// damit der Abgleich gleich läuft. <c>false</c>, wenn die API nicht von dieser App gestartet wurde und erst beim nächsten Start
    /// neu liest.
    /// </summary>
    Task<bool> SetCentralAsync(string? connectionString, CancellationToken ct = default);

    /// <summary>Schlüsselcode des Spas für die Gesundheitsdaten beim Abgleich (ADR 0048); <c>null</c>, solange keiner gewählt ist.</summary>
    string? ShareCode { get; }

    /// <summary>
    /// Merkt sich den Schlüsselcode des Spas geschützt auf diesem PC (gilt ab dem nächsten Start der API, z. B. über
    /// <see cref="SetCentralAsync"/>). Ohne Angabe entsteht ein neuer, für die erste Stelle. Gibt den gespeicherten Code zurück.
    /// </summary>
    string SetShareCode(string? code);

    /// <summary>Es gibt einen Wiederherstellungsschlüssel; ohne ihn sichert die App nicht (Schritt 5).</summary>
    bool HasRecoveryKey { get; }

    /// <summary>Erzeugt einen neuen Wiederherstellungsschlüssel, merkt sich nur den damit verschlüsselten Schlüssel und gibt den Code zurück.</summary>
    string CreateRecoveryKey();

    /// <summary>
    /// Sichert die lokale Datenbank mit <c>pg_dump</c> in <paramref name="folder"/>, legt den verschlüsselten Schlüssel daneben und
    /// behält die letzten <see cref="LocalServer.BackupsKept"/> Sicherungen. Gibt den Pfad der Sicherung zurück.
    /// </summary>
    Task<string> BackupAsync(string folder, CancellationToken ct = default);

    /// <summary>
    /// Spielt eine Sicherung samt Schlüssel (mit dem Wiederherstellungsschlüssel) in eine neue lokale Datenbank ein und startet die API.
    /// Nur, wenn hier noch kein Spa eingerichtet ist oder die Zugangsdaten unlesbar sind; bisherige Daten werden beiseitegelegt, nie gelöscht.
    /// </summary>
    Task RestoreAsync(string dumpFile, string recoveryCode, IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Mehrplatz: Adressen und Verbindungscode, solange dieser PC im Netz freigegeben ist; sonst <c>null</c>.</summary>
    NetworkShareInfo? Network { get; }

    /// <summary>
    /// Gibt die API zusätzlich per HTTPS im Netz frei oder nimmt die Freigabe zurück und startet die API neu (die Datenbank bleibt
    /// nur an 127.0.0.1). Beim ersten Freigeben entsteht das Zertifikat dieses PCs. <c>false</c> wie bei <see cref="SetCentralAsync"/>.
    /// </summary>
    Task<bool> SetSharedAsync(bool share, CancellationToken ct = default);
}

/// <summary>Wo die mitgelieferten Teile liegen und wohin die Daten kommen.</summary>
/// <param name="AppDirectory">Ordner der App; darin <c>postgres\bin</c>, <c>api</c> und <c>web</c> aus dem Windows-Paket.</param>
/// <param name="DataDirectory">Datenordner im Benutzerprofil.</param>
public sealed record LocalServerPaths(string AppDirectory, string DataDirectory)
{
    public static LocalServerPaths Default { get; } = new(AppContext.BaseDirectory,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudio", "daten"));

    private static string Exe(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    public string PostgresBin => Path.Combine(AppDirectory, "postgres", "bin");

    public string Tool(string name) => Path.Combine(PostgresBin, Exe(name));

    public string Api => Path.Combine(AppDirectory, "api", Exe("AispaStudio.Api"));

    public string WebRoot => Path.Combine(AppDirectory, "web");

    public string Cluster => Path.Combine(DataDirectory, "pg");

    public string Logs => Path.Combine(DataDirectory, "protokolle");

    public string Config => Path.Combine(DataDirectory, "einzelplatz.json");

    public string Secrets => Path.Combine(DataDirectory, "zugang.bin");

    /// <summary>Legt die App diese Datei an, beendet sich die API geordnet (<c>Hosting:StopFile</c>).</summary>
    public string StopFile => Path.Combine(DataDirectory, "api.stop");

    /// <summary>
    /// Schlüssel für Gesundheitsdaten, mit dem Wiederherstellungsschlüssel verschlüsselt, für „Passwort vergessen“ in der API
    /// (<c>Hosting:RecoveryFile</c>). Ohne den Code nutzlos; dieselbe Datei liegt neben jeder Sicherung.
    /// </summary>
    public string RecoveryFile => Path.Combine(DataDirectory, "wiederherstellung.json");

    /// <summary>Zertifikat für die Freigabe im Netz; das Passwort liegt geschützt in <see cref="Secrets"/>.</summary>
    public string NetworkCertificate => Path.Combine(DataDirectory, "netz.pfx");
}

/// <summary>Ports dieses Einzelplatzes; einmal gewählt und gemerkt, damit die Adresse im Browser gleich bleibt.</summary>
/// <param name="NetworkPort">HTTPS-Port für die anderen PCs im Netz; beim ersten Freigeben gewählt.</param>
/// <param name="Shared">Die API ist im Netz freigegeben (Mehrplatz).</param>
public sealed record LocalServerConfig(int DatabasePort, int ApiPort, int NetworkPort = 0, bool Shared = false);

/// <summary>Datenbank-Passwort und Schlüssel für Gesundheitsdaten (Base64, 32 Byte).</summary>
/// <param name="HealthKeyId">
/// Kennung des Schlüssels, je Installation eindeutig. Beim Abgleich mit einer zentralen Datenbank (Schritt 4) kommt der Schlüssel dort
/// unter dieser Kennung dazu, damit die verschlüsselt übertragenen Gesundheitsdaten lesbar bleiben; „lokal1“ an jedem PC würde sich überschneiden.
/// </param>
/// <param name="CentralConnectionString">Zentrale PostgreSQL für den Abgleich (ADR 0041, Schritt 4); liegt wie das Passwort nur geschützt hier.</param>
/// <param name="RecoveryBlob">Schlüssel für Gesundheitsdaten, mit dem Wiederherstellungsschlüssel verschlüsselt (<see cref="RecoveryKey"/>).</param>
/// <param name="ShareCode">Schlüsselcode des Spas für den Abgleich (ADR 0048); damit lesen die Stellen die Gesundheitsdaten der anderen.</param>
/// <param name="NetworkCertificatePassword">Passwort der Datei <see cref="LocalServerPaths.NetworkCertificate"/> (Mehrplatz im Netz).</param>
public sealed record LocalSecrets(string DatabasePassword, string HealthKey, string? HealthKeyId = null, string? CentralConnectionString = null,
    string? RecoveryBlob = null, string? ShareCode = null, string? NetworkCertificatePassword = null)
{
    public string KeyId => HealthKeyId ?? "lokal1";
}

/// <summary>Schützt die Zugangsdaten auf der Platte; unter Windows mit DPAPI für das angemeldete Windows-Konto.</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] data);

    byte[] Unprotect(byte[] data);
}

[SupportedOSPlatform("windows")]
public sealed class DpapiProtector : ISecretProtector
{
    public byte[] Protect(byte[] data) => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}

/// <summary>Ohne Schutz, nur für Tests.</summary>
public sealed class PlainProtector : ISecretProtector
{
    public byte[] Protect(byte[] data) => data;

    public byte[] Unprotect(byte[] data) => data;
}

public sealed record ProcessResult(int ExitCode, string Output);

public interface IRunningProcess
{
    bool HasExited { get; }

    /// <summary>Beendet den Prozess samt Kindprozessen und wartet, bis er weg ist (der Port ist dann wieder frei).</summary>
    Task KillAsync();
}

/// <summary>Startet Programme; austauschbar, damit sich der Ablauf ohne PostgreSQL testen lässt.</summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string file, IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? env = null,
        CancellationToken ct = default);

    IRunningProcess Start(string file, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, string logFile);
}

public sealed class LocalServerException(string message) : Exception(message);

public sealed class LocalServer : ILocalServer
{
    public const string DatabaseName = "aispadb";
    // Nicht „aispa“: heißt das Konto wie ein Schema, steht dieses Schema über "$user" vorn im search_path, und EF sucht die
    // Migrationstabelle beim zweiten Start dort statt in public und spielt alle Migrationen noch einmal ein.
    public const string DatabaseUser = "aispa_app";

    /// <summary>So viele Sicherungen bleiben im Ordner, ältere löscht die App (bei täglicher Sicherung gut zwei Wochen).</summary>
    public const int BackupsKept = 14;

    private readonly LocalServerPaths _paths;
    private readonly IProcessRunner _runner;
    private readonly ISecretProtector _protector;
    private readonly Func<Uri, CancellationToken, Task<bool>> _isApiReady;
    private readonly TimeSpan _apiTimeout;
    private readonly TimeSpan _stopTimeout;
    private LocalServerConfig _config;
    private readonly TimeProvider _time;
    private IRunningProcess? _api;
    private LocalSecrets? _secrets;

    public LocalServer(LocalServerPaths paths, IProcessRunner runner, ISecretProtector protector,
        Func<Uri, CancellationToken, Task<bool>>? isApiReady = null, TimeSpan? apiTimeout = null, TimeProvider? time = null,
        TimeSpan? stopTimeout = null)
    {
        _time = time ?? TimeProvider.System;
        _paths = paths;
        _runner = runner;
        _protector = protector;
        _isApiReady = isApiReady ?? PingAsync;
        _apiTimeout = apiTimeout ?? TimeSpan.FromMinutes(3);
        _stopTimeout = stopTimeout ?? TimeSpan.FromSeconds(20);
        _config = LoadOrCreateConfig();
    }

    public Uri ApiAddress => new($"http://127.0.0.1:{_config.ApiPort}/");

    public LocalServerConfig Config => _config;

    public async Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        foreach (var required in new[] { _paths.Tool("pg_ctl"), _paths.Api })
        {
            if (!File.Exists(required))
            {
                throw new LocalServerException(
                    $"Für den Einzelplatz-Betrieb fehlt {required}. Bitte das vollständige Windows-Paket entpacken.");
            }
        }

        Directory.CreateDirectory(_paths.Logs);
        var secrets = _secrets = LoadOrCreateSecrets();
        await StartDatabaseAsync(secrets, progress, ct).ConfigureAwait(false);

        if (await _isApiReady(ApiAddress, ct).ConfigureAwait(false))
        {
            return;
        }

        progress?.Report("Starte AISPA Studio auf diesem PC …");
        await StartApiAsync(secrets, ct).ConfigureAwait(false);
    }

    /// <summary>Cluster anlegen, falls es ihn nicht gibt, PostgreSQL starten und die Datenbank anlegen.</summary>
    private async Task StartDatabaseAsync(LocalSecrets secrets, IProgress<string>? progress, CancellationToken ct)
    {
        if (!File.Exists(Path.Combine(_paths.Cluster, "PG_VERSION")))
        {
            progress?.Report("Lege die lokale Datenbank an …");
            await InitClusterAsync(secrets, ct).ConfigureAwait(false);
        }

        var status = await _runner.RunAsync(_paths.Tool("pg_ctl"), ["status", "-D", _paths.Cluster], ct: ct).ConfigureAwait(false);
        if (status.ExitCode != 0)
        {
            progress?.Report("Starte die lokale Datenbank …");
            var started = await _runner.RunAsync(_paths.Tool("pg_ctl"),
            [
                "start", "-D", _paths.Cluster, "-w", "-t", "60", "-l", Path.Combine(_paths.Logs, "datenbank.log"),
                "-o", ServerOptions(_config.DatabasePort),
            ], ct: ct).ConfigureAwait(false);
            if (started.ExitCode != 0)
            {
                var log = Path.Combine(_paths.Logs, "datenbank.log");
                throw new LocalServerException(
                    $"Die lokale Datenbank startet nicht: {started.Output.Trim()} {LogTail(log)} Protokoll: {log}");
            }
        }

        await EnsureDatabaseAsync(secrets, ct).ConfigureAwait(false);
    }

    public string? CentralDescription => _secrets?.CentralConnectionString is { } central ? CentralConnection.Describe(central) : null;

    public async Task<bool> SetCentralAsync(string? connectionString, CancellationToken ct = default)
    {
        var central = string.IsNullOrWhiteSpace(connectionString) ? null : connectionString.Trim();
        if (central is not null)
        {
            CentralConnection.Validate(central, _config.DatabasePort);
        }

        var secrets = (_secrets ?? LoadOrCreateSecrets()) with { CentralConnectionString = central };
        SaveSecrets(secrets);
        _secrets = secrets;
        if (_api is not { HasExited: false } running)
        {
            return false;
        }

        // Die API liest den Abschnitt Sync nur beim Start; die Datenbank läuft weiter.
        await StopApiAsync(running).ConfigureAwait(false);
        await StartApiAsync(secrets, ct).ConfigureAwait(false);
        return true;
    }

    public NetworkShareInfo? Network => _config.Shared && NetworkCode(_secrets ?? TryLoadSecrets()) is { } code
        ? new NetworkShareInfo(NetworkShare.Addresses(_config.NetworkPort), code)
        : null;

    public async Task<bool> SetSharedAsync(bool share, CancellationToken ct = default)
    {
        var secrets = _secrets ?? LoadOrCreateSecrets();
        if (share && NetworkCode(secrets) is null)
        {
            var password = NewDatabasePassword();
            using var certificate = NetworkShare.CreateCertificate(Environment.MachineName, _time.GetUtcNow());
            Directory.CreateDirectory(_paths.DataDirectory);
            await File.WriteAllBytesAsync(_paths.NetworkCertificate, certificate.Export(X509ContentType.Pfx, password), ct).ConfigureAwait(false);
            secrets = secrets with { NetworkCertificatePassword = password };
            SaveSecrets(secrets);
            _secrets = secrets;
        }

        _config = _config with
        {
            Shared = share,
            NetworkPort = _config.NetworkPort > 0 ? _config.NetworkPort : FreePort(NetworkShare.DefaultPort, IPAddress.Any),
        };
        SaveConfig(_config);
        if (_api is not { HasExited: false } running)
        {
            return false;
        }

        // Kestrel liest die Endpunkte nur beim Start; die Datenbank läuft weiter.
        await StopApiAsync(running).ConfigureAwait(false);
        await StartApiAsync(secrets, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Verbindungscode des gespeicherten Zertifikats; <c>null</c>, wenn es fehlt oder nicht lesbar ist (z. B. nach dem Wiederherstellen).</summary>
    private string? NetworkCode(LocalSecrets? secrets)
    {
        if (secrets?.NetworkCertificatePassword is not { } password || !File.Exists(_paths.NetworkCertificate))
        {
            return null;
        }

        try
        {
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(_paths.NetworkCertificate, password);
            return NetworkShare.Code(certificate);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public string? ShareCode => (_secrets ?? TryLoadSecrets())?.ShareCode;

    public string SetShareCode(string? code)
    {
        if (code is not null && !KeySeal.IsComplete(code))
        {
            throw new LocalServerException("Der Schlüsselcode des Spas hat 24 Zeichen, z. B. K7QM-2XRT-H9PW-4CNE-YB3D-ZF6A.");
        }

        var normalized = code is null
            ? KeySeal.NewCode()
            : string.Join('-', code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).Chunk(4).Select(c => new string(c)));
        var secrets = (_secrets ?? LoadOrCreateSecrets()) with { ShareCode = normalized };
        SaveSecrets(secrets);
        _secrets = secrets;
        return normalized;
    }

    public bool HasRecoveryKey => (_secrets ?? LoadOrCreateSecrets()).RecoveryBlob is not null;

    public string CreateRecoveryKey()
    {
        var secrets = _secrets ?? LoadOrCreateSecrets();
        var code = RecoveryKey.NewCode();
        secrets = secrets with { RecoveryBlob = RecoveryKey.Seal(secrets.KeyId, secrets.HealthKey, code) };
        SaveSecrets(secrets);
        _secrets = secrets;
        return code;
    }

    public async Task<string> BackupAsync(string folder, CancellationToken ct = default)
    {
        var secrets = _secrets ?? LoadOrCreateSecrets();
        if (secrets.RecoveryBlob is null)
        {
            throw new LocalServerException("Vor der ersten Sicherung einen Wiederherstellungsschlüssel erzeugen.");
        }

        var name = $"aispa-{TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone):yyyyMMdd-HHmmss}";
        var dump = Path.Combine(folder, name + ".dump");
        var partial = dump + ".teil";
        try
        {
            Directory.CreateDirectory(folder);
            var result = await _runner.RunAsync(_paths.Tool("pg_dump"),
            [
                "-h", "127.0.0.1", "-p", _config.DatabasePort.ToString(System.Globalization.CultureInfo.InvariantCulture), "-U", DatabaseUser,
                "-d", DatabaseName, "-Fc", "-f", partial,
            ], new Dictionary<string, string> { ["PGPASSWORD"] = secrets.DatabasePassword }, ct).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                File.Delete(partial);
                throw new LocalServerException($"Die Sicherung ist fehlgeschlagen: {result.Output.Trim()}");
            }

            // Erst der Schlüssel, dann die Sicherung unter ihrem Namen: eine Sicherung ohne Schlüssel daneben gibt es nie.
            await File.WriteAllTextAsync(Path.Combine(folder, name + ".schluessel.json"), secrets.RecoveryBlob, ct).ConfigureAwait(false);
            File.Move(partial, dump, overwrite: true);
            PruneBackups(folder);
            return dump;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LocalServerException($"Der Ordner für die Sicherung ist nicht erreichbar ({folder}): {ex.Message}");
        }
    }

    /// <summary>
    /// Sicherung ohne laufende App (Windows-Aufgabe, <see cref="ScheduledBackup"/>): startet die Datenbank nur, wenn sie nicht läuft,
    /// und beendet sie danach wieder, außer die App ist inzwischen gestartet (<paramref name="appRunning"/>) und braucht sie.
    /// </summary>
    public async Task<string> BackupUnattendedAsync(string folder, Func<bool> appRunning, CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(_paths.Cluster, "PG_VERSION")))
        {
            throw new LocalServerException("Auf diesem PC gibt es noch keine lokale Datenbank zum Sichern.");
        }

        var secrets = _secrets = LoadOrCreateSecrets();
        var status = await _runner.RunAsync(_paths.Tool("pg_ctl"), ["status", "-D", _paths.Cluster], ct: ct).ConfigureAwait(false);
        var startedHere = status.ExitCode != 0;
        if (startedHere)
        {
            Directory.CreateDirectory(_paths.Logs);
            await StartDatabaseAsync(secrets, null, ct).ConfigureAwait(false);
        }

        try
        {
            return await BackupAsync(folder, ct).ConfigureAwait(false);
        }
        finally
        {
            if (startedHere && !appRunning())
            {
                await StopAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task RestoreAsync(string dumpFile, string recoveryCode, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var keyFile = Path.ChangeExtension(dumpFile, ".schluessel.json");
        if (!File.Exists(dumpFile) || !File.Exists(keyFile))
        {
            throw new LocalServerException(
                "Sicherung oder Schlüsseldatei nicht gefunden. Beide liegen im Ordner der Sicherung: aispa-….dump und aispa-….schluessel.json.");
        }

        var sealedKey = await File.ReadAllTextAsync(keyFile, ct).ConfigureAwait(false);
        var (keyId, healthKey) = RecoveryKey.Open(sealedKey, recoveryCode);
        if (File.Exists(Path.Combine(_paths.Cluster, "PG_VERSION")) && TryLoadSecrets() is { } current)
        {
            await StartDatabaseAsync(current, null, ct).ConfigureAwait(false);
            if (await HasSpaAsync(current, ct).ConfigureAwait(false))
            {
                throw new LocalServerException(
                    "Auf diesem PC ist schon ein Spa eingerichtet. Wiederherstellen geht nur in eine leere Installation, damit nichts überschrieben wird.");
            }
        }

        progress?.Report("Lege die bisherigen Daten beiseite …");
        await StopAsync().ConfigureAwait(false);
        var stamp = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        if (Directory.Exists(_paths.Cluster))
        {
            Directory.Move(_paths.Cluster, $"{_paths.Cluster}-vor-wiederherstellung-{stamp}");
        }

        if (File.Exists(_paths.Secrets))
        {
            File.Move(_paths.Secrets, Path.Combine(_paths.DataDirectory, $"zugang-vor-wiederherstellung-{stamp}.bin"));
        }

        // Neue Datenbank mit neuem Passwort, aber dem Schlüssel aus der Sicherung; der Wiederherstellungsschlüssel gilt weiter.
        var secrets = new LocalSecrets(NewDatabasePassword(), healthKey, keyId, RecoveryBlob: sealedKey);
        SaveSecrets(secrets);
        _secrets = secrets;
        Directory.CreateDirectory(_paths.Logs);
        progress?.Report("Lege die lokale Datenbank neu an …");
        await StartDatabaseAsync(secrets, null, ct).ConfigureAwait(false);

        progress?.Report("Spiele die Sicherung ein …");
        var restored = await _runner.RunAsync(_paths.Tool("pg_restore"),
        [
            .. Connection(), "-d", DatabaseName, "--no-owner", "--no-privileges", "--exit-on-error", dumpFile,
        ], new Dictionary<string, string> { ["PGPASSWORD"] = secrets.DatabasePassword }, ct).ConfigureAwait(false);
        if (restored.ExitCode != 0)
        {
            // Halb eingespielt hilft niemandem: beiseitelegen, beim nächsten Start entsteht eine leere Datenbank.
            await StopAsync().ConfigureAwait(false);
            Directory.Move(_paths.Cluster, $"{_paths.Cluster}-abgebrochen-{stamp}");
            throw new LocalServerException($"Die Sicherung ließ sich nicht einspielen: {restored.Output.Trim()}");
        }

        await StartAsync(progress, ct).ConfigureAwait(false);
    }

    /// <summary>Gibt es in der lokalen Datenbank schon ein Spa (Zeile in <c>Tenants</c>)?</summary>
    private async Task<bool> HasSpaAsync(LocalSecrets secrets, CancellationToken ct)
    {
        var env = new Dictionary<string, string> { ["PGPASSWORD"] = secrets.DatabasePassword };
        var table = await _runner.RunAsync(_paths.Tool("psql"),
            [.. Connection(), "-d", DatabaseName, "-tAc", "SELECT count(*) FROM pg_tables WHERE schemaname = 'aispa' AND tablename = 'Tenants'"], env, ct)
            .ConfigureAwait(false);
        if (table.ExitCode != 0)
        {
            throw new LocalServerException($"Keine Verbindung zur lokalen Datenbank: {table.Output.Trim()}");
        }

        if (table.Output.Trim() == "0")
        {
            return false;
        }

        var spas = await _runner.RunAsync(_paths.Tool("psql"),
            [.. Connection(), "-d", DatabaseName, "-tAc", "SELECT count(*) FROM aispa.\"Tenants\""], env, ct).ConfigureAwait(false);
        return spas.ExitCode != 0 || spas.Output.Trim() != "0";
    }

    private string[] Connection() =>
        ["-h", "127.0.0.1", "-p", _config.DatabasePort.ToString(System.Globalization.CultureInfo.InvariantCulture), "-U", DatabaseUser];

    /// <summary>Nur eigene Dateien (aispa-JJJJMMTT-…), die neuesten bleiben.</summary>
    private static void PruneBackups(string folder)
    {
        var old = Directory.GetFiles(folder, "aispa-*.dump").Select(Path.GetFileName).OfType<string>()
            .Where(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^aispa-\d{8}-\d{6}\.dump$"))
            .OrderDescending(StringComparer.Ordinal).Skip(BackupsKept);
        foreach (var file in old)
        {
            File.Delete(Path.Combine(folder, file));
            File.Delete(Path.Combine(folder, Path.ChangeExtension(file, ".schluessel.json")));
        }
    }

    private async Task StartApiAsync(LocalSecrets secrets, CancellationToken ct)
    {
        WriteRecoveryFile(secrets);
        _api = _runner.Start(_paths.Api, [], ApiEnvironment(secrets), Path.Combine(_paths.Logs, "api.log"));
        var deadline = DateTime.UtcNow + _apiTimeout;
        while (!await _isApiReady(ApiAddress, ct).ConfigureAwait(false))
        {
            if (_api.HasExited || DateTime.UtcNow > deadline)
            {
                var log = Path.Combine(_paths.Logs, "api.log");
                throw new LocalServerException($"AISPA Studio startet auf diesem PC nicht. {LastError(log)}Protokoll: {log}");
            }

            await Task.Delay(500, ct).ConfigureAwait(false);
        }
    }

    public async Task StopAsync()
    {
        if (_api is { HasExited: false } running)
        {
            await StopApiAsync(running).ConfigureAwait(false);
        }

        if (File.Exists(Path.Combine(_paths.Cluster, "postmaster.pid")))
        {
            await _runner.RunAsync(_paths.Tool("pg_ctl"), ["stop", "-D", _paths.Cluster, "-m", "fast", "-w"]).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Bittet die API über die Stopp-Datei, sich zu beenden: laufende Anfragen, Buchungen und der Abgleich werden fertig. Erst wenn sie
    /// nach <c>stopTimeout</c> noch läuft (z. B. eine ältere API ohne Stopp-Datei), wird sie hart beendet.
    /// </summary>
    private async Task StopApiAsync(IRunningProcess api)
    {
        try
        {
            await File.WriteAllTextAsync(_paths.StopFile, "").ConfigureAwait(false);
            var waited = Stopwatch.StartNew();
            while (!api.HasExited && waited.Elapsed < _stopTimeout)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Datei nicht anlegbar: dann eben hart.
        }

        if (!api.HasExited)
        {
            await api.KillAsync().ConfigureAwait(false);
        }

        File.Delete(_paths.StopFile);
    }

    /// <summary>
    /// Umgebung der API: Einzelplatz, eigene Datenbank, Web-Oberfläche, Migration beim Start, Schlüssel für Gesundheitsdaten und, wenn
    /// eingetragen, die zentrale Datenbank für den Abgleich.
    /// </summary>
    public IReadOnlyDictionary<string, string> ApiEnvironment(LocalSecrets secrets)
    {
        var env = BaseEnvironment(secrets);
        if (secrets.CentralConnectionString is { } central)
        {
            env["Sync__CentralConnectionString"] = central;
        }

        if (secrets.ShareCode is { } shareCode)
        {
            env["HealthData__ShareCode"] = shareCode;
        }

        return env;
    }

    private Dictionary<string, string> BaseEnvironment(LocalSecrets secrets)
    {
        var env = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["ConnectionStrings__aispadb"] =
                $"Host=127.0.0.1;Port={_config.DatabasePort};Database={DatabaseName};Username={DatabaseUser};Password={secrets.DatabasePassword}",
            ["Hosting__Standalone"] = "true",
            ["Hosting__WebRoot"] = _paths.WebRoot,
            ["Hosting__StopFile"] = _paths.StopFile,
            ["Hosting__RecoveryFile"] = _paths.RecoveryFile,
            ["Database__MigrateOnStartup"] = "true",
            ["HealthData__CurrentKeyId"] = secrets.KeyId,
            [$"HealthData__Keys__{secrets.KeyId}"] = secrets.HealthKey,
        };
        var local = $"http://127.0.0.1:{_config.ApiPort}";
        if (_config.Shared && NetworkCode(secrets) is not null)
        {
            // Mehrplatz: lokal wie bisher, dazu HTTPS an allen Netzwerkkarten. Mit eigenen Endpunkten gilt ASPNETCORE_URLS nicht mehr.
            env["Kestrel__Endpoints__Lokal__Url"] = local;
            env["Kestrel__Endpoints__Netz__Url"] = $"https://*:{_config.NetworkPort}";
            env["Kestrel__Endpoints__Netz__Certificate__Path"] = _paths.NetworkCertificate;
            env["Kestrel__Endpoints__Netz__Certificate__Password"] = secrets.NetworkCertificatePassword!;
        }
        else
        {
            env["ASPNETCORE_URLS"] = local;
        }

        return env;
    }

    private async Task InitClusterAsync(LocalSecrets secrets, CancellationToken ct)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        if (Directory.Exists(_paths.Cluster))
        {
            // Rest eines abgebrochenen ersten Starts (ohne PG_VERSION); initdb verlangt einen leeren Ordner.
            Directory.Delete(_paths.Cluster, recursive: true);
        }

        var passwordFile = Path.Combine(_paths.DataDirectory, "initdb.tmp");
        await File.WriteAllTextAsync(passwordFile, secrets.DatabasePassword, ct).ConfigureAwait(false);
        try
        {
            var result = await _runner.RunAsync(_paths.Tool("initdb"),
            [
                "-D", _paths.Cluster, "-U", DatabaseUser, "--pwfile", passwordFile, "-A", "scram-sha-256", "-E", "UTF8",
                "--locale=C", "--locale-provider=icu", "--icu-locale=de-AT",
            ], ct: ct).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new LocalServerException($"Die lokale Datenbank ließ sich nicht anlegen: {result.Output.Trim()}");
            }
        }
        finally
        {
            File.Delete(passwordFile);
        }
    }

    private async Task EnsureDatabaseAsync(LocalSecrets secrets, CancellationToken ct)
    {
        var env = new Dictionary<string, string> { ["PGPASSWORD"] = secrets.DatabasePassword };
        string[] connection = ["-h", "127.0.0.1", "-p", _config.DatabasePort.ToString(System.Globalization.CultureInfo.InvariantCulture), "-U", DatabaseUser];
        var exists = await _runner.RunAsync(_paths.Tool("psql"),
            [.. connection, "-d", "postgres", "-tAc", $"SELECT 1 FROM pg_database WHERE datname = '{DatabaseName}'"], env, ct).ConfigureAwait(false);
        if (exists.ExitCode != 0)
        {
            throw new LocalServerException($"Keine Verbindung zur lokalen Datenbank: {exists.Output.Trim()}");
        }

        // Die Datenbank muss vor der API da sein, weil Wolverine vor der Migration verbindet.
        if (exists.Output.Trim() != "1")
        {
            var created = await _runner.RunAsync(_paths.Tool("createdb"), [.. connection, DatabaseName], env, ct).ConfigureAwait(false);
            if (created.ExitCode != 0)
            {
                throw new LocalServerException($"Die Datenbank {DatabaseName} ließ sich nicht anlegen: {created.Output.Trim()}");
            }
        }
    }

    private LocalServerConfig LoadOrCreateConfig()
    {
        try
        {
            if (File.Exists(_paths.Config) && JsonSerializer.Deserialize<LocalServerConfig>(File.ReadAllText(_paths.Config), ApiClient.Json) is
                { DatabasePort: > 0, ApiPort: > 0 } saved)
            {
                return saved;
            }
        }
        catch (JsonException)
        {
            // Kaputte Datei: neu wählen.
        }

        var config = new LocalServerConfig(FreePort(55432), FreePort(55086));
        SaveConfig(config);
        return config;
    }

    private void SaveConfig(LocalServerConfig config)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        File.WriteAllText(_paths.Config, JsonSerializer.Serialize(config, ApiClient.Json));
    }

    private LocalSecrets LoadOrCreateSecrets()
    {
        if (File.Exists(_paths.Secrets))
        {
            try
            {
                var json = Encoding.UTF8.GetString(_protector.Unprotect(File.ReadAllBytes(_paths.Secrets)));
                if (JsonSerializer.Deserialize<LocalSecrets>(json, ApiClient.Json) is { } saved)
                {
                    return saved;
                }
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException)
            {
                // Ohne diese Daten ist die Datenbank nicht mehr lesbar; nie still neu anlegen.
            }

            throw new LocalServerException(
                "Die Zugangsdaten der lokalen Datenbank lassen sich nicht lesen (anderes Windows-Konto oder anderer PC?). Bitte mit der Sicherung wiederherstellen.");
        }

        var secrets = new LocalSecrets(NewDatabasePassword(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            $"lokal-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}");
        SaveSecrets(secrets);
        return secrets;
    }

    private static string NewDatabasePassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    /// <summary>Die gespeicherten Zugangsdaten oder <c>null</c>, wenn sie fehlen oder nicht lesbar sind.</summary>
    private LocalSecrets? TryLoadSecrets()
    {
        try
        {
            return File.Exists(_paths.Secrets) ? LoadOrCreateSecrets() : null;
        }
        catch (LocalServerException)
        {
            return null;
        }
    }

    /// <summary>Erst in eine Nebendatei, dann umbenennen: ein Absturz beim Schreiben darf die Zugangsdaten nie zerstören.</summary>
    private void SaveSecrets(LocalSecrets secrets)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var temp = _paths.Secrets + ".neu";
        File.WriteAllBytes(temp, _protector.Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(secrets, ApiClient.Json))));
        File.Move(temp, _paths.Secrets, overwrite: true);
        WriteRecoveryFile(secrets);
    }

    /// <summary>Für „Passwort vergessen“: die API liest die Datei bei jeder Anfrage, ein neuer Schlüssel gilt also sofort.</summary>
    private void WriteRecoveryFile(LocalSecrets secrets)
    {
        if (secrets.RecoveryBlob is null)
        {
            File.Delete(_paths.RecoveryFile);
        }
        else if (!File.Exists(_paths.RecoveryFile) || File.ReadAllText(_paths.RecoveryFile) != secrets.RecoveryBlob)
        {
            File.WriteAllText(_paths.RecoveryFile, secrets.RecoveryBlob);
        }
    }

    /// <summary>Die letzte Fehlerzeile aus dem Protokoll der API, damit die Meldung in der App etwas sagt.</summary>
    /// <summary>
    /// Nur TCP an 127.0.0.1. Unter Linux und macOS zusätzlich kein Unix-Socket, weil dessen Standardordner nicht beschreibbar ist.
    /// Unter Windows ist er ohnehin aus, und pg_ctl reicht die Optionen dort über cmd.exe weiter, das <c>''</c> nicht als leeren
    /// Wert versteht.
    /// </summary>
    internal static string ServerOptions(int port) =>
        $"-p {port} -c listen_addresses=127.0.0.1" + (OperatingSystem.IsWindows() ? "" : " -c unix_socket_directories=''");

    /// <summary>Die letzten Zeilen eines Protokolls, damit die Ursache in der Fehlermeldung steht.</summary>
    private static string LogTail(string path, int lines = 5)
    {
        try
        {
            return File.Exists(path) ? string.Join(" ", File.ReadLines(path).TakeLast(lines).Select(l => l.Trim())) : "";
        }
        catch (IOException)
        {
            return "";
        }
    }

    private static string LastError(string log)
    {
        try
        {
            using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            // Ein Absturz beim Start steht als „crit:“ oder „Unhandled exception.“ im Protokoll, die Meldung in der Zeile danach.
            var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var index = Array.FindLastIndex(lines, l => l.StartsWith("crit:", StringComparison.Ordinal)
                || l.StartsWith("Unhandled exception.", StringComparison.Ordinal));
            var message = index < 0 ? null
                : lines[index].StartsWith("Unhandled exception.", StringComparison.Ordinal) ? lines[index]["Unhandled exception.".Length..].Trim()
                : index + 1 < lines.Length ? lines[index + 1] : null;
            return string.IsNullOrWhiteSpace(message) ? "" : $"{message} ";
        }
        catch (IOException)
        {
            return "";
        }
    }

    /// <summary>Der Wunsch-Port, wenn frei; sonst einer, den das System vergibt.</summary>
    private static int FreePort(int preferred, IPAddress? address = null)
    {
        foreach (var port in new[] { preferred, 0 })
        {
            try
            {
                var listener = new TcpListener(address ?? IPAddress.Loopback, port);
                listener.Start();
                var chosen = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return chosen;
            }
            catch (SocketException)
            {
                // Belegt: nächster Versuch.
            }
        }

        throw new LocalServerException("Kein freier Port für den Einzelplatz-Betrieb.");
    }

    private static async Task<bool> PingAsync(Uri address, CancellationToken ct)
    {
        using var http = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            using var response = await http.GetAsync("api/system/status", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}

/// <summary>Echte Prozesse ohne Fenster; Ausgabe der API in eine Protokolldatei.</summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string file, IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? env = null,
        CancellationToken ct = default)
    {
        using var process = new Process { StartInfo = Info(file, args, env), EnableRaisingEvents = true };
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        var output = new StringBuilder();
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new CountdownEvent(2);
        process.OutputDataReceived += (_, e) => Collect(output, e.Data, closed);
        process.ErrorDataReceived += (_, e) => Collect(output, e.Data, closed);
        process.Exited += (_, _) => exited.TrySetResult();
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Nicht auf das Ende der Ausgabe warten: Unter Windows erbt der von „pg_ctl start“ gestartete Server die Pipes und hält sie
        // offen, solange er läuft. WaitForExitAsync und ReadToEnd würden dann nie zurückkehren.
        await exited.Task.WaitAsync(ct).ConfigureAwait(false);
        await Task.Run(() => closed.Wait(TimeSpan.FromSeconds(2)), ct).ConfigureAwait(false);
        lock (output)
        {
            return new ProcessResult(process.ExitCode, output.ToString());
        }
    }

    private static void Collect(StringBuilder output, string? line, CountdownEvent closed)
    {
        if (line is null)
        {
            closed.Signal();
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }

    public IRunningProcess Start(string file, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, string logFile)
    {
        var process = new Process { StartInfo = Info(file, args, env), EnableRaisingEvents = true };
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        var log = new StreamWriter(logFile, append: false) { AutoFlush = true };
        process.OutputDataReceived += (_, e) => Write(log, e.Data);
        process.ErrorDataReceived += (_, e) => Write(log, e.Data);
        process.Exited += (_, _) => log.Dispose();
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new Running(process);
    }

    private static void Write(StreamWriter log, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (log)
        {
            try
            {
                log.WriteLine(line);
            }
            catch (ObjectDisposedException)
            {
                // Prozess schon beendet.
            }
        }
    }

    private static ProcessStartInfo Info(string file, IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? env)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(file)! };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env ?? new Dictionary<string, string>())
        {
            info.Environment[key] = value;
        }

        return info;
    }

    private sealed class Running(Process process) : IRunningProcess
    {
        public bool HasExited => process.HasExited;

        public async Task KillAsync()
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
    }
}
