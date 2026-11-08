using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Schritt 12: Vor- und Nachbereitung gelten je Termin, lassen sich je Termin ändern; der Gast-Puffer steht getrennt.</summary>
public sealed class AppointmentTimesTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string TimesPath(AppointmentDto appointment) => $"/api/appointments/{appointment.Id}/times";

    [Fact]
    public async Task Aendern_speichert_die_Werte_und_die_Bindung_von_Termin_und_Belegungen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomB.Id);
        Assert.Equal((5, 10, 0), (appointment.PreparationMinutes, appointment.FollowUpMinutes, appointment.ExtraFollowUpMinutes));

        var changed = await spa.PutAsync<AppointmentDto>(TimesPath(appointment), new AppointmentTimesRequest(15, 20), Ct);

        Assert.Equal((15, 20, 0), (changed.PreparationMinutes, changed.FollowUpMinutes, changed.ExtraFollowUpMinutes));
        Assert.Equal((TestSpa.At(10), TestSpa.At(10, 50)), (changed.Start, changed.End));
        Assert.Equal((TestSpa.At(9, 45), TestSpa.At(11, 10)), (changed.BlockedFrom, changed.BlockedUntil));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var bookings = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().ResourceBookings.AsNoTracking()
            .Where(b => b.AppointmentId == appointment.Id).ToListAsync(Ct);
        var person = Assert.Single(bookings, b => b.Kind == ResourceKind.Employee);
        var room = Assert.Single(bookings, b => b.Kind == ResourceKind.Room);
        Assert.Equal((TestSpa.At(9, 45), TestSpa.At(11, 10), spa.Anna.Id), (person.From, person.Until, person.ResourceId));
        Assert.Equal((TestSpa.At(9, 45), TestSpa.At(11, 10), spa.RoomB.Id), (room.From, room.Until, room.ResourceId));
    }

    [Fact]
    public async Task Laengere_Vorbereitung_die_in_den_Termin_davor_ragt_wird_mit_Begruendung_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomB.Id);
        var second = await spa.BookOkAsync(TestSpa.At(11, 5), Ct, spa.Anna.Id, spa.RoomB.Id);

        var response = await spa.Client.PutAsJsonAsync(TimesPath(second), new AppointmentTimesRequest(15, 10), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var rejected = (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
        Assert.NotEmpty(rejected.Message);
        Assert.Contains(rejected.Conflicts, c => c.Kind == ResourceKind.Employee && c.ResourceId == spa.Anna.Id);

        // Nichts hat sich geändert.
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        var unchanged = Assert.Single(day.Appointments, a => a.Id == second.Id);
        Assert.Equal((5, 10, TestSpa.At(11)), (unchanged.PreparationMinutes, unchanged.FollowUpMinutes, unchanged.BlockedFrom));
    }

    [Fact]
    public async Task Grenzen_von_0_bis_120_gelten_und_nur_gebuchte_oder_eingecheckte_Termine_lassen_sich_aendern()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var zero = await spa.PutAsync<AppointmentDto>(TimesPath(appointment), new AppointmentTimesRequest(0, 120), Ct);
        Assert.Equal((0, 120, TestSpa.At(10), TestSpa.At(12, 50)), (zero.PreparationMinutes, zero.FollowUpMinutes, zero.BlockedFrom, zero.BlockedUntil));

        foreach (var invalid in new[] { new AppointmentTimesRequest(121, 10), new AppointmentTimesRequest(5, 121), new AppointmentTimesRequest(-1, 10) })
        {
            var response = await spa.Client.PutAsJsonAsync(TimesPath(appointment), invalid, TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var checkedIn = await spa.PutAsync<AppointmentDto>(TimesPath(appointment), new AppointmentTimesRequest(5, 10), Ct);
        Assert.Equal((5, 10), (checkedIn.PreparationMinutes, checkedIn.FollowUpMinutes));
    }

    [Fact]
    public async Task Stornierter_Termin_laesst_sich_nicht_aendern()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest(null), Ct);

        var response = await spa.Client.PutAsJsonAsync(TimesPath(appointment), new AppointmentTimesRequest(10, 10), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Verschieben_behaelt_die_geaenderten_Minuten_statt_die_der_Behandlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.PutAsync<AppointmentDto>(TimesPath(appointment), new AppointmentTimesRequest(15, 20), Ct);

        var moved = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(13), spa.Anna.Id, null, null), Ct);

        Assert.Equal((15, 20), (moved.PreparationMinutes, moved.FollowUpMinutes));
        Assert.Equal((TestSpa.At(12, 45), TestSpa.At(14, 10)), (moved.BlockedFrom, moved.BlockedUntil));
    }

    [Fact]
    public async Task Gast_Puffer_steht_getrennt_und_die_Nachbereitung_bleibt_die_der_Behandlung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<List<CustomerFlagDto>>($"/api/customers/{spa.Customer.Id}/flags", new CustomerFlagInput(CustomerFlagKind.MoreTime), Ct);

        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        Assert.Equal((5, 10, 10), (appointment.PreparationMinutes, appointment.FollowUpMinutes, appointment.ExtraFollowUpMinutes));
        Assert.Equal(TestSpa.At(11, 10), appointment.BlockedUntil);

        // Beim Ändern bleibt der Puffer, wie er ist.
        var changed = await spa.PutAsync<AppointmentDto>(TimesPath(appointment), new AppointmentTimesRequest(5, 15), Ct);
        Assert.Equal((15, 10, TestSpa.At(11, 15)), (changed.FollowUpMinutes, changed.ExtraFollowUpMinutes, changed.BlockedUntil));
    }
}
