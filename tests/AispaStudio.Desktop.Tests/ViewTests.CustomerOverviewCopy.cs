using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenübersicht (T-031): Dieselbe Übersicht hängt im Terminkopf an zwei Flyouts (Initialen und Name), jedes mit eigener Ansicht. Kopiert wird über
/// die gerade offene Ansicht; bliebe die zuerst geöffnete der Besitzer, käme nach dem Schließen nichts mehr in der Zwischenablage an, obwohl „Kopiert.“
/// dasteht.
/// </summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Kundenuebersicht_kopiert_auch_im_zweiten_Flyout_in_die_Zwischenablage() => Render("hell", "kalender-kundenuebersicht-kopieren", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump();
        await window.Clipboard!.ClearAsync();

        var avatar = window.GetVisualDescendants().OfType<Button>().First(b => b.Flyout is not null && b.IsEffectivelyVisible && ToolTip.GetTip(b) as string == "Kundenübersicht");
        ClickCenter(window, avatar);
        await Pump();
        await Pump();
        avatar.Flyout!.Hide();
        await Pump();

        ClickCenter(window, Named<TextBlock>(window, "GuestName"));
        await Pump();
        await Pump();
        var copyPhone = window.GetVisualDescendants().OfType<Button>()
            .First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Telefonnummer kopieren");
        ClickCenter(window, copyPhone);
        await Pump();
        await Pump();

        Assert.Equal("+43 664 123 45 67", await window.Clipboard.TryGetTextAsync());
        Assert.Equal("Kopiert.", calendar.Selected!.Overview.Copy.Text);
        return calendar;
    }, WithGuestOverview(Api()));
}
