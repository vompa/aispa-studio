using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Durchsicht der Verwaltung „Lager“ (Befunde 97 bis 105 der Sichtprüfung): Kopfzeilen der Chargen-Tabellen, Gefahrenfarbe für negative Beträge, gefährliche Knöpfe rechts außen,
/// Hinweise an gesperrten Knöpfen, höchstens drei Felder je Zeile, ein Begriff für den Mehrverbrauch, ein gemeinsamer linker Rand der Formulare.
/// </summary>
public sealed partial class ViewTests
{
    private static Border CardOf(Control control) => control.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("card"));

    private static IReadOnlyList<TextBlock> VisibleTexts(Window window) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible)];

    private static readonly string[] NegativeCountTexts = ["-2", "-25,00 €"];

    private static Color ForegroundOf(TextBlock text) => ((ISolidColorBrush)text.Foreground!).Color;

    private static Color CritColor(Control control) =>
        control.TryFindResource("CritBrush", control.ActualThemeVariant, out var brush) && brush is ISolidColorBrush solid ? solid.Color : throw new InvalidOperationException("CritBrush fehlt");

    /// <summary>Gleiche Spalten: jede Zelle der Kopfzeile steht über der Zelle der ersten Zeile (links und rechts auf gleicher Höhe der Achse).</summary>
    private static void AssertHeaderAbovePlace(Window window, string listName)
    {
        var list = Named<ItemsControl>(window, listName);
        var card = CardOf(list);
        var header = card.GetVisualDescendants().OfType<Grid>().First(g => g.Children.OfType<TextBlock>().Any(t => t.Classes.Contains("group")));
        var row = list.GetVisualDescendants().OfType<Grid>().First(g => g.Children.Count >= header.Children.Count);
        Assert.Equal(header.Children.Count, row.Children.Count);
        for (var column = 0; column < header.Children.Count; column++)
        {
            var head = (Control)header.Children[column];
            var cell = (Control)row.Children[column];
            Assert.True(Math.Abs(LeftOf(window, head) - LeftOf(window, cell)) < 1, $"{listName}, Spalte {column}: Kopf {LeftOf(window, head)}, Zelle {LeftOf(window, cell)}");
            Assert.True(Math.Abs(RightOf(window, head) - RightOf(window, cell)) < 1, $"{listName}, Spalte {column}: Kopf rechts {RightOf(window, head)}, Zelle rechts {RightOf(window, cell)}");
        }
    }

    [Fact]
    public Task Given_die_Ansicht_Chargen_When_sie_oeffnet_Then_hat_jede_Tabelle_eine_Kopfzeile_und_die_Anrede_ist_unpersoenlich() => Render("hell", "verwaltung-lager-chargen-kopf", async (window, main) =>
    {
        var (shell, _) = await OpenStock(window, main);
        await ClickScrolled(window, "BatchesViewButton");
        await Pump(Settling);

        // Then steht über jeder der drei Tabellen eine Kopfzeile mit den Spalten der Zeilen
        AssertHeaderAbovePlace(window, "TaskExpiringList");
        AssertHeaderAbovePlace(window, "TaskSellOffList");
        AssertHeaderAbovePlace(window, "TaskExpiredList");
        var expiringHeader = CardOf(Named<ItemsControl>(window, "TaskExpiringList")).GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("group")).Select(t => t.Text);
        Assert.Equal(["Material", "Charge", "Lagerort", "Ablauf", "Bestand"], expiringHeader);

        // Und die Anrede im Satz zu den abgelaufenen Chargen ist unpersönlich (texte.md T1)
        var texts = VisibleTexts(window);
        Assert.DoesNotContain(texts, t => t.Text!.Contains(" du ", StringComparison.Ordinal) || t.Text!.Contains("darfst", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Text == "Diese Chargen nicht mehr verwenden. Nach der Entsorgung ausbuchen.");
        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Given_eine_Zaehlung_mit_Fehlmenge_When_sie_gezaehlt_wird_Then_stehen_Differenz_und_Wert_in_der_Gefahrenfarbe_und_der_Knopf_nennt_was_er_bucht() =>
        Render("hell", "verwaltung-lager-inventur-negativ", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "CountTabButton");
            await ClickScrolled(window, "StartCountButton");
            CountField(window, "Massageöl").Text = "8";
            await Pump(Settling);
            Assert.Equal(("-2", "-25,00 €"), (stock.Count.Rows.Single(r => r.Name == "Massageöl").DifferenceText, stock.Count.Rows.Single(r => r.Name == "Massageöl").WorthText));

            // Then sind die negative Differenz und der negative Wert danger (design.md 5.7); die Summe ebenso; nicht gezählte Zeilen bleiben leer
            var crit = CritColor(window);
            var shown = VisibleTexts(window);
            foreach (var text in NegativeCountTexts)
            {
                var cells = shown.Where(t => t.Text == text).ToList();
                Assert.NotEmpty(cells);
                Assert.All(cells, c => Assert.Equal(crit, ForegroundOf(c)));
            }

            // Und der Knopf sagt im Hinweis, was gebucht wird, wenn eine Zeile nicht gezählt ist
            Assert.Contains("nicht gezählte", Avalonia.Controls.ToolTip.GetTip(Named<Button>(window, "BookCountButton")) as string, StringComparison.OrdinalIgnoreCase);
            return shell.AdminPage;
        }, StockApi());

    [Fact]
    public Task Given_ein_Entwurf_When_die_Bestellung_gezeigt_wird_Then_steht_der_gefaehrliche_Knopf_rechts_aussen_und_der_gesperrte_hat_einen_Grund() =>
        Render("hell", "verwaltung-lager-bestellung-knoepfe", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            var discard = OrderButton(window, "Entwurf verwerfen, Bestellung 7");
            var actions = Named<WrapPanel>(window, "OrderActions");
            var card = CardOf(discard);

            // Then steht „Entwurf verwerfen“ rechts von allen anderen Knöpfen der Bestellung und am rechten Rand der Karte (design.md 5.1)
            foreach (var other in actions.Children.OfType<Button>().Where(b => b.IsEffectivelyVisible))
            {
                Assert.True(LeftOf(window, discard) >= RightOf(window, other), $"„{other.Content}“ endet bei {RightOf(window, other)}, der gefährliche Knopf beginnt bei {LeftOf(window, discard)}");
            }

            Assert.True(RightOf(window, card) - RightOf(window, discard) <= card.Padding.Right + 1, $"Rand der Karte {RightOf(window, card)}, Knopf endet bei {RightOf(window, discard)}");

            // Und „Mengen speichern“ nennt gesperrt den Grund, mit einer geänderten Menge das Kürzel; „Bestelltext“ sagt, was er tut
            var save = OrderButton(window, "Mengen der Bestellung 7 speichern");
            Assert.False(save.IsEffectivelyEnabled);
            Assert.Equal("Keine Menge geändert", Avalonia.Controls.ToolTip.GetTip(save));
            OrderQuantity(window).Text = "6";
            await Pump(Settling);
            Assert.Equal("Speichern (Strg+S)", Avalonia.Controls.ToolTip.GetTip(save));
            Assert.Contains("Text der Bestellung", (string)Avalonia.Controls.ToolTip.GetTip(OrderButton(window, "Bestelltext der Bestellung 7"))!, StringComparison.Ordinal);
            return shell.AdminPage;
        }, StockApi());

    /// <summary>Das erste Feld und die Knopfzeile: beide müssen nach dem Öffnen zugleich im Bild stehen.</summary>
    private static readonly string[] ItemFormEnds = ["ItemNameField", "CreateItemButton"];

    private static readonly string[] ItemFormFields =
    [
        "ItemNameField", "ItemUnitField", "ItemUnitCostField", "ItemMinimumField", "ItemArticleField", "ItemSupplierField", "ItemSupplierNumberField", "ItemBarcodeField",
        "ItemPackUnitField", "ItemPackSizeField", "ItemCaseUnitField", "ItemPacksPerCaseField", "ItemDoseField", "ItemWarningField",
    ];

    /// <summary>Nach dem Öffnen des Formulars liegt die Knopfzeile im sichtbaren Teil der Seite (masken.md R5); jeder Hilfetext unter einem Feld ist eine Zeile (R6).</summary>
    private static async Task AssertItemFormButtonsVisible(Window window)
    {
        await ClickScrolled(window, "NewItemButton");
        await Pump(Settling);
        var form = CardOf(Named<TextBox>(window, "ItemNameField"));
        foreach (var field in form.GetVisualDescendants().OfType<FormField>().Where(f => !string.IsNullOrEmpty(f.Help)))
        {
            var help = field.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == field.Help);
            Assert.True(help.Bounds.Height <= 20, $"Der Hilfetext „{field.Help}“ ist {help.Bounds.Height} hoch");
        }

        var scroll = Named<ScrollViewer>(window, "StockScroll");
        foreach (var name in ItemFormEnds)
        {
            var control = Named<Control>(window, name);
            var top = control.TranslatePoint(default, scroll)!.Value.Y;
            Assert.True(top >= 0 && top + control.Bounds.Height <= scroll.Viewport.Height + 0.5,
                $"{name}: Oberkante {top}, Unterkante {top + control.Bounds.Height}, sichtbar bis {scroll.Viewport.Height}");
        }
    }

    [Fact]
    public Task Given_das_Formular_Material_anlegen_When_es_offen_ist_Then_stehen_hoechstens_drei_Felder_in_einer_Zeile_und_die_Knoepfe_im_Bild() =>
        Render("hell", "verwaltung-lager-material-felder", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await AssertItemFormButtonsVisible(window);

            var perRow = ItemFormFields.Select(n => Named<Control>(window, n)).GroupBy(c => Math.Round(TopOf(window, c))).Select(g => g.Count()).ToList();
            Assert.True(perRow.Max() <= 3, $"Felder je Zeile: {string.Join(", ", perRow)}");
            return shell.AdminPage;
        }, StockApi());

    [Fact]
    public Task Given_das_Mindestfenster_When_das_Formular_Material_anlegen_geoeffnet_wird_Then_liegt_die_Knopfzeile_im_sichtbaren_Teil() =>
        Render("hell", "verwaltung-lager-material-mindestfenster", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await SetMinimumWindow(window, shell);
            await AssertItemFormButtonsVisible(window);
            return shell.AdminPage;
        }, StockApi());

    private static StockUsageReportDto UsageWithFind() => UsageWithDeviations with
    {
        Lines = [.. UsageWithDeviations.Lines, new StockUsageLineDto(Guid.NewGuid(), "Kerzen", "Stk", 10m, 8m, -2m, -5m, []) { ExcessPercent = -20m }],
    };

    [Fact]
    public Task Given_die_Tabelle_Verbrauch_When_sie_laedt_Then_heisst_die_Spalte_wie_die_Kachel_einzeilig_und_der_Pfeil_hat_einen_Hinweis() =>
        Render("hell", "verwaltung-lager-verbrauch-begriff", async (window, main) =>
        {
            var (shell, stock) = await OpenStockOverview(window, main);
            stock.SetTab(StockTab.Usage);
            await Pump(Settling);
            await Pump(Settling);

            // Then heißt die Spalte „Mehrverbrauch“ wie die Kachel der Übersicht, steht in einer Zeile, und den alten Namen gibt es nicht mehr
            var header = Assert.Single(Named<Border>(window, "UsageCard").GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Classes.Contains("group") && t.Text == "Mehrverbrauch");
            Assert.True(header.Bounds.Height < 24, $"Kopf ist {header.Bounds.Height} hoch");
            Assert.False(Shows(window, "Wert der Abweichung"));
            Assert.Contains(stock.Todo.Stats, s => s.Label == "Mehrverbrauch");

            // Und der Pfeil vor dem Material nennt im Hinweis, was er tut
            var toggle = UsageRowButtons(window)[0];
            Assert.Equal("Behandlungen zeigen", Avalonia.Controls.ToolTip.GetTip(toggle));
            return shell.AdminPage;
        }, UsageApi(UsageWithDeviations));

    [Fact]
    public Task Given_ein_Fund_in_der_Inventur_When_die_Tabelle_Verbrauch_laedt_Then_steht_der_negative_Betrag_in_der_Gefahrenfarbe() =>
        Render("hell", "verwaltung-lager-verbrauch-fund", async (window, main) =>
        {
            var (shell, stock) = await OpenStockOverview(window, main);
            stock.SetTab(StockTab.Usage);
            await Pump(Settling);
            await Pump(Settling);
            await ClickButton(window, "Alle zeigen");

            Assert.True(stock.Usage.Rows.Single(r => r.Material == "Kerzen").ValueIsNegative);
            var crit = CritColor(window);
            var negative = Assert.Single(VisibleTexts(window), t => t.Classes.Contains("num") && t.Text == "-5,00 €");
            Assert.Equal(crit, ForegroundOf(negative));
            var positive = VisibleTexts(window).First(t => t.Classes.Contains("num") && t.Text == "21,60 €");
            Assert.NotEqual(crit, ForegroundOf(positive));
            return shell.AdminPage;
        }, UsageApi(UsageWithFind()));

    [Fact]
    public Task Given_die_Einrichtung_der_Lagerorte_When_das_Hauptlager_gewaehlt_ist_Then_nennt_die_Art_ihren_Grund_und_der_Hinweis_zu_den_Mindestmengen_ist_eine_Zeile() =>
        Render("hell", "verwaltung-lager-lagerorte-hauptlager", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await OpenPlaces(window, stock);
            stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Id == MainId);
            await Pump(Settling);

            // Then steht beim Hauptlager unter „Art“ der Grund und am gesperrten Feld derselbe Hinweis
            var kind = Named<ComboBox>(window, "PlaceKindField");
            Assert.False(kind.IsEffectivelyEnabled);
            Assert.Equal("Beim Hauptlager fest.", Avalonia.Controls.ToolTip.GetTip(kind));
            Assert.Contains(VisibleTexts(window), t => t.Text == "Beim Hauptlager fest.");

            // Und der Hinweis unter „Mindestmengen“ steht in einer Zeile (masken.md R6)
            var hint = Assert.Single(VisibleTexts(window), t => t.Text?.StartsWith("Fällt der Bestand an diesem Ort darunter", StringComparison.Ordinal) == true);
            Assert.True(hint.Bounds.Height <= 20, $"Der Hinweis ist {hint.Bounds.Height} hoch");

            // Und „Von Raum … übernehmen“ sagt, was übernommen wird
            Assert.Contains("Mindestmengen", (string)Avalonia.Controls.ToolTip.GetTip(Named<Button>(window, "CopyMinimumsButton"))!, StringComparison.Ordinal);
            return shell.AdminPage;
        }, StockApi());

    [Fact]
    public Task Given_die_Formulare_der_Einrichtung_When_Lagerort_und_Lieferant_gezeigt_werden_Then_beginnen_die_Felder_am_selben_linken_Rand_der_Karte() =>
        Render("hell", "verwaltung-lager-lieferanten-rand", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await OpenPlaces(window, stock);
            await Pump(Settling);
            var placeOffset = LeftOf(window, Named<TextBox>(window, "PlaceNameField")) - LeftOf(window, CardOf(Named<TextBox>(window, "PlaceNameField")));

            await ClickScrolled(window, "SuppliersTabButton");
            await Pump(Settling);
            var name = Named<TextBox>(window, "SupplierNameField");
            var supplierOffset = LeftOf(window, name) - LeftOf(window, CardOf(name));

            Assert.True(Math.Abs(placeOffset - supplierOffset) < 1, $"Lagerort {placeOffset}, Lieferant {supplierOffset}");
            Assert.True(supplierOffset < 40, $"Die Felder des Lieferanten beginnen {supplierOffset} vom Kartenrand");
            return shell.AdminPage;
        }, StockApi());

    [Fact]
    public Task Given_ein_gespeicherter_Lieferant_When_die_Antwort_kommt_Then_bestaetigt_ein_Toast_unten_und_kein_gruener_Balken_steht_ueber_der_Liste() =>
        Render("hell", "verwaltung-lager-lieferant-gespeichert", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "SetupButton");
            await ClickScrolled(window, "SuppliersTabButton");
            await TypeInto(window, "SupplierNameField", "Haslauer");
            ClickCenter(window, window.GetVisualDescendants().OfType<SaveBar>().Single(b => b.IsEffectivelyVisible).GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Speichern"));
            await Pump(Settling);

            // Then bestätigt der Toast unten in einer Zeile ohne Schlusspunkt (design.md 5.10, meldungen.md M1), und über der Liste steht keine Erfolgs-InfoBar
            Assert.Equal("Gespeichert", stock.Toast);
            Assert.True(Named<Border>(window, "StockToast").IsEffectivelyVisible);
            Assert.False(stock.HasMessage);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<InfoBar>(), b => b.IsEffectivelyVisible && b.Severity == InfoBarSeverity.Success);
            return shell.AdminPage;
        }, StockApi());
}
