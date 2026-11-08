using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kundenerkennung im Posteingang (Auftraggeber, 29.10.2026): ist der Absender erkannt, stehen Initialen und Name im Kopf und zeigen die Kundenübersicht
/// wie im Terminkopf des Kalenders; unter „Wer ist das?“ zeigt der Name jedes Vorschlags dieselbe Übersicht.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Claudia Haider mit Kennzahlen, Kennzeichen, Wünschen und Terminen, wie im Kalender.</summary>
    private static StubApi WithHaiderOverview(StubApi api)
    {
        var id = SpaSample.HaiderId;
        return api
            .On($"GET /api/customers/{id}", SpaSample.Customers[0] with { Id = id, FirstName = "Claudia", LastName = "Haider", TitlesBefore = null })
            .On($"GET /api/customers/{id}/insights", SpaSample.Insights())
            .On($"GET /api/customers/{id}/flags", SpaSample.CustomerFlags())
            .On($"GET /api/reports/customers/{id}/preferences", SpaSample.Wishes())
            .On($"GET /api/customers/{id}/appointments", SpaSample.CustomerHistory());
    }

    private static InboundMailDto RecognizedMail() => SpaSample.InboxMail() with { CustomerId = SpaSample.HaiderId, CustomerConfirmed = true };

    private static async Task<(ShellViewModel Shell, InboxDetailViewModel Detail)> OpenInboxMail(MainViewModel main)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
        await Pump();
        shell.Inbox.SelectedRow = shell.Inbox.Mails[0];
        await Pump();
        await Pump();
        return (shell, shell.Inbox.Detail!);
    }

    private static Button SenderAvatar(Window window) => window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "SenderAvatar");

    [Fact]
    public Task Posteingang_erkannter_Absender_zeigt_Initialen_Namen_und_die_Kundenuebersicht_wie_der_Kalender() => Render("hell", "posteingang-kundenuebersicht", async (window, main) =>
    {
        var (shell, detail) = await OpenInboxMail(main);

        // Kopf wie im Terminkopf: Initialen, Name aus der Kundenkarte, darunter der Absender, wie er geschrieben hat.
        var avatar = SenderAvatar(window);
        Assert.True(avatar.IsEffectivelyVisible);
        Assert.Contains(avatar.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "CH");
        var name = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "SenderName");
        Assert.Equal("Claudia Haider", name.Text);
        Assert.Equal("Kundenübersicht", ToolTip.GetTip(name));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Claudia Haider <c.haider@example.at>" && t.IsEffectivelyVisible);

        ClickCenter(window, avatar);
        await Pump();
        await Pump();
        Assert.True(avatar.Flyout!.IsOpen);
        var overview = detail.Overview!;
        Assert.False(overview.IsLoading);
        Assert.Equal("7 Besuche in zwölf Monaten", overview.VisitsText);
        Assert.Equal(["Braucht mehr Zeit", "Nicht bei Marco Berger"], overview.Flags);
        Assert.True(overview.CanOpenCard && overview.CanEdit);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "7 Besuche in zwölf Monaten" && t.IsEffectivelyVisible);
        return shell.Inbox;
    }, WithHaiderOverview(Api().On($"GET /api/inbox/{SpaSample.InboxMailId}", RecognizedMail())));

    [Fact]
    public Task Posteingang_Kundenuebersicht_oeffnet_per_Klick_auf_den_Namen_und_fuehrt_zur_Kundenkarte() => Render("hell", "posteingang-kundenuebersicht-name", async (window, main) =>
    {
        var (shell, detail) = await OpenInboxMail(main);
        var name = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "SenderName");

        ClickCenter(window, name);
        await Pump();
        await Pump();
        Assert.True(FlyoutBase.GetAttachedFlyout(name)!.IsOpen);
        Assert.Equal("Claudia Haider", detail.Overview!.Name);

        // „Kundenkarte öffnen“ wechselt zu den Kunden und schließt die Übersicht.
        ClickCenter(window, ButtonWithText(window, "Kundenkarte öffnen"));
        await Pump();
        await Pump();
        Assert.False(FlyoutBase.GetAttachedFlyout(name)!.IsOpen);
        Assert.Same(shell.Customers, shell.SelectedPage!.Page);
        Assert.Equal(SpaSample.HaiderId, shell.Customers.Selected?.Id);
        return shell.Customers;
    }, WithHaiderOverview(Api().On($"GET /api/inbox/{SpaSample.InboxMailId}", RecognizedMail())));

    [Fact]
    public Task Posteingang_Kundenuebersicht_Bearbeiten_oeffnet_die_Kundendaten() => Render("hell", "posteingang-kundenuebersicht-bearbeiten", async (window, main) =>
    {
        var (shell, _) = await OpenInboxMail(main);
        var avatar = SenderAvatar(window);
        ClickCenter(window, avatar);
        await Pump();
        await Pump();

        ClickCenter(window, ButtonWithText(window, "Bearbeiten"));
        await Pump();
        await Pump();
        Assert.False(avatar.Flyout!.IsOpen);
        Assert.Same(shell.Customers, shell.SelectedPage!.Page);
        Assert.NotNull(shell.Customers.Contact);
        return shell.Customers;
    }, WithHaiderOverview(Api().On($"GET /api/inbox/{SpaSample.InboxMailId}", RecognizedMail())));

    [Fact]
    public Task Posteingang_unbekannter_Absender_hat_keine_Initialen_und_keinen_Hinweis_am_Namen() => Render("hell", "posteingang-unbekannter-absender", async (window, main) =>
    {
        var (shell, detail) = await OpenInboxMail(main);

        Assert.False(SenderAvatar(window).IsEffectivelyVisible);
        var name = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "SenderName");
        Assert.Equal("Claudia Haider", name.Text);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "c.haider@example.at" && t.IsEffectivelyVisible);
        Assert.Null(ToolTip.GetTip(name));
        Assert.Null(detail.Overview);
        ClickCenter(window, name);
        await Pump();
        Assert.False(FlyoutBase.GetAttachedFlyout(name)!.IsOpen);
        return shell.Inbox;
    }, Api().On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail()));

    [Fact]
    public Task Posteingang_wer_ist_das_zeigt_zum_Vorschlag_die_Kundenuebersicht() => Render("hell", "posteingang-wer-ist-das-kundenuebersicht", async (window, main) =>
    {
        var (shell, detail) = await OpenInboxMail(main);
        Assert.True(detail.HasCandidates);
        var link = window.GetVisualDescendants().OfType<Button>()
            .First(b => b.Flyout is not null && b.IsEffectivelyVisible && b.DataContext is CandidateOption { Name: "Claudia Haider" });
        Assert.Equal("Kundenübersicht Claudia Haider", Avalonia.Automation.AutomationProperties.GetName(link));
        link.BringIntoView();
        await Pump();

        ClickCenter(window, link);
        await Pump();
        await Pump();
        Assert.True(link.Flyout!.IsOpen);
        var overview = ((CandidateOption)link.DataContext!).Overview;
        Assert.False(overview.IsLoading);
        Assert.Equal("7 Besuche in zwölf Monaten", overview.VisitsText);
        Assert.True(overview.CanOpenCard);
        return shell.Inbox;
    }, WithHaiderOverview(Api()
        .On("GET /api/inbox", new List<InboundMailSummary> { SpaSample.Inbox()[0] with { Status = InboundMailStatus.NeedsReview } })
        .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMailWhoIsThis())));
}
