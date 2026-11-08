using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Media;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Hintergrund einer Kalenderspalte: außerhalb von Dienst- oder Öffnungszeit schraffiert, darüber Stunden- und Viertelstundenlinien.
/// Gezeichnet statt aus vielen Einzelelementen gebaut, damit auch volle Tage mit vielen Spalten flüssig scrollen.
/// </summary>
public sealed class TimeGrid : Avalonia.Controls.Control
{
    public static readonly StyledProperty<IReadOnlyList<CalendarSpan>?> AvailableProperty =
        AvaloniaProperty.Register<TimeGrid, IReadOnlyList<CalendarSpan>?>(nameof(Available));

    public static readonly StyledProperty<IBrush?> AvailableBrushProperty =
        AvaloniaProperty.Register<TimeGrid, IBrush?>(nameof(AvailableBrush));

    public static readonly StyledProperty<IBrush?> OffBrushProperty =
        AvaloniaProperty.Register<TimeGrid, IBrush?>(nameof(OffBrush));

    public static readonly StyledProperty<IBrush?> HatchBrushProperty =
        AvaloniaProperty.Register<TimeGrid, IBrush?>(nameof(HatchBrush));

    public static readonly StyledProperty<IBrush?> HourLineBrushProperty =
        AvaloniaProperty.Register<TimeGrid, IBrush?>(nameof(HourLineBrush));

    public static readonly StyledProperty<IBrush?> QuarterLineBrushProperty =
        AvaloniaProperty.Register<TimeGrid, IBrush?>(nameof(QuarterLineBrush));

    public static readonly StyledProperty<double> PixelsPerMinuteProperty =
        AvaloniaProperty.Register<TimeGrid, double>(nameof(PixelsPerMinute), DayLayout.PixelsPerMinute);

    static TimeGrid()
    {
        AffectsRender<TimeGrid>(AvailableProperty, AvailableBrushProperty, OffBrushProperty, HatchBrushProperty,
            HourLineBrushProperty, QuarterLineBrushProperty, PixelsPerMinuteProperty);
    }

    /// <summary>Maßstab des Kalenders (Zoom), für den Abstand der Viertelstundenlinien.</summary>
    public double PixelsPerMinute
    {
        get => GetValue(PixelsPerMinuteProperty);
        set => SetValue(PixelsPerMinuteProperty, value);
    }

    public IReadOnlyList<CalendarSpan>? Available
    {
        get => GetValue(AvailableProperty);
        set => SetValue(AvailableProperty, value);
    }

    public IBrush? AvailableBrush
    {
        get => GetValue(AvailableBrushProperty);
        set => SetValue(AvailableBrushProperty, value);
    }

    public IBrush? OffBrush
    {
        get => GetValue(OffBrushProperty);
        set => SetValue(OffBrushProperty, value);
    }

    public IBrush? HatchBrush
    {
        get => GetValue(HatchBrushProperty);
        set => SetValue(HatchBrushProperty, value);
    }

    public IBrush? HourLineBrush
    {
        get => GetValue(HourLineBrushProperty);
        set => SetValue(HourLineBrushProperty, value);
    }

    public IBrush? QuarterLineBrush
    {
        get => GetValue(QuarterLineBrushProperty);
        set => SetValue(QuarterLineBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var all = new Rect(0, 0, width, height);
        context.FillRectangle(OffBrush ?? Brushes.Transparent, all);

        // Schraffur für „nicht im Dienst“ bzw. „geschlossen“.
        if (HatchBrush is { } hatch)
        {
            var pen = new Pen(hatch, 1);
            using (context.PushClip(all))
            {
                for (var x = -height; x < width; x += 9)
                {
                    context.DrawLine(pen, new Point(x, height), new Point(x + height, 0));
                }
            }
        }

        foreach (var span in Available ?? [])
        {
            context.FillRectangle(AvailableBrush ?? Brushes.Transparent, new Rect(0, span.Top, width, span.Height));
        }

        var quarter = 15 * Math.Max(0.1, PixelsPerMinute);
        var quarterPen = QuarterLineBrush is null ? null : new Pen(QuarterLineBrush, 1);
        var hourPen = HourLineBrush is null ? null : new Pen(HourLineBrush, 1);
        for (var (y, i) = (0.0, 0); y <= height; y += quarter, i++)
        {
            var pen = i % 4 == 0 ? hourPen : quarterPen;
            if (pen is not null)
            {
                // Halbe Pixel, damit die Linie scharf auf einem Pixel liegt.
                context.DrawLine(pen, new Point(0, Math.Floor(y) + 0.5), new Point(width, Math.Floor(y) + 0.5));
            }
        }
    }
}
