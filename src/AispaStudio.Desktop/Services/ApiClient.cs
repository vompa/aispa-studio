using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Zugang der Windows-App zur API: der gemeinsame HTTP-Zugang (<see cref="HttpApiClient"/>) plus der Betrieb ohne Verbindung (ADR 0005):
/// zuletzt geladener Stand, vorgemerkte Änderungen und ihr Abgleich. Die ViewModels sehen nur <see cref="IWorkstationApi"/>.
/// </summary>
public sealed class ApiClient : HttpApiClient, IWorkstationApi
{
    public static readonly JsonSerializerOptions Json = ApiJson.Options;

    private int _syncing;

    /// <param name="certificateCode">
    /// Mehrplatz im Netz: Verbindungscode des Haupt-PCs (<see cref="NetworkShare"/>). Dann gilt nur dessen selbst ausgestelltes Zertifikat.
    /// </param>
    public ApiClient(Uri baseAddress, ISessionStore store, HttpMessageHandler? handler = null, OfflineCache? offline = null,
        ISessionStore? deviceStore = null, string? certificateCode = null)
        : base(baseAddress, store, handler, deviceStore, certificateCode is null ? null : certificate => NetworkShare.Matches(certificate, certificateCode))
    {
        CertificateCode = certificateCode;
        Offline = offline;
    }

    /// <summary>Verbindungscode des Haupt-PCs, wenn diese App mit einem freigegebenen Einzelplatz im Netz arbeitet.</summary>
    public string? CertificateCode { get; }

    /// <summary>Lokaler Stand für den Betrieb ohne Internet; ohne ihn liefern Aufrufe ohne Verbindung nur eine Fehlermeldung.</summary>
    public OfflineCache? Offline { get; }

    /// <summary>Ein vorgemerkter Termin wurde beim Abgleich vom Server bestätigt.</summary>
    public event Action<AppointmentDto>? Synced;

    /// <summary>Der lokale Sitzungswiderruf konnte nicht vollständig gespeichert werden.</summary>
    public event Action<Exception>? LocalStateFailed;

    /// <summary>Widerruft beim Lösen der Serververbindung den lokalen Zugang vor dem Neustart.</summary>
    public void ForgetSignIn() => RejectServerContext();

    protected override void OnSessionEnded() => Offline?.Clear(keepDevice: !ServerContextClosed);

    protected override void OnSessionConfirmed(CurrentUser user)
    {
        Offline?.Put("api/auth/me", JsonSerializer.Serialize(user, Json));
        Offline?.Flush();
    }

    protected override void OnSharedDeviceChanged(SharedDeviceDto? device)
    {
        if (Offline is not null)
        {
            Offline.SharedDevice = device;
        }
    }

    protected override void OnJsonReceived(string path, string json)
    {
        if (Offline is not null && OfflineCache.IsCacheable(path))
        {
            Offline.Put(path, json);
        }
    }

