using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Rechte Seitenleiste einer Verwaltungsmaske (Design-Brief 5.12, ADR 0098): Breite 360, Kopf wie alle Leisten (<see cref="SidePanelHeader"/>) mit Trenner, darunter
/// der Inhalt (<see cref="Body"/>), F6-Bereich. Esc und das Kreuz schließen (<see cref="IsOpen"/> wird falsch, dazu läuft <see cref="CloseCommand"/>);
/// beim Öffnen geht der Fokus in die Leiste, beim Schließen zurück auf das Element, das ihn vorher hatte. Grundlage von <see cref="HistoryPanel"/> und der Leiste „Anträge“ im Wochenplan.
/// </summary>
public class SidePanel : Border
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<SidePanel, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty = AvaloniaProperty.Register<SidePanel, string?>(nameof(Subtitle));

    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<SidePanel, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<SidePanel, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<Control?> BodyProperty = AvaloniaProperty.Register<SidePanel, Control?>(nameof(Body));

    public static readonly StyledProperty<Control?> HeaderActionProperty = AvaloniaProperty.Register<SidePanel, Control?>(nameof(HeaderAction));

    private readonly SidePanelHeader _header = new() { Margin = new Thickness(20, 16, 12, 12), CloseTip = "Schließen (Esc)" };
    private readonly ContentControl _body = new() { Padding = new Thickness(0) };
    private IInputElement? _before;

    static SidePanel()
    {
        IsOpenProperty.Changed.AddClassHandler<SidePanel>((panel, e) => panel.OnIsOpenChanged(e.NewValue is true));
        TitleProperty.Changed.AddClassHandler<SidePanel>((panel, e) =>
        {
            panel._header.Title = e.NewValue as string;
            AutomationProperties.SetName(panel, e.NewValue as string);
        });
        SubtitleProperty.Changed.AddClassHandler<SidePanel>((panel, e) => panel._header.Subtitle = e.NewValue as string);
        BodyProperty.Changed.AddClassHandler<SidePanel>((panel, e) => panel._body.Content = e.NewValue);
        HeaderActionProperty.Changed.AddClassHandler<SidePanel>((panel, e) => panel._header.Action = e.NewValue as Control);
    }

    public SidePanel()
    {
        Width = 360;
        Padding = new Thickness(0);
        Classes.Add("sidepanel");
        IsVisible = false;
        RegionNavigation.SetIsRegion(this, true);
        _header.CloseCommand = new RelayCommand(Close);
        var divider = new Border();
        divider.Classes.Add("divider");
        Grid.SetRow(divider, 1);
        Grid.SetRow(_body, 2);
        Child = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Children = { _header, divider, _body } };
    }

    /// <summary>Ohne diesen Schlüssel griffe der Stil <c>Border.sidepanel</c> nicht (Fläche, Rahmen, Bewegung), und die Leiste wäre durchsichtig.</summary>
    protected override Type StyleKeyOverride => typeof(Border);

    /// <summary>Titel im Kopf, zugleich der Name der Leiste für den Screenreader.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Untertitel unter dem Titel, z. B. wessen Verlauf das ist.</summary>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Läuft zusätzlich, wenn die Leiste per Kreuz oder Esc geschlossen wird.</summary>
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>Inhalt unter dem Kopf; er rollt selbst, wenn er höher als die Leiste ist.</summary>
    public Control? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    /// <summary>Knopf links vom Kreuz, z. B. das Fragezeichen der Hilfe.</summary>
    public Control? HeaderAction
    {
        get => GetValue(HeaderActionProperty);
        set => SetValue(HeaderActionProperty, value);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && IsOpen)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Close()
    {
        IsOpen = false;
        CloseCommand?.Execute(null);
    }

    private InputElement? FirstInBody() => _body.GetVisualDescendants().OfType<InputElement>()
        .FirstOrDefault(e => e.Focusable && e.IsTabStop && e.IsEffectivelyVisible && e.IsEffectivelyEnabled);

    private void OnIsOpenChanged(bool open)
    {
        // Wie bei der Seitenleiste im Kalender: sichtbar und „open“ zugleich, die Bewegung kommt aus den Stilen.
        IsVisible = open;
        Classes.Set("open", open);
        if (open)
        {
            _before = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            // Zuerst das erste Bedienelement des Inhalts (bei „Anträge“ der erste Antrag), nicht das Fragezeichen im Kopf; ohne eines wie bisher das Ziel des Bereichs.
            Dispatcher.UIThread.Post(() => (FirstInBody() ?? RegionNavigation.Target(this))?.Focus(NavigationMethod.Tab), DispatcherPriority.Background);
        }
        else
        {
            var back = _before;
            _before = null;
            Dispatcher.UIThread.Post(() =>
            {
                if (back is InputElement { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } element && !this.IsVisualAncestorOf(element))
                {
                    element.Focus(NavigationMethod.Tab);
                }
            }, DispatcherPriority.Background);
        }
    }
}
