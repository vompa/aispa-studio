using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Gruppe im Balken der Verwaltung (R05 Slice 125, Brief 6): die erlaubten Masken, von denen <see cref="LastArea"/> beim Zurückkehren wieder gewählt wird.</summary>
public sealed record AdminGroup(string Title, string Icon, IReadOnlyList<AdminArea> Areas)
{
    public AdminArea LastArea { get; set; } = Areas[0];

    /// <summary>Die Reiterzeile steht nur bei mehreren Masken.</summary>
    public bool HasTabs => Areas.Count > 1;
}

/// <summary>
/// Ein Bereich der Verwaltung (docs/umsetzung/masken-inventar.md) mit seiner nativen Maske. Seite und Reiter sind die Schlüssel, mit denen Kalender und Assistent
/// dorthin springen (<c>masterData</c>/<c>treatments</c>, <c>stock</c>).
/// </summary>
public sealed record AdminArea(string Title, string Page, string? Tab)
{
    public override string ToString() => Title;
}

/// <summary>
/// Seite „Verwaltung“ (R05 Slice 93, ADR 0084; Slice 125: Balken statt Liste): oben die Gruppen der Bereiche, die die angemeldete Person sehen darf, darunter der gewählte Bereich. Native Masken (zuerst
/// Firmendaten, Standorte, Schließzeiten, Raumsperren, Kalender-Abos, Behandlungen; jede setzt <see cref="INativeArea"/> um) zeigt die App selbst. Seit Slice 103 gibt es keine
/// eingebettete Verwaltung mehr: ein Sprung ohne Recht oder ohne Ziel öffnet die erste erlaubte Maske mit einem Hinweis (<see cref="Notice"/>). Wer eine Maske mit ungespeicherten Änderungen verlässt, wird gefragt (<see cref="UnsavedChanges"/>). Der Balken blendet nur aus; die Rechte prüft die API.
/// </summary>
public sealed partial class AdminPageViewModel : ViewModelBase
{
    /// <summary>
    /// Alle Bereiche in der Reihenfolge des Inventars, mit den Rechten, von denen eines reicht, damit der Balken den Bereich zeigt, und dem Webmodul, das dafür eingeschaltet sein muss
    /// (<c>null</c>: der Bereich braucht kein Modul). T-081: ohne das Modul antwortet die API mit 404, der Bereich steht dann gar nicht im Balken.
    /// </summary>
    private static readonly (AdminArea Area, AccessRight[] AnyOf, Func<SystemModules, bool>? Module)[] All =
    [
        (new("Firmendaten", "masterData", "company"), [AccessRight.ManageOrganization], null),
        (new("Standorte & Räume", "masterData", "locations"), [AccessRight.ManageLocation], null),
        (new("Schließzeiten", "masterData", ClosuresPageViewModel.ClosuresSection), [AccessRight.ManageLocation], null),
        (new("Kalender-Abos", "masterData", "subscriptions"), [AccessRight.ManageLocation], null),
        (new("Handbuch", "masterData", "handbook"), [AccessRight.ManageLocation], null),
        (new("Behandlungen", "masterData", "treatments"), [AccessRight.ManageCatalog], null),
        (new("Team", "masterData", "team"),
            [AccessRight.ManageCatalog, AccessRight.ManageStaff, AccessRight.ViewCalendar, AccessRight.ManageLocation, AccessRight.ViewStaffRecords], null),
        (new("Erinnerungen", "masterData", "reminders"), [AccessRight.ManageOrganization], m => m.Reminders || m.Rebooking),
        (new("Postfach & Nummern", "masterData", "intake"), [AccessRight.ManageOrganization], m => m.BookingIntake),
        (new("Zahlungen", "masterData", "payments"), [AccessRight.ManageOrganization], m => m.OnlineBooking),
        (new("Kassa", "masterData", "kassa"), [AccessRight.ManageOrganization], null),
        (new("KI", "masterData", "ai"), [AccessRight.ManageOrganization], null),
        (new("Lager", "stock", null), [AccessRight.Book], null),
        (new("Aktionen", "promotions", null), [AccessRight.ManageCatalog], null),
        (new("Zahlen", "reports", null), [AccessRight.ViewCalendar, AccessRight.ViewFinance], null),
        (new("Kosten", "costs", null), [AccessRight.ViewAmountReports], null),
        (new("Buchhaltung", "accounting", null), [AccessRight.ViewAmountReports], null),
    ];

