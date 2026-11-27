using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>T-108 (Auftraggeber, 26.11.2026, Punkt 6): Im Panel „Neuer Eintrag“ steht in keinem Reiter eine leere Pille.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Leere_Pille_Neuer_Eintrag_zeigt_in_keinem_Reiter_eine_Pille_ohne_Text() => Render("hell", "t108-neuer-eintrag-sauna", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.StartBookingAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        await Pump(Settling);

        foreach (var kind in calendar.NewEntry!.Choices.Select(c => c.Kind).ToList())
        {
            Assert.True(calendar.NewEntry.Select(kind));
            await Pump(Settling);
            var empty = EmptyVisiblePills(SidePanelOf(window));
            Assert.False(Named<Border>(window, "AppointmentChips").IsEffectivelyVisible, $"Reiter {kind}: Die Chip-Zeile steht ohne Termin da");
            Assert.True(empty.Count == 0, $"Reiter {kind}: {empty.Count} sichtbare Pille(n) ohne Text: " + string.Join(" | ", empty.Select(Describe)));
        }

        Assert.True(calendar.NewEntry.Select(NewEntryKind.Area));
        await Pump(Settling);
        return calendar;
    });

    private static List<Border> EmptyVisiblePills(Control panel) =>
        [.. panel.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Classes.Contains("pill") && b.IsEffectivelyVisible)
            .Where(b => !b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text)))];

    private static string Describe(Border pill) =>
        $"[{string.Join(' ', pill.Classes)}] DataContext={pill.DataContext?.GetType().Name ?? "null"} Eltern={string.Join('>', pill.GetVisualAncestors().OfType<Control>().Select(a => a.Name).Where(n => n is not null).Take(4))} Vorfahren={string.Join('>', pill.GetVisualAncestors().Take(6).Select(a => a.GetType().Name))}";
}
