using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 114: Handscanner als Tastatur im Fenster. Der Scanner tippt den Code und schließt mit Enter ab; getestet wird dieser Weg, nicht der Befehl.</summary>
public sealed partial class ViewTests
{
    private const string ScannedCode = "9001234567890";

    private static async Task ScanWithEnter(Window window, string field, string code)
    {
        await TypeInto(window, field, code);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Pump(Settling);
    }

    [Fact]
    public Task Lager_Code_scannen_oeffnet_den_Wareneingang_mit_dem_Material_und_die_Menge_hat_den_Fokus()
    {
        var api = StockApi().On($"GET /api/stock/items/by-barcode?code={ScannedCode}", Oil with { Barcode = ScannedCode });
        return Render("hell", "verwaltung-lager-scan", async (window, main) =>
        {
            var (shell, stock) = await OpenStock(window, main);

            await ScanWithEnter(window, "ScanField", ScannedCode);

            Assert.True(stock.Booking.IsOpen);
            Assert.Equal(OilId, stock.Booking.SelectedItem?.Id);
            Assert.Equal("", Named<TextBox>(window, "ScanField").Text);
            Assert.True(Named<TextBox>(window, "MovementAmountField").IsFocused);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Inventur_Code_scannen_springt_in_das_Feld_Gezaehlt_der_Zeile()
    {
        var api = StockApi().On($"GET /api/stock/items/by-barcode?code={ScannedCode}", Oil with { Barcode = ScannedCode });
        return Render("hell", "verwaltung-lager-inventur-scan", async (window, main) =>
        {
            var (shell, _) = await OpenStock(window, main);
            await ClickScrolled(window, "CountTabButton");
            await ClickScrolled(window, "StartCountButton");

            await ScanWithEnter(window, "ScanField", ScannedCode);

            Assert.True(CountField(window, "Massageöl").IsFocused);
            return shell.AdminPage;
        }, api);
    }

    [Fact]
    public Task Kassa_Code_9001234567890_scannen_legt_den_Artikel_mit_Menge_1_auf_den_Beleg()
    {
        var article = SpaSample.Articles[0];
        var stub = Api().On($"GET /api/pos/articles/by-barcode?code={ScannedCode}", article)
            .On($"POST /api/pos/bills/{SpaSample.BillId}/lines", SpaSample.Bill());
        return Render("hell", "kassa-scan", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump();

            await ScanWithEnter(window, "PosScanField", ScannedCode);

            var sent = System.Text.Json.JsonSerializer.Deserialize<BillLineInput>(Assert.Single(stub.Calls, c => c.Route == $"POST /api/pos/bills/{SpaSample.BillId}/lines").Body!, ApiClient.Json)!;
            Assert.Equal((BillLineKind.Article, (Guid?)article.Id, 1), (sent.Kind, sent.ArticleId, sent.Quantity));
            Assert.Equal("", shell.Pos.ScanText);
            return shell.Pos;
        }, stub);
    }

    [Fact]
    public Task Kassa_unbekannter_Code_scannen_meldet_Code_unbekannt_und_der_Beleg_bleibt_unveraendert()
    {
        var stub = Api().On($"GET /api/pos/articles/by-barcode?code={ScannedCode}", null, System.Net.HttpStatusCode.NotFound);
        return Render("hell", "kassa-scan-unbekannt", async (window, main) =>
        {
            await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await shell.CheckoutAsync(SpaSample.Paying);
            await Pump();

            await ScanWithEnter(window, "PosScanField", ScannedCode);

            Assert.Contains(ScannedCode, shell.Pos.Error, StringComparison.Ordinal);
            Assert.DoesNotContain(stub.Calls, c => c.Route.EndsWith("/lines", StringComparison.Ordinal));
            return shell.Pos;
        }, stub);
    }
}