    /// <summary>
    /// Die Gruppen des Balkens mit ihren Masken (Auftraggeber, 31.10.2026, Ä293; acht Gruppen seit dem Nachtrag zu ADR 0098 vom 17.11.2026: „Angebot“ ist in „Behandlungen“ und „Aktionen“ geteilt). Keine Gruppe hat mehr als fünf Masken (ADR 0098): Schließzeiten und Raumsperren stehen auf einer
    /// Seite.
    /// </summary>
    private static readonly (string Title, string Icon, string[] Areas)[] GroupLayout =
    [
        ("Betrieb", Icons.Location, ["Firmendaten", "Standorte & Räume", "Schließzeiten", "Kalender-Abos", "Handbuch"]),
        ("Behandlungen", Icons.Steam, ["Behandlungen"]),
        ("Aktionen", Icons.Gift, ["Aktionen"]),
        ("Team", Icons.Users, ["Team"]),
        ("Lager", Icons.Archive, ["Lager"]),
        ("Kassa & Geld", Icons.Register, ["Kassa", "Zahlungen", "Kosten", "Buchhaltung"]),
        ("Kontakt", Icons.Chat, ["Erinnerungen", "Postfach & Nummern", "KI"]),
        ("Zahlen", Icons.Percent, ["Zahlen"]),
    ];

    /// <summary>Hinweis, wenn ein Sprung auf eine Maske ohne das Recht dazu zeigt (meldungen.md M5: Problem und Ausweg).</summary>
    public const string NoRightNotice = "Diese Maske lässt sich mit der eigenen Rolle nicht öffnen. Das Recht vergibt die Leitung im Bereich „Team“.";

    /// <summary>Hinweis, wenn es das Ziel eines Sprungs in der Windows-App nicht gibt.</summary>
    public const string UnknownNotice = "Diese Stelle gibt es in der Windows-App nicht. Die passende Maske oben im Balken der Verwaltung wählen.";

    /// <summary>Kurzform von <see cref="NoRightNotice"/> für die Meldung unten (meldungen.md M1: eine Zeile, höchstens 60 Zeichen, ohne Punkt).</summary>
    public const string NoRightToast = "Diese Maske braucht ein Recht, das die Leitung vergibt";

    /// <summary>Kurzform von <see cref="UnknownNotice"/> für die Meldung unten.</summary>
    public const string UnknownToast = "Diese Stelle gibt es in der Windows-App nicht";

    /// <summary>Hinweis, wenn ein Sprung auf einen Bereich zeigt, dessen Webmodul ausgeschaltet ist (T-081; meldungen.md M5: Problem und Ausweg).</summary>
    public const string ModuleOffNotice = "Dieses Webmodul ist in dieser Installation ausgeschaltet. Einschalten lässt es sich in den Einstellungen des Servers (Hilfe „Einzelplatz“).";

    /// <summary>Kurzform von <see cref="ModuleOffNotice"/> für die Meldung unten.</summary>
    public const string ModuleOffToast = "Dieses Webmodul ist ausgeschaltet";

    /// <summary>Die native Maske je erlaubtem Bereich; angelegt in <see cref="CreateNative"/>.</summary>
    private readonly Dictionary<AdminArea, ViewModelBase> _native = [];
    private readonly SystemModules? _modules;
    private bool _initialized;
    private bool _reverting;

    /// <summary>Hinweis, der mit dem nächsten Wechsel des Bereichs erscheint; nach einer Rückfrage zu ungespeicherten Änderungen gilt er erst dann.</summary>
    private string? _pendingNotice;

    /// <summary>Sprung in einen anderen nativen Bereich, der erst nach der Rückfrage zu ungespeicherten Änderungen gilt.</summary>
    private Action? _nativeJump;

