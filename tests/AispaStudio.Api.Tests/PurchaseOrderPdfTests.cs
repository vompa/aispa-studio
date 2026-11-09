using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Bestellung an den Lieferanten als PDF (PDF-Ausgaben F, ADR 0091): Lieferant, Lieferadresse, Positionen mit Menge, Einheit, Einzelpreis und Summe, Gesamtsumme netto, erwartete Lieferung
/// (aus der Lieferzeit des Lieferanten) und Hinweis. Recht wie die Bestellungen (<c>ManageLocation</c>), fremder Mandant 404. Das Senden prüft <see cref="PurchaseOrderSendTests"/>.
/// </summary>
public sealed class PurchaseOrderPdfTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Haslauer liefert in 3 Tagen; bestellt sind 2 Flaschen Öl zu 12,50 € (Artikelnummer ML-500) und 40 Tuchmasken zu 0,40 € mit dem Hinweis „Bitte vormittags liefern“.</summary>
    internal static async Task<(TestSpa Spa, PurchaseOrderDto Order, StockSupplierDto Supplier)> OrderAsync(
        ApiFixture fixture, string? email = null, string supplierName = "Haslauer", string? note = "Bitte vormittags liefern")
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput(supplierName, "Frau Haslauer", email, null, 3), Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items",
            new StockItemInput("Massageöl", "Flasche", 12.5m, 0m, null, SupplierId: supplier.Id, SupplierArticleNumber: "ML-500"), Ct);
        var masks = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Tuchmaske", "Stück", 0.4m, 0m, null, SupplierId: supplier.Id), Ct);
        var order = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/locations/{spa.Location.Id}/orders",
            new PurchaseOrderInput(supplier.Name, [new PurchaseOrderLineInput(oil.Id, 2m), new PurchaseOrderLineInput(masks.Id, 40m)], note), Ct);
        return (spa, order, supplier);
    }

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/purchase-orders/{orderId}/pdf", Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    [Fact]
    public async Task Die_Bestellung_nennt_Lieferant_Positionen_mit_Menge_Einheit_Preis_Summe_die_Gesamtsumme_den_Hinweis_und_die_Lieferadresse()
    {
        var (spa, order, _) = await OrderAsync(fixture, "bestellung@haslauer.example.at");

        var (response, pdf) = await PdfAsync(spa.Client, order.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"Bestellung-{order.Number}-Haslauer.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        PdfText.Keep(pdf, "bestellung");
        using var document = PdfText.Open(pdf);
        Assert.Equal(1, document.PageCount);
        var text = PdfText.Text(pdf);
        Assert.Contains($"Bestellung {order.Number}", text, StringComparison.Ordinal);
        Assert.Contains("Haslauer", text, StringComparison.Ordinal);
        Assert.Contains("Frau Haslauer", text, StringComparison.Ordinal);
        Assert.Contains("Lieferung an", text, StringComparison.Ordinal);
        Assert.Contains("Wien", text, StringComparison.Ordinal);
        foreach (var heading in new[] { "Menge", "Einheit", "Einzelpreis", "Summe" })
        {
            Assert.Contains(heading, text, StringComparison.Ordinal);
        }

        Assert.Contains("Massageöl", text, StringComparison.Ordinal);
        Assert.Contains("ML-500", text, StringComparison.Ordinal);
        Assert.Contains("Flasche", text, StringComparison.Ordinal);
        Assert.Contains("12,50 €", text, StringComparison.Ordinal);
        Assert.Contains("25,00 €", text, StringComparison.Ordinal);
        Assert.Contains("Tuchmaske", text, StringComparison.Ordinal);
        Assert.Contains("0,40 €", text, StringComparison.Ordinal);
        Assert.Contains("16,00 €", text, StringComparison.Ordinal);
        Assert.Contains("41,00 €", text, StringComparison.Ordinal);
        Assert.Contains("netto", text, StringComparison.Ordinal);
        Assert.Contains("Erwartete Lieferung", text, StringComparison.Ordinal);
        Assert.Contains("Bitte vormittags liefern", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Lieferzeit_und_ohne_Hinweis_fehlen_beide_Zeilen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Wiesner"), Ct);
        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Handtuch", "Stück", 3m, 0m, null, SupplierId: supplier.Id), Ct);
        var order = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/locations/{spa.Location.Id}/orders",
            new PurchaseOrderInput(supplier.Name, [new PurchaseOrderLineInput(item.Id, 10m)]), Ct);

        var (_, pdf) = await PdfAsync(spa.Client, order.Id);

        var text = PdfText.Text(pdf);
        Assert.DoesNotContain("Erwartete Lieferung", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hinweis", text, StringComparison.Ordinal);
        Assert.Contains("30,00 €", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_langer_Materialname_bricht_in_der_Zelle_um_und_laeuft_nicht_in_die_Nachbarspalte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var supplier = await spa.PostAsync<StockSupplierDto>("/api/stock/suppliers", new StockSupplierInput("Lieferant mit einem ziemlich langen Firmennamen GmbH & Co. KG"), Ct);
        var longName = "Aromatherapeutisches-Massageöl-Lavendel-Bergamotte-Ylang-Ylang-Bio-Großgebinde";
        var item = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput(longName, "Stück", 3m, 0m, null, SupplierId: supplier.Id), Ct);
        var order = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/locations/{spa.Location.Id}/orders",
            new PurchaseOrderInput(supplier.Name, [new PurchaseOrderLineInput(item.Id, 10m)]), Ct);

        var (response, pdf) = await PdfAsync(spa.Client, order.Id);

        response.EnsureSuccessStatusCode();
        PdfText.Keep(pdf, "bestellung-langer-name");
        var lines = PdfText.Text(pdf).Split('\n');
        Assert.DoesNotContain(longName, lines);
        Assert.Contains("30,00 €", lines);
    }

    [Fact]
    public async Task Die_Leitung_darf_die_Bestellung_drucken_die_Rezeption_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var (spa, order, _) = await OrderAsync(fixture);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (allowed, _) = await PdfAsync(manager, order.Id);
        var (forbidden, _) = await PdfAsync(reception, order.Id);
        var (foreign, _) = await PdfAsync(other.Client, order.Id);
        var (unknown, _) = await PdfAsync(spa.Client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
