using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Durchsicht der Seiten „Verlauf“, „Posteingang“ und „Geräte“ (Befunde 46 bis 55): Aktionen sehen wie Knöpfe aus, Symbole und Marken tragen ein Wort, nichts liegt übereinander.</summary>
public sealed partial class ViewTests
{
    private static void AssertSeparate(Visual root, params Control[] parts)
    {
        for (var i = 0; i < parts.Length; i++)
        {
            for (var j = i + 1; j < parts.Length; j++)
            {
                var (a, b) = (BoundsIn(parts[i], root), BoundsIn(parts[j], root));
                Assert.False(a.Intersects(b), $"{parts[i].Name ?? parts[i].GetType().Name} {a} überlappt {parts[j].Name ?? parts[j].GetType().Name} {b}");
            }
        }
    }

    private static InboxView VisibleInbox(Window window) => window.GetVisualDescendants().OfType<InboxView>().Single(v => v.IsEffectivelyVisible);

    /// <summary>Befund 52a: Absender, Uhrzeit und Quelle einer Listenzeile überlappen nie.</summary>
    private static void AssertInboxRowsSeparate(Window window)
    {
        var list = VisibleInbox(window).GetVisualDescendants().OfType<ListBox>().First(l => l.IsEffectivelyVisible);
        foreach (var item in list.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsEffectivelyVisible))
        {
            var parts = new List<Control> { Named<TextBlock>(item, "RowFrom"), Named<TextBlock>(item, "RowTime"), Named<TextBlock>(item, "RowSubject") };
            var channel = Named<Border>(item, "RowChannel");
            if (channel.IsEffectivelyVisible)
            {
                parts.Add(channel);
            }

            AssertSeparate(item, [.. parts]);
        }
    }

    /// <summary>Befunde 49b/50c, 49c, 51b: Aktionen als Knopf, Schalter mit Namen und Hinweis, Haken nennt sein Ziel.</summary>
    private static void AssertInboxControlsExplained(Window window)
    {
        var view = VisibleInbox(window);
        Assert.DoesNotContain("ghost", Named<Button>(view, "DismissButton").Classes);
        var toggle = view.GetVisualDescendants().OfType<ToggleSwitch>().Single();
        Assert.Equal("Nur Nachrichten mit Status „Prüfen“ zeigen", AutomationProperties.GetName(toggle));
        Assert.NotNull(ToolTip.GetTip(toggle));
        var live = view.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("pill") && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text is "Live" or "Nicht live"));
        Assert.Contains("Live-Verbindung", ToolTip.GetTip(live)?.ToString() ?? "", StringComparison.Ordinal);
    }

    /// <summary>Befunde 54a bis 54c und 55a/55b: Aktionen der Geräte sind Knöpfe, Pflichtfelder tragen den Stern, ZVT ist erklärt, die Anleitung steht nicht unter der Hilfetextgröße.</summary>
    private static void AssertDevicesExplained(Window window)
    {
        var remove = Named<Button>(window, "RemoveTerminalButton");
        Assert.Contains("danger", remove.Classes);
        Assert.DoesNotContain("ghost", remove.Classes);
        Assert.False(remove.IsEffectivelyEnabled);
        Assert.StartsWith("Verfügbar", ToolTip.GetTip(remove)?.ToString(), StringComparison.Ordinal);

        // „Terminal anlegen“ ist aktiv und prüft beim Klick: ohne Adresse steht der Fehler am Feld (T-086).
        var add = Named<Button>(window, "AddTerminal");
        Assert.True(add.IsEffectivelyEnabled);
        Assert.Null(ToolTip.GetTip(add));
        var required = window.GetVisualDescendants().OfType<FormField>().Where(f => f.IsRequired).Select(f => f.Label).ToList();
        Assert.Equal(["Name", "Standort", "Adresse"], required);

        // Technikwörter wie ZVT stehen nur unter „Technische Angaben“ (MessageBarTests); der Hinweis erklärt das Protokoll in Alltagssprache.
        Assert.Contains("Kartenterminal", ToolTip.GetTip(Named<ComboBox>(window, "NewTerminalProtocol"))?.ToString(), StringComparison.Ordinal);
        Assert.Contains(window.GetVisualDescendants().OfType<HelpButton>(), h => h.Section == "neues-terminal");

        var how = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("Ein Bildschirm im Pausenraum", StringComparison.Ordinal) == true);
        Assert.True(how.FontSize >= 12, "Anleitung zum Koppeln steht unter der Hilfetextgröße 12");
    }
}
