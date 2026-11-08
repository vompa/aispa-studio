using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Views;

/// <summary>Zahlungskonto (R05 Slice 96). „Stripe öffnen“ öffnet die Einrichtungsseite im Browser des Systems.</summary>
public partial class PaymentsView : UserControl
{
    private PaymentsViewModel? _model;

    public PaymentsView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _model = DataContext as PaymentsViewModel;
        if (_model is not null)
        {
            _model.OpenRequested += OnOpenRequested;
        }
    }

    /// <summary>Das Modell lebt länger als die Ansicht; ausgehängt öffnet sie nichts mehr.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_model is not null)
        {
            _model.OpenRequested -= OnOpenRequested;
            _model = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnOpenRequested(Uri uri)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }
}
