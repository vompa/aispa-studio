using System.ComponentModel;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Eine Behandlung (R05 Slice 94). Setzt den Fokus, den das Modell verlangt: ins Namens- oder Preisfeld nach einem Fehler (masken.md, R8), an die
/// Qualifikationen beim Hinweis „Keine Qualifikation gewählt“, in Preis oder Dauer über „Jetzt ergänzen“ des Assistenten. Ein Sprung oder Fehler, der
/// eine Gruppe nennt (z. B. „material“), rollt zu ihr und setzt den Fokus in ihr erstes Feld (ADR 0098). Kommt die Angabe, bevor die Maske steht,
/// gilt sie beim Einhängen.
/// </summary>
public partial class TreatmentEditorView : UserControl
{
    /// <summary>Luft über einer Gruppe, zu der gerollt wurde (Zwischentitel zu Inhalt, design.md 4.1).</summary>
    private const double GroupGap = 8;

    /// <summary>Breite, die der Karte neben der Seitenleiste mindestens bleiben muss (breites Formular, masken.md Abschnitt 4).</summary>
    private const double FormWidth = 560;

    /// <summary>Rand rechts neben der Karte, wie im rollenden Teil (Platz für die Scrollleiste).</summary>
    private const double CardGutter = 16;

    /// <summary>Abstand vom festen Kopf mit der Schiene zum ersten Feld (Gruppe zu Gruppe, design.md 4.1).</summary>
    private const double GroupSpace = 20;

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

        PlaceHistory();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PlaceHistory();
    }

    /// <summary>
    /// Ist die Seitenleiste „Verlauf“ offen und bleibt daneben eine Formularbreite, rückt die Karte neben die Leiste; im schmalen Fenster liegt die Leiste
    /// über dem rechten Teil der Karte (ADR 0098, „Folgen und Stand“).
    /// </summary>
    private void PlaceHistory()
    {
        var beside = _model?.IsHistoryOpen == true && Bounds.Width - HistorySide.Width >= FormWidth;
        var side = beside ? HistorySide.Width : 0;
        CardHead.Margin = new Thickness(0, 0, CardGutter + side, GroupSpace);
        FieldsScroll.Margin = new Thickness(0, 0, side, 0);
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
        if (e.PropertyName is nameof(TreatmentEditorViewModel.FocusTarget) or nameof(TreatmentEditorViewModel.GroupTarget)
            or nameof(TreatmentEditorViewModel.IsLoaded))
        {
            FocusRequested();
        }
        else if (e.PropertyName == nameof(TreatmentEditorViewModel.IsHistoryOpen))
        {
            PlaceHistory();
        }
    }

    private void FocusRequested()
    {
        if (_model is not { IsLoaded: true } model || TopLevel.GetTopLevel(this) is null || (model.FocusTarget is null && model.GroupTarget is null))
        {
            return;
        }

        var field = model.FocusTarget;
        var group = model.GroupTarget;
        model.FocusTarget = null;
        model.GroupTarget = null;
        // Erst nach dem Layout: der Bereich des Felds oder der Gruppe wird mit der Auswahl sichtbar.
        Dispatcher.UIThread.Post(() =>
        {
            if (group is not null && Group(group) is { IsEffectivelyVisible: true } section)
            {
                ScrollTo(section);
                if (field is null)
                {
                    FirstInput(section)?.Focus();
                }
            }

            if (field is not null && Target(field) is { } target)
            {
                target.BringIntoView();
                target.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Die Gruppe zu einem Sprungziel (die Abschnittsnamen von vor ADR 0098).</summary>
    private StackPanel? Group(string group) => group switch
    {
        TreatmentEditorViewModel.Process => ProcessGroup,
        TreatmentEditorViewModel.Material => MaterialGroup,
        TreatmentEditorViewModel.Provide => ProvideGroup,
        TreatmentEditorViewModel.Prices => PricesGroup,
        TreatmentEditorViewModel.Sales => SalesGroup,
        _ => null,
    };

    /// <summary>Rollt die Gruppe an den oberen Rand; die erste Gruppe eines Bereichs bleibt unter Titel und Schiene stehen.</summary>
    private void ScrollTo(StackPanel section)
    {
        var isFirst = section.Parent is Panel area && area.Children.FirstOrDefault(c => c.IsVisible) == section;
        var top = FieldsScroll.Content is Visual content ? section.TranslatePoint(default, content)?.Y ?? 0 : 0;
        FieldsScroll.Offset = new Vector(0, isFirst ? 0 : Math.Max(0, top - GroupGap));
    }

    /// <summary>Das erste Eingabeelement der Gruppe; das Fragezeichen am Gruppentitel zählt nicht.</summary>
    private static InputElement? FirstInput(StackPanel section) =>
        section.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(c =>
            c is TextBox or ComboBox or CheckBox or Button and not HelpButton && c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);

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
