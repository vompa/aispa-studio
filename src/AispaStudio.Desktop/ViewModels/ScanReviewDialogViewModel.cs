using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Eine Frage im Prüfdialog: der Vorschlag der KI; gelb markiert, bis jemand die Antwort angefasst hat.</summary>
public sealed partial class ScanQuestionRow : ObservableObject
{
    private readonly AnamnesisQuestion _question;
    private readonly string? _reason;
    private readonly Action _changed;

    public ScanQuestionRow(AnamnesisQuestion question, ExtractedAnswer? suggestion, Action changed)
    {
        _question = question;
        _reason = suggestion?.Reason;
        _changed = () => { };
        Yes = suggestion?.Yes;
        Details = suggestion?.Details ?? "";
        IsUncertain = suggestion?.Uncertain == true;
        _changed = changed;
    }

    public string Key => _question.Key;

    public string Label => _question.Label;

    public string DetailsLabel => _question.DetailsLabel ?? "Antwort";

    public bool IsYesNo => _question.Kind == AnamnesisQuestionKind.YesNo;

    public bool IsMissing => IsYesNo && Yes is null;

    public string UncertainText => $"Unsicher: {_reason ?? "mit dem Foto vergleichen"}";

    public string YesName => $"{Label}: Ja";

    public string NoName => $"{Label}: Nein";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(YesChecked), nameof(NoChecked), nameof(IsMissing))]
    public partial bool? Yes { get; private set; }

    [ObservableProperty]
    public partial string Details { get; set; }

    [ObservableProperty]
    public partial bool IsUncertain { get; private set; }

    public bool YesChecked
    {
        get => Yes == true;
        set => Choose(value, true);
    }

    public bool NoChecked
    {
        get => Yes == false;
        set => Choose(value, false);
    }

    public AnamnesisAnswer ToAnswer() => new(Key, IsYesNo ? Yes : null, Details.Length == 0 ? null : Details);

    private void Choose(bool isChecked, bool answer)
    {
        if (isChecked)
        {
            Yes = answer;
            Touch();
        }
    }

    partial void OnDetailsChanged(string value) => Touch();

    private void Touch()
    {
        IsUncertain = false;
        _changed();
    }
}

/// <summary>
/// Prüfen eines fotografierten Papierbogens: links das Foto, rechts der Vorschlag der KI. Gespeichert wird nur mit „Übernehmen“; die Prüfungen liegen beim Server,
/// die Sperre des Knopfs führt nur vorab durch den Bogen. Die Antworten und das Foto bleiben im Speicher, bis der Dialog geschlossen ist.
/// </summary>
public sealed partial class ScanReviewDialogViewModel : ConfirmDialogViewModel, IDisposable
{
    private const string PhotoFailed = "Foto nicht geladen. Den Bogen schließen und erneut öffnen.";

    private static readonly TimeSpan ReadingPause = TimeSpan.FromSeconds(2);

    private readonly IWorkstationApi _api;
    private readonly string _path;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _closing = new();
    private readonly CancellationToken _closingToken;

    public ScanReviewDialogViewModel(IWorkstationApi api, string anamnesisPath, Guid scanId, TimeProvider time, Action<ConfirmResult> answer)
        : base(new ConfirmRequest("Papierbogen prüfen", AiLabels.Disclaimer, "Übernehmen", CancelText: "Schließen"), answer)
    {
        _api = api;
        _path = $"{anamnesisPath}/scans/{scanId}";
        _time = time;
        _closingToken = _closing.Token;
    }

