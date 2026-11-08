using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Grundlage der Ansichten „Schließzeiten“ und „Raumsperren“ (R05 Slice 95): setzt den Fokus, den das Modell verlangt (<see cref="PeriodAreaViewModel.FocusTarget"/>, der Name des Feldes):
/// ins erste fehlerhafte Feld nach einem Fehler (masken.md, R8) und ins erste Feld eines neuen Formulars. Das Formular entsteht erst nach dem Wechsel der Auswahl; deshalb sucht die
/// Ansicht das Feld ein paarmal nach, bis es steht. Kommt die Angabe, bevor die Maske steht, gilt sie beim Einhängen.
/// </summary>
public class PeriodAreaView : UserControl
{
    private const int Attempts = 12;

    private PeriodAreaViewModel? _model;

    public PeriodAreaView() =>
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
        FocusRequested();
    }

    /// <summary>Das Modell lebt länger als die Ansicht (sie wird bei jedem Bereichswechsel neu gebaut); ausgehängt hört sie nicht mehr zu.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private void Attach()
    {
        Detach();
        _model = DataContext as PeriodAreaViewModel;
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
        if (e.PropertyName == nameof(PeriodAreaViewModel.FocusTarget))
        {
            FocusRequested();
        }
    }

    private void FocusRequested()
    {
        if (_model is not { FocusTarget: { } field } model || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }

        model.FocusTarget = null;
        _ = FocusWhenReadyAsync(field);
    }

    private async Task FocusWhenReadyAsync(string field)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            if (TopLevel.GetTopLevel(this) is null)
            {
                return;
            }

            if (this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == field) is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control)
            {
                control.BringIntoView();
                control.Focus();
                return;
            }

            await Task.Delay(40).ConfigureAwait(true);
        }
    }
}
