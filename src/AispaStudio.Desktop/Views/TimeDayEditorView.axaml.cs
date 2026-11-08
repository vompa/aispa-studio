using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Die Zeiten eines Tages (R05 Slice 99): Abschnitte, Verlauf, Nachtragen, Ändern und Entfernen; das Verhalten steht im Modell. Die Ansicht setzt nur den Fokus: beim Öffnen der Eingabe in den
/// Beginn (masken.md, R13), nach abgelehntem Speichern ins erste Feld mit Meldung (R8). Ohne Eingabe gibt es kein Feld, der Tag rückt nur ins Bild.
/// </summary>
public partial class TimeDayEditorView : UserControl
{
    private TimeDayEditorViewModel? _model;

    public TimeDayEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            Listen(DataContext as TimeDayEditorViewModel);
            if (_model is not null)
            {
                Dispatcher.UIThread.Post(ShowDay, DispatcherPriority.Loaded);
            }
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Listen(DataContext as TimeDayEditorViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Listen(null);
        base.OnDetachedFromVisualTree(e);
    }

    private void Listen(TimeDayEditorViewModel? model)
    {
        if (_model is not null)
        {
            _model.InvalidField -= OnInvalidField;
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = model;
        if (_model is not null)
        {
            _model.InvalidField += OnInvalidField;
            _model.PropertyChanged += OnModelChanged;
        }
    }

    private void ShowDay()
    {
        TimeDay.BringIntoView();
        FocusStartIfEditing();
    }

    private void FocusStartIfEditing()
    {
        if (_model?.IsEditing == true)
        {
            StartField.BringIntoView();
            StartField.Focus();
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimeDayEditorViewModel.IsEditing))
        {
            Dispatcher.UIThread.Post(FocusStartIfEditing, DispatcherPriority.Loaded);
        }
    }

    private void OnInvalidField() => Dispatcher.UIThread.Post(() =>
    {
        Control field = _model?.StartError is not null ? StartField : TimeReason;
        field.BringIntoView();
        field.Focus();
    }, DispatcherPriority.Loaded);
}
