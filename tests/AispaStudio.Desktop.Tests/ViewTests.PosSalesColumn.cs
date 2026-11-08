using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Kassa mit einklappbarem Panel „Artikel und Gutscheine“ (Slice 129, Ä310), seit Slice 137 rechts neben der Rechnung und zu Beginn zugeklappt (Ä345). Alle Fälle als Inhaberin (<c>Owner()</c> der Bildtests).</summary>
public sealed partial class ViewTests
{
    private static async Task<(PosView Page, ShellViewModel Shell)> OpenCheckoutAt1280(Window window, MainViewModel main, bool salesOpen = false, double width = 1280)
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        shell.Pos.IsSalesColumnOpen = salesOpen;
        await shell.CheckoutAsync(SpaSample.Paying);
        window.Width = width;
        await Pump(Settling);
        return (window.GetVisualDescendants().OfType<PosView>().First(), shell);
    }

    private static double Width(Control page, string name) => Named<Control>(page, name).Bounds.Width;

    private static double Left(Window window, Control control) => control.TranslatePoint(new Point(0, 0), window)!.Value.X;

    [Fact]
    public Task Kassieren_zeigt_links_die_Liste_daneben_Rechnung_Artikel_und_Gutscheine_und_Bezahlen() => Render("hell", "kassa-spalten", async (window, main) =>
    {
        var (page, shell) = await OpenCheckoutAt1280(window, main, salesOpen: true, width: 1600);
        shell.Pos.OpenBills.Add(SpaSample.Bill());
        await Pump(Settling);

        var today = Named<Control>(page, "TodayColumn");
        var bill = Named<Control>(page, "BillColumn");
        var sales = Named<Control>(page, "SalesColumn");
        var pay = Named<Control>(page, "PayColumn");
        Assert.True(Left(window, today) < Left(window, bill));
        Assert.True(Left(window, bill) < Left(window, sales));
        Assert.True(Left(window, sales) < Left(window, pay));

        bool In(Control column, Func<Control, bool> match) => column.GetVisualDescendants().OfType<Control>().Any(c => c.IsEffectivelyVisible && match(c));
        bool Text(Control column, string text) => In(column, c => c is TextBlock { Text: { } t } && t == text);
        Assert.True(In(sales, c => c is Button { DataContext: ArticleDto }));
        Assert.True(Text(sales, "Wertgutschein verkaufen"));
        Assert.True(In(sales, c => c is Button { Content: "Behandlung verkaufen" }));
        Assert.True(In(sales, c => c is Button { Content: "Paket verkaufen" }));
        Assert.False(In(sales, c => c.Name == "PosScanField"));
        Assert.True(In(today, c => c.Name == "PosScanField"));
        Assert.True(Text(today, "Heute abzurechnen"));
        Assert.True(Text(today, "Geparkte Rechnungen"));
        Assert.False(In(today, c => c is Button { DataContext: ArticleDto }));
        Assert.False(In(today, c => c is Button { Content: "Paket verkaufen" }));
        return shell.Pos;
    });

    [Fact]
    public Task Bei_1280_px_bleiben_Rechnung_und_Bezahlen_voll_nutzbar_und_Liste_und_Panel_rollen_fuer_sich() => Render("hell", "kassa-spalten-1280", async (window, main) =>
    {
        var (page, shell) = await OpenCheckoutAt1280(window, main, salesOpen: true);

        Assert.True(Width(page, "BillColumn") >= 310, $"Rechnung {Width(page, "BillColumn")}, Seite {page.Bounds.Width}, Liste {Width(page, "TodayColumn")}, Panel {Width(page, "SalesColumn")}");
        Assert.InRange(Width(page, "PayColumn"), 339, 341);
        Assert.True(Width(page, "TodayColumn") >= 380, $"Liste {Width(page, "TodayColumn")}");
        var salesScroll = Named<ScrollViewer>(page, "SalesScroll");
        var todayScroll = Named<ScrollViewer>(page, "TodayScroll");
        Assert.False(salesScroll.IsVisualAncestorOf(todayScroll) || todayScroll.IsVisualAncestorOf(salesScroll));
        Assert.Equal(ScrollBarVisibility.Disabled, salesScroll.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, todayScroll.HorizontalScrollBarVisibility);
        Assert.True(Left(window, Named<Control>(page, "PayColumn")) + Width(page, "PayColumn") <= window.Width);
        // Rechnung und Panel behalten ihre Höhe, auch wenn Bezahlen unter ihnen steht.
        Assert.True(Named<Control>(page, "BillColumn").Bounds.Height >= 200, $"Rechnung {Named<Control>(page, "BillColumn").Bounds.Height} px hoch");
        Assert.True(Named<Control>(page, "SalesColumn").Bounds.Height >= 200, $"Panel {Named<Control>(page, "SalesColumn").Bounds.Height} px hoch");
        return shell.Pos;
    });

    [Fact]
    public Task Klick_auf_den_Kopf_klappt_das_Panel_ein_und_die_Rechnung_wird_breiter() => Render("hell", "kassa-spalte-zu", async (window, main) =>
    {
        // Bei 1600 px bleibt Bezahlen in beiden Zuständen neben der Rechnung; bei 1280 px rückt es mit offenem Panel unter die Rechnung (eigener Test).
        var (page, shell) = await OpenCheckoutAt1280(window, main, salesOpen: true, width: 1600);
        var header = Named<AccordionHeader>(page, "SalesColumnHeader");
        var billBefore = Width(page, "BillColumn");
        var listBefore = Width(page, "TodayColumn");
        var salesBefore = Width(page, "SalesColumn");
        Assert.True(header.IsChecked);
        // Der Titel steht ganz da, nicht mit „…“ gekürzt.
        Assert.DoesNotContain(Named<TextBlock>(page, "SalesColumnTitle").TextLayout.TextLines, line => line.HasCollapsed);
        Assert.True(Width(page, "SalesColumn") > 200);

        ClickCenter(window, header);
        await Pump(Settling);

        Assert.False(shell.Pos.IsSalesColumnOpen);
        Assert.False(header.IsChecked);
        Assert.InRange(Width(page, "SalesColumn"), 30, 60);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.DataContext is ArticleDto);
        Assert.True(header.IsEffectivelyVisible);
        // Den frei gewordenen Platz teilen Rechnung und Liste (Anteile 3* und 2*, Slice 137); die Rechnung wird deutlich breiter.
        Assert.True(Width(page, "BillColumn") >= billBefore + 100, $"{billBefore} -> {Width(page, "BillColumn")}");
        Assert.Equal(billBefore + listBefore + salesBefore - Width(page, "SalesColumn"), Width(page, "BillColumn") + Width(page, "TodayColumn"), 1);

        ClickCenter(window, header);
        await Pump(Settling);

        Assert.True(shell.Pos.IsSalesColumnOpen);
        Assert.True(Width(page, "SalesColumn") > 200);
        Assert.Equal(billBefore, Width(page, "BillColumn"), 1);

        // Das Bild für das Handbuch zeigt die eingeklappte Leiste.
        ClickCenter(window, header);
        await Pump(Settling);
        Assert.False(shell.Pos.IsSalesColumnOpen);
        return shell.Pos;
    });

    [Fact]
    public Task Aufgeklappt_bleibt_das_Panel_nach_neuem_Start_aufgeklappt() => Render("hell", "kassa-spalte-auf-neustart", async (window, main) =>
    {
        var store = new MemorySettingsStore();
        var first = new PosViewModel(new StubApi().Client(), new SampleTime(), store) { IsSalesColumnOpen = true };
        Assert.True(store.Load().PosSalesPanelOpen);

        var restarted = new PosViewModel(new StubApi().Client(), new SampleTime(), store);
        var other = new Window { Width = 1280, Height = 800, Content = new PosView { DataContext = restarted } };
        other.Show();
        await Pump(Settling);
        var page = other.GetVisualDescendants().OfType<PosView>().First();

        Assert.True(restarted.IsSalesColumnOpen);
        Assert.True(Named<AccordionHeader>(page, "SalesColumnHeader").IsChecked);
        Assert.True(Width(page, "SalesColumn") > 200);
        other.Close();
        return first;
    });

    [Fact]
    public Task Leertaste_auf_dem_Kopf_klappt_ein_und_der_Peer_meldet_eingeklappt() => Render("hell", "kassa-spalte-taste", async (window, main) =>
    {
        var (page, shell) = await OpenCheckoutAt1280(window, main, salesOpen: true);
        var header = Named<AccordionHeader>(page, "SalesColumnHeader");
        Assert.Equal("Artikel und Gutscheine", ControlAutomationPeer.CreatePeerForElement(header).GetName());
        Assert.Equal(ExpandCollapseState.Expanded, StateOf(header));

        header.Focus(NavigationMethod.Tab);
        await Pump();
        Press(window, PhysicalKey.Space);
        await Pump(Settling);

        Assert.False(shell.Pos.IsSalesColumnOpen);
        Assert.Equal(ExpandCollapseState.Collapsed, StateOf(header));
        Assert.Equal("Artikel und Gutscheine", ControlAutomationPeer.CreatePeerForElement(header).GetName());
        Assert.Same(header, Focused(window));

        Press(window, PhysicalKey.Space);
        await Pump(Settling);

        Assert.True(shell.Pos.IsSalesColumnOpen);
        Assert.Equal(ExpandCollapseState.Expanded, StateOf(header));
        Assert.Same(header, Focused(window));
        return shell.Pos;
    });

    [Fact]
    public Task Artikel_und_Gutscheine_ist_beim_ersten_Start_zugeklappt() => Render("hell", "kassa-spalte-start", async (window, main) =>
    {
        await SignedInCalendar(main);
        var shell = (ShellViewModel)main.Content!;
        await shell.CheckoutAsync(SpaSample.Paying);
        window.Width = 1280;
        await Pump(Settling);
        var page = window.GetVisualDescendants().OfType<PosView>().First();

        Assert.False(shell.Pos.IsSalesColumnOpen);
        Assert.False(Named<AccordionHeader>(page, "SalesColumnHeader").IsChecked);
        Assert.InRange(Width(page, "SalesColumn"), 30, 60);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.DataContext is ArticleDto);
        return shell.Pos;
    });

    [Fact]
    public Task Fokusreihenfolge_Kassa_Suchfeld_zuerst_der_Kopf_des_Panels_vor_den_Kacheln_und_vor_Bezahlen() => Render("hell", "fokus-kassa-spalte", async (window, main) =>
    {
        var (page, _) = await OpenCheckoutAt1280(window, main, salesOpen: true, width: 1600);
        Named<TextBox>(page, "PosScanField").Focus(NavigationMethod.Tab);
        await Pump();

        var order = await TabOrder(window, page);
        WriteFocusOrder("kassa-spalte", order);
        var all = string.Join(" | ", order);

        Assert.StartsWith("Gast, Code oder Artikel suchen", order[0], StringComparison.Ordinal);
        var header = order.IndexOf("Artikel und Gutscheine (Button)");
        Assert.True(header > 0, all);
        // Die nächste Tab-Stelle nach dem Kopf ist die erste Artikel-Kachel.
        Assert.Contains("Massageöl", order[header + 1], StringComparison.Ordinal);
        var voucher = order.FindIndex(o => o.StartsWith("Behandlung (ComboBox)", StringComparison.Ordinal));
        Assert.True(voucher > header, all);
        return page;
    });
}
