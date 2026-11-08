using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Dienstplan: einzelne Tage ersetzen die Wochenarbeitszeit, Kalender und Buchung folgen ihm.</summary>
public sealed class RosterTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateOnly Monday = TestSpa.Monday;
    private static readonly DateOnly Tuesday = TestSpa.Monday.AddDays(1);

    private static RosterEntry Work(Guid location, int from, int to) => new(RosterKind.Work, location, new(from, 0), new(to, 0));

    private static Task<RosterDayResult> PlanAsync(TestSpa spa, Guid employee, DateOnly date, params RosterEntry[] entries) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{employee}/{date:yyyy-MM-dd}", new RosterDayInput(entries, null), Ct);

    [Fact]
    public async Task Urlaub_nimmt_Person_aus_Kalender_und_Buchung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var result = await PlanAsync(spa, spa.Anna.Id, Monday, new RosterEntry(RosterKind.Vacation, null, null, null));
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Monday:yyyy-MM-dd}", Ct);
        var booking = await spa.BookAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var rejected = await booking.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);

        Assert.Equal(RosterSource.Plan, result.Day.Source);
        Assert.DoesNotContain(calendar.Shifts, s => s.EmployeeId == spa.Anna.Id);
        Assert.Contains(calendar.Shifts, s => s.EmployeeId == spa.Ben.Id);
        Assert.Equal(HttpStatusCode.Conflict, booking.StatusCode);
        Assert.Contains(rejected!.Conflicts, c => c.Reason.Contains("nicht im Dienst"));
    }

    [Fact]
    public async Task Zusatzdienst_macht_Buchung_moeglich()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        // Ohne Dienst am ganzen Tag trägt die Rezeption mit Hinweis ein (ADR 0073, Nachtrag R03); mit dem Zusatzdienst entfällt der Hinweis.
        var before = await spa.BookAsync(TestSpa.At(16, date: Tuesday), Ct, employee: spa.Ben.Id);
        await PlanAsync(spa, spa.Ben.Id, Tuesday, Work(spa.Location.Id, 10, 14));
        var after = await spa.BookAsync(TestSpa.At(11, date: Tuesday), Ct, employee: spa.Ben.Id);
        var calendar = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Tuesday:yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.Created, before.StatusCode);
        Assert.Equal(["Der Standort hat an diesem Tag geschlossen."], (await TestSpa.ReadAsync<AppointmentDto>(before, Ct)).Warnings);
        Assert.Equal(HttpStatusCode.Created, after.StatusCode);
        Assert.Null((await TestSpa.ReadAsync<AppointmentDto>(after, Ct)).Warnings);
        Assert.Equal([spa.Ben.Id], calendar.Shifts.Select(s => s.EmployeeId));
        Assert.Equal(TestSpa.At(10, date: Tuesday), calendar.Shifts[0].Start);
    }

    [Fact]
    public async Task Krankenstand_meldet_betroffene_Termine_und_Zuruecksetzen_stellt_Wochenmuster_her()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var sick = await PlanAsync(spa, spa.Anna.Id, Monday, new RosterEntry(RosterKind.Sick, null, null, null));
        var reset = await TestSpa.ReadAsync<RosterDayResult>(
            await spa.Client.DeleteAsync($"/api/locations/{spa.Location.Id}/roster/{spa.Anna.Id}/{Monday:yyyy-MM-dd}", Ct), Ct);

        Assert.Equal([appointment.Id], sick.AppointmentsOutsideShift.Select(a => a.Id));
        Assert.Equal(RosterSource.Pattern, reset.Day.Source);
        Assert.Empty(reset.AppointmentsOutsideShift);
    }

    [Fact]
    public async Task Given_Urlaub_als_Entwurf_mit_drei_gebuchten_Terminen_When_gespeichert_Then_meldet_die_API_die_drei_Termine()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = new[]
        {
            await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id),
            await spa.BookOkAsync(TestSpa.At(12), Ct, employee: spa.Anna.Id),
            await spa.BookOkAsync(TestSpa.At(14), Ct, employee: spa.Anna.Id),
        };

        var draft = await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Anna.Id}/{Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null, Draft: true), Ct);

        Assert.True(draft.Day.Draft);
        Assert.Equal(3, draft.AppointmentsOutsideShift.Count);
        Assert.Equal(booked.Select(a => a.Id), draft.AppointmentsOutsideShift.Select(a => a.Id));
    }

    [Fact]
    public async Task Wochenplan_zeigt_Hinweise_zu_Arbeits_und_Ruhezeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, spa.Anna.Id, Monday, Work(spa.Location.Id, 9, 20));
        await PlanAsync(spa, spa.Anna.Id, Tuesday, Work(spa.Location.Id, 6, 12));

        var week = await spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}", Ct);
        var anna = week.Employees.Single(e => e.EmployeeId == spa.Anna.Id);
        var ben = week.Employees.Single(e => e.EmployeeId == spa.Ben.Id);

        Assert.Equal(7, week.Dates.Count);
        Assert.Contains(anna.Days[0].Warnings, w => w.Contains("Mehr als 10 Stunden"));
        Assert.Contains(anna.Days[1].Warnings, w => w.Contains("Ruhezeit"));
        Assert.Equal(17, anna.PlannedHours);
        Assert.Equal(RosterSource.Pattern, ben.Days[0].Source);
        Assert.Equal(8, ben.PlannedHours);
        Assert.Empty(ben.Warnings);
    }

    [Fact]
    public async Task Sieben_Diensttage_am_Stueck_verletzen_die_Wochenruhe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        for (var i = 0; i < 7; i++)
        {
            await PlanAsync(spa, spa.Ben.Id, Monday.AddDays(i), Work(spa.Location.Id, 9, 15));
        }

        var week = await spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}", Ct);

        Assert.Contains(week.Employees.Single(e => e.EmployeeId == spa.Ben.Id).Warnings, w => w.Contains("Wochenruhe"));
    }

    [Fact]
    public async Task Rezeption_sieht_den_Plan_aendert_ihn_aber_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var read = await reception.GetAsync($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}", Ct);
        var write = await reception.PutAsJsonAsync($"/api/locations/{spa.Location.Id}/roster/{spa.Anna.Id}/{Monday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.DayOff, null, null, null)], null), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Standortleitung_plant_nur_ihren_Standort_und_laesst_fremde_Dienste_stehen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var salzburg = await spa.CreateLocationAsync("Salzburg", [DayOfWeek.Monday], Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [salzburg.Id]);
        await PlanAsync(spa, spa.Anna.Id, Monday, Work(spa.Location.Id, 9, 12));

        // Die Standortleitung Salzburg teilt Anna am Nachmittag ein; den Vormittag in Wien sieht sie, ändern kann sie ihn nicht.
        var response = await manager.PutAsJsonAsync($"/api/locations/{salzburg.Id}/roster/{spa.Anna.Id}/{Monday:yyyy-MM-dd}",
            new RosterDayInput([Work(salzburg.Id, 14, 18)], null), TestSpa.Json, Ct);
        var result = await TestSpa.ReadAsync<RosterDayResult>(response, Ct);

        Assert.Equal(2, result.Day.Entries.Count);
        Assert.Contains(result.Day.Entries, e => e.LocationId == spa.Location.Id && e.Start == new TimeOnly(9, 0));
        Assert.Contains(result.Day.Entries, e => e.LocationId == salzburg.Id && e.Start == new TimeOnly(14, 0));
    }
}
