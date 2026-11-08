using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Reiter am unteren Rand der Seitenleiste: eine offene Seite mit Symbol und kurzem Text.</summary>
public sealed partial class SidePanelTab : ViewModelBase
{
    public SidePanelTab(ISidePanel panel, object? owner)
    {
        Kind = panel.PanelKind;
        Owner = owner;
        // Der Setter hängt den Reiter an die Seite an (OnPanelChanged).
        Panel = panel;
    }

    public PanelKind Kind { get; }

    /// <summary>Das ViewModel, das der Kalender für diese Seite hält (z. B. das Eingabefenster mit seiner Umschaltleiste); zum Wiederherstellen.</summary>
    public object? Owner { get; set; }

    /// <summary>Die Seite, die der Reiter zeigt; wechselt, wenn der Kalender sie neu aufbaut (z. B. nach dem Laden).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(Icon), nameof(Tip), nameof(IsInProgress), nameof(Name))]
    public partial ISidePanel Panel { get; set; }

    partial void OnPanelChanged(ISidePanel oldValue, ISidePanel newValue) => Watch(oldValue, newValue);

    /// <summary>Text und Kennzeichen folgen der Seite, z. B. dem gewählten Gast in „Neuer Termin“ (Ä149).</summary>
    private void Watch(ISidePanel? before, ISidePanel now)
    {
        if (before is INotifyPropertyChanged old)
        {
            old.PropertyChanged -= OnPanelPropertyChanged;
        }

        if (now is INotifyPropertyChanged watched)
        {
            watched.PropertyChanged += OnPanelPropertyChanged;
        }
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ISidePanel.PanelTabText) or nameof(ISidePanel.PanelInProgress))
        {
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Tip));
            OnPropertyChanged(nameof(IsInProgress));
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Text => Panel.PanelTabText;

    public string Icon => Panel.PanelIcon;

    /// <summary>Tooltip: der Titel der Seite; in Bearbeitung der volle Text mit Kennzeichen, weil der Reiter ihn kürzen kann.</summary>
    public string Tip => IsInProgress ? Name : Panel.PanelTitle;

    /// <summary>Punkt „in Bearbeitung“ am Symbol (Ä149).</summary>
    public bool IsInProgress => Panel.PanelInProgress;

    /// <summary>Name für den Screenreader: der Text, in Bearbeitung mit dem Zusatz.</summary>
    public string Name => IsInProgress ? $"{Text}, in Bearbeitung" : Text;

    /// <summary>Die Seite ist gerade offen.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Die Seitenleiste des Kalenders hält bis zu sechs offene Seiten als Reiter (Design-Brief 5.12, Punkt 7). Eine neue Seite ersetzt die Anzeige und lässt die
/// bisherige als Reiter stehen; je Art von Seite gibt es einen Reiter (<see cref="PanelKind"/>). Der zuletzt geöffnete steht ganz links, sichtbar sind mit Text höchstens
/// zwei (der gewählte und der davor benutzte), nur mit Symbol bis zu fünf (Ä29); die übrigen erreicht man über die Klappliste „⋯ n“ und die Pfeile (Ä15). Das Kreuz und Esc schließen nur die
/// gewählte Seite, danach ist der nächste Reiter gewählt oder die Leiste zu. Die einzelnen Eigenschaften des Kalenders (Selected, WaitlistPanel …) halten weiter die angezeigte Seite.
/// </summary>
public partial class CalendarViewModel
{
    /// <summary>Höchstens so viele Reiter; der siebte ersetzt den ältesten nicht gewählten.</summary>
    public const int MaxPanelTabs = 6;

    /// <summary>Die Art der Seite, die gerade angezeigt wird.</summary>
    private PanelKind? _shown;

    /// <summary>Reiter mit Text: höchstens so viele stehen in der Schiene; die übrigen offenen Seiten stehen in der Klappliste.</summary>
    public const int MaxVisibleTabs = 2;

