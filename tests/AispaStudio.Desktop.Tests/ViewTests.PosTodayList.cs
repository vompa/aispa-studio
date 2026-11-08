using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kassa mit „Heute abzurechnen“ als Hauptliste links (Slice 137, Ä344, Ä345). Alle Fälle als Inhaberin (<c>Owner()</c> der Bildtests).</summary>
public sealed partial class ViewTests
{
    private static readonly AppointmentDto AromaDone = SpaSample.Paying with
    {
        Id = Guid.NewGuid(),
        CustomerName = "Christian Steiner",
        TreatmentName = "Aroma-Ganzkörpermassage",
        Start = SpaSample.At(9),
        End = SpaSample.At(9, 50),
        Status = AppointmentStatus.Completed,
    };

    private static readonly AppointmentDto FacialDone = SpaSample.Paying with
    {
        Id = Guid.NewGuid(),
        CustomerName = "Brigitte Müller",
        TreatmentName = "Gesichtsbehandlung",
        Start = SpaSample.At(9, 30),
        End = SpaSample.At(10, 20),
        Status = AppointmentStatus.Completed,
    };

    private static readonly AppointmentDto LaterToday = SpaSample.Paying with
    {
        Id = Guid.NewGuid(),
        CustomerName = "Eva Muster",
        TreatmentName = "Hot-Stone-Massage",
        Start = SpaSample.At(14),
        End = SpaSample.At(15),
        Status = AppointmentStatus.Booked,
    };

    private static readonly AppointmentDto PaidEarlier = SpaSample.Paying with
    {
        Id = Guid.NewGuid(),
        CustomerName = "Paul Huber",
        TreatmentName = "Rückenmassage",
        Start = SpaSample.At(8),
        End = SpaSample.At(8, 50),
        Status = AppointmentStatus.Completed,
    };

    /// <summary>Tagesliste wie vom Server: zwei Wartende (fertig seit ihrem Ende), ein späterer Termin, ein bezahlter.</summary>
    private static PosDayListDto TodayList() => new(
        [SpaSample.DayEntry(AromaDone) with { FinishedAt = AromaDone.End }, SpaSample.DayEntry(FacialDone) with { FinishedAt = FacialDone.End }],
        [SpaSample.DayEntry(LaterToday)],
        [SpaSample.DayEntry(PaidEarlier)]);

    private static StubApi TodayApi() => Api().On($"GET /api/pos/locations/{SpaSample.LocationId}/day-list?date=2026-10-19", TodayList());

