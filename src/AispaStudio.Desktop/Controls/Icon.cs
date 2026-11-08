using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Linien-Symbol auf einem 24×24-Raster (Stil wie Lucide), gezeichnet in der Textfarbe der Umgebung.
/// Dadurch passt es sich hell, dunkel und dem Zustand von Schaltflächen ohne eigene Farben an.
/// </summary>
public sealed class Icon : Avalonia.Controls.Control
{
    public static readonly StyledProperty<string?> DataProperty =
        AvaloniaProperty.Register<Icon, string?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icon>();

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), 1.8);

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ForegroundProperty, StrokeThicknessProperty);
        WidthProperty.OverrideDefaultValue<Icon>(18);
        HeightProperty.OverrideDefaultValue<Icon>(18);
    }

    private Geometry? _geometry;

    /// <summary>Pfaddaten, z. B. aus <see cref="Icons"/>.</summary>
    public string? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty)
        {
            _geometry = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Data is null || Foreground is null)
        {
            return;
        }

        _geometry ??= StreamGeometry.Parse(Data);

        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24;
        var pen = new Pen(Foreground, StrokeThickness / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(null, pen, _geometry);
        }
    }
}
