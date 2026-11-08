using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace AispaStudio.Desktop.Views;

/// <summary>„Freie Zeit suchen“ im Kalender und im Posteingang; ein Tipp auf eine Zeit wählt sie.</summary>
public partial class FreeTimeView : UserControl
{
    public FreeTimeView()
    {
        InitializeComponent();
    }

    private void OnSlotTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: FreeSlotRow row } && DataContext is FreeTimeViewModel freeTime)
        {
            freeTime.Choose(row);
        }
    }
}
