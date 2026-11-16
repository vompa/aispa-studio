using System.Windows.Input;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Rechte Seitenleiste „Verlauf“ (Design-Brief 5.12): zeigt vorhandene <see cref="HistorySectionViewModel"/> untereinander mit der <see cref="HistorySectionView"/>.
/// Breite 360, Kopf wie alle Leisten (<see cref="SidePanelHeader"/>), F6-Bereich. Esc und das Kreuz schließen (<see cref="IsOpen"/> wird falsch, dazu läuft <see cref="CloseCommand"/>);
/// beim Öffnen geht der Fokus in die Leiste, beim Schließen zurück auf das Element, das ihn vorher hatte.
/// </summary>
public sealed class HistoryPanel : Border
{
    public static readonly StyledProperty<IEnumerable<HistorySectionViewModel>?> SectionsProperty =
        AvaloniaProperty.Register<HistoryPanel, IEnumerable<HistorySectionViewModel>?>(nameof(Sections));

    public static readonly StyledProperty<string?> SubtitleProperty = AvaloniaProperty.Register<HistoryPanel, string?>(nameof(Subtitle));

    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<HistoryPanel, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<HistoryPanel, ICommand?>(nameof(CloseCommand));

    private readonly SidePanelHeader _header = new() { Title = "Verlauf", Margin = new Thickness(20, 16, 12, 12), CloseTip = "Schließen (Esc)" };
    private readonly ItemsControl _list = new();
    private IInputElement? _before;

    static HistoryPanel()
    {
        IsOpenProperty.Changed.AddClassHandler<HistoryPanel>((panel, e) => panel.OnIsOpenChanged(e.NewValue is true));
        SubtitleProperty.Changed.AddClassHandler<HistoryPanel>((panel, e) => panel._header.Subtitle = e.NewValue as string);
        SectionsProperty.Changed.AddClassHandler<HistoryPanel>((panel, e) => panel._list.ItemsSource = e.NewValue as IEnumerable<HistorySectionViewModel>);
    }

    public HistoryPanel()
    {
        Width = 360;
        Padding = new Thickness(0);
        Classes.Add("sidepanel");
        IsVisible = false;
        RegionNavigation.SetIsRegion(this, true);
        AutomationProperties.SetName(this, "Verlauf");
        _header.CloseCommand = new RelayCommand(Close);
        _list.ItemTemplate = new FuncDataTemplate<HistorySectionViewModel>((section, _) => new HistorySectionView { DataContext = section });
        _list.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 12 });
        var divider = new Border();
        divider.Classes.Add("divider");
        Grid.SetRow(divider, 1);
        var body = new ScrollViewer { Padding = new Thickness(16, 12, 16, 32), Content = _list };
        body.Classes.Add("panelbody");
        Grid.SetRow(body, 2);
        Child = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Children = { _header, divider, body } };
    }

    /// <summary>Die Stile der Seitenleisten (<c>Border.sidepanel</c>: Fläche, Linie links, Bewegung) gelten auch für diese Ableitung.</summary>
    protected override Type StyleKeyOverride => typeof(Border);

    public IEnumerable<HistorySectionViewModel>? Sections
    {
        get => GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
    }

    /// <summary>Untertitel unter „Verlauf“, z. B. wessen Verlauf das ist.</summary>
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

    private void OnIsOpenChanged(bool open)
    {
        // Wie bei der Seitenleiste im Kalender: sichtbar und „open“ zugleich, die Bewegung kommt aus den Stilen.
        IsVisible = open;
        Classes.Set("open", open);
        if (open)
        {
            _before = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            Dispatcher.UIThread.Post(() => RegionNavigation.Target(this)?.Focus(NavigationMethod.Tab), DispatcherPriority.Background);
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
