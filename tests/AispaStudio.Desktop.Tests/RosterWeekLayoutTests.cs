using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R07 Slice 142 (Ä350): die lesbare Woche im Dienstplan. Schichten als Chips, Urlaub als Balken über die Tage, Stunden gegen die Wochenarbeitszeit beim Namen,
/// heute hervorgehoben, Besetzung je Tag und das Entwurfsband statt der Legende.
/// </summary>
public sealed class RosterWeekLayoutTests
{
    private static readonly DateOnly Monday = RosterSample.Monday;

    /// <summary>Lena arbeitet von Montag bis Freitag 9–17 Uhr in Wien (40 Stunden pro Woche).</summary>
    private static readonly EmployeeDto FullTimeLena = RosterSample.Lena with
    {
        WorkingHours = [.. Enumerable.Range(1, 5).Select(d => new WorkingHours(RosterSample.Vienna.Id, (DayOfWeek)d, new(9, 0), new(17, 0)))],
    };

    private static StubApi Team(params EmployeeDto[] people) =>
        TeamSample.AddTeam(new StubApi()
                .On("GET /api/qualifications", new List<QualificationDto>())
                .On("GET /api/locations", new List<LocationDto> { RosterSample.Vienna, RosterSample.Graz }),
            people, [], [], []);

    private static async Task<(RosterSample Roster, RosterPlanViewModel Plan)> OpenPlan(CurrentUser? user = null, params EmployeeDto[] people)
    {
        people = people.Length == 0 ? [FullTimeLena, RosterSample.Marco] : people;
        var api = Team(people);
        var roster = new RosterSample(api, RosterSample.Vienna, people);
        roster.PlanVacation(FullTimeLena, Monday.AddDays(1), Monday.AddDays(4));
        var team = await RosterTests.Loaded(api, user);
        Assert.True(await team.SwitchToAsync(TeamViewModel.RosterMode));
        await RosterTests.Until(() => team.Plan!.IsLoaded);
        return (roster, team.Plan!);
    }

    [Fact]
    public async Task Given_Lena_Montag_Dienst_und_Dienstag_bis_Freitag_Urlaub_When_die_Woche_geoeffnet_wird_Then_steht_ein_Balken_Urlaub_Di_bis_Fr_und_neben_dem_Namen_8_von_40_h()
    {
        var (_, plan) = await OpenPlan();

        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");

        var bar = Assert.Single(lena.Bars);
        Assert.Equal("Urlaub Di–Fr", bar.Text);
        Assert.Equal(1, bar.Column);
        Assert.Equal(4, bar.Span);
        Assert.Equal("8 / 40 h", lena.HoursText);
        Assert.Equal(["09:00–17:00"], lena.Cells[0].Lines);
        Assert.True(lena.Cells[0].ShowsChips);
        Assert.All(lena.Cells.Skip(1).Take(4), c => Assert.False(c.ShowsChips));
    }

    [Fact]
    public async Task Given_ein_einzelner_Krankheitstag_When_die_Woche_geoeffnet_wird_Then_traegt_der_Balken_nur_die_Art()
    {
        var (roster, plan) = await OpenPlan();
        roster.Plan(FullTimeLena, Monday.AddDays(5), false, new RosterEntry(RosterKind.Sick, null, null, null));
        await plan.LoadCommand.ExecuteAsync(null);

        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");

        Assert.Equal(["Urlaub Di–Fr", "Krank"], lena.Bars.Select(b => b.Text));
        Assert.Equal(5, lena.Bars[1].Column);
        Assert.Equal(1, lena.Bars[1].Span);
    }

    [Fact]
    public async Task Given_eine_Person_ohne_Wochenarbeitszeit_When_sie_Dienst_hat_Then_zeigt_der_Name_nur_die_Stunden()
    {
        var (roster, plan) = await OpenPlan(null, FullTimeLena, RosterSample.Marco);
        roster.Plan(RosterSample.Marco, Monday, false, new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new(10, 0), new(14, 30)));
        await plan.LoadCommand.ExecuteAsync(null);

        var marco = plan.Rows.Single(r => r.Name == "Marco Berger");

