using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AispaStudio.Desktop.Views;

/// <summary>Absenderregeln im Bereich „Postfach &amp; Nummern“ (R05 Slice 97).</summary>
public partial class SenderRulesSection : UserControl
{
    public SenderRulesSection() => InitializeComponent();

    /// <summary>Die Notiz einer Regel wird beim Verlassen des Feldes gespeichert, wenn sie sich geändert hat.</summary>
    private async void OnNoteLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SenderRuleRow row })
        {
            await row.CommitNoteAsync().ConfigureAwait(true);
        }
    }
}
