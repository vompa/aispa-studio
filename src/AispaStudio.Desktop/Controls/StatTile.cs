using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>Zustand einer <see cref="StatTile"/>; „Warn“ und „Ok“ zeigen zusätzlich ein Symbol (nie nur Farbe).</summary>
public enum StatTileState
{
    None,
    Ok,
    Warn,
}

/// <summary>
/// Kennzahl-Kachel für Übersichten (Design-Brief 5.5): Beschriftung oben, der Wert groß darunter, darunter ein optionaler Hinweis. Mit <see cref="Button.Command"/> ist die Kachel
/// ein Knopf (Tab-Stopp, Enter und Leertaste lösen aus, Hover, Drücken und Fokus wie bei den Knöpfen); ohne Befehl ist sie reine Anzeige und kein Tab-Stopp.
/// Screenreader hören „Beschriftung: Wert“, mit Hinweis „Beschriftung: Wert, Hinweis“. Stile: <c>Button.stattile</c> in <c>Styles.axaml</c>.
/// </summary>
public sealed class StatTile : Button
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<StatTile, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<StatTile, string?>(nameof(Value));

    public static readonly StyledProperty<string?> HintProperty = AvaloniaProperty.Register<StatTile, string?>(nameof(Hint));

    public static readonly StyledProperty<string?> AccessibleHintProperty = AvaloniaProperty.Register<StatTile, string?>(nameof(AccessibleHint));

    public static readonly StyledProperty<StatTileState> StateProperty = AvaloniaProperty.Register<StatTile, StatTileState>(nameof(State));

    public static readonly StyledProperty<string?> ChangeProperty = AvaloniaProperty.Register<StatTile, string?>(nameof(Change));

    public static readonly StyledProperty<StatTileState> ChangeStateProperty = AvaloniaProperty.Register<StatTile, StatTileState>(nameof(ChangeState));

    private readonly TextBlock _label = new() { Name = "StatTileLabel", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _value = new() { Name = "StatTileValue" };
    private readonly TextBlock _hint = new() { Name = "StatTileHint", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Icon _icon = new() { Name = "StatTileIcon", Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Top, IsVisible = false };
    private readonly TextBlock _changeText = new() { Classes = { "num" } };
    private readonly Border _change = new() { Name = "StatTileChange", Margin = new Thickness(0, 4, 0, 2), IsVisible = false };

    static StatTile()
    {
        LabelProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        ValueProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        HintProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        StateProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        AccessibleHintProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        ChangeProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        ChangeStateProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        CommandProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
    }

    public StatTile()
    {
        Classes.Add("stattile");
        _label.Classes.Add("group");
        _value.Classes.Add("bigvalue");
        _hint.Classes.Add("muted");
        _hint.Classes.Add("small");
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(_icon, 1);
        head.Children.Add(_label);
        head.Children.Add(_icon);
        _change.Classes.Add("pill");
        _change.Child = _changeText;
        Content = new StackPanel { Spacing = 2, Children = { head, _value, _change, _hint } };
        Refresh();
    }

    /// <summary>Kleine, gedämpfte Beschriftung oben.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Der hervorgehobene Wert.</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Optionale gedämpfte Zeile darunter (z. B. Vergleich mit der Vorperiode).</summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>
    /// Satz für Screenreader statt des sichtbaren Hinweises, wenn dieser Pfeile und Kurzformen enthält („24 % unter der Vorperiode“): der Name lautet dann „Beschriftung Wert, Satz“.
    /// Ein Warnzeichen wird dann nicht noch einmal angesagt, der Satz nennt die Richtung selbst.
    /// </summary>
    public string? AccessibleHint
    {
        get => GetValue(AccessibleHintProperty);
        set => SetValue(AccessibleHintProperty, value);
    }

    public StatTileState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Veränderung als Pille unter dem Wert („↓ 24 %“, Management-Bericht T-104): ungünstig warn auf warn-soft, günstig ok auf ok-soft (<see cref="ChangeState"/>), sonst grau.
    /// Die Pille ersetzt Symbol und Fläche von <see cref="State"/>; Kacheln ohne sie bleiben wie bisher.
    /// </summary>
    public string? Change
    {
        get => GetValue(ChangeProperty);
        set => SetValue(ChangeProperty, value);
    }

    /// <summary>Richtung der Veränderung für die Farbe der Pille; Pfeil und Text sagen dasselbe, die Farbe trägt nie allein.</summary>
    public StatTileState ChangeState
    {
        get => GetValue(ChangeStateProperty);
        set => SetValue(ChangeStateProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Button);

    private void Refresh()
    {
        _label.Text = Label;
        _value.Text = Value;
        _hint.Text = Hint;
        _hint.IsVisible = !string.IsNullOrEmpty(Hint);
        _changeText.Text = Change;
        _change.IsVisible = !string.IsNullOrEmpty(Change);
        _change.Classes.Set("ok", ChangeState == StatTileState.Ok);
        _change.Classes.Set("warn", ChangeState == StatTileState.Warn);
        _icon.Data = State switch
        {
            StatTileState.Warn => Icons.Warning,
            StatTileState.Ok => Icons.Check,
            _ => null,
        };
        _icon.IsVisible = State != StatTileState.None;
        Classes.Set("ok", State == StatTileState.Ok);
        Classes.Set("warn", State == StatTileState.Warn);

        var clickable = Command is not null;
        Classes.Set("static", !clickable);
        Focusable = clickable;
        IsTabStop = clickable;
        if (!string.IsNullOrEmpty(AccessibleHint))
        {
            AutomationProperties.SetName(this, $"{Label} {Value}, {AccessibleHint}");
            return;
        }

        var name = string.IsNullOrEmpty(Hint) ? $"{Label}: {Value}" : $"{Label}: {Value}, {Hint}";
        AutomationProperties.SetName(this, State == StatTileState.Warn ? $"{name}, Warnung" : name);
    }
}
