using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kartengarantie, Stand der Anzahlung und Erinnerungsverlauf am Termin, Rückerstattung und Gutscheinabfrage an der Kassa (Runde R05, Slice 124: Ä285).</summary>
public sealed partial class ViewTests
{
    private static readonly string CardRoute = $"/api/appointments/{SpaSample.Selected.Id}/card-guarantee";

    private static readonly string DepositStateRoute = $"/api/appointments/{SpaSample.Selected.Id}/deposit";

    private static readonly string RemindersRoute = $"/api/appointments/{SpaSample.Selected.Id}/reminders";

    private const string VoucherCodeSample = "GS-7K3M-Q9";

    private static CardGuaranteeStaffDto Guarantee(CardGuaranteeStatus status, bool canCharge = false, string? hint = null) =>
        new(Guid.NewGuid(), status, 27m, "EUR", 30, SpaSample.At(9), SpaSample.At(9), canCharge, hint, null, null, null);

    private static DepositStaffDto DepositState(int percent = 30) =>
        new(Guid.NewGuid(), OnlineDepositStatus.Receipted, 30m, "EUR", percent, SpaSample.At(9), SpaSample.At(9), Guid.NewGuid(), false, null, false, false, "Wird beim Kassieren verrechnet.", null, null);

    private static OnlineRefundDto Refund(Guid receiptId, bool voided = false, OnlineRefundStatus? status = null, string? error = null, bool canRetry = false) =>
        new(receiptId, OnlinePaymentSource.Deposit, 30m, "EUR", "Stripe", voided, status, null, null, error, canRetry);

