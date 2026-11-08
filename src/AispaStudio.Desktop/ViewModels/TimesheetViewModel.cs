using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zelle in den Zeiten: Soll und Ist eines Tages einer Person mit Hinweisen; ein Klick öffnet den Tag.</summary>
public sealed partial class TimesheetCellViewModel : ObservableObject
{
    public TimesheetCellViewModel(TimesheetRowViewModel row, TimesheetDay day, Action<TimesheetRowViewModel, TimesheetCellViewModel> open)
    {
        Row = row;
        Day = day;
        Warnings = day.Warnings;
        HasRecords = day.Records.Count > 0;
        IsRunning = day.Records.Any(r => r.End is null);
        HasHours = day.PlannedHours > 0 || HasRecords;
        HoursText = $"{TimeDayEditorViewModel.Hours(day.PlannedHours)} / {(HasRecords ? TimeDayEditorViewModel.Hours(day.ActualHours) : "–")}{(IsRunning ? " ▶" : "")}";
        OpenCommand = new RelayCommand(() => open(row, this));
        AutomationName = $"{row.Name}, {DayLayout.ShortDay(day.Date)}: "
            + (HasHours
                ? $"Soll {TimeDayEditorViewModel.Hours(day.PlannedHours)} Stunden, Ist {(HasRecords ? TimeDayEditorViewModel.Hours(day.ActualHours) + " Stunden" : "keine Zeiten")}{(IsRunning ? ", läuft" : "")}"
                : "keine Zeiten")
            + string.Concat(Warnings.Select(w => $". Warnung: {w}"));
    }

    public TimesheetRowViewModel Row { get; }

    public TimesheetDay Day { get; }

    public IReadOnlyList<string> Warnings { get; }

    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>„Hinweis“ bzw. „2 Hinweise“: die Zelle bleibt schmal, der Wortlaut steht im Tooltip und bei den Zeiten des Tages.</summary>
    public string WarningHint => Warnings.Count == 1 ? "Hinweis" : $"{Warnings.Count} Hinweise";

    public string WarningTip => string.Join('\n', Warnings);

    /// <summary>Es gibt Zeiten für den Tag (fett), sonst gilt nur der Dienstplan (grau).</summary>
    public bool HasRecords { get; }

    public bool IsRunning { get; }

    public bool HasHours { get; }

    public bool HasNoHours => !HasHours;

    /// <summary>„8 / 8,5“, ohne Zeiten „8 / –“, ein laufender Abschnitt trägt „▶“.</summary>
    public string HoursText { get; }

    public string AutomationName { get; }

    public IRelayCommand OpenCommand { get; }

    /// <summary>Der Tag ist im Editor offen.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }
}

/// <summary>Eine Zeile in den Zeiten: eine Person mit sieben Tagen und der Wochensumme „Soll / Ist“.</summary>
public sealed class TimesheetRowViewModel
{
    public TimesheetRowViewModel(TimesheetRow row, bool showsName, bool canOpenPerson, Action<Guid> openPerson)
    {
        ShowsName = showsName;
        Id = row.EmployeeId;
        Name = row.DisplayName;
        Color = row.Color;
        HoursText = $"{TimeDayEditorViewModel.Hours(row.PlannedHours)} / {TimeDayEditorViewModel.Hours(row.ActualHours)} h";
        Warnings = row.Warnings;
        CanOpenPerson = canOpenPerson;
        OpenPersonCommand = new RelayCommand(() => openPerson(row.EmployeeId), () => canOpenPerson);
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>Farbe der Person, ein Hex-Wert; die Oberfläche bindet ihn an den Punkt vor dem Namen.</summary>
    public string Color { get; }

    public string HoursText { get; }

    public IReadOnlyList<string> Warnings { get; }

    public IReadOnlyList<TimesheetCellViewModel> Cells { get; set; } = [];

    /// <summary>Im Überblick ist der Name ein Knopf, der die Personenkarte im Reiter „Zeiten“ öffnet (wenn die Person Personen sehen darf), sonst nur Text.</summary>
    public bool CanOpenPerson { get; }

    /// <summary>Auf der Karte einer Person steht ihr Name schon darüber.</summary>
    public bool ShowsName { get; }

    public bool IsPlainName => !CanOpenPerson;

    public string OpenPersonName => $"Karte von {Name} öffnen";

    public IRelayCommand OpenPersonCommand { get; }
}

/// <summary>
/// Zeiterfassung einer Woche (R05 Slice 99, Ä191, Ä250): Standort, Wochenwahl, eine Zeile je Person mit sieben Zellen „Soll / Ist“ und Hinweisen nach dem Arbeitszeitrecht und der geöffnete Tag
/// (<see cref="TimeDayEditorViewModel"/>). Ohne <c>employeeId</c> ist es die Übersicht für alle (im Bereich Team), mit ihr der Reiter „Zeiten“ einer Person (die API liefert dann nur sie). Die
/// Woche beginnt am Montag in der Zeitzone des gewählten Standorts. Sehen darf, wer Standorte verwaltet oder Personalunterlagen liest; ändern nur mit ManageLocation. Die App zeichnet nur, Regeln und
/// Hinweise kommen von der API. Vorbild: <c>web/src/time/TimesheetPage.tsx</c>.
/// </summary>
public sealed partial class TimesheetViewModel : ViewModelBase, IEditableArea
{
    private readonly TeamContext _context;
    private readonly Guid? _employeeId;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private int _version;
    private bool _loaded;
    private bool _reverting;