    /// <summary>Öffnet die Kassa über die Navigation, ohne offene Rechnung; auf Wunsch mit „KI und Hilfe“.</summary>
    private static async Task<(PosView Page, ShellViewModel Shell)> OpenPos(Window window, MainViewModel main, double width, bool companion = false)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        window.Width = width;
        if (companion)
        {
            shell.Companion.ToggleCommand.Execute(null);
        }

        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Pos);
        await Pump(Settling);
        await shell.Pos.LoadDayAsync();
        await Pump(Settling);
        return (window.GetVisualDescendants().OfType<PosView>().First(), shell);
    }

    private static List<string> VisibleTextsIn(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text)).Select(t => t.Text!)];

    [Fact]
    public Task Beim_Oeffnen_stehen_zwei_unter_Wartet_auf_Abrechnung_eine_unter_Spaeter_heute_und_Erledigt_1_zu() => Render("hell", "kassa-heute-abzurechnen", async (window, main) =>
    {
        var (page, shell) = await OpenPos(window, main, 1600);

        var texts = VisibleTextsIn(Named<ListBox>(page, "TodayList"));
        int At(string text) => texts.IndexOf(text);
        Assert.True(At("Wartet auf Abrechnung") == 0, string.Join(" | ", texts));
        Assert.True(At("Christian Steiner") > 0 && At("Christian Steiner") < At("Brigitte Müller"), string.Join(" | ", texts));
        Assert.True(At("Brigitte Müller") < At("Später heute") && At("Später heute") < At("Eva Muster"), string.Join(" | ", texts));
        Assert.Contains("fertig seit 09:50", texts);
        Assert.DoesNotContain("Paul Huber", texts);

        Assert.Equal("Erledigt (1)", Named<TextBlock>(page, "DoneTitleText").Text);
        Assert.False(Named<AccordionHeader>(page, "DoneHeader").IsChecked);
        Assert.False(Named<ItemsControl>(page, "DoneList").IsEffectivelyVisible);
        return shell.Pos;
    }, TodayApi());

    [Fact]
    public Task Bei_1280_px_stehen_Christian_Steiner_und_Aroma_Ganzkoerpermassage_ungekuerzt_in_der_Zeile() => Render("hell", "kassa-heute-1280", async (window, main) =>
    {
        var (page, shell) = await OpenPos(window, main, 1280);

        var list = Named<ListBox>(page, "TodayList");
        var name = list.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Christian Steiner");
        var treatment = list.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text?.StartsWith("Aroma-Ganzkörpermassage", StringComparison.Ordinal) == true);
        // Name auf einer Zeile, die Behandlung bricht erst nach ihrem ganzen Namen um (nicht am Bindestrich, nicht mit „…“).
        Assert.Single(name.TextLayout.TextLines);
        Assert.DoesNotContain(name.TextLayout.TextLines, line => line.HasCollapsed);
        Assert.True(treatment.TextLayout.TextLines[0].Length >= "Aroma-Ganzkörpermassage".Length, $"erste Zeile: {treatment.TextLayout.TextLines[0].Length} Zeichen");
        Assert.DoesNotContain(treatment.TextLayout.TextLines, line => line.HasCollapsed);
        return shell.Pos;
    }, TodayApi());

    [Fact]
    public Task Pfeil_nach_unten_und_Enter_im_Suchfeld_oeffnen_die_Rechnung_des_ersten_Wartenden()
    {
        var stub = TodayApi();
        return Render("hell", "kassa-heute-tastatur", async (window, main) =>
        {
            var (_, shell) = await OpenPos(window, main, 1600);
            Assert.Same(Named<TextBox>(window, "PosScanField"), Focused(window));

            Press(window, PhysicalKey.ArrowDown);
            await Pump(Settling);
            Press(window, PhysicalKey.Enter);
            await Pump(Settling);

            var created = stub.Calls.Last(c => c.Route == "POST /api/pos/bills").Body!;
            Assert.Contains($"\"appointmentIds\":[\"{AromaDone.Id}\"]", created, StringComparison.Ordinal);
            Assert.NotNull(shell.Pos.Bill);
            return shell.Pos;
        }, stub);
    }

    [Fact]
    public Task Klick_auf_die_Gruppenueberschrift_setzt_keinen_Gast_auf_die_Rechnung_Klick_auf_die_Zeile_schon()
    {
        var stub = TodayApi();
        return Render("hell", "kassa-heute-ueberschrift", async (window, main) =>
        {
            var (page, shell) = await OpenPos(window, main, 1600);
            var list = Named<ListBox>(page, "TodayList");

            ClickCenter(window, list.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == "Wartet auf Abrechnung"));
            await Pump(Settling);
            Assert.DoesNotContain(stub.Calls, c => c.Route == "POST /api/pos/bills");

            ClickCenter(window, list.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Christian Steiner"));
            await Pump(Settling);
            Assert.Contains($"\"appointmentIds\":[\"{AromaDone.Id}\"]", stub.Calls.Last(c => c.Route == "POST /api/pos/bills").Body!, StringComparison.Ordinal);
            return shell.Pos;
        }, stub);
    }

    [Fact]
    public Task Mit_KI_und_Hilfe_bei_1920_px_ist_Heute_abzurechnen_mindestens_380_px_breit() => Render("hell", "kassa-heute-1920-ki", async (window, main) =>
    {
        var (page, shell) = await OpenPos(window, main, 1920, companion: true);

        Assert.True(shell.Companion.IsOpen);
        Assert.True(Width(page, "TodayColumn") >= 380, $"Liste {Width(page, "TodayColumn")}");
        return shell.Pos;
    }, TodayApi());

    [Fact]
    public Task Bei_1280_px_mit_KI_und_Hilfe_steht_Bezahlen_unter_der_Rechnung_und_die_Liste_bleibt_380_px() => Render("hell", "kassa-heute-1280-ki", async (window, main) =>
    {
        var (page, shell) = await OpenPos(window, main, 1280, companion: true);
        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);

        var bill = Named<Control>(page, "BillColumn");
        var pay = Named<Control>(page, "PayColumn");
        Assert.True(Width(page, "TodayColumn") >= 380, $"Liste {Width(page, "TodayColumn")}");
        Assert.Equal(Left(window, bill), Left(window, pay), 1);
        Assert.True(pay.TranslatePoint(new Point(0, 0), window)!.Value.Y > bill.TranslatePoint(new Point(0, 0), window)!.Value.Y, "Bezahlen steht unter der Rechnung");
        Assert.True(Left(window, pay) + pay.Bounds.Width <= Left(window, page) + page.Bounds.Width + 1, "Bezahlen ragt nicht aus der Seite");
        // Bezahlen drückt die Rechnung nicht zusammen: sie behält ihre Höhe, die Seite rollt, und Bezahlen ist ganz erreichbar.
        Assert.True(bill.Bounds.Height >= 200, $"Rechnung {bill.Bounds.Height} px hoch");
        var scroll = Named<ScrollViewer>(page, "PageScroll");
        Assert.True(scroll.Extent.Height >= pay.Bounds.Bottom, $"Seite {scroll.Extent.Height}, Bezahlen endet bei {pay.Bounds.Bottom}");
        return shell.Pos;
    }, TodayApi());

    [Fact]
    public Task Beim_Oeffnen_der_Kassa_liegt_der_Fokus_im_Suchfeld() => Render("hell", "kassa-heute-fokus", async (window, main) =>
    {
        var (_, shell) = await OpenPos(window, main, 1600);

        Assert.Same(Named<TextBox>(window, "PosScanField"), Focused(window));
        return shell.Pos;
    }, TodayApi());

    private static TabButton PosTab(Control page, string text) => page.GetVisualDescendants().OfType<TabButton>().First(t => t.Content as string == text);

    private static Button ArticleButton(Control page, string name) =>
        Named<ItemsControl>(page, "ArticleList").GetVisualDescendants().OfType<Button>().First(b => b.DataContext is ArticleDto a && a.Name == name);

    /// <summary>T-027: Zurück von „Belege“ auf „Kassieren“ steht der Cursor wieder im Suchfeld, nicht auf dem Reiter.</summary>
    [Fact]
    public Task Nach_Belege_und_zurueck_auf_Kassieren_liegt_der_Fokus_im_Suchfeld() => Render("hell", "kassa-heute-fokus-reiter", async (window, main) =>
    {
        var (page, shell) = await OpenPos(window, main, 1600);

        ClickCenter(window, PosTab(page, "Belege"));
        await Pump(Settling);
        Assert.True(shell.Pos.IsReceipts);
        ClickCenter(window, PosTab(page, "Kassieren"));
        await Pump(Settling);

        Assert.True(shell.Pos.IsCheckout);
        Assert.Same(Named<TextBox>(window, "PosScanField"), Focused(window));
        return shell.Pos;
    }, TodayApi());

    /// <summary>T-027: Passt keine Zeile, aber ein Artikel, führt Pfeil nach unten zum Artikel; Enter setzt ihn auf die Rechnung.</summary>
    [Fact]
    public Task Passt_keine_Zeile_fuehrt_Pfeil_nach_unten_in_die_Artikel_und_Enter_setzt_den_Artikel_auf_die_Rechnung()
    {
        var stub = TodayApi();
        return Render("hell", "kassa-heute-artikel-tastatur", async (window, main) =>
        {
            var (page, shell) = await OpenPos(window, main, 1600);
            shell.Pos.ScanText = "Ingwer";
            await Pump(Settling);
            Assert.Empty(shell.Pos.Unbilled);

            Press(window, PhysicalKey.ArrowDown);
            await Pump(Settling);
            Assert.Same(ArticleButton(page, "Ingwertee"), Focused(window));

            Press(window, PhysicalKey.Enter);
            await Pump(Settling);
            Assert.Contains(stub.Calls, c => c.Route == "POST /api/pos/bills");
            return shell.Pos;
        }, stub);
    }

    /// <summary>T-027: In den Artikeln wählen die Pfeiltasten weiter; Pfeil nach oben auf dem ersten und Esc führen zurück ins Suchfeld.</summary>
    [Fact]
    public Task In_den_Artikeln_fuehrt_Pfeil_nach_oben_auf_dem_ersten_zurueck_ins_Suchfeld() => Render("hell", "kassa-heute-artikel-pfeile", async (window, main) =>
    {
        var (page, shell) = await OpenPos(window, main, 1600);
        var field = Named<TextBox>(window, "PosScanField");
        shell.Pos.ScanText = "Verleih";
        await Pump(Settling);
        var names = shell.Pos.ArticleHits.Select(a => a.Name).ToList();
        Assert.Equal(2, names.Count);

        Press(window, PhysicalKey.ArrowDown);
        Press(window, PhysicalKey.ArrowDown);
        await Pump(Settling);
        Assert.Same(ArticleButton(page, names[1]), Focused(window));

        Press(window, PhysicalKey.ArrowUp);
        await Pump(Settling);
        Assert.Same(ArticleButton(page, names[0]), Focused(window));
        Press(window, PhysicalKey.ArrowUp);
        await Pump(Settling);
        Assert.Same(field, Focused(window));

        Press(window, PhysicalKey.ArrowDown);
        await Pump(Settling);
        Press(window, PhysicalKey.Escape);
        await Pump(Settling);
        Assert.Same(field, Focused(window));
        return shell.Pos;
    }, TodayApi());

    /// <summary>T-032: Kassa auf „Belege“ stehen lassen und zum Kalender wechseln, wie die Rezeption es tut, bevor sie dort kassiert.</summary>
    private static async Task<ShellViewModel> LeavePosOnReceipts(Window window, MainViewModel main)
    {
        var (_, shell) = await OpenPos(window, main, 1600);
        await shell.Pos.ShowReceiptsCommand.ExecuteAsync(null);
        await Pump(Settling);
        shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Calendar);
        await Pump(Settling);
        return shell;
    }

    /// <summary>T-032: Nach einem Kassieren von außen steht die Kassa auf „Kassieren“, und die Rechnung ist zu sehen.</summary>
    private static void AssertBillOnCheckout(Window window, ShellViewModel shell)
    {
        Assert.True(shell.Pos.IsCheckout, $"Reiter {shell.Pos.Mode}");
        Assert.NotNull(shell.Pos.Bill);
        Assert.True(Named<Control>(window, "BillColumn").IsEffectivelyVisible);
    }

    [Fact]
    public Task Kassieren_aus_dem_Kalender_waehrend_Belege_zeigt_die_Rechnung_auf_Kassieren() => Render("hell", "kassa-von-aussen-termin", async (window, main) =>
    {
        var shell = await LeavePosOnReceipts(window, main);

        await shell.CheckoutAsync(SpaSample.Paying);
        await Pump(Settling);

        AssertBillOnCheckout(window, shell);
        return shell.Pos;
    }, TodayApi());

    [Fact]
    public Task Gruppe_kassieren_waehrend_Belege_zeigt_Kassieren() => Render("hell", "kassa-von-aussen-gruppe", async (window, main) =>
    {
        var shell = await LeavePosOnReceipts(window, main);

        await shell.CheckoutGroupAsync([SpaSample.Paying]);
        await Pump(Settling);

        AssertBillOnCheckout(window, shell);
        return shell.Pos;
    }, TodayApi());

    [Fact]
    public Task Eintritt_kassieren_waehrend_Belege_zeigt_Kassieren() => Render("hell", "kassa-von-aussen-eintritt", async (window, main) =>
    {
        var shell = await LeavePosOnReceipts(window, main);

        await shell.CheckoutVisitAsync(SpaSample.UnbilledVisits()[0]);
        await Pump(Settling);

        AssertBillOnCheckout(window, shell);
        return shell.Pos;
    }, TodayApi());

    [Fact]
    public Task Offene_Rechnung_aus_dem_Assistenten_waehrend_Belege_zeigt_Kassieren() => Render("hell", "kassa-von-aussen-assistent", async (window, main) =>
    {
        var (_, shell) = await OpenPos(window, main, 1600);
        await shell.Pos.ShowReceiptsCommand.ExecuteAsync(null);
        await Pump(Settling);

        await shell.OpenAssistantOrderAsync(new AssistantSuggestion(AssistantSuggestionKind.OpenBill, "Rechnung öffnen", SpaSample.LocationId, null, null, null, null, null,
            TargetId: SpaSample.BillId));
        await Pump(Settling);

        AssertBillOnCheckout(window, shell);
        return shell.Pos;
    }, TodayApi().On($"GET /api/pos/bills/{SpaSample.BillId}", SpaSample.Bill()));
}
