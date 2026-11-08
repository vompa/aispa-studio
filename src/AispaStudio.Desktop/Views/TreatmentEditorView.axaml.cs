using System.ComponentModel;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Eine Behandlung (R05 Slice 94). Setzt den Fokus, den das Modell verlangt: ins Namens- oder Preisfeld nach einem Fehler (masken.md, R8), an die
/// Qualifikationen beim Hinweis „Keine Qualifikation gewählt“, in Preis oder Dauer über „Jetzt ergänzen“ des Assistenten. Kommt die Angabe, bevor die
/// Maske steht, gilt sie beim Einhängen.
/// </summary>
public partial class TreatmentEditorView : UserControl
{
    private TreatmentEditorViewModel? _model;

    public TreatmentEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            Detach();
            if (TopLevel.GetTopLevel(this) is not null)
            {
                Attach();
                FocusRequested();
            }
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach();
        FocusRequested();
    }

    /// <summary>Das Modell lebt länger als die Ansicht (sie wird bei jedem Wechsel neu gebaut); ausgehängt hört sie nicht mehr zu.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private void Attach()
    {
        Detach();
        _model = DataContext as TreatmentEditorViewModel;
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
        if (e.PropertyName is nameof(TreatmentEditorViewModel.FocusTarget) or nameof(TreatmentEditorViewModel.IsLoaded))
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

        model.FocusTarget = null;
        // Erst nach dem Layout: der Abschnitt des Felds wird mit der Auswahl sichtbar.
        Dispatcher.UIThread.Post(() =>
        {
            if (Target(field) is { } target)
            {
                target.BringIntoView();
                target.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Feld zum Namen; bei den Qualifikationen das erste Kästchen.</summary>
    private Control? Target(string field) => field switch
    {
        TreatmentEditorViewModel.NameField => NameField,
        TreatmentEditorViewModel.PriceField => PriceField,
        TreatmentEditorViewModel.DurationField => DurationField,
        TreatmentEditorViewModel.QualificationsField => QualificationsField.GetLogicalDescendants().OfType<CheckBox>().FirstOrDefault(),
        _ => null,
    };
}
