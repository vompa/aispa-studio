using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Views;

public partial class ReportsView : FocusRequestView
{
    public ReportsView()
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
