using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Abwesenheit aus der Handy-App beantragen (Slice 106), in der Windows-App genehmigen oder ablehnen (Slice 99).</summary>
public sealed class AbsenceRequestTests(ApiFixture fixture)
{
    private static readonly DateOnly Friday = TestSpa.Monday.AddDays(4);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(TestSpa Spa, EmployeeDto Lena, HttpClient App, MyAbsenceRequestDto Request)> FridayRequestedAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        var app = await ChannelTests.StaffAppAsync(spa, StaffRole.Therapist, lena.Id, Ct);
        var created = await app.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.DayOff, Friday, Friday, "Arzttermin"), TestSpa.Json, Ct);
        return (spa, lena, app, await TestSpa.ReadAsync<MyAbsenceRequestDto>(created, Ct));
    }

    [Fact]
    public async Task Gegeben_Lena_beantragt_Freitag_frei_Wenn_die_Managerin_die_Liste_oeffnet_Dann_steht_der_Antrag_darin()
    {
        var (spa, lena, app, request) = await FridayRequestedAsync(fixture);
        using var client = app;

        var open = await spa.GetAsync<List<AbsenceRequestDto>>("/api/absence-requests", Ct);

        var row = Assert.Single(open);
        Assert.Equal(request.Id, row.Id);
        Assert.Equal(lena.Id, row.EmployeeId);
        Assert.Equal("Lena", row.EmployeeName);
        Assert.Equal(RosterKind.DayOff, row.Kind);
        Assert.Equal(Friday, row.From);
        Assert.Equal("Arzttermin", row.Note);
    }

    [Fact]
    public async Task Die_Liste_zaehlt_die_Termine_im_Zeitraum()
    {
        var (spa, lena, app, _) = await FridayRequestedAsync(fixture);
        using var client = app;
        await spa.BookOkAsync(TestSpa.At(10), Ct, lena.Id);
        (await app.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.Vacation, TestSpa.Monday, TestSpa.Monday, null), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        var open = await spa.GetAsync<List<AbsenceRequestDto>>("/api/absence-requests", Ct);

        Assert.Equal(1, Assert.Single(open, r => r.From == TestSpa.Monday).AffectedAppointments);
        Assert.Equal(0, Assert.Single(open, r => r.From == Friday).AffectedAppointments);
    }

    [Fact]
    public async Task Gegeben_Genehmigung_Dann_steht_die_Abwesenheit_im_Dienstplan_und_Lena_sieht_die_Entscheidung()
    {
        var (spa, lena, app, request) = await FridayRequestedAsync(fixture);
        using var client = app;

        var approved = await spa.Client.PostAsync($"/api/absence-requests/{request.Id}/approve", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        var roster = await spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={Friday:yyyy-MM-dd}&days=1", Ct);
        Assert.Equal(RosterKind.DayOff, Assert.Single(roster.Employees, r => r.EmployeeId == lena.Id).Days[0].Entries[0].Kind);
        var mine = await TestSpa.ReadAsync<List<MyAbsenceRequestDto>>(await app.GetAsync("/api/absence-requests/mine", Ct), Ct);
        Assert.Equal(AbsenceRequestState.Approved, Assert.Single(mine).State);
        Assert.NotNull(mine[0].DecidedAt);
        Assert.Empty(await spa.GetAsync<List<AbsenceRequestDto>>("/api/absence-requests", Ct));
    }

    [Fact]
    public async Task Gegeben_Ablehnung_Dann_bleibt_der_Dienstplan_und_Lena_sieht_die_Entscheidung()
    {
        var (spa, lena, app, request) = await FridayRequestedAsync(fixture);
        using var client = app;

        var rejected = await spa.Client.PostAsync($"/api/absence-requests/{request.Id}/reject", null, Ct);
        var again = await spa.Client.PostAsync($"/api/absence-requests/{request.Id}/approve", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        var roster = await spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={Friday:yyyy-MM-dd}&days=1", Ct);
        Assert.DoesNotContain(Assert.Single(roster.Employees, r => r.EmployeeId == lena.Id).Days[0].Entries, e => e.Kind != RosterKind.Work);
        var mine = await TestSpa.ReadAsync<List<MyAbsenceRequestDto>>(await app.GetAsync("/api/absence-requests/mine", Ct), Ct);
        Assert.Equal(AbsenceRequestState.Rejected, Assert.Single(mine).State);
    }

    [Theory]
    [InlineData(RosterKind.Work, 4, 4)]
    [InlineData(RosterKind.DayOff, 5, 4)]
    [InlineData(RosterKind.DayOff, -2000, -2000)]
    [InlineData(RosterKind.Vacation, 0, 100)]
    public async Task Ungueltige_Antraege_lehnt_die_API_ab(RosterKind kind, int from, int until)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], spa.Location.Id, Ct);
        using var app = await ChannelTests.StaffAppAsync(spa, StaffRole.Therapist, lena.Id, Ct);

        var created = await app.PostAsJsonAsync("/api/absence-requests",
            new AbsenceRequestInput(kind, TestSpa.Monday.AddDays(from), TestSpa.Monday.AddDays(until), null), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    [Fact]
    public async Task Ein_zweiter_offener_Antrag_ueber_denselben_Tag_wird_abgelehnt()
    {
        var (_, _, app, _) = await FridayRequestedAsync(fixture);
        using var client = app;

        var second = await app.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.Vacation, Friday, Friday.AddDays(1), null), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Ein_Antrag_ueber_einen_genehmigten_Tag_wird_abgelehnt_ein_abgelehnter_Tag_ist_wieder_frei()
    {
        var (spa, _, app, request) = await FridayRequestedAsync(fixture);
        using var client = app;
        (await spa.Client.PostAsync($"/api/absence-requests/{request.Id}/approve", null, Ct)).EnsureSuccessStatusCode();
        var monday = TestSpa.Monday.AddDays(7);
        var second = await TestSpa.ReadAsync<MyAbsenceRequestDto>(await app.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.DayOff, monday, monday, null), TestSpa.Json, Ct), Ct);
        (await spa.Client.PostAsync($"/api/absence-requests/{second.Id}/reject", null, Ct)).EnsureSuccessStatusCode();

        var overApproved = await app.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.Vacation, Friday, Friday, null), TestSpa.Json, Ct);
        var overRejected = await app.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.Vacation, monday, monday, null), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, overApproved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, overRejected.StatusCode);
    }

    [Fact]
    public async Task Gleichzeitig_genehmigen_und_ablehnen_Dann_passt_der_Dienstplan_zur_einen_Entscheidung()
    {
        var (spa, lena, app, request) = await FridayRequestedAsync(fixture);
        using var client = app;

        var answers = await Task.WhenAll(
            spa.Client.PostAsync($"/api/absence-requests/{request.Id}/approve", null, Ct),
            spa.Client.PostAsync($"/api/absence-requests/{request.Id}/reject", null, Ct));

        Assert.Single(answers, a => a.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(answers, a => a.StatusCode == HttpStatusCode.BadRequest);
        var state = Assert.Single(await TestSpa.ReadAsync<List<MyAbsenceRequestDto>>(await app.GetAsync("/api/absence-requests/mine", Ct), Ct)).State;
        var roster = await spa.GetAsync<RosterWeek>($"/api/locations/{spa.Location.Id}/roster?from={Friday:yyyy-MM-dd}&days=1", Ct);
        var inRoster = Assert.Single(roster.Employees, r => r.EmployeeId == lena.Id).Days[0].Entries.Any(e => e.Kind == RosterKind.DayOff);
        Assert.Equal(state == AbsenceRequestState.Approved, inRoster);
    }

    [Fact]
    public async Task Standortleitung_eines_anderen_Standorts_sieht_und_entscheidet_den_Antrag_nicht()
    {
        var (spa, _, app, request) = await FridayRequestedAsync(fixture);
        using var client = app;
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        using var manager = await TestUsers.ClientAsync(spa.Fixture, Ct, StaffRole.Manager, [graz.Id]);

        var open = await TestSpa.ReadAsync<List<AbsenceRequestDto>>(await manager.GetAsync("/api/absence-requests", Ct), Ct);
        var approved = await manager.PostAsync($"/api/absence-requests/{request.Id}/approve", null, Ct);

        Assert.Empty(open);
        Assert.Equal(HttpStatusCode.NotFound, approved.StatusCode);
    }

    [Fact]
    public async Task Die_Liste_der_offenen_Antraege_bleibt_der_Mitarbeiter_App_verschlossen()
    {
        var (_, _, app, request) = await FridayRequestedAsync(fixture);
        using var client = app;

        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync("/api/absence-requests", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.PostAsync($"/api/absence-requests/{request.Id}/approve", null, Ct)).StatusCode);
    }
}
