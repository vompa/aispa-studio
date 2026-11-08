using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Bedarfsprüfung: Verbrauch der gebuchten Termine gegen den Bestand, und die Warnung beim Buchen, wenn ein Termin eine Grenze reißt.</summary>
public sealed class StockDemandTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string Day = $"{TestSpa.Monday:yyyy-MM-dd}";

    [Fact]
    public async Task Bedarf_eines_Tages_meldet_Mindestmenge_und_fehlenden_Bestand()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilAsync(spa, minimum: 50m, onHand: 100m);
        var extraItem = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Teebeutel", "Stk", 0.1m, 0m, null), Ct);
        await spa.PutAsync<List<TreatmentExtraDto>>($"/api/treatments/{spa.MassageTreatment.Id}/extras",
            new[] { new TreatmentExtraInput("Tee", ExtraTiming.AfterTreatment, null, extraItem.Id, 1m) }, Ct);
        for (var i = 0; i < 3; i++)
        {
            await spa.BookOkAsync(TestSpa.At(10 + i * 2), Ct, i % 2 == 0 ? spa.Anna.Id : spa.Ben.Id);
        }

        var three = await spa.GetAsync<StockDemandDto>($"/api/stock/locations/{spa.Location.Id}/demand?from={Day}&until={Day}", Ct);
        var rest = Assert.Single(three.Warnings, w => w.ItemId == oil.Id);
        await spa.BookOkAsync(TestSpa.At(15), Ct, spa.Ben.Id);
        var four = await spa.GetAsync<StockDemandDto>($"/api/stock/locations/{spa.Location.Id}/demand?from={Day}&until={Day}", Ct);
        var short4 = Assert.Single(four.Warnings, w => w.ItemId == oil.Id);
        var emptyDay = await spa.GetAsync<StockDemandDto>($"/api/stock/locations/{spa.Location.Id}/demand?from=2027-03-02&until=2027-03-02", Ct);

        Assert.Equal(3, three.Appointments);
        Assert.Equal((StockWarningKind.BelowMinimum, 90m, 100m, 3), (rest.Warning!.Value, rest.Needed, rest.OnHand, rest.Appointments));
        Assert.Contains("unter der Mindestmenge von 50 ml", rest.Message);
        Assert.Equal(0, rest.TreatmentsLeft);
        Assert.Equal((StockWarningKind.Insufficient, 120m, 4), (short4.Warning!.Value, short4.Needed, short4.Appointments));
        Assert.Equal((20m, 0m), (short4.Shortfall, rest.Shortfall));
        Assert.Equal(TestSpa.At(15), short4.ShortFrom);
        Assert.Contains("reicht nicht für alle gebuchten Termine", short4.Message);
        Assert.Contains("noch für 3 Behandlungen", short4.Message);
        Assert.Equal(4m, four.Lines.Single(l => l.ItemId == extraItem.Id).Needed);
        Assert.Equal(0, emptyDay.Appointments);
        Assert.Empty(emptyDay.Lines);
    }

    [Fact]
    public async Task Abgeschlossene_und_stornierte_Termine_zaehlen_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilAsync(spa, minimum: 0m, onHand: 100m);
        var done = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var cancelled = await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest("Test"), Ct);
        await spa.SetStatusAsync(done.Id, AppointmentStatus.Completed, Ct);

        var demand = await spa.GetAsync<StockDemandDto>($"/api/stock/locations/{spa.Location.Id}/demand?from={Day}&until={Day}", Ct);

        var line = demand.Lines.Single(l => l.ItemId == oil.Id);
        Assert.Equal((1, 30m), (line.Appointments, line.Needed));
    }

    [Fact]
    public async Task Bedarf_prueft_den_Zeitraum_und_die_Berechtigung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = $"/api/stock/locations/{spa.Location.Id}/demand";

        var reversed = await spa.Client.GetAsync($"{url}?from=2027-03-05&until=2027-03-01", Ct);
        var tooLong = await spa.Client.GetAsync($"{url}?from=2027-03-01&until=2027-05-01", Ct);
        var unknown = await spa.Client.GetAsync($"/api/stock/locations/{Guid.NewGuid()}/demand?from={Day}&until={Day}", Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);

        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync($"{url}?from={Day}&until={Day}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await therapist.GetAsync($"{url}?from={Day}&until={Day}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Buchen_warnt_nur_wenn_der_neue_Termin_eine_Grenze_reisst_und_bleibt_erlaubt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await OilAsync(spa, minimum: 40m, onHand: 100m);
        var monday = NextMonday();
        var warnings = new List<IReadOnlyList<string>?>();
        var previews = new List<List<StockDemandLine>>();
        var appointments = new List<AppointmentDto>();
        foreach (var (hour, employee) in new[] { (10, spa.Anna.Id), (10, spa.Ben.Id), (12, spa.Anna.Id), (12, spa.Ben.Id) })
        {
            var start = TestSpa.At(hour, 0, monday);
            previews.Add(await spa.GetAsync<List<StockDemandLine>>(
                $"/api/stock/locations/{spa.Location.Id}/demand/booking?treatmentId={spa.MassageTreatment.Id}&start={Uri.EscapeDataString(start.ToString("O"))}", Ct));
            var response = await spa.BookAsync(start, Ct, employee);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var booked = await TestSpa.ReadAsync<AppointmentDto>(response, Ct);
            appointments.Add(booked);
            warnings.Add(booked.Warnings);
        }

        var farAway = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        Assert.Equal(4, appointments.Count);
        Assert.Contains("noch für 2 Behandlungen", Assert.Single(warnings[0]!));
        Assert.Null(warnings[1]);
        Assert.Contains("unter der Mindestmenge", Assert.Single(warnings[2]!));
        Assert.Contains("reicht nicht für alle gebuchten Termine", Assert.Single(warnings[3]!));
        Assert.All(warnings.OfType<IReadOnlyList<string>>(), w => Assert.All(w, text => Assert.Contains("Massageöl", text)));
        Assert.Equal(new StockWarningKind?[] { StockWarningKind.LowCoverage, null, StockWarningKind.BelowMinimum, StockWarningKind.Insufficient },
            previews.Select(p => p.SingleOrDefault(l => l.ItemId == oil.Id)?.Warning));
        Assert.Null(farAway.Warnings);
    }

    [Fact]
    public async Task Ohne_Lagermaterial_im_Rezept_gibt_es_keine_Warnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(10, 0, NextMonday()), Ct, spa.Anna.Id), Ct);

        Assert.Null(appointment.Warnings);
    }

    /// <summary>Material mit 30 ml je Massage und Bestand am Standort.</summary>
    private static async Task<StockItemDto> OilAsync(TestSpa spa, decimal minimum, decimal onHand)
    {
        var oil = await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput("Massageöl", "ml", 0.04m, minimum, null), Ct);
        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(oil.Id, StockMovementKind.GoodsReceipt, onHand, 0.05m), Ct);
        await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials",
            new[] { new MaterialLine("", 30m, "", 0, oil.Id) }, Ct);
        return oil;
    }

    /// <summary>Der nächste Montag, mindestens zwei Tage voraus: liegt innerhalb der 14 Tage, in denen beim Buchen geprüft wird.</summary>
    private static DateOnly NextMonday()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
