using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Startauswahl auf einem PC ohne Einrichtung (ADR 0104): bringt keine eigene Logik, sondern führt zu den vorhandenen Ansichten. „Neues Spa“ und
/// „Aus einer Sicherung wiederherstellen“ schalten auf den Einzelplatz und starten neu, dann erscheint „Spa einrichten“; das Beispiel-Spa startet
/// wie <c>--demo</c> und merkt sich nichts; „Mit einem vorhandenen Spa verbinden“ zeigt die Karten für Netzwerk und Server.
/// </summary>
/// <param name="restart">Startet die App neu, mit <c>--demo</c> für das Beispiel-Spa.</param>
public partial class StartChoiceViewModel(ISettingsStore settings, Action<bool> restart) : ViewModelBase
{
    /// <summary>Die Auswahl steht auf dem Bildschirm.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; }

    /// <summary>Die Person hat „Mit einem vorhandenen Spa verbinden“ gewählt: „Erneut versuchen“ zeigt die Auswahl nicht wieder.</summary>
    public bool IsDeclined { get; private set; }

    [RelayCommand]
    private void SetUp() => ChooseStandalone();

    [RelayCommand]
    private void Restore() => ChooseStandalone();

    [RelayCommand]
    private void ShowDemo() => restart(true);

    [RelayCommand]
    private void ConnectToExisting()
    {
        IsDeclined = true;
        IsShown = false;
    }

    /// <summary>Ein Server hat geantwortet: dieser PC hat ein Spa, die Startauswahl erscheint nicht mehr.</summary>
    public void MarkMade()
    {
        var current = settings.Load();
        if (!current.StartChoiceMade)
        {
            settings.Save(current with { StartChoiceMade = true });
        }
    }

    private void ChooseStandalone()
    {
        settings.Save(settings.Load() with { Standalone = true, StartChoiceMade = true });
        restart(false);
    }
}
