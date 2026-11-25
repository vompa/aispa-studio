using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.System;
using Npgsql;

namespace AispaStudio.Api.Demo;

/// <summary>Was die Prüfung auf veraltete Beispieldaten ergeben hat.</summary>
internal enum StaleDemoOutcome
{
    Fresh,
    Recreated,
    Stale,
}

/// <summary>Ergebnis der Prüfung; <see cref="Notice"/> ist der Hinweis für Log und Anmeldemaske, wenn nicht neu angelegt werden durfte.</summary>
internal sealed record StaleDemoCheck(StaleDemoOutcome Outcome, DateTimeOffset? LatestStart = null, string? Failure = null)
{
    public static readonly StaleDemoCheck Fresh = new(StaleDemoOutcome.Fresh);

    public string Notice => Outcome == StaleDemoOutcome.Stale
        ? $"Beispieldaten abgelaufen (letzter Termin {LatestStart!.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}). "
            + "Neu einspielen: im Aspire-Dashboard an postgres „Beispiel-Spa neu einspielen …“, sonst die Datenbank leeren und neu starten."
        : "";
}

/// <summary>
/// Beispieldaten entstehen einmal und reichen nur <see cref="DemoDataOptions.WeeksAhead"/> Wochen voraus; danach ist der Kalender leer.
/// Beim Start prüft die API deshalb den spätesten Termin. Veraltete Beispieldaten legt sie im Debug-Build und im Beispiel-Spa
/// (<c>Demo:Enabled</c>, ADR 0100) neu an, sonst meldet sie sie nur. Gelöscht wird nur eine Datenbank mit Konten der Beispiel-Domain
/// (eine eingespielte Sicherung bleibt unberührt), nur von der Instanz, die migriert, und nie im Befehl <c>migrate</c>.
/// Läuft vor DbContext und Wolverine, weil das Neuanlegen die Datenbank löscht.
/// </summary>
internal static partial class StaleDemoData
{
    /// <summary>Schaltet das Neuanlegen ab (Standard: an); die Tests setzen es aus, weil sie mit einer Woche Vorschau einspielen.</summary>
    public const string RefreshSetting = "DemoData:RefreshWhenStale";

    /// <summary>
    /// Kommentar an der Datenbank, den „Sicherung einspielen …“ im AppHost setzt (gleicher Wert in <c>DatabaseCommands</c>): eine eingespielte
    /// Sicherung wird nie als veraltete Beispieldaten neu angelegt, auch wenn sie Beispielkonten enthält.
    /// </summary>
    public const string RestoredBackupComment = "aispa:restored-backup";

    private static readonly TimeSpan MaximumLead = TimeSpan.FromDays(14);
    private const string MaintenanceDatabase = "postgres";
    private const string InvalidCatalog = "3D000";

#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif

    /// <summary>
    /// Veraltet, wenn der späteste Termin vor jetzt plus 14 Tagen liegt (bei kurzer Vorschau vor ihrer Hälfte, sonst wäre jeder Start
    /// ein Neuanlegen). Ohne Termine ist nichts veraltet: eine leere Datenbank füllt der <see cref="DemoSeeder"/> ohnehin.
    /// </summary>
    public static bool IsStale(DateTimeOffset? latestStart, DateTimeOffset now, int weeksAhead)
    {
        if (latestStart is null)
        {
            return false;
        }

        var lead = TimeSpan.FromDays(Math.Min(MaximumLead.TotalDays, weeksAhead * 7 / 2.0));
        return latestStart < now + lead;
    }

    /// <summary>Prüft die Datenbank der Verbindung <paramref name="connectionName"/> und legt sie neu an, wenn es erlaubt ist.</summary>
    public static Task<StaleDemoCheck> CheckAsync(IConfiguration configuration, string connectionName, bool migrateCommand, CancellationToken ct = default)
    {
        var connection = configuration.GetConnectionString(connectionName)!;
        var demo = DemoSpa.IsEnabled(configuration);
        var mayRecreate = configuration.GetValue(RefreshSetting, true)
            && configuration.GetValue<bool>("Database:MigrateOnStartup")
            && !migrateCommand
            && (demo || IsDebugBuild);
        var recreate = mayRecreate ? Recreation(connection, demo) : null;
        var weeksAhead = configuration.GetValue($"{DemoDataOptions.Section}:{nameof(DemoDataOptions.WeeksAhead)}", new DemoDataOptions().WeeksAhead);
        return HandleAsync(connection, TimeProvider.System.GetUtcNow(), weeksAhead, recreate, ct);
    }

    /// <summary>Im Beispiel-Spa über <see cref="DemoSpa"/> mit festem Namen (ADR 0100), sonst die Datenbank der Verbindung.</summary>
    private static Func<CancellationToken, Task> Recreation(string connection, bool demo)
    {
        if (!demo)
        {
            return token => RecreateDatabaseAsync(connection, token);
        }

        return async token =>
        {
            await DemoSpa.DropAsync(connection, token);
            await DemoSpa.EnsureDatabaseAsync(connection, token);
        };
    }

