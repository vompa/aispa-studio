using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Meldungen in Posteingang und Kassa als InfoBar mit Symbol und Ansage (Runde R01, Slice 05: W33, W46; meldungen.md M6).</summary>
public sealed partial class ViewTests
{
    private static InfoBar VisibleInfoBar(Control root, string message)
        => root.GetVisualDescendants().OfType<InfoBar>().Single(b => b.IsEffectivelyVisible && b.Message == message);

    [Fact]
    public Task Posteingang_Fehlermeldung_ist_eine_InfoBar_mit_Severity_Error() => Render("hell", "posteingang-fehler", async (window, main) =>
    {
        var detail = await OpenInboxDetail(window, main);
        await detail.DismissCommand.ExecuteAsync(null);
        await Pump();
        Assert.True(detail.NoticeIsError);

        var view = window.GetVisualDescendants().OfType<InboxView>().Single(v => v.IsEffectivelyVisible);
        var bar = VisibleInfoBar(view, detail.Notice!);
        Assert.Equal(InfoBarSeverity.Error, bar.Severity);
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(bar));
        return detail;
    }, Api().On($"POST /api/inbox/{SpaSample.InboxMailId}/dismiss", null, HttpStatusCode.InternalServerError));

    [Fact]
    public Task Kassa_abgelehnte_Kartenzahlung_zeigt_Fehlersymbol_und_wird_sofort_angesagt() => Render("hell", "kassa-terminal-abgelehnt", async (window, main) =>
    {
        var card = await StartCardPayment(main);
        Assert.Equal("Nicht genehmigt: Falsche PIN", card.TerminalText);

        var bar = VisibleInfoBar(window.GetVisualDescendants().OfType<PosView>().First(), card.TerminalText);
        Assert.Equal(InfoBarSeverity.Error, bar.Severity);
        Assert.Contains(bar.GetVisualDescendants().OfType<Icon>(), i => i.IsEffectivelyVisible && i.Data == Icons.ErrorCircle);
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(bar));
        return card;
    }, Api().On($"GET /api/pos/card-payments/{CardId}", Card(CardPaymentStatus.Declined) with { Message = "Falsche PIN" }));

    [Fact]
    public Task Kassa_genehmigte_Kartenzahlung_wird_hoeflich_angesagt() => Render("hell", "kassa-terminal-genehmigt", async (window, main) =>
    {
        var card = await StartCardPayment(main);
        Assert.True(card.IsApproved);

        var bar = VisibleInfoBar(window.GetVisualDescendants().OfType<PosView>().First(), card.TerminalText);
        Assert.Equal(InfoBarSeverity.Success, bar.Severity);
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(bar));
        Assert.Contains(bar.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Content as string == "Stornieren");
        var shell = (ShellViewModel)main.Content!;
        Assert.Same(shell.Pos.CancelCardPaymentCommand, bar.ActionCommand);
        Assert.Same(card, bar.ActionParameter);
        return card;
    });

    private static async Task<PaymentEntry> StartCardPayment(MainViewModel main)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        var card = shell.Pos.Payments.First(p => p.IsCard);
        await shell.Pos.StartCardPaymentCommand.ExecuteAsync(card);
        await Pump();
        return card;
    }
}
