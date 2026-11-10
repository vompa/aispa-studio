using System.Globalization;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Zelle im Dienstplan: ein Tag einer Person mit Diensten, Entwurf und Warnungen; ein Klick öffnet den Tag zum Planen.</summary>
public sealed partial class RosterCellViewModel : ObservableObject
{
    public RosterCellViewModel(RosterRowViewModel row, RosterCell cell, IReadOnlyList<string> lines, Action<RosterRowViewModel, RosterCellViewModel> open, bool canOpen)
    {
        Row = row;
        Cell = cell;
        Lines = lines;
        Warnings = cell.Warnings;
        IsDraft = cell.Draft;
        IsPlanned = cell.Source == RosterSource.Plan;
        HasLines = lines.Count > 0;
        IsAbsence = cell.Entries.Count > 0 && cell.Entries.All(e => e.Kind != RosterKind.Work);
        Chips = ShowsChips ? [.. lines.Select(l => new RosterChipViewModel(l, row.Color, IsPlanned))] : [];
        CanOpen = canOpen;
        OpenCommand = new RelayCommand(() => open(row, this), () => canOpen);
        AutomationName = $"{row.Name}, {DayLayout.ShortDay(cell.Date)}{(IsDraft ? " (Entwurf)" : "")}: {(HasLines ? string.Join(", ", lines) : "nicht eingeteilt")}"
            + string.Concat(Warnings.Select(w => $". Warnung: {w}"));
    }

    public RosterRowViewModel Row { get; }

    public RosterCell Cell { get; }

    public IReadOnlyList<string> Lines { get; }

    public IReadOnlyList<string> Warnings { get; }

    public bool HasLines { get; }

    /// <summary>Ein freier Tag ohne Eintrag: die Zelle zeigt ein schwaches „frei“ statt eines leeren Kastens.</summary>
    public bool IsFree => !HasLines;

    /// <summary>Urlaub, Krank, Frei oder Schulung den ganzen Tag: der Balken der Zeile zeichnet ihn über die Tage, die Zelle selbst bleibt leer.</summary>
    public bool IsAbsence { get; }

    /// <summary>Dienste als Chips; Abwesenheiten zeichnet der Balken.</summary>
    public bool ShowsChips => HasLines && !IsAbsence;

    public IReadOnlyList<RosterChipViewModel> Chips { get; }

    /// <summary>Die Spalte von heute (am Standort), nur in der Woche, die heute enthält.</summary>
    public bool IsToday { get; init; }

    public bool HasWarnings => Warnings.Count > 0;

    public bool IsDraft { get; }

    /// <summary>Für diesen Tag geplant, sonst gilt die Wochenarbeitszeit.</summary>
    public bool IsPlanned { get; }

    public bool CanOpen { get; }

    public string AutomationName { get; }

    public IRelayCommand OpenCommand { get; }

    /// <summary>Der Tag ist im Editor offen.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }
}

/// <summary>Ein Dienst als Chip: Zeit und Farbe der Person; die Fläche mischt <c>RosterChip</c> je nach Farbschema, <c>IsPlanned</c> hebt Dienste hervor, die für genau diesen Tag geplant sind (sonst gilt die Wochenarbeitszeit).</summary>
public sealed record RosterChipViewModel(string Text, string PersonColor, bool IsPlanned);

/// <summary>Ein Balken über mehrere Tage einer Zeile: „Urlaub Di–Fr“ (Spalte 0 bis 6, Breite in Tagen).</summary>
public sealed record RosterBarViewModel(int Column, int Span, string Text, RosterKind Kind)
{
    public bool IsVacation => Kind == RosterKind.Vacation;

    public bool IsSick => Kind == RosterKind.Sick;

    public bool IsTraining => Kind == RosterKind.Training;
}

/// <summary>Kopf einer Tagesspalte; die Spalte von heute ist hervorgehoben.</summary>
public sealed record RosterHeadViewModel(string Text, bool IsToday);

/// <summary>Besetzung eines Tages: die Zahl der Personen im Dienst am Standort.</summary>
public sealed record RosterCoverageViewModel(string Text, bool IsToday);

