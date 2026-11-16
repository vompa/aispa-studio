using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>Seite „Schließzeiten“ mit den Abschnitten Schließzeiten und Raumsperren (ADR 0098); ein Sprungziel rollt an den Anfang seines Abschnitts.</summary>
public partial class ClosuresPageView : FocusRequestView
{
    public ClosuresPageView() => InitializeComponent();

    /// <summary>Der Sprung aus dem Kalender verlangt den Abschnitt, bevor es die Ansicht gibt; er gilt beim Einhängen.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FocusRequested();
    }

    protected override void Reveal(Control control) => _ = ScrollToStartAsync(PageScroll, control);
}
