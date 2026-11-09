using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Ein Dokument der API für den Knopfblock (ADR 0091): Titel für die Rückfrage („Rechnung K1-142“), Pfad des PDF, Pfad des Versands (ohne: kein Knopf „Per Mail senden“),
/// das Recht und der Hinweis, wenn es fehlt (<c>ui:DisabledHint</c>). <paramref name="AlternativePdfPath"/>: ein zweites Format zum Speichern, z. B. der Beleg im Bon-Format (im Menü an „PDF speichern“);
/// <paramref name="PrintLabel"/>: Beschriftung des Druckknopfs, wo ein zweiter „Drucken“ steht (Kassa: Bondrucker).
/// </summary>
public sealed record DocumentSource(string Title, string PdfPath, string? SendPath = null, bool IsAllowed = true, string? DisabledHint = null,
    string? AlternativePdfPath = null, string AlternativeLabel = "PDF im Bon-Format speichern", string PrintLabel = "Drucken");

/// <summary>
/// Der Knopfblock „Drucken · PDF speichern · Per Mail senden“ (ADR 0091): lädt das PDF von der API, speichert es über den Dialog des Fensters mit dem Dateinamen
/// der API, druckt es über den Standarddrucker von Windows oder sendet es nach einer Rückfrage mit Adresse, Betreff und Text (<see cref="SendDocumentDialogViewModel"/>).
/// Erfolge sagt die Maske als Toast, Fehler stehen unter den Knöpfen. Der Server rechnet, die App zeigt: Inhalt, Dateiname und Mailtext kommen von der API.
/// </summary>
public sealed partial class DocumentActionsViewModel(DocumentSource source, IAispaApi api, IConfirmDialogs? dialogs, IDocumentPrinter? printer = null)
    : ObservableObject, ISavesFiles
{
    public const string FileTypeName = "PDF-Datei";

    private readonly IDocumentPrinter _printer = printer ?? new ShellDocumentPrinter();

    public DocumentSource Source => source;

    public IFileSaver? Files { get; set; }

    /// <summary>Meldung unten (Toast, ohne Punkt): „Gespeichert: Beleg-K1-142.pdf“, „Gesendet an anna@…“, „Gedruckt“.</summary>
    public Action<string>? ShowToast { get; init; }

    public bool IsAllowed => source.IsAllowed;

    public bool HasSend => source.SendPath is not null;

    public bool HasAlternative => source.AlternativePdfPath is not null;

    public string AlternativeLabel => source.AlternativeLabel;

    public string PrintLabel => source.PrintLabel;

    public string? DisabledHint => source.IsAllowed ? null : source.DisabledHint ?? "Dafür fehlt das Recht.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand), nameof(SaveCommand), nameof(SaveAlternativeCommand), nameof(SendCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task PrintAsync()
    {
        if (await LoadAsync(source.PdfPath).ConfigureAwait(true) is not { } file)
        {
            return;
        }

        var problem = await _printer.PrintAsync(file.FileName, file.Content, CancellationToken.None).ConfigureAwait(true);
        ShowToast?.Invoke(problem ?? "Gedruckt");
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task SaveAsync() => SaveFromAsync(source.PdfPath);

    /// <summary>Das zweite Format, z. B. der Beleg schmal wie ein Bon.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveAlternative))]
    private Task SaveAlternativeAsync() => SaveFromAsync(source.AlternativePdfPath!);

    private async Task SaveFromAsync(string path)
    {
        if (await LoadAsync(path).ConfigureAwait(true) is not { } file || Files is null)
        {
            return;
        }

        try
        {
            if (await Files.SaveAsync(file.FileName, FileTypeName, ["*.pdf"], file.Content).ConfigureAwait(true))
            {
                ShowToast?.Invoke($"Gespeichert: {file.FileName}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = $"Nicht gespeichert: {e.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (source.SendPath is not { } path || dialogs is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Error = null;
            var draft = await api.GetAsync<DocumentMailDraft>(path).ConfigureAwait(true);
            if (draft.Value is null)
            {
                Error = draft.Error ?? "Die Vorbelegung der Mail kam nicht an.";
                return;
            }

            SendDocumentDialogViewModel? dialog = null;
            var answer = await dialogs.ShowAsync(reply => dialog = new SendDocumentDialogViewModel(source.Title, draft.Value, reply)).ConfigureAwait(true);
            if (!answer.IsConfirmed || dialog is null)
            {
                return;
            }

            var to = dialog.To.Trim();
            var sent = await api.PostAsync<object>(path, new SendDocumentRequest(to, dialog.Subject.Trim(), dialog.Text)).ConfigureAwait(true);
            if (sent.Ok)
            {
                ShowToast?.Invoke($"Gesendet an {to}");
            }
            else
            {
                Error = sent.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseError() => Error = null;

    private bool CanWork() => source.IsAllowed && !IsBusy;

    private bool CanSaveAlternative() => CanWork() && HasAlternative;

    private bool CanSend() => source.IsAllowed && HasSend && !IsBusy;

    private async Task<ApiFile?> LoadAsync(string path)
    {
        IsBusy = true;
        try
        {
            Error = null;
            var result = await api.GetFileAsync(path).ConfigureAwait(true);
            if (result.Value is null)
            {
                Error = result.Error ?? "Das Dokument kam nicht an.";
            }

            return result.Value;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