    /// <summary>
    /// Reiter, die nur ihr Symbol zeigen (<see cref="TabsCompact"/>), brauchen wenig Platz: bis zu fünf stehen zugleich in der Schiene, erst die sechste offene Seite
    /// bringt den Chip „⋯ n“ und die Pfeile (Ä29). 5 × 32 + 4 × 2 und der Chip mit den Pfeilen (112) passen in die 291 px der Schiene.
    /// </summary>
    public const int MaxIconTabs = 5;

    /// <summary>Die offenen Seiten, die zuletzt geöffnete zuerst; ein Klick auf einen Reiter ändert die Reihenfolge nicht.</summary>
    public ObservableCollection<SidePanelTab> OpenPanels { get; } = [];

    /// <summary>Die Reiter in der Schiene: der gewählte und der davor benutzte, in der Reihenfolge von <see cref="OpenPanels"/>.</summary>
    public ObservableCollection<SidePanelTab> VisibleTabs { get; } = [];

    /// <summary>So viele Reiter zeigt die Schiene höchstens: zwei mit Text, fünf nur mit Symbol.</summary>
    public int VisibleTabLimit => TabsCompact ? MaxIconTabs : MaxVisibleTabs;

    /// <summary>Es sind mehr Seiten offen, als die Schiene zeigt: der Chip „⋯ n“ und die Pfeile erscheinen.</summary>
    public bool HasTabOverflow => OpenPanels.Count > VisibleTabLimit;

    /// <summary>Platz in der Schiene unten: Breite der Seitenleiste ohne den Rahmen links (1) und Innenabstand (8 links, 60 rechts für „KI und Hilfe“).</summary>
    private double RailWidth => PanelWidth - 1 - 8 - 60;

    /// <summary>Breite des Chips „⋯ n“ und der beiden Pfeile samt Abständen, solange mehr als zwei Seiten offen sind.</summary>
    public const double TabOverflowWidth = 112;

    /// <summary>
    /// Unter dieser Breite je Reiter zeigt er nur sein Symbol (Tooltip und Name nennen die Seite, der Chip „⋯ n“ zählt die übrigen); darüber steht der Text, bei Platzmangel gekürzt
    /// mit „…“ (Design-Brief 5.12, Punkt 7, Ä19). Fluent nennt keine Breite; 96 lässt Symbol, Innenabstand und etwa sechs Zeichen.
    /// </summary>
    public const double MinTabWidth = 96;

    /// <summary>Platz für einen der höchstens zwei Reiter mit Text; ab der dritten Seite steht der Chip mit den Pfeilen in der Schiene.</summary>
    public double TabWidth => (RailWidth - (OpenPanels.Count > MaxVisibleTabs ? TabOverflowWidth : 0) - 2) / 2;

    /// <summary>Die Reiter zeigen nur das Symbol.</summary>
    public bool TabsCompact => TabWidth < MinTabWidth;

    /// <summary>Höchstbreite des Reitertexts: der Reiter ohne Innenabstand (8 + 8), Symbol (16) und Abstand (6).</summary>
    public double TabTextMaxWidth => Math.Max(0, TabWidth - 16 - 16 - 6);

    /// <summary>Chip in der Schiene: „⋯“ und die Zahl der Seiten, die nicht als Reiter sichtbar sind.</summary>
    public string TabOverflowText => $"⋯ {OpenPanels.Count - VisibleTabs.Count}";

    /// <summary>Die Art der Seite, die vor der gewählten angezeigt war; ihr Reiter bleibt neben dem gewählten sichtbar.</summary>
    private PanelKind? _previousKind;

    /// <summary>Ein Reiter wird angeklickt (nicht neu geöffnet): die Reihenfolge bleibt.</summary>
    private bool _restoring;

