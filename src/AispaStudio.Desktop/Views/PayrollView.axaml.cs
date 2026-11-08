using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>Ansicht „Lohn“ im Bereich Team (R05 Slice 100); das Verhalten steht im Modell, der Speicherdialog kommt vom Fenster.</summary>
public partial class PayrollView : UserControl
{
    public PayrollView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ISavesFiles model)
            {
                model.Files ??= new StorageFileSaver(this);
            }
        };
    }
}
