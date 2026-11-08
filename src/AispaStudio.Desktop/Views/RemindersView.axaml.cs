using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Erinnerungen (R05 Slice 96): Terminerinnerungen und Wiederbuchungs-Hinweise. Setzt den Fokus nach einem Fehler ins Feld (masken.md, R8).</summary>
public partial class RemindersView : UserControl
{
    private RemindersViewModel? _model;

    public RemindersView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _model = DataContext as RemindersViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
        }
    }

    /// <summary>Das Modell lebt länger als die Ansicht; ausgehängt hört sie nicht mehr zu.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RemindersViewModel.FocusTarget) || _model is not { FocusTarget: { } target } model)
        {
            return;
        }

        Control? field = target switch
        {
            RemindersViewModel.SecondHoursField => SecondHoursField,
            RemindersViewModel.TemplateField => TemplateField,
            RemindersViewModel.LanguageField => LanguageField,
            _ => null,
        };
        model.FocusTarget = null;
        if (field is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                field.BringIntoView();
                field.Focus();
            }, DispatcherPriority.Loaded);
        }
    }
}
