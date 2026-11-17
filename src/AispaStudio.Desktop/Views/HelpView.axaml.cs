using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Hilfe (ADR 0079): die Hilfe-Seite in einem eingebetteten Browser (<see cref="HelpBrowser"/>), darüber nativ die offenen Fragen an die Hilfe.
/// </summary>
public partial class HelpView : UserControl
{
    public HelpView()
    {
        InitializeComponent();
        // Der Knopf „KI und Hilfe“ steht unter dem Browser, nicht dahinter.
        Host.Margin = new Thickness(0, 0, 0, EmbeddedBrowser.DockHeight);
        Host.Created += () => Placeholder.IsVisible = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as HelpViewModel)?.EnsureShown();
    }
}
