using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Urlaubskonto (ADR 0097): Anspruch und Übertrag trägt die Leitung ein, genommen und geplant zählt der veröffentlichte Dienstplan.</summary>
public sealed class LeaveAccountTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int LastYear => DateTime.UtcNow.Year - 1;

    private static int NextYear => DateTime.UtcNow.Year + 1;

    /// <summary>Der erste Montag des Jahres; Anna und Ben arbeiten in <see cref="TestSpa"/> nur montags.</summary>
    private static DateOnly FirstMonday(int year)
    {
        var day = new DateOnly(year, 1, 1);
        return day.AddDays(((int)DayOfWeek.Monday - (int)day.DayOfWeek + 7) % 7);
    }

    private static Task<RosterDayResult> VacationAsync(TestSpa spa, DateOnly date, params RosterEntry[] more) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Ben.Id}/{date:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null), .. more], null, RulesConfirmed: true), Ct);

    private static string Path(TestSpa spa, int year) => $"/api/employees/{spa.Ben.Id}/leave-account/{year}";

    [Fact]
    public async Task Ohne_eingetragenen_Anspruch_zaehlt_das_Konto_nur_und_nennt_keinen_Rest()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var monday = FirstMonday(NextYear);
        await VacationAsync(spa, monday);
        await VacationAsync(spa, monday.AddDays(1));

        var account = await spa.GetAsync<LeaveAccountDto>(Path(spa, NextYear), Ct);

        Assert.Null(account.Entitlement);
        Assert.Null(account.Remaining);
        Assert.Equal(0, account.Taken);
        Assert.Equal(1, account.Planned);
        Assert.Equal(1, account.UncountedDays);
        Assert.Equal([DayOfWeek.Monday], account.Workdays);
        Assert.False(account.WorkdaysAssumed);
    }

    [Fact]
    public async Task Gegeben_Anspruch_und_Uebertrag_Dann_rechnet_das_Konto_genommen_und_geplant_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var monday = FirstMonday(LastYear);
        await VacationAsync(spa, monday);
        await VacationAsync(spa, monday.AddDays(7));

        var saved = await spa.PutAsync<LeaveAccountDto>(Path(spa, LastYear), new LeaveEntitlementInput(25, 2.5m), Ct);
        var other = await spa.GetAsync<LeaveAccountDto>(Path(spa, NextYear), Ct);

        Assert.Equal(25, saved.Entitlement);
        Assert.Equal(2.5m, saved.CarriedOver);
        Assert.Equal(2, saved.Taken);
        Assert.Equal(0, saved.Planned);
        Assert.Equal(25.5m, saved.Remaining);
        Assert.Null(other.Entitlement);
        Assert.Equal(0, other.Taken);
    }

    [Fact]
    public async Task Ein_Urlaubstag_mit_Dienst_zaehlt_halb()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await VacationAsync(spa, FirstMonday(NextYear), new RosterEntry(RosterKind.Work, spa.Location.Id, new(9, 0), new(13, 0)));
        await spa.PutAsync<LeaveAccountDto>(Path(spa, NextYear), new LeaveEntitlementInput(10, 0), Ct);

        var account = await spa.GetAsync<LeaveAccountDto>(Path(spa, NextYear), Ct);

        Assert.Equal(0.5m, account.Planned);
        Assert.Equal(9.5m, account.Remaining);
    }

    [Fact]
    public async Task Der_Anspruch_laesst_sich_wieder_leeren()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LeaveAccountDto>(Path(spa, NextYear), new LeaveEntitlementInput(25, 0), Ct);

        var cleared = await spa.PutAsync<LeaveAccountDto>(Path(spa, NextYear), new LeaveEntitlementInput(null, 0), Ct);

        Assert.Null(cleared.Entitlement);
        Assert.Null(cleared.Remaining);
    }

    [Theory]
    [InlineData(25.3, 0)]
    [InlineData(-1, 0)]
    [InlineData(400, 0)]
    [InlineData(25, 0.25)]
    [InlineData(25, -400)]
    public async Task Unpassende_Werte_lehnt_die_API_ab(double entitlement, double carried)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PutAsJsonAsync(Path(spa, NextYear), new LeaveEntitlementInput((decimal)entitlement, (decimal)carried), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unbekannte_Person_oder_unsinniges_Jahr_gibt_es_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync($"/api/employees/{Guid.NewGuid()}/leave-account/{NextYear}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync(Path(spa, 1900), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await spa.Client.PutAsJsonAsync($"/api/employees/{Guid.NewGuid()}/leave-account/{NextYear}", new LeaveEntitlementInput(25, 0), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Rezeption_sieht_und_aendert_das_Urlaubskonto_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(spa.Fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync(Path(spa, NextYear), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PutAsJsonAsync(Path(spa, NextYear), new LeaveEntitlementInput(25, 0), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Ein_anderes_Spa_sieht_das_Konto_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(Path(spa, NextYear), Ct)).StatusCode);
    }
}
