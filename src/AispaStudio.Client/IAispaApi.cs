using AispaStudio.Contracts;

namespace AispaStudio.Client;

/// <summary>
/// Zugang zur API mit dem Anmelde-Cookie der angemeldeten Person: lesen, schreiben, anmelden. Plattformunabhängig;
/// was ohne Verbindung geschieht, ergänzt der jeweilige Client (Windows-App: <c>IWorkstationApi</c>).
/// </summary>
public interface IAispaApi
{
    Uri BaseAddress { get; }

    /// <summary>Der letzte Aufruf erreichte den Server nicht.</summary>
    bool IsOffline { get; }

    /// <summary>Verbindung verloren oder wieder da.</summary>
    event Action? ConnectivityChanged;

    /// <summary>Aktuelles Anmelde-Cookie für SignalR (das eine eigene Verbindung aufbaut).</summary>
    string? SessionCookie { get; }

    Task<SystemStatus?> StatusAsync(CancellationToken ct = default);

    Task<ApiResult<CurrentUser>> SignInWithCodeAsync(string code, CancellationToken ct = default);

    Task<ApiResult<CurrentUser>> SignInAsTestAccountAsync(string email, CancellationToken ct = default);

    Task<ApiResult<CurrentUser>> SignInWithPasswordAsync(string userName, string password, CancellationToken ct = default);

    Task<ApiResult<CurrentUser>> CompleteStandaloneSetupAsync(StandaloneSetup setup, CancellationToken ct = default);

    Task<ApiResult<CurrentUser>> RecoverPasswordAsync(string userName, string recoveryCode, string newPassword, CancellationToken ct = default);

    Task<ApiResult<CurrentUser>> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    Task SignOutAsync(CancellationToken ct = default);

    Task<ApiResult<CurrentUser>> QuickSwitchAsync(string userId, string pin, CancellationToken ct = default);

    Task<ApiResult<SharedDeviceDto>> RegisterDeviceAsync(SharedDeviceInput input, CancellationToken ct = default);

    Task<ApiResult<object>> RevokeDeviceAsync(CancellationToken ct = default);

    Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct = default);

    Task<ApiResult<T>> PostAsync<T>(string path, object? body, CancellationToken ct = default);

    Task<ApiResult<T>> PutAsync<T>(string path, object? body, CancellationToken ct = default);

    Task<ApiResult<T>> PatchAsync<T>(string path, object? body, CancellationToken ct = default);

    Task<ApiResult<T>> DeleteAsync<T>(string path, CancellationToken ct = default);

    /// <summary>Löschen mit Rumpf (z. B. der Grund beim Entfernen eines Zeitabschnitts).</summary>
    Task<ApiResult<T>> DeleteAsync<T>(string path, object body, CancellationToken ct = default);

    /// <summary>Rohdaten, z. B. ESC/POS-Befehle für den Bondrucker.</summary>
    Task<ApiResult<byte[]>> GetBytesAsync(string path, CancellationToken ct = default);

    /// <summary>Liefert die Adresse eine HTML-Seite (z. B. <c>/anmelden/</c> im Einzelplatz)? Ohne Verbindung oder bei einer anderen Antwort <c>false</c>.</summary>
    Task<bool> IsPageAsync(Uri address, CancellationToken ct = default);
}
