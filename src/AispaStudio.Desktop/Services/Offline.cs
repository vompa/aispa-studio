using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services;

/// <summary>Zuletzt geladene Antwort des Servers.</summary>
public sealed record CachedResponse(string Json, DateTimeOffset SavedAt);

/// <summary>
/// Ohne Verbindung vorgemerkte Änderung, die beim nächsten Kontakt mit dem Server gesendet wird.
/// <c>Preview</c> zeigt den Termin bis dahin so im Kalender, wie er nach der Änderung aussähe; <c>Visit</c> ebenso einen Eintritt
/// unter „Sauna und Ruheraum“. <c>TenantId</c> ist das Spa, in dem sie vorgemerkt wurde: gesendet wird sie nur dorthin.
/// </summary>
public sealed record PendingChange(Guid Id, string Path, string Body, string Description, DateTimeOffset CreatedAt, AppointmentDto? Preview,
    AreaVisitDto? Visit = null, Guid? TenantId = null);

/// <summary>Vorgemerkte Änderung, die der Server beim Abgleich abgelehnt hat (z. B. Raum inzwischen belegt).</summary>
public sealed record FailedChange(Guid Id, string Description, string Reason, DateTimeOffset At, Guid? TenantId = null);

public sealed record OfflineState(
    Dictionary<string, CachedResponse> Responses,
    List<PendingChange> Pending,
    List<FailedChange> Failed)
{
    public static OfflineState Empty() => new([], [], []);
}

/// <summary>Lokaler Stand der Rezeption für den Betrieb ohne Internet (ADR 0005).</summary>
public interface IOfflineStore
{
    OfflineState Load();

    void Save(OfflineState state);

    void Clear();
}

/// <summary>Nur im Speicher (Tests, andere Betriebssysteme): nach dem Beenden ist der Stand weg.</summary>
public sealed class MemoryOfflineStore : IOfflineStore
{
    private string? _json;

    public OfflineState Load() => _json is null ? OfflineState.Empty() : JsonSerializer.Deserialize<OfflineState>(_json, ApiClient.Json) ?? OfflineState.Empty();

    public void Save(OfflineState state) => _json = JsonSerializer.Serialize(state, ApiClient.Json);

    public void Clear() => _json = null;
}

/// <summary>
/// Unter Windows im Benutzerprofil, verschlüsselt mit DPAPI: Kundendaten liest nur dasselbe Windows-Konto auf demselben PC.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsOfflineStore : IOfflineStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudio", "offline.bin");

    public OfflineState Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return OfflineState.Empty();
            }

            var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser));
            return JsonSerializer.Deserialize<OfflineState>(json, ApiClient.Json) ?? OfflineState.Empty();
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            return OfflineState.Empty();
        }
    }

    public void Save(OfflineState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, ApiClient.Json)), null, DataProtectionScope.CurrentUser);
        var temp = FilePath + ".neu";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, FilePath, overwrite: true);
    }

    public void Clear() => File.Delete(FilePath);
}

/// <summary>
/// Hält die zuletzt geladenen Antworten (Kalender, Stammdaten, Kunden) und die ohne Verbindung vorgemerkten Änderungen.
/// Gesundheitsdaten werden nie zwischengespeichert. Beim Abmelden wird das Gelesene gelöscht; Vorgemerktes bleibt, bis es gesendet ist.
/// </summary>
public sealed class OfflineCache(IOfflineStore store, TimeProvider? time = null)
{
    /// <summary>Was älter ist, wird beim Speichern verworfen.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

    // Nur Lesendes, das die Rezeption ohne Verbindung braucht. Anamnese, Gesundheit und Berichte bleiben ausgeschlossen.
    private static readonly string[] Cacheable =
    [
        "api/auth/me", "api/system/modules", "api/locations", "api/customers", "api/pos/registers", "api/pos/articles",
    ];

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _lock = new();
    private OfflineState? _state;
    private DateTimeOffset _lastSave = DateTimeOffset.MinValue;
    private bool _dirty;

    /// <summary>Vorgemerkte oder abgelehnte Änderungen haben sich geändert.</summary>
    public event Action? Changed;

    /// <summary>
    /// Spa der angemeldeten Person. Neu Vorgemerktes bekommt es mitgegeben; <see cref="Pending"/> und <see cref="Failed"/> zeigen nur
    /// das eigene Spa (und Älteres ohne Angabe), damit nach einem Wechsel nichts an ein anderes Spa geht.
    /// </summary>
    public Guid? Tenant
    {
        get
        {
            lock (_lock)
            {
                return _tenant;
            }
        }
        set
        {
            lock (_lock)
            {
                _tenant = value;
            }

            Changed?.Invoke();
        }
    }

    private Guid? _tenant;

    private bool Mine(Guid? tenant) => tenant is null || _tenant is null || tenant == _tenant;

    public static bool IsCacheable(string path)
    {
        var p = path.TrimStart('/');
        return Cacheable.Any(prefix => p == prefix || p.StartsWith(prefix + "/", StringComparison.Ordinal) || p.StartsWith(prefix + "?", StringComparison.Ordinal))
            && !p.Contains("anamnes", StringComparison.OrdinalIgnoreCase)
            && !p.Contains("health", StringComparison.OrdinalIgnoreCase);
    }

    private OfflineState State => _state ??= store.Load();

