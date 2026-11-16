using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>„Kommt später“ durch den Gast über den Termin-Link: nur kurz vor dem Termin, feste Auswahl, der Termin bleibt an seiner Zeit.</summary>
public sealed partial class OnlineBookingTests
{
    /// <summary>Legt den Beginn eines gebuchten Termins auf <paramref name="fromNow"/> ab jetzt; die Tests laufen mit der echten Uhr.</summary>
    private async Task StartsInAsync(TestSpa spa, Guid appointmentId, TimeSpan fromNow)
    {
        var start = DateTimeOffset.UtcNow + fromNow;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Appointments.Where(a => a.Id == appointmentId)
            .ExecuteUpdateAsync(a => a.SetProperty(x => x.Start, start).SetProperty(x => x.End, start.AddMinutes(50)), Ct);
    }

    private static Task<HttpResponseMessage> LateAsync(HttpClient client, string token, int minutes) =>
        client.PostAsJsonAsync($"/api/online/bookings/{token}/late", new OnlineLateRequest(minutes), TestSpa.Json, Ct);

    [Fact]
    public async Task Gast_meldet_kurz_vor_dem_Termin_dass_er_spaeter_kommt_und_nimmt_es_zurueck()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(11), NewEmail());
        using var client = spa.OnlineClient();
        // Wochen vorher bietet die Seite die Meldung nicht an.
        Assert.False(confirmation.Appointment.CanReportLate);
        var early = await LateAsync(client, confirmation.ManageToken, 10);
        await StartsInAsync(spa, confirmation.Appointment.Id, TimeSpan.FromMinutes(40));

        var late = await TestSpa.ReadAsync<OnlineAppointment>(await LateAsync(client, confirmation.ManageToken, 15), Ct);
        var staff = await spa.GetAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}", Ct);
        var odd = await LateAsync(client, confirmation.ManageToken, 45);
        var wrong = await LateAsync(client, confirmation.AnamnesisToken, 10);
        var back = await TestSpa.ReadAsync<OnlineAppointment>(await LateAsync(client, confirmation.ManageToken, 0), Ct);

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal((15, true), (late.LateMinutes, late.CanReportLate));
        // Der Kalender der Rezeption sieht dieselbe Markierung; der Termin bleibt gebucht und an seiner Zeit.
        Assert.Equal((15, AppointmentStatus.Booked), (staff.LateMinutes, staff.Status));
        Assert.Equal(HttpStatusCode.Conflict, odd.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Null(back.LateMinutes);
    }

    [Fact]
    public async Task Lange_nach_Beginn_und_nach_einer_Absage_nimmt_die_Seite_keine_Verspaetung_mehr_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var started = await BookAsGuestAsync(spa, TestSpa.At(11), NewEmail());
        var cancelled = await BookAsGuestAsync(spa, TestSpa.At(13), NewEmail());
        using var client = spa.OnlineClient();
        await StartsInAsync(spa, started.Appointment.Id, TimeSpan.FromMinutes(-10));
        var justLate = await LateAsync(client, started.ManageToken, 5);
        await StartsInAsync(spa, started.Appointment.Id, TimeSpan.FromMinutes(-30));
        (await spa.Client.PostAsJsonAsync($"/api/appointments/{cancelled.Appointment.Id}/cancel", new { }, TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        await StartsInAsync(spa, cancelled.Appointment.Id, TimeSpan.FromMinutes(40));

        // Zehn Minuten nach Beginn geht es noch, eine halbe Stunde danach nicht mehr.
        Assert.Equal(HttpStatusCode.OK, justLate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await LateAsync(client, started.ManageToken, 10)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await LateAsync(client, cancelled.ManageToken, 10)).StatusCode);
    }
}
