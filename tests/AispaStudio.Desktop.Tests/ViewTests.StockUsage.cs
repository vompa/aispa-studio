using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Lager, Verbrauch (Überarbeitung der Masken, P6): Tabelle höchstens 900 breit, als Standard nur Abweichungen (sortiert, wie die API sie liefert), Abweichung in Menge und Prozent,
/// Behandlungen je Material zum Aufklappen, die Summe steht nur einmal (in der Kachel der Übersicht).
/// </summary>
public sealed partial class ViewTests
{
    private static readonly Guid ThaiTreatment = Guid.Parse("00000000-0000-0000-0000-0000000c1801");
    private static readonly Guid AyurvedaTreatment = Guid.Parse("00000000-0000-0000-0000-0000000c1802");

    /// <summary>Wie die API sie liefert: der größte Wert der Abweichung zuerst; Handtuch und Peeling haben keine Abweichung.</summary>
    private static readonly StockUsageReportDto UsageWithDeviations = new(StockSample.Vienna.Id, new DateOnly(2026, 7, 22), new DateOnly(2026, 10, 19),
    [
        new StockUsageLineDto(Guid.NewGuid(), "Zimtöl, Mischung nach Wahl", "ml", 660m, 780m, 120m, 21.60m,
            [new StockUsageTreatmentDto(ThaiTreatment, "Thai-Aromaölmassage", 480m, 16), new StockUsageTreatmentDto(AyurvedaTreatment, "Ayurveda Abhyanga", 180m, 1)]) { ExcessPercent = 18m },
        new StockUsageLineDto(Guid.NewGuid(), "Einweg-Kosmetiktücher", "Stk", 20m, 26m, 6m, 9.60m, []) { ExcessPercent = 30m },
        new StockUsageLineDto(Guid.NewGuid(), "Handtuch", "Stück", 40m, 40m, 0m, 0m, [new StockUsageTreatmentDto(ThaiTreatment, "Thai-Aromaölmassage", 40m, 8)]) { ExcessPercent = 0m },
        new StockUsageLineDto(Guid.NewGuid(), "Peeling", "g", 300m, 300m, 0m, 0m, []) { ExcessPercent = 0m },
    ], 31.20m, false);

    private static StubApi UsageApi(StockUsageReportDto report) => StockApi()
        .On($"GET /api/reports/locations/{StockSample.Vienna.Id}/stock-usage?from=2026-09-20&until=2026-10-19", report with { From = new DateOnly(2026, 9, 20) })
        .On($"GET /api/reports/locations/{StockSample.Vienna.Id}/stock-usage?from=2026-07-22&until=2026-10-19", report);

