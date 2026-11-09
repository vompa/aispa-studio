using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Wiederkehrende Termine: Vorschau mit Konflikten, Anlegen, ab hier stornieren, „nach dem letzten Besuch“.</summary>
public sealed class AppointmentSeriesTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Week(int weeks) => TestSpa.Monday.AddDays(7 * weeks);

    private static Task<BlockDto> AwayAsync(TestSpa spa, Guid employee, DateTimeOffset from, DateTimeOffset until) =>
        spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Employee, employee, from, until, "Urlaub"), Ct);

    [Fact]
    public async Task Vorschau_zeigt_je_Termin_ob_frei_andere_Zeit_oder_andere_Person()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        // In zwei Wochen ist Anna um 10 Uhr belegt, in vier Wochen den ganzen Tag weg (ein Feiertag, Ostermontag, ändert nichts, siehe HolidayNeutralityTests).
        await AwayAsync(spa, spa.Anna.Id, TestSpa.At(9, 30, Week(2)), TestSpa.At(11, 30, Week(2)));
        await AwayAsync(spa, spa.Anna.Id, TestSpa.At(8, date: Week(4)), TestSpa.At(18, date: Week(4)));

        var preview = await spa.PostAsync<List<SeriesOccurrence>>($"/api/appointments/{first.Id}/series/preview",
            new SeriesInput(2, Count: 3), Ct);

        Assert.Equal([Week(2), Week(4), Week(6)], preview.Select(o => o.Date));
        Assert.Equal(SeriesOccurrenceState.OtherTime, preview[0].State);
        Assert.Equal(spa.Anna.Id, preview[0].EmployeeId);
        Assert.Contains("Anna", preview[0].Problem, StringComparison.Ordinal);
        Assert.Equal(SeriesOccurrenceState.OtherEmployee, preview[1].State);
        Assert.Equal(spa.Ben.Id, preview[1].EmployeeId);
        Assert.Equal(TestSpa.At(10, date: Week(4)), preview[1].Start);
        Assert.Equal(SeriesOccurrenceState.Free, preview[2].State);

        // Feste Therapeutin: dann kein Ersatz durch jemand anderen.
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}/booking-preferences", new CustomerBookingPreferences(spa.Anna.Id, true), Ct);
        var fixedPreview = await spa.PostAsync<List<SeriesOccurrence>>($"/api/appointments/{first.Id}/series/preview",
            new SeriesInput(2, Count: 3), Ct);
        Assert.Equal(SeriesOccurrenceState.Conflict, fixedPreview[1].State);
        Assert.Null(fixedPreview[1].EmployeeId);

        foreach (var wrong in new[] { new SeriesInput(0, Count: 3), new SeriesInput(2), new SeriesInput(2, Until: TestSpa.Monday.AddDays(400)) })
        {
            var response = await spa.Client.PostAsJsonAsync($"/api/appointments/{first.Id}/series/preview", wrong, TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Serie_anlegen_und_ab_hier_stornieren()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var preview = await spa.PostAsync<List<SeriesOccurrence>>($"/api/appointments/{first.Id}/series/preview",
            new SeriesInput(1, Until: Week(3)), Ct);

        var created = await spa.PostAsync<SeriesResult>($"/api/appointments/{first.Id}/series",
            new SeriesCreate(new SeriesInput(1, Until: Week(3)), [.. preview.Select(o => new SeriesPick(o.Start, o.EmployeeId))]), Ct);

        Assert.Equal(4, created.Changed.Count);
        Assert.Empty(created.Failed);
        Assert.All(created.Changed, a => Assert.Equal(created.Series.Id, a.SeriesId));
        Assert.Equal([TestSpa.At(10), TestSpa.At(10, date: Week(1)), TestSpa.At(10, date: Week(2)), TestSpa.At(10, date: Week(3))], created.Series.Starts);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Week(1):yyyy-MM-dd}", Ct);
        Assert.Equal(4, Assert.Single(day.Series!).Starts.Count);
        var again = await spa.Client.PostAsJsonAsync($"/api/appointments/{first.Id}/series/preview", new SeriesInput(1, Count: 2), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        // Ab dem dritten Termin stornieren: dieser und der folgende fallen weg, die ersten beiden bleiben.
        var cancelled = await spa.PostAsync<SeriesResult>($"/api/appointments/{created.Changed[2].Id}/series/cancel-following",
            new CancelAppointmentRequest("Gast pausiert"), Ct);
        Assert.Equal(2, cancelled.Changed.Count);
        Assert.All(cancelled.Changed, a => Assert.Equal(AppointmentStatus.Cancelled, a.Status));
        Assert.True(cancelled.Series.Ended);
        Assert.Equal(2, cancelled.Series.Starts.Count);
    }

    [Fact]
    public async Task Nach_dem_letzten_Besuch_bucht_den_naechsten_erst_wenn_der_letzte_erledigt_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var rule = new SeriesInput(4, SeriesMode.AfterLastVisit, Count: 2);
        var preview = await spa.PostAsync<List<SeriesOccurrence>>($"/api/appointments/{first.Id}/series/preview", rule, Ct);
        var created = await spa.PostAsync<SeriesResult>($"/api/appointments/{first.Id}/series",
            new SeriesCreate(rule, [new SeriesPick(preview[0].Start, preview[0].EmployeeId)]), Ct);
        var next = created.Changed[1];

        Assert.Equal(TestSpa.At(10, date: Week(4)), Assert.Single(preview).Start);
        // Der nächste Termin kommt vier Wochen nach dem erledigten in Woche 4, also in Woche 8; dort ist Anna weg, also bei Ben.
        await AwayAsync(spa, spa.Anna.Id, TestSpa.At(8, date: Week(8)), TestSpa.At(18, date: Week(8)));
        await spa.SetStatusAsync(next.Id, AppointmentStatus.Completed, Ct);

        var third = await WaitForStartsAsync(spa, Week(8), created.Series.Id, 3);
        Assert.Equal(TestSpa.At(10, date: Week(8)), third.Starts[2]);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Week(8):yyyy-MM-dd}", Ct);
        Assert.Equal(spa.Ben.Id, Assert.Single(day.Appointments).EmployeeId);

        // Anzahl erreicht (zwei weitere Termine): nach dem dritten kommt keiner mehr.
        await spa.SetStatusAsync(day.Appointments[0].Id, AppointmentStatus.Completed, Ct);
        await Task.Delay(500, Ct);
        var after = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Week(8):yyyy-MM-dd}", Ct);
        Assert.Equal(3, Assert.Single(after.Series!).Starts.Count);
    }

    private static async Task<SeriesInfo> WaitForStartsAsync(TestSpa spa, DateOnly date, Guid seriesId, int count)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={date:yyyy-MM-dd}", Ct);
            if (day.Series?.FirstOrDefault(s => s.Id == seriesId) is { } series && series.Starts.Count >= count)
            {
                return series;
            }

            await Task.Delay(100, timeout.Token);
        }
    }
}
