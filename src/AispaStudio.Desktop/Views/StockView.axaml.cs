using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Lager in der Verwaltung (R05 Slice 101). Holt den Abschnitt „Verbrauch“ der Übersicht ins Bild, wenn das Modell es verlangt (Sprungziel <see cref="StockTab.Usage"/>, ADR 0098);
/// das Modell lebt länger als die Ansicht, ausgehängt hört sie nicht mehr zu.
/// </summary>
public partial class StockView : UserControl
{
    private StockViewModel? _model;

    public StockView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            Detach();
            if (TopLevel.GetTopLevel(this) is not null)
            {
                Attach();
            }
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private void Attach()
    {
        Detach();
        _model = DataContext as StockViewModel;
        if (_model is not null)
        {
            _model.Files ??= new StorageFileSaver(this);
            _model.UsageRequested += ShowUsage;
            ShowUsage();
        }
    }

    private void Detach()
    {
        if (_model is not null)
        {
            _model.UsageRequested -= ShowUsage;
            _model = null;
        }
    }

    /// <summary>Erst nach dem Layout: der Abschnitt ist bis zum Wechsel in die Übersicht nicht sichtbar und hat noch keinen Platz.</summary>
    private void ShowUsage()
    {
        if (_model?.TakeUsageRequest() == true)
        {
            Dispatcher.UIThread.Post(() => UsageArea.BringIntoView(), DispatcherPriority.Loaded);
        }
    }
}