    public void Put(string path, string json)
    {
        lock (_lock)
        {
            State.Responses[Key(path)] = new CachedResponse(json, _time.GetUtcNow());
            _dirty = true;

            // Gelesenes höchstens alle fünf Sekunden auf die Platte (beim Vorausladen kommen viele Antworten schnell nacheinander).
            if (_time.GetUtcNow() - _lastSave >= TimeSpan.FromSeconds(5))
            {
                Persist();
            }
        }
    }

    /// <summary>Noch nicht Gespeichertes jetzt schreiben, z. B. nach dem Vorausladen.</summary>
    public void Flush()
    {
        lock (_lock)
        {
            if (_dirty)
            {
                Persist();
            }
        }
    }

    public CachedResponse? Get(string path)
    {
        lock (_lock)
        {
            if (State.Responses.TryGetValue(Key(path), out var hit))
            {
                return hit;
            }

            // Kundensuche ohne Verbindung: in allen bisher geladenen Kunden suchen.
            const string Search = "api/customers?search=";
            var key = Key(path);
            if (key.StartsWith(Search, StringComparison.Ordinal))
            {
                var query = Uri.UnescapeDataString(key[Search.Length..]).Trim();
                var found = KnownCustomers()
                    .Where(c => query.Length == 0 || Matches(c, query))
                    .OrderBy(c => c.LastName, StringComparer.CurrentCulture).ThenBy(c => c.FirstName, StringComparer.CurrentCulture)
                    .Take(50)
                    .ToList();
                return new CachedResponse(JsonSerializer.Serialize(found, ApiClient.Json), State.Responses.Values.Select(r => r.SavedAt).DefaultIfEmpty().Min());
            }

            return null;
        }
    }

    /// <summary>Wann zuletzt etwas vom Server kam, für den Hinweis „Stand …“.</summary>
    public DateTimeOffset? LastSaved
    {
        get
        {
            lock (_lock)
            {
                return State.Responses.Count == 0 ? null : State.Responses.Values.Max(r => r.SavedAt);
            }
        }
    }

    public IReadOnlyList<PendingChange> Pending
    {
        get
        {
            lock (_lock)
            {
                return [.. State.Pending.Where(p => Mine(p.TenantId))];
            }
        }
    }

    public IReadOnlyList<FailedChange> Failed
    {
        get
        {
            lock (_lock)
            {
                return [.. State.Failed.Where(f => Mine(f.TenantId))];
            }
        }
    }

    public void Enqueue(PendingChange change)
    {
        lock (_lock)
        {
            State.Pending.Add(change.TenantId is null ? change with { TenantId = _tenant } : change);
            Persist();
        }

        Changed?.Invoke();
    }

    public void Complete(Guid id, FailedChange? failure = null)
    {
        lock (_lock)
        {
            var tenant = State.Pending.FirstOrDefault(p => p.Id == id)?.TenantId ?? _tenant;
            State.Pending.RemoveAll(p => p.Id == id);
            if (failure is not null)
            {
                State.Failed.Add(failure.TenantId is null ? failure with { TenantId = tenant } : failure);
            }

            Persist();
        }

        Changed?.Invoke();
    }

    public void DismissFailed(Guid id)
    {
        lock (_lock)
        {
            State.Failed.RemoveAll(f => f.Id == id);
            Persist();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Beim Abmelden oder Personenwechsel: der gelesene Stand (Kalender, Kundenkarten) wird gelöscht, damit auf einem gemeinsam
    /// genutzten PC nichts davon zurückbleibt. Vorgemerkte und abgelehnte Änderungen bleiben und gehen mit der nächsten Anmeldung
    /// im selben Spa an den Server; sonst wäre ohne Verbindung Eingetragenes verloren.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            var kept = OfflineState.Empty();
            kept.Pending.AddRange(State.Pending);
            kept.Failed.AddRange(State.Failed);
            _state = kept;
            _tenant = null;
            if (kept.Pending.Count == 0 && kept.Failed.Count == 0)
            {
                store.Clear();
            }
            else
            {
                Persist();
            }
        }

        Changed?.Invoke();
    }

    private List<CustomerDto> KnownCustomers()
    {
        var seen = new Dictionary<Guid, CustomerDto>();
        foreach (var (key, response) in State.Responses)
        {
            if (!key.StartsWith("api/customers", StringComparison.Ordinal) || key.EndsWith("/appointments", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(response.Json);
                var items = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement.Deserialize<List<CustomerDto>>(ApiClient.Json) ?? []
                    : doc.RootElement.Deserialize<CustomerDto>(ApiClient.Json) is { } single ? [single] : [];
                foreach (var customer in items)
                {
                    seen[customer.Id] = customer;
                }
            }
            catch (JsonException)
            {
            }
        }

        return [.. seen.Values];
    }

    private static bool Matches(CustomerDto c, string query) =>
        $"{c.FirstName} {c.LastName}".Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || c.InHouseName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || $"{c.LastName} {c.FirstName}".Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || (c.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (c.Phone is { } phone && Digits(phone).Contains(Digits(query), StringComparison.Ordinal) && Digits(query).Length >= 3);

    private static string Digits(string text) => new([.. text.Where(char.IsDigit)]);

    private void Persist()
    {
        var limit = _time.GetUtcNow() - MaxAge;
        foreach (var old in State.Responses.Where(r => r.Value.SavedAt < limit).Select(r => r.Key).ToList())
        {
            State.Responses.Remove(old);
        }

        store.Save(State);
        _dirty = false;
        _lastSave = _time.GetUtcNow();
    }

    private static string Key(string path) => path.TrimStart('/');
}
