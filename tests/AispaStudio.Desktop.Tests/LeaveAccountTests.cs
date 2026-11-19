using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Urlaubskonto auf der Personenkarte (ADR 0097): die App zeigt, was die API zählt, und sendet Anspruch und Übertrag.</summary>
public sealed class LeaveAccountTests
{
    private static (StubApi Api, RosterSample Roster, LeaveAccountViewModel Leave) Setup(params AccessRight[] rights)
    {
        var api = new StubApi();
        var roster = new RosterSample(api, RosterSample.Vienna, RosterSample.Lena, RosterSample.Marco);
        var user = rights.Length > 0 ? TestData.Owner() with { Rights = rights } : TestData.Owner();
        var context = new TeamContext(api.Client(), user, new RosterTests.RosterClock(), standalone: false);
        return (api, roster, new LeaveAccountViewModel(context, RosterSample.Lena.Id));
    }

    [Fact]
    public async Task Given_kein_Anspruch_When_das_Konto_laedt_Then_zaehlt_es_und_nennt_keinen_Rest()
    {
        var (_, roster, leave) = Setup();
        // Lena arbeitet dienstags: Der Dienstag zählt, der Mittwoch nicht.
        roster.PlanVacation(RosterSample.Lena, RosterSample.Monday.AddDays(8), RosterSample.Monday.AddDays(9));

        await leave.EnsureLoadedAsync();

        Assert.Equal("Urlaubskonto 2027", leave.Heading);
        Assert.Equal("", leave.EntitlementText);
        Assert.Equal("0 Tage", leave.TakenText);
        Assert.Equal("1 Tag", leave.PlannedText);
        Assert.Equal("–", leave.RemainingText);
        Assert.Contains("laut Stammdaten (Di)", leave.Hint, StringComparison.Ordinal);
        Assert.Contains("1 Urlaubseintrag an einem anderen Wochentag zählt nicht.", leave.Hint, StringComparison.Ordinal);
        Assert.Contains("Für 2027 ist kein Anspruch eingetragen", leave.Hint, StringComparison.Ordinal);
        Assert.False(leave.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Anspruch_und_Uebertrag_eingetragen_When_gespeichert_wird_Then_gehen_sie_an_die_API_und_der_Rest_steht_da()
    {
        var (api, roster, leave) = Setup();
        roster.PlanVacation(RosterSample.Lena, RosterSample.Monday.AddDays(1), RosterSample.Monday.AddDays(1));
        await leave.EnsureLoadedAsync();

        leave.EntitlementText = "25";
        leave.CarriedOverText = "2,5";
        Assert.True(leave.HasUnsavedChanges);
        Assert.True(await leave.SaveChangesAsync());

        Assert.Equal(new LeaveEntitlementInput(25, 2.5m), roster.Leave[(RosterSample.Lena.Id, 2027)]);
        Assert.Contains(api.Calls, c => c.Route == $"PUT /api/employees/{RosterSample.Lena.Id}/leave-account/2027");
        Assert.Equal("1 Tag", leave.TakenText);
        Assert.Equal("26,5 Tage", leave.RemainingText);
        Assert.False(leave.IsOverdrawn);
        Assert.False(leave.HasUnsavedChanges);
        Assert.Equal("Gespeichert", leave.Toast.Text);
    }

    [Fact]
    public async Task Given_mehr_Urlaub_als_Anspruch_Then_ist_der_Rest_negativ_und_hervorgehoben()
    {
        var (_, roster, leave) = Setup();
        roster.Leave[(RosterSample.Lena.Id, 2027)] = new LeaveEntitlementInput(1, 0);
        roster.PlanVacation(RosterSample.Lena, RosterSample.Monday.AddDays(8), RosterSample.Monday.AddDays(15));

        await leave.EnsureLoadedAsync();

        Assert.Equal("2 Tage", leave.PlannedText);
        Assert.Equal("-1 Tage", leave.RemainingText);
        Assert.True(leave.IsOverdrawn);
    }

    [Fact]
    public async Task Given_Text_statt_Zahl_When_gespeichert_wird_Then_bleibt_die_Eingabe_und_nichts_geht_an_die_API()
    {
        var (api, _, leave) = Setup();
        await leave.EnsureLoadedAsync();

        leave.EntitlementText = "viel";

        Assert.False(await leave.SaveChangesAsync());
        Assert.Contains("als Zahl", leave.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT", StringComparison.Ordinal));
        leave.Discard();
        Assert.Equal("", leave.EntitlementText);
        Assert.False(leave.HasError);
    }

    [Fact]
    public async Task Given_ein_anderes_Jahr_When_geblaettert_wird_Then_laedt_das_Konto_dieses_Jahr()
    {
        var (api, roster, leave) = Setup();
        roster.Leave[(RosterSample.Lena.Id, 2026)] = new LeaveEntitlementInput(20, 0);
        await leave.EnsureLoadedAsync();

        await leave.PreviousYearCommand.ExecuteAsync(null);

        Assert.Equal("Urlaubskonto 2026", leave.Heading);
        Assert.Equal("20", leave.EntitlementText);
        Assert.Equal("20 Tage", leave.RemainingText);
        Assert.Contains(api.Calls, c => c.Route == $"GET /api/employees/{RosterSample.Lena.Id}/leave-account/2026");
    }

    [Fact]
    public async Task Given_nur_Personalunterlagen_lesen_Then_ist_das_Konto_sichtbar_aber_nicht_aenderbar()
    {
        var (_, _, leave) = Setup(AccessRight.ViewStaffRecords);
        await leave.EnsureLoadedAsync();

        leave.EntitlementText = "30";

        Assert.True(leave.IsReadOnly);
        Assert.False(leave.HasUnsavedChanges);
        Assert.False(leave.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Ohne_Arbeitszeiten_nennt_der_Hinweis_Montag_bis_Freitag()
    {
        var account = new LeaveAccountDto(Guid.NewGuid(), 2027, 25, 0, 0, 0, 25, 0,
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], true);

        Assert.StartsWith("Keine Arbeitszeiten hinterlegt: gezählt wird Urlaub von Montag bis Freitag.", LeaveAccountViewModel.HintFor(account), StringComparison.Ordinal);
    }
}
