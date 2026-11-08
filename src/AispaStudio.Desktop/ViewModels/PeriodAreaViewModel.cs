using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Reiter der Liste: aktuelle (aktiv und geplant) und beendete Einträge.</summary>
public enum PeriodTab
{
    Current,
    Ended,
}

/// <summary>Eine Zeile der Liste: Art (bei Raumsperren mit Raum), Zeitraum und eine Nebenzeile (offene Termine, Grund oder Text).</summary>
public sealed record PeriodRow(Guid Id, PeriodTab Tab, string Title, string Period, string Detail)
{
    /// <summary>Alles in einer Zeile für Screenreader; sonst läse die Liste den Typnamen.</summary>
    public string Name => $"{Title}, {Period}, {Detail}";
}

/// <summary>Eine Art in der Auswahl (Optionsfelder): ein Wert des Aufzählungstyps der Maske, seine Beschriftung und ob er gerade gewählt ist.</summary>
public sealed class KindOption(object value, string label, Func<object> current, Action<object> select) : ObservableObject
{
    public object Value { get; } = value;

    public string Label { get; } = label;

    /// <summary>Setzen auf wahr wählt die Art; ein Optionsfeld wird nur durch die Wahl eines anderen abgewählt.</summary>
    public bool IsSelected
    {
        get => Equals(current(), Value);
        set
        {
            if (value)
            {
                select(Value);
            }
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsSelected));
}

/// <summary>
/// Gemeinsame Grundlage der Masken „Schließzeiten“ und „Raumsperren“ (R05 Slice 95, <c>ClosuresPage.tsx</c> und <c>RoomClosuresPage.tsx</c>): Standort wählen, Liste mit den Reitern
/// „Aktuell“ und „Beendet“, daneben das Formular des gewählten Eintrags oder eines neuen (Zeitraum in <see cref="Period"/>, Art und Texte in der Maske), Speichern mit Strg+S, Verwerfen und
/// Löschen mit Rückfrage. Wer mit ungespeicherten Änderungen die Zeile, den Standort, den Reiter oder die Seite wechselt, wird gefragt (<see cref="UnsavedChanges"/>). Prüfen und Speichern
/// macht die API; vorab prüft die Maske die Regeln des Servers, damit der Fehler am Feld steht. Sprungziele aus Kalender und Assistent (<see cref="Open(AdminTarget)"/>) wählen Standort und legen bei
/// einem Zeitraum oder Raum ein neues Formular an.
/// </summary>
public abstract partial class PeriodAreaViewModel : ViewModelBase, IEditableArea, ITargetedArea
{
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly TimeProvider _time;
    private readonly Dictionary<Guid, LocationDto> _locations = [];
    private List<PeriodRow> _all = [];
    private AdminTarget? _pending;
    private AdminTarget? _newTarget;
    private Task? _loading;
    private string? _savedSnapshot;
    private bool _applying;
    private bool _reverting;
    private bool _switching;

