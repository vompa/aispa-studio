using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.LiveTests.Launcher;

/// <summary>Eine Rolle zur Auswahl; ohne Ablauf ist sie ausgegraut („kommt noch“).</summary>
internal sealed record RoleOption(string Name, LiveJourney? Journey)
{
    public bool IsAvailable => Journey is not null;

    public string Display => IsAvailable ? Name : $"{Name} (kommt noch)";
}

/// <summary>Ein Thema mit Häkchen.</summary>
internal sealed partial class TopicOption(LiveTopic topic) : ObservableObject
{
    public LiveTopic Topic { get; } = topic;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = true;
}

/// <summary>Eine Zeile der Schrittliste.</summary>
internal sealed partial class StepRow(int number, string name) : ObservableObject
{
    public int Number { get; } = number;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial string Status { get; set; } = "läuft …";

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOk), nameof(IsFailed))]
    public partial bool? Passed { get; set; }

    public bool IsOk => Passed == true;

    public bool IsFailed => Passed == false;

    public bool HasError => Error is not null;

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));
}

/// <summary>
/// Starter-App: Rolle und Themen wählen, Server prüfen, Lauf im Hintergrund starten und mit Knöpfen steuern. Die Schritte
/// kommen über die Ereignisse von <see cref="LiveSession"/> und werden über den Dispatcher in die Liste gesetzt.
/// </summary>
internal sealed partial class MainViewModel : ObservableObject
{
    private static readonly string[] SpeedKeys = ["slow", "normal", "fast"];
    private readonly Func<Rectangle?> _appArea;
    private readonly string? _appPath;
    private readonly string _outputRoot;
    private LiveSession? _session;

    public MainViewModel(Func<Rectangle?> appArea)
    {
        _appArea = appArea;
        var root = FindRepositoryRoot();
        _appPath = root is null ? null : Path.Combine(root, "src", "AispaStudio.Desktop", "bin", "Release", "net10.0", "AispaStudio.Desktop.exe");
        _outputRoot = Path.Combine(root ?? AppContext.BaseDirectory, ".test-logs", "live");
        AppMissing = _appPath is null || !File.Exists(_appPath);
        SelectedRole = Roles[0];
        _ = CheckServerAsync();
    }

    public IReadOnlyList<RoleOption> Roles { get; } =
    [
        new("Rezeption", ReceptionJourney.Journey),
        new("Inhaberin", null),
        new("Mitarbeiterin", null),
    ];

    public ObservableCollection<TopicOption> Topics { get; } = [];

    public ObservableCollection<StepRow> Steps { get; } = [];

    public IReadOnlyList<string> Speeds { get; } = ["langsam (1,5 s)", "normal (0,5 s)", "schnell"];

