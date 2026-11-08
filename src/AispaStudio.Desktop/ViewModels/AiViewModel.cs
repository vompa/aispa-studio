using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// KI des Servers (R05 Slice 97, ADR 0057), native Maske nach <c>AiTab.tsx</c>: Stand von Claude und vom lokalen Modell, Fortschritt des Downloads und „Modell herunterladen“.
/// Den Claude-Schlüssel setzt der Betreiber als Einstellung des Servers; die Maske zeigt nur „Eingeschaltet“ oder „Aus“. Es gibt nichts zu speichern. Solange das Modell lädt,
/// fragt die Maske alle zwei Sekunden nach (ihr Abstand ist ein Parameter, damit Tests ihn kurz halten); <see cref="Dispose"/> beendet die Nachfrage.
/// </summary>
public sealed partial class AiViewModel : ViewModelBase, INativeArea, IDisposable
{
    private readonly IWorkstationApi _api;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _stop = new();
    private AiStatus? _status;
    private bool _polling;

    public AiViewModel(IWorkstationApi api, TimeSpan? pollInterval = null)
    {
        _api = api;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    public string Title => "KI";

    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public string ClaudeText => _status is { Claude: true } ? "Eingeschaltet." : "Aus: auf diesem Server ist kein API-Schlüssel gesetzt.";

    public string LocalText => _status?.Local switch
    {
        LocalAiState.Unreachable => "Eingerichtet, aber der Dienst für das lokale Modell antwortet nicht.",
        LocalAiState.Missing => "Der Dienst läuft, das Modell ist noch nicht geladen.",
        LocalAiState.Downloading => "Das Modell wird heruntergeladen.",
        LocalAiState.Ready => "Bereit. Ohne Internet liest das lokale Modell Terminwünsche und Fragen ans Team.",
        _ => "Nicht eingerichtet. Ohne Internet arbeitet die Terminannahme dann nach festen Regeln.",
    };

    /// <summary>„Modell: gemma3:4b · 40 %“, wenn ein Modell eingerichtet ist; die Prozent nur während des Downloads.</summary>
    public string? ModelText => _status is { LocalModel: { Length: > 0 } model }
        ? $"Modell: {model}{(IsDownloading && _status.DownloadPercent is { } percent ? $" · {percent} %" : "")}"
        : null;

    public bool HasModelText => ModelText is not null;

    public bool IsDownloading => _status?.Local == LocalAiState.Downloading;

    public double DownloadPercent => _status?.DownloadPercent ?? 0;

    public string? LocalError => string.IsNullOrEmpty(_status?.LocalError) ? null : _status.LocalError;

    public bool HasLocalError => LocalError is not null;

    public bool CanDownload => _status?.Local == LocalAiState.Missing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    [ObservableProperty]
    public partial InfoBarSeverity MessageSeverity { get; private set; } = InfoBarSeverity.Info;

    /// <summary>Es gibt nichts zu speichern.</summary>
    public bool HasUnsavedChanges => false;

    public Task<bool> SaveChangesAsync() => Task.FromResult(true);

    public void Discard()
    {
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var result = await _api.GetAsync<AiStatus>("api/system/ai").ConfigureAwait(true);
        if (result.Value is not { } status)
        {
            LoadError = result.Error ?? "Der Stand der KI wurde nicht geladen.";
            return;
        }

        LoadError = null;
        Apply(status);
        IsLoaded = true;
        if (IsDownloading && !_polling)
        {
            _ = PollWhileDownloadingAsync();
        }
    }

    /// <summary>Lädt beim ersten Öffnen; stand der letzte Stand auf „wird heruntergeladen“ und die Nachfrage läuft nicht mehr (sie ist ausgefallen), lädt es neu.</summary>
    public Task EnsureLoadedAsync() => IsLoaded && !(IsDownloading && !_polling) ? Task.CompletedTask : LoadAsync();

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        var result = await _api.PostAsync<object>("api/system/ai/local/download", null).ConfigureAwait(true);
        if (result.Ok)
        {
            Message = "Download gestartet. Er läuft im Hintergrund weiter, auch beim Verlassen der Seite.";
            MessageSeverity = InfoBarSeverity.Success;
        }
        else
        {
            Message = result.Error ?? "Der Download wurde nicht gestartet.";
            MessageSeverity = InfoBarSeverity.Error;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void CloseMessage() => Message = null;

    public void Open(Guid? id, string? section, string? field)
    {
    }

    /// <summary>Fragt den Stand nach, bis das Modell nicht mehr lädt (oder die Maske beendet wird); schlägt eine Abfrage fehl, bleibt der letzte Stand stehen.</summary>
    private async Task PollWhileDownloadingAsync()
    {
        _polling = true;
        var stop = _stop.Token;
        try
        {
            while (IsDownloading)
            {
                await Task.Delay(_pollInterval, stop).ConfigureAwait(true);
                var result = await _api.GetAsync<AiStatus>("api/system/ai", stop).ConfigureAwait(true);
                if (result.Value is not { } status)
                {
                    return;
                }

                Apply(status);
            }
        }
        catch (OperationCanceledException)
        {
            // Die Maske wurde beendet.
        }
        finally
        {
            _polling = false;
        }
    }

    private void Apply(AiStatus status)
    {
        _status = status;
        foreach (var name in new[]
            {
                nameof(ClaudeText), nameof(LocalText), nameof(ModelText), nameof(HasModelText), nameof(IsDownloading), nameof(DownloadPercent), nameof(LocalError),
                nameof(HasLocalError), nameof(CanDownload),
            })
        {
            OnPropertyChanged(name);
        }

        DownloadCommand.NotifyCanExecuteChanged();
    }
}
