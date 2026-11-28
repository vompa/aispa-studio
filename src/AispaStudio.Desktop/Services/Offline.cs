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
/// <param name="Substitute">
/// Ersatzbeleg, den die Kassa ohne Verbindung ausgegeben hat (ADR 0096): die Zweitschrift bleibt hier, bis der Server ihn nacherfasst hat.
/// </param>
public sealed record PendingChange(Guid Id, string Path, string Body, string Description, DateTimeOffset CreatedAt, AppointmentDto? Preview,
    AreaVisitDto? Visit = null, Guid? TenantId = null, ReceiptDto? Substitute = null);

/// <summary>Vorgemerkte Änderung, die der Server beim Abgleich abgelehnt hat (z. B. Raum inzwischen belegt).</summary>
/// <param name="Substitute">
/// Abgelehnter Ersatzbeleg (ADR 0096): Das Geld ist kassiert, deshalb lässt er sich nicht verwerfen, nur erneut senden
/// (<c>Path</c> und <c>Body</c> der Nacherfassung bleiben dafür erhalten).
/// </param>
public sealed record FailedChange(Guid Id, string Description, string Reason, DateTimeOffset At, Guid? TenantId = null,
    ReceiptDto? Substitute = null, string? Path = null, string? Body = null)
{
    /// <summary>Nur was kein Geld betrifft, lässt sich als erledigt abhaken.</summary>
    public bool CanDismiss => Substitute is null;

    public bool CanRetry => Substitute is not null && Path is not null && Body is not null;
}

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
public sealed class OfflineCache
{
    /// <summary>Was älter ist, wird nicht mehr gezeigt und beim Speichern verworfen.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

    /// <summary>Liste der anonymisierten Gäste: steuert das Bereinigen und gehört selbst nicht in den Stand.</summary>
    public const string AnonymizedPath = "api/customers/anonymized";

    private const string CustomerSearch = "api/customers?search=";
    private const string RegistersPath = "api/pos/registers";

    // Antworten, die Name oder Mailadresse eines Gastes ohne seine Kunden-Id tragen können (gehaltene Termine, Lückenvorschläge,
    // Schließzeiten und Raumsperren mit betroffenen Terminen, Gutscheinbestellungen); beim Bereinigen fallen sie ganz weg und laden
    // mit Verbindung neu. Die Tagesliste der Kassa gehört nicht dazu: jede Zeile trägt Termin oder Besuch mit der Kunden-Id.
    private static readonly string[] NamedWithoutId = ["/appointment-holds", "/gap-suggestions", "/closure", "/room-closures", "/voucher-orders"];

    private const string DeletedGuest = "Gelöschter Gast";

    // Nur Lesendes, das die Rezeption ohne Verbindung braucht. Anamnese, Gesundheit und Berichte bleiben ausgeschlossen.
    private static readonly string[] Cacheable =
    [
        "api/auth/me", "api/system/modules", "api/locations", "api/customers", "api/pos/registers", "api/pos/articles",
        // Für den Ersatzbeleg ohne Verbindung (ADR 0096): Tagesliste der Kassa.
        "api/pos/locations",
    ];

    private readonly IOfflineStore _store;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly HashSet<Guid> _decommissionedRegisters = [];
    private OfflineState _state;
    private DateTimeOffset _lastSave = DateTimeOffset.MinValue;
    private bool _dirty;

