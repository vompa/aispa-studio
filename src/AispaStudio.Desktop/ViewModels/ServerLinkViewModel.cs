using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Seite „Netz“ im Server-Betrieb (ADR 0084, Punkt 5): zeigt, womit die App verbunden ist, und löst einen eingetragenen Server oder Haupt-PC
/// wieder. Die Umgebungsvariable <c>AISPA_API_URL</c> hat Vorrang und lässt sich hier nicht lösen.
/// </summary>
/// <param name="current">Die beim Start gewählte Verbindung.</param>
/// <param name="restart">Startet die App neu, damit sie ohne den Eintrag arbeitet.</param>
/// <param name="forgetSignIn">Widerruft den lokalen Zugang zum bisherigen Server.</param>
public partial class ServerLinkViewModel(ISettingsStore settings, ServerChoice.Choice current, Action restart, Action? forgetSignIn = null) : ViewModelBase
{
    public string Text => current.Source switch
    {
        ServerSource.Server => $"Die App ist mit dem Server {current.Address} verbunden.",
        ServerSource.MainPc => $"Die App ist mit dem Haupt-PC {current.Address} im Netz verbunden.",
        ServerSource.Environment => $"Die App ist über die Umgebungsvariable AISPA_API_URL mit {current.Address} verbunden. Sie hat Vorrang vor einem eingetragenen Server und lässt sich hier nicht lösen.",
        _ => $"Es ist kein Server eingetragen. Die App sucht ihn unter {current.Address}.",
    };

    private bool CanRelease => current.Source is ServerSource.Server or ServerSource.MainPc;

    [RelayCommand(CanExecute = nameof(CanRelease))]
    private void Release() => ConnectionSettings.Change(settings, null, null, forgetSignIn, restart);
}

internal static class ConnectionSettings
{
    // Beide Startkarten und die Seite „Netz“ wechseln dasselbe Ziel im selben lokalen Profil.
    internal static void Change(ISettingsStore settings, Uri? address, string? code, Action? forgetSignIn, Action restart, Action<string>? showError = null)
    {
        var previous = settings.Load();
        var updated = previous with { ServerAddress = address?.ToString(), ServerCode = code };
        if (ServerChoice.Resolve(previous, null) != ServerChoice.Resolve(updated, null))
        {
            try
            {
                forgetSignIn?.Invoke();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                showError?.Invoke("Die bisherige Anmeldung konnte nicht vollständig entfernt werden. Die Serververbindung wurde nicht geändert.");
                throw;
            }
        }

        settings.Save(updated);
        restart();
    }
}
