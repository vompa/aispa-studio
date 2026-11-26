using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// KI des Servers (R05 Slice 97, ADR 0057), native Maske nach <c>AiTab.tsx</c>: Stand von Claude und vom lokalen Modell, Fortschritt des Downloads und „Modell herunterladen“.
/// Den Claude-Schlüssel trägt die Inhaberin hier ein, prüft und entfernt ihn (T-111, ADR 0103); „Speichern“ speichert sofort, deshalb gibt es keine Leiste „Speichern / Verwerfen“.
/// Ist er in der Konfiguration des Servers gesetzt, gilt er, und das Feld ist gesperrt. Der Schlüssel kommt nie zurück, nur seine Maske. Solange das Modell lädt,
/// fragt die Maske alle zwei Sekunden nach (ihr Abstand ist ein Parameter, damit Tests ihn kurz halten); <see cref="Dispose"/> beendet die Nachfrage.
/// </summary>
public sealed partial class AiViewModel : ViewModelBase, INativeArea, IDisposable
{
    private readonly IWorkstationApi _api;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _stop = new();
    private AiStatus? _status;
    private ClaudeKeyStatus? _key;
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

    public string ClaudeText => _status is { Claude: true } ? "Eingeschaltet." : "Aus: es ist kein API-Schlüssel gesetzt.";

