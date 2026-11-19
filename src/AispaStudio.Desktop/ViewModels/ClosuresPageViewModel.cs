using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zeile der gemeinsamen Liste: Art (Standort oder Raum), Zeitraum, was und warum, offene Termine und Zustand.</summary>
public sealed record ClosureEntryRow(Guid Id, ClosureEntryType Type, string Title, string Period, string Reason, string AppointmentsText, string StatusText, bool IsEnded)
{
    public bool IsRoom => Type == ClosureEntryType.Room;

    /// <summary>Das Kennzeichen der Art: „Standort“ für eine Schließzeit des ganzen Standorts, „Raum“ für eine Raumsperre.</summary>
    public string TypeText => IsRoom ? "Raum" : "Standort";

    public bool HasReason => Reason.Length > 0;

    /// <summary>Alles in einer Zeile für Screenreader; sonst läse die Liste den Typnamen.</summary>
    public string Name => $"{TypeText}, {Title}, {Period}, {(HasReason ? Reason + ", " : "")}{AppointmentsText}, {StatusText}";
}

/// <summary>Eine Auswahl im Filter über der Liste („Alle 3“, „Standort geschlossen 2“, „Raum gesperrt 1“): Beschriftung mit Zahl und ob sie gilt.</summary>
public sealed partial class ClosureFilterOption(string id, string label, Action<string> select) : ObservableObject
{
    public string Id => id;

    [ObservableProperty]
    public partial string Label { get; set; } = label;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [RelayCommand]
    private void Select() => select(id);
}

/// <summary>
/// Seite „Schließzeiten“ der Verwaltung (ADR 0098, masken.md R15; Auftraggeber, 17.11.2026): Schließzeiten des ganzen Standorts und Raumsperren stehen in <b>einer</b> Liste, kommende zuerst,
/// Vergangenes eingeklappt; die Art ist ein Kennzeichen an der Zeile. Oben steht fest „Neue Schließzeit“; in der Seitenleiste wählt man „Ganzer Standort“ oder „Einzelner Raum“. Eine Zeile
/// öffnet ihr Formular in der Seitenleiste (<see cref="ClosuresViewModel"/> und <see cref="RoomClosuresViewModel"/> sind die Formulare, die Seite besitzt Standort und Liste). Die
/// Wartezeit vor der Nachricht an den Gast liegt hinter „Einrichtung“ (<see cref="ClosureSetupViewModel"/>). Rückmeldungen nach Speichern, Löschen und Absagen sind ein Toast unten
/// (5 Sekunden, die Maus darüber hält ihn an, meldungen.md M1). Wer mit ungespeicherten Änderungen die Zeile, den Standort oder die Seite wechselt, wird gefragt (<see cref="UnsavedChanges"/>).
/// Die Liste kommt aus einem Aufruf der API (<c>closure-overview</c>), Zustand und Zahl offener Termine rechnet der Server. Sprungziele aus Kalender und Assistent (<see cref="Open(AdminTarget)"/>)
/// wählen den Standort und legen bei einem Zeitraum oder Raum ein neues Formular an; das frühere Ziel <c>roomClosures</c> öffnet die Raumsperre.
/// </summary>
public sealed partial class ClosuresPageViewModel : ViewModelBase, ITargetedArea
{
    /// <summary>Die Seite (und Sprungziel „Schließzeiten“).</summary>
    public const string ClosuresSection = "closures";

    /// <summary>Früher eine eigene Maske, als Sprungziel weiter gültig: öffnet ein neues Formular „Einzelner Raum“ (mit Raum und Zeitraum des Sprungs).</summary>
    public const string RoomClosuresSection = "roomClosures";

    private const string LoadFailedText = "Die Schließzeiten konnten nicht geladen werden.";

    private readonly IWorkstationApi _api;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly Dictionary<Guid, LocationDto> _locations = [];
    private readonly Dictionary<Guid, List<RoomOption>> _rooms = [];
    private List<ClosureEntryRow> _upcomingAll = [];
    private List<ClosureEntryRow> _pastAll = [];
    private AdminTarget? _pending;
    private Task? _loading;
    private bool _switching;
    private bool _reverting;
    private bool _internal;

