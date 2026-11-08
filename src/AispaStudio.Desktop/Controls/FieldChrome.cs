using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Unterkante und Fokus-Unterstrich der Eingabe- und Auswahlfelder (Design-Brief 2.6, 4.5, 5.2): jedes <c>TextBox</c> und <c>ComboBox</c> bekommt
/// zwei dünne Streifen am unteren Rand, die Unterkante 1 px <c>fg-3</c> (Kontrast 3:1) und den Unterstrich 2 px <c>accent</c>, der beim Fokus
/// (auch per Maus) von der Mitte aus wächst. Die Fluent-Vorlage von Avalonia hat dafür keine Stelle; deshalb werden die Streifen nach dem Aufbau
/// der Vorlage in deren Fläche eingesetzt. Aussehen, Dauer und Kurven stehen in <c>Styles.axaml</c> (<c>Border.field-edge</c>, <c>Border.field-underline</c>),
/// damit <c>ReducedMotion</c> sie abschalten kann; das Steuerelement spiegelt nur seinen Zustand als Klassen auf die Streifen.
/// </summary>
public static class FieldChrome
{
    public const string EdgeClass = "field-edge";

    public const string UnderlineClass = "field-underline";

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, bool>("IsEnabled", typeof(FieldChrome));

    private static readonly AttachedProperty<ChromeParts?> PartsProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, ChromeParts?>("Parts", typeof(FieldChrome));

    static FieldChrome() => IsEnabledProperty.Changed.AddClassHandler<TemplatedControl>(OnEnabledChanged);

    public static bool GetIsEnabled(TemplatedControl control) => control.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(TemplatedControl control, bool value) => control.SetValue(IsEnabledProperty, value);

    private static void OnEnabledChanged(TemplatedControl control, AvaloniaPropertyChangedEventArgs e)
    {
        control.TemplateApplied -= OnTemplateApplied;
        control.PropertyChanged -= Mirror;
        if (e.NewValue is true)
        {
            control.TemplateApplied += OnTemplateApplied;
            control.PropertyChanged += Mirror;
            // Klassen wie „invalid“ und „warning“ setzt das FormField bzw. das Feld selbst; die Streifen folgen ihnen.
            control.Classes.CollectionChanged += (_, _) => MirrorClasses(control);
        }
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        // Der versteckte Texteingabeteil einer bearbeitbaren Aufklappliste trägt keine eigenen Streifen; die Liste hat ihre.
        if (sender is not TemplatedControl control || control.TemplatedParent is ComboBox)
        {
            return;
        }

        // Die Vorlage setzt den Platzhalter mit 50 % Deckkraft; ein Wert in der Vorlage schlägt jeden Stil, deshalb hier: fg-2 voll deckend
        // erreicht 4,5:1 (Design-Brief 5.2), mit 50 % bliebe kaum Kontrast (Wunsch des Auftraggebers Ä24).
        foreach (var name in new[] { "PART_Placeholder", "PART_FloatingPlaceholder" })
        {
            if (e.NameScope.Find<TextBlock>(name) is { } placeholder)
            {
                placeholder.Opacity = 1;
            }
        }

        // Die Vorlage füllt eine Aufklappliste bei Fokus mit der Akzentfarbe („Highlight“); ein Wert in der Vorlage schlägt jeden Stil, deshalb lokal abgeschaltet.
        if (e.NameScope.Find<Border>("HighlightBackground") is { } highlight)
        {
            highlight.IsVisible = false;
        }

        // Die geöffnete Liste einer Aufklappliste (Brief 5.3): Fläche surface, Rahmen line, Radius 8, Schatten wie Karten. Die Werte stehen als lokale
        // Werte in der Vorlage und schlagen deshalb jeden Stil; die Farben folgen weiter dem Thema über die Ressourcen.
        if (e.NameScope.Find<Border>("PopupBorder") is { } list)
        {
            list.CornerRadius = new CornerRadius(8);
            list.BorderThickness = new Thickness(1);
            list.Bind(Border.BackgroundProperty, control.GetResourceObservable("SurfaceBrush"));
            list.Bind(Border.BorderBrushProperty, control.GetResourceObservable("LineBrush"));
            list.Bind(Border.BoxShadowProperty, control.GetResourceObservable("CardShadow"));
        }

        // Die Fläche der Vorlage: TextBox „PART_BorderElement“ in einem Panel, ComboBox „Background“ in einem Grid.
        var surface = e.NameScope.Find<Border>("PART_BorderElement") ?? e.NameScope.Find<Border>("Background");
        if (surface?.Parent is not Panel panel)
        {
            return;
        }

        var edge = Strip(EdgeClass);
        var underline = Strip(UnderlineClass);
        // In der Vorlage einer Aufklappliste liegt die Fläche in einem Grid mit einer Spalte für den Pfeil: die Streifen laufen über alle Spalten.
        if (panel is Grid grid)
        {
            Grid.SetColumnSpan(edge, Math.Max(1, grid.ColumnDefinitions.Count));
            Grid.SetColumnSpan(underline, Math.Max(1, grid.ColumnDefinitions.Count));
        }

        panel.Children.Insert(panel.Children.IndexOf(surface) + 1, edge);
        panel.Children.Insert(panel.Children.IndexOf(surface) + 2, underline);
        control.SetValue(PartsProperty, new ChromeParts(edge, underline));
        MirrorClasses(control);
    }

    private static Border Strip(string cssClass)
    {
        var strip = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Focusable = false,
        };
        strip.Classes.Add(cssClass);
        return strip;
    }

    private static void Mirror(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is TemplatedControl control && (e.Property == InputElement.IsKeyboardFocusWithinProperty || e.Property == InputElement.IsPointerOverProperty
            || e.Property == InputElement.IsEnabledProperty || e.Property == TextBox.IsReadOnlyProperty))
        {
            MirrorClasses(control);
        }
    }

    private static void MirrorClasses(TemplatedControl control)
    {
        if (control.GetValue(PartsProperty) is not { } parts)
        {
            return;
        }

        var focused = control.IsKeyboardFocusWithin;
        var hover = control.IsPointerOver && control.IsEnabled;
        var invalid = control.Classes.Contains("invalid");
        var warning = control.Classes.Contains("warning");
        var readOnly = control is TextBox { IsReadOnly: true };
        foreach (var strip in parts.All)
        {
            strip.Classes.Set("on", focused);
            strip.Classes.Set("hover", hover);
            strip.Classes.Set("invalid", invalid);
            strip.Classes.Set("warning", warning);
            strip.Classes.Set("readonly", readOnly);
        }
    }

    private sealed record ChromeParts(Border Edge, Border Underline)
    {
        public IEnumerable<Border> All => [Edge, Underline];
    }
}
