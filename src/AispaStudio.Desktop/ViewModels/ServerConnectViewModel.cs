using System.Security.Authentication;
using System.Text.Json;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Verwalten von zu Hause (ADR 0084, Punkt 5): die Adresse des Servers eintragen, ohne Umgebungsvariable. Die App prüft die Verbindung mit dem
/// Zertifikat des Servers (Windows entscheidet wie bei jeder Webseite), merkt die Adresse und startet neu wie bei ADR 0049.
/// Erscheint, solange kein Server erreichbar ist.
/// </summary>
/// <param name="probe">Prüft die Adresse; gibt <c>null</c> zurück, wenn der Server antwortet, sonst die Meldung.</param>
/// <param name="restart">Startet die App neu, damit sie mit der neuen Adresse arbeitet.</param>
public partial class ServerConnectViewModel(ISettingsStore settings, Action restart,
    Func<Uri, CancellationToken, Task<string?>>? probe = null) : ViewModelBase
{
    private readonly Func<Uri, CancellationToken, Task<string?>> _probe = probe ?? ((address, ct) => ProbeAsync(address, ct));

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string Address { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gemerkter Server; dann lässt er sich auch wieder vergessen (z. B. nach einem Tippfehler, der sich verbinden ließ).</summary>
    public string? SavedText => settings.Load() is { ServerAddress: { } address, ServerCode: null } ? $"Eingetragen: {address}" : null;

    public bool HasSaved => SavedText is not null;

    private bool CanConnect => !IsBusy && Address.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        Error = null;
        if (Address.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            Error = "http:// ist unverschlüsselt. Die Adresse beginnt mit https://, z. B. https://spa.example.at.";
            return;
        }

        if (ServerChoice.Normalize(Address) is not { } address)
        {
            Error = "Das ist keine gültige Adresse. Beispiel: spa.example.at oder https://spa.example.at.";
            return;
        }

        IsBusy = true;
        try
        {
            Error = await _probe(address, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        if (Error is null)
        {
            settings.Save(settings.Load() with { ServerAddress = address.ToString(), ServerCode = null });
            restart();
        }
    }

    /// <summary>Den gemerkten Server wieder austragen.</summary>
    [RelayCommand]
    private void Forget()
    {
        settings.Save(settings.Load() with { ServerAddress = null, ServerCode = null });
        restart();
    }

    /// <summary>Fragt den Status ab; das Zertifikat prüft Windows.</summary>
    public static async Task<string?> ProbeAsync(Uri address, CancellationToken ct, HttpMessageHandler? handler = null)
    {
        var api = new HttpApiClient(address, new MemorySessionStore(), handler);
        try
        {
            return await api.StatusAsync(ct).ConfigureAwait(false) is null ? NotAServer(address) : null;
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            return $"Das Zertifikat von {address.Host} ist nicht gültig. Ein Server im Internet braucht ein öffentliches Zertifikat; für einen Haupt-PC im Spa gilt „Mit Haupt-PC im Netz verbinden“.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            return NotAServer(address);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return NotAServer(address);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return $"{address.Host} ist nicht erreichbar: {ex.Message} Stimmt die Adresse, und ist der Server oder das VPN des Spas an?";
        }
    }

    private static string NotAServer(Uri address) =>
        $"{address.Host} antwortet, ist aber kein AISPA-Studio-Server. Stimmt die Adresse?";
}
