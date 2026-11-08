using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// „KI und Hilfe“ als Reiter der Seitenleiste (Design-Brief 5.28, Ä66): der Assistent ist eine Seite wie die Warteliste oder die Meldungen, hat sein Symbol in der
/// Reiterschiene, und das Schließen lässt die übrigen Reiter stehen. Der Knopf „KI und Hilfe“ und Strg+Leertaste wählen diesen Reiter und öffnen dafür die Seitenleiste.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Der Assistent, solange sein Reiter gewählt ist; sonst <c>null</c> (auch wenn der Reiter im Hintergrund offen ist).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial CompanionViewModel? CompanionPanel { get; private set; }

    /// <summary>Wählt den Reiter des Assistenten; ist er noch nicht offen, entsteht er, und die bisherige Seite bleibt als Reiter stehen.</summary>
    public void ShowAssistant(CompanionViewModel assistant)
    {
        if (CompanionPanel is not null)
        {
            return;
        }

        if (OpenPanels.FirstOrDefault(t => t.Kind == PanelKind.Assistant) is { } tab && Restore(tab))
        {
            return;
        }

        ClosePanels();
        CompanionPanel = assistant;
        Rebuild();
    }

    /// <summary>Schließt den Reiter des Assistenten, wenn er gewählt ist; die übrigen Reiter bleiben, der nächste wird gewählt oder die Leiste geht zu.</summary>
    public void HideAssistant()
    {
        if (CompanionPanel is null)
        {
            return;
        }

        CloseActivePanel();
        Reselect();
    }
}
