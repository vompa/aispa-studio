using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Dienst im Tageseditor: Standort, Beginn und Ende, wie die Arbeitszeiten in den Stammdaten (<see cref="WorkingHoursRow"/>).</summary>
public sealed partial class ShiftRow : ObservableObject
{
    private const string OtherLocation = "Anderer Standort";

    private readonly Action _changed;

    public ShiftRow(int number, IReadOnlyList<Choice> locations, Guid? locationId, TimeOnly? start, TimeOnly? end, Action changed, Action<ShiftRow> remove)
    {
        // Ein Standort, den die angemeldete Person nicht sieht, bleibt mit seiner Id stehen; die API behält solche Dienste selbst.
        var known = locations.FirstOrDefault(l => l.Id == locationId);
        Locations = known is null && locationId is not null ? [.. locations, new Choice(locationId, OtherLocation)] : locations;
        Location = known ?? (locationId is null ? (Locations.Count > 0 ? Locations[0] : null) : Locations[^1]);
        Start = start;
        End = end;
        Number = number;
        _changed = changed;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public IReadOnlyList<Choice> Locations { get; }

    public IRelayCommand RemoveCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocationName), nameof(StartName), nameof(EndName), nameof(RemoveName))]
    public partial int Number { get; set; }

    [ObservableProperty]
    public partial Choice? Location { get; set; }

    [ObservableProperty]
    public partial TimeOnly? Start { get; set; }

    [ObservableProperty]
    public partial TimeOnly? End { get; set; }

    /// <summary>Fehler an dieser Zeile („Beginn muss vor Ende liegen.“), bis sie geändert wird.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFieldError))]
    public partial string? FieldError { get; set; }

    public bool HasFieldError => FieldError is not null;

    public string LocationName => $"Dienst {Number} Standort";

    public string StartName => $"Dienst {Number} Beginn";

    public string EndName => $"Dienst {Number} Ende";

    public string RemoveName => $"Dienst {Number} entfernen";

    public string StateText => $"{Location?.Id}|{Start}|{End}";

    partial void OnLocationChanged(Choice? value) => Edited();

    partial void OnStartChanged(TimeOnly? value) => Edited();

    partial void OnEndChanged(TimeOnly? value) => Edited();

    private void Edited()
    {
        FieldError = null;
        _changed?.Invoke();
    }

    public RosterEntry ToEntry() => new(RosterKind.Work, Location?.Id, Start, End);
}

/// <summary>Eine Art im Tageseditor („Dienst“, „Urlaub“ …) in der segmentierten Leiste.</summary>
public sealed partial class RosterKindOption(RosterKind kind, string label, Action<RosterKind> select) : ObservableObject
{
    public RosterKind Kind => kind;

    public string Label => label;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [RelayCommand]
    private void Select() => select(kind);
}

/// <summary>
/// Ein Tag im Dienstplan planen (R05 Slice 99, Ä191): Art (Dienst, Urlaub, Krank, Frei, Schulung), Dienste mit Standort, Beginn und Ende, Notiz, „Sofort gültig,
/// ohne Veröffentlichen“ (Krankenstand gilt immer sofort) und „Wie Wochenarbeitszeit“. Nach dem Speichern nennt die Maske die Warnungen der API und die Zahl der Termine,
/// die jetzt außerhalb des Dienstes liegen (Zahl aus <see cref="RosterDayResult.AppointmentsOutsideShift"/>, die App rechnet nichts); „Ersatz vorschlagen“ öffnet den Ausfall-Assistenten.
/// Vorbild: <c>DayEditor</c> in <c>web/src/roster/RosterPage.tsx</c>.
/// </summary>
public sealed partial class RosterDayEditorViewModel : ViewModelBase, IEditableArea
{
    public const string DraftSaved = "Als Entwurf gespeichert. Er gilt, sobald der Dienstplan veröffentlicht ist.";

    public const string Saved = "Gespeichert. Kalender und Buchung richten sich ab sofort danach.";

    private const int MaxShifts = 6;

    private readonly TeamContext _context;
    private readonly Guid _locationId;
    private readonly IReadOnlyList<Choice> _locations;
    private readonly Guid _employeeId;
    private readonly string _employeeName;
    private readonly DateOnly _date;
    private readonly Func<Task> _saved;
    private readonly Action _close;
    private readonly TimeZoneInfo _zone;
    private bool _seeding;
    private bool _immediateChosen;
    private string _savedState = "";