    protected PeriodAreaViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time, string title, string firstDayMissing, string lastDayMissing)
    {
        Api = api;
        User = user;
        _time = time;
        Title = title;
        Period = new PeriodFormViewModel(firstDayMissing, lastDayMissing, () => Today);
        Period.Changed += Touch;
    }

    public string Title { get; }

    /// <summary>Von, Bis und die Uhrzeiten eines Teil-Tages.</summary>
    public PeriodFormViewModel Period { get; }

    /// <summary>Die Standorte; die Auswahl erscheint nur bei mehreren.</summary>
    public ObservableCollection<LocationChoice> Locations { get; } = [];

    /// <summary>Die Einträge des Reiters.</summary>
    public ObservableCollection<PeriodRow> Rows { get; } = [];

    /// <summary>Die Maske wurde gespeichert, gelöscht oder hat Termine abgesagt; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    public bool HasSeveralLocations => Locations.Count > 1;

    public bool HasNoLocation => IsLoaded && Locations.Count == 0;

    public bool IsCurrentTab => Tab == PeriodTab.Current;

    public bool IsEndedTab => Tab == PeriodTab.Ended;

    public string CurrentTabLabel => Label("Aktuell", PeriodTab.Current);

    public string EndedTabLabel => Label("Beendet", PeriodTab.Ended);

    /// <summary>Name der Liste für Screenreader: „Schließzeiten: Aktuell“.</summary>
    public string ListLabel => $"{Title}: {(IsEndedTab ? "Beendet" : "Aktuell")}";

    public string EmptyText => IsEndedTab ? EmptyEndedText : EmptyCurrentText;

    public bool ShowEmpty => IsLoaded && Rows.Count == 0;

    public bool HasForm => IsCreating || CurrentId is not null;

    /// <summary>Weder ein Eintrag noch ein neues Formular: rechts steht der Hinweis, links zu wählen.</summary>
    public bool ShowSelectHint => IsLoaded && !HasForm;

    public bool CanDelete => HasForm && !IsCreating && CanDeleteItem;

    public bool HasPhase => PhaseText.Length > 0;

    public bool HasOpenNotice => OpenNotice is not null;

    public bool HasCreatedText => CreatedText is not null;

    public bool HasMessage => Message is not null;

    public bool HasError => Error is not null;

    public bool HasLoadError => LoadError is not null;

    public bool HasUnsavedChanges => IsLoaded && HasForm && _savedSnapshot is not null && Snapshot() != _savedSnapshot;

    // ---------- Texte der Maske ----------

    /// <summary>„Neue Schließzeit“: Beschriftung des Knopfes und Überschrift des neuen Formulars.</summary>
    public abstract string NewText { get; }

    /// <summary>Satz neben den Reitern, was die Sperre bewirkt.</summary>
    public abstract string HintText { get; }

    public abstract string HelpTopic { get; }

    public abstract string HelpSection { get; }

    /// <summary>Hinweis, wenn nichts gewählt ist.</summary>
    public abstract string SelectHint { get; }

    public abstract string LoadFailedText { get; }

    public abstract string NoLocationText { get; }

    protected abstract string EmptyCurrentText { get; }

    protected abstract string EmptyEndedText { get; }

    protected abstract string SaveFailedText { get; }

    // ---------- Zustand ----------

    [ObservableProperty]
    public partial LocationChoice? SelectedLocation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoLocation), nameof(ShowEmpty), nameof(ShowSelectHint))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrentTab), nameof(IsEndedTab), nameof(EmptyText), nameof(ListLabel))]
    public partial PeriodTab Tab { get; private set; }

    [ObservableProperty]
    public partial PeriodRow? SelectedRow { get; set; }

    /// <summary>Ein neues Formular ist offen.</summary>
    [ObservableProperty]
    public partial bool IsCreating { get; private set; }

    /// <summary>Der Eintrag, dessen Formular offen ist; leer bei einem neuen.</summary>
    [ObservableProperty]
    public partial Guid? CurrentId { get; private set; }

    [ObservableProperty]
    public partial string HeadingText { get; private set; } = "";

    /// <summary>„Geplant“, „Aktiv“ oder „Beendet“; leer bei einem neuen Formular.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhase))]
    public partial string PhaseText { get; private set; } = "";

    /// <summary>Warnung über Termine, die in der Sperre liegen und gebucht bleiben.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenNotice))]
    public partial string? OpenNotice { get; private set; }

    /// <summary>„Angelegt von Katrin am 01.03.2027 08:00.“</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreatedText))]
    public partial string? CreatedText { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    /// <summary>Fehler über der Leiste: die Zusammenfassung nach einer Prüfung oder die Meldung der API.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>Eine Rückmeldung zu einer Aktion (Gelöscht, Termine abgesagt) oder ein Fehler dabei, über der Liste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; private set; } = InfoBarSeverity.Success;

    /// <summary>Das Feld, das den Fokus bekommen soll (Feldkennung); die Ansicht setzt ihn und leert die Angabe.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    // ---------- Ableitungen der Masken ----------

    protected IWorkstationApi Api { get; }

    protected CurrentUser User { get; }

    protected LocationDto? CurrentLocation => SelectedLocation?.Id is { } id && _locations.TryGetValue(id, out var location) ? location : null;

    protected TimeZoneInfo Zone => CurrentLocation is { } location ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone) : _time.LocalTimeZone;

    /// <summary>Der heutige Tag am Standort.</summary>
    protected DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), Zone).DateTime);

    protected DateTimeOffset UtcNow => _time.GetUtcNow();

    protected virtual bool CanDeleteItem => true;

    protected virtual bool CanCreateNew => true;

    /// <summary>Das erste Feld des Formulars, das bei einem neuen den Fokus bekommt.</summary>
    protected virtual string FirstField => PeriodFormViewModel.FromDateField;

    protected abstract Task<string?> FetchAsync(Guid locationId);

    protected abstract IReadOnlyList<PeriodRow> BuildRows();

    protected abstract void ApplyItem(Guid id);

    protected abstract void ApplyBlank(AdminTarget? target);

    /// <summary>Was die Maske über den Zeitraum hinaus eingibt, für die Frage „geändert?“.</summary>
    protected abstract string FormSignature();

    /// <summary>Prüft die übrigen Felder; liefert die Kennung des ersten fehlerhaften Felds oder nichts.</summary>
    protected abstract string? ValidateFields();

    protected abstract Task<(Guid? Id, PeriodTab Tab, string? Error)> SendAsync(Guid locationId, Guid? id, PeriodValue period);

    protected abstract Task<string?> DeleteItemAsync(Guid locationId, Guid id);

    protected abstract ConfirmRequest DeleteRequest(Guid id);

    protected virtual Task LoadExtraAsync(IReadOnlyList<LocationDto> locations) => Task.CompletedTask;

    protected virtual Task OnLocationReadyAsync(Guid locationId) => Task.CompletedTask;

    protected virtual Task OnItemShownAsync(Guid id) => Task.CompletedTask;

    protected virtual void FormStateChanged()
    {
    }

    /// <summary>Der Standort, zu dem ein Sprungziel gehört; bei Raumsperren der des Raums.</summary>
    protected virtual Guid? LocationFor(AdminTarget target) => target.Location;

    /// <summary>Das Sprungziel legt ein neues Formular an (Zeitraum vom Assistenten, Raum vom Kalender).</summary>
    protected virtual bool OpensNew(AdminTarget target) => target.From is not null || target.Until is not null;

    protected static PeriodTab TabOf(ClosurePhase phase) => phase == ClosurePhase.Ended ? PeriodTab.Ended : PeriodTab.Current;

    protected static string PhaseName(ClosurePhase phase) => phase switch
    {
        ClosurePhase.Planned => "Geplant",
        ClosurePhase.Active => "Aktiv",
        _ => "Beendet",
    };

    /// <summary>„01.03.2027 08:00“ in der Zeitzone des Standorts.</summary>
    protected string Stamp(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Zone).ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    protected static string? Trimmed(string text) => text.Trim().Length == 0 ? null : text.Trim();

    /// <summary>Überschrift, Zustand, Warnung und Angabe zum Anlegen des Formulars; die Maske setzt sie beim Anzeigen eines Eintrags.</summary>
    protected void SetInfo(string heading, string phase, string? openNotice, string? created)
    {
        HeadingText = heading;
        PhaseText = phase;
        OpenNotice = openNotice;
        CreatedText = created;
    }

    /// <summary>Eine Rückmeldung über der Liste.</summary>
    protected void Notify(string text, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        Message = text;
        MessageSeverity = severity;
    }

    /// <summary>Eine Eingabe der Maske hat sich geändert: der Stand „Gespeichert.“ gilt nicht mehr, und „Verwerfen“ wird bedienbar.</summary>
    protected void Touch()
    {
        if (_applying)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Lädt die Liste des Standorts neu und behält danach den Eintrag mit der Kennung (oder den ersten des Reiters).</summary>
    protected async Task ReloadAsync(Guid? keep = null)
    {
        await LoadItemsAsync().ConfigureAwait(true);
        var row = keep is { } id ? _all.FirstOrDefault(r => r.Id == id) : null;
        if (row is not null)
        {
            Tab = row.Tab;
        }

        RefreshRows();
        if (Rows.FirstOrDefault(r => r.Id == keep) is { } shown)
        {
            await SelectRowAsync(shown).ConfigureAwait(true);
        }
        else
        {
            await SelectFirstOrNoneAsync().ConfigureAwait(true);
        }
    }

    protected void AnnounceSaved() => Saved?.Invoke();

    /// <summary>Wendet <paramref name="fill"/> an, ohne dass es als Änderung der Person gilt, und merkt den Stand für „Verwerfen“ und die Rückfrage.</summary>
    protected void Apply(Action fill)
    {
        _applying = true;
        try
        {
            fill();
        }
        finally
        {
            _applying = false;
        }

        _savedSnapshot = Snapshot();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    // ---------- Laden ----------

    /// <summary>Lädt die Standorte und die Liste des gewählten; beim ersten Öffnen und über „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var locations = await Api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        if (locations.Value is not { } list)
        {
            LoadError = locations.Error ?? LoadFailedText;
            return;
        }

        await LoadExtraAsync(list).ConfigureAwait(true);
        _locations.Clear();
        Locations.Clear();
        foreach (var location in list)
        {
            _locations[location.Id] = location;
            Locations.Add(new LocationChoice(location.Id, location.Name));
        }

        OnPropertyChanged(nameof(HasSeveralLocations));
        IsLoaded = true;
        var wanted = _pending is { } target && LocationFor(target) is { } id ? Locations.FirstOrDefault(c => c.Id == id) : null;
        ChooseLocation(wanted ?? Locations.FirstOrDefault());
        await LoadItemsAsync().ConfigureAwait(true);
        RefreshRows();
        await SelectFirstOrNoneAsync().ConfigureAwait(true);
        NewCommand.NotifyCanExecuteChanged();
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

    /// <summary>Sprung aus Kalender oder Assistent: wählt den Standort und legt bei einem Zeitraum oder Raum ein neues Formular an; steht die Maske noch nicht, gilt das Ziel nach dem Laden.</summary>
    public void Open(AdminTarget target)
    {
        _pending = target;
        if (IsLoaded)
        {
            _ = ApplyPendingAsync();
        }
    }

    /// <summary>Der Sprung ohne Standort und Zeitraum (die Schnittstelle aller nativen Bereiche).</summary>
    public void Open(Guid? id, string? section, string? field) => Open(new AdminTarget(id, null, null, null, field));

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

        if (OpensNew(target) && await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            StartNew(target);
        }
    }

    private async Task LoadItemsAsync()
    {
        if (SelectedLocation?.Id is not { } locationId)
        {
            _all = [];
            return;
        }

        var error = await FetchAsync(locationId).ConfigureAwait(true);
        if (error is not null)
        {
            LoadError = error;
            return;
        }

        LoadError = null;
        _all = [.. BuildRows()];
        await OnLocationReadyAsync(locationId).ConfigureAwait(true);
    }

    private void RefreshRows()
    {
        _switching = true;
        try
        {
            Rows.Clear();
            foreach (var row in _all.Where(r => r.Tab == Tab))
            {
                Rows.Add(row);
            }
        }
        finally
        {
            _switching = false;
        }

        OnPropertyChanged(nameof(CurrentTabLabel));
        OnPropertyChanged(nameof(EndedTabLabel));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    private string Label(string name, PeriodTab tab) => IsLoaded ? $"{name} ({_all.Count(r => r.Tab == tab)})" : name;

    // ---------- Auswahl ----------

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
    }

    private async Task ChangeLocationAsync()
    {
        Message = null;
        await LoadItemsAsync().ConfigureAwait(true);
        RefreshRows();
        await SelectFirstOrNoneAsync().ConfigureAwait(true);
        NewCommand.NotifyCanExecuteChanged();
    }

    private async Task SelectFirstOrNoneAsync()
    {
        if (Rows.FirstOrDefault() is { } first)
        {
            await SelectRowAsync(first).ConfigureAwait(true);
        }
        else
        {
            ClearForm();
        }
    }

    private async Task SelectRowAsync(PeriodRow row)
    {
        _switching = true;
        try
        {
            SelectedRow = row;
        }
        finally
        {
            _switching = false;
        }

        await ShowRowAsync(row).ConfigureAwait(true);
    }

    private async Task ShowRowAsync(PeriodRow row)
    {
        IsCreating = false;
        CurrentId = row.Id;
        Message = null;
        ResetFeedback();
        Apply(() => ApplyItem(row.Id));
        RaiseFormState();
        await OnItemShownAsync(row.Id).ConfigureAwait(true);
    }

    private void ClearForm()
    {
        _switching = true;
        try
        {
            SelectedRow = null;
        }
        finally
        {
            _switching = false;
        }

        IsCreating = false;
        CurrentId = null;
        _savedSnapshot = null;
        ResetFeedback();
        SetInfo("", "", null, null);
        RaiseFormState();
    }

    private void StartNew(AdminTarget? target)
    {
        _newTarget = target;
        _switching = true;
        try
        {
            SelectedRow = null;
        }
        finally
        {
            _switching = false;
        }

        IsCreating = true;
        CurrentId = null;
        Message = null;
        ResetFeedback();
        SetInfo(NewText, "", null, null);
        Apply(() =>
        {
            ApplyBlank(target);
            Period.Set(target?.From ?? target?.Until ?? Today, null, target?.Until ?? target?.From ?? Today, null);
        });
        RaiseFormState();
        FocusTarget = FirstField;
    }

    private void ResetFeedback()
    {
        StatusText = null;
        Error = null;
    }

    private void RaiseFormState()
    {
        OnPropertyChanged(nameof(HasForm));
        OnPropertyChanged(nameof(ShowSelectHint));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        FormStateChanged();
    }

    partial void OnSelectedRowChanged(PeriodRow? oldValue, PeriodRow? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Liste bleibt bei der alten Zeile, bis die Antwort da ist.
            Revert(() => SelectedRow = oldValue);
            _ = LeaveToRowAsync(newValue);
            return;
        }

        _ = ShowRowAsync(newValue);
    }

    partial void OnSelectedLocationChanged(LocationChoice? oldValue, LocationChoice? newValue)
    {
        if (_reverting || _switching || newValue is null)
        {
            return;
        }

        if (oldValue is not null && HasUnsavedChanges)
        {
            Revert(() => SelectedLocation = oldValue);
            _ = LeaveToLocationAsync(newValue);
            return;
        }

        _ = ChangeLocationAsync();
    }

    private async Task LeaveToRowAsync(PeriodRow row)
    {
        if (await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            await SelectRowAsync(row).ConfigureAwait(true);
        }
    }

    private async Task LeaveToLocationAsync(LocationChoice choice)
    {
        if (await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            ChooseLocation(choice);
            await ChangeLocationAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Zurück zur alten Auswahl. Kam der Wechsel aus der Liste, übernimmt sie einen Wert, den das Modell noch während ihrer eigenen Auswahl zurücksetzt, nicht; deshalb erst danach
    /// (gepostet), dann sieht sie eine echte Änderung.
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

    private Task<bool> ConfirmLeaveAsync() => UnsavedChanges.ConfirmLeaveAsync(Dialogs, this);

    // ---------- Befehle ----------

    private bool CanNew => IsLoaded && SelectedLocation is not null && CanCreateNew;

    /// <summary>Ein neues Formular; die Wahl der Zeile fragt vorher, wenn etwas ungespeichert ist.</summary>
    [RelayCommand(CanExecute = nameof(CanNew))]
    private async Task NewAsync()
    {
        if (await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            StartNew(null);
        }
    }

    [RelayCommand]
    private Task ShowCurrentAsync() => ShowTabAsync(PeriodTab.Current);

    [RelayCommand]
    private Task ShowEndedAsync() => ShowTabAsync(PeriodTab.Ended);

    private async Task ShowTabAsync(PeriodTab tab)
    {
        if (Tab == tab || !await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            return;
        }

        Tab = tab;
        Message = null;
        RefreshRows();
        await SelectFirstOrNoneAsync().ConfigureAwait(true);
    }

    private bool CanSave => !IsSaving && IsLoaded && HasForm;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving || !IsLoaded || !HasForm || SelectedLocation?.Id is not { } locationId)
        {
            return false;
        }

        Error = null;
        var periodRead = Period.TryRead(out var period, out var periodField);
        var fieldsField = ValidateFields();
        if (!periodRead || fieldsField is not null)
        {
            Error = SaveFailedText;
            FocusTarget = periodField ?? fieldsField;
            return false;
        }

        IsSaving = true;
        var (id, tab, error) = await SendAsync(locationId, CurrentId, period).ConfigureAwait(true);
        IsSaving = false;
        if (id is not { } savedId)
        {
            Error = error ?? SaveFailedText;
            return false;
        }

        Tab = tab;
        await ReloadAsync(savedId).ConfigureAwait(true);
        StatusText = "Gespeichert.";
        Saved?.Invoke();
        return true;
    }

    /// <summary>Setzt alle Felder auf den gespeicherten Stand zurück.</summary>
    [RelayCommand]
    public void Discard()
    {
        if (CurrentId is { } id)
        {
            Apply(() => ApplyItem(id));
        }
        else if (IsCreating)
        {
            // Ein neuer Eintrag aus einem Sprung behält Raum und Zeitraum des Sprungs.
            var target = _newTarget;
            Apply(() =>
            {
                ApplyBlank(target);
                Period.Set(target?.From ?? target?.Until ?? Today, null, target?.Until ?? target?.From ?? Today, null);
            });
        }

        Error = null;
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void CloseMessage() => Message = null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (CurrentId is not { } id || SelectedLocation?.Id is not { } locationId)
        {
            return;
        }

        if (!(await AskAsync(DeleteRequest(id)).ConfigureAwait(true)).IsConfirmed)
        {
            return;
        }

        var error = await DeleteItemAsync(locationId, id).ConfigureAwait(true);
        if (error is not null)
        {
            Notify(error, InfoBarSeverity.Error);
            return;
        }

        await ReloadAsync().ConfigureAwait(true);
        Notify("Gelöscht.");
        Saved?.Invoke();
    }

    private string Snapshot() => $"{Period.Signature}§{FormSignature()}";
}
