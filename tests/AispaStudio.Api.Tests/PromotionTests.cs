using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Promotions;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Aktionen (ADR 0071): anlegen, ändern, pausieren und archivieren mit Rechten, Gültigkeitsregeln, „laufende Aktionen am Datum“ mit Zeitzone,
/// und an der Kassa der Vorschlag (nur bei passender Aktion) und das Anwenden (die Kassa setzt den Rabatt, der Beleg nennt die Aktion).
/// </summary>
public sealed class PromotionTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    private static PromotionInput Input(string name = "Herbst", decimal? percent = 10m, decimal? amount = null, int from = -5, int until = 5, bool all = true,
        Guid[]? locations = null, PromotionScope scope = PromotionScope.AllTreatments, Guid[]? treatments = null, bool active = true) =>
        new(name, $"Gästetext {name}", Today.AddDays(from), Today.AddDays(until), all, locations ?? [], amount is null ? percent : null, amount, scope, treatments ?? [], active);

    private static Task<PromotionDto> CreateAsync(TestSpa spa, PromotionInput input) => spa.PostAsync<PromotionDto>("/api/promotions", input, Ct);

    private static async Task<HttpValidationProblemDetails> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
    }

    private static Task<List<ActivePromotionDto>> ActiveAsync(HttpClient client, Guid locationId, DateOnly? date = null) =>
        client.GetFromJsonAsync<List<ActivePromotionDto>>($"/api/promotions/active?locationId={locationId}{(date is { } d ? $"&date={d:yyyy-MM-dd}" : "")}", TestSpa.Json, Ct)!;

    // ---------- Verwaltung ----------

    [Fact]
    public async Task Aktion_anlegen_aendern_pausieren_und_archivieren_mit_Zustand_je_Zeitraum()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var running = await CreateAsync(spa, Input("Herbst"));
        var planned = await CreateAsync(spa, Input("Winter", from: 10, until: 40));
        var expired = await CreateAsync(spa, Input("Sommer", from: -40, until: -10));
        var paused = await CreateAsync(spa, Input("Frühling", active: false));

        Assert.Equal((PromotionPhase.Running, PromotionPhase.Planned, PromotionPhase.Expired, PromotionPhase.Paused),
            (running.Phase, planned.Phase, expired.Phase, paused.Phase));
        Assert.Equal((10m, (decimal?)null, PromotionScope.AllTreatments, true, false), (running.DiscountPercent, running.DiscountAmount, running.Scope, running.AllLocations, running.IsArchived));
        Assert.Equal(["Sommer", "Frühling", "Herbst", "Winter"], (await spa.GetAsync<List<PromotionDto>>("/api/promotions", Ct)).Select(p => p.Name));

        // Ändern: Betrag statt Prozent, auf einen Standort und eine Behandlung begrenzen, pausieren.
        var changed = await spa.PutAsync<PromotionDto>($"/api/promotions/{running.Id}",
            Input("Herbst 2", percent: null, amount: 5m, all: false, locations: [spa.Location.Id], scope: PromotionScope.SelectedTreatments,
                treatments: [spa.MassageTreatment.Id], active: false), Ct);
        Assert.Equal(("Herbst 2", (decimal?)null, 5m, false, PromotionPhase.Paused), (changed.Name, changed.DiscountPercent, changed.DiscountAmount, changed.AllLocations, changed.Phase));
        Assert.Equal([spa.Location.Id], changed.LocationIds);
        Assert.Equal([spa.MassageTreatment.Id], changed.TreatmentIds);
        // Wieder auf alle Standorte und alle Behandlungen: die Kinder verschwinden.
        var back = await spa.PutAsync<PromotionDto>($"/api/promotions/{running.Id}", Input("Herbst 2"), Ct);
        Assert.Equal((PromotionPhase.Running, true), (back.Phase, back.AllLocations));
        Assert.Empty(back.LocationIds);
        Assert.Empty(back.TreatmentIds);

        // Archivieren: bleibt im Verlauf, steht aber nicht mehr in der Liste und läuft nie mehr.
        var archived = await spa.PostAsync<PromotionDto>($"/api/promotions/{expired.Id}/archive", new { }, Ct);
        Assert.True(archived.IsArchived);
        Assert.DoesNotContain(await spa.GetAsync<List<PromotionDto>>("/api/promotions", Ct), p => p.Id == expired.Id);
        Assert.Contains(await spa.GetAsync<List<PromotionDto>>("/api/promotions?includeArchived=true", Ct), p => p.Id == expired.Id && p.IsArchived);
        Assert.True((await spa.GetAsync<PromotionDto>($"/api/promotions/{expired.Id}", Ct)).IsArchived);
        var restored = await spa.PostAsync<PromotionDto>($"/api/promotions/{expired.Id}/unarchive", new { }, Ct);
        Assert.False(restored.IsArchived);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"/api/promotions/{Guid.NewGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PutAsJsonAsync($"/api/promotions/{Guid.NewGuid()}", Input(), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"/api/promotions/{Guid.NewGuid()}/archive", new { }, TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Pruefregeln_Prozent_oder_Betrag_Zeitraum_Standorte_Behandlungen_und_Name()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await CreateAsync(spa, Input("Herbst"));
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);

        async Task<List<string>> FieldsAsync(PromotionInput input)
        {
            var problem = await ProblemAsync(await spa.Client.PostAsJsonAsync("/api/promotions", input, TestSpa.Json, Ct));
            return [.. problem.Errors.Keys.Order(StringComparer.Ordinal)];
        }

        Assert.Equal(["discountPercent"], await FieldsAsync(Input("Ohne Rabatt", percent: null)));
        Assert.Equal(["discountPercent"], await FieldsAsync(Input("Beides", percent: 10m) with { DiscountAmount = 5m }));
        Assert.Equal(["discountPercent"], await FieldsAsync(Input("Prozent 0", percent: 0m)));
        Assert.Equal(["discountPercent"], await FieldsAsync(Input("Prozent 100", percent: 100m)));
        Assert.Equal(["discountPercent"], await FieldsAsync(Input("Prozent Stellen", percent: 10.123m)));
        Assert.Equal(["discountAmount"], await FieldsAsync(Input("Betrag 0", percent: null, amount: 0m)));
        Assert.Equal(["discountAmount"], await FieldsAsync(Input("Betrag Stellen", percent: null, amount: 5.123m)));
        Assert.Equal(["validUntil"], await FieldsAsync(Input("Zeitraum", from: 5, until: 1)));
        Assert.Equal(["name"], await FieldsAsync(Input("   ")));
        Assert.Equal(["name"], await FieldsAsync(Input(new string('x', 41))));
        Assert.Equal(["name"], await FieldsAsync(Input("HERBST")));
        Assert.Equal(["guestText"], await FieldsAsync(Input("Ohne Text") with { GuestText = " " }));
        Assert.Equal(["locationIds"], await FieldsAsync(Input("Ohne Standort", all: false)));
        Assert.Equal(["locationIds"], await FieldsAsync(Input("Doppelt", all: true, locations: [spa.Location.Id])));
        Assert.Equal(["locationIds"], await FieldsAsync(Input("Unbekannt", all: false, locations: [Guid.NewGuid()])));
        Assert.Equal(["treatmentIds"], await FieldsAsync(Input("Ohne Behandlung", scope: PromotionScope.SelectedTreatments)));
        Assert.Equal(["treatmentIds"], await FieldsAsync(Input("Pakete ohne", scope: PromotionScope.SelectedPackages)));
        Assert.Equal(["treatmentIds"], await FieldsAsync(Input("Zu viel", treatments: [spa.MassageTreatment.Id])));
        Assert.Equal(["treatmentIds"], await FieldsAsync(Input("Unbekannte", scope: PromotionScope.SelectedTreatments, treatments: [Guid.NewGuid()])));
        Assert.Equal(["scope"], await FieldsAsync(Input("Art") with { Scope = (PromotionScope)99 }));

        // Gültige Randfälle: ein Tag, zwei Standorte, Pakete.
        var oneDay = await CreateAsync(spa, Input("Ein Tag", from: 0, until: 0, all: false, locations: [spa.Location.Id, other.Id]));
        var packages = await CreateAsync(spa, Input("Pakete", amount: 20m, percent: null, scope: PromotionScope.SelectedPackages, treatments: [spa.MassageTreatment.Id]));
        Assert.Equal(2, oneDay.LocationIds.Count);
        Assert.Equal(PromotionScope.SelectedPackages, packages.Scope);

        // Ein archivierter Name ist wieder frei, die Aktion selbst darf ihren Namen behalten.
        var old = await CreateAsync(spa, Input("Alt", from: -30, until: -20));
        await spa.PostAsync<PromotionDto>($"/api/promotions/{old.Id}/archive", new { }, Ct);
        await CreateAsync(spa, Input("Alt"));
        await spa.PutAsync<PromotionDto>($"/api/promotions/{oneDay.Id}", Input("Ein Tag", from: 0, until: 1, all: false, locations: [spa.Location.Id]), Ct);
    }

    [Fact]
    public async Task Verwalten_nur_mit_Katalogrecht_und_nur_im_eigenen_Standort_Lesen_fuer_jeden_Zugang()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var everywhere = await CreateAsync(spa, Input("Überall"));
        var here = await CreateAsync(spa, Input("Hier", all: false, locations: [spa.Location.Id]));
        var there = await CreateAsync(spa, Input("Dort", all: false, locations: [other.Id]));
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var anonymous = fixture.CreateClient();

        // Rezeption und Therapeutin verwalten nichts, lesen aber die laufenden Aktionen ihres Standorts.
        foreach (var client in new[] { reception, therapist })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/promotions", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/promotions", Input("Heimlich"), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/promotions/{here.Id}", Input("Heimlich"), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/promotions/{here.Id}/archive", new { }, TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(["Hier", "Überall"], (await ActiveAsync(client, spa.Location.Id)).Select(p => p.Name).Order());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/promotions/active?locationId={other.Id}", Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/promotions/active?locationId={spa.Location.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"/api/promotions/active?locationId={Guid.NewGuid()}", Ct)).StatusCode);

        // Die Standortleitung eines Standorts sieht und ändert nur Aktionen ihres Standorts, und legt keine für alle an.
        Assert.Equal(["Hier"], (await manager.GetFromJsonAsync<List<PromotionDto>>("/api/promotions", TestSpa.Json, Ct))!.Select(p => p.Name));
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/promotions/{everywhere.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/promotions/{there.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsJsonAsync($"/api/promotions/{there.Id}", Input("Dort", all: false, locations: [other.Id]), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"/api/promotions/{everywhere.Id}/archive", new { }, TestSpa.Json, Ct)).StatusCode);
        Assert.Contains("allLocations", (await ProblemAsync(await manager.PostAsJsonAsync("/api/promotions", Input("Für alle"), TestSpa.Json, Ct))).Errors.Keys);
        Assert.Contains("locationIds", (await ProblemAsync(await manager.PostAsJsonAsync("/api/promotions",
            Input("Fremd", all: false, locations: [other.Id]), TestSpa.Json, Ct))).Errors.Keys);
        var own = await manager.PostAsJsonAsync("/api/promotions", Input("Eigene", all: false, locations: [spa.Location.Id]), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
    }

    // ---------- Laufende Aktionen ----------

    [Fact]
    public async Task Laufende_Aktionen_am_Datum_nach_Standort_Zustand_und_Gueltigkeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var tea = await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Teezeremonie", 30, 0, 0, 30m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);
        await CreateAsync(spa, Input("Alle Massagen", scope: PromotionScope.SelectedTreatments, treatments: [spa.MassageTreatment.Id, tea.Id], until: 3));
        await CreateAsync(spa, Input("Betrag", percent: null, amount: 5.5m, until: 9, all: false, locations: [spa.Location.Id]));
        await CreateAsync(spa, Input("Nur dort", all: false, locations: [other.Id]));
        await CreateAsync(spa, Input("Pausiert", active: false));
        await CreateAsync(spa, Input("Vorbei", from: -20, until: -1));
        await CreateAsync(spa, Input("Später", from: 2, until: 20));
        var archived = await CreateAsync(spa, Input("Archiviert"));
        await spa.PostAsync<PromotionDto>($"/api/promotions/{archived.Id}/archive", new { }, Ct);

        var now = await ActiveAsync(spa.Client, spa.Location.Id);
        var inThreeDays = await ActiveAsync(spa.Client, spa.Location.Id, Today.AddDays(3));
        var inFourDays = await ActiveAsync(spa.Client, spa.Location.Id, Today.AddDays(4));
        var yesterday = await ActiveAsync(spa.Client, spa.Location.Id, Today.AddDays(-1));
        var overThere = await ActiveAsync(spa.Client, other.Id);

        // Früheres Ende zuerst; erster und letzter Tag zählen mit.
        Assert.Equal(["Alle Massagen", "Betrag"], now.Select(p => p.Name));
        Assert.Equal(["Alle Massagen", "Betrag", "Später"], inThreeDays.Select(p => p.Name));
        Assert.Equal(["Betrag", "Später"], inFourDays.Select(p => p.Name));
        Assert.Equal(["Vorbei", "Alle Massagen", "Betrag"], yesterday.Select(p => p.Name));
        Assert.Equal(["Alle Massagen", "Nur dort"], overThere.Select(p => p.Name));
        Assert.DoesNotContain(now, p => p.Name is "Nur dort" or "Pausiert" or "Vorbei" or "Später" or "Archiviert");

        var massages = now.Single(p => p.Name == "Alle Massagen");
        Assert.Equal(("Gästetext Alle Massagen", "−10 %", PromotionScope.SelectedTreatments), (massages.GuestText, massages.DiscountText, massages.Scope));
        Assert.Equal(["Massage", "Teezeremonie"], massages.Treatments.Select(t => t.Name));
        var amount = now.Single(p => p.Name == "Betrag");
        Assert.Equal(("−5,50 €", (decimal?)null, 5.5m), (amount.DiscountText, amount.DiscountPercent, amount.DiscountAmount));
        Assert.Empty(amount.Treatments);

        // Ohne Datum gilt heute am Standort.
        Assert.Equal(now.Select(p => p.Id), (await ActiveAsync(spa.Client, spa.Location.Id, Today)).Select(p => p.Id));
    }

    [Fact]
    public async Task Heute_zaehlt_in_der_Zeitzone_des_Standorts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var auckland = await spa.PostAsync<LocationDto>("/api/locations",
            new LocationInput("Auckland", "Pacific/Auckland", "EUR", [new OpeningHours(DayOfWeek.Monday, new(9, 0), new(20, 0))]), Ct);
        var day = new DateOnly(2027, 3, 2);
        await spa.PostAsync<PromotionDto>("/api/promotions", new PromotionInput("Dienstag", "Nur am 2. März", day, day, true, [], 10m, null, PromotionScope.AllTreatments, [], true), Ct);

        // 12:00 UTC am 1. März: in Wien noch der 1. März, in Auckland (UTC+13) schon der 2. März.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var service = new PromotionService(db, new FixedTime(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2027, 3, 1), await service.TodayAsync(spa.Location.Id, Ct));
        Assert.Equal(new DateOnly(2027, 3, 2), await service.TodayAsync(auckland.Id, Ct));
        Assert.Null(await service.TodayAsync(Guid.NewGuid(), Ct));
        Assert.Empty(await service.GetActiveAsync(spa.Location.Id, (await service.TodayAsync(spa.Location.Id, Ct))!.Value, Ct));
        Assert.Equal("Dienstag", Assert.Single(await service.GetActiveAsync(auckland.Id, (await service.TodayAsync(auckland.Id, Ct))!.Value, Ct)).Name);
        Assert.Empty(await service.GetActiveAsync(Guid.NewGuid(), day, Ct));
    }

    // ---------- Kassa ----------

    private static async Task<CashRegisterDto> CreateRegisterAsync(TestSpa spa) =>
        await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14], FiscalCountry.AT,
            "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);

    private static async Task<BillDto> BillWithAppointmentAsync(TestSpa spa, CashRegisterDto register, int hour = 10)
    {
        var appointment = await spa.BookOkAsync(TestSpa.At(hour), Ct);
        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        return await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), Ct);
    }

    private static Task<List<PromotionOfferDto>> OffersAsync(HttpClient client, Guid billId) =>
        client.GetFromJsonAsync<List<PromotionOfferDto>>($"/api/pos/bills/{billId}/promotions", TestSpa.Json, Ct)!;

    private static Task<HttpResponseMessage> ApplyAsync(HttpClient client, Guid billId, Guid promotionId) =>
        client.PostAsJsonAsync($"/api/pos/bills/{billId}/promotions/{promotionId}/apply", new { }, TestSpa.Json, Ct);

    [Fact]
    public async Task Vorschlag_erscheint_nur_bei_passender_laufender_Aktion()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var tea = await spa.PostAsync<TreatmentDto>("/api/treatments", new TreatmentInput("Teezeremonie", 30, 0, 0, 30m, [spa.Massage.Id], [spa.RoomA.Id], []), Ct);
        var bill = await BillWithAppointmentAsync(spa, register);
        Assert.Empty(await OffersAsync(spa.Client, bill.Id));

        // Keine Treffer: pausiert, vorbei, später, anderer Standort, andere Behandlung, nur Pakete, archiviert.
        await CreateAsync(spa, Input("Pausiert", active: false));
        await CreateAsync(spa, Input("Vorbei", from: -20, until: -1));
        await CreateAsync(spa, Input("Später", from: 1, until: 20));
        await CreateAsync(spa, Input("Dort", all: false, locations: [other.Id]));
        await CreateAsync(spa, Input("Tee", scope: PromotionScope.SelectedTreatments, treatments: [tea.Id]));
        await CreateAsync(spa, Input("Pakete", scope: PromotionScope.SelectedPackages, treatments: [spa.MassageTreatment.Id]));
        var archived = await CreateAsync(spa, Input("Archiviert"));
        await spa.PostAsync<PromotionDto>($"/api/promotions/{archived.Id}/archive", new { }, Ct);
        Assert.Empty(await OffersAsync(spa.Client, bill.Id));

        // Treffer: alle Behandlungen (10 %) und ausgewählte Behandlung (5 € je Position); der größere Rabatt zuerst.
        var all = await CreateAsync(spa, Input("Herbst"));
        var chosen = await CreateAsync(spa, Input("Massage-Woche", percent: null, amount: 5m, scope: PromotionScope.SelectedTreatments, treatments: [spa.MassageTreatment.Id]));
        var offers = await OffersAsync(spa.Client, bill.Id);

        Assert.Equal([all.Id, chosen.Id], offers.Select(o => o.PromotionId));
        Assert.Equal(("Herbst", "−10 %", 7.90m), (offers[0].Name, offers[0].DiscountText, offers[0].TotalDiscount));
        Assert.Equal((bill.Lines[0].Id, 7.90m), (offers[0].Lines[0].LineId, offers[0].Lines[0].Discount));
        Assert.Equal(("−5,00 €", 5m), (offers[1].DiscountText, offers[1].TotalDiscount));
        // Der Vorschlag ändert nichts.
        var unchanged = await spa.GetAsync<BillDto>($"/api/pos/bills/{bill.Id}", Ct);
        Assert.Equal((0m, 79m), (unchanged.Lines[0].Discount, unchanged.Total));

        // Bezahlte Rechnung und unbekannte Rechnung.
        await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout", new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, 79m, null, null)]), Ct);
        Assert.Empty(await OffersAsync(spa.Client, bill.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"/api/pos/bills/{Guid.NewGuid()}/promotions", Ct)).StatusCode);
    }

    [Fact]
    public async Task Anwenden_setzt_den_Rabatt_der_Position_und_der_Beleg_nennt_die_Aktion()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var promotion = await CreateAsync(spa, Input("Herbst"));
        var bill = await BillWithAppointmentAsync(spa, register);
        var oil = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines",
            new BillLineInput(BillLineKind.Custom, null, null, 1, 12.50m, 0, "Tee", TaxCategory.Normal, null, null, null), Ct);

        var applied = await TestSpa.ReadAsync<BillDto>(await ApplyAsync(spa.Client, bill.Id, promotion.Id), Ct);

        // Nur die Behandlung bekommt den Rabatt, nicht der Tee (kein Teil der Aktion), und nichts wird doppelt.
        Assert.Equal((7.90m, 71.10m, "Aktion Herbst 10 %"), (applied.Lines[0].Discount, applied.Lines[0].Amount, applied.Lines[0].DiscountNote));
        Assert.Equal((0m, 12.50m, (string?)null), (applied.Lines[1].Discount, applied.Lines[1].Amount, applied.Lines[1].DiscountNote));
        Assert.Equal(71.10m + 12.50m, applied.Total);
        Assert.Empty(await OffersAsync(spa.Client, bill.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await ApplyAsync(spa.Client, bill.Id, promotion.Id)).StatusCode);

        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, applied.Open, null, null)]), Ct);
        var line = receipt.Lines.Single(l => l.AppointmentId != null);
        Assert.Equal((71.10m, 7.90m, "Aktion Herbst 10 %"), (line.Amount, line.Discount, line.DiscountNote));
        Assert.Equal(83.60m, receipt.Total);
        Assert.Equal(83.60m, receipt.Taxes.Sum(t => t.Gross));
        var text = await spa.Client.GetStringAsync($"/api/pos/receipts/{receipt.Id}/print?format=Text&width=42", Ct);
        var rows = text.Split('\n');
        var at = Array.FindIndex(rows, r => r.Contains(spa.MassageTreatment.Name, StringComparison.Ordinal));
        Assert.EndsWith("79,00", rows[at], StringComparison.Ordinal);
        Assert.Contains("Aktion Herbst 10 %", rows[at + 1], StringComparison.Ordinal);
        Assert.EndsWith("-7,90", rows[at + 1], StringComparison.Ordinal);
        Assert.Equal(oil.Lines[1].Id, applied.Lines[1].Id);

        // Das Storno kehrt den Rabatt mit um.
        var voided = await spa.PostAsync<ReceiptDto>($"/api/pos/receipts/{receipt.Id}/void", new VoidRequest("Falsch kassiert"), Ct);
        var back = voided.Lines.Single(l => l.AppointmentId != null);
        Assert.Equal((-71.10m, -7.90m, "Aktion Herbst 10 %"), (back.Amount, back.Discount, back.DiscountNote));
    }

    [Fact]
    public async Task Betrag_je_Position_nennt_auf_dem_Beleg_den_Prozentsatz_und_geht_nicht_ueber_den_Preis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var promotion = await CreateAsync(spa, Input("Fünfer", percent: null, amount: 5m));
        var bill = await BillWithAppointmentAsync(spa, register);
        var second = await spa.BookOkAsync(TestSpa.At(12), Ct);
        await spa.SetStatusAsync(second.Id, AppointmentStatus.CheckedIn, Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", new BillLineInput(BillLineKind.Appointment,
            second.Id, null, 1, null, 0, null, null, null, null, null), Ct);
        var big = await CreateAsync(spa, Input("Riesenrabatt", percent: null, amount: 500m));

        var offers = await OffersAsync(spa.Client, bill.Id);
        var applied = await TestSpa.ReadAsync<BillDto>(await ApplyAsync(spa.Client, bill.Id, promotion.Id), Ct);

        // 500 € wären höher als jede Position: kein Vorschlag. 5 € auf beide Termine: 6,3 % von 79 €.
        Assert.Equal([promotion.Id], offers.Select(o => o.PromotionId));
        Assert.Equal(10m, offers[0].TotalDiscount);
        Assert.All(applied.Lines, l => Assert.Equal((5m, 74m, "Aktion Fünfer 6,3 %"), (l.Discount, l.Amount, l.DiscountNote)));
        Assert.Equal(HttpStatusCode.BadRequest, (await ApplyAsync(spa.Client, bill.Id, big.Id)).StatusCode);
    }

    [Fact]
    public async Task Anderer_Rabatt_geht_vor_und_die_Aktion_braucht_keine_Freigabe_ueber_der_Rabattgrenze()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}/discount", new CustomerDiscount(10m), Ct);
        var halfOff = await CreateAsync(spa, Input("Halber Preis", percent: 50m));
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        // Mit Kundenrabatt: kein Vorschlag, Aktion lässt sich nicht aufstapeln.
        var discounted = await BillWithAppointmentAsync(spa, register);
        Assert.Equal(7.90m, discounted.Lines[0].Discount);
        Assert.Empty(await OffersAsync(reception, discounted.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await ApplyAsync(reception, discounted.Id, halfOff.Id)).StatusCode);

        // Ohne Kundenrabatt gibt die Rezeption die Aktion mit 50 % (über der Grenze von 20 %): sie wurde von der Leitung festgelegt.
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}/discount", new CustomerDiscount(0m), Ct);
        var plain = await BillWithAppointmentAsync(spa, register, 12);
        Assert.Equal(0m, plain.Lines[0].Discount);
        var viaReception = await TestSpa.ReadAsync<BillDto>(await ApplyAsync(reception, plain.Id, halfOff.Id), Ct);
        Assert.Equal((39.50m, 39.50m, "Aktion Halber Preis 50 %"), (viaReception.Lines[0].Discount, viaReception.Lines[0].Amount, viaReception.Lines[0].DiscountNote));

        // Ein von Hand gegebener Rabatt geht ebenfalls vor.
        var manual = await BillWithAppointmentAsync(spa, register, 14);
        manual = await TestSpa.ReadAsync<BillDto>(await spa.Client.PutAsJsonAsync($"/api/pos/bills/{manual.Id}/lines/{manual.Lines[0].Id}/discount",
            new LineDiscountInput(2m), TestSpa.Json, Ct), Ct);
        Assert.Empty(await OffersAsync(spa.Client, manual.Id));
        Assert.Equal("Rabatt 2,5 %", manual.Lines[0].DiscountNote);
    }

    [Fact]
    public async Task Aktion_auf_Pakete_senkt_den_Paketpreis_und_der_Gutschein_traegt_den_bezahlten_Wert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await CreateRegisterAsync(spa);
        var promotion = await CreateAsync(spa, Input("Paketwoche", scope: PromotionScope.SelectedPackages, treatments: [spa.MassageTreatment.Id]));
        var bill = await spa.PostAsync<BillDto>("/api/pos/bills", new BillInput(register.Id, spa.Customer.Id, []), Ct);
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", new BillLineInput(BillLineKind.Voucher, null, null, 1, 600m, 0, null, null, null,
            VoucherKind.SinglePurpose, spa.MassageTreatment.Id, Units: 10), Ct);
        // Ein Einzweckgutschein ohne Paket und ein Termin gehören nicht zur Paketaktion.
        bill = await spa.PostAsync<BillDto>($"/api/pos/bills/{bill.Id}/lines", new BillLineInput(BillLineKind.Voucher, null, null, 1, null, 0, null, null, null,
            VoucherKind.SinglePurpose, spa.MassageTreatment.Id), Ct);

        var offers = await OffersAsync(spa.Client, bill.Id);
        var applied = await TestSpa.ReadAsync<BillDto>(await ApplyAsync(spa.Client, bill.Id, promotion.Id), Ct);

        Assert.Equal(60m, Assert.Single(offers).TotalDiscount);
        Assert.Equal((60m, 540m, "Aktion Paketwoche 10 %"), (applied.Lines[0].Discount, applied.Lines[0].Amount, applied.Lines[0].DiscountNote));
        Assert.Equal((0m, 79m), (applied.Lines[1].Discount, applied.Lines[1].Amount));

        var receipt = await spa.PostAsync<ReceiptDto>($"/api/pos/bills/{bill.Id}/checkout",
            new CheckoutRequest(null, [new PaymentInput(PaymentMethod.Cash, applied.Open, null, null)]), Ct);
        var package = receipt.IssuedVouchers.Single(v => v.Units == 10);
        Assert.Equal((540m, 10), (package.InitialValue, package.UnitsLeft));
        Assert.Equal((540m, 60m), (receipt.Lines[0].Amount, receipt.Lines[0].Discount));

        // Die Paketaktion trifft keine Termin-Positionen.
        var other = await BillWithAppointmentAsync(spa, register);
        Assert.Empty(await OffersAsync(spa.Client, other.Id));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
