using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Controls;

/// <summary>
/// Lesbare Namen für den Screenreader (Design-Brief 9, Fluent 2 F28): Avalonia nennt bei einem Knopf mit Symbol und Text (<c>StackPanel</c> als Inhalt) den Typnamen
/// („Avalonia.Controls.StackPanel“). Diese Regel ergänzt, was fehlt, und lässt ausdrücklich gesetzte Namen unberührt: der Name eines Knopfs ist der erste Text in seinem
/// Inhalt (folgt Änderungen), sonst der Text seines Tooltips ohne das Kürzel in Klammern. Eingabefelder bekommen keinen Namen aus dem Platzhalter (R01 Slice 11, W64):
/// der Platzhalter ist ein Beispiel, kein Name; ein Feld ohne Beschriftung soll im Namens-Scan auffallen. Das Textfeld in der Vorlage einer <c>AutoCompleteBox</c>
/// bekommt Namen und Beschriftung seiner Auswahl, denn den Fokus hat das Textfeld, die Beschriftung von <see cref="FormField"/> hängt an der Auswahl.
/// </summary>
public static class AccessibleNames
{
    private static bool Registered;

    /// <summary>Trägt die Regel ein; mehrfaches Aufrufen schadet nicht.</summary>
    public static void Register()
    {
        if (Registered)
        {
            return;
        }

        Registered = true;
        Control.LoadedEvent.AddClassHandler<Button>((button, _) => NameFromContent(button));
        Control.LoadedEvent.AddClassHandler<ToggleButton>((button, _) => NameFromContent(button));
        Control.LoadedEvent.AddClassHandler<TextBox>((box, _) => NameFromAutoCompleteBox(box));
    }

    private static bool HasName(Control control) =>
        control.IsSet(AutomationProperties.NameProperty) || AutomationProperties.GetLabeledBy(control) is not null;

    private static void NameFromContent(ContentControl button)
    {
        if (HasName(button) || button.Content is string or null)
        {
            return;
        }

        var text = (button.Content as Visual)?.GetSelfAndVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text));
        if (text is not null)
        {
            // Der Name folgt dem Text, damit gebundene Beschriftungen („3 Termine“, „Aus dem Team nehmen“) auch nach einer Änderung stimmen.
            button.Bind(AutomationProperties.NameProperty, text.GetObservable(TextBlock.TextProperty));
            return;
        }

        if (ToolTip.GetTip(button) is string tip && !string.IsNullOrWhiteSpace(tip))
        {
            AutomationProperties.SetName(button, WithoutShortcut(tip));
        }
    }

    private static void NameFromAutoCompleteBox(TextBox box)
    {
        if (HasName(box) || box.TemplatedParent is not AutoCompleteBox owner)
        {
            return;
        }

        box.Bind(AutomationProperties.NameProperty, owner.GetObservable(AutomationProperties.NameProperty));
        box.Bind(AutomationProperties.LabeledByProperty, owner.GetObservable(AutomationProperties.LabeledByProperty));
    }

    /// <summary>Name einer Listenzeile aus ihren sichtbaren Teilen in Lesereihenfolge, leere Teile entfallen („Do. 22.10., 09:00–10:20, Lena, Lotus“).</summary>
    public static string Join(params string?[] parts) => string.Join(", ", parts.Where(p => !string.IsNullOrEmpty(p)));

    /// <summary>Der Tooltip ohne das Kürzel in Klammern („Vortag (←)“ ergibt „Vortag“); das Kürzel liest der Screenreader getrennt (<see cref="ShortcutHints"/>).</summary>
    public static string WithoutShortcut(string tip)
    {
        var shortcut = ShortcutHints.Parse(tip);
        return shortcut is null ? tip : tip.Replace($"({shortcut})", "", StringComparison.Ordinal).Replace("  ", " ", StringComparison.Ordinal).Trim();
    }
}
