using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace AispaStudio.Desktop.Views;

public partial class RegisterFiscalView : UserControl
{
    private RegisterFiscalViewModel? _model;
    private bool _attached;

    public RegisterFiscalView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model != DataContext)
            {
                _model?.HideSecrets();
                _model?.ObserveConnection(false);
                _model = DataContext as RegisterFiscalViewModel;
                _model?.ObserveConnection(_attached);
            }
        };
        AttachedToVisualTree += (_, _) =>
        {
            _attached = true;
            _model?.ObserveConnection(true);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _attached = false;
            _model?.ObserveConnection(false);
            _model?.HideSecrets();
        };
    }

    private async void CopyValue(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value } && _model?.MayCopy(value) == true
            && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(value);
        }
    }
}
