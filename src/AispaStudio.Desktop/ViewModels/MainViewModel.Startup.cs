using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Stand für den Startbildschirm (Design-Brief 5.31): ob er zu sehen ist, was die Statuszeile sagt und wann der Hinweis „dauert länger“ kommt.</summary>
public partial class MainViewModel
{
    private const string ConnectingStep = "Verbinden";

    private const string ServerStartingText = "AISPA Studio wird gestartet …";

    /// <summary>
    /// Vorgabe für <see cref="StartupScreenDelay"/>; ein Haken für die Bildtests, die das Fenster mit echten Timern öffnen: dort erscheint der
    /// Startbildschirm nie, auch wenn der Start unter Last länger als 300 ms dauert. Tests des Startbildschirms setzen die Zeit selbst.
    /// </summary>
    internal static TimeSpan DefaultStartupScreenDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Antwortet die API schneller, erscheint kein Startbildschirm; das Fenster zeigt dann gleich den Kalender oder die Anmeldung.</summary>
    public TimeSpan StartupScreenDelay { get; init; } = DefaultStartupScreenDelay;

    /// <summary>Danach steht unter der Statuszeile „Das dauert länger als üblich.“ mit den technischen Angaben.</summary>
    public TimeSpan SlowStartAfter { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Der Start läuft: Verbindung, Datenbank, gemerkte Anmeldung.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    public partial bool IsStarting { get; private set; }

    /// <summary>Logo, Name und Stand sind zu sehen: der Start dauert länger als <see cref="StartupScreenDelay"/> oder endete ohne Verbindung.</summary>
    [ObservableProperty]
    public partial bool IsStartupScreenShown { get; private set; }

    [ObservableProperty]
    public partial bool IsSlowStart { get; private set; }

    /// <summary>Kurzer Stand in der Statuszeile („Datenbank wird vorbereitet …“).</summary>
    [ObservableProperty]
    public partial string StartupStatus { get; private set; } = ServerStartingText;

    /// <summary>Phase für die technischen Angaben („Migrating“, „Seeding“).</summary>
    [ObservableProperty]
    public partial string StartupStep { get; private set; } = ConnectingStep;

    /// <summary>Die Anmeldung braucht eine Verbindung. Die Arbeitsfläche ist mit gemerktem Zugriff auch offline bedienbar (ADR 0005).</summary>
    public bool CanShowContent => CanStart && !IsIdleSigningOut && Content is not null && (IsConnected || Content is ShellViewModel);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(CheckStatusCommand))]
    [NotifyPropertyChangedFor(nameof(CanShowContent), nameof(IsOffline))]
    public partial bool CanStart { get; private set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocalSessionFailure))]
    public partial string? LocalFailureTitle { get; private set; }

    [ObservableProperty]
    public partial string? LocalFailureMessage { get; private set; }

    public bool HasLocalSessionFailure => LocalFailureTitle is not null;

    /// <summary>Der Start ist beendet und es gibt weder eine Verbindung noch eine Arbeitsfläche aus dem lokalen Stand.</summary>
    public bool IsOffline => CanStart && !CanShowContent && !IsStarting && !IsIdleSigningOut;

    private StartChoiceViewModel? _startChoice;

    /// <summary>Gesetzt, wenn auf diesem PC noch keine Betriebsart gewählt wurde: antwortet dann kein Server, steht die Startauswahl statt „Keine Verbindung“ (ADR 0104).</summary>
    public StartChoiceViewModel? StartChoice
    {
        get => _startChoice;
        init
        {
            _startChoice = value;
            if (value is not null)
            {
                value.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(StartChoiceViewModel.IsShown))
                    {
                        OnPropertyChanged(nameof(IsStartChoiceShown));
                    }
                };
            }
        }
    }

    /// <summary>Die Startauswahl steht auf dem Bildschirm; die Karte „Keine Verbindung zum Server“ ist dann ausgeblendet.</summary>
    public bool IsStartChoiceShown => StartChoice?.IsShown == true;

    /// <summary>Kurze Probe auf den Standardserver (<see cref="ProbeTimeout"/>); <c>true</c>, wenn die Startauswahl statt des 30-Sekunden-Suchens steht.</summary>
    private async Task<bool> ShowStartChoiceIfNoServerAsync()
    {
        // Mit lokalem Stand (ADR 0005) hat dieser PC schon ein Spa: ein Server, der noch hochfährt, löst keine Startauswahl aus.
        if (StartChoice is not { IsDeclined: false } choice || api.Offline?.Get("api/auth/me") is not null)
        {
            return false;
        }

        try
        {
            using var attempt = new CancellationTokenSource(ProbeTimeout, _time);
            await api.StatusAsync(attempt.Token).ConfigureAwait(true);
            if (!CanStart)
            {
                return true;
            }
            choice.MarkMade();
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            if (!CanStart)
            {
                return true;
            }
            IsConnected = false;
            ConnectionTitle = NoConnectionTitle;
            StatusText = ServerUnreachableAdvice;
            Content = null;
            choice.IsShown = true;
            return true;
        }
    }

    private void ShowStartupStep(string step, string status)
    {
        StartupStep = step;
        StartupStatus = status;
    }

    /// <summary>Beginnt den Start; die zurückgegebene Quelle beendet die Wartezeiten für Startbildschirm und Hinweis.</summary>
    private CancellationTokenSource BeginStartup()
    {
        // Nach „Erneut versuchen“ steht der Startbildschirm schon: er bleibt, statt für 300 ms zu verschwinden.
        IsStartupScreenShown = IsStartupScreenShown && !CanShowContent;
        IsSlowStart = false;
        ShowStartupStep(ConnectingStep, ServerStartingText);
        IsStarting = true;
        var startup = new CancellationTokenSource();
        _ = AfterAsync(StartupScreenDelay, () => IsStartupScreenShown = true, startup.Token);
        _ = AfterAsync(SlowStartAfter, () => IsSlowStart = true, startup.Token);
        return startup;
    }

    private void EndStartup(CancellationTokenSource startup)
    {
        startup.Cancel();
        if (!CanShowContent)
        {
            IsStartupScreenShown = true;
        }

        IsStarting = false;
    }

    private async Task AfterAsync(TimeSpan delay, Action action, CancellationToken token)
    {
        if (delay == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        try
        {
            await Task.Delay(delay, _time, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        action();
    }
}
