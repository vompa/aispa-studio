using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AispaStudio.Desktop.Controls;

/// <summary>Schweregrad einer Meldung; je mit Symbol und Farbe (nie nur Farbe), siehe docs/gestaltung/meldungen.md.</summary>
public enum InfoBarSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// Meldung zu einem Zustand der Seite oder des Formulars (Fluent 2 „Message bar“): Symbol, optional Titel, Text von ein bis zwei Zeilen, rechts
/// eine Aktion und das Schließen-Kreuz. Fehler und Warnungen sagt ein Screenreader sofort an, Info und Erfolg höflich (<c>LiveSetting</c>).
/// </summary>
public sealed class InfoBar : Border
{
    public static readonly StyledProperty<InfoBarSeverity> SeverityProperty = AvaloniaProperty.Register<InfoBar, InfoBarSeverity>(nameof(Severity));

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<InfoBar, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty = AvaloniaProperty.Register<InfoBar, string?>(nameof(Message));

    public static readonly StyledProperty<object?> DetailProperty = AvaloniaProperty.Register<InfoBar, object?>(nameof(Detail));

    public static readonly StyledProperty<string?> ActionTextProperty = AvaloniaProperty.Register<InfoBar, string?>(nameof(ActionText));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty = AvaloniaProperty.Register<InfoBar, ICommand?>(nameof(ActionCommand));

    public static readonly StyledProperty<object?> ActionParameterProperty = AvaloniaProperty.Register<InfoBar, object?>(nameof(ActionParameter));

    public static readonly StyledProperty<bool> StackedProperty = AvaloniaProperty.Register<InfoBar, bool>(nameof(Stacked));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<InfoBar, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<object?> CloseParameterProperty = AvaloniaProperty.Register<InfoBar, object?>(nameof(CloseParameter));

    private readonly Icon _icon = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ContentControl _detail = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly StackPanel _texts;
    private readonly StackPanel _buttons;
    private readonly Button _action = new() { VerticalAlignment = VerticalAlignment.Top, MinHeight = 0, Padding = new Thickness(10, 3) };
    private readonly Button _close = new() { VerticalAlignment = VerticalAlignment.Top };

    static InfoBar()
    {
        SeverityProperty.Changed.AddClassHandler<InfoBar>((bar, _) => bar.Refresh());
        TitleProperty.Changed.AddClassHandler<InfoBar>((bar, _) => bar.Refresh());
        MessageProperty.Changed.AddClassHandler<InfoBar>((bar, _) => bar.Refresh());
        DetailProperty.Changed.AddClassHandler<InfoBar>((bar, _) => bar.Refresh());
        ActionTextProperty.Changed.AddClassHandler<InfoBar>((bar, _) => bar.Refresh());
        StackedProperty.Changed.AddClassHandler<InfoBar>((bar, _) => bar.Refresh());
        ActionCommandProperty.Changed.AddClassHandler<InfoBar>((bar, e) => bar._action.Command = e.NewValue as ICommand);
        ActionParameterProperty.Changed.AddClassHandler<InfoBar>((bar, e) => bar._action.CommandParameter = e.NewValue);
        CloseCommandProperty.Changed.AddClassHandler<InfoBar>((bar, e) =>
        {
            bar._close.Command = e.NewValue as ICommand;
            bar.Refresh();
        });
        CloseParameterProperty.Changed.AddClassHandler<InfoBar>((bar, e) => bar._close.CommandParameter = e.NewValue);
    }

    public InfoBar()
    {
        Classes.Add("infobar");
        _icon.Classes.Add("sev");
        _texts = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { _title, _message, _detail } };
        _buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top, Children = { _action, _close } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, Children = { _icon, _texts, _buttons } };
        Grid.SetColumn(_texts, 1);
        Grid.SetColumn(_buttons, 2);
        Child = grid;
        _action.Classes.Add("infobar-action");
        _close.Classes.Add("ghost");
        _close.Classes.Add("icon");
        _close.Classes.Add("infobar-close");
        _close.Content = new Icon { Data = Icons.Close, Width = 14, Height = 14 };
        ToolTip.SetTip(_close, "Schließen");
        AutomationProperties.SetName(_close, "Meldung schließen");
        Refresh();
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public InfoBarSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>Kurz, was passiert ist; kann fehlen.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Was die Person tun kann; ein bis zwei kurze Sätze.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Inhalt unter dem Text, z. B. ein einmal gezeigter Code; ohne Inhalt nichts.</summary>
    public object? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>Ein Verb wie „Erneut versuchen“; ohne Text gibt es keinen Knopf.</summary>
    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public object? ActionParameter
    {
        get => GetValue(ActionParameterProperty);
        set => SetValue(ActionParameterProperty, value);
    }

    /// <summary>Die Aktion steht unter dem Text statt rechts daneben: für schmale Flächen wie die Seitenleiste, wo der Knopf den Text sonst zusammendrückt.</summary>
    public bool Stacked
    {
        get => GetValue(StackedProperty);
        set => SetValue(StackedProperty, value);
    }

    /// <summary>Was das Kreuz tut; ohne Befehl gibt es kein Kreuz.</summary>
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public object? CloseParameter
    {
        get => GetValue(CloseParameterProperty);
        set => SetValue(CloseParameterProperty, value);
    }

    /// <summary>Die Aktion steht rechts neben dem Text; mit <see cref="Stacked"/> als letzte Zeile unter dem Text.</summary>
    private void PlaceAction()
    {
        var host = Stacked ? _texts : _buttons;
        if (_action.Parent == host)
        {
            return;
        }

        (_action.Parent as Panel)?.Children.Remove(_action);
        host.Children.Insert(Stacked ? host.Children.Count : 0, _action);
        _action.HorizontalAlignment = Stacked ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        _action.Margin = Stacked ? new Thickness(0, 6, 0, 0) : default;
    }

    private void Refresh()
    {
        Classes.Set("info", Severity == InfoBarSeverity.Info);
        Classes.Set("ok", Severity == InfoBarSeverity.Success);
        Classes.Set("warn", Severity == InfoBarSeverity.Warning);
        Classes.Set("crit", Severity == InfoBarSeverity.Error);
        _icon.Data = Severity switch
        {
            InfoBarSeverity.Success => Icons.CheckCircle,
            InfoBarSeverity.Warning => Icons.Warning,
            InfoBarSeverity.Error => Icons.ErrorCircle,
            _ => Icons.Info,
        };
        _title.Text = Title;
        _title.IsVisible = !string.IsNullOrEmpty(Title);
        _message.Text = Message;
        _message.IsVisible = !string.IsNullOrEmpty(Message);
        _detail.Content = Detail;
        _detail.IsVisible = Detail is not null;
        _action.Content = ActionText;
        _action.IsVisible = !string.IsNullOrEmpty(ActionText);
        PlaceAction();
        _close.IsVisible = CloseCommand is not null;
        // Fehler und Warnungen unterbrechen den Screenreader, Info und Erfolg warten, bis er fertig ist (M6).
        AutomationProperties.SetLiveSetting(this, Severity is InfoBarSeverity.Error or InfoBarSeverity.Warning ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        AutomationProperties.SetName(this, string.Join(": ", new[] { Title, Message }.Where(t => !string.IsNullOrEmpty(t))));
    }
}
