using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Tastatur der Reiterleisten (Design-Brief 5.4 und 5.12, Fluent 2 „Tab list“, WinUI „TabView“): die Schiene ist <b>ein</b> Tab-Stopp (der gewählte Reiter, dafür sorgt der Stil
/// mit <c>IsTabStop</c> an allen nicht gewählten), ←/→ bewegen den Fokus ohne zu wählen und laufen nicht im Kreis, Pos1 und Ende gehen zum ersten und letzten.
/// Leertaste und Enter wählen, weil der Fokus auf einem Knopf liegt. Strg+Tab, Strg+W und Strg+Ziffer stehen im Kalender (<c>CalendarView</c>).
/// Betroffen sind die segmentierte Schiene (<c>Border.segmented</c>), die Reiter unten in der Seitenleiste (<c>Border.panelrail</c>) und der Balken der Verwaltung (<c>Border.adminbar</c>).
/// </summary>
public static class TabStripBehavior
{
    private static bool Registered;

    /// <summary>Trägt das Verhalten ein; mehrfaches Aufrufen schadet nicht.</summary>
    public static void Register()
    {
        if (Registered)
        {
            return;
        }

        Registered = true;
        InputElement.KeyDownEvent.AddClassHandler<Border>(OnKeyDown, RoutingStrategies.Bubble);
    }

    /// <summary>Die Knöpfe der Schiene in der Reihenfolge der Oberfläche; was nicht zu sehen oder nicht bedienbar ist, fehlt.</summary>
    public static IReadOnlyList<Button> TabsOf(Border strip) =>
        [.. strip.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled && b.Focusable)];

    private static void OnKeyDown(Border strip, KeyEventArgs e)
    {
        if (!strip.Classes.Contains("segmented") && !strip.Classes.Contains("panelrail") && !strip.Classes.Contains("adminbar"))
        {
            return;
        }

        if (e.Key is not (Key.Left or Key.Right or Key.Home or Key.End) || e.KeyModifiers != KeyModifiers.None || e.Handled)
        {
            return;
        }

        var tabs = TabsOf(strip);
        if (TopLevel.GetTopLevel(strip)?.FocusManager?.GetFocusedElement() is not Button current || !tabs.Contains(current))
        {
            return;
        }

        var index = tabs.ToList().IndexOf(current);
        var target = e.Key switch
        {
            Key.Left => index - 1,
            Key.Right => index + 1,
            Key.Home => 0,
            _ => tabs.Count - 1,
        };

        // Am Rand bleibt der Fokus stehen (kein Umlauf); die Taste gehört der Schiene, damit sie nicht den Kalender (Tag wechseln) bewegt.
        e.Handled = true;
        if (target >= 0 && target < tabs.Count && target != index)
        {
            tabs[target].Focus(NavigationMethod.Directional);
        }
    }
}
