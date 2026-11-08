using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Reiter „Stammdaten“ (R05 Slice 98). Setzt den Fokus ins Namensfeld, wenn der Name beim Speichern fehlt (masken.md, R8).</summary>
public partial class PersonDetailsView : UserControl
{
    private PersonDetailsViewModel? _model;

    public PersonDetailsView()
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
        _model = DataContext as PersonDetailsViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
            FocusRequested();
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
        if (e.PropertyName == nameof(PersonDetailsViewModel.FocusTarget))
        {
            FocusRequested();
        }
    }

    private void FocusRequested()
    {
        if (_model is not { FocusTarget: PersonDetailsViewModel.NameField } model || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }

        model.FocusTarget = null;
        // Erst nach dem Layout: der Reiter wird mit der Auswahl sichtbar.
        Dispatcher.UIThread.Post(() =>
        {
            PersonNameField.BringIntoView();
            PersonNameField.Focus();
        }, DispatcherPriority.Loaded);
    }
}