        Assert.Equal("4,5 h", marco.HoursText);
    }

    [Fact]
    public async Task Given_freie_Tage_When_die_Woche_geoeffnet_wird_Then_zeigen_sie_ein_schwaches_frei_statt_eines_leeren_Kastens()
    {
        var (_, plan) = await OpenPlan();

        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");

        Assert.False(lena.Cells[0].IsFree);
        Assert.True(lena.Cells[5].IsFree);
        Assert.True(lena.Cells[6].IsFree);
        Assert.False(lena.Cells[5].ShowsChips);
        Assert.False(lena.Cells[1].IsFree);
    }

    [Fact]
    public async Task Given_heute_ist_Dienstag_When_die_Woche_geoeffnet_wird_Then_ist_nur_die_Spalte_von_heute_hervorgehoben()
    {
        var (_, plan) = await OpenPlan();

        Assert.Equal([false, true, false, false, false, false, false], plan.Heads.Select(h => h.IsToday));
        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");
        Assert.Equal([false, true, false, false, false, false, false], lena.Cells.Select(c => c.IsToday));
    }

    [Fact]
    public async Task Given_heute_ist_Dienstag_When_die_Woche_geoeffnet_wird_Then_nennen_Kopf_und_Zellen_von_heute_das_Wort()
    {
        var (_, plan) = await OpenPlan();

        Assert.Contains("Heute", plan.Heads.Single(h => h.IsToday).Label);
        Assert.DoesNotContain(plan.Heads.Where(h => !h.IsToday), h => h.Label.Contains("Heute"));
        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");
        Assert.Contains("heute", lena.Cells[1].AutomationName);
        Assert.DoesNotContain("heute", lena.Cells[0].AutomationName);
        Assert.Contains("heute", plan.Coverage[1].AutomationName);
        Assert.Equal(1, plan.TodayColumn);
        Assert.True(plan.HasToday);
    }

    [Fact]
    public async Task Given_eine_andere_Woche_When_sie_geoeffnet_wird_Then_ist_keine_Spalte_hervorgehoben()
    {
        var (_, plan) = await OpenPlan();

        await plan.NextWeekCommand.ExecuteAsync(null);

        Assert.DoesNotContain(plan.Heads, h => h.IsToday);
    }

    [Fact]
    public async Task Given_zwei_Personen_im_Dienst_When_die_Woche_geoeffnet_wird_Then_zaehlt_die_Zeile_Besetzung_je_Tag_die_Personen_im_Dienst()
    {
        var (roster, plan) = await OpenPlan(null, FullTimeLena, RosterSample.Marco);
        roster.Plan(RosterSample.Marco, Monday, false, new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new(10, 0), new(14, 0)));
        roster.Plan(RosterSample.Marco, Monday.AddDays(1), false, new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new(10, 0), new(14, 0)));
        await plan.LoadCommand.ExecuteAsync(null);

        Assert.True(plan.ShowsCoverage);
        Assert.Equal(["2", "1", "0", "0", "0", "0", "0"], plan.Coverage.Select(c => c.Text));
    }

    [Fact]
    public async Task Given_die_Karte_einer_Person_When_der_Plan_geladen_ist_Then_gibt_es_keine_Zeile_Besetzung_und_die_Stunden_stehen_da()
    {
        var api = Team(FullTimeLena);
        _ = new RosterSample(api, RosterSample.Vienna, FullTimeLena);
        var context = new TeamContext(api.Client(), TestData.Owner(), new FixedTime(RosterSample.Now), false)
        {
            Data = new TeamData([FullTimeLena], [], [], [RosterSample.Vienna], [], []),
        };
        var plan = new RosterPlanViewModel(context, FullTimeLena.Id);

        await plan.LoadAsync();

        Assert.False(plan.ShowsCoverage);
        Assert.Equal("40 / 40 h", plan.Rows.Single().HoursText);
    }

    [Fact]
    public async Task Given_eine_geaenderte_Zelle_When_die_Woche_geoeffnet_wird_Then_traegt_sie_den_Entwurfsrand_und_oben_steht_das_Band()
    {
        var (roster, plan) = await OpenPlan();
        roster.Plan(FullTimeLena, Monday, true, new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new(12, 0), new(20, 0)));
        await plan.LoadCommand.ExecuteAsync(null);

        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");

        Assert.True(lena.Cells[0].IsDraft);
        Assert.False(lena.Cells[1].IsDraft);
        Assert.True(plan.CanPublish);
        Assert.Equal("Entwurf: 1 Änderung nicht veröffentlicht", plan.DraftBandText);
    }

    [Fact]
    public async Task Given_zwei_Entwuerfe_When_das_Band_gezeigt_wird_Then_steht_die_Zahl_in_der_Mehrzahl()
    {
        var (roster, plan) = await OpenPlan();
        roster.Plan(FullTimeLena, Monday, true, new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new(12, 0), new(20, 0)));
        roster.Plan(FullTimeLena, Monday.AddDays(5), true);
        await plan.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Entwurf: 2 Änderungen nicht veröffentlicht", plan.DraftBandText);
    }

    [Fact]
    public async Task Given_kein_Entwurf_When_die_Woche_geoeffnet_wird_Then_zeigt_der_Plan_kein_Band()
    {
        var (_, plan) = await OpenPlan();

        Assert.False(plan.CanPublish);
        Assert.Equal(0, plan.DraftCount);
    }

    [Fact]
    public async Task Given_eine_Rezeptionistin_ohne_Planungsrecht_When_sie_auf_eine_Zelle_klickt_Then_oeffnet_sich_kein_Bearbeiten_und_es_gibt_kein_Band()
    {
        var (roster, plan) = await OpenPlan(TestData.Reception());
        roster.Plan(FullTimeLena, Monday, true, new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new(12, 0), new(20, 0)));
        await plan.LoadCommand.ExecuteAsync(null);
        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");

        await plan.OpenDayAsync(lena, lena.Cells[0]);

        Assert.Null(plan.Day);
        Assert.False(lena.Cells[0].CanOpen);
        Assert.False(plan.CanPublish);
        Assert.False(plan.IsEditable);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }
}