    private static List<string> VisibleTexts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text!)];

    private static List<string> PanelTexts(Window window, string card) => VisibleTexts(Named<Border>(Named<Border>(window, "SidePanel"), card));

    private static bool IsShown(Window window, string name) => window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible);

    private static async Task<CalendarViewModel> OpenSelected(MainViewModel main)
    {
        var calendar = await SignedInCalendar(main);
        calendar.OpenAppointment(SpaSample.Selected);
        await Pump(Settling);
        return calendar;
    }

    // ---------- Kartengarantie ----------

    [Fact]
    public Task Termin_ohne_Hinterlegung_mit_Angebot_Klick_auf_Karte_anfordern_zeigt_Link_verschickt()
    {
        var stub = Api()
            .On($"GET {CardRoute}", null, HttpStatusCode.NoContent)
            .On($"GET {CardRoute}/offer", new CardRequestOffer(true, 27m, "EUR", 30, null))
            .On($"POST {CardRoute}/request", Guarantee(CardGuaranteeStatus.AwaitingConsent));
        return Render("hell", "termin-karte-anfordern", async (window, main) =>
        {
            var calendar = await OpenSelected(main);

            Assert.Contains("Der Standort verlangt 30 % (27,00 €). Der Gast bekommt einen Link, stimmt zu und hinterlegt eine Karte.", PanelTexts(window, "CardGuaranteeCard"));
            Assert.Equal("Karte per Link anfordern", Named<Button>(window, "RequestCardButton").Content);
            Assert.DoesNotContain(stub.Calls, c => c.Route == $"POST {CardRoute}/request");

            await ClickScrolled(window, "RequestCardButton");

            Assert.Contains(stub.Calls, c => c.Route == $"POST {CardRoute}/request");
            Assert.Contains("Link verschickt, Zustimmung offen · 27,00 € (30 %) seit 19.10.2026 09:00", PanelTexts(window, "CardGuaranteeCard"));
            Assert.Equal("Link erneut senden", Named<Button>(window, "RequestCardButton").Content);
            Assert.Equal("Link per Mail verschickt", calendar.Toast);
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Ausfallgebuehr_belasten_fragt_nach_und_ruft_charge_erst_nach_Ja()
    {
        var stub = Api()
            .On($"GET {CardRoute}", Guarantee(CardGuaranteeStatus.Saved, canCharge: true))
            .On($"POST {CardRoute}/charge", Guarantee(CardGuaranteeStatus.Charged));
        return Render("hell", "termin-ausfallgebuehr-belasten", async (window, main) =>
        {
            var calendar = await OpenSelected(main);
            Assert.Contains("Karte hinterlegt · 27,00 € (30 %) seit 19.10.2026 09:00", PanelTexts(window, "CardGuaranteeCard"));

            await ClickScrolled(window, "ChargeFeeButton");

            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Ausfallgebühr belasten?", dialog.Title);
            Assert.Contains("27,00 €", dialog.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(stub.Calls, c => c.Route == $"POST {CardRoute}/charge");

            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);

            Assert.Contains(stub.Calls, c => c.Route == $"POST {CardRoute}/charge");
            var texts = PanelTexts(window, "CardGuaranteeCard");
            Assert.Contains("Gebühr belastet · 27,00 € (30 %) seit 19.10.2026 09:00", texts);
            Assert.Contains("Beleg an der Kassa erstellt, der Gast bekommt ihn per Mail.", texts);
            Assert.False(IsShown(window, "ChargeFeeButton"));
            return calendar;
        }, stub);
    }

    [Fact]
    public Task Ausfallgebuehr_ohne_CanCharge_zeigt_den_Hinweis_der_API_und_keinen_Knopf() => Render("hell", "termin-ausfallgebuehr-nicht-faellig", async (window, main) =>
    {
        var calendar = await OpenSelected(main);

        Assert.Contains("Erst fällig, wenn der Gast nicht erscheint.", PanelTexts(window, "CardGuaranteeCard"));
        Assert.False(IsShown(window, "ChargeFeeButton"));
        return calendar;
    }, Api().On($"GET {CardRoute}", Guarantee(CardGuaranteeStatus.Saved, hint: "Erst fällig, wenn der Gast nicht erscheint.")));

    [Fact]
    public Task Ausfallgebuehr_ohne_das_Recht_Kassenaufsicht_zeigt_keinen_Knopf() => Render("hell", "termin-ausfallgebuehr-rezeption", async (window, main) =>
    {
        var calendar = await OpenSelected(main);

        Assert.Contains("Karte hinterlegt · 27,00 € (30 %) seit 19.10.2026 09:00", PanelTexts(window, "CardGuaranteeCard"));
        Assert.False(IsShown(window, "ChargeFeeButton"));
        return calendar;
    }, ReceptionApi().On($"GET {CardRoute}", Guarantee(CardGuaranteeStatus.Saved, canCharge: true)));

    [Fact]
    public Task Solange_die_Gebuehr_belastet_wird_fragt_das_Panel_nach_bis_der_Stand_da_ist()
    {
        var charged = false;
        var stub = Api().OnJson($"GET {CardRoute}", _ => charged ? Guarantee(CardGuaranteeStatus.Charged) : Guarantee(CardGuaranteeStatus.Charging));
        return Render("hell", "termin-ausfallgebuehr-laeuft", async (window, main) =>
        {
            var calendar = await OpenSelected(main);
            Assert.Contains("Gebühr wird belastet … · 27,00 € (30 %) seit 19.10.2026 09:00", PanelTexts(window, "CardGuaranteeCard"));

            charged = true;
            await Task.Delay(TimeSpan.FromSeconds(2));
            await Pump(Settling);

            Assert.Contains("Gebühr belastet · 27,00 € (30 %) seit 19.10.2026 09:00", PanelTexts(window, "CardGuaranteeCard"));
            return calendar;
        }, stub);
    }

    // ---------- Stand der Anzahlung ----------

    [Fact]
    public Task Termin_mit_Anzahlung_zeigt_Stand_Betrag_und_Prozent_auch_der_Rezeption_und_keinen_Knopf_Anfordern() => Render("hell", "termin-anzahlung-stand", async (window, main) =>
    {
        var calendar = await OpenSelected(main);

        var texts = PanelTexts(window, "DepositCard");
        Assert.Contains("Anzahlung", texts);
        Assert.Contains("Bezahlt, Anzahlungsbeleg erstellt · 30,00 € (30 %) am 19.10.2026 09:00", texts);
        Assert.Contains("Wird beim Kassieren verrechnet.", texts);
        Assert.False(IsShown(window, "ReturnDepositButton"));
        Assert.DoesNotContain(Named<Border>(window, "DepositCard").GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible);
        return calendar;
    }, ReceptionApi().On($"GET {DepositStateRoute}", DepositState()));

    [Fact]
    public Task Termin_mit_voller_Zahlung_heisst_Vorauszahlung() => Render("hell", "termin-vorauszahlung", async (window, main) =>
    {
        var calendar = await OpenSelected(main);

        Assert.Contains("Vorauszahlung", PanelTexts(window, "DepositCard"));
        return calendar;
    }, Api().On($"GET {DepositStateRoute}", DepositState(percent: 100)));

    // ---------- Erinnerungsverlauf ----------

    [Fact]
    public Task Termin_mit_versendeten_Erinnerungen_zeigt_den_Verlauf() => Render("hell", "termin-erinnerungen", async (window, main) =>
    {
        var calendar = await OpenSelected(main);

        var texts = PanelTexts(window, "RemindersCard");
        Assert.Contains("Terminerinnerungen", texts);
        Assert.Contains("1. per Mail: gesendet 19.10.2026 09:00", texts);
        Assert.Contains("2. per SMS: fehlgeschlagen (Nummer ungültig)", texts);
        return calendar;
    }, Api().On($"GET {RemindersRoute}", new List<AppointmentReminderDto>
    {
        new(1, ReminderChannel.Email, "Sent", SpaSample.At(8), SpaSample.At(9), null),
        new(2, ReminderChannel.Sms, "Failed", SpaSample.At(9), null, "Nummer ungültig"),
    }));

    [Fact]
    public Task Termin_ohne_Erinnerungen_zeigt_keine_Karte() => Render("hell", "termin-ohne-erinnerungen", async (window, main) =>
    {
        var calendar = await OpenSelected(main);

        Assert.False(IsShown(window, "RemindersCard"));
        Assert.False(IsShown(window, "CardGuaranteeCard"));
        return calendar;
    }, Api().On($"GET {RemindersRoute}", new List<AppointmentReminderDto>()));

    // ---------- Gutschein an der Kassa ----------

    private static async Task<(PosViewModel Pos, TextBox Box)> VoucherFieldAfterLeaving(Window window, MainViewModel main, string code)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);
        var page = window.GetVisualDescendants().OfType<PosView>().First();
        var box = page.GetVisualDescendants().OfType<TextBox>().First(t => t.PlaceholderText == "z. B. GS-7K3M-Q9");
        box.Focus();
        box.Text = code;
        await Pump();
        page.GetVisualDescendants().OfType<TextBox>().First(t => t != box && t.IsEffectivelyVisible && t.IsEffectivelyEnabled).Focus();
        await Pump(Settling);
        return (shell.Pos, box);
    }

    private static VoucherDto SampleVoucher(VoucherKind kind = VoucherKind.MultiPurpose, VoucherStatus status = VoucherStatus.Active, decimal balance = 50m) =>
        new(Guid.NewGuid(), VoucherCodeSample, kind, 100m, balance, null, status, new DateOnly(2027, 12, 31));

    private static string VoucherRoute => $"GET /api/pos/vouchers/{VoucherCodeSample}";

    [Fact]
    public Task Gueltiger_Gutschein_zeigt_Restwert_und_Gueltigkeit_und_schlaegt_den_Betrag_vor() => Render("hell", "kassa-gutschein-gueltig", async (window, main) =>
    {
        var (pos, _) = await VoucherFieldAfterLeaving(window, main, VoucherCodeSample);

        Assert.Contains("Restwert 50,00 € · Wertgutschein · gültig bis 31.12.2027", VisibleTexts(window.GetVisualDescendants().OfType<PosView>().First()));
        Assert.Equal("50,00", pos.Payments.First(p => p.IsVoucher).Amount);
        return pos;
    }, Api().On(VoucherRoute, SampleVoucher()));

    [Fact]
    public Task Eingeloester_Gutschein_sagt_nicht_mehr_gueltig_und_schlaegt_keinen_Betrag_vor() => Render("hell", "kassa-gutschein-eingeloest", async (window, main) =>
    {
        var (pos, _) = await VoucherFieldAfterLeaving(window, main, VoucherCodeSample);

        Assert.Contains("Restwert 0,00 € · Wertgutschein · nicht mehr gültig", VisibleTexts(window.GetVisualDescendants().OfType<PosView>().First()));
        Assert.Equal("", pos.Payments.First(p => p.IsVoucher).Amount);
        return pos;
    }, Api().On(VoucherRoute, SampleVoucher(status: VoucherStatus.Redeemed, balance: 0m)));

    [Fact]
    public Task Unbekannter_Gutschein_steht_als_Fehler_am_Feld() => Render("hell", "kassa-gutschein-unbekannt", async (window, main) =>
    {
        var (pos, _) = await VoucherFieldAfterLeaving(window, main, VoucherCodeSample);

        Assert.Contains("Diesen Gutschein-Code gibt es nicht.", VisibleTexts(window.GetVisualDescendants().OfType<PosView>().First()));
        return pos;
    }, Api());

    // ---------- Rückerstattung am Beleg ----------

    private static async Task<PosViewModel> OpenReceipts(MainViewModel main)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump();
        var pos = shell.Pos;
        pos.ShowReceiptsCommand.Execute(null);
        await pos.LoadReceiptsAsync();
        await Pump(Settling);
        return pos;
    }

    private static StubApi WithOnlineReceipt(StubApi stub, ReceiptDto receipt, OnlineRefundDto refund) => stub
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new List<ReceiptDto> { receipt })
        .On($"GET /api/pos/receipts/{receipt.Id}/online-refund", refund);

    [Fact]
    public Task Online_bezahlter_Beleg_zeigt_vor_dem_Storno_den_Weg_des_Geldes()
    {
        var receipt = SpaSample.Receipt();
        return Render("hell", "kassa-online-beleg", async (window, main) =>
        {
            var pos = await OpenReceipts(main);

            var texts = VisibleTexts(Named<Border>(window, "OnlineRefundCard"));
            Assert.Contains("Online-Anzahlung, online bezahlt über Stripe.", texts);
            Assert.Contains("Ein Storno gibt 30,00 € über den Zahlungsanbieter zurück und schickt dem Gast den Stornobeleg.", texts);
            Assert.False(IsShown(window, "RetryRefundButton"));
            return pos;
        }, WithOnlineReceipt(Api(), receipt, Refund(receipt.Id)));
    }

    [Fact]
    public Task Fehlgeschlagene_Erstattung_laesst_sich_erneut_anstossen()
    {
        var receipt = SpaSample.Receipt();
        var stub = WithOnlineReceipt(Api(), receipt, Refund(receipt.Id, voided: true, OnlineRefundStatus.Failed, "Karte abgelaufen", canRetry: true))
            .On($"POST /api/pos/receipts/{receipt.Id}/online-refund", Refund(receipt.Id, voided: true, OnlineRefundStatus.Refunded));
        return Render("hell", "kassa-erstattung-fehlgeschlagen", async (window, main) =>
        {
            var pos = await OpenReceipts(main);
            Assert.Contains("Nicht erstattet: Karte abgelaufen", window.GetVisualDescendants().OfType<AispaStudio.Desktop.Controls.InfoBar>().Where(b => b.IsEffectivelyVisible).Select(b => b.Message));

            await ClickScrolled(window, "RetryRefundButton");

            Assert.Contains(stub.Calls, c => c.Route == $"POST /api/pos/receipts/{receipt.Id}/online-refund");
            Assert.Contains("30,00 € zurückerstattet, der Gast hat den Stornobeleg per Mail.", VisibleTexts(Named<Border>(window, "OnlineRefundCard")));
            Assert.False(IsShown(window, "RetryRefundButton"));
            return pos;
        }, stub);
    }

    [Fact]
    public Task Stornobeleg_zeigt_die_Erstattung_des_stornierten_Belegs()
    {
        var originalId = Guid.NewGuid();
        var voiding = SpaSample.Receipt() with { VoidsReceiptId = originalId };
        var stub = Api()
            .On($"GET /api/pos/registers/{SpaSample.Register.Id}/receipts?date=2026-10-19", new List<ReceiptDto> { voiding })
            .On($"GET /api/pos/receipts/{originalId}/online-refund", Refund(originalId, voided: true, OnlineRefundStatus.Refunded));
        return Render("hell", "kassa-stornobeleg-erstattung", async (window, main) =>
        {
            var pos = await OpenReceipts(main);

            Assert.Contains("30,00 € zurückerstattet, der Gast hat den Stornobeleg per Mail.", VisibleTexts(Named<Border>(window, "OnlineRefundCard")));
            return pos;
        }, stub);
    }

    [Fact]
    public Task Wird_die_Auswahl_geleert_waehrend_die_Erstattung_noch_nicht_begonnen_hat_endet_das_Nachfragen()
    {
        var receipt = SpaSample.Receipt();
        return Render("hell", "kassa-erstattung-auswahl-leer", async (window, main) =>
        {
            var pos = await OpenReceipts(main);
            Assert.True(pos.IsPollingOnlineRefund);

            pos.SelectedReceiptItem = null;
            await Pump(Settling);

            Assert.False(pos.IsPollingOnlineRefund);
            Assert.False(IsShown(window, "OnlineRefundCard"));
            return pos;
        }, WithOnlineReceipt(Api(), receipt, Refund(receipt.Id, voided: true)));
    }

    [Fact]
    public Task Erstattung_erneut_anstossen_fehlt_ohne_das_Recht_Kassenaufsicht()
    {
        var receipt = SpaSample.Receipt();
        return Render("hell", "kassa-erstattung-rezeption", async (window, main) =>
        {
            var pos = await OpenReceipts(main);

            Assert.True(IsShown(window, "OnlineRefundCard"));
            Assert.False(IsShown(window, "RetryRefundButton"));
            return pos;
        }, WithOnlineReceipt(ReceptionApi(), receipt, Refund(receipt.Id, voided: true, OnlineRefundStatus.Failed, "Karte abgelaufen", canRetry: true)));
    }
}
