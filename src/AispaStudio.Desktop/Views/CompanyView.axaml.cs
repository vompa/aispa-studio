using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Firmendaten (R05 Slice 93). Setzt den Fokus, den das Modell verlangt: ins Namensfeld nach einem Fehler (masken.md, R8), ins Feld Website über „Jetzt ergänzen“
/// des Assistenten. Kommt die Angabe, bevor die Maske steht, gilt sie beim Einhängen.
/// </summary>
public partial class CompanyView : UserControl
{
    private CompanyViewModel? _model;

    public CompanyView()
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
        _model = DataContext as CompanyViewModel;
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

    /// <summary>„Code kopieren“: legt den Einbettungscode in die Zwischenablage und sagt es neben dem Knopf.</summary>
    private async void OnCopyEmbedClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CompanyViewModel { EmbedCode: { } code } model && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(code).ConfigureAwait(true);
            model.EmbedStatus = "Code kopiert.";
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CompanyViewModel.FocusTarget) or nameof(CompanyViewModel.IsLoaded))
        {
            FocusRequested();
        }
    }

    private void FocusRequested()
    {
        if (_model is not { FocusTarget: { } field, IsLoaded: true } model || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }

        TextBox? box = field switch
        {
            CompanyViewModel.NameField => NameField,
            CompanyViewModel.WebsiteField => WebsiteField,
            CompanyViewModel.EmbedField => EmbedField,
            _ => null,
        };
        model.FocusTarget = null;
        if (box is not null)
        {
            // Erst nach dem Layout: die Felder werden mit dem Laden sichtbar.
            Dispatcher.UIThread.Post(() =>
            {
                box.BringIntoView();
                box.Focus();
            }, DispatcherPriority.Loaded);
        }
    }
}