    public bool AppMissing { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial RoleOption? SelectedRole { get; set; }

    [ObservableProperty]
    public partial string SetupName { get; set; } = "";

    [ObservableProperty]
    public partial int SpeedIndex { get; set; }

    [ObservableProperty]
    public partial bool StartPaused { get; set; }

    [ObservableProperty]
    public partial string ApiUrl { get; set; } = "http://localhost:5086/";

    [ObservableProperty]
    public partial string ServerText { get; set; } = "Server wird geprüft …";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool ServerReady { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(TogglePauseCommand), nameof(StepOnceCommand), nameof(ShotCommand), nameof(StopCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseText))]
    [NotifyCanExecuteChangedFor(nameof(StepOnceCommand))]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Bereit.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenReportCommand), nameof(OpenFolderCommand))]
    public partial string? RunFolder { get; set; }

    public bool IsIdle => !IsRunning;

    public string PauseText => IsPaused ? "Weiter" : "Pause";

    partial void OnSelectedRoleChanged(RoleOption? value)
    {
        Topics.Clear();
        foreach (var topic in value?.Journey?.Topics ?? [])
        {
            Topics.Add(new TopicOption(topic));
        }

        SetupName = value?.Journey?.SetupName ?? "Für diese Rolle gibt es noch keinen Ablauf.";
    }

    partial void OnApiUrlChanged(string value) => _ = CheckServerAsync();

    [RelayCommand]
    private async Task CheckServerAsync()
    {
        ServerReady = false;
        ServerText = "Server wird geprüft …";
        if (!Uri.TryCreate(ApiUrl, UriKind.Absolute, out var api))
        {
            ServerText = "Adresse ungültig.";
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var status = JsonDocument.Parse(await http.GetStringAsync(new Uri(api, "api/system/status")));
            var phase = status.RootElement.GetProperty("startup").GetProperty("phase").GetString();
            ServerReady = phase == "Ready";
            ServerText = ServerReady ? "Server bereit." : $"Server startet noch ({phase}).";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            ServerText = "Server nicht erreichbar. Zuerst: docker compose up -d --build";
        }
    }

    private bool CanStart() => !IsRunning && ServerReady && !AppMissing && SelectedRole?.Journey is not null;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        var journey = SelectedRole!.Journey!;
        var topics = Topics.Where(topic => topic.IsChecked).Select(topic => topic.Topic).ToList();
        try
        {
            journey.CheckDependencies(topics);
        }
        catch (ArgumentException ex)
        {
            StatusText = ex.Message;
            return;
        }

        var options = new LiveOptions(_appPath!, new Uri(ApiUrl), LiveOptions.SpeedOf(SpeedKeys[SpeedIndex]), StartPaused, _outputRoot,
            topics.Select(topic => topic.Key).ToList(), ConsoleKeys: false, AppArea: _appArea());
        Steps.Clear();
        RunFolder = null;
        IsPaused = StartPaused;
        IsRunning = true;
        StatusText = "AISPA-App startet …";
        _ = Task.Run(() => Run(journey, topics, options));
    }

    private void Run(LiveJourney journey, IReadOnlyList<LiveTopic> topics, LiveOptions options)
    {
        string result;
        try
        {
            using var session = LiveSession.Start(options, journey.Role);
            session.StepStarted += (number, name) => Dispatcher.UIThread.Post(() =>
            {
                Steps.Add(new StepRow(number, name));
                StatusText = $"Schritt {number}: {name}";
            });
            session.StepFinished += step => Dispatcher.UIThread.Post(() => Finish(step));
            Dispatcher.UIThread.Post(() =>
            {
                _session = session;
                RunFolder = session.Folder;
                StatusText = "Lauf beginnt …";
                RefreshControls();
            });
            journey.Run(session, topics);
            result = session.Result ?? "";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            result = $"Lauf nicht gestartet: {ex.Message}";
        }

        Dispatcher.UIThread.Post(() =>
        {
            _session = null;
            IsRunning = false;
            IsPaused = false;
            StatusText = result;
        });
    }

    private void Finish(LiveStepResult step)
    {
        var row = Steps.LastOrDefault(row => row.Number == step.Number);
        if (row is null)
        {
            return;
        }

        row.Passed = step.Passed;
        row.Status = $"{(step.Passed ? "✓" : "✗")} {step.Duration.TotalSeconds:0.0} s";
        row.Error = step.Error;
    }

    private bool HasSession() => IsRunning && _session is not null;

    [RelayCommand(CanExecute = nameof(HasSession))]
    private void TogglePause() => IsPaused = _session!.Control.TogglePause();

    private bool CanStepOnce() => HasSession() && IsPaused;

    [RelayCommand(CanExecute = nameof(CanStepOnce))]
    private void StepOnce() => _session!.Control.StepOnce();

    [RelayCommand(CanExecute = nameof(HasSession))]
    private void Shot()
    {
        _session!.Control.RequestShot();
        StatusText = "Bild kommt vor dem nächsten Schritt.";
    }

    [RelayCommand(CanExecute = nameof(HasSession))]
    private void Stop()
    {
        _session!.Control.Stop();
        StatusText = "Stopp vor dem nächsten Schritt …";
    }

    private bool HasFolder() => RunFolder is not null;

    [RelayCommand(CanExecute = nameof(HasFolder))]
    private void OpenReport() => Open(Path.Combine(RunFolder!, "bericht.md"));

    [RelayCommand(CanExecute = nameof(HasFolder))]
    private void OpenFolder() => Open(RunFolder!);

    private void RefreshControls()
    {
        TogglePauseCommand.NotifyCanExecuteChanged();
        StepOnceCommand.NotifyCanExecuteChanged();
        ShotCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            StatusText = $"Öffnen nicht möglich: {ex.Message}";
        }
    }

    /// <summary>Sucht nach oben den Ordner mit <c>AispaStudio.slnx</c>; die Starter-App liegt im Build-Ordner des Repositorys.</summary>
    private static string? FindRepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "AispaStudio.slnx")))
            {
                return folder.FullName;
            }
        }

        return null;
    }
}
