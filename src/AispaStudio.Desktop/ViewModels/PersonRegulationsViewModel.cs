using System.ComponentModel;
using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Auswahl im Formular: ein Wert mit seinem Text.</summary>
public sealed record RegulationOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Ein Wochentag zum Ankreuzen bei „feste freie Wochentage“.</summary>
public sealed partial class RegulationDayOption(DayOfWeek day, string label, Action changed) : ObservableObject
{
    public DayOfWeek Day => day;

    public string Label => label;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    partial void OnIsCheckedChanged(bool value) => changed();
}

/// <summary>Eine Zeile der drei Listen; abgelaufene stehen ausgegraut.</summary>
public sealed class RegulationRow(string title, IReadOnlyList<string> lines, bool isExpired, bool isEditable, IRelayCommand edit, IAsyncRelayCommand delete)
{
    public string Title => title;

    public IReadOnlyList<string> Lines => lines;

    public bool IsExpired => isExpired;

    public bool IsEditable => isEditable;

    public IRelayCommand EditCommand => edit;

    public IAsyncRelayCommand DeleteCommand => delete;
}

/// <summary>
/// Reiter „Regelungen und Vollmachten“ der Personenkarte (R05 Slice 111, Ä252, E10, E11): Hinweise, Sonderregelungen und Vollmachten mit Gültig-von und Gültig-bis, nur für Inhaberin und Manager.
/// Ein offenes Formular zählt zu den ungespeicherten Änderungen der Karte, hat aber seine eigene Leiste. Bei Ausgeschiedenen nur lesen. Die Texte der Regelungen und die Prüfung liegen in der API.
/// </summary>
public sealed partial class PersonRegulationsViewModel : ViewModelBase
{
    private enum FormKind
    {
        None,
        Note,
        Rule,
        Power,
    }

