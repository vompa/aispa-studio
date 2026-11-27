using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace AispaStudio.Desktop.Views;

/// <summary>Seitenleiste „Mit KI auswerten“; „Kopieren“ braucht die Zwischenablage des Fensters und steht deshalb hier.</summary>
public partial class ManagementAnalysisPanel : UserControl
{
    public ManagementAnalysisPanel() => InitializeComponent();

    private async void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ManagementAnalysisViewModel analysis)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            analysis.MarkCopied(false);
            return;
        }

        await clipboard.SetTextAsync(analysis.CopyText).ConfigureAwait(true);
        analysis.MarkCopied(true);
    }
}
