using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Lieferschein-Foto: Die KI schlägt je offener Position Menge, Charge und Ablaufdatum vor; gebucht wird erst über „Lieferung buchen“.</summary>
public sealed class DeliveryNoteScanTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Bestellung bei der Aroma GmbH über 1000 ml Arganöl (Artikelnummer AR-500) und 2000 g Meersalz, als bestellt markiert.</summary>
    private static async Task<(TestSpa Spa, PurchaseOrderDto Order)> OrderedAsync(ApiFixture fixture, bool ordered = true)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items",
            new StockItemInput("Arganöl", "ml", 0.10m, 1000m, null, Supplier: "Aroma GmbH", SupplierArticleNumber: "AR-500"), Ct);
        var salt = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Meersalz", "g", 0.01m, 2000m, null, Supplier: "Aroma GmbH"), Ct);
        var order = await TestSpa.ReadAsync<PurchaseOrderDto>(await spa.Client.PostAsJsonAsync($"/api/stock/locations/{spa.Location.Id}/orders",
            new PurchaseOrderInput("Aroma GmbH", [new PurchaseOrderLineInput(oil.Id, 1000m), new PurchaseOrderLineInput(salt.Id, 2000m)]), TestSpa.Json, Ct), Ct);
        return (spa, ordered ? await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{order.Id}/ordered", new { }, Ct) : order);
    }

    private static async Task<HttpResponseMessage> ScanAsync(HttpClient client, Guid orderId, byte[] photo)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(photo);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "lieferschein.jpg");
        return await client.PostAsync($"/api/stock/orders/{orderId}/delivery-note-scan", content, Ct);
    }

    [Fact]
    public async Task Gegeben_ein_lesbarer_Lieferschein_Dann_kommt_ein_Vorschlag_je_Position_und_nichts_ist_gebucht()
    {
        var (spa, order) = await OrderedAsync(fixture);
        var photo = FakeDeliveryNoteReader.Photo(new DeliveryNoteReading("LS-2027-118",
            [new ReadDeliveryLine("p1", 1000m, "L-77", new DateOnly(2028, 6, 30), 0.95, null), new ReadDeliveryLine("p2", 1500m, null, null, 0.6, "Menge von Hand korrigiert")],
            ["1 Karton Duftkerzen"], null));

        var suggestion = await TestSpa.ReadAsync<DeliveryNoteSuggestion>(await ScanAsync(spa.Client, order.Id, photo), Ct);

        Assert.True(suggestion.Read);
        Assert.Equal("LS-2027-118", suggestion.DeliveryNote);
        Assert.Equal(2, suggestion.Lines.Count);
        var oil = suggestion.Lines.Single(l => l.LineId == order.Lines[0].Id);
        Assert.Equal((1000m, "L-77", new DateOnly(2028, 6, 30), false), (oil.Quantity, oil.Batch, oil.ExpiresOn, oil.Uncertain));
        var salt = suggestion.Lines.Single(l => l.LineId == order.Lines[1].Id);
        Assert.True(salt.Uncertain);
        Assert.Equal("Menge von Hand korrigiert", salt.Reason);
        Assert.Equal(["1 Karton Duftkerzen"], suggestion.Unmatched);
        // Der Leser sieht die offenen Positionen mit Artikelnummer, aber keine Ids der Datenbank.
        Assert.Equal(["p1", "p2"], fixture.DeliveryNotes.LastLines.Select(l => l.Key));
        Assert.Equal("AR-500", fixture.DeliveryNotes.LastLines[0].ArticleNumber);
        // Nichts gebucht: Die Bestellung ist unverändert offen.
        var after = await spa.GetAsync<PurchaseOrderDto>($"/api/stock/orders/{order.Id}", Ct);
        Assert.Equal(PurchaseOrderStatus.Ordered, after.Status);
        Assert.All(after.Lines, l => Assert.Equal(0, l.Received));

        // Der Vorschlag passt unverändert in „Lieferung buchen“.
        var delivered = await spa.PostAsync<PurchaseOrderDto>($"/api/stock/orders/{order.Id}/deliveries", new PurchaseOrderDeliveryInput(
            [.. suggestion.Lines.Select(l => new PurchaseOrderDeliveryLineInput(l.LineId, l.Quantity, null, l.Batch, l.ExpiresOn))], suggestion.DeliveryNote), Ct);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, delivered.Status);
    }

    [Fact]
    public async Task Unbekannte_Positionen_Doppelte_und_unbrauchbare_Mengen_fallen_weg_und_zu_viel_wird_markiert()
    {
        var (spa, order) = await OrderedAsync(fixture);
        var photo = FakeDeliveryNoteReader.Photo(new DeliveryNoteReading(new string('9', 150),
            [
                new ReadDeliveryLine("p1", 1200m, null, null, 0.99, null),
                new ReadDeliveryLine("p1", 5m, null, null, 0.99, null),
                new ReadDeliveryLine("p2", -3m, null, null, 0.99, null),
                new ReadDeliveryLine("p9", 10m, null, null, 0.99, null),
            ], [], null));

        var suggestion = await TestSpa.ReadAsync<DeliveryNoteSuggestion>(await ScanAsync(spa.Client, order.Id, photo), Ct);

        var only = Assert.Single(suggestion.Lines);
        Assert.Equal((order.Lines[0].Id, 1200m, true, "Mehr als noch offen ist."), (only.LineId, only.Quantity, only.Uncertain, only.Reason));
        // Eine überlange Lieferscheinnummer wird nicht übernommen.
        Assert.Null(suggestion.DeliveryNote);
    }

    [Fact]
    public async Task Ein_unlesbares_Foto_gibt_einen_leeren_Vorschlag_mit_Hinweis()
    {
        var (spa, order) = await OrderedAsync(fixture);

        var suggestion = await TestSpa.ReadAsync<DeliveryNoteSuggestion>(await ScanAsync(spa.Client, order.Id, FakeDeliveryNoteReader.Unreadable()), Ct);

        Assert.False(suggestion.Read);
        Assert.Empty(suggestion.Lines);
        Assert.Equal(DeliveryNoteSuggestions.Failed, suggestion.Note);
    }

    [Fact]
    public async Task Kein_Bild_ein_Entwurf_und_eine_fremde_Bestellung_werden_abgelehnt()
    {
        var (spa, order) = await OrderedAsync(fixture);
        var (other, _) = await OrderedAsync(fixture);
        var (draftSpa, draft) = await OrderedAsync(fixture, ordered: false);
        var photo = FakeDeliveryNoteReader.Photo(new DeliveryNoteReading(null, [], [], null));

        Assert.Equal(HttpStatusCode.BadRequest, (await ScanAsync(spa.Client, order.Id, "kein Bild"u8.ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ScanAsync(draftSpa.Client, draft.Id, photo)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ScanAsync(other.Client, order.Id, photo)).StatusCode);
    }

    [Fact]
    public async Task Rezeption_darf_keinen_Lieferschein_lesen_lassen()
    {
        var (spa, order) = await OrderedAsync(fixture);
        using var reception = await TestUsers.ClientAsync(spa.Fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var response = await ScanAsync(reception, order.Id, FakeDeliveryNoteReader.Photo(new DeliveryNoteReading(null, [], [], null)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Ohne_eingerichtete_KI_liest_der_Leser_nichts()
    {
        Assert.False(new ManualDeliveryNoteReader().Enabled);
    }
}
