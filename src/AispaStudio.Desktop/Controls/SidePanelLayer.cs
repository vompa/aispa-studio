using Avalonia;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Immer nur eine Leiste rechts (Design-Brief 5.12, Punkt 1): Solange die Hilfe in der Leiste „KI und Hilfe“ steht, blendet jede offene <see cref="SidePanel"/>
/// einer Maske (z. B. <see cref="HistoryPanel"/>) sich aus und bleibt dabei offen; schließt die Hilfe, ist sie wieder da. Öffnet man eine solche Leiste,
/// während die Hilfe steht, meldet sie das (<see cref="PanelOpened"/>), und die Hilfe macht ihr Platz. Der Rahmen der App setzt die Ebene als
/// geerbte Eigenschaft (<see cref="CurrentProperty"/>), die Leisten der Masken finden ihn darüber.
/// </summary>
public sealed class SidePanelLayer
{
    /// <summary>Die Ebene des Rahmens; erbt sich auf alle Masken darunter.</summary>
    public static readonly AttachedProperty<SidePanelLayer?> CurrentProperty =
        AvaloniaProperty.RegisterAttached<SidePanelLayer, Control, SidePanelLayer?>("Current", inherits: true);

    public static SidePanelLayer? GetCurrent(Control control) => control.GetValue(CurrentProperty);

    public static void SetCurrent(Control control, SidePanelLayer? value) => control.SetValue(CurrentProperty, value);

    /// <summary>Die Hilfe liegt über den Leisten der Masken; offene Leisten sind ausgeblendet.</summary>
    public bool IsCovered
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                CoveredChanged?.Invoke();
            }
        }
    }

    public event Action? CoveredChanged;

    /// <summary>Eine Leiste einer Maske ging auf, während die Hilfe stand.</summary>
    public event Action? PanelOpened;

    /// <summary>Von <see cref="SidePanel"/>: sie geht gerade auf.</summary>
    public void ReportOpened()
    {
        if (IsCovered)
        {
            PanelOpened?.Invoke();
        }
    }
}
