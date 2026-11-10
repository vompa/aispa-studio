using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>Personenkarte (R05 Slice 98): Kopf, Reiter und ihre Inhalte; das Verhalten steht im Modell. Die Ansicht reicht den Speicherdialog für die Kalenderdatei herein.</summary>
public partial class PersonCardView : UserControl
{
    public PersonCardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PersonCardViewModel { CalendarFile: { } calendarFile })
            {
                calendarFile.Files ??= new StorageFileSaver(this);
            }
        };
    }
}
