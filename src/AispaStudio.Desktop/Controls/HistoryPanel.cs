using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Rechte Seitenleiste „Verlauf“ (Design-Brief 5.12): zeigt vorhandene <see cref="HistorySectionViewModel"/> untereinander mit der <see cref="HistorySectionView"/>.
/// Kopf, Breite, Esc, Fokus und F6-Bereich kommen von <see cref="SidePanel"/>.
/// </summary>
public sealed class HistoryPanel : SidePanel
{
    public static readonly StyledProperty<IEnumerable<HistorySectionViewModel>?> SectionsProperty =
        AvaloniaProperty.Register<HistoryPanel, IEnumerable<HistorySectionViewModel>?>(nameof(Sections));

    private readonly ItemsControl _list = new();

    static HistoryPanel()
    {
        SectionsProperty.Changed.AddClassHandler<HistoryPanel>((panel, e) => panel._list.ItemsSource = e.NewValue as IEnumerable<HistorySectionViewModel>);
    }

    public HistoryPanel()
    {
        Title = "Verlauf";
        _list.ItemTemplate = new FuncDataTemplate<HistorySectionViewModel>((section, _) => new HistorySectionView { DataContext = section });
        _list.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 12 });
        var body = new ScrollViewer { Padding = new Thickness(16, 12, 16, 32), Content = _list };
        body.Classes.Add("panelbody");
        Body = body;
    }

    public IEnumerable<HistorySectionViewModel>? Sections
    {
        get => GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
    }
}
