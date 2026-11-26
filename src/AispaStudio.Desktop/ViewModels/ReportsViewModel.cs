using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// „Zahlen“ (R05 Slice 102, R07 Slice 144): Berichte je Rolle nach web/src/reports/ReportsPage.tsx und die Betriebsinfo unter einer einzigen Reiterebene: Tagesstart · Mein Tag · Hygiene ·
/// Berichte · Betriebsinfo, je nach Recht. „Berichte“ zeigt Management und Pflichtberichte als Abschnitte untereinander (ADR 0098); ihre Ids bleiben Sprungziele und rollen zum
/// Abschnitt. Der Standort gilt für alle Reiter der Berichte gemeinsam; der Tag für alle außer „Management“, das einen eigenen Zeitraum hat. Alle Kennzahlen rechnet die API; die Maske
/// zeigt nur an.
/// </summary>
public sealed partial class ReportsViewModel : ViewModelBase, ITargetedArea, ISavesFiles, IFocusTarget
{
    public const string DayTab = "day";
    public const string BriefingTab = "briefing";
    public const string HygieneTab = "hygiene";

    /// <summary>Der Reiter „Berichte“ und zugleich sein erster Abschnitt „Management“.</summary>
    public const string ManagementTab = "management";

    /// <summary>Der Abschnitt „Pflichtberichte“ im Reiter „Berichte“; bis ADR 0098 ein Unterreiter, als Sprungziel weiter gültig.</summary>
    public const string ComplianceTab = "compliance";
    public const string BusinessInfoTab = "businessInfo";

    /// <summary>Die Reiterleiste in ihrer Reihenfolge: Alltag zuerst, Verwaltung dahinter (Ä353).</summary>
    private static readonly (string Id, string Title)[] Strip =
    [
        (BriefingTab, "Tagesstart"),
        (DayTab, "Mein Tag"),
        (HygieneTab, "Hygiene"),
        (ManagementTab, "Berichte"),
        (BusinessInfoTab, "Betriebsinfo"),
    ];

    private readonly IWorkstationApi _api;
    private readonly CurrentUser _user;
    private Task? _loading;
    private bool _isReady;
    private Guid? _targetLocation;

    /// <summary>Der Toast der Shell, auch für die Betriebsinfo.</summary>
    public ToastState Toast { get; }

