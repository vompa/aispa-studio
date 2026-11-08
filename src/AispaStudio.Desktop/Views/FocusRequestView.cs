using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Grundlage der Abschnitte des Lagers (R05 Slice 101): setzt den Fokus, den das Modell verlangt (<see cref="IFocusTarget.FocusTarget"/>, der Name des Feldes): ins erste fehlerhafte
/// Feld nach einem Fehler (masken.md, R8) und ins erste Feld der geöffneten Leiste. Das Modell lebt länger als die Ansicht; ausgehängt hört sie nicht mehr zu.
/// </summary>
public class FocusRequestView : UserControl
{
    private IFocusTarget? _model;

    public FocusRequestView() =>
        DataContextChanged += (_, _) =>
        {
            Detach();
            if (TopLevel.GetTopLevel(this) is not null)
            {
                Attach();
            }
        };

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
        _model = DataContext as IFocusTarget;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
        }
    }

    private void Detach()
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model = null;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IFocusTarget.FocusTarget) || _model is not { FocusTarget: { } field } model)
        {
            return;
        }

        model.FocusTarget = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<Control>(field) is { } control)
            {
                control.BringIntoView();
                control.Focus();
            }
        }, DispatcherPriority.Loaded);
    }
}
