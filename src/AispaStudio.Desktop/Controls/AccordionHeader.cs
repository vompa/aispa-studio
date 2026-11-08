using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Primitives;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Kopf eines Akkordeons (Fluent 2 Accordion, Design-Brief 5.6 und 7): ein Knopf mit Zustand „aufgeklappt“. Gewählt (<c>IsChecked</c>) heißt aufgeklappt; für
/// Screenreader meldet er den Zustand als auf- oder zugeklappt statt als Ein/Aus (<see cref="IExpandCollapseProvider"/>). Enter und Leertaste schalten wie bei
/// jedem Knopf. Aussehen und Inhalt gibt der Stil bzw. die Maske vor; der Inhalt unter dem Kopf gehört der Maske.
/// </summary>
public sealed class AccordionHeader : ToggleButton
{
    static AccordionHeader() =>
        IsCheckedProperty.Changed.AddClassHandler<AccordionHeader>((header, e) =>
            (ControlAutomationPeer.FromElement(header) as AccordionHeaderPeer)?.StateChanged(e.OldValue is true, e.NewValue is true));

    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override AutomationPeer OnCreateAutomationPeer() => new AccordionHeaderPeer(this);

    private sealed class AccordionHeaderPeer(AccordionHeader owner) : ToggleButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public bool ShowsMenu => false;

        public ExpandCollapseState ExpandCollapseState => owner.IsChecked == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public void Expand() => owner.IsChecked = true;

        public void Collapse() => owner.IsChecked = false;

        public void StateChanged(bool wasExpanded, bool isExpanded) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                wasExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                isExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }
}
