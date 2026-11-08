using System.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Die rechte Seitenleiste des Kalenders als ein Muster (Fluent 2): ein Kopf für alle Leisten (<see cref="ActivePanel"/>), dieselbe Breite,
/// nie zwei Leisten zugleich, kurze Bewegung beim Öffnen (200 ms, abbremsend) und schnelleres Schließen (120 ms), bei Kurzbefehlen ohne Bewegung.
/// </summary>
public partial class CalendarViewModel
{
    private PanelPresence? _presence;

    /// <summary>Die offene Leiste; ihr Titel und ihr Schließen-Kreuz stehen im gemeinsamen Kopf.</summary>
    public ISidePanel? ActivePanel => (ISidePanel?)Selected ?? NewEntry?.Content as ISidePanel ?? WaitlistPanel ?? (ISidePanel?)GapsPanel ?? (ISidePanel?)FreeTime ?? (ISidePanel?)Absence ?? (ISidePanel?)Request ?? (ISidePanel?)RoomCarePanel ?? (ISidePanel?)MessageDetail ?? (ISidePanel?)CompanionPanel ?? MessagesPanel;

    /// <summary>Wann die Leiste im Layout steht und ob sie sich bewegt (Maus) oder nicht (Kurzbefehl).</summary>
    public PanelPresence Presence => _presence ??= new PanelPresence(_time);

    /// <summary>Wird gesetzt, wenn eine Leiste zum Eingeben aufgeht (nicht beim reinen Ansehen eines Termins); die Ansicht setzt den Fokus ins erste Feld.</summary>
    public event Action? PanelOpened;

    /// <summary>Eine Seite bittet die Ansicht, ein Element in der Seitenleiste zu fokussieren (Name des Elements), z. B. das Notizfeld.</summary>
    public event Action<string>? PanelFocusRequested;

    private void RequestPanelFocus(string name) => PanelFocusRequested?.Invoke(name);

    /// <summary>Schließt die offene Leiste samt allen Reitern.</summary>
    public void CloseAllPanels()
    {
        if (HasPanel)
        {
            DropAllPanels();
            Rebuild();
        }
    }

    /// <summary>Wechselt die Umschaltleiste die Art, ändert sich der Inhalt und mit ihm Titel und Untertitel im Kopf.</summary>
    partial void OnNewEntryChanged(NewEntryViewModel? oldValue, NewEntryViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnNewEntryContentChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnNewEntryContentChanged;
        }

        WatchEntryInput();
    }

    private void OnNewEntryContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NewEntryViewModel.Content))
        {
            OnPropertyChanged(nameof(ActivePanel));
            WatchEntryInput();
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(Selected) or nameof(NewEntry) or nameof(WaitlistPanel) or nameof(GapsPanel) or nameof(FreeTime) or nameof(Absence) or nameof(Request) or nameof(RoomCarePanel) or nameof(MessagesPanel) or nameof(MessageDetail) or nameof(CompanionPanel):
                OnPropertyChanged(nameof(ActivePanel));
                OnPropertyChanged(nameof(PanelWidth));
                break;
            case nameof(PanelWidth):
                // Die Reiter unten teilen sich die Breite der Seitenleiste (Ä19).
                RefreshVisibleTabs();
                break;
            case nameof(ActivePanel):
                SyncTabs();
                break;
            case nameof(ByRoom):
                Selected?.RefreshTimeBar();
                break;
            case nameof(HasPanel):
                UpdatePanelVisible();
                break;
        }
    }

    private void UpdatePanelVisible()
    {
        Presence.Update(HasPanel);
        if (HasPanel && Selected is null)
        {
            PanelOpened?.Invoke();
        }
    }
}