    public ReportsViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time, ToastState? toast = null)
    {
        Toast = toast ?? new ToastState(time);
        (_api, _user) = (api, user);
        DayPlan = new DayPlanReportViewModel(api, time, user.EmployeeId);
        Briefing = new BriefingReportViewModel(api, time);
        Hygiene = new HygieneReportViewModel(api, time, user);
        Management = new ManagementReportViewModel(api, time) { ShowToast = text => Toast.Show(text) };
        Compliance = new ComplianceReportViewModel(api, time);
        if (CanSeeBusinessInfo)
        {
            BusinessInfo = new BusinessInfoViewModel(api, user, Toast);
            BusinessInfo.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BusinessInfoViewModel.HasUnsavedChanges))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(Title));
                }
            };
        }

        TabTitles = [.. Strip.Where(t => IsAllowed(t.Id)).Select(t => t.Title)];
        Date = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        Tab = FirstAllowedTab();
    }

    public string Title => BusinessInfo is { HasUnsavedChanges: true } ? BusinessInfo.Title : "Zahlen";

    /// <summary>Die Titel der Reiter, die die Person sieht, in der Reihenfolge der Leiste.</summary>
    public IReadOnlyList<string> TabTitles { get; }

    /// <summary>Betriebsinformationen als letzter Reiter; mit dem Recht auf Berichte mit Zahlen oder zu buchen (T-025), die API prüft jede Aktion selbst.</summary>
    public BusinessInfoViewModel? BusinessInfo { get; }

    public DayPlanReportViewModel DayPlan { get; }

    public BriefingReportViewModel Briefing { get; }

    public HygieneReportViewModel Hygiene { get; }

    public ManagementReportViewModel Management { get; }

    public ComplianceReportViewModel Compliance { get; }

    public IFileSaver? Files
    {
        get => Compliance.Files;
        set => Compliance.Files = Hygiene.Files = Management.Files = Management.Export.Files = value;
    }

    /// <summary>Die Rückfragen des Fensters gelten auch für die Betriebsinfo (Archivieren, ungespeicherte Änderungen) und für „Per Mail senden“ im Management-Bericht.</summary>
    public override IConfirmDialogs? Dialogs
    {
        get => base.Dialogs;
        set
        {
            base.Dialogs = value;
            Management.Dialogs = value;
            if (BusinessInfo is not null)
            {
                BusinessInfo.Dialogs = value;
            }
        }
    }

    public bool CanSeeDay => _user.Rights.Contains(AccessRight.ViewCalendar);

    public bool CanSeeBriefing => _user.Rights.Contains(AccessRight.Book);

    public bool CanSeeHygiene => _user.Rights.Contains(AccessRight.ChangeStatus);

    public bool CanSeeManagement => _user.Rights.Contains(AccessRight.ViewFinance);

    public bool CanSeeCompliance => _user.Rights.Contains(AccessRight.ViewFinance);

    /// <summary>Betriebsinfo lesen: mit Berichtsrecht oder wer buchen darf (Rezeption, T-025); Einträge der Stufe „Leitung“ filtert der Server.</summary>
    public bool CanSeeBusinessInfo => _user.Rights.Contains(AccessRight.ViewFinance) || _user.Rights.Contains(AccessRight.Book);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDayTab), nameof(IsBriefingTab), nameof(IsHygieneTab), nameof(IsManagementTab), nameof(IsBusinessInfoTab), nameof(ShowFilters),
        nameof(ShowDate), nameof(CurrentTab), nameof(HasUnsavedChanges))]
    public partial string Tab { get; private set; }

    public bool IsDayTab => Tab == DayTab;

    public bool IsBriefingTab => Tab == BriefingTab;

    public bool IsHygieneTab => Tab == HygieneTab;

    /// <summary>Der Reiter „Berichte“ ist offen: Management und, mit dem Recht, die Pflichtberichte darunter.</summary>
    public bool IsManagementTab => Tab == ManagementTab;

    public bool IsBusinessInfoTab => Tab == BusinessInfoTab;

    /// <summary>Der zuletzt verlangte Abschnitt von „Berichte“ (<see cref="ManagementTab"/> oder <see cref="ComplianceTab"/>); die Ansicht rollt über <see cref="FocusTarget"/> dorthin.</summary>
    public string? FocusedSection { get; private set; }

    /// <summary>Der Name des Abschnitts, zu dem die Ansicht rollt; gesetzt, sobald beide Abschnitte geladen sind, damit der Inhalt darüber die Stelle nicht mehr verschiebt.</summary>
    [ObservableProperty]
    public partial string? FocusTarget { get; set; }

    /// <summary>Standort und Datum rechts im Kopf; die Betriebsinfo hat weder noch.</summary>
    public bool ShowFilters => !IsBusinessInfoTab;

    /// <summary>Das Datum im Kopf; in „Berichte“ steht es im Kopf des Abschnitts „Pflichtberichte“, weil Management einen eigenen Zeitraum hat.</summary>
    public bool ShowDate => ShowFilters && !IsManagementTab;

    /// <summary>Der Inhalt eines Reiters mit einer Ansicht; „Berichte“ zeigt stattdessen seine beiden Abschnitte.</summary>
    public ReportTabViewModel? CurrentTab => Tab switch
    {
        DayTab => DayPlan,
        BriefingTab => Briefing,
        HygieneTab => Hygiene,
        _ => null,
    };

    [ObservableProperty]
    public partial IReadOnlyList<LocationDto> Locations { get; private set; } = [];

    [ObservableProperty]
    public partial LocationDto? SelectedLocation { get; set; }

    [ObservableProperty]
    public partial DateOnly? Date { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public bool HasUnsavedChanges => DayPlan.HasUnsavedNotes || BusinessInfo is { HasUnsavedChanges: true };

    public Task EnsureLoadedAsync() => _loading ??= LoadAsync();

    public void Open(Guid? id, string? section, string? field) => Open(new AdminTarget(id, Section: section, Field: field));

    public void Open(AdminTarget target)
    {
        _targetLocation = target.Location ?? _targetLocation;
        if (target.From is not null || target.Until is not null)
        {
            Management.SetRange(target.From, target.Until);
        }

        var wasReady = _isReady;
        _isReady = false;
        if (target.Section is { } section && IsAllowed(section))
        {
            ChangeTab(TabOf(section));
            FocusedSection = SectionOf(section);
            if (section == BusinessInfoTab)
            {
                BusinessInfo!.Open(target.Id, null, target.Field);
            }
        }

        if (wasReady && Locations.FirstOrDefault(l => l.Id == target.Location) is { } location)
        {
            SelectedLocation = location;
        }

        _isReady = wasReady;
        if (wasReady)
        {
            _ = ShowCurrentAsync();
        }
    }

    public async Task<bool> SaveChangesAsync()
    {
        var saved = await DayPlan.SaveNotesAsync().ConfigureAwait(true);
        if (BusinessInfo is { HasUnsavedChanges: true } info)
        {
            saved &= await info.SaveChangesAsync().ConfigureAwait(true);
        }

        return saved;
    }

    public void Discard()
    {
        DayPlan.DiscardNotes();
        BusinessInfo?.Discard();
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        if (!IsAllowed(tab))
        {
            return;
        }

        FocusedSection = SectionOf(tab);
        if (TabOf(tab) != Tab)
        {
            ChangeTab(TabOf(tab));
            _ = ShowCurrentAsync();
        }
        else
        {
            ScrollToSection();
        }
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        LoadError = null;
        var locations = await _api.GetAsync<List<LocationDto>>("api/locations").ConfigureAwait(true);
        if (locations.Value is not { } list)
        {
            LoadError = locations.Error ?? "Die Standorte konnten nicht geladen werden.";
            _loading = null;
            return;
        }

        var employees = await _api.GetAsync<List<EmployeeDto>>("api/employees").ConfigureAwait(true);
        if (CanSeeBriefing)
        {
            DayPlan.SetPeople(employees.Value ?? []);
        }

        Locations = list;
        SelectedLocation = list.FirstOrDefault(l => l.Id == _targetLocation) ?? list.FirstOrDefault();
        _isReady = true;
        IsLoaded = true;
        await ShowCurrentAsync().ConfigureAwait(true);
    }

    partial void OnSelectedLocationChanged(LocationDto? value) => ShowIfReady();

    partial void OnDateChanged(DateOnly? value)
    {
        // In „Berichte“ gehört das Datum nur zu den Pflichtberichten; Management behält seinen Zeitraum und lädt nicht neu.
        if (_isReady && IsManagementTab)
        {
            _ = ShowComplianceAsync();
        }
        else
        {
            ShowIfReady();
        }
    }

    private void ShowIfReady()
    {
        if (_isReady)
        {
            _ = ShowCurrentAsync();
        }
    }

    private async Task ShowCurrentAsync()
    {
        if (IsBusinessInfoTab)
        {
            await BusinessInfo!.EnsureLoadedAsync().ConfigureAwait(true);
            return;
        }

        if (SelectedLocation is not { } location || Date is not { } date)
        {
            return;
        }

        if (CurrentTab is { } tab)
        {
            await tab.ShowAsync(location, date).ConfigureAwait(true);
            return;
        }

        await Task.WhenAll(Management.ShowAsync(location, date), ShowComplianceAsync()).ConfigureAwait(true);
        ScrollToSection();
    }

    private Task ShowComplianceAsync() =>
        CanSeeCompliance && SelectedLocation is { } location && Date is { } date ? Compliance.ShowAsync(location, date) : Task.CompletedTask;

    /// <summary>Meldet der Ansicht den verlangten Abschnitt von „Berichte“; ohne offenes „Berichte“ geschieht nichts.</summary>
    private void ScrollToSection()
    {
        if (IsManagementTab && _isReady)
        {
            FocusTarget = FocusedSection == ComplianceTab ? "ReportComplianceSection" : "ReportManagementSection";
        }
    }

    private void ChangeTab(string tab)
    {
        if (CurrentTab is { } current)
        {
            current.Leave();
        }
        else if (IsManagementTab)
        {
            Management.Leave();
            Compliance.Leave();
        }

        Tab = tab;
    }

    /// <summary>Der Reiter zu einer Id: Die Pflichtberichte liegen im Reiter „Berichte“.</summary>
    private static string TabOf(string id) => id == ComplianceTab ? ManagementTab : id;

    /// <summary>Der Abschnitt zu einer Id, nur in „Berichte“.</summary>
    private static string? SectionOf(string id) => id is ManagementTab or ComplianceTab ? id : null;

    private string FirstAllowedTab() => Strip.Select(t => t.Id).FirstOrDefault(IsAllowed) ?? DayTab;

    private bool IsAllowed(string tab) => tab switch
    {
        DayTab => CanSeeDay,
        BriefingTab => CanSeeBriefing,
        HygieneTab => CanSeeHygiene,
        ManagementTab => CanSeeManagement,
        ComplianceTab => CanSeeCompliance,
        BusinessInfoTab => CanSeeBusinessInfo,
        _ => false,
    };
}
