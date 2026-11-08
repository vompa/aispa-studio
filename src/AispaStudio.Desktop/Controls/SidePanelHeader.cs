using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Kopf aller rechten Seitenleisten (Fluent 2: ein Muster für Panels): Titel und Untertitel links, das Schließen-Kreuz rechts oben an
/// derselben Stelle, immer mit demselben Abstand. Davor kann ein Zeichen oder Avatar stehen (<see cref="Leading"/>); statt des Titeltexts
/// lässt sich eigener Inhalt zeigen (<see cref="TitleContent"/>, z. B. Name mit ♀/♂-Zeichen und Stift). Links vom Kreuz kann eine Aktion stehen
/// (<see cref="Action"/>, z. B. „Neu beginnen“ bei KI und Hilfe, Ä359); sie teilt sich die Spalte mit dem Kreuz, damit der Titel ohne Aktion gleich breit bleibt.
/// Das Kreuz sagt im Hinweis, wie die Leiste noch zu schließen ist (Esc, bei KI und Hilfe Strg+Leertaste).
/// </summary>
public sealed class SidePanelHeader : Grid
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<SidePanelHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty = AvaloniaProperty.Register<SidePanelHeader, string?>(nameof(Subtitle));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<SidePanelHeader, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<string> CloseTipProperty = AvaloniaProperty.Register<SidePanelHeader, string>(nameof(CloseTip), "Schließen (Esc)");

    public static readonly StyledProperty<Control?> LeadingProperty = AvaloniaProperty.Register<SidePanelHeader, Control?>(nameof(Leading));

    public static readonly StyledProperty<Control?> TitleContentProperty = AvaloniaProperty.Register<SidePanelHeader, Control?>(nameof(TitleContent));

    public static readonly StyledProperty<Control?> ActionProperty = AvaloniaProperty.Register<SidePanelHeader, Control?>(nameof(Action));

    private readonly TextBlock _title = new() { VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _subtitle = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ContentControl _leading = new() { VerticalAlignment = VerticalAlignment.Top, IsVisible = false };
    private readonly ContentControl _titleContent = new() { IsVisible = false, Padding = new Thickness(0) };
    private readonly Button _close = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly ContentControl _action = new() { VerticalAlignment = VerticalAlignment.Top, IsVisible = false };

    static SidePanelHeader()
    {
        TitleProperty.Changed.AddClassHandler<SidePanelHeader>((h, _) => h.Refresh());
        SubtitleProperty.Changed.AddClassHandler<SidePanelHeader>((h, _) => h.Refresh());
        LeadingProperty.Changed.AddClassHandler<SidePanelHeader>((h, _) => h.Refresh());
        TitleContentProperty.Changed.AddClassHandler<SidePanelHeader>((h, _) => h.Refresh());
        ActionProperty.Changed.AddClassHandler<SidePanelHeader>((h, _) => h.Refresh());
        CloseTipProperty.Changed.AddClassHandler<SidePanelHeader>((h, e) => ToolTip.SetTip(h._close, e.NewValue));
        CloseCommandProperty.Changed.AddClassHandler<SidePanelHeader>((h, e) => h._close.Command = e.NewValue as ICommand);
    }

    public SidePanelHeader()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        ColumnSpacing = 12;
        Classes.Add("sidepanelheader");
        _title.Classes.Add("h2");
        _subtitle.Classes.Add("muted");
        _subtitle.Classes.Add("small");
        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { _titleContent, _title, _subtitle } };
        SetColumn(texts, 1);
        _close.Classes.Add("ghost");
        _close.Classes.Add("icon");
        _close.Content = new Icon { Data = Icons.Close };
        ToolTip.SetTip(_close, CloseTip);
        AutomationProperties.SetName(_close, "Schließen");
        var end = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Children = { _action, _close } };
        SetColumn(end, 2);
        Children.Add(_leading);
        Children.Add(texts);
        Children.Add(end);
        Refresh();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Was das Kreuz tut; meist der Schließen-Befehl des Panels.</summary>
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>Hinweis am Kreuz, z. B. „Schließen (Esc)“.</summary>
    public string CloseTip
    {
        get => GetValue(CloseTipProperty);
        set => SetValue(CloseTipProperty, value);
    }

    /// <summary>Zeichen oder Avatar vor dem Titel.</summary>
    public Control? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    /// <summary>Eigener Inhalt statt des Titeltexts.</summary>
    public Control? TitleContent
    {
        get => GetValue(TitleContentProperty);
        set => SetValue(TitleContentProperty, value);
    }

    /// <summary>
    /// Aktion links vom Kreuz, meist ein Knopf; ihre Sichtbarkeit steuert sie selbst. Den Abstand zum Kreuz trägt sie als eigenen Rand: so nimmt eine ausgeblendete
    /// Aktion dem Titel keinen Platz (ein Abstand am Platzhalter bliebe stehen).
    /// </summary>
    public Control? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    private void Refresh()
    {
        _title.Text = Title;
        _title.IsVisible = TitleContent is null && !string.IsNullOrEmpty(Title);
        _subtitle.Text = Subtitle;
        _subtitle.IsVisible = !string.IsNullOrEmpty(Subtitle);
        _leading.Content = Leading;
        _leading.IsVisible = Leading is not null;
        _titleContent.Content = TitleContent;
        _titleContent.IsVisible = TitleContent is not null;
        _action.Content = Action;
        _action.IsVisible = Action is not null;
    }
}
