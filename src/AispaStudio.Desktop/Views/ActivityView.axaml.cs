using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Views;

/// <summary>Seite „Verlauf“: Strg+F fokussiert die Suche, Enter öffnet den gewählten Eintrag im Seitenpanel, Esc schließt es, am Ende der Liste lädt die nächste Seite nach.</summary>
public partial class ActivityView : UserControl
{
    /// <summary>Abstand zum Ende der Liste in Pixeln, ab dem die nächste Seite geladen wird.</summary>
    private const double LoadMoreDistance = 80;

    public ActivityView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Subscribe();
        KeyDown += OnKeyDown;
        List.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
        List.DoubleTapped += (_, _) => (DataContext as ActivityViewModel)?.OpenSelected();
    }

    private ActivityViewModel? _activity;

    private void Subscribe()
    {
        if (_activity is not null)
        {
            _activity.EntryOpened -= FocusPanel;
            _activity.PanelClosed -= FocusList;
        }

        _activity = DataContext as ActivityViewModel;
        if (_activity is not null)
        {
            _activity.EntryOpened += FocusPanel;
            _activity.PanelClosed += FocusList;
        }
    }

    /// <summary>Beim Öffnen bekommt der primäre Knopf des Panels den Fokus (5.12 Punkt 9); ohne einen den ersten Knopf unter dem Kopf.</summary>
    private void FocusPanel(ActivityEntry entry) =>
        Dispatcher.UIThread.Post(() =>
        {
            var buttons = SidePanel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled && b.Focusable).ToList();
            (buttons.FirstOrDefault(b => b.Classes.Contains("primary")) ?? buttons.FirstOrDefault(b => b.Name is not null))?.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Background);

    /// <summary>Nach Esc oder Schließen steht der Fokus wieder auf dem gewählten Eintrag der Liste.</summary>
    private void FocusList() =>
        Dispatcher.UIThread.Post(() =>
        {
            var item = _activity?.SelectedItem is { } selected ? List.ContainerFromItem(selected) as Control : null;
            (item ?? List).Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Background);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && _activity is { HasPanel: true } && !IsInside(e.Source, SearchBox))
        {
            _activity.ClosePanel();
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && e.Source is ListBoxItem && DataContext is ActivityViewModel activity)
        {
            activity.OpenSelected();
            e.Handled = true;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is ScrollViewer scroll && DataContext is ActivityViewModel { ShowMore: true } activity
            && scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - LoadMoreDistance)
        {
            Dispatcher.UIThread.Post(() => _ = activity.LoadMoreAsync(), DispatcherPriority.Background);
        }
    }

    private static bool IsInside(object? source, Control container) => source is Visual visual && (ReferenceEquals(visual, container) || visual.GetVisualAncestors().Contains(container));
}
