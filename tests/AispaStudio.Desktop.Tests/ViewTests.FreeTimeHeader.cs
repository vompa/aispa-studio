using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Slice 84 (Ä157): „Sprache des Gastes“ ausgeblendet. „Freie Zeit suchen“ steht seit dem 29.10.2026 (Auftraggeber) nicht mehr als eigener Hauptknopf neben dem Datum,
/// sondern als zweite Schaltfläche (Kalender mit Lupe) direkt rechts neben „+ Termin“.
/// </summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Kalenderkopf_zeigt_Freie_Zeit_suchen_als_zweite_Schaltflaeche_neben_Termin() => Render("hell", "kalender-kopf-freie-zeit", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();

        var button = Named<Button>(window, "FreeTimeButton");
        var newAppointment = window.GetVisualDescendants().OfType<SplitButton>().First(s => s.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyVisible);
        Assert.DoesNotContain("primary", button.Classes);
        Assert.DoesNotContain("primary", newAppointment.Classes);
        Assert.Same(newAppointment.Parent, button.Parent);
        Assert.Equal(TopOf(window, newAppointment) + newAppointment.Bounds.Height / 2, TopOf(window, button) + button.Bounds.Height / 2, 1.0);
        Assert.True(LeftOf(window, button) > LeftOf(window, newAppointment) + newAppointment.Bounds.Width - 1);
        Assert.Equal("Freie Zeit suchen (Strg+N)", ToolTip.GetTip(button)?.ToString());
        Assert.Equal("Freie Zeit suchen", AutomationProperties.GetName(button));
        // Namen für den Live-Tester (UI Automation): der Knopf „Termin“ und der Text der Meldung unten.
        Assert.Equal("Termin", AutomationProperties.GetName(newAppointment));
        var toastText = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ToastBar").GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("msg"));
        Assert.Equal("Meldung", AutomationProperties.GetName(toastText));
        Assert.False(ShowsText(window, "Freie Zeit suchen"));

        Assert.Null(calendar.FreeTime);
        button.Command!.Execute(button.CommandParameter);
        Assert.NotNull(calendar.FreeTime);
        return calendar;
    });

    [Fact]
    public Task Freie_Zeit_suchen_zeigt_kein_Feld_Sprache_des_Gastes() => Render("hell", "kalender-freie-zeit-ohne-sprache", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        Assert.True(calendar.HandleKey(Avalonia.Input.Key.N, Avalonia.Input.KeyModifiers.Control));
        await Pump();
        Assert.True(calendar.FreeTime!.HasLanguages);
        Assert.False(ShowsText(window, "Sprache des Gastes"));
        return calendar;
    });
}
