using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Buchen außerhalb der Öffnungszeit mit „Trotzdem buchen“ (Slice 85, Ä151; ADR 0073, Nachtrag Runde R04): Die Rezeption bucht nach dem Hinweis ausdrücklich
/// außerhalb der Öffnungszeit und des Dienstes (<see cref="AppointmentRequest.OutsideHoursConfirmed"/>). Ohne Bestätigung bleibt die Ablehnung; Gäste, die KI und die
/// Suche nach freien Zeiten bekommen solche Zeiten nie. Testmontag: Öffnungszeit 9 bis 20 Uhr, Anna und Ben im Dienst 9 bis 17 Uhr.
/// </summary>
public sealed class OutsideHoursBookingTests(ApiFixture fixture)
{
    private const string Closed = "Der Standort hat zu dieser Zeit geschlossen.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>90 Minuten: um 18:45 endet sie um 20:15, nach der Öffnungszeit.</summary>
    private static async Task<TreatmentDto> RitualAsync(TestSpa spa) =>
        await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Ritual", 90, 5, 10, 129m, [spa.Massage.Id], [spa.RoomA.Id, spa.RoomB.Id], []), Ct);

    private static AppointmentRequest Request(TestSpa spa, TreatmentDto treatment, DateTimeOffset start, Guid employee, bool confirmed) =>
        new(spa.Location.Id, treatment.Id, spa.Customer.Id, start, employee, null, null, null, OutsideHoursConfirmed: confirmed);

