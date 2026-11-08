using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Zum Termin bereitstellen (Extras mit Uhrzeit, Haken, Summen), Abbuchung mit dem Rezept ohne Doppelbuchung, Vorbereitung mit Lagerartikel und Rezeptionshandbuch.</summary>
public sealed class OperationsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string Day = $"{TestSpa.Monday:yyyy-MM-dd}";

    [Fact]
    public async Task Extras_werden_gespeichert_geprueft_und_behalten_ihre_Id()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var tea = await Item(spa, "Teebeutel", "Stk");
        var url = $"/api/treatments/{spa.MassageTreatment.Id}/extras";
        TreatmentExtraInput[] input =
        [
            new("Wasser", ExtraTiming.OnArrival),
            new("Früchteteller", ExtraTiming.BeforeEnd, 10),
            new("Tee", ExtraTiming.AfterTreatment, null, tea.Id, 2m),
        ];

        var saved = await spa.PutAsync<List<TreatmentExtraDto>>(url, input, Ct);
        var again = await spa.PutAsync<List<TreatmentExtraDto>>(url, input.Reverse().Reverse().ToArray(), Ct);
        var listed = await spa.GetAsync<List<TreatmentExtraDto>>(url, Ct);

        Assert.Equal(["Wasser", "Früchteteller", "Tee"], listed.Select(e => e.Name));
        Assert.Equal(saved.Select(e => e.Id), again.Select(e => e.Id));
        Assert.Equal(("Teebeutel", "Stk", 2m), (listed[2].StockItemName, listed[2].Unit, listed[2].Quantity));
        Assert.Equal(10, listed[1].MinutesBeforeEnd);

        foreach (var bad in new TreatmentExtraInput[][]
        {
            [new("", ExtraTiming.OnArrival)],
            [new("Wasser", ExtraTiming.OnArrival, null, null, 1m)],
            [new("Tee", ExtraTiming.OnArrival, null, tea.Id, 0m)],
            [new("Tee", ExtraTiming.OnArrival, null, Guid.NewGuid(), 1m)],
            [new("Obst", ExtraTiming.BeforeEnd, null)],
            [new("Obst", ExtraTiming.BeforeEnd, 500)],
        })
        {
            var response = await spa.Client.PutAsJsonAsync(url, bad, TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PutAsJsonAsync(url, input, TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync(url, Ct)).StatusCode);
    }

    [Fact]
    public async Task Bereitstellen_hat_Uhrzeit_Wunsch_Haken_und_Summe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<List<TreatmentExtraDto>>($"/api/treatments/{spa.MassageTreatment.Id}/extras",
            new TreatmentExtraInput[]
            {
                new("Wasser", ExtraTiming.OnArrival),
                new("Früchteteller", ExtraTiming.BeforeEnd, 10),
                new("Tee", ExtraTiming.AfterTreatment),
            }, Ct);
        await spa.PutAsync<CustomerPreferences>($"/api/reports/customers/{spa.Customer.Id}/preferences",
            new CustomerPreferences(null, null, null, null, null, null, null, "Pfefferminztee, kein Zucker"), Ct);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        var second = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id, spa.RoomB.Id);
        var cancelled = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id, spa.RoomA.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest("Test"), Ct);

        var day = await spa.GetAsync<ServingDayDto>($"/api/locations/{spa.Location.Id}/servings?date={Day}", Ct);
        var fruit = day.Items.Single(i => i.AppointmentId == first.Id && i.Text == "Früchteteller");
        var water = day.Items.First(i => i.AppointmentId == first.Id && i.Text == "Wasser");
        var check = await spa.Client.PutAsJsonAsync($"/api/appointments/{first.Id}/extras/{fruit.ExtraId}/check", new ServingCheck(true), TestSpa.Json, Ct);
        var checked1 = await spa.GetAsync<ServingDayDto>($"/api/locations/{spa.Location.Id}/servings?date={Day}", Ct);
        await spa.Client.PutAsJsonAsync($"/api/appointments/{first.Id}/extras/{fruit.ExtraId}/check", new ServingCheck(false), TestSpa.Json, Ct);
        var unchecked1 = await spa.GetAsync<ServingDayDto>($"/api/locations/{spa.Location.Id}/servings?date={Day}", Ct);

        Assert.Equal(6, day.Items.Count);
        Assert.Equal(TestSpa.At(10), water.DueAt);
        Assert.Equal(first.End.AddMinutes(-10), fruit.DueAt);
        Assert.Equal(first.End, day.Items.First(i => i.AppointmentId == first.Id && i.Text == "Tee").DueAt);
        Assert.Equal("Pfefferminztee, kein Zucker", fruit.Wishes);
        Assert.Equal(spa.RoomA.Name, fruit.RoomName);
        Assert.All(day.Items, i => Assert.DoesNotContain(i.AppointmentId, new[] { cancelled.Id }));
        Assert.Contains(day.Totals, t => t is { Text: "Früchteteller", Count: 2 } && t.DueTimes.Count == 2);
        Assert.Contains(day.Totals, t => t is { Text: "Tee", Count: 2 });
        Assert.Equal(HttpStatusCode.NoContent, check.StatusCode);
        var done = checked1.Items.Single(i => i.AppointmentId == first.Id && i.Text == "Früchteteller");
        Assert.True(done.Done);
        Assert.NotNull(done.DoneAt);
        Assert.False(string.IsNullOrEmpty(done.DoneBy));
        Assert.False(unchecked1.Items.Single(i => i.AppointmentId == first.Id && i.Text == "Früchteteller").Done);
        Assert.DoesNotContain(day.Items, i => i.AppointmentId == second.Id && i.Wishes is null);
        var strange = await spa.Client.PutAsJsonAsync($"/api/appointments/{first.Id}/extras/{Guid.NewGuid()}/check", new ServingCheck(true), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NotFound, strange.StatusCode);
    }

    [Fact]
    public async Task Abschluss_bucht_Rezept_und_Extras_zusammen_ab_und_Vorbereitung_bucht_nichts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await Item(spa, "Massageöl", "ml");
        await Receive(spa, oil.Id, 1000m);
        await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials",
            new[] { new MaterialLine("", 30m, "", 0, oil.Id) }, Ct);
        await spa.PutAsync<List<TreatmentExtraDto>>($"/api/treatments/{spa.MassageTreatment.Id}/extras",
            new[] { new TreatmentExtraInput("Ölprobe", ExtraTiming.OnArrival, null, oil.Id, 2m) }, Ct);
        var steps = await spa.PutAsync<List<PreparationStepDto>>($"/api/reports/treatments/{spa.MassageTreatment.Id}/preparation",
            new[] { new PreparationStepInput("Öl vorwärmen", 10, oil.Id) }, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        await spa.Client.PutAsJsonAsync($"/api/reports/appointments/{appointment.Id}/preparation/{steps[0].Id}", new PreparationCheck(true), TestSpa.Json, Ct);
        var afterPreparation = await Quantity(spa, oil.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        var used = await WaitAsync(spa, oil.Id, q => q != 1000m);
        var overview = await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct);

        Assert.Equal(1000m, afterPreparation);
        Assert.Equal(968m, used);
        Assert.Equal(("Massageöl", 30m + 2m, "ml"), (steps[0].StockItemName, steps[0].Quantity, steps[0].Unit));
        Assert.Equal(968m, overview.Items.Single(i => i.ItemId == oil.Id).Quantity);
    }

    [Fact]
    public async Task Vorbereitung_verweist_nur_auf_Material_des_Rezepts_und_steht_auf_der_Karte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var oil = await Item(spa, "Lavendelöl", "ml");
        var url = $"/api/reports/treatments/{spa.MassageTreatment.Id}/preparation";

        var rejected = await spa.Client.PutAsJsonAsync(url, new[] { new PreparationStepInput("Öl bereitstellen", null, oil.Id) }, TestSpa.Json, Ct);
        await spa.PutAsync<List<MaterialLine>>($"/api/costs/treatments/{spa.MassageTreatment.Id}/materials",
            new[] { new MaterialLine("", 3m, "", 0, oil.Id) }, Ct);
        var saved = await spa.PutAsync<List<PreparationStepDto>>(url, new[] { new PreparationStepInput("Öl bereitstellen", 5, oil.Id), new PreparationStepInput("Liege vorwärmen", null) }, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var card = await spa.GetAsync<TreatmentCard>($"/api/reports/appointments/{appointment.Id}/card", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal((oil.Id, "Lavendelöl", 3m), (saved[0].StockItemId, saved[0].StockItemName, saved[0].Quantity));
        Assert.Null(saved[1].StockItemId);
        Assert.Contains(card.Todo, t => t.Text == "Öl bereitstellen (Lavendelöl, 3 ml)");
        Assert.Contains(card.Todo, t => t.Text == "Liege vorwärmen");
    }

    [Fact]
    public async Task Handbuch_Eintraege_pflegen_und_wichtige_am_Tagesstart_erinnern()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var always = await spa.PostAsync<HandbookEntryDto>("/api/handbook",
            new HandbookEntryInput("Gäste mit Hund", "Hunde sind im Haus nicht erlaubt.", "Empfang", true, []), Ct);
        var mondays = await spa.PostAsync<HandbookEntryDto>("/api/handbook",
            new HandbookEntryInput("Wäschelieferung", "Montags bis 10 Uhr.", "Wäsche", true, [DayOfWeek.Monday]), Ct);
        await spa.PostAsync<HandbookEntryDto>("/api/handbook", new HandbookEntryInput("Nur nachlesen", "Kein Hinweis am Tagesstart.", null, false, []), Ct);
        var off = await spa.PostAsync<HandbookEntryDto>("/api/handbook", new HandbookEntryInput("Alt", "Nicht mehr gültig.", null, true, [], false), Ct);

        var monday = await spa.GetAsync<List<HandbookEntryDto>>($"/api/handbook/reminders?date={Day}", Ct);
        var tuesday = await spa.GetAsync<List<HandbookEntryDto>>($"/api/handbook/reminders?date={TestSpa.Monday.AddDays(1):yyyy-MM-dd}", Ct);
        var briefing = await spa.GetAsync<BriefingDto>($"/api/reports/locations/{spa.Location.Id}/briefing?date={Day}", Ct);
        var edited = await spa.PutAsync<HandbookEntryDto>($"/api/handbook/{always.Id}",
            new HandbookEntryInput("Gäste mit Hund", "Hunde nur im Hundeparkplatz.", "Empfang", false, [DayOfWeek.Friday]), Ct);
        var all = await spa.GetAsync<List<HandbookEntryDto>>("/api/handbook", Ct);
        var empty = await spa.Client.PostAsJsonAsync("/api/handbook", new HandbookEntryInput(" ", "Text", null, false, []), TestSpa.Json, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var receptionWrites = await reception.PostAsJsonAsync("/api/handbook", new HandbookEntryInput("X", "Y", null, false, []), TestSpa.Json, Ct);
        var receptionReads = await reception.GetFromJsonAsync<List<HandbookEntryDto>>("/api/handbook", TestSpa.Json, Ct);
        var deleted = await spa.Client.DeleteAsync($"/api/handbook/{off.Id}", Ct);

        Assert.Equal(["Gäste mit Hund", "Wäschelieferung"], monday.Select(e => e.Title).ToArray());
        Assert.Equal(["Gäste mit Hund"], tuesday.Select(e => e.Title));
        Assert.Equal(monday.Select(e => e.Id), briefing.Reminders!.Select(e => e.Id));
        Assert.Equal(mondays.Id, monday.Single(e => e.Days.Contains(DayOfWeek.Monday)).Id);
        Assert.Equal([DayOfWeek.Friday], edited.Days);
        Assert.False(edited.RemindAtDayStart);
        Assert.Equal(4, all.Count);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, receptionWrites.StatusCode);
        Assert.Equal(4, receptionReads!.Count);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Tagesstart_nennt_Summen_der_Extras()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<List<TreatmentExtraDto>>($"/api/treatments/{spa.MassageTreatment.Id}/extras",
            new[] { new TreatmentExtraInput("Tee", ExtraTiming.AfterTreatment) }, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id, spa.RoomB.Id);

        var briefing = await spa.GetAsync<BriefingDto>($"/api/reports/locations/{spa.Location.Id}/briefing?date={Day}", Ct);

        var tea = Assert.Single(briefing.Servings!);
        Assert.Equal(("Tee", 2), (tea.Text, tea.Count));
    }

    private static async Task<StockItemDto> Item(TestSpa spa, string name, string unit) =>
        await spa.PostAsync<StockItemDto>("/api/stock/items", new StockItemInput(name, unit, 0.05m, 0m, null), Ct);

    private static async Task Receive(TestSpa spa, Guid itemId, decimal quantity) =>
        await spa.PostAsync<List<StockMovementDto>>($"/api/stock/locations/{spa.Location.Id}/movements",
            new StockMovementInput(itemId, StockMovementKind.GoodsReceipt, quantity, 0.05m), Ct);

    private static async Task<decimal> Quantity(TestSpa spa, Guid itemId) =>
        (await spa.GetAsync<StockOverviewDto>($"/api/stock/locations/{spa.Location.Id}", Ct)).Items.Single(l => l.ItemId == itemId).Quantity;

    private static async Task<decimal> WaitAsync(TestSpa spa, Guid itemId, Func<decimal, bool> done)
    {
        for (var i = 0; ; i++)
        {
            var quantity = await Quantity(spa, itemId);
            if (done(quantity) || i == 100)
            {
                return quantity;
            }

            await Task.Delay(100, Ct);
        }
    }
}
