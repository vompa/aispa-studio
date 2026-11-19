using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Befunde der Durchsicht der Kassa (Windows-App): Fuß der Spalte „Bezahlen“, Einzug der Summe, Beschriftungen, Tooltips und Begründungen gesperrter Knöpfe.</summary>
public sealed partial class ViewTests
{
    private static Rect ClientRect(Window window) => new(new Point(0, 0), window.ClientSize);

    private static PosView PosPage(Window window) => window.GetVisualDescendants().OfType<PosView>().First();

    [Fact]
    public Task Kassa_Bezahlen_zeigt_Knopf_und_Fusstext_bei_1440_mal_900_ganz_auch_mit_den_Zahlungsdaten_der_Ueberweisung()
    {
        var stub = Api().On($"GET {TransferRoute}", SampleTransfer());
        return Render("hell", "kassa-bezahlen-fuss", async (window, main) =>
        {
            var pos = await OpenBill(window, main);
            await ClickVisible(window, "TransferDetailsButton");

            var column = InWindow(window, Named<Border>(window, "PayColumn"));
            var button = InWindow(window, Named<Button>(window, "PayButton"));
            var note = InWindow(window, Named<TextBlock>(window, "PayFooterNote"));
            var client = ClientRect(window);
            // Der Fuß steht in der Spalte und im Fenster, nichts ragt unten heraus.
            Assert.True(client.Contains(button.TopLeft) && client.Contains(button.BottomRight), $"Knopf {button}, Fenster {client}");
            Assert.True(client.Contains(note.TopLeft) && client.Contains(note.BottomRight), $"Fußtext {note}, Fenster {client}");
            Assert.True(note.Bottom <= column.Bottom + 0.5, $"Fußtext {note.Bottom}, Spalte {column.Bottom}");
            Assert.DoesNotContain(Named<TextBlock>(window, "PayFooterNote").TextLayout.TextLines, line => line.HasCollapsed);

            // Die Zahlungsdaten rollen im Körper der Spalte; der Knopf bleibt dabei stehen.
            var scroll = Named<ScrollViewer>(window, "PayScroll");
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height, $"Inhalt {scroll.Extent.Height}, sichtbar {scroll.Viewport.Height}");
            var qr = VisibleNamed<Image>(window, "TransferQrImage");
            qr.BringIntoView();
            await Pump(Settling);
            var qrRect = InWindow(window, qr);
            var viewport = InWindow(window, scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().First());
            Assert.True(qrRect.Top >= viewport.Top - 0.5 && qrRect.Bottom <= viewport.Bottom + 0.5, $"QR {qrRect}, sichtbarer Bereich {viewport}");
            Assert.Equal(button, InWindow(window, Named<Button>(window, "PayButton")));
            Assert.Contains("Zahlungstext (EPC)", VisibleTexts(PosPage(window)));
            return pos;
        }, stub);
    }

    [Fact]
    public Task Kassa_Bezahlen_zeigt_den_Fusstext_auch_im_Mindestfenster_ganz() => Render("hell", "kassa-bezahlen-fuss-minimal", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        window.Width = 1100;
        window.Height = 680;
        await Pump(Settling);

        var column = InWindow(window, Named<Border>(window, "PayColumn"));
        var note = InWindow(window, Named<TextBlock>(window, "PayFooterNote"));
        var button = InWindow(window, Named<Button>(window, "PayButton"));
        Assert.True(note.Bottom <= column.Bottom + 0.5, $"Fußtext {note.Bottom}, Spalte {column.Bottom}");
        Assert.True(button.Bottom <= note.Top, "Der Knopf steht über dem Fußtext");
        Assert.DoesNotContain(Named<TextBlock>(window, "PayFooterNote").TextLayout.TextLines, line => line.HasCollapsed);
        // Schmal rollt die Seite als Ganzes; die Spalte ist dann ganz erreichbar.
        var page = Named<ScrollViewer>(window, "PageScroll");
        Assert.True(page.Extent.Height >= column.Bottom - InWindow(window, page).Top - 0.5, $"Seite {page.Extent.Height}, Spalte bis {column.Bottom}");
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Summe_steht_mit_dem_Einzug_der_Positionen_und_davon_offen_nur_bei_Teilzahlung() => Render("hell", "kassa-summe", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);

        var bill = Named<Panel>(window, "BillColumn");
        var sum = bill.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Summe" && t.IsEffectivelyVisible);
        var firstLine = bill.GetVisualDescendants().OfType<TextBlock>().First(t => t.Inlines is { Count: > 0 } && t.IsEffectivelyVisible);
        Assert.Equal(LeftOf(window, firstLine), LeftOf(window, sum), 1);
        // Voll offen ist „davon offen“ dieselbe Zahl wie die Summe und fehlt; der Rest steht rechts als „Noch offen“.
        Assert.False(shell.Pos.ShowOpenRemainder);
        Assert.False(Named<TextBlock>(window, "BillOpenLabel").IsEffectivelyVisible);
        Assert.StartsWith("Noch offen:", shell.Pos.CoverText, StringComparison.Ordinal);
        return shell.Pos;
    });

    [Fact]
    public async Task Kassa_zeigt_davon_offen_bei_einer_Teilzahlung()
    {
        var partial = SpaSample.Bill() with { Paid = 50m, Open = SpaSample.Bill().Total - 50m };
        var pos = new PosViewModel(PosApi().On("POST /api/pos/bills", partial).Client(), new TestTime());
        await pos.InitializeAsync(SpaSample.LocationId);
        await pos.AddAppointmentAsync(SpaSample.Paying);

        Assert.True(pos.ShowOpenRemainder);
        Assert.Equal("88,80 €", pos.OpenText);
    }

    private static StubApi PosApi() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna })
        .On("GET /api/pos/registers", new List<CashRegisterDto> { SpaSample.Register })
        .On("GET /api/pos/articles", SpaSample.Articles)
        .On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", SpaSample.DayList())
        .On($"GET /api/pos/registers/{SpaSample.Register.Id}/bills", new List<BillDto>())
        .On($"POST /api/pos/bills/{SpaSample.BillId}/checkout", SpaSample.Receipt());

    private sealed class TestTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SpaSample.At(11, 40);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    [Fact]
    public Task Kassa_zeigt_nach_dem_Bezahlen_den_Stand_statt_der_leeren_Maske_und_danach_wieder_die_Maske() => Render("hell", "kassa-bezahlt-stand", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "89";
        await shell.Pos.PayCommand.ExecuteAsync(null);
        await Pump(Settling);

        Assert.Equal("Bezahlt · Beleg Nr. 1842", shell.Pos.PaidText);
        Assert.Contains("Bezahlt · Beleg Nr. 1842", VisibleTexts(Named<Border>(window, "PayColumn")));
        Assert.False(Named<ScrollViewer>(window, "PayScroll").IsEffectivelyVisible);
        Assert.False(Named<Button>(window, "PayButton").IsEffectivelyVisible);

        // Mit der nächsten Rechnung ist die Maske wieder da.
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);
        Assert.True(shell.Pos.ShowPayForm);
        Assert.True(Named<Button>(window, "PayButton").IsEffectivelyVisible);
        Assert.False(Named<StackPanel>(window, "PaidState").IsEffectivelyVisible);
        return shell.Pos;
    });

    [Fact]
    public void Gutschein_Code_ist_erst_mit_einem_Betrag_ueber_0_Pflicht()
    {
        var entry = new PaymentEntry(PaymentMethod.Voucher, () => { });
        Assert.False(entry.IsVoucherCodeRequired);
        entry.Amount = "0,00";
        Assert.False(entry.IsVoucherCodeRequired);
        entry.Amount = "50";
        Assert.True(entry.IsVoucherCodeRequired);
        Assert.False(new PaymentEntry(PaymentMethod.Cash, () => { }) { Amount = "50" }.IsVoucherCodeRequired);
    }

    [Fact]
    public Task Kassa_Karte_am_Terminal_nennt_den_Grund_der_Sperre_und_den_Knopf_Kartenzahlung_stornieren() => Render("hell", "kassa-terminal-sperre", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        var card = shell.Pos.Payments.First(p => p.IsCard);
        await shell.Pos.StartCardPaymentCommand.ExecuteAsync(card);
        await Pump(Settling);

        Assert.Equal("Am Terminal bezahlt. Zum Ändern die Kartenzahlung stornieren.", card.LockedHint);
        var column = Named<Border>(window, "PayColumn");
        var field = column.GetVisualDescendants().OfType<MoneyField>().First(f => f.DataContext == card);
        Assert.True(field.IsReadOnly);
        Assert.Equal(card.LockedHint, ToolTip.GetTip(field));
        var cancel = ButtonWithText(column, "Kartenzahlung stornieren");
        var inColumn = InWindow(window, column);
        var rect = InWindow(window, cancel);
        Assert.True(rect.Left >= inColumn.Left && rect.Right <= inColumn.Right, $"Knopf {rect}, Spalte {inColumn}");
        Assert.True(card.TerminalText.StartsWith("Genehmigt ·", StringComparison.Ordinal), "Kein Umbruch vor dem Punkt");
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Belege_nennt_Stornobeleg_und_storniert_und_zu_welchem_Beleg_sie_gehoeren() => Render("hell", "kassa-belege-storno", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.ShowReceiptsCommand.Execute(null);
        await shell.Pos.LoadReceiptsAsync();
        await Pump(Settling);

        var voidItem = shell.Pos.Receipts.Single(r => r.IsVoid);
        var voided = shell.Pos.Receipts.Single(r => r.IsVoided);
        Assert.Equal("Storniert Beleg Nr. 1840", voidItem.Relation);
        Assert.Equal("Storniert mit Stornobeleg Nr. 1841", voided.Relation);
        Assert.Null(shell.Pos.Receipts.First(r => !r.IsVoid && !r.IsVoided).Relation);
        var texts = VisibleTexts(PosPage(window));
        Assert.Contains("Stornobeleg", texts);
        Assert.Contains("storniert", texts);
        Assert.DoesNotContain("Storno", texts);
        var pills = PosPage(window).GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("pill") && b.IsEffectivelyVisible && b.Child is TextBlock { Text: "Stornobeleg" or "storniert" }).ToList();
        Assert.Equal(["Storniert Beleg Nr. 1840", "Storniert mit Stornobeleg Nr. 1841"], pills.Select(p => ToolTip.GetTip(p) as string).Order(StringComparer.Ordinal));
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Stornieren_ist_voll_sichtbar_und_nennt_bei_gesperrtem_Knopf_den_Grund_als_Satz() => Render("hell", "kassa-stornieren-sichtbar", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.ShowReceiptsCommand.Execute(null);
        await shell.Pos.LoadReceiptsAsync();
        shell.Pos.OwnerLevel = false;
        var first = shell.Pos.Receipts[0];
        shell.Pos.SelectedReceiptItem = first with { Receipt = first.Receipt with { IsDepositReceipt = true } };
        shell.Pos.VoidReasonInput = "Falsch kassiert";
        await Pump(Settling);

        var button = Named<Button>(window, "VoidButton");
        Assert.False(button.IsEffectivelyEnabled);
        Assert.Equal(ReceiptViewModel.DepositVoidBlocked, ToolTip.GetTip(button));
        var note = Named<TextBlock>(window, "VoidBlockedNote");
        Assert.True(note.IsEffectivelyVisible);
        Assert.Equal(ReceiptViewModel.DepositVoidBlocked, note.Text);
        var rect = InWindow(window, button);
        Assert.True(ClientRect(window).Contains(rect.BottomRight), $"Knopf {rect}, Fenster {ClientRect(window)}");
        var noteRect = InWindow(window, note);
        Assert.True(ClientRect(window).Contains(noteRect.BottomRight), $"Satz {noteRect}, Fenster {ClientRect(window)}");
        // Die Karten stehen nebeneinander, wo Platz ist, damit der Knopf nicht am unteren Rand klebt.
        Assert.Equal(TopOf(window, Named<Border>(window, "VoidCard")), TopOf(window, Named<WrapPanel>(window, "ReceiptCards").Children.OfType<Border>().First(b => b.IsVisible)), 1);

        // Ein Grund genügt bei freier Rolle: der Satz verschwindet, der Knopf ist bedienbar.
        shell.Pos.OwnerLevel = true;
        shell.Pos.SelectedReceiptItem = first;
        shell.Pos.VoidReasonInput = "Falsch kassiert";
        await Pump(Settling);
        Assert.True(Named<Button>(window, "VoidButton").IsEffectivelyEnabled);
        Assert.False(Named<TextBlock>(window, "VoidBlockedNote").IsEffectivelyVisible);
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Kassenlade_nennt_Fragezeichen_Erklaerungen_und_den_Grund_fuer_Buchen() => Render("hell", "kassa-abschluss-hilfe", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.ShowClosingCommand.Execute(null);
        await shell.Pos.LoadReportAsync();
        await Pump(Settling);

        var page = PosPage(window);
        var topics = page.GetVisualDescendants().OfType<HelpButton>().Where(b => b.IsEffectivelyVisible).Select(b => $"{b.Topic}#{b.Section}").ToList();
        Assert.Contains("kassa-tagesabschluss#schritte", topics);
        Assert.Contains("kassa-einlage-entnahme#rechte", topics);
        string? TipOf(string text) => page.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text && t.IsEffectivelyVisible) is { } t ? ToolTip.GetTip(t) as string : null;
        Assert.Equal("Das Bargeld, das laut Kassa in der Lade sein muss", TipOf("Soll-Bestand"));
        Assert.Equal("Der Umsatz, aufgeteilt auf die Konten der Buchhaltung", TipOf("Erlöskonten"));
        var book = ButtonWithText(page, "Buchen");
        Assert.False(book.IsEffectivelyEnabled);
        Assert.Equal("Verfügbar, sobald Betrag und Grund eingetragen sind.", ToolTip.GetTip(book));
        Assert.EndsWith("4 Belege, 1 Storno", shell.Pos.ReportPeriod, StringComparison.Ordinal);
        Assert.Contains(page.GetVisualDescendants().OfType<FormField>(), f => f.Label == "Betrag (€)" && f.IsEffectivelyVisible);
        return shell.Pos;
    });

    [Fact]
    public Task Kassa_Kopf_und_Symbolknoepfe_tragen_einen_zugaenglichen_Namen() => Render("hell", "kassa-kopf-namen", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        shell.Pos.Payments.First(p => p.Method == PaymentMethod.Card).Amount = "89";
        await shell.Pos.PayCommand.ExecuteAsync(null);
        await Pump(Settling);

        var page = PosPage(window);
        var buttons = page.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("icon")).ToList();
        Assert.NotEmpty(buttons);
        Assert.All(buttons, b =>
        {
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)), $"Symbolknopf ohne Namen: {b.Name} {ToolTip.GetTip(b)}");
            Assert.NotNull(ToolTip.GetTip(b));
        });
        Assert.Contains(buttons, b => AutomationProperties.GetName(b) == "Aktualisieren");
        Assert.Contains(buttons, b => AutomationProperties.GetName(b) == "Beleg schließen");
        return shell.Pos;
    });
}
