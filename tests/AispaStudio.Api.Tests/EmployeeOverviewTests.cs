using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Übersicht der Personenkarte: Kennzahlen zu einer Person, vom Server gerechnet. Die Uhr der API steht je Test fest (der Testkalender liegt im März 2027);
/// Ben arbeitet laut Wochenarbeitszeit montags 9 bis 17 Uhr in Wien, Cleo hat keine Wochenarbeitszeit und nur geplante Dienste.
/// </summary>
public sealed class EmployeeOverviewTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Monday = TestSpa.Monday;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private WebApplicationFactory<Program> ClockAt(DateOnly date, int hour, int minute = 0) =>
        fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(TestSpa.At(hour, minute, date)))));

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, StaffRole role, Guid[] locations)
    {
        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await anonymous.PostAsJsonAsync("/api/auth/dev/register",
            new DevRegistration($"test-{Guid.NewGuid():N}@aispa.test", "Test Person", role, locations, null), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        return client;
    }

    private static Task<HttpResponseMessage> OverviewResponseAsync(HttpClient client, Guid employeeId) =>
        client.GetAsync($"/api/employees/{employeeId}/overview", Ct);

    private static async Task<EmployeeOverviewDto> OverviewAsync(HttpClient client, Guid employeeId) =>
        await TestSpa.ReadAsync<EmployeeOverviewDto>(await OverviewResponseAsync(client, employeeId), Ct);

    private static async Task<T> PutAsync<T>(HttpClient client, string path, object body) =>
        await TestSpa.ReadAsync<T>(await client.PutAsJsonAsync(path, body, TestSpa.Json, Ct), Ct);

    private static Task<RosterDayResult> PlanAsync(HttpClient client, Guid locationId, Guid employeeId, DateOnly date, RosterEntry entry, bool draft = false) =>
        PutAsync<RosterDayResult>(client, $"/api/locations/{locationId}/roster/{employeeId}/{date:yyyy-MM-dd}",
            new RosterDayInput([entry], null, Draft: draft, RulesConfirmed: true));

    private static RosterEntry Shift(Guid locationId, int from, int to) => new(RosterKind.Work, locationId, new TimeOnly(from, 0), new TimeOnly(to, 0));

    private static Task<EmployeeDto> CleoAsync(TestSpa spa) =>
        spa.PostAsync<EmployeeDto>("/api/employees", new EmployeeInput("Cleo", "#2f6f5e", [spa.Massage.Id], []), Ct);

    private static async Task AddRecordAsync(HttpClient client, Guid locationId, Guid employeeId, DateOnly date, int from, int to) =>
        (await client.PostAsJsonAsync($"/api/locations/{locationId}/time-records",
            new TimeRecordInput(employeeId, TestSpa.At(from, 0, date), TestSpa.At(to, 0, date), "Nachgetragen"), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

    private static async Task AddCertificateAsync(HttpClient client, Guid employeeId, string name, DateOnly? validUntil, int remindDays = 60) =>
        (await client.PostAsJsonAsync($"/api/employees/{employeeId}/certificates", new StaffCertificateInput(name, ValidUntil: validUntil, RemindDays: remindDays), TestSpa.Json, Ct))
        .EnsureSuccessStatusCode();

    [Fact]
    public async Task Der_heutige_Dienst_ist_der_naechste_solange_er_nicht_vorbei_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await SignInAsync(ClockAt(Monday, 16), StaffRole.Owner, [spa.Location.Id]);

        var overview = await OverviewAsync(client, spa.Ben.Id);

        Assert.Equal(spa.Ben.Id, overview.EmployeeId);
        Assert.Equal(new EmployeeNextShiftDto(Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), spa.Location.Id, "Wien"), overview.NextShift);
    }

    [Fact]
    public async Task Ist_der_heutige_Dienst_vorbei_folgt_der_naechste()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await SignInAsync(ClockAt(Monday, 18), StaffRole.Owner, [spa.Location.Id]);

        var overview = await OverviewAsync(client, spa.Ben.Id);

        Assert.Equal(Monday.AddDays(7), overview.NextShift!.Date);
    }

    [Fact]
    public async Task Ein_Entwurf_zaehlt_nicht_als_naechster_Dienst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var cleo = await CleoAsync(spa);
        using var client = await SignInAsync(ClockAt(Monday, 8), StaffRole.Owner, [spa.Location.Id]);
        await PlanAsync(client, spa.Location.Id, cleo.Id, Monday.AddDays(1), Shift(spa.Location.Id, 10, 14), draft: true);

        var withDraftOnly = await OverviewAsync(client, cleo.Id);
        await PlanAsync(client, spa.Location.Id, cleo.Id, Monday.AddDays(2), Shift(spa.Location.Id, 12, 16));
        var withPublished = await OverviewAsync(client, cleo.Id);

        Assert.Null(withDraftOnly.NextShift);
        Assert.Equal(new EmployeeNextShiftDto(Monday.AddDays(2), new TimeOnly(12, 0), new TimeOnly(16, 0), spa.Location.Id, "Wien"), withPublished.NextShift);
    }

    [Fact]
    public async Task Ein_Dienst_in_mehr_als_31_Tagen_ist_noch_kein_naechster_Dienst()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var cleo = await CleoAsync(spa);
        using var client = await SignInAsync(ClockAt(Monday, 8), StaffRole.Owner, [spa.Location.Id]);
        await PlanAsync(client, spa.Location.Id, cleo.Id, Monday.AddDays(32), Shift(spa.Location.Id, 10, 14));

        var tooFar = await OverviewAsync(client, cleo.Id);
        await PlanAsync(client, spa.Location.Id, cleo.Id, Monday.AddDays(31), Shift(spa.Location.Id, 10, 14));
        var onLastDay = await OverviewAsync(client, cleo.Id);

        Assert.Null(tooFar.NextShift);
        Assert.Equal(Monday.AddDays(31), onLastDay.NextShift!.Date);
    }

    [Fact]
    public async Task Die_Wochenstunden_zaehlen_alle_Standorte_der_laufenden_Woche()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Wednesday], Ct);
        using var client = await SignInAsync(ClockAt(Monday.AddDays(4), 12), StaffRole.Owner, [spa.Location.Id, graz.Id]);
        await PlanAsync(client, graz.Id, spa.Ben.Id, Monday.AddDays(2), Shift(graz.Id, 10, 14));
        await AddRecordAsync(client, spa.Location.Id, spa.Ben.Id, Monday, 9, 12);
        await AddRecordAsync(client, spa.Location.Id, spa.Ben.Id, Monday, 13, 17);
        await AddRecordAsync(client, graz.Id, spa.Ben.Id, Monday.AddDays(2), 10, 14);
        // Der Montag der Vorwoche und der folgende Montag gehören nicht zur Woche.
        await AddRecordAsync(client, spa.Location.Id, spa.Ben.Id, Monday.AddDays(-7), 9, 17);

        var overview = await OverviewAsync(client, spa.Ben.Id);

        Assert.Equal(12m, overview.WeekPlannedHours);
        Assert.Equal(11m, overview.WeekActualHours);
    }

    [Fact]
    public async Task Gegeben_Anspruch_und_Urlaub_Dann_nennt_die_Uebersicht_den_Rest_des_Jahres()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await SignInAsync(ClockAt(Monday.AddDays(2), 10), StaffRole.Owner, [spa.Location.Id]);
        var vacation = new RosterEntry(RosterKind.Vacation, null, null, null);
        await PlanAsync(client, spa.Location.Id, spa.Ben.Id, Monday, vacation);
        await PlanAsync(client, spa.Location.Id, spa.Ben.Id, Monday.AddDays(7), vacation);
        await PutAsync<LeaveAccountDto>(client, $"/api/employees/{spa.Ben.Id}/leave-account/{Monday.Year}", new LeaveEntitlementInput(25, 2));

        var overview = await OverviewAsync(client, spa.Ben.Id);

        Assert.Equal(new EmployeeLeaveSummaryDto(Monday.Year, 25m, 1m, 1m), overview.Leave);
    }

    [Fact]
    public async Task Ohne_hinterlegten_Anspruch_fehlt_der_Rest_des_Urlaubs()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await SignInAsync(ClockAt(Monday.AddDays(2), 10), StaffRole.Owner, [spa.Location.Id]);
        await PlanAsync(client, spa.Location.Id, spa.Ben.Id, Monday, new RosterEntry(RosterKind.Vacation, null, null, null));

        var overview = await OverviewAsync(client, spa.Ben.Id);

        Assert.Equal(new EmployeeLeaveSummaryDto(Monday.Year, null, 0m, 1m), overview.Leave);
    }

    [Fact]
    public async Task Offene_Antraege_zaehlen_nur_die_dieser_Person_und_nur_offene()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var benApp = await ChannelTests.StaffAppAsync(spa, StaffRole.Therapist, spa.Ben.Id, Ct);
        using var annaApp = await ChannelTests.StaffAppAsync(spa, StaffRole.Therapist, spa.Anna.Id, Ct);
        var first = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
        var toReject = await TestSpa.ReadAsync<MyAbsenceRequestDto>(
            await benApp.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.DayOff, first, first, null), TestSpa.Json, Ct), Ct);
        (await benApp.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.DayOff, first.AddDays(5), first.AddDays(5), null), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        (await annaApp.PostAsJsonAsync("/api/absence-requests", new AbsenceRequestInput(RosterKind.DayOff, first, first, null), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        (await spa.Client.PostAsync($"/api/absence-requests/{toReject.Id}/reject", null, Ct)).EnsureSuccessStatusCode();
        using var client = await SignInAsync(ClockAt(Monday, 10), StaffRole.Owner, [spa.Location.Id]);

        var ben = await OverviewAsync(client, spa.Ben.Id);
        var anna = await OverviewAsync(client, spa.Anna.Id);

        Assert.Equal(1, ben.OpenAbsenceRequests);
        Assert.Equal(1, anna.OpenAbsenceRequests);
    }

    [Fact]
    public async Task Faellige_und_abgelaufene_Zertifikate_werden_gezaehlt_Erneuerte_und_unbefristete_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await SignInAsync(ClockAt(Monday, 10), StaffRole.Owner, [spa.Location.Id]);
        await AddCertificateAsync(client, spa.Ben.Id, "Erste Hilfe", Monday.AddDays(20));
        await AddCertificateAsync(client, spa.Ben.Id, "Hygiene", Monday.AddDays(-9));
        await AddCertificateAsync(client, spa.Ben.Id, "Brandschutz", null);
        await AddCertificateAsync(client, spa.Ben.Id, "Datenschutz", Monday.AddDays(300));
        await AddCertificateAsync(client, spa.Ben.Id, "Massagelizenz", Monday.AddDays(-40));
        await AddCertificateAsync(client, spa.Ben.Id, "Massagelizenz", Monday.AddDays(300));

        var ben = await OverviewAsync(client, spa.Ben.Id);
        var anna = await OverviewAsync(client, spa.Anna.Id);

        Assert.Equal(2, ben.DueCertificates);
        Assert.Equal(1, ben.ExpiredCertificates);
        Assert.Equal(0, anna.DueCertificates);
        Assert.Equal(0, anna.ExpiredCertificates);
    }

    [Fact]
    public async Task Das_Backoffice_sieht_Dienst_Zeiten_und_Urlaub_aber_weder_Antraege_noch_Zertifikate()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await SignInAsync(ClockAt(Monday, 10), StaffRole.Backoffice, [spa.Location.Id]);

        var overview = await OverviewAsync(client, spa.Ben.Id);

        Assert.NotNull(overview.NextShift);
        Assert.NotNull(overview.WeekPlannedHours);
        Assert.NotNull(overview.WeekActualHours);
        Assert.NotNull(overview.Leave);
        Assert.Null(overview.OpenAbsenceRequests);
        Assert.Null(overview.DueCertificates);
        Assert.Null(overview.ExpiredCertificates);
    }

    [Fact]
    public async Task Die_Standortleitung_sieht_Antraege_aber_keine_Zertifikate()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await SignInAsync(ClockAt(Monday, 10), StaffRole.Manager, [spa.Location.Id]);

        var overview = await OverviewAsync(client, spa.Ben.Id);

        Assert.NotNull(overview.NextShift);
        Assert.NotNull(overview.Leave);
        Assert.Equal(0, overview.OpenAbsenceRequests);
        Assert.Null(overview.DueCertificates);
        Assert.Null(overview.ExpiredCertificates);
    }

    [Theory]
    [InlineData(StaffRole.Reception)]
    [InlineData(StaffRole.Therapist)]
    public async Task Ohne_eines_der_Teilrechte_antwortet_die_API_mit_403(StaffRole role)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = await SignInAsync(ClockAt(Monday, 10), role, [spa.Location.Id]);

        var response = await OverviewResponseAsync(client, spa.Ben.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Eine_unbekannte_Person_gibt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await OverviewResponseAsync(spa.Client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Eine_Person_aus_einem_anderen_Mandanten_gibt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var response = await OverviewResponseAsync(spa.Client, other.Ben.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
