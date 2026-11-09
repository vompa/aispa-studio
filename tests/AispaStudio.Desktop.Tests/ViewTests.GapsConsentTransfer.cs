using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Sofortüberweisung an der Kassa, Lückenvorschläge im Kalender und Einwilligung für Wiederbuchungs-Hinweise am Termin (Runde R05, Slice 126: Ä290–Ä292).</summary>
public sealed partial class ViewTests
{
    private static readonly string TransferRoute = $"/api/pos/bills/{SpaSample.BillId}/instant-transfer?amount=89";

    private static readonly string GapsRoute = $"/api/locations/{SpaSample.LocationId}/gap-suggestions?date=2026-10-19";

    private static readonly string ConsentRoute = $"/api/customers/{SpaSample.Selected.CustomerId}/marketing-consent";

    private static InstantTransferQr SampleTransfer() =>
        new("BCD\n002\n1\nSCT\n\nDay-Spa Wien GmbH\nAT611904300234573201\nEUR89.00", 89m, "AT61 1904 3002 3457 3201", "Day-Spa Wien GmbH");

    private static GapSuggestion SampleGap() =>
        new(SpaSample.Selected.Id, SpaSample.Lena.Id, "Lena Huber", "Brigitte Müller", "Aromaöl-Massage", SpaSample.At(10, 30), SpaSample.At(10, 10), 20,
            "Schließt eine Lücke von 20 Minuten bei Lena Huber.");

    private static MarketingConsentDto Consent(MarketingConsentState state) => state switch
    {
        MarketingConsentState.Confirmed => new(state, "brigitte@example.test", "de", SpaSample.At(9), SpaSample.At(10), null, "Team"),
        MarketingConsentState.Pending => new(state, "brigitte@example.test", "de", SpaSample.At(9), null, null, "Team"),
        MarketingConsentState.Withdrawn => new(state, "brigitte@example.test", "de", SpaSample.At(9), SpaSample.At(10), SpaSample.At(11), "Team"),
        MarketingConsentState.Refused => new(state, "brigitte@example.test", "de", null, null, SpaSample.At(11), "Rezeption"),
        _ => new(state, null, null, null, null, null, null),
    };

    /// <summary>Nur Ansehen: ohne das Recht zum Buchen bleiben Einwilligung und Lückenvorschläge ohne Knöpfe.</summary>
    private static StubApi ReadOnlyApi() => Api().On("GET /api/auth/me", new CurrentUser("u3", "ansicht.wien@demo.aispa.test", "Anna Ansicht", 1,
        new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null, [AccessRight.ViewCalendar]));