    public RosterDayEditorViewModel(TeamContext context, Guid locationId, IReadOnlyList<Choice> locations, Guid employeeId, string employeeName, RosterCell cell,
        bool inCard, Func<Task> saved, Action close)
    {
        _context = context;
        _locationId = locationId;
        _locations = locations;
        _employeeId = employeeId;
        _employeeName = employeeName;
        _date = cell.Date;
        InCard = inCard;
        _saved = saved;
        _close = close;
        _zone = context.ZoneAt(locationId);
        Cell = cell;
        Kinds =
        [
            new(RosterKind.Work, "Dienst", Choose), new(RosterKind.Vacation, "Urlaub", Choose), new(RosterKind.Sick, "Krank", Choose),
            new(RosterKind.DayOff, "Frei", Choose), new(RosterKind.Training, "Schulung", Choose),
        ];
        Seed(cell);
        _savedState = State();
    }

    /// <summary>Der Tag, wie ihn der Plan zuletzt geliefert hat (nach dem Speichern der neue Stand).</summary>
    public RosterCell Cell { get; private set; }

    /// <summary>Der Editor steht auf der Personenkarte: Entwürfe veröffentlicht dort niemand, die Maske verweist auf den Wochenplan.</summary>
    public bool InCard { get; }

    public IReadOnlyList<RosterKindOption> Kinds { get; }

    public ObservableCollection<ShiftRow> Shifts { get; } = [];

    /// <summary>Überschrift und Name in der Rückfrage: „Lena Huber, Di. 3.11.“.</summary>
    public string Heading => $"{_employeeName}, {DayLayout.ShortDay(_date)}";

    public string Title => Heading;

    public Guid EmployeeId => _employeeId;

