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
    /// <summary>So oft führt <see cref="ScrollToStartAsync"/> die Stelle nach (rund eine halbe Sekunde), wie die Suche nach dem Feld in <see cref="PeriodAreaView"/>.</summary>
    private const int ScrollAttempts = 12;

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
        if (e.PropertyName == nameof(IFocusTarget.FocusTarget))
        {
            FocusRequested();
        }
    }

    /// <summary>
    /// Setzt um, was das Modell verlangt, und leert die Angabe. Eine Seite mit Abschnitten ruft das auch beim Einhängen auf: Bei einem Sprung aus einer anderen Seite steht die Angabe
    /// schon, bevor es die Ansicht gibt.
    /// </summary>
    protected void FocusRequested()
    {
        if (_model is not { FocusTarget: { } field } model)
        {
            return;
        }

        model.FocusTarget = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<Control>(field) is { } control)
            {
                Reveal(control);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Zeigt das verlangte Element und gibt ihm den Fokus; eine Ansicht mit Abschnitten rollt stattdessen an deren Anfang.</summary>
    protected virtual void Reveal(Control control)
    {
        control.BringIntoView();
        control.Focus();
    }

    /// <summary>
    /// Rollt so, dass <paramref name="section"/> oben im sichtbaren Bereich beginnt. <c>BringIntoView</c> rollt nur so weit wie nötig und ließe bei einem Abschnitt, der höher ist
    /// als der sichtbare Bereich, den Kopf über dem Rand stehen. Die Stelle wird ein paarmal nachgeführt: Nach dem Laden entsteht der Inhalt darüber erst, und eine Liste holt
    /// ihre gewählte Zeile selbst ins Bild (gemessen: 521 statt 1047 px gerollt).
    /// </summary>
    protected async Task ScrollToStartAsync(ScrollViewer scroll, Control section)
    {
        for (var attempt = 0; attempt < ScrollAttempts; attempt++)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            if (TopLevel.GetTopLevel(this) is null)
            {
                return;
            }

            if (scroll.Content is Visual content && section.TranslatePoint(default, content) is { } top)
            {
                scroll.Offset = new Vector(scroll.Offset.X, top.Y);
            }

            await Task.Delay(40).ConfigureAwait(true);
        }
    }
}