    private static async Task<BookingRejected> RejectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!;
    }

    [Fact]
    public async Task Given_Dienst_bis_17_und_18_45_When_ohne_Bestaetigung_gebucht_wird_Then_lehnt_der_Server_mit_Geschlossen_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ritual = await RitualAsync(spa);

        var rejected = await RejectedAsync(await spa.Client.PostAsJsonAsync("/api/appointments",
            Request(spa, ritual, TestSpa.At(18, 45), spa.Anna.Id, confirmed: false), TestSpa.Json, Ct));

        Assert.Equal(Closed, rejected.Message);
        Assert.True(rejected.OutsideHours);
    }

    [Fact]
    public async Task Given_derselbe_Termin_When_Trotzdem_buchen_bestaetigt_ist_Then_ist_der_Termin_gebucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ritual = await RitualAsync(spa);

        var response = await spa.Client.PostAsJsonAsync("/api/appointments",
            Request(spa, ritual, TestSpa.At(18, 45), spa.Anna.Id, confirmed: true), TestSpa.Json, Ct);

        var booked = await TestSpa.ReadAsync<AppointmentDto>(response, Ct);
        Assert.Equal((TestSpa.At(18, 45), TestSpa.At(20, 15), spa.Anna.Id), (booked.Start, booked.End, booked.EmployeeId));
        Assert.Equal(["Außerhalb der Öffnungszeit oder des Dienstes gebucht."], booked.Warnings);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Contains(day.Appointments, a => a.Id == booked.Id && a.Status == AppointmentStatus.Booked);
    }

    [Fact]
    public async Task Given_offen_aber_nach_Dienstende_When_ohne_Bestaetigung_gebucht_wird_Then_traegt_die_Ablehnung_OutsideHours_und_mit_Bestaetigung_wird_gebucht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        // 17:30 bis 18:20 liegt in der Öffnungszeit, aber nach Annas Dienstende.
        var rejected = await RejectedAsync(await spa.Client.PostAsJsonAsync("/api/appointments",
            Request(spa, spa.MassageTreatment, TestSpa.At(17, 30), spa.Anna.Id, confirmed: false), TestSpa.Json, Ct));
        var booked = await TestSpa.ReadAsync<AppointmentDto>(await spa.Client.PostAsJsonAsync("/api/appointments",
            Request(spa, spa.MassageTreatment, TestSpa.At(17, 30), spa.Anna.Id, confirmed: true), TestSpa.Json, Ct), Ct);

        Assert.True(rejected.OutsideHours);
        Assert.Contains(rejected.Conflicts, c => c.ResourceId == spa.Anna.Id && c.Reason == "ist zu dieser Zeit nicht im Dienst");
        Assert.Equal((TestSpa.At(17, 30), spa.Anna.Id), (booked.Start, booked.EmployeeId));
    }

    [Fact]
    public async Task Mit_Bestaetigung_gilt_die_Doppelbelegung_weiter()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ritual = await RitualAsync(spa);
        await TestSpa.ReadAsync<AppointmentDto>(await spa.Client.PostAsJsonAsync("/api/appointments",
            Request(spa, ritual, TestSpa.At(18, 45), spa.Anna.Id, confirmed: true), TestSpa.Json, Ct), Ct);

        var again = await RejectedAsync(await spa.Client.PostAsJsonAsync("/api/appointments",
            Request(spa, ritual, TestSpa.At(19, 0), spa.Anna.Id, confirmed: true), TestSpa.Json, Ct));

        Assert.False(again.OutsideHours);
        Assert.Contains(again.Conflicts, c => c.ResourceId == spa.Anna.Id && c.Reason == "ist zu dieser Zeit schon belegt");
    }

    [Fact]
    public async Task Die_Rezeption_bucht_mit_Bestaetigung_auch_mit_der_Rolle_Rezeption()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ritual = await RitualAsync(spa);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var response = await reception.PostAsJsonAsync("/api/appointments",
            Request(spa, ritual, TestSpa.At(18, 45), spa.Anna.Id, confirmed: true), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Given_ein_Benutzer_ohne_Buchungsrecht_When_mit_Bestaetigung_gebucht_wird_Then_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ritual = await RitualAsync(spa);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var response = await anna.PostAsJsonAsync("/api/appointments",
            Request(spa, ritual, TestSpa.At(18, 45), spa.Anna.Id, confirmed: true), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(AppointmentSource.Online)]
    [InlineData(AppointmentSource.Email)]
    public async Task Given_eine_Buchung_von_Gast_oder_KI_mit_Bestaetigung_When_ausserhalb_gebucht_wird_Then_bleibt_sie_abgelehnt(AppointmentSource source)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ritual = await RitualAsync(spa);
        await using var scope = spa.Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);

        var outcome = await scope.ServiceProvider.GetRequiredService<BookingService>().BookAsync(
            Request(spa, ritual, TestSpa.At(18, 45), spa.Anna.Id, confirmed: true), Ct, source: source);

        var rejected = Assert.IsType<BookingOutcome.Rejected>(outcome);
        Assert.Equal(Closed, rejected.Reason.Message);
        Assert.False(rejected.Reason.OutsideHours);
    }

    [Fact]
    public async Task Given_die_Online_Buchung_When_Zeiten_gesucht_werden_Then_ist_keine_Zeit_ausserhalb_der_Oeffnungszeit_dabei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ritual = await RitualAsync(spa);
        // Ein Termin außerhalb der Öffnungszeit macht den Abend nicht buchbar.
        await TestSpa.ReadAsync<AppointmentDto>(await spa.Client.PostAsJsonAsync("/api/appointments",
            Request(spa, ritual, TestSpa.At(18, 45), spa.Anna.Id, confirmed: true), TestSpa.Json, Ct), Ct);

        var json = await spa.OnlineClient().GetStringAsync(
            $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={ritual.Id}&from={TestSpa.Monday:yyyy-MM-dd}&days=1", Ct);
        var times = JsonSerializer.Deserialize<OnlineAvailability>(json, TestSpa.Json)!.Days[0].Times;

        Assert.NotEmpty(times);
        Assert.All(times, t => Assert.InRange(t, TestSpa.At(9), TestSpa.At(20).AddMinutes(-90)));
        // Dienst bis 17 Uhr mit 10 Minuten Nachbereitung: letzter Beginn 15:20.
        Assert.All(times, t => Assert.True(t.AddMinutes(100) <= TestSpa.At(17), $"{t:HH:mm} liegt nach dem Dienst"));
    }

    [Fact]
    public async Task Given_Freie_Zeit_suchen_When_Zeiten_gesucht_werden_Then_ist_keine_Zeit_ausserhalb_der_Oeffnungszeit_dabei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var ritual = await RitualAsync(spa);

        var slots = await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={ritual.Id}&date={TestSpa.Monday:yyyy-MM-dd}", Ct);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.True(TestSpa.At(9) <= s.Start && s.End <= TestSpa.At(20), $"{s.Start:HH:mm}–{s.End:HH:mm} liegt außerhalb der Öffnungszeit"));
        Assert.DoesNotContain(slots, s => s.Start == TestSpa.At(18, 45));
    }
}
