using System.Collections.ObjectModel;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eintrag in der Navigation links; <c>Badge</c> zeigt eine Zahl am Symbol (z. B. offene Nachrichten).</summary>
public sealed partial class NavItem(string title, string icon, ViewModelBase page) : ObservableObject
{
    public string Title { get; } = title;

    public string Icon { get; } = icon;

    public ViewModelBase Page { get; } = page;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial string? Badge { get; set; }

    public bool HasBadge => Badge is not null;

    /// <summary>Name der Zeile für den Screenreader: „Posteingang, 3 offen“; ohne Zahl nur die Seite.</summary>
    public string AccessibleName => Badge is null ? Title : $"{Title}, {Badge} offen";
}

/// <summary>
/// Rahmen nach der Anmeldung: Navigation links (Kalender und weitere Bereiche), unten Farbschema, Verbindung und Abmelden.
/// Die Bereiche bleiben beim Wechseln erhalten, damit Tag, Standort und Auswahl nicht verloren gehen.
/// </summary>
public partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly ThemeController _theme;
    private readonly Func<Task> _signOut;
    private readonly IWorkstationApi _api;
    private readonly TimeProvider _time;
    private readonly TimeSpan _checkInterval;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly CancellationTokenSource _stop = new();
    private DateTimeOffset _lastPrefetch = DateTimeOffset.MinValue;
    private Task? _loop;

    public ShellViewModel(IWorkstationApi api, CurrentUser user, ILiveUpdates? live, TimeProvider time, ThemeController theme,
        string connection, Func<Task> signOut, TimeSpan? checkInterval = null, SystemModules? modules = null,
        SharedDeviceDto? device = null, Action<SharedDeviceDto?>? deviceChanged = null, ILocalServer? local = null, IConfirmDialogs? dialogs = null,
        ServerLinkViewModel? serverLink = null)
    {
        Device = device;
        _theme = theme;
        _signOut = signOut;
        _api = api;
        _time = time;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(15);
        User = user;
        HasInbox = user.Rights.Contains(AccessRight.Book) && modules?.BookingIntake != false;
        Connection = connection;
        Calendar = new CalendarViewModel(api, user, live, time, theme.Store) { CanAskAssistant = modules?.Assistant != false };
        Customers = new CustomersViewModel(api, time) { User = user, BookRequested = BookForCustomer };
        Activity = new ActivityViewModel(api, user, time) { LocationSource = () => Calendar.Location };
        Pos = new PosViewModel(api, time, theme.Store) { OwnerLevel = user.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager, MaySuperviseCash = user.Rights.Contains(AccessRight.SuperviseCash), MaySeeAmountReports = user.Rights.Contains(AccessRight.ViewAmountReports) };
        HasPin = user.HasPin;
        Devices = new DevicesViewModel(api, theme.Store)
        {
            CanManageDevice = user.Rights.Contains(AccessRight.ManageStaff),
            CanManageTerminals = user.Rights.Contains(AccessRight.ManageLocation),
            DashboardOn = modules?.Dashboard != false,
            SharedDevice = device,
            DeviceChanged = d =>
            {
                Device = d;
                deviceChanged?.Invoke(d);
            },
        };
        Inbox = new InboxViewModel(api, live, time);
        Calendar.Dialogs = Customers.Dialogs = Activity.Dialogs = Pos.Dialogs = Devices.Dialogs = Inbox.Dialogs = dialogs;
        // Das Backoffice (ADR 0070) hat weder Kalender noch Kunden; es arbeitet in der Verwaltung.
        List<NavItem> pages = HasCalendar ? [new("Kalender", Icons.Calendar, Calendar), new("Kunden", Icons.Users, Customers), new("Verlauf", Icons.History, Activity)] : [];
        if (user.Rights.Contains(AccessRight.Book))
        {
            if (HasInbox)
            {
                var inbox = new NavItem("Anfragen", Icons.Mail, Inbox);
                pages.Add(inbox);
                Inbox.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(InboxViewModel.OpenCount))
                    {
                        inbox.Badge = Inbox.OpenCount > 0 ? Inbox.OpenCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                    }
                };
            }

            pages.Add(new("Kassa", Icons.Register, Pos));
            pages.Add(new("Geräte", Icons.Printer, Devices));
            Calendar.CheckoutRequested = CheckoutAsync;
            Customers.CheckoutRequested = CheckoutAsync;
            Calendar.CheckoutWithArticleRequested = CheckoutAsync;
            Calendar.CheckoutVisitRequested = CheckoutVisitAsync;
            Activity.ReceiptRequested = ShowReceiptAsync;
        }

        if (local is not null)
        {
            NavItem? item = null;
            Sync = new SyncViewModel(api, local, time)
            {
                CanConfigure = user.Rights.Contains(AccessRight.ManageLocation),
                CanResolve = user.Rights.Contains(AccessRight.Book),
                ConflictsChanged = count => item!.Badge = count > 0 ? count.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            };
            item = new NavItem("Abgleich", Icons.Refresh, Sync);
            pages.Add(item);
            Backup = new BackupViewModel(local, theme.Store, time, WindowsBackupScheduler.ForThisPc()) { CanConfigure = user.Rights.Contains(AccessRight.ManageLocation) };
            Backup.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BackupViewModel.HasReminder))
                {
                    RefreshBanners();
                }
            };
            pages.Add(new NavItem("Sicherung", Icons.Archive, Backup));
            Network = new NetworkViewModel(local) { CanConfigure = user.Rights.Contains(AccessRight.ManageLocation) };
            pages.Add(new NavItem("Im Netz", Icons.Wifi, Network));
        }
        else if (serverLink is not null)
        {
            // Verwalten von zu Hause (ADR 0084, Punkt 5): zeigt den Server und löst ihn wieder.
            ServerLink = serverLink;
            pages.Add(new NavItem("Netz", Icons.Wifi, serverLink));
        }

        // Verwaltung (ADR 0084): alle Bereiche nativ in der App (seit Slice 103 ohne eingebettete Team-Oberfläche).
        AdminPage = new AdminPageViewModel(api, user, time, live) { Dialogs = dialogs };
        pages.Add(new NavItem("Verwaltung", Icons.Sliders, AdminPage));
        _web = new ServerWeb(api);
        Help = new HelpViewModel(() => _web.Address, time: time, prepare: _web.LookUpAsync);
        pages.Add(new NavItem("Hilfe", Icons.Help, Help));
        Help.Role = user.Access?.Role;
        // Strg+Leertaste im eingebetteten Browser der Hilfe kommt als Nachricht der Seite (appShortcuts.ts); sie zählt nur, solange die Seite offen ist.
        Help.CompanionRequested += () => ToggleCompanionFromBrowser(Help);
        HelpContent.ShowInApp = ShowHelp;
        Companion = new CompanionViewModel(api, theme.Store,
            // Das Backoffice hat keinen Kalender, aber Auswertungen: der Assistent beantwortet ihm nur diese und Fragen zur Bedienung (ADR 0072).
            (user.Rights.Contains(AccessRight.ViewCalendar) || user.Rights.Contains(AccessRight.ViewFinance)) && modules?.Assistant != false,
            user.Rights.Contains(AccessRight.ViewFinance) ? AssistantAudience.Management
                : user.Rights.Contains(AccessRight.Book) ? AssistantAudience.Reception : AssistantAudience.Therapist,
            () => Calendar.Location?.Id, time: time)
        {
            ReportsOnly = !user.Rights.Contains(AccessRight.ViewCalendar),
            HistoryOwner = user.Id,
            LinkRequested = ShowAssistantLink,
            CanBook = user.Rights.Contains(AccessRight.Book),
            CustomerRequested = ShowCustomerAsync,
            AppointmentRequested = ShowAppointment,
            Booked = appointment => Calendar.ApplyBooked(appointment),
            CustomerAppointmentsRequested = ShowCustomerAppointmentsAsync,
            FreeTimeRequested = ShowFreeTimeAsync,
            RequestRequested = ShowRequest,
            WaitlistRequested = ShowWaitlist,
            OrderRequested = OpenAssistantOrderAsync,
            ReferenceSource = OpenGuest,
        };
        if (AdminPage.Company is { } company)
        {
            company.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.Locations is { } locations)
        {
            locations.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.Closures is { } closures)
        {
            closures.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.RoomClosures is { } roomClosures)
        {
            roomClosures.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.Subscriptions is { } subscriptions)
        {
            subscriptions.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.Treatments is { } treatments)
        {
            treatments.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.Reminders is { } reminders)
        {
            reminders.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.Payments is { } payments)
        {
            payments.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.PosSettings is { } posSettings)
        {
            posSettings.Saved += Companion.OnDataSaved;
        }

        if (AdminPage.Team is { } team)
        {
            team.Saved += Companion.OnDataSaved;
            team.RosterChanged += () => _calendarStale = true;
        }

        // Im Kalender ist KI und Hilfe ein Reiter der Seitenleiste (Ä66): „offen“ heißt, der Reiter ist gewählt. Wählt man einen anderen Reiter,
        // bleibt der Assistent im Hintergrund offen; schließt man ihn, bleiben die übrigen Reiter stehen. Auf den anderen Seiten gilt die Leiste der Shell.
        Calendar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CalendarViewModel.CompanionPanel) && ShowsCalendar)
            {
                Companion.IsOpen = Calendar.CompanionPanel is not null;
            }
        };
        Companion.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CompanionViewModel.IsOpen) && ShowsCalendar)
            {
                SyncAssistantTab();
            }
        };

        Calendar.CustomerRequested = ShowCustomerAsync;
        Calendar.CancelledRequested = day => _ = ShowCancelledAsync(day);
        Activity.CustomerRequested = ShowCustomerAsync;
        Activity.AppointmentRequested = ShowAppointment;
        Activity.CancelledAppointmentRequested = ShowCancelledAppointment;
        Activity.NewTimeRequested = ShowRestoreAtNewTimeAsync;
        Activity.TreatmentRequested = ShowTreatment;
        Activity.RosterRequested = ShowRosterWeek;
        Activity.AccessRequested = ShowAccess;
        Calendar.AdminRequested = ShowAdmin;
        Calendar.InboxRequested = ShowInboxMailAsync;
        Calendar.HrFileRequested = ShowHrFile;
        Calendar.PersonDutiesRequested = ShowPersonDuties;
        Inbox.CustomerRequested = ShowCustomerAsync;
        if (HasInbox)
        {
            Activity.RequestRequested = ShowInboxMailAsync;
        }

        Inbox.CustomerEditRequested = EditCustomerAsync;
        Calendar.CheckoutGroupRequested = CheckoutGroupAsync;
        Pages = pages;
        SelectedPage = Pages[0];
        api.ConnectivityChanged += () => OnUi(RefreshOffline);
        api.Synced += appointment => OnUi(() => Calendar.ApplyLive(appointment));
        if (api.Offline is { } offline)
        {
            // Vorgemerktes aus einer früheren Anmeldung im selben Spa geht mit dieser Anmeldung an den Server.
            offline.Tenant = user.Access?.TenantId;
            offline.Changed += () => OnUi(RefreshOffline);
        }

        RefreshOffline();
        theme.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ThemeText));
            OnPropertyChanged(nameof(ThemeIsLight));
            OnPropertyChanged(nameof(ThemeIsDark));
            OnPropertyChanged(nameof(ThemeIsSystem));
        };
    }

    public CurrentUser User { get; }

    private bool ShowsCalendar => SelectedPage?.Page == Calendar;

    /// <summary>Bezug für „sie“ und „der Gast“ im Assistenten (Ä315): der offene Termin im Kalender, sonst die offene Kundenkarte.</summary>
    private AssistantReference? OpenGuest() =>
        ShowsCalendar && Calendar.Selected is { } open ? new AssistantReference(open.Appointment.Id, null, null)
        : SelectedPage?.Page == Customers && Customers.Selected is { } card ? new AssistantReference(null, card.Id, null)
        : null;

    /// <summary>Der Reiter „KI und Hilfe“ im Kalender folgt dem Stand des Assistenten: gewählt, wenn er offen ist, sonst weg (oder im Hintergrund, wenn ein anderer Reiter gewählt wurde).</summary>
    private void SyncAssistantTab()
    {
        if (Companion.IsOpen)
        {
            Calendar.ShowAssistant(Companion);
        }
        else
        {
            Calendar.HideAssistant();
        }
    }

    /// <summary>Strg+Leertaste aus dem eingebetteten Browser der Hilfe: wie der Kurzbefehl der Shell, ohne Bewegung; eine Seite im Hintergrund schaltet nichts.</summary>
    private void ToggleCompanionFromBrowser(object page)
    {
        if (SelectedPage?.Page != page)
        {
            return;
        }

        Companion.Presence.UsingKeyboard = true;
        Companion.Toggle();
    }

    /// <summary>Hilfe zu allen Einstellungen (die Hilfe-Seite im eingebetteten Browser, ADR 0079); die Fragezeichen der Bereiche öffnen sie beim passenden Abschnitt.</summary>
    public HelpViewModel Help { get; }

    /// <summary>Woher die Hilfe kommt, wenn sie nicht neben der App liegt.</summary>
    private readonly ServerWeb _web;

    internal bool ShowHelp(string? topic, string? section)
    {
        _ = ShowHelpAsync(topic, section);
        return true;
    }

    /// <summary>Öffnet die Seite Hilfe beim Thema; die Adresse steht, sobald die Web-Adresse des Servers bekannt ist.</summary>
    internal Task ShowHelpAsync(string? topic, string? section)
    {
        var shown = Help.ShowAsync(topic, section);
        SelectedPage = Pages.First(p => p.Page == Help);
        return shown;
    }

    public CalendarViewModel Calendar { get; }

    public CustomersViewModel Customers { get; }

    /// <summary>Die Seite „Verlauf“: was im Betrieb passiert ist (nur für Zugänge mit Kalender).</summary>
    public ActivityViewModel Activity { get; }

    public PosViewModel Pos { get; }

    /// <summary>Posteingang der KI-Terminannahme (nur mit Buchungsrecht).</summary>
    public InboxViewModel Inbox { get; }

    /// <summary>Die Person darf den Kalender und die Kunden sehen (nicht das Backoffice).</summary>
    public bool HasCalendar => User.Rights.Contains(AccessRight.ViewCalendar);

    /// <summary>Buchungsrecht und Terminannahme eingeschaltet (im Einzelplatz-Betrieb meist aus, ADR 0041).</summary>
    public bool HasInbox { get; }

    /// <summary>Bondrucker und Kartenterminal an diesem PC.</summary>
    public DevicesViewModel Devices { get; }

    /// <summary>Abgleich mit der zentralen Datenbank; nur im Einzelplatz-Betrieb (ADR 0041).</summary>
    public SyncViewModel? Sync { get; }

    /// <summary>Tägliche Sicherung der lokalen Datenbank; nur im Einzelplatz-Betrieb (ADR 0041, Schritt 5).</summary>
    public BackupViewModel? Backup { get; }

    /// <summary>Freigabe für die anderen PCs im Spa (Mehrplatz im Netz); nur im Einzelplatz-Betrieb (ADR 0041).</summary>
    public NetworkViewModel? Network { get; }

    /// <summary>Server-Betrieb: Seite „Netz“ mit dem eingetragenen Server.</summary>
    public ServerLinkViewModel? ServerLink { get; }

    /// <summary>Seite „Verwaltung“: oben Balken und Reiter, darunter die native Maske.</summary>
    public AdminPageViewModel AdminPage { get; }

    /// <summary>KI und Hilfe rechts, auf jeder Seite (Strg+Leertaste).</summary>
    public CompanionViewModel Companion { get; }

    public IReadOnlyList<NavItem> Pages { get; }

    [ObservableProperty]
    public partial NavItem? SelectedPage { get; set; }

    public string Connection { get; }

    /// <summary>Der Server ist gerade nicht erreichbar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionText), nameof(ConnectionTip), nameof(OfflineBanner))]
    public partial bool IsOffline { get; private set; }

    /// <summary>Ohne Verbindung vorgemerkte Änderungen, die noch zum Server müssen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionText), nameof(ConnectionTip), nameof(OfflineBanner))]
    public partial int PendingCount { get; private set; }

    /// <summary>Vorgemerkte Änderungen, die der Server beim Abgleich abgelehnt hat; die Rezeption muss sie neu klären.</summary>
    public ObservableCollection<FailedChange> Failed { get; } = [];

    [ObservableProperty]
    public partial bool HasFailed { get; private set; }

    /// <summary>„Alle zeigen“ unter den Bannern: auch die dritte Meldung zeigen, bis wieder höchstens zwei anstehen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Banners))]
    public partial bool ShowAllBanners { get; private set; }

    /// <summary>Welche Banner gerade stehen (höchstens zwei, Abgleichfehler zuerst).</summary>
    public ShellBanners Banners => ShellBanners.For(HasFailed, IsOffline, Backup?.HasReminder == true, ShowAllBanners);

    partial void OnIsOfflineChanged(bool value) => RefreshBanners();

    partial void OnHasFailedChanged(bool value) => RefreshBanners();

    private void RefreshBanners()
    {
        if (new[] { HasFailed, IsOffline, Backup?.HasReminder == true }.Count(b => b) <= ShellBanners.MaxVisible)
        {
            ShowAllBanners = false;
        }

        OnPropertyChanged(nameof(Banners));
    }

    [RelayCommand]
    private void RevealBanners() => ShowAllBanners = true;

    /// <summary>Aktion „Sicherung öffnen“ am Sicherungs-Banner.</summary>
    [RelayCommand]
    private void OpenBackup() => SelectedPage = Pages.FirstOrDefault(p => p.Page == Backup && Backup is not null) ?? SelectedPage;

    public string ConnectionText => IsOffline ? "Offline" : PendingCount > 0 ? "Gleicht ab" : "Online";

    public string ConnectionTip => IsOffline
        ? $"Keine Verbindung zum Server.{(PendingCount > 0 ? $" {PendingCount} Änderung(en) vorgemerkt." : "")}"
        : PendingCount > 0 ? $"{PendingCount} vorgemerkte Änderung(en) werden gesendet." : Connection;

    public string? OfflineBanner => IsOffline
        ? "Keine Verbindung zum Server. Kalender und Kunden zeigen den zuletzt geladenen Stand; Buchungen, Verschieben und Status werden " +
          "vorgemerkt und beim nächsten Kontakt gesendet. Kassieren geht erst wieder mit Verbindung. " +
          $"Die App prüft alle {_checkInterval.TotalSeconds:0} Sekunden von selbst und lädt neu, sobald der Server wieder antwortet." +
          (PendingCount > 0 ? $" Vorgemerkt: {PendingCount}." : "")
        : null;

    public string Initials => DayLayout.Initials(User.DisplayName);

    // ---------- Schnellwechsel (ADR 0042) ----------

    /// <summary>Gesetzt, wenn dieser PC ein Empfangsgerät ist: „Abmelden“ heißt dann „Person wechseln“.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignOutText))]
    public partial SharedDeviceDto? Device { get; private set; }

    public string SignOutText => Device is null ? "Abmelden" : "Person wechseln";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinLabel))]
    [NotifyCanExecuteChangedFor(nameof(RemovePinCommand))]
    public partial bool HasPin { get; private set; }

    public string PinLabel => HasPin ? "Neue PIN" : "PIN";

    public string PinHelp => $"{PinMinimum} bis 8 Ziffern.";

    private int PinMinimum => User.Access?.Role is StaffRole.Owner or StaffRole.GeneralManager ? 6 : 4;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SavePinCommand))]
    public partial string PinInput { get; set; } = "";

    [ObservableProperty]
    public partial string? PinText { get; private set; }

    partial void OnPinInputChanged(string value)
    {
        var digits = new string(value.Where(char.IsAsciiDigit).Take(8).ToArray());
        if (digits != value)
        {
            PinInput = digits;
        }
    }

    private bool CanSavePin => PinInput.Length >= PinMinimum;

    /// <summary>Eigene PIN festlegen oder ändern; sie gilt nur auf Empfangsgeräten.</summary>
    [RelayCommand(CanExecute = nameof(CanSavePin))]
    private async Task SavePinAsync()
    {
        var result = await _api.PutAsync<object>("api/auth/pin", new QuickSwitchPin(PinInput)).ConfigureAwait(true);
        PinInput = "";
        if (!result.Ok)
        {
            PinText = result.Error;
            return;
        }

        PinText = HasPin ? "PIN geändert." : "PIN festgelegt. Am Empfangsgerät reichen ab jetzt Name und PIN.";
        HasPin = true;
    }

    /// <summary>Eigene PIN entfernen; am Empfangsgerät geht der Wechsel zum eigenen Zugang danach nur noch mit voller Anmeldung.</summary>
    [RelayCommand(CanExecute = nameof(HasPin))]
    private async Task RemovePinAsync()
    {
        var result = await _api.DeleteAsync<object>("api/auth/pin").ConfigureAwait(true);
        PinInput = "";
        if (!result.Ok)
        {
            PinText = result.Error;
            return;
        }

        PinText = "PIN entfernt. Am Empfangsgerät melden Sie sich wieder voll an.";
        HasPin = false;
    }

    public string UserText => User.Access is { } access ? $"{User.DisplayName}\n{RoleText(access.Role)} · {access.TenantName}" : User.DisplayName;

    /// <summary>Name des Konto-Knopfs für den Screenreader: „Konto Eva Muster, Inhaberin · …“ statt der Initialen.</summary>
    public string AccountName => $"Konto {UserText.Replace("\n", ", ", StringComparison.Ordinal)}";

    public string ThemeText => _theme.Choice switch
    {
        ThemeChoice.Light => "Hell",
        ThemeChoice.Dark => "Dunkel",
        _ => "System",
    };

    public bool ThemeIsLight => _theme.Choice == ThemeChoice.Light;

    public bool ThemeIsDark => _theme.Choice == ThemeChoice.Dark;

    public bool ThemeIsSystem => _theme.Choice == ThemeChoice.System;

    /// <summary>Kalender laden; mit Buchungsrecht auch die Geräte, damit ein hier gewähltes Kartenterminal sofort angesteuert wird.</summary>
    public async Task InitializeAsync()
    {
        if (User.Rights.Contains(AccessRight.Book))
        {
            _ = Devices.InitializeAsync();
        }

        if (HasInbox)
        {
            // Gleich anmelden, damit die Zahl offener Nachrichten in der Navigation stimmt, bevor jemand den Posteingang öffnet.
            _ = Inbox.InitializeAsync();
        }

        if (Sync is not null)
        {
            _ = Sync.RefreshAsync();
        }

        if (Backup is not null)
        {
            _ = Backup.RunIfDueAsync();
        }

        if (HasCalendar)
        {
            await Calendar.InitializeAsync().ConfigureAwait(true);
        }

        // Nach Dispose ist die Quelle freigegeben; ihr Token darf nicht mehr gelesen werden.
        _loop ??= _stop.IsCancellationRequested ? Task.CompletedTask : RunAsync(_stop.Token);
    }

    /// <summary>Im Hintergrund: Verbindung prüfen, Vorgemerktes senden und den Stand für den Betrieb ohne Internet auffrischen.</summary>
    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, ct).ConfigureAwait(true);
                await CheckInAsync(ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Ein Durchgang: ohne Verbindung oder mit Vorgemerktem den Server anfragen; ist er wieder da, Vorgemerktes senden und den Kalender neu laden.
    /// Alle zehn Minuten die nächsten Tage vorausladen.
    /// </summary>
    public async Task CheckInAsync(CancellationToken ct = default)
    {
        var wasOffline = _api.IsOffline || Calendar.IsOfflineView;
        if (wasOffline || PendingCount > 0)
        {
            var status = await _api.GetAsync<SystemStatus>("api/system/status", ct).ConfigureAwait(true);
            if (!status.Ok)
            {
                RefreshOffline();
                return;
            }

            var sent = await _api.SyncAsync(ct).ConfigureAwait(true);
            RefreshOffline();
            if ((sent > 0 || wasOffline) && HasCalendar)
            {
                await Calendar.LoadAsync().ConfigureAwait(true);
            }

            if (wasOffline && !_api.IsOffline)
            {
                // Wieder verbunden: was ohne Verbindung aus dem lokalen Stand kam, neu laden, ohne dass jemand „Verbindung prüfen“ drückt.
                await Customers.ReloadListAsync().ConfigureAwait(true);
                if (HasInbox)
                {
                    await Inbox.ReloadAfterOutageAsync().ConfigureAwait(true);
                }
            }
        }

        if (HasInbox && !_api.IsOffline)
        {
            await Inbox.CheckInAsync().ConfigureAwait(true);
        }

        if (Sync is not null)
        {
            await Sync.RefreshAsync().ConfigureAwait(true);
        }

        if (Backup is not null)
        {
            await Backup.RunIfDueAsync().ConfigureAwait(true);
        }

        if (_api.Offline is not null && !_api.IsOffline && _time.GetUtcNow() - _lastPrefetch >= TimeSpan.FromMinutes(10))
        {
            await PrefetchAsync(ct).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Lädt, was die Rezeption bei einem Ausfall braucht: Stammdaten und Kalender der nächsten sieben Tage aller Standorte,
    /// die Kundenkarten der Gäste dieser Tage und die Kassa-Artikel. Die Antworten landen im lokalen Stand.
    /// </summary>
    public async Task PrefetchAsync(CancellationToken ct = default)
    {
        try
        {
            await PrefetchCoreAsync(ct).ConfigureAwait(true);
        }
        finally
        {
            _api.Offline?.Flush();
        }
    }

    private async Task PrefetchCoreAsync(CancellationToken ct)
    {
        _lastPrefetch = _time.GetUtcNow();
        var customers = new HashSet<Guid>();
        foreach (var location in Calendar.Locations)
        {
            await _api.GetAsync<LocationResources>($"api/locations/{location.Id}/resources", ct).ConfigureAwait(true);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).DateTime);
            for (var i = 0; i < 7; i++)
            {
                var day = await _api.GetAsync<CalendarDay>($"api/locations/{location.Id}/calendar?date={today.AddDays(i):yyyy-MM-dd}", ct)
                    .ConfigureAwait(true);
                if (day.Status == 0)
                {
                    return;
                }

                customers.UnionWith(day.Value?.Appointments.Select(a => a.CustomerId) ?? []);
            }
        }

        foreach (var id in customers.Take(300))
        {
            if ((await _api.GetAsync<CustomerDto>($"api/customers/{id}", ct).ConfigureAwait(true)).Status == 0)
            {
                return;
            }
        }

        if (User.Rights.Contains(AccessRight.Book))
        {
            await _api.GetAsync<List<CashRegisterDto>>("api/pos/registers", ct).ConfigureAwait(true);
            await _api.GetAsync<List<ArticleDto>>("api/pos/articles", ct).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void DismissFailed(FailedChange failed) => _api.Offline?.DismissFailed(failed.Id);

    /// <summary>Aktion „Verbindung prüfen“ am Offline-Banner: denselben Durchgang wie im Hintergrund sofort laufen lassen.</summary>
    [RelayCommand]
    private Task CheckConnectionAsync() => CheckInAsync();

    private void RefreshOffline()
    {
        IsOffline = _api.IsOffline;
        PendingCount = _api.Offline?.Pending.Count ?? 0;
        Failed.Clear();
        foreach (var failed in _api.Offline?.Failed ?? [])
        {
            Failed.Add(failed);
        }

        HasFailed = Failed.Count > 0;
        Calendar.ShowPending();
    }

    /// <summary>Hintergrundabgleich beenden (beim Abmelden).</summary>
    public void Dispose()
    {
        if (HelpContent.ShowInApp == ShowHelp)
        {
            HelpContent.ShowInApp = null;
        }

        Companion.Dispose();
        if (!_stop.IsCancellationRequested)
        {
            _stop.Cancel();
        }

        _stop.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnUi(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }

    /// <summary>Bereiche laden ihre Daten erst beim ersten Öffnen.</summary>
    partial void OnSelectedPageChanged(NavItem? value)
    {
        // Ohne Seite (die Leiste räumt die Auswahl) gibt es nichts zu laden; „Abgleich“ fehlt im Server-Betrieb,
        // dann wäre „value?.Page == Sync“ null == null gewesen und Sync!.RefreshAsync() abgestürzt.
        if (value is null || _returning)
        {
            return;
        }

        if (_shown?.Page == AdminPage && value != _shown && AdminPage.HasUnsavedChanges)
        {
            // Ungespeicherte Änderungen in der Verwaltung: erst fragen, dann wechseln; bis dahin bleibt die Seite stehen.
            ReturnTo(_shown);
            _ = LeaveAdminAsync(value);
            return;
        }

        _shown = value;

        Companion.InCalendar = value.Page == Calendar;
        Companion.ShowPage(PageKey(value.Title));
        if (Companion.InCalendar)
        {
            SyncAssistantTab();
        }

        if (value.Page == Calendar && _calendarStale)
        {
            // Der Dienstplan bestimmt die Verfügbarkeit im Kalender. Neu geladen wird nur der Tag; offene Termine und Eingaben bleiben, wie bei jedem Neuladen des Tages.
            _calendarStale = false;
            _ = Calendar.LoadAsync();
        }
        else if (value.Page == Inbox)
        {
            _ = Inbox.RefreshAsync();
        }
        else if (Sync is not null && value.Page == Sync)
        {
            _ = Sync.RefreshAsync();
        }
        else if (value.Page == AdminPage)
        {
            _ = AdminPage.InitializeAsync();
        }
        else if (value.Page == Customers)
        {
            _ = Customers.InitializeAsync();
        }
        else if (value.Page == Activity)
        {
            _ = Activity.RefreshAsync();
        }
        else if (value.Page == Pos)
        {
            Pos.DevicesChanged();
            _ = Pos.InitializeAsync(Calendar.Location?.Id);
        }
    }

    /// <summary>Die zuletzt gezeigte Seite; von ihr aus wird beim Verlassen der Verwaltung gefragt.</summary>
    private NavItem? _shown;

    /// <summary>Im Bereich Team hat sich der Dienstplan geändert, seit der Kalender zuletzt offen war.</summary>
    private bool _calendarStale;

    private bool _returning;

    /// <summary>
    /// Zurück zur Seite, die gefragt hat. Kam der Wechsel aus der Navigationsleiste, übernimmt sie einen Wert, den das Modell noch während ihrer eigenen Auswahl
    /// zurücksetzt, nicht; deshalb erst danach (gepostet), dann sieht sie eine echte Änderung.
    /// </summary>
    private void ReturnTo(NavItem page)
    {
        void Return()
        {
            _returning = true;
            SelectedPage = page;
            _returning = false;
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

    private async Task LeaveAdminAsync(NavItem target)
    {
        if (await AdminPage.ConfirmLeaveAsync().ConfigureAwait(true))
        {
            SelectedPage = target;
        }
    }

    /// <summary>Vorschlag des Assistenten: zum Tag des Termins springen und ihn markieren.</summary>
    internal void ShowAppointment(Guid id, DateTimeOffset? start)
    {
        var zone = Calendar.Location is { } location ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone) : TimeZoneInfo.Local;
        var date = start is { } s ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s, zone).DateTime) : Calendar.Date;
        SelectedPage = Pages.First(p => p.Page == Calendar);
        Calendar.OpenSearchHit(new CalendarSearchHit("", "", date, id));
    }

    /// <summary>„Termin öffnen“ bei einem Storno im Verlauf: der Kalender springt auf den Tag und zeigt das Panel des stornierten Termins (wie über die Kundenkarte).</summary>
    internal void ShowCancelledAppointment(Guid id, DateTimeOffset start)
    {
        SelectedPage = Pages.First(p => p.Page == Calendar);
        Calendar.OpenAppointmentOn(id, start);
    }

    /// <summary>„Neue Zeit wählen“ im Verlauf: der Kalender zeigt den nächsten freien Tag und den stornierten Termin mit der Verschieben-Karte und den Freie-Zeit-Vorschlägen.</summary>
    internal async Task ShowRestoreAtNewTimeAsync(NewTimeRequest request)
    {
        SelectedPage = Pages.First(p => p.Page == Calendar);
        await Calendar.ShowRestoreAtNewTimeAsync(request).ConfigureAwait(true);
    }

    /// <summary>„Behandlung öffnen“ im Verlauf: die Behandlung in den Stammdaten der Verwaltung.</summary>
    internal void ShowTreatment(Guid treatmentId) => OpenAdmin(AdminPage.Show("masterData", "treatments", treatmentId));

    /// <summary>„Dienstplan öffnen“ im Verlauf: der Wochenplan im Bereich Team, die Woche des Tages am gewählten Standort.</summary>
    internal void ShowRosterWeek(DateOnly day, Guid? employeeId) => OpenAdmin(AdminPage.Show("roster", id: employeeId, location: Calendar.Location?.Id, from: day));

    /// <summary>„Zugang öffnen“ im Verlauf: der Zugang der Person auf ihrer Karte im Bereich Team.</summary>
    internal void ShowAccess(Guid? employeeId) => OpenAdmin(AdminPage.Show("staff", id: employeeId));

    /// <summary>„Storniert n“ in der Kopfzeile des Kalenders: der Verlauf zeigt die Stornos dieses Tages.</summary>
    internal async Task ShowCancelledAsync(DateOnly day)
    {
        // Die Filter setzt ShowDayAsync gleich beim Aufruf; der Wechsel der Seite lädt danach mit denselben Filtern.
        var shown = Activity.ShowDayAsync(day, ActivityKind.Cancelled);
        SelectedPage = Pages.First(p => p.Page == Activity);
        await shown.ConfigureAwait(true);
    }

    /// <summary>„Beleg anzeigen“ im Verlauf: Kassa, Reiter Belege, Tag des Belegs, Beleg gewählt.</summary>
    internal async Task ShowReceiptAsync(Guid receiptId, DateTimeOffset issuedAt)
    {
        var zone = Calendar.Location is { } location ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone) : TimeZoneInfo.Local;
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(issuedAt, zone).DateTime);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).DateTime);
        SelectedPage = Pages.First(p => p.Page == Pos);
        Pos.Mode = PosMode.Receipts;
        Pos.ReceiptDaysBack = Math.Max(0, today.DayNumber - day.DayNumber);
        await Pos.InitializeAsync(Calendar.Location?.Id).ConfigureAwait(true);
        await Pos.LoadReceiptsAsync(receiptId).ConfigureAwait(true);
    }

    /// <summary>„Alle Termine von Steffi Huber“: der Kalender zeigt sie über sieben Tage.</summary>
    internal async Task ShowCustomerAppointmentsAsync(Guid customerId)
    {
        SelectedPage = Pages.First(p => p.Page == Calendar);
        await Calendar.FilterByCustomerAsync(customerId).ConfigureAwait(true);
    }

    /// <summary>„Freie Zeit suchen“ im Kalender, vorbelegt aus Assistent, Warteliste oder Posteingang; <paramref name="start"/> ist der Tag, ab dem gesucht wird.</summary>
    internal async Task ShowFreeTimeAsync(FreeTimePrefill prefill, DateTimeOffset? start)
    {
        var zone = Calendar.Location is { } location ? TimeZoneInfo.FindSystemTimeZoneById(location.TimeZone) : TimeZoneInfo.Local;
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var day = start is { } s ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s, zone).DateTime) : (DateOnly?)null;
        SelectedPage = Pages.First(p => p.Page == Calendar);
        // Vergangene Tage haben keine freie Zeit: dann ab heute suchen.
        await Calendar.ShowFreeTimeAsync(prefill, day is { } d && d < today ? today : day).ConfigureAwait(true);
    }

    /// <summary>Aus der Meldung „Gast hat sich nicht gemeldet“: die Anfrage mit der Vormerkung im Posteingang öffnen.</summary>
    internal async Task ShowInboxMailAsync(Guid mailId)
    {
        SelectedPage = Pages.First(p => p.Page == Inbox);
        await Inbox.ShowMailAsync(mailId).ConfigureAwait(true);
    }

    internal void ShowRequest()
    {
        SelectedPage = Pages.First(p => p.Page == Calendar);
        Calendar.OpenRequestCommand.Execute(null);
    }

    internal void ShowWaitlist()
    {
        SelectedPage = Pages.First(p => p.Page == Calendar);
        Calendar.OpenWaitlistCommand.Execute(null);
    }

    /// <summary>Auftrag aus „KI und Hilfe“ (Ä319): „offene Rechnung“ öffnet die Rechnung in der Kassa, alle anderen die Maske im Kalender; gespeichert wird dort.</summary>
    internal async Task OpenAssistantOrderAsync(AssistantSuggestion order)
    {
        if (order.Kind == AssistantSuggestionKind.OpenBill)
        {
            if (order.TargetId is { } bill)
            {
                await OpenCheckoutAsync(order.LocationId).ConfigureAwait(true);
                await Pos.OpenBillByIdAsync(bill).ConfigureAwait(true);
            }

            return;
        }

        SelectedPage = Pages.First(p => p.Page == Calendar);
        await Calendar.OpenAssistantOrderAsync(order).ConfigureAwait(true);
    }

    /// <summary>Kennung der Seite für Hilfe und KI, z. B. „Geräte“ → „geraete“, „Im Netz“ → „im-netz“.</summary>
    internal static string PageKey(string title) =>
        title.ToLowerInvariant().Replace("ä", "ae", StringComparison.Ordinal).Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal).Replace(' ', '-');

    /// <summary>
    /// Kassa mit einer Rechnung von außen öffnen (Kalender, Kundenkarte, Gruppe, Eintritt, Assistent): immer auf dem Reiter „Kassieren“, sonst läge die
    /// Rechnung unsichtbar hinter „Belege“ oder „Kassenlade &amp; Abschluss“ (T-032). Die Navigation über die Seitenleiste behält den Reiter.
    /// </summary>
    private Task OpenCheckoutAsync(Guid? locationId)
    {
        SelectedPage = Pages.First(p => p.Page == Pos);
        Pos.Mode = PosMode.Checkout;
        return Pos.InitializeAsync(locationId);
    }

    public async Task CheckoutAsync(AppointmentDto appointment)
    {
        await OpenCheckoutAsync(appointment.LocationId).ConfigureAwait(true);
        await Pos.AddAppointmentAsync(appointment).ConfigureAwait(true);
    }

    /// <summary>„Mit kassieren“ im Termin-Panel: der Termin auf die Rechnung, danach der vorgeschlagene Artikel (Slice 115).</summary>
    public async Task CheckoutAsync(AppointmentDto appointment, Guid articleId)
    {
        await CheckoutAsync(appointment).ConfigureAwait(true);
        if (Pos.Bill is not null && Pos.Error is null)
        {
            await Pos.AddArticleByIdAsync(articleId).ConfigureAwait(true);
        }
    }

    /// <summary>Alle Teile einer gemeinsamen Buchung auf eine Rechnung; die Rechnung gehört zum Gast des ersten Teils.</summary>
    public async Task CheckoutGroupAsync(IReadOnlyList<AppointmentDto> parts)
    {
        if (parts.Count == 0)
        {
            return;
        }

        await OpenCheckoutAsync(parts[0].LocationId).ConfigureAwait(true);
        foreach (var part in parts)
        {
            await Pos.AddAppointmentAsync(part).ConfigureAwait(true);
        }
    }

    public async Task CheckoutVisitAsync(AreaVisitDto visit)
    {
        await OpenCheckoutAsync(visit.LocationId).ConfigureAwait(true);
        await Pos.AddVisitAsync(visit).ConfigureAwait(true);
    }

    public async Task ShowCustomerAsync(Guid customerId)
    {
        SelectedPage = Pages.First(p => p.Page == Customers);
        await Customers.InitializeAsync().ConfigureAwait(true);
        await Customers.ShowAsync(customerId).ConfigureAwait(true);
    }

    /// <summary>„Anlegen und Termin buchen“ in den Kunden: der Kalender öffnet die Buchung mit dem neuen Gast.</summary>
    private void BookForCustomer(CustomerDto guest)
    {
        SelectedPage = Pages.First(p => p.Page == Calendar);
        Calendar.OpenBookingFor(guest);
    }

    /// <summary>„Bearbeiten“ in der Kundenübersicht des Posteingangs: die Kundenkarte mit den Kundendaten zum Bearbeiten.</summary>
    public async Task EditCustomerAsync(Guid customerId)
    {
        await ShowCustomerAsync(customerId).ConfigureAwait(true);
        Customers.EditContactCommand.Execute(null);
    }

    /// <summary>„Zur Personalakte“ aus der Erinnerung an ein Folgegespräch (R05 Slice 110).</summary>
    internal void ShowHrFile(Guid employeeId) => OpenAdmin(AdminPage.Show("masterData", "team", employeeId, PersonCardViewModel.HrFileTab));

    internal void ShowPersonDuties(Guid employeeId) => OpenAdmin(AdminPage.Show("masterData", "team", employeeId, PersonCardViewModel.DutiesTab));

    /// <summary>Die Verwaltung bei einer bestimmten Stelle öffnen (z. B. eine Behandlung bearbeiten; dort gleich beim Ablauf).</summary>
    internal void ShowAdmin(string page, string? tab, Guid? id) => OpenAdmin(AdminPage.Show(page, tab, id, tab == "treatments" ? "ablauf" : null));

    /// <summary>
    /// Wechselt nach einem Sprung zur Verwaltung. Hat die Person dort gar keinen Bereich, bleibt die Seite stehen und der Hinweis aus
    /// <see cref="AdminPageViewModel.Show"/> erscheint als Meldung unten (Slice 103).
    /// </summary>
    private void OpenAdmin(string? problem)
    {
        if (problem is not null && AdminPage.Areas.Count == 0)
        {
            Toast = problem;
            return;
        }

        Toast = null;
        SelectedPage = Pages.First(p => p.Page == AdminPage);
    }

    /// <summary>Meldung unten (Warnung, bleibt bis „Schließen“, meldungen.md M1): ein Sprung in die Verwaltung ging nicht.</summary>
    [ObservableProperty]
    public partial string? Toast { get; private set; }

    [RelayCommand]
    private void CloseToast() => Toast = null;

    /// <summary>Text der Meldung nach einem unerwarteten Fehler in der Oberfläche (T-055); der Fehler selbst steht in protokolle\app.log.</summary>
    internal const string UnexpectedErrorText = "Unerwarteter Fehler protokolliert. Bitte erneut versuchen";

    /// <summary>
    /// Ein Fehler im UI-Thread wurde protokolliert und die App läuft weiter (T-052). Ohne Meldung täte ein Klick still nichts;
    /// deshalb dieselbe Warnung unten, die bis „Schließen“ stehen bleibt (meldungen.md M1).
    /// </summary>
    internal void ReportUnexpectedError() => Toast = UnexpectedErrorText;

    /// <summary>
    /// Eine Antwort des Assistenten verweist auf eine Einstellung oder einen Bericht („Im Bericht öffnen“): die Verwaltung öffnet bei dieser Seite und diesem Reiter,
    /// bei Berichten mit dem Standort und dem Zeitraum der Antwort. Der Assistent führt nichts aus; geöffnet wird erst nach dem Klick.
    /// </summary>
    internal void ShowAssistantLink(AssistantLink link) =>
        OpenAdmin(AdminPage.Show(link.Page, link.Tab, link.TargetId, null, link.LocationId, link.From, link.Until, link.Field));

    [RelayCommand]
    private void NextTheme() => _theme.Next();

    [RelayCommand]
    private async Task SignOutAsync()
    {
        Companion.ForgetHistory();
        Dispose();
        await Devices.DisposeAsync().ConfigureAwait(true);
        await _signOut().ConfigureAwait(true);
    }

    internal static string RoleText(StaffRole role) => role switch
    {
        StaffRole.Reception => "Rezeption",
        StaffRole.Therapist => "Therapie",
        StaffRole.Manager => "Standortleitung",
        StaffRole.GeneralManager => "Manager",
        StaffRole.Backoffice => "Backoffice",
        _ => "Inhaberin / Geschäftsführung",
    };
}
