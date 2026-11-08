using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Terminkarte im Kalender (Termin, Sperrzeit, Pause): für Screenreader ein Listeneintrag mit Namen (<c>AutomationProperties.Name</c>, „10:30 Brigitte Müller,
/// Hot-Stone-Massage, Gebucht“) und dem Zustand „ausgewählt“ (<see cref="ISelectionItemProvider"/>). Aussehen und Bedienung bleiben die des <see cref="Border"/>
/// mit der Klasse <c>appt</c>; ausgewählt wird weiter mit Maus und Pfeiltasten. Wählen die Pfeiltasten einen anderen Termin, bekommt dessen Karte den Fokus,
/// damit ein Screenreader sie ansagt (Slice 18); ein Tab-Stopp und ein eigener Fokusrahmen sind sie nicht, die Stelle zeigt der Auswahlring.
/// </summary>
public sealed class AppointmentCard : Border
{
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<AppointmentCard, bool>(nameof(IsSelected));

    static AppointmentCard() =>
        IsSelectedProperty.Changed.AddClassHandler<AppointmentCard>((card, e) =>
            (ControlAutomationPeer.FromElement(card) as AppointmentCardPeer)?.SelectionChanged(e.OldValue is true, e.NewValue is true));

    public AppointmentCard()
    {
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Border);

    protected override AutomationPeer OnCreateAutomationPeer() => new AppointmentCardPeer(this);

    private sealed class AppointmentCardPeer(AppointmentCard owner) : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        // Auswählen per Screenreader-Befehl gibt es nicht; die Auswahl gehört der Kalenderansicht (Maus, Pfeiltasten).
        public void AddToSelection()
        {
        }

        public void RemoveFromSelection()
        {
        }

        public void Select()
        {
        }

        public void SelectionChanged(bool wasSelected, bool isSelected) =>
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, wasSelected, isSelected);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
