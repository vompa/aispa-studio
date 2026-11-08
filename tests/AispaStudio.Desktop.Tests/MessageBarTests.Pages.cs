using System.Net;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Meldungen in Kalender, Kunden, Kassa und Posteingang; Stil <c>Border.alert</c> entfällt (Runde R01, Slice 05: W33, W46, W48, W63, W-Muster 2, 3).</summary>
public sealed partial class MessageBarTests
{
    private static string View(string file) => File.ReadAllText(Path.Combine(ViewsDir, file));

    [Fact]
    public void Keine_Ansicht_der_App_nutzt_Border_alert_und_Styles_kennt_den_Stil_nicht()
    {
        var app = Path.GetDirectoryName(ViewsDir)!;
        foreach (var file in Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("Classes=\"alert\"", StringComparison.Ordinal) || text.Contains("Classes.alert=", StringComparison.Ordinal), file);
        }

        Assert.DoesNotContain("Border.alert", File.ReadAllText(Path.Combine(app, "Themes", "Styles.axaml")), StringComparison.Ordinal);
    }

    [Fact]
    public void Fehler_der_Buchungsleisten_stehen_ueber_dem_Buchen_Knopf()
    {
        // Der breite Primärknopf am Fuß der Leiste; die Warteliste hat „Buchen“ nur je Zeile.
        const string Book = "Command=\"{Binding BookCommand}\" HorizontalAlignment=\"Stretch\"";
        var templates = Regex.Split(View("CalendarView.axaml"), "<DataTemplate DataType=").Where(t => t.Contains(Book, StringComparison.Ordinal)).ToList();
        Assert.Equal(3, templates.Count);
        foreach (var template in templates)
        {
            var error = template.IndexOf("Message=\"{Binding Error}\"", StringComparison.Ordinal);
            Assert.InRange(error, 0, template.IndexOf(Book, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Fehler_der_Eintritte_steht_ausserhalb_des_Formulars_und_bleibt_bei_Ladefehler_sichtbar()
    {
        // Ein Lade- oder Stornofehler kommt ohne Formular (ShowForm falsch); die Meldung darf nicht darin stecken.
        var areas = Regex.Split(View("CalendarView.axaml"), "<DataTemplate DataType=").Single(t => t.StartsWith("\"vm:AreasViewModel\"", StringComparison.Ordinal));
        var error = areas.IndexOf("Message=\"{Binding Error}\"", StringComparison.Ordinal);
        Assert.InRange(error, 0, areas.IndexOf("IsVisible=\"{Binding ShowForm}\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Wechselgeld_und_Druckstatus_der_Kassa_sind_hoeflich_angesagt()
    {
        var lines = Regex.Matches(View("PosView.axaml"), @"<TextBlock[^>]*(ChangeText|PrintText)\}""[^>]*>");
        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", line.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Rechnungshinweis_der_Kassa_ist_eine_InfoBar_Warnung()
    {
        var bars = Regex.Matches(View("PosView.axaml"), @"<(\S+)[^>]*\{Binding InvoiceHint\}[^>]*>");
        Assert.Equal(2, bars.Count);
        Assert.All(bars, bar =>
        {
            Assert.Equal("ui:InfoBar", bar.Groups[1].Value);
            Assert.Contains("Severity=\"Warning\"", bar.Value, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(CardPaymentStatus.Pending, InfoBarSeverity.Info, "Abbrechen")]
    [InlineData(CardPaymentStatus.Approved, InfoBarSeverity.Success, "Stornieren")]
    [InlineData(CardPaymentStatus.Declined, InfoBarSeverity.Error, null)]
    [InlineData(CardPaymentStatus.Cancelled, InfoBarSeverity.Info, null)]
    [InlineData(CardPaymentStatus.ReversalPending, InfoBarSeverity.Info, null)]
    [InlineData(CardPaymentStatus.Reversed, InfoBarSeverity.Info, null)]
    [InlineData(CardPaymentStatus.RefundPending, InfoBarSeverity.Info, null)]
    [InlineData(CardPaymentStatus.Refunded, InfoBarSeverity.Success, null)]
    [InlineData(CardPaymentStatus.RefundFailed, InfoBarSeverity.Error, null)]
    public void Kartenzahlung_Schweregrad_und_Aktion_folgen_dem_Ergebnis(CardPaymentStatus status, InfoBarSeverity severity, string? action)
    {
        var card = new PaymentEntry(PaymentMethod.Card, () => { })
        {
            CardPayment = new CardPaymentDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, "EUR", status, null, null, null, null, null, null, DateTimeOffset.UnixEpoch, null),
        };
        Assert.Equal((severity, action), (card.TerminalSeverity, card.TerminalActionText));
    }

    [Fact]
    public async Task Posteingang_Hinweis_ohne_Fehler_ist_Info_und_mit_Fehler_Error()
    {
        var route = $"POST /api/inbox/{SpaSample.InboxMailId}/dismiss";
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
            .On("GET /api/inbox", new List<InboundMailSummary> { SpaSample.Inbox()[0] })
            .On($"GET /api/inbox/{SpaSample.InboxMailId}", SpaSample.InboxMail())
            .On(route, SpaSample.InboxMail());
        var vm = new InboxViewModel(api.Client(), null, TimeProvider.System, poll: TimeSpan.Zero);
        await vm.InitializeAsync();
        vm.SelectedRow = vm.Mails[0];
        for (var i = 0; i < 100 && vm.Detail is null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var detail = vm.Detail!;
        await detail.DismissCommand.ExecuteAsync(null);
        Assert.Equal(InfoBarSeverity.Info, detail.NoticeSeverity);

        api.On(route, null, HttpStatusCode.InternalServerError);
        await detail.DismissCommand.ExecuteAsync(null);
        Assert.Equal(InfoBarSeverity.Error, detail.NoticeSeverity);
    }
}
