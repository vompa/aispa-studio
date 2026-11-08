using System.Diagnostics;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Der Befehl <c>dotnet AispaStudio.Api.dll migrate</c> (ADR 0087, Aufgabe D) als eigener Prozess mit der Api-DLL aus der Testausgabe:
/// Migration ohne Host-Start, Exitcode 0 bei Erfolg. Die Umgebung des Kindprozesses ist abgeschlossen: Production (lädt keine
/// User-Secrets), leeres APPDATA/HOME, kein lokales Modell, nie der Schlüsselordner des Rechners.
/// </summary>
public sealed class MigrateCommandTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task Leere_Datenbank_wird_migriert_und_das_Aenderungsprotokoll_angehaengt()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);

        var result = await RunAsync(connection, ["migrate"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Migration abgeschlossen", result.Output, StringComparison.Ordinal);
        using var scope = fixture.Factory.Services.CreateScope();
        var aispa = scope.ServiceProvider.GetRequiredService<AispaDbContext>().Database.GetMigrations().Count();
        var online = scope.ServiceProvider.GetRequiredService<OnlineDbContext>().Database.GetMigrations().Count();
        Assert.Equal(aispa, await ScalarAsync(connection, """SELECT count(*) FROM public."__EFMigrationsHistory" """));
        Assert.Equal(online, await ScalarAsync(connection, """SELECT count(*) FROM online."__EFMigrationsHistory" """));
        // Änderungsprotokoll (ChangeLog.InstallSql) liegt im Schema sync.
        Assert.Equal(1, await ScalarAsync(connection, "SELECT count(*) FROM pg_class WHERE oid = to_regclass('sync.changes')"));
    }

    [Fact]
    public async Task Zweiter_Lauf_aendert_nichts_und_endet_mit_0()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        Assert.Equal(0, (await RunAsync(connection, ["migrate"])).ExitCode);
        var before = await ScalarAsync(connection, """SELECT count(*) FROM public."__EFMigrationsHistory" """)
            + await ScalarAsync(connection, """SELECT count(*) FROM online."__EFMigrationsHistory" """);

        var second = await RunAsync(connection, ["migrate"]);

        Assert.Equal(0, second.ExitCode);
        var after = await ScalarAsync(connection, """SELECT count(*) FROM public."__EFMigrationsHistory" """)
            + await ScalarAsync(connection, """SELECT count(*) FROM online."__EFMigrationsHistory" """);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Befehl_startet_keinen_Host_auch_nicht_mit_weiteren_Optionen()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);

        // Groß- und Kleinschreibung egal; weitere --Schlüssel=Wert liest die Konfiguration, der Befehl bleibt ein Befehl.
        var result = await RunAsync(connection, ["Migrate", "--Logging:LogLevel:Default=Warning"]);

        Assert.Equal(0, result.ExitCode);
        // Wolverine legt sein Schema erst beim Start des Hosts an (bleibt bei den Instanzen, ADR 0087).
        Assert.Equal(0, await ScalarAsync(connection, "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'wolverine'"));
    }

    [Fact]
    public async Task Mit_DataProtection_Database_entsteht_die_Schluesseltabelle_und_die_Schluessel_werden_uebernommen()
    {
        using var folder = new TempFolder();
        var keyId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(folder.Path, $"key-{keyId}.xml"), $"""<key id="{keyId}" version="1" />""", Ct);
        var connection = await fixture.CreateDatabaseAsync(Ct);

        var result = await RunAsync(connection, ["migrate"], new Dictionary<string, string>
        {
            ["DataProtection__Store"] = "Database",
            ["DataProtection__ImportFolder"] = folder.Path,
        });

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, await ScalarAsync(connection, """SELECT count(*) FROM pg_class WHERE oid = to_regclass('system."DataProtectionKeys"')"""));
        Assert.Equal(1, await ScalarAsync(connection, $"""SELECT count(*) FROM system."DataProtectionKeys" WHERE "FriendlyName" = 'key-{keyId}'"""));
        // Nur der ASCII-Teil der Zeile „1 Schlüssel aus …“: die Konsolen-Codepage des Kindprozesses ist nicht festgelegt.
        Assert.Contains("1 Schl", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zwei_Laeufe_zugleich_gegen_eine_frische_Datenbank_enden_beide_mit_0()
    {
        using var empty = new TempFolder();
        var connection = await fixture.CreateDatabaseAsync(Ct);
        var keyStore = new Dictionary<string, string>
        {
            ["DataProtection__Store"] = "Database",
            ["DataProtection__ImportFolder"] = empty.Path,
        };

        var results = await Task.WhenAll(RunAsync(connection, ["migrate"], keyStore), RunAsync(connection, ["migrate"], keyStore));

        Assert.All(results, r => Assert.True(r.ExitCode == 0, r.Output));
    }

    [Fact]
    public async Task Wartender_Lauf_haengt_nicht_am_Befehls_Zeitlimit_und_meldet_das_Warten()
    {
        var connection = await fixture.CreateDatabaseAsync(Ct);
        Assert.Equal(0, (await RunAsync(connection, ["migrate"])).ExitCode);
        // Schlüssel der Sperre (ASCII „AISPMIGR“), wie in DatabaseMigrations; ein zweiter Lauf auf der migrierten Datenbank hat danach nichts mehr zu tun.
        const long LockKey = 0x41495350_4D494752;
        var shortLimit = new NpgsqlConnectionStringBuilder(connection) { CommandTimeout = 3 }.ConnectionString;
        await using var holder = new NpgsqlConnection(connection);
        await holder.OpenAsync(Ct);
        await using (var acquire = new NpgsqlCommand($"SELECT pg_advisory_lock({LockKey})", holder))
        {
            await acquire.ExecuteNonQueryAsync(Ct);
        }

        var waiting = RunAsync(shortLimit, ["migrate"]);
        // Erst loslassen, wenn der Lauf wirklich an der Sperre wartet, und dann länger als das Befehls-Zeitlimit.
        var deadline = DateTime.UtcNow + Limit;
        while (await ScalarAsync(connection, "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted") == 0)
        {
            Assert.True(DateTime.UtcNow < deadline, "Der Lauf wartete nie an der Sperre.");
            await Task.Delay(200, Ct);
        }

        await Task.Delay(TimeSpan.FromSeconds(6), Ct);
        await using (var release = new NpgsqlCommand($"SELECT pg_advisory_unlock({LockKey})", holder))
        {
            await release.ExecuteNonQueryAsync(Ct);
        }

        var result = await waiting;

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("Warte auf die Migrationssperre", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fehlende_Datenbank_endet_mit_1_und_wird_nicht_angelegt()
    {
        var existing = await fixture.CreateDatabaseAsync(Ct);
        var missingName = $"aispa_missing_{Guid.NewGuid():N}";
        var missing = new NpgsqlConnectionStringBuilder(existing) { Database = missingName }.ConnectionString;

        var result = await RunAsync(missing, ["migrate"]);

        Assert.Equal(1, result.ExitCode);
        // Ohne die typografischen Anführungszeichen: die Konsolen-Codepage des Kindprozesses ist nicht festgelegt (cp850 unter Windows).
        Assert.Contains(missingName, result.Output, StringComparison.Ordinal);
        Assert.Contains("gibt es nicht", result.Output, StringComparison.Ordinal);
        Assert.Equal(0, await ScalarAsync(existing, $"SELECT count(*) FROM pg_database WHERE datname = '{missingName}'"));
    }

    [Fact]
    public async Task Fehlende_Verbindung_endet_mit_einem_Code_ungleich_0()
    {
        var result = await RunAsync(connection: null, ["migrate"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Keine Datenbankverbindung", result.Output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string? connection, string[] args, Dictionary<string, string>? extra = null)
    {
        using var home = new TempFolder();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "AispaStudio.Api.dll"));
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        // Nichts vom Rechner oder vom Testlauf erben: weder Verbindung noch Umgebung noch Rolle.
        foreach (var name in start.Environment.Keys.Where(k =>
            k.StartsWith("ConnectionStrings__", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("DataProtection__", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("Database__", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("Hosting__", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(name);
        }

        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["APPDATA"] = home.Path;
        start.Environment["LOCALAPPDATA"] = home.Path;
        start.Environment["HOME"] = home.Path;
        start.Environment["Ai__Local__Url"] = "";
        start.Environment["DataProtection__ImportFolder"] = home.Path;
        if (connection is not null)
        {
            start.Environment["ConnectionStrings__aispadb"] = connection;
        }

        foreach (var (key, value) in extra ?? [])
        {
            start.Environment[key] = value;
        }

        using var process = Process.Start(start)!;
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(Limit);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // Läuft der Befehl nicht von selbst aus (z. B. startet er den Server), den ganzen Prozessbaum beenden.
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Der Befehl endete nicht innerhalb von {Limit}. Ausgabe:{Environment.NewLine}{output}");
        }

        // Ohne diesen Aufruf fehlen die letzten Zeilen der asynchronen Ausgabe.
        process.WaitForExit();
        return (process.ExitCode, output.ToString());
    }

    private static void Append(StringBuilder output, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }

    private static async Task<long> ScalarAsync(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, db);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    /// <summary>Eigener leerer Ordner je Aufruf, nie der Standardordner des Rechners.</summary>
    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("aispa-migrate-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
