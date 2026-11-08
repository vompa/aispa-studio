using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Seite „Netz“ im Server-Betrieb (ADR 0084, Punkt 5): zeigt, womit die App verbunden ist, und löst einen eingetragenen Server oder Haupt-PC
/// wieder. Die Umgebungsvariable <c>AISPA_API_URL</c> hat Vorrang und lässt sich hier nicht lösen.
/// </summary>
/// <param name="current">Die beim Start gewählte Verbindung.</param>
/// <param name="restart">Startet die App neu, damit sie ohne den Eintrag arbeitet.</param>
public partial class ServerLinkViewModel(ISettingsStore settings, ServerChoice.Choice current, Action restart) : ViewModelBase
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
    private void Release()
    {
        settings.Save(settings.Load() with { ServerAddress = null, ServerCode = null });
        restart();
    }
}
