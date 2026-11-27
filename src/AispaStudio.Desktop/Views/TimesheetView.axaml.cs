using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>Zeiten einer Woche (R05 Slice 99): Übersicht für alle im Bereich Team und die Zeiten einer Person auf ihrer Karte; das Verhalten steht im Modell.</summary>
public partial class TimesheetView : UserControl
{
    public TimesheetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TimesheetViewModel times)
            {
                times.Files ??= new StorageFileSaver(this);
            }
        };
    }
}
