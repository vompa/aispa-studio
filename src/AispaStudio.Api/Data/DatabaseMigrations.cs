using AispaStudio.Api.Online;
using AispaStudio.Api.Sync;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AispaStudio.Api.Data;

/// <summary>
/// Die Schritte der Datenbankmigration, gemeinsam für den Start mit <c>Database:MigrateOnStartup</c> (<see cref="DatabaseMigrator"/>)
/// und den Befehl <c>migrate</c> (<see cref="MigrateCommand"/>), damit beide dasselbe tun.
/// </summary>
internal static partial class DatabaseMigrations
{
    /// <summary>Schlüssel der Advisory-Sperre (ASCII „AISPMIGR“), gemeinsam für alle Läufe gegen dieselbe Datenbank.</summary>
    private const long MigrationLockKey = 0x41495350_4D494752;

    /// <summary>
    /// Migriert unter einer Sperre, die zwei gleichzeitige Läufe nacheinander ablaufen lässt: Entity Framework sperrt nur je
    /// Migrationstransaktion, ein zweiter Lauf spielte sonst dieselbe Migration noch einmal ein (42P07). <paramref name="further"/>
    /// läuft zuletzt noch unter der Sperre (Schlüsselring des Befehls).
    /// </summary>
    public static async Task ApplyAsync(IServiceProvider scopedServices, CancellationToken cancellationToken, Func<Task>? further = null)
    {
        var aispa = scopedServices.GetRequiredService<AispaDbContext>();
        var logger = scopedServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseMigrations));
        await using var gate = await MigrationLock.TryAcquireAsync(
            aispa.Database.GetConnectionString(), () => WaitingForLock(logger), cancellationToken);
        await aispa.Database.MigrateAsync(cancellationToken);
        await scopedServices.GetRequiredService<OnlineDbContext>().Database.MigrateAsync(cancellationToken);
        // Änderungsprotokoll für den Abgleich an alle Tabellen, auch an die aus Migrationen dieses Laufs (ADR 0041).
        await ChangeLog.AttachAsync(aispa, cancellationToken);
        if (further is not null)
        {
            await further();
        }
    }

    private sealed class MigrationLock(NpgsqlConnection? connection) : IAsyncDisposable
    {
        public static async Task<MigrationLock> TryAcquireAsync(
            string? connectionString, Action onWaiting, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return new MigrationLock(null);
            }

            var connection = new NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
            }
            catch (PostgresException error) when (error.SqlState == "3D000")
            {
                // Mit Database:MigrateOnStartup legt Entity Framework eine fehlende Datenbank an (Einzelplatz, Aspire, Tests). Dann gibt
                // es noch nichts, was ein zweiter Lauf stören könnte; ohne Sperre weitermachen. Der Befehl prüft die Datenbank vorher selbst.
                await connection.DisposeAsync();
                return new MigrationLock(null);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }

            // Erst ohne Warten versuchen: nur wer wirklich warten muss, meldet das. Das Warten selbst hat bewusst kein Befehls-Zeitlimit
            // (Npgsql-Standard 30 s): ein laufender Lauf darf länger migrieren, begrenzt nur das CancellationToken.
            if (!await TryLockAsync(connection, "pg_try_advisory_lock", commandTimeout: null, cancellationToken))
            {
                onWaiting();
                await TryLockAsync(connection, "pg_advisory_lock", commandTimeout: 0, cancellationToken);
            }

            return new MigrationLock(connection);
        }

        private static async Task<bool> TryLockAsync(
            NpgsqlConnection connection, string function, int? commandTimeout, CancellationToken cancellationToken)
        {
            await using var command = new NpgsqlCommand($"SELECT {function}(@key)", connection);
            if (commandTimeout is not null)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            command.Parameters.AddWithValue("key", MigrationLockKey);
            // pg_advisory_lock liefert void (null), pg_try_advisory_lock true oder false.
            return await command.ExecuteScalarAsync(cancellationToken) is not false;
        }

        public async ValueTask DisposeAsync()
        {
            if (connection is null)
            {
                return;
            }

            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
                command.Parameters.AddWithValue("key", MigrationLockKey);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                // Schließen gibt die Sperre der Sitzung in jedem Fall frei.
                await connection.DisposeAsync();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Warte auf die Migrationssperre: ein anderer Lauf migriert diese Datenbank gerade")]
    private static partial void WaitingForLock(ILogger logger);
}
