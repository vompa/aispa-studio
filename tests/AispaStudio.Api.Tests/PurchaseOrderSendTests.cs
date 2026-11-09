using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Bestellung per Mail an den Lieferanten (PDF-Ausgaben F, ADR 0091): Vorbelegung mit der Mailadresse des Lieferanten und dem Betreff „Bestellung … vom …“, Senden über die Queue mit
/// dem PDF als Anhang, ungültige Adresse 400. Das Senden ändert den Stand der Bestellung nicht: „bestellt“ bleibt ein eigener Schritt in der Maske.
/// </summary>
public sealed class PurchaseOrderSendTests(ApiFixture fixture)
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly TimeSpan MailWait = TimeSpan.FromSeconds(60);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string OrderDate(PurchaseOrderDto order) => TimeZoneInfo.ConvertTime(order.CreatedAt, Vienna).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Die_Vorbelegung_nennt_die_Adresse_des_Lieferanten_den_Betreff_mit_Nummer_und_Datum_und_einen_Text_mit_Gruss()
    {
        var email = $"bestellung-{Guid.NewGuid():N}@haslauer.example.at";
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture, email);

        var draft = await spa.GetAsync<DocumentMailDraft>($"/api/purchase-orders/{order.Id}/send", Ct);

        Assert.Equal(email, draft.To);
        Assert.Equal($"Bestellung {order.Number} vom {OrderDate(order)}", draft.Subject);
        Assert.Contains("Guten Tag", draft.Text, StringComparison.Ordinal);
        Assert.Contains("im Anhang", draft.Text, StringComparison.Ordinal);
        Assert.Contains("Freundliche Grüße", draft.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Mailadresse_beim_Lieferanten_ist_die_Adresse_der_Vorbelegung_leer()
    {
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture);

        var draft = await spa.GetAsync<DocumentMailDraft>($"/api/purchase-orders/{order.Id}/send", Ct);

        Assert.Null(draft.To);
    }

    [Fact]
    public async Task Senden_legt_die_Mail_mit_dem_PDF_in_die_Queue_und_aendert_den_Stand_der_Bestellung_nicht()
    {
        var to = $"bestellung-{Guid.NewGuid():N}@haslauer.example.at";
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture, to);

        var response = await spa.Client.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest($" {to} "), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(to, m => m.Attachments is { Count: > 0 }, Ct, MailWait);
        var attachment = Assert.Single(mail.Attachments!);
        Assert.Equal($"Bestellung-{order.Number}-Haslauer.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Contains("Massageöl", PdfText.Text(attachment.Content), StringComparison.Ordinal);
        Assert.Equal($"Bestellung {order.Number} vom {OrderDate(order)}", mail.Subject);
        Assert.Equal(PurchaseOrderStatus.Draft, (await spa.GetAsync<PurchaseOrderDto>($"/api/stock/orders/{order.Id}", Ct)).Status);
    }

    [Fact]
    public async Task Nach_dem_Senden_steht_der_Vermerk_Gesendet_am_an_in_der_Datenbank_und_an_der_Bestellung_in_Liste_und_Einzelabruf()
    {
        var to = $"bestellung-{Guid.NewGuid():N}@haslauer.example.at";
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture, to);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id], spa.Ben.Id);
        Assert.Empty(order.SentMails ?? []);

        (await manager.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest(to), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest("zweite@haslauer.example.at"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var notes = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().SentDocuments.AsNoTracking()
            .Where(d => d.Kind == SentDocument.PurchaseOrderKind && d.TargetId == order.Id).OrderBy(d => d.SentAt).ToListAsync(Ct);
        Assert.Equal([to, "zweite@haslauer.example.at"], notes.Select(n => n.To));
        Assert.Equal($"Bestellung {order.Number} vom {OrderDate(order)}", notes[0].Subject);
        var single = await spa.GetAsync<PurchaseOrderDto>($"/api/stock/orders/{order.Id}", Ct);
        var listed = (await spa.GetAsync<List<PurchaseOrderDto>>($"/api/stock/locations/{spa.Location.Id}/orders", Ct)).Single(o => o.Id == order.Id);
        foreach (var dto in new[] { single, listed })
        {
            Assert.Equal([to, "zweite@haslauer.example.at"], dto.SentMails!.Select(m => m.To));
            Assert.All(dto.SentMails!, m => Assert.Equal("Ben", m.SentBy));
            Assert.All(dto.SentMails!, m => Assert.InRange(m.SentAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1)));
        }
    }

    [Fact]
    public async Task Ein_eigener_Betreff_und_eine_andere_Adresse_gelten_statt_der_Vorbelegung()
    {
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture, $"alt-{Guid.NewGuid():N}@haslauer.example.at");
        var to = $"neu-{Guid.NewGuid():N}@haslauer.example.at";

        var response = await spa.Client.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest(to, "Eilbestellung", "Bitte bis Freitag."), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(to, m => m.Attachments is { Count: > 0 }, Ct, MailWait);
        Assert.Equal("Eilbestellung", mail.Subject);
        Assert.Contains("Bitte bis Freitag.", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_ungueltige_Adresse_ist_400()
    {
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture, $"bestellung-{Guid.NewGuid():N}@haslauer.example.at");

        var response = await spa.Client.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest("haslauer@"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    [Fact]
    public async Task Ist_die_Vollmacht_Bestellungen_ausloesen_abgelaufen_lehnt_die_API_das_Senden_ab_und_nennt_sie_wie_bei_Bestellen_und_senden()
    {
        var to = $"bestellung-{Guid.NewGuid():N}@haslauer.example.at";
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture, to);
        await spa.PostAsync<StaffPowerDto>($"/api/employees/{spa.Ben.Id}/regulations/powers", new StaffPowerInput(StaffPowerKind.TriggerOrders, Today.AddDays(-30), Today.AddDays(-1)), Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id], spa.Ben.Id);

        var response = await manager.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest(to), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Bestellungen auslösen", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Empty(fixture.Mailbox.To(to));
    }

    [Fact]
    public async Task Mit_gueltiger_Vollmacht_darf_die_Standortleitung_senden_und_die_Inhaberin_braucht_keine()
    {
        var to = $"bestellung-{Guid.NewGuid():N}@haslauer.example.at";
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture, to);
        await spa.PostAsync<StaffPowerDto>($"/api/employees/{spa.Ben.Id}/regulations/powers", new StaffPowerInput(StaffPowerKind.TriggerOrders, Today), Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id], spa.Ben.Id);

        var byManager = await manager.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest(to), TestSpa.Json, Ct);
        var byOwner = await spa.Client.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest(to), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Accepted, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, byOwner.StatusCode);
    }

    [Fact]
    public async Task Die_Leitung_darf_senden_die_Rezeption_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var (spa, order, _) = await PurchaseOrderPdfTests.OrderAsync(fixture, $"bestellung-{Guid.NewGuid():N}@haslauer.example.at");
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var draftForbidden = await reception.GetAsync($"/api/purchase-orders/{order.Id}/send", Ct);
        var sendForbidden = await reception.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest("a@example.at"), TestSpa.Json, Ct);
        var draftForeign = await other.Client.GetAsync($"/api/purchase-orders/{order.Id}/send", Ct);
        var sendForeign = await other.Client.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/send", new SendDocumentRequest("a@example.at"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, draftForbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sendForbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draftForeign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, sendForeign.StatusCode);
    }
}
