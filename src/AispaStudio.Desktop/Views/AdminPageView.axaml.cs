using System.ComponentModel;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AispaStudio.Desktop.Views;

/// <summary>Seite „Verwaltung“: Balken mit den Gruppen, Reiter der Masken und die Maske darunter (R05 Slice 125, Brief 6).</summary>
public partial class AdminPageView : UserControl
{
    private const string MoreTitle = "Mehr";
    private const string MoreTip = "Weitere Einträge";

    private AdminPageViewModel? _page;
    private AdminGroup? _shownGroup;

    public AdminPageView()
    {
        InitializeComponent();
        DressOverflow(GroupStrip, BarContent(Icons.ChevronDown, MoreTitle), "adminbaritem");
        DressOverflow(AreaStrip, MoreTitle, null);
        GroupStrip.LayoutUpdated += (_, _) => MarkOverflow(GroupStrip);
        AreaStrip.LayoutUpdated += (_, _) => MarkOverflow(AreaStrip);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_page is not null)
        {
            _page.PropertyChanged -= OnPagePropertyChanged;
        }

        _page = DataContext as AdminPageViewModel;
        _shownGroup = null;
        if (_page is null)
        {
            return;
        }

        _page.PropertyChanged += OnPagePropertyChanged;
        Refill(GroupStrip, _page.Groups.Select(GroupEntry));
        ShowSelection();
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdminPageViewModel.SelectedGroup))
        {
            ShowSelection();
        }
    }

    /// <summary>Zeigt die Wahl im Balken und in den Reitern; die Reiter werden nur beim Wechsel der Gruppe neu angelegt, damit der Fokus auf einem Reiter bleibt.</summary>
    private void ShowSelection()
    {
        var group = _page?.SelectedGroup;
        if (group != _shownGroup)
        {
            _shownGroup = group;
            Refill(AreaStrip, group?.Areas.Select(AreaEntry) ?? []);
        }

        foreach (var entry in GroupStrip.Children.OfType<TabButton>())
        {
            entry.IsSelected = ReferenceEquals(entry.CommandParameter, group);
        }

        foreach (var entry in AreaStrip.Children.OfType<TabButton>())
        {
            entry.IsSelected = ReferenceEquals(entry.CommandParameter, _page?.SelectedArea);
        }

        MarkOverflow(GroupStrip);
        MarkOverflow(AreaStrip);
    }

    private TabButton GroupEntry(AdminGroup group)
    {
        var entry = new TabButton { Content = BarContent(group.Icon, group.Title), Command = _page!.SelectGroupCommand, CommandParameter = group };
        entry.Classes.Add("adminbaritem");
        Describe(entry, group.Title, group.Icon);
        return entry;
    }

    private TabButton AreaEntry(AdminArea area)
    {
        var entry = new TabButton { Content = area.Title, Command = _page!.SelectAreaCommand, CommandParameter = area };
        Describe(entry, area.Title, null);
        return entry;
    }

    private static void Describe(TabButton entry, string title, string? icon)
    {
        AutomationProperties.SetName(entry, title);
        OverflowStrip.SetHeader(entry, title);
        OverflowStrip.SetIcon(entry, icon);
    }

    private static void Refill(OverflowStrip strip, IEnumerable<TabButton> entries)
    {
        strip.Children.RemoveAll([.. strip.Children.OfType<TabButton>()]);
        strip.Children.InsertRange(0, [.. entries]);
    }

    private static StackPanel BarContent(string icon, string title) => new()
    {
        Spacing = 4,
        HorizontalAlignment = HorizontalAlignment.Center,
        Children =
        {
            new Icon { Data = icon, Width = 22, Height = 22, HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = title, Classes = { "navtitle" }, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center },
            new Border { Classes = { "barmark" }, Width = 20, Height = 3, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Center },
        },
    };

    /// <summary>Das Menü „Mehr“ sieht aus wie ein Eintrag seiner Schiene, nicht wie der Chip der Kalender-Kopfzeile.</summary>
    private static void DressOverflow(OverflowStrip strip, object content, string? entryClass)
    {
        var more = strip.OverflowButton;
        more.Classes.Remove("headchip");
        if (entryClass is not null)
        {
            more.Classes.Add(entryClass);
        }

        more.Content = content;
        ToolTip.SetTip(more, MoreTip);
        AutomationProperties.SetName(more, MoreTitle);
    }

    /// <summary>Steht die Wahl im Überlauf, trägt „Mehr“ ihren Zustand: Sonst hätte die Schiene keinen Tab-Stopp, und niemand sähe, was offen ist (Brief 6).</summary>
    private static void MarkOverflow(OverflowStrip strip)
    {
        var hidden = strip.Overflowed.OfType<TabButton>().FirstOrDefault(e => e.IsSelected);
        var more = strip.OverflowButton;
        var chosen = hidden is null ? null : $"{MoreTitle}, gewählt: {AutomationProperties.GetName(hidden)}";
        more.Classes.Set("on", hidden is not null);
        AutomationProperties.SetName(more, chosen ?? MoreTitle);
        ToolTip.SetTip(more, chosen ?? MoreTip);
    }
}