    private static async Task ClickVisible(Window window, string name)
    {
        var control = VisibleNamed<Control>(window, name);
        control.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, control);
        await Pump(Settling);
    }

    // ---------- Sofortüberweisung ----------

    private static async Task<PosViewModel> OpenBill(Window window, MainViewModel main)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);
        return shell.Pos;
    }

    [Fact]
    public Task Sofortueberweisung_zeigt_Empfaenger_IBAN_und_Betrag_der_API_mit_dem_offenen_Betrag()
    {
        var stub = Api().On($"GET {TransferRoute}", SampleTransfer());
        return Render("hell", "kassa-sofortueberweisung", async (window, main) =>
        {
            var pos = await OpenBill(window, main);
            Assert.DoesNotContain(stub.Calls, c => c.Route == $"GET {TransferRoute}");

            await ClickVisible(window, "TransferDetailsButton");

            Assert.Contains(stub.Calls, c => c.Route == $"GET {TransferRoute}");
            var texts = VisibleTexts(window.GetVisualDescendants().OfType<PosView>().First());
            Assert.Contains("Empfänger: Day-Spa Wien GmbH", texts);
            Assert.Contains("IBAN: AT61 1904 3002 3457 3201", texts);
            Assert.Contains("Betrag: 89,00 €", texts);
            Assert.Equal("89,00", pos.Payments.First(p => p.Method == PaymentMethod.InstantTransfer).Amount);
            Assert.Equal(SampleTransfer().Payload, VisibleNamed<TextBox>(window, "TransferPayloadBox").Text);
            return pos;
        }, stub);
    }

    [Fact]
    public Task Sofortueberweisung_zeigt_das_QR_Bild_mit_dem_Zahlungstext_der_API_und_benanntem_Bild()
    {
        var stub = Api().On($"GET {TransferRoute}", SampleTransfer());
        return Render("hell", "kassa-sofortueberweisung-qr", async (window, main) =>
        {
            var pos = await OpenBill(window, main);
            Assert.False(IsShown(window, "TransferQrImage"));

            await ClickVisible(window, "TransferDetailsButton");

            var image = VisibleNamed<Image>(window, "TransferQrImage");
            var bitmap = Assert.IsType<Bitmap>(image.Source);
            Assert.Equal("QR-Code für die Sofortüberweisung", AutomationProperties.GetName(image));
            Assert.Equal(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            using var expected = QrCodeImage.Create(SampleTransfer().Payload);
            Assert.Equal(expected.PixelSize, bitmap.PixelSize);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], QrCodeImage.Png(SampleTransfer().Payload).Take(4));
            Assert.NotEqual(QrCodeImage.Png(SampleTransfer().Payload), QrCodeImage.Png(SampleTransfer().Payload + "x"));
            return pos;
        }, stub);
    }

    [Fact]
    public Task Sofortueberweisung_mit_Fehler_der_API_zeigt_den_Fehler_und_laesst_das_Feld_leer()
    {
        var stub = Api().On($"GET {TransferRoute}", new ProblemDetailsSample("Für diese Kasse ist kein Konto hinterlegt."), HttpStatusCode.UnprocessableEntity);
        return Render("hell", "kassa-sofortueberweisung-fehler", async (window, main) =>
        {
            var pos = await OpenBill(window, main);

            await ClickVisible(window, "TransferDetailsButton");

            Assert.Contains("Für diese Kasse ist kein Konto hinterlegt.", VisibleTexts(window.GetVisualDescendants().OfType<PosView>().First()));
            Assert.Equal("", pos.Payments.First(p => p.Method == PaymentMethod.InstantTransfer).Amount);
            Assert.False(IsShown(window, "TransferPayloadBox"));
            return pos;
        }, stub);
    }

    private sealed record ProblemDetailsSample(string Detail);

    // ---------- Lückenvorschläge ----------

    private static async Task<CalendarViewModel> OpenGaps(MainViewModel main)
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenGapsCommand.Execute(null);
        await Pump(Settling);
        return calendar;
    }

    [Fact]
    public Task Lueckenvorschlaege_stehen_im_Panel_mit_Gast_Zeiten_und_Grund_der_API() => Render("hell", "kalender-lueckenvorschlaege", async (window, main) =>
    {
        var calendar = await OpenGaps(main);

        var texts = PanelTexts(window, "GapsList");
        Assert.Contains("Brigitte Müller, Aromaöl-Massage", texts);
        Assert.Contains("bei Lena Huber von 10:30 auf 10:10", texts);
        Assert.Contains("Schließt eine Lücke von 20 Minuten bei Lena Huber.", texts);
        Assert.Equal("Auf 10:10 verschieben", VisibleNamed<Button>(window, "MoveGapButton").Content);
        return calendar;
    }, Api().On($"GET {GapsRoute}", new List<GapSuggestion> { SampleGap() }));

    [Fact]
    public Task Lueckenvorschlag_Klick_auf_Verschieben_ruft_move_mit_der_Zeit_des_Vorschlags()
    {
        var moved = false;
        var stub = Api()
            .OnJson($"GET {GapsRoute}", _ => moved ? new List<GapSuggestion>() : new List<GapSuggestion> { SampleGap() })
            .OnJson($"POST /api/appointments/{SpaSample.Selected.Id}/move", _ =>
            {
                moved = true;
                return SpaSample.Selected with { Start = SpaSample.At(10, 10), End = SpaSample.Selected.End.AddMinutes(-20) };
            });
        return Render("hell", "kalender-luecke-verschieben", async (window, main) =>
        {
            var calendar = await OpenGaps(main);
            Assert.DoesNotContain(stub.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal) && c.Route.EndsWith("/move", StringComparison.Ordinal));

            await ClickVisible(window, "MoveGapButton");

            var call = Assert.Single(stub.Calls, c => c.Route == $"POST /api/appointments/{SpaSample.Selected.Id}/move");
            Assert.Contains(SpaSample.Lena.Id.ToString(), call.Body, StringComparison.Ordinal);
            Assert.Contains("2026-10-19T08:10:00", call.Body, StringComparison.Ordinal);
            Assert.Contains("Keine Lücke, die sich mit wenigen Minuten Verschieben schließen lässt.", PanelTexts(window, "GapsList"));
            Assert.Equal("Brigitte Müller auf 10:10 verschoben", calendar.Toast);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Ohne_Lueckenvorschlaege_sagt_das_Panel_dass_nichts_zu_schliessen_ist() => Render("hell", "kalender-lueckenvorschlaege-leer", async (window, main) =>
    {
        var calendar = await OpenGaps(main);

        Assert.Contains("Keine Lücke, die sich mit wenigen Minuten Verschieben schließen lässt.", PanelTexts(window, "GapsList"));
        Assert.False(IsShown(window, "MoveGapButton"));
        return calendar;
    }, Api().On($"GET {GapsRoute}", new List<GapSuggestion>()));

    // ---------- Einwilligung für Wiederbuchungs-Hinweise ----------

    [Fact]
    public Task Gast_ohne_Einwilligung_Klick_auf_Anfragen_zeigt_den_neuen_Stand()
    {
        var stub = Api()
            .On($"GET {ConsentRoute}", Consent(MarketingConsentState.None))
            .On($"POST {ConsentRoute}", Consent(MarketingConsentState.Pending));
        return Render("hell", "termin-einwilligung-anfragen", async (window, main) =>
        {
            var calendar = await OpenSelected(main);
            Assert.Contains("Nicht angefragt", PanelTexts(window, "ConsentCard"));
            Assert.False(IsShown(window, "WithdrawConsentButton"));

            await ClickScrolled(window, "RequestConsentButton");

            var call = Assert.Single(stub.Calls, c => c.Route == $"POST {ConsentRoute}");
            Assert.Contains("\"language\":\"de\"", call.Body, StringComparison.Ordinal);
            var texts = PanelTexts(window, "ConsentCard");
            Assert.Contains("Angefragt am 19.10.2026, wartet auf Bestätigung durch den Gast (brigitte@example.test)", texts);
            Assert.Equal("Mail zum Bestätigen ist unterwegs", calendar.Toast);
            Assert.True(IsShown(window, "WithdrawConsentButton"));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Gast_hat_am_Empfang_abgelehnt_Panel_zeigt_die_Ablehnung_und_Anfragen()
    {
        var stub = Api().On($"GET {ConsentRoute}", Consent(MarketingConsentState.Refused));
        return Render("hell", "termin-einwilligung-abgelehnt", async (window, main) =>
        {
            var calendar = await OpenSelected(main);
            Assert.Contains("Abgelehnt am 19.10.2026", PanelTexts(window, "ConsentCard"));
            Assert.True(IsShown(window, "RequestConsentButton"));
            Assert.False(IsShown(window, "WithdrawConsentButton"));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Gast_mit_Einwilligung_Klick_auf_Widerrufen_zeigt_den_Widerruf()
    {
        var stub = Api()
            .On($"GET {ConsentRoute}", Consent(MarketingConsentState.Confirmed))
            .On($"DELETE {ConsentRoute}", Consent(MarketingConsentState.Withdrawn));
        return Render("hell", "termin-einwilligung-widerrufen", async (window, main) =>
        {
            var calendar = await OpenSelected(main);
            Assert.Contains("Eingewilligt am 19.10.2026 (brigitte@example.test)", PanelTexts(window, "ConsentCard"));
            Assert.False(IsShown(window, "RequestConsentButton"));

            await ClickScrolled(window, "WithdrawConsentButton");

            Assert.Contains(stub.Calls, c => c.Route == $"DELETE {ConsentRoute}");
            Assert.Contains("Widerrufen am 19.10.2026", PanelTexts(window, "ConsentCard"));
            Assert.Equal("Einwilligung widerrufen", calendar.Toast);
            Assert.False(IsShown(window, "WithdrawConsentButton"));
            Assert.True(IsShown(window, "RequestConsentButton"));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Einwilligung_ohne_Buchungsrecht_zeigt_den_Stand_ohne_Knoepfe() => Render("hell", "termin-einwilligung-ohne-recht", async (window, main) =>
    {
        var calendar = await OpenSelected(main);

        Assert.Contains("Eingewilligt am 19.10.2026 (brigitte@example.test)", PanelTexts(window, "ConsentCard"));
        Assert.False(IsShown(window, "RequestConsentButton"));
        Assert.False(IsShown(window, "WithdrawConsentButton"));
        return calendar;
    }, ReadOnlyApi().On($"GET {ConsentRoute}", Consent(MarketingConsentState.Confirmed)));

    [Fact]
    public Task Sofortueberweisung_Betrag_aendern_raeumt_die_Zahlungsdaten_weg()
    {
        var stub = Api().On($"GET {TransferRoute}", SampleTransfer());
        return Render("hell", "kassa-sofortueberweisung-betrag-geaendert", async (window, main) =>
        {
            var pos = await OpenBill(window, main);
            await ClickVisible(window, "TransferDetailsButton");
            Assert.True(IsShown(window, "TransferPayloadBox"));

            pos.Payments.First(p => p.Method == PaymentMethod.InstantTransfer).Amount = "50,00";
            await Pump(Settling);

            Assert.False(IsShown(window, "TransferPayloadBox"));
            Assert.False(IsShown(window, "TransferQrImage"));
            return pos;
        }, stub);
    }

    [Fact]
    public Task Lueckenvorschlaege_des_alten_Tages_verschwinden_beim_Tageswechsel() => Render("hell", "kalender-lueckenvorschlaege-tageswechsel", async (window, main) =>
    {
        var calendar = await OpenGaps(main);
        Assert.True(IsShown(window, "MoveGapButton"));

        calendar.Date = calendar.Date.AddDays(1);
        await Pump(Settling);

        Assert.False(IsShown(window, "MoveGapButton"));
        return calendar;
    }, Api().On($"GET {GapsRoute}", new List<GapSuggestion> { SampleGap() }));
}
