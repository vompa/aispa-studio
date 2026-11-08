using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Handy-App für Mitarbeitende (Slice 106): Termin begonnen und beendet melden, Allergie-Kennzeichen ohne Inhalt.</summary>
public sealed class StaffMobileTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Die Regel prüft die echte Uhr: Der Termin liegt danach am heutigen Tag des Standorts.</summary>
    private static Task MoveToTodayAsync(TestSpa spa, Guid appointmentId) => MoveAsync(spa, appointmentId, DateTimeOffset.UtcNow);

    private static async Task MoveAsync(TestSpa spa, Guid appointmentId, DateTimeOffset start)
    {
        await using var scope = spa.Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        await db.Appointments.Where(a => a.Id == appointmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Start, start).SetProperty(a => a.End, start.AddMinutes(50)), Ct);
    }

    private static async Task<(TestSpa Spa, AppointmentDto Appointment, HttpClient App)> LenaWithAppointmentTodayAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, lena.Id);
        await MoveToTodayAsync(spa, appointment.Id);
        return (spa, appointment, await ChannelTests.StaffAppAsync(spa, StaffRole.Therapist, lena.Id, Ct));
    }

    [Fact]
    public async Task Gegeben_Lena_meldet_begonnen_Wenn_die_Rezeption_den_Kalender_ansieht_Dann_steht_der_Termin_als_laufend()
    {
        var (spa, appointment, app) = await LenaWithAppointmentTodayAsync(fixture);
        using var client = app;

        var begun = await app.PostAsync($"/api/appointments/{appointment.Id}/begin", null, Ct);

        Assert.Equal(HttpStatusCode.OK, begun.StatusCode);
        var vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, vienna).DateTime);
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={today:yyyy-MM-dd}", Ct);
        var shown = Assert.Single(calendar.Appointments, a => a.Id == appointment.Id);
        Assert.Equal(AppointmentStatus.CheckedIn, shown.Status);
        Assert.NotNull(shown.StartedAt);
    }

    [Fact]
    public async Task Begonnen_geht_nur_am_Tag_des_Termins()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        var future = await spa.BookOkAsync(TestSpa.At(10), Ct, lena.Id);
        using var app = await ChannelTests.StaffAppAsync(spa, StaffRole.Therapist, lena.Id, Ct);

        var begun = await app.PostAsync($"/api/appointments/{future.Id}/begin", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, begun.StatusCode);
    }

    [Fact]
    public async Task Beendet_geht_nur_nach_begonnen_und_schliesst_den_Termin_ab()
    {
        var (spa, appointment, app) = await LenaWithAppointmentTodayAsync(fixture);
        using var client = app;

        var early = await app.PostAsync($"/api/appointments/{appointment.Id}/finish", null, Ct);
        (await app.PostAsync($"/api/appointments/{appointment.Id}/begin", null, Ct)).EnsureSuccessStatusCode();
        var finished = await TestSpa.ReadAsync<AppointmentDto>(await app.PostAsync($"/api/appointments/{appointment.Id}/finish", null, Ct), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Equal(AppointmentStatus.Completed, finished.Status);
        Assert.Equal(AppointmentStatus.Completed, (await spa.GetAsync<AppointmentDto>($"/api/appointments/{appointment.Id}", Ct)).Status);
    }

    [Fact]
    public async Task Fremder_Termin_antwortet_403()
    {
        var (spa, _, app) = await LenaWithAppointmentTodayAsync(fixture);
        using var client = app;
        var others = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);

        var begun = await app.PostAsync($"/api/appointments/{others.Id}/begin", null, Ct);
        var finished = await app.PostAsync($"/api/appointments/{others.Id}/finish", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, begun.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, finished.StatusCode);
    }

    [Fact]
    public async Task Einchecken_zuruecknehmen_leert_den_Beginn()
    {
        var (spa, appointment, app) = await LenaWithAppointmentTodayAsync(fixture);
        using var client = app;
        (await app.PostAsync($"/api/appointments/{appointment.Id}/begin", null, Ct)).EnsureSuccessStatusCode();

        var undone = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/status", new AppointmentStatusChange(AppointmentStatus.Booked), Ct);

        Assert.Equal(AppointmentStatus.Booked, undone.Status);
        Assert.Null(undone.StartedAt);
    }

    [Fact]
    public async Task Stornieren_und_wiederherstellen_leert_den_Beginn_und_Lena_kann_neu_beginnen()
    {
        var (spa, appointment, app) = await LenaWithAppointmentTodayAsync(fixture);
        using var client = app;
        (await app.PostAsync($"/api/appointments/{appointment.Id}/begin", null, Ct)).EnsureSuccessStatusCode();

        await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/cancel", new CancelAppointmentRequest(null), Ct);
        await MoveAsync(spa, appointment.Id, appointment.Start); // Wiederherstellen prüft die Öffnungszeiten, die echte Uhr liegt oft außerhalb.
        var restored = await spa.PostAsync<AppointmentDto>($"/api/appointments/{appointment.Id}/restore", new RestoreAppointmentRequest(), Ct);
        await MoveToTodayAsync(spa, appointment.Id);
        var begun = await TestSpa.ReadAsync<AppointmentDto>(await app.PostAsync($"/api/appointments/{appointment.Id}/begin", null, Ct), Ct);

        Assert.Null(restored.StartedAt);
        Assert.Equal(AppointmentStatus.CheckedIn, begun.Status);
        Assert.NotNull(begun.StartedAt);
    }

    [Fact]
    public async Task Eigene_Karte_nennt_das_Allergie_Kennzeichen_ohne_Inhalt_die_Karte_fuer_die_Rezeption_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, lena.Id);
        var link = await spa.PostAsync<AnamnesisTabletLink>($"/api/customers/{spa.Customer.Id}/anamnesis/tablet", new AnamnesisLinkRequest(null, "de"), Ct);
        using (var tablet = spa.Fixture.CreateClient())
        {
            var token = link.Url.Split("#/anamnese/")[1].Split('?')[0];
            var answers = new List<AnamnesisAnswer>
            {
                new("allergies", true, "Lavendelöl"),
                new("skin", false, null),
                new("circulation", false, null),
                new("pregnancy", false, null),
                new("medication", false, null),
                new("surgery", false, null),
                new("other", null, null),
            };
            var submitted = await tablet.PostAsJsonAsync($"/api/online/anamnesis/{token}", new AnamnesisSubmission(answers, true, "Eva Muster", "de"), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
        }

        var path = $"/api/reports/locations/{spa.Location.Id}/day-plan?date={TestSpa.Monday:yyyy-MM-dd}";
        using var app = await ChannelTests.StaffAppAsync(spa, StaffRole.Therapist, lena.Id, Ct);
        var own = await app.GetAsync(path, Ct);
        var text = await own.Content.ReadAsStringAsync(Ct);
        var reception = await spa.GetAsync<DayPlanDto>($"{path}&employeeId={lena.Id}", Ct);

        Assert.True((await TestSpa.ReadAsync<DayPlanDto>(own, Ct)).Cards[0].AllergyFlag);
        Assert.DoesNotContain("Lavendel", text, StringComparison.Ordinal);
        Assert.False(reception.Cards[0].AllergyFlag);
    }
}
