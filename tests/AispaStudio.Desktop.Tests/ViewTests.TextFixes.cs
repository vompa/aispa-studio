using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Runde R02, Slice 35 (W8, W57, W58): Technisches steht aufklappbar unter „Technische Angaben“, die Meldungskarte ist ein Knopf mit lesbarem Namen,
/// das Menü „Neuer Termin“ hat keinen deaktivierten Gruppentitel. (W27, die Trefferzahl der Hilfesuche, prüft <c>web/src/help/helpPage.test.tsx</c>.)
/// </summary>
public sealed partial class ViewTests
{
    [Fact]
    public Task Technische_Angaben_sind_zu_bis_man_sie_aufklappt_und_melden_ihren_Zustand() => InWindow("hell", () => new TechnicalDetails { Text = "Port 9100, ESC/POS" }, 400, 200, async window =>
    {
        var details = (TechnicalDetails)window.Content!;
        var header = details.GetVisualDescendants().OfType<AccordionHeader>().Single();
        Assert.Equal("Technische Angaben", ControlAutomationPeer.CreatePeerForElement(header).GetName());
        var text = details.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Port 9100, ESC/POS");
        var peer = (IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(header);

        Assert.False(text.IsEffectivelyVisible);
        Assert.Equal(ExpandCollapseState.Collapsed, peer.ExpandCollapseState);

        header.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        await Pump();
        Assert.True(text.IsEffectivelyVisible);
        Assert.Equal(ExpandCollapseState.Expanded, peer.ExpandCollapseState);

        Press(window, PhysicalKey.Enter);
        await Pump();
        Assert.False(text.IsEffectivelyVisible);
    });

    [Fact]
    public Task Meldungskarte_ist_ein_Knopf_mit_Titel_und_Hinweis_im_Namen() => Render("hell", "kalender-meldungskarte-knopf", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        calendar.ByRoom = true;
        await Pump();
        calendar.OpenMessagesCommand.Execute(null);
        await Pump();

        var card = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("messagecard") && b.DataContext is MessageItem { Kind: MessageKind.DayStart });
        var item = (MessageItem)card.DataContext!;
        var peer = ControlAutomationPeer.CreatePeerForElement(card);
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Contains(item.Title, peer.GetName(), StringComparison.Ordinal);
        Assert.Contains(item.Text, peer.GetName(), StringComparison.Ordinal);
        Assert.Contains("Details", peer.GetHelpText(), StringComparison.Ordinal);

        // Keine Knöpfe in Knöpfen: Aktion und „Wegklicken“ liegen neben der Karte.
        Assert.Empty(card.GetVisualDescendants().OfType<Button>());

        card.BringIntoView();
        await Pump();
        card.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        await Pump(Settling);
        Assert.Equal("Meldung", calendar.ActivePanel!.PanelTitle);
        return calendar;
    }, OperationsApi());

    [Fact]
    public Task Menue_Neuer_Termin_hat_keinen_deaktivierten_Eintrag_Sonderfaelle_sondern_einen_Trenner_vor_den_Sonderfaellen() => Render("hell", "kalender-neuer-termin-menue", async (window, main) =>
    {
        var calendar = await SignedInCalendar(main);
        await Pump();
        var split = window.GetVisualDescendants().OfType<SplitButton>().First(s => s.Flyout is MenuFlyout);
        var items = ((MenuFlyout)split.Flyout!).Items.OfType<Control>().ToList();

        Assert.DoesNotContain(items, i => i is MenuItem { IsEnabled: false });
        Assert.DoesNotContain(items, i => i is MenuItem m && (m.Header as string) == "Sonderfälle");
        var firstSpecial = items.FindIndex(i => i is MenuItem m && AutomationProperties.GetName(m) == "Paar oder Spa-Tag");
        Assert.True(firstSpecial > 0);
        Assert.IsType<Separator>(items[firstSpecial - 1]);
        return calendar;
    }, OperationsApi());
}
