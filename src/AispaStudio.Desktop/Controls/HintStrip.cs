using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Tastenleiste unter dem Kalender: immer eine Zeile, nie ein Umbruch. Reicht die Breite nicht, fallen Hinweise von rechts nach links weg; ein Hinweis mit
/// <see cref="KeepProperty"/> („Alle Tasten“) bleibt stehen, damit der Weg zur vollständigen Tastenhilfe nie verschwindet. Die Taste selbst wirkt weiter,
/// nur der Hinweis in der Leiste entfällt: ein weggefallener Hinweis ist unsichtbar, ohne Platz, nicht anklickbar und kein Tab-Halt.
/// </summary>
public sealed class HintStrip : Panel
{
    /// <summary>Der Hinweis bleibt auch bei Platzmangel stehen.</summary>
    public static readonly AttachedProperty<bool> KeepProperty = AvaloniaProperty.RegisterAttached<HintStrip, Control, bool>("Keep");

    /// <summary>Der Hinweis ist gerade weggefallen: ohne Platz, unsichtbar und nicht mit Tab erreichbar.</summary>
    public static readonly AttachedProperty<bool> IsDroppedProperty = AvaloniaProperty.RegisterAttached<HintStrip, Control, bool>("IsDropped");

    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<HintStrip, double>(nameof(Spacing), 16);

    static HintStrip()
    {
        AffectsMeasure<HintStrip>(SpacingProperty);
        KeepProperty.Changed.AddClassHandler<Control>((control, _) => (control.GetVisualParent() as HintStrip)?.InvalidateMeasure());
        IsDroppedProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            var dropped = e.NewValue is true;
            control.Opacity = dropped ? 0 : 1;
            control.IsHitTestVisible = !dropped;
            KeyboardNavigation.SetTabNavigation(control, dropped ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        });
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public static bool GetKeep(Control control) => control.GetValue(KeepProperty);

    public static void SetKeep(Control control, bool value) => control.SetValue(KeepProperty, value);

    public static bool GetIsDropped(Control control) => control.GetValue(IsDroppedProperty);

    public static void SetIsDropped(Control control, bool value) => control.SetValue(IsDroppedProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = new List<Size>(Children.Count);
        foreach (var child in Children)
        {
            // Jeder Hinweis wird in seiner natürlichen Breite gemessen, auch ein weggefallener; so entscheidet nur die Breite der Leiste, nicht der letzte Stand.
            child.Measure(Size.Infinity);
            natural.Add(child.DesiredSize);
        }

        var dropped = Dropped(natural, availableSize.Width);
        var height = 0.0;
        var width = 0.0;
        var shown = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            SetIsDropped(Children[i], dropped[i]);
            if (dropped[i])
            {
                continue;
            }

            width += natural[i].Width + (shown++ > 0 ? Spacing : 0);
            height = Math.Max(height, natural[i].Height);
        }

        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var first = true;
        foreach (var child in Children)
        {
            if (GetIsDropped(child))
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            x += first ? 0 : Spacing;
            first = false;
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }

        return finalSize;
    }

    /// <summary>Welche Hinweise wegfallen: von rechts nach links, bis der Rest passt; Hinweise mit <see cref="KeepProperty"/> bleiben.</summary>
    private bool[] Dropped(List<Size> natural, double available)
    {
        var dropped = new bool[natural.Count];
        if (double.IsInfinity(available))
        {
            return dropped;
        }

        double Total() => natural.Select((size, i) => (size, i)).Where(t => !dropped[t.i]).Select(t => t.size.Width).DefaultIfEmpty().Sum()
            + Spacing * Math.Max(0, dropped.Count(d => !d) - 1);

        for (var i = natural.Count - 1; i >= 0 && Total() > available; i--)
        {
            if (!GetKeep(Children[i]))
            {
                dropped[i] = true;
            }
        }

        return dropped;
    }
}
