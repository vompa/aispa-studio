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

    public static readonly StyledProperty<StatTileState> StateProperty = AvaloniaProperty.Register<StatTile, StatTileState>(nameof(State));

    private readonly TextBlock _label = new() { Name = "StatTileLabel", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _value = new() { Name = "StatTileValue" };
    private readonly TextBlock _hint = new() { Name = "StatTileHint", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Icon _icon = new() { Name = "StatTileIcon", Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Top, IsVisible = false };

    static StatTile()
    {
        LabelProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        ValueProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        HintProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
        StateProperty.Changed.AddClassHandler<StatTile>((tile, _) => tile.Refresh());
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
        Content = new StackPanel { Spacing = 2, Children = { head, _value, _hint } };
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

    public StatTileState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Button);

    private void Refresh()
    {
        _label.Text = Label;
        _value.Text = Value;
        _hint.Text = Hint;
        _hint.IsVisible = !string.IsNullOrEmpty(Hint);
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
        var name = string.IsNullOrEmpty(Hint) ? $"{Label}: {Value}" : $"{Label}: {Value}, {Hint}";
        AutomationProperties.SetName(this, State == StatTileState.Warn ? $"{name}, Warnung" : name);
    }
}