    private void RefreshVisibleTabs()
    {
        var selected = OpenPanels.FirstOrDefault(t => t.IsSelected);
        var previous = OpenPanels.FirstOrDefault(t => t != selected && t.Kind == _previousKind)
            ?? OpenPanels.FirstOrDefault(t => t != selected);
        // Der gewählte und der davor benutzte Reiter stehen immer in der Schiene, den Rest füllen die zuletzt geöffneten auf.
        var limit = VisibleTabLimit;
        var chosen = new HashSet<SidePanelTab>(new[] { selected, previous }.OfType<SidePanelTab>());
        foreach (var tab in OpenPanels.TakeWhile(_ => chosen.Count < limit))
        {
            chosen.Add(tab);
        }

        var shown = OpenPanels.Where(chosen.Contains).Take(limit).ToList();
        if (!VisibleTabs.SequenceEqual(shown))
        {
            VisibleTabs.Clear();
            foreach (var tab in shown)
            {
                VisibleTabs.Add(tab);
            }
        }

        OnPropertyChanged(nameof(HasTabOverflow));
        OnPropertyChanged(nameof(VisibleTabLimit));
        OnPropertyChanged(nameof(TabOverflowText));
        OnPropertyChanged(nameof(TabWidth));
        OnPropertyChanged(nameof(TabsCompact));
        OnPropertyChanged(nameof(TabTextMaxWidth));
    }

    /// <summary>
    /// Zählt der Kalender gerade alle Leisten zu, um einer neuen Platz zu machen: die bisherige bleibt als Reiter stehen,
    /// und das Verschwinden der angezeigten Seite ist kein Schließen.
    /// </summary>
    private bool _makingRoom;

    /// <summary>Gleicht die Reiter mit der angezeigten Seite ab: neue Seite = neuer oder ersetzter Reiter, verschwundene Seite = geschlossen.</summary>
    private void SyncTabs()
    {
        if (_makingRoom)
        {
            return;
        }

        if (ActivePanel is not { } active)
        {
            CloseShownTab();
            return;
        }

        var tab = OpenPanels.FirstOrDefault(t => t.Kind == active.PanelKind);
        if (tab is null)
        {
            tab = new SidePanelTab(active, OwnerOf(active.PanelKind));
            OpenPanels.Insert(0, tab);
            while (OpenPanels.Count > MaxPanelTabs)
            {
                OpenPanels.Remove(OpenPanels.Last(t => t != tab));
            }
        }
        else
        {
            tab.Panel = active;
            tab.Owner = OwnerOf(active.PanelKind);
            if (!_restoring && OpenPanels[0] != tab)
            {
                OpenPanels.Move(OpenPanels.IndexOf(tab), 0);
            }
        }

        if (_shown is { } was && was != active.PanelKind)
        {
            _previousKind = was;
        }

        _shown = active.PanelKind;
        foreach (var other in OpenPanels)
        {
            other.IsSelected = other == tab;
        }

        RefreshVisibleTabs();
    }

    /// <summary>Die angezeigte Seite ist weg (Kreuz, Fertig): ihr Reiter fällt weg.</summary>
    private void CloseShownTab()
    {
        if (_shown is { } kind)
        {
            _shown = null;
            if (_keepNextClosed)
            {
                if (OpenPanels.FirstOrDefault(t => t.Kind == kind) is { } shown)
                {
                    OpenPanels.Remove(shown);
                }

                RefreshVisibleTabs();
                return;
            }

            RemoveTabAndShowNext(kind);
        }
    }

    /// <summary>Beim Schließen der Seite bleibt die Leiste zu, statt den Nachbar-Reiter zu zeigen (nach dem Speichern einer Eingabe).</summary>
    private bool _keepNextClosed;

    private void CloseWithoutNextTab(Action close)
    {
        _keepNextClosed = true;
        try
        {
            close();
        }
        finally
        {
            _keepNextClosed = false;
        }
    }

    /// <summary>Der Reiter der geschlossenen Seite fällt weg; der Reiter an derselben Stelle (sonst der letzte) wird gewählt.</summary>
    private void RemoveTabAndShowNext(PanelKind kind)
    {
        var index = OpenPanels.ToList().FindIndex(t => t.Kind == kind);
        if (index >= 0)
        {
            OpenPanels.RemoveAt(index);
        }

        while (OpenPanels.Count > 0)
        {
            var next = OpenPanels[Math.Min(Math.Max(index, 0), OpenPanels.Count - 1)];
            if (Restore(next))
            {
                return;
            }

            index = Math.Min(index, OpenPanels.Count);
        }
    }

