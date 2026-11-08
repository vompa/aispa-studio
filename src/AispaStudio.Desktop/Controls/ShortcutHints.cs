using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Tastenkürzel sichtbar und lesbar (Design-Brief 5.22 und 9, Fluent 2 F29, WinUI „Keyboard accessibility“): nennt ein Tooltip das Kürzel in Klammern
/// („Vortag (←)“, „Rückgängig (Strg+Z)“), steht es zugleich als <see cref="AutomationProperties.AcceleratorKeyProperty"/> am Steuerelement, damit der Screenreader es nennt.
/// So gilt das auch für Tooltips, die erst zur Laufzeit entstehen; ein ausdrücklich gesetzter Wert bleibt unberührt.
/// </summary>
public static partial class ShortcutHints
{
    private static readonly AttachedProperty<bool> DerivedProperty = AvaloniaProperty.RegisterAttached<Control, Control, bool>("ShortcutDerived");

    private static bool Registered;

    /// <summary>Trägt das Verhalten ein; mehrfaches Aufrufen schadet nicht.</summary>
    public static void Register()
    {
        if (Registered)
        {
            return;
        }

        Registered = true;
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((control, e) => Apply(control, e.NewValue as string));
    }

    /// <summary>Das erste Tastenkürzel in Klammern im Text eines Tooltips („Heute (Pos1)“ ergibt „Pos1“), sonst null.</summary>
    public static string? Parse(string? tip)
    {
        if (string.IsNullOrEmpty(tip))
        {
            return null;
        }

        foreach (Match group in Parenthesis().Matches(tip))
        {
            var text = group.Groups["text"].Value.Trim();
            if (IsShortcut(text))
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>Ob der Text ein Tastenkürzel nennt: Umschalttasten mit Taste („Strg+Z“, „Strg 0“), Sondertasten, ein einzelnes Zeichen, „Taste O“, oder mehrere davon mit „oder“.</summary>
    public static bool IsShortcut(string text) => text.Split([" oder ", ", "], StringSplitOptions.TrimEntries).All(part => part.Length > 0 && SingleKey().IsMatch(part));

    private static void Apply(Control control, string? tip)
    {
        var shortcut = Parse(tip);
        if (shortcut is not null)
        {
            if (!control.IsSet(AutomationProperties.AcceleratorKeyProperty) || control.GetValue(DerivedProperty))
            {
                AutomationProperties.SetAcceleratorKey(control, shortcut);
                control.SetValue(DerivedProperty, true);
            }
        }
        else if (control.GetValue(DerivedProperty))
        {
            control.ClearValue(AutomationProperties.AcceleratorKeyProperty);
            control.SetValue(DerivedProperty, false);
        }
    }

    [GeneratedRegex(@"\((?<text>[^()]{1,48})\)")]
    private static partial Regex Parenthesis();

    // Entweder eine Sondertaste oder ein einzelnes Zeichen („Taste O“, „G“, „.“) oder Umschalttasten mit einer Taste („Strg+Z“, „Strg 0“, „Alt+3“).
    // Eine bloße Ziffer oder ein Wort in Kleinbuchstaben („(2)“, „(optional)“) ist keines.
    [GeneratedRegex(@"^(?:(?:Taste\s+)?(?:Esc|Enter|Entf|Pos1|Ende|Leertaste|Tab|F\d{1,2}|[A-ZÄÖÜ]|[←→↑↓]|[.,?])|(?:(?:Strg|Alt|Umschalt)\s?\+?\s?)+(?:Esc|Enter|Entf|Pos1|Ende|Leertaste|Tab|Mausrad|F\d{1,2}|[A-ZÄÖÜ0-9]|[←→↑↓]|[.,?+−/]))$")]
    private static partial Regex SingleKey();
}
