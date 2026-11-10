using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben F (ADR 0092): „Aushang als PDF“ im Wochenplan, „Stundenliste als PDF“ auf der Karte einer Person, „Zählliste drucken …“ in der Inventur, der Knopfblock mit Vermerk
/// „Gesendet am … an …“ in der Bestellung und „Ablaufliste als PDF“ in den Ablauf-Aufgaben, als Inhaberin im Fenster (hell; der Wochenplan auch dunkel). Den Inhalt der PDFs prüft die API.
/// </summary>
public sealed partial class ViewTests
{
    private static Border CardNamed(Window window, string name)
    {
        var card = Named<Border>(window, name);
        Assert.True(card.IsEffectivelyVisible, name);
        return card;
    }

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Wochenplan_traegt_Aushang_als_PDF_mit_Drucken_und_Speichern_ohne_Senden(string theme)
    {
        var (api, _) = RosterApi();
        return Render(theme, "verwaltung-wochenplan-aushang-pdf", async (window, main) =>
        {
            var team = await OpenWeekPlan(window, main);
            Assert.False(Named<Border>(window, "RosterDocumentCard").IsEffectivelyVisible);

            await ClickButton(window, Named<Button>(window, "RosterDocumentButton"));

            var card = CardNamed(window, "RosterDocumentCard");
            Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
            Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text?.StartsWith("Druckt den veröffentlichten Dienstplan der Woche", StringComparison.Ordinal) == true);
            return team;
        }, api);
    }

    [Fact]
    public Task Zeiten_einer_Person_tragen_Stundenliste_als_PDF_in_der_Uebersicht_aller_nicht()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-zeiten-stundenliste-pdf", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            Assert.False(Named<Button>(window, "TimesDocumentButton").IsEffectivelyVisible);
            await ClickSection(window, "Personen");
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Zeiten");
            await Pump(Settling);

            await ClickButton(window, VisibleNamed<Button>(window, "TimesDocumentButton"));

            var card = VisibleNamed<Border>(window, "TimesDocumentCard");
            Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
            Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text?.StartsWith("Druckt die Stundenliste für", StringComparison.Ordinal) == true);
            return team;
        }, api);
    }

    [Fact]
    public Task Inventur_traegt_Zaehlliste_drucken_vor_der_Zaehlung_mit_Drucken_und_Speichern() => Render("hell", "verwaltung-lager-zaehlliste-pdf", async (window, main) =>
    {
        var (shell, stock) = await OpenStockOverview(window, main);
        stock.SetTab(StockTab.Count);
        await Pump(Settling);
        Assert.False(Named<Border>(window, "CountDocumentCard").IsEffectivelyVisible);

        await ClickButton(window, VisibleNamed<Button>(window, "CountDocumentButton"));

        var card = CardNamed(window, "CountDocumentCard");
        Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
        Assert.Equal("Zählliste drucken …", VisibleNamed<Button>(window, "CountDocumentButton").Content);
        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Bestellung_traegt_Knopfblock_mit_Senden_und_den_Vermerk_Gesendet_am_an() => Render("hell", "verwaltung-lager-bestellung-pdf", async (window, main) =>
    {
        var (shell, stock) = await OpenStockOverview(window, main);
        stock.SetTab(StockTab.Orders);
        await Pump(Settling);

        var block = Named<StackPanel>(window, "OrderDocument");
        block.BringIntoView();
        await Pump(Settling);
        Assert.True(block.IsEffectivelyVisible);
        Assert.Equal(["PrintButton", "SaveButton", "SendButton"], VisibleButtonNames(block));
        Assert.Contains(block.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Gesendet am 18.10.2026 11:12 an bestellung@lavendelhof.example von Anna");
        return shell.AdminPage;
    }, StockSample.Api(Api(), period: "from=2026-09-19&until=2026-10-19", locationList: false,
        startOrder: DraftOrder with { SentMails = [new SentDocumentDto("bestellung@lavendelhof.example", new DateTimeOffset(2026, 10, 18, 9, 12, 0, TimeSpan.Zero), "Anna")] }));

    [Fact]
    public Task Ablauf_Aufgaben_tragen_Ablaufliste_als_PDF_mit_Zeitraum_und_Knopfblock() => Render("hell", "verwaltung-lager-ablaufliste-pdf", async (window, main) =>
    {
        var (shell, stock) = await OpenStockOverview(window, main);
        stock.SetTab(StockTab.Tasks);
        await Pump(Settling);
        Assert.False(Named<StackPanel>(window, "ExpiringDocumentCard").IsEffectivelyVisible);

        await ClickButton(window, VisibleNamed<Button>(window, "ExpiringDocumentButton"));

        var card = Named<StackPanel>(window, "ExpiringDocumentCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal(["PrintButton", "SaveButton"], VisibleButtonNames(card));
        Assert.Equal(30, Named<ComboBox>(window, "ExpiringDays").SelectedItem);
        return shell.AdminPage;
    }, StockApi());
}
