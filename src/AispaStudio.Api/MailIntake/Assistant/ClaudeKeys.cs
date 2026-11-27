using System.Collections.Concurrent;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Anthropic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>Claude-Schlüssel eines Spas, in der App eingetragen (ADR 0103). Nur verschlüsselt gespeichert, an den Mandanten gebunden.</summary>
public sealed class ClaudeKeySetting
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Mit ASP.NET Data Protection verschlüsselt (<see cref="ClaudeKeyStore.Purpose"/>).</summary>
    public required string ProtectedKey { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal static class ClaudeKeyModel
{
    public static void Configure(ModelBuilder builder) =>
        builder.Entity<ClaudeKeySetting>(e =>
        {
            e.Property(k => k.Id).ValueGeneratedNever();
            e.Property(k => k.ProtectedKey).HasMaxLength(4000);
        });
}

/// <summary>
/// Liefert den Claude-Schlüssel je Spa (ADR 0103): <c>Ai:Claude:ApiKey</c> aus der Konfiguration hat Vorrang, sonst der in der App eingetragene.
/// <c>Ai:Provider=RuleBased</c> schaltet Claude ganz ab. Der Schlüssel aus der Datenbank bleibt eine Minute im Speicher; Speichern und Entfernen
/// leeren diesen Eintrag sofort. Andere Knoten (Rollen Api und Worker) sehen die Änderung spätestens nach dieser Minute.
/// </summary>
internal sealed class ClaudeKeyStore(IConfiguration configuration, IDataProtectionProvider protection, IServiceScopeFactory scopes, TimeProvider time)
{
    public const string Purpose = "Ai.Claude.ApiKey";
    public const string Prefix = "sk-ant-";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<Guid, (string? Key, DateTimeOffset Until)> _cache = new();

    /// <summary>Claude ist mit <c>Ai:Provider=RuleBased</c> ganz aus, auch mit Schlüssel.</summary>
    public bool Off => string.Equals(configuration["Ai:Provider"], "RuleBased", StringComparison.OrdinalIgnoreCase);

    public string? ConfiguredKey => configuration["Ai:Claude:ApiKey"] is { } key && !string.IsNullOrWhiteSpace(key) ? key.Trim() : null;

    /// <summary>Schlüssel für Aufrufe des Spas; leer, wenn Claude aus ist oder kein Schlüssel da ist.</summary>
    public string? KeyFor(Guid? tenantId) => Off ? null : StoredOrConfigured(tenantId).Key;

    /// <summary>Gesetzter Schlüssel und seine Quelle, unabhängig davon, ob Claude abgeschaltet ist.</summary>
    public (string? Key, ClaudeKeySource Source) StoredOrConfigured(Guid? tenantId)
    {
        if (ConfiguredKey is { } configured)
        {
            return (configured, ClaudeKeySource.Config);
        }

        var stored = tenantId is { } id ? Stored(id) : null;
        return stored is null ? (null, ClaudeKeySource.None) : (stored, ClaudeKeySource.App);
    }

    public async Task SaveAsync(AispaDbContext db, Guid tenantId, string key, CancellationToken ct)
    {
        var setting = await db.Set<ClaudeKeySetting>().FirstOrDefaultAsync(ct);
        var protectedKey = Protector(tenantId).Protect(key);
        if (setting is null)
        {
            db.Add(new ClaudeKeySetting { ProtectedKey = protectedKey, UpdatedAt = time.GetUtcNow() });
        }
        else
        {
            setting.ProtectedKey = protectedKey;
            setting.UpdatedAt = time.GetUtcNow();
        }

        await db.SaveChangesAsync(ct);
        _cache.TryRemove(tenantId, out _);
    }

    public async Task RemoveAsync(AispaDbContext db, Guid tenantId, CancellationToken ct)
    {
        await db.Set<ClaudeKeySetting>().ExecuteDeleteAsync(ct);
        _cache.TryRemove(tenantId, out _);
    }

    /// <summary>„sk-ant-…a1b2“: Anfang und die letzten vier Zeichen, nie der ganze Schlüssel.</summary>
    public static string Mask(string key) => $"{Prefix}…{key[^Math.Min(4, key.Length)..]}";

    private string? Stored(Guid tenantId)
    {
        var now = time.GetUtcNow();
        if (_cache.TryGetValue(tenantId, out var hit) && hit.Until > now)
        {
            return hit.Key;
        }

        // Synchron: die Dienste fragen „verfügbar?“ als Eigenschaft ab; dank Zwischenspeicher höchstens einmal je Minute und Spa.
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var protectedKey = db.Set<ClaudeKeySetting>().AsNoTracking().Select(k => k.ProtectedKey).FirstOrDefault();
        var key = protectedKey is null ? null : Unprotect(tenantId, protectedKey);
        _cache[tenantId] = (key, now + CacheFor);
        return key;
    }

    /// <summary>Ein Schlüssel, der sich nicht mehr entschlüsseln lässt (Schlüsselring verloren), zählt als nicht gesetzt; die App zeigt dann „kein Schlüssel“.</summary>
    private string? Unprotect(Guid tenantId, string protectedKey)
    {
        try
        {
            return Protector(tenantId).Unprotect(protectedKey);
        }
        catch (global::System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private IDataProtector Protector(Guid tenantId) => protection.CreateProtector(Purpose, tenantId.ToString());
}

/// <summary>Der Schlüssel für das Spa des laufenden Aufrufs (HTTP-Anfrage oder Hintergrundauftrag mit <see cref="TenantContext.Use"/>).</summary>
internal sealed class ClaudeAccess(ClaudeKeyStore store, TenantContext tenant)
{
    public string? Key => store.KeyFor(tenant.TenantId);

    public bool Available => Key is not null;
}

/// <summary>Ein Client je Schlüssel, solange die Anwendung läuft; ein neuer Schlüssel bekommt einen neuen Client.</summary>
internal sealed class ClaudeClients : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<AnthropicClient>> _clients = new(StringComparer.Ordinal);

    public AnthropicClient For(string key) => _clients.GetOrAdd(key, k => new Lazy<AnthropicClient>(() => new AnthropicClient { ApiKey = k })).Value;

    public void Dispose()
    {
        foreach (var client in _clients.Values.Where(c => c.IsValueCreated))
        {
            client.Value.Dispose();
        }
    }
}
