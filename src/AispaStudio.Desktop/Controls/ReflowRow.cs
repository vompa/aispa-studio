using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Kopfzeile mit einem linken und einem rechten Teil (T-038, Ä378): das erste Kind steht links, alle weiteren zusammen rechts in derselben Zeile. Reicht die Breite für beide nicht,
/// rutscht der rechte Teil rechtsbündig in eine zweite Zeile; der linke (bei „Zahlen“ die Reiter, Design-Brief 5.4) bricht nie um. Die Reihenfolge der Kinder bleibt die Tab-Reihenfolge.
/// Ein ausgeblendetes Kind nimmt weder Platz noch Abstand.
/// </summary>
public sealed class ReflowRow : Panel
{
    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<ReflowRow, double>(nameof(Spacing));

    public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<ReflowRow, double>(nameof(LineSpacing));

    static ReflowRow() => AffectsMeasure<ReflowRow>(SpacingProperty, LineSpacingProperty);

    /// <summary>Abstand zwischen dem linken und dem rechten Teil und zwischen den rechten Kindern.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Abstand zwischen den beiden Zeilen.</summary>
    public double LineSpacing
    {
        get => GetValue(LineSpacingProperty);
        set => SetValue(LineSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }

        var (left, right) = Parts();
        var leftSize = SizeOf(left);
        var rightSize = SizeOf(right);
        if (Fits(leftSize, rightSize, availableSize.Width))
        {
            return new Size(leftSize.Width + rightSize.Width + Gap(leftSize, rightSize), Math.Max(leftSize.Height, rightSize.Height));
        }

        return new Size(Math.Max(leftSize.Width, rightSize.Width), leftSize.Height + LineSpacing + rightSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (left, right) = Parts();
        var leftSize = SizeOf(left);
        var rightSize = SizeOf(right);
        var oneLine = Fits(leftSize, rightSize, finalSize.Width);
        var lineHeight = oneLine ? Math.Max(leftSize.Height, rightSize.Height) : leftSize.Height;
        left?.Arrange(new Rect(0, 0, leftSize.Width, lineHeight));

        var x = Math.Max(0, finalSize.Width - rightSize.Width);
        var y = oneLine ? 0 : leftSize.Height + LineSpacing;
        foreach (var child in right)
        {
            child.Arrange(new Rect(x, y, child.DesiredSize.Width, oneLine ? lineHeight : rightSize.Height));
            x += child.DesiredSize.Width + Spacing;
        }

        foreach (var hidden in Children.Where(c => c.DesiredSize.Width <= 0))
        {
            hidden.Arrange(new Rect(0, 0, 0, 0));
        }

        return finalSize;
    }

    /// <summary>Der linke Teil (erstes Kind) und der rechte Teil (alle weiteren); Kinder ohne Breite zählen nicht.</summary>
    private (Control? Left, List<Control> Right) Parts()
    {
        var present = Children.Where(c => c.DesiredSize.Width > 0).ToList();
        var left = present.FirstOrDefault(c => Children.IndexOf(c) == 0);
        return (left, present.Where(c => !ReferenceEquals(c, left)).ToList());
    }

    private static Size SizeOf(Control? child) => child?.DesiredSize ?? default;

    private Size SizeOf(List<Control> children) => children.Count == 0
        ? default
        : new Size(children.Sum(c => c.DesiredSize.Width) + (Spacing * (children.Count - 1)), children.Max(c => c.DesiredSize.Height));

    private double Gap(Size left, Size right) => left.Width > 0 && right.Width > 0 ? Spacing : 0;

    private bool Fits(Size left, Size right, double width) =>
        double.IsInfinity(width) || left.Width + right.Width + Gap(left, right) <= width + 0.5;
}
