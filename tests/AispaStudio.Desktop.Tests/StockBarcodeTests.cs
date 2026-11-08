using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 114: Handscanner im Lager (Wareneingang, Inventur) mit Rückfrage bei unbekanntem Code. Der Weg über die Tastatur (Text tippen, Enter) steht in <c>ViewTests.StockBarcode</c>;
/// hier prüft das ViewModel, was bei einem unbekannten Code gefragt, gesendet und vorbelegt wird.
/// </summary>
public sealed class StockBarcodeTests
{
    private const string Ean = "9001234567890";

    private static string ScanRoute(string code) => $"GET /api/stock/items/by-barcode?code={code}";

    private static StubApi UnknownCodeApi() => Api().On(ScanRoute(Ean), null, HttpStatusCode.NotFound);

    private static async Task<StockViewModel> Open(StubApi api, FakeDialogs? dialogs = null, CurrentUser? user = null)
    {
        var stock = new StockViewModel(api.Client(), user ?? TestData.Owner(), new FixedTime()) { Dialogs = dialogs };
        await stock.EnsureLoadedAsync();
        return stock;
    }

    private static CurrentUser WithRights(params AccessRight[] rights) =>
        new("u9", "recht@aispa.test", "Rolf", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, true, []), null, rights);

    [Fact]
    public async Task Given_ein_unbekannter_Code_im_Lager_When_Anlegen_gewaehlt_wird_Then_oeffnet_das_Formular_mit_dem_Code()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var stock = await Open(UnknownCodeApi(), dialogs);

        stock.ScanText = Ean;
        await stock.ScanCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        FakeDialogs.AssertRules(asked);
        Assert.Contains(Ean, asked.Message, StringComparison.Ordinal);
        Assert.Equal(Ean, stock.ItemForm.Barcode);
        Assert.Equal(StockItemFormViewModel.NameField, stock.ItemForm.FocusTarget);
        Assert.True(stock.IsStockTab);
        Assert.Equal("", stock.ScanText);
    }

    [Fact]
    public async Task Given_ein_unbekannter_Code_im_Lager_When_Zuordnen_gewaehlt_wird_Then_bekommt_das_gewaehlte_Material_den_Code()
    {
        var api = UnknownCodeApi();
        var stock = await Open(api, new FakeDialogs().Answer(ConfirmChoice.Alternative));

        stock.ScanText = Ean;
        await stock.ScanCommand.ExecuteAsync(null);
        Assert.Contains(Ean, stock.Message, StringComparison.Ordinal);
        await stock.Overview.ChangeAsync(TowelId);
        Assert.Equal(Ean, stock.ItemForm.Barcode);
        Assert.True(stock.ItemForm.IsDirty);
        await stock.ItemForm.CreateItemAsync();

        var sent = JsonSerializer.Deserialize<StockItemInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/stock/items/{TowelId}").Body!, ApiClient.Json)!;
        Assert.Equal((Towel.Name, Ean), (sent.Name, sent.Barcode));
    }

    [Fact]
    public async Task Given_Zuordnen_gewaehlt_When_der_Reiter_gewechselt_wird_Then_uebernimmt_ein_spaeteres_Aendern_den_Code_nicht()
    {
        var stock = await Open(UnknownCodeApi(), new FakeDialogs().Answer(ConfirmChoice.Alternative));
        stock.ScanText = Ean;
        await stock.ScanCommand.ExecuteAsync(null);

        stock.SetTab(StockTab.Movements);
        stock.SetTab(StockTab.Stock);
        await stock.Overview.ChangeAsync(TowelId);

        Assert.Equal("", stock.ItemForm.Barcode);
        Assert.False(stock.ItemForm.IsDirty);
    }

    [Fact]
    public async Task Given_eine_Rueckfrage_zum_unbekannten_Code_When_abgebrochen_wird_Then_aendert_sich_nichts()
    {
        var stock = await Open(UnknownCodeApi(), new FakeDialogs().Answer(ConfirmChoice.Cancelled));

        stock.ScanText = Ean;
        await stock.ScanCommand.ExecuteAsync(null);

        Assert.Equal("", stock.ItemForm.Barcode);
        Assert.False(stock.ItemForm.IsDirty);
        Assert.Null(stock.TakeBarcodeToAssign());
    }

    [Fact]
    public async Task Given_das_Scanfeld_When_Enter_ohne_Text_gedrueckt_wird_Then_passiert_nichts()
    {
        var api = Api();
        var stock = await Open(api);

        stock.ScanText = "   ";
        await stock.ScanCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("by-barcode", StringComparison.Ordinal));
        Assert.False(stock.HasMessage);
        Assert.False(stock.HasError);
    }

    [Fact]
    public async Task Given_ein_bekannter_Code_When_er_im_Lager_gescannt_wird_Then_oeffnet_die_Leiste_mit_dem_Material_als_Wareneingang()
    {
        var stock = await Open(Api().On(ScanRoute(Ean), Oil with { Barcode = Ean }));

        stock.ScanText = Ean;
        await stock.ScanCommand.ExecuteAsync(null);

        Assert.True(stock.Booking.IsOpen);
        Assert.Equal((OilId, StockMovementKind.GoodsReceipt), (stock.Booking.SelectedItem?.Id, stock.Booking.Kind));
        Assert.Equal(MovementBookingViewModel.AmountField, stock.Booking.FocusTarget);
    }

    [Fact]
    public async Task Given_die_Inventur_laeuft_nicht_When_ein_Code_gescannt_wird_Then_steht_die_Meldung_Inventur_starten()
    {
        var stock = await Open(Api().On(ScanRoute(Ean), Oil with { Barcode = Ean }));
        stock.SetTab(StockTab.Count);

        stock.ScanText = Ean;
        await stock.ScanCommand.ExecuteAsync(null);

        Assert.Contains("Starte zuerst die Inventur", stock.Error, StringComparison.Ordinal);
        Assert.False(stock.Booking.IsOpen);
    }

    [Fact]
    public async Task Given_ohne_Recht_auf_den_Katalog_When_ein_unbekannter_Code_gescannt_wird_Then_fragt_das_Lager_nicht_und_nennt_den_Grund()
    {
        var dialogs = new FakeDialogs();
        var stock = await Open(UnknownCodeApi(), dialogs, WithRights(AccessRight.Book, AccessRight.ViewCalendar));

        stock.ScanText = Ean;
        await stock.ScanCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Asked);
        Assert.Contains(Ean, stock.Error, StringComparison.Ordinal);
    }
}
