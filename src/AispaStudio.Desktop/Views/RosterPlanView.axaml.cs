using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Dienstplan einer Woche (R05 Slice 99): Wochenplan für alle im Bereich Team und die Woche einer Person auf ihrer Karte; das Verhalten steht im Modell.
/// Die Ansicht legt nur den offenen Tag als Popover direkt unter seine Zelle (Ä370, T-017), innerhalb der Breite der Ansicht.
/// </summary>
public partial class RosterPlanView : UserControl
{
    /// <summary>Höchstbreite des Tages als Popover; schmaler, wenn die Ansicht schmaler ist (Karte).</summary>
    private const double DayWidth = 560;

    /// <summary>Abstand zwischen Zelle und Popover.</summary>
    private const double DayGap = 4;

    private Button? _anchor;

    public RosterPlanView()
    {
        InitializeComponent();
        PlanRoot.LayoutUpdated += (_, _) => PlaceDay();
    }

    /// <summary>
    /// Setzt Lage und Breite des offenen Tages nach jeder Layoutrunde (Öffnen, anderer Tag, Neuladen nach dem Speichern, waagrechtes Scrollen, Größe);
    /// nur bei Änderung, damit keine Schleife entsteht. Bei einer neuen Zelle holt sie den Tag danach ins Bild: das Ins-Bild-Holen beim Fokus läuft womöglich noch auf der alten Lage.
    /// </summary>
    private void PlaceDay()
    {
        if (!DayHost.IsVisible)
        {
            _anchor = null;
            return;
        }

        var cell = PlanRoot.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("rcell") && b.Classes.Contains("open") && b.IsEffectivelyVisible);
        if (cell?.TranslatePoint(default, PlanRoot) is not { } at)
        {
            return;
        }

        var width = Math.Min(DayWidth, PlanRoot.Bounds.Width);
        var left = Math.Clamp(at.X, 0, Math.Max(0, PlanRoot.Bounds.Width - width));
        var margin = new Thickness(left, at.Y + cell.Bounds.Height + DayGap, 0, 0);
        if (!DayHost.Width.Equals(width))
        {
            DayHost.Width = width;
        }

        if (DayHost.Margin != margin)
        {
            DayHost.Margin = margin;
        }

        if (!ReferenceEquals(cell, _anchor))
        {
            _anchor = cell;
            Dispatcher.UIThread.Post(() => DayHost.BringIntoView(), DispatcherPriority.Background);
        }
    }
}
