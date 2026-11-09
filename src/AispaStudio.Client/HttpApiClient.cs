using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Client;

/// <summary>
/// Zugang zur API mit dem Anmelde-Cookie der angemeldeten Person.
/// Das Cookie wird über <see cref="ISessionStore"/> gemerkt, damit die Rezeption sich nicht bei jedem Start neu anmeldet.
/// Was ohne Verbindung geschieht (lokaler Stand, Vormerkungen), ergänzt ein abgeleiteter Client über die geschützten Haken.
/// </summary>
public class HttpApiClient : IAispaApi
{
    private const string CookieName = "aispa.auth";

    /// <summary>Kennzeichen des Empfangsgeräts (ADR 0042); bleibt beim Abmelden und beim Personenwechsel erhalten.</summary>
    private const string DeviceCookieName = "aispa.device";

    private const string ApiPrefix = "api/";
    private const string VersionPrefix = "api/v1/";

    // Schon versioniert oder ohne Version (ADR 0085); dieselben Bereiche wie ApiVersion in der API.
    private static readonly string[] SkipPrefixes = [VersionPrefix, "api/online/", "api/hooks/", "api/operator/"];

    private readonly HttpClient _http;
    private readonly CookieContainer _cookies;
    private readonly ISessionStore _store;
    private readonly ISessionStore _deviceStore;