    public OfflineCache(IOfflineStore store, TimeProvider? time = null)
    {
        _store = store;
        _time = time ?? TimeProvider.System;
        _state = store.Load();
        var protectedKeys = _state.Responses.Keys.Where(IsProtected).ToArray();
        foreach (var key in protectedKeys)
        {
            _state.Responses.Remove(key);
        }

        var pending = _state.Pending.RemoveAll(change => !CanQueue(change.Path));
        var failed = _state.Failed.RemoveAll(change => change.Path is { } path && !CanQueue(path));
        if (protectedKeys.Length != 0 || pending != 0 || failed != 0)
        {
            Persist();
        }
    }

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
        return !IsProtected(path)
            && Cacheable.Any(prefix => p == prefix || p.StartsWith(prefix + "/", StringComparison.Ordinal) || p.StartsWith(prefix + "?", StringComparison.Ordinal))
            && !p.Contains("anamnes", StringComparison.OrdinalIgnoreCase)
            && !p.Contains("health", StringComparison.OrdinalIgnoreCase)
            && !p.StartsWith(AnonymizedPath, StringComparison.Ordinal);
    }

    internal static bool CanQueue(string path) =>
        !IsProtected(path) && !string.Equals(EndpointPath(path), RegistersPath, StringComparison.OrdinalIgnoreCase);

    private static bool IsProtected(string path)
    {
        var endpoint = EndpointPath(path);
        return endpoint.StartsWith(RegistersPath + "/", StringComparison.OrdinalIgnoreCase)
            && (endpoint.EndsWith("/fiscal-credentials", StringComparison.OrdinalIgnoreCase)
                || endpoint.EndsWith("/fiscal-registration", StringComparison.OrdinalIgnoreCase));
    }

    private static string EndpointPath(string path)
    {
        var route = Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile ? uri.AbsolutePath : path.Split('?', '#')[0];
        route = Uri.UnescapeDataString(route).Trim('/');
        return route.StartsWith("api/v1/", StringComparison.OrdinalIgnoreCase) ? "api/" + route["api/v1/".Length..] : route;
    }

    private OfflineState State => _state;

    public void Put(string path, string json)
    {
        if (IsProtected(path))
        {
            return;
        }

        lock (_lock)
        {
            if (Key(path).StartsWith(CustomerSearch, StringComparison.Ordinal))
            {
                // Eine Suchliste liegt je Gast als Kundenkarte im Stand, nie unter dem Suchbegriff: der wäre ein Name im Schlüssel.
                foreach (var (id, customer) in CustomersOf(json))
                {
                    State.Responses[$"api/customers/{id:D}"] = new CachedResponse(customer, _time.GetUtcNow());
                }
            }
            else
            {
                var key = Key(path);
                State.Responses[key] = new CachedResponse(ApplyDecommissionedRegisters(key, json), _time.GetUtcNow());
            }

            _dirty = true;

            // Gelesenes höchstens alle fünf Sekunden auf die Platte (beim Vorausladen kommen viele Antworten schnell nacheinander).
            if (_time.GetUtcNow() - _lastSave >= TimeSpan.FromSeconds(5))
            {
                Persist();
            }
        }
    }

    internal bool HasUnresolvedSubstitute(Guid registerId)
    {
        lock (_lock)
        {
            return State.Pending.Any(change => Mine(change.TenantId) && change.Substitute?.RegisterId == registerId)
                || State.Failed.Any(change => Mine(change.TenantId) && change.Substitute?.RegisterId == registerId);
        }
    }

    internal void MarkRegisterDecommissioned(Guid registerId)
    {
        lock (_lock)
        {
            _decommissionedRegisters.Add(registerId);
            foreach (var (key, response) in State.Responses.ToArray())
            {
                var json = ApplyDecommissionedRegisters(key, response.Json);
                if (json != response.Json)
                {
                    State.Responses[key] = response with { Json = json };
                    _dirty = true;
                }
            }

            if (_dirty)
            {
                Persist();
            }
        }
    }

    private string ApplyDecommissionedRegisters(string key, string json)
    {
        if (_decommissionedRegisters.Count == 0 || !key.StartsWith(RegistersPath, StringComparison.Ordinal))
        {
            return json;
        }

        var queryStart = key.IndexOf('?');
        var path = queryStart < 0 ? key : key[..queryStart];
        try
        {
            if (path == RegistersPath && JsonSerializer.Deserialize<List<CashRegisterDto>>(json, ApiClient.Json) is { } registers
                && registers.Any(IsDecommissioned))
            {
                return JsonSerializer.Serialize(registers.Select(register => IsDecommissioned(register) ? Decommissioned(register) : register), ApiClient.Json);
            }

            if (path.StartsWith(RegistersPath + "/", StringComparison.Ordinal)
                && Guid.TryParse(path.AsSpan(RegistersPath.Length + 1), out var id)
                && _decommissionedRegisters.Contains(id)
                && JsonSerializer.Deserialize<CashRegisterDto>(json, ApiClient.Json) is { } register && IsDecommissioned(register))
            {
                return JsonSerializer.Serialize(Decommissioned(register), ApiClient.Json);
            }
        }
        catch (JsonException)
        {
            // GetAsync behandelt einen nicht lesbaren Eintrag wie eine fehlende Offline-Antwort.
        }

        return json;
    }

    private bool IsDecommissioned(CashRegisterDto register) => register.IsActive && _decommissionedRegisters.Contains(register.Id);

    private static CashRegisterDto Decommissioned(CashRegisterDto register) => register with { IsActive = false, Readiness = null };

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
        if (IsProtected(path))
        {
            return null;
        }

        lock (_lock)
        {
            if (State.Responses.TryGetValue(Key(path), out var hit) && !IsExpired(hit))
            {
                return hit;
            }

            // Kundensuche ohne Verbindung: in allen bisher geladenen Kunden suchen.
            const string Search = CustomerSearch;
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
        if (!CanQueue(change.Path))
        {
            return;
        }

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
            if (failure is not null && (failure.Path is null || CanQueue(failure.Path)))
            {
                State.Failed.Add(failure.TenantId is null ? failure with { TenantId = tenant } : failure);
            }

            Persist();
        }

        Changed?.Invoke();
    }

    /// <summary>Abgelehnte Vormerkung als erledigt abhaken; ein abgelehnter Ersatzbeleg bleibt (ADR 0096).</summary>
    public void DismissFailed(Guid id)
    {
        lock (_lock)
        {
            State.Failed.RemoveAll(f => f.Id == id && f.CanDismiss);
            Persist();
        }

        Changed?.Invoke();
    }

    /// <summary>Abgelehnten Ersatzbeleg wieder vormerken, z. B. nachdem der Grund an der Kassa geklärt ist; gesendet wird beim nächsten Abgleich.</summary>
    public void Retry(Guid id)
    {
        lock (_lock)
        {
            if (State.Failed.FirstOrDefault(f => f.Id == id) is not { CanRetry: true } failed)
            {
                return;
            }

            State.Failed.Remove(failed);
            State.Pending.Add(new PendingChange(failed.Id, failed.Path!, failed.Body!, failed.Description, failed.At, null, null, failed.TenantId, failed.Substitute));
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
            _decommissionedRegisters.Clear();
            if (kept.Pending.Count == 0 && kept.Failed.Count == 0)
            {
                _store.Clear();
            }
            else
            {
                Persist();
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Nach dem Anonymisieren (ADR 0093): entfernt jede Antwort, die einen dieser Gäste nennt, und speichert sofort. Dazu alle
    /// Suchlisten (der Suchbegriff, also der Name, steht in ihrem Schlüssel) und die Antworten, die einen Namen ohne Kunden-Id tragen.
    /// Die Suche ohne Verbindung arbeitet danach mit den verbliebenen Kundenkarten.
    /// </summary>
    public void RemoveCustomers(IReadOnlyCollection<Guid> customers)
    {
        if (customers.Count == 0)
        {
            return;
        }

        // System.Text.Json schreibt eine Guid klein im Format „D“, im Pfad wie in der Antwort.
        var ids = customers.Select(id => id.ToString("D")).ToList();
        lock (_lock)
        {
            var hits = State.Responses
                .Where(r => r.Key.StartsWith(CustomerSearch, StringComparison.Ordinal)
                    || NamedWithoutId.Any(part => PathOf(r.Key).Contains(part, StringComparison.Ordinal))
                    || ids.Any(id => r.Key.Contains(id, StringComparison.OrdinalIgnoreCase) || r.Value.Json.Contains(id, StringComparison.OrdinalIgnoreCase)))
                .Select(r => r.Key)
                .ToList();
            foreach (var key in hits)
            {
                State.Responses.Remove(key);
            }

            // Vorgemerktes zu dem Gast bleibt (der Server entscheidet beim Abgleich), nennt ihn aber nicht mehr. Ein Ersatzbeleg bleibt, wie er ist.
            for (var i = 0; i < State.Pending.Count; i++)
            {
                var p = State.Pending[i];
                if (p.Substitute is null && ids.Any(id => p.Body.Contains(id, StringComparison.OrdinalIgnoreCase) || p.Path.Contains(id, StringComparison.OrdinalIgnoreCase)))
                {
                    State.Pending[i] = p with
                    {
                        Description = $"{DeletedGuest}: vorgemerkte Änderung",
                        Preview = p.Preview is null ? null : p.Preview with { CustomerName = DeletedGuest },
                        Visit = p.Visit is null ? null : p.Visit with { CustomerName = DeletedGuest },
                    };
                }
            }

            Persist();
        }
    }

    private static string PathOf(string key) => key.Split('?', 2)[0];

    private static List<(Guid Id, string Json)> CustomersOf(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind != JsonValueKind.Array ? [] :
            [
                .. doc.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var id) && id.TryGetGuid(out _))
                    .Select(item => (item.GetProperty("id").GetGuid(), item.GetRawText())),
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private bool IsExpired(CachedResponse response) => response.SavedAt < _time.GetUtcNow() - MaxAge;

    private List<CustomerDto> KnownCustomers()
    {
        var seen = new Dictionary<Guid, CustomerDto>();
        foreach (var (key, response) in State.Responses)
        {
            if (!key.StartsWith("api/customers", StringComparison.Ordinal) || key.EndsWith("/appointments", StringComparison.Ordinal) || IsExpired(response))
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

        // Ein anonymisierter Gast („Gelöschter Gast“ mit DeletedAt) ist kein Treffer mehr, wie in der Suche des Servers.
        return [.. seen.Values.Where(c => c.DeletedAt is null)];
    }

    // Wie die Suche des Servers: jeder Begriff muss treffen, „Ann Hub“ findet Anna Huber.
    private static bool Matches(CustomerDto c, string query) =>
        MatchesTerm(c, query) || query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(term => MatchesTerm(c, term));

    private static bool MatchesTerm(CustomerDto c, string query) =>
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

        _store.Save(State);
        _dirty = false;
        _lastSave = _time.GetUtcNow();
    }

    private static string Key(string path) => path.TrimStart('/');
}
