using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.ReportSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R07 Slice 144 (Ä352): „Zahlen“ hat eine Reiterebene (Tagesstart · Mein Tag · Hygiene · Berichte · Betriebsinfo); Tagesstart, Mein Tag und Hygiene zeigen oben den Status, dann Werte
/// und Listen statt Fließtext. Berichte und Betriebsinfo gibt es nur mit Recht (Ä353).
/// </summary>
public sealed partial class AdminReportsTests
{
    private static readonly LocationDto Opening = StockSample.Vienna with { OpeningHours = [new OpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0))] };

    /// <summary>Wartet in echter Zeit (höchstens 5 s) auf eine Bedingung, die ein nachfragender Reiter im Hintergrund herstellt.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Die Bedingung traf in 5 s nicht ein.");
            await Task.Delay(10);
        }
    }

    private static BriefingStaff OnDuty(string name, string times = "09:00–17:00") => new(Guid.NewGuid(), name, "Dienst", times);

    private static async Task<BriefingReportViewModel> OpenBriefing(BriefingDto briefing, LocationDto? location = null)
    {
        var api = AddReports(new StubApi()).On(BriefingRoute, briefing);
        if (location is not null)
        {
            api.On("GET /api/locations", new List<LocationDto> { location, StockSample.Salzburg });
        }

        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        Assert.True(reports.IsBriefingTab);
        return reports.Briefing;
    }

    private static BriefingDto Quiet() => Briefing() with
    {
        Appointments = 15,
        Utilization = 70,
        Staff = [OnDuty("Lisa"), OnDuty("Ben"), OnDuty("Anna"), OnDuty("Jo"), OnDuty("Eva")],
        ActionNeeded = [],
    };

    [Fact]
    public async Task Given_die_Inhaberin_When_sie_Zahlen_oeffnet_Then_gibt_es_genau_eine_Reiterebene_und_Tagesstart_ist_offen()
    {
        var reports = Reports(AddReports(new StubApi()));
        await reports.EnsureLoadedAsync();

        Assert.Equal(["Tagesstart", "Mein Tag", "Hygiene", "Berichte", "Betriebsinfo"], reports.TabTitles);
        Assert.True(reports.IsBriefingTab);
        Assert.NotNull(reports.BusinessInfo);

        var group = Shell(AddReports(new StubApi())).AdminPage.Groups.Single(g => g.Title == "Zahlen");
        Assert.Equal(["Zahlen"], group.Areas.Select(a => a.Title));
        Assert.False(group.HasTabs);
    }

    [Fact]
    // T-025 (Auftraggeber, 05.11.2026): Wer buchen darf, liest die Team-Einträge der Betriebsinfo; die Stufe „Leitung“ filtert der Server.
    public async Task Given_eine_Rezeptionistin_ohne_Berichtsrecht_When_sie_Zahlen_oeffnet_Then_sieht_sie_die_Betriebsinfo_ohne_Bearbeiten_aber_keine_Berichte()
    {
        var api = AddBusinessInfo(AddReports(new StubApi()));
        var reports = Reports(api, Lead(PersonId));

        reports.Open(new AdminTarget(Section: ReportsViewModel.BusinessInfoTab));
        await reports.EnsureLoadedAsync();

        Assert.Equal(["Tagesstart", "Mein Tag", "Hygiene", "Betriebsinfo"], reports.TabTitles);
        Assert.True(reports.CanSeeBusinessInfo);
        Assert.True(reports.IsBusinessInfoTab);
        var info = Assert.IsType<BusinessInfoViewModel>(reports.BusinessInfo);
        Assert.False(info.CanManage);
        Assert.Contains(api.Calls, c => c.Route == "GET /api/business-contacts?includeArchived=false");
    }

    [Fact]
    public async Task Ein_Konto_nur_mit_Kalenderrecht_beginnt_bei_Mein_Tag()
    {
        var therapist = TestData.Reception(PersonId) with { Rights = [AccessRight.ViewCalendar] };
        var reports = Reports(AddReports(new StubApi()), therapist);

        await reports.EnsureLoadedAsync();

        Assert.Equal(["Mein Tag"], reports.TabTitles);
        Assert.True(reports.IsDayTab);
    }

    [Fact]
    public async Task Berichte_sind_ein_Reiter_mit_Management_und_Pflichtberichten_als_Unterreiter()
    {
        var reports = Reports(AddReports(new StubApi()));
        await reports.EnsureLoadedAsync();

        reports.SelectTabCommand.Execute(ReportsViewModel.ComplianceTab);
        Assert.True(reports.IsReportsTab);
        Assert.True(reports.IsComplianceTab);

        reports.SelectTabCommand.Execute(ReportsViewModel.ManagementTab);
        Assert.True(reports.IsReportsTab);
        Assert.False(reports.IsComplianceTab);
        Assert.False(reports.IsBriefingTab);
    }

    [Fact]
    public async Task Ein_Sprung_zur_Betriebsinfo_oeffnet_Zahlen_beim_Reiter_Betriebsinfo()
    {
        var api = AddBusinessInfo(AddReports(new StubApi()));
        var shell = Shell(api);

        shell.AdminPage.Show("businessInfo");
        await shell.AdminPage.InitializeAsync();

        Assert.Equal("Zahlen", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Reports, shell.AdminPage.Content);
        Assert.True(shell.AdminPage.Reports!.IsBusinessInfoTab);
        Assert.Contains(api.Calls, c => c.Route == "GET /api/business-contacts?includeArchived=false");
    }

    [Fact]
    public async Task Offene_Aenderungen_der_Betriebsinfo_zaehlen_als_ungespeichert_und_gehen_mit_Verwerfen_weg()
    {
        var api = AddBusinessInfo(AddReports(new StubApi()));
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();
        reports.SelectTabCommand.Execute(ReportsViewModel.BusinessInfoTab);
        await reports.BusinessInfo!.EnsureLoadedAsync();
        Assert.False(reports.HasUnsavedChanges);

        await reports.BusinessInfo.NewCommand.ExecuteAsync(null);
        reports.BusinessInfo.Designation = "Installateur";

        Assert.True(reports.HasUnsavedChanges);
        reports.Discard();
        Assert.False(reports.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_heute_15_Behandlungen_5_im_Dienst_und_nichts_Dringendes_When_man_Zahlen_oeffnet_Then_steht_oben_der_Status_und_darunter_die_Werte_15_und_5()
    {
        var briefing = await OpenBriefing(Quiet());

        Assert.Equal("Nichts Dringendes offen", briefing.StatusText);
        Assert.True(briefing.StatusIsOk);
        Assert.Empty(briefing.StatusPoints);
        Assert.Equal([("Behandlungen", "15"), ("Auslastung", "70 %"), ("Im Dienst", "5"), ("Abwesend", "0")], briefing.Values.Select(v => (v.Label, v.Text)));
    }

    [Theory]
    [InlineData(1, "1 Punkt braucht Aufmerksamkeit")]
    [InlineData(2, "2 Punkte brauchen Aufmerksamkeit")]
    public async Task Offene_Punkte_stehen_im_Status_als_Liste(int count, string status)
    {
        var points = Enumerable.Range(1, count).Select(i => new BriefingItem(null, null, $"Punkt {i}")).ToList();

        var briefing = await OpenBriefing(Quiet() with { ActionNeeded = points });

        Assert.Equal(status, briefing.StatusText);
        Assert.False(briefing.StatusIsOk);
        Assert.Equal(points.Select(p => p.Text), briefing.StatusPoints);
        Assert.DoesNotContain(briefing.Sections, s => s.Title == "Handlungsbedarf");
    }

    [Fact]
    public async Task Given_Lena_Huber_hat_Urlaub_When_man_den_Tagesstart_ansieht_Then_steht_sie_in_der_Zeitleiste_mit_Chip_Urlaub()
    {
        var briefing = await OpenBriefing(Quiet() with { Staff = [OnDuty("Lisa"), new BriefingStaff(Guid.NewGuid(), "Lena Huber", "Urlaub", null)] });

        var lena = briefing.StaffBars.Single(s => s.Name == "Lena Huber");
        Assert.True(lena.IsAbsent);
        Assert.Equal("Urlaub", lena.Chip);
        Assert.Empty(lena.Spans);
        Assert.False(briefing.StaffBars.Single(s => s.Name == "Lisa").IsAbsent);
        Assert.Equal([("Im Dienst", "1"), ("Abwesend", "1")], briefing.Values.Skip(2).Select(v => (v.Label, v.Text)));
    }

    [Fact]
    public async Task Die_Zeitleiste_reicht_von_Oeffnung_bis_Schluss_des_Standorts()
    {
        var briefing = await OpenBriefing(Quiet() with { Staff = [OnDuty("Lisa", "09:00–13:00, 14:00–17:00")] }, Opening);

        Assert.Equal(("09:00", "18:00"), (briefing.OpensText, briefing.ClosesText));
        var spans = briefing.StaffBars.Single().Spans;
        Assert.Equal(2, spans.Count);
        Assert.Equal(0d, spans[0].Start, 6);
        Assert.Equal(4d / 9, spans[0].End, 6);
        Assert.Equal(5d / 9, spans[1].Start, 6);
        Assert.Equal(8d / 9, spans[1].End, 6);
    }

    [Fact]
    public async Task Ohne_Oeffnungszeiten_reicht_die_Zeitleiste_vom_ersten_Dienstbeginn_bis_zum_letzten_Dienstende_und_ein_Dienst_ausserhalb_weitet_sie()
    {
        var plain = await OpenBriefing(Quiet() with { Staff = [OnDuty("Lisa", "08:00–12:00"), OnDuty("Ben", "10:00–16:00")] });
        Assert.Equal(("08:00", "16:00"), (plain.OpensText, plain.ClosesText));

        var early = await OpenBriefing(Quiet() with { Staff = [OnDuty("Lisa", "08:00–12:00")] }, Opening);
        Assert.Equal(("08:00", "18:00"), (early.OpensText, early.ClosesText));
    }

    [Fact]
    public async Task Die_Kassa_zeigt_den_Anfangsbestand_als_Wert_und_Hinweise_als_Warnungen()
    {
        var briefing = await OpenBriefing(Quiet() with
        {
            Registers =
            [
                new BriefingRegister(Guid.NewGuid(), "AT1", "EUR", 250m, ["Signatureinheit nicht erreichbar."]),
                new BriefingRegister(Guid.NewGuid(), "DE1", "EUR", null, []),
            ],
        });

        Assert.Equal([("Kassa AT1: Anfangsbestand", "250,00 €"), ("Kassa DE1: Anfangsbestand", "kein Abschluss")], briefing.RegisterValues.Select(v => (v.Label, v.Text)));
        Assert.Equal(["Kassa AT1: Signatureinheit nicht erreichbar."], briefing.RegisterWarnings);
        Assert.DoesNotContain(briefing.Sections, s => s.Title == "Kassa");
    }

    [Fact]
    public async Task Die_Kassa_in_Franken_zeigt_den_Anfangsbestand_mit_CHF_und_ab_1000_den_Tausenderpunkt()
    {
        var briefing = await OpenBriefing(Quiet() with
        {
            Registers =
            [
                new BriefingRegister(Guid.NewGuid(), "CH1", "CHF", 250m, []),
                new BriefingRegister(Guid.NewGuid(), "AT2", "EUR", 1250.50m, []),
            ],
        });

        Assert.Equal([("Kassa CH1: Anfangsbestand", "CHF 250.00"), ("Kassa AT2: Anfangsbestand", "1.250,50 €")], briefing.RegisterValues.Select(v => (v.Label, v.Text)));
        Assert.Empty(briefing.RegisterWarnings);
    }

    [Fact]
    public async Task Ohne_Feld_Registers_stehen_die_Saetze_als_Hinweise()
    {
        // Given ein älterer Server ohne das Feld Registers; Then bleibt kein Satz der Kassa unsichtbar, und die App liest keinen Betrag aus dem Wortlaut
        var briefing = await OpenBriefing(Quiet() with
        {
            Register =
            [
                new BriefingItem(null, null, "Kassa AT1: Anfangsbestand laut letztem Abschluss 250,00 €."),
                new BriefingItem(null, null, "Kassa AT1: Signatureinheit nicht erreichbar."),
            ],
        });

        Assert.Empty(briefing.RegisterValues);
        Assert.Equal(["Kassa AT1: Anfangsbestand laut letztem Abschluss 250,00 €.", "Kassa AT1: Signatureinheit nicht erreichbar."], briefing.RegisterWarnings);
    }

    [Fact]
    public async Task Gaeste_und_Raeume_bleiben_Abschnitte_mit_Zeilen()
    {
        var briefing = await OpenBriefing(Quiet() with
        {
            Guests = [new BriefingItem(null, null, "10:00 Anna Berg: Erstbesuch")],
            Rooms = [new BriefingItem(null, null, "Raum 2 gesperrt")],
        });

        Assert.Equal(["Gäste", "Räume und Geräte"], briefing.Sections.Select(s => s.Title));
        Assert.Equal(["10:00 Anna Berg: Erstbesuch"], briefing.Sections[0].Lines);
    }

    [Fact]
    public async Task Mein_Tag_zeigt_Status_Werte_und_Liste()
    {
        var reports = Reports(AddReports(new StubApi()));
        await reports.EnsureLoadedAsync();

        await reports.DayPlan.ShowAsync(reports.SelectedLocation!, reports.Date!.Value);

        var plan = reports.DayPlan;
        Assert.Equal("1 Hinweis zum Tag", plan.StatusText);
        Assert.False(plan.StatusIsOk);
        Assert.Equal(["Raumwechsel um 11:00"], plan.StatusPoints);
        Assert.Equal([("Behandlungen", "1"), ("Vorbereitung offen", "1"), ("Dienst", "09:00–17:00")], plan.Values.Select(v => (v.Label, v.Text)));
    }

    [Fact]
    public async Task Mein_Tag_ohne_Hinweise_und_ohne_Behandlungen_sagt_es_im_Status()
    {
        var api = AddReports(new StubApi()).On(DayPlanRoute, DayPlan() with { Cards = [], Hints = [], Shifts = [] });
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();

        await reports.DayPlan.ShowAsync(reports.SelectedLocation!, reports.Date!.Value);

        Assert.Equal("Keine Behandlungen heute", reports.DayPlan.StatusText);
        Assert.True(reports.DayPlan.StatusIsOk);
        Assert.Equal([("Behandlungen", "0"), ("Vorbereitung offen", "0"), ("Dienst", "kein Dienst")], reports.DayPlan.Values.Select(v => (v.Label, v.Text)));
    }

    [Fact]
    public async Task Hygiene_zeigt_Status_Werte_und_Liste()
    {
        var reports = Reports(AddReports(new StubApi()));
        await reports.EnsureLoadedAsync();

        await reports.Hygiene.ShowAsync(reports.SelectedLocation!, reports.Date!.Value);

        var hygiene = reports.Hygiene;
        Assert.Equal("2 Punkte offen, 1 überfällig", hygiene.StatusText);
        Assert.False(hygiene.StatusIsOk);
        Assert.Equal([("Räume", "1"), ("Offen", "2"), ("Überfällig", "1"), ("Abgenommen", "0")], hygiene.Values.Select(v => (v.Label, v.Text)));
    }

    [Fact]
    public async Task Hygiene_ohne_offene_Punkte_ist_im_Status_erledigt()
    {
        var api = AddReports(new StubApi()).On(RoomStartRoute, RoomStart(done: true)).On(RoomCareRoute, RoomCare(done: true));
        var reports = Reports(api);
        await reports.EnsureLoadedAsync();

        await reports.Hygiene.ShowAsync(reports.SelectedLocation!, reports.Date!.Value);

        Assert.Equal("Alles erledigt", reports.Hygiene.StatusText);
        Assert.True(reports.Hygiene.StatusIsOk);
        Assert.Equal(("Offen", "0"), (reports.Hygiene.Values[1].Label, reports.Hygiene.Values[1].Text));
    }
}
