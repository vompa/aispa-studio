using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R07 Slice 142 (Ä350): die lesbare Woche im Dienstplan. Schichten als Chips in der Farbe der Person, Urlaub als Balken über die Tage, Stunden gegen die Wochenarbeitszeit beim Namen,
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

    [Theory]
    [InlineData("#2f6f5e")]
    [InlineData("#7a4fa3")]
    [InlineData("#ffd400")]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    [InlineData("#c0392b")]
    public void Given_eine_Farbe_der_Person_When_der_Chip_gefuellt_wird_Then_hat_die_Zeit_mindestens_4_5_zu_1_Kontrast(string personColor)
    {
        var (fill, ink) = FillFor(personColor, dark: false);

        Assert.True(Contrast(fill, ink) >= 4.5, $"{personColor}: Fläche {fill}, Schrift {ink}, Kontrast {Contrast(fill, ink):0.00}");
    }

    [Theory]
    [InlineData("#2f6f5e")]
    [InlineData("#7a4fa3")]
    [InlineData("#b5524a")]
    [InlineData("#c28a2c")]
    [InlineData("#9a3f6e")]
    [InlineData("#ffd400")]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    [InlineData("#c0392b")]
    public void Given_eine_Farbe_der_Person_When_im_Dunkeln_gefuellt_Then_hat_die_Zeit_mindestens_4_5_zu_1_Kontrast(string personColor)
    {
        var (fill, ink) = FillFor(personColor, dark: true);

        Assert.True(Contrast(fill, ink) >= 4.5, $"{personColor}: Fläche {fill}, Schrift {ink}, Kontrast {Contrast(fill, ink):0.00}");
    }

    [Fact]
    public void Given_die_Farbe_der_Person_When_der_Chip_gefuellt_wird_Then_bleibt_der_Farbton_erkennbar()
    {
        var (light, _) = FillFor("#2f6f5e", dark: false);
        var (dark, _) = FillFor("#2f6f5e", dark: true);

        Assert.NotEqual("#ffffff", light);
        Assert.NotEqual("#2f6f5e", light);
        Assert.NotEqual("#2f6f5e", dark);
        Assert.NotEqual(Token(dark: true, "RosterChipBaseBrush"), dark);
    }

    [Fact]
    public void Given_die_Farbe_der_Person_When_im_Dunkeln_gefuellt_Then_ist_die_Flaeche_dunkler_als_im_Hellen()
    {
        foreach (var personColor in new[] { "#2f6f5e", "#7a4fa3", "#c28a2c", "#ffd400", "#ffffff" })
        {
            var (light, _) = FillFor(personColor, dark: false);
            var (dark, _) = FillFor(personColor, dark: true);

            Assert.True(Luminance(dark) < Luminance(light), $"{personColor}: dunkel {dark} nicht dunkler als hell {light}");
        }
    }

    [Fact]
    public void Given_alle_Kanalwerte_When_hell_gemischt_Then_gleich_der_bisherigen_Aufhellung()
    {
        for (var channel = 0; channel < 256; channel++)
        {
            var old = (int)Math.Round(channel + ((255 - channel) * 0.78));
            var person = $"#{channel:x2}{channel:x2}{channel:x2}";

            var fill = RosterChipColors.Fill(person, Token(dark: false, "RosterChipBaseBrush"), Token(dark: false, "RosterChipInkBrush"), RosterChipColors.ShareLight);

            Assert.Equal($"#{old:x2}{old:x2}{old:x2}", fill);
        }
    }

    [Fact]
    public void Given_eine_ungueltige_Farbe_When_der_Chip_gefuellt_wird_Then_ist_die_Flaeche_die_Basis()
    {
        Assert.Equal("#ffffff", RosterChipColors.Fill("rot", "#ffffff", "#1a2321", RosterChipColors.ShareLight));
        Assert.Equal("#0c1211", RosterChipColors.Fill("", "#0c1211", "#e2eae7", RosterChipColors.ShareDark));
    }

    private static string Token(bool dark, string key) => DesignTokenTests.AppValues(dark, key).Single();

    private static (string Fill, string Ink) FillFor(string personColor, bool dark)
    {
        var ink = Token(dark, "RosterChipInkBrush");
        var share = dark ? RosterChipColors.ShareDark : RosterChipColors.ShareLight;
        return (RosterChipColors.Fill(personColor, Token(dark, "RosterChipBaseBrush"), ink, share), ink);
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        static double Channel(string pair)
        {
            var c = Convert.ToInt32(pair, 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(hex[1..3])) + (0.7152 * Channel(hex[3..5])) + (0.0722 * Channel(hex[5..7]));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }
}