    /// <summary>
    /// Lesen. Ohne Verbindung kommt, soweit vorhanden, der zuletzt geladene Stand (<see cref="ApiResult{T}.CachedAt"/> gesetzt).
    /// </summary>
    public override async Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var version = SessionVersion;
        var result = await base.GetAsync<T>(path, ct).ConfigureAwait(false);
        lock (SessionLock)
        {
            if (!IsCurrentSession(version))
            {
                return new(default, "Die Anmeldung hat gewechselt.", HttpStatusCode.Conflict);
            }

            if (path == "api/auth/me" && result.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                try
                {
                    RejectSession();
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    LocalStateFailed?.Invoke(error);
                    return result with { Error = $"{result.Error} Die lokale Abmeldung konnte nicht vollständig gespeichert werden." };
                }
            }

            if (result.Ok && result.Value is QuickSwitchState state)
            {
                OnSharedDeviceChanged(state.Device);
            }

            return result.Status == 0 ? Offline?.Read<T>(path) ?? result : result;
        }
    }

    private (Task<ApiResult<T>> Response, Guid? Tenant, long Version) StartPost<T>(string path, object body, CancellationToken ct)
    {
        lock (SessionLock)
        {
            lock (Offline?.SyncRoot ?? SessionLock)
            {
                var version = SessionVersion;
                var tenant = IsCurrentSession(version) ? Offline?.Tenant : null;
                return (PostAsync<T>(path, body, ct), tenant, version);
            }
        }
    }

    private ApiResult<T> QueueForTenant<T>(OfflineCache cache, Guid tenant, long version, PendingChange change, T preview, Action? storePreview = null)
    {
        lock (SessionLock)
        {
            lock (cache.SyncRoot)
            {
                var current = IsCurrentSession(version) && cache.Tenant == tenant;
                if (current)
                {
                    storePreview?.Invoke();
                }

                cache.Enqueue(change with { TenantId = tenant });
                if (!current)
                {
                    return new(default, "Die Anmeldung oder der Betrieb hat gewechselt.", HttpStatusCode.Conflict) { Queued = true };
                }

                return new(preview, null, 0) { Queued = true };
            }
        }
    }

    /// <summary>
    /// Termin ändern oder buchen. Ist der Server nicht erreichbar, wird die Änderung vorgemerkt und <paramref name="preview"/>
    /// zurückgegeben; gesendet wird beim nächsten Abgleich (<see cref="SyncAsync"/>). Der Server prüft dann wie sonst auch.
    /// </summary>
    public async Task<ApiResult<AppointmentDto>> PostOrQueueAsync(string path, object body, string description, AppointmentDto preview,
        object? queuedBody = null, CancellationToken ct = default)
    {
        var (send, tenant, version) = StartPost<AppointmentDto>(path, body, ct);
        var result = await send.ConfigureAwait(false);
        if (result.Status != 0 || Offline is null || tenant is null || !OfflineCache.CanQueue(path))
        {
            return result;
        }

        var queued = queuedBody ?? body;
        return QueueForTenant(Offline, tenant.Value, version, new PendingChange(Guid.NewGuid(), path, JsonSerializer.Serialize(queued, queued.GetType(), Json), description,
            DateTimeOffset.UtcNow, preview), preview);
    }

    /// <summary>
    /// Kundin oder Kunde anlegen. Die Id vergibt der Arbeitsplatz, damit ohne Verbindung vorgemerkte Termine schon auf die neue
    /// Kundin verweisen können und ein wiederholtes Senden nichts doppelt anlegt. Ohne Verbindung wird die Anlage vorgemerkt und
    /// die Kundin sofort in der lokalen Suche gefunden.
    /// </summary>
    public async Task<ApiResult<CustomerDto>> CreateCustomerAsync(CustomerInput input, CancellationToken ct = default)
    {
        input = input with { ClientId = input.ClientId ?? Guid.CreateVersion7() };
        var (send, tenant, version) = StartPost<CustomerDto>("api/customers", input, ct);
        var result = await send.ConfigureAwait(false);
        if (result.Status != 0 || Offline is null || tenant is null)
        {
            return result;
        }

        var preview = new CustomerDto(input.ClientId!.Value, input.FirstName, input.LastName, input.Email, input.Phone, input.BirthDate, input.Note);
        var change = new PendingChange(Guid.NewGuid(), "api/customers", JsonSerializer.Serialize(input, Json),
            $"Neu angelegt: {input.FirstName} {input.LastName}", DateTimeOffset.UtcNow, null);
        return QueueForTenant(Offline, tenant.Value, version, change, preview, () =>
        {
            Offline.Put($"api/customers/{preview.Id}", JsonSerializer.Serialize(preview, Json));
            Offline.Put($"api/customers/{preview.Id}/appointments", "[]");
        });
    }

    /// <summary>
    /// Eintritt in einen Bereich buchen. Ohne Verbindung wird er mit vom Arbeitsplatz vergebener Id vorgemerkt; ob noch Platz ist,
    /// entscheidet der Server beim Abgleich.
    /// </summary>
    public async Task<ApiResult<AreaVisitDto>> BookAreaVisitAsync(AreaVisitRequest request, string customerName, string description,
        CancellationToken ct = default)
    {
        request = request with { ClientId = request.ClientId ?? Guid.CreateVersion7() };
        var (send, tenant, version) = StartPost<AreaVisitDto>("api/area-visits", request, ct);
        var result = await send.ConfigureAwait(false);
        if (result.Status != 0 || Offline is null || tenant is null)
        {
            return result;
        }

        var preview = new AreaVisitDto(request.ClientId!.Value, request.LocationId, request.RoomId, request.CustomerId, customerName,
            request.Persons, request.From, request.Until, false, request.Note);
        return QueueForTenant(Offline, tenant.Value, version, new PendingChange(Guid.NewGuid(), "api/area-visits", JsonSerializer.Serialize(request, Json), description,
            DateTimeOffset.UtcNow, null, preview), preview);
    }

    /// <summary>
    /// Paar- oder Spa-Tag-Buchung. Ohne Verbindung wird sie mit ihrer Id vorgemerkt und kommt ohne Termine zurück: wer behandelt und in
    /// welchem Raum, entscheidet erst der Server beim Abgleich; danach steht sie im Kalender.
    /// </summary>
    public async Task<ApiResult<GroupBookingResult>> BookGroupAsync(GroupBookingRequest request, string description, CancellationToken ct = default)
    {
        request = request with { ClientId = request.ClientId ?? Guid.CreateVersion7() };
        var (send, tenant, version) = StartPost<GroupBookingResult>("api/appointment-groups", request, ct);
        var result = await send.ConfigureAwait(false);
        if (result.Status != 0 || Offline is null || tenant is null)
        {
            return result;
        }

        return QueueForTenant(Offline, tenant.Value, version, new PendingChange(Guid.NewGuid(), "api/appointment-groups", JsonSerializer.Serialize(request, Json), description,
            DateTimeOffset.UtcNow, null), new GroupBookingResult(request.ClientId!.Value, []));
    }

    /// <summary>
    /// Vorgemerkte Änderungen der Reihe nach senden. Lehnt der Server eine ab, landet sie mit Begründung unter „nicht übernommen“;
    /// ist er nicht erreichbar, bleibt der Rest für den nächsten Versuch. Gibt zurück, wie viele gesendet wurden.
    /// </summary>
    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        if (Offline is null || Interlocked.Exchange(ref _syncing, 1) != 0)
        {
            return 0;
        }

        try
        {
            Guid tenant;
            long version;
            IReadOnlyList<PendingChange> pending;
            lock (SessionLock)
            {
                lock (Offline.SyncRoot)
                {
                    version = SessionVersion;
                    if (Offline.Tenant is not { } current || !IsCurrentSession(version))
                    {
                        return 0;
                    }

                    tenant = current;
                    pending = Offline.Pending;
                }
            }

            var sent = 0;
            foreach (var entry in pending)
            {
                PendingChange change;
                Task<(HttpStatusCode Status, string? Error, AppointmentDto? Appointment)> send;
                lock (SessionLock)
                {
                    lock (Offline.SyncRoot)
                    {
                        if (!IsCurrentSession(version) || Offline.Tenant != tenant)
                        {
                            break;
                        }

                        if (Offline.FindPending(entry.Id) is not { } current)
                        {
                            continue;
                        }

                        change = current;
                        send = SendPendingAsync(change, ct);
                    }
                }

                var (status, error, appointment) = await send.ConfigureAwait(false);
                if (status == 0)
                {
                    break;
                }

                var failed = error is null ? null : new FailedChange(change.Id, change.Description, error, DateTimeOffset.UtcNow, tenant, change.Substitute,
                    Path: change.Substitute is null ? null : change.Path, Body: change.Substitute is null ? null : change.Body);
                lock (SessionLock)
                {
                    lock (Offline.SyncRoot)
                    {
                        Offline.Complete(change.Id, failed, tenant, appointment);
                        if (appointment is not null && IsCurrentSession(version) && Offline.Tenant == tenant)
                        {
                            Synced?.Invoke(appointment);
                        }
                    }
                }

                sent++;
            }

            return sent;
        }
        finally
        {
            Volatile.Write(ref _syncing, 0);
        }
    }

    private async Task<(HttpStatusCode Status, string? Error, AppointmentDto? Appointment)> SendPendingAsync(PendingChange change, CancellationToken ct)
    {
        using var content = new StringContent(change.Body, System.Text.Encoding.UTF8, "application/json");
        if (change.Preview is null)
        {
            var result = await SendAsync<JsonElement>(HttpMethod.Post, change.Path, content, ct).ConfigureAwait(false);
            return (result.Status, result.Error, null);
        }

        var booked = await SendAsync<AppointmentDto>(HttpMethod.Post, change.Path, content, ct).ConfigureAwait(false);
        return (booked.Status, booked.Error, booked.Value);
    }
}