    /// <param name="modules">Die eingeschalteten Webmodule; <c>null</c> (noch keine Antwort) heißt wie in der Shell: alle an.</param>
    public AdminPageViewModel(IWorkstationApi api, CurrentUser user, TimeProvider time, ILiveUpdates? live = null, SystemModules? modules = null)
    {
        _modules = modules;
        Areas = [.. All.Where(a => a.AnyOf.Any(user.Rights.Contains) && ModuleOn(a.Module)).Select(a => a.Area)];
        Groups = [.. GroupLayout
            .Select(g => (g.Title, g.Icon, Areas: Areas.Where(a => g.Areas.Contains(a.Title)).OrderBy(a => Array.IndexOf(g.Areas, a.Title)).ToList()))
            .Where(g => g.Areas.Count > 0)
            .Select(g => new AdminGroup(g.Title, g.Icon, g.Areas))];
        foreach (var area in Areas)
        {
            _native[area] = CreateNative(area, api, user, time, live, _modules);
        }

        Company = _native.Values.OfType<CompanyViewModel>().SingleOrDefault();
        Locations = _native.Values.OfType<LocationsViewModel>().SingleOrDefault();
        ClosuresPage = _native.Values.OfType<ClosuresPageViewModel>().SingleOrDefault();
        Subscriptions = _native.Values.OfType<SubscriptionsViewModel>().SingleOrDefault();
        Treatments = _native.Values.OfType<TreatmentsViewModel>().SingleOrDefault();
        Reminders = _native.Values.OfType<RemindersViewModel>().SingleOrDefault();
        Payments = _native.Values.OfType<PaymentsViewModel>().SingleOrDefault();
        PosSettings = _native.Values.OfType<PosSettingsViewModel>().SingleOrDefault();
        Team = _native.Values.OfType<TeamViewModel>().SingleOrDefault();
        Handbook = _native.Values.OfType<HandbookViewModel>().SingleOrDefault();
        Intake = _native.Values.OfType<IntakeViewModel>().SingleOrDefault();
        Ai = _native.Values.OfType<AiViewModel>().SingleOrDefault();
        Stock = _native.Values.OfType<StockViewModel>().SingleOrDefault();
        Promotions = _native.Values.OfType<PromotionsViewModel>().SingleOrDefault();
        Reports = _native.Values.OfType<ReportsViewModel>().SingleOrDefault();
        Accounting = _native.Values.OfType<AccountingViewModel>().SingleOrDefault();
        Costs = _native.Values.OfType<CostsViewModel>().SingleOrDefault();
        if (Company is not null)
        {
            // „Betrieb einrichten“ (ADR 0099): jeder Schritt springt in die passende Maske; fehlt dort das Recht, steht der Hinweis der Seite.
            Company.SetupStepRequested += step => Show(step.Page, step.Tab);
        }

        if (Treatments is not null)
        {
            Treatments.PersonRequested += id => Show("masterData", "team", id, area: PersonCardViewModel.QualificationsTab);
        }

        if (Costs is not null)
        {
            Costs.TreatmentRequested += id => Show("masterData", "treatments", id, "material");
        }

        SelectedArea = Areas.Count > 0 ? Areas[0] : null;
    }

    public IReadOnlyList<AdminArea> Areas { get; }

    /// <summary>Die Gruppen im Balken, in denen die Person mindestens eine Maske sehen darf.</summary>
    public IReadOnlyList<AdminGroup> Groups { get; }

    /// <summary>Die Gruppe der gewählten Maske; die Auswahl selbst bleibt <see cref="SelectedArea"/>, damit Sprünge und die Rückfrage nur einen Weg kennen.</summary>
    public AdminGroup? SelectedGroup => Groups.FirstOrDefault(g => SelectedArea is not null && g.Areas.Contains(SelectedArea));

    [RelayCommand]
    private void SelectGroup(AdminGroup group) => SelectedArea = group.LastArea;

    [RelayCommand]
    private void SelectArea(AdminArea area) => SelectedArea = area;

    /// <summary>Firmendaten; nur, wer sie pflegen darf (ManageOrganization).</summary>
    public CompanyViewModel? Company { get; }

    /// <summary>Standorte &amp; Räume; nur, wer Standorte verwalten darf (ManageLocation).</summary>
    public LocationsViewModel? Locations { get; }

    /// <summary>Seite „Schließzeiten“ mit Schließzeiten und Raumsperren in einer Liste (ADR 0098); nur, wer Standorte verwalten darf (ManageLocation).</summary>
    public ClosuresPageViewModel? ClosuresPage { get; }

    /// <summary>Kalender-Abos; nur, wer Standorte verwalten darf (ManageLocation).</summary>
    public SubscriptionsViewModel? Subscriptions { get; }

    /// <summary>Behandlungen (R05 Slice 94); nur, wer den Katalog pflegen darf (ManageCatalog).</summary>
    public TreatmentsViewModel? Treatments { get; }

