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
        AttachedToVisualTree += (_, _) => BindFileSaver();
        DataContextChanged += (_, _) => BindFileSaver();
    }

    private void BindFileSaver()
    {
        if (DataContext is DocumentActionsViewModel model && TopLevel.GetTopLevel(this) is { } window
            && (model.Files is null || model.Files is StorageFileSaver native && native.Window != window))
        {
            // Das Dokument lebt beim Seitenwechsel weiter. Sein Dialog gehört dem Fenster.
            model.Files = new StorageFileSaver(window);
        }
    }
}
