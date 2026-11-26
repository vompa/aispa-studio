using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Raster für Kennzahl-Kacheln (Design-Brief 5.5, T-104): so viele gleich breite Spalten, wie mit <see cref="MinColumnWidth"/> und <see cref="Spacing"/> in die Breite passen;
/// die Kacheln füllen die Breite, eine Zeile ist so hoch wie ihre höchste Kachel. Die Spaltenzahl hängt nur an der Breite, nicht an der Zahl der Kacheln,
/// damit Gruppen untereinander in denselben Spalten stehen. Ein ausgeblendetes Kind nimmt keinen Platz; die Reihenfolge der Kinder bleibt die Tab-Reihenfolge.
/// </summary>
public sealed class TileGrid : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty = AvaloniaProperty.Register<TileGrid, double>(nameof(MinColumnWidth), 160);

    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<TileGrid, double>(nameof(Spacing));

    static TileGrid() => AffectsMeasure<TileGrid>(MinColumnWidthProperty, SpacingProperty);

    /// <summary>Mindestbreite einer Spalte; darunter rückt eine Kachel in die nächste Zeile.</summary>
    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <summary>Abstand zwischen Spalten und zwischen Zeilen.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Spalten bei der letzten Anordnung.</summary>
    public int Columns { get; private set; } = 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth : availableSize.Width;
        var columns = ColumnsFor(width);
        var columnWidth = ColumnWidth(width, columns);
        var visible = Children.Where(c => c.IsVisible).ToList();
        foreach (var child in visible)
        {
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
        }

        var height = RowHeights(visible, columns).Sum() + Math.Max(0, RowCount(visible.Count, columns) - 1) * Spacing;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Columns = ColumnsFor(finalSize.Width);
        var columnWidth = ColumnWidth(finalSize.Width, Columns);
        var visible = Children.Where(c => c.IsVisible).ToList();
        var heights = RowHeights(visible, Columns);
        var y = 0.0;
        for (var row = 0; row < heights.Count; row++)
        {
            for (var column = 0; column < Columns && row * Columns + column < visible.Count; column++)
            {
                visible[row * Columns + column].Arrange(new Rect(column * (columnWidth + Spacing), y, columnWidth, heights[row]));
            }

            y += heights[row] + Spacing;
        }

        return finalSize;
    }

    private int ColumnsFor(double width) => Math.Max(1, (int)Math.Floor((width + Spacing) / (MinColumnWidth + Spacing)));

    private double ColumnWidth(double width, int columns) => Math.Max(0, (width - (columns - 1) * Spacing) / columns);

    private static int RowCount(int count, int columns) => (count + columns - 1) / columns;

    private static List<double> RowHeights(List<Control> visible, int columns) =>
        [.. Enumerable.Range(0, RowCount(visible.Count, columns)).Select(row => visible.Skip(row * columns).Take(columns).Max(c => c.DesiredSize.Height))];
}
