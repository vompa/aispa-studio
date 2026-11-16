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
    public async Task Kein_Vorschlag_wenn_dadurch_nur_eine_neue_Luecke_entsteht()
    {
        // 10:30 ist zwischen 9:05 und 11:35 eingeklemmt: vorziehen ließe 20 Minuten danach frei, 9:05 nach hinten 20 Minuten davor.
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(9, 5), Ct, employee: spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(10, 30), Ct, employee: spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(11, 35), Ct, employee: spa.Anna.Id);

        Assert.Empty(await SuggestionsAsync(spa));
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
}
