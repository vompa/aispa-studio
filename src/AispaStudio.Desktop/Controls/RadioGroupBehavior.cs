using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Optionsgruppen nach Design-Brief 5.2 (Fluent 2 „Radio group“, WinUI): eine Gruppe ist <b>ein</b> Tab-Stopp, und zwar die gewählte Option, ohne Auswahl die erste.
/// Pfeile bewegen den Fokus und wählen, Strg+Pfeil bewegt nur den Fokus (die Leertaste wählt), am Ende der Gruppe gibt es keinen Umlauf.
/// Eine Gruppe sind die Optionsfelder mit gleichem <see cref="RadioButton.GroupName"/> unter demselben Elternelement.
/// </summary>
public static class RadioGroupBehavior
{
    private static bool Registered;

    /// <summary>Trägt das Verhalten für alle Optionsfelder ein; mehrfaches Aufrufen schadet nicht.</summary>
    public static void Register()
    {
        if (Registered)
        {
            return;
        }

        Registered = true;
        InputElement.KeyDownEvent.AddClassHandler<RadioButton>(OnKeyDown, RoutingStrategies.Tunnel);
        ToggleButton.IsCheckedProperty.Changed.AddClassHandler<RadioButton>((radio, _) => UpdateTabStops(radio));
        Control.LoadedEvent.AddClassHandler<RadioButton>((radio, _) => UpdateTabStops(radio));
    }

    /// <summary>Die Optionsfelder der Gruppe in der Reihenfolge der Oberfläche.</summary>
    public static IReadOnlyList<RadioButton> GroupOf(RadioButton radio) =>
        radio.GetLogicalParent()?.GetLogicalChildren().OfType<RadioButton>().Where(r => r.GroupName == radio.GroupName).ToList() ?? [radio];

    /// <summary>Nur die gewählte (sonst die erste) Option ist ein Tab-Stopp.</summary>
    private static void UpdateTabStops(RadioButton radio)
    {
        var group = GroupOf(radio);
        var stop = group.FirstOrDefault(r => r.IsChecked == true) ?? group.FirstOrDefault(r => r.IsEnabled) ?? group[0];
        foreach (var member in group)
        {
            member.IsTabStop = ReferenceEquals(member, stop);
        }
    }

    private static void OnKeyDown(RadioButton radio, KeyEventArgs e)
    {
        var step = e.Key switch
        {
            Key.Down or Key.Right => 1,
            Key.Up or Key.Left => -1,
            _ => 0,
        };
        if (step == 0 || e.KeyModifiers.HasFlag(KeyModifiers.Alt) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        var group = GroupOf(radio);
        var index = group.ToList().IndexOf(radio);
        var next = index;
        do
        {
            next += step;
        }
        while (next >= 0 && next < group.Count && !group[next].IsEnabled);

        // Am Ende bleibt der Fokus stehen (kein Umlauf); die Taste gehört der Gruppe, damit er nicht aus ihr herausspringt.
        e.Handled = true;
        if (next < 0 || next >= group.Count)
        {
            return;
        }

        var target = group[next];
        target.Focus(NavigationMethod.Directional);
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            target.IsChecked = true;
        }
    }
}
