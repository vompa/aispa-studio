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

    /// <summary>Ein Sprung auf „Berichte“ aus einer anderen Seite verlangt den Abschnitt, bevor es die Ansicht gibt; er gilt beim Einhängen.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FocusRequested();
    }

    /// <summary>Ein Sprung auf einen Abschnitt von „Berichte“ (ADR 0098) rollt an dessen Anfang.</summary>
    protected override void Reveal(Control control)
    {
        if (control == ReportManagementSection || control == ReportComplianceSection)
        {
            _ = ScrollToStartAsync(FormScroll, control);
            return;
        }

        base.Reveal(control);
    }
}
