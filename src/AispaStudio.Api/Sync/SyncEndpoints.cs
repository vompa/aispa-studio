using System.Data.Common;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Sync;

/// <summary>Stand des Abgleichs und abgelehnte Zeilen für die Rezeption (ADR 0041, Schritt 4c).</summary>
internal static class SyncEndpoints
{
    private static readonly Dictionary<string, string> Areas = new(StringComparer.Ordinal)
    {
        ["Appointments"] = "Termine",
        ["ResourceBookings"] = "Termine",
        ["AreaVisits"] = "Eintritte",
        ["Customers"] = "Kunden",
        ["RosterDays"] = "Dienstplan",
        ["Receipts"] = "Kassa",
    };

    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/system/sync").WithTags("System");

        group.MapGet("", async (AispaDbContext db, IOptions<SyncOptions> options, CancellationToken ct) =>
            {
                var enabled = !string.IsNullOrWhiteSpace(options.Value.CentralConnectionString);
                var connection = await OpenAsync(db, ct);
                if (await ScalarAsync(connection, "SELECT to_regclass('sync.peers') IS NOT NULL", ct) is not true)
                {
                    return new SyncStatus(enabled, null, null, 0, []);
                }

                DateTimeOffset? lastRun = null;
                string? lastError = null;
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT last_run, last_error FROM sync.peers WHERE peer = 'zentrale'";
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        lastRun = reader.IsDBNull(0) ? null : reader.GetFieldValue<DateTimeOffset>(0);
                        lastError = reader.IsDBNull(1) ? null : reader.GetString(1);
                    }
                }

                var pending = enabled ? (long)(await ScalarAsync(connection, "SELECT count(*) FROM sync.changes WHERE origin IS NULL", ct))! : 0;
                var conflicts = new List<SyncConflict>();
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = """
                        SELECT id, table_name, row_key::text, reason, detected_at FROM sync.conflicts
                         WHERE resolved_at IS NULL AND (tenant_id = @tenant OR tenant_id IS NULL) ORDER BY id
                        """;
                    Add(command, "tenant", db.CurrentTenantId);
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        var table = reader.GetString(1);
                        conflicts.Add(new SyncConflict(reader.GetInt64(0), Areas.GetValueOrDefault(table, "Sonstiges"), table, reader.GetString(2),
                            reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4)));
                    }
                }

                return new SyncStatus(enabled, lastRun, lastError, pending, conflicts);
            })
            .RequirePermission(AccessRight.ViewCalendar)
            .WithName("GetSyncStatus")
            .WithSummary("Stand des Abgleichs mit der zentralen Datenbank und offene, abgelehnte Zeilen.");

        group.MapPost("/conflicts/{id:long}/resolve", async (long id, AispaDbContext db, CancellationToken ct) =>
            {
                var connection = await OpenAsync(db, ct);
                if (await ScalarAsync(connection, "SELECT to_regclass('sync.conflicts') IS NOT NULL", ct) is not true)
                {
                    return Results.NotFound();
                }

                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE sync.conflicts SET resolved_at = now()
                     WHERE id = @id AND resolved_at IS NULL AND (tenant_id = @tenant OR tenant_id IS NULL)
                    """;
                Add(command, "id", id);
                Add(command, "tenant", db.CurrentTenantId);
                return await command.ExecuteNonQueryAsync(ct) == 1 ? Results.NoContent() : Results.NotFound();
            })
            .RequirePermission(AccessRight.Book)
            .WithName("ResolveSyncConflict")
            .WithSummary("Markiert eine abgelehnte Zeile als erledigt, nachdem die Rezeption sie geklärt hat.");

        return routes;
    }

    private static async Task<DbConnection> OpenAsync(AispaDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != global::System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(ct);
        }

        return connection;
    }

    private static async Task<object?> ScalarAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct);
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
