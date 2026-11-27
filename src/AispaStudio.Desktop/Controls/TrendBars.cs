using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AispaStudio.Desktop.Controls;

/// <summary>Ein Balken im Verlauf: Beschriftung („Mo 02.11.“), Wert für die Höhe und der Wert als Text („1.234,50 €“).</summary>
public sealed record TrendBar(string Label, double Value, string Text);

/// <summary>
/// Einfaches Säulendiagramm für eine Reihe (Verlauf im Management-Bericht): Titel oben, Säulen gleicher Breite (höchstens 32) mit 4 px Abstand, oben 4 px gerundet, aus der Grundlinie
/// (1 px line), Füllung accent; darunter die erste und die letzte Beschriftung. Der höchste Wert füllt die Höhe, negative Werte stehen als 0. Jede Säule nennt im Tooltip
/// Beschriftung und Wert. Für Screenreader sind die Säulen nur ein Bild: dieselben Werte stehen in der Tabelle „Werte als Tabelle“ darunter (aufklappbar).
/// </summary>
public sealed class TrendBars : StackPanel
{
    /// <summary>Höhe der Zeichenfläche.</summary>
    public const double ChartHeight = 96;

    /// <summary>Breiteste Säule: bei wenigen Werten bleiben es Säulen statt einer Fläche.</summary>
    private const double MaxBarWidth = 32;

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<TrendBars, string?>(nameof(Title));

    public static readonly StyledProperty<IReadOnlyList<TrendBar>?> BarsProperty = AvaloniaProperty.Register<TrendBars, IReadOnlyList<TrendBar>?>(nameof(Bars));

    private readonly TextBlock _title = new() { Name = "TrendTitle" };
    private readonly Grid _plot = new() { Name = "TrendPlot", Height = ChartHeight };
    private readonly TextBlock _first = new() { Name = "TrendFirstLabel" };
    private readonly TextBlock _last = new() { Name = "TrendLastLabel", HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Grid _table = new() { Name = "TrendTable", ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, RowSpacing = 4 };
    private readonly Expander _tableToggle = new() { Name = "TrendTableToggle", Header = "Werte als Tabelle", HorizontalAlignment = HorizontalAlignment.Stretch };

    static TrendBars()
    {
        TitleProperty.Changed.AddClassHandler<TrendBars>((chart, _) => chart.Rebuild());
        BarsProperty.Changed.AddClassHandler<TrendBars>((chart, _) => chart.Rebuild());
    }

    public TrendBars()
    {
        Spacing = 4;
        _title.Classes.Add("group");
        _first.Classes.Add("muted");
        _first.Classes.Add("small");
        _last.Classes.Add("muted");
        _last.Classes.Add("small");
        AutomationProperties.SetAccessibilityView(_plot, AccessibilityView.Raw);
        var baseline = new Border { Height = 1 };
        baseline.Bind(Border.BackgroundProperty, baseline.GetResourceObservable("LineBrush"));
        AutomationProperties.SetAccessibilityView(baseline, AccessibilityView.Raw);
        var axis = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { _first, _last } };
        Grid.SetColumn(_last, 1);
        _tableToggle.Content = _table;
        Children.AddRange([_title, _plot, baseline, axis, _tableToggle]);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IReadOnlyList<TrendBar>? Bars
    {
        get => GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    private void Rebuild()
    {
        var bars = Bars ?? [];
        _title.Text = Title;
        // Sichtbar steht nur „Werte als Tabelle“ (der Titel steht schon darüber); der Screenreader hört, zu welcher Reihe die Tabelle gehört.
        AutomationProperties.SetName(_tableToggle, $"{Title}: Werte als Tabelle");
        _first.Text = bars.Count > 0 ? bars[0].Label : null;
        _last.Text = bars.Count > 1 ? bars[^1].Label : null;

        _plot.Children.Clear();
        _plot.ColumnDefinitions.Clear();
        var max = bars.Count == 0 ? 0 : bars.Max(b => b.Value);
        for (var i = 0; i < bars.Count; i++)
        {
            _plot.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var height = max > 0 ? Math.Max(0, bars[i].Value) / max * ChartHeight : 0;
            var column = new Border
            {
                Height = height,
                VerticalAlignment = VerticalAlignment.Bottom,
                MaxWidth = MaxBarWidth,
                Margin = new Thickness(2, 0),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Background = Brushes.Transparent,
            };
            column.Bind(Border.BackgroundProperty, column.GetResourceObservable("AccentBrush"));
            ToolTip.SetTip(column, $"{bars[i].Label}: {bars[i].Text}");
            Grid.SetColumn(column, i);
            _plot.Children.Add(column);
        }

        _table.Children.Clear();
        _table.RowDefinitions.Clear();
        for (var i = 0; i < bars.Count; i++)
        {
            _table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = new TextBlock { Text = bars[i].Label };
            var value = new TextBlock { Text = bars[i].Text };
            value.Classes.Add("num");
            Grid.SetRow(label, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            _table.Children.Add(label);
            _table.Children.Add(value);
        }
    }
}
