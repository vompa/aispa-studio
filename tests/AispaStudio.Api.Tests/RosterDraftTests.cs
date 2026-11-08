using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Dienstplan als Entwurf: gilt erst nach dem Veröffentlichen; kurzfristige Änderungen werden gemeldet.</summary>
public sealed class RosterDraftTests(ApiFixture fixture)
{
    private static readonly DateOnly Monday = TestSpa.Monday;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly RosterEntry[] Vacation = [new(RosterKind.Vacation, null, null, null)];

    private static Task<RosterDayResult> PlanAsync(TestSpa spa, DateOnly date, RosterEntry[] entries, bool draft) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Anna.Id}/{date:yyyy-MM-dd}",
            new RosterDayInput(entries, null, draft), Ct);

    private static Task<RosterPublishResult> PublishAsync(TestSpa spa, DateOnly from, DateOnly until) =>
        spa.PostAsync<RosterPublishResult>($"/api/locations/{spa.Location.Id}/roster/publish", new RosterPublishRequest(from, until), Ct);

    private static Task<RosterWeek> WeekAsync(TestSpa spa, DateOnly from) =>
        spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={from:yyyy-MM-dd}", Ct);

    private static async Task<bool> AnnaInCalendarAsync(TestSpa spa, DateOnly date) =>
        (await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={date:yyyy-MM-dd}", Ct))
            .Shifts.Any(s => s.EmployeeId == spa.Anna.Id);

    private static RosterCell AnnaOn(RosterWeek week, DateOnly date) =>
        week.Employees.Single(e => e.DisplayName == "Anna").Days.Single(d => d.Date == date);

    [Fact]
    public async Task Entwurf_gilt_erst_nach_dem_Veroeffentlichen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var planned = await PlanAsync(spa, Monday, Vacation, draft: true);
        var draftWeek = await WeekAsync(spa, Monday);
        var stillWorking = await AnnaInCalendarAsync(spa, Monday);
        var result = await PublishAsync(spa, Monday, Monday.AddDays(6));
        var publishedWeek = await WeekAsync(spa, Monday);
        var nowOff = !await AnnaInCalendarAsync(spa, Monday);

        Assert.True(planned.Day.Draft);
        Assert.True(AnnaOn(draftWeek, Monday).Draft);
        Assert.Equal(1, draftWeek.Drafts);
        Assert.Null(draftWeek.LastPublishedAt);
        Assert.True(stillWorking);
        Assert.Equal(1, result.Days);
        Assert.Empty(result.ShortNotice);
        Assert.False(AnnaOn(publishedWeek, Monday).Draft);
        Assert.Equal(RosterKind.Vacation, Assert.Single(AnnaOn(publishedWeek, Monday).Entries).Kind);
        Assert.Equal(0, publishedWeek.Drafts);
        Assert.True(Math.Abs((result.PublishedAt - publishedWeek.LastPublishedAt!.Value).TotalSeconds) < 1);
        Assert.True(nowOff);
    }

    [Fact]
    public async Task Sofort_gueltig_ersetzt_den_Entwurf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, Monday, Vacation, draft: true);

        var sick = await PlanAsync(spa, Monday, [new(RosterKind.Sick, null, null, null)], draft: false);
        var week = await WeekAsync(spa, Monday);

        Assert.False(sick.Day.Draft);
        Assert.Equal(0, week.Drafts);
        Assert.Equal(RosterKind.Sick, Assert.Single(AnnaOn(week, Monday).Entries).Kind);
        Assert.False(await AnnaInCalendarAsync(spa, Monday));
    }

    [Fact]
    public async Task Zuruecksetzen_als_Entwurf_und_Verwerfen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, Monday, Vacation, draft: false);
        var nextMonday = Monday.AddDays(7);
        await PlanAsync(spa, nextMonday, Vacation, draft: true);

        var reset = await spa.Client.DeleteAsync($"/api/locations/{spa.Location.Id}/roster/{spa.Anna.Id}/{Monday:yyyy-MM-dd}?draft=true", Ct);
        var resetCell = (await TestSpa.ReadAsync<RosterDayResult>(reset, Ct)).Day;
        var discard = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/roster/discard",
            new RosterPublishRequest(nextMonday, nextMonday), TestSpa.Json, Ct);
        var stillOff = !await AnnaInCalendarAsync(spa, Monday);
        await PublishAsync(spa, Monday, nextMonday);

        Assert.True(resetCell.Draft);
        Assert.Equal(RosterSource.Pattern, resetCell.Source);
        Assert.Equal(HttpStatusCode.NoContent, discard.StatusCode);
        Assert.True(stillOff);
        Assert.True(await AnnaInCalendarAsync(spa, Monday));
        Assert.False(AnnaOn(await WeekAsync(spa, nextMonday), nextMonday).Draft);
        Assert.True(await AnnaInCalendarAsync(spa, nextMonday));
    }

    [Fact]
    public async Task Meldet_kurzfristige_Aenderungen_nach_Landesrecht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var soon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        await PlanAsync(spa, soon, Vacation, draft: true);

        var result = await PublishAsync(spa, soon, soon);

        Assert.Contains("weniger als 14 Tage vorher (§ 19c Abs. 2 AZG)", Assert.Single(result.ShortNotice), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rezeption_darf_nicht_veroeffentlichen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        var response = await reception.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/roster/publish",
            new RosterPublishRequest(Monday, Monday), TestSpa.Json, Ct);
        var tooLong = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/roster/publish",
            new RosterPublishRequest(Monday, Monday.AddDays(70)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }
}
