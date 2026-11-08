using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Kassa-Einstellungen (R05 Slice 96). Setzt den Fokus nach einem Fehler ins Feld der Rabattgrenze (masken.md, R8).</summary>
public partial class PosSettingsView : UserControl
{
    private PosSettingsViewModel? _model;

    public PosSettingsView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _model = DataContext as PosSettingsViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
        }
    }

    /// <summary>Das Modell lebt länger als die Ansicht (sie wird bei jedem Bereichswechsel neu gebaut); ausgehängt hört sie nicht mehr zu.</summary>
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
        if (e.PropertyName != nameof(PosSettingsViewModel.FocusTarget) || _model is not { FocusTarget: PosSettingsViewModel.LimitField } model)
        {
            return;
        }

        model.FocusTarget = null;
        Dispatcher.UIThread.Post(() =>
        {
            LimitField.BringIntoView();
            LimitField.Focus();
        }, DispatcherPriority.Loaded);
    }
}
