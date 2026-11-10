using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Lager: Bericht Soll-Ist-Verbrauch je Artikel und Behandlung (Slice 116).</summary>
public sealed class StockUsageReportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_Soll_300_ml_und_die_Inventur_zeigt_420_ml_Verbrauch_When_der_Bericht_laeuft_Then_steht_120_ml_Mehrverbrauch_mit_den_Behandlungen_die_den_Artikel_nutzen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await ConsumeOilAsync(spa);

        await CountAsync(spa, oil, 580m);
        var report = await spa.GetAsync<StockUsageReportDto>(Url(spa), Ct);

        var line = Assert.Single(report.Lines);
        Assert.Equal((oil.Id, 300m, 420m, 120m), (line.ItemId, line.Planned, line.Actual, line.Excess));
        Assert.Equal(6.00m, line.ExcessValue);
        Assert.Equal(
            [("Gesichtsbehandlung", 100m, 1), ("Massage", 200m, 2)],
            line.Treatments.Select(t => (t.Name, t.Planned, t.Appointments)).ToList());
    }

    [Fact]
    public async Task Given_die_Inventur_findet_mehr_als_erwartet_When_der_Bericht_laeuft_Then_ist_der_Mehrverbrauch_negativ()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await ConsumeOilAsync(spa);

        await CountAsync(spa, oil, 750m);
        var line = Assert.Single((await spa.GetAsync<StockUsageReportDto>(Url(spa), Ct)).Lines);

        Assert.Equal((300m, 250m, -50m), (line.Planned, line.Actual, line.Excess));
    }

    [Fact]
    public async Task Given_ein_Bruch_als_Korrektur_When_der_Bericht_laeuft_Then_zaehlt_er_nicht_zum_Mehrverbrauch()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await ConsumeOilAsync(spa);

        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(oil.Id, StockMovementKind.Correction, -5m, Reason: "Flasche zerbrochen", StoragePlaceId: await MainAsync(spa)), Ct);
        var line = Assert.Single((await spa.GetAsync<StockUsageReportDto>(Url(spa), Ct)).Lines);

        Assert.Equal((300m, 300m, 0m), (line.Planned, line.Actual, line.Excess));
    }

    [Fact]
    public async Task Given_ein_Zeitraum_ohne_Verbrauch_When_der_Bericht_laeuft_Then_ist_er_leer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ConsumeOilAsync(spa);

        var report = await spa.GetAsync<StockUsageReportDto>($"/api/reports/locations/{spa.Location.Id}/stock-usage?from=2000-01-01&until=2000-01-31", Ct);

        Assert.Empty(report.Lines);
    }

    [Theory]
    [InlineData("2026-02-01", "2026-01-01")]
    [InlineData("2020-01-01", "2026-01-01")]
    public async Task Given_ein_ungueltiger_Zeitraum_When_der_Bericht_laeuft_Then_antwortet_die_API_mit_einer_Feldmeldung(string from, string until)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.GetAsync($"/api/reports/locations/{spa.Location.Id}/stock-usage?from={from}&until={until}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("until", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_Extra_zum_Termin_mit_Lagerartikel_When_der_Bericht_laeuft_Then_zaehlt_die_Behandlung_auch_dafuer()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var tea = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Teebeutel", "Stück", 0.1m, 0m, null), Ct);
        await Receive(spa, tea, 50m);
        await spa.PutAsync<List<TreatmentExtraDto>>($"/api/treatments/{spa.MassageTreatment.Id}/extras",
            new[] { new TreatmentExtraInput("Tee", ExtraTiming.AfterTreatment, null, tea.Id, 2m) }, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        await Complete(spa, appointment);
        await WaitForConsumptionsAsync(spa, 1);
        var line = Assert.Single((await spa.GetAsync<StockUsageReportDto>(Url(spa), Ct)).Lines);

        Assert.Equal((tea.Id, 2m), (line.ItemId, line.Planned));
        var treatment = Assert.Single(line.Treatments);
        Assert.Equal((spa.MassageTreatment.Id, 2m, 1), (treatment.TreatmentId, treatment.Planned, treatment.Appointments));
    }

    [Fact]
    public async Task Given_die_Rezeption_When_sie_den_Bericht_ruft_Then_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var response = await reception.GetAsync(Url(spa), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Given_die_Leitung_ohne_Betragsrecht_When_der_Bericht_laeuft_Then_fehlt_der_Wert_in_Euro()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await ConsumeOilAsync(spa);
        await CountAsync(spa, oil, 580m);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);

        var report = await manager.GetFromJsonAsync<StockUsageReportDto>(Url(spa), TestSpa.Json, Ct);

        var line = Assert.Single(report!.Lines);
        Assert.Equal(120m, line.Excess);
        Assert.Null(line.ExcessValue);
        Assert.Null(report.ExcessValue);
        Assert.True(report.AmountsHidden);
    }

    private static string Url(TestSpa spa)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return $"/api/reports/locations/{spa.Location.Id}/stock-usage?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}";
    }

    /// <summary>1000 ml Öl, drei abgeschlossene Termine zu je 100 ml: zwei Massagen und eine Gesichtsbehandlung; im Lager bleiben 700 ml.</summary>
    private static async Task<StockItemDto> ConsumeOilAsync(TestSpa spa)
    {
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.05m, 0m, null), Ct);
        await Receive(spa, oil, 1000m);
        var facial = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Gesichtsbehandlung", 50, 5, 10, 79m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), Ct);
        foreach (var treatmentId in new[] { spa.MassageTreatment.Id, facial.Id })
        {
            await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{treatmentId}/materials", new[] { new MaterialLine("", 100m, "", 0, oil.Id) }, Ct);
        }

        foreach (var (hour, treatment) in new[] { (10, (TreatmentDto?)null), (12, null), (14, facial) })
        {
            var response = await spa.BookAsync(TestSpa.At(hour), Ct, spa.Anna.Id, treatment: treatment);
            await Complete(spa, await TestSpa.ReadAsync<AppointmentDto>(response, Ct));
        }

        await WaitForConsumptionsAsync(spa, 3);
        return oil;
    }

    private static Task<AppointmentDto> Complete(TestSpa spa, AppointmentDto appointment) =>
        spa.SetStatusAsync(appointment.Id, AppointmentStatus.Completed, Ct);

    private static async Task Receive(TestSpa spa, StockItemDto item, decimal quantity) =>
        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(item.Id, StockMovementKind.GoodsReceipt, quantity), Ct);

    private static async Task CountAsync(TestSpa spa, StockItemDto item, decimal counted) =>
        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(item.Id, StockMovementKind.Count, counted, StoragePlaceId: await MainAsync(spa)), Ct);

    private static async Task<Guid> MainAsync(TestSpa spa) =>
        (await spa.GetAsync<List<StoragePlaceDto>>($"/api/stock/locations/{spa.Location.Id}/storage-places", Ct)).Single(p => p.Kind == StoragePlaceKind.Main).Id;

    /// <summary>Der Verbrauch wird über die Queue gebucht: warten, bis die erwarteten Bewegungen da sind.</summary>
    private static async Task WaitForConsumptionsAsync(TestSpa spa, int expected)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var url = $"/api/stock/locations/{spa.Location.Id}/movements?from={today.AddDays(-1):yyyy-MM-dd}&until={today.AddDays(1):yyyy-MM-dd}";
        for (var i = 0; i < 100; i++)
        {
            var movements = await spa.GetAsync<List<StockMovementDto>>(url, Ct);
            if (movements.Count(m => m.Kind == StockMovementKind.Consumption) >= expected)
            {
                return;
            }

            await Task.Delay(100, Ct);
        }

        throw new TimeoutException("Der Verbrauch wurde nicht gebucht.");
    }
}
