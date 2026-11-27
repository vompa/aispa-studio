using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// PDF-Ausgaben F (ADR 0092): „Aushang als PDF“ im Wochenplan, das Menü „Ausgabe“ der Zeiten mit „Stundenliste“ auf der Karte einer Person, „Zählliste drucken …“ in der Inventur, der Knopfblock mit Vermerk
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

    private static List<string?> VisibleMenuHeaders(Window window) =>
        [.. window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).Select(i => i.Header as string)];

    [Fact]
    public Task Zeiten_Menue_Ausgabe_hat_auf_der_Karte_beide_Gruppen_und_in_der_Uebersicht_nur_die_Liste()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-zeiten-menue-ausgabe", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name is "TimesDocumentButton" or "TimesDocumentCard" or "TimesExportButton" or "TimesExportSaved");

            var overview = VisibleNamed<Button>(window, "TimesOutputButton");
            overview.Flyout!.ShowAt(overview);
            await Pump(Settling);
            Assert.Equal(["Liste · Monat der Woche", "Stundenliste als Excel", "Stundenliste als CSV", "Abwesenheiten als Excel", "Abwesenheiten als CSV"], VisibleMenuHeaders(window));
            Press(window, PhysicalKey.Escape);
            await Pump(Settling);

            await ClickSection(window, "Personen");
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Zeiten");
            await Pump(Settling);
            var card = VisibleNamed<Button>(window, "TimesOutputButton");
            card.Flyout!.ShowAt(card);
            await Pump(Settling);

            Assert.Equal(
            [
                "Stundenliste · Monat der Woche", "Drucken …", "Als PDF speichern", "Liste · Monat der Woche",
                "Stundenliste als Excel", "Stundenliste als CSV", "Abwesenheiten als Excel", "Abwesenheiten als CSV",
            ], VisibleMenuHeaders(window));
            return team;
        }, api);
    }

    [Fact]
    public Task Zeiten_Menue_Ausgabe_ist_mit_der_Tastatur_erreichbar_und_Esc_gibt_den_Fokus_zurueck()
    {
        var (api, _, _) = TimesApi();
        return Render("hell", "verwaltung-zeiten-menue-tastatur", async (window, main) =>
        {
            var team = await OpenTimes(window, main);
            await ClickSection(window, "Personen");
            await ClickRow(window, "Lena Huber");
            await ClickCardTab(window, "Zeiten");
            await Pump(Settling);
            var output = VisibleNamed<Button>(window, "TimesOutputButton");
            Assert.Equal("Ausgabe", AutomationProperties.GetName(output));
            output.Focus();
            await Pump(Settling);
            Assert.Same(output, Focused(window));

            Press(window, PhysicalKey.Enter);
            await Pump(Settling);
            Assert.True(output.Flyout!.IsOpen);

            // Gruppenüberschriften sind abgeschaltet: die Pfeile überspringen sie und erreichen jeden Befehl.
            var reached = new List<string?>();
            if (Focused(window) is not MenuItem)
            {
                Press(window, PhysicalKey.ArrowDown);
                await Pump();
            }

            for (var step = 0; step < 6; step++)
            {
                reached.Add((Focused(window) as Control)?.Name);
                Press(window, PhysicalKey.ArrowDown);
                await Pump();
            }

            Assert.Equal(["TimesPrintItem", "TimesSavePdfItem", "TimesHoursExcelItem", "TimesHoursCsvItem", "TimesAbsencesExcelItem", "TimesAbsencesCsvItem"], reached);

            Press(window, PhysicalKey.Escape);
            await Pump(Settling);
            Assert.False(output.Flyout.IsOpen);
            Assert.Same(output, Focused(window));
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
