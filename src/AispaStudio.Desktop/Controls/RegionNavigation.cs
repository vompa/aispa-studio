using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Bereichswechsel mit F6 und Umschalt+F6 (Design-Brief 9, Fluent 2 F27, WinUI „Keyboard accessibility“): Windows bietet das für eigene Rahmen nicht, deshalb markieren die Ansichten
/// ihre großen Bereiche (Navigation, Kopfzeile, Kalenderfläche, Seitenleiste, Reiter unten, KI und Hilfe) mit <see cref="IsRegionProperty"/> und einem <c>AutomationProperties.Name</c>.
/// F6 geht zum nächsten sichtbaren Bereich in der Reihenfolge der Oberfläche (von links nach rechts, von oben nach unten), am Ende von vorn; Umschalt+F6 rückwärts.
/// Der Fokus landet im Bereich auf dem ersten Bedienelement (die Navigation auf der gewählten Seite) oder auf <see cref="FocusTargetProperty"/>.
/// </summary>
public static class RegionNavigation
{
    /// <summary>Kennzeichnet ein Element als Bereich, den F6 anspringt. Fehlt es im sichtbaren Baum oder ist es ausgeblendet, gibt es den Bereich nicht.</summary>
    public static readonly AttachedProperty<bool> IsRegionProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, bool>("IsRegion");

    /// <summary>Das Element, das den Fokus bekommt, wenn der Bereich kein eigenes Bedienelement hat (z. B. die Kalenderfläche: die Ansicht selbst nimmt die Tasten).</summary>
    public static readonly AttachedProperty<Control?> FocusTargetProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, Control?>("FocusTarget");

    public static bool GetIsRegion(Control control) => control.GetValue(IsRegionProperty);

    public static void SetIsRegion(Control control, bool value) => control.SetValue(IsRegionProperty, value);

    public static Control? GetFocusTarget(Control control) => control.GetValue(FocusTargetProperty);

    public static void SetFocusTarget(Control control, Control? value) => control.SetValue(FocusTargetProperty, value);

    /// <summary>
    /// Die Bereiche unter <paramref name="root"/> in der Reihenfolge der Oberfläche, nur sichtbare. <paramref name="fallback"/> (die Seite selbst) zählt als Bereich,
    /// solange sie keinen eigenen enthält: Seiten ohne Unterteilung sind dann ein Bereich.
    /// </summary>
    public static IReadOnlyList<Control> RegionsOf(Visual root, Control? fallback = null)
    {
        var visible = root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
        var regions = visible.Where(GetIsRegion).ToList();
        if (fallback is not null && fallback.IsEffectivelyVisible && !regions.Any(r => fallback.IsVisualAncestorOf(r)))
        {
            regions.Add(fallback);
            regions = [.. visible.Where(c => regions.Contains(c))];
        }

        return regions;
    }

    /// <summary>Der Bereich, in dem der Fokus gerade liegt; bei verschachtelten der innerste (der später in der Reihenfolge der Oberfläche). Ohne Fokus oder außerhalb aller: -1.</summary>
    public static int CurrentIndex(IReadOnlyList<Control> regions, Visual? focused)
    {
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var region = regions[i];
            if (focused is not null && (ReferenceEquals(region, focused) || region.IsVisualAncestorOf(focused) || ReferenceEquals(GetFocusTarget(region), focused)))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// F6 oder Umschalt+F6: zum nächsten bzw. vorigen Bereich. Gibt wahr zurück, wenn der Fokus gesetzt wurde. Ohne Fokus in einem Bereich beginnt F6 beim ersten
    /// und Umschalt+F6 beim letzten.
    /// </summary>
    public static bool Move(Visual root, Control? fallback, bool backwards)
    {
        var regions = RegionsOf(root, fallback);
        if (regions.Count == 0)
        {
            return false;
        }

        var focused = TopLevel.GetTopLevel(root)?.FocusManager?.GetFocusedElement() as Visual;
        var current = CurrentIndex(regions, focused);
        var next = current < 0
            ? (backwards ? regions.Count - 1 : 0)
            : ((current + (backwards ? -1 : 1)) % regions.Count + regions.Count) % regions.Count;

        // Ein Bereich ohne Bedienelement wird übersprungen, höchstens einmal rundherum.
        for (var tried = 0; tried < regions.Count; tried++)
        {
            if (Target(regions[next]) is { } target && target.Focus(NavigationMethod.Tab))
            {
                return true;
            }

            next = ((next + (backwards ? -1 : 1)) % regions.Count + regions.Count) % regions.Count;
        }

        return false;
    }

    /// <summary>Das Element, das beim Betreten des Bereichs den Fokus bekommt: das ausdrückliche Ziel, die gewählte Zeile einer Liste, sonst das erste Bedienelement.</summary>
    public static InputElement? Target(Control region)
    {
        if (GetFocusTarget(region) is { } explicitTarget && explicitTarget.IsEffectivelyVisible && explicitTarget.IsEffectivelyEnabled)
        {
            return explicitTarget;
        }

        var selectedRow = region.GetVisualDescendants().OfType<SelectingItemsControl>().FirstOrDefault(l => l.IsEffectivelyVisible && l.SelectedIndex >= 0);
        if (selectedRow?.ContainerFromIndex(selectedRow.SelectedIndex) is InputElement { Focusable: true } row)
        {
            return row;
        }

        var inside = region.GetVisualDescendants().OfType<InputElement>().Where(e => e.IsEffectivelyVisible && e.IsEffectivelyEnabled && e.Focusable).ToList();
        return inside.FirstOrDefault(e => e.IsTabStop) ?? inside.FirstOrDefault();
    }
}
