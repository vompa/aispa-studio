using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Runde 5, Ä33: die Notiz zu einem Termin lässt sich nachträglich setzen, ändern und löschen; sie ist nicht die Kundennotiz.</summary>
public sealed class AppointmentNoteTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NotePath(AppointmentDto appointment) => $"/api/appointments/{appointment.Id}/note";

    [Fact]
    public async Task Setzen_aendern_und_loeschen_zeigt_sich_im_Termin_und_im_Kalender_ohne_die_Kundennotiz_zu_beruehren()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        Assert.Null(appointment.Note);

        var set = await spa.PutAsync<AppointmentDto>(NotePath(appointment), new AppointmentNoteRequest("  Lavendelöl, wenig Gespräch  "), Ct);
        Assert.Equal("Lavendelöl, wenig Gespräch", set.Note);
        Assert.True(set.UpdatedAt >= appointment.UpdatedAt);
        Assert.Equal((appointment.Start, appointment.End, appointment.Status), (set.Start, set.End, set.Status));

        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Equal("Lavendelöl, wenig Gespräch", Assert.Single(day.Appointments).Note);
        Assert.Equal(spa.Customer.Note, (await spa.GetAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", Ct)).Note);

        var changed = await spa.PutAsync<AppointmentDto>(NotePath(appointment), new AppointmentNoteRequest("Fokus Nacken"), Ct);
        Assert.Equal("Fokus Nacken", changed.Note);

        // Leer und nur Leerzeichen löschen die Notiz.
        var cleared = await spa.PutAsync<AppointmentDto>(NotePath(appointment), new AppointmentNoteRequest("   "), Ct);
        Assert.Null(cleared.Note);
        Assert.Null((await spa.GetAsync<AppointmentDto>($"/api/appointments/{appointment.Id}", Ct)).Note);
    }

    [Fact]
    public async Task Hoechstens_500_Zeichen_und_ein_unbekannter_Termin_ist_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var full = await spa.PutAsync<AppointmentDto>(NotePath(appointment), new AppointmentNoteRequest(new string('a', 500)), Ct);
        Assert.Equal(500, full.Note!.Length);

        var tooLong = await spa.Client.PutAsJsonAsync(NotePath(appointment), new AppointmentNoteRequest(new string('a', 501)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(500, (await spa.GetAsync<AppointmentDto>($"/api/appointments/{appointment.Id}", Ct)).Note!.Length);

        var unknown = await spa.Client.PutAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/note", new AppointmentNoteRequest("x"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Auch_erledigte_und_stornierte_Termine_lassen_sich_mit_einer_Notiz_ergaenzen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var done = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{done.Id}/status", new AppointmentStatusChange(AppointmentStatus.Completed), Ct);
        var cancelled = await spa.BookOkAsync(TestSpa.At(13), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest(null), Ct);

        var afterDone = await spa.PutAsync<AppointmentDto>(NotePath(done), new AppointmentNoteRequest("Trank Tee, kommt wieder"), Ct);
        var afterCancel = await spa.PutAsync<AppointmentDto>(NotePath(cancelled), new AppointmentNoteRequest("Krank gemeldet"), Ct);

        Assert.Equal((AppointmentStatus.Completed, "Trank Tee, kommt wieder"), (afterDone.Status, afterDone.Note));
        Assert.Equal((AppointmentStatus.Cancelled, "Krank gemeldet"), (afterCancel.Status, afterCancel.Note));
    }

    [Fact]
    public async Task Nur_wer_buchen_darf_aendert_die_Notiz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        var allowed = await reception.PutAsJsonAsync(NotePath(appointment), new AppointmentNoteRequest("Am Empfang notiert"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var denied = await therapist.PutAsJsonAsync(NotePath(appointment), new AppointmentNoteRequest("Nicht erlaubt"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("Am Empfang notiert", (await spa.GetAsync<AppointmentDto>($"/api/appointments/{appointment.Id}", Ct)).Note);
    }
}
