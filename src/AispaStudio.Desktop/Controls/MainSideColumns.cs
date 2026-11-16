using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Hauptinhalt und Nebeninhalt nebeneinander im Verhältnis 3 : 2 (masken.md R14, ADR 0094): das erste sichtbare Kind ist der Hauptinhalt links, das zweite der Nebeninhalt rechts.
/// Ist die Breite kleiner als <see cref="StackBelow"/>, stehen beide in voller Breite untereinander, damit der Nebeninhalt nicht in einer zu schmalen Spalte umbricht.
/// Ein ausgeblendetes Kind nimmt weder Platz noch Abstand; die Reihenfolge der Kinder bleibt die Tab-Reihenfolge.
/// </summary>
public sealed class MainSideColumns : Panel
{
    /// <summary>Anteil des Hauptinhalts: 3 von 5 Teilen (1,5 : 1, nahe am goldenen Schnitt 1,618 : 1).</summary>
    public const double MainShare = 3.0 / 5.0;

    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<MainSideColumns, double>(nameof(Spacing));

    public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<MainSideColumns, double>(nameof(LineSpacing));

    public static readonly StyledProperty<double> StackBelowProperty = AvaloniaProperty.Register<MainSideColumns, double>(nameof(StackBelow));

    static MainSideColumns() => AffectsMeasure<MainSideColumns>(SpacingProperty, LineSpacingProperty, StackBelowProperty);

    /// <summary>Abstand zwischen den beiden Spalten.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Abstand zwischen Haupt- und Nebeninhalt, wenn sie untereinander stehen.</summary>
    public double LineSpacing
    {
        get => GetValue(LineSpacingProperty);
        set => SetValue(LineSpacingProperty, value);
    }

    /// <summary>Unter dieser Breite stehen Haupt- und Nebeninhalt untereinander.</summary>
    public double StackBelow
    {
        get => GetValue(StackBelowProperty);
        set => SetValue(StackBelowProperty, value);
    }

    /// <summary>Ob die beiden Inhalte bei der letzten Anordnung untereinander standen.</summary>
    public bool IsStacked { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (main, side) = Parts();
        var width = availableSize.Width;
        if (main is null || side is null || Stacks(width))
        {
            return MeasureStacked(main, side, width);
        }

        var (mainWidth, sideWidth) = Columns(width);
        main.Measure(new Size(mainWidth, double.PositiveInfinity));
        side.Measure(new Size(sideWidth, double.PositiveInfinity));
        return new Size(width, Math.Max(main.DesiredSize.Height, side.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (main, side) = Parts();
        IsStacked = main is not null && side is not null && Stacks(finalSize.Width);
        if (main is null || side is null)
        {
            (main ?? side)?.Arrange(new Rect(finalSize));
            return finalSize;
        }

        if (IsStacked)
        {
            main.Arrange(new Rect(0, 0, finalSize.Width, main.DesiredSize.Height));
            side.Arrange(new Rect(0, main.DesiredSize.Height + LineSpacing, finalSize.Width, side.DesiredSize.Height));
            return finalSize;
        }

        var (mainWidth, sideWidth) = Columns(finalSize.Width);
        main.Arrange(new Rect(0, 0, mainWidth, finalSize.Height));
        side.Arrange(new Rect(mainWidth + Spacing, 0, sideWidth, finalSize.Height));
        return finalSize;
    }

    private bool Stacks(double width) => double.IsInfinity(width) || width < StackBelow;

    private Size MeasureStacked(Control? main, Control? side, double width)
    {
        var constraint = new Size(width, double.PositiveInfinity);
        main?.Measure(constraint);
        side?.Measure(constraint);
        var height = (main?.DesiredSize.Height ?? 0) + (side?.DesiredSize.Height ?? 0) + (main is not null && side is not null ? LineSpacing : 0);
        var desiredWidth = Math.Max(main?.DesiredSize.Width ?? 0, side?.DesiredSize.Width ?? 0);
        return new Size(double.IsInfinity(width) ? desiredWidth : width, height);
    }

    private (double Main, double Side) Columns(double width)
    {
        var content = Math.Max(0, width - Spacing);
        var mainWidth = content * MainShare;
        return (mainWidth, content - mainWidth);
    }

    private (Control? Main, Control? Side) Parts()
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        return (visible.ElementAtOrDefault(0), visible.ElementAtOrDefault(1));
    }
}