    public ClosuresPageViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time, ToastState? toast = null)
    {
        _api = api;
        Toast = toast ?? new ToastState(time);
        ClosureEditor = new ClosuresViewModel(api, user, time);
        RoomEditor = new RoomClosuresViewModel(api, user, time);
        Editor = ClosureEditor;
        Setup = new ClosureSetupViewModel(api);
        Setup.ToastRequested += text => Toast.Show(text);
        foreach (var editor in new PeriodAreaViewModel[] { ClosureEditor, RoomEditor })
        {
            editor.Refresh = RefreshAfterChangeAsync;
            editor.ToastRequested += text => Toast.Show(text);
            editor.Saved += () => Saved?.Invoke();
            editor.Removed += OnEditorRemoved;
            editor.PropertyChanged += (_, e) =>
            {
                if (ReferenceEquals(editor, Editor) && e.PropertyName is nameof(HasUnsavedChanges) or nameof(Title))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(Title));
                }
            };
        }

        Filters =
        [
            new ClosureFilterOption(FilterAll, "Alle", SelectFilter) { IsOn = true },
            new ClosureFilterOption(FilterLocation, "Standort geschlossen", SelectFilter),
            new ClosureFilterOption(FilterRoom, "Raum gesperrt", SelectFilter),
        ];
    }

    public const string FilterAll = "all";

    public const string FilterLocation = "location";

    public const string FilterRoom = "room";

    /// <summary>Das Formular einer Schließzeit des ganzen Standorts.</summary>
    public ClosuresViewModel ClosureEditor { get; }

    /// <summary>Das Formular einer Raumsperre.</summary>
    public RoomClosuresViewModel RoomEditor { get; }

    /// <summary>Die Einrichtung der Seite: Wartezeit vor der Nachricht an den Gast.</summary>
    public ClosureSetupViewModel Setup { get; }

    /// <summary>Ein Formular wurde gespeichert, gelöscht oder hat Termine abgesagt; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    public ObservableCollection<LocationChoice> Locations { get; } = [];

    /// <summary>Die kommenden Einträge (laufend und geplant), nach Beginn aufsteigend.</summary>
    public ObservableCollection<ClosureEntryRow> Upcoming { get; } = [];

    /// <summary>Die vergangenen Einträge, neueste zuerst; nur gefüllt, solange „Vergangene“ aufgeklappt ist.</summary>
    public ObservableCollection<ClosureEntryRow> Past { get; } = [];

    public IReadOnlyList<ClosureFilterOption> Filters { get; }

    /// <summary>Name der Maske in der Rückfrage: das Formular mit den ungespeicherten Änderungen.</summary>
    public string Title => Editor.HasUnsavedChanges ? Editor.Title : "Schließzeiten";

    public bool HasUnsavedChanges => Editor is { HasForm: true, HasUnsavedChanges: true };

    public bool HasSeveralLocations => Locations.Count > 1;

    public bool HasNoLocation => IsLoaded && Locations.Count == 0;

    public bool HasLoadError => LoadError is not null;

    public bool HasMessage => Message is not null;

    /// <summary>Es gibt Räume am Standort; nur dann lässt sich „Einzelner Raum“ wählen.</summary>
    public bool CanSelectRoomKind => RoomEditor.Rooms.Count > 0;

    /// <summary>Der Standort hat weder eine kommende noch eine vergangene Schließzeit oder Raumsperre: die Liste zeigt statt leerer Tabelle die erste Aktion.</summary>
    public bool ShowEmpty => IsLoaded && SelectedLocation is not null && UpcomingCount == 0 && PastCount == 0;

    /// <summary>Keine kommende, aber vergangene Einträge: eine ruhige Zeile statt der Aktionen.</summary>
    public bool ShowNoUpcoming => IsLoaded && SelectedLocation is not null && UpcomingCount == 0 && PastCount > 0;

    public string EmptyTitle => $"{SelectedLocation?.Title} ist durchgehend offen";

    public const string EmptyText = "Betriebsurlaub, Feiertag oder eine Raumsperre eintragen.";

    /// <summary>Der Filter lässt nichts übrig, obwohl es Einträge gibt.</summary>
    public bool ShowNoMatch => IsLoaded && Upcoming.Count == 0 && Past.Count == 0 && !ShowEmpty && !ShowNoUpcoming && Filter != FilterAll;

    public bool ShowList => IsLoaded && (UpcomingCount > 0 || PastCount > 0);

    public string PastLabel => $"Vergangene ({PastCount})";

    public bool HasPast => PastCount > 0;

    public const string HintText = "In dieser Zeit gibt es keine freien Zeiten. Bestehende Termine bleiben, bis man sie absagt oder umbucht.";

    // ---------- Zustand ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoUpcoming), nameof(ShowList), nameof(EmptyTitle))]
    public partial LocationChoice? SelectedLocation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoLocation), nameof(ShowEmpty), nameof(ShowNoUpcoming), nameof(ShowList))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoUpcoming), nameof(ShowList))]
    public partial int UpcomingCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty), nameof(ShowNoUpcoming), nameof(ShowList), nameof(PastLabel), nameof(HasPast))]
    public partial int PastCount { get; private set; }

    /// <summary>„Vergangene“ ist aufgeklappt (ADR 0098, Regel 6: Filter statt Zustandsreiter); die Liste lädt die vergangenen erst dann.</summary>
    [ObservableProperty]
    public partial bool IsPastOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoMatch))]
    public partial string Filter { get; private set; } = FilterAll;

    /// <summary>Die gewählte Zeile in der Liste der kommenden; die Liste und diese Eigenschaft folgen dem offenen Formular (<see cref="SyncSelection"/>).</summary>
    [ObservableProperty]
    public partial ClosureEntryRow? SelectedUpcoming { get; set; }

    [ObservableProperty]
    public partial ClosureEntryRow? SelectedPast { get; set; }

    /// <summary>Das Formular in der Seitenleiste: das der Schließzeit oder das der Raumsperre; bleibt nach dem Schließen stehen, damit die Leiste nicht leer aufblitzt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HasUnsavedChanges), nameof(PanelTitle))]
    public partial PeriodAreaViewModel Editor { get; private set; }

    /// <summary>Die Seitenleiste mit dem Formular ist offen.</summary>
    [ObservableProperty]
    public partial bool IsPanelOpen { get; set; }

    /// <summary>Die Seitenleiste „Einrichtung“ ist offen.</summary>
    [ObservableProperty]
    public partial bool IsSetupOpen { get; set; }

    /// <summary>Das neue Formular gilt einem einzelnen Raum statt dem ganzen Standort („Ganzer Standort“ / „Einzelner Raum“ in der Leiste).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewIsLocation))]
    public partial bool NewIsRoom { get; set; }

    /// <summary>Das Gegenstück zu <see cref="NewIsRoom"/> für das Optionsfeld „Ganzer Standort“; ein Optionsfeld wird nur durch die Wahl eines anderen abgewählt.</summary>
    public bool NewIsLocation
    {
        get => !NewIsRoom;
        set
        {
            if (value)
            {
                NewIsRoom = false;
            }
        }
    }

    /// <summary>Eine Meldung der Seite (ein Eintrag ließ sich nicht laden); sie bleibt, bis man sie schließt (meldungen.md M2).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; private set; } = InfoBarSeverity.Error;

    /// <summary>Rückmeldung unten („Schließzeit gespeichert“): der eine Toast der Shell (meldungen.md M1).</summary>
    public ToastState Toast { get; }

    /// <summary>Überschrift der Seitenleiste: die des Formulars (bei einem neuen „Neue Schließzeit“ bzw. „Neue Raumsperre“).</summary>
    public string PanelTitle => Editor.HasForm ? Editor.HeadingText : "";

    /// <summary>Beide Formulare fragen über die Dialoge des Fensters.</summary>
    public override IConfirmDialogs? Dialogs
    {
        get => base.Dialogs;
        set
        {
            base.Dialogs = value;
            ClosureEditor.Dialogs = value;
            RoomEditor.Dialogs = value;
        }
    }

    // ---------- Laden ----------

    /// <summary>Lädt die Standorte und die Liste des gewählten; beim ersten Öffnen und über „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        if (locations.Value is not { } list)
        {
            LoadError = locations.Error ?? LoadFailedText;
            return;
        }

        var resources = await Task.WhenAll(list.Select(l => _api.GetAsync<LocationResources>($"api/locations/{l.Id}/resources"))).ConfigureAwait(true);
        _rooms.Clear();
        _locations.Clear();
        Locations.Clear();
        for (var i = 0; i < list.Count; i++)
        {
            _rooms[list[i].Id] = [.. (resources[i].Value?.Rooms ?? []).Select(r => new RoomOption(r.Id, r.Name))];
            _locations[list[i].Id] = list[i];
            Locations.Add(new LocationChoice(list[i].Id, list[i].Name));
        }

        OnPropertyChanged(nameof(HasSeveralLocations));
        IsLoaded = true;
        var wanted = _pending is { } target && LocationFor(target) is { } id ? Locations.FirstOrDefault(c => c.Id == id) : null;
        ChooseLocation(wanted ?? Locations.FirstOrDefault());
        await LoadLocationDataAsync().ConfigureAwait(true);
        await ApplyPendingAsync().ConfigureAwait(true);
    }

    /// <summary>Lädt nur, wenn noch nichts geladen ist (beim Öffnen des Bereichs); ein vorgemerktes Sprungziel gilt danach.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded)
        {
            await ApplyPendingAsync().ConfigureAwait(true);
            return;
        }

        // Die Seite und ein Sprung öffnen den Bereich fast gleichzeitig: ein zweiter Aufruf wartet auf das Laden des ersten.
        var load = _loading ??= LoadAsync();
        await load.ConfigureAwait(true);
        if (ReferenceEquals(_loading, load))
        {
            _loading = null;
        }
    }

    /// <summary>Der Sprung ohne Standort und Zeitraum (die Schnittstelle aller nativen Bereiche).</summary>
    public void Open(Guid? id, string? section, string? field) => Open(new AdminTarget(id, Field: field, Section: section));

    /// <summary>Sprung aus Kalender oder Assistent: wählt den Standort und legt bei einem Zeitraum oder Raum ein neues Formular an; steht die Seite noch nicht, gilt das Ziel nach dem Laden.</summary>
    public void Open(AdminTarget target)
    {
        _pending = target;
        if (IsLoaded)
        {
            _ = ApplyPendingAsync();
        }
    }

    private bool IsRoomTarget(AdminTarget target) => target.Section == RoomClosuresSection || (target.Id is { } room && _rooms.Values.Any(rooms => rooms.Any(r => r.Id == room)));

    /// <summary>Der Standort, zu dem ein Sprungziel gehört; bei einer Raumsperre der des Raums.</summary>
    private Guid? LocationFor(AdminTarget target) =>
        target.Id is { } room && _rooms.FirstOrDefault(l => l.Value.Any(r => r.Id == room)) is { Value: not null } found ? found.Key : target.Location;

    private async Task ApplyPendingAsync()
    {
        if (_pending is not { } target || !IsLoaded)
        {
            return;
        }

        _pending = null;
        if (LocationFor(target) is { } id && Locations.FirstOrDefault(c => c.Id == id) is { } choice && choice != SelectedLocation)
        {
            if (!await ConfirmLeaveAsync().ConfigureAwait(true))
            {
                return;
            }

            ChooseLocation(choice);
            await ChangeLocationAsync().ConfigureAwait(true);
        }

        var room = IsRoomTarget(target);
        if (room && RoomEditor.Rooms.Count > 0 && (target.From is not null || target.Until is not null || target.Id is not null))
        {
            await NewAsync(ClosureEntryType.Room, target).ConfigureAwait(true);
        }
        else if (!room && (target.From is not null || target.Until is not null))
        {
            await NewAsync(ClosureEntryType.Location, target).ConfigureAwait(true);
        }
    }

    private async Task LoadLocationDataAsync()
    {
        var locationId = SelectedLocation?.Id;
        var location = locationId is { } id ? _locations.GetValueOrDefault(id) : null;
        ClosureEditor.SetLocation(location);
        RoomEditor.SetLocation(location);
        RoomEditor.SetRooms(locationId is { } rooms ? _rooms.GetValueOrDefault(rooms) ?? [] : []);
        OnPropertyChanged(nameof(CanSelectRoomKind));
        await Setup.LoadAsync(locationId).ConfigureAwait(true);
        await LoadOverviewAsync().ConfigureAwait(true);
    }

    /// <summary>Lädt die Liste des Standorts: die kommenden, mit „Vergangene“ aufgeklappt auch die vergangenen, in einem Aufruf.</summary>
    private async Task LoadOverviewAsync()
    {
        if (SelectedLocation?.Id is not { } locationId)
        {
            _upcomingAll = [];
            _pastAll = [];
            UpcomingCount = 0;
            PastCount = 0;
            ApplyFilter();
            return;
        }

        var scope = IsPastOpen ? ClosureScope.All : ClosureScope.Upcoming;
        var result = await _api.GetAsync<ClosureOverview>($"api/locations/{locationId}/closure-overview?scope={scope}").ConfigureAwait(true);
        if (result.Value is not { } overview)
        {
            LoadError = result.Error ?? LoadFailedText;
            return;
        }

        LoadError = null;
        UpcomingCount = overview.UpcomingCount;
        PastCount = overview.PastCount;
        _upcomingAll = [.. overview.Items.Where(i => i.Phase != ClosurePhase.Ended).Select(ToRow)];
        _pastAll = [.. overview.Items.Where(i => i.Phase == ClosurePhase.Ended).Select(ToRow)];
        ApplyFilter();
    }

    private static ClosureEntryRow ToRow(ClosureOverviewItem item)
    {
        var isRoom = item.Type == ClosureEntryType.Room;
        var kind = isRoom ? RoomClosuresViewModel.KindShort(Enum.TryParse<RoomClosureKind>(item.Kind, out var roomKind) ? roomKind : RoomClosureKind.Other)
            : ClosuresViewModel.KindShort(Enum.TryParse<ClosureKind>(item.Kind, out var closureKind) ? closureKind : ClosureKind.Vacation);
        var title = isRoom ? $"{item.RoomName}, {kind}" : kind;
        var appointments = item.OpenAppointments > 0 || item.OpenWaitlist > 0 ? $"{CalendarViewModel.ClosureWhat(item.OpenAppointments, item.OpenWaitlist)} offen" : "keine offenen Termine";
        var status = item.Phase switch
        {
            ClosurePhase.Active => "Läuft",
            ClosurePhase.Planned => "Geplant",
            _ => "Beendet",
        };
        return new ClosureEntryRow(item.Id, item.Type, title, PeriodFormViewModel.Label(item.FromDate, item.FromTime, item.UntilDate, item.UntilTime), item.Reason ?? "", appointments, status,
            item.Phase == ClosurePhase.Ended);
    }

    // ---------- Filter und Auswahl ----------

    private void SelectFilter(string id)
    {
        Filter = id;
        ApplyFilter();
    }

    private bool Matches(ClosureEntryRow row) => Filter switch
    {
        FilterLocation => !row.IsRoom,
        FilterRoom => row.IsRoom,
        _ => true,
    };

    private void ApplyFilter()
    {
        _switching = true;
        try
        {
            Replace(Upcoming, _upcomingAll.Where(Matches));
            Replace(Past, _pastAll.Where(Matches));
        }
        finally
        {
            _switching = false;
        }

        var all = _upcomingAll.Concat(_pastAll).ToList();
        Filters[0].Label = $"Alle {all.Count}";
        Filters[1].Label = $"Standort geschlossen {all.Count(r => !r.IsRoom)}";
        Filters[2].Label = $"Raum gesperrt {all.Count(r => r.IsRoom)}";
        foreach (var option in Filters)
        {
            option.IsOn = option.Id == Filter;
        }

        OnPropertyChanged(nameof(ShowNoMatch));
        SyncSelection();
    }

    private static void Replace(ObservableCollection<ClosureEntryRow> target, IEnumerable<ClosureEntryRow> rows)
    {
        target.Clear();
        foreach (var row in rows)
        {
            target.Add(row);
        }
    }

    /// <summary>Die Zeile, deren Formular offen ist (Art und Kennung), sonst nichts; ein neues Formular hat keine.</summary>
    private ClosureEntryRow? CurrentRow()
    {
        if (!IsPanelOpen || Editor.CurrentId is not { } id)
        {
            return null;
        }

        var isRoom = ReferenceEquals(Editor, RoomEditor);
        return Upcoming.Concat(Past).FirstOrDefault(r => r.Id == id && r.IsRoom == isRoom);
    }

    /// <summary>Stellt die Auswahl beider Listen auf das offene Formular, ohne dass es als Klick der Person gilt.</summary>
    private void SyncSelection()
    {
        var current = CurrentRow();
        _switching = true;
        try
        {
            SelectedUpcoming = current is not null && Upcoming.Contains(current) ? current : null;
            SelectedPast = current is not null && Past.Contains(current) ? current : null;
        }
        finally
        {
            _switching = false;
        }
    }

    partial void OnSelectedUpcomingChanged(ClosureEntryRow? value) => Picked(value);

    partial void OnSelectedPastChanged(ClosureEntryRow? value) => Picked(value);

    private void Picked(ClosureEntryRow? row)
    {
        if (_switching || _reverting || row is null)
        {
            return;
        }

        _ = PickAsync(row);
    }

    private async Task PickAsync(ClosureEntryRow row)
    {
        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste zeigt bis zur Antwort das offene Formular.
            Revert(SyncSelection);
            if (!await ConfirmLeaveAsync().ConfigureAwait(true))
            {
                return;
            }
        }

        await OpenRowAsync(row).ConfigureAwait(true);
    }

    /// <summary>Öffnet das Formular der Zeile in der Seitenleiste.</summary>
    public async Task OpenRowAsync(ClosureEntryRow row)
    {
        var editor = row.IsRoom ? (PeriodAreaViewModel)RoomEditor : ClosureEditor;
        var error = await editor.OpenAsync(row.Id).ConfigureAwait(true);
        if (error is not null)
        {
            Message = error;
            MessageSeverity = InfoBarSeverity.Error;
            SyncSelection();
            return;
        }

        Message = null;
        Show(editor, row.IsRoom);
    }

    private void Show(PeriodAreaViewModel editor, bool room)
    {
        var other = ReferenceEquals(editor, RoomEditor) ? (PeriodAreaViewModel)ClosureEditor : RoomEditor;
        other.Clear();
        _internal = true;
        try
        {
            Editor = editor;
            NewIsRoom = room;
            IsSetupOpen = false;
            IsPanelOpen = true;
        }
        finally
        {
            _internal = false;
        }

        OnPropertyChanged(nameof(PanelTitle));
        SyncSelection();
    }

    // ---------- Neu ----------

    private bool CanNew => IsLoaded && SelectedLocation is not null;

    /// <summary>„Neue Schließzeit“: ein neues Formular in der Seitenleiste; dort wählt man „Ganzer Standort“ oder „Einzelner Raum“.</summary>
    [RelayCommand(CanExecute = nameof(CanNew))]
    private Task NewAsync() => NewAsync(ClosureEntryType.Location, null);

    /// <summary>„Schließzeit anlegen“ im Leerzustand.</summary>
    [RelayCommand(CanExecute = nameof(CanNew))]
    private Task NewLocationAsync() => NewAsync(ClosureEntryType.Location, null);

    /// <summary>„Raum sperren“ im Leerzustand.</summary>
    [RelayCommand(CanExecute = nameof(CanNew))]
    private Task NewRoomAsync() => NewAsync(ClosureEntryType.Room, null);

    private async Task NewAsync(ClosureEntryType type, AdminTarget? target)
    {
        if (!await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            return;
        }

        var room = type == ClosureEntryType.Room && RoomEditor.Rooms.Count > 0;
        var editor = room ? (PeriodAreaViewModel)RoomEditor : ClosureEditor;
        editor.StartNew(target);
        Show(editor, room);
    }

    partial void OnNewIsRoomChanged(bool value)
    {
        if (_internal || _reverting || !Editor.IsCreating)
        {
            return;
        }

        _ = SwitchNewAsync(value);
    }

    /// <summary>Wechselt im neuen Formular zwischen „Ganzer Standort“ und „Einzelner Raum“; ungespeicherte Eingaben gehen dabei verloren, deshalb fragt es vorher.</summary>
    private async Task SwitchNewAsync(bool room)
    {
        if (room && RoomEditor.Rooms.Count == 0)
        {
            Revert(() => NewIsRoom = false);
            return;
        }

        if (HasUnsavedChanges)
        {
            Revert(() => NewIsRoom = !room);
            if (!await ConfirmLeaveAsync().ConfigureAwait(true))
            {
                return;
            }
        }

        var editor = room ? (PeriodAreaViewModel)RoomEditor : ClosureEditor;
        editor.StartNew(null);
        Show(editor, room);
    }

    // ---------- Seitenleisten ----------

    partial void OnIsPanelOpenChanged(bool value)
    {
        if (_internal || _reverting || value)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Esc und Kreuz schließen die Leiste sofort; mit ungespeicherten Änderungen fragt die Seite erst und öffnet sie bis dahin wieder.
            Revert(() => IsPanelOpen = true);
            _ = CloseAskAsync();
            return;
        }

        FinishClose();
    }

    private async Task CloseAskAsync()
    {
        if (await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            SetPanelOpen(false);
            FinishClose();
        }
    }

    private void FinishClose()
    {
        Editor.Clear();
        OnPropertyChanged(nameof(PanelTitle));
        SyncSelection();
    }

    private void SetPanelOpen(bool open)
    {
        _internal = true;
        try
        {
            IsPanelOpen = open;
        }
        finally
        {
            _internal = false;
        }
    }

    private void OnEditorRemoved()
    {
        SetPanelOpen(false);
        FinishClose();
    }

    /// <summary>„Einrichtung“: öffnet die Seitenleiste mit der Wartezeit; ein offenes Formular mit Änderungen fragt vorher.</summary>
    [RelayCommand]
    private async Task OpenSetupAsync()
    {
        if (!await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            return;
        }

        SetPanelOpen(false);
        FinishClose();
        IsSetupOpen = true;
    }

    /// <summary>Lädt die Liste nach Speichern, Löschen oder Absagen neu; das offene Formular bleibt gewählt.</summary>
    private async Task RefreshAfterChangeAsync() => await LoadOverviewAsync().ConfigureAwait(true);

    // ---------- Vergangene ----------

    /// <summary>„Vergangene (3)“: klappt die vergangenen Einträge auf und lädt sie, oder klappt sie wieder zu.</summary>
    [RelayCommand]
    private async Task TogglePastAsync()
    {
        IsPastOpen = !IsPastOpen;
        await LoadOverviewAsync().ConfigureAwait(true);
    }

    // ---------- Standort ----------

    private void ChooseLocation(LocationChoice? choice)
    {
        _switching = true;
        try
        {
            SelectedLocation = choice;
        }
        finally
        {
            _switching = false;
        }

        NewCommand.NotifyCanExecuteChanged();
        NewLocationCommand.NotifyCanExecuteChanged();
        NewRoomCommand.NotifyCanExecuteChanged();
    }

    private async Task ChangeLocationAsync()
    {
        Message = null;
        SetPanelOpen(false);
        IsSetupOpen = false;
        FinishClose();
        await LoadLocationDataAsync().ConfigureAwait(true);
    }

    partial void OnSelectedLocationChanged(LocationChoice? oldValue, LocationChoice? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        _ = ChangeLocationConfirmedAsync(oldValue, newValue);
    }

    private async Task ChangeLocationConfirmedAsync(LocationChoice? oldValue, LocationChoice newValue)
    {
        if (oldValue is not null && HasUnsavedChanges)
        {
            Revert(() => SelectedLocation = oldValue);
            if (!await ConfirmLeaveAsync().ConfigureAwait(true))
            {
                return;
            }

            ChooseLocation(newValue);
        }

        await ChangeLocationAsync().ConfigureAwait(true);
    }

    // ---------- Rückfrage, Speichern, Verwerfen ----------

    private Task<bool> ConfirmLeaveAsync() => HasUnsavedChanges ? UnsavedChanges.ConfirmLeaveAsync(Dialogs, this) : Task.FromResult(true);

    public async Task<bool> SaveChangesAsync() => Editor.HasForm && await Editor.SaveChangesAsync().ConfigureAwait(true);

    public void Discard()
    {
        if (Editor.HasForm)
        {
            Editor.Discard();
        }
    }

    /// <summary>
    /// Zurück zur alten Auswahl. Kam der Wechsel aus einer Liste oder einem Auswahlfeld, übernimmt es einen Wert nicht, den das Modell noch während seiner eigenen Änderung zurücksetzt;
    /// deshalb erst danach (gepostet), dann sieht es eine echte Änderung.
    /// </summary>
    private void Revert(Action restore)
    {
        void Restore()
        {
            _reverting = true;
            try
            {
                restore();
            }
            finally
            {
                _reverting = false;
            }
        }

        if (_ui is null)
        {
            Restore();
        }
        else
        {
            _ui.Post(_ => Restore(), null);
        }
    }

    [RelayCommand]
    private void CloseMessage() => Message = null;
}
