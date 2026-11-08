using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Wie eine Rückfrage beantwortet wurde.</summary>
public enum ConfirmChoice
{
    /// <summary>„Abbrechen“, Esc oder ein zweiter Dialog: es geschieht nichts.</summary>
    Cancelled,

    /// <summary>Die Aktion (links im Dialog).</summary>
    Confirmed,

    /// <summary>Die zweite Aktion (z. B. „Diesen und alle folgenden“).</summary>
    Alternative,
}

/// <summary>
/// Eine Rückfrage vor Stornieren, Löschen oder Verwerfen (Design-Brief 5.27): der Titel ist eine Frage, der Text nennt zuerst die Folge, die Knöpfe sind die konkrete
/// Antwort in ein bis zwei Wörtern („Löschen“, „Abbrechen“), nie „Ja“, „Nein“ oder „OK“.
/// </summary>
/// <param name="Title">Die Frage, z. B. „Termin stornieren?“.</param>
/// <param name="Message">Die Folge, zuerst: „Der Termin von Mia Muster ist danach storniert.“</param>
/// <param name="ConfirmText">Die Aktion, z. B. „Stornieren“.</param>
/// <param name="IsDestructive">Zerstörend: die Aktion ist rot gefüllt, <b>Abbrechen</b> hat den Anfangsfokus und beantwortet Enter. Sonst ist die Aktion der primäre Knopf mit dem Fokus.</param>
/// <param name="CancelText">Die Antwort, die nichts tut; bei „Änderungen verwerfen?“ ist das „Weiter bearbeiten“.</param>
/// <param name="AlternativeText">Eine zweite Aktion, z. B. „Diesen und alle folgenden“; ohne Text gibt es nur zwei Knöpfe.</param>
/// <param name="InputLabel">Beschriftung eines Textfelds über den Knöpfen (z. B. „Grund“); ohne Text gibt es kein Feld.</param>
/// <param name="InputText">Vorbelegung des Felds.</param>
public sealed record ConfirmRequest(string Title, string Message, string ConfirmText, bool IsDestructive = true, string CancelText = "Abbrechen",
    string? AlternativeText = null, string? InputLabel = null, string? InputText = null);

/// <summary>Die Antwort: welcher Knopf und, wenn die Rückfrage ein Feld hatte, dessen Text.</summary>
public sealed record ConfirmResult(ConfirmChoice Choice, string? Input = null)
{
    public bool IsConfirmed => Choice == ConfirmChoice.Confirmed;

    public static ConfirmResult Cancelled { get; } = new(ConfirmChoice.Cancelled);
}

/// <summary>Wer eine Rückfrage stellt: die Windows-App zeigt sie als Dialog über dem Fenster, Tests antworten selbst.</summary>
public interface IConfirmDialogs
{
    Task<ConfirmResult> AskAsync(ConfirmRequest request);

    /// <summary>
    /// Zeigt eine Rückfrage mit eigenem Inhalt (z. B. <see cref="CancelClosureDialogViewModel"/> mit Liste und Textfeld); sie folgt denselben Regeln wie jede Rückfrage und teilt sich den
    /// einen Dialog des Fensters. Der Dialog bekommt die Rückgabe der Antwort von hier; wer fragt, liest danach Auswahl und Texte aus seinem Objekt.
    /// </summary>
    Task<ConfirmResult> ShowAsync(Func<Action<ConfirmResult>, ConfirmDialogViewModel> create);
}

/// <summary>Der offene Dialog: die Texte der Rückfrage und die drei Antworten.</summary>
public partial class ConfirmDialogViewModel : ObservableObject
{
    private readonly Action<ConfirmResult> _answer;

    public ConfirmDialogViewModel(ConfirmRequest request, Action<ConfirmResult> answer)
    {
        Request = request;
        _answer = answer;
        Input = request.InputText ?? "";
    }

    public ConfirmRequest Request { get; }

    public string Title => Request.Title;

    public string Message => Request.Message;

    public string ConfirmText => Request.ConfirmText;

    public string CancelText => Request.CancelText;

    public string? AlternativeText => Request.AlternativeText;

    public bool HasAlternative => Request.AlternativeText is not null;

    public bool IsDestructive => Request.IsDestructive;

    public bool HasInput => Request.InputLabel is not null;

    public string? InputLabel => Request.InputLabel;

    [ObservableProperty]
    public partial string Input { get; set; }

    /// <summary>Beantwortet die Rückfrage; für Rückfragen mit eigenem Inhalt, die ihre Antwort selbst bilden.</summary>
    protected void Answer(ConfirmResult result) => _answer(result);

    [RelayCommand]
    private void Confirm() => _answer(new ConfirmResult(ConfirmChoice.Confirmed, HasInput ? Input : null));

    [RelayCommand]
    private void Alternative() => _answer(new ConfirmResult(ConfirmChoice.Alternative, HasInput ? Input : null));

    /// <summary>Solange ein Dialog mit eigenem Inhalt speichert, lässt er sich nicht schließen.</summary>
    protected virtual bool CanCancel() => true;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _answer(ConfirmResult.Cancelled);
}

/// <summary>
/// Hält den einen Dialog des Fensters (Design-Brief 5.27: höchstens ein Dialog, nie auf einem Dialog). Eine zweite Rückfrage, während einer offen ist, gilt als „Abbrechen“.
/// </summary>
public sealed partial class ConfirmDialogHost : ObservableObject, IConfirmDialogs
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDialog))]
    public partial ConfirmDialogViewModel? Current { get; private set; }

    public bool HasDialog => Current is not null;

    public Task<ConfirmResult> AskAsync(ConfirmRequest request) => ShowAsync(answer => new ConfirmDialogViewModel(request, answer));

    public Task<ConfirmResult> ShowAsync(Func<Action<ConfirmResult>, ConfirmDialogViewModel> create)
    {
        if (Current is not null)
        {
            return Task.FromResult(ConfirmResult.Cancelled);
        }

        var source = new TaskCompletionSource<ConfirmResult>(TaskCreationOptions.None);
        Current = create(result =>
        {
            Current = null;
            source.TrySetResult(result);
        });
        return source.Task;
    }
}
