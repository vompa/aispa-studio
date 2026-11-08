using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Rahmen der App: Verbindung prüfen, dann Anmeldung oder die Arbeitsfläche mit Navigation.</summary>
/// <param name="local">Einzelplatz-Betrieb: Datenbank und API auf diesem PC, die App startet sie selbst (ADR 0041).</param>
public partial class MainViewModel(IWorkstationApi api, Func<ILiveUpdates>? liveFactory = null, TimeProvider? time = null, ThemeController? theme = null,
    ILocalServer? local = null)
    : ViewModelBase
{
    /// <summary>So lange vor dem Abmelden ohne Eingabe warnt die App (wie im Browser).</summary>
    public const int IdleWarningSeconds = 30;

    /// <summary>Der eine Rückfragedialog des Fensters (Design-Brief 5.27); die Arbeitsfläche stellt ihre Rückfragen darüber.</summary>
    public ConfirmDialogHost DialogHost { get; } = new();

    private readonly ThemeController _theme = theme ?? new ThemeController(new MemorySettingsStore());
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private DateTimeOffset _lastActivity;
    private bool _idleSigningOut;

    /// <summary>Gesetzt, wenn dieser PC ein Empfangsgerät ist: dann meldet die App nach der Leerlaufzeit ab (ADR 0042).</summary>
    [ObservableProperty]
    public partial SharedDeviceDto? Device { get; private set; }

    /// <summary>Sekunden bis zum Abmelden ohne Eingabe; nur in den letzten 30 Sekunden gesetzt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdleWarning), nameof(IdleText))]
    public partial int? IdleSecondsLeft { get; private set; }

    public bool IsIdleWarning => IdleSecondsLeft is not null;

    public string IdleText => IdleSecondsLeft is { } s ? $"Ohne Eingabe wird in {s} Sekunden abgemeldet." : "";

    /// <summary>Maus, Tastatur oder Berührung: der Leerlauf beginnt neu.</summary>
    public void NoteActivity()
    {
        _lastActivity = _time.GetUtcNow();
        IdleSecondsLeft = null;
    }

    [RelayCommand]
    private void KeepWorking() => NoteActivity();

    /// <summary>
    /// Jede Sekunde aus dem Fenster: am Empfangsgerät nach der eingestellten Zeit ohne Eingabe abmelden, damit die nächste
    /// Buchung nicht unter falschem Namen landet. Die letzten 30 Sekunden warnt ein Hinweis mit „Weiter arbeiten“.
    /// </summary>
    public async Task TickAsync()
    {
        if (Device is not { } device || Content is not ShellViewModel shell || _idleSigningOut)
        {
            IdleSecondsLeft = null;
            return;
        }

        var left = (int)Math.Ceiling(device.IdleMinutes * 60 - (_time.GetUtcNow() - _lastActivity).TotalSeconds);
        if (left > 0)
        {
            IdleSecondsLeft = left <= IdleWarningSeconds ? left : null;
            return;
        }

        IdleSecondsLeft = null;
        _idleSigningOut = true;
        try
        {
            await shell.SignOutCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
        finally
        {
            _idleSigningOut = false;
        }
    }

    /// <summary>Nur für den XAML-Designer.</summary>
    public MainViewModel()
        : this(new ApiClient(new Uri("http://localhost:5086"), new MemorySessionStore()))
    {
    }

    public string ApiAddress => api.BaseAddress.ToString();

    public bool IsLocal => local is not null;

    /// <summary>Einzelplatz: aus der täglichen Sicherung wiederherstellen, am Startbildschirm und bei der Einrichtung.</summary>
    public RestoreViewModel? Restore => local is null ? null : _restore ??= new RestoreViewModel(local, StartAsync);

    private RestoreViewModel? _restore;

    /// <summary>Einzelplatz, neuer PC eines bestehenden Spas: mit der zentralen Datenbank verbinden statt einzurichten.</summary>
    public JoinViewModel? Join => local is null ? null : _join ??= new JoinViewModel(local, ShowLoginAsync);

    private JoinViewModel? _join;

    /// <summary>Server-Betrieb ohne Verbindung: mit einem im Netz freigegebenen Haupt-PC verbinden (Mehrplatz).</summary>
    public NetworkConnectViewModel? Connect { get; init; }

    /// <summary>Server-Betrieb ohne Verbindung: die Adresse eines Servers eintragen (Verwalten von zu Hause).</summary>
    public ServerConnectViewModel? ServerConnect { get; init; }

    /// <summary>Server-Betrieb: Seite „Netz“ in der Seitenleiste.</summary>
    public ServerLinkViewModel? ServerLink { get; init; }

    private const string NoConnectionTitle = "Keine Verbindung zum Server";

    private const string StartingTitle = "AISPA Studio startet …";

    private const string WaitingTitle = "Verbindung wird aufgebaut …";

    /// <summary>Überschrift, solange keine Verbindung besteht.</summary>
    [ObservableProperty]
    public partial string ConnectionTitle { get; private set; } = local is null ? NoConnectionTitle : StartingTitle;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Noch nicht geprüft.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserText), nameof(IsSignedIn))]
    public partial CurrentUser? User { get; private set; }

    /// <summary>Anmeldung oder Arbeitsfläche.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Content { get; private set; }

    public bool IsSignedIn => User is not null;

    public string UserText => User?.Access is { } access ? $"{User.DisplayName} · {access.TenantName}" : "";

    /// <summary>
    /// Zeitlimit je Anfrage beim Start. Ein Server, der gerade hochfährt, nimmt die Verbindung an und antwortet nicht gleich; die 20 Sekunden des
    /// <see cref="HttpApiClient"/> ließen den Startbildschirm dann minutenlang stehen (Ä68). Danach folgt der nächste Versuch.
    /// </summary>
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Pause zwischen den Versuchen, solange der Server nicht antwortet oder noch migriert.</summary>
    public TimeSpan ProbeInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>So lange wird ein Server versucht, der gar nicht antwortet, bevor „keine Verbindung“ steht (er fährt vielleicht gerade hoch).</summary>
    public TimeSpan ProbeWindow { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>So lange wartet der Start auf einen Server, der antwortet, aber noch die Datenbank migriert oder das Beispiel-Spa einspielt.</summary>
    public TimeSpan StartupWindow { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Zeitlimit je Versuch, die gemerkte Anmeldung zu laden. Der erste Aufruf braucht im Debug-Build länger (Datenmodell, Wolverine-Code); danach folgt
    /// der nächste Versuch, bis <see cref="StartupWindow"/> um ist.
    /// </summary>
    public TimeSpan SignInTimeout { get; init; } = TimeSpan.FromSeconds(20);

    [RelayCommand]
    private async Task CheckStatusAsync()
    {
        StatusText = "Verbinde …";
        var started = _time.GetTimestamp();
        while (true)
        {
            SystemStatus? status;
            try
            {
                using var attempt = new CancellationTokenSource(ProbeTimeout, _time);
                status = await api.StatusAsync(attempt.Token).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // Der Server nimmt die Verbindung nicht an, antwortet nicht oder noch mit einem Fehler (er fährt vielleicht noch hoch): kurz warten und
                // es noch einmal versuchen. Mit lokalem Stand (ADR 0005) gibt es nichts zu warten, die App arbeitet gleich ohne Verbindung weiter.
                ShowStartupStep(ConnectingStep, ServerStartingText);
                if (api.Offline?.Get("api/auth/me") is null && await WaitForServerAsync(started, ProbeWindow, "Der Server antwortet noch nicht. Die App versucht es weiter.").ConfigureAwait(true))
                {
                    continue;
                }

                IsConnected = false;
                ConnectionTitle = local is null ? NoConnectionTitle : StartingTitle;
                StatusText = ex is HttpRequestException ? $"Server nicht erreichbar: {ex.Message}" : "Der Server antwortet nicht.";
                return;
            }

            IsConnected = status is not null;
            if (status?.Startup is { Phase: StartupPhase.Migrating or StartupPhase.Seeding } startup)
            {
                // Der Server antwortet, aber seine Datenbank ist noch nicht bereit: Anfragen an sie liefen in das Zeitlimit; hier steht stattdessen der Stand.
                IsConnected = false;
                ShowStartupStep(startup.Phase.ToString(), startup.Phase == StartupPhase.Migrating ? "Datenbank wird vorbereitet …" : $"{startup.Text} …");
                if (await WaitForServerAsync(started, StartupWindow, startup.Text).ConfigureAwait(true))
                {
                    continue;
                }

                IsConnected = true;
            }

            StatusText = status is null
                ? "Leere Antwort vom Server."
                : $"Verbunden · Version {status.Version.Split('+')[0]} · {status.Environment}";
            return;
        }
    }

    /// <summary>Zeigt „Verbindung wird aufgebaut …“ mit dem Grund und wartet bis zum nächsten Versuch; <c>false</c>, wenn die Wartezeit <paramref name="limit"/> um ist.</summary>
    private async Task<bool> WaitForServerAsync(long started, TimeSpan limit, string reason)
    {
        if (_time.GetElapsedTime(started) >= limit)
        {
            return false;
        }

        ConnectionTitle = WaitingTitle;
        StatusText = reason;
        await Task.Delay(ProbeInterval, _time).ConfigureAwait(true);
        return true;
    }

    /// <summary>Beim Start: mit gemerkter Anmeldung direkt in den Kalender, sonst zur Anmeldung. Solange steht der Startbildschirm.</summary>
    [RelayCommand]
    public async Task StartAsync()
    {
        using var startup = BeginStartup();
        try
        {
            await StartCoreAsync().ConfigureAwait(true);
        }
        finally
        {
            EndStartup(startup);
        }
    }

    private async Task StartCoreAsync()
    {
        if (local is not null && !await StartLocalAsync().ConfigureAwait(true))
        {
            return;
        }

        await CheckStatusAsync().ConfigureAwait(true);

        // Ohne Verbindung, aber mit lokalem Stand: mit der zuletzt angemeldeten Person weiterarbeiten (ADR 0005).
        if (!IsConnected && api.Offline?.Get("api/auth/me") is null)
        {
            Content = null;
            return;
        }

        var signIn = await LoadSignInAsync().ConfigureAwait(true);
        if (signIn.Value is { Access: not null } user)
        {
            await ShowShellAsync(user).ConfigureAwait(true);
        }
        else if (signIn.Status == 0 && signIn.Value is null)
        {
            // Der Server antwortet auf die Anmeldung nicht: „keine Verbindung“ statt der Anmeldeseite, die eine gültige Anmeldung nur verdecken würde.
            IsConnected = false;
            ConnectionTitle = NoConnectionTitle;
            StatusText = "Der Server antwortet nicht.";
            Content = null;
        }
        else
        {
            await ShowLoginAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Lädt die gemerkte Anmeldung. Antwortet der Server darauf noch nicht, versucht es die App sichtbar weiter, bis <see cref="StartupWindow"/> um ist;
    /// ohne Verbindung liefert <see cref="ApiClient.GetAsync{T}"/> den lokalen Stand (ADR 0005), dann wird nicht gewartet.
    /// </summary>
    private async Task<ApiResult<CurrentUser>> LoadSignInAsync()
    {
        ShowStartupStep("Anmeldung", "Anmeldung wird geladen …");
        var started = _time.GetTimestamp();
        while (true)
        {
            ApiResult<CurrentUser> result;
            try
            {
                using var attempt = new CancellationTokenSource(SignInTimeout, _time);
                result = await api.GetAsync<CurrentUser>("api/auth/me", attempt.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                result = new(null, "Der Server antwortet nicht.", 0);
            }

            if (result.Status != 0 || result.Value is not null
                || !await WaitForServerAsync(started, StartupWindow, "Die Anmeldung wird geladen. Der Server antwortet noch nicht, die App versucht es weiter.").ConfigureAwait(true))
            {
                return result;
            }
        }
    }

    /// <summary>Einzelplatz: Datenbank und API starten; der Fortschritt steht im Statustext.</summary>
    private async Task<bool> StartLocalAsync()
    {
        IsConnected = false;
        ConnectionTitle = StartingTitle;
        try
        {
            await local!.StartAsync(new Progress<string>(step =>
            {
                StatusText = step;
                ShowStartupStep("Einzelplatz", step);
            })).ConfigureAwait(true);
            return true;
        }
        catch (LocalServerException ex)
        {
            ConnectionTitle = "Einzelplatz startet nicht";
            StatusText = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await api.SignOutAsync().ConfigureAwait(true);
        User = null;
        await ShowLoginAsync().ConfigureAwait(true);
    }

    private async Task ShowLoginAsync()
    {
        var login = new LoginViewModel(api, ShowShellAsync) { Restore = Restore, Join = Join };
        Content = login;
        await login.LoadAsync().ConfigureAwait(true);
        Device = login.Device;
    }

    private async Task ShowShellAsync(CurrentUser user)
    {
        User = user;
        // Ohne Antwort (älterer Server) alles zeigen wie bisher; ausgeschaltete Module antworten ohnehin mit 404 (ADR 0041).
        var modules = (await api.GetAsync<SystemModules>("api/system/modules").ConfigureAwait(true)).Value;
        Device = (await api.GetAsync<QuickSwitchState>("api/auth/quick-switch").ConfigureAwait(true)).Value?.Device;
        NoteActivity();
        var shell = new ShellViewModel(api, user, liveFactory?.Invoke(), _time, _theme, StatusText, SignOutAsync,
            modules: modules, device: Device, deviceChanged: device => Device = device, local: local, dialogs: DialogHost, serverLink: ServerLink);
        Content = shell;
        await shell.InitializeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Ein Fehler im UI-Thread ist protokolliert (Program, UnhandledExceptionLogger); nach der Anmeldung meldet ihn die Shell unten (T-055).
    /// Vor der Anmeldung gibt es keine Meldungsfläche: Start und Anmeldung zeigen ihre eigenen Fehler.
    /// </summary>
    public void ReportUiError() => (Content as ShellViewModel)?.ReportUnexpectedError();
}