    /// <summary>Terminerinnerungen und Wiederbuchungs-Hinweise (R05 Slice 96); nur mit ManageOrganization.</summary>
    public RemindersViewModel? Reminders { get; }

    /// <summary>Zahlungskonto für Online-Zahlungen (R05 Slice 96); nur mit ManageOrganization.</summary>
    public PaymentsViewModel? Payments { get; }

    /// <summary>Kassa-Einstellungen (R05 Slice 96); nur mit ManageOrganization.</summary>
    public PosSettingsViewModel? PosSettings { get; }
    /// <summary>Handbuch (R05 Slice 97); nur, wer Standorte verwalten darf (ManageLocation).</summary>
    public HandbookViewModel? Handbook { get; }

    /// <summary>Postfach &amp; Nummern (R05 Slice 97); nur, wer die Organisation verwalten darf (ManageOrganization).</summary>
    public IntakeViewModel? Intake { get; }

    /// <summary>KI (R05 Slice 97); nur, wer die Organisation verwalten darf (ManageOrganization).</summary>
    public AiViewModel? Ai { get; }

    /// <summary>Lager (R05 Slice 101); nur, wer buchen darf (Book); Inventur und Lagerorte darin nur mit ManageLocation.</summary>
    public StockViewModel? Stock { get; }

    /// <summary>Aktionen (R05 Slice 101); nur, wer den Katalog pflegen darf (ManageCatalog).</summary>
    public PromotionsViewModel? Promotions { get; }

    /// <summary>Zahlen (R05 Slice 102, R07 Slice 144): Berichte und Betriebsinfo unter einer Reiterebene; mit ViewCalendar oder ViewFinance, die Reiter darin je nach Recht.</summary>
    public ReportsViewModel? Reports { get; }

    /// <summary>Buchhaltung mit DATEV-Export (R05 Slice 102); nur mit ViewAmountReports.</summary>
    public AccountingViewModel? Accounting { get; }

    /// <summary>Vollkosten je Behandlung (R05 Slice 102); nur mit ViewAmountReports, ändern nur mit ManageCatalog.</summary>
    public CostsViewModel? Costs { get; }

    /// <summary>Team mit Personenkarte (R05 Slice 98), Wochenplan und Anträgen (Slice 99); mit Stammdaten, Zugängen, Kalender, Standorten oder Personalunterlagen.</summary>
    public TeamViewModel? Team { get; }

    /// <summary>Die native Maske eines Bereichs, den die Person sehen darf.</summary>
    private INativeArea? NativeFor(AdminArea? area) => area is not null && _native.TryGetValue(area, out var native) ? native as INativeArea : null;

    /// <summary>Wird gesetzt, sobald die Dialoge des Fensters da sind; die nativen Masken fragen über dieselben.</summary>
    public new IConfirmDialogs? Dialogs
    {
        get => base.Dialogs;
        set
        {
            base.Dialogs = value;
            foreach (var native in _native.Values)
            {
                native.Dialogs = value;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedGroup))]
    public partial AdminArea? SelectedArea { get; set; }

    /// <summary>
    /// Hinweis über der Maske (InfoBar, Warnung) nach einem Sprung ohne Recht oder ohne Ziel (Slice 103); verschwindet mit dem nächsten Wechsel, der gelingt,
    /// oder über „Schließen“.
    /// </summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    [RelayCommand]
    private void CloseNotice() => Notice = null;

    /// <summary>Was unter Balken und Reitern steht: die native Maske des Bereichs.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Content { get; private set; }

    /// <summary>Die offene Maske hat ungespeicherte Änderungen.</summary>
    public bool HasUnsavedChanges => Editing?.HasUnsavedChanges == true;

    private IEditableArea? Editing => Content as IEditableArea;

