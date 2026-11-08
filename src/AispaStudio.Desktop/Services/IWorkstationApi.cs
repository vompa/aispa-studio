using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Zugang der ViewModels zur API: der gemeinsame HTTP-Zugang (<see cref="IAispaApi"/>) plus der Betrieb ohne Verbindung der Windows-App
/// (ADR 0005). Umgesetzt von <see cref="ApiClient"/>; die ViewModels hängen nur an dieser Schnittstelle (Ä282).
/// </summary>
public interface IWorkstationApi : IAispaApi
{
    /// <summary>Verbindungscode des Haupt-PCs, wenn diese App mit einem freigegebenen Einzelplatz im Netz arbeitet.</summary>
    string? CertificateCode { get; }

    /// <summary>Lokaler Stand für den Betrieb ohne Internet; ohne ihn liefern Aufrufe ohne Verbindung nur eine Fehlermeldung.</summary>
    OfflineCache? Offline { get; }

    /// <summary>Ein vorgemerkter Termin wurde beim Abgleich vom Server bestätigt.</summary>
    event Action<AppointmentDto>? Synced;

    /// <summary>Termin ändern oder buchen; ohne Verbindung vorgemerkt, <paramref name="preview"/> zeigt ihn bis zum Abgleich.</summary>
    Task<ApiResult<AppointmentDto>> PostOrQueueAsync(string path, object body, string description, AppointmentDto preview,
        object? queuedBody = null, CancellationToken ct = default);

    /// <summary>Kundin oder Kunde anlegen; ohne Verbindung vorgemerkt und gleich lokal auffindbar.</summary>
    Task<ApiResult<CustomerDto>> CreateCustomerAsync(CustomerInput input, CancellationToken ct = default);

    /// <summary>Eintritt in einen Bereich buchen; ohne Verbindung vorgemerkt.</summary>
    Task<ApiResult<AreaVisitDto>> BookAreaVisitAsync(AreaVisitRequest request, string customerName, string description, CancellationToken ct = default);

    /// <summary>Paar- oder Spa-Tag-Buchung; ohne Verbindung vorgemerkt.</summary>
    Task<ApiResult<GroupBookingResult>> BookGroupAsync(GroupBookingRequest request, string description, CancellationToken ct = default);

    /// <summary>Vorgemerkte Änderungen der Reihe nach senden; gibt zurück, wie viele gesendet wurden.</summary>
    Task<int> SyncAsync(CancellationToken ct = default);
}
