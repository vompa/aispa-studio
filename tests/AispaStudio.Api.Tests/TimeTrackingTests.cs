using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Roster;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Zeiterfassung: selbst stempeln, Soll und Ist, Korrekturen nur mit Grund und Protokoll, Lohn aus Ist-Zeiten.</summary>
public sealed class TimeTrackingTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Ein vergangener Montag, an dem Anna laut Wochenarbeitszeit 9 bis 17 Uhr arbeitet.</summary>
    private static readonly DateOnly Past = new(2026, 6, 1);

    private static DateTimeOffset At(int hour, int minute = 0, DateOnly? date = null) => TestSpa.At(hour, minute, date ?? Past);

    private static Task<HttpResponseMessage> AddAsync(TestSpa spa, Guid employee, DateTimeOffset start, DateTimeOffset? end, string reason = "Stempeln vergessen") =>
        spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/time-records", new TimeRecordInput(employee, start, end, reason), TestSpa.Json, Ct);

    private static Task<Timesheet> SheetAsync(TestSpa spa) =>
        spa.GetAsync<Timesheet>($"/api/locations/{spa.Location.Id}/timesheet?from={Past:yyyy-MM-dd}", Ct);

    [Fact]
    public async Task Kommen_Pause_Gehen_stempelt_eigene_Zeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);

        var clockIn = await anna.PostAsJsonAsync("/api/time/me/clock-in", new ClockInput(spa.Location.Id), TestSpa.Json, Ct);
        var status = await TestSpa.ReadAsync<MyTimeStatus>(clockIn, Ct);
        var twice = await anna.PostAsJsonAsync("/api/time/me/clock-in", new ClockInput(spa.Location.Id), TestSpa.Json, Ct);
        var clockOut = await TestSpa.ReadAsync<MyTimeStatus>(await anna.PostAsync("/api/time/me/clock-out", null, Ct), Ct);
        var outAgain = await anna.PostAsync("/api/time/me/clock-out", null, Ct);
        var sheet = await anna.GetAsync($"/api/locations/{spa.Location.Id}/timesheet?from={Past:yyyy-MM-dd}", Ct);

        Assert.Equal(spa.Anna.Id, status.EmployeeId);
        Assert.NotNull(status.Running);
        Assert.Equal(TimeRecordSource.Clock, status.Running.Source);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Null(clockOut.Running);
        Assert.Single(clockOut.Today);
        Assert.NotNull(clockOut.Today[0].End);
        Assert.Equal(HttpStatusCode.Conflict, outAgain.StatusCode);
        // Die Zeiten der Kolleginnen sieht nur die Standortleitung.
        Assert.Equal(HttpStatusCode.Forbidden, sheet.StatusCode);
    }

    [Fact]
    public async Task Konto_ohne_Person_kann_nicht_stempeln()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/time/me/clock-in", new ClockInput(spa.Location.Id), TestSpa.Json, Ct);
        var status = await spa.GetAsync<MyTimeStatus>("/api/time/me", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(status.EmployeeId);
    }

    [Fact]
    public async Task Nachtragen_und_Korrigieren_mit_Grund_und_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await TestSpa.ReadAsync<TimeRecordDto>(await AddAsync(spa, spa.Anna.Id, At(9), At(13)), Ct);
        var afternoon = await TestSpa.ReadAsync<TimeRecordDto>(await AddAsync(spa, spa.Anna.Id, At(13, 15), At(17, 30)), Ct);

        var before = (await SheetAsync(spa)).Employees.Single(e => e.EmployeeId == spa.Anna.Id).Days[0];

        var withoutReason = await AddAsync(spa, spa.Anna.Id, At(18), At(19), reason: "");
        var overlapping = await AddAsync(spa, spa.Anna.Id, At(12), At(14));
        var future = await AddAsync(spa, spa.Anna.Id, DateTimeOffset.UtcNow.AddHours(2), null);
        var corrected = await spa.PutAsync<TimeRecordDto>($"/api/locations/{spa.Location.Id}/time-records/{afternoon.Id}",
            new TimeRecordInput(spa.Anna.Id, At(13, 30), At(17, 30), "Pause war 30 Minuten"), Ct);
        var after = (await SheetAsync(spa)).Employees.Single(e => e.EmployeeId == spa.Anna.Id);
        var changes = await spa.GetAsync<List<TimeRecordChangeDto>>($"/api/locations/{spa.Location.Id}/time-records/{afternoon.Id}/changes", Ct);

        Assert.Equal(8, before.PlannedHours);
        Assert.Equal(8.25, before.ActualHours);
        Assert.Equal(15, before.BreakMinutes);
        Assert.Contains(before.Warnings, w => w.StartsWith("Pause zu kurz: 15 von 30", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overlapping.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.True(corrected.Changed);
        Assert.Equal(TimeRecordSource.Manual, corrected.Source);
        Assert.Equal(8, after.Days[0].ActualHours);
        Assert.Equal(30, after.Days[0].BreakMinutes);
        Assert.Empty(after.Days[0].Warnings);
        Assert.Equal(8, after.ActualHours);
        Assert.Equal([TimeRecordAction.Created, TimeRecordAction.Changed], changes.Select(c => c.Action));
        Assert.Equal(At(13, 15), changes[1].OldStart);
        Assert.Equal(At(13, 30), changes[1].NewStart);
        Assert.Equal("Pause war 30 Minuten", changes[1].Reason);
        Assert.Equal("Test Person", changes[1].ChangedBy);
    }

    [Fact]
    public async Task Entfernen_bleibt_im_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var record = await TestSpa.ReadAsync<TimeRecordDto>(await AddAsync(spa, spa.Ben.Id, At(9), At(12)), Ct);

        var noReason = await spa.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/locations/{spa.Location.Id}/time-records/{record.Id}")
        {
            Content = JsonContent.Create(new TimeRecordDeletion(""), options: TestSpa.Json),
        }, Ct);
        var deleted = await spa.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/locations/{spa.Location.Id}/time-records/{record.Id}")
        {
            Content = JsonContent.Create(new TimeRecordDeletion("Doppelt erfasst"), options: TestSpa.Json),
        }, Ct);
        var ben = (await SheetAsync(spa)).Employees.Single(e => e.EmployeeId == spa.Ben.Id);
        var changes = await spa.GetAsync<List<TimeRecordChangeDto>>($"/api/locations/{spa.Location.Id}/time-records/{record.Id}/changes", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(ben.Days[0].Records);
        Assert.Equal(TimeRecordAction.Deleted, changes[^1].Action);
        Assert.Equal("Doppelt erfasst", changes[^1].Reason);
    }

    [Fact]
    public async Task Lohn_nimmt_Ist_Zeiten_wo_gestempelt_wurde()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Juni 2026: Anna laut Plan an fünf Montagen 9–17 Uhr (40 h); am 1. Juni nur 9–12 Uhr gestempelt.
        await TestSpa.ReadAsync<TimeRecordDto>(await AddAsync(spa, spa.Anna.Id, At(9), At(12)), Ct);

        var preview = await spa.GetAsync<PayrollPreviewDto>("/api/payroll/preview?year=2026&month=6", Ct);

        Assert.Equal(35m, preview.Lines.Single(l => l.EmployeeId == spa.Anna.Id && l.Item == PayrollItem.Hours).Quantity);
        Assert.Equal(40m, preview.Lines.Single(l => l.EmployeeId == spa.Ben.Id && l.Item == PayrollItem.Hours).Quantity);
        Assert.Contains(preview.Warnings, w => w.StartsWith("An 9 Diensttagen wurde nicht gestempelt", StringComparison.Ordinal));
    }

    [Fact]
    public void Arbeitszeitregeln_je_Land()
    {
        // 8 bis 18 Uhr mit 30 Minuten Pause: 9,5 Stunden Arbeit.
        (TimeSpan, TimeSpan)[] day = [(TimeSpan.FromHours(8), TimeSpan.FromHours(12.5)), (TimeSpan.FromHours(13), TimeSpan.FromHours(18))];
        // 12 Stunden Arbeit von 7 bis 22 Uhr mit 3 Stunden Pause.
        (TimeSpan, TimeSpan)[] longDay = [(TimeSpan.FromHours(7), TimeSpan.FromHours(13)), (TimeSpan.FromHours(16), TimeSpan.FromHours(22))];

        var austria = WorkingTime.DayWarnings(day, null, WorkingTimeRules.Austria, checkBreaks: true);
        var germany = WorkingTime.DayWarnings(day, null, WorkingTimeRules.Germany, checkBreaks: true);
        var switzerland = WorkingTime.DayWarnings(longDay, null, WorkingTimeRules.Switzerland, checkBreaks: true);
        var austriaLong = WorkingTime.DayWarnings(longDay, null, WorkingTimeRules.Austria, checkBreaks: true);

        Assert.Empty(austria);
        Assert.StartsWith("Pause zu kurz: 30 von 45 Minuten", Assert.Single(germany), StringComparison.Ordinal);
        Assert.Equal(["Arbeitstag länger als 14 Stunden einschließlich Pausen (15 h)."], switzerland);
        Assert.Contains(austriaLong, w => w.StartsWith("Mehr als 10 Stunden", StringComparison.Ordinal));
        Assert.Equal(60, WorkingTimeRules.Switzerland.RequiredBreakMinutes(9.5));
        Assert.Equal(50, WorkingTimeRules.For("ch").MaxHoursPerWeek);
        Assert.Same(WorkingTimeRules.Austria, WorkingTimeRules.For(null));
    }
}
