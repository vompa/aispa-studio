using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Reiter „Zugang und Rolle“ (R05 Slice 98). Nur der Kopier-Knopf braucht die Ansicht (Zwischenablage); alles andere steht im Modell.</summary>
public partial class PersonAccessView : UserControl
{
    private PersonAccessViewModel? _model;

    public PersonAccessView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.FocusRequested -= FocusField;
            }

            _model = DataContext as PersonAccessViewModel;
            if (_model is not null)
            {
                _model.FocusRequested += FocusField;
            }
        };
    }

    /// <summary>Nach einem Fehler beim Einladen, Anlegen oder Zurücksetzen springt der Fokus ins erste ungültige Feld (masken.md, R8).</summary>
    private void FocusField(string name) => Dispatcher.UIThread.Post(() => this.FindControl<Control>(name)?.Focus());

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
