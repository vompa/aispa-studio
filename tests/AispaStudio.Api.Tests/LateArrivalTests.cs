using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Der Gast kommt später: die Verspätung wird am Termin vermerkt, der Termin bleibt an seiner Zeit (Runde 6 Ä71).</summary>
public sealed class LateArrivalTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Verspaetung_setzt_nur_die_Markierung_und_der_Termin_bleibt_stehen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var path = $"/api/appointments/{appointment.Id}/late";

        var late = await spa.PutAsync<AppointmentDto>(path, new ReportLateRequest(15), Ct);
        Assert.Equal(15, late.LateMinutes);
        Assert.Equal((appointment.Start, appointment.End, appointment.EmployeeId, appointment.RoomId, appointment.Price),
            (late.Start, late.End, late.EmployeeId, late.RoomId, late.Price));

        // Auch wenn der Beginn schon verstrichen wäre oder der nächste Gast gleich danach kommt: keine Verschiebung, kein Kürzen.
        await spa.BookOkAsync(TestSpa.At(11, 10), Ct, spa.Anna.Id);
        var longer = await spa.PutAsync<AppointmentDto>(path, new ReportLateRequest(30), Ct);
        Assert.Equal((30, appointment.Start, appointment.End), (longer.LateMinutes, longer.Start, longer.End));

        // Verschoben: die Meldung ist erledigt.
        var moved = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(13), spa.Anna.Id, appointment.RoomId, null), Ct);
        Assert.Null(moved.LateMinutes);
    }

    [Fact]
    public async Task Verspaetung_zuruecknehmen_und_ungueltige_Meldungen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var path = $"/api/appointments/{appointment.Id}/late";

        await spa.PutAsync<AppointmentDto>(path, new ReportLateRequest(10), Ct);
        var back = await spa.PutAsync<AppointmentDto>(path, new ReportLateRequest(0), Ct);
        Assert.Null(back.LateMinutes);

        var tooMuch = await spa.Client.PutAsJsonAsync(path, new ReportLateRequest(121), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        await spa.SetStatusAsync(appointment.Id, AppointmentStatus.CheckedIn, Ct);
        var checkedIn = await spa.Client.PutAsJsonAsync(path, new ReportLateRequest(5), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, checkedIn.StatusCode);
    }
}