    public DateOnly Date => _date;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsShifts), nameof(IsImmediate), nameof(CanChooseImmediate), nameof(ShowsSickHint), nameof(HasUnsavedChanges))]
    public partial RosterKind Kind { get; private set; } = RosterKind.Work;

    public bool ShowsShifts => Kind == RosterKind.Work;

    public bool HasNoShifts => ShowsShifts && Shifts.Count == 0;

    public bool CanAddShift => Shifts.Count < MaxShifts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    public partial string Note { get; set; } = "";

    /// <summary>Sofort gültig statt Entwurf; Krankenstand gilt immer sofort und lässt sich nicht abwählen.</summary>
    public bool IsImmediate
    {
        get => _immediateChosen || Kind == RosterKind.Sick;
        set
        {
            if (Kind == RosterKind.Sick || _immediateChosen == value)
            {
                return;
            }

            _immediateChosen = value;
            OnPropertyChanged();
            Changed();
        }
    }

    public bool CanChooseImmediate => Kind != RosterKind.Sick;

    public bool ShowsSickHint => Kind == RosterKind.Sick;

    /// <summary>„Wie Wochenarbeitszeit“ gibt es, wo ein Plan oder Entwurf für den Tag steht.</summary>
    public bool CanReset => Cell.Source == RosterSource.Plan || Cell.Draft;

    /// <summary>Status nach dem Speichern („Gespeichert …“ oder „Als Entwurf gespeichert …“), neben der Leiste.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Sonderregelung der Person (R05 Slice 111, E10): Die API warnt, gespeichert wird erst nach „Trotzdem speichern“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRuleWarning))]
    public partial string? RuleWarning { get; private set; }

    public bool HasRuleWarning => RuleWarning is not null;

    /// <summary>Warnungen der API zum gespeicherten Tag (Arbeits- und Ruhezeiten), eine pro Zeile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    public partial string? WarningsText { get; private set; }

    public bool HasWarnings => WarningsText is not null;

    /// <summary>„3 Termine liegen jetzt außerhalb des Dienstes. Bitte im Kalender verschieben:“ samt Liste; leer, wenn kein Termin betroffen ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutside), nameof(CanSuggestReplacement))]
    public partial string? OutsideText { get; private set; }

    public bool HasOutside => OutsideText is not null;

    /// <summary>Die Zahl der Termine außerhalb des Dienstes nach dem Speichern.</summary>
    [ObservableProperty]
    public partial int OutsideCount { get; private set; }

    public bool CanSuggestReplacement => HasOutside && _context.CanBook && Assistant is null;

    /// <summary>Nach dem Speichern als Entwurf auf der Karte: der Entwurf gilt erst nach dem Veröffentlichen im Wochenplan.</summary>
    [ObservableProperty]
    public partial bool ShowsOpenWeekPlan { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSuggestReplacement), nameof(HasAssistant))]
    public partial AbsenceAssistantViewModel? Assistant { get; private set; }

    public bool HasAssistant => Assistant is not null;

    public bool HasUnsavedChanges => State() != _savedState;

    /// <summary>Der Editor will, dass der Wochenplan geöffnet wird (Entwurf auf der Karte gespeichert).</summary>
    public event Action? WeekPlanRequested;

    /// <summary>Legt Art, Dienste und Notiz nach dem Tag an (beim Öffnen und nach „Wie Wochenarbeitszeit“).</summary>
    private void Seed(RosterCell cell)
    {
        _seeding = true;
        var work = cell.Entries.Where(e => e.Kind == RosterKind.Work).ToList();
        Kind = cell.Entries.Count > 0 && work.Count == 0 ? cell.Entries[0].Kind : RosterKind.Work;
        foreach (var each in Kinds)
        {
            each.IsOn = each.Kind == Kind;
        }

        Shifts.Clear();
        foreach (var entry in work)
        {
            Shifts.Add(NewShift(entry.LocationId, entry.Start ?? new TimeOnly(9, 0), entry.End ?? new TimeOnly(17, 0)));
        }

        Note = cell.Note ?? "";
        _seeding = false;
        RaiseShifts();
    }

    private ShiftRow NewShift(Guid? locationId, TimeOnly start, TimeOnly end) =>
        new(Shifts.Count + 1, _locations, locationId, start, end, Changed, Remove);

    private void Choose(RosterKind kind)
    {
        if (kind == Kind)
        {
            return;
        }

        Kind = kind;
        foreach (var each in Kinds)
        {
            each.IsOn = each.Kind == kind;
        }

        RaiseShifts();
        OnPropertyChanged(nameof(IsImmediate));
        Changed();
    }

    private void Remove(ShiftRow row)
    {
        Shifts.Remove(row);
        for (var i = 0; i < Shifts.Count; i++)
        {
            Shifts[i].Number = i + 1;
        }

        RaiseShifts();
        Changed();
    }

    [RelayCommand]
    private void AddShift()
    {
        if (!CanAddShift)
        {
            return;
        }

        var start = Shifts.Count > 0 && Shifts[^1].End is { } end ? end : new TimeOnly(9, 0);
        Shifts.Add(NewShift(_locationId, start, new TimeOnly(17, 0)));
        RaiseShifts();
        Changed();
    }

    private void RaiseShifts()
    {
        OnPropertyChanged(nameof(HasNoShifts));
        OnPropertyChanged(nameof(CanAddShift));
    }

    private void Changed()
    {
        if (_seeding)
        {
            return;
        }

        StatusText = null;
        RuleWarning = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnNoteChanged(string value) => Changed();

    private string State() =>
        $"{Kind}|{Note.Trim()}|{_immediateChosen}|{(Kind == RosterKind.Work ? string.Join(';', Shifts.Select(s => s.StateText)) : "")}";

    /// <summary>Beginn und Ende je Dienst müssen da sein und Beginn vor Ende liegen; die Meldung steht an der Zeile.</summary>
    private bool Check()
    {
        var ok = true;
        foreach (var shift in Kind == RosterKind.Work ? Shifts.ToList() : new List<ShiftRow>())
        {
            shift.FieldError = shift.Location?.Id is null ? "Bitte einen Standort wählen."
                : shift.Start is null || shift.End is null ? "Bitte Beginn und Ende angeben."
                : shift.Start >= shift.End ? "Beginn muss vor Ende liegen."
                : null;
            ok &= shift.FieldError is null;
        }

        return ok;
    }

    /// <summary>Speichern wurde abgelehnt; die Ansicht setzt den Fokus in das falsche Feld dieses Dienstes (masken.md, R8).</summary>
    public event Action<ShiftRow>? InvalidField;

    public Task<bool> SaveChangesAsync() => SaveDayAsync(rulesConfirmed: false);

    [RelayCommand]
    private async Task SaveAnywayAsync() => await SaveDayAsync(rulesConfirmed: true).ConfigureAwait(true);

    private async Task<bool> SaveDayAsync(bool rulesConfirmed)
    {
        Error = null;
        RuleWarning = null;
        if (!Check())
        {
            Error = "Der Tag wurde nicht gespeichert. Bitte die markierten Felder prüfen.";
            InvalidField?.Invoke(Shifts.First(s => s.FieldError is not null));
            return false;
        }

        var entries = Kind == RosterKind.Work ? Shifts.Select(s => s.ToEntry()).ToList() : new List<RosterEntry> { new(Kind, null, null, null) };
        var input = new RosterDayInput(entries, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(), Draft: !IsImmediate, RulesConfirmed: rulesConfirmed);
        return await FinishAsync(await _context.Api.PutAsync<RosterDayResult>(Path(), input).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    /// <summary>„Wie Wochenarbeitszeit“: der Tag fällt auf die regelmäßige Arbeitszeit zurück (als Entwurf oder sofort wie beim Speichern).</summary>
    [RelayCommand]
    private async Task ResetAsync()
    {
        Error = null;
        RuleWarning = null;
        var result = await _context.Api.DeleteAsync<RosterDayResult>($"{Path()}?draft={(IsImmediate ? "false" : "true")}").ConfigureAwait(true);
        if (await FinishAsync(result).ConfigureAwait(true))
        {
            Seed(Cell);
            _savedState = State();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private string Path() => $"api/locations/{_locationId}/roster/{_employeeId}/{_date:yyyy-MM-dd}";

    private async Task<bool> FinishAsync(ApiResult<RosterDayResult> result)
    {
        if (result.Rejection is { StaffRule: true } rule)
        {
            RuleWarning = rule.Message;
            return false;
        }

        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Der Tag wurde nicht gespeichert.";
            return false;
        }

        Cell = saved.Day;
        OnPropertyChanged(nameof(CanReset));
        _savedState = State();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        StatusText = saved.Day.Draft ? DraftSaved : Saved;
        ShowsOpenWeekPlan = saved.Day.Draft && InCard;
        WarningsText = saved.Day.Warnings.Count == 0 ? null : string.Join('\n', saved.Day.Warnings);
        OutsideCount = saved.AppointmentsOutsideShift.Count;
        OutsideText = OutsideCount == 0 ? null : OutsideMessage(saved.AppointmentsOutsideShift);
        Assistant = null;
        await _saved().ConfigureAwait(true);
        return true;
    }

    private string OutsideMessage(IReadOnlyList<AppointmentDto> appointments)
    {
        var head = appointments.Count == 1
            ? "Ein Termin liegt jetzt außerhalb des Dienstes. Bitte im Kalender verschieben:"
            : $"{appointments.Count} Termine liegen jetzt außerhalb des Dienstes. Bitte im Kalender verschieben:";
        return head + string.Concat(appointments.Select(a => $"\n{DayLayout.Local(a.Start, _zone):HH:mm} {a.CustomerName} · {a.TreatmentName}"));
    }

    public void Discard()
    {
        Seed(Cell);
        _immediateChosen = false;
        OnPropertyChanged(nameof(IsImmediate));
        foreach (var shift in Shifts)
        {
            shift.FieldError = null;
        }

        Error = null;
        RuleWarning = null;
        StatusText = null;
        _savedState = State();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private void DiscardChanges() => Discard();

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void Close() => _close();

    [RelayCommand]
    private void OpenWeekPlan() => WeekPlanRequested?.Invoke();

    /// <summary>„Ersatz vorschlagen“: der Ausfall-Assistent für die Termine dieser Person an diesem Tag.</summary>
    [RelayCommand]
    private async Task SuggestReplacementAsync()
    {
        if (!CanSuggestReplacement)
        {
            return;
        }

        var assistant = new AbsenceAssistantViewModel(_context.Api, _locationId, _employeeId, _employeeName, _date, _zone,
            _ => _saved(), () => Assistant = null)
        {
            Dialogs = Dialogs,
        };
        Assistant = assistant;
        await assistant.LoadAsync().ConfigureAwait(true);
    }
}
