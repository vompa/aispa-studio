using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Stand für den Startbildschirm (Design-Brief 5.31): ob er zu sehen ist, was die Statuszeile sagt und wann der Hinweis „dauert länger“ kommt.</summary>
public partial class MainViewModel
{
    private const string ConnectingStep = "Verbinden";

    private const string ServerStartingText = "Server wird gestartet …";

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

    /// <summary>Kein Start mehr und keine Verbindung: die Karte „Keine Verbindung zum Server“ steht auf der Petrolfläche.</summary>
    public bool IsOffline => !IsConnected && !IsStarting;

    private void ShowStartupStep(string step, string status)
    {
        StartupStep = step;
        StartupStatus = status;
    }

    /// <summary>Beginnt den Start; die zurückgegebene Quelle beendet die Wartezeiten für Startbildschirm und Hinweis.</summary>
    private CancellationTokenSource BeginStartup()
    {
        // Nach „Erneut versuchen“ steht der Startbildschirm schon: er bleibt, statt für 300 ms zu verschwinden.
        IsStartupScreenShown = IsStartupScreenShown && !IsConnected;
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
        if (!IsConnected)
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