    private object? OwnerOf(PanelKind kind) => kind switch
    {
        PanelKind.Appointment => Selected,
        PanelKind.NewEntry => NewEntry,
        PanelKind.Waitlist => WaitlistPanel,
        PanelKind.Gaps => GapsPanel,
        PanelKind.FreeTime => FreeTime,
        PanelKind.Absence => Absence,
        PanelKind.Request => Request,
        PanelKind.RoomCare => RoomCarePanel,
        PanelKind.MessageDetail => MessageDetail,
        PanelKind.Assistant => CompanionPanel,
        _ => MessagesPanel,
    };

    /// <summary>Zeigt die Seite des Reiters wieder; schlägt das fehl (der Termin liegt nicht mehr an diesem Tag), fällt der Reiter weg.</summary>
    private bool Restore(SidePanelTab tab)
    {
        if (!CanRestore(tab))
        {
            OpenPanels.Remove(tab);
            return false;
        }

        _restoring = true;
        try
        {
            ShowRestored(tab);
        }
        finally
        {
            _restoring = false;
        }

        return true;
    }

    private void ShowRestored(SidePanelTab tab)
    {
        ClosePanels();
        Ghost = null;
        switch (tab.Kind)
        {
            case PanelKind.Appointment:
                OpenAppointment(_day!.Appointments.First(a => a.Id == ((AppointmentViewModel)tab.Panel).Appointment.Id));
                break;
            case PanelKind.NewEntry:
                NewEntry = (NewEntryViewModel)tab.Owner!;
                Rebuild();
                break;
            case PanelKind.Waitlist:
                OpenWaitlist();
                break;
            case PanelKind.Gaps:
                OpenGaps();
                break;
            case PanelKind.FreeTime:
                var freeTime = (FreeTimeViewModel)tab.Owner!;
                freeTime.PropertyChanged += OnFreeTimeChanged;
                FreeTime = freeTime;
                UpdateSlotMarks();
                Rebuild();
                // Inzwischen kann gebucht worden sein: die Zeiten neu holen, damit keine vergebene mehr in der Liste steht.
                _ = freeTime.LoadAsync();
                break;
            case PanelKind.Absence:
                Absence = (AbsenceAssistantViewModel)tab.Owner!;
                Rebuild();
                break;
            case PanelKind.Request:
                var request = (CalendarRequestViewModel)tab.Owner!;
                request.PropertyChanged += OnRequestChanged;
                Request = request;
                UpdateSlotMarks();
                Rebuild();
                RequestOpened?.Invoke();
                break;
            case PanelKind.RoomCare:
                RoomCarePanel = (RoomCarePanelViewModel)tab.Owner!;
                Rebuild();
                break;
            case PanelKind.MessageDetail:
                MessageDetail = (MessageDetailViewModel)tab.Owner!;
                Rebuild();
                break;
            case PanelKind.Assistant:
                CompanionPanel = (CompanionViewModel)tab.Owner!;
                Rebuild();
                break;
            default:
                MessagesPanel = (MessagesPanelViewModel)tab.Owner!;
                Rebuild();
                break;
        }
    }

    /// <summary>Der Termin wird frisch geöffnet, deshalb muss er am angezeigten Tag noch da sein.</summary>
    private bool CanRestore(SidePanelTab tab) => tab.Kind switch
    {
        PanelKind.Appointment => _day?.Appointments.Any(a => a.Id == ((AppointmentViewModel)tab.Panel).Appointment.Id) == true,
        _ => true,
    };

    /// <summary>Ein Klick auf einen Reiter zeigt seine Seite; die bisherige bleibt als Reiter stehen.</summary>
    [RelayCommand]
    private void SelectPanelTab(SidePanelTab? tab)
    {
        if (tab is not null && !tab.IsSelected && OpenPanels.Contains(tab))
        {
            Restore(tab);
        }
    }

    /// <summary>Strg+Tab (<paramref name="step"/> = 1) und Strg+Umschalt+Tab (−1) wechseln den Reiter; über das Ende hinaus beginnt es von vorn.</summary>
    public bool CyclePanelTab(int step)
    {
        if (!HasPanel)
        {
            return false;
        }

        var current = OpenPanels.ToList().FindIndex(t => t.IsSelected);
        if (OpenPanels.Count > 1)
        {
            SelectPanelTab(OpenPanels[(current + step + OpenPanels.Count) % OpenPanels.Count]);
        }

        return true;
    }

