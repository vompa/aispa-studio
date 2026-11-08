using AispaStudio.Api.Online;
using AispaStudio.Contracts;
using Npgsql;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Schlüssel für Gesundheitsdaten beim Abgleich (ADR 0048). Jede Stelle verschlüsselt mit eigenem Schlüssel; beim Abgleich legt sie ihn,
/// mit dem Schlüsselcode des Spas verschlüsselt, in <c>sync.health_keys</c> auf beiden Seiten und öffnet die der anderen Stellen. Ein
/// Datenbankauszug allein bleibt damit unlesbar, die Stellen lesen aber die Anamnesen der anderen.
/// </summary>
/// <param name="keyId">Kennung des eigenen Schlüssels (<c>HealthData:CurrentKeyId</c>).</param>
/// <param name="key">Eigener Schlüssel als Base64.</param>
public sealed class HealthKeyExchange(string keyId, string key, string code, HealthKeyRing ring)
{
    /// <summary>Eigener Schlüssel, mit dem Code verschlüsselt; einmal je Prozess, PBKDF2 kostet bewusst Zeit.</summary>
    private string? _ownSealed;

    public const string WrongCode =
        "Der Schlüsselcode des Spas passt nicht zur zentralen Datenbank. Bitte unter „Abgleich“ den Code der Stelle eintragen, die zuerst verbunden war.";

    /// <summary>Aus der Konfiguration; <c>null</c> ohne Schlüsselcode oder ohne eigenen Schlüssel.</summary>
    public static HealthKeyExchange? From(HealthDataOptions options, HealthKeyRing ring) =>
        !string.IsNullOrWhiteSpace(options.ShareCode) && options.Keys.TryGetValue(options.CurrentKeyId, out var key) && !string.IsNullOrWhiteSpace(key)
            ? new HealthKeyExchange(options.CurrentKeyId, key, options.ShareCode, ring)
            : null;

    /// <summary>
    /// Vor dem Senden: Schlüssel der zentralen Seite prüfen und öffnen; passt der Code nicht, wirft der Abgleich, bevor etwas übertragen
    /// wird. Danach den eigenen Schlüssel auf beiden Seiten hinterlegen und die Schlüssel der anderen Stellen auch lokal ablegen, damit
    /// sie nach einem Neustart ohne Verbindung lesbar bleiben.
    /// </summary>
    public async Task ExchangeAsync(NpgsqlConnection local, NpgsqlConnection central, Guid tenant, CancellationToken ct)
    {
        Open(await ReadAsync(local, tenant, ct), failOnWrongCode: false);
        var others = await ReadAsync(central, tenant, ct);
        Open(others, failOnWrongCode: true);

        // Eigener Schlüssel: mit dem jetzigen Code, auch wenn lokal noch einer mit einem früheren Code liegt.
        _ownSealed ??= KeySeal.Seal(keyId, key, code);
        await UpsertAsync(local, tenant, keyId, _ownSealed, ct);
        await InsertAsync(central, tenant, keyId, _ownSealed, ct);
        foreach (var (otherId, sealedKey) in others.Where(o => o.KeyId != keyId))
        {
            await InsertAsync(local, tenant, otherId, sealedKey, ct);
        }
    }

    public const string NoSpa = "Mit diesem Schlüsselcode gibt es in der zentralen Datenbank kein Spa. Bitte den Code der ersten Stelle prüfen.";

    /// <summary>Für einen neuen PC ohne Spa: das Spa, dessen Schlüssel sich mit dem Code öffnen lassen.</summary>
    public async Task<Guid> FindTenantAsync(NpgsqlConnection central, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT DISTINCT ON (tenant_id) tenant_id, sealed FROM sync.health_keys ORDER BY tenant_id, created_at", central);
        var candidates = new List<(Guid Tenant, string Sealed)>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                candidates.Add((reader.GetGuid(0), reader.GetString(1)));
            }
        }

        var matches = candidates.Where(c => KeySeal.TryOpen(c.Sealed, code, out _, out _)).Select(c => c.Tenant).ToList();
        return matches is [var tenant] ? tenant : throw new InvalidOperationException(NoSpa);
    }

    private void Open(List<(string KeyId, string Sealed)> rows, bool failOnWrongCode)
    {
        foreach (var (rowId, sealedKey) in rows)
        {
            if (rowId == keyId || ring.Contains(rowId))
            {
                continue;
            }

            if (KeySeal.TryOpen(sealedKey, code, out var openedId, out var opened) && openedId == rowId)
            {
                ring.Add(rowId, opened);
            }
            else if (failOnWrongCode)
            {
                throw new InvalidOperationException(WrongCode);
            }
        }
    }

    private static async Task<List<(string KeyId, string Sealed)>> ReadAsync(NpgsqlConnection connection, Guid tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT key_id, sealed FROM sync.health_keys WHERE tenant_id = @tenant ORDER BY created_at",
            connection);
        command.Parameters.AddWithValue("tenant", tenant);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<(string, string)>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add((reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static Task InsertAsync(NpgsqlConnection connection, Guid tenant, string keyId, string sealedKey, CancellationToken ct) =>
        WriteAsync(connection, tenant, keyId, sealedKey, "DO NOTHING", ct);

    private static Task UpsertAsync(NpgsqlConnection connection, Guid tenant, string keyId, string sealedKey, CancellationToken ct) =>
        WriteAsync(connection, tenant, keyId, sealedKey, "DO UPDATE SET sealed = EXCLUDED.sealed WHERE sync.health_keys.sealed <> EXCLUDED.sealed", ct);

    private static async Task WriteAsync(NpgsqlConnection connection, Guid tenant, string keyId, string sealedKey, string onConflict,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand($"""
            INSERT INTO sync.health_keys (key_id, tenant_id, sealed) VALUES (@key, @tenant, @sealed) ON CONFLICT (key_id) {onConflict}
            """, connection);
        command.Parameters.AddWithValue("key", keyId);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("sealed", sealedKey);
        await command.ExecuteNonQueryAsync(ct);
    }
}
