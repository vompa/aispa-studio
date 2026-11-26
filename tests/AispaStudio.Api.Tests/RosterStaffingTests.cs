using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Besetzung über den Tag im Wochenplan: Personen im Dienst je Stunde am Standort.</summary>
public sealed class RosterStaffingTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateOnly Monday = TestSpa.Monday;
    private static readonly DateOnly Tuesday = TestSpa.Monday.AddDays(1);
    private static readonly DateOnly Wednesday = TestSpa.Monday.AddDays(2);

    private static RosterEntry Work(Guid location, TimeOnly from, TimeOnly until) => new(RosterKind.Work, location, from, until);

    private static Task<RosterDayResult> PlanAsync(TestSpa spa, Guid employee, DateOnly date, params RosterEntry[] entries) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{employee}/{date:yyyy-MM-dd}", new RosterDayInput(entries, null), Ct);

    private static async Task<IReadOnlyList<HourlyStaffing>> StaffingAsync(TestSpa spa, DateOnly day) =>
        (await spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}", Ct)).HourlyStaffing!.Where(h => h.Date == day).ToList();

    [Fact]
    public async Task Dienst_hier_zaehlt_Dienst_anderswo_und_Abwesenheit_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        var cora = await spa.CreateEmployeeAsync("Cora", [spa.Massage.Id], spa.Location.Id, Ct);
        await PlanAsync(spa, spa.Anna.Id, Monday, new RosterEntry(RosterKind.Vacation, null, null, null));
        await PlanAsync(spa, spa.Ben.Id, Monday, Work(other.Id, new(10, 0), new(12, 0)));
        await PlanAsync(spa, cora.Id, Monday, Work(spa.Location.Id, new(10, 0), new(12, 0)));

        var hours = await StaffingAsync(spa, Monday);

        Assert.Equal([0, 1, 1, 0], hours.Where(h => h.Hour is >= 9 and <= 12).Select(h => h.Count));
    }

    [Fact]
    public async Task Angebrochene_Stunde_zaehlt_ab_30_Minuten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, spa.Anna.Id, Tuesday, Work(spa.Location.Id, new(10, 0), new(12, 29)));
        await PlanAsync(spa, spa.Ben.Id, Tuesday, Work(spa.Location.Id, new(10, 0), new(12, 30)));

        var hours = await StaffingAsync(spa, Tuesday);

        Assert.Equal([2, 2, 1, 0], hours.Where(h => h.Hour is >= 10 and <= 13).Select(h => h.Count));
    }

    [Fact]
    public async Task Geschlossener_Tag_und_Sonntag_sind_nicht_geoeffnet()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, spa.Anna.Id, Tuesday, Work(spa.Location.Id, new(10, 0), new(12, 0)));
        _ = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Tuesday));

        var week = (await spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={Monday:yyyy-MM-dd}", Ct)).HourlyStaffing!;

        Assert.DoesNotContain(week, h => h.Date == Tuesday && h.Open);
        Assert.DoesNotContain(week, h => h.Date == Monday.AddDays(6) && h.Open);
        Assert.Contains(week, h => h.Date == Wednesday && h.Open);
    }

    [Fact]
    public async Task Geoeffnete_Stunde_ohne_Dienst_ist_offen_mit_Null()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, spa.Anna.Id, Tuesday, Work(spa.Location.Id, new(10, 0), new(12, 0)));

        var hours = await StaffingAsync(spa, Wednesday);

        Assert.Equal(Enumerable.Range(9, 11), hours.Select(h => h.Hour));
        Assert.All(hours, h => Assert.True(h.Open && h.Count == 0));
    }
}
