using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 116 (Ä272, Ä271): die Inventur-Ansicht zeigt Zählungen vom Handy zum Übernehmen und Abschließen, der Reiter „Verbrauch“ den Bericht Soll-Ist. Echte Klicks im Fenster,
/// die API ist ein Stub; Oberfläche und Rechte prüft das Fenster, die Buchung selbst die API-Tests (<c>StockCountSessionTests</c>).
/// </summary>
public sealed partial class ViewTests
{
    private static readonly Guid CabinetSessionId = Guid.Parse("00000000-0000-0000-0000-0000000c1601");
    private static readonly Guid ShopSessionId = Guid.Parse("00000000-0000-0000-0000-0000000c1602");

    /// <summary>Lena hat den Schrank Zeder gezählt: zwei Öl-Flaschen weniger als im Bestand, Handtücher stimmen.</summary>
    private static readonly StockCountSessionDto CabinetSession = new(CabinetSessionId, StockSample.Vienna.Id, CabinetId, "Schrank Zeder", StockCountSessionStatus.Submitted, "Lena Huber",
        new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 19, 8, 50, 0, TimeSpan.Zero), null,
        [new StockCountLineDto(OilId, "Massageöl", "Flasche", 8m, 10m), new StockCountLineDto(TowelId, "Handtuch", "Stück", 5m, 5m)]);

    private static readonly StockCountSessionDto ShopSession = new(ShopSessionId, StockSample.Vienna.Id, ShopId, "Verkaufsregal", StockCountSessionStatus.Submitted, "Ben Maier",
        new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 19, 9, 10, 0, TimeSpan.Zero), null,
        [new StockCountLineDto(OilId, "Massageöl", "Flasche", 1m, 1m)]);

    /// <summary>Das Lager mit zwei abgeschickten Zählungen; <c>close</c> und <c>discard</c> nehmen die Zählung aus der Liste, wie die API.</summary>
    private static StubApi HandyApi()
    {
        var waiting = new List<StockCountSessionDto> { CabinetSession, ShopSession };
        return StockApi()
            .OnJson($"GET {Route("/count-sessions?status=Submitted")}", _ => waiting.ToList())
            .On($"GET {Route($"/count-sessions/{CabinetSessionId}")}", CabinetSession)
            .On($"GET {Route($"/count-sessions/{ShopSessionId}")}", ShopSession)
            .OnJson($"POST {Route($"/count-sessions/{CabinetSessionId}/close")}", _ =>
            {
                waiting.RemoveAll(s => s.Id == CabinetSessionId);
                return CabinetSession with { Status = StockCountSessionStatus.Closed };
            })
            .OnJson($"POST {Route($"/count-sessions/{CabinetSessionId}/discard")}", _ =>
            {
                waiting.RemoveAll(s => s.Id == CabinetSessionId);
                return CabinetSession with { Status = StockCountSessionStatus.Discarded };
            });
    }

    private static bool HasButton(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == name);

    private static async Task ClickButton(Window window, string name)
    {
        var button = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == name);
        button.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, button);
        await Pump(Settling);
        await Pump(Settling);
    }

    [Fact]
    public Task Given_zwei_Zaehlungen_vom_Handy_When_die_Inventur_offen_ist_Then_zeigt_der_Hinweis_beide_und_Uebernehmen_fuellt_die_Zaehlliste()
    {
        var api = HandyApi();
        return Render("hell", "verwaltung-lager-inventur-handy", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "CountTabButton");

            Assert.True(Shows(window, "2 Zählungen vom Handy warten"));
            Assert.True(Shows(window, "Lena Huber"));
            Assert.True(Shows(window, "Ben Maier"));
            Assert.True(Shows(window, "Verkaufsregal"));

            await ClickButton(window, "Zählung Schrank Zeder von Lena Huber übernehmen");

            Assert.Equal(1, CallsTo(api, $"GET {Route($"/count-sessions/{CabinetSessionId}")}"));
            Assert.True(stock.Count.IsCounting);
            Assert.True(Shows(window, "Inventur: Schrank Zeder"));
            Assert.Equal("8", CountField(window, "Massageöl").Text);
            Assert.Equal("5", CountField(window, "Handtuch").Text);
            var oil = stock.Count.Rows.Single(r => r.Item.Id == OilId);
            Assert.Equal(("10", "-2"), (oil.TargetText, oil.DifferenceText));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_die_uebernommene_Zaehlung_When_die_Leitung_Inventur_buchen_klickt_Then_geht_close_an_die_API_und_die_Zaehlung_verschwindet()
    {
        var api = HandyApi();
        return Render("hell", "verwaltung-lager-inventur-handy-gebucht", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "CountTabButton");
            await ClickButton(window, "Zählung Schrank Zeder von Lena Huber übernehmen");

            await ClickScrolled(window, "BookCountButton");
            Assert.Equal("Inventur buchen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.Equal(0, CallsTo(api, $"POST {Route($"/count-sessions/{CabinetSessionId}/close")}"));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"POST {Route($"/count-sessions/{CabinetSessionId}/close")}"));
            Assert.Empty(SentMovements(api));
            Assert.True(Shows(window, "Inventur gebucht: 1 Position."));
            Assert.False(stock.Count.IsCounting);
            Assert.False(HasButton(window, "Zählung Schrank Zeder von Lena Huber übernehmen"));
            Assert.True(Shows(window, "Eine Zählung vom Handy wartet"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_eine_Zaehlung_vom_Handy_When_die_Leitung_verwirft_Then_fragt_die_Maske_und_discard_geht_an_die_API()
    {
        var api = HandyApi();
        return Render("hell", "verwaltung-lager-inventur-handy-verworfen", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "CountTabButton");

            await ClickButton(window, "Zählung Schrank Zeder von Lena Huber verwerfen");
            Assert.Equal("Zählung verwerfen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.Equal(0, CallsTo(api, $"POST {Route($"/count-sessions/{CabinetSessionId}/discard")}"));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"POST {Route($"/count-sessions/{CabinetSessionId}/discard")}"));
            Assert.False(HasButton(window, "Zählung Schrank Zeder von Lena Huber verwerfen"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_ohne_das_Recht_When_die_Inventur_offen_ist_Then_fehlt_der_Block()
    {
        var withoutRight = new CurrentUser("u3", "ohne.recht@demo.aispa.test", "Rita Steiner", 1,
            new CurrentAccess(Guid.NewGuid(), "Day-Spa Wien", StaffRole.Reception, true, []), null, [.. Enum.GetValues<AccessRight>().Where(r => r != AccessRight.ManageLocation)]);
        var api = HandyApi().On("GET /api/auth/me", withoutRight);
        return Render("hell", "verwaltung-lager-inventur-handy-ohne-recht", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);

            Assert.False(Named<Control>(window, "CountTabButton").IsEffectivelyVisible);
            stock.SetTab(StockTab.Count);
            await Pump(Settling);
            Assert.False(stock.IsCountTab);
            Assert.False(Shows(window, "Zählungen vom Handy"));
            Assert.Equal(0, CallsTo(api, $"GET {Route("/count-sessions?status=Submitted")}"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_der_Reiter_Verbrauch_When_der_Zeitraum_gewaehlt_wird_Then_zeigt_die_Tabelle_Soll_Ist_und_Mehrverbrauch()
    {
        var report = new StockUsageReportDto(StockSample.Vienna.Id, new DateOnly(2026, 7, 21), new DateOnly(2026, 10, 19),
            [new StockUsageLineDto(OilId, "Massageöl", "Flasche", 10m, 12m, 2m, 25m,
                [new StockUsageTreatmentDto(Guid.NewGuid(), "Aromaöl-Massage", 8m, 4), new StockUsageTreatmentDto(Guid.NewGuid(), "Klassische Massage", 2m, 1)])],
            25m, false);
        var api = StockApi()
            .On($"GET /api/reports/locations/{StockSample.Vienna.Id}/stock-usage?from=2026-09-20&until=2026-10-19", report with { From = new DateOnly(2026, 9, 20) })
            .On($"GET /api/reports/locations/{StockSample.Vienna.Id}/stock-usage?from=2026-07-22&until=2026-10-19", report);
        return Render("hell", "verwaltung-lager-verbrauch", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "UsageTabButton");

            Assert.Equal(1, CallsTo(api, $"GET /api/reports/locations/{StockSample.Vienna.Id}/stock-usage?from=2026-09-20&until=2026-10-19"));
            var period = Named<ComboBox>(window, "UsagePeriodField");
            period.SelectedItem = Choice(period, "Letzte 90 Tage");
            await Pump(Settling);
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"GET /api/reports/locations/{StockSample.Vienna.Id}/stock-usage?from=2026-07-22&until=2026-10-19"));
            var row = window.GetVisualDescendants().OfType<Grid>().First(g => AutomationProperties.GetName(g)?.StartsWith("Massageöl, Soll", StringComparison.Ordinal) == true);
            Assert.Equal("Massageöl, Soll 10 Flasche, Ist 12 Flasche, Mehrverbrauch 2 Flasche, Wert 25,00 €", AutomationProperties.GetName(row));
            Assert.True(Shows(window, "Aromaöl-Massage"));
            Assert.True(Shows(window, "4 Termine"));
            return shell.AdminPage;
        }, api);
    }
}
