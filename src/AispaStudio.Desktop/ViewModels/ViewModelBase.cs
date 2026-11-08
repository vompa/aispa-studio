using CommunityToolkit.Mvvm.ComponentModel;

namespace AispaStudio.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Wer Rückfragen stellt (der Dialog des Fensters, Design-Brief 5.27); ohne ihn gilt jede Rückfrage als „Abbrechen“, und es geschieht nichts Zerstörendes.</summary>
    public virtual IConfirmDialogs? Dialogs { get; set; }

    /// <summary>Stellt eine Rückfrage vor Stornieren, Löschen oder Verwerfen und wartet auf die Antwort.</summary>
    protected Task<ConfirmResult> AskAsync(ConfirmRequest request) => Dialogs?.AskAsync(request) ?? Task.FromResult(ConfirmResult.Cancelled);
}
