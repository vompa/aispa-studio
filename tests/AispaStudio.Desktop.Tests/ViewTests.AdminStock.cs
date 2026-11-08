using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 101 (Ä191): native Maske „Lager“ im Fenster: Material anlegen, Wareneingang und Inventur per Klick und Eingabe, Bildtests, Scans.</summary>
public sealed partial class ViewTests
{
    /// <summary>Die Bildtests laufen am 19.10.2026; „die letzten 30 Tage“ der Bewegungen beginnen am 19.9.</summary>
    private static StubApi StockApi() => StockSample.Api(Api(), period: "from=2026-09-19&until=2026-10-19", locationList: false);

    /// <summary>Öffnet das Lager im Reiter „Bestand“ (der Start steht seit Slice 143 in der Übersicht; <see cref="OpenStockOverview"/> lässt sie stehen).</summary>
    private static async Task<(ShellViewModel Shell, StockViewModel Stock)> OpenStock(Window window, MainViewModel main)
    {
        var (shell, stock) = await OpenStockOverview(window, main);
        stock.SetTab(StockTab.Stock);
        await Pump(Settling);
        return (shell, stock);
    }

    private static async Task<(ShellViewModel Shell, StockViewModel Stock)> OpenStockOverview(Window window, MainViewModel main)
    {
        var shell = await OpenAdminPage(window, main);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Lager");
        await Pump(Settling);
        await Pump(Settling);
        Assert.Same(shell.AdminPage.Stock, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Stock!.IsLoaded);
        return (shell, shell.AdminPage.Stock);
    }

