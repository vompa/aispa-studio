using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 101 (Ä191): native Maske „Lager“ nach web/src/stock (Bestand, Material anlegen, Bewegung buchen, Inventur, Lagerorte). Das ViewModel prüft den gesendeten Inhalt,
/// die Rechte und die Rückfragen, nicht die Wirkung im Server (Abbuchung an der Kassa, Lagerrechnung: vorhandene API-Tests).
/// </summary>
public sealed class AdminStockTests
{
    private static readonly string[] AlreadyThere = ["Dieses Material gibt es schon."];
    private static readonly string[] NotEnough = ["Der Bestand reicht nicht."];
    private static readonly string[] CountAgain = ["Zähle neu."];

    private static async Task<StockViewModel> Open(StubApi api, FakeDialogs? dialogs = null, CurrentUser? user = null)
    {
        var stock = new StockViewModel(api.Client(), user ?? TestData.Owner(), new FixedTime()) { Dialogs = dialogs };
        await stock.EnsureLoadedAsync();
        return stock;
    }

    private static CurrentUser WithRights(params AccessRight[] rights) =>
        new("u9", "recht@aispa.test", "Rolf", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, true, []), null, rights);

    private static async Task Chosen(StockViewModel stock, Action<MovementBookingViewModel> fill)
    {
        stock.Booking.Open();
        fill(stock.Booking);
        await Task.Yield();
    }

    private static StockItemInput SentItem(StubApi api) => JsonSerializer.Deserialize<StockItemInput>(api.Calls.Single(c => c.Route == "POST /api/stock/items").Body!, ApiClient.Json)!;

    // ── Bedarf und Rückverfolgung (Slice 123) ──

    [Fact]
    public async Task Given_nur_das_Recht_zu_buchen_When_das_Lager_laedt_Then_laedt_der_Bedarf_und_die_Bestellungen_nicht()
    {
        var api = Api();

        var stock = await Open(api, user: WithRights(AccessRight.Book));

        Assert.Contains(api.Calls, c => c.Route == $"GET {Route("/demand?from=2027-03-01&until=2027-03-07")}");
        Assert.DoesNotContain(api.Calls, c => c.Route == $"GET {Route("/orders")}");
        Assert.False(stock.CanManage);
    }

    [Fact]
    public async Task Given_ohne_das_Recht_zu_buchen_When_das_Lager_laedt_Then_fragt_es_den_Bedarf_nicht_an()
    {
        var api = Api();

        await Open(api, user: WithRights(AccessRight.ViewCalendar));

        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/demand", StringComparison.Ordinal));
    }

    // ── Bestand ──

    [Fact]
    public async Task Given_10_Flaschen_Massageoel_When_die_Kassa_2_verkauft_und_das_Lager_neu_laedt_Then_zeigt_das_native_Lager_8()
    {
        var quantity = 10m;
        var stock = await Open(Api(oil: () => quantity));
        Assert.Equal("10", stock.Overview.Rows.Single(r => r.ItemId == OilId).QuantityText);

        quantity -= 2; // die Kassa bucht den Verkauf ab (API-Test: PosStockTests)
        await stock.ReloadAsync();

        Assert.Equal("8", stock.Overview.Rows.Single(r => r.ItemId == OilId).QuantityText);
    }

    [Fact]
    public async Task Given_Bestand_When_die_Maske_laedt_Then_zeigt_sie_Kennzahlen_Zeilen_und_Hinweise_wie_im_Web()
    {
        var stock = await Open(Api());

        Assert.Equal(["Massageöl", "Handtuch"], stock.Overview.Rows.Select(r => r.Name));
        Assert.Equal("140,00 €", stock.Overview.ValueText);
        var towel = stock.Overview.Rows.Single(r => r.ItemId == TowelId);
        Assert.True(towel.Below);
        Assert.Equal("5", towel.SuggestedText);
        Assert.Equal("Hauptlager: 9 · Verkaufsregal: 1", stock.Overview.Rows[0].PlacesText);
    }

    [Fact]
    public async Task Given_Suche_und_Nur_unter_Minimum_When_gesetzt_Then_filtert_die_Liste()
    {
        var stock = await Open(Api());

        stock.Overview.Search = "tuch";
        Assert.Equal(["Handtuch"], stock.Overview.Rows.Select(r => r.Name));
        stock.Overview.Search = "";
        stock.Overview.OnlyBelow = true;
        Assert.Equal(["Handtuch"], stock.Overview.Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Given_ein_Lagerort_gewaehlt_When_die_Liste_aufgebaut_wird_Then_zeigt_sie_dessen_Bestand_ohne_Standortspalten()
    {
        var stock = await Open(Api());

        stock.SelectedPlace = stock.PlaceFilter.Single(p => p.Id == ShopId);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.False(stock.Overview.ShowSiteColumns);
        Assert.Equal(["Massageöl"], stock.Overview.Rows.Select(r => r.Name));
        Assert.Equal("12,50 €", stock.Overview.ValueText);
        Assert.True(stock.Overview.Rows[0].CanRefill);
        Assert.False(stock.Overview.Rows[0].SiteColumns);
    }

    // ── Material anlegen ──

    [Fact]
    public async Task Given_Material_When_angelegt_Then_POST_mit_allen_Feldern_und_Rueckmeldung()
    {
        var api = Api();
        var stock = await Open(api);
        var form = stock.ItemForm;

        form.ItemName = "  Sauna-Aufguss ";
        form.Unit = "ml";
        form.UnitCost = "0,04";
        form.MinimumQuantity = "500";
        form.SelectedArticle = form.Articles.Single(a => a.Id == ArticleId);
        var supplier = form.Suppliers.Single(s => s.Name == "Lavendelhof");
        form.SelectedSupplier = supplier;
        form.SupplierArticleNumber = "SA-7";
        Assert.True(stock.HasUnsavedChanges);
        Assert.True(await form.CreateItemAsync());

        var sent = SentItem(api);
        Assert.Equal(("Sauna-Aufguss", "ml", 0.04m, 500m, ArticleId, true, supplier.Id, "SA-7"),
            (sent.Name, sent.Unit, sent.UnitCost, sent.MinimumQuantity, sent.ArticleId, sent.IsActive, sent.SupplierId, sent.SupplierArticleNumber));
        Assert.Equal("Sauna-Aufguss angelegt.", stock.Message);
        Assert.Equal("", form.ItemName);
        Assert.Equal("Stück", form.Unit);
        Assert.False(stock.HasUnsavedChanges);
        Assert.Contains(stock.Items, i => i.Name == "Sauna-Aufguss");
    }

    [Fact]
    public async Task Given_Preis_mit_vier_Stellen_When_nur_der_Lieferant_geaendert_Then_bleibt_der_aktuelle_Preis_des_Lagers()
    {
        var api = Api();
        var stock = await Open(api);
        var form = stock.ItemForm;

        // Das Formular steht auf einem älteren Stand; inzwischen hat ein Wareneingang den Preis des Lagers (3 €) geändert.
        form.Edit(Towel with { UnitCost = 0.0039m });
        Assert.Equal("0,0039", form.UnitCost);
        form.SelectedSupplier = form.Suppliers.Single(s => s.Name == "Lavendelhof");
        Assert.True(await form.CreateItemAsync());

        var sent = System.Text.Json.JsonSerializer.Deserialize<StockItemInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/stock/items/{TowelId}").Body!, ApiClient.Json)!;
        Assert.Equal(3m, sent.UnitCost);
    }

    [Theory]
    [InlineData("Flasche", "", "", "", StockItemFormViewModel.PackSizeField)]
    [InlineData("", "500", "", "", StockItemFormViewModel.PackUnitField)]
    [InlineData("", "", "Karton", "4", StockItemFormViewModel.PackUnitField)]
    [InlineData("Flasche", "500", "Karton", "", StockItemFormViewModel.PacksPerCaseField)]
    [InlineData("Flasche", "500", "", "4", StockItemFormViewModel.CaseUnitField)]
    [InlineData("Flasche", "500", "Karton", "0", StockItemFormViewModel.PacksPerCaseField)]
    public async Task Given_unvollstaendige_Packung_When_angelegt_Then_Fehler_am_Feld_und_kein_Aufruf(string pack, string size, string container, string count, string field)
    {
        var api = Api();
        var form = (await Open(api)).ItemForm;
        form.ItemName = "Öl";
        form.PackUnit = pack;
        form.PackSize = size;
        form.CaseUnit = container;
        form.PacksPerCase = count;

        Assert.False(await form.CreateItemAsync());

        Assert.Equal(field, form.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/stock/items");
    }

    [Theory]
    [InlineData("LAVENDELHOF", "", "", StockSuppliersViewModel.NameField)]
    [InlineData("Neu", "keine-adresse", "", StockSuppliersViewModel.EmailField)]
    [InlineData("Neu", "", "400", StockSuppliersViewModel.LeadTimeField)]
    public async Task Given_ungueltiger_Lieferant_When_gespeichert_Then_Fehler_am_Feld_und_kein_Aufruf(string name, string mail, string leadTime, string field)
    {
        var api = Api();
        var stock = await Open(api);
        var suppliers = stock.SuppliersAdmin;
        suppliers.SupplierName = name;
        suppliers.Email = mail;
        suppliers.LeadTimeDays = leadTime;

        Assert.False(await suppliers.SaveChangesAsync());

        Assert.Equal(field, suppliers.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/stock/suppliers");
    }

    [Fact]
    public async Task Given_Lieferanten_laden_scheitert_When_neu_geladen_Then_Meldung_und_der_alte_Stand_bleibt()
    {
        var api = Api();
        var stock = await Open(api);
        var count = stock.ItemForm.Suppliers.Count;
        api.On("GET /api/stock/suppliers", new { }, HttpStatusCode.InternalServerError);

        await stock.ReloadAsync();

        Assert.Equal(count, stock.ItemForm.Suppliers.Count);
        Assert.NotNull(stock.Error);
    }

    [Fact]
    public async Task Given_Zaehlung_und_Lieferant_offen_When_ein_anderer_Lieferant_gewaehlt_und_gespeichert_Then_bleibt_die_Zaehlung_ungebucht()
    {
        var api = Api();
        var stock = await Open(api);
        await stock.Count.StartCommand.ExecuteAsync(null);
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "7,5";
        Assert.True(stock.Count.HasUnsavedChanges);
        var dialogs = new SavingDialogs();
        stock.Dialogs = dialogs;
        stock.SuppliersAdmin.SelectedRow = stock.SuppliersAdmin.Rows[0];
        stock.SuppliersAdmin.ContactPerson = "Eva";

        stock.SuppliersAdmin.SelectedRow = stock.SuppliersAdmin.Rows.Last();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(1, dialogs.Asked);
        Assert.True(stock.Count.HasUnsavedChanges);
        Assert.False(stock.SuppliersAdmin.HasUnsavedChanges);
        Assert.Contains(api.Calls, c => c.Route == $"PUT /api/stock/suppliers/{SupplierId}");
    }

    private sealed class SavingDialogs : IConfirmDialogs
    {
        public int Asked { get; private set; }

        public Task<ConfirmResult> AskAsync(ConfirmRequest request)
        {
            Asked++;
            return Task.FromResult(new ConfirmResult(ConfirmChoice.Confirmed));
        }

        public Task<ConfirmResult> ShowAsync(Func<Action<ConfirmResult>, ConfirmDialogViewModel> create) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Given_nur_Bezeichnung_und_Einheit_When_angelegt_Then_Preis_und_Mindestbestand_0_und_Felder_null()
    {
        var api = Api();
        var stock = await Open(api);

        stock.ItemForm.ItemName = "Kerze";
        Assert.True(await stock.ItemForm.CreateItemAsync());

        var sent = SentItem(api);
        Assert.Equal((0m, 0m, (Guid?)null, (Guid?)null, (string?)null), (sent.UnitCost, sent.MinimumQuantity, sent.ArticleId, sent.SupplierId, sent.SupplierArticleNumber));
    }

    [Fact]
    public async Task Given_Bezeichnung_oder_Einheit_fehlt_When_angelegt_Then_Knopf_aus_Fehler_am_Feld_und_kein_POST()
    {
        var api = Api();
        var stock = await Open(api);
        var form = stock.ItemForm;

        Assert.False(form.CreateCommand.CanExecute(null));
        Assert.False(await form.CreateItemAsync());
        Assert.Equal("Die Bezeichnung fehlt. Trage sie ein.", form.NameError);
        Assert.Equal(StockItemFormViewModel.NameField, form.FocusTarget);

        form.ItemName = "Kerze";
        form.Unit = " ";
        Assert.False(form.CreateCommand.CanExecute(null));
        Assert.False(await form.CreateItemAsync());
        Assert.Equal(StockItemFormViewModel.UnitField, form.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/stock/items");
    }

    [Fact]
    public async Task Given_ein_unlesbarer_Preis_When_angelegt_Then_Fehler_am_Feld_und_kein_POST()
    {
        var api = Api();
        var stock = await Open(api);
        stock.ItemForm.ItemName = "Kerze";
        stock.ItemForm.UnitCost = "viel";
        stock.ItemForm.MinimumQuantity = "-1";

        Assert.False(await stock.ItemForm.CreateItemAsync());

        Assert.NotNull(stock.ItemForm.UnitCostError);
        Assert.NotNull(stock.ItemForm.MinimumError);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/stock/items");
    }

    [Fact]
    public async Task Given_die_API_lehnt_das_Material_ab_When_angelegt_Then_steht_der_Fehler_da_und_die_Eingabe_bleibt()
    {
        var api = Api().On("POST /api/stock/items", new { errors = new { name = AlreadyThere } }, HttpStatusCode.BadRequest);
        var stock = await Open(api);
        stock.ItemForm.ItemName = "Massageöl";

        Assert.False(await stock.ItemForm.CreateItemAsync());

        Assert.Equal("Dieses Material gibt es schon.", stock.ItemForm.Error);
        Assert.Equal("Massageöl", stock.ItemForm.ItemName);
        Assert.True(stock.ItemForm.IsDirty);
    }

    // ── Bewegung buchen ──

    [Fact]
    public async Task Given_Wareneingang_When_gebucht_Then_POST_mit_Menge_Preis_Charge_Ablauf_und_Hauptlager()
    {
        var api = Api();
        var stock = await Open(api);
        await Chosen(stock, b =>
        {
            b.SelectedItem = b.Items.Single(i => i.Id == OilId);
            b.Amount = "5,5";
            b.UnitCost = "11,90";
            b.Batch = "L-2";
            b.ExpiresOn = new DateOnly(2027, 12, 31);
            b.Reason = "Lieferung";
        });

        Assert.True(await stock.Booking.BookMovementAsync());

        var sent = SentMovement(api);
        Assert.Equal((OilId, StockMovementKind.GoodsReceipt, 5.5m, (decimal?)11.9m, "L-2", new DateOnly(2027, 12, 31), MainId, (Guid?)null, (Guid?)null, "Lieferung"),
            (sent.ItemId, sent.Kind, sent.Quantity, sent.UnitCost, sent.Batch, sent.ExpiresOn, sent.StoragePlaceId, sent.TargetStoragePlaceId, sent.TargetLocationId, sent.Reason));
        Assert.Equal("Gebucht.", stock.Message);
        Assert.Null(stock.Booking.SelectedItem);
        Assert.Equal("", stock.Booking.Amount);
    }

    [Fact]
    public async Task Given_Nachfuellen_When_gebucht_Then_POST_vom_Hauptlager_an_den_Lagerort()
    {
        var api = Api();
        var stock = await Open(api);
        stock.Overview.Refill(OilId);
        stock.Booking.Amount = "2";

        Assert.Equal(StockMovementKind.Refill, stock.Booking.Kind);
        Assert.True(await stock.Booking.BookMovementAsync());

        var sent = SentMovement(api);
        Assert.Equal((OilId, StockMovementKind.Refill, 2m, MainId), (sent.ItemId, sent.Kind, sent.Quantity, sent.StoragePlaceId));
        Assert.NotNull(sent.TargetStoragePlaceId);
        Assert.Null(sent.TargetLocationId);
        Assert.Null(sent.Batch);
    }

    [Fact]
    public async Task Given_Umbuchen_an_einen_anderen_Standort_When_gebucht_Then_POST_mit_Zielstandort_ohne_Ziel_Lagerort()
    {
        var api = Api(two: true);
        var stock = await Open(api);
        await Chosen(stock, b =>
        {
            b.Kind = StockMovementKind.Transfer;
            b.SelectedItem = b.Items.Single(i => i.Id == OilId);
            b.Amount = "1";
            b.SelectedTarget = b.Targets.Single(t => t.Key == $"location:{Salzburg.Id}");
        });

        Assert.True(await stock.Booking.BookMovementAsync());

        var sent = SentMovement(api);
        Assert.Equal((StockMovementKind.Transfer, (Guid?)Salzburg.Id, (Guid?)null), (sent.Kind, sent.TargetLocationId, sent.TargetStoragePlaceId));
    }

    [Fact]
    public async Task Given_Umbuchen_an_einen_Lagerort_When_gebucht_Then_POST_mit_Ziel_Lagerort()
    {
        var api = Api();
        var stock = await Open(api);
        await Chosen(stock, b =>
        {
            b.Kind = StockMovementKind.Transfer;
            b.SelectedItem = b.Items.Single(i => i.Id == TowelId);
            b.Amount = "3";
            b.SelectedTarget = b.Targets.Single(t => t.Key == $"place:{CabinetId}");
        });

        Assert.True(await stock.Booking.BookMovementAsync());

        var sent = SentMovement(api);
        Assert.Equal((StockMovementKind.Transfer, (Guid?)CabinetId, (Guid?)null, MainId), (sent.Kind, sent.TargetStoragePlaceId, sent.TargetLocationId, sent.StoragePlaceId));
    }

    [Fact]
    public async Task Given_Korrektur_When_mit_Vorzeichen_und_Grund_gebucht_Then_POST_mit_negativer_Menge()
    {
        var api = Api();
        var stock = await Open(api);
        await Chosen(stock, b =>
        {
            b.Kind = StockMovementKind.Correction;
            b.SelectedItem = b.Items.Single(i => i.Id == OilId);
            b.Amount = "-2";
            b.Reason = "Bruch";
        });

        Assert.True(await stock.Booking.BookMovementAsync());

        var sent = SentMovement(api);
        Assert.Equal((StockMovementKind.Correction, -2m, "Bruch", (decimal?)null, (string?)null), (sent.Kind, sent.Quantity, sent.Reason, sent.UnitCost, sent.Batch));
    }

    [Theory]
    [InlineData(StockMovementKind.GoodsReceipt, "0", "")]
    [InlineData(StockMovementKind.GoodsReceipt, "-3", "")]
    [InlineData(StockMovementKind.GoodsReceipt, "viel", "")]
    [InlineData(StockMovementKind.GoodsReceipt, "", "")]
    [InlineData(StockMovementKind.Correction, "0", "Bruch")]
    [InlineData(StockMovementKind.Correction, "-2", "")]
    [InlineData(StockMovementKind.Correction, "-2", "  ")]
    public async Task Given_ungueltige_Eingabe_When_gebucht_Then_Knopf_aus_und_nichts_geht_an_die_API(StockMovementKind kind, string amount, string reason)
    {
        var api = Api();
        var stock = await Open(api);
        await Chosen(stock, b =>
        {
            b.Kind = kind;
            b.SelectedItem = b.Items.Single(i => i.Id == OilId);
            b.Amount = amount;
            b.Reason = reason;
        });

        Assert.False(stock.Booking.BookCommand.CanExecute(null));
        Assert.False(await stock.Booking.BookMovementAsync());
        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {Route("/movements")}");
        Assert.NotNull(stock.Booking.Error);
    }

    [Fact]
    public async Task Given_kein_Material_gewaehlt_When_gebucht_Then_nichts_geht_an_die_API_und_der_Fokus_geht_ins_Material()
    {
        var api = Api();
        var stock = await Open(api);
        stock.Booking.Open();
        stock.Booking.Amount = "2";
        stock.Booking.FocusTarget = null;

        Assert.False(await stock.Booking.BookMovementAsync());

        Assert.Equal(MovementBookingViewModel.ItemField, stock.Booking.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {Route("/movements")}");
    }

    [Fact]
    public async Task Given_die_API_lehnt_die_Buchung_ab_When_gebucht_Then_steht_der_Fehler_da_und_die_Eingabe_bleibt()
    {
        var api = Api().On($"POST {Route("/movements")}", new { errors = new { quantity = NotEnough } }, HttpStatusCode.BadRequest);
        var stock = await Open(api);
        await Chosen(stock, b =>
        {
            b.Kind = StockMovementKind.Correction;
            b.SelectedItem = b.Items.Single(i => i.Id == OilId);
            b.Amount = "-50";
            b.Reason = "Bruch";
        });

        Assert.False(await stock.Booking.BookMovementAsync());

        Assert.Equal("Der Bestand reicht nicht.", stock.Booking.Error);
        Assert.Equal("-50", stock.Booking.Amount);
    }

    [Fact]
    public async Task Given_nur_ein_Lagerort_und_ein_Standort_When_die_Leiste_oeffnet_Then_nur_Wareneingang_und_Korrektur()
    {
        var api = Api();
        var stock = await Open(api);
        var single = new List<StoragePlaceDto> { Main };
        api.On($"GET {Route("/storage-places")}", single);
        await stock.ReloadAsync();

        stock.Booking.Open();

        Assert.False(stock.Booking.ShowRefillTab);
        Assert.False(stock.Booking.ShowTransferTab);
        Assert.False(stock.Booking.ShowPlace);
    }

    [Fact]
    public async Task Given_abgelaufene_Charge_When_ausgebucht_Then_Korrektur_mit_Grund_und_Menge_negativ()
    {
        var expired = new StockBatchDto(OilId, "Massageöl", "Flasche", "L-0", new DateOnly(2027, 2, 1), 3, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), true, true, MainId, "Hauptlager");
        var api = Api().On($"GET {Route("/batches")}", new StockBatchesDto(Vienna.Id, new DateOnly(2027, 3, 1), 14, [expired]));
        var stock = await Open(api);

        Assert.Single(stock.Todo.Items, i => i.Title == "1 Charge ist abgelaufen");
        Assert.True(stock.Overview.Rows[0].Expired);
        await stock.Overview.WriteOffAsync(expired);

        var sent = SentMovement(api);
        Assert.Equal((StockMovementKind.Correction, -3m, MainId, "Abgelaufen, Charge L-0 (1.2.2027)"), (sent.Kind, sent.Quantity, sent.StoragePlaceId, sent.Reason));
        Assert.Equal("3 Flasche Massageöl ausgebucht.", stock.Message);
    }

    // ── Bewegungen ──

    [Fact]
    public async Task Given_der_Reiter_Bewegungen_When_geoeffnet_Then_lädt_er_die_letzten_30_Tage_und_zeigt_Art_Menge_und_Grund()
    {
        var api = Api();
        var stock = await Open(api);

        stock.SetTab(StockTab.Movements);
        await stock.Movements.LoadAsync();

        Assert.Equal(["Wareneingang", "Korrektur"], stock.Movements.Rows.Select(r => r.KindText));
        Assert.Equal(["12", "-2"], stock.Movements.Rows.Select(r => r.QuantityText));
        Assert.True(stock.Movements.Rows[1].IsNegative);
        Assert.Contains("Bruch", stock.Movements.Rows[1].Detail, StringComparison.Ordinal);
        Assert.Contains("Charge L-1", stock.Movements.Rows[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_Ende_vor_Beginn_When_gewaehlt_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = Api();
        var stock = await Open(api);
        await stock.Movements.LoadAsync();
        var calls = api.Calls.Count;

        stock.Movements.Until = new DateOnly(2027, 1, 1);
        await Task.Delay(30, TestContext.Current.CancellationToken);

        Assert.NotNull(stock.Movements.PeriodError);
        Assert.Equal(calls, api.Calls.Count);
    }

    [Fact]
    public async Task Given_Bewegungen_mehrerer_Lagerorte_When_ein_Lagerort_gefiltert_wird_Then_nur_dessen_Bewegungen()
    {
        var shopMove = Movement(StockMovementKind.Refill, 1) with { StoragePlaceId = ShopId, StoragePlaceName = "Verkaufsregal" };
        var api = Api(movements: [Movement(StockMovementKind.GoodsReceipt, 12), shopMove]);
        var stock = await Open(api);
        await stock.Movements.LoadAsync();

        stock.SelectedPlace = stock.PlaceFilter.Single(p => p.Id == ShopId);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(["Nachfüllen"], stock.Movements.Rows.Select(r => r.KindText));
    }

    // ── Inventur ──

    private static async Task<StockViewModel> Counting(StubApi api, FakeDialogs? dialogs = null)
    {
        var stock = await Open(api, dialogs);
        stock.SetTab(StockTab.Count);
        await stock.Count.StartCommand.ExecuteAsync(null);
        return stock;
    }

    [Fact]
    public async Task Given_Inventur_mit_einer_Abweichung_When_gebucht_Then_nur_diese_Zeile_als_Count_mit_der_gezaehlten_Menge()
    {
        var api = Api();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var stock = await Counting(api, dialogs);
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "7,5";
        stock.Count.Rows.Single(r => r.Item.Id == TowelId).Entered = "5"; // Soll 5: keine Abweichung

        Assert.Equal(1, stock.Count.ChangeCount);
        Assert.True(await stock.Count.BookCountAsync());

        var sent = Assert.Single(SentMovements(api));
        Assert.Equal((OilId, StockMovementKind.Count, 7.5m, MainId), (sent.ItemId, sent.Kind, sent.Quantity, sent.StoragePlaceId));
        Assert.Equal("Inventur gebucht: 1 Position.", stock.Message);
        Assert.False(stock.Count.IsCounting);
        Assert.False(stock.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Inventur_When_gebucht_Then_fragt_die_Maske_vorher_und_Abbrechen_bucht_nichts()
    {
        var api = Api();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var stock = await Counting(api, dialogs);
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "8";

        Assert.False(await stock.Count.BookCountAsync());

        var asked = dialogs.AssertRules().Asked.Single();
        Assert.Equal("Inventur buchen?", asked.Title);
        Assert.Contains("Hauptlager", asked.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {Route("/movements")}");
        Assert.Equal("8", stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered);
    }

    [Fact]
    public async Task Given_Zaehlung_When_Differenz_und_Wert_berechnet_werden_Then_gegen_das_Soll_des_Lagerorts()
    {
        var stock = await Counting(Api());
        var oil = stock.Count.Rows.Single(r => r.Item.Id == OilId);

        oil.Entered = "7";

        Assert.Equal("10", oil.TargetText);
        Assert.Equal("-3", oil.DifferenceText);
        Assert.Equal("-37,50 €", oil.WorthText);
        Assert.True(oil.DifferenceIsNegative);
        Assert.Equal("-37,50 €", stock.Count.WorthSumText);
        Assert.Equal("Summe der Differenzen (1 Position)", stock.Count.SumText);
    }

    [Fact]
    public async Task Given_ein_negativer_gezaehlter_Bestand_When_gebucht_Then_gesperrt_und_nichts_geht_an_die_API()
    {
        var api = Api();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var stock = await Counting(api, dialogs);
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "-1";
        stock.Count.Rows.Single(r => r.Item.Id == TowelId).Entered = "3";

        Assert.True(stock.Count.HasInvalid);
        Assert.False(stock.Count.BookCommand.CanExecute(null));
        Assert.False(await stock.Count.BookCountAsync());
        Assert.Empty(dialogs.Asked);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {Route("/movements")}");
    }

    [Fact]
    public async Task Given_Zaehlung_ohne_Abweichung_When_Inventur_buchen_Then_aus()
    {
        var api = Api();
        var stock = await Counting(api, new FakeDialogs().Answer(ConfirmChoice.Confirmed));

        Assert.False(stock.Count.BookCommand.CanExecute(null));
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "10";
        stock.Count.Rows.Single(r => r.Item.Id == TowelId).Entered = "5,0";

        Assert.Equal(0, stock.Count.ChangeCount);
        Assert.False(stock.Count.BookCommand.CanExecute(null));
        Assert.False(await stock.Count.BookCountAsync());
        Assert.DoesNotContain(api.Calls, c => c.Route == $"POST {Route("/movements")}");
    }

    [Fact]
    public async Task Given_Soll_ausblenden_When_gesetzt_Then_zeigen_die_Zeilen_kein_Soll()
    {
        var stock = await Counting(Api());

        stock.Count.HideTarget = true;

        Assert.All(stock.Count.Rows, r => Assert.False(r.ShowTarget));
        stock.Count.HideTarget = false;
        Assert.All(stock.Count.Rows, r => Assert.True(r.ShowTarget));
    }

    [Fact]
    public async Task Given_Inventur_When_ein_Buchen_scheitert_Then_steht_der_Fehler_mit_dem_Material_da()
    {
        var api = Api().On($"POST {Route("/movements")}", new { errors = new { quantity = CountAgain } }, HttpStatusCode.BadRequest);
        var stock = await Counting(api, new FakeDialogs().Answer(ConfirmChoice.Confirmed));
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "8";

        Assert.False(await stock.Count.BookCountAsync());

        Assert.Equal("Massageöl: Zähle neu.", stock.Count.Error);
        Assert.True(stock.Count.IsCounting);
    }

    // ── Lagerorte ──

    [Fact]
    public async Task Given_Lagerorte_When_der_Reiter_laedt_Then_Liste_mit_Neuer_Lagerort_und_das_Hauptlager_ist_gewaehlt()
    {
        var stock = await Open(Api());

        Assert.Equal(["Hauptlager", "Verkaufsregal", "Schrank Zeder", "Neuer Lagerort"], stock.PlacesAdmin.Rows.Select(r => r.Name));
        Assert.Equal("Hauptlager", stock.PlacesAdmin.HeadingText);
        Assert.True(stock.PlacesAdmin.IsMain);
        Assert.False(stock.PlacesAdmin.CanDelete);
        Assert.Equal(["Hauptlager"], stock.PlacesAdmin.KindOptions.Select(k => k.Label));
    }

    [Fact]
    public async Task Given_neuer_Lagerort_When_gespeichert_Then_POST_mit_Art_Raum_und_Mindestmengen()
    {
        var api = Api();
        var stock = await Open(api);
        var places = stock.PlacesAdmin;

        places.SelectedRow = places.Rows.Last();
        await Task.Yield();
        places.PlaceName = "Kühlschrank";
        places.SelectedKind = places.KindOptions.Single(k => k.Kind == StoragePlaceKind.Room);
        places.SelectedRoom = places.RoomOptions.Single(r => r.Id == RoomId);
        places.AddMinimumCommand.Execute(null);
        places.Minimums[0].Item = places.Minimums[0].Choices.Single(i => i.Id == OilId);
        places.Minimums[0].Amount = "3";
        Assert.True(places.HasUnsavedChanges);
        Assert.True(await places.SaveChangesAsync());

        var post = JsonSerializer.Deserialize<StoragePlaceInput>(Assert.Single(api.Calls, c => c.Route == $"POST {Route("/storage-places")}").Body!, ApiClient.Json)!;
        Assert.Equal(("Kühlschrank", StoragePlaceKind.Room, (Guid?)RoomId, true), (post.Name, post.Kind, post.RoomId, post.IsActive));
        Assert.Equal([new StoragePlaceMinimumDto(OilId, 3)], post.Minimums);
        Assert.Equal("Gespeichert.", stock.Message);
        Assert.Equal("Kühlschrank", stock.PlacesAdmin.HeadingText);
    }

    [Fact]
    public async Task Given_ein_Lagerort_When_geaendert_und_gespeichert_Then_PUT()
    {
        var api = Api();
        var stock = await Open(api);
        var places = stock.PlacesAdmin;
        places.SelectedRow = places.Rows.Single(r => r.Id == ShopId);
        await Task.Yield();

        places.PlaceName = "Regal Empfang";
        places.IsActive = false;
        Assert.True(await places.SaveChangesAsync());

        var put = JsonSerializer.Deserialize<StoragePlaceInput>(Assert.Single(api.Calls, c => c.Route == $"PUT {Route($"/storage-places/{ShopId}")}").Body!, ApiClient.Json)!;
        Assert.Equal(("Regal Empfang", StoragePlaceKind.Sales, (Guid?)null, false), (put.Name, put.Kind, put.RoomId, put.IsActive));
        Assert.Equal("Regal Empfang", stock.PlacesAdmin.HeadingText);
    }

    [Fact]
    public async Task Given_das_Hauptlager_When_umbenannt_Then_bleibt_es_Hauptlager_und_aktiv()
    {
        var api = Api();
        var stock = await Open(api);

        stock.PlacesAdmin.PlaceName = "Zentrallager";
        stock.PlacesAdmin.IsActive = false;
        Assert.True(await stock.PlacesAdmin.SaveChangesAsync());

        var put = JsonSerializer.Deserialize<StoragePlaceInput>(Assert.Single(api.Calls, c => c.Route == $"PUT {Route($"/storage-places/{MainId}")}").Body!, ApiClient.Json)!;
        Assert.Equal(("Zentrallager", StoragePlaceKind.Main, true), (put.Name, put.Kind, put.IsActive));
    }

    [Fact]
    public async Task Given_Lagerort_ohne_Name_When_gespeichert_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = Api();
        var stock = await Open(api);
        stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Last();
        await Task.Yield();

        Assert.False(await stock.PlacesAdmin.SaveChangesAsync());

        Assert.Equal("Der Name fehlt. Trage ihn ein.", stock.PlacesAdmin.NameError);
        Assert.Equal(StoragePlacesViewModel.NameField, stock.PlacesAdmin.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal) && c.Route.Contains("storage-places", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_Lagerort_When_Loeschen_bestaetigt_Then_DELETE_und_das_Hauptlager_ist_gewaehlt()
    {
        var api = Api();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var stock = await Open(api, dialogs);
        stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Id == ShopId);
        await Task.Yield();

        await stock.PlacesAdmin.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"DELETE {Route($"/storage-places/{ShopId}")}"));
        var asked = dialogs.AssertRules().Asked.Single();
        Assert.Equal("Lagerort löschen?", asked.Title);
        Assert.Contains("Verkaufsregal", asked.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("bitte", asked.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Lagerort gelöscht.", stock.Message);
        Assert.Equal("Hauptlager", stock.PlacesAdmin.HeadingText);
        Assert.DoesNotContain(stock.PlacesAdmin.Rows, r => r.Id == ShopId);
    }

    [Fact]
    public async Task Given_Lagerort_When_Loeschen_abgebrochen_Then_kein_DELETE()
    {
        var api = Api();
        var stock = await Open(api, new FakeDialogs().Answer(ConfirmChoice.Cancelled));
        stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Id == ShopId);
        await Task.Yield();

        await stock.PlacesAdmin.DeleteCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("DELETE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_Mindestmengen_eines_Raums_When_uebernommen_Then_POST_copy_from_und_erst_nach_dem_Speichern()
    {
        var api = Api();
        var stock = await Open(api);
        var places = stock.PlacesAdmin;
        places.SelectedRow = places.Rows.Single(r => r.Id == ShopId);
        await Task.Yield();

        Assert.True(places.HasCopySources);
        places.CopySource = places.CopySources.Single(s => s.Id == CabinetId);
        Assert.True(places.CopyMinimumsCommand.CanExecute(null));
        places.PlaceName = "Anders";
        Assert.False(places.CopyMinimumsCommand.CanExecute(null));
        places.Discard();
        places.CopySource = places.CopySources.Single(s => s.Id == CabinetId);
        await places.CopyMinimumsCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"POST {Route($"/storage-places/{ShopId}/minimums/copy-from/{CabinetId}")}"));
        Assert.Equal("Mindestmengen übernommen.", stock.Message);
    }

    // ── Rechte ──

    [Fact]
    public async Task Given_Rezeption_When_die_Maske_laedt_Then_nur_Bestand_und_Bewegungen_und_ohne_Material_anlegen()
    {
        var api = Api();
        var stock = await Open(api, user: TestData.Reception());

        Assert.True(stock.CanBook);
        Assert.False(stock.CanManage);
        Assert.False(stock.CanCatalog);
        stock.SetTab(StockTab.Count);
        Assert.Equal(StockTab.Overview, stock.Tab);
        stock.SetTab(StockTab.Places);
        Assert.Equal(StockTab.Overview, stock.Tab);
        stock.SetTab(StockTab.Movements);
        Assert.Equal(StockTab.Movements, stock.Tab);
        Assert.DoesNotContain(api.Calls, c => c.Route == "GET /api/pos/articles");
        Assert.DoesNotContain(api.Calls, c => c.Route == $"GET /api/locations/{Vienna.Id}/resources");
        Assert.Empty(stock.PlacesAdmin.Rows);
    }

    [Fact]
    public async Task Given_Standortleitung_ohne_Katalogrecht_When_die_Maske_laedt_Then_Inventur_und_Lagerorte_aber_kein_Material_anlegen()
    {
        var api = Api();
        var stock = await Open(api, user: WithRights(AccessRight.Book, AccessRight.ManageLocation));

        Assert.True(stock.CanManage);
        Assert.False(stock.CanCatalog);
        stock.SetTab(StockTab.Count);
        Assert.Equal(StockTab.Count, stock.Tab);
        Assert.DoesNotContain(api.Calls, c => c.Route == "GET /api/pos/articles");
    }

    [Fact]
    public async Task Given_Rezeption_When_ein_Bestandszeile_buchen_gewaehlt_wird_Then_oeffnet_die_Leiste_aber_ohne_Recht_zu_buchen_nicht()
    {
        var stock = await Open(Api(), user: WithRights(AccessRight.ViewCalendar));

        stock.Overview.Book(OilId);

        Assert.False(stock.CanBook);
        Assert.False(stock.Booking.IsOpen);
        Assert.False(stock.Overview.Rows[0].CanBook);
    }

    // ── Ungespeicherte Änderungen ──

    [Fact]
    public async Task Given_keine_Aenderung_When_die_Maske_verlassen_wird_Then_keine_Rueckfrage()
    {
        var dialogs = new FakeDialogs();
        var stock = await Open(Api(), dialogs);

        Assert.True(await UnsavedChanges.ConfirmLeaveAsync(dialogs, stock));
        Assert.Empty(dialogs.Asked);
    }

    [Fact]
    public async Task Given_nur_das_Recht_zu_buchen_When_Aufgaben_oder_Lieferanten_gewaehlt_Then_bleibt_der_Bestand()
    {
        var api = Api();
        var stock = await Open(api, user: WithRights(AccessRight.Book));

        stock.SetTab(StockTab.Tasks);
        Assert.Equal(StockTab.Overview, stock.Tab);
        stock.SetTab(StockTab.Suppliers);
        Assert.Equal(StockTab.Overview, stock.Tab);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/expiry-tasks", StringComparison.Ordinal) || c.Route == "GET /api/stock/suppliers");
    }

    [Fact]
    public async Task Given_ein_angefangenes_Material_When_die_Maske_verlassen_wird_Then_fragt_sie_und_Verwerfen_leert_das_Formular()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Alternative);
        var stock = await Open(Api(), dialogs);
        stock.ItemForm.ItemName = "Kerze";

        Assert.True(stock.HasUnsavedChanges);
        Assert.True(await UnsavedChanges.ConfirmLeaveAsync(dialogs, stock));

        Assert.Equal("Änderungen speichern?", dialogs.AssertRules().Asked.Single().Title);
        Assert.Equal("", stock.ItemForm.ItemName);
        Assert.False(stock.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ein_geaenderter_Lagerort_When_die_Maske_verlassen_wird_Then_fragt_sie_und_Abbrechen_bleibt()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var stock = await Open(Api(), dialogs);
        stock.PlacesAdmin.PlaceName = "Zentrallager";

        Assert.True(stock.HasUnsavedChanges);
        Assert.False(await UnsavedChanges.ConfirmLeaveAsync(dialogs, stock));

        Assert.Equal("Zentrallager", stock.PlacesAdmin.PlaceName);
    }

    [Fact]
    public async Task Given_eingetippte_Zaehlwerte_When_die_Maske_verlassen_wird_Then_fragt_sie_und_Speichern_fragt_vor_dem_Buchen_noch_einmal()
    {
        var api = Api();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed).Answer(ConfirmChoice.Confirmed);
        var stock = await Counting(api, dialogs);
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "8";

        Assert.True(stock.HasUnsavedChanges);
        Assert.True(await UnsavedChanges.ConfirmLeaveAsync(dialogs, stock));

        Assert.Equal(["Änderungen speichern?", "Inventur buchen?"], dialogs.Asked.Select(a => a.Title));
        Assert.Equal(8m, Assert.Single(SentMovements(api)).Quantity);
    }

    [Fact]
    public async Task Given_ungespeicherte_Zaehlwerte_When_der_Standort_gewechselt_wird_Then_fragt_die_Maske_und_Abbrechen_bleibt_beim_Standort()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var stock = await Counting(Api(two: true), dialogs);
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "8";

        stock.SelectedLocation = stock.Locations.Single(l => l.Id == Salzburg.Id);
        await Task.Delay(30, TestContext.Current.CancellationToken);

        Assert.Equal("Wien", stock.SelectedLocation?.Name);
        Assert.Equal("8", stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered);
        Assert.Equal("Änderungen speichern?", dialogs.Asked.Single().Title);
    }

    // ── Sprungziel ──

    [Fact]
    public async Task Given_ein_Sprung_aus_dem_Kalender_When_Show_stock_Then_oeffnet_die_Verwaltung_die_native_Maske_und_waehlt_das_Material()
    {
        var api = Api();
        var shell = new ShellViewModel(api.Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        shell.AdminPage.Show("stock", null, TowelId);
        await shell.AdminPage.InitializeAsync();
        await shell.AdminPage.Stock!.EnsureLoadedAsync();

        Assert.Equal("Lager", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Stock, shell.AdminPage.Content);
        Assert.Equal(TowelId, shell.AdminPage.Stock.Overview.SelectedRow?.ItemId);
        Assert.Equal(StockTab.Stock, shell.AdminPage.Stock.Tab);
    }

    [Fact]
    public async Task Given_die_Maske_steht_schon_When_ein_zweiter_Sprung_kommt_Then_wechselt_die_Auswahl_zum_neuen_Material()
    {
        var stock = await Open(Api());

        stock.Open(OilId, null, null);
        Assert.Equal(OilId, stock.Overview.SelectedRow?.ItemId);
        stock.SetTab(StockTab.Movements);
        stock.Open(TowelId, null, null);

        Assert.Equal(StockTab.Stock, stock.Tab);
        Assert.Equal(TowelId, stock.Overview.SelectedRow?.ItemId);
    }

    [Fact]
    public async Task Given_ein_Fehler_beim_Laden_When_Erneut_laden_Then_laedt_die_Maske_den_Bestand()
    {
        var api = Api().On($"GET {Route()}", new { }, HttpStatusCode.InternalServerError);
        var stock = new StockViewModel(api.Client(), TestData.Owner(), new FixedTime());

        await stock.LoadAsync();

        Assert.True(stock.HasLoadError);
        Assert.False(stock.IsLoaded);
        Api(api);
        await stock.LoadAsync();
        Assert.False(stock.HasLoadError);
        Assert.True(stock.IsLoaded);
        Assert.Equal(2, stock.Overview.Rows.Count);
    }

    // ── Durchsicht-Befunde (Slice 101) ──

    private static StoragePlaceInput SentPlace(StubApi api, string method, Guid id) =>
        JsonSerializer.Deserialize<StoragePlaceInput>(Assert.Single(api.Calls, c => c.Route == $"{method} {Route($"/storage-places/{id}")}").Body!, ApiClient.Json)!;

    [Fact]
    public async Task Given_Mindestmenge_ab_1000_When_nur_der_Name_geaendert_und_gespeichert_wird_Then_geht_die_Menge_unveraendert_an_die_API()
    {
        var api = Api().On($"GET {Route("/storage-places")}", new List<StoragePlaceDto> { Main, Shop with { Minimums = [new StoragePlaceMinimumDto(OilId, 1250)] }, Cabinet });
        var stock = await Open(api);
        stock.PlacesAdmin.SelectedRow = stock.PlacesAdmin.Rows.Single(r => r.Id == ShopId);
        await Task.Yield();

        stock.PlacesAdmin.PlaceName = "Regal Empfang";
        Assert.True(await stock.PlacesAdmin.SaveChangesAsync());

        Assert.Equal([new StoragePlaceMinimumDto(OilId, 1250)], SentPlace(api, "PUT", ShopId).Minimums);
    }

    [Fact]
    public void Given_Minuszeichen_U2212_When_eine_Zahl_gelesen_wird_Then_zaehlt_es_als_Minus()
    {
        Assert.Equal(-2m, StockFormat.Parse("−2"));
        Assert.Equal(-2m, StockFormat.Parse("-2"));
    }

    [Fact]
    public async Task Given_schneller_Wechsel_des_Lagerorts_When_die_alte_Antwort_zuletzt_kommt_Then_bleibt_der_Bestand_des_neuen_Lagerorts()
    {
        var gate = new TaskCompletionSource();
        var api = Api();
        var stock = await Open(api);
        api.Hold($"GET {Route($"/storage-places/{ShopId}/levels")}", gate.Task);

        stock.SelectedPlace = stock.PlaceFilter.Single(p => p.Id == ShopId);
        stock.SelectedPlace = stock.PlaceFilter.Single(p => p.Id == MainId);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        gate.SetResult();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("77,50 €", stock.Overview.ValueText);
        Assert.Equal(["Massageöl", "Handtuch"], stock.Overview.Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Given_schneller_Wechsel_des_Standorts_When_die_alte_Antwort_zuletzt_kommt_Then_bleiben_die_Daten_des_neuen_Standorts()
    {
        var salzburgMain = new StoragePlaceDto(Guid.NewGuid(), Salzburg.Id, "Hauptlager Salzburg", null, StoragePlaceKind.Main, null, true, 0, []);
        var salzburg = $"/api/stock/locations/{Salzburg.Id}";
        var api = Api(two: true)
            .On($"GET {salzburg}", new StockOverviewDto(Salzburg.Id, 14, 0, []))
            .On($"GET {salzburg}/batches", new StockBatchesDto(Salzburg.Id, new DateOnly(2027, 3, 1), 14, []))
            .On($"GET {salzburg}/storage-places", new List<StoragePlaceDto> { salzburgMain })
            .On($"GET /api/locations/{Salzburg.Id}/resources", new LocationResources(Salzburg, [], [], [], [], []));
        var stock = await Open(api);
        var gate = new TaskCompletionSource();
        api.Hold($"GET {Route()}", gate.Task);

        var slowReload = stock.ReloadAsync();
        stock.SelectedLocation = stock.Locations.Single(l => l.Id == Salzburg.Id);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        gate.SetResult();
        await slowReload;
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(Salzburg.Id, stock.OverviewData?.LocationId);
        Assert.Equal([salzburgMain.Id], stock.Places.Select(p => p.Id));
    }

    [Fact]
    public async Task Given_ein_schneller_Wechsel_des_Zeitraums_When_die_alte_Antwort_zuletzt_kommt_Then_zeigt_die_Liste_den_neuen_Zeitraum()
    {
        var gate = new TaskCompletionSource();
        var api = Api().Hold($"GET {Route("/movements?from=2027-01-30&until=2027-03-01")}", gate.Task)
            .On($"GET {Route("/movements?from=2027-01-30&until=2027-02-15")}", new List<StockMovementDto> { Movement(StockMovementKind.Refill, 1) });
        var stock = await Open(api);

        stock.SetTab(StockTab.Movements);
        stock.Movements.Until = new DateOnly(2027, 2, 15);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        gate.SetResult();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(["Nachfüllen"], stock.Movements.Rows.Select(r => r.KindText));
    }

    [Fact]
    public async Task Given_laufende_Inventurbuchung_When_ein_Zaehlfeld_geleert_wird_Then_ist_es_gesperrt_und_gebucht_wird_der_Stand_vom_Klick()
    {
        var gate = new TaskCompletionSource();
        var api = Api();
        var stock = await Counting(api, new FakeDialogs().Answer(ConfirmChoice.Confirmed));
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "8";
        stock.Count.Rows.Single(r => r.Item.Id == TowelId).Entered = "3";
        api.Hold($"POST {Route("/movements")}", gate.Task);

        var booking = stock.Count.BookCountAsync();
        Assert.True(stock.Count.IsBooking);
        Assert.False(stock.Count.CanEditRows);
        stock.Count.Rows.Single(r => r.Item.Id == TowelId).Entered = "";
        gate.SetResult();

        Assert.True(await booking);
        Assert.Equal([8m, 3m], SentMovements(api).Select(m => m.Quantity));
        Assert.True(stock.Count.CanEditRows);
    }

    [Fact]
    public async Task Given_gezaehlt_aber_ohne_buchbare_Abweichung_When_die_Maske_verlassen_wird_Then_keine_Rueckfrage()
    {
        var dialogs = new FakeDialogs();
        var stock = await Counting(Api(), dialogs);
        var oil = stock.Count.Rows.Single(r => r.Item.Id == OilId);

        oil.Entered = "10"; // Soll 10
        Assert.False(stock.HasUnsavedChanges);
        oil.Entered = "-1";
        Assert.True(stock.Count.HasInvalid);
        Assert.False(stock.HasUnsavedChanges);
        Assert.True(await UnsavedChanges.ConfirmLeaveAsync(dialogs, stock));
        Assert.Empty(dialogs.Asked);
        oil.Entered = "9";
        Assert.True(stock.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Lagerort_gespeichert_aber_das_Neuladen_scheitert_When_erneut_gespeichert_wird_Then_PUT_und_kein_zweiter_POST()
    {
        var api = Api();
        var stock = await Open(api);
        var places = stock.PlacesAdmin;
        places.SelectedRow = places.Rows.Last();
        await Task.Yield();
        places.PlaceName = "Kühlschrank";
        api.On($"GET {Route()}", new { }, HttpStatusCode.InternalServerError);

        Assert.True(await places.SaveChangesAsync());
        places.PlaceName = "Kühlschrank 2";
        await places.SaveChangesAsync();

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"POST {Route("/storage-places")}"));
        Assert.Contains(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal) && c.Route.Contains("/storage-places/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_ein_Teilfehler_bei_der_Inventur_When_erneut_gebucht_wird_Then_nur_die_noch_offene_Zeile()
    {
        var api = Api();
        var calls = 0;
        api.OnJson($"POST {Route("/movements")}", _ =>
        {
            if (++calls == 1)
            {
                api.On($"POST {Route("/movements")}", new { errors = new { quantity = CountAgain } }, HttpStatusCode.BadRequest);
            }

            return new List<StockMovementDto> { Movement(StockMovementKind.Count, 1) };
        });
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed).Answer(ConfirmChoice.Confirmed);
        var stock = await Counting(api, dialogs);
        stock.Count.Rows.Single(r => r.Item.Id == OilId).Entered = "8";
        stock.Count.Rows.Single(r => r.Item.Id == TowelId).Entered = "3";

        Assert.False(await stock.Count.BookCountAsync());
        Assert.Equal("Handtuch: Zähle neu.", stock.Count.Error);
        Assert.Equal(1, stock.Count.ChangeCount);
        api.On($"POST {Route("/movements")}", new List<StockMovementDto> { Movement(StockMovementKind.Count, 1) });
        Assert.True(await stock.Count.BookCountAsync());

        Assert.Equal([(OilId, 8m), (TowelId, 3m), (TowelId, 3m)], SentMovements(api).Select(m => (m.ItemId, m.Quantity)));
    }

    [Fact]
    public async Task Given_unlesbarer_Preis_oder_Mindestbestand_When_angelegt_Then_geht_der_Fokus_ins_Feld()
    {
        var stock = await Open(Api());
        stock.ItemForm.ItemName = "Kerze";
        stock.ItemForm.UnitCost = "viel";

        Assert.False(await stock.ItemForm.CreateItemAsync());
        Assert.Equal(StockItemFormViewModel.UnitCostField, stock.ItemForm.FocusTarget);

        stock.ItemForm.UnitCost = "";
        stock.ItemForm.MinimumQuantity = "-1";
        Assert.False(await stock.ItemForm.CreateItemAsync());
        Assert.Equal(StockItemFormViewModel.MinimumField, stock.ItemForm.FocusTarget);
    }
}
