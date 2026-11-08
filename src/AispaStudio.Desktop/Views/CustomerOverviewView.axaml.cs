using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace AispaStudio.Desktop.Views;

/// <summary>Kundenübersicht im Flyout, im Kalender (Terminkopf) und im Posteingang (erkannter Absender, Vorschläge unter „Wer ist das?“).</summary>
public partial class CustomerOverviewView : UserControl
{
    public CustomerOverviewView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UseThisClipboard();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UseThisClipboard();
    }

    /// <summary>
    /// Dieselbe Übersicht steht in mehreren Flyouts, jedes mit eigener Ansicht (Initialen und Name im Terminkopf). Kopiert wird über die zuletzt
    /// angehängte, also die offene Ansicht; eine geschlossene hat kein Fenster mehr und kopierte stumm nichts (T-031).
    /// </summary>
    private void UseThisClipboard()
    {
        if (DataContext is CustomerOverviewViewModel overview)
        {
            overview.ContactActions = new CalendarView.WindowContactActions(this);
        }
    }

    /// <summary>
    /// „Kundenkarte öffnen“ und „Bearbeiten“: der Befehl läuft zuerst, das Flyout geht erst danach zu (beim Schließen löst sich der Inhalt und mit ihm
    /// die Bindung des Befehls).
    /// </summary>
    private void OnAction(object? sender, RoutedEventArgs e)
    {
        if (this.FindLogicalAncestorOfType<Popup>() is { } popup)
        {
            Dispatcher.UIThread.Post(popup.Close, DispatcherPriority.Background);
        }
    }
}
