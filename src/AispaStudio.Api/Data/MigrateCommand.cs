using AispaStudio.Api.System;
using Npgsql;

namespace AispaStudio.Api.Data;

/// <summary>
/// <c>dotnet AispaStudio.Api.dll migrate</c> (ADR 0087, Aufgabe D): spielt die Migrationen einmal ein und beendet sich, ohne Host zu
/// starten. So migriert bei mehreren Instanzen ein eigener Lauf je Update (Compose-Dienst, Kubernetes-Job) statt jeder Instanz beim Start.
/// Exitcode 0 bei Erfolg, 1 bei einem Fehler der Migration. Fehler der Konfiguration vor dem Bau des Hosts (fehlende Verbindung,
/// ungültige Rolle) enden mit einem anderen Code ungleich 0.
/// </summary>
internal static partial class MigrateCommand
{
    /// <summary>PostgreSQL-Fehlercode „Datenbank existiert nicht“.</summary>
    private const string InvalidCatalogName = "3D000";

    /// <summary>Genau das erste Argument „migrate“, ohne Rücksicht auf Groß- und Kleinschreibung; weitere Optionen liest die Konfiguration.</summary>
    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(WebApplication app, string connectionString, DataProtectionStore keyStore)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(MigrateCommand));
        try
        {
            // EF legt eine fehlende Datenbank still an; ein Tippfehler im Namen ergäbe sonst eine leere Zweitdatenbank.
            await EnsureDatabaseExistsAsync(connectionString);
            await using var scope = app.Services.CreateAsyncScope();
            await DatabaseMigrations.ApplyAsync(scope.ServiceProvider, CancellationToken.None, async () =>
            {
                if (keyStore == DataProtectionStore.Database)
                {
                    // Beides lässt sich wiederholen; jeder API-Start tut es ohnehin.
                    var keys = scope.ServiceProvider.GetRequiredService<KeyStoreMigration>();
                    await keys.MigrateAsync(CancellationToken.None);
                    await keys.ImportAsync(CancellationToken.None);
                }
            });

            Finished(logger);
            return 0;
        }
        catch (DatabaseMissingException missing)
        {
            DatabaseMissing(logger, missing.Name);
            return 1;
        }
        catch (Exception error)
        {
            Failed(logger, error);
            return 1;
        }
    }

    private static async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (PostgresException error) when (error.SqlState == InvalidCatalogName)
        {
            throw new DatabaseMissingException(new NpgsqlConnectionStringBuilder(connectionString).Database ?? "");
        }
    }

    private sealed class DatabaseMissingException(string name) : Exception
    {
        public string Name { get; } = name;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Migration abgeschlossen")]
    private static partial void Finished(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Die Datenbank „{Name}“ gibt es nicht. Zuerst anlegen und Benutzer und Rechte prüfen "
        + "(ConnectionStrings:aispadb); der Befehl legt sie bewusst nicht selbst an.")]
    private static partial void DatabaseMissing(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Die Migration ist fehlgeschlagen")]
    private static partial void Failed(ILogger logger, Exception error);
}
