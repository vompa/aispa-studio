using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Reiter einer Reiterschiene (Design-Brief 5.4 und 5.12, Fluent 2 „Tab list“): ein Knopf mit dem Zustand „gewählt“ (<see cref="IsSelected"/>). Für Screenreader eine
/// Registerkarte mit Auswahlzustand (<see cref="ISelectionItemProvider"/>) statt eines Knopfs ohne Zustand. Aussehen und Tastatur bleiben die des <see cref="Button"/>
/// mit der Klasse <c>on</c>, die der Reiter aus <see cref="IsSelected"/> selbst setzt (Stile in <c>Styles.axaml</c>, <see cref="TabStripBehavior"/>).
/// </summary>
public sealed class TabButton : Button
{
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<TabButton, bool>(nameof(IsSelected));

    static TabButton() =>
        IsSelectedProperty.Changed.AddClassHandler<TabButton>((tab, e) =>
        {
            tab.Classes.Set("on", e.NewValue is true);
            (ControlAutomationPeer.FromElement(tab) as TabButtonPeer)?.SelectionChanged(e.OldValue is true, e.NewValue is true);
        });

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Button);

    protected override AutomationPeer OnCreateAutomationPeer() => new TabButtonPeer(this);

    private sealed class TabButtonPeer(TabButton owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        // „Auswählen“ per Screenreader wirkt wie ein Klick; abwählen lässt sich ein Reiter nur durch die Wahl eines anderen.
        public void Select()
        {
            if (!owner.IsSelected)
            {
                Invoke();
            }
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void SelectionChanged(bool wasSelected, bool isSelected) =>
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, wasSelected, isSelected);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
    }
}