    /// <summary>Kern der Prüfung; <paramref name="recreate"/> ist <c>null</c>, wenn nicht neu angelegt werden darf.</summary>
    public static async Task<StaleDemoCheck> HandleAsync(
        string connection, DateTimeOffset now, int weeksAhead, Func<CancellationToken, Task>? recreate, CancellationToken ct)
    {
        var latest = await LatestStartAsync(connection, ct);
        if (!IsStale(latest, now, weeksAhead) || !await HasDemoAccountsAsync(connection, ct) || await IsRestoredBackupAsync(connection, ct))
        {
            return StaleDemoCheck.Fresh;
        }

        if (recreate is null)
        {
            return new StaleDemoCheck(StaleDemoOutcome.Stale, latest);
        }

        try
        {
            await recreate(ct);
            return new StaleDemoCheck(StaleDemoOutcome.Recreated, latest);
        }
        catch (NpgsqlException ex)
        {
            // Z. B. ein Konto ohne Recht, Datenbanken zu löschen oder anzulegen (ADR 0100): dann nur melden, der Start geht weiter.
            return new StaleDemoCheck(StaleDemoOutcome.Stale, latest, ex.Message);
        }
    }

    /// <summary>Beginn des spätesten Termins; <c>null</c> ohne Datenbank, ohne Tabelle oder ohne Termine.</summary>
    public static async Task<DateTimeOffset?> LatestStartAsync(string connection, CancellationToken ct)
    {
        var start = await ScalarAsync(connection, "aispa.\"Appointments\"", "SELECT max(\"Start\") FROM aispa.\"Appointments\"", ct);
        return start is DateTime value ? new DateTimeOffset(value, TimeSpan.Zero) : null;
    }

    /// <summary>Legt die Datenbank der Verbindung leer neu an; Migrator und <see cref="DemoSeeder"/> füllen sie danach wie beim ersten Start.</summary>
    public static async Task RecreateDatabaseAsync(string connection, CancellationToken ct)
    {
        var name = new NpgsqlConnectionStringBuilder(connection).Database!;
        var quoted = "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        // Verbindungen des eigenen Pools zuerst schließen, sonst bleiben sie bis zum Zwangsende offen.
        NpgsqlConnection.ClearAllPools();
        await using var server = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connection) { Database = MaintenanceDatabase, Pooling = false }.ConnectionString);
        await server.OpenAsync(ct);
        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {quoted} WITH (FORCE)", server))
        {
            await drop.ExecuteNonQueryAsync(ct);
        }

        await using var create = new NpgsqlCommand($"CREATE DATABASE {quoted}", server);
        await create.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> IsRestoredBackupAsync(string connection, CancellationToken ct)
    {
        const string Sql = "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database()";
        return await ScalarAsync(connection, "pg_catalog.pg_database", Sql, ct) is RestoredBackupComment;
    }

    private static async Task<bool> HasDemoAccountsAsync(string connection, CancellationToken ct)
    {
        const string Sql = "SELECT EXISTS (SELECT 1 FROM aispa.\"AspNetUsers\" WHERE \"NormalizedEmail\" LIKE @suffix)";
        return await ScalarAsync(connection, "aispa.\"AspNetUsers\"", Sql, ct, ("suffix", "%@" + DemoAccounts.Domain.ToUpperInvariant())) is true;
    }

    /// <summary>Ein Wert aus <paramref name="sql"/>; <c>null</c>, wenn die Datenbank oder die Tabelle <paramref name="table"/> fehlt.</summary>
    private static async Task<object?> ScalarAsync(
        string connection, string table, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        try
        {
            await using var db = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString);
            await db.OpenAsync(ct);
            // Eine fehlende Tabelle scheitert schon beim Parsen; deshalb vorher nachsehen statt in derselben Abfrage.
            await using (var exists = new NpgsqlCommand("SELECT to_regclass(@table) IS NOT NULL", db))
            {
                exists.Parameters.AddWithValue("table", table);
                if (await exists.ExecuteScalarAsync(ct) is not true)
                {
                    return null;
                }
            }

            await using var command = new NpgsqlCommand(sql, db);
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            var result = await command.ExecuteScalarAsync(ct);
            return result is DBNull ? null : result;
        }
        catch (PostgresException ex) when (ex.SqlState == InvalidCatalog)
        {
            // Die Datenbank gibt es noch nicht: nichts veraltet.
            return null;
        }
    }

    /// <summary>Meldet das Ergebnis im Log und gibt den Hinweis an den Startzustand (Health-Check, <c>/api/system/status</c>, Anmeldemaske).</summary>
    public static void LogStaleDemoData(this WebApplication app, StaleDemoCheck check)
    {
        var database = new NpgsqlConnectionStringBuilder(app.Configuration.GetConnectionString("aispadb")).Database ?? "";
        switch (check.Outcome)
        {
            case StaleDemoOutcome.Recreated:
                Recreated(app.Logger, check.LatestStart!.Value, database);
                break;
            case StaleDemoOutcome.Stale:
                if (check.Failure is not null)
                {
                    RecreateFailed(app.Logger, database, check.Failure);
                }

                StaleOnly(app.Logger, check.Notice);
                app.Services.GetRequiredService<StartupState>().Notice = check.Notice;
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Beispieldaten veraltet (letzter Termin {LatestStart:dd.MM.yyyy}): Datenbank {Database} neu angelegt, das Beispiel-Spa wird neu eingespielt.")]
    private static partial void Recreated(ILogger logger, DateTimeOffset latestStart, string database);

    [LoggerMessage(Level = LogLevel.Error, Message = "Beispieldaten veraltet, Datenbank {Database} ließ sich nicht neu anlegen: {Failure}")]
    private static partial void RecreateFailed(ILogger logger, string database, string failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Notice}")]
    private static partial void StaleOnly(ILogger logger, string notice);
}
