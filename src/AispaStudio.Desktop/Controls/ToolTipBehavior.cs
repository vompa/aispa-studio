using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Verhalten aller Tooltips (Design-Brief 5.22, Fluent 2 „Tooltip“): erscheint nach 250 ms bei Hover und bei Tastaturfokus, Abstand 4 zum Ziel,
/// Esc oder Strg blendet ihn aus. Die Werte sind Standardwerte der angehängten Eigenschaften von <see cref="ToolTip"/>, damit nicht jedes Steuerelement sie setzen muss;
/// wer andere Werte braucht (Hover-Karten im Kalender), setzt sie am Steuerelement.
/// Was nur im Tooltip steht (Bedeutung der Sterne, Adresszeile, Kennzahlen), erreicht die Tastatur über <see cref="ReachableProperty"/>: das Element wird ein
/// Tab-Stopp mit Fokusrahmen (Klasse <see cref="ReachableClass"/> in Styles.axaml), sein Tooltip erscheint beim Fokus wie bei jedem Knopf, und der Screenreader
/// liest ihn als Hilfetext. Mehrere nebeneinander (die Kennzahlen eines Kopfes) fasst <see cref="ReachableGroupProperty"/> zu einem Stopp mit Pfeiltasten zusammen,
/// wie eine Optionsgruppe (W-Muster 12, Fluent 2 „Accessibility“: Gruppen sind ein Tab-Stopp). Beides wird einmal im XAML gesetzt und nicht zurückgenommen.
/// </summary>
public static class ToolTipBehavior
{
    /// <summary>Klasse der per Tab erreichbaren Elemente mit Tooltip, für den Fokusrahmen in Styles.axaml.</summary>
    public const string ReachableClass = "tipstop";

    /// <summary>Verzögerung bis zum Erscheinen in Millisekunden.</summary>
    public const int ShowDelayMilliseconds = 250;

    /// <summary>Abstand zwischen Ziel und Tooltip in Pixeln (kein Pfeil).</summary>
    public const double Gap = 4;

    /// <summary>Klasse des Tooltips mit reichem Inhalt (Karte statt Text), siehe Styles.axaml.</summary>
    public const string RichClass = "hovercard";

    /// <summary>Das Element mit Tooltip ist per Tab erreichbar; der Tooltip-Text ist zugleich sein Hilfetext für den Screenreader (W-Muster 12).</summary>
    public static readonly AttachedProperty<bool> ReachableProperty = AvaloniaProperty.RegisterAttached<Control, Control, bool>("Reachable");

    /// <summary>Die erreichbaren Kinder des Panels sind <b>ein</b> Tab-Stopp; Pfeiltasten wechseln zwischen ihnen, ohne Umlauf.</summary>
    public static readonly AttachedProperty<bool> ReachableGroupProperty = AvaloniaProperty.RegisterAttached<Control, Panel, bool>("ReachableGroup");

    private static bool Registered;
    private static Control? FocusTip;