    private static TextBox CountField(Window window, string material) =>
        window.GetVisualDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == $"Gezählt {material}");

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Lager(string theme) => Render(theme, "verwaltung-lager", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);
        Assert.True(Shows(window, "Massageöl"));
        Assert.True(Shows(window, "unter Minimum"));
        Assert.Equal("Bestand", Named<TabButton>(window, "StockTabButton").Content);
        stock.Overview.Book(OilId);
        await Pump(Settling);
        Assert.Same(stock.Booking.Items.Single(i => i.Id == OilId), Named<ComboBox>(window, "MovementItemField").SelectedItem);
        return shell.AdminPage;
    }, StockApi());

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Verwaltung_Lager_Inventur(string theme) => Render(theme, "verwaltung-lager-inventur", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);
        await ClickScrolled(window, "CountTabButton");
        await ClickScrolled(window, "StartCountButton");
        CountField(window, "Massageöl").Text = "8";
        await Pump(Settling);
        Assert.True(Shows(window, "Inventur: Hauptlager"));
        Assert.Equal(1, stock.Count.ChangeCount);
        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Material_anlegen_ueber_Klick()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-material", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            Assert.False(Named<Button>(window, "CreateItemButton").IsEffectivelyEnabled);

            await TypeInto(window, "ItemNameField", "Duftkerze");
            await TypeInto(window, "ItemUnitCostField", "2,5");
            Named<ComboBox>(window, "ItemSupplierField").SelectedItem = stock.ItemForm.Suppliers.Single(s => s.Name == "Lavendelhof");
            Assert.True(stock.HasUnsavedChanges);
            await ClickScrolled(window, "CreateItemButton");
            await Pump(Settling);

            var sent = System.Text.Json.JsonSerializer.Deserialize<StockItemInput>(Assert.Single(api.Calls, c => c.Route == "POST /api/stock/items").Body!, ApiClient.Json)!;
            Assert.Equal(("Duftkerze", "Stück", 2.5m, stock.ItemForm.Suppliers.Single(s => s.Name == "Lavendelhof").Id), (sent.Name, sent.Unit, sent.UnitCost, sent.SupplierId));
            Assert.True(Shows(window, "Duftkerze angelegt."));
            Assert.Equal("", Named<TextBox>(window, "ItemNameField").Text);
            Assert.False(stock.HasUnsavedChanges);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Wareneingang_buchen_ueber_Klick()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-wareneingang", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);

            await ClickScrolled(window, "BookMovementButton");
            Assert.True(stock.Booking.IsOpen);
            Assert.False(Named<Button>(window, "BookButton").IsEffectivelyEnabled);
            Named<ComboBox>(window, "MovementItemField").SelectedItem = stock.Booking.Items.Single(i => i.Id == OilId);
            await TypeInto(window, "MovementAmountField", "4");
            await TypeInto(window, "MovementBatchField", "L-9");
            Assert.True(Named<Button>(window, "BookButton").IsEffectivelyEnabled);
            await ClickScrolled(window, "BookButton");
            await Pump(Settling);

            var sent = SentMovement(api);
            Assert.Equal((OilId, StockMovementKind.GoodsReceipt, 4m, "L-9", MainId), (sent.ItemId, sent.Kind, sent.Quantity, sent.Batch, sent.StoragePlaceId));
            Assert.True(Shows(window, "Gebucht."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Inventur_zaehlen_und_buchen_ueber_Eingabe_und_Klick()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-inventur-gebucht", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);

            await ClickScrolled(window, "CountTabButton");
            await ClickScrolled(window, "StartCountButton");
            var field = CountField(window, "Massageöl");
            field.BringIntoView();
            await Pump(Settling);
            ClickCenter(window, field);
            await Pump();
            field.SelectAll();
            window.KeyTextInput("8");
            await Pump(Settling);
            Assert.True(stock.HasUnsavedChanges);
            Assert.True(Named<Button>(window, "BookCountButton").IsEffectivelyEnabled);

            await ClickScrolled(window, "BookCountButton");
            Assert.Equal("Inventur buchen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.Empty(SentMovements(api));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);

            var sent = Assert.Single(SentMovements(api));
            Assert.Equal((OilId, StockMovementKind.Count, 8m, MainId), (sent.ItemId, sent.Kind, sent.Quantity, sent.StoragePlaceId));
            Assert.True(Shows(window, "Inventur gebucht: 1 Position."));
            Assert.False(stock.Count.IsCounting);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Lagerort_loeschen_fragt_nach_und_sendet_DELETE()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-lagerorte", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "PlacesTabButton");
            stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Id == ShopId);
            await Pump(Settling);
            Assert.Equal("Verkaufsregal", Named<TextBox>(window, "PlaceNameField").Text);

            await ClickScrolled(window, "DeletePlaceButton");
            Assert.Equal("Lagerort löschen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.Equal(0, CallsTo(api, $"DELETE {Route($"/storage-places/{ShopId}")}"));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"DELETE {Route($"/storage-places/{ShopId}")}"));
            Assert.True(Shows(window, "Lagerort gelöscht."));
            return shell.AdminPage;
        }, api);
    }

    private static object Choice(ComboBox box, string text) => box.Items.Cast<object>().First(o => o.ToString() == text);

    private static Button OrderButton(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name);

    private static TextBox OrderQuantity(Window window) =>
        window.GetVisualDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "Menge Massageöl (Flasche)");

    [Fact]
    public Task Given_Vorschlag_unter_dem_Mindestbestellwert_When_die_Leitung_die_Bestellung_oeffnet_Then_warnt_die_Maske_mit_dem_fehlenden_Betrag() =>
        Render("hell", "verwaltung-lager-bestellungen", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);

            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            Assert.True(Shows(window, "Unter dem Mindestbestellwert von 150,00 €: es fehlen 87,50 €."));
            Assert.True(Shows(window, "3 Flasche am Tag × 5 Tage, Termine 9 Flasche"));
            Assert.True(Shows(window, "Geht per Mail an bestellung@lavendelhof.example."));
            return shell.AdminPage;
        }, StockApi());

    [Fact]
    public Task Given_Bestellen_und_senden_When_die_Leitung_bestaetigt_Then_geht_die_Bestellung_an_die_API_und_steht_auf_bestellt()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-bestellt", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            ClickCenter(window, OrderButton(window, "Bestellung 7 bestellen und senden"));
            await Pump(Settling);
            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Bestellung 7 senden?", dialog.Title);
            Assert.Contains("bestellung@lavendelhof.example", dialog.Message, StringComparison.Ordinal);
            Assert.Contains("es fehlen 87,50 €", dialog.Message, StringComparison.Ordinal);
            Assert.Equal(0, CallsTo(api, $"POST /api/stock/orders/{OrderId}/send"));

            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"POST /api/stock/orders/{OrderId}/send"));
            Assert.True(Shows(window, "bestellt"));
            Assert.True(Shows(window, "Bestellung 7 ist bestellt, die Mail an bestellung@lavendelhof.example ist unterwegs."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Bestellen_und_senden_When_die_Leitung_im_Dialog_abbricht_Then_geht_nichts_hinaus()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-bestellung-abbrechen", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            ClickCenter(window, OrderButton(window, "Bestellung 7 bestellen und senden"));
            await Pump(Settling);
            ClickCenter(window, DialogButton(window, "CancelButton"));
            await Pump(Settling);

            Assert.Equal(0, CallsTo(api, $"POST /api/stock/orders/{OrderId}/send"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Menge_im_Entwurf_aendern_speichert_ueber_Klick_und_rechnet_die_Warnung_neu()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-bestellung-menge", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            OrderQuantity(window).Text = "12";
            await Pump(Settling);
            Assert.True(stock.HasUnsavedChanges);
            ClickCenter(window, OrderButton(window, "Mengen der Bestellung 7 speichern"));
            await Pump(Settling);
            await Pump(Settling);

            var sent = System.Text.Json.JsonSerializer.Deserialize<PurchaseOrderInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/stock/orders/{OrderId}").Body!, ApiClient.Json)!;
            Assert.Equal(12m, sent.Lines.Single().Quantity);
            Assert.False(stock.HasUnsavedChanges);
            Assert.Equal("12", OrderQuantity(window).Text);
            Assert.False(Shows(window, "Unter dem Mindestbestellwert von 150,00 €: es fehlen 87,50 €."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_geaenderte_Menge_unter_dem_Mindestwert_When_direkt_gesendet_Then_nennt_die_Rueckfrage_den_neuen_Fehlbetrag()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-bestellung-neuer-fehlbetrag", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            OrderQuantity(window).Text = "2";
            await Pump(Settling);
            ClickCenter(window, OrderButton(window, "Bestellung 7 bestellen und senden"));
            await Pump(Settling);

            Assert.Contains("es fehlen 125,00 €", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Message, StringComparison.Ordinal);
            Assert.Equal(0, CallsTo(api, $"POST /api/stock/orders/{OrderId}/send"));
            ClickCenter(window, DialogButton(window, "CancelButton"));
            await Pump(Settling);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_ungueltige_Menge_When_gespeichert_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-bestellung-fehler", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            OrderQuantity(window).Text = "viel";
            await Pump(Settling);
            ClickCenter(window, OrderButton(window, "Mengen der Bestellung 7 speichern"));
            await Pump(Settling);

            Assert.True(Shows(window, "Gib eine Zahl ab 0 ein, z. B. 500."));
            Assert.Equal(0, CallsTo(api, $"PUT /api/stock/orders/{OrderId}"));
            return shell.AdminPage;
        }, api);
    }

    private static TextBox FieldNamed(Window window, string name) =>
        window.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible && AutomationProperties.GetName(t) == name);

    private static async Task ClickOrder(Window window, string name)
    {
        var button = OrderButton(window, name);
        button.BringIntoView();
        await Pump(Settling);
        ClickCenter(window, button);
        await Pump(Settling);
        await Pump(Settling);
    }

    private static StubApi OrderedApi() => StockSample.Api(Api(), period: "from=2026-09-19&until=2026-10-19", locationList: false, startOrder: OrderedOrder);

    private static PurchaseOrderDeliveryInput SentDelivery(StubApi api) =>
        System.Text.Json.JsonSerializer.Deserialize<PurchaseOrderDeliveryInput>(Assert.Single(api.Calls, c => c.Route == $"POST /api/stock/orders/{OrderId}/deliveries").Body!, ApiClient.Json)!;

    [Fact]
    public Task Given_bestellte_Lieferung_When_der_Wareneingang_in_der_App_gebucht_wird_Then_steigt_der_Bestand_und_die_Bestellung_steht_auf_geliefert()
    {
        var api = OrderedApi();
        return Render("hell", "verwaltung-lager-lieferung", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            Assert.Equal("5", FieldNamed(window, "Geliefert Massageöl (Flasche)").Text);
            FieldNamed(window, "Charge Massageöl").Text = "L-9";
            FieldNamed(window, "Lieferschein der Bestellung 7").Text = "LS-3";
            await Pump(Settling);
            await ClickOrder(window, "Lieferung zu Bestellung 7 buchen");

            var sent = SentDelivery(api);
            var line = Assert.Single(sent.Lines);
            Assert.Equal((DraftOrder.Lines[0].Id, 5m, "L-9", "LS-3"), (line.LineId, line.Quantity, line.Batch, sent.DeliveryNote));
            Assert.True(Shows(window, "Lieferung zu Bestellung 7 gebucht."));
            Assert.True(Shows(window, "geliefert"));
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "Lieferung zu Bestellung 7 buchen");
            Assert.Equal("15", stock.Overview.Rows.Single(r => r.ItemId == OilId).QuantityText);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Teillieferung_When_gebucht_Then_teilweise_geliefert_und_der_Rest_ist_vorbelegt_und_Abschliessen_fragt_nach()
    {
        var api = OrderedApi();
        return Render("hell", "verwaltung-lager-teillieferung", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            FieldNamed(window, "Geliefert Massageöl (Flasche)").Text = "2";
            await Pump(Settling);
            await ClickOrder(window, "Lieferung zu Bestellung 7 buchen");

            Assert.True(Shows(window, "teilweise geliefert"));
            Assert.Equal("3", FieldNamed(window, "Geliefert Massageöl (Flasche)").Text);

            await ClickOrder(window, "Bestellung 7 abschließen");
            Assert.Equal("Bestellung 7 abschließen?", Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current).Title);
            Assert.Equal(0, CallsTo(api, $"POST /api/stock/orders/{OrderId}/close"));
            ClickCenter(window, DialogButton(window, "ConfirmButton"));
            await Pump(Settling);
            await Pump(Settling);
            Assert.Equal(1, CallsTo(api, $"POST /api/stock/orders/{OrderId}/close"));
            Assert.True(Shows(window, "Bestellung 7 abgeschlossen."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_falsche_Liefermenge_When_gebucht_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = OrderedApi();
        return Render("hell", "verwaltung-lager-lieferung-fehler", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            FieldNamed(window, "Geliefert Massageöl (Flasche)").Text = "viel";
            await Pump(Settling);
            await ClickOrder(window, "Lieferung zu Bestellung 7 buchen");

            Assert.True(Shows(window, "Gib eine Zahl ab 0 ein, z. B. 500."));
            Assert.Equal(0, CallsTo(api, $"POST /api/stock/orders/{OrderId}/deliveries"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_unlesbares_Ablaufdatum_When_gebucht_Then_geht_keine_Lieferung_hinaus()
    {
        var api = OrderedApi();
        return Render("hell", "verwaltung-lager-lieferung-datum", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            var date = window.GetVisualDescendants().OfType<DateField>().First(d => d.IsEffectivelyVisible && AutomationProperties.GetName(d) == "Ablaufdatum Massageöl");
            date.Text = "32.13.2026";
            date.Commit();
            await Pump(Settling);
            await ClickOrder(window, "Lieferung zu Bestellung 7 buchen");

            Assert.Equal(0, CallsTo(api, $"POST /api/stock/orders/{OrderId}/deliveries"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_anderer_Arbeitsplatz_bucht_When_das_Lager_neu_laedt_Then_steht_der_neue_Rest_und_die_Charge_bleibt()
    {
        var api = OrderedApi();
        return Render("hell", "verwaltung-lager-lieferung-anderswo", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            FieldNamed(window, "Charge Massageöl").Text = "L-9";
            await Pump(Settling);
            api.On($"GET {Route("/orders")}", new[] { OrderedOrder with { Status = PurchaseOrderStatus.PartiallyReceived, Lines = [OrderedOrder.Lines[0] with { Received = 3m }] } });
            await stock.ReloadAsync();
            await Pump(Settling);

            Assert.Equal("2", FieldNamed(window, "Geliefert Massageöl (Flasche)").Text);
            Assert.Equal("L-9", FieldNamed(window, "Charge Massageöl").Text);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_offener_Bestelltext_When_die_Menge_geaendert_wird_Then_verschwindet_der_alte_Text()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-bestelltext-alt", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            await ClickOrder(window, "Bestelltext der Bestellung 7");
            Assert.Equal(OrderText, FieldNamed(window, "Bestelltext der Bestellung 7 zum Kopieren").Text);
            OrderQuantity(window).Text = "12";
            await Pump(Settling);

            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBox>(), t => t.IsEffectivelyVisible && AutomationProperties.GetName(t) == "Bestelltext der Bestellung 7 zum Kopieren");
            return shell.AdminPage;
        }, api);
    }

    private static readonly string[] DeliveryRefused = ["Gelieferte Menge angeben."];

    [Fact]
    public Task Given_API_lehnt_die_Lieferung_ab_When_gebucht_Then_steht_ihr_Text_da_und_die_Eingaben_bleiben()
    {
        var api = OrderedApi().On($"POST /api/stock/orders/{OrderId}/deliveries", new { errors = new { lines = DeliveryRefused } }, System.Net.HttpStatusCode.BadRequest);
        return Render("hell", "verwaltung-lager-lieferung-abgelehnt", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            FieldNamed(window, "Charge Massageöl").Text = "L-9";
            await Pump(Settling);
            await ClickOrder(window, "Lieferung zu Bestellung 7 buchen");

            Assert.True(Shows(window, "Gelieferte Menge angeben."));
            Assert.Equal("L-9", FieldNamed(window, "Charge Massageöl").Text);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Eingaben_zur_Lieferung_When_das_Lager_neu_laedt_Then_bleiben_sie_stehen_und_gelten_nicht_als_ungesichert()
    {
        var api = OrderedApi();
        return Render("hell", "verwaltung-lager-lieferung-neuladen", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            FieldNamed(window, "Geliefert Massageöl (Flasche)").Text = "2";
            FieldNamed(window, "Charge Massageöl").Text = "L-9";
            await Pump(Settling);
            await stock.ReloadAsync();
            await Pump(Settling);

            Assert.Equal("2", FieldNamed(window, "Geliefert Massageöl (Flasche)").Text);
            Assert.Equal("L-9", FieldNamed(window, "Charge Massageöl").Text);
            Assert.False(stock.HasUnsavedChanges);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Entwurf_mit_geaenderter_Menge_When_als_bestellt_markiert_Then_erst_speichern_dann_markieren_ohne_Mail()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-als-bestellt", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            OrderQuantity(window).Text = "12";
            await Pump(Settling);
            await ClickOrder(window, "Bestellung 7 als bestellt markieren");

            var calls = api.Calls.Select(c => c.Route).ToList();
            var put = calls.IndexOf($"PUT /api/stock/orders/{OrderId}");
            Assert.InRange(put, 0, calls.IndexOf($"POST /api/stock/orders/{OrderId}/ordered") - 1);
            Assert.Equal(0, CallsTo(api, $"POST /api/stock/orders/{OrderId}/send"));
            Assert.True(Shows(window, "Bestellung 7 als bestellt markiert."));
            Assert.Equal("12", FieldNamed(window, "Geliefert Massageöl (Flasche)").Text);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Bestellung_When_der_Bestelltext_geoeffnet_wird_Then_steht_der_Text_der_API_zum_Kopieren_da()
    {
        var api = OrderedApi();
        return Render("hell", "verwaltung-lager-bestelltext", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            await ClickOrder(window, "Bestelltext der Bestellung 7");

            Assert.Equal(1, CallsTo(api, $"GET /api/stock/orders/{OrderId}/text"));
            var text = FieldNamed(window, "Bestelltext der Bestellung 7 zum Kopieren");
            Assert.Equal(OrderText, text.Text);
            Assert.True(text.IsReadOnly);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_bestellte_Bestellung_When_storniert_Then_fragt_die_Maske_und_nennt_die_Absage_beim_Lieferanten()
    {
        var api = OrderedApi();
        return Render("hell", "verwaltung-lager-stornieren", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "OrdersTabButton");
            await Pump(Settling);

            await ClickOrder(window, "Bestellung 7 stornieren");
            var dialog = Assert.IsType<ConfirmDialogViewModel>(main.DialogHost.Current);
            Assert.Equal("Bestellung 7 stornieren?", dialog.Title);
            Assert.Contains("Lieferanten", dialog.Message, StringComparison.Ordinal);
            ClickCenter(window, DialogButton(window, "CancelButton"));
            await Pump(Settling);
            Assert.Equal(0, CallsTo(api, $"POST /api/stock/orders/{OrderId}/close"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_gebuchte_Termine_ueber_dem_Bestand_When_die_Bedarfspruefung_oeffnet_Then_zeigt_sie_den_Artikel_mit_der_Fehlmenge()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-bedarf", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            stock.SetTab(StockTab.Orders);
            await Pump(Settling);
            Named<Control>(window, "DemandList").BringIntoView();
            await Pump(Settling);

            Assert.True(Shows(window, "1 Hinweis zur Bedarfsprüfung"));
            Assert.False(Shows(window, "Massageöl: Bestand 10 Flasche reicht nicht für alle gebuchten Termine (Bedarf 12 Flasche)."));
            Named<Expander>(window, "DemandWarnings").IsExpanded = true;
            await Pump(Settling);
            Assert.True(Shows(window, "Massageöl: Bestand 10 Flasche reicht nicht für alle gebuchten Termine (Bedarf 12 Flasche)."));
            var row = window.GetVisualDescendants().OfType<Grid>().First(g => AutomationProperties.GetName(g)?.StartsWith("Massageöl, Bestand", StringComparison.Ordinal) == true);
            Assert.Equal("Massageöl, Bestand 10 Flasche, Bedarf 12 Flasche, es fehlen 2 Flasche, 4 Termine, danach noch für 0 Behandlungen", AutomationProperties.GetName(row));
            Assert.Contains(row.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "2 Flasche");
            Assert.Equal(1, CallsTo(api, $"GET {Route("/demand?from=2026-10-19&until=2026-10-25")}"));

            var period = Named<ComboBox>(window, "DemandPeriodField");
            period.SelectedItem = Choice(period, "Nächste 14 Tage");
            await Pump(Settling);
            Assert.Equal(1, CallsTo(api, $"GET {Route("/demand?from=2026-10-19&until=2026-11-01")}"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Charge_mit_Verbrauch_in_einem_Termin_When_die_Rueckverfolgung_oeffnet_Then_zeigt_sie_Bewegungen_und_Termin()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-rueckverfolgung", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "MovementsTabButton");
            await Pump(Settling);

            Assert.False(Named<Button>(window, "TraceBatchButton").IsEffectivelyEnabled);
            var item = Named<ComboBox>(window, "TraceItemField");
            item.SelectedItem = Choice(item, "Massageöl");
            await TypeInto(window, "TraceBatchField", " L-9 ");
            await ClickScrolled(window, "TraceBatchButton");
            await Pump(Settling);

            Assert.Equal(1, CallsTo(api, $"GET /api/stock/batch-usage?itemId={OilId}&batch=L-9"));
            Assert.True(Shows(window, "Massageöl, Charge L-9: 12 Flasche erhalten, 2 Flasche verwendet oder verkauft, 1 Kundin oder Kunde."));
            Assert.True(Shows(window, "Maria Gruber"));
            Assert.True(Shows(window, "Aromaöl-Massage"));
            Assert.True(Shows(window, "Behandlung"));
            Assert.True(Shows(window, "Beleg 42"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Given_Charge_ohne_Buchung_und_mit_Sonderzeichen_When_zurueckverfolgt_Then_sagt_die_Maske_dass_nichts_gebucht_ist()
    {
        var api = StockApi().On($"GET /api/stock/batch-usage?itemId={OilId}&batch=A%26B", BatchUsage with { Batch = "A&B", Received = 0, Used = 0, Entries = [] });
        return Render("hell", "verwaltung-lager-rueckverfolgung-leer", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "MovementsTabButton");
            await Pump(Settling);

            var item = Named<ComboBox>(window, "TraceItemField");
            item.SelectedItem = Choice(item, "Massageöl");
            await TypeInto(window, "TraceBatchField", "A&B");
            await ClickScrolled(window, "TraceBatchButton");
            await Pump(Settling);

            Assert.True(Shows(window, "Zu dieser Charge ist nichts gebucht."));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Hoechstmenge_im_Lagerort_speichert_ueber_Eingabe()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-hoechstmenge", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);
            await ClickScrolled(window, "PlacesTabButton");
            await Pump(Settling);
            stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Name == "Verkaufsregal");
            await Pump(Settling);
            await ClickScrolled(window, "AddMinimumButton");
            await Pump(Settling);
            var row = stock.PlacesAdmin.Minimums.Single();
            row.Item = row.Choices.Single(c => c.Id == OilId);
            await Pump(Settling);
            window.GetVisualDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == row.AmountName).Text = "2";
            window.GetVisualDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == row.MaximumName).Text = "20";
            await Pump(Settling);
            ClickCenter(window, window.GetVisualDescendants().OfType<SaveBar>().Single(b => b.IsEffectivelyVisible).GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Speichern"));
            await Pump(Settling);

            var sent = System.Text.Json.JsonSerializer.Deserialize<StoragePlaceInput>(Assert.Single(api.Calls, c => c.Route == $"PUT {Route($"/storage-places/{ShopId}")}").Body!, ApiClient.Json)!;
            Assert.Equal((2m, 20m), (sent.Minimums!.Single().MinimumQuantity, sent.Minimums!.Single().MaximumQuantity!.Value));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Lieferant_anlegen_ueber_Klick()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-lieferanten", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);

            await ClickScrolled(window, "SuppliersTabButton");
            await TypeInto(window, "SupplierNameField", "Haslauer");
            await TypeInto(window, "SupplierContactField", "Eva Muster");
            await TypeInto(window, "SupplierEmailField", "bestellung@haslauer.example");
            await TypeInto(window, "SupplierLeadTimeField", "5");
            await TypeInto(window, "SupplierMinimumOrderField", "150");
            var channel = Named<ComboBox>(window, "SupplierChannelField");
            channel.SelectedItem = Choice(channel, "Portal");
            await Pump(Settling);
            ClickCenter(window, window.GetVisualDescendants().OfType<SaveBar>().Single(b => b.IsEffectivelyVisible).GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Speichern"));
            await Pump(Settling);

            var sent = System.Text.Json.JsonSerializer.Deserialize<StockSupplierInput>(Assert.Single(api.Calls, c => c.Route == "POST /api/stock/suppliers").Body!, ApiClient.Json)!;
            Assert.Equal(("Haslauer", "Eva Muster", 5, 150m, SupplierOrderChannel.Portal), (sent.Name, sent.ContactPerson, sent.LeadTimeDays, sent.MinimumOrderValue, sent.OrderChannel));
            Assert.True(Shows(window, "Gespeichert."));
            Assert.True(Shows(window, "Haslauer"));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Material_aendern_ordnet_den_Lieferanten_zu()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-material-aendern", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);

            // „Ändern“ steht seit Slice 143 im Seitenpanel der gewählten Zeile
            stock.Overview.Select(TowelId);
            await Pump(Settling);
            var change = window.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Handtuch ändern");
            ClickCenter(window, change);
            await Pump(Settling);
            Assert.Equal("Handtuch", Named<TextBox>(window, "ItemNameField").Text);
            Assert.True(Shows(window, "Material ändern"));
            Assert.False(stock.HasUnsavedChanges);
            var supplier = Named<ComboBox>(window, "ItemSupplierField");
            supplier.SelectedItem = Choice(supplier, "Lavendelhof");
            await Pump(Settling);
            Assert.True(stock.HasUnsavedChanges);
            await ClickScrolled(window, "CreateItemButton");
            await Pump(Settling);

            var sent = System.Text.Json.JsonSerializer.Deserialize<StockItemInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/stock/items/{TowelId}").Body!, ApiClient.Json)!;
            Assert.Equal(("Handtuch", SupplierId, "Packung", 10m, "Karton", 5), (sent.Name, sent.SupplierId, sent.PackUnit, sent.PackSize, sent.CaseUnit, sent.PacksPerCase));
            Assert.True(Shows(window, "Handtuch gespeichert."));
            Assert.False(stock.HasUnsavedChanges);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Wareneingang_im_Karton_ueber_Klick()
    {
        var api = StockApi();
        return Render("hell", "verwaltung-lager-wareneingang-karton", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);

            await ClickScrolled(window, "BookMovementButton");
            Named<ComboBox>(window, "MovementItemField").SelectedItem = stock.Booking.Items.Single(i => i.Id == TowelId);
            await Pump(Settling);
            var package = Named<ComboBox>(window, "MovementPackageField");
            package.SelectedItem = Choice(package, "Karton (5 × 10 Stück)");
            await TypeInto(window, "MovementAmountField", "2");
            await Pump(Settling);
            Assert.True(Shows(window, "= 100 Stück"));
            Assert.True(Shows(window, "Preis netto je Karton"));
            await ClickScrolled(window, "BookButton");
            await Pump(Settling);

            var sent = SentMovement(api);
            Assert.Equal((TowelId, 2m, StockPackageLevel.Case), (sent.ItemId, sent.Quantity, sent.PackageLevel));
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Aufgaben_zeigen_Bald_ablaufend_mit_Menge_und_Lagerort() => Render("hell", "verwaltung-lager-aufgaben", async (window, main) =>
    {
        var (shell, _) = await OpenStock(window, main);

        await ClickScrolled(window, "TasksTabButton");
        await Pump(Settling);

        Assert.True(Shows(window, "Bald ablaufend"));
        Assert.True(Shows(window, "Abverkaufsliste"));
        Assert.True(Shows(window, "Abgelaufen"));
        Assert.True(Shows(window, "3 Flasche"));
        Assert.True(Shows(window, "Hauptlager"));
        Assert.True(Shows(window, "noch 20 Tage"));
        Assert.True(Shows(window, "Massageöl 100 ml"));
        return shell.AdminPage;
    }, StockApi());

    [Fact]
    public Task Lager_melden_keine_Bindungsfehler() => Render("hell", "bindungen-lager", async (window, main) =>
    {
        await WithBindingLog(async log =>
        {
            var shell = await OpenAdminPage(window, main);
            await AssertNoBindingErrors(log, "Lager öffnen", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Lager");
                return Task.CompletedTask;
            });
            var stock = shell.AdminPage.Stock!;
            await AssertNoBindingErrors(log, "Bestand", () => { stock.SetTab(StockTab.Stock); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Suche", () => { stock.ScanText = "ö"; stock.Overview.OnlyBelow = true; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Suche leer", () => { stock.ScanText = ""; stock.Overview.OnlyBelow = false; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Zeile gewählt", () => { stock.Overview.SelectedRow = stock.Overview.Rows[0]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Seitenpanel geschlossen", () => { stock.Overview.CloseDetailCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Übersicht", () => { stock.SetTab(StockTab.Overview); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Bestand wieder", () => { stock.SetTab(StockTab.Stock); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Lagerort gewählt", () => { stock.SelectedPlace = stock.PlaceFilter.Single(p => p.Id == ShopId); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Alle Lagerorte", () => { stock.SelectedPlace = stock.PlaceFilter[0]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Material anlegen", () => { stock.ItemForm.ItemName = "x"; stock.ItemForm.UnitCost = "abc"; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Material verworfen", () => { stock.ItemForm.Clear(); return Task.CompletedTask; });
            foreach (var kind in new[] { StockMovementKind.GoodsReceipt, StockMovementKind.Refill, StockMovementKind.Transfer, StockMovementKind.Correction })
            {
                await AssertNoBindingErrors(log, $"Leiste {kind}", () =>
                {
                    stock.Booking.Open(new MovementPreset(kind, OilId));
                    stock.Booking.Amount = "1";
                    return Task.CompletedTask;
                });
            }

            await AssertNoBindingErrors(log, "Leiste geschlossen", () => { stock.Booking.Close(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Bewegungen", () => { stock.SetTab(StockTab.Movements); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Inventur", async () =>
            {
                stock.SetTab(StockTab.Count);
                await stock.Count.StartCommand.ExecuteAsync(null);
                stock.Count.Rows[0].Entered = "-1";
                stock.Count.HideTarget = true;
            });
            await AssertNoBindingErrors(log, "Inventur abgebrochen", () => { stock.Count.Reset(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Chargen", () => { stock.SetTab(StockTab.Tasks); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Bestellen", () => { stock.SetTab(StockTab.Orders); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Bestellt", () => stock.Orders.Orders[0].MarkOrderedCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Bestelltext", () => stock.Orders.Orders[0].ShowTextCommand.ExecuteAsync(null));
            await AssertNoBindingErrors(log, "Rückverfolgung", async () =>
            {
                stock.SetTab(StockTab.Movements);
                stock.BatchTrace.SelectedItem = stock.BatchTrace.Items.Single(i => i.Id == OilId);
                stock.BatchTrace.Batch = "L-9";
                await stock.BatchTrace.TraceCommand.ExecuteAsync(null);
            });
            await AssertNoBindingErrors(log, "Lieferanten", () => { stock.SetTab(StockTab.Suppliers); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Neuer Lieferant", () => { stock.SuppliersAdmin.SelectedRow = stock.SuppliersAdmin.Rows.Last(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Material ändern", async () => { stock.SetTab(StockTab.Stock); await stock.Overview.ChangeAsync(TowelId); });
            await AssertNoBindingErrors(log, "Ändern abgebrochen", () => { stock.ItemForm.Clear(); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Lagerorte", () => { stock.SetTab(StockTab.Places); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Neuer Lagerort", () =>
            {
                stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Last();
                stock.PlacesAdmin.AddMinimumCommand.Execute(null);
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Schrank", () => { stock.PlacesAdmin.Discard(); stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Id == CabinetId); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Hauptlager", () => { stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows[0]; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Anderer Bereich", () =>
            {
                shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Behandlungen");
                return Task.CompletedTask;
            });
            log.AssertClean();
            return shell.AdminPage;
        });
        return true;
    }, StockApi());

    [Fact]
    public Task Jede_Seite_Lager_hat_fuer_jedes_bedienbare_Element_einen_lesbaren_Namen_und_Tab_erreicht_jedes_einmal() => Render("hell", "namen-lager", async (window, main) =>
    {
        var (shell, stock) = await OpenStock(window, main);
        var problems = new SortedSet<string>(StringComparer.Ordinal);

        async Task Scan(string state)
        {
            foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(c => c is Button or ToggleButton or CheckBox or TextBox or ComboBox or ListBoxItem))
            {
                if (control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control.Focusable && HasUnreadableName(control, out var name))
                {
                    problems.Add($"{state}: {control.GetType().Name}#{control.Name} \"{name}\"");
                }
            }

            foreach (var problem in await TabProblems(window, $"Lager, {state}"))
            {
                problems.Add(problem);
            }
        }

        stock.SetTab(StockTab.Overview);
        await Pump(Settling);
        await Scan("Übersicht");
        stock.SetTab(StockTab.Stock);
        await Pump(Settling);
        await Scan("Bestand");
        stock.Overview.Select(OilId);
        await Pump(Settling);
        await Scan("Bestand mit Seitenpanel");
        stock.Overview.Book(OilId);
        await Pump(Settling);
        await Scan("Bestand mit Leiste");
        stock.Booking.Close();
        stock.SetTab(StockTab.Movements);
        await Pump(Settling);
        await Scan("Bewegungen");
        stock.SetTab(StockTab.Count);
        await Pump(Settling);
        await Scan("Inventur");
        await stock.Count.StartCommand.ExecuteAsync(null);
        await Pump(Settling);
        await Scan("Zählliste");
        stock.Count.Reset();
        stock.SetTab(StockTab.Places);
        await Pump(Settling);
        await Scan("Lagerorte");
        stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Id == CabinetId);
        await Pump(Settling);
        await Scan("Schrank mit Mindestmengen");
        stock.PlacesAdmin.Discard();
        stock.SetTab(StockTab.Tasks);
        await Pump(Settling);
        await Scan("Aufgaben");
        stock.SetTab(StockTab.Suppliers);
        await Pump(Settling);
        await Scan("Lieferanten");
        stock.SetTab(StockTab.Orders);
        await Pump(Settling);
        await Scan("Bestellungen, Entwurf");
        await stock.Orders.Orders[0].MarkOrderedCommand.ExecuteAsync(null);
        await Pump(Settling);
        await Scan("Bestellungen, bestellt");

        Assert.True(problems.Count == 0, $"{problems.Count} Abweichungen:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        return shell.AdminPage;
    }, StockApi());
}