/// <summary>
/// Rechnet die Fläche des Dienst-Chips: die Farbe der Person, zu einem Anteil in die Basis des Farbschemas (Token <c>RosterChipBaseBrush</c>) gemischt, Tinte <c>RosterChipInkBrush</c> (Brief 5.8).
/// Hell 22 % (gleich der früheren Aufhellung um 78 % zu Weiß), dunkel 45 %. Fällt der Kontrast zur Tinte unter 4,5:1, sinkt der Anteil in 5-Punkt-Schritten.
/// </summary>
public static class RosterChipColors
{
    public const double ShareLight = 0.22;
    public const double ShareDark = 0.45;
    private const double MinimumContrast = 4.5;
    private const double ShareStep = 0.05;

    /// <summary>Fläche als <c>#rrggbb</c>; ist eine der Farben ungültig, gilt die Basis.</summary>
    public static string Fill(string personColor, string baseColor, string inkColor, double share)
    {
        if (!TryParse(personColor, out var person) || !TryParse(baseColor, out var background) || !TryParse(inkColor, out var ink))
        {
            return baseColor;
        }

        for (var current = share; current > 0; current = Math.Round(current - ShareStep, 10))
        {
            var fill = (R: Mix(person.R, background.R, current), G: Mix(person.G, background.G, current), B: Mix(person.B, background.B, current));
            if (Contrast(fill, ink) >= MinimumContrast)
            {
                return $"#{fill.R:x2}{fill.G:x2}{fill.B:x2}";
            }
        }

        return baseColor;
    }

    private static int Mix(int person, int background, double share) => (int)Math.Round(background + ((person - background) * share));