    /// <param name="serverCertificate">
    /// Gilt nur ein bestimmtes Server-Zertifikat (z. B. das selbst ausgestellte eines Haupt-PCs im Netz), prüft diese Funktion es.
    /// </param>
    public HttpApiClient(Uri baseAddress, ISessionStore store, HttpMessageHandler? handler = null, ISessionStore? deviceStore = null,
        Func<X509Certificate2?, bool>? serverCertificate = null)
    {
        _store = store;
        _deviceStore = deviceStore ?? new MemorySessionStore();
        _cookies = new CookieContainer();
        if (store.Load() is { } saved)
        {
            _cookies.Add(baseAddress, new Cookie(CookieName, saved));
        }

        if (_deviceStore.Load() is { } device)
        {
            _cookies.Add(baseAddress, new Cookie(DeviceCookieName, device));
        }

        // Kurzes Zeitlimit: am Empfang ist „keine Verbindung“ nach 20 Sekunden besser als eine hängende Oberfläche.
        _http = handler is null
            ? new HttpClient(new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                ServerCertificateCustomValidationCallback = serverCertificate is null ? null : (_, certificate, _, _) => serverCertificate(certificate),
            })
            { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(20) }
            : new HttpClient(handler) { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(20) };
    }

    public Uri BaseAddress => _http.BaseAddress!;

    /// <summary>Der letzte Aufruf erreichte den Server nicht.</summary>
    public bool IsOffline { get; private set; }

    /// <summary>Verbindung verloren oder wieder da.</summary>
    public event Action? ConnectivityChanged;

    /// <summary>Ein Aufruf erreichte den Server nicht (technische Ursache für das Protokoll; die Oberfläche zeigt nur <see cref="Unreachable"/>).</summary>
    public event Action<Exception>? TransportFailed;

    /// <summary>Lesbarer Fehler mit Weg, ohne Systemtext (meldungen.md M5).</summary>
    public const string Unreachable = "Server nicht erreichbar. Verbindung prüfen und erneut versuchen.";

    /// <summary>Aktuelles Anmelde-Cookie für SignalR (das eine eigene Verbindung aufbaut).</summary>
    public string? SessionCookie => _cookies.GetCookies(BaseAddress)[CookieName]?.Value ?? _store.Load();

    public async Task<SystemStatus?> StatusAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<SystemStatus>(Versioned("api/system/status"), ApiJson.Options, ct).ConfigureAwait(false);

    public async Task<ApiResult<CurrentUser>> SignInWithCodeAsync(string code, CancellationToken ct = default)
    {
        var result = await SendAsync<CurrentUser>(HttpMethod.Post, "api/auth/device-code/redeem", new DeviceCodeRedeem(code), ct).ConfigureAwait(false);
        if (result.Ok)
        {
            Remember();
        }
        else if (result.Status == HttpStatusCode.Unauthorized)
        {
            return result with { Error = "Der Code stimmt nicht, ist abgelaufen oder wurde schon verwendet." };
        }

        return result;
    }

    /// <summary>
    /// Nur in der Entwicklungsumgebung der API: Anmeldung mit einem Testkonto des Beispiel-Spas (<c>@demo.aispa.test</c>)
    /// ohne Einmal-Code. In Produktion gibt es den Endpunkt nicht.
    /// </summary>
    public async Task<ApiResult<CurrentUser>> SignInAsTestAccountAsync(string email, CancellationToken ct = default)
    {
        var result = await SendAsync<CurrentUser>(HttpMethod.Post, "api/auth/dev/login", new DevLogin(email, true), ct).ConfigureAwait(false);
        if (result.Ok)
        {
            Remember();
        }

        return result;
    }

    /// <summary>Einzelplatz-Betrieb (ADR 0041): Anmeldung mit Benutzername und Passwort; die App bleibt angemeldet.</summary>
    public async Task<ApiResult<CurrentUser>> SignInWithPasswordAsync(string userName, string password, CancellationToken ct = default)
    {
        var result = await SendAsync<CurrentUser>(HttpMethod.Post, "api/auth/standalone/login", new PasswordLogin(userName, password, true), ct).ConfigureAwait(false);
        if (result.Ok)
        {
            Remember();
        }
        else if (result.Status == HttpStatusCode.Unauthorized)
        {
            return result with { Error = "Benutzername oder Passwort stimmt nicht." };
        }

        return result;
    }

    /// <summary>Einzelplatz, erster Start: Spa, ersten Standort und Inhaber-Konto anlegen; meldet gleich an.</summary>
    public async Task<ApiResult<CurrentUser>> CompleteStandaloneSetupAsync(StandaloneSetup setup, CancellationToken ct = default)
    {
        var result = await SendAsync<CurrentUser>(HttpMethod.Post, "api/auth/standalone/setup", setup, ct).ConfigureAwait(false);
        if (result.Ok)
        {
            Remember();
        }

        return result;
    }

    /// <summary>Eigenes Passwort ändern; nach einem Startpasswort Pflicht. Das neue Anmelde-Cookie wird gemerkt.</summary>
    /// <summary>Passwort vergessen ohne Leitung: neues Passwort mit dem Wiederherstellungsschlüssel, danach angemeldet.</summary>
    public async Task<ApiResult<CurrentUser>> RecoverPasswordAsync(string userName, string recoveryCode, string newPassword, CancellationToken ct = default)
    {
        var result = await SendAsync<CurrentUser>(HttpMethod.Post, "api/auth/standalone/recover", new PasswordRecovery(userName, recoveryCode, newPassword), ct)
            .ConfigureAwait(false);
        if (result.Ok)
        {
            Remember();
        }

        return result;
    }

    public async Task<ApiResult<CurrentUser>> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var result = await SendAsync<CurrentUser>(HttpMethod.Post, "api/auth/standalone/password", new PasswordChange(currentPassword, newPassword), ct).ConfigureAwait(false);
        if (result.Ok)
        {
            Remember();
        }

        return result;
    }

    /// <summary>Abmelden. Das Kennzeichen des Empfangsgeräts bleibt, damit danach die nächste Person per PIN wechseln kann.</summary>
    public async Task SignOutAsync(CancellationToken ct = default)
    {
        await SendAsync<object>(HttpMethod.Post, "api/auth/logout", null, ct).ConfigureAwait(false);
        ForgetSession();
    }

    /// <summary>
    /// Am Empfangsgerät zu einer Person wechseln (Name und PIN). Die Anmeldung wird nicht gemerkt: nach einem Neustart
    /// wählt die nächste Person wieder ihren Namen. Der lokale Stand der vorigen Person wird gelöscht.
    /// </summary>
    public async Task<ApiResult<CurrentUser>> QuickSwitchAsync(string userId, string pin, CancellationToken ct = default)
    {
        var result = await SendAsync<CurrentUser>(HttpMethod.Post, "api/auth/quick-switch", new QuickSwitchRequest(userId, pin), ct).ConfigureAwait(false);
        if (result.Ok)
        {
            _store.Clear();
            OnSessionEnded();
        }
        else if (result.Status == HttpStatusCode.Unauthorized)
        {
            return result with { Error = "PIN falsch. Nach fünf Fehlversuchen ist sie 15 Minuten gesperrt." };
        }

        return result;
    }

    /// <summary>Diesen PC als Empfangsgerät eines Standorts anmelden; das Kennzeichen wird verschlüsselt gemerkt.</summary>
    public async Task<ApiResult<SharedDeviceDto>> RegisterDeviceAsync(SharedDeviceInput input, CancellationToken ct = default)
    {
        var result = await SendAsync<SharedDeviceDto>(HttpMethod.Post, "api/auth/shared-device", input, ct).ConfigureAwait(false);
        if (result.Ok && _cookies.GetCookies(BaseAddress)[DeviceCookieName] is { } cookie)
        {
            _deviceStore.Save(cookie.Value);
        }

        return result;
    }

    /// <summary>Dieser PC ist kein Empfangsgerät mehr.</summary>
    public async Task<ApiResult<object>> RevokeDeviceAsync(CancellationToken ct = default)
    {
        var result = await SendAsync<object>(HttpMethod.Delete, "api/auth/shared-device", null, ct).ConfigureAwait(false);
        if (result.Ok)
        {
            if (_cookies.GetCookies(BaseAddress)[DeviceCookieName] is { } cookie)
            {
                cookie.Expired = true;
            }

            _deviceStore.Clear();
        }

        return result;
    }

    private void ForgetSession()
    {
        foreach (Cookie cookie in _cookies.GetCookies(BaseAddress))
        {
            if (cookie.Name != DeviceCookieName)
            {
                cookie.Expired = true;
            }
        }

        _store.Clear();
        OnSessionEnded();
    }

    /// <summary>Die Anmeldung dieser Person endet (Abmelden, Personenwechsel); ein abgeleiteter Client löscht hier ihren lokalen Stand.</summary>
    protected virtual void OnSessionEnded()
    {
    }

    /// <summary>Ein GET hat geklappt; ein abgeleiteter Client merkt sich hier die Antwort für den Betrieb ohne Verbindung.</summary>
    protected virtual void OnJsonReceived(string path, string json)
    {
    }

    /// <summary>Lesen. Ein abgeleiteter Client kann ohne Verbindung auf einen lokalen Stand zurückfallen.</summary>
    public virtual Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Get, path, null, ct);

    public Task<ApiResult<T>> PostAsync<T>(string path, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, path, body, ct);

    /// <summary>Liefert die Adresse eine HTML-Seite (z. B. <c>/anmelden/</c> im Einzelplatz)? Ohne Verbindung oder bei einer anderen Antwort <c>false</c>.</summary>
    public async Task<bool> IsPageAsync(Uri address, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "text/html";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Rohdaten, z. B. ESC/POS-Befehle für den Bondrucker.</summary>
    public async Task<ApiResult<byte[]>> GetBytesAsync(string path, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(Versioned(path), ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new(await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false), null, response.StatusCode)
                : new(null, await ErrorAsync(response, ct).ConfigureAwait(false), response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            ReportTransportFailure(ex);
            return new(null, Unreachable, 0);
        }
    }

    /// <summary>Eine Datei zum Speichern (Export); der Dateiname kommt aus <c>Content-Disposition</c> (<c>filename*</c> vor <c>filename</c>).</summary>
    public async Task<ApiResult<ApiFile>> GetFileAsync(string path, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(Versioned(path), ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new(null, await ErrorAsync(response, ct).ConfigureAwait(false), response.StatusCode);
            }

            var disposition = response.Content.Headers.ContentDisposition;
            var name = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
            return new(new ApiFile(await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false), string.IsNullOrWhiteSpace(name) ? null : name), null, response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            ReportTransportFailure(ex);
            return new(null, Unreachable, 0);
        }
    }

    public Task<ApiResult<T>> PutAsync<T>(string path, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Put, path, body, ct);

    public Task<ApiResult<T>> PatchAsync<T>(string path, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Patch, path, body, ct);

    public Task<ApiResult<T>> DeleteAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Delete, path, null, ct);

    /// <summary>Löschen mit Rumpf (z. B. der Grund beim Entfernen eines Zeitabschnitts).</summary>
    public Task<ApiResult<T>> DeleteAsync<T>(string path, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Delete, path, body, ct);

    /// <summary>Senden; <paramref name="body"/> als fertiger <see cref="HttpContent"/> (z. B. ein vorgemerkter JSON-Text) oder als Objekt.</summary>
    protected async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, Versioned(path));
            if (body is HttpContent content)
            {
                request.Content = content;
            }
            else if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType(), options: ApiJson.Options);
            }

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            SetOffline(false);
            if (response.IsSuccessStatusCode)
            {
                if (response.Content.Headers.ContentLength == 0 || response.StatusCode == HttpStatusCode.NoContent)
                {
                    return new(default, null, response.StatusCode);
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (method == HttpMethod.Get)
                {
                    OnJsonReceived(path, json);
                }

                return new(json.Length == 0 ? default : JsonSerializer.Deserialize<T>(json, ApiJson.Options), null, response.StatusCode);
            }

            return new(default, await ErrorAsync(response, ct).ConfigureAwait(false), response.StatusCode)
            {
                Rejection = response.StatusCode == HttpStatusCode.Conflict ? await RejectionAsync(response, ct).ConfigureAwait(false) : null,
            };
        }
        catch (HttpRequestException ex)
        {
            ReportTransportFailure(ex);
            SetOffline(true);
            return new(default, Unreachable, 0);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            SetOffline(true);
            return new(default, "Server antwortet nicht.", 0);
        }
    }

    /// <summary>
    /// Team-API unter <c>api/v1/…</c> (ADR 0085). Aufrufer, Offline-Stand und vorgemerkte Aufrufe behalten <c>api/…</c>; nur hier kommt die Version dazu.
    /// Kundenbereich und Betreiber haben keine Version. Pfade sind relativ zur Server-Adresse, ohne führenden Schrägstrich.
    /// </summary>
    private static string Versioned(string path) =>
        path.StartsWith(ApiPrefix, StringComparison.Ordinal) && !SkipPrefixes.Any(p => path.StartsWith(p, StringComparison.Ordinal))
            ? VersionPrefix + path[ApiPrefix.Length..]
            : path;

    /// <summary>Die technische Ursache (Systemtext mit Host und Port) gehört ins Protokoll der App, nicht in die Oberfläche; einmal je Übergang zu „ohne Verbindung“.</summary>
    private void ReportTransportFailure(HttpRequestException error)
    {
        if (!IsOffline)
        {
            TransportFailed?.Invoke(error);
        }
    }

    private void SetOffline(bool offline)
    {
        if (IsOffline != offline)
        {
            IsOffline = offline;
            ConnectivityChanged?.Invoke();
        }
    }

    private static async Task<BookingRejected?> RejectionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BookingRejected>(ApiJson.Options, ct).ConfigureAwait(false) is { Message: not null } rejected
                ? rejected
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string> ErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var message))
            {
                // BookingRejected: Begründung und betroffene Ressourcen.
                var reasons = root.TryGetProperty("conflicts", out var conflicts)
                    ? string.Join(" ", conflicts.EnumerateArray().Select(c => $"{c.GetProperty("resourceName").GetString()}: {c.GetProperty("reason").GetString()}."))
                    : "";
                return $"{message.GetString()} {reasons}".Trim();
            }

            if (root.TryGetProperty("errors", out var errors))
            {
                return string.Join(" ", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray().Select(v => v.GetString())));
            }

            if (root.TryGetProperty("detail", out var detail) || root.TryGetProperty("title", out detail))
            {
                return detail.GetString() ?? response.ReasonPhrase ?? "Fehler";
            }
        }
        catch (JsonException)
        {
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Nicht mehr angemeldet.",
            HttpStatusCode.Forbidden => "Dafür fehlt die Berechtigung.",
            HttpStatusCode.NotFound => "Nicht gefunden.",
            _ => $"Fehler {(int)response.StatusCode}.",
        };
    }

    private void Remember()
    {
        if (_cookies.GetCookies(BaseAddress)[CookieName] is { } cookie)
        {
            _store.Save(cookie.Value);
        }
    }
}
