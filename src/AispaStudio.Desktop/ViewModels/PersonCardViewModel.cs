using System.Globalization;
using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Personenkarte im Bereich „Team“ (R05 Slice 98, Ä250): Kopf mit Name und Status, Reiter „Stammdaten“, „Zugang und Rolle“, „Qualifikationen“ und „Verlauf“, je
/// nach Recht und Art der Zeile. Ein „Speichern“ für die Karte: geänderte Stammdaten und Qualifikationen gehen zusammen an <c>api/employees</c>, danach ein
/// geänderter Zugang an <c>api/staff/{userId}</c>; der erste Fehler hält an, die Meldung der API steht über der Leiste und nennt, was schon gespeichert ist. Der Reiter „Dienstplan und
/// Abwesenheit“ (R05 Slice 99) zeigt die Woche der Person (<see cref="RosterPlanViewModel"/>), der Reiter „Zeiten“ ihre Zeiterfassung (<see cref="TimesheetViewModel"/>); ein offener Tag
/// zählt zu den ungespeicherten Änderungen der Karte, hat aber seine eigene Leiste. Der Reiter „Lohn“ (R05 Slice 100, <see cref="PersonPayrollViewModel"/>) hat die Personalnummer,
/// die mit der Leiste der Karte gespeichert wird, und die Monatswerte der Person. Der Reiter „Personalakte“ (R05 Slice 110, <see cref="PersonHrFileViewModel"/>) hat wie der Dienstplan seine eigene Leiste.
/// </summary>
public sealed partial class PersonCardViewModel : ViewModelBase, IEditableArea
{
    public const string DetailsTab = "stammdaten";
    public const string AccessTab = "zugang";
    public const string QualificationsTab = "qualifikationen";
    public const string RosterTab = "dienstplan";
    public const string TimesTab = "zeiten";
    public const string PayrollTab = "lohn";
    public const string HrFileTab = "personalakte";
    public const string RegulationsTab = "regelungen";
    public const string DutiesTab = "zustaendigkeiten";
    public const string HistoryTab = "verlauf";

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
            RegulationsHistory = new HistorySectionViewModel(context.Api, $"api/employees/{employee.Id}/regulations/history", context.Zone, "Wer Hinweise, Sonderregelungen und Vollmachten wann geändert hat, ohne Inhalte.");
            HrHistory = new HistorySectionViewModel(context.Api, $"api/employees/{employee.Id}/hr-file/history", context.Zone, "Wer die Personalakte wann geändert hat, ohne Inhalt");
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
            DutiesHistory = new HistorySectionViewModel(context.Api, $"api/employees/{employee.Id}/duties/history", context.Zone, "Wer Zuständigkeiten, Zertifikate und Scans wann geändert hat, ohne Inhalte.");
        }

        if (context.IsOwnerLevel)
        {
            AccessHistory = member is null ? null
                : new HistorySectionViewModel(context.Api, $"api/staff/{member.UserId}/history", context.Zone, "Wer Zugang, Rolle und Standorte wann geändert hat");
            RosterHistory = employee is null || !person ? null
                : new HistorySectionViewModel(context.Api, $"api/employees/{employee.Id}/roster-history", context.Zone,
                    "Wer Dienstplan und Abwesenheiten wann geändert hat");
        }

        Tabs = [.. TabsOf()];
        SelectTab(null);
    }

    /// <summary>Die Zeile der Liste; nach dem Neuladen der neue Stand (<see cref="Refreshed"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(StatusLine), nameof(HasStatusLine))]
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

    /// <summary>„Rolle · Status“ unter dem Namen.</summary>
    public string StatusLine => Entry.Subtitle;

    public bool HasStatusLine => StatusLine.Length > 0 && !Entry.IsNew;

    /// <summary>„Person einladen“ trägt seine Überschrift mit Hilfe im Reiter; die Karte wiederholt sie nicht.</summary>
    public bool ShowsHeader => Entry.Kind != TeamEntryKind.NewAccess;

    /// <summary>Ein einzelner Reiter braucht keine Leiste.</summary>
    public bool ShowsTabs => Tabs.Count > 1;

    /// <summary>Ausgeschiedene Personen sind nur lesbar.</summary>
    public bool IsReadOnly => Entry.Kind == TeamEntryKind.Former;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTabs))]
    public partial IReadOnlyList<CardTab> Tabs { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDetails), nameof(ShowsAccess), nameof(ShowsQualifications), nameof(ShowsRoster), nameof(ShowsTimes), nameof(ShowsPayroll), nameof(ShowsHrFile), nameof(ShowsRegulations), nameof(ShowsDuties), nameof(ShowsHistory), nameof(HasSaveBar))]
    public partial string SelectedTab { get; private set; } = DetailsTab;

    public bool ShowsDetails => SelectedTab == DetailsTab;

    public bool ShowsAccess => SelectedTab == AccessTab;

    public bool ShowsQualifications => SelectedTab == QualificationsTab;

    public bool ShowsRoster => SelectedTab == RosterTab;

    public bool ShowsTimes => SelectedTab == TimesTab;

    public bool ShowsPayroll => SelectedTab == PayrollTab;

    public bool ShowsHrFile => SelectedTab == HrFileTab;

    public bool ShowsRegulations => SelectedTab == RegulationsTab;

    public bool ShowsDuties => SelectedTab == DutiesTab;

    public bool ShowsHistory => SelectedTab == HistoryTab;

    [ObservableProperty]
    public partial PersonDetailsViewModel? Details { get; private set; }

    [ObservableProperty]
    public partial PersonQualificationsViewModel? Qualifications { get; private set; }

    [ObservableProperty]
    public partial PersonAccessViewModel? Access { get; private set; }

    /// <summary>Dienstplan und Abwesenheit der Person (R05 Slice 99); nur für eine gespeicherte Person im Team, mit dem Recht auf den Dienstplan.</summary>
    public RosterPlanViewModel? Roster { get; }

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

    public bool HasHrHistory => HrHistory is not null;

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

    public bool HasAccessHistory => AccessHistory is not null;

    /// <summary>Verlauf von Dienstplan und Abwesenheiten (nur Inhaber und Manager, nur für eine Person im Team).</summary>
    public HistorySectionViewModel? RosterHistory { get; }

    public bool HasRosterHistory => RosterHistory is not null;

    /// <summary>Fehler der API beim Speichern, über der Leiste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    /// <summary>„Gespeichert.“ in der Leiste, bis wieder etwas geändert wird.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; set; }

    /// <summary>Die Karte hat etwas zu speichern (Stammdaten, Qualifikationen oder ein Zugang zum Bearbeiten).</summary>
    public bool HasSaveBar => !IsReadOnly && (Details is not null || Qualifications is not null || Access is { IsMember: true } || Payroll is { IsEditable: true })
        && !((ShowsRoster || ShowsTimes || ShowsHrFile || ShowsRegulations || ShowsDuties) && !OtherChanges);

    /// <summary>„Aus dem Team nehmen“ für eine gespeicherte Person im Team.</summary>
    public bool CanRemove => Entry.Kind == TeamEntryKind.Person && _context.CanCatalog;

    private bool DetailsChanged => Details is not null && !IsReadOnly && Details.State() != _detailsState;

    /// <summary>Änderungen an Stammdaten, Qualifikationen oder Zugang; der offene Tag des Dienstplans und der Zeiten hat seine eigene Leiste und zählt nicht dazu.</summary>
    private bool OtherChanges => DetailsChanged || Qualifications?.HasUnsavedChanges == true || Access?.HasUnsavedChanges == true || Payroll?.HasUnsavedChanges == true;

    public bool HasUnsavedChanges => OtherChanges || Roster?.HasUnsavedChanges == true || Times?.HasUnsavedChanges == true || HrFile?.HasUnsavedChanges == true || Regulations?.HasUnsavedChanges == true
        || Duties?.HasUnsavedChanges == true;

    /// <summary>Die Karte hat auf dem Server etwas geändert; die Liste lädt neu. <c>true</c>: die Karte bleibt offen.</summary>
    public event Func<PersonCardViewModel, bool, Task>? DataChanged;

    /// <summary>Ein Tag wurde als Entwurf gespeichert und die Person will den Wochenplan zum Veröffentlichen sehen.</summary>
    public event Action? WeekPlanRequested;

    /// <summary>Wählt einen Reiter; einen, den die Karte nicht hat, ersetzt der erste.</summary>
    public void SelectTab(string? tab)
    {
        var id = Tabs.FirstOrDefault(t => t.Id == tab)?.Id ?? (Tabs.Count > 0 ? Tabs[0].Id : DetailsTab);
        foreach (var each in Tabs)
        {
            each.IsOn = each.Id == id;
        }

        SelectedTab = id;
        if (id == RosterTab && Roster is not null)
        {
            _ = Roster.EnsureLoadedAsync();
        }

        if (id == TimesTab && Times is not null)
        {
            _ = Times.EnsureLoadedAsync();
        }

        if (id == PayrollTab && Payroll is not null)
        {
            _ = Payroll.EnsureLoadedAsync();
        }

        if (id == HrFileTab && HrFile is not null)
        {
            _ = HrFile.EnsureLoadedAsync();
        }

        if (id == RegulationsTab && Regulations is not null)
        {
            _ = Regulations.EnsureLoadedAsync();
        }

        if (id == DutiesTab && Duties is not null)
        {
            _ = Duties.EnsureLoadedAsync();
        }
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
        OnPropertyChanged(nameof(EmployeeId));
        return true;
    }

    public void Discard()
    {
        Details?.Apply(_employee);
        Qualifications?.Discard();
        Access?.Discard();
        Roster?.Discard();
        Times?.Discard();
        Payroll?.Discard();
        HrFile?.Discard();
        Regulations?.Discard();
        Duties?.Discard();
        Error = null;
        StatusText = null;
        OnPropertyChanged(nameof(HasUnsavedChanges));
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
    }

    private IEnumerable<CardTab> TabsOf()
    {
        if (Details is not null)
        {
            yield return new CardTab(DetailsTab, "Stammdaten", SelectTab);
        }

        if (Access is not null)
        {
            yield return new CardTab(AccessTab, "Zugang und Rolle", SelectTab);
        }

        if (Qualifications is not null)
        {
            yield return new CardTab(QualificationsTab, "Qualifikationen", SelectTab);
        }

        if (Roster is not null)
        {
            yield return new CardTab(RosterTab, "Dienstplan und Abwesenheit", SelectTab);
        }

        if (Times is not null)
        {
            yield return new CardTab(TimesTab, "Zeiten", SelectTab);
        }

        if (Payroll is not null)
        {
            yield return new CardTab(PayrollTab, "Lohn", SelectTab);
        }

        if (HrFile is not null)
        {
            yield return new CardTab(HrFileTab, "Personalakte", SelectTab);
        }

        if (Regulations is not null)
        {
            yield return new CardTab(RegulationsTab, "Regelungen und Vollmachten", SelectTab);
        }

        if (Duties is not null)
        {
            yield return new CardTab(DutiesTab, "Zuständigkeiten", SelectTab);
        }

        if (AccessHistory is not null || RosterHistory is not null || HrHistory is not null)
        {
            yield return new CardTab(HistoryTab, "Verlauf", SelectTab);
        }
    }

    /// <summary>Was der Reiter „Zugang und Rolle“ zeigt: den Zugang (der eigene nur lesbar), die offene Einladung oder „Einladen“ bzw. im Einzelplatz „Zugang anlegen“.</summary>
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