    private static bool TryParse(string hex, out (int R, int G, int B) color)
    {
        color = default;
        if (hex.Length != 7 || hex[0] != '#' || !int.TryParse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = ((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
        return true;
    }

    private static double Contrast((int R, int G, int B) first, (int R, int G, int B) second)
    {
        var (a, b) = (Luminance(first), Luminance(second));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance((int R, int G, int B) color)
    {
        static double Linear(int channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));
    }
}

/// <summary>Eine Zeile im Dienstplan: eine Person mit sieben Tagen, den Balken für Abwesenheiten und den Stunden der Woche gegen die Wochenarbeitszeit.</summary>
public sealed class RosterRowViewModel
{
    public RosterRowViewModel(RosterRow row, bool showsName, bool canOpenPerson, Action<Guid> openPerson, double? targetHours = null)
    {
        ShowsName = showsName;
        Id = row.EmployeeId;
        Name = row.DisplayName;
        Color = row.Color;
        var german = CultureInfo.GetCultureInfo("de-AT");
        var planned = row.PlannedHours.ToString("0.##", german);
        HoursText = targetHours is > 0 ? $"{planned} / {targetHours.Value.ToString("0.##", german)} h" : $"{planned} h";
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

    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Hinweis zur Person als Symbol neben dem Namen (R05 Slice 111).</summary>
    public string? NoteText { get; init; }

    public bool HasNote => NoteText is not null;

    public IReadOnlyList<RosterCellViewModel> Cells { get; set; } = [];

    /// <summary>Abwesenheiten über aufeinanderfolgende Tage derselben Art, je ein Balken.</summary>
    public IReadOnlyList<RosterBarViewModel> Bars { get; set; } = [];

    /// <summary>Im Wochenplan ist der Name ein Knopf, der die Personenkarte öffnet (wenn die Person Personen sehen darf), sonst nur Text.</summary>
    public bool CanOpenPerson { get; }

    /// <summary>Auf der Karte einer Person steht ihr Name schon darüber.</summary>
    public bool ShowsName { get; }

    /// <summary>Breite der Spalte links: mit Namen 176, auf der Karte nur Platz für die Stunden.</summary>
    public double NameColumnWidth => RosterPlanViewModel.NameColumnWidthFor(ShowsName);

    public bool IsPlainName => !CanOpenPerson;

    public string OpenPersonName => $"Karte von {Name} öffnen";

    public IRelayCommand OpenPersonCommand { get; }
}

/// <summary>
/// Dienstplan einer Woche (R05 Slice 99, Ä191, Ä250): Standort, Wochenwahl, Legende, eine Zeile je Person mit sieben Zellen und der Stundensumme, Entwürfe veröffentlichen oder
/// verwerfen und der geöffnete Tag (<see cref="RosterDayEditorViewModel"/>). Ohne <c>employeeId</c> ist es der Wochenplan für alle (im Bereich Team), mit ihr die Karte einer Person
/// (Reiter „Dienstplan und Abwesenheit“; die API liefert dann nur diese Person, auch ohne Dienst am Standort). Die App zeichnet nur: Regeln, Warnungen und Termine außerhalb eines
/// Dienstes kommen von der API. Vorbild: <c>web/src/roster/RosterPage.tsx</c>.
/// </summary>
public sealed partial class RosterPlanViewModel : ViewModelBase, IEditableArea
{
    private readonly TeamContext _context;
    private readonly Guid? _employeeId;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private RosterWeek? _week;
    private int _version;
    private bool _loaded;
    private bool _reverting;

    public RosterPlanViewModel(TeamContext context, Guid? employeeId, Guid? preferredLocation = null)
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
    public string Title => "Dienstplan";

    /// <summary>Planen (und Entwürfe veröffentlichen) darf nur, wer Standorte verwaltet; alle anderen sehen den Plan.</summary>
    public bool IsEditable => _context.CanPlan;

    /// <summary>Auf der Personenkarte: nur diese Person, ohne Namen und ohne Veröffentlichen.</summary>
    public bool IsCard => _employeeId is not null;

    public bool ShowsNames => !IsCard;

    public double NameColumnWidth => NameColumnWidthFor(ShowsNames);

    internal static double NameColumnWidthFor(bool showsName) => showsName ? 176 : 72;

    public IReadOnlyList<Choice> Locations { get; }

    [ObservableProperty]
    public partial Choice? SelectedLocation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekTitle))]
    public partial DateOnly WeekStart { get; private set; }

    public string WeekTitle => $"Woche ab {DayLayout.ShortDay(WeekStart)}";

    [ObservableProperty]
    public partial IReadOnlyList<RosterHeadViewModel> Heads { get; private set; } = [];

    /// <summary>Zeile „Besetzung“: die Zahl der Personen im Dienst je Tag. Eine Warnung gibt es nicht, solange keine Mindestbesetzung hinterlegt werden kann.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<RosterCoverageViewModel> Coverage { get; private set; } = [];

    /// <summary>Auf der Karte einer Person gibt es nichts zu zählen.</summary>
    public bool ShowsCoverage => ShowsNames && Rows.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(ShowsCoverage))]
    public partial IReadOnlyList<RosterRowViewModel> Rows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    public bool IsEmpty => IsLoaded && Rows.Count == 0 && !HasLoadError;

    public string EmptyText => SelectedLocation is null ? "Es gibt noch keinen Standort." : "An diesem Standort ist niemand eingeteilt.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(IsEmpty))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Entwürfe dieser Woche am gewählten Standort (nur im Wochenplan veröffentlichen oder verwerfen).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPublish), nameof(DraftBandText))]
    public partial int DraftCount { get; private set; }

    public bool CanPublish => IsEditable && !IsCard && DraftCount > 0;

    /// <summary>Text des Bandes über der Woche; „Veröffentlichen“ und „Verwerfen“ stehen als Knöpfe daneben.</summary>
    public string DraftBandText => DraftCount == 1 ? "Entwurf: 1 Änderung nicht veröffentlicht" : $"Entwurf: {DraftCount} Änderungen nicht veröffentlicht";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastPublished))]
    public partial string? LastPublishedText { get; private set; }

    public bool HasLastPublished => LastPublishedText is not null;

    /// <summary>Ergebnis der Veröffentlichung: Tage, kurzfristige Änderungen und Termine außerhalb eines Dienstes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPublishMessage))]
    public partial string? PublishMessage { get; private set; }

    public bool HasPublishMessage => PublishMessage is not null;

    [ObservableProperty]
    public partial InfoBarSeverity PublishSeverity { get; private set; } = InfoBarSeverity.Success;

    /// <summary>Fehler beim Veröffentlichen oder Verwerfen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>Der geöffnete Tag; ein Klick auf eine andere Zelle ersetzt ihn (nach der Rückfrage bei ungespeicherten Änderungen).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDay), nameof(HasUnsavedChanges))]
    public partial RosterDayEditorViewModel? Day { get; private set; }

    public bool HasDay => Day is not null;

    public bool HasUnsavedChanges => Day?.HasUnsavedChanges == true;

    /// <summary>Im Wochenplan: ein Klick auf einen Namen soll die Karte der Person öffnen.</summary>
    public event Action<Guid>? PersonRequested;

    /// <summary>Auf der Karte: ein als Entwurf gespeicherter Tag verweist auf den Wochenplan zum Veröffentlichen.</summary>
    public event Action? WeekPlanRequested;

    /// <summary>Ein Tag wurde geändert; wer den Plan einbettet (Karte), lädt dann nichts neu, aber andere Ansichten können reagieren.</summary>
    public event Action? Changed;

    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    /// <summary>Eine andere Ansicht hat den Dienstplan geändert: beim nächsten Öffnen neu laden. Ein offener Tag ohne Änderungen zeigt einen alten Stand und schließt; Eingaben bleiben.</summary>
    public void MarkStale()
    {
        _loaded = false;
        if (!HasUnsavedChanges)
        {
            Day = null;
        }
    }

    /// <summary>Sprung von der Karte: zeigt deren Standort und Woche; geladen wird beim Öffnen der Ansicht.</summary>
    public void ShowWeek(Guid? locationId, DateOnly monday)
    {
        _reverting = true;
        SelectedLocation = Locations.FirstOrDefault(l => l.Id == locationId) ?? SelectedLocation;
        _reverting = false;
        WeekStart = monday;
        PublishMessage = null;
        Day = null;
        _loaded = false;
    }

    private DateOnly TodayAt(Guid? locationId) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_context.Now, _context.ZoneAt(locationId)).DateTime);

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
        var path = $"api/locations/{locationId}/roster?from={WeekStart:yyyy-MM-dd}" + (_employeeId is { } one ? $"&employeeId={one}" : "");
        var result = await _context.Api.GetAsync<RosterWeek>(path).ConfigureAwait(true);
        var symbols = await LoadNoteSymbolsAsync().ConfigureAwait(true);
        if (version != _version)
        {
            return;
        }

        if (result.Value is not { } week)
        {
            LoadError = result.Error ?? "Der Dienstplan wurde nicht geladen.";
            Rows = [];
            _week = null;
            DraftCount = 0;
            LastPublishedText = null;
            IsLoaded = true;
            return;
        }

        Build(week, locationId, symbols);
        IsLoaded = true;
    }

    private void Build(RosterWeek week, Guid locationId, Dictionary<Guid, string> symbols)
    {
        _week = week;
        var today = TodayAt(locationId);
        Heads = [.. week.Dates.Select(d => new RosterHeadViewModel(DayLayout.ShortDay(d), d == today))];
        DraftCount = week.Drafts;
        LastPublishedText = week.LastPublishedAt is { } at
            ? $"Zuletzt veröffentlicht {TimeZoneInfo.ConvertTime(at, _context.ZoneAt(locationId)):d.M.yyyy HH:mm}"
            : null;
        var canOpenPerson = !IsCard && _context.CanPeople;
        var open = Day;
        Rows =
        [
            .. week.Employees.Select(row =>
            {
                var vm = new RosterRowViewModel(row, ShowsNames, canOpenPerson, id => PersonRequested?.Invoke(id), WeeklyTargetHours(row.EmployeeId)) { NoteText = symbols.GetValueOrDefault(row.EmployeeId) };
                vm.Cells = [.. row.Days.Select(cell => new RosterCellViewModel(vm, cell, [.. cell.Entries.Select(e => EntryText(e, locationId))], OpenDay, IsEditable)
                {
                    IsOpen = open is not null && open.EmployeeId == row.EmployeeId && open.Date == cell.Date,
                    IsToday = cell.Date == today,
                })];
                vm.Bars = BarsOf(vm.Cells);
                return vm;
            }),
        ];
        Coverage = [.. week.Dates.Select((date, day) => new RosterCoverageViewModel(Rows.Count(r => r.Cells[day].Cell.Entries.Any(e => e.Kind == RosterKind.Work && e.LocationId == locationId)).ToString(CultureInfo.InvariantCulture), date == today))];
    }

    /// <summary>Soll der Woche aus der regelmäßigen Wochenarbeitszeit der Person (alle Standorte); ohne sie gibt es kein Soll.</summary>
    private double? WeeklyTargetHours(Guid employeeId) =>
        _context.Data.Employees.FirstOrDefault(e => e.Id == employeeId) is { } employee
            ? employee.WorkingHours.Sum(h => Math.Max(0, (h.End - h.Start).TotalHours))
            : null;

    /// <summary>Aufeinanderfolgende Abwesenheitstage derselben Art werden ein Balken: „Urlaub Di–Fr“, ein einzelner Tag nur „Krank“.</summary>
    private static List<RosterBarViewModel> BarsOf(IReadOnlyList<RosterCellViewModel> cells)
    {
        var bars = new List<RosterBarViewModel>();
        for (var start = 0; start < cells.Count; start++)
        {
            if (!cells[start].IsAbsence)
            {
                continue;
            }

            var kind = cells[start].Cell.Entries[0].Kind;
            var end = start;
            while (end + 1 < cells.Count && cells[end + 1].IsAbsence && cells[end + 1].Cell.Entries[0].Kind == kind)
            {
                end++;
            }

            var label = KindLabel(kind);
            var text = end == start ? label : $"{label} {WeekdayOf(cells[start].Cell.Date)}–{WeekdayOf(cells[end].Cell.Date)}";
            bars.Add(new RosterBarViewModel(start, end - start + 1, text, kind));
            start = end;
        }

        return bars;
    }

    private static string WeekdayOf(DateOnly date) => DayLayout.ShortDay(date).Split('.')[0];

    private string EntryText(RosterEntry entry, Guid locationId)
    {
        if (entry.Kind != RosterKind.Work)
        {
            return KindLabel(entry.Kind);
        }

        var times = $"{entry.Start:HH:mm}–{entry.End:HH:mm}";
        return entry.LocationId == locationId ? times : $"{times} {Locations.FirstOrDefault(l => l.Id == entry.LocationId)?.Label ?? "anderer Standort"}";
    }

    public static string KindLabel(RosterKind kind) => kind switch
    {
        RosterKind.Work => "Dienst",
        RosterKind.Vacation => "Urlaub",
        RosterKind.Sick => "Krank",
        RosterKind.DayOff => "Frei",
        _ => "Schulung",
    };

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

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
        PublishMessage = null;
        await LoadAsync().ConfigureAwait(true);
    }

    partial void OnSelectedLocationChanged(Choice? oldValue, Choice? newValue)
    {
        UpdateDocument();
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
        PublishMessage = null;
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
        PublishMessage = null;
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

    private void OpenDay(RosterRowViewModel row, RosterCellViewModel cell) => _ = OpenDayAsync(row, cell);

    /// <summary>Öffnet einen Tag zum Planen; ein anderer offener Tag mit Änderungen fragt vorher „Änderungen speichern?“.</summary>
    public async Task OpenDayAsync(RosterRowViewModel row, RosterCellViewModel cell)
    {
        if (!IsEditable || SelectedLocation?.Id is not { } locationId)
        {
            return;
        }

        if (Day is { } open && open.EmployeeId == row.Id && open.Date == cell.Cell.Date)
        {
            return;
        }

        if (!await LeaveDayAsync().ConfigureAwait(true))
        {
            return;
        }

        var editor = new RosterDayEditorViewModel(_context, locationId, Locations, row.Id, row.Name, cell.Cell, IsCard, ReloadAfterSaveAsync, () => _ = CloseDayAsync())
        {
            Dialogs = Dialogs,
        };
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RosterDayEditorViewModel.HasUnsavedChanges))
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        };
        editor.WeekPlanRequested += () => WeekPlanRequested?.Invoke();
        Day = editor;
        MarkOpen();
    }

    private void MarkOpen()
    {
        foreach (var row in Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsOpen = Day is { } day && day.EmployeeId == row.Id && day.Date == cell.Cell.Date;
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

    /// <summary>Nach dem Speichern eines Tages: den Plan neu laden, der Tag bleibt offen und zeigt sein Ergebnis.</summary>
    private async Task ReloadAfterSaveAsync()
    {
        await LoadAsync().ConfigureAwait(true);
        MarkOpen();
        Changed?.Invoke();
    }

    // ---------- Veröffentlichen und Verwerfen ----------

    [RelayCommand]
    private async Task PublishAsync()
    {
        if (_week is null || SelectedLocation?.Id is not { } locationId || !CanPublish || !await LeaveDayAsync().ConfigureAwait(true))
        {
            return;
        }

        Error = null;
        var result = await _context.Api.PostAsync<RosterPublishResult>($"api/locations/{locationId}/roster/publish", RangeOf(_week)).ConfigureAwait(true);
        if (result.Value is not { } published)
        {
            Error = result.Error ?? "Der Dienstplan wurde nicht veröffentlicht.";
            return;
        }

        var lines = new List<string>
        {
            $"{(published.Days == 1 ? "1 Tag" : $"{published.Days} Tage")} veröffentlicht; Kalender und Buchung richten sich ab sofort danach.",
        };
        lines.AddRange(published.ShortNotice.Select(w => $"Kurzfristig: {w}"));
        if (published.AppointmentsOutsideShift.Count > 0)
        {
            lines.Add($"{(published.AppointmentsOutsideShift.Count == 1 ? "Ein Termin liegt" : $"{published.AppointmentsOutsideShift.Count} Termine liegen")} jetzt außerhalb eines Dienstes. "
                + "Bitte im Kalender verschieben oder den Ausfall-Assistenten am Tag verwenden.");
        }

        PublishSeverity = lines.Count > 1 ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        PublishMessage = string.Join('\n', lines);
        Day = null;
        await LoadAsync().ConfigureAwait(true);
        Changed?.Invoke();
    }

    [RelayCommand]
    private async Task DiscardDraftsAsync()
    {
        if (_week is null || SelectedLocation?.Id is not { } locationId || !CanPublish || !await LeaveDayAsync().ConfigureAwait(true))
        {
            return;
        }

        var count = DraftCount;
        var answer = await AskAsync(new ConfirmRequest("Entwürfe verwerfen?",
            $"{(count == 1 ? "1 Entwurf dieser Woche ist" : $"{count} Entwürfe dieser Woche sind")} danach weg. Der veröffentlichte Stand bleibt.", "Verwerfen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        Error = null;
        var result = await _context.Api.PostAsync<object>($"api/locations/{locationId}/roster/discard", RangeOf(_week)).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        PublishMessage = null;
        Day = null;
        await LoadAsync().ConfigureAwait(true);
        Changed?.Invoke();
    }

    private static RosterPublishRequest RangeOf(RosterWeek week) => new(week.From, week.Dates.Count > 0 ? week.Dates[^1] : week.From);

    [RelayCommand]
    private void ClosePublishMessage() => PublishMessage = null;

    [RelayCommand]
    private void CloseError() => Error = null;

    // ---------- IEditableArea ----------

    public async Task<bool> SaveChangesAsync() => Day is not { HasUnsavedChanges: true } day || await day.SaveChangesAsync().ConfigureAwait(true);

    public void Discard() => Day?.Discard();

    /// <summary>Gilt für heute, wenn die Woche heute enthält, sonst für den ersten Tag der Woche. Auf der Karte einer Person steht der Hinweis schon oben.</summary>
    private async Task<Dictionary<Guid, string>> LoadNoteSymbolsAsync()
    {
        if (!ShowsNames)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_context.Now, _context.Zone).DateTime);
        var on = today >= WeekStart && today <= WeekStart.AddDays(6) ? today : WeekStart;
        var result = await _context.Api.GetAsync<List<StaffNoteSymbolDto>>($"api/employee-notes/symbols?on={on:yyyy-MM-dd}").ConfigureAwait(true);
        return (result.Value ?? []).GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => string.Join("\n", g.Select(s => s.Text)));
    }
}
