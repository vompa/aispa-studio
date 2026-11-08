using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Dienstplan und Zeiten für die Personenkarte der Windows-App (R05 Slice 99): der Parameter <c>employeeId</c> liefert genau eine Person, auch wenn sie
/// am Standort noch nicht eingeteilt ist; dazu die Abnahme „Dienst verschieben, Kalender folgt“.
/// </summary>
public sealed class RosterTeamTests(ApiFixture fixture)
{
    private static readonly DateOnly Monday = TestSpa.Monday;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RosterEntry Shift(TestSpa spa, int from, int until) => new(RosterKind.Work, spa.Location.Id, new TimeOnly(from, 0), new TimeOnly(until, 0));

    private static Task<RosterDayResult> PlanAsync(TestSpa spa, RosterEntry entry, bool draft) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Anna.Id}/{Monday:yyyy-MM-dd}", new RosterDayInput([entry], null, draft), Ct);

    private static Task<CalendarDay> CalendarAsync(TestSpa spa) =>
        spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Monday:yyyy-MM-dd}", Ct);

    private static async Task<DateTimeOffset?> AnnaStartAsync(TestSpa spa) =>
        (await CalendarAsync(spa)).Shifts.SingleOrDefault(s => s.EmployeeId == spa.Anna.Id)?.Start;

    [Fact]
    public async Task Given_Lena_Dienstag_9_bis_17_When_die_Managerin_auf_12_bis_20_verschiebt_Then_bietet_der_Kalender_sie_erst_ab_12_an()
    {
        // Anna steht für Lena: Dienst 9–17 aus der Wochenarbeitszeit (am Testmontag).
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var before = await AnnaStartAsync(spa);

        var result = await PlanAsync(spa, Shift(spa, 12, 20), draft: false);
        var after = await AnnaStartAsync(spa);
        var tooEarly = await spa.BookAsync(TestSpa.At(11), Ct, spa.Anna.Id);
        // 5 Minuten Vorbereitung der Behandlung liegen vor dem Termin und gehören zur Belegung.
        var fromTwelve = await spa.BookAsync(TestSpa.At(12, 5), Ct, spa.Anna.Id);

        Assert.Equal(TestSpa.At(9), before);
        Assert.False(result.Day.Draft);
        Assert.Equal(TestSpa.At(12), after);
        Assert.False(tooEarly.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Created, fromTwelve.StatusCode);
    }

    [Fact]
    public async Task Given_Lena_verschoben_als_Entwurf_When_veroeffentlicht_Then_gilt_erst_danach_12_Uhr()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var result = await PlanAsync(spa, Shift(spa, 12, 20), draft: true);
        var duringDraft = await AnnaStartAsync(spa);
        _ = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/roster/publish", new RosterPublishRequest(Monday, Monday), TestSpa.Json, Ct);
        var published = await AnnaStartAsync(spa);

        Assert.True(result.Day.Draft);
        Assert.Equal(TestSpa.At(9), duringDraft);
        Assert.Equal(TestSpa.At(12), published);
    }

    [Fact]
    public async Task Given_eine_Person_ohne_Dienst_am_Standort_When_der_Plan_mit_employeeId_geladen_wird_Then_steht_nur_sie_darin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var second = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);

        var all = await spa.GetAsync<RosterWeek>($"/api/locations/{second.Id}/roster?from={Monday:yyyy-MM-dd}", Ct);
        var one = await spa.GetAsync<RosterWeek>($"/api/locations/{second.Id}/roster?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}", Ct);

        Assert.Empty(all.Employees);
        var row = Assert.Single(one.Employees);
        Assert.Equal(spa.Anna.Id, row.EmployeeId);
        Assert.Equal(7, row.Days.Count);
    }

    [Fact]
    public async Task Given_eine_Person_mit_Dienst_am_Standort_When_der_Plan_mit_employeeId_geladen_wird_Then_fehlen_die_anderen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var week = await spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}&employeeId={spa.Ben.Id}", Ct);

        Assert.Equal(spa.Ben.Id, Assert.Single(week.Employees).EmployeeId);
    }

    [Fact]
    public async Task Given_eine_Person_ohne_Abschnitte_When_die_Zeiten_mit_employeeId_geladen_werden_Then_steht_nur_sie_darin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var second = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);

        var all = await spa.GetAsync<Timesheet>($"/api/locations/{second.Id}/timesheet?from={Monday:yyyy-MM-dd}", Ct);
        var one = await spa.GetAsync<Timesheet>($"/api/locations/{second.Id}/timesheet?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}", Ct);

        Assert.Empty(all.Employees);
        var row = Assert.Single(one.Employees);
        Assert.Equal(spa.Anna.Id, row.EmployeeId);
        Assert.Empty(row.Days.SelectMany(d => d.Records));
    }

    private static RosterEntry ShiftAt(Guid location, int from, int until) => new(RosterKind.Work, location, new TimeOnly(from, 0), new TimeOnly(until, 0));

    private static RosterEntry Vacation() => new(RosterKind.Vacation, null, null, null);

    private static Task<RosterDayResult> PlanDayAsync(TestSpa spa, DateOnly date, string? note, params RosterEntry[] entries) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Anna.Id}/{date:yyyy-MM-dd}", new RosterDayInput(entries, note, false), Ct);

    private static async Task<T> ReadOkAsync<T>(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TestSpa.ReadAsync<T>(response, Ct);
    }

    [Fact]
    public async Task Given_Leitung_Graz_When_sie_den_Plan_einer_Person_aus_Wien_abfragt_Then_sieht_sie_weder_Dienste_noch_Abwesenheit_noch_Notiz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        await PlanDayAsync(spa, Monday, "Arzttermin", Vacation());
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [graz.Id]);

        var week = await ReadOkAsync<RosterWeek>(manager, $"/api/locations/{graz.Id}/roster?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}");

        var row = Assert.Single(week.Employees);
        Assert.Empty(row.Days.SelectMany(d => d.Entries));
        Assert.All(row.Days, d =>
        {
            Assert.Null(d.Note);
            Assert.Empty(d.Warnings);
            Assert.Equal(RosterSource.Pattern, d.Source);
        });
        Assert.Equal(0, row.PlannedHours);
    }

    [Fact]
    public async Task Given_Leitung_Graz_When_sie_eine_Aushilfe_abfragt_Then_sieht_sie_den_Grazer_Dienst_und_die_Abwesenheit_aber_nicht_den_Wiener_Dienst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        await PlanDayAsync(spa, Monday, "Urlaub lange geplant", Vacation());
        await PlanDayAsync(spa, Monday.AddDays(1), null, ShiftAt(graz.Id, 10, 14));
        await PlanDayAsync(spa, Monday.AddDays(2), "nur Wien", ShiftAt(spa.Location.Id, 9, 17));
        await PlanDayAsync(spa, Monday.AddDays(3), null, ShiftAt(spa.Location.Id, 8, 11), ShiftAt(graz.Id, 13, 17));
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [graz.Id]);

        var week = await ReadOkAsync<RosterWeek>(manager, $"/api/locations/{graz.Id}/roster?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}");

        var row = Assert.Single(week.Employees);
        Assert.Equal(RosterKind.Vacation, Assert.Single(row.Days[0].Entries).Kind);
        Assert.Equal("Urlaub lange geplant", row.Days[0].Note);
        Assert.Equal(graz.Id, Assert.Single(row.Days[1].Entries).LocationId);
        Assert.Empty(row.Days[2].Entries);
        Assert.Null(row.Days[2].Note);
        Assert.Equal(graz.Id, Assert.Single(row.Days[3].Entries).LocationId);
        // 4 Stunden Dienstag und 4 Stunden Donnerstag in Graz; die Wiener Stunden zählen hier nicht.
        Assert.Equal(8, row.PlannedHours);
    }

    [Fact]
    public async Task Given_Therapeutin_Graz_When_sie_den_Plan_ohne_employeeId_sieht_Then_fehlen_die_Wiener_Dienste_der_Aushilfe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        await PlanDayAsync(spa, Monday, null, ShiftAt(spa.Location.Id, 8, 11), ShiftAt(graz.Id, 13, 17));
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [graz.Id]);

        var week = await ReadOkAsync<RosterWeek>(therapist, $"/api/locations/{graz.Id}/roster?from={Monday:yyyy-MM-dd}");

        var anna = week.Employees.Single(e => e.EmployeeId == spa.Anna.Id);
        Assert.Equal(graz.Id, Assert.Single(anna.Days[0].Entries).LocationId);
        Assert.Equal(4, anna.PlannedHours);
    }

    [Fact]
    public async Task Given_Therapeutin_When_sie_den_Plan_mit_employeeId_abfragt_Then_wird_abgelehnt_und_ohne_employeeId_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id]);

        var one = await therapist.GetAsync($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}", Ct);
        var all = await therapist.GetAsync($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, one.StatusCode);
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
    }

    [Theory]
    [InlineData(StaffRole.Manager)]
    [InlineData(StaffRole.Backoffice)]
    public async Task Given_Standortleitung_oder_Backoffice_am_Standort_When_sie_den_Plan_mit_employeeId_abfragen_Then_sehen_sie_die_Person(StaffRole role)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await TestUsers.ClientAsync(fixture, Ct, role, [spa.Location.Id]);

        var week = await ReadOkAsync<RosterWeek>(client, $"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}");

        Assert.NotEmpty(Assert.Single(week.Employees).Days.SelectMany(d => d.Entries));
    }

    [Fact]
    public async Task Given_Leitung_Graz_When_sie_die_Zeiten_einer_Person_abfragt_Then_fehlen_die_Abschnitte_aus_Wien()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        var past = new DateOnly(2026, 6, 1);
        var utc = new DateTimeOffset(past.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        await spa.PostAsync<TimeRecordDto>($"/api/locations/{spa.Location.Id}/time-records",
            new TimeRecordInput(spa.Anna.Id, utc.AddHours(8), utc.AddHours(12), "Stempeln vergessen"), Ct);
        await spa.PostAsync<TimeRecordDto>($"/api/locations/{graz.Id}/time-records",
            new TimeRecordInput(spa.Anna.Id, utc.AddHours(13), utc.AddHours(15), "Stempeln vergessen"), Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [graz.Id]);

        var sheet = await ReadOkAsync<Timesheet>(manager, $"/api/locations/{graz.Id}/timesheet?from={past:yyyy-MM-dd}&employeeId={spa.Anna.Id}");
        var all = await ReadOkAsync<Timesheet>(manager, $"/api/locations/{graz.Id}/timesheet?from={past:yyyy-MM-dd}");

        foreach (var row in new[] { Assert.Single(sheet.Employees), all.Employees.Single(e => e.EmployeeId == spa.Anna.Id) })
        {
            var day = row.Days.Single(d => d.Date == past);
            Assert.Equal(graz.Id, Assert.Single(day.Records).LocationId);
            Assert.Equal(2, day.ActualHours);
            Assert.Equal(2, row.ActualHours);
            // Die Wochenarbeitszeit der Person liegt in Wien und ist hier kein Soll.
            Assert.Equal(0, row.PlannedHours);
        }
    }

    [Fact]
    public async Task Given_eine_ausgeschiedene_Person_When_die_Zeiten_mit_employeeId_geladen_werden_Then_steht_niemand_darin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var removed = await spa.Client.DeleteAsync($"/api/employees/{spa.Anna.Id}", Ct);
        Assert.True(removed.IsSuccessStatusCode);

        var sheet = await spa.GetAsync<Timesheet>($"/api/locations/{spa.Location.Id}/timesheet?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}", Ct);

        Assert.Empty(sheet.Employees);
    }

    [Fact]
    public async Task Given_eine_fremde_Standortleitung_When_sie_den_Plan_mit_employeeId_laedt_Then_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [other.Id]);

        var roster = await manager.GetAsync($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}", Ct);
        var timesheet = await manager.GetAsync($"/api/locations/{spa.Location.Id}/timesheet?from={Monday:yyyy-MM-dd}&employeeId={spa.Anna.Id}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, roster.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, timesheet.StatusCode);
    }
}
