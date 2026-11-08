using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    protected override void OnSessionEnded() => Offline?.Clear();

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
        var result = await base.GetAsync<T>(path, ct).ConfigureAwait(false);
        if (result.Status == 0 && Offline is not null && OfflineCache.IsCacheable(path) && Offline.Get(path) is { } cached)
        {
            try
            {
                return new(JsonSerializer.Deserialize<T>(cached.Json, Json), null, 0) { CachedAt = cached.SavedAt };
            }
            catch (JsonException)
            {
            }
        }

        return result;
    }

    /// <summary>
    /// Termin ändern oder buchen. Ist der Server nicht erreichbar, wird die Änderung vorgemerkt und <paramref name="preview"/>
    /// zurückgegeben; gesendet wird beim nächsten Abgleich (<see cref="SyncAsync"/>). Der Server prüft dann wie sonst auch.
    /// </summary>
    public async Task<ApiResult<AppointmentDto>> PostOrQueueAsync(string path, object body, string description, AppointmentDto preview,
        object? queuedBody = null, CancellationToken ct = default)
    {
        var result = await PostAsync<AppointmentDto>(path, body, ct).ConfigureAwait(false);
        if (result.Status != 0 || Offline is null)
        {
            return result;
        }

        var queued = queuedBody ?? body;
        Offline.Enqueue(new PendingChange(Guid.NewGuid(), path, JsonSerializer.Serialize(queued, queued.GetType(), Json), description,
            DateTimeOffset.UtcNow, preview));
        return new(preview, null, 0) { Queued = true };
    }

    /// <summary>
    /// Kundin oder Kunde anlegen. Die Id vergibt der Arbeitsplatz, damit ohne Verbindung vorgemerkte Termine schon auf die neue
    /// Kundin verweisen können und ein wiederholtes Senden nichts doppelt anlegt. Ohne Verbindung wird die Anlage vorgemerkt und
    /// die Kundin sofort in der lokalen Suche gefunden.
    /// </summary>
    public async Task<ApiResult<CustomerDto>> CreateCustomerAsync(CustomerInput input, CancellationToken ct = default)
    {
        input = input with { ClientId = input.ClientId ?? Guid.CreateVersion7() };
        var result = await PostAsync<CustomerDto>("api/customers", input, ct).ConfigureAwait(false);
        if (result.Status != 0 || Offline is null)
        {
            return result;
        }

        var preview = new CustomerDto(input.ClientId!.Value, input.FirstName, input.LastName, input.Email, input.Phone, input.BirthDate, input.Note);
        Offline.Put($"api/customers/{preview.Id}", JsonSerializer.Serialize(preview, Json));
        Offline.Put($"api/customers/{preview.Id}/appointments", "[]");
        Offline.Enqueue(new PendingChange(Guid.NewGuid(), "api/customers", JsonSerializer.Serialize(input, Json),
            $"Neu angelegt: {input.FirstName} {input.LastName}", DateTimeOffset.UtcNow, null));
        return new(preview, null, 0) { Queued = true };
    }

    /// <summary>
    /// Eintritt in einen Bereich buchen. Ohne Verbindung wird er mit vom Arbeitsplatz vergebener Id vorgemerkt; ob noch Platz ist,
    /// entscheidet der Server beim Abgleich.
    /// </summary>
    public async Task<ApiResult<AreaVisitDto>> BookAreaVisitAsync(AreaVisitRequest request, string customerName, string description,
        CancellationToken ct = default)
    {
        request = request with { ClientId = request.ClientId ?? Guid.CreateVersion7() };
        var result = await PostAsync<AreaVisitDto>("api/area-visits", request, ct).ConfigureAwait(false);
        if (result.Status != 0 || Offline is null)
        {
            return result;
        }

        var preview = new AreaVisitDto(request.ClientId!.Value, request.LocationId, request.RoomId, request.CustomerId, customerName,
            request.Persons, request.From, request.Until, false, request.Note);
        Offline.Enqueue(new PendingChange(Guid.NewGuid(), "api/area-visits", JsonSerializer.Serialize(request, Json), description,
            DateTimeOffset.UtcNow, null, preview));
        return new(preview, null, 0) { Queued = true };
    }

    /// <summary>
    /// Paar- oder Spa-Tag-Buchung. Ohne Verbindung wird sie mit ihrer Id vorgemerkt und kommt ohne Termine zurück: wer behandelt und in
    /// welchem Raum, entscheidet erst der Server beim Abgleich; danach steht sie im Kalender.
    /// </summary>
    public async Task<ApiResult<GroupBookingResult>> BookGroupAsync(GroupBookingRequest request, string description, CancellationToken ct = default)
    {
        request = request with { ClientId = request.ClientId ?? Guid.CreateVersion7() };
        var result = await PostAsync<GroupBookingResult>("api/appointment-groups", request, ct).ConfigureAwait(false);
        if (result.Status != 0 || Offline is null)
        {
            return result;
        }

        Offline.Enqueue(new PendingChange(Guid.NewGuid(), "api/appointment-groups", JsonSerializer.Serialize(request, Json), description,
            DateTimeOffset.UtcNow, null));
        return new(new GroupBookingResult(request.ClientId!.Value, []), null, 0) { Queued = true };
    }

    /// <summary>
    /// Vorgemerkte Änderungen der Reihe nach senden. Lehnt der Server eine ab, landet sie mit Begründung unter „nicht übernommen“;
    /// ist er nicht erreichbar, bleibt der Rest für den nächsten Versuch. Gibt zurück, wie viele gesendet wurden.
    /// </summary>
    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        if (Offline is null || Offline.Pending.Count == 0)
        {
            return 0;
        }

        // Läuft schon ein Abgleich, nicht doppelt senden.
        if (Interlocked.Exchange(ref _syncing, 1) == 1)
        {
            return 0;
        }

        try
        {
            var sent = 0;
            var renewed = new Dictionary<(Guid Appointment, uint Version), uint>();
            foreach (var change in Offline.Pending)
            {
                // Termine liefern den gebuchten Termin zurück; andere Vormerkungen (z. B. neue Kunden) nur Erfolg oder Fehler.
                HttpStatusCode status;
                string? error;
                AppointmentDto? appointment = null;
                var (body, expected) = change.Preview is { } preview ? RenewVersion(change.Body, preview.Id, renewed) : (change.Body, null);
                using (var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"))
                {
                    if (change.Preview is null)
                    {
                        var plain = await SendAsync<JsonElement>(HttpMethod.Post, change.Path, content, ct).ConfigureAwait(false);
                        (status, error) = (plain.Status, plain.Error);
                    }
                    else
                    {
                        var booked = await SendAsync<AppointmentDto>(HttpMethod.Post, change.Path, content, ct).ConfigureAwait(false);
                        (status, error, appointment) = (booked.Status, booked.Error, booked.Value);
                    }
                }

                if (status == 0)
                {
                    break;
                }

                sent++;
                Offline.Complete(change.Id, error is null ? null : new FailedChange(change.Id, change.Description, error, DateTimeOffset.UtcNow));
                if (appointment is not null)
                {
                    if (expected is { } used)
                    {
                        renewed[(appointment.Id, used)] = appointment.Version;
                    }

                    Synced?.Invoke(appointment);
                }
            }

            return sent;
        }
        finally
        {
            Volatile.Write(ref _syncing, 0);
        }
    }

    /// <summary>
    /// Ä281: Mehrere Vormerkungen am selben Termin gingen alle vom Stand vor der ersten aus. Hat eine eigene davon beim Abgleich geklappt, bekommt die
    /// nächste die Version, die der Server dafür geliefert hat; nur eine fremde Änderung dazwischen führt dann zu „nicht übernommen“.
    /// </summary>
    /// <returns>Der zu sendende Körper und die erwartete Version darin (<c>null</c> ohne Versionsfeld).</returns>
    private static (string Body, uint? Expected) RenewVersion(string body, Guid appointmentId, Dictionary<(Guid Appointment, uint Version), uint> renewed)
    {
        if (JsonNode.Parse(body) is not JsonObject json || json["expectedVersion"] is not JsonValue field || field.GetValueKind() != JsonValueKind.Number)
        {
            return (body, null);
        }

        var original = field.GetValue<uint>();
        var version = original;
        for (var step = 0; step < renewed.Count && renewed.TryGetValue((appointmentId, version), out var next) && next != version; step++)
        {
            version = next;
        }

        if (version == original)
        {
            return (body, original);
        }

        json["expectedVersion"] = version;
        return (json.ToJsonString(Json), version);
    }
}
