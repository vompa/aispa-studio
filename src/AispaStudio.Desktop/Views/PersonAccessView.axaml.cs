using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace AispaStudio.Desktop.Views;

/// <summary>Reiter „Zugang und Rolle“ (R05 Slice 98). Nur der Kopier-Knopf braucht die Ansicht (Zwischenablage); alles andere steht im Modell.</summary>
public partial class PersonAccessView : UserControl
{
    public PersonAccessView() => InitializeComponent();

    private async void OnCopyLinkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PersonAccessViewModel { InviteLink: { } link } model)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(link).ConfigureAwait(true);
            model.LinkCopied(true);
            return;
        }

        InviteLinkField.Focus();
        InviteLinkField.SelectAll();
        model.LinkCopied(false);
    }
}
