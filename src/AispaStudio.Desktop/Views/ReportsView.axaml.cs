using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;

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

    /// <summary>
    /// Ein Sprung auf einen Abschnitt von „Berichte“ (ADR 0098) rollt an dessen Anfang. <c>BringIntoView</c> rollt nur so weit wie nötig und ließe bei einem Abschnitt, der höher
    /// ist als der sichtbare Bereich, den Kopf über dem Rand stehen.
    /// </summary>
    protected override void Reveal(Control control)
    {
        if ((control == ReportManagementSection || control == ReportComplianceSection) && FormScroll.Content is Visual content
            && control.TranslatePoint(default, content) is { } top)
        {
            FormScroll.Offset = new Vector(FormScroll.Offset.X, top.Y);
            return;
        }

        base.Reveal(control);
    }
}
