using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Vorschläge, Termine um wenige Minuten zu verschieben, damit bei einer Person keine unbrauchbare Lücke bleibt.</summary>
public sealed class GapSuggestionTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<List<GapSuggestion>> SuggestionsAsync(TestSpa spa) =>
        spa.GetAsync<List<GapSuggestion>>($"/api/locations/{spa.Location.Id}/gap-suggestions?date={TestSpa.Monday:yyyy-MM-dd}", Ct);

    [Fact]
    public async Task Schlaegt_vor_den_Termin_nach_der_Luecke_vorzuziehen_und_danach_nichts_mehr()
    {
        // Massage bindet 65 Minuten; Dienst 9–17. 9:05 bindet Anna 9:00–10:05, 10:30 ab 10:25: 20 Minuten dazwischen bleiben leer.
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(9, 5), Ct, employee: spa.Anna.Id);
        var later = await spa.BookOkAsync(TestSpa.At(10, 30), Ct, employee: spa.Anna.Id);

        var suggestion = Assert.Single(await SuggestionsAsync(spa));
        Assert.Equal(later.Id, suggestion.AppointmentId);
        Assert.Equal(TestSpa.At(10, 10), suggestion.SuggestedStart);
        Assert.Equal(20, suggestion.GapMinutes);
        Assert.Contains("20 Minuten", suggestion.Reason, StringComparison.Ordinal);

        await spa.PostAsync<AppointmentDto>($"/api/appointments/{later.Id}/move",
            new MoveAppointmentRequest(suggestion.SuggestedStart, suggestion.EmployeeId, null, null), Ct);
        Assert.Empty(await SuggestionsAsync(spa));
    }

    [Fact]
    public async Task Eingeklemmter_Termin_wird_zur_Kette_und_die_Kette_rueckt_in_einem_Aufruf_vor()
    {
        // 10:30 ist zwischen 9:05 und 11:35 eingeklemmt: allein vorziehen ließe 20 Minuten danach frei. Zusammen mit 11:35 rücken beide vor;
        // hinter 11:35 ist bis Dienstende Platz.
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(9, 5), Ct, employee: spa.Anna.Id);
        var middle = await spa.BookOkAsync(TestSpa.At(10, 30), Ct, employee: spa.Anna.Id);
        var last = await spa.BookOkAsync(TestSpa.At(11, 35), Ct, employee: spa.Anna.Id);

        var suggestion = Assert.Single(await SuggestionsAsync(spa));
        Assert.Equal((middle.Id, TestSpa.At(10, 10)), (suggestion.AppointmentId, suggestion.SuggestedStart));
        var follower = Assert.Single(suggestion.Chain!);
        Assert.Equal((last.Id, TestSpa.At(11, 35), TestSpa.At(11, 15)), (follower.AppointmentId, follower.CurrentStart, follower.SuggestedStart));
        Assert.Contains("rücken 2 Termine nacheinander um 20 Minuten vor", suggestion.Reason, StringComparison.Ordinal);

        var moved = await spa.PostAsync<List<AppointmentDto>>("/api/gap-chains",
            new GapChainRequest([new(follower.AppointmentId, follower.SuggestedStart), new(suggestion.AppointmentId, suggestion.SuggestedStart)]), Ct);
        Assert.Equal([TestSpa.At(10, 10), TestSpa.At(11, 15)], moved.Select(a => a.Start));
        Assert.All(moved, a => Assert.Equal(spa.Anna.Id, a.EmployeeId));
        Assert.Empty(await SuggestionsAsync(spa));
    }

    [Fact]
    public async Task Scheitert_ein_Schritt_der_Kette_gehen_die_verschobenen_Termine_zurueck()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(9, 5), Ct, employee: spa.Anna.Id);
        var middle = await spa.BookOkAsync(TestSpa.At(10, 30), Ct, employee: spa.Anna.Id);
        var last = await spa.BookOkAsync(TestSpa.At(11, 35), Ct, employee: spa.Anna.Id);

        // Der zweite Schritt zielt auf 9:05, wo Anna schon behandelt.
        var response = await spa.Client.PostAsJsonAsync("/api/gap-chains",
            new GapChainRequest([new(middle.Id, TestSpa.At(10, 10)), new(last.Id, TestSpa.At(9, 5))]), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var after = await spa.GetAsync<AppointmentDto>($"/api/appointments/{middle.Id}", Ct);
        Assert.Equal(TestSpa.At(10, 30), after.Start);
        Assert.Equal(TestSpa.At(11, 35), (await spa.GetAsync<AppointmentDto>($"/api/appointments/{last.Id}", Ct)).Start);

        // Gemischte Richtungen und einzelne Termine sind keine Kette.
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PostAsJsonAsync("/api/gap-chains",
            new GapChainRequest([new(middle.Id, TestSpa.At(10, 10)), new(last.Id, TestSpa.At(12))]), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.PostAsJsonAsync("/api/gap-chains",
            new GapChainRequest([new(middle.Id, TestSpa.At(10, 10))]), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Keine_Kette_wenn_auch_hinter_dem_vierten_Termin_eine_unbrauchbare_Luecke_bliebe()
    {
        // Fünf Massagen ohne Platz dazwischen bis zum Dienstende: Die Kette bräuchte mehr als vier Termine.
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(9, 5), Ct, employee: spa.Anna.Id);
        foreach (var (hour, minute) in new[] { (10, 30), (11, 35), (12, 40), (13, 45), (14, 50) })
        {
            await spa.BookOkAsync(TestSpa.At(hour, minute), Ct, employee: spa.Anna.Id);
        }

        var suggestions = await SuggestionsAsync(spa);
        Assert.DoesNotContain(suggestions, s => s.Chain is { Count: > 0 });
    }

    [Fact]
    public async Task Die_Einwirkzeit_eines_Termins_ist_keine_Luecke()
    {
        // Packung ab 9:05: Anna ist 9:00–9:25 am Gast, 20 Minuten Einwirkzeit, dann wieder bis zur Nachbereitung. Die Pause dazwischen gehört zum Termin.
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var pack = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Packung", 60, 5, 10, 85m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], [], HandsOnMinutes: 20, ProcessingMinutes: 20), Ct);
        (await spa.BookAsync(TestSpa.At(9, 5), Ct, spa.Anna.Id, spa.RoomB.Id, pack)).EnsureSuccessStatusCode();

        Assert.Empty(await SuggestionsAsync(spa));
    }

    [Fact]
    public async Task Ein_Vorschlag_nennt_die_Herkunft_Treatwell()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(9, 5), Ct, employee: spa.Anna.Id);
        var later = await spa.BookOkAsync(TestSpa.At(10, 30), Ct, employee: spa.Anna.Id);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
            await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Appointments.Where(a => a.Id == later.Id)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.Source, AppointmentSource.Treatwell), Ct);
        }

        var suggestion = Assert.Single(await SuggestionsAsync(spa));

        Assert.Equal(AppointmentSource.Treatwell, suggestion.Source);
    }

    [Fact]
    public async Task Mit_days_kommen_die_Vorschlaege_mehrerer_Tage_und_mit_employeeId_nur_die_einer_Person()
    {
        // Dieselbe Lücke bei Anna am Montag und bei Ben am Montag darauf (beide arbeiten nur montags).
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var next = TestSpa.Monday.AddDays(7);
        await spa.BookOkAsync(TestSpa.At(9, 5), Ct, employee: spa.Anna.Id);
        var annas = await spa.BookOkAsync(TestSpa.At(10, 30), Ct, employee: spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(9, 5, next), Ct, employee: spa.Ben.Id);
        var bens = await spa.BookOkAsync(TestSpa.At(10, 30, next), Ct, employee: spa.Ben.Id);
        var path = $"/api/locations/{spa.Location.Id}/gap-suggestions?date={TestSpa.Monday:yyyy-MM-dd}";

        var day = await spa.GetAsync<List<GapSuggestion>>(path, Ct);
        var week = await spa.GetAsync<List<GapSuggestion>>($"{path}&days=7", Ct);
        var twoWeeks = await spa.GetAsync<List<GapSuggestion>>($"{path}&days=14", Ct);
        var eight = await spa.GetAsync<List<GapSuggestion>>($"{path}&days=7&employeeId={spa.Ben.Id}", Ct);

        Assert.Equal([annas.Id], day.Select(s => s.AppointmentId));
        Assert.Equal([annas.Id], week.Select(s => s.AppointmentId));
        // Höchstens sieben Tage: der Montag darauf ist der achte.
        Assert.Equal([annas.Id], twoWeeks.Select(s => s.AppointmentId));
        Assert.Empty(eight);
        var bensWeek = await spa.GetAsync<List<GapSuggestion>>(
            $"/api/locations/{spa.Location.Id}/gap-suggestions?date={TestSpa.Monday.AddDays(1):yyyy-MM-dd}&days=7&employeeId={spa.Ben.Id}", Ct);
        Assert.Equal([bens.Id], bensWeek.Select(s => s.AppointmentId));
    }
}
