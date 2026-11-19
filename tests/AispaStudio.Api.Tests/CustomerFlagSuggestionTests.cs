using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Vorschläge für Kennzeichen aus dem Verlauf (Block „Behandlung und Kalender“, Punkt 3): „Braucht mehr Zeit“ bei wiederholter Verspätung oder Überziehen,
/// „Leitung informieren“ bei wiederholtem Nicht-Erscheinen; nur für wer das Kennzeichen setzen darf; verworfen bleibt verworfen, übernommen verschwindet.
/// </summary>
public sealed class CustomerFlagSuggestionTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly DaysAgo(int days) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime).AddDays(-days);

    private static Task<List<CustomerFlagSuggestion>> SuggestionsAsync(HttpClient client, Guid customerId) =>
        client.GetFromJsonAsync<List<CustomerFlagSuggestion>>($"/api/customers/{customerId}/flags/suggestions", TestSpa.Json, Ct)!;

    /// <summary>Vergangenen Termin direkt in der Datenbank anpassen: Status, gemeldete Verspätung, tatsächlicher Beginn und Ende.</summary>
    private static async Task PastAsync(TestSpa spa, int daysAgo, AppointmentStatus status, int? late = null, int? overrun = null)
    {
        var appointment = await spa.BookInThePastAsync(TestSpa.At(10, date: DaysAgo(daysAgo)), Ct);
        await using var scope = spa.Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var started = appointment.Start;
        var finished = overrun is { } minutes ? appointment.End.AddMinutes(minutes) : (DateTimeOffset?)null;
        await db.Appointments.Where(a => a.Id == appointment.Id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.Status, status)
            .SetProperty(a => a.LateMinutes, late)
            .SetProperty(a => a.StartedAt, overrun is null ? null : started)
            .SetProperty(a => a.FinishedAt, finished), Ct);
    }

    [Fact]
    public async Task Verspaetung_und_Ueberziehen_ergeben_den_Vorschlag_mehr_Zeit_fuer_die_Rezeption()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        await PastAsync(spa, 20, AppointmentStatus.Completed, late: 15);
        Assert.Empty(await SuggestionsAsync(reception, spa.Customer.Id));

        // Zu wenig: 5 Minuten Verspätung und 5 Minuten länger zählen nicht; vor mehr als 12 Monaten zählt nicht.
        await PastAsync(spa, 30, AppointmentStatus.Completed, late: 5, overrun: 5);
        await PastAsync(spa, 400, AppointmentStatus.Completed, late: 20);
        Assert.Empty(await SuggestionsAsync(reception, spa.Customer.Id));

        await PastAsync(spa, 40, AppointmentStatus.Completed, overrun: 15);
        var suggestion = Assert.Single(await SuggestionsAsync(reception, spa.Customer.Id));
        Assert.Equal(CustomerFlagKind.MoreTime, suggestion.Kind);
        Assert.Equal("In den letzten 12 Monaten 1-mal mindestens 10 Minuten zu spät gekommen und 1-mal länger behandelt (bis 15 Minuten).", suggestion.Evidence);

        // Übernehmen: Das Kennzeichen ist gesetzt, der Vorschlag weg.
        (await reception.PostAsJsonAsync($"/api/customers/{spa.Customer.Id}/flags", new CustomerFlagInput(CustomerFlagKind.MoreTime, null, suggestion.Evidence),
            TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        Assert.Empty(await SuggestionsAsync(reception, spa.Customer.Id));
    }

    [Fact]
    public async Task Zweimal_nicht_erschienen_schlaegt_der_Leitung_vor_sie_zu_informieren_verworfen_bleibt_verworfen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        await PastAsync(spa, 10, AppointmentStatus.NoShow);
        await PastAsync(spa, 50, AppointmentStatus.NoShow);
        await PastAsync(spa, 60, AppointmentStatus.Cancelled);

        var suggestion = Assert.Single(await SuggestionsAsync(spa.Client, spa.Customer.Id));
        Assert.Equal((CustomerFlagKind.InformManagement, "In den letzten 12 Monaten 2-mal nicht erschienen."), (suggestion.Kind, suggestion.Evidence));

        // Die Rezeption darf „Leitung informieren“ nicht setzen und bekommt den Vorschlag nicht; verwerfen darf sie ihn auch nicht.
        Assert.Empty(await SuggestionsAsync(reception, spa.Customer.Id));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reception.PostAsync($"/api/customers/{spa.Customer.Id}/flags/suggestions/InformManagement/dismiss", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await spa.Client.PostAsync($"/api/customers/{spa.Customer.Id}/flags/suggestions/InformManagement/dismiss", null, Ct)).StatusCode);
        Assert.Empty(await SuggestionsAsync(spa.Client, spa.Customer.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"/api/customers/{Guid.NewGuid()}/flags/suggestions", Ct)).StatusCode);
    }
}