    public TimesheetViewModel(TeamContext context, Guid? employeeId, Guid? preferredLocation = null)
    {
        _context = context;
        _employeeId = employeeId;
        Locations = [.. context.Data.Locations.Select(l => new Choice(l.Id, l.Name))];
        _reverting = true;
        SelectedLocation = Locations.FirstOrDefault(l => l.Id == preferredLocation) ?? (Locations.Count > 0 ? Locations[0] : null);
        _reverting = false;
        WeekStart = MondayOf(TodayAt(SelectedLocation?.Id));
    }

    /// <summary>Name der Maske in der Rückfrage.</summary>
    public string Title => "Zeiten";

    /// <summary>Zeiten nachtragen und ändern darf nur, wer Standorte verwaltet; das Backoffice liest.</summary>
    public bool IsEditable => _context.CanPlan;

    /// <summary>Auf der Personenkarte: nur diese Person, ohne Namen.</summary>
    public bool IsCard => _employeeId is not null;

    public bool ShowsNames => !IsCard;

    public IReadOnlyList<Choice> Locations { get; }

    [ObservableProperty]
    public partial Choice? SelectedLocation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekTitle))]
    public partial DateOnly WeekStart { get; private set; }

    public string WeekTitle => $"Zeiten ab {DayLayout.ShortDay(WeekStart)}";

    /// <summary>„Soll / Ist in Stunden · Hinweise nach Österreich (AZG, ARG)“; vor dem Laden ohne Land.</summary>
    [ObservableProperty]
    public partial string Legend { get; private set; } = "Soll / Ist in Stunden";

    [ObservableProperty]
    public partial IReadOnlyList<string> DayHeads { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<TimesheetRowViewModel> Rows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    public bool IsEmpty => IsLoaded && Rows.Count == 0 && !HasLoadError;

    public string EmptyText => SelectedLocation is null ? "Es gibt noch keinen Standort." : "An diesem Standort ist niemand eingeteilt und niemand hat gestempelt.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(IsEmpty))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Der geöffnete Tag; ein Klick auf eine andere Zelle ersetzt ihn (nach der Rückfrage bei ungespeicherten Änderungen).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDay), nameof(HasUnsavedChanges))]
    public partial TimeDayEditorViewModel? Day { get; private set; }

    public bool HasDay => Day is not null;

    public bool HasUnsavedChanges => Day?.HasUnsavedChanges == true;

    /// <summary>Im Überblick: ein Klick auf einen Namen soll die Karte der Person öffnen.</summary>
    public event Action<Guid>? PersonRequested;

    /// <summary>Eine Zeit wurde geändert; die andere Ansicht derselben Zeiten lädt dann beim nächsten Öffnen neu.</summary>
    public event Action? Changed;

    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    /// <summary>Etwas anderes hat Soll oder Ist geändert: beim nächsten Öffnen neu laden. Ein offener Tag ohne Änderungen zeigt einen alten Stand und schließt; Eingaben bleiben.</summary>
    public void MarkStale()
    {
        _loaded = false;
        if (!HasUnsavedChanges)
        {
            Day = null;
        }
    }

    private DateOnly TodayAt(Guid? locationId) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_context.Now, _context.ZoneAt(locationId)).DateTime);

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    [RelayCommand]
    public async Task LoadAsync()
    {
        _loaded = true;
        LoadError = null;
        if (SelectedLocation?.Id is not { } locationId)
        {
            Rows = [];
            IsLoaded = true;
            return;
        }

        var version = ++_version;
        var path = $"api/locations/{locationId}/timesheet?from={WeekStart:yyyy-MM-dd}" + (_employeeId is { } one ? $"&employeeId={one}" : "");
        var result = await _context.Api.GetAsync<Timesheet>(path).ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (result.Value is not { } sheet)
        {
            LoadError = result.Error ?? "Die Zeiten wurden nicht geladen.";
            Rows = [];
            IsLoaded = true;
            return;
        }

        Build(sheet);
        IsLoaded = true;
    }

    private void Build(Timesheet sheet)
    {
        DayHeads = [.. sheet.Dates.Select(DayLayout.ShortDay)];
        Legend = $"Soll / Ist in Stunden · Hinweise nach {CountryText(sheet.Country)}" + (IsEditable ? " · Zelle anklicken zum Nachtragen" : "");
        var canOpenPerson = !IsCard && _context.CanPeople;
        Rows =
        [
            .. sheet.Employees.Select(row =>
            {
                var vm = new TimesheetRowViewModel(row, ShowsNames, canOpenPerson, id => PersonRequested?.Invoke(id));
                vm.Cells = [.. row.Days.Select(day => new TimesheetCellViewModel(vm, day, OpenDay) { IsOpen = IsOpenDay(row.EmployeeId, day.Date) })];
                return vm;
            }),
        ];
        if (Day is { } open && sheet.Employees.FirstOrDefault(e => e.EmployeeId == open.EmployeeId)?.Days.FirstOrDefault(d => d.Date == open.Date) is { } fresh)
        {
            open.Refresh(fresh);
        }
    }

    private bool IsOpenDay(Guid employeeId, DateOnly date) => Day is { } day && day.EmployeeId == employeeId && day.Date == date;

    private static string CountryText(string country) => country switch
    {
        "AT" => "Österreich (AZG, ARG)",
        "DE" => "Deutschland (ArbZG)",
        "CH" => "Schweiz (ArG)",
        _ => country,
    };

    // ---------- Woche und Standort ----------

    [RelayCommand]
    private Task PreviousWeekAsync() => MoveToAsync(WeekStart.AddDays(-7));

    [RelayCommand]
    private Task NextWeekAsync() => MoveToAsync(WeekStart.AddDays(7));

    [RelayCommand]
    private Task ThisWeekAsync() => MoveToAsync(MondayOf(TodayAt(SelectedLocation?.Id)));

    /// <summary>Wechselt die Woche; ein offener Tag mit Änderungen fragt vorher, denn der Editor gehört zur alten Woche.</summary>
    public async Task MoveToAsync(DateOnly monday)
    {
        if (monday == WeekStart || !await LeaveDayAsync().ConfigureAwait(true))
        {
            return;
        }

        Day = null;
        WeekStart = monday;
        await LoadAsync().ConfigureAwait(true);
    }

    partial void OnSelectedLocationChanged(Choice? oldValue, Choice? newValue)
    {
        if (_reverting)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: die Auswahl bleibt beim alten Standort, bis die Antwort da ist.
            ReturnTo(oldValue);
            _ = ChangeLocationAsync(newValue);
            return;
        }

        Day = null;
        _ = LoadAsync();
    }

    private async Task ChangeLocationAsync(Choice? target)
    {
        if (!await LeaveDayAsync().ConfigureAwait(true))
        {
            return;
        }

        Day = null;
        _reverting = true;
        SelectedLocation = target;
        _reverting = false;
        await LoadAsync().ConfigureAwait(true);
    }

    private void ReturnTo(Choice? location)
    {
        void Return()
        {
            _reverting = true;
            SelectedLocation = location;
            _reverting = false;
        }

        if (_ui is null)
        {
            Return();
        }
        else
        {
            _ui.Post(_ => Return(), null);
        }
    }

    // ---------- Tag öffnen ----------

    private void OpenDay(TimesheetRowViewModel row, TimesheetCellViewModel cell) => _ = OpenDayAsync(row, cell);

    /// <summary>Öffnet einen Tag; ein anderer offener Tag mit Änderungen fragt vorher „Änderungen speichern?“.</summary>
    public async Task OpenDayAsync(TimesheetRowViewModel row, TimesheetCellViewModel cell)
    {
        if (SelectedLocation?.Id is not { } locationId)
        {
            return;
        }

        if (IsOpenDay(row.Id, cell.Day.Date) || !await LeaveDayAsync().ConfigureAwait(true))
        {
            return;
        }

        var editor = new TimeDayEditorViewModel(_context, locationId, Locations, _context.ZoneAt(locationId), row.Id, row.Name, cell.Day, ReloadAfterSaveAsync, () => _ = CloseDayAsync())
        {
            Dialogs = Dialogs,
        };
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimeDayEditorViewModel.HasUnsavedChanges))
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        };
        Day = editor;
        MarkOpen();
    }

    private void MarkOpen()
    {
        foreach (var row in Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsOpen = IsOpenDay(row.Id, cell.Day.Date);
            }
        }
    }

    private Task<bool> LeaveDayAsync() => Day is { } day ? UnsavedChanges.ConfirmLeaveAsync(Dialogs, day) : Task.FromResult(true);

    private async Task CloseDayAsync()
    {
        if (await LeaveDayAsync().ConfigureAwait(true))
        {
            Day = null;
            MarkOpen();
        }
    }

    /// <summary>Nach dem Speichern oder Entfernen: die Zeiten neu laden, der Tag bleibt offen und zeigt den neuen Stand.</summary>
    private async Task ReloadAfterSaveAsync()
    {
        await LoadAsync().ConfigureAwait(true);
        MarkOpen();
        Changed?.Invoke();
    }

    // ---------- IEditableArea ----------

    public async Task<bool> SaveChangesAsync() => Day is not { HasUnsavedChanges: true } day || await day.SaveChangesAsync().ConfigureAwait(true);

    public void Discard() => Day?.Discard();
}