    /// <summary>
    /// Die Maske eines Bereichs. Erweiterungsstelle: ein Bereich bekommt seine Maske, indem er in <see cref="All"/> steht und
    /// hier eine Zeile steht (<c>"subscriptions" =&gt; new SubscriptionsViewModel(api, time)</c>), die <see cref="INativeArea"/> umsetzt; mehr braucht die Seite nicht.
    /// </summary>
    private static ViewModelBase CreateNative(AdminArea area, IWorkstationApi api, CurrentUser user, TimeProvider time, ILiveUpdates? live, SystemModules? modules) => area.Tab switch
    {
        "company" => new CompanyViewModel(api, time.LocalTimeZone),
        "locations" => new LocationsViewModel(api, user),
        ClosuresPageViewModel.ClosuresSection => new ClosuresPageViewModel(api, user, time),
        "subscriptions" => new SubscriptionsViewModel(api, time),
        "treatments" => new TreatmentsViewModel(api, time.LocalTimeZone, user.Rights.Contains(AccessRight.ViewAmountReports)),
        "reminders" => new RemindersViewModel(api, time.LocalTimeZone, remindersOn: modules?.Reminders != false, rebookingOn: modules?.Rebooking != false),
        "payments" => new PaymentsViewModel(api),
        "kassa" => new PosSettingsViewModel(api),
        "team" => new TeamViewModel(api, user, time),
        "handbook" => new HandbookViewModel(api),
        "intake" => new IntakeViewModel(api, time.LocalTimeZone),
        "ai" => new AiViewModel(api),
        _ when area.Page == "stock" => new StockViewModel(api, user, time, live, FileSettingsStore.Default),
        _ when area.Page == "promotions" => new PromotionsViewModel(api, time),
        _ when area.Page == "reports" => new ReportsViewModel(api, user, time),
        _ when area.Page == "accounting" => new AccountingViewModel(api, user, time),
        _ when area.Page == "costs" => new CostsViewModel(api, user, time),
        _ => throw new InvalidOperationException($"Der Bereich „{area.Title}“ hat keine native Maske."),
    };

    /// <summary>Was unter Balken und Reitern steht: die Maske des Bereichs. Die Maske fragt über denselben Dialog des Fensters wie die Seite.</summary>
    private ViewModelBase? ContentOf(AdminArea? area)
    {
        if (area is not null && _native.TryGetValue(area, out var native))
        {
            native.Dialogs = Dialogs;
            return native;
        }

        return null;
    }

    /// <summary>Beim Öffnen der Seite: den gewählten Bereich laden.</summary>
    public Task InitializeAsync()
    {
        _initialized = true;
        return LoadContentAsync();
    }

    /// <summary>Darf die Seite verlassen werden? Fragt bei ungespeicherten Änderungen „Änderungen speichern?“.</summary>
    public Task<bool> ConfirmLeaveAsync() => Editing is { } editing ? UnsavedChanges.ConfirmLeaveAsync(Dialogs, editing) : Task.FromResult(true);

    /// <summary>
    /// Öffnet die Verwaltung bei einer bestimmten Stelle (Kalender, Assistent): den Bereich mit dieser Seite und diesem Reiter; die Maske setzt den Fokus auf das
    /// genannte Feld. Ohne Recht auf das Ziel oder ohne dieses Ziel öffnet die erste erlaubte Maske mit einem Hinweis (<see cref="Notice"/>). Zurück kommt die
    /// Kurzform des Hinweises (<see cref="NoRightToast"/>, <see cref="UnknownToast"/>), damit die Shell ihn als Meldung zeigen kann, wenn die Person gar keinen Bereich hat;
    /// nach einem gelungenen Sprung <c>null</c>.
    /// </summary>
    public string? Show(string page, string? tab = null, Guid? id = null, string? area = null, Guid? location = null, DateOnly? from = null, DateOnly? until = null,
        string? field = null)
    {
        // R05 Slice 98: Zugänge und Qualifikationen stehen auf der Personenkarte im Bereich Team.
        (page, tab, area) = (page, tab) switch
        {
            ("staff", _) => ("masterData", "team", area ?? PersonCardViewModel.AccessTab),
            ("masterData", "qualifications") => ("masterData", "team", area ?? PersonCardViewModel.QualificationsTab),
            // R05 Slice 99: der Dienstplan ist der Wochenplan im Bereich Team.
            ("roster", _) => ("masterData", "team", area ?? TeamViewModel.RosterMode),
            // R05 Slice 99: die Zeiterfassung ist die Ansicht „Zeiten“ im Bereich Team.
            ("time", _) => ("masterData", "team", area ?? TeamViewModel.TimesMode),
            // R07 Slice 144: die Betriebsinfo ist ein Reiter in „Zahlen“.
            ("businessInfo", _) => ("reports", ReportsViewModel.BusinessInfoTab, area),
            // ADR 0098: die Raumsperren stehen in der Liste der Seite „Schließzeiten“.
            ("masterData", ClosuresPageViewModel.RoomClosuresSection) => ("masterData", ClosuresPageViewModel.ClosuresSection, ClosuresPageViewModel.RoomClosuresSection),
            _ => (page, tab, area),
        };
        var target = Find(Areas, page, tab);
        if (NativeFor(target) is not { } native)
        {
            var known = Find(All.Select(a => a.Area), page, tab);
            if (known is null)
            {
                return ShowNotice(UnknownNotice, UnknownToast);
            }

            var module = All.Single(a => a.Area == known).Module;
            return ModuleOn(module) ? ShowNotice(NoRightNotice, NoRightToast) : ShowNotice(ModuleOffNotice, ModuleOffToast);
        }

        void Jump()
        {
            if (native is ITargetedArea targeted)
            {
                targeted.Open(new AdminTarget(id, location, from, until, field, target!.Tab is null ? tab : area));
            }
            else
            {
                native.Open(id, area, field);
            }
        }

        _pendingNotice = null;
        if (target == SelectedArea)
        {
            Notice = null;
            Jump();
            _ = LoadContentAsync();
            return null;
        }

        if (HasUnsavedChanges)
        {
            _nativeJump = Jump;
        }
        else
        {
            Jump();
        }

        SelectedArea = target;
        return null;
    }