    private static List<Button> UsageRowButtons(Window window) =>
        [.. Named<ItemsControl>(window, "UsageList").GetVisualDescendants().OfType<Button>().Where(b => b.Name == "UsageDetailsButton")];

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Given_der_Abschnitt_Verbrauch_When_er_laedt_Then_zeigt_er_nur_Abweichungen_mit_Menge_und_Prozent_und_die_Summe_nur_einmal(string theme) =>
        Render(theme, "verwaltung-lager-verbrauch", async (window, main) =>
        {
            var (shell, stock) = await OpenStockOverview(window, main);
            stock.SetTab(StockTab.Usage);
            await Pump(Settling);
            await Pump(Settling);

            var usage = stock.Usage;
            Assert.Equal(["Zimtöl, Mischung nach Wahl", "Einweg-Kosmetiktücher"], usage.Rows.Select(r => r.Material).ToList());
            Assert.Equal("Zimtöl, Mischung nach Wahl, Soll 660 ml, Ist 780 ml, Abweichung +120 ml · 18 %, Wert 21,60 €", usage.Rows[0].Summary);
            Assert.True(Shows(window, "2 weitere Materialien nach Rezept verbraucht"));
            Assert.True(Shows(window, "Wert der Abweichung"));
            Assert.False(Shows(window, "Mehrverbrauch zum Einstandspreis"));
            var card = Named<Border>(window, "UsageCard");
            Assert.True(card.Bounds.Width <= 900.5, $"Die Tabelle ist {card.Bounds.Width} breit");
            Assert.False(Shows(window, "Thai-Aromaölmassage"));
            return shell.AdminPage;
        }, UsageApi(UsageWithDeviations));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Given_die_Tabelle_When_ein_Material_aufgeklappt_wird_Then_stehen_die_Behandlungen_mit_Soll_und_Terminen_darunter(string theme) =>
        Render(theme, "verwaltung-lager-verbrauch-details-1100", async (window, main) =>
        {
            // Im Mindestfenster (1100 breit) bleibt die Tabelle im Bild.
            var (shell, stock) = await OpenStockOverview(window, main);
            await SetMinimumWindow(window, shell);
            stock.SetTab(StockTab.Usage);
            await Pump(Settling);
            await Pump(Settling);

            var first = UsageRowButtons(window)[0];
            first.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, first);
            await Pump(Settling);
            await Pump(Settling);

            Assert.True(stock.Usage.Rows[0].IsExpanded);
            Assert.True(RightOf(window, Named<Border>(window, "UsageCard")) <= window.Bounds.Width);
            Assert.True(Shows(window, "Thai-Aromaölmassage"));
            Assert.True(Shows(window, "16 Termine"));
            Assert.True(Shows(window, "1 Termin"));
            Assert.False(stock.Usage.Rows[1].IsExpanded);
            return shell.AdminPage;
        }, UsageApi(UsageWithDeviations));

    [Fact]
    public Task Given_nur_Abweichungen_When_Alle_zeigen_gewaehlt_wird_Then_stehen_auch_die_Materialien_ohne_Abweichung_in_der_Tabelle() =>
        Render("hell", "verwaltung-lager-verbrauch-alle", async (window, main) =>
        {
            var (shell, stock) = await OpenStockOverview(window, main);
            stock.SetTab(StockTab.Usage);
            await Pump(Settling);
            await Pump(Settling);

            await ClickButton(window, "Alle zeigen");

            Assert.False(stock.Usage.OnlyDeviations);
            Assert.Equal(4, stock.Usage.Rows.Count);
            Assert.False(Shows(window, "weitere Materialien nach Rezept verbraucht"));
            Assert.True(Shows(window, "Handtuch"));
            return shell.AdminPage;
        }, UsageApi(UsageWithDeviations));

    [Fact]
    public Task Given_der_Abschnitt_Verbrauch_der_Uebersicht_When_der_Zeitraum_gewaehlt_wird_Then_laedt_die_Tabelle_neu()
    {
        var api = UsageApi(UsageWithDeviations);
        return Render("hell", "verwaltung-lager-verbrauch-zeitraum", async (window, main) =>
        {
            // Die Übersicht lädt den Verbrauch mit; das alte Sprungziel „Verbrauch“ holt den Abschnitt ins Bild und lädt nicht noch einmal.
            var (shell, stock) = await OpenStockOverview(window, main);
            stock.SetTab(StockTab.Usage);
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"GET /api/reports/locations/{StockSample.Vienna.Id}/stock-usage?from=2026-09-20&until=2026-10-19"));
            var period = Named<ComboBox>(window, "UsagePeriodField");
            period.SelectedItem = Choice(period, "Letzte 90 Tage");
            await Pump(Settling);
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"GET /api/reports/locations/{StockSample.Vienna.Id}/stock-usage?from=2026-07-22&until=2026-10-19"));
            Assert.Equal(2, stock.Usage.Rows.Count);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_ohne_das_Recht_fuer_Betraege_When_die_Tabelle_laedt_Then_fehlt_die_Spalte_Wert_und_der_Hinweis_steht_einmal() =>
        Render("hell", "verwaltung-lager-verbrauch-ohne-betrag", async (window, main) =>
        {
            var (shell, stock) = await OpenStockOverview(window, main);
            stock.SetTab(StockTab.Usage);
            await Pump(Settling);
            await Pump(Settling);

            Assert.False(Shows(window, "Wert der Abweichung"));
            Assert.True(Shows(window, "Den Wert in Euro zeigt der Bericht nur mit dem Recht"));
            Assert.Equal("Zimtöl, Mischung nach Wahl, Soll 660 ml, Ist 780 ml, Abweichung +120 ml · 18 %", stock.Usage.Rows[0].Summary);
            return shell.AdminPage;
        }, UsageApi(UsageWithDeviations with
        {
            ExcessValue = null,
            AmountsHidden = true,
            Lines = [.. UsageWithDeviations.Lines.Select(l => l with { ExcessValue = null })],
        }));
}
