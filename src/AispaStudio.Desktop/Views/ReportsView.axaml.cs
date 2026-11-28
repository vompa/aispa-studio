using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

public partial class ReportsView : FocusRequestView
{
    public ReportsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindFileSaver();
    }

    /// <summary>Ein Sprung auf „Berichte“ aus einer anderen Seite verlangt den Abschnitt, bevor es die Ansicht gibt; er gilt beim Einhängen.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BindFileSaver();
        FocusRequested();
    }

    private void BindFileSaver()
    {
        if (DataContext is ReportsViewModel model && TopLevel.GetTopLevel(this) is { } window)
        {
            var previous = model.Files;
            StorageFileSaver.BindWindow(model, window);
            // Das Menü speichert über das bereits geladene Dokument, auch wenn die Ansicht neu entsteht.
            if (model.Management.Document is { } document && (document.Files is null || document.Files == previous))
            {
                document.Files = model.Files;
            }
        }
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
