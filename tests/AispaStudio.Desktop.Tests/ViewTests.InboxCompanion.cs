using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Posteingang und Leiste „KI und Hilfe“ nach R01 Slice 10 (W28, W29, W30, W32, W34, W35, W-Muster 1, 4, 13): ein Primärknopf in der Leiste, die Antwort wird
/// angesagt, Felder haben Namen, jede KI-Zusammenfassung trägt die Pille, die Knöpfe zum Überarbeiten tragen Verben.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly AssistantAnswer BookingAnswer = new(AssistantAudience.Reception, "Morgen Nachmittag ist eine Hot-Stone-Massage frei:", [],
        [new AssistantSuggestion(AssistantSuggestionKind.BookSlot, "Di., 14:00 Hot-Stone-Massage bei Lena Huber", SpaSample.LocationId,
            SpaSample.Treatments[1].Id, SpaSample.Lena.Id, null, null, SpaSample.At(14).AddDays(1))],
        "Antwort einer KI. Zahlen und Zeiten kommen aus Kalender und Kassa.",
        [new AssistantHelpLink("kalender-freie-zeit-suchen", null, "Freie Zeit suchen", "Wo ist noch Platz?")]);

    /// <summary>Öffnet die Leiste, fragt und öffnet die Buchungskarte zum ersten Vorschlag.</summary>
    private static async Task<(CompanionViewModel Companion, CompanionContent Panel)> CompanionBooking(Window window, MainViewModel main)
    {
        await SignedInCalendar(main);
        var companion = ((ShellViewModel)main.Content!).Companion;
        companion.ToggleCommand.Execute(null);
        await companion.AskExampleCommand.ExecuteAsync("Wann ist morgen Nachmittag eine Massage frei?");
        await companion.UseSuggestionCommand.ExecuteAsync(companion.Suggestions[0]);
        await Pump(Settling);
        Assert.True(companion.IsBooking);
        Assert.NotNull(companion.HelpAction);
        return (companion, window.GetVisualDescendants().OfType<CompanionContent>().Single());
    }

    private static string? PeerName(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName();

    [Fact]
    public Task KI_und_Hilfe_mit_Antwort_Buchungsvorschlag_und_Hilfe_Aktion_zeigt_genau_einen_Primaerknopf_Fragen() => Render("hell", "ki-und-hilfe-primaer", async (window, main) =>
    {
        var (companion, panel) = await CompanionBooking(window, main);
        Assert.True(VisibleButton(panel, "Termin buchen").IsEffectivelyVisible);
        Assert.True(VisibleButton(panel, companion.HelpAction!.Label).IsEffectivelyVisible);

        var primaries = panel.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("primary")).ToList();
        Assert.Equal("Fragen", Assert.Single(primaries).Content);
        return companion;
    }, Api().On("POST /api/assistant", BookingAnswer));

    [Fact]
    public Task KI_und_Hilfe_Antwort_erscheint_mit_LiveSetting_Polite_in_der_Antwortkarte() => Render("hell", "ki-und-hilfe-ansage", async (window, main) =>
    {
        await SignedInCalendar(main);
        var companion = ((ShellViewModel)main.Content!).Companion;
        companion.ToggleCommand.Execute(null);
        await companion.AskExampleCommand.ExecuteAsync("Wie kassiere ich einen Termin?");
        await Pump(Settling);

        // Avalonia sagt eine Live-Region an, wenn sich der Name des Elements ändert; der Name des Antworttexts ist die Antwort.
        var panel = window.GetVisualDescendants().OfType<CompanionContent>().Single();
        var answer = panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible && t.Text == AssistantSample.Text);
        Assert.Contains(answer.GetVisualAncestors().OfType<Border>(), b => b.Classes.Contains("card"));
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(answer));
        Assert.Equal(AssistantSample.Text, PeerName(answer));
        return companion;
    }, Api().On("POST /api/assistant", AssistantSample));

    [Fact]
    public Task KI_und_Hilfe_Buchung_hat_Feld_Fuer_wen_und_Gebucht_mit_Haken() => Render("hell", "ki-und-hilfe-gebucht", async (window, main) =>
    {
        var (companion, panel) = await CompanionBooking(window, main);
        var guest = panel.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "CompanionGuest");
        var question = panel.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "CompanionQuestion");
        Assert.Equal("Für wen?", PeerName(guest));
        Assert.Equal("Frage an den Assistenten", PeerName(question));

        await companion.Guest!.SearchAsync("br");
        companion.Guest.Customer = companion.Guest.Results[0];
        await companion.BookCommand.ExecuteAsync(null);
        await Pump(Settling);

        Assert.NotNull(companion.BookedText);
        var booked = panel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible && t.Text == companion.BookedText);
        var row = booked.GetVisualAncestors().OfType<Panel>().First();
        Assert.Contains(row.GetVisualDescendants().OfType<Icon>(), i => i.IsEffectivelyVisible && i.Data == Icons.CheckCircle);
        return companion;
    }, Api().On("POST /api/assistant", BookingAnswer).On("POST /api/appointments", SpaSample.Selected));

    [Fact]
    public Task Posteingang_Zeile_mit_KI_Zusammenfassung_zeigt_die_KI_Pille() => Render("hell", "posteingang-zeile-pille", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump(Settling);

        var rows = window.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.DataContext is InboxRow && i.IsEffectivelyVisible).ToList();
        var withSummary = rows.First(r => ((InboxRow)r.DataContext!).HasSummary);
        var without = rows.First(r => !((InboxRow)r.DataContext!).HasSummary);
        Assert.Contains(withSummary.GetVisualDescendants().OfType<AiPill>(), p => p.IsEffectivelyVisible);
        Assert.DoesNotContain(without.GetVisualDescendants().OfType<AiPill>(), p => p.IsEffectivelyVisible);
        return shell.Inbox;
    }, Api().On("GET /api/inbox", new List<InboundMailSummary>
    {
        SpaSample.Inbox()[0],
        SpaSample.Inbox()[2] with { Summary = null },
    }));

    [Fact]
    public Task Posteingang_Ueberarbeiten_Knoepfe_tragen_Verben() => Render("hell", "posteingang-ueberarbeiten", async (window, main) =>
    {
        var detail = await OpenInboxDetail(window, main);
        Assert.True(detail.CanRevise);
        var view = window.GetVisualDescendants().OfType<InboxView>().Single(v => v.IsEffectivelyVisible);
        var labels = view.GetVisualDescendants().OfType<Button>()
            .Where(b => b.IsEffectivelyVisible && b.Command == detail.ReviseCommand)
            .Select(b => b.Content as string).ToList();
        Assert.Equal(["Freundlicher formulieren", "Kürzer formulieren", "Siezen", "Duzen", "Entschuldigung ergänzen"], labels);
        return detail;
    }, Api().On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail() with { CanRevise = true }));

    [Fact]
    public Task Posteingang_Antwort_Felder_haben_Namen_nicht_aus_dem_Platzhalter() => Render("hell", "posteingang-feldnamen", async (window, main) =>
    {
        var detail = await OpenInboxDetail(window, main);
        Assert.True(detail.CanRevise);
        var view = window.GetVisualDescendants().OfType<InboxView>().Single(v => v.IsEffectivelyVisible);
        var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.IsEffectivelyVisible && s.Content is ContentControl);

        AssertFieldsNamedNotFromPlaceholder(scroll, atLeast: 4);
        var boxes = scroll.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible).ToList();
        Assert.Contains(boxes, b => b.AcceptsReturn && PeerName(b) == detail.ReplyLabel);
        Assert.Contains(boxes, b => PeerName(b) == "Betreff");
        return detail;
    }, Api().On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail() with { CanRepropose = true, CanRevise = true }));
}
