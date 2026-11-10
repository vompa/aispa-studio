using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>Knopfblock „Drucken · PDF speichern · Per Mail senden“ (ADR 0092); das Verhalten steht im <see cref="DocumentActionsViewModel"/>, der Speicherdialog kommt vom Fenster.</summary>
public partial class DocumentActions : UserControl
{
    public DocumentActions()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DocumentActionsViewModel model)
            {
                model.Files ??= new StorageFileSaver(this);
            }
        };
    }
}
