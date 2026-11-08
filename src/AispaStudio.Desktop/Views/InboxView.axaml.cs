using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AispaStudio.Desktop.Views;

public partial class InboxView : UserControl
{
    public InboxView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    /// <summary>Klick auf die Initialen des erkannten Absenders: Kundenübersicht neu laden, das Flyout öffnet der Knopf selbst.</summary>
    private void OnSenderOverviewClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: InboxDetailViewModel detail })
        {
            detail.ReloadOverview();
        }
    }

    /// <summary>Klick auf den Namen des erkannten Absenders: wie bei den Initialen die Kundenübersicht zeigen.</summary>
    private void OnSenderNamePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: InboxDetailViewModel { HasAssigned: true } detail } name
            && e.GetCurrentPoint(name).Properties.IsLeftButtonPressed)
        {
            detail.ReloadOverview();
            FlyoutBase.ShowAttachedFlyout(name);
            e.Handled = true;
        }
    }

    /// <summary>Klick auf den Namen eines Vorschlags unter „Wer ist das?“: Kundenübersicht laden, das Flyout öffnet der Knopf selbst.</summary>
    private void OnCandidateOverviewClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CandidateOption candidate })
        {
            _ = candidate.Overview.LoadAsync();
        }
    }

    /// <summary>Außerhalb von Eingabefeldern: O schaltet zwischen Übersetzung und Original, Strg+Z holt die vorige Fassung des Entwurfs.</summary>
    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not InboxViewModel { Detail: { } detail } || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.None)
        {
            detail.ToggleOriginal();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control && detail.UndoRevisionCommand.CanExecute(null))
        {
            _ = detail.UndoRevisionCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }
}
