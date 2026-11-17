using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Personenkarte im Bereich „Team“ (R05 Slice 98, Ä250, ADR 0098): Kopf mit Name und Status, darunter höchstens vier Bereiche: „Übersicht“ (Kennzahl-Kacheln,
/// <see cref="PersonOverviewViewModel"/>), „Profil“ (Stammdaten, Zugang und Rolle, Qualifikationen, Zuständigkeiten), „Arbeitszeit“ (Dienstplan und Abwesenheit mit Urlaubskonto,
/// Regelarbeitszeit, Zeiten) und „Lohn und Akte“ (Lohn, Personalakte, Regelungen und Vollmachten). Ein Abschnitt steht nur da, wenn Recht und Art der Zeile ihn erlauben; ein Bereich ohne Abschnitt fehlt.
/// Der Verlauf ist eine Seitenleiste (<see cref="IsHistoryOpen"/>). Die bisherigen Reiter-Ids gelten als Abschnitts-Ids weiter (<see cref="SelectTab"/>).
/// Ein „Speichern“ für die Karte: geänderte Stammdaten und Qualifikationen gehen zusammen an <c>api/employees</c>, danach ein geänderter Zugang an <c>api/staff/{userId}</c>;
/// der erste Fehler hält an, die Meldung der API steht über der Leiste und nennt, was schon gespeichert ist. Dienstplan, Zeiten, Personalakte, Regelungen und Zuständigkeiten haben
/// ihre eigene Leiste; ihre offenen Änderungen zählen zu den ungespeicherten der Karte. Der Lohn (<see cref="PersonPayrollViewModel"/>) hat die Personalnummer, die mit der Leiste
/// der Karte gespeichert wird, und die Monatswerte der Person.
/// </summary>
public sealed partial class PersonCardViewModel : ViewModelBase, IEditableArea
{
    /// <summary>Bereich „Übersicht“: Kennzahlen der Person, nur für eine gespeicherte Person im Team.</summary>
    public const string OverviewArea = "uebersicht";

    /// <summary>Bereich „Profil“: Stammdaten, Zugang und Rolle, Qualifikationen, Zuständigkeiten.</summary>
    public const string ProfileArea = "profil";

    /// <summary>Bereich „Arbeitszeit“: Dienstplan und Abwesenheit, Zeiten.</summary>
    public const string WorkTimeArea = "arbeitszeit";

    /// <summary>Bereich „Lohn und Akte“: Lohn, Personalakte, Regelungen und Vollmachten.</summary>
    public const string PayAndFileArea = "lohnakte";

    // Abschnitts-Ids (die früheren Reiter); Sprungziele von außen nennen sie und müssen die Bereiche nicht kennen.
    public const string DetailsTab = "stammdaten";
    public const string AccessTab = "zugang";
    public const string QualificationsTab = "qualifikationen";
    public const string RosterTab = "dienstplan";

    /// <summary>Die Regelarbeitszeit der Person (Wochenraster), im Bereich „Arbeitszeit“ (Nachtrag zu ADR 0098, P5).</summary>
    public const string WorkingHoursTab = "regelarbeitszeit";
    public const string TimesTab = "zeiten";
    public const string PayrollTab = "lohn";
    public const string HrFileTab = "personalakte";
    public const string RegulationsTab = "regelungen";
    public const string DutiesTab = "zustaendigkeiten";
    /// <summary>Kein Abschnitt, sondern die Seitenleiste „Verlauf“.</summary>
    public const string HistoryTab = "verlauf";

    /// <summary>Die Abschnitte in der Reihenfolge der Karte und der Bereich, in dem jeder steht.</summary>
    private static readonly (string Section, string Area)[] Layout =
    [
        (DetailsTab, ProfileArea), (AccessTab, ProfileArea), (QualificationsTab, ProfileArea), (DutiesTab, ProfileArea),
        (RosterTab, WorkTimeArea), (WorkingHoursTab, WorkTimeArea), (TimesTab, WorkTimeArea),
        (PayrollTab, PayAndFileArea), (HrFileTab, PayAndFileArea), (RegulationsTab, PayAndFileArea),
    ];

    private static readonly (string Id, string Label)[] Areas =
        [(OverviewArea, "Übersicht"), (ProfileArea, "Profil"), (WorkTimeArea, "Arbeitszeit"), (PayAndFileArea, "Lohn und Akte")];

    private const string EmployeePartSaved = "Stammdaten und Qualifikationen sind gespeichert.";

    private readonly TeamContext _context;
    private EmployeeDto? _employee;
    private string _detailsState = "";
    private bool _isSaving;

    public PersonCardViewModel(TeamContext context, TeamEntry entry, EmployeeDto? employee, StaffMemberDto? member, InvitationDto? invitation)
    {
        _context = context;
        _employee = employee;
        Entry = entry;
        Member = member;
        var person = entry.Kind is TeamEntryKind.Person or TeamEntryKind.Former or TeamEntryKind.NewPerson;
        if (entry.Kind == TeamEntryKind.Person && employee is not null && (context.CanViewPersonRoster || context.CanHrFile))
        {
            Overview = new PersonOverviewViewModel(context, employee.Id, OverviewJump);
        }

        if (person && context.CanCatalog)
        {
            Details = new PersonDetailsViewModel(employee, context.Data.Locations, Changed, IsReadOnly);
            _detailsState = Details.State();
        }

        if (entry.Kind is TeamEntryKind.Person or TeamEntryKind.NewPerson && context.CanCatalog)
        {
            Qualifications = new PersonQualificationsViewModel(context.Api, context.Data.Qualifications, employee?.QualificationIds ?? [], Changed);
            Qualifications.ListChanged += list => _context.Data = _context.Data with { Qualifications = list };
        }

        if (AccessModeOf(entry.Kind, member, invitation) is { } mode && context.CanStaff)
        {
            Access = new PersonAccessViewModel(context, mode, member, invitation, employee, Changed);
            Access.DataChanged += keep => DataChanged is { } changed ? changed(this, keep) : Task.CompletedTask;
            Access.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PersonAccessViewModel.IsActive))
                {
                    OnPropertyChanged(nameof(ToggleAccessLabel));
                }
            };
        }

        var home = employee?.WorkingHours.Select(h => h.LocationId).FirstOrDefault(id => context.Data.Locations.Any(l => l.Id == id));
        if (entry.Kind == TeamEntryKind.Person && employee is not null && context.CanViewPersonRoster)
        {
            Roster = new RosterPlanViewModel(context, employee.Id, home == Guid.Empty ? null : home) { Dialogs = context.Dialogs };
            Roster.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RosterPlanViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(HasSaveBar));
                }
            };
            Roster.WeekPlanRequested += () => WeekPlanRequested?.Invoke();
            Leave = new LeaveAccountViewModel(context, employee.Id);
            Leave.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LeaveAccountViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            };
            Roster.Changed += () =>
            {
                _ = Leave.RefreshAsync();
                _ = Overview?.RefreshAsync();
            };
            Leave.Saved += () => _ = Overview?.RefreshAsync();
        }

        if (entry.Kind == TeamEntryKind.Person && employee is not null
            && (context.User.Rights.Contains(AccessRight.ManageLocation) || context.User.EmployeeId == employee.Id))
        {
            CalendarFile = CalendarFileFor(context, employee.Id);
        }

        if (entry.Kind == TeamEntryKind.Person && employee is not null && context.CanViewTimes)
        {
            Times = new TimesheetViewModel(context, employee.Id, home == Guid.Empty ? null : home) { Dialogs = context.Dialogs };
            Times.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TimesheetViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(HasSaveBar));
                }
            };
            Times.Changed += () => _ = Overview?.RefreshAsync();
        }

        if (entry.Kind == TeamEntryKind.Person && employee is not null && context.CanViewPayroll)
        {
            Payroll = new PersonPayrollViewModel(context, employee.Id, Changed);
        }

        if (entry.Kind is TeamEntryKind.Person or TeamEntryKind.Former && employee is not null && context.CanHrFile)
        {
            HrFile = new PersonHrFileViewModel(context, employee.Id, employee.DisplayName, IsReadOnly);
            HrFile.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PersonHrFileViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(HasSaveBar));
                }
            };
            Regulations = new PersonRegulationsViewModel(context, employee.Id, IsReadOnly);
            Regulations.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PersonRegulationsViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(HasSaveBar));
                }
            };
            Regulations.Changed += () => DataChanged?.Invoke(this, true) ?? Task.CompletedTask;
            RegulationsHistory = new HistorySectionViewModel(context.Api, $"api/employees/{employee.Id}/regulations/history", context.Zone,
                "Wer Hinweise, Sonderregelungen und Vollmachten wann geändert hat, ohne Inhalte.", "Regelungen und Vollmachten");
            HrHistory = new HistorySectionViewModel(context.Api, $"api/employees/{employee.Id}/hr-file/history", context.Zone, "Wer die Personalakte wann geändert hat, ohne Inhalt",
                "Personalakte");
            Duties = new PersonDutiesViewModel(context, employee.Id, IsReadOnly);
            Duties.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PersonDutiesViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(HasSaveBar));
                }
            };
            Duties.Changed += () => DataChanged?.Invoke(this, true) ?? Task.CompletedTask;
            DutiesHistory = new HistorySectionViewModel(context.Api, $"api/employees/{employee.Id}/duties/history", context.Zone,
                "Wer Zuständigkeiten, Zertifikate und Scans wann geändert hat, ohne Inhalte.", "Zuständigkeiten und Zertifikate");
        }

        if (context.IsOwnerLevel)
        {
            AccessHistory = member is null ? null
                : new HistorySectionViewModel(context.Api, $"api/staff/{member.UserId}/history", context.Zone, "Wer Zugang, Rolle und Standorte wann geändert hat", "Zugang und Rolle");
            RosterHistory = employee is null || !person ? null
                : new HistorySectionViewModel(context.Api, $"api/employees/{employee.Id}/roster-history", context.Zone,
                    "Wer Dienstplan und Abwesenheiten wann geändert hat", "Dienstplan und Abwesenheiten");
        }

        HistorySections = [.. new[] { AccessHistory, RosterHistory, HrHistory, RegulationsHistory, DutiesHistory }.OfType<HistorySectionViewModel>()];
        Sections = [.. Layout.Where(l => SectionExists(l.Section)).Select(l => l.Section)];
        Tabs = [.. Areas.Where(a => a.Id == OverviewArea ? Overview is not null : Layout.Any(l => l.Area == a.Id && HasSection(l.Section)))
            .Select(a => new CardTab(a.Id, a.Label, SelectTab))];
        SelectTab(null);
    }

    /// <summary>Die Zeile der Liste; nach dem Neuladen der neue Stand (<see cref="Refreshed"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Initials), nameof(HeadLine), nameof(HasHeadLine), nameof(PillText), nameof(HasPill), nameof(PillIsOk), nameof(PillIsWarning), nameof(PillIsCritical),
        nameof(PillIsNeutral))]
    public partial TeamEntry Entry { get; private set; }

    public StaffMemberDto? Member { get; }

    /// <summary>Id der Person im Team; leer ohne Person (Zugang, Einladung, neu).</summary>
    public Guid? EmployeeId => _employee?.Id;

    /// <summary>Überschrift und Name in der Rückfrage.</summary>
    public string Title => Entry.Kind switch
    {
        TeamEntryKind.NewPerson => "Neue Person",
        TeamEntryKind.NewAccess => _context.Standalone ? "Zugang anlegen" : "Person einladen",
        _ => _employee?.DisplayName ?? Entry.Title,
    };

    /// <summary>Zwei Buchstaben für den Kreis vor dem Namen im Kopf.</summary>
    public string Initials => DayLayout.Initials(Title);

    /// <summary>„Rolle · Standort“ unter dem Namen: die Rolle des Zugangs und der Standort, an dem die Person arbeitet.</summary>
    public string HeadLine => string.Join(" · ", new[] { Entry.Role, HomeLocationName }.Where(part => !string.IsNullOrEmpty(part)));

    public bool HasHeadLine => HeadLine.Length > 0 && !Entry.IsNew;

    private string? HomeLocationName => _employee?.WorkingHours.Select(h => _context.Data.Locations.FirstOrDefault(l => l.Id == h.LocationId)?.Name).FirstOrDefault(name => name is not null);

    /// <summary>
    /// Das Kennzeichen im Kopf: die Abweichung der Liste („Gesperrt“, „bis 19.12.2026“, „Eingeladen“), „Ohne Zugang“ (in der Liste ein Filter statt eines Kennzeichens), sonst
    /// „Zugang aktiv“ bei einem Zugang. Eine neue Zeile und der eigene Zugang einer Person ohne Karte haben keines.
    /// </summary>
    public string? PillText => Entry.IsNew ? null : Entry.Badge ?? (Member is not null ? "Zugang aktiv" : Entry.HasNoAccess ? "Ohne Zugang" : null);

    public bool HasPill => PillText is not null;

    public bool PillIsOk => HasPill && Entry.Badge is null && !Entry.HasNoAccess;

    public bool PillIsWarning => HasPill && Entry.BadgeIsWarning;

    public bool PillIsCritical => HasPill && Entry.BadgeIsCritical;

    public bool PillIsNeutral => HasPill && (Entry.BadgeIsNeutral || (Entry.Badge is null && Entry.HasNoAccess));

    /// <summary>„Person einladen“ trägt seine Überschrift mit Hilfe im Reiter; die Karte wiederholt sie nicht.</summary>
    public bool ShowsHeader => Entry.Kind != TeamEntryKind.NewAccess;

    /// <summary>Ein einzelner Bereich braucht keine Schiene.</summary>
    public bool ShowsTabs => Tabs.Count > 1;

    /// <summary>Ausgeschiedene Personen sind nur lesbar.</summary>
    public bool IsReadOnly => Entry.Kind == TeamEntryKind.Former;

    /// <summary>Die Bereiche der Schiene (Übersicht, Profil, Arbeitszeit, Lohn und Akte), soweit die Karte Abschnitte dafür hat.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTabs))]
    public partial IReadOnlyList<CardTab> Tabs { get; private set; } = [];

    /// <summary>Die Abschnitts-Ids der Karte in ihrer Reihenfolge (die früheren Reiter, ohne „verlauf“).</summary>
    public IReadOnlyList<string> Sections { get; }

    public bool HasSection(string id) => Sections.Contains(id);

    /// <summary>Der gewählte Bereich.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsOverview), nameof(ShowsDetails), nameof(ShowsAccess), nameof(ShowsQualifications), nameof(ShowsRoster), nameof(ShowsWorkingHours), nameof(ShowsTimes), nameof(ShowsPayroll),
        nameof(ShowsHrFile), nameof(ShowsRegulations), nameof(ShowsDuties), nameof(HasSaveBar))]
    public partial string SelectedTab { get; private set; } = ProfileArea;

    /// <summary>Der zuletzt angesprungene Abschnitt; die Ansicht rollt ihn ins Bild. Leer, sobald ein Bereich ohne Abschnitt gewählt wird.</summary>
    [ObservableProperty]
    public partial string? FocusedSection { get; private set; }

    /// <summary>Ein Abschnitt wurde angesprungen (auch derselbe ein zweites Mal); die Ansicht rollt ihn ins Bild und setzt den Fokus hinein.</summary>
    public event Action<string>? SectionRequested;

    public bool ShowsOverview => Overview is not null && SelectedTab == OverviewArea;

    public bool ShowsDetails => Shows(DetailsTab);

    public bool ShowsAccess => Shows(AccessTab);

    public bool ShowsQualifications => Shows(QualificationsTab);

    public bool ShowsRoster => Shows(RosterTab);

    public bool ShowsWorkingHours => Shows(WorkingHoursTab);

    public bool ShowsTimes => Shows(TimesTab);

    public bool ShowsPayroll => Shows(PayrollTab);

    public bool ShowsHrFile => Shows(HrFileTab);

    public bool ShowsRegulations => Shows(RegulationsTab);

    public bool ShowsDuties => Shows(DutiesTab);

    /// <summary>Kennzahlen der Person (ADR 0098); nur für eine gespeicherte Person im Team mit einem Recht auf Dienstplan, Zeiten oder Personalakte.</summary>
    public PersonOverviewViewModel? Overview { get; }

    /// <summary>Die Verläufe der Karte in fester Reihenfolge, für die Seitenleiste „Verlauf“.</summary>
    public IReadOnlyList<HistorySectionViewModel> HistorySections { get; }

    public bool HasHistory => HistorySections.Count > 0;

    /// <summary>Die Seitenleiste „Verlauf“ ist offen.</summary>
    [ObservableProperty]
    public partial bool IsHistoryOpen { get; set; }

    [RelayCommand]
    private void ToggleHistory() => IsHistoryOpen = !IsHistoryOpen;

    [ObservableProperty]
    public partial PersonDetailsViewModel? Details { get; private set; }

    [ObservableProperty]
    public partial PersonQualificationsViewModel? Qualifications { get; private set; }

    [ObservableProperty]
    public partial PersonAccessViewModel? Access { get; private set; }

    /// <summary>Dienstplan und Abwesenheit der Person (R05 Slice 99); nur für eine gespeicherte Person im Team, mit dem Recht auf den Dienstplan.</summary>
    public RosterPlanViewModel? Roster { get; }

    /// <summary>Urlaubskonto der Person (ADR 0097), im selben Reiter unter der Woche.</summary>
    public LeaveAccountViewModel? Leave { get; }

    public bool HasLeave => Leave is not null;

    /// <summary>Tage, die die Kalenderdatei einer Person ab heute umfasst.</summary>
    public const int CalendarFileDays = 90;

    /// <summary>
    /// „Kalenderdatei …“ im Dienstplan (Export/Import, Aufgabe C): Dienste und Termine der nächsten 90 Tage für den privaten Kalender, ohne Namen der Gäste; nur für die Leitung
    /// und die Person selbst. Die Ansicht reicht den Speicherdialog des Fensters herein.
    /// </summary>
    public ExportViewModel? CalendarFile { get; }

    public bool HasCalendarFile => CalendarFile is not null;

    /// <summary>„Gespeichert: Kalender_Ben.ics“ unter dem Knopf, bis zum nächsten Speichern.</summary>
    [ObservableProperty]
    public partial string? CalendarFileSaved { get; private set; }

    public ExportOption? CalendarFileOption => CalendarFile?.Options[0];

    private ExportViewModel CalendarFileFor(TeamContext context, Guid employeeId)
    {
        var today = DateOnly.FromDateTime(context.Time.GetLocalNow().DateTime);
        var path = string.Create(CultureInfo.InvariantCulture,
            $"api/employees/{employeeId}/ics?from={today:yyyy-MM-dd}&until={today.AddDays(CalendarFileDays):yyyy-MM-dd}");
        return new ExportViewModel(context.Api, [ExportOption.For(ExportFormat.Ics, () => path, "Kalenderdatei …")])
        {
            Saved = text => CalendarFileSaved = text,
            Failed = text => Error = text,
        };
    }

    /// <summary>Die Zeiten der Person (R05 Slice 99); nur für eine gespeicherte Person im Team, mit dem Recht auf die Zeiten.</summary>
    public TimesheetViewModel? Times { get; }

    /// <summary>Personalnummer und Monatswerte der Person (R05 Slice 100); nur für eine gespeicherte Person im Team, mit dem Recht auf den Lohn.</summary>
    public PersonPayrollViewModel? Payroll { get; }

    /// <summary>Personalakte der Person (R05 Slice 110); nur für Inhaber und Manager, bei Ausgeschiedenen nur lesbar.</summary>
    public PersonHrFileViewModel? HrFile { get; }

    /// <summary>Verlauf der Personalakte ohne Inhalt (nur, wer die Akte sieht).</summary>
    public HistorySectionViewModel? HrHistory { get; }

    /// <summary>Reiter „Regelungen und Vollmachten“ (R05 Slice 111); nur für Inhaberin und Manager.</summary>
    public PersonRegulationsViewModel? Regulations { get; }

    public HistorySectionViewModel? RegulationsHistory { get; }

    /// <summary>Reiter „Zuständigkeiten“ (R05 Slice 117); nur für Inhaberin und Manager.</summary>
    public PersonDutiesViewModel? Duties { get; }

    public HistorySectionViewModel? DutiesHistory { get; }

    /// <summary>
    /// Kennzeichen oben auf der Karte: nur heute gültige Einträge; Warnungen der Zuständigkeiten (Zertifikat fehlt oder ist abgelaufen) stehen zuerst, mit „Warnung:“ im Text und Warnsymbol.
    /// </summary>
    public IReadOnlyList<FlagLine> FlagItems => _employee is { } person && _context.Flags.TryGetValue(person.Id, out var flags)
        ?
        [
            .. (flags.Duties ?? []).Where(d => d.Warning is not null).Select(d => new FlagLine($"Warnung: {d.Text} – {d.Warning}", true)),
            .. flags.Notes.Select(n => new FlagLine($"Hinweis: {n}", false)),
            .. flags.Rules.Select(r => new FlagLine($"Sonderregelung: {r}", false)),
            .. flags.Powers.Select(p => new FlagLine($"Vollmacht: {p}", false)),
            .. (flags.Duties ?? []).Where(d => d.Warning is null).Select(d => new FlagLine($"Zuständigkeit: {d.Text}", false)),
        ]
        : [];

    /// <summary>Kennzeichen oben auf der Karte: nur heute gültige Einträge.</summary>
    public IReadOnlyList<string> FlagLines => [.. FlagItems.Select(f => f.Text)];

    public bool HasFlags => FlagLines.Count > 0;

    /// <summary>Verlauf von Zugang, Rolle und Standorten (nur Inhaber und Manager, nur mit Zugang).</summary>
    public HistorySectionViewModel? AccessHistory { get; }

    /// <summary>Verlauf von Dienstplan und Abwesenheiten (nur Inhaber und Manager, nur für eine Person im Team).</summary>
    public HistorySectionViewModel? RosterHistory { get; }

    /// <summary>Fehler der API beim Speichern, über der Leiste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; set; }

    /// <summary>
    /// Die Leiste der Karte steht, wenn der gewählte Bereich einen Abschnitt zeigt, der mit ihr gespeichert wird (Stammdaten, Qualifikationen, Zugang eines Mitglieds, Lohn zum
    /// Bearbeiten), oder wenn es in solchen Abschnitten ungespeicherte Änderungen gibt. Dienstplan, Zeiten, Personalakte, Regelungen und Zuständigkeiten haben ihre eigene Leiste.
    /// </summary>
    public bool HasSaveBar => !IsReadOnly && (Details is not null || Qualifications is not null || Access is { IsMember: true } || Payroll is { IsEditable: true })
        && (ShowsDetails || ShowsQualifications || (ShowsAccess && Access is { IsMember: true }) || (ShowsPayroll && Payroll is { IsEditable: true }) || OtherChanges);

    /// <summary>„Aus dem Team nehmen“ für eine gespeicherte Person im Team.</summary>
    public bool CanRemove => Entry.Kind == TeamEntryKind.Person && _context.CanCatalog;

    /// <summary>„Zugang sperren“ bzw. „Zugang freigeben“ im Menü „⋯“: für den Zugang eines anderen Mitglieds, solange die Karte nicht nur lesbar ist.</summary>
    public bool CanToggleAccess => !IsReadOnly && Access is { IsMember: true };

    /// <summary>Der Eintrag im Menü ändert den Schalter „Zugang aktiv“; gesperrt wird erst mit „Speichern“, wie alles auf der Karte.</summary>
    public string ToggleAccessLabel => Access is { IsActive: false } ? "Zugang freigeben" : "Zugang sperren";

    /// <summary>Das Menü „⋯“ im Kopf hat einen Eintrag, sonst steht der Knopf nicht da.</summary>
    public bool HasMoreMenu => CanToggleAccess || CanRemove;

    /// <summary>Die Befehle für <see cref="Controls.MoreMenuButton"/>: ein Eintrag steht nur, wenn sein Befehl gesetzt ist.</summary>
    public IRelayCommand? ToggleAccessMenu => CanToggleAccess ? ToggleAccessCommand : null;

    public IAsyncRelayCommand? RemoveMenu => CanRemove ? RemoveFromTeamCommand : null;

    [RelayCommand]
    private void ToggleAccess()
    {
        if (Access is { IsMember: true } access)
        {
            access.IsActive = !access.IsActive;
            OnPropertyChanged(nameof(ToggleAccessLabel));
        }
    }

    [RelayCommand]
    private async Task RemoveFromTeamAsync() => await RemoveAsync().ConfigureAwait(true);

    private bool DetailsChanged => Details is not null && !IsReadOnly && Details.State() != _detailsState;

    /// <summary>Änderungen an Stammdaten, Qualifikationen oder Zugang; der offene Tag des Dienstplans und der Zeiten hat seine eigene Leiste und zählt nicht dazu.</summary>
    private bool OtherChanges => DetailsChanged || Qualifications?.HasUnsavedChanges == true || Access?.HasUnsavedChanges == true || Payroll?.HasUnsavedChanges == true;

    public bool HasUnsavedChanges => OtherChanges || Roster?.HasUnsavedChanges == true || Leave?.HasUnsavedChanges == true || Times?.HasUnsavedChanges == true || HrFile?.HasUnsavedChanges == true || Regulations?.HasUnsavedChanges == true
        || Duties?.HasUnsavedChanges == true;

    /// <summary>Die Karte hat auf dem Server etwas geändert; die Liste lädt neu. <c>true</c>: die Karte bleibt offen.</summary>
    public event Func<PersonCardViewModel, bool, Task>? DataChanged;

    /// <summary>Ein Tag wurde als Entwurf gespeichert und die Person will den Wochenplan zum Veröffentlichen sehen.</summary>
    public event Action? WeekPlanRequested;

    /// <summary>Die Kachel „Offene Anträge“ der Übersicht will die Ansicht „Anträge“ des Teams.</summary>
    public event Action? AbsenceRequestsRequested;

    /// <summary>
    /// Wählt einen Bereich (<see cref="OverviewArea"/> …) oder springt zu einem Abschnitt (<see cref="DetailsTab"/> …): der Bereich mit dem Abschnitt öffnet, <see cref="FocusedSection"/>
    /// nennt den Abschnitt. <see cref="HistoryTab"/> öffnet die Seitenleiste „Verlauf“ und lässt den Bereich, wie er ist. Was die Karte nicht hat, ersetzt der Startbereich
    /// (Übersicht, sonst der erste).
    /// </summary>
    public void SelectTab(string? id)
    {
        if (id == HistoryTab && HasHistory)
        {
            IsHistoryOpen = true;
            return;
        }

        var section = id is not null && HasSection(id) ? id : null;
        var area = section is not null ? AreaOf(section)
            : Tabs.FirstOrDefault(t => t.Id == id)?.Id ?? (Tabs.Count > 0 ? Tabs[0].Id : ProfileArea);
        foreach (var each in Tabs)
        {
            each.IsOn = each.Id == area;
        }

        SelectedTab = area;
        LoadArea(area);
        FocusedSection = section;
        if (section is not null)
        {
            SectionRequested?.Invoke(section);
        }
    }

    private static string AreaOf(string section) => Layout.First(l => l.Section == section).Area;

    private bool Shows(string section) => HasSection(section) && SelectedTab == AreaOf(section);

    private bool SectionExists(string section) => section switch
    {
        DetailsTab => Details is not null,
        AccessTab => Access is not null,
        QualificationsTab => Qualifications is not null,
        DutiesTab => Duties is not null,
        RosterTab => Roster is not null,
        WorkingHoursTab => Details is not null,
        TimesTab => Times is not null,
        PayrollTab => Payroll is not null,
        HrFileTab => HrFile is not null,
        _ => Regulations is not null,
    };

    /// <summary>Lädt, was der Bereich zeigt und noch nicht geladen ist.</summary>
    private void LoadArea(string area)
    {
        switch (area)
        {
            case OverviewArea:
                _ = Overview?.EnsureLoadedAsync();
                break;
            case ProfileArea:
                _ = Duties?.EnsureLoadedAsync();
                break;
            case WorkTimeArea:
                _ = Roster?.EnsureLoadedAsync();
                _ = Leave?.EnsureLoadedAsync();
                _ = Times?.EnsureLoadedAsync();
                break;
            case PayAndFileArea:
                _ = Payroll?.EnsureLoadedAsync();
                _ = HrFile?.EnsureLoadedAsync();
                _ = Regulations?.EnsureLoadedAsync();
                break;
        }
    }

    /// <summary>Klick auf eine Kachel der Übersicht: zum Abschnitt, bei „Offene Anträge“ zur Ansicht „Anträge“ des Teams.</summary>
    private void OverviewJump(string target)
    {
        if (target == PersonOverviewViewModel.RequestsTarget)
        {
            AbsenceRequestsRequested?.Invoke();
            return;
        }

        SelectTab(target);
    }

    /// <summary>Die Liste ist neu geladen und die Karte bleibt: Kopfzeile aus der neuen Zeile; eine eben gesendete Einladung zeigt der Reiter als offene Einladung.</summary>
    public void Refreshed(TeamEntry entry)
    {
        OnPropertyChanged(nameof(FlagLines));
        OnPropertyChanged(nameof(FlagItems));
        OnPropertyChanged(nameof(HasFlags));
        Entry = entry;
        if (Access is { IsInvite: true } access && entry.Kind == TeamEntryKind.Person && entry.InvitationId is { } id
            && _context.Data.Invitations.FirstOrDefault(i => i.Id == id) is { } invitation)
        {
            access.ShowInvitation(invitation);
        }
    }

    public async Task<bool> SaveChangesAsync()
    {
        if (IsReadOnly || !HasUnsavedChanges)
        {
            return true;
        }

        // Ein zweites Speichern während des ersten würde eine neue Person doppelt anlegen.
        if (_isSaving)
        {
            return false;
        }

        _isSaving = true;
        Error = null;
        try
        {
            var created = _employee is null && Details is not null;
            var employeeSaved = false;
            if (DetailsChanged || Qualifications?.HasUnsavedChanges == true)
            {
                if (!await SaveEmployeeAsync().ConfigureAwait(true))
                {
                    return false;
                }

                employeeSaved = true;
            }

            var accessSaved = false;
            if (Access is { IsMember: true, HasUnsavedChanges: true } access)
            {
                var result = await access.SaveAsync().ConfigureAwait(true);
                if (result.Value is null)
                {
                    var reason = result.Error ?? "Der Zugang wurde nicht gespeichert.";
                    access.Rejected(reason);
                    SelectTab(AccessTab);
                    Error = employeeSaved ? $"{EmployeePartSaved} Der Zugang ist nicht gespeichert: {reason}" : reason;
                    await ReportSavedAsync(employeeSaved, !created).ConfigureAwait(true);
                    return false;
                }

                accessSaved = true;
            }

            if (Access is { IsFormFilled: true } form)
            {
                // Einladen und Anlegen haben eigene Knöpfe; „Speichern“ schickt keine Mail und legt kein Konto an.
                SelectTab(AccessTab);
                var open = form.IsInvite
                    ? "Die Einladung ist noch nicht gesendet. Sende sie oder verwirf die Eingaben."
                    : "Der Zugang ist noch nicht angelegt. Lege ihn an oder verwirf die Eingaben.";
                Error = employeeSaved ? $"{EmployeePartSaved} {open}" : open;
                await ReportSavedAsync(employeeSaved, !created).ConfigureAwait(true);
                return false;
            }

            var payrollSaved = false;
            if (Payroll is { HasUnsavedChanges: true } payroll)
            {
                if (await payroll.SaveAsync().ConfigureAwait(true) is { } reason)
                {
                    SelectTab(PayrollTab);
                    var saved = employeeSaved || accessSaved;
                    Error = saved ? $"Die übrigen Änderungen sind gespeichert. Die Personalnummer ist nicht gespeichert: {reason}" : reason;
                    await ReportSavedAsync(saved, !created).ConfigureAwait(true);
                    return false;
                }

                payrollSaved = true;
            }

            if (Roster is { HasUnsavedChanges: true } roster && !await roster.SaveChangesAsync().ConfigureAwait(true))
            {
                SelectTab(RosterTab);
                await ReportSavedAsync(employeeSaved || accessSaved, !created).ConfigureAwait(true);
                return false;
            }

            if (Leave is { HasUnsavedChanges: true } leave && !await leave.SaveChangesAsync().ConfigureAwait(true))
            {
                SelectTab(RosterTab);
                await ReportSavedAsync(employeeSaved || accessSaved, !created).ConfigureAwait(true);
                return false;
            }

            if (Times is { HasUnsavedChanges: true } times && !await times.SaveChangesAsync().ConfigureAwait(true))
            {
                SelectTab(TimesTab);
                await ReportSavedAsync(employeeSaved || accessSaved, !created).ConfigureAwait(true);
                return false;
            }

            if (HrFile is { HasUnsavedChanges: true } hrFile && !await hrFile.SaveChangesAsync().ConfigureAwait(true))
            {
                SelectTab(HrFileTab);
                await ReportSavedAsync(employeeSaved || accessSaved, !created).ConfigureAwait(true);
                return false;
            }

            if (Duties is { HasUnsavedChanges: true } duties && !await duties.SaveChangesAsync().ConfigureAwait(true))
            {
                SelectTab(DutiesTab);
                await ReportSavedAsync(employeeSaved || accessSaved, !created).ConfigureAwait(true);
                return false;
            }

            if (Regulations is { HasUnsavedChanges: true } regulations && !await regulations.SaveChangesAsync().ConfigureAwait(true))
            {
                SelectTab(RegulationsTab);
                await ReportSavedAsync(employeeSaved || accessSaved, !created).ConfigureAwait(true);
                return false;
            }

            StatusText = accessSaved ? "Gespeichert. Gilt ab sofort." : "Gespeichert.";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(HasSaveBar));
            await ReportSavedAsync(employeeSaved || accessSaved || payrollSaved, !created).ConfigureAwait(true);
            return true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ReportSavedAsync(bool saved, bool keep)
    {
        if (saved && DataChanged is { } changed)
        {
            await changed(this, keep).ConfigureAwait(true);
        }
    }

    /// <summary>Stammdaten und Qualifikationen zusammen, wie im Web.</summary>
    private async Task<bool> SaveEmployeeAsync()
    {
        var details = Details!;
        if (!details.Check())
        {
            SelectTab(DetailsTab);
            return false;
        }

        var input = details.ToInput(Qualifications?.Selected ?? _employee?.QualificationIds ?? []);
        var result = _employee is { } employee
            ? await _context.Api.PutAsync<EmployeeDto>($"api/employees/{employee.Id}", input).ConfigureAwait(true)
            : await _context.Api.PostAsync<EmployeeDto>("api/employees", input).ConfigureAwait(true);
        if (result.Value is not { } saved)
        {
            Error = result.Error ?? "Die Stammdaten wurden nicht gespeichert.";
            return false;
        }

        _employee = saved;
        details.Apply(saved);
        _detailsState = details.State();
        Qualifications?.Saved(saved.QualificationIds);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(HeadLine));
        OnPropertyChanged(nameof(HasHeadLine));
        OnPropertyChanged(nameof(EmployeeId));
        return true;
    }

    public void Discard()
    {
        Details?.Apply(_employee);
        Qualifications?.Discard();
        Access?.Discard();
        Roster?.Discard();
        Leave?.Discard();
        Times?.Discard();
        Payroll?.Discard();
        HrFile?.Discard();
        Regulations?.Discard();
        Duties?.Discard();
        Error = null;
        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(HasSaveBar));
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    /// <summary>„Aus dem Team nehmen“ nach der Rückfrage. Die API lehnt ab, solange künftige Termine an der Person hängen.</summary>
    public async Task<bool> RemoveAsync()
    {
        if (_employee is not { } employee || !CanRemove || _context.Dialogs is not { } dialogs)
        {
            return false;
        }

        var answer = await dialogs.AskAsync(new ConfirmRequest("Aus dem Team nehmen?",
            $"{employee.DisplayName} wird nicht mehr eingeplant und steht danach unter „Ausgeschiedene“. Künftige Termine vorher umbuchen.",
            "Aus dem Team nehmen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return false;
        }

        var result = await _context.Api.DeleteAsync<object>($"api/employees/{employee.Id}").ConfigureAwait(true);
        if (result.Error is { } error)
        {
            Error = error;
            return false;
        }

        if (DataChanged is { } changed)
        {
            await changed(this, false).ConfigureAwait(true);
        }

        return true;
    }

    private void Changed()
    {
        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(HasSaveBar));
    }

    /// <summary>Was der Abschnitt „Zugang und Rolle“ zeigt: den Zugang (der eigene nur lesbar), die offene Einladung oder „Einladen“ bzw. im Einzelplatz „Zugang anlegen“.</summary>
    private AccessMode? AccessModeOf(TeamEntryKind kind, StaffMemberDto? member, InvitationDto? invitation)
    {
        var create = _context.Standalone ? AccessMode.Account : AccessMode.Invite;
        return kind switch
        {
            TeamEntryKind.Person or TeamEntryKind.Account when member is not null => member.UserId == _context.User.Id ? AccessMode.Own : AccessMode.Member,
            TeamEntryKind.Person or TeamEntryKind.Invitation when invitation is not null => AccessMode.Invitation,
            TeamEntryKind.Person or TeamEntryKind.NewAccess => create,
            _ => null,
        };
    }
}