    private static readonly string[] DayNames = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];

    private readonly TeamContext _context;
    private readonly string _basePath;
    private FormKind _form;
    private Guid? _editingId;
    private string _savedState = "";
    private bool _applying;

    public PersonRegulationsViewModel(TeamContext context, Guid employeeId, bool isReadOnly)
    {
        _context = context;
        _basePath = $"api/employees/{employeeId}/regulations";
        IsEditable = !isReadOnly;
        Days = [.. Enum.GetValues<DayOfWeek>().OrderBy(d => ((int)d + 6) % 7).Select(d => new RegulationDayOption(d, DayNames[((int)d + 6) % 7], FormChanged))];
    }

    /// <summary>Etwas wurde gespeichert oder gelöscht: Kennzeichen auf Karte und Liste neu holen.</summary>
    public event Func<Task>? Changed;

    /// <summary>Die Ansicht setzt den Fokus auf das erste Feld oder das erste ungültige Feld.</summary>
    public event Action<string>? FocusRequested;

    public bool IsEditable { get; }

    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_context.Now, _context.Zone).DateTime);

    public IReadOnlyList<RegulationOption<StaffNoteVisibility>> Visibilities { get; } =
    [
        new(StaffNoteVisibility.Leadership, "Nur Leitung"),
        new(StaffNoteVisibility.Team, "Ganzes Team"),
    ];

    public IReadOnlyList<RegulationOption<StaffRuleKind>> RuleKinds { get; } =
    [
        new(StaffRuleKind.NoAppointmentsAfter, "Keine Termine nach Uhrzeit"),
        new(StaffRuleKind.MaxHoursPerDay, "Höchstens Stunden pro Tag"),
        new(StaffRuleKind.FixedDaysOff, "Feste freie Wochentage"),
        new(StaffRuleKind.ProtectionPeriod, "Schutzfrist"),
    ];

    public IReadOnlyList<RegulationOption<StaffPowerKind>> PowerKinds { get; } =
    [
        new(StaffPowerKind.CashVoid, "Kassa-Storno"),
        new(StaffPowerKind.CashClosing, "Kassa-Abschluss"),
        new(StaffPowerKind.Safe, "Tresor"),
        new(StaffPowerKind.BankDeposit, "Bankeinzahlung"),
        new(StaffPowerKind.Keys, "Schlüssel"),
        new(StaffPowerKind.AcceptDeliveries, "Lieferungen annehmen"),
        new(StaffPowerKind.TriggerOrders, "Bestellungen auslösen"),
        new(StaffPowerKind.DeputyManagement, "Vertretung der Leitung"),
    ];

    public IReadOnlyList<RegulationDayOption> Days { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLoading))]
    public partial bool IsLoaded { get; private set; }

    public bool ShowsLoading => !IsLoaded && !HasLoadError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(ShowsLoading))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoNotes))]
    public partial IReadOnlyList<RegulationRow> Notes { get; private set; } = [];

    public bool HasNoNotes => Notes.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRules))]
    public partial IReadOnlyList<RegulationRow> Rules { get; private set; } = [];

    public bool HasNoRules => Rules.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoPowers))]
    public partial IReadOnlyList<RegulationRow> Powers { get; private set; } = [];

    public bool HasNoPowers => Powers.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ShowsFormError), nameof(ShowsListError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    public bool ShowsFormError => HasError && IsEditing;

    public bool ShowsListError => HasError && !IsEditing;

    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    // ---------- Offenes Formular ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAddButtons), nameof(ShowsFormError), nameof(ShowsListError), nameof(HasUnsavedChanges))]
    public partial bool IsEditing { get; private set; }

    public bool ShowsAddButtons => IsEditable && !IsEditing;

    [ObservableProperty]
    public partial string FormTitle { get; private set; } = "";

    public bool IsNoteForm => _form == FormKind.Note;

    public bool IsRuleForm => _form == FormKind.Rule;

    public bool IsPowerForm => _form == FormKind.Power;

    [ObservableProperty]
    public partial string NoteText { get; set; } = "";

    [ObservableProperty]
    public partial RegulationOption<StaffNoteVisibility>? SelectedVisibility { get; set; }

    [ObservableProperty]
    public partial bool ShowAsSymbol { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLatestEnd), nameof(ShowsMaxHours), nameof(ShowsDaysOff))]
    public partial RegulationOption<StaffRuleKind>? SelectedRuleKind { get; set; }

    public bool ShowsLatestEnd => SelectedRuleKind?.Value == StaffRuleKind.NoAppointmentsAfter;

    public bool ShowsMaxHours => SelectedRuleKind?.Value == StaffRuleKind.MaxHoursPerDay;

    public bool ShowsDaysOff => SelectedRuleKind?.Value == StaffRuleKind.FixedDaysOff;

    [ObservableProperty]
    public partial TimeOnly? LatestEnd { get; set; }

    [ObservableProperty]
    public partial decimal? MaxHours { get; set; }

    [ObservableProperty]
    public partial string Reason { get; set; } = "";

    [ObservableProperty]
    public partial RegulationOption<StaffPowerKind>? SelectedPowerKind { get; set; }

    [ObservableProperty]
    public partial DateOnly? ValidFrom { get; set; }

    [ObservableProperty]
    public partial DateOnly? ValidUntil { get; set; }

    [ObservableProperty]
    public partial string? NoteTextError { get; private set; }

    [ObservableProperty]
    public partial string? ValidFromError { get; private set; }

    [ObservableProperty]
    public partial string? ValidUntilError { get; private set; }

    [ObservableProperty]
    public partial string? ValueError { get; private set; }

    public bool HasUnsavedChanges => IsEditing && State() != _savedState;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(NoteText) or nameof(SelectedVisibility) or nameof(ShowAsSymbol) or nameof(SelectedRuleKind) or nameof(LatestEnd) or nameof(MaxHours)
            or nameof(Reason) or nameof(SelectedPowerKind) or nameof(ValidFrom) or nameof(ValidUntil))
        {
            FormChanged();
        }
    }

    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var result = await _context.Api.GetAsync<StaffRegulationsDto>(_basePath).ConfigureAwait(true);
        if (result.Value is not { } regulations)
        {
            LoadError = result.Error ?? "Die Regelungen und Vollmachten wurden nicht geladen.";
            return;
        }

        Notes = [.. regulations.Notes.Select(ToRow)];
        Rules = [.. regulations.Rules.Select(ToRow)];
        Powers = [.. regulations.Powers.Select(ToRow)];
        IsLoaded = true;
    }

    [RelayCommand]
    private void AddNote() => OpenNote(null);

    [RelayCommand]
    private void AddRule() => OpenRule(null);

    [RelayCommand]
    private void AddPower() => OpenPower(null);

    /// <summary>Für die Rückfrage der Karte: speichert nur, wenn etwas geändert ist.</summary>
    public Task<bool> SaveChangesAsync() => HasUnsavedChanges ? SaveFormAsync() : Task.FromResult(true);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsEditing)
        {
            await SaveFormAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void Cancel() => Discard();

    [RelayCommand]
    private void CloseError() => Error = null;

    public void Discard()
    {
        Close();
        Error = null;
        StatusText = null;
    }

    private static string Day(DateOnly day) => day.ToString("d.M.yyyy", CultureInfo.InvariantCulture);

    private static string Period(DateOnly? from, DateOnly? until) => (from, until) switch
    {
        ({ } f, { } u) => $"Gültig von {Day(f)} bis {Day(u)}",
        ({ } f, null) => $"Gültig ab {Day(f)}",
        (null, { } u) => $"Gültig bis {Day(u)}",
        _ => "Ohne Befristung",
    };

    private static string TitleOf(string title, bool isExpired) => isExpired ? $"{title} (abgelaufen)" : title;

    private RegulationRow ToRow(StaffNoteDto note) => NewRow(
        TitleOf(note.Text, note.IsExpired),
        [$"{Visibilities.First(v => v.Value == note.Visibility).Label}{(note.ShowAsSymbol ? ", mit Symbol im Dienstplan und Kalender" : "")}", Period(note.ValidFrom, note.ValidUntil)],
        note.IsExpired, () => OpenNote(note), "Hinweis", $"{_basePath}/notes/{note.Id}");

    private RegulationRow ToRow(StaffRuleDto rule)
    {
        var lines = new List<string> { Period(rule.ValidFrom, rule.ValidUntil) };
        if (rule.Reason is { } reason)
        {
            lines.Add($"Grund: {reason}");
        }

        return NewRow(TitleOf(rule.Summary, rule.IsExpired), lines, rule.IsExpired, () => OpenRule(rule), "Sonderregelung", $"{_basePath}/rules/{rule.Id}");
    }

    private RegulationRow ToRow(StaffPowerDto power) => NewRow(
        TitleOf(power.Label, power.IsExpired), [Period(power.ValidFrom, power.ValidUntil), $"Erteilt von {power.GrantedBy}"],
        power.IsExpired, () => OpenPower(power), "Vollmacht", $"{_basePath}/powers/{power.Id}");

    private RegulationRow NewRow(string title, IReadOnlyList<string> lines, bool isExpired, Action edit, string kind, string path) => new(
        title, lines, isExpired, IsEditable, new RelayCommand(edit, () => !IsEditing), new AsyncRelayCommand(() => DeleteAsync(kind, title, path), () => !IsEditing));

    private void OpenNote(StaffNoteDto? note)
    {
        Open(FormKind.Note, note?.Id, note is null ? "Hinweis anlegen" : "Hinweis ändern", () =>
        {
            NoteText = note?.Text ?? "";
            SelectedVisibility = Visibilities.First(v => v.Value == (note?.Visibility ?? StaffNoteVisibility.Leadership));
            ShowAsSymbol = note?.ShowAsSymbol ?? false;
            ValidFrom = note?.ValidFrom;
            ValidUntil = note?.ValidUntil;
        });
        FocusRequested?.Invoke("NoteTextField");
    }

    private void OpenRule(StaffRuleDto? rule)
    {
        Open(FormKind.Rule, rule?.Id, rule is null ? "Sonderregelung anlegen" : "Sonderregelung ändern", () =>
        {
            SelectedRuleKind = RuleKinds.First(k => k.Value == (rule?.Kind ?? StaffRuleKind.NoAppointmentsAfter));
            LatestEnd = rule?.LatestEnd;
            MaxHours = rule?.MaxHours;
            foreach (var day in Days)
            {
                day.IsChecked = rule?.DaysOff.Contains(day.Day) == true;
            }

            Reason = rule?.Reason ?? "";
            ValidFrom = rule?.ValidFrom ?? Today;
            ValidUntil = rule?.ValidUntil;
        });
        FocusRequested?.Invoke("RuleKindField");
    }

    private void OpenPower(StaffPowerDto? power)
    {
        Open(FormKind.Power, power?.Id, power is null ? "Vollmacht erteilen" : "Vollmacht ändern", () =>
        {
            SelectedPowerKind = PowerKinds.First(k => k.Value == (power?.Kind ?? StaffPowerKind.CashVoid));
            ValidFrom = power?.ValidFrom ?? Today;
            ValidUntil = power?.ValidUntil;
        });
        FocusRequested?.Invoke("PowerKindField");
    }

    private void Open(FormKind form, Guid? id, string title, Action fill)
    {
        _applying = true;
        _form = form;
        _editingId = id;
        fill();
        NoteTextError = ValidFromError = ValidUntilError = ValueError = Error = StatusText = null;
        FormTitle = title;
        _applying = false;
        _savedState = State();
        IsEditing = true;
        OnFormKindChanged();
    }

    private void Close()
    {
        _form = FormKind.None;
        _editingId = null;
        IsEditing = false;
        OnFormKindChanged();
    }

    private void OnFormKindChanged()
    {
        OnPropertyChanged(nameof(IsNoteForm));
        OnPropertyChanged(nameof(IsRuleForm));
        OnPropertyChanged(nameof(IsPowerForm));
        foreach (var row in Notes.Concat(Rules).Concat(Powers))
        {
            row.EditCommand.NotifyCanExecuteChanged();
            row.DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    private void FormChanged()
    {
        if (_applying || !IsEditing)
        {
            return;
        }

        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private string State() => string.Join('|', _form, NoteText, SelectedVisibility?.Value, ShowAsSymbol, SelectedRuleKind?.Value, LatestEnd, MaxHours,
        string.Concat(Days.Select(d => d.IsChecked ? '1' : '0')), Reason, SelectedPowerKind?.Value, ValidFrom, ValidUntil);

    /// <summary>Sagt nur früher, was fehlt; geprüft wird in der API.</summary>
    private bool Check()
    {
        NoteTextError = _form == FormKind.Note && string.IsNullOrWhiteSpace(NoteText) ? "Bitte einen Text eingeben." : null;
        ValidFromError = _form != FormKind.Note && ValidFrom is null ? "Bitte ein Datum eingeben." : null;
        ValidUntilError = ValidFrom is { } from && ValidUntil is { } until && until < from ? "„Gültig bis“ darf nicht vor „Gültig von“ liegen." : null;
        ValueError = _form != FormKind.Rule ? null : SelectedRuleKind?.Value switch
        {
            StaffRuleKind.NoAppointmentsAfter when LatestEnd is null => "Bitte die Uhrzeit eingeben.",
            StaffRuleKind.MaxHoursPerDay when MaxHours is not > 0 => "Bitte die Stunden eingeben.",
            StaffRuleKind.FixedDaysOff when !Days.Any(d => d.IsChecked) => "Bitte mindestens einen Wochentag wählen.",
            _ => null,
        };
        var first = NoteTextError is not null ? "NoteTextField" : ValueError is not null ? ValueField() : ValidFromError is not null ? "ValidFromField" : ValidUntilError is not null ? "ValidUntilField" : null;
        if (first is not null)
        {
            FocusRequested?.Invoke(first);
        }

        return first is null;
    }

    private string ValueField() => SelectedRuleKind?.Value switch
    {
        StaffRuleKind.NoAppointmentsAfter => "LatestEndField",
        StaffRuleKind.MaxHoursPerDay => "MaxHoursField",
        _ => "RuleKindField",
    };

    private async Task<bool> SaveFormAsync()
    {
        if (!Check())
        {
            return false;
        }

        Error = null;
        var (segment, input) = _form switch
        {
            FormKind.Note => ("notes", (object)new StaffNoteInput(NoteText.Trim(), SelectedVisibility!.Value, ShowAsSymbol, ValidFrom, ValidUntil)),
            FormKind.Rule => ("rules", new StaffRuleInput(SelectedRuleKind!.Value, ValidFrom!.Value, ValidUntil, ShowsLatestEnd ? LatestEnd : null, ShowsMaxHours ? MaxHours : null,
                ShowsDaysOff ? [.. Days.Where(d => d.IsChecked).Select(d => d.Day)] : null, string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim())),
            _ => ("powers", new StaffPowerInput(SelectedPowerKind!.Value, ValidFrom!.Value, ValidUntil)),
        };
        var result = _editingId is { } id
            ? await _context.Api.PutAsync<object>($"{_basePath}/{segment}/{id}", input).ConfigureAwait(true)
            : await _context.Api.PostAsync<object>($"{_basePath}/{segment}", input).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return false;
        }

        Close();
        await AfterChangeAsync().ConfigureAwait(true);
        StatusText = "Gespeichert.";
        return true;
    }

    private async Task DeleteAsync(string kind, string title, string path)
    {
        if (_context.Dialogs is not { } dialogs)
        {
            return;
        }

        var answer = await dialogs.AskAsync(new ConfirmRequest($"{kind} löschen?", $"„{title}“ wird gelöscht. Im Verlauf bleibt nur, dass gelöscht wurde.", "Löschen", IsDestructive: true)).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        var result = await _context.Api.DeleteAsync<object>(path).ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return;
        }

        StatusText = "Gelöscht.";
        await AfterChangeAsync().ConfigureAwait(true);
    }

    private async Task AfterChangeAsync()
    {
        await LoadAsync().ConfigureAwait(true);
        if (Changed is { } changed)
        {
            await changed().ConfigureAwait(true);
        }
    }
}
