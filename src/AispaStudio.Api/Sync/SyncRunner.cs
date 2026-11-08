using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace AispaStudio.Api.Sync;

/// <summary>Ergebnis eines Abgleichs: gesendete und geholte Zeilen, abgelehnte Zeilen (Konflikte).</summary>
public sealed record SyncResult(int Pushed, int Pulled, int Conflicts);

/// <summary>
/// Gleicht eine Einzelplatz-Datenbank mit der zentralen PostgreSQL ab (ADR 0041, Schritt 4b). Grundlage ist das Änderungsprotokoll
/// <c>sync.changes</c> auf beiden Seiten (<see cref="ChangeLog"/>). Übertragen wird der aktuelle Stand jeder geänderten Zeile;
/// gelöschte Zeilen werden gelöscht. Gelesen werden nur abgeschlossene Transaktionen unterhalb von <c>pg_snapshot_xmin</c>, der
/// Stand je Gegenstelle liegt in <c>sync.peers</c>.
/// Mit <paramref name="healthKeys"/> tauschen die Stellen vorher ihre Schlüssel für Gesundheitsdaten aus (ADR 0048).
/// Nach <paramref name="centralRetention"/> räumt jede Stelle das Protokoll der zentralen Seite auf; wer länger nicht geholt hat (oder
/// neu dazukommt), holt dann den ganzen Bestand seines Spas.
/// </summary>
public sealed class SyncRunner(string localConnectionString, string centralConnectionString, string siteId, HealthKeyExchange? healthKeys = null,
    TimeSpan? centralRetention = null)
{
    /// <summary>So lange bleiben Änderungen im Protokoll der zentralen Seite, wenn nichts anderes eingestellt ist.</summary>
    public static readonly TimeSpan DefaultCentralRetention = TimeSpan.FromDays(90);

    /// <summary>Herkunft der Änderungen, die von der zentralen Seite kommen (im lokalen Protokoll).</summary>
    public const string CentralOrigin = "zentrale";

    /// <summary>Kassa: Belege und Kassenjournal werden nur eingefügt, nie überschrieben oder gelöscht (ADR 0041 Abschnitt 4, ADR 0010).</summary>
    private static readonly HashSet<string> InsertOnly =
        ["aispa.Receipts", "aispa.ReceiptLine", "aispa.ReceiptPayment", "aispa.CashMovements", "aispa.DayClosings"];

    private const string Peer = "zentrale";

    public async Task<SyncResult> RunOnceAsync(CancellationToken ct)
    {
        await using var local = await OpenAsync(localConnectionString, ct);
        await using var central = await OpenAsync(centralConnectionString, ct);
        await ChangeLogInstall(local, ct);
        await ChangeLogInstall(central, ct);

        await Execute(local, $"INSERT INTO sync.peers (peer) VALUES ('{Peer}') ON CONFLICT DO NOTHING", ct);

        try
        {
            var tenant = await TenantAsync(local, central, ct);
            if (healthKeys is not null)
            {
                await healthKeys.ExchangeAsync(local, central, tenant, ct);
            }

            var conflicts = new List<Conflict>();

            // Senden: eigene Änderungen (ohne Herkunft) an die zentrale Seite.
            var (pushedBelow, pulledBelow) = await PeerStateAsync(local, ct);
            var (outgoing, pushUpper) = await ReadChangesAsync(local, pushedBelow,
                "origin IS NULL", [], ct);
            await ApplyAsync(central, outgoing, siteId, "Zentral abgelehnt", conflicts, ct);
            await Execute(local, "UPDATE sync.peers SET pushed_below = @upper WHERE peer = @peer", ct,
                ("upper", pushUpper, NpgsqlDbType.Unknown), ("peer", Peer, NpgsqlDbType.Text));

            // Holen: Änderungen der zentralen Seite und anderer Stellen für diesen Mandanten, nicht die eigenen.
            var (incoming, pullUpper) = await ReadChangesAsync(central, pulledBelow,
                """
                origin IS DISTINCT FROM @site AND (tenant_id = @tenant OR (table_name = 'Tenants' AND row_key ->> 'Id' = @tenant::text)
                    OR (tenant_id IS NULL AND table_name <> 'Tenants' AND sync.key_tenant(table_schema, table_name, row_key) = @tenant))
                """,
                [("site", siteId, NpgsqlDbType.Text), ("tenant", tenant, NpgsqlDbType.Uuid)], ct, snapshotTenant: tenant);
            await ApplyAsync(local, incoming, CentralOrigin, "Hier abgelehnt", conflicts, ct);

            await using (var tx = await local.BeginTransactionAsync(ct))
            {
                foreach (var conflict in conflicts)
                {
                    await Execute(local, """
                        INSERT INTO sync.conflicts (table_schema, table_name, row_key, tenant_id, reason)
                        VALUES (@schema, @table, @key, @tenant, @reason)
                        """, ct, ("schema", conflict.Change.Schema, NpgsqlDbType.Text), ("table", conflict.Change.Table, NpgsqlDbType.Text),
                        ("key", conflict.Change.Key, NpgsqlDbType.Jsonb), ("tenant", tenant, NpgsqlDbType.Uuid),
                        ("reason", conflict.Reason, NpgsqlDbType.Text));
                }

                await Execute(local, """
                    UPDATE sync.peers SET pulled_below = @upper, last_run = now(), last_error = NULL WHERE peer = @peer
                    """, ct, ("upper", pullUpper, NpgsqlDbType.Unknown), ("peer", Peer, NpgsqlDbType.Text));
                // Gesendetes braucht lokal niemand mehr; Geholtes liegt im Protokoll der zentralen Seite.
                await Execute(local, "DELETE FROM sync.changes WHERE xid < @upper::xid8", ct, ("upper", pushUpper, NpgsqlDbType.Unknown));
                await tx.CommitAsync(ct);
            }

            await PruneCentralAsync(central, centralRetention ?? DefaultCentralRetention, ct);
            return new SyncResult(outgoing.Count, incoming.Count, conflicts.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Execute(local, "UPDATE sync.peers SET last_run = now(), last_error = @error WHERE peer = @peer", CancellationToken.None,
                ("error", ex.Message, NpgsqlDbType.Text), ("peer", Peer, NpgsqlDbType.Text));
            throw;
        }
    }

    private sealed record Change(string Schema, string Table, string Key, string? Row)
    {
        public string Name => $"{Schema}.{Table}";
    }

    private sealed record Conflict(Change Change, string Reason);

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static Task ChangeLogInstall(NpgsqlConnection connection, CancellationToken ct) => Execute(connection, ChangeLog.InstallSql, ct);

    /// <summary>
    /// Das Spa dieses Einzelplatzes. Ein neuer PC ohne Spa verbindet sich mit einem bestehenden: welches, sagt der Schlüsselcode
    /// (ADR 0048), und der erste Abgleich holt den ganzen Bestand samt Zugängen.
    /// </summary>
    private async Task<Guid> TenantAsync(NpgsqlConnection local, NpgsqlConnection central, CancellationToken ct)
    {
        var tenants = await TenantsAsync(local, ct);
        return tenants.Count switch
        {
            1 => tenants[0],
            0 when healthKeys is not null => await healthKeys.FindTenantAsync(central, ct),
            0 => throw new InvalidOperationException(
                "Auf diesem PC ist noch kein Spa. Zum Verbinden mit einem bestehenden Spa den Schlüsselcode des Spas eintragen."),
            _ => throw new InvalidOperationException("Der Abgleich braucht eine Einzelplatz-Datenbank mit genau einem Spa."),
        };
    }

    private static async Task<List<Guid>> TenantsAsync(NpgsqlConnection local, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""SELECT "Id" FROM aispa."Tenants" LIMIT 2""", local);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var tenants = new List<Guid>();
        while (await reader.ReadAsync(ct))
        {
            tenants.Add(reader.GetGuid(0));
        }

        return tenants;
    }

    private static async Task<(string PushedBelow, string PulledBelow)> PeerStateAsync(NpgsqlConnection local, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT pushed_below::text, pulled_below::text FROM sync.peers WHERE peer = @peer", local);
        command.Parameters.AddWithValue("peer", Peer);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (reader.GetString(0), reader.GetString(1));
    }

    /// <summary>
    /// Liest alle Zeilen, die in Transaktionen zwischen <paramref name="below"/> (einschließlich) und dem aktuellen
    /// <c>pg_snapshot_xmin</c> geändert wurden, mit ihrem jetzigen Stand (<c>null</c> = gelöscht). Unterhalb von xmin ist jede
    /// Transaktion abgeschlossen; was darüber liegt, kommt beim nächsten Lauf.
    /// Mit <paramref name="snapshotTenant"/>: ist das Protokoll über <paramref name="below"/> hinaus aufgeräumt, kommt stattdessen der
    /// ganze Bestand dieses Spas (im selben Snapshot, damit nichts zwischen Aufräumen und Lesen verloren geht).
    /// </summary>
    private static async Task<(List<Change> Changes, string Upper)> ReadChangesAsync(NpgsqlConnection connection, string below, string filter,
        (string Name, object Value, NpgsqlDbType Type)[] parameters, CancellationToken ct, Guid? snapshotTenant = null)
    {
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        string upper;
        await using (var command = new NpgsqlCommand("SELECT pg_snapshot_xmin(pg_current_snapshot())::text", connection, tx))
        {
            upper = (string)(await command.ExecuteScalarAsync(ct))!;
        }

        if (snapshotTenant is { } tenant)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM sync.pruned WHERE up_to >= @below::xid8)", connection, tx);
            command.Parameters.Add(new NpgsqlParameter("below", NpgsqlDbType.Unknown) { Value = below });
            if ((bool)(await command.ExecuteScalarAsync(ct))!)
            {
                var all = await ReadTenantAsync(connection, tx, tenant, ct);
                await tx.CommitAsync(ct);
                return (all, upper);
            }
        }

        var keys = new List<(string Schema, string Table, string Key)>();
        await using (var command = new NpgsqlCommand($"""
            SELECT DISTINCT table_schema, table_name, row_key::text FROM sync.changes
             WHERE xid >= @below::xid8 AND xid < @upper::xid8 AND {filter}
            """, connection, tx))
        {
            command.Parameters.Add(new NpgsqlParameter("below", NpgsqlDbType.Unknown) { Value = below });
            command.Parameters.Add(new NpgsqlParameter("upper", NpgsqlDbType.Unknown) { Value = upper });
            foreach (var (name, value, type) in parameters)
            {
                command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value });
            }

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                keys.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        var tables = new Dictionary<string, TableInfo>();
        var changes = new List<Change>(keys.Count);
        foreach (var (schema, table, key) in keys)
        {
            var info = await TableAsync(connection, tx, tables, schema, table, ct);
            if (info is null)
            {
                continue;
            }

            await using var command = new NpgsqlCommand(
                $"SELECT to_jsonb(t)::text FROM {info.Name} t, jsonb_populate_record(NULL::{info.Name}, @key) k WHERE {info.KeyMatch}",
                connection, tx);
            command.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Jsonb) { Value = key });
            changes.Add(new Change(schema, table, key, (string?)await command.ExecuteScalarAsync(ct)));
        }

        await tx.CommitAsync(ct);
        return (changes, upper);
    }

    /// <summary>
    /// Der ganze Bestand eines Spas aus allen abgeglichenen Tabellen, für eine Stelle, deren Stand im Protokoll nicht mehr vorliegt.
    /// Was zentral in der Zwischenzeit gelöscht wurde, bleibt bei ihr stehen; das Protokoll kennt es nicht mehr.
    /// </summary>
    private static async Task<List<Change>> ReadTenantAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid tenant, CancellationToken ct)
    {
        var tables = new List<(string Schema, string Table)>();
        await using (var command = new NpgsqlCommand("""
            SELECT DISTINCT event_object_schema::text, event_object_table::text FROM information_schema.triggers
             WHERE trigger_name = 'aispa_sync_log' ORDER BY 1, 2
            """, connection, tx))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                tables.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        var cache = new Dictionary<string, TableInfo>();
        var changes = new List<Change>();
        foreach (var (schema, table) in tables)
        {
            var info = await TableAsync(connection, tx, cache, schema, table, ct);
            if (info is null)
            {
                continue;
            }

            var filter = table == "Tenants" ? "(to_jsonb(t) ->> 'Id')::uuid = @tenant" : "sync.row_tenant(@schema, @table, to_jsonb(t)) = @tenant";
            var key = string.Join(", ", info.Key.Select(c => $"'{c.Trim('"')}', t.{c}"));
            await using var command = new NpgsqlCommand($"SELECT jsonb_build_object({key})::text, to_jsonb(t)::text FROM {info.Name} t WHERE {filter}",
                connection, tx);
            command.Parameters.AddWithValue("schema", schema);
            command.Parameters.AddWithValue("table", table);
            command.Parameters.AddWithValue("tenant", tenant);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                changes.Add(new Change(schema, table, reader.GetString(0), reader.GetString(1)));
            }
        }

        return changes;
    }

    /// <summary>
    /// Löscht im Protokoll der zentralen Seite, was älter als <paramref name="retention"/> ist, und merkt sich bis wohin. Nur
    /// abgeschlossene Transaktionen (unter xmin); Löschen und Merken in einer Transaktion, damit ein Leser beides oder keines sieht.
    /// </summary>
    private static async Task PruneCentralAsync(NpgsqlConnection central, TimeSpan retention, CancellationToken ct)
    {
        if (retention <= TimeSpan.Zero)
        {
            return;
        }

        await using var tx = await central.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("""
            WITH gone AS (
                DELETE FROM sync.changes
                 WHERE changed_at < now() - @retention AND xid < pg_snapshot_xmin(pg_current_snapshot())
                RETURNING xid
            )
            INSERT INTO sync.pruned (up_to)
            SELECT max(xid) FROM gone HAVING count(*) > 0
            ON CONFLICT (single) DO UPDATE SET up_to = GREATEST(sync.pruned.up_to, EXCLUDED.up_to), pruned_at = now()
            """, central, tx);
        command.Parameters.AddWithValue("retention", retention);
        await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Spielt Änderungen in einer Transaktion ein. Fremdschlüssel sind aufschiebbar, die Reihenfolge ist deshalb egal. Lehnt die
    /// Gegenseite eine Zeile wegen eines Belegungs- oder Eindeutigkeitskonflikts ab, wird nur diese Zeile zurückgenommen und als
    /// Konflikt vermerkt.
    /// </summary>
    private static async Task ApplyAsync(NpgsqlConnection connection, List<Change> changes, string origin, string side, List<Conflict> conflicts,
        CancellationToken ct)
    {
        if (changes.Count == 0)
        {
            return;
        }

        await using var tx = await connection.BeginTransactionAsync(ct);
        await Execute(connection, "SET CONSTRAINTS ALL DEFERRED", ct);
        await Execute(connection, $"SELECT set_config('{ChangeLog.OriginSetting}', @origin, true)", ct, ("origin", origin, NpgsqlDbType.Text));

        var tables = new Dictionary<string, TableInfo>();
        foreach (var change in changes)
        {
            var info = await TableAsync(connection, tx, tables, change.Schema, change.Table, ct);
            if (info is null)
            {
                continue;
            }

            var insertOnly = InsertOnly.Contains(change.Name);
            string sql;
            if (change.Row is null)
            {
                if (insertOnly)
                {
                    continue;
                }

                sql = $"DELETE FROM {info.Name} t USING jsonb_populate_record(NULL::{info.Name}, @row) k WHERE {info.KeyMatch}";
            }
            else
            {
                var update = insertOnly || info.Updatable.Length == 0
                    ? "DO NOTHING"
                    : $"DO UPDATE SET ({string.Join(", ", info.Updatable)}) = ROW({string.Join(", ", info.Updatable.Select(c => "EXCLUDED." + c))})";
                sql = $"""
                    INSERT INTO {info.Name} ({string.Join(", ", info.Columns)}) OVERRIDING SYSTEM VALUE
                    SELECT {string.Join(", ", info.Columns)} FROM jsonb_populate_record(NULL::{info.Name}, @row)
                    ON CONFLICT ({string.Join(", ", info.Key)}) {update}
                    """;
            }

            await Execute(connection, "SAVEPOINT sync_row", ct);
            try
            {
                await Execute(connection, sql, ct, ("row", change.Row ?? change.Key, NpgsqlDbType.Jsonb));
                await Execute(connection, "RELEASE SAVEPOINT sync_row", ct);
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.UniqueViolation)
            {
                await Execute(connection, "ROLLBACK TO SAVEPOINT sync_row", ct);
                conflicts.Add(new Conflict(change, ex.SqlState == PostgresErrorCodes.ExclusionViolation
                    ? $"{side}: Raum, Person oder Gerät ist zur selben Zeit schon gebucht."
                    : $"{side}: schon vorhanden ({ex.ConstraintName})."));
            }
        }

        await tx.CommitAsync(ct);
    }

    private sealed record TableInfo(string Name, string[] Key, string[] Columns, string[] Updatable)
    {
        public string KeyMatch => string.Join(" AND ", Key.Select(c => $"t.{c} = k.{c}"));
    }

    private static async Task<TableInfo?> TableAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Dictionary<string, TableInfo> cache,
        string schema, string table, CancellationToken ct)
    {
        var name = $"{schema}.{table}";
        if (cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        await using var command = new NpgsqlCommand("""
            SELECT a.attname,
                   EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = c.oid AND i.indisprimary AND a.attnum = ANY (i.indkey::int2[]))
              FROM pg_class c
              JOIN pg_namespace ns ON ns.oid = c.relnamespace
              JOIN pg_attribute a ON a.attrelid = c.oid
             WHERE ns.nspname = @schema AND c.relname = @table AND a.attnum > 0 AND NOT a.attisdropped AND a.attgenerated = ''
             ORDER BY a.attnum
            """, connection, tx);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);
        var columns = new List<(string Name, bool Key)>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                columns.Add((Quote(reader.GetString(0)), reader.GetBoolean(1)));
            }
        }

        // Tabelle gibt es auf dieser Seite (noch) nicht, z. B. bei unterschiedlichem Migrationsstand: überspringen.
        if (columns.Count == 0 || !columns.Any(c => c.Key))
        {
            return null;
        }

        var info = new TableInfo($"{Quote(schema)}.{Quote(table)}", [.. columns.Where(c => c.Key).Select(c => c.Name)],
            [.. columns.Select(c => c.Name)], [.. columns.Where(c => !c.Key).Select(c => c.Name)]);
        cache[name] = info;
        return info;
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static async Task Execute(NpgsqlConnection connection, string sql, CancellationToken ct,
        params (string Name, object Value, NpgsqlDbType Type)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value, type) in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value });
        }

        await command.ExecuteNonQueryAsync(ct);
    }
}
