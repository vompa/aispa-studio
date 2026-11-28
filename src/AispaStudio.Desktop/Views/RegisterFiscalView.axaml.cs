using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AispaStudio.Desktop.Views;

public partial class RegisterFiscalView : UserControl
{
    private static readonly DataFormat<byte[]> ExcludeFromClipboardHistory =
        DataFormat.CreateBytesPlatformFormat("ExcludeClipboardContentFromMonitorProcessing");

    private RegisterFiscalViewModel? _model;
    private bool _attached;

    public RegisterFiscalView()
    {
        InitializeComponent();
        AddHandler(SelectableTextBlock.CopyingToClipboardEvent, CopySelection);
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
        if (sender is Button { Tag: string value })
        {
            await CopyRegistrationAsync(value, value);
        }
    }

    private async void CopySelection(object? sender, RoutedEventArgs e)
    {
        if (e.Source is SelectableTextBlock { DataContext: RegistrationField registration } selection)
        {
            e.Handled = true;
            await CopyRegistrationAsync(registration.Value, selection.SelectedText);
        }
    }

    private async Task CopyRegistrationAsync(string value, string text)
    {
        if (text.Length != 0 && _model?.MayCopy(value) == true
            && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            var item = DataTransferItem.CreateText(text);
            item.Set(ExcludeFromClipboardHistory, [0]);
            var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer);
        }
    }
}
