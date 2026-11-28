using System.Net;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
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
    private const string UnconfirmedSignIn = "Der Server hat die Anmeldung nicht bestätigt.";

    /// <summary>Kennzeichen des Empfangsgeräts (ADR 0042); bleibt beim Abmelden und beim Personenwechsel erhalten.</summary>
    private const string DeviceCookieName = "aispa.device";

    private const string ApiPrefix = "api/";
    private const string VersionPrefix = "api/v1/";

    // Schon versioniert oder ohne Version (ADR 0085); dieselben Bereiche wie ApiVersion in der API.
    private static readonly string[] SkipPrefixes = [VersionPrefix, "api/online/", "api/hooks/", "api/operator/"];

    // Jede Sitzung besitzt ihren CookieContainer. Eine alte Antwort darf Set-Cookie nur dort anwenden.
    private sealed class SessionTransport(CookieContainer cookies, HttpClient http) : IDisposable
    {
        private int _references = 1;

        public bool Remember { get; set; }
        public CookieContainer Cookies { get; } = cookies;
        public HttpClient Http { get; } = http;

        public SessionTransport Retain()
        {
            Interlocked.Increment(ref _references);
            return this;
        }

        public void Dispose()
        {
            if (Interlocked.Decrement(ref _references) == 0)
            {
                Http.Dispose();
            }
        }
    }

    private readonly HttpMessageHandler? _handler;
    private readonly Func<X509Certificate2?, bool>? _serverCertificate;
    private SessionTransport _transport;
    private readonly ISessionStore _store;
    private readonly ISessionStore _deviceStore;
    private Task _lastCookieChange = Task.CompletedTask;
    private long _sessionVersion;
    private int _pendingLogouts;
    private bool _authenticating;

    protected Lock SessionLock { get; } = new();

    protected long SessionVersion => Volatile.Read(ref _sessionVersion);

    protected bool IsCurrentSession(long version) => version == SessionVersion && _pendingLogouts == 0 && !_authenticating;

    // CookieContainer übernimmt Set-Cookie schon beim Empfang der Antwort. Nur diese Anmeldewechsel laufen in Aufrufreihenfolge.
    private (Task Previous, TaskCompletionSource Finished) ReserveCookieChange()
    {
        lock (SessionLock)
        {
            var previous = _lastCookieChange;
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _lastCookieChange = finished.Task;
            return (previous, finished);
        }
    }

    /// <param name="serverCertificate">
    /// Gilt nur ein bestimmtes Server-Zertifikat (z. B. das selbst ausgestellte eines Haupt-PCs im Netz), prüft diese Funktion es.
    /// </param>
    public HttpApiClient(Uri baseAddress, ISessionStore store, HttpMessageHandler? handler = null, ISessionStore? deviceStore = null,
        Func<X509Certificate2?, bool>? serverCertificate = null)
    {
        _store = store;
        _deviceStore = deviceStore ?? new MemorySessionStore();
        BaseAddress = baseAddress;
        _handler = handler;
        _serverCertificate = serverCertificate;
        var cookies = new CookieContainer();
        if (store.Load() is { } saved)
        {
            cookies.Add(baseAddress, new Cookie(CookieName, saved));
        }

        if (_deviceStore.Load() is { } device)
        {
            cookies.Add(baseAddress, new Cookie(DeviceCookieName, device));
        }

        _transport = CreateTransport(cookies);
        _transport.Remember = cookies.GetCookies(baseAddress)[CookieName] is not null;
    }

    public Uri BaseAddress { get; }

    private SessionTransport CreateTransport(CookieContainer cookies)
    {
        var handler = _handler ?? new HttpClientHandler
        {
            CookieContainer = cookies,
            UseCookies = true,
            ServerCertificateCustomValidationCallback = _serverCertificate is null ? null : (_, certificate, _, _) => _serverCertificate(certificate),
        };
        return new(cookies, new HttpClient(handler, disposeHandler: _handler is null) { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(20) });
    }

    private SessionTransport CopyTransport(bool deviceOnly = false)
    {
        var cookies = new CookieContainer();
        foreach (Cookie cookie in _transport.Cookies.GetCookies(BaseAddress))
        {
            if (!deviceOnly || cookie.Name == DeviceCookieName)
            {
                cookies.Add(BaseAddress, cookie);
            }
        }

        var copy = CreateTransport(cookies);
        copy.Remember = !deviceOnly && _transport.Remember;
        return copy;
    }

    private SessionTransport CurrentTransport()
    {
        lock (SessionLock)
        {
            return _transport.Retain();
        }
    }

    private void ReplaceTransport(SessionTransport replacement)
    {
        var previous = _transport;
        _transport = replacement;
        previous.Dispose();
    }

    /// <summary>Der letzte Aufruf erreichte den Server nicht.</summary>
    public bool IsOffline { get; private set; }

    /// <summary>Verbindung verloren oder wieder da.</summary>
    public event Action? ConnectivityChanged;

    /// <summary>Ein Aufruf erreichte den Server nicht (technische Ursache für das Protokoll; die Oberfläche zeigt nur <see cref="Unreachable"/>).</summary>
    public event Action<Exception>? TransportFailed;

    /// <summary>Lesbarer Fehler mit Weg, ohne Systemtext (meldungen.md M5).</summary>
    public const string Unreachable = "Keine Verbindung. Verbindung prüfen und erneut versuchen.";

    /// <summary>Aktuelles Anmelde-Cookie für SignalR (das eine eigene Verbindung aufbaut).</summary>
    public string? SessionCookie
    {
        get
        {
            lock (SessionLock)
            {
                return _pendingLogouts == 0 ? _transport.Cookies.GetCookies(BaseAddress)[CookieName]?.Value : null;
            }
        }
    }

    /// <summary>Das Gerätekennzeichen ist vorhanden, auch wenn die letzte bestätigte Leerlaufvorgabe auf diesem PC noch fehlt.</summary>
    public bool HasDeviceCookie => _transport.Cookies.GetCookies(BaseAddress)[DeviceCookieName] is not null;

    public async Task<SystemStatus?> StatusAsync(CancellationToken ct = default)
    {
        using var transport = CurrentTransport();
        return await transport.Http.GetFromJsonAsync<SystemStatus>(Versioned("api/system/status"), ApiJson.Options, ct).ConfigureAwait(false);
    }

    public async Task<ApiResult<CurrentUser>> SignInWithCodeAsync(string code, CancellationToken ct = default)
    {
        var result = await AuthenticateAsync("api/auth/device-code/redeem", new DeviceCodeRedeem(code), ct).ConfigureAwait(false);
        if (result.Status == HttpStatusCode.Unauthorized)
        {
            return result with { Error = "Der Code stimmt nicht, ist abgelaufen oder wurde schon verwendet." };
        }

        return result;
    }

    /// <summary>
    /// Nur in der Entwicklungsumgebung der API: Anmeldung mit einem Testkonto des Beispiel-Spas (<c>@demo.aispa.test</c>)
    /// ohne Einmal-Code. In Produktion gibt es den Endpunkt nicht.
    /// </summary>
    public Task<ApiResult<CurrentUser>> SignInAsTestAccountAsync(string email, CancellationToken ct = default) =>
        AuthenticateAsync("api/auth/dev/login", new DevLogin(email, true), ct);

    /// <summary>Einzelplatz-Betrieb (ADR 0041): Anmeldung mit Benutzername und Passwort; die App bleibt angemeldet.</summary>
    public async Task<ApiResult<CurrentUser>> SignInWithPasswordAsync(string userName, string password, CancellationToken ct = default)
    {
        var result = await AuthenticateAsync("api/auth/standalone/login", new PasswordLogin(userName, password, true), ct).ConfigureAwait(false);
        if (result.Status == HttpStatusCode.Unauthorized)
        {
            return result with { Error = "Benutzername oder Passwort stimmt nicht." };
        }

        return result;
    }

    /// <summary>Einzelplatz, erster Start: Spa, ersten Standort und Inhaber-Konto anlegen; meldet gleich an.</summary>
    public Task<ApiResult<CurrentUser>> CompleteStandaloneSetupAsync(StandaloneSetup setup, CancellationToken ct = default) =>
        AuthenticateAsync("api/auth/standalone/setup", setup, ct);

    /// <summary>Passwort vergessen ohne Leitung: neues Passwort mit dem Wiederherstellungsschlüssel, danach angemeldet.</summary>
    public Task<ApiResult<CurrentUser>> RecoverPasswordAsync(string userName, string recoveryCode, string newPassword, CancellationToken ct = default) =>
        AuthenticateAsync("api/auth/standalone/recover", new PasswordRecovery(userName, recoveryCode, newPassword), ct);

    /// <summary>Das eigene Passwort ändern erneuert dieselbe Anmeldung.</summary>
    public Task<ApiResult<CurrentUser>> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) =>
        AuthenticateAsync("api/auth/standalone/password", new PasswordChange(currentPassword, newPassword), ct, renew: true);

    /// <summary>Abmelden. Das Kennzeichen des Empfangsgeräts bleibt, damit danach die nächste Person per PIN wechseln kann.</summary>
    public async Task SignOutAsync(CancellationToken ct = default)
    {
        // Den Platz vor späteren Anmeldungen reservieren. HTTP darf das bisherige Cookie noch für diesen Logout senden.
        var (turn, finished) = ReserveCookieChange();
        SessionTransport? logout = null;
        try
        {
            try
            {
                lock (SessionLock)
                {
                    _pendingLogouts++;
                    _sessionVersion++;
                    logout = CopyTransport();
                    ReplaceTransport(CopyTransport(deviceOnly: true));
                    ClearRememberedSession();
                }
            }
            finally
            {
                await turn.ConfigureAwait(false);
            }

            await SendAsync<object>(HttpMethod.Post, "api/auth/logout", null, logout, SessionVersion, ct).ConfigureAwait(false);
        }
        finally
        {
            logout?.Dispose();
            lock (SessionLock)
            {
                _pendingLogouts--;
            }

            finished.SetResult();
        }
    }

    /// <summary>
    /// Am Empfangsgerät zu einer Person wechseln (Name und PIN). Die Anmeldung wird nicht gemerkt: nach einem Neustart
    /// wählt die nächste Person wieder ihren Namen. Der lokale Stand der vorigen Person wird gelöscht.
    /// </summary>
    public async Task<ApiResult<CurrentUser>> QuickSwitchAsync(string userId, string pin, CancellationToken ct = default)
    {
        var result = await AuthenticateAsync("api/auth/quick-switch", new QuickSwitchRequest(userId, pin), ct, remember: false).ConfigureAwait(false);
        if (result.Status == HttpStatusCode.Unauthorized)
        {
            return result with { Error = "PIN falsch. Nach fünf Fehlversuchen ist sie 15 Minuten gesperrt." };
        }

        return result;
    }

    /// <summary>Diesen PC als Empfangsgerät eines Standorts anmelden; das Kennzeichen wird verschlüsselt gemerkt.</summary>
    public Task<ApiResult<SharedDeviceDto>> RegisterDeviceAsync(SharedDeviceInput input, CancellationToken ct = default) =>
        ChangeDeviceAsync<SharedDeviceDto>(HttpMethod.Post, input, ct);

    /// <summary>Dieser PC ist kein Empfangsgerät mehr.</summary>
    public Task<ApiResult<object>> RevokeDeviceAsync(CancellationToken ct = default) =>
        ChangeDeviceAsync<object>(HttpMethod.Delete, null, ct);

    private async Task<ApiResult<T>> ChangeDeviceAsync<T>(HttpMethod method, SharedDeviceInput? input, CancellationToken ct)
    {
        var (turn, finished) = ReserveCookieChange();
        await turn.ConfigureAwait(false);
        try
        {
            using var active = CurrentTransport();
            var result = await SendAsync<T>(method, "api/auth/shared-device", input, active, SessionVersion, ct).ConfigureAwait(false);
            if (result.Ok)
            {
                lock (SessionLock)
                {
                    if (result.Value is SharedDeviceDto device)
                    {
                        if (active.Cookies.GetCookies(BaseAddress)[DeviceCookieName] is { } cookie)
                        {
                            // Eine lokale Abmeldung darf schon begonnen haben. Das bestätigte Gerätekennzeichen bleibt am PC.
                            _transport.Cookies.Add(BaseAddress, cookie);
                            _deviceStore.Save(cookie.Value);
                        }

                        OnSharedDeviceChanged(device);
                    }
                    else if (method == HttpMethod.Delete)
                    {
                        if (_transport.Cookies.GetCookies(BaseAddress)[DeviceCookieName] is { } cookie)
                        {
                            cookie.Expired = true;
                        }

                        _deviceStore.Clear();
                        OnSharedDeviceChanged(null);
                    }
                }
            }

            return result;
        }
        finally
        {
            finished.SetResult();
        }
    }

    protected void RejectSession()
    {
        _sessionVersion++;
        ReplaceTransport(CopyTransport(deviceOnly: true));
        ClearRememberedSession();
    }

    private void ClearRememberedSession()
    {
        ExceptionDispatchInfo? sessionFailure = null;
        try
        {
            _store.Clear();
        }
        catch (Exception error)
        {
            sessionFailure = ExceptionDispatchInfo.Capture(error);
        }

        // Beide Speicher sind unabhängig. Jeder erhält seinen Löschversuch, bevor ein Fehler zurückgeht.
        try
        {
            OnSessionEnded();
        }
        catch (Exception cacheError) when (sessionFailure is not null)
        {
            throw new IOException("Sitzung und lokaler Lesestand konnten nicht gelöscht werden.", new AggregateException(sessionFailure.SourceException, cacheError));
        }

        sessionFailure?.Throw();
    }

    /// <summary>Die Anmeldung dieser Person endet (Abmelden, Personenwechsel); ein abgeleiteter Client löscht hier ihren lokalen Stand.</summary>
    protected virtual void OnSessionEnded()
    {
    }

    protected virtual void OnSessionConfirmed(CurrentUser user)
    {
    }

    /// <summary>Die Registrierung dieses Geräts wurde vom Server bestätigt oder aufgehoben.</summary>
    protected virtual void OnSharedDeviceChanged(SharedDeviceDto? device)
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
            using var transport = CurrentTransport();
            using var response = await transport.Http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
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
            using var transport = CurrentTransport();
            using var response = await transport.Http.GetAsync(Versioned(path), ct).ConfigureAwait(false);
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

    /// <summary>Eine Datei zum Speichern (Export) oder ein PDF der Dokumente (ADR 0092); der Dateiname kommt aus <c>Content-Disposition</c> (<c>filename*</c> vor <c>filename</c>).</summary>
    public async Task<ApiResult<ApiFile>> GetFileAsync(string path, CancellationToken ct = default)
    {
        try
        {
            using var transport = CurrentTransport();
            using var response = await transport.Http.GetAsync(Versioned(path), ct).ConfigureAwait(false);
            return await ReadFileAsync(response, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            ReportTransportFailure(ex);
            return new(null, Unreachable, 0);
        }
    }

    /// <summary>Wie <see cref="GetFileAsync"/>, aber mit einem Rumpf (z. B. eine hochgeladene Datei als <c>multipart/form-data</c>), dessen Antwort eine Datei ist.</summary>
    public async Task<ApiResult<ApiFile>> PostFileAsync(string path, HttpContent body, CancellationToken ct = default)
    {
        try
        {
            using var transport = CurrentTransport();
            using var response = await transport.Http.PostAsync(Versioned(path), body, ct).ConfigureAwait(false);
            return await ReadFileAsync(response, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            ReportTransportFailure(ex);
            return new(null, Unreachable, 0);
        }
    }

    private static async Task<ApiResult<ApiFile>> ReadFileAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            return new(null, await ErrorAsync(response, ct).ConfigureAwait(false), response.StatusCode);
        }

        var disposition = response.Content.Headers.ContentDisposition;
        var name = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
        return new(new ApiFile(await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false), string.IsNullOrWhiteSpace(name) ? null : name), null, response.StatusCode);
    }

    public Task<ApiResult<T>> PutAsync<T>(string path, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Put, path, body, ct);

    public Task<ApiResult<T>> PatchAsync<T>(string path, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Patch, path, body, ct);

    public Task<ApiResult<T>> DeleteAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Delete, path, null, ct);

    /// <summary>Löschen mit Rumpf (z. B. der Grund beim Entfernen eines Zeitabschnitts).</summary>
    public Task<ApiResult<T>> DeleteAsync<T>(string path, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Delete, path, body, ct);

    /// <summary>Senden; <paramref name="body"/> als fertiger <see cref="HttpContent"/> (z. B. ein vorgemerkter JSON-Text) oder als Objekt.</summary>
    protected async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        SessionTransport active;
        long version;
        lock (SessionLock)
        {
            active = _transport.Retain();
            version = SessionVersion;
        }

        using (active)
        {
            return await SendAsync<T>(method, path, body, active, version, ct).ConfigureAwait(false);
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, SessionTransport transport, long version, CancellationToken ct, bool requireAuthCookie = false)
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

            using var response = await transport.Http.SendAsync(request, ct).ConfigureAwait(false);
            SetOffline(false);
            if (response.IsSuccessStatusCode)
            {
                if (requireAuthCookie && !ConfirmResponseCookie(response, new Uri(BaseAddress, Versioned(path)), transport))
                {
                    return new(default, UnconfirmedSignIn, HttpStatusCode.BadGateway);
                }

                if (response.Content.Headers.ContentLength == 0 || response.StatusCode == HttpStatusCode.NoContent)
                {
                    return new(default, null, response.StatusCode);
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (method == HttpMethod.Get)
                {
                    lock (SessionLock)
                    {
                        if (IsCurrentSession(version))
                        {
                            OnJsonReceived(path, json);
                        }
                    }
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
            return new(default, "AISPA Studio antwortet nicht.", 0);
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

    // Nur die Header dieser Antwort belegen die Ausstellung, auch bei der Erneuerung einer vorhandenen Sitzung.
    private bool ConfirmResponseCookie(HttpResponseMessage response, Uri requestUri, SessionTransport transport)
    {
        var origin = response.RequestMessage?.RequestUri ?? requestUri;
        if (origin != requestUri || !response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            return false;
        }

        var issued = new CookieContainer();
        foreach (var header in headers)
        {
            try
            {
                issued.SetCookies(origin, header);
            }
            catch (CookieException)
            {
                return false;
            }
        }

        if (issued.GetCookies(BaseAddress)[CookieName] is not { Name: CookieName, Value.Length: > 0 } cookie)
        {
            return false;
        }

        // Der native HTTP-Handler pflegt seinen Container selbst. Injizierte Handler liefern nur die Header.
        if (_handler is not null)
        {
            transport.Cookies.Add(BaseAddress, cookie);
        }

        return transport.Cookies.GetCookies(BaseAddress)[CookieName]?.Value == cookie.Value;
    }

    private async Task<ApiResult<CurrentUser>> AuthenticateAsync(string path, object input, CancellationToken ct, bool remember = true, bool renew = false)
    {
        var (turn, finished) = ReserveCookieChange();
        await turn.ConfigureAwait(false);
        SessionTransport? candidate = null;
        var changingIdentity = false;
        try
        {
            lock (SessionLock)
            {
                candidate = CopyTransport(deviceOnly: !renew);
                _authenticating = true;
                _sessionVersion++;
            }

            var result = await SendAsync<CurrentUser>(HttpMethod.Post, path, input, candidate, SessionVersion, ct, requireAuthCookie: true).ConfigureAwait(false);
            lock (SessionLock)
            {
                if (_pendingLogouts != 0)
                {
                    return new(null, "Die Anmeldung wurde beendet.", HttpStatusCode.Conflict);
                }

                if (!result.Ok)
                {
                    return result;
                }

                if (result.Value is not { } user || string.IsNullOrWhiteSpace(user.Id))
                {
                    return new(null, UnconfirmedSignIn, HttpStatusCode.BadGateway);
                }

                changingIdentity = true;
                ReplaceTransport(candidate.Retain());
                if (!renew)
                {
                    ClearRememberedSession();
                    candidate.Remember = remember;
                }

                if (candidate.Remember && user is { Access: not null, MustChangePassword: false } && candidate.Cookies.GetCookies(BaseAddress)[CookieName] is { } cookie)
                {
                    OnSessionConfirmed(user);
                    _store.Save(cookie.Value);
                }
            }

            return result;
        }
        catch when (changingIdentity)
        {
            lock (SessionLock)
            {
                ReplaceTransport(CopyTransport(deviceOnly: true));
            }

            throw;
        }
        finally
        {
            lock (SessionLock)
            {
                _authenticating = false;
                _sessionVersion++;
            }

            candidate?.Dispose();
            finished.SetResult();
        }
    }
}
