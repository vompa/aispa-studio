using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Tagesregel am Status-Endpunkt (L1): Einchecken nur am Tag des Termins, Erledigt und Nicht erschienen erst ab dem Tag des Termins; die Kassa nimmt nur
/// eingecheckte oder erledigte Termine auf die Rechnung. Wie <see cref="PastBookingTests"/> rechnet die Klasse mit der echten Uhr relativ zu heute.
/// </summary>
public sealed class StatusDayTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    private static DateTimeOffset LocalToday(int hour, int minute, int addDays = 0) => TestSpa.At(hour, minute, Today.AddDays(addDays));

    private static Task<HttpResponseMessage> SetStatusAsync(TestSpa spa, Guid appointmentId, AppointmentStatus status) =>
        spa.Client.PostAsJsonAsync($"/api/appointments/{appointmentId}/status", new AppointmentStatusChange(status), TestSpa.Json, Ct);

    private static async Task AssertInvalidAsync(HttpResponseMessage response, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
        Assert.Contains(message, problem.Errors["status"]);
    }

    [Fact]
    public async Task Einchecken_geht_morgen_nicht_und_heute_schon()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var tomorrow = await spa.BookOkAsync(LocalToday(10, 0, 1), Ct, spa.Anna.Id, spa.RoomA.Id);
        var today = await spa.BookOkAsync(LocalToday(12, 0), Ct, spa.Anna.Id, spa.RoomA.Id);

        await AssertInvalidAsync(await SetStatusAsync(spa, tomorrow.Id, AppointmentStatus.CheckedIn), StatusDays.CheckInMessage);
        var checkedIn = await TestSpa.ReadAsync<AppointmentDto>(await SetStatusAsync(spa, today.Id, AppointmentStatus.CheckedIn), Ct);

        Assert.Equal(AppointmentStatus.CheckedIn, checkedIn.Status);
    }

    [Fact]
    public async Task Erledigt_und_Nicht_erschienen_gehen_morgen_nicht_gestern_schon()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var tomorrow = await spa.BookOkAsync(LocalToday(10, 0, 1), Ct, spa.Anna.Id, spa.RoomA.Id);
        var yesterday = await spa.BookInThePastAsync(LocalToday(10, 0, -1), Ct, spa.Anna.Id, spa.RoomA.Id);

        await AssertInvalidAsync(await SetStatusAsync(spa, tomorrow.Id, AppointmentStatus.Completed), StatusDays.CompleteMessage);
        await AssertInvalidAsync(await SetStatusAsync(spa, tomorrow.Id, AppointmentStatus.NoShow), StatusDays.CompleteMessage);
        var noShow = await TestSpa.ReadAsync<AppointmentDto>(await SetStatusAsync(spa, yesterday.Id, AppointmentStatus.NoShow), Ct);

        Assert.Equal(AppointmentStatus.NoShow, noShow.Status);
    }

    [Fact]
    public async Task Nur_eingecheckte_oder_erledigte_Termine_kommen_auf_die_Rechnung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var register = await spa.PostAsync<CashRegisterDto>("/api/pos/registers", new CashRegisterInput(spa.Location.Id, $"T-{Guid.NewGuid():N}"[..14],
            FiscalCountry.AT, "Test-Spa GmbH\nTeststraße 1, 1010 Wien\nATU12345678", "Test-Spa GmbH", "AT61 1904 3002 3457 3201", null), Ct);
        var appointment = await spa.BookOkAsync(LocalToday(12, 0), Ct, spa.Anna.Id, spa.RoomA.Id);

        var booked = await spa.Client.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), TestSpa.Json, Ct);
        await TestSpa.ReadAsync<AppointmentDto>(await SetStatusAsync(spa, appointment.Id, AppointmentStatus.CheckedIn), Ct);
        var checkedIn = await spa.Client.PostAsJsonAsync("/api/pos/bills", new BillInput(register.Id, null, [appointment.Id]), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, booked.StatusCode);
        Assert.Contains("Der Gast ist noch nicht eingecheckt.", await booked.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, checkedIn.StatusCode);
    }
}