    static ToolTipBehavior()
    {
        ReachableProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            var reachable = e.NewValue is true;
            control.Focusable = reachable;
            control.Classes.Set(ReachableClass, reachable);
            if (reachable)
            {
                control.AddHandler(InputElement.KeyDownEvent, OnReachableKeyDown);
            }

            SyncHelpText(control);
            // Aus XAML hängt ein Kind schon in der Gruppe, bevor es seine Eigenschaften bekommt; die Gruppe muss es dann noch als Mitglied einordnen.
            if (control.Parent is Panel group && GetReachableGroup(group))
            {
                UpdateGroupTabStops(group);
            }
        });
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((control, _) => SyncHelpText(control));
        ReachableGroupProperty.Changed.AddClassHandler<Panel>((panel, e) =>
        {
            if (e.NewValue is true)
            {
                panel.AddHandler(InputElement.KeyDownEvent, OnGroupKeyDown, RoutingStrategies.Tunnel);
                panel.Children.CollectionChanged += (_, _) => UpdateGroupTabStops(panel);
                UpdateGroupTabStops(panel);
            }
        });
    }

    /// <summary>Der Tooltip-Text eines erreichbaren Elements ist sein Hilfetext; ohne Tooltip oder mit reichem Inhalt (Karte) bleibt es ohne.</summary>
    private static void SyncHelpText(Control control)
    {
        if (!GetReachable(control))
        {
            return;
        }

        if (ToolTip.GetTip(control) is string text)
        {
            AutomationProperties.SetHelpText(control, text);
        }
        else
        {
            control.ClearValue(AutomationProperties.HelpTextProperty);
        }
    }

    /// <summary>Ein erreichbares Element zeigt nur; Enter und Leertaste dürfen keinen Knopf auslösen, in dem es liegt (die Kennzahlen stehen im Kopf eines Akkordeons).</summary>
    private static void OnReachableKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && e.KeyModifiers == KeyModifiers.None && ReferenceEquals(sender, e.Source))
        {
            e.Handled = true;
        }
    }

    /// <summary>Die erreichbaren Kinder der Gruppe in der Reihenfolge der Oberfläche.</summary>
    private static List<Control> MembersOf(Panel group) => [.. group.Children.OfType<Control>().Where(GetReachable)];

    /// <summary>Nur das erste Mitglied ist ein Tab-Stopp; die Pfeiltasten erreichen die anderen, und der Stopp wandert mit (<see cref="MoveGroupTabStop"/>).</summary>
    private static void UpdateGroupTabStops(Panel group) => MoveGroupTabStop(group, MembersOf(group).FirstOrDefault());

    /// <summary>Das zuletzt per Pfeil erreichte Mitglied ist der Tab-Stopp der Gruppe, damit Tab und Umschalt+Tab von dort aus der Gruppe hinausführen.</summary>
    private static void MoveGroupTabStop(Panel group, Control? stop)
    {
        foreach (var member in MembersOf(group))
        {
            member.IsTabStop = ReferenceEquals(member, stop);
        }
    }

    /// <summary>Pfeiltasten wechseln innerhalb der Gruppe; am Ende bleibt der Fokus stehen, und die Taste gehört der Gruppe (kein Tageswechsel im Kalender).</summary>
    private static void OnGroupKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Panel group || e.Source is not Visual source)
        {
            return;
        }

        var step = e.Key switch
        {
            Key.Down or Key.Right => 1,
            Key.Up or Key.Left => -1,
            _ => 0,
        };
        var members = MembersOf(group).Where(m => m.IsEffectivelyVisible).ToList();
        var index = members.FindIndex(m => ReferenceEquals(m, source) || m.IsVisualAncestorOf(source));
        if (step == 0 || index < 0 || e.KeyModifiers.HasFlag(KeyModifiers.Alt) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        e.Handled = true;
        var next = index + step;
        if (next >= 0 && next < members.Count)
        {
            MoveGroupTabStop(group, members[next]);
            members[next].Focus(NavigationMethod.Directional);
        }
    }

    /// <summary>Trägt Standardwerte und Tastaturverhalten ein; mehrfaches Aufrufen schadet nicht.</summary>
    public static void Register()
    {
        if (Registered)
        {
            return;
        }

        Registered = true;
        // Die angehängten Eigenschaften tragen schon Standardwerte am Steuerelement (400 ms, am Zeiger); sie lassen sich nicht überschreiben. Deshalb setzt die Änderung
        // von ToolTip.Tip die neuen Werte mit Stil-Rang: ein am Steuerelement gesetzter Wert (Hover-Karten im Kalender) gewinnt.
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((control, _) => ApplyDefaults(control));
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        // Reicher Inhalt (Hover-Karte eines Termins, Hinweis am Spaltenkopf) bekommt die helle, breitere Karte; Text bleibt der schlichte Tooltip.
        ContentControl.ContentProperty.Changed.AddClassHandler<ToolTip>((tip, e) => tip.Classes.Set(RichClass, e.NewValue is not (null or string)));
        // Verschwindet das Ziel (Klick auf den Chip „Filter aufheben“ blendet ihn aus), bliebe der Tooltip sonst stehen.
        Visual.IsVisibleProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (e.NewValue is false && ToolTip.GetIsOpen(control))
            {
                ToolTip.SetIsOpen(control, false);
            }
        });
        InputElement.GotFocusEvent.AddClassHandler<TopLevel>(OnGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        InputElement.LostFocusEvent.AddClassHandler<TopLevel>(OnLostFocus, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public static bool GetReachable(Control control) => control.GetValue(ReachableProperty);

    public static void SetReachable(Control control, bool value) => control.SetValue(ReachableProperty, value);

    public static bool GetReachableGroup(Panel panel) => panel.GetValue(ReachableGroupProperty);

    public static void SetReachableGroup(Panel panel, bool value) => panel.SetValue(ReachableGroupProperty, value);

    /// <summary>Verzögerung, Abstand und Platz gelten für jedes Steuerelement mit Tooltip, das sie nicht selbst festlegt.</summary>
    private static void ApplyDefaults(Control control)
    {
        control.SetValue(ToolTip.ShowDelayProperty, ShowDelayMilliseconds, BindingPriority.Style);
        // Wird kurz nach einem Tooltip ein weiterer gebraucht, erscheint er sofort statt nach der Verzögerung.
        control.SetValue(ToolTip.BetweenShowDelayProperty, ShowDelayMilliseconds, BindingPriority.Style);
        control.SetValue(ToolTip.PlacementProperty, PlacementMode.Bottom, BindingPriority.Style);
        control.SetValue(ToolTip.VerticalOffsetProperty, Gap, BindingPriority.Style);
    }

    /// <summary>Esc oder Strg blendet jeden offenen Tooltip aus. Die Taste bleibt unberührt, damit sie ihre eigentliche Wirkung behält.</summary>
    private static void OnKeyDown(TopLevel top, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.LeftCtrl or Key.RightCtrl)
        {
            HideAll(top);
        }
    }

    /// <summary>Schließt alle Tooltips unterhalb von <paramref name="root"/>.</summary>
    public static void HideAll(Visual root)
    {
        FocusTip = null;
        foreach (var control in root.GetVisualDescendants().OfType<Control>().Where(ToolTip.GetIsOpen).ToList())
        {
            ToolTip.SetIsOpen(control, false);
        }
    }

    /// <summary>Tastaturfokus (Tab, Pfeile) auf ein Steuerelement mit Tooltip: nach der Verzögerung erscheint er wie beim Überfahren mit der Maus.</summary>
    private static void OnGotFocus(TopLevel top, FocusChangedEventArgs e)
    {
        if (e.NavigationMethod is not (NavigationMethod.Tab or NavigationMethod.Directional) || e.Source is not Control control
            || ToolTip.GetTip(control) is null || !ToolTip.GetServiceEnabled(control))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => ShowAfterDelay(control), DispatcherPriority.Background);
    }

    private static void ShowAfterDelay(Control control) =>
        DispatcherTimer.RunOnce(() =>
        {
            if (control.IsFocused && control.IsEffectivelyVisible && !ToolTip.GetIsOpen(control))
            {
                FocusTip = control;
                ToolTip.SetIsOpen(control, true);
            }
        }, TimeSpan.FromMilliseconds(control.GetValue(ToolTip.ShowDelayProperty)));

    private static void OnLostFocus(TopLevel top, RoutedEventArgs e)
    {
        if (FocusTip is { } control && ReferenceEquals(e.Source, control))
        {
            FocusTip = null;
            ToolTip.SetIsOpen(control, false);
        }
    }
}