    /// <summary>
    /// Strg+W schließt die gewählte Seite (mit der Rückfrage bei Eingaben), Strg+1 bis Strg+8 wählen den n-ten Reiter in der Reihenfolge der Schiene, Strg+9 den letzten (WinUI „TabView“).
    /// Mit Vorschlägen im Wunsch bleibt Strg+1 bis Strg+3 das Buchen des Vorschlags; ohne offene Seite gibt es nichts zu wählen.
    /// </summary>
    public bool HandleTabShortcut(Key key)
    {
        if (!HasPanel)
        {
            return false;
        }

        if (key == Key.W)
        {
            _ = RequestClosePanelAsync();
            return true;
        }

        var number = key switch
        {
            >= Key.D1 and <= Key.D9 => key - Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad0,
            _ => 0,
        };
        if (number == 0 || (number <= 3 && Request is { HasProposals: true }))
        {
            return false;
        }

        var tab = number == 9 ? OpenPanels.LastOrDefault() : OpenPanels.ElementAtOrDefault(number - 1);
        if (tab is null)
        {
            return true;
        }

        SelectPanelTab(tab);
        return true;
    }

    /// <summary>Die Pfeile in der Schiene blättern durch die Reihe; am Ende beginnt es von vorn.</summary>
    [RelayCommand]
    private void NextPanelTab() => CyclePanelTab(1);

    [RelayCommand]
    private void PreviousPanelTab() => CyclePanelTab(-1);

    /// <summary>
    /// Das Kreuz in der Seitenleiste, Esc und Strg+W: schließt die gewählte Seite. Hat sie Eingaben, die verloren gingen, fragt vorher der Rückfragedialog
    /// „Änderungen verwerfen?“ mit „Verwerfen“ und „Weiter bearbeiten“ (Design-Brief 5.27); „Weiter bearbeiten“ lässt die Seite offen.
    /// </summary>
    [RelayCommand]
    public async Task RequestClosePanelAsync()
    {
        // Im Eingabefenster zählen auch die gemerkten Arten, die gerade nicht angezeigt werden (Ä153).
        if (ActivePanel is { HasUnsavedChanges: true } || (NewEntry is { HasUnsavedChanges: true } entry && ReferenceEquals(ActivePanel, entry.Content)))
        {
            var answer = await AskAsync(new ConfirmRequest("Änderungen verwerfen?", "Was Sie hier eingetragen haben, geht beim Schließen verloren.",
                "Verwerfen", CancelText: "Weiter bearbeiten")).ConfigureAwait(true);
            if (!answer.IsConfirmed)
            {
                return;
            }
        }

        CloseActivePanel();
        Reselect();
    }

    /// <summary>Schließt die gewählte Seite (Esc); der nächste Reiter wird gewählt.</summary>
    private void CloseActivePanel()
    {
        var kind = _shown;
        // Der Reiter „KI und Hilfe“ wird ausdrücklich geschlossen (nicht nur abgewählt): der Assistent beginnt beim nächsten Öffnen von vorn.
        CompanionPanel?.StartFresh();
        ClosePanels();
        if (kind is { } closed)
        {
            RemoveTabAndShowNext(closed);
        }
    }

    /// <summary>
    /// Schließt alle Seiten samt Reitern (z. B. beim Filter auf einen Gast). „KI und Hilfe“ bleibt: der Assistent hat diesen Filter oft selbst ausgelöst,
    /// und er ist nicht Teil der Arbeit im Kalender.
    /// </summary>
    private void DropAllPanels()
    {
        if (CompanionPanel is null)
        {
            ClosePanels();
            _shown = null;
        }

        foreach (var tab in OpenPanels.Where(t => t.Kind != PanelKind.Assistant).ToList())
        {
            OpenPanels.Remove(tab);
        }

        _previousKind = null;
        RefreshVisibleTabs();
    }
}
