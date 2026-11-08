using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Abschnitt gearbeiteter Zeit im Tag: Zeitspanne, Standort (wenn fremd), „Ändern“ und „Verlauf“.</summary>
public sealed partial class TimeRecordRow : ObservableObject
{
    public TimeRecordRow(TimeRecordDto record, string spanText, string? place, bool canChange, bool canHistory, Action<TimeRecordRow> change, Func<TimeRecordRow, Task> history)
    {
        Record = record;
        SpanText = spanText;
        var kind = record.Source == TimeRecordSource.Manual ? record.Changed ? "korrigiert" : "nachgetragen" : null;
        DetailText = string.Join(" · ", new[] { place, kind }.Where(t => t is not null));
        HasDetail = DetailText.Length > 0;
        CanChange = canChange;
        CanHistory = canHistory;
        ChangeName = $"Abschnitt {spanText} ändern";
        HistoryName = $"Verlauf von Abschnitt {spanText}";
        ChangeCommand = new RelayCommand(() => change(this));
        HistoryCommand = new AsyncRelayCommand(() => history(this));
    }

    public TimeRecordDto Record { get; }

    /// <summary>„09:00–17:00“, ohne Ende „08:00–läuft“.</summary>
    public string SpanText { get; }

    /// <summary>„Graz · nachgetragen“; leer bei einem selbst gestempelten Abschnitt am eigenen Standort.</summary>
    public string DetailText { get; }

    public bool HasDetail { get; }

    /// <summary>Ändern nur mit dem Recht dazu und nur am gewählten Standort; Abschnitte anderer Standorte sind lesbar.</summary>
    public bool CanChange { get; }

    public bool CanHistory { get; }

    public string ChangeName { get; }

    public string HistoryName { get; }

    public IRelayCommand ChangeCommand { get; }

    public IAsyncRelayCommand HistoryCommand { get; }

    /// <summary>Zeilen des Änderungsprotokolls, solange der Verlauf aufgeklappt ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHistory))]
    public partial IReadOnlyList<string> History { get; set; } = [];

    public bool HasHistory => History.Count > 0;
}

/// <summary>
/// Die Zeiten eines Tages (R05 Slice 99, Ä191, Ä250): Soll, Ist und Pause, Hinweise nach dem Arbeitszeitrecht, die Abschnitte mit „Ändern“ und „Verlauf“ und das Nachtragen,
/// Ändern und Entfernen eines Abschnitts. Der Grund ist Pflicht (Meldung am Feld, kein Aufruf ohne ihn) und bleibt im Änderungsprotokoll; ein Ende vor dem Beginn endet am Folgetag.
/// Datum und Uhrzeit gelten in der Zeitzone des Standorts, nicht der des Rechners. Bearbeiten darf nur, wer Standorte verwaltet (<see cref="CanEdit"/>); das Backoffice liest.
/// Vorbild: <c>DayPanel</c> und <c>RecordEditor</c> in <c>web/src/time/TimesheetPage.tsx</c>.
/// </summary>
public sealed partial class TimeDayEditorViewModel : ViewModelBase, IEditableArea
{
    public const string Saved = "Gespeichert.";

    public const string Removed = "Abschnitt entfernt.";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private readonly TeamContext _context;
    private readonly Guid _locationId;
    private readonly IReadOnlyList<Choice> _locations;
    private readonly TimeZoneInfo _zone;
    private readonly string _employeeName;
    private readonly Func<Task> _saved;
    private readonly Action _close;
    private TimeRecordDto? _editing;
    private bool _seeding;
    private string _savedState = "";

    public TimeDayEditorViewModel(TeamContext context, Guid locationId, IReadOnlyList<Choice> locations, TimeZoneInfo zone, Guid employeeId, string employeeName,
        TimesheetDay day, Func<Task> saved, Action close)
    {
        _context = context;
        _locationId = locationId;
        _locations = locations;
        _zone = zone;
        EmployeeId = employeeId;
        _employeeName = employeeName;
        _saved = saved;
        _close = close;
        Day = day;
        Show(day);
        if (CanEdit && day.Records.Count == 0)
        {
            OpenEditor(null);
        }
    }

    public Guid EmployeeId { get; }

    public DateOnly Date => Day.Date;

    /// <summary>Der Tag, wie ihn die Zeiterfassung zuletzt geliefert hat.</summary>
    public TimesheetDay Day { get; private set; }

    /// <summary>Überschrift und Name in der Rückfrage: „Lena Huber, Di. 3.3.“.</summary>
    public string Heading => $"{_employeeName}, {DayLayout.ShortDay(Date)}";

    public string Title => Heading;

