using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Arten von Aktionen (Nachtrag zu ADR 0071): kurzfristig freie Termine (Vorlauf zwischen Buchung und Beginn) und schwache Zeiten (Wochenraster aus Wochentag und
/// Tageszeit) neben der befristeten Aktion. Die Kassa schlägt sie nur am passenden Termin vor, den höheren Rabatt zuerst, nie gestapelt, mit Beleg-Text; dazu Prüfregeln,
/// die Vorschau des Gästepreises und die Rechte.
/// </summary>
public sealed class PromotionKindTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    private static PromotionInput Input(string name, PromotionKind? kind, decimal percent = 10m, int? leadHours = null, PromotionSlot[]? slots = null,
        PromotionScope scope = PromotionScope.AllTreatments, Guid[]? treatments = null) =>
        new(name, $"Gästetext {name}", Today.AddDays(-5), Today.AddDays(5), true, [], percent, null, scope, treatments ?? [], true)
        {
            Kind = kind,
            LeadHours = leadHours,
            Slots = slots,
        };

    private static Task<PromotionDto> CreateAsync(TestSpa spa, PromotionInput input) => spa.PostAsync<PromotionDto>("/api/promotions", input, Ct);

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    /// <summary>Bucht am Testmontag (Wien) zur Stunde, setzt den Buchungszeitpunkt auf so viele Stunden vor Beginn und legt die Rechnung an.</summary>
    private async Task<BillDto> BillAsync(TestSpa spa, CashRegisterDto register, int hour, double bookedHoursBefore)
    {
        var appointment = await spa.BookOkAsync(TestSpa.At(hour), Ct);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            var bookedAt = appointment.Start.AddHours(-bookedHoursBefore);
            await db.Appointments.Where(a => a.Id == appointment.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAt, bookedAt), Ct);
        }

        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        return await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
    }

    private static Task<List<PromotionOfferDto>> OffersAsync(HttpClient client, Guid billId) =>
        client.GetFromJsonAsync<List<PromotionOfferDto>>($"/api/pos/bills/{billId}/promotions", TestSpa.Json, Ct)!;

    private static Task<HttpResponseMessage> ApplyAsync(HttpClient client, Guid billId, Guid promotionId) =>
        client.PostAsJsonAsync($"/api/pos/bills/{billId}/promotions/{promotionId}/apply", new { }, TestSpa.Json, Ct);

    [Fact]
    public async Task Kurzfristig_frei_trifft_nur_Termine_die_hoechstens_den_Vorlauf_vor_Beginn_gebucht_wurden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var lastMinute = await CreateAsync(spa, Input("Kurzfristig", PromotionKind.LastMinute, 20m, leadHours: 24));

        var shortNotice = await BillAsync(spa, register, 10, 3);
        var exactlyAtLimit = await BillAsync(spa, register, 12, 24);
        var early = await BillAsync(spa, register, 14, 25);

        Assert.Equal([lastMinute.Id], (await OffersAsync(spa.Client, shortNotice.Id)).Select(o => o.PromotionId));
        Assert.Equal([lastMinute.Id], (await OffersAsync(spa.Client, exactlyAtLimit.Id)).Select(o => o.PromotionId));
        Assert.Empty(await OffersAsync(spa.Client, early.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await ApplyAsync(spa.Client, early.Id, lastMinute.Id)).StatusCode);

        var applied = await TestSpa.ReadAsync<BillDto>(await ApplyAsync(spa.Client, shortNotice.Id, lastMinute.Id), Ct);
        Assert.Equal((15.80m, 63.20m, "Aktion Kurzfristig 20 %"), (applied.Lines[0].Discount, applied.Lines[0].Amount, applied.Lines[0].DiscountNote));
    }

    [Fact]
    public async Task Schwache_Zeiten_treffen_nur_Termine_in_einem_gewaehlten_Feld_des_Wochenrasters()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var offPeak = await CreateAsync(spa, Input("Ruhige Vormittage", PromotionKind.OffPeak, 15m,
            slots: [new(DayOfWeek.Monday, DayPart.Morning), new(DayOfWeek.Tuesday, DayPart.Evening)]));

        // Montag 11:00 liegt im Vormittag, Montag 12:00 schon im Nachmittag.
        var morning = await BillAsync(spa, register, 11, 72);
        var noon = await BillAsync(spa, register, 12, 72);

        var offers = await OffersAsync(spa.Client, morning.Id);
        Assert.Equal(("Ruhige Vormittage", "−15 %", 11.85m), (Assert.Single(offers).Name, offers[0].DiscountText, offers[0].TotalDiscount));
        Assert.Empty(await OffersAsync(spa.Client, noon.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await ApplyAsync(spa.Client, noon.Id, offPeak.Id)).StatusCode);

        var applied = await TestSpa.ReadAsync<BillDto>(await ApplyAsync(spa.Client, morning.Id, offPeak.Id), Ct);
        Assert.Equal((11.85m, "Aktion Ruhige Vormittage 15 %"), (applied.Lines[0].Discount, applied.Lines[0].DiscountNote));
    }

    [Fact]
    public async Task Treffen_zwei_Arten_zu_steht_der_hoehere_Rabatt_vorn_und_nach_dem_Klick_gilt_nur_einer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var limited = await CreateAsync(spa, Input("Herbst", null, 10m));
        var offPeak = await CreateAsync(spa, Input("Montagmorgen", PromotionKind.OffPeak, 20m, slots: [new(DayOfWeek.Monday, DayPart.Morning)]));
        var lastMinute = await CreateAsync(spa, Input("Kurzfristig", PromotionKind.LastMinute, 15m, leadHours: 48));
        var bill = await BillAsync(spa, register, 10, 2);

        var offers = await OffersAsync(spa.Client, bill.Id);
        Assert.Equal([offPeak.Id, lastMinute.Id, limited.Id], offers.Select(o => o.PromotionId));

        // Nichts wird automatisch angewendet; erst der Klick setzt den Rabatt, danach gibt es keinen zweiten.
        Assert.Equal(0m, (await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct)).Lines[0].Discount);
        var applied = await TestSpa.ReadAsync<BillDto>(await ApplyAsync(spa.Client, bill.Id, offPeak.Id), Ct);
        Assert.Equal((15.80m, "Aktion Montagmorgen 20 %"), (applied.Lines[0].Discount, applied.Lines[0].DiscountNote));
        Assert.Empty(await OffersAsync(spa.Client, bill.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await ApplyAsync(spa.Client, bill.Id, limited.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ApplyAsync(spa.Client, bill.Id, lastMinute.Id)).StatusCode);

        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, applied.Open, null, null)]), Ct);
        Assert.Equal((63.20m, 15.80m, "Aktion Montagmorgen 20 %"), (receipt.Lines[0].Amount, receipt.Lines[0].Discount, receipt.Lines[0].DiscountNote));
    }

    [Fact]
    public async Task Pruefregeln_fuer_Art_Vorlauf_und_Wochenraster_und_Rueckgabe_der_Felder()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        async Task<List<string>> FieldsAsync(PromotionInput input)
        {
            var response = await spa.Client.PostAsJsonAsync("/api/promotions", input, TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
            return [.. problem.Errors.Keys.Order(StringComparer.Ordinal)];
        }

        Assert.Equal(["leadHours"], await FieldsAsync(Input("Ohne Vorlauf", PromotionKind.LastMinute)));
        Assert.Equal(["leadHours"], await FieldsAsync(Input("Vorlauf 0", PromotionKind.LastMinute, leadHours: 0)));
        Assert.Equal(["leadHours"], await FieldsAsync(Input("Vorlauf 169", PromotionKind.LastMinute, leadHours: 169)));
        Assert.Equal(["slots"], await FieldsAsync(Input("Ohne Feld", PromotionKind.OffPeak)));
        Assert.Equal(["slots"], await FieldsAsync(Input("Nur jederzeit", PromotionKind.OffPeak, slots: [new(DayOfWeek.Monday, DayPart.Any)])));
        Assert.Equal(["scope"], await FieldsAsync(Input("Pakete kurzfristig", PromotionKind.LastMinute, leadHours: 24, scope: PromotionScope.SelectedPackages,
            treatments: [spa.MassageTreatment.Id])));
        Assert.Equal(["kind"], await FieldsAsync(Input("Unbekannt", (PromotionKind)99)));

        // Ohne Art bleibt es eine befristete Aktion; was nicht zur Art gehört, wird nicht gespeichert.
        var legacy = await CreateAsync(spa, Input("Alt", null, leadHours: 5, slots: [new(DayOfWeek.Friday, DayPart.Evening)]));
        Assert.Equal((PromotionKind.Limited, (int?)null), (legacy.Kind, legacy.LeadHours));
        Assert.Empty(legacy.Slots!);
        var lastMinute = await CreateAsync(spa, Input("Kurz", PromotionKind.LastMinute, leadHours: 168));
        Assert.Equal((PromotionKind.LastMinute, (int?)168), (lastMinute.Kind, lastMinute.LeadHours));
        var offPeak = await CreateAsync(spa, Input("Raster", PromotionKind.OffPeak,
            slots: [new(DayOfWeek.Sunday, DayPart.Evening), new(DayOfWeek.Monday, DayPart.Afternoon), new(DayOfWeek.Monday, DayPart.Morning), new(DayOfWeek.Monday, DayPart.Morning)]));
        Assert.Equal([new(DayOfWeek.Monday, DayPart.Morning), new(DayOfWeek.Monday, DayPart.Afternoon), new PromotionSlot(DayOfWeek.Sunday, DayPart.Evening)], offPeak.Slots);
        Assert.Equal(offPeak.Slots, (await spa.GetAsync<PromotionDto>($"/api/promotions/{offPeak.Id}", Ct)).Slots);

        // Art wechseln: das Raster fällt weg.
        var changed = await spa.PutAsync<PromotionDto>($"/api/promotions/{offPeak.Id}", Input("Raster", PromotionKind.LastMinute, leadHours: 12), Ct);
        Assert.Equal((PromotionKind.LastMinute, (int?)12), (changed.Kind, changed.LeadHours));
        Assert.Empty(changed.Slots!);
    }

    [Fact]
    public async Task Vorschau_zeigt_Beispieltermin_und_Gaestepreise_wie_die_Kassa_nur_mit_Katalogrecht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var tea = await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Teezeremonie", 30, 0, 0, 30m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);
        var input = Input("Vorschau", PromotionKind.OffPeak, 15m, slots: [new(Today.AddDays(2).DayOfWeek, DayPart.Afternoon)]);

        var preview = await spa.PostAsync<PromotionPreviewDto>("/api/promotions/preview", input, Ct);

        Assert.EndsWith(", Nachmittag", preview.Example, StringComparison.Ordinal);
        Assert.Contains(Today.AddDays(2).ToString("dd.MM.", CultureInfo.InvariantCulture), preview.Example, StringComparison.Ordinal);
        Assert.Equal([("Massage", 79m, 67.15m), ("Teezeremonie", 30m, 25.50m)], preview.Lines.Select(l => (l.Name, l.Price, l.GuestPrice)));
        Assert.Contains("höheren zuerst", preview.Note, StringComparison.Ordinal);

        // Ausgewählte Behandlung mit Betrag; ohne gültigen Rabatt bleibt der Preis.
        var amount = await spa.PostAsync<PromotionPreviewDto>("/api/promotions/preview",
            input with { DiscountPercent = null, DiscountAmount = 5m, Scope = PromotionScope.SelectedTreatments, TreatmentIds = [tea.Id] }, Ct);
        Assert.Equal([("Teezeremonie", 30m, 25m)], amount.Lines.Select(l => (l.Name, l.Price, l.GuestPrice)));
        var none = await spa.PostAsync<PromotionPreviewDto>("/api/promotions/preview", input with { DiscountPercent = null }, Ct);
        Assert.All(none.Lines, l => Assert.Equal(l.Price, l.GuestPrice));
        var lastMinute = await spa.PostAsync<PromotionPreviewDto>("/api/promotions/preview", Input("Kurz", PromotionKind.LastMinute, leadHours: 24), Ct);
        Assert.Contains("höchstens 24 Stunden", lastMinute.Example, StringComparison.Ordinal);

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsJsonAsync("/api/promotions/preview", input, TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsJsonAsync("/api/promotions", input, TestSpa.Json, Ct)).StatusCode);
    }
}
