using System.Security.Authentication;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Mehrplatz im Netz, an einem weiteren PC (ADR 0041): Adresse und Verbindungscode des Haupt-PCs eintragen, wie sie dort unter
/// „Im Netz“ stehen. Die App prüft die Verbindung, merkt sich beides und startet neu. Erscheint, solange kein Server erreichbar ist.
/// </summary>
/// <param name="probe">Prüft Adresse und Code; gibt <c>null</c> zurück, wenn der Haupt-PC antwortet, sonst die Meldung.</param>
/// <param name="restart">Startet die App neu, damit sie mit der neuen Adresse arbeitet.</param>
public partial class NetworkConnectViewModel(ISettingsStore settings, Action restart,
    Func<Uri, string, CancellationToken, Task<string?>>? probe = null) : ViewModelBase
{
    private readonly Func<Uri, string, CancellationToken, Task<string?>> _probe = probe ?? ProbeAsync;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string Address { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial string Code { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gemerkter Haupt-PC; dann lässt er sich auch wieder vergessen.</summary>
    public string? SavedText => settings.Load() is { ServerAddress: { } address, ServerCode: not null } ? $"Eingetragen: {address}" : null;

    public bool HasSaved => SavedText is not null;

    private bool CanConnect => !IsBusy && Address.Trim().Length > 0 && Code.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        Error = null;
        if (NetworkShare.NormalizeAddress(Address) is not { } address)
        {
            Error = "Die Adresse steht am Haupt-PC unter „Im Netz“, z. B. https://empfang-pc:55443.";
            return;
        }

        if (NetworkShare.NormalizeCode(Code) is not { } code)
        {
            Error = "Der Verbindungscode hat 20 Zeichen in Vierergruppen, z. B. 3F9A-12C4-77B0-E1D2-9A0B.";
            return;
        }

        IsBusy = true;
        try
        {
            Error = await _probe(address, code, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        if (Error is null)
        {
            settings.Save(settings.Load() with { ServerAddress = address.ToString(), ServerCode = code });
            restart();
        }
    }

    /// <summary>Zurück zum Server aus der Voreinstellung (z. B. nach einem Umzug auf einen echten Server).</summary>
    [RelayCommand]
    private void Forget()
    {
        settings.Save(settings.Load() with { ServerAddress = null, ServerCode = null });
        restart();
    }

    /// <summary>Fragt den Status ab, nur mit dem Zertifikat zum Code.</summary>
    public static async Task<string?> ProbeAsync(Uri address, string code, CancellationToken ct)
    {
        var api = new HttpApiClient(address, new MemorySessionStore(), serverCertificate: certificate => NetworkShare.Matches(certificate, code));
        try
        {
            return await api.StatusAsync(ct).ConfigureAwait(false) is null ? "Leere Antwort vom Haupt-PC." : null;
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            return $"Der Verbindungscode passt nicht zu {address.Host}. Bitte am Haupt-PC unter „Im Netz“ nachsehen.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return $"{address.Host} ist nicht erreichbar: {ex.Message} Ist der Haupt-PC an, die App dort offen und „Im Netz freigeben“ eingeschaltet?";
        }
    }
}