    /// <summary>Ist das Webmodul des Bereichs an? Ohne Angabe zu den Modulen gilt wie in der Shell: alle an.</summary>
    private bool ModuleOn(Func<SystemModules, bool>? module) => module is null || _modules is null || module(_modules);

    /// <summary>Der Bereich mit dieser Seite und diesem Reiter, sonst der Bereich der Seite ohne Reiter (<c>reports</c>/<c>management</c> → Berichte).</summary>
    private static AdminArea? Find(IEnumerable<AdminArea> areas, string page, string? tab)
    {
        var list = areas.ToList();
        return list.FirstOrDefault(a => a.Page == page && a.Tab == tab) ?? list.FirstOrDefault(a => a.Page == page && a.Tab is null);
    }

    /// <summary>Ziel ohne Recht oder unbekannt: die erste erlaubte Maske mit dem Hinweis; ohne jeden Bereich bleibt alles, wie es ist.</summary>
    private string ShowNotice(string text, string toast)
    {
        if (Areas.Count == 0)
        {
            return toast;
        }

        if (SelectedArea == Areas[0])
        {
            Notice = text;
            _ = LoadContentAsync();
        }
        else
        {
            _pendingNotice = text;
            SelectedArea = Areas[0];
        }

        return toast;
    }

    partial void OnSelectedAreaChanged(AdminArea? oldValue, AdminArea? newValue)
    {
        if (_reverting)
        {
            return;
        }

        if (oldValue is not null && HasUnsavedChanges)
        {
            // Erst fragen, dann wechseln: Balken und Reiter bleiben beim alten Bereich, bis die Antwort da ist.
            ReturnTo(oldValue);
            _ = LeaveAreaAsync(newValue);
            return;
        }

        if (SelectedGroup is { } group && newValue is not null)
        {
            group.LastArea = newValue;
        }

        Notice = _pendingNotice;
        _pendingNotice = null;
        Content = ContentOf(newValue);
        _ = LoadContentAsync();
    }

    private async Task LeaveAreaAsync(AdminArea? target)
    {
        var jump = _nativeJump;
        _nativeJump = null;
        if (await ConfirmLeaveAsync().ConfigureAwait(true))
        {
            jump?.Invoke();
            SelectedArea = target;
        }
        else
        {
            // Der Hinweis gehört zum abgebrochenen Sprung, nicht zu einem späteren Klick auf einen Bereich.
            _pendingNotice = null;
        }
    }

    private Task LoadContentAsync()
    {
        if (!_initialized)
        {
            return Task.CompletedTask;
        }

        return Content is INativeArea native ? native.EnsureLoadedAsync() : Task.CompletedTask;
    }

    /// <summary>Zurück zum Bereich, der gefragt hat. Der Fensterdialog antwortet immer später als dieser Aufruf, die Rückkehr kommt also vor der Antwort.</summary>
    private void ReturnTo(AdminArea area)
    {
        _reverting = true;
        SelectedArea = area;
        _reverting = false;
    }
}