    [ObservableProperty]
    public partial Bitmap? Photo { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhotoStatus))]
    public partial string? PhotoStatus { get; private set; } = "Foto wird geladen …";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasForm))]
    public partial IReadOnlyList<ScanQuestionRow> Questions { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsReading { get; private set; } = true;

    [ObservableProperty]
    public partial string ReaderText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsReadByAi { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string? Note { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    public partial string Signature { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSignatureUncertain { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    public partial bool IsSigned { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    public partial bool HasConsentOnPaper { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasForm => Questions.Count > 0;

    public bool HasPhotoStatus => PhotoStatus is not null;

    public bool HasNote => !string.IsNullOrEmpty(Note);

    public bool HasError => Error is not null;

    private int MissingCount => Questions.Count(q => q.IsMissing);

    private int OpenCount => Questions.Count(q => q.IsUncertain) + (IsSignatureUncertain ? 1 : 0);

    public string MissingText => MissingCount > 0 ? $"Noch ohne Ja/Nein: {MissingCount} Frage(n)." : "";

    public string OpenText => OpenCount > 0 ? $"Noch gelb: {OpenCount} Feld(er)." : "";

    public bool HasMissing => MissingCount > 0;

    public bool HasOpen => OpenCount > 0;

    public bool CanAccept => HasForm && !IsBusy && MissingCount == 0 && IsSigned && HasConsentOnPaper && !string.IsNullOrWhiteSpace(Signature);

    /// <summary>Solange die KI liest, fragt der Dialog alle zwei Sekunden nach; das Hochladen selbst wartet nicht darauf.</summary>
    public async Task LoadAsync()
    {
        _ = LoadPhotoAsync();
        try
        {
            var result = await _api.GetAsync<AnamnesisScanDto>(_path, _closingToken).ConfigureAwait(true);
            while (result.Value is { Status: AnamnesisScanStatus.Reading })
            {
                await Task.Delay(ReadingPause, _time, _closingToken).ConfigureAwait(true);
                result = await _api.GetAsync<AnamnesisScanDto>(_path, _closingToken).ConfigureAwait(true);
            }

            var form = await _api.GetAsync<AnamnesisFormDefinition>("api/online/anamnesis/form?language=de", _closingToken).ConfigureAwait(true);
            if (_closing.IsCancellationRequested)
            {
                return;
            }

            if (result.Value is not { } scan || form.Value is not { } definition)
            {
                IsReading = false;
                Error = "Der Bogen konnte nicht geladen werden.";
                return;
            }

            Show(scan, definition);
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested)
        {
            // Der Dialog ist zu; es wartet niemand mehr auf den Bogen.
        }
    }

    private void Show(AnamnesisScanDto scan, AnamnesisFormDefinition form)
    {
        IsReading = false;
        IsReadByAi = scan.Reader != "Manuell";
        ReaderText = IsReadByAi ? $"Gelesen von {scan.Reader}. Gelb: mit dem Foto vergleichen." : "Keine KI-Übernahme: vom Foto übertragen.";
        Note = scan.Note;
        Signature = scan.Signature ?? "";
        HasConsentOnPaper = scan.ConsentChecked == true;
        IsSignatureUncertain = scan.SignatureUncertain;
        Questions = [.. form.Questions.Select(q => new ScanQuestionRow(q, scan.Answers.FirstOrDefault(a => a.Key == q.Key), RefreshCounts))];
        RefreshCounts();
    }

    private async Task LoadPhotoAsync()
    {
        try
        {
            var image = await _api.GetBytesAsync($"{_path}/image", _closingToken).ConfigureAwait(true);
            if (_closing.IsCancellationRequested)
            {
                return;
            }

            if (image.Value is not { } bytes)
            {
                PhotoStatus = PhotoFailed;
                return;
            }

            using var stream = new MemoryStream(bytes);
            Photo = new Bitmap(stream);
            PhotoStatus = null;
        }
        catch (OperationCanceledException)
        {
            // Ist der Dialog zu, braucht das Foto niemand mehr; sonst lief das Zeitlimit ab.
            if (!_closing.IsCancellationRequested)
            {
                PhotoStatus = PhotoFailed;
            }
        }
    }

    protected override bool CanCancel() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => CancelCommand.NotifyCanExecuteChanged();

    partial void OnSignatureChanged(string value)
    {
        if (HasForm)
        {
            IsSignatureUncertain = false;
            RefreshCounts();
        }
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(MissingText));
        OnPropertyChanged(nameof(OpenText));
        OnPropertyChanged(nameof(HasMissing));
        OnPropertyChanged(nameof(HasOpen));
        OnPropertyChanged(nameof(CanAccept));
    }

    [RelayCommand]
    private async Task AcceptAsync()
    {
        IsBusy = true;
        Error = null;
        var confirmation = new AnamnesisScanConfirmation([.. Questions.Select(q => q.ToAnswer())], HasConsentOnPaper, IsSigned, Signature, null);
        var result = await _api.PostAsync<AnamnesisRecordDto>($"{_path}/confirm", confirmation).ConfigureAwait(true);
        IsBusy = false;
        if (result.Error is null)
        {
            Answer(new ConfirmResult(ConfirmChoice.Confirmed));
        }
        else
        {
            Error = "Nicht übernommen. Alle Fragen, Einwilligung und Unterschrift prüfen.";
        }
    }

    [RelayCommand]
    private async Task DiscardAsync()
    {
        IsBusy = true;
        Error = null;
        var result = await _api.DeleteAsync<object>(_path).ConfigureAwait(true);
        IsBusy = false;
        if (result.Error is null)
        {
            Answer(new ConfirmResult(ConfirmChoice.Alternative));
        }
        else
        {
            Error = "Foto konnte nicht verworfen werden.";
        }
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        var photo = Photo;
        Photo = null;
        photo?.Dispose();
    }
}
