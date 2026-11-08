using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>Konfliktmeldung mit Liste in Ortszeit (<see cref="ViewModels.ConflictNotice"/>); der DataContext ist die Meldung, ohne sie bleibt die Ansicht leer.</summary>
public partial class ConflictNoticeView : UserControl
{
    public ConflictNoticeView()
    {
        InitializeComponent();
    }
}