    /// <summary>Zeiten ändern darf nur, wer Standorte verwaltet (die API prüft es ebenfalls); das Backoffice liest.</summary>
    public bool CanEdit => _context.CanPlan;

    /// <summary>„Soll 8 h · Ist 8,5 h · Pause 30 min“.</summary>
    [ObservableProperty]
    public partial string SummaryText { get; private set; } = "";

    /// <summary>Hinweise nach dem Arbeitszeitrecht, eine pro Zeile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    public partial string? WarningsText { get; private set; }

    public bool HasWarnings => WarningsText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRecords))]
    public partial IReadOnlyList<TimeRecordRow> Records { get; private set; } = [];

    public bool HasNoRecords => Records.Count == 0;

    /// <summary>Ein Abschnitt wird nachgetragen oder geändert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd), nameof(CanRemove), nameof(HasUnsavedChanges))]
    public partial bool IsEditing { get; private set; }

    public bool CanAdd => CanEdit && !IsEditing;

    public bool CanRemove => IsEditing && _editing is not null;

    public string EditorTitle => _editing is null ? "Zeit nachtragen" : "Abschnitt ändern";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    public partial TimeOnly? Start { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges), nameof(NextDayHint))]
    public partial TimeOnly? End { get; set; }

    /// <summary>Ein Ende vor dem Beginn liegt am Folgetag (über Mitternacht).</summary>
    public string? NextDayHint => End is { } end && Start is { } start && end < start ? "Endet am Folgetag." : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    public partial string Reason { get; set; } = "";

    [ObservableProperty]
    public partial string? StartError { get; private set; }

    [ObservableProperty]
    public partial string? ReasonError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert.“ neben der Leiste.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; set; }

    public bool HasUnsavedChanges => IsEditing && State() != _savedState;

    private string State() => $"{Start}|{End}|{Reason}";

    /// <summary>Übernimmt einen frisch geladenen Tag (nach dem Speichern oder Entfernen); der Editor ist dann zu.</summary>
    public void Refresh(TimesheetDay day)
    {
        Day = day;
        Show(day);
    }

    private void Show(TimesheetDay day)
    {
        SummaryText = $"Soll {Hours(day.PlannedHours)} h · Ist {Hours(day.ActualHours)} h · Pause {day.BreakMinutes} min";
        WarningsText = day.Warnings.Count == 0 ? null : string.Join('\n', day.Warnings);
        Records = [.. day.Records.Select(RowOf)];
    }

    public static string Hours(double hours) => hours.ToString("0.##", German);

    private TimeRecordRow RowOf(TimeRecordDto record)
    {
        var own = record.LocationId == _locationId;
        var place = own ? null : _locations.FirstOrDefault(l => l.Id == record.LocationId)?.Label ?? "anderer Standort";
        var span = $"{Local(record.Start):HH:mm}–{(record.End is { } end ? Local(end).ToString("HH:mm", CultureInfo.InvariantCulture) : "läuft")}";
        return new TimeRecordRow(record, span, place, CanEdit && own, own && record.Source == TimeRecordSource.Manual, row => OpenEditor(row.Record), ShowHistoryAsync);
    }

    private DateTime Local(DateTimeOffset at) => DayLayout.Local(at, _zone);

    // ---------- Nachtragen und Ändern ----------

    [RelayCommand]
    private void Add() => OpenEditor(null);

    private void OpenEditor(TimeRecordDto? record)
    {
        if (!CanEdit)
        {
            return;
        }

        _seeding = true;
        _editing = record;
        Start = record is null ? new TimeOnly(9, 0) : TimeOnly.FromDateTime(Local(record.Start));
        End = record is null ? new TimeOnly(17, 0) : record.End is { } end ? TimeOnly.FromDateTime(Local(end)) : null;
        Reason = "";
        StartError = null;
        ReasonError = null;
        Error = null;
        StatusText = null;
        IsEditing = true;
        _seeding = false;
        _savedState = State();
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnStartChanged(TimeOnly? value)
    {
        OnPropertyChanged(nameof(NextDayHint));
        Edited();
        StartError = null;
    }

    partial void OnEndChanged(TimeOnly? value) => Edited();

    partial void OnReasonChanged(string value)
    {
        Edited();
        if (value.Trim().Length > 0)
        {
            ReasonError = null;
        }
    }

    private void Edited()
    {
        if (!_seeding)
        {
            StatusText = null;
        }
    }

    private bool RequireReason()
    {
        ReasonError = Reason.Trim().Length == 0 ? "Grund angeben." : null;
        return ReasonError is null;
    }

    /// <summary>Speichern oder Entfernen wurde abgelehnt; die Ansicht setzt den Fokus in das erste Feld mit Meldung (masken.md, R8).</summary>
    public event Action? InvalidField;

    public async Task<bool> SaveChangesAsync()
    {
        if (!IsEditing)
        {
            return true;
        }

        Error = null;
        StartError = Start is null ? "Beginn angeben." : null;
        var reasonGiven = RequireReason();
        if (Start is not { } start || !reasonGiven)
        {
            InvalidField?.Invoke();
            return false;
        }

        var endAt = End is { } end ? DayLayout.LocalToUtc(end < start ? Date.AddDays(1) : Date, end, _zone) : (DateTimeOffset?)null;
        var input = new TimeRecordInput(EmployeeId, DayLayout.LocalToUtc(Date, start, _zone), endAt, Reason.Trim());
        var path = $"api/locations/{_locationId}/time-records";
        var result = _editing is { } record
            ? await _context.Api.PutAsync<TimeRecordDto>($"{path}/{record.Id}", input).ConfigureAwait(true)
            : await _context.Api.PostAsync<TimeRecordDto>(path, input).ConfigureAwait(true);
        if (result.Value is null)
        {
            Error = result.Error ?? "Die Zeit wurde nicht gespeichert.";
            return false;
        }

        await FinishAsync(Saved).ConfigureAwait(true);
        return true;
    }

    [RelayCommand]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    /// <summary>Schließt den Editor und lädt die Zeiten neu; der Status bleibt neben der Leiste stehen.</summary>
    private async Task FinishAsync(string status)
    {
        _editing = null;
        IsEditing = false;
        _savedState = State();
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        await _saved().ConfigureAwait(true);
        StatusText = status;
    }

    /// <summary>Verwerfen schließt den Editor; „Zeit nachtragen“ öffnet ihn wieder.</summary>
    public void Discard()
    {
        _editing = null;
        IsEditing = false;
        StartError = null;
        ReasonError = null;
        Error = null;
        StatusText = null;
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private void DiscardChanges() => Discard();

    // ---------- Entfernen ----------

    /// <summary>„Entfernen“: erst der Grund (Pflicht), dann die Rückfrage mit Zeitspanne und Grund; der Abschnitt bleibt mit dem Grund im Verlauf.</summary>
    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (_editing is not { } record)
        {
            return;
        }

        Error = null;
        if (!RequireReason())
        {
            InvalidField?.Invoke();
            return;
        }

        var span = record.End is { } end ? $"{Local(record.Start):HH:mm}–{Local(end):HH:mm}" : $"seit {Local(record.Start):HH:mm} (läuft)";
        var reason = Reason.Trim();
        var answer = await AskAsync(new ConfirmRequest("Abschnitt entfernen?",
            $"Der Abschnitt {span} wird mit dem Grund „{reason}“ entfernt und bleibt im Verlauf sichtbar.", "Entfernen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _context.Api.DeleteAsync<object>($"api/locations/{_locationId}/time-records/{record.Id}", new TimeRecordDeletion(reason)).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        await FinishAsync(Removed).ConfigureAwait(true);
    }

    // ---------- Verlauf ----------

    private async Task ShowHistoryAsync(TimeRecordRow row)
    {
        if (row.HasHistory)
        {
            row.History = [];
            return;
        }

        var result = await _context.Api.GetAsync<List<TimeRecordChangeDto>>($"api/locations/{_locationId}/time-records/{row.Record.Id}/changes").ConfigureAwait(true);
        if (result.Value is not { } changes)
        {
            Error = result.Error ?? "Der Verlauf wurde nicht geladen.";
            return;
        }

        row.History = [.. changes.Select(HistoryLine)];
    }

    /// <summary>„03.03. 09:12 Marco Berger geändert (vorher 09:00–17:00): Stempeln vergessen“.</summary>
    private string HistoryLine(TimeRecordChangeDto change)
    {
        var action = change.Action switch
        {
            TimeRecordAction.Created => "nachgetragen",
            TimeRecordAction.Changed => "geändert",
            _ => "entfernt",
        };
        var before = change.OldStart is { } oldStart
            ? $" (vorher {Local(oldStart):HH:mm}–{(change.OldEnd is { } oldEnd ? Local(oldEnd).ToString("HH:mm", CultureInfo.InvariantCulture) : "offen")})"
            : "";
        return $"{Local(change.ChangedAt):dd.MM. HH:mm} {change.ChangedBy} {action}{before}{(string.IsNullOrWhiteSpace(change.Reason) ? "" : $": {change.Reason}")}";
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    [RelayCommand]
    private void Close() => _close();
}
