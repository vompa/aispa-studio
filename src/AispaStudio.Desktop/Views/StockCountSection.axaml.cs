using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>Inventur im Lager (R05 Slice 101). Der Handscanner (Slice 114) setzt den Fokus in das Zählfeld der gefundenen Zeile.</summary>
public partial class StockCountSection : UserControl
{
    private StockCountViewModel? _model;

    public StockCountSection()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null)
            {
                _model.RowFocusRequested -= FocusRow;
            }

            _model = DataContext as StockCountViewModel;
            if (_model is not null)
            {
                _model.RowFocusRequested += FocusRow;
            }
        };
    }

    private void FocusRow(CountRow row) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (CountList.ContainerFromItem(row) is not { } container || container.FindDescendantOfType<TextBox>() is not { } field)
            {
                return;
            }

            container.BringIntoView();
            field.Focus();
            field.SelectAll();
        });
}