    /// <summary>Neuer Schlüssel; nach dem Speichern wieder leer.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveKeyCommand))]
    public partial string ApiKeyInput { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditKey), nameof(CanRemoveKey))]
    [NotifyCanExecuteChangedFor(nameof(SaveKeyCommand), nameof(CheckKeyCommand), nameof(RemoveKeyCommand))]
    public partial bool IsKeyBusy { get; private set; }

    /// <summary>Der Schlüssel steht in der Konfiguration des Servers (<c>Ai:Claude:ApiKey</c>) und hat Vorrang.</summary>
    public bool KeyFromConfig => _key?.Source == ClaudeKeySource.Config;

    public bool CanEditKey => _key is not null && !KeyFromConfig && !IsKeyBusy;

    public string? KeyDisabledHint => KeyFromConfig
        ? "Der Schlüssel ist in der Konfiguration des Servers gesetzt und gilt vor einem Eintrag hier. Ändern lässt er sich nur dort."
        : null;

    public string KeyStatusText => _key switch
    {
        { Source: ClaudeKeySource.Config } key => $"Aus der Konfiguration gesetzt: {key.Mask}",
        { Source: ClaudeKeySource.App } key => $"Eingetragen: {key.Mask}",
        _ => "Kein Schlüssel eingetragen.",
    };

    public bool CanRemoveKey => _key?.Source == ClaudeKeySource.App && !IsKeyBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKeyMessage))]
    public partial string? KeyMessage { get; private set; }

    public bool HasKeyMessage => KeyMessage is not null;

    [ObservableProperty]
    public partial InfoBarSeverity KeyMessageSeverity { get; private set; } = InfoBarSeverity.Info;

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

    /// <summary>„Speichern“ speichert den Schlüssel sofort; ein getippter, nicht gespeicherter Schlüssel löst beim Verlassen keine Rückfrage aus.</summary>
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
        // Ohne Stand des Schlüssels bleibt das Feld gesperrt; der Stand der KI ist trotzdem zu sehen.
        var key = await _api.GetAsync<ClaudeKeyStatus>("api/system/ai/claude-key").ConfigureAwait(true);
        if (key.Value is { } keyStatus)
        {
            ApplyKey(keyStatus);
        }
        else
        {
            ShowKeyMessage(key.Error ?? "Der Stand des Claude-Schlüssels wurde nicht geladen.", InfoBarSeverity.Error);
        }

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

    [RelayCommand]
    private void CloseKeyMessage() => KeyMessage = null;

    private bool CanSaveKey() => CanEditKey && ApiKeyInput.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanSaveKey))]
    private async Task SaveKeyAsync()
    {
        IsKeyBusy = true;
        var result = await _api.PutAsync<ClaudeKeyStatus>("api/system/ai/claude-key", new ClaudeKeyInput(ApiKeyInput.Trim())).ConfigureAwait(true);
        IsKeyBusy = false;
        if (result.Value is not { } key)
        {
            ShowKeyMessage(result.Error ?? "Der Schlüssel wurde nicht gespeichert.", InfoBarSeverity.Error);
            return;
        }

        ApiKeyInput = "";
        ApplyKey(key);
        ShowKeyMessage("Schlüssel gespeichert. Claude ist ab sofort eingeschaltet; „Prüfen“ zeigt, ob er funktioniert.", InfoBarSeverity.Success);
        await RefreshClaudeAsync().ConfigureAwait(true);
    }

    private bool CanCheckKey() => _key is { IsSet: true } && !IsKeyBusy;

    [RelayCommand(CanExecute = nameof(CanCheckKey))]
    private async Task CheckKeyAsync()
    {
        IsKeyBusy = true;
        var result = await _api.PostAsync<ClaudeKeyCheck>("api/system/ai/claude-key/check", null).ConfigureAwait(true);
        IsKeyBusy = false;
        if (result.Value is not { } check)
        {
            ShowKeyMessage(result.Error ?? "Der Schlüssel wurde nicht geprüft.", InfoBarSeverity.Error);
            return;
        }

        ShowKeyMessage(check.Message, check.Outcome switch
        {
            ClaudeKeyCheckOutcome.Valid => InfoBarSeverity.Success,
            ClaudeKeyCheckOutcome.Invalid => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Warning,
        });
    }

    [RelayCommand(CanExecute = nameof(CanRemoveKey))]
    private async Task RemoveKeyAsync()
    {
        var answer = await AskAsync(new ConfirmRequest("Claude-Schlüssel entfernen?",
            "Claude ist danach für diesen Betrieb aus: keine Antworten an Gäste, Übersetzungen und Zusammenfassungen durch die KI mehr, bis wieder ein Schlüssel eingetragen ist.",
            "Entfernen")).ConfigureAwait(true);
        if (!answer.IsConfirmed)
        {
            return;
        }

        IsKeyBusy = true;
        var result = await _api.DeleteAsync<ClaudeKeyStatus>("api/system/ai/claude-key").ConfigureAwait(true);
        IsKeyBusy = false;
        if (result.Value is not { } key)
        {
            ShowKeyMessage(result.Error ?? "Der Schlüssel wurde nicht entfernt.", InfoBarSeverity.Error);
            return;
        }

        ApplyKey(key);
        ShowKeyMessage("Schlüssel entfernt. Claude ist aus.", InfoBarSeverity.Success);
        await RefreshClaudeAsync().ConfigureAwait(true);
    }

    private void ShowKeyMessage(string message, InfoBarSeverity severity)
    {
        KeyMessage = message;
        KeyMessageSeverity = severity;
    }

    /// <summary>Nach Speichern oder Entfernen den Stand „Eingeschaltet / Aus“ neu holen; schlägt das fehl, bleibt der alte stehen.</summary>
    private async Task RefreshClaudeAsync()
    {
        if ((await _api.GetAsync<AiStatus>("api/system/ai").ConfigureAwait(true)).Value is { } status)
        {
            Apply(status);
        }
    }

    private void ApplyKey(ClaudeKeyStatus key)
    {
        _key = key;
        foreach (var name in new[] { nameof(KeyFromConfig), nameof(CanEditKey), nameof(KeyDisabledHint), nameof(KeyStatusText), nameof(CanRemoveKey) })
        {
            OnPropertyChanged(name);
        }

        SaveKeyCommand.NotifyCanExecuteChanged();
        CheckKeyCommand.NotifyCanExecuteChanged();
        RemoveKeyCommand.NotifyCanExecuteChanged();
    }

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
