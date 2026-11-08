using System.ComponentModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Bereich „Team“ der Verwaltung (R05 Slice 98, Ä191, Ä250): links alle Personen mit Suche, Rolle und Status (aktiv, gesperrt, abgelaufen, eingeladen,
/// ausgeschieden) und „Person anlegen“ bzw. „Person einladen“, rechts die Personenkarte (<see cref="PersonCardViewModel"/>). Ersetzt die Web-Masken
/// <c>TeamTab.tsx</c>, <c>StaffPage.tsx</c> (ohne Empfangsgerät) und <c>QualificationsTab.tsx</c>. Wer mit ungespeicherten Änderungen eine andere Person
/// wählt, wird gefragt (<see cref="UnsavedChanges"/>). Über der Fläche schaltet eine Leiste um (R05 Slice 99): „Personen“ (die Karten), „Wochenplan“ (der Dienstplan für alle,
/// <see cref="RosterPlanViewModel"/>), „Zeiten“ (<see cref="TimesheetViewModel"/>) und „Anträge“ (<see cref="AbsenceRequestsViewModel"/>), je nach Recht. Wer nur den Kalender sehen darf,
/// bekommt nur den Wochenplan, ohne Personenliste. Ein Klick auf einen Namen im Wochenplan öffnet die Karte der Person im Reiter „Dienstplan und Abwesenheit“, in den Zeiten im Reiter „Zeiten“.
/// </summary>
public sealed partial class TeamViewModel : ViewModelBase, ITargetedArea
{
    public const string PeopleMode = "personen";
    public const string RosterMode = "wochenplan";
    public const string TimesMode = "zeitenuebersicht";
    public const string RequestsMode = "antraege";
    public const string PayrollMode = "lohn-export";
    public const string PowersMode = "wer-darf-was";
    public const string DutiesMode = "zustaendigkeiten";

    private const string NotInTeam = "Diese Person steht nicht mehr im Team und deshalb nicht in der Liste.";

    private static readonly TimeSpan EndsSoon = TimeSpan.FromDays(7);

    private static readonly string[] Forwarded =
        [nameof(HasUnsavedChanges), nameof(StatusText), nameof(Error), nameof(HasError), nameof(HasSaveBar), nameof(CanRemove), nameof(Title)];

    private readonly IWorkstationApi _api;
    private readonly CurrentUser _user;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private TeamContext? _context;
    private IReadOnlyList<TeamEntry> _all = [];
    private bool _reverting;

    /// <summary>Sprungziel (Behandlungen, Kalender, Assistent), das vor dem Laden oder vor dem Wechsel der Person kam; gilt, sobald sie offen ist.</summary>
    private (Guid? Id, string? Section, string? Field)? _pending;

    public TeamViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time)
    {
        _api = api;
        _user = user;
        _time = time;
        Modes = [.. ModesOf().Select(m => new CardTab(m.Id, m.Label, id => _ = SwitchToAsync(id)))];
        Mode = Modes.Count > 0 ? Modes[0].Id : PeopleMode;
        foreach (var each in Modes)
        {
            each.IsOn = each.Id == Mode;
        }
    }

    /// <summary>
    /// Die Ansichten der Leiste nach den Rechten: Personen (Stammdaten oder Zugänge verwalten), Wochenplan (Kalender oder Personalunterlagen sehen), Zeiten (Standorte verwalten
    /// oder Personalunterlagen lesen) und Anträge (Standorte verwalten).
    /// </summary>
    private IEnumerable<(string Id, string Label)> ModesOf()
    {
        if (CanPeople)
        {
            yield return (PeopleMode, "Personen");
        }

        if (_user.Rights.Contains(AccessRight.ViewCalendar) || _user.Rights.Contains(AccessRight.ViewStaffRecords))
        {
            yield return (RosterMode, "Wochenplan");
        }

        if (CanTimes)
        {
            yield return (TimesMode, "Zeiten");
        }

        if (_user.Rights.Contains(AccessRight.ManageLocation))
        {
            yield return (RequestsMode, "Anträge");
        }

        if (CanPayroll)
        {
            yield return (PayrollMode, "Lohn");
        }

        if (_user.Rights.Contains(AccessRight.ManageHrFile))
        {
            yield return (PowersMode, "Wer darf was?");
            yield return (DutiesMode, "Zuständigkeiten");
        }
    }

    /// <summary>Lohn sehen: die Organisation verwalten oder Personalunterlagen lesen (wie die API an <c>GET api/payroll/…</c>).</summary>
    private bool CanPayroll => _user.Rights.Contains(AccessRight.ManageOrganization) || _user.Rights.Contains(AccessRight.ViewStaffRecords);

    /// <summary>Name der Maske in der Rückfrage: die offene Person, im Wochenplan „Dienstplan“.</summary>
    public string Title => ShowsPeople ? Card?.Title ?? "Team" : ActiveArea?.Title ?? "Team";

    /// <summary>„Ausgeschiedene zeigen“ gibt es nur mit dem Recht auf die Stammdaten (wie <c>GET api/employees/former</c>).</summary>
    public bool CanShowFormer => _user.Rights.Contains(AccessRight.ManageCatalog);

    private bool CanStaff => _user.Rights.Contains(AccessRight.ManageStaff);

    /// <summary>Zeiten sehen: Standorte verwalten oder Personalunterlagen lesen (wie die API an <c>GET …/timesheet</c>); ändern nur mit ManageLocation.</summary>
    private bool CanTimes => _user.Rights.Contains(AccessRight.ManageLocation) || _user.Rights.Contains(AccessRight.ViewStaffRecords);

    /// <summary>Personenliste und Karten: wer Stammdaten pflegt oder Zugänge verwaltet. Ohne dieses Recht lädt die Maske weder Personen noch Zugänge.</summary>
    private bool CanPeople => CanShowFormer || CanStaff;

    /// <summary>Die Ansichten der Leiste; die Leiste steht nur, wenn es mehr als eine gibt.</summary>
    public IReadOnlyList<CardTab> Modes { get; }

    public bool ShowsModes => Modes.Count > 1;

    /// <summary>Die Leiste und jede Ansicht stehen erst, wenn das Team geladen ist.</summary>
    public bool ShowsModesBar => IsLoaded && ShowsModes;

    public bool ShowsPeopleView => IsLoaded && ShowsPeople;

    public bool ShowsRosterView => IsLoaded && ShowsRoster;

    public bool ShowsTimesView => IsLoaded && ShowsTimes;

    public bool ShowsRequestsView => IsLoaded && ShowsRequests;

    public bool ShowsPayrollView => IsLoaded && ShowsPayroll;

    public bool ShowsPowersView => IsLoaded && Mode == PowersMode;

    public bool ShowsDutiesView => IsLoaded && Mode == DutiesMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPeople), nameof(ShowsRoster), nameof(ShowsTimes), nameof(ShowsRequests), nameof(ShowsPayroll), nameof(ShowsPeopleView), nameof(ShowsRosterView),
        nameof(ShowsTimesView), nameof(ShowsRequestsView), nameof(ShowsPayrollView), nameof(ShowsPowersView), nameof(ShowsDutiesView), nameof(Title), nameof(HasUnsavedChanges), nameof(StatusText), nameof(Error), nameof(HasError), nameof(HasSaveBar), nameof(CanRemove))]
    public partial string Mode { get; private set; } = PeopleMode;

    public bool ShowsPeople => Mode == PeopleMode;

    public bool ShowsRoster => Mode == RosterMode;

    public bool ShowsTimes => Mode == TimesMode;

    public bool ShowsRequests => Mode == RequestsMode;

    public bool ShowsPayroll => Mode == PayrollMode;

    /// <summary>Der Dienstplan für alle (Wochenplan); nur mit dem Recht auf den Dienstplan, angelegt beim Laden.</summary>
    public RosterPlanViewModel? Plan { get; private set; }

    /// <summary>Die Zeiterfassung für alle; nur mit dem Recht auf die Zeiten, angelegt beim Laden.</summary>
    public TimesheetViewModel? Times { get; private set; }

    /// <summary>Offene Abwesenheitsanträge; nur mit dem Recht, Standorte zu verwalten, angelegt beim Laden.</summary>
    public AbsenceRequestsViewModel? Requests { get; private set; }

    /// <summary>Monatswerte, Export und Einstellungen der Lohnverrechnung (R05 Slice 100); nur mit dem Recht auf den Lohn, angelegt beim Laden.</summary>
    public PayrollViewModel? Payroll { get; private set; }

    /// <summary>„Wer darf was?“ (R05 Slice 111); nur mit dem Recht auf die Personalakte, angelegt beim Laden.</summary>
    public PowerOverviewViewModel? Powers { get; private set; }

    /// <summary>„Zuständigkeiten“ (R05 Slice 117); nur mit dem Recht auf die Personalakte, angelegt beim Laden.</summary>
    public DutyOverviewViewModel? Duties { get; private set; }

    /// <summary>Die Maske der gewählten Ansicht mit Rückfrage bei ungespeicherten Änderungen (Karte, Wochenplan, Zeiten oder Lohn; Anträge ändern nichts offen).</summary>
    private IEditableArea? ActiveArea => Mode switch
    {
        PeopleMode => Card,
        RosterMode => Plan,
        TimesMode => Times,
        PayrollMode => Payroll,
        DutiesMode => Duties,
        _ => null,
    };

    [ObservableProperty]
    public partial IReadOnlyList<TeamEntry> Entries { get; private set; } = [];

    [ObservableProperty]
    public partial TeamEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HasUnsavedChanges), nameof(StatusText), nameof(Error), nameof(HasError), nameof(HasSaveBar),
        nameof(CanRemove))]
    public partial PersonCardViewModel? Card { get; private set; }

    /// <summary>Suche über Name und E-Mail.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowFormer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsModesBar), nameof(ShowsPeopleView), nameof(ShowsRosterView), nameof(ShowsTimesView), nameof(ShowsRequestsView), nameof(ShowsPayrollView), nameof(ShowsPowersView), nameof(ShowsDutiesView))]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    /// <summary>Meldung zur Liste über der Fußleiste: ein Sprungziel, das nicht im Team steht.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    public bool HasNotice => Notice is not null;

    private TeamEntry? FirstEntry => Entries.Count > 0 ? Entries[0] : null;

    /// <summary>Etwas wurde gespeichert; die Leiste „KI und Hilfe“ bietet dann an, eine Frage zu wiederholen.</summary>
    public event Action? Saved;

    /// <summary>Der Dienstplan hat sich geändert (Karte oder Wochenplan); der Kalender richtet sich danach und lädt beim nächsten Öffnen neu.</summary>
    public event Action? RosterChanged;

    public bool HasUnsavedChanges => ActiveArea?.HasUnsavedChanges == true;

    public string? StatusText => ShowsPeople ? Card?.StatusText : null;

    public string? Error => ShowsPeople ? Card?.Error : null;

    public bool HasError => ShowsPeople && Card?.HasError == true;

    public bool HasSaveBar => ShowsPeople && Card?.HasSaveBar == true;

    public bool CanRemove => ShowsPeople && Card?.CanRemove == true;

    [RelayCommand]
    private void CloseNotice() => Notice = null;

    [RelayCommand]
    private void CloseError() => Card?.CloseErrorCommand.Execute(null);

    /// <summary>Lädt Personen, Qualifikationen, Standorte und (mit Recht) Zugänge und Einladungen, beim ersten Öffnen und über „Erneut laden“.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadError = null;
        var modules = await _api.GetAsync<SystemModules>("api/system/modules").ConfigureAwait(true);
        if (modules.Value is null)
        {
            LoadError = modules.Error ?? "Das Team wurde nicht geladen.";
            return;
        }

        _context = new TeamContext(_api, _user, _time, modules.Value.Standalone) { Dialogs = Dialogs };
        if (!await (CanPeople ? RefreshAsync() : LoadLocationsAsync()).ConfigureAwait(true))
        {
            return;
        }

        CreateModes();
        IsLoaded = true;
        await LoadModeAsync().ConfigureAwait(true);
        if (!CanPeople)
        {
            return;
        }

        var target = _pending?.Id;
        var jumpEntry = target is null ? null : EntryOf(target.Value);
        Select(jumpEntry ?? FirstEntry);
        if (target is not null && jumpEntry is null)
        {
            RejectPending();
            return;
        }

        ApplyPending();
    }

    /// <summary>Wer nur den Dienstplan sieht, braucht keine Personenliste, nur die Standorte.</summary>
    private async Task<bool> LoadLocationsAsync()
    {
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        if (locations.Value is null)
        {
            LoadError = locations.Error ?? "Das Team wurde nicht geladen.";
            return false;
        }

        _context!.Data = _context.Data with { Locations = locations.Value };
        return true;
    }

    /// <summary>Wochenplan, Zeiten und Anträge entstehen mit dem Laden, denn sie brauchen die Standorte; sie laden ihre Daten erst, wenn ihre Ansicht offen ist.</summary>
    private void CreateModes()
    {
        if (Modes.Any(m => m.Id == RosterMode))
        {
            Plan = new RosterPlanViewModel(_context!, null) { Dialogs = Dialogs };
            Plan.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RosterPlanViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            };
            Plan.PersonRequested += id => _ = OpenPersonAsync(id, PersonCardViewModel.RosterTab, null);
            Plan.WeekPlanRequested += () => _ = SwitchToAsync(RosterMode);
            Plan.Changed += OnPlanChanged;
            ShowPendingWeek();
        }

        if (Modes.Any(m => m.Id == TimesMode))
        {
            Times = new TimesheetViewModel(_context!, null) { Dialogs = Dialogs };
            Times.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TimesheetViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            };
            Times.PersonRequested += id => _ = OpenPersonAsync(id, PersonCardViewModel.TimesTab, null);
            Times.Changed += () => Card?.Times?.MarkStale();
        }

        if (Modes.Any(m => m.Id == RequestsMode))
        {
            Requests = new AbsenceRequestsViewModel(_context!) { Dialogs = Dialogs };
            Requests.Approved += () =>
            {
                Plan?.MarkStale();
                OnPlanChanged();
            };
        }

        if (Modes.Any(m => m.Id == PayrollMode))
        {
            Payroll = new PayrollViewModel(_context!) { Dialogs = Dialogs };
            Payroll.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PayrollViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            };
        }

        if (Modes.Any(m => m.Id == PowersMode))
        {
            Powers = new PowerOverviewViewModel(_api);
        }

        if (Modes.Any(m => m.Id == DutiesMode))
        {
            Duties = new DutyOverviewViewModel(_api);
            Duties.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DutyOverviewViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            };
        }

        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(Times));
        OnPropertyChanged(nameof(Requests));
        OnPropertyChanged(nameof(Payroll));
        OnPropertyChanged(nameof(Powers));
        OnPropertyChanged(nameof(Duties));
    }

    /// <summary>Lädt die Daten der offenen Ansicht, wenn das noch nicht geschehen ist (Personen laden mit der Maske).</summary>
    private Task LoadModeAsync()
    {
        if (Plan is not null)
        {
            Plan.Dialogs = Dialogs;
        }

        if (Times is not null)
        {
            Times.Dialogs = Dialogs;
        }

        if (Requests is not null)
        {
            Requests.Dialogs = Dialogs;
        }

        if (Payroll is not null)
        {
            Payroll.Dialogs = Dialogs;
        }

        return Mode switch
        {
            PayrollMode when Payroll is not null => Payroll.EnsureLoadedAsync(),
            RosterMode when Plan is not null => Plan.EnsureLoadedAsync(),
            TimesMode when Times is not null => Times.EnsureLoadedAsync(),
            RequestsMode when Requests is not null => Requests.EnsureLoadedAsync(),
            PowersMode when Powers is not null => Powers.EnsureLoadedAsync(),
            DutiesMode when Duties is not null => Duties.EnsureLoadedAsync(),
            PeopleMode when Card is { } card => ReloadCardTabAsync(card),
            _ => Task.CompletedTask,
        };
    }

    /// <summary>Zurück auf der Karte: was eine andere Ansicht inzwischen geändert hat, lädt der offene Reiter neu.</summary>
    private static Task ReloadCardTabAsync(PersonCardViewModel card)
    {
        card.SelectTab(card.SelectedTab);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Wechselt die Ansicht der Leiste. Ein offener Tag im Wochenplan oder eine Karte mit ungespeicherten Änderungen fragt vorher „Änderungen speichern?“; false, wenn der Wechsel
    /// abgebrochen wurde oder die Ansicht fehlt.
    /// </summary>
    public async Task<bool> SwitchToAsync(string mode)
    {
        if (mode == Mode)
        {
            return true;
        }

        if (Modes.All(m => m.Id != mode))
        {
            return false;
        }

        if (IsLoaded && ActiveArea is { } area && !await UnsavedChanges.ConfirmLeaveAsync(Dialogs, area).ConfigureAwait(true))
        {
            return false;
        }

        Mode = mode;
        foreach (var each in Modes)
        {
            each.IsOn = each.Id == mode;
        }

        if (IsLoaded)
        {
            await LoadModeAsync().ConfigureAwait(true);
        }

        return true;
    }



    public Task EnsureLoadedAsync() => IsLoaded ? Task.CompletedTask : LoadAsync();

    /// <summary>
    /// Sprung mit Ziel (Assistent, Ä316): „Wochenplan“ mit einem Tag zeigt die Woche dieses Tages am Standort des Ziels, auch wenn der Bereich erst noch lädt; ein offener Tag
    /// mit Änderungen fragt vorher „Änderungen speichern?“. Alles andere wie <see cref="Open(Guid?, string?, string?)"/>.
    /// </summary>
    public void Open(AdminTarget target)
    {
        if (target.Section == RosterMode && target.From is { } day)
        {
            _ = OpenWeekAsync(target.Location, day);
            return;
        }

        Open(target.Id, target.Section, target.Field);
    }

    private async Task OpenWeekAsync(Guid? location, DateOnly day)
    {
        var shown = Mode == RosterMode && IsLoaded && Plan is not null;
        if (shown && !await UnsavedChanges.ConfirmLeaveAsync(Dialogs, Plan!).ConfigureAwait(true))
        {
            return;
        }

        _pendingWeek = (location, day);
        ShowPendingWeek();
        if (shown)
        {
            await Plan!.EnsureLoadedAsync().ConfigureAwait(true);
            return;
        }

        await SwitchToAsync(RosterMode).ConfigureAwait(true);
    }

    private (Guid? Location, DateOnly Day)? _pendingWeek;

    private void ShowPendingWeek()
    {
        if (Plan is not null && _pendingWeek is { } week)
        {
            Plan.ShowWeek(week.Location, week.Day.AddDays(-(((int)week.Day.DayOfWeek + 6) % 7)));
            _pendingWeek = null;
        }
    }

    /// <summary>Sprung: wählt die Person (<paramref name="id"/>, Person im Team), dann den Reiter (<c>stammdaten</c>, <c>zugang</c>, <c>qualifikationen</c>, <c>dienstplan</c>, <c>zeiten</c>, <c>verlauf</c>).</summary>
    public void Open(Guid? id, string? section, string? field)
    {
        if (section is RosterMode or TimesMode or RequestsMode or PayrollMode or PowersMode)
        {
            _ = SwitchToAsync(section);
            return;
        }

        if (!CanPeople)
        {
            return;
        }

        if (Mode != PeopleMode)
        {
            _ = OpenPersonAsync(id, section, field);
            return;
        }

        OpenPerson(id, section, field);
    }

    /// <summary>Wechselt zu „Personen“ (mit Rückfrage) und öffnet dann die Karte; aus dem Wochenplan und von Sprüngen.</summary>
    private async Task OpenPersonAsync(Guid? id, string? section, string? field)
    {
        if (await SwitchToAsync(PeopleMode).ConfigureAwait(true))
        {
            OpenPerson(id, section, field);
        }
    }

    private void OpenPerson(Guid? id, string? section, string? field)
    {
        _pending = (id ?? SelectedEntry?.EmployeeId, section, field);
        if (!IsLoaded)
        {
            return;
        }

        if (id is { } person)
        {
            if (EntryOf(person) is not { } entry)
            {
                RejectPending();
                return;
            }

            if (entry != SelectedEntry)
            {
                SelectedEntry = entry;
                return;
            }
        }

        ApplyPending();
    }

    public Task<bool> SaveChangesAsync() => ActiveArea is { } area ? area.SaveChangesAsync() : Task.FromResult(true);

    public void Discard() => ActiveArea?.Discard();

    [RelayCommand]
    private async Task SaveAsync() => await SaveChangesAsync().ConfigureAwait(true);

    [RelayCommand]
    private void DiscardChanges() => Discard();

    /// <summary>„Aus dem Team nehmen“ nach der Rückfrage.</summary>
    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (Card is { } card)
        {
            await card.RemoveAsync().ConfigureAwait(true);
        }
    }

    partial void OnSearchChanged(string value) => Filter();

    partial void OnShowFormerChanged(bool value) => Filter();

    partial void OnSelectedEntryChanged(TeamEntry? oldValue, TeamEntry? newValue)
    {
        if (_reverting || newValue is null)
        {
            return;
        }

        // Die Prüfung hängt an der offenen Karte: hat die Suche ihre Zeile weggefiltert, ist oldValue leer und die Eingabe trotzdem da.
        if (Card is { } open && HasUnsavedChanges)
        {
            if (open.Entry.SameAs(newValue))
            {
                return;
            }

            // Erst fragen, dann wechseln: die Liste bleibt bei der alten Person, bis die Antwort da ist.
            ReturnTo(oldValue);
            _ = LeaveAsync(newValue);
            return;
        }

        Show(newValue);
    }

    private async Task LeaveAsync(TeamEntry target)
    {
        if (await UnsavedChanges.ConfirmLeaveAsync(Dialogs, this).ConfigureAwait(true))
        {
            Select(target);
        }
        else
        {
            _pending = null;
        }
    }

    /// <summary>Lädt alle Listen und baut die Zeilen neu; die gewählte Zeile bleibt gewählt.</summary>
    private async Task<bool> RefreshAsync()
    {
        var employees = await _api.GetAsync<List<EmployeeDto>>("api/employees").ConfigureAwait(true);
        var qualifications = await _api.GetAsync<List<QualificationDto>>("api/qualifications").ConfigureAwait(true);
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        var former = CanShowFormer ? await _api.GetAsync<List<EmployeeDto>>("api/employees/former").ConfigureAwait(true) : new([], null, 0);
        var members = CanStaff ? await _api.GetAsync<List<StaffMemberDto>>("api/staff").ConfigureAwait(true) : new([], null, 0);
        var invitations = CanStaff && !_context!.Standalone
            ? await _api.GetAsync<List<InvitationDto>>("api/staff/invitations").ConfigureAwait(true)
            : new([], null, 0);
        if (employees.Value is null || qualifications.Value is null || locations.Value is null || former.Value is null || members.Value is null
            || invitations.Value is null)
        {
            LoadError = employees.Error ?? qualifications.Error ?? locations.Error ?? former.Error ?? members.Error ?? invitations.Error
                ?? "Das Team wurde nicht geladen.";
            return false;
        }

        _context!.Data = new TeamData(employees.Value, former.Value, qualifications.Value, locations.Value, members.Value, invitations.Value);
        if (_context.CanHrFile && (await _api.GetAsync<List<StaffFlagsDto>>("api/staff-regulations/flags").ConfigureAwait(true)).Value is { } flags)
        {
            _context.Flags = flags.ToDictionary(f => f.EmployeeId);
        }

        _all = [.. BuildEntries(_context.Data).Select(WithFlags)];
        Filter();
        return true;
    }

    /// <summary>
    /// Die Liste sagt nur, welche Arten es gibt; die Einträge stehen oben auf der Karte. Warnungen der Zuständigkeiten (Zertifikat fehlt oder ist abgelaufen) stehen ausgeschrieben vorn,
    /// mit „Warnung:“ im Text, damit auch der Screenreader und wer Farben nicht unterscheidet sie bemerkt.
    /// </summary>
    private TeamEntry WithFlags(TeamEntry entry)
    {
        if (entry.EmployeeId is not { } id || !_context!.Flags.TryGetValue(id, out var flags))
        {
            return entry;
        }

        var duties = flags.Duties ?? [];
        var warnings = duties.Where(d => d.Warning is not null).Select(d => $"Warnung: {d.Text} – {d.Warning}");
        var kinds = new[] { (flags.Notes.Count, "Hinweis"), (flags.Rules.Count, "Sonderregelung"), (flags.Powers.Count, "Vollmacht"), (duties.Count, "Zuständigkeit") };
        return entry with
        {
            FlagText = string.Join(" · ", warnings.Concat(kinds.Where(k => k.Item1 > 0).Select(k => k.Item2))),
            HasFlagWarning = duties.Any(d => d.Warning is not null),
        };
    }

    /// <summary>
    /// Team und Zugänge (eine Zeile je Person, ein Zugang ohne Person für sich), dann offene Einladungen ohne Person, dann Ausgeschiedene, je alphabetisch;
    /// am Ende „Person anlegen“ und „Person einladen“ bzw. „Zugang anlegen“.
    /// </summary>
    private List<TeamEntry> BuildEntries(TeamData data)
    {
        var now = _context!.Now;
        var rows = new List<(int Group, TeamEntry Entry)>();
        foreach (var employee in data.Employees)
        {
            var member = data.Members.FirstOrDefault(m => m.EmployeeId == employee.Id);
            var invitation = member is null ? data.Invitations.FirstOrDefault(i => i.EmployeeId == employee.Id) : null;
            var subtitle = member is not null ? StatusOf(member, now)
                : invitation is not null ? $"{RoleChoice.LabelOf(invitation.Role)} · eingeladen"
                : CanStaff ? "ohne Zugang" : "";
            rows.Add((0, new TeamEntry(TeamEntryKind.Person, employee.DisplayName, subtitle, employee.Id, member?.UserId, invitation?.Id,
                member?.Email ?? invitation?.Email)));
        }

        var team = data.Employees.Select(e => e.Id).ToHashSet();
        foreach (var member in data.Members.Where(m => m.EmployeeId is not { } id || !team.Contains(id)))
        {
            rows.Add((0, new TeamEntry(TeamEntryKind.Account, member.DisplayName, StatusOf(member, now), UserId: member.UserId, Email: member.Email)));
        }

        foreach (var invitation in data.Invitations.Where(i => i.EmployeeId is not { } id || !team.Contains(id)))
        {
            rows.Add((1, new TeamEntry(TeamEntryKind.Invitation, invitation.DisplayName, $"{RoleChoice.LabelOf(invitation.Role)} · eingeladen",
                InvitationId: invitation.Id, Email: invitation.Email)));
        }

        rows.AddRange(data.Former.Select(e => (2, new TeamEntry(TeamEntryKind.Former, e.DisplayName, "ausgeschieden", e.Id))));
        var entries = rows.OrderBy(r => r.Group).ThenBy(r => r.Entry.Title, StringComparer.CurrentCulture).ThenBy(r => r.Entry.Kind)
            .Select(r => r.Entry).ToList();
        if (CanShowFormer)
        {
            entries.Add(new TeamEntry(TeamEntryKind.NewPerson, "Person anlegen", ""));
        }

        if (CanStaff)
        {
            entries.Add(new TeamEntry(TeamEntryKind.NewAccess, _context.Standalone ? "Zugang anlegen" : "Person einladen", ""));
        }

        return entries;
    }

    /// <summary>„Rolle · Status“ wie die Zeile in <c>StaffPage.tsx</c>; dazu „aktiv“ ohne Befristung.</summary>
    private string StatusOf(StaffMemberDto member, DateTimeOffset now) => $"{RoleChoice.LabelOf(member.Role)} · {(!member.IsActive ? "gesperrt" : member.ExpiresAt switch
    {
        null => "aktiv",
        { } end when end <= now => "abgelaufen",
        { } end => $"{(end - now <= EndsSoon ? "endet bald, " : "")}bis {_context!.DateText(end.AddTicks(-1))}",
    })}";

    /// <summary>Suche über Name und E-Mail, „Ausgeschiedene zeigen“; die Neu-Zeilen bleiben immer.</summary>
    private void Filter()
    {
        var search = Search.Trim();
        var entries = _all.Where(e => e.IsNew || ((ShowFormer || e.Kind != TeamEntryKind.Former) && (search.Length == 0
            || e.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || e.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) == true))).ToList();
        var selected = SelectedEntry ?? Card?.Entry;
        _reverting = true;
        Entries = entries;
        SelectedEntry = selected is null ? null : entries.FirstOrDefault(e => e.SameAs(selected));
        _reverting = false;
    }

    /// <summary>Die Zeile einer Person im Team, auch wenn sie gerade weggefiltert ist (Suche, Ausgeschiedene).</summary>
    private TeamEntry? EntryOf(Guid employeeId)
    {
        if (Entries.FirstOrDefault(e => e.EmployeeId == employeeId && e.Kind == TeamEntryKind.Person) is { } shown)
        {
            return shown;
        }

        if (!_all.Any(e => e.EmployeeId == employeeId && e.Kind == TeamEntryKind.Person))
        {
            return null;
        }

        _reverting = true;
        Search = "";
        _reverting = false;
        return Entries.FirstOrDefault(e => e.EmployeeId == employeeId && e.Kind == TeamEntryKind.Person);
    }

    /// <summary>Ein Sprungziel, das nicht (mehr) im Team steht: nichts an der offenen Person ändern, nur melden.</summary>
    private void RejectPending()
    {
        _pending = null;
        Notice = NotInTeam;
    }

    private void Select(TeamEntry? entry)
    {
        _reverting = true;
        SelectedEntry = entry;
        _reverting = false;
        if (entry is not null)
        {
            Show(entry);
        }
    }

    private void Show(TeamEntry entry)
    {
        if (Card is not null)
        {
            Card.PropertyChanged -= OnCardChanged;
            Card.DataChanged -= OnCardDataChanged;
            Card.WeekPlanRequested -= OnCardWeekPlan;
            if (Card.Roster is { } oldRoster)
            {
                oldRoster.Changed -= OnCardRosterChanged;
            }

            if (Card.Times is { } oldTimes)
            {
                oldTimes.Changed -= OnCardTimesChanged;
            }
        }

        Notice = null;
        var data = _context!.Data;
        var employee = entry.EmployeeId is { } id ? data.Employees.Concat(data.Former).FirstOrDefault(e => e.Id == id) : null;
        var member = entry.UserId is { } user ? data.Members.FirstOrDefault(m => m.UserId == user) : null;
        var invitation = entry.InvitationId is { } invited ? data.Invitations.FirstOrDefault(i => i.Id == invited) : null;
        _context.Dialogs = Dialogs;
        Card = new PersonCardViewModel(_context, entry, employee, member, invitation) { Dialogs = Dialogs };
        Card.PropertyChanged += OnCardChanged;
        Card.DataChanged += OnCardDataChanged;
        Card.WeekPlanRequested += OnCardWeekPlan;
        if (Card.Roster is { } roster)
        {
            roster.Changed += OnCardRosterChanged;
        }

        if (Card.Times is { } times)
        {
            times.Changed += OnCardTimesChanged;
        }

        if (_pending?.Id is { } target && target == entry.EmployeeId)
        {
            ApplyPending();
        }
    }

    private void ApplyPending()
    {
        if (_pending is not { } pending || Card is null)
        {
            return;
        }

        _pending = null;
        if (pending.Section is not null)
        {
            Card.SelectTab(pending.Section);
        }

        if (pending.Field is PersonDetailsViewModel.NameField && Card.Details is { } details)
        {
            Card.SelectTab(PersonCardViewModel.DetailsTab);
            details.FocusTarget = PersonDetailsViewModel.NameField;
        }
    }

    /// <summary>Der Sprung zeigt die Woche und den Standort, an dem die Karte den Entwurf gespeichert hat.</summary>
    private void OnCardWeekPlan()
    {
        if (Card?.Roster is { } roster)
        {
            Plan?.ShowWeek(roster.SelectedLocation?.Id, roster.WeekStart);
        }

        _ = SwitchToAsync(RosterMode);
    }

    private void OnPlanChanged()
    {
        Card?.Roster?.MarkStale();
        Card?.Times?.MarkStale();
        Times?.MarkStale();
        RosterChanged?.Invoke();
    }

    private void OnCardRosterChanged()
    {
        Plan?.MarkStale();
        Card?.Times?.MarkStale();
        Times?.MarkStale();
        RosterChanged?.Invoke();
    }

    private void OnCardTimesChanged() => Times?.MarkStale();

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is { } name && Forwarded.Contains(name))
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>
    /// Die Karte hat gespeichert, eingeladen, angelegt, zurückgezogen oder die Person aus dem Team genommen: Liste neu laden. Bleibt die Karte, zeigt die Liste
    /// die neue Zeile und die Karte den neuen Stand; hängt der Zugang jetzt an einer anderen Zeile, öffnet die neu; sonst öffnet die Zeile derselben Person (neu angelegt)
    /// oder die erste. Wurde während des Ladens schon eine andere Person gewählt, bleibt sie offen und nur die Liste ist neu.
    /// </summary>
    private async Task OnCardDataChanged(PersonCardViewModel card, bool keep)
    {
        var status = card.StatusText;
        var loaded = await RefreshAsync().ConfigureAwait(true);
        Saved?.Invoke();
        if (!loaded || !ReferenceEquals(Card, card))
        {
            return;
        }

        if (keep)
        {
            var current = _all.FirstOrDefault(e => e.SameAs(card.Entry))
                ?? (card.EmployeeId is { } id ? _all.FirstOrDefault(e => e.EmployeeId == id) : null)
                ?? (card.Entry.UserId is { } user ? _all.FirstOrDefault(e => e.UserId == user) : null);
            if (current is not null && (current.Kind != card.Entry.Kind || current.UserId != card.Entry.UserId))
            {
                Reopen(current, card.Access?.CreatedText ?? status);
                return;
            }

            _reverting = true;
            SelectedEntry = current is null ? SelectedEntry : Entries.FirstOrDefault(e => e.SameAs(current));
            _reverting = false;
            if (current is not null)
            {
                card.Refreshed(current);
            }

            OnPropertyChanged(nameof(Title));
            return;
        }

        var next = card.EmployeeId is { } employee ? Entries.FirstOrDefault(e => e.EmployeeId == employee && e.Kind == TeamEntryKind.Person) : null;
        Select(next ?? FirstEntry);
        if (next is not null && Card is not null)
        {
            Card.StatusText = status;
        }
    }

    /// <summary>Die Zeile des Zugangs hat gewechselt (eine Person zugeordnet oder gelöst, im Einzelplatz angelegt): die Karte passt nicht mehr und öffnet neu.</summary>
    private void Reopen(TeamEntry entry, string? status)
    {
        if (!Entries.Any(e => e.SameAs(entry)))
        {
            _reverting = true;
            Search = "";
            _reverting = false;
        }

        Select(Entries.First(e => e.SameAs(entry)));
        Card!.StatusText = status;
    }

    /// <summary>Zurück zur Person, die gefragt hat; gepostet, damit die Liste den Wert übernimmt (wie in der Verwaltung, Slice 93).</summary>
    private void ReturnTo(TeamEntry? entry)
    {
        void Return()
        {
            _reverting = true;
            SelectedEntry = entry;
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
}
