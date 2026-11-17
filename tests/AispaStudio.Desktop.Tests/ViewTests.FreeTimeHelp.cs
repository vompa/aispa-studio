using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Hilfe aus „Freie Zeit suchen“ (R04 Slice 87, Ä155): das Fragezeichen am Panel öffnet das Thema „Freie Zeit suchen“ in der Leiste „KI und Hilfe“.</summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Freie_Zeit_suchen_hat_ein_Fragezeichen_das_das_Thema_Freie_Zeit_suchen_oeffnet() => Render("hell", "kalender-freie-zeit-hilfe", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenFreeTimeCommand.Execute(null);
        await Pump();

        // Given „Freie Zeit suchen“ offen, When auf das Fragezeichen des Panels geklickt wird, Then zeigt die Leiste „KI und Hilfe“ das Thema „Freie Zeit suchen“.
        var panel = SidePanelOf(window);
        var help = Assert.Single(panel.GetVisualDescendants().OfType<HelpButton>(), b => b.IsEffectivelyVisible && b.Topic == "kalender-freie-zeit-suchen");
        Assert.Null(help.Section);
        help.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Pump();

        // Seit der Hilfe als Leiste (P1) öffnet das Fragezeichen im Kalender den Reiter „KI und Hilfe“ mit dem Thema, die Seite bleibt.
        var shell = (ShellViewModel)main.Content!;
        Assert.Equal(new HelpTarget("kalender-freie-zeit-suchen", null), shell.Companion.Help);
        Assert.Same(shell.Companion, calendar.CompanionPanel);
        return calendar;
    });
}
