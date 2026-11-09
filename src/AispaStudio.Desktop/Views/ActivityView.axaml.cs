using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Seite „Verlauf“: Strg+F fokussiert die Suche, Enter öffnet den gewählten Eintrag, am Ende der Liste lädt die nächste Seite nach.</summary>
public partial class ActivityView : UserControl
{
    /// <summary>Abstand zum Ende der Liste in Pixeln, ab dem die nächste Seite geladen wird.</summary>
    private const double LoadMoreDistance = 80;

    public ActivityView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        List.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
        List.DoubleTapped += (_, _) => (DataContext as ActivityViewModel)?.OpenSelected();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
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
}
