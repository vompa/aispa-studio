using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Handbuch in der Verwaltung (R05 Slice 97). Setzt den Fokus, den das Modell nach einem Fehler verlangt (Titel oder Text, masken.md, R8).</summary>
public partial class HandbookView : UserControl
{
    private HandbookViewModel? _model;

    public HandbookView()
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

    /// <summary>Das Modell lebt länger als die Ansicht (sie wird bei jedem Bereichswechsel neu gebaut); ausgehängt hört sie nicht mehr zu.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private void Attach()
    {
        Detach();
        _model = DataContext as HandbookViewModel;
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
        if (e.PropertyName != nameof(HandbookViewModel.FocusTarget) || _model is not { FocusTarget: { } field } model)
        {
            return;
        }

        TextBox? box = field switch
        {
            HandbookViewModel.TitleField => EntryTitleField,
            HandbookViewModel.TextField => TextField,
            _ => null,
        };
        model.FocusTarget = null;
        if (box is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                box.BringIntoView();
                box.Focus();
            }, DispatcherPriority.Loaded);
        }
    }
}
