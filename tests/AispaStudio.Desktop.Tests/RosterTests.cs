using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 99 (Ä191, Ä250, ADR 0084): Dienstplan, Abwesenheit und Anträge im Bereich „Team“ (Wochenplan, Reiter „Dienstplan und Abwesenheit“ der Personenkarte, Anträge). Die Regeln
/// (Termine außerhalb des Dienstes, Warnungen, Entwurf, Verfügbarkeit im Kalender) prüft die API (<c>RosterTeamTests</c>); hier zählt, was die App von ihr nimmt und sagt.
/// </summary>
public sealed class RosterTests
{
    private static readonly DateOnly Tuesday = RosterSample.Monday.AddDays(1);
    private static readonly bool[] PeopleOnly = [true, false, false, false, false, false, false];

    internal sealed class RosterClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => RosterSample.Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    internal static (StubApi Api, RosterSample Roster) Setup()
    {
        var api = TeamSample.AddTeam(new StubApi()
                .On("GET /api/qualifications", new List<QualificationDto>())
                .On("GET /api/locations", new List<LocationDto> { RosterSample.Vienna, RosterSample.Graz }),
            [RosterSample.Lena, RosterSample.Marco], [], [], []);
        return (api, new RosterSample(api, RosterSample.Vienna, RosterSample.Lena, RosterSample.Marco));
    }

    internal static async Task<TeamViewModel> Loaded(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var team = new TeamViewModel(api.Client(), user ?? TestData.Owner(), new RosterClock()) { Dialogs = dialogs };
        await team.LoadAsync();
        Assert.Null(team.LoadError);
        return team;
    }

    private static async Task<RosterPlanViewModel> Plan(TeamViewModel team)
    {
        Assert.True(await team.SwitchToAsync(TeamViewModel.RosterMode));
        var plan = team.Plan!;
        await Until(() => plan.IsLoaded);
        return plan;
    }

    internal static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static async Task<RosterDayEditorViewModel> OpenDay(RosterPlanViewModel plan, string person, int dayIndex)
    {
        var row = plan.Rows.Single(r => r.Name == person);
        await plan.OpenDayAsync(row, row.Cells[dayIndex]);
        return plan.Day!;
    }

    private static RosterDayInput PutBody(StubApi api, EmployeeDto person, DateOnly date) =>
        JsonSerializer.Deserialize<RosterDayInput>(
            Assert.Single(api.Calls, c => c.Route == $"PUT /api/locations/{RosterSample.Vienna.Id}/roster/{person.Id}/{date:yyyy-MM-dd}").Body!, ApiClient.Json)!;

    internal static CurrentUser WithRights(StaffRole role, params AccessRight[] rights) =>
        new("u9", "x@aispa.test", "Test", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", role, true, []), null, rights);

    // ---------- Wochenplan ----------

    [Fact]
    public async Task Given_Wochenplan_When_geladen_Then_zeigt_er_die_Woche_mit_Diensten_Stunden_und_Namen_aller_Eingeteilten()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);

        var plan = await Plan(team);

        Assert.Equal("Woche ab Mo. 1.3.", plan.WeekTitle);
        Assert.Equal(["Mo. 1.3.", "Di. 2.3.", "Mi. 3.3.", "Do. 4.3.", "Fr. 5.3.", "Sa. 6.3.", "So. 7.3."], plan.Heads.Select(h => h.Text));
        var lena = Assert.Single(plan.Rows, r => r.Name == "Lena Huber");
        Assert.DoesNotContain(plan.Rows, r => r.Name == "Marco Berger");
        Assert.Equal(["09:00–17:00"], lena.Cells[1].Lines);
        Assert.Empty(lena.Cells[0].Lines);
        Assert.False(lena.Cells[1].IsPlanned);
        Assert.Equal("8 / 8 h", lena.HoursText);
        Assert.True(plan.IsEditable);
    }

    [Fact]
    public async Task Given_Wochenplan_When_die_Woche_gewechselt_wird_Then_laedt_die_Maske_die_neue_Woche_vom_Server()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));

        await plan.NextWeekCommand.ExecuteAsync(null);
        Assert.Equal("Woche ab Mo. 8.3.", plan.WeekTitle);
        await plan.ThisWeekCommand.ExecuteAsync(null);

        Assert.Equal("Woche ab Mo. 1.3.", plan.WeekTitle);
        Assert.Contains(api.Calls, c => c.Route == $"GET /api/locations/{RosterSample.Vienna.Id}/roster?from=2027-03-08");
    }

    [Fact]
    public async Task Given_Lena_Dienstag_9_bis_17_When_die_Managerin_12_bis_20_eintippt_und_speichert_Then_geht_genau_dieser_Dienst_an_die_API()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);

        day.Shifts[0].Start = new TimeOnly(12, 0);
        day.Shifts[0].End = new TimeOnly(20, 0);
        day.IsImmediate = true;
        Assert.True(plan.HasUnsavedChanges);
        Assert.True(await day.SaveChangesAsync());

        var body = PutBody(api, RosterSample.Lena, Tuesday);
        var entry = Assert.Single(body.Entries);
        Assert.Equal(new RosterEntry(RosterKind.Work, RosterSample.Vienna.Id, new TimeOnly(12, 0), new TimeOnly(20, 0)), entry);
        Assert.False(body.Draft);
        Assert.Equal(RosterDayEditorViewModel.Saved, day.StatusText);
        Assert.False(plan.HasUnsavedChanges);
        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");
        Assert.Equal(["12:00–20:00"], lena.Cells[1].Lines);
        Assert.True(lena.Cells[1].IsPlanned);
    }

    [Fact]
    public async Task Given_Tag_ohne_Haken_Sofort_gueltig_When_gespeichert_Then_geht_er_als_Entwurf_an_die_API_und_die_Zelle_zeigt_Entwurf()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);

        day.Shifts[0].Start = new TimeOnly(12, 0);
        await day.SaveChangesAsync();

        Assert.True(PutBody(api, RosterSample.Lena, Tuesday).Draft);
        Assert.Equal(RosterDayEditorViewModel.DraftSaved, day.StatusText);
        Assert.True(plan.Rows.Single().Cells[1].IsDraft);
        Assert.Equal("Entwurf: 1 Änderung nicht veröffentlicht", plan.DraftBandText);
        Assert.True(plan.CanPublish);
    }

    [Fact]
    public async Task Given_Urlaub_mit_drei_gebuchten_Terminen_When_gespeichert_Then_warnt_die_Maske_mit_der_Zahl_3()
    {
        var (api, roster) = Setup();
        roster.Outside = 3;
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);

        day.Kinds.Single(k => k.Kind == RosterKind.Vacation).SelectCommand.Execute(null);
        day.IsImmediate = true;
        await day.SaveChangesAsync();

        Assert.Equal(RosterKind.Vacation, Assert.Single(PutBody(api, RosterSample.Lena, Tuesday).Entries).Kind);
        Assert.Equal(3, day.OutsideCount);
        Assert.StartsWith("3 Termine liegen jetzt außerhalb des Dienstes. Bitte im Kalender verschieben:", day.OutsideText, StringComparison.Ordinal);
        Assert.Contains("Gast 1", day.OutsideText, StringComparison.Ordinal);
        Assert.True(day.CanSuggestReplacement);
    }

    [Fact]
    public async Task Given_ein_Termin_betroffen_When_gespeichert_Then_steht_die_Einzahl_in_der_Warnung()
    {
        var (api, roster) = Setup();
        roster.Outside = 1;
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);

        day.Kinds.Single(k => k.Kind == RosterKind.Sick).SelectCommand.Execute(null);
        await day.SaveChangesAsync();

        Assert.StartsWith("Ein Termin liegt jetzt außerhalb des Dienstes.", day.OutsideText, StringComparison.Ordinal);
        Assert.False(PutBody(api, RosterSample.Lena, Tuesday).Draft);
    }

    [Fact]
    public async Task Given_kein_Termin_betroffen_When_gespeichert_Then_gibt_es_keine_Warnung_und_keinen_Ersatzvorschlag()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);

        day.Kinds.Single(k => k.Kind == RosterKind.Vacation).SelectCommand.Execute(null);
        await day.SaveChangesAsync();

        Assert.False(day.HasOutside);
        Assert.False(day.CanSuggestReplacement);
    }

    [Fact]
    public async Task Given_Warnungen_der_API_When_gespeichert_Then_zeigt_die_Maske_sie_unveraendert()
    {
        var (api, roster) = Setup();
        roster.Warnings.Add("Ruhezeit von 11 Stunden unterschritten");
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);

        day.Shifts[0].Start = new TimeOnly(7, 0);
        await day.SaveChangesAsync();

        Assert.Equal("Ruhezeit von 11 Stunden unterschritten", day.WarningsText);
        Assert.Equal(["07:00–17:00"], plan.Rows.Single().Cells[1].Lines);
        Assert.Equal(["Ruhezeit von 11 Stunden unterschritten"], plan.Rows.Single().Cells[1].Warnings);
    }

    [Fact]
    public async Task Given_Ersatz_gewuenscht_When_Ersatz_vorschlagen_geklickt_Then_oeffnet_der_Ausfall_Assistent_fuer_Person_und_Tag()
    {
        var (api, roster) = Setup();
        roster.Outside = 2;
        api.On($"POST /api/locations/{RosterSample.Vienna.Id}/absence/proposal", new AbsenceProposal(RosterSample.Lena.Id, Tuesday, []));
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Kinds.Single(k => k.Kind == RosterKind.Vacation).SelectCommand.Execute(null);
        await day.SaveChangesAsync();

        await day.SuggestReplacementCommand.ExecuteAsync(null);

        Assert.NotNull(day.Assistant);
        Assert.Equal("Ersatz für Lena Huber", day.Assistant.Title);
        var call = Assert.Single(api.Calls, c => c.Route == $"POST /api/locations/{RosterSample.Vienna.Id}/absence/proposal");
        var request = JsonSerializer.Deserialize<AbsenceProposalRequest>(call.Body!, ApiClient.Json)!;
        Assert.Equal(new AbsenceProposalRequest(RosterSample.Lena.Id, Tuesday), request);
    }

    [Fact]
    public async Task Given_ohne_Recht_Termine_zu_buchen_When_Termine_betroffen_sind_Then_gibt_es_keinen_Ersatzvorschlag()
    {
        var (api, roster) = Setup();
        roster.Outside = 2;
        var user = WithRights(StaffRole.Manager, AccessRight.ViewCalendar, AccessRight.ManageLocation);
        var plan = await Plan(await Loaded(api, user));
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Kinds.Single(k => k.Kind == RosterKind.Vacation).SelectCommand.Execute(null);
        await day.SaveChangesAsync();

        Assert.True(day.HasOutside);
        Assert.False(day.CanSuggestReplacement);
    }

    [Fact]
    public async Task Given_Krank_When_die_Art_gewaehlt_wird_Then_ist_Sofort_gueltig_fest_an()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);
        Assert.False(day.IsImmediate);
        Assert.True(day.CanChooseImmediate);

        day.Kinds.Single(k => k.Kind == RosterKind.Sick).SelectCommand.Execute(null);
        day.IsImmediate = false;

        Assert.True(day.IsImmediate);
        Assert.False(day.CanChooseImmediate);
        Assert.True(day.ShowsSickHint);
        day.Kinds.Single(k => k.Kind == RosterKind.Work).SelectCommand.Execute(null);
        Assert.False(day.IsImmediate);
    }

    [Fact]
    public async Task Given_Beginn_nach_Ende_When_gespeichert_Then_Meldung_an_der_Zeile_und_kein_Aufruf()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);

        day.Shifts[0].Start = new TimeOnly(18, 0);
        Assert.False(await day.SaveChangesAsync());

        Assert.Equal("Beginn muss vor Ende liegen.", day.Shifts[0].FieldError);
        Assert.True(day.HasError);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        day.Shifts[0].Start = new TimeOnly(10, 0);
        Assert.Null(day.Shifts[0].FieldError);
    }

    [Fact]
    public async Task Given_ein_Dienst_When_hinzugefuegt_und_entfernt_Then_sind_Nummern_und_Namen_der_Felder_fortlaufend()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);

        day.AddShiftCommand.Execute(null);
        Assert.Equal(["Dienst 1 Beginn", "Dienst 2 Beginn"], day.Shifts.Select(s => s.StartName));
        Assert.Equal(new TimeOnly(17, 0), day.Shifts[1].Start);
        day.Shifts[0].RemoveCommand.Execute(null);

        Assert.Equal("Dienst 1 Standort", Assert.Single(day.Shifts).LocationName);
        Assert.True(day.HasUnsavedChanges);
        day.Discard();
        Assert.Single(day.Shifts);
        Assert.Equal(new TimeOnly(9, 0), day.Shifts[0].Start);
        Assert.False(day.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Tag_nach_Plan_When_Wie_Wochenarbeitszeit_gewaehlt_Then_geht_DELETE_an_die_API_und_der_Tag_zeigt_die_Wochenarbeitszeit()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Shifts[0].Start = new TimeOnly(12, 0);
        day.IsImmediate = true;
        await day.SaveChangesAsync();
        Assert.True(day.CanReset);

        await day.ResetCommand.ExecuteAsync(null);

        Assert.Contains(api.Calls, c => c.Route == $"DELETE /api/locations/{RosterSample.Vienna.Id}/roster/{RosterSample.Lena.Id}/2027-03-02?draft=false");
        Assert.Equal(new TimeOnly(9, 0), day.Shifts[0].Start);
        Assert.False(day.CanReset);
        Assert.Equal(["09:00–17:00"], plan.Rows.Single().Cells[1].Lines);
    }

    // ---------- Entwürfe ----------

    private static async Task<(StubApi Api, RosterSample Roster, RosterPlanViewModel Plan, FakeDialogs Dialogs)> WithDraft(ConfirmChoice? answer = null)
    {
        var (api, roster) = Setup();
        var dialogs = new FakeDialogs();
        if (answer is { } choice)
        {
            dialogs.Answer(choice);
        }

        var plan = await Plan(await Loaded(api, dialogs: dialogs));
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Shifts[0].Start = new TimeOnly(12, 0);
        await day.SaveChangesAsync();
        Assert.Equal(1, plan.DraftCount);
        return (api, roster, plan, dialogs);
    }

    [Fact]
    public async Task Given_Entwuerfe_When_veroeffentlichen_geklickt_Then_meldet_die_Maske_Tage_und_kurzfristige_Aenderungen()
    {
        var (api, roster, plan, _) = await WithDraft();
        roster.ShortNotice.Add("Lena Huber, Di. 2.3.: weniger als 14 Tage vorher (§ 19c Abs. 2 AZG)");

        await plan.PublishCommand.ExecuteAsync(null);

        var call = Assert.Single(api.Calls, c => c.Route == $"POST /api/locations/{RosterSample.Vienna.Id}/roster/publish");
        Assert.Equal(new RosterPublishRequest(RosterSample.Monday, RosterSample.Monday.AddDays(6)), JsonSerializer.Deserialize<RosterPublishRequest>(call.Body!, ApiClient.Json));
        Assert.Contains("1 Tag veröffentlicht; Kalender und Buchung richten sich ab sofort danach.", plan.PublishMessage, StringComparison.Ordinal);
        Assert.Contains("Kurzfristig: Lena Huber, Di. 2.3.: weniger als 14 Tage vorher (§ 19c Abs. 2 AZG)", plan.PublishMessage, StringComparison.Ordinal);
        Assert.Equal(AispaStudio.Desktop.Controls.InfoBarSeverity.Warning, plan.PublishSeverity);
        Assert.Equal(0, plan.DraftCount);
        Assert.False(plan.CanPublish);
        Assert.False(plan.Rows.Single().Cells[1].IsDraft);
    }

    [Fact]
    public async Task Given_Entwuerfe_When_verwerfen_abgebrochen_Then_bleibt_alles()
    {
        var (api, _, plan, dialogs) = await WithDraft(ConfirmChoice.Cancelled);

        await plan.DiscardDraftsCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal("Entwürfe verwerfen?", asked.Title);
        Assert.Equal("1 Entwurf dieser Woche ist danach weg. Der veröffentlichte Stand bleibt.", asked.Message);
        dialogs.AssertRules();
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/roster/discard", StringComparison.Ordinal));
        Assert.Equal(1, plan.DraftCount);
        Assert.True(plan.Rows.Single().Cells[1].IsDraft);
    }

    [Fact]
    public async Task Given_Entwuerfe_When_verwerfen_bestaetigt_Then_geht_discard_an_die_API_und_die_Zelle_zeigt_wieder_die_Wochenarbeitszeit()
    {
        var (api, _, plan, _) = await WithDraft(ConfirmChoice.Confirmed);

        await plan.DiscardDraftsCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == $"POST /api/locations/{RosterSample.Vienna.Id}/roster/discard");
        Assert.Equal(0, plan.DraftCount);
        Assert.Equal(["09:00–17:00"], plan.Rows.Single().Cells[1].Lines);
    }

    // ---------- Rückfragen bei offenem Tag ----------

    [Fact]
    public async Task Given_ein_offener_Tag_mit_Aenderung_When_die_Woche_gewechselt_wird_Then_fragt_die_Maske_und_bleibt_bei_Abbrechen()
    {
        var (api, _) = Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var plan = await Plan(await Loaded(api, dialogs: dialogs));
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Note = "Dienst getauscht";

        await plan.NextWeekCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal("Änderungen speichern?", asked.Title);
        Assert.Contains("Lena Huber, Di. 2.3.", asked.Message, StringComparison.Ordinal);
        Assert.Equal("Woche ab Mo. 1.3.", plan.WeekTitle);
        Assert.Same(day, plan.Day);
    }

    [Fact]
    public async Task Sprung_in_den_Wochenplan_fragt_bei_offenem_Tag_mit_Aenderung()
    {
        // Slice 134 (Ä316): eine Personenzeile des Assistenten verwirft keine offene Änderung im Wochenplan.
        var (api, _) = Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var team = await Loaded(api, dialogs: dialogs);
        var plan = await Plan(team);
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Note = "Dienst getauscht";

        team.Open(new AdminTarget(RosterSample.Lena.Id, RosterSample.Vienna.Id, RosterSample.Monday.AddDays(9), null, null, TeamViewModel.RosterMode));
        await Until(() => dialogs.Asked.Count == 1);

        Assert.Equal("Änderungen speichern?", dialogs.Asked[0].Title);
        Assert.Equal(RosterSample.Monday, plan.WeekStart);
        Assert.Same(day, plan.Day);
    }

    [Fact]
    public async Task Given_ein_offener_Tag_mit_Aenderung_When_ein_anderer_Tag_geoeffnet_und_Verwerfen_gewaehlt_wird_Then_oeffnet_der_andere_Tag()
    {
        var (api, _) = Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Alternative);
        var plan = await Plan(await Loaded(api, dialogs: dialogs));
        var first = await OpenDay(plan, "Lena Huber", 1);
        first.Note = "Dienst getauscht";

        var second = await OpenDay(plan, "Lena Huber", 2);

        Assert.NotSame(first, second);
        Assert.Equal("Lena Huber, Mi. 3.3.", second.Heading);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_ein_offener_Tag_mit_Aenderung_When_die_Ansicht_gewechselt_wird_Then_fragt_die_Maske()
    {
        var (api, _) = Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var team = await Loaded(api, dialogs: dialogs);
        var plan = await Plan(team);
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Note = "Dienst getauscht";
        Assert.True(team.HasUnsavedChanges);
        Assert.Equal("Dienstplan", team.Title);

        var switched = await team.SwitchToAsync(TeamViewModel.PeopleMode);

        Assert.False(switched);
        Assert.Equal("Änderungen speichern?", Assert.Single(dialogs.Asked).Title);
        Assert.Equal(TeamViewModel.RosterMode, team.Mode);
        Assert.Contains("Dienstplan", dialogs.Asked[0].Message, StringComparison.Ordinal);
    }

    // ---------- Personenkarte ----------

    private static async Task<PersonCardViewModel> LenasCard(TeamViewModel team)
    {
        team.SelectedEntry = team.Entries.Single(e => e.Title == "Lena Huber");
        await Until(() => team.Card?.Entry.Title == "Lena Huber");
        var card = team.Card!;
        card.SelectTab(PersonCardViewModel.RosterTab);
        await Until(() => card.Roster!.IsLoaded);
        return card;
    }

    [Fact]
    public async Task Given_Lena_mit_Dienstplanrecht_When_ihre_Karte_offen_ist_Then_hat_sie_den_Reiter_Dienstplan_und_Abwesenheit_vor_dem_Verlauf()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);

        var card = await LenasCard(team);

        Assert.Equal([PersonCardViewModel.DetailsTab, PersonCardViewModel.AccessTab, PersonCardViewModel.QualificationsTab, PersonCardViewModel.RosterTab, PersonCardViewModel.TimesTab, PersonCardViewModel.PayrollTab, PersonCardViewModel.HrFileTab, PersonCardViewModel.RegulationsTab, PersonCardViewModel.DutiesTab, PersonCardViewModel.HistoryTab],
            card.Tabs.Select(t => t.Id));
        Assert.Equal("Dienstplan und Abwesenheit", card.Tabs.Single(t => t.Id == PersonCardViewModel.RosterTab).Label);
        Assert.True(card.ShowsRoster);
        var plan = card.Roster!;
        Assert.True(plan.IsCard);
        Assert.False(plan.ShowsNames);
        Assert.Equal("Lena Huber", Assert.Single(plan.Rows).Name);
        Assert.Contains(api.Calls, c => c.Route == $"GET /api/locations/{RosterSample.Vienna.Id}/roster?from=2027-03-01&employeeId={RosterSample.Lena.Id}");
        Assert.False(plan.CanPublish);
    }

    [Fact]
    public async Task Given_Marco_ohne_Dienst_am_Standort_When_seine_Karte_offen_ist_Then_laedt_sie_nur_ihn_und_zeigt_sieben_leere_Tage()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        team.SelectedEntry = team.Entries.Single(e => e.Title == "Marco Berger");
        await Until(() => team.Card?.Entry.Title == "Marco Berger");

        team.Card!.SelectTab(PersonCardViewModel.RosterTab);
        await Until(() => team.Card!.Roster!.IsLoaded);

        var row = Assert.Single(team.Card.Roster!.Rows);
        Assert.Equal("Marco Berger", row.Name);
        Assert.Equal(7, row.Cells.Count);
        Assert.All(row.Cells, c => Assert.Empty(c.Lines));
    }

    [Fact]
    public async Task Given_ein_offener_Tag_mit_Aenderung_When_eine_andere_Person_gewaehlt_wird_Then_fragt_die_Maske()
    {
        var (api, _) = Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var team = await Loaded(api, dialogs: dialogs);
        var card = await LenasCard(team);
        var plan = card.Roster!;
        await plan.OpenDayAsync(plan.Rows[0], plan.Rows[0].Cells[1]);
        plan.Day!.Note = "Dienst getauscht";
        Assert.True(card.HasUnsavedChanges);
        Assert.True(team.HasUnsavedChanges);
        Assert.False(card.HasSaveBar);

        team.SelectedEntry = team.Entries.Single(e => e.Title == "Marco Berger");
        await Until(() => dialogs.Asked.Count == 1);

        Assert.Equal("Änderungen speichern?", dialogs.Asked[0].Title);
        Assert.Contains("Lena Huber", dialogs.Asked[0].Message, StringComparison.Ordinal);
        Assert.Same(card, team.Card);
    }

    [Fact]
    public async Task Given_ein_offener_Tag_mit_Aenderung_When_die_Karte_gespeichert_wird_Then_geht_der_Tag_mit_an_die_API()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        var card = await LenasCard(team);
        var plan = card.Roster!;
        await plan.OpenDayAsync(plan.Rows[0], plan.Rows[0].Cells[1]);
        plan.Day!.Shifts[0].Start = new TimeOnly(12, 0);
        plan.Day.IsImmediate = true;

        Assert.True(await team.SaveChangesAsync());

        Assert.Equal(new TimeOnly(12, 0), PutBody(api, RosterSample.Lena, Tuesday).Entries[0].Start);
        Assert.DoesNotContain(api.Calls, c => c.Route == $"PUT /api/employees/{RosterSample.Lena.Id}");
        Assert.False(team.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Stammdaten_geaendert_und_Tag_offen_When_die_Karte_gespeichert_wird_Then_zaehlt_die_Fusszeile_und_beides_wird_gespeichert()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        var card = await LenasCard(team);
        var plan = card.Roster!;
        await plan.OpenDayAsync(plan.Rows[0], plan.Rows[0].Cells[1]);
        plan.Day!.Note = "Dienst getauscht";
        card.Details!.Name = "Lena Maria Huber";
        Assert.True(card.HasSaveBar);

        Assert.True(await team.SaveChangesAsync());

        Assert.Single(api.Calls, c => c.Route == $"PUT /api/employees/{RosterSample.Lena.Id}");
        Assert.Equal("Dienst getauscht", JsonSerializer.Deserialize<RosterDayInput>(Assert.Single(api.Calls,
            c => c.Route == $"PUT /api/locations/{RosterSample.Vienna.Id}/roster/{RosterSample.Lena.Id}/2027-03-02").Body!, ApiClient.Json)!.Note);
    }

    [Fact]
    public async Task Given_Entwurf_auf_der_Karte_gespeichert_When_Wochenplan_oeffnen_geklickt_Then_wechselt_die_Maske_zum_Wochenplan()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        var card = await LenasCard(team);
        var plan = card.Roster!;
        await plan.OpenDayAsync(plan.Rows[0], plan.Rows[0].Cells[1]);
        plan.Day!.Shifts[0].Start = new TimeOnly(12, 0);
        await plan.Day.SaveChangesAsync();
        Assert.True(plan.Day.ShowsOpenWeekPlan);

        plan.Day.OpenWeekPlanCommand.Execute(null);
        await Until(() => team.Mode == TeamViewModel.RosterMode);

        Assert.True(team.ShowsRoster);
        Assert.Equal(1, team.Plan!.DraftCount);
    }

    // ---------- Wochenplan → Karte ----------

    [Fact]
    public async Task Given_Wochenplan_When_auf_den_Namen_geklickt_wird_Then_oeffnet_die_Karte_im_Reiter_Dienstplan()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        var plan = await Plan(team);
        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");
        Assert.True(lena.CanOpenPerson);

        lena.OpenPersonCommand.Execute(null);
        await Until(() => team.ShowsPeople && team.Card?.Title == "Lena Huber" && team.Card.SelectedTab == PersonCardViewModel.RosterTab);

        Assert.True(team.Card!.Roster!.IsCard);
        Assert.Equal("Lena Huber", team.SelectedEntry?.Title);
    }

    [Fact]
    public async Task Given_ein_offener_Tag_mit_Aenderung_When_auf_einen_Namen_geklickt_wird_Then_fragt_die_Maske_und_bleibt_im_Wochenplan()
    {
        var (api, _) = Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var team = await Loaded(api, dialogs: dialogs);
        var plan = await Plan(team);
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Note = "Dienst getauscht";

        plan.Rows.Single(r => r.Name == "Lena Huber").OpenPersonCommand.Execute(null);
        await Until(() => dialogs.Asked.Count == 1);

        Assert.Equal(TeamViewModel.RosterMode, team.Mode);
    }

    // ---------- Rechte ----------

    [Fact]
    public async Task Given_nur_ViewCalendar_When_Team_geoeffnet_wird_Then_nur_Wochenplan_und_keine_Bearbeitung()
    {
        var (api, _) = Setup();

        var team = await Loaded(api, TestData.Reception());

        Assert.Equal(["Wochenplan"], team.Modes.Select(m => m.Label));
        Assert.False(team.ShowsModes);
        Assert.Equal(TeamViewModel.RosterMode, team.Mode);
        Assert.Empty(team.Entries);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("GET /api/employees", StringComparison.Ordinal) || c.Route.StartsWith("GET /api/staff", StringComparison.Ordinal)
            || c.Route.StartsWith("GET /api/qualifications", StringComparison.Ordinal));
        var plan = team.Plan!;
        await Until(() => plan.IsLoaded);
        Assert.False(plan.IsEditable);
        var lena = plan.Rows.Single(r => r.Name == "Lena Huber");
        Assert.False(lena.CanOpenPerson);
        Assert.False(lena.Cells[1].OpenCommand.CanExecute(null));
        await plan.OpenDayAsync(lena, lena.Cells[1]);
        Assert.Null(plan.Day);
        Assert.False(plan.CanPublish);
    }

    [Fact]
    public async Task Given_nur_ViewStaffRecords_When_Team_geoeffnet_wird_Then_ist_der_Wochenplan_nur_lesbar_und_die_Zeiten_stehen_daneben()
    {
        var (api, _) = Setup();

        var team = await Loaded(api, WithRights(StaffRole.Backoffice, AccessRight.ViewStaffRecords));
        await Until(() => team.Plan!.IsLoaded);

        Assert.Equal(["Wochenplan", "Zeiten", "Lohn"], team.Modes.Select(m => m.Label));
        Assert.False(team.Plan!.IsEditable);
        Assert.Null(team.Requests);
    }

    [Fact]
    public async Task Given_Standortleitung_mit_ManageLocation_ohne_Stammdaten_When_Team_geoeffnet_wird_Then_Wochenplan_Zeiten_und_Antraege_aber_keine_Personenliste()
    {
        var (api, _) = Setup();

        var team = await Loaded(api, WithRights(StaffRole.Manager, AccessRight.ViewCalendar, AccessRight.ManageLocation));

        Assert.Equal(["Wochenplan", "Zeiten", "Anträge"], team.Modes.Select(m => m.Label));
        Assert.True(team.ShowsModes);
        Assert.Equal(TeamViewModel.RosterMode, team.Mode);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("GET /api/employees", StringComparison.Ordinal));
        await Until(() => team.Plan!.IsLoaded);
        Assert.True(team.Plan!.IsEditable);
        Assert.False(await team.SwitchToAsync(TeamViewModel.PeopleMode));
    }

    [Fact]
    public async Task Given_Inhaberin_When_Team_geoeffnet_wird_Then_Personen_Wochenplan_Zeiten_und_Antraege_in_dieser_Reihenfolge()
    {
        var (api, _) = Setup();

        var team = await Loaded(api);

        Assert.Equal(["Personen", "Wochenplan", "Zeiten", "Anträge", "Lohn", "Wer darf was?", "Zuständigkeiten"], team.Modes.Select(m => m.Label));
        Assert.Equal(TeamViewModel.PeopleMode, team.Mode);
        Assert.Equal(PeopleOnly, team.Modes.Select(m => m.IsOn));
    }

    // ---------- Sprünge ----------

    private static ShellViewModel Shell(StubApi api, CurrentUser user) =>
        new(api.Client(), user, null, new RosterClock(), new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

    [Fact]
    public async Task Given_Sprung_zum_Dienstplan_When_Show_roster_gerufen_wird_Then_oeffnet_Team_im_Wochenplan()
    {
        var (api, _) = Setup();
        var shell = Shell(api, TestData.Owner());

        shell.AdminPage.Show("roster");

        Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
        var team = shell.AdminPage.Team!;
        Assert.Equal(TeamViewModel.RosterMode, team.Mode);
        await team.EnsureLoadedAsync();
        await Until(() => team.Plan!.IsLoaded);
        Assert.Equal("Lena Huber", Assert.Single(team.Plan!.Rows).Name);
        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title == "Dienstplan");
    }

    [Fact]
    public async Task Given_Rezeption_When_Show_roster_gerufen_wird_Then_oeffnet_Team_im_Wochenplan_ohne_Personenliste()
    {
        var (api, _) = Setup();
        var shell = Shell(api, TestData.Reception());

        shell.AdminPage.Show("roster");

        Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
        Assert.Equal(TeamViewModel.RosterMode, shell.AdminPage.Team!.Mode);
        await shell.AdminPage.Team.EnsureLoadedAsync();
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("GET /api/staff", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_Wochenplan_offen_When_ein_Sprung_zu_einer_Person_kommt_Then_wechselt_Team_zu_Personen_und_oeffnet_die_Karte()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        await Plan(team);

        team.Open(RosterSample.Lena.Id, PersonCardViewModel.DetailsTab, null);
        await Until(() => team.ShowsPeople && team.Card?.Title == "Lena Huber");

        Assert.Equal(PersonCardViewModel.DetailsTab, team.Card!.SelectedTab);
    }

    // ---------- Anträge ----------

    private static async Task<AbsenceRequestsViewModel> Requests(TeamViewModel team)
    {
        Assert.True(await team.SwitchToAsync(TeamViewModel.RequestsMode));
        var requests = team.Requests!;
        await Until(() => requests.IsLoaded);
        return requests;
    }

    [Fact]
    public async Task Given_keine_Antrags_Route_When_Antraege_geoeffnet_Then_leer_ohne_Fehler()
    {
        var (api, _) = Setup();

        var requests = await Requests(await Loaded(api));

        Assert.Contains(api.Calls, c => c.Route == "GET /api/absence-requests");
        Assert.True(requests.IsEmpty);
        Assert.False(requests.HasLoadError);
        Assert.Empty(requests.Rows);
    }

    [Fact]
    public async Task Given_ein_Serverfehler_bei_den_Antraegen_When_geoeffnet_Then_meldet_die_Maske_den_Fehler_und_zeigt_nicht_leer()
    {
        var (api, _) = Setup();
        api.On("GET /api/absence-requests", new { title = "kaputt" }, HttpStatusCode.InternalServerError);

        var requests = await Requests(await Loaded(api));

        Assert.True(requests.HasLoadError);
        Assert.False(requests.IsEmpty);
    }

    [Fact]
    public async Task Given_zwei_Antraege_When_Antraege_geoeffnet_Then_stehen_sie_nach_Eingang_mit_Art_Zeitraum_und_Anmerkung_in_der_Liste()
    {
        var (api, _) = Setup();
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto>
        {
            RosterSample.Request(RosterSample.Lena, RosterKind.Vacation, 8, 12, affected: 2, note: "Familienfeier"),
            RosterSample.Request(RosterSample.Marco, RosterKind.Training, 4, 4),
        });

        var requests = await Requests(await Loaded(api));

        Assert.Equal("2 offene Anträge", requests.CountText);
        var lena = requests.Rows.Single(r => r.Name == "Lena Huber");
        Assert.Equal("Urlaub", lena.KindText);
        Assert.Equal("8.3.2027 bis 12.3.2027", lena.PeriodText);
        Assert.Equal("Familienfeier", lena.Note);
        Assert.Equal("In dieser Zeit liegen 2 Termine.", lena.AffectedText);
        var marco = requests.Rows.Single(r => r.Name == "Marco Berger");
        Assert.Equal("4.3.2027", marco.PeriodText);
        Assert.False(marco.HasAffected);
    }

    [Fact]
    public async Task Given_der_Wochenplan_war_schon_offen_When_ein_Antrag_genehmigt_wird_Then_laden_Plan_und_Kalender_neu()
    {
        var (api, _) = Setup();
        var request = RosterSample.Request(RosterSample.Marco, RosterKind.Training, 4, 4);
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto> { request }).On($"POST /api/absence-requests/{request.Id}/approve", null);
        var team = await Loaded(api);
        await Plan(team);
        var calendarTold = 0;
        team.RosterChanged += () => calendarTold++;
        var requests = await Requests(team);
        var loadsBefore = api.Calls.Count(c => c.Route.Contains("/roster", StringComparison.Ordinal));

        await requests.Rows[0].ApproveCommand.ExecuteAsync(null);
        Assert.True(await team.SwitchToAsync(TeamViewModel.RosterMode));

        await Until(() => api.Calls.Count(c => c.Route.Contains("/roster", StringComparison.Ordinal)) > loadsBefore);
        Assert.Equal(1, calendarTold);
    }

    [Fact]
    public async Task Given_ein_Antrag_When_genehmigt_Then_POST_approve_und_Zeile_weg()
    {
        var (api, _) = Setup();
        var request = RosterSample.Request(RosterSample.Marco, RosterKind.Training, 4, 4);
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto> { request }).On($"POST /api/absence-requests/{request.Id}/approve", null);
        var dialogs = new FakeDialogs();
        var requests = await Requests(await Loaded(api, dialogs: dialogs));

        await requests.Rows[0].ApproveCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == $"POST /api/absence-requests/{request.Id}/approve");
        Assert.Empty(requests.Rows);
        Assert.True(requests.IsEmpty);
        Assert.Equal("Schulung von Marco Berger, 4.3.2027 ist genehmigt.", requests.Message);
        Assert.Empty(dialogs.Asked);
    }

    [Fact]
    public async Task Given_ein_Antrag_mit_Terminen_When_genehmigen_Then_Rueckfrage_mit_Zahl()
    {
        var (api, _) = Setup();
        var request = RosterSample.Request(RosterSample.Lena, RosterKind.Vacation, 8, 12, affected: 3);
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto> { request }).On($"POST /api/absence-requests/{request.Id}/approve", null);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        var requests = await Requests(await Loaded(api, dialogs: dialogs));

        await requests.Rows[0].ApproveCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/approve", StringComparison.Ordinal));
        Assert.Single(requests.Rows);
        await requests.Rows[0].ApproveCommand.ExecuteAsync(null);

        Assert.Equal(2, dialogs.Asked.Count);
        Assert.Equal("Abwesenheit genehmigen?", dialogs.Asked[0].Title);
        Assert.StartsWith("3 Termine von Lena Huber liegen in dieser Zeit und müssen danach umgebucht werden.", dialogs.Asked[0].Message, StringComparison.Ordinal);
        Assert.Equal("Genehmigen", dialogs.Asked[0].ConfirmText);
        dialogs.AssertRules();
        Assert.Single(api.Calls, c => c.Route == $"POST /api/absence-requests/{request.Id}/approve");
        Assert.Empty(requests.Rows);
    }

    [Fact]
    public async Task Given_ein_Antrag_When_abgelehnt_Then_POST_reject()
    {
        var (api, _) = Setup();
        var request = RosterSample.Request(RosterSample.Lena, RosterKind.Vacation, 8, 12);
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto> { request }).On($"POST /api/absence-requests/{request.Id}/reject", null);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        var requests = await Requests(await Loaded(api, dialogs: dialogs));

        await requests.Rows[0].RejectCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/reject", StringComparison.Ordinal));
        await requests.Rows[0].RejectCommand.ExecuteAsync(null);

        dialogs.AssertRules();
        Assert.Equal("Antrag ablehnen?", dialogs.Asked[0].Title);
        Assert.Single(api.Calls, c => c.Route == $"POST /api/absence-requests/{request.Id}/reject");
        Assert.Empty(requests.Rows);
        Assert.Equal("Urlaub von Lena Huber, 8.3.2027 bis 12.3.2027 ist abgelehnt.", requests.Message);
    }

    [Fact]
    public async Task Given_ein_Antrag_When_der_Server_die_Entscheidung_ablehnt_Then_bleibt_die_Zeile_und_die_Maske_meldet_den_Fehler()
    {
        var (api, _) = Setup();
        var request = RosterSample.Request(RosterSample.Marco, RosterKind.Training, 4, 4);
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto> { request })
            .On($"POST /api/absence-requests/{request.Id}/approve", new { title = "Der Antrag ist schon entschieden." }, HttpStatusCode.Conflict);
        var requests = await Requests(await Loaded(api, dialogs: new FakeDialogs()));

        await requests.Rows[0].ApproveCommand.ExecuteAsync(null);

        Assert.Single(requests.Rows);
        Assert.True(requests.MessageIsError);
        Assert.NotNull(requests.Message);
    }

    [Fact]
    public async Task Given_ein_Antrag_mit_einem_Termin_When_genehmigen_Then_steht_die_Rueckfrage_in_der_Einzahl()
    {
        var (api, _) = Setup();
        var request = RosterSample.Request(RosterSample.Lena, RosterKind.Vacation, 8, 12, affected: 1);
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto> { request });
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var requests = await Requests(await Loaded(api, dialogs: dialogs));

        await requests.Rows[0].ApproveCommand.ExecuteAsync(null);

        Assert.StartsWith("1 Termin von Lena Huber liegt in dieser Zeit und muss danach umgebucht werden.", dialogs.Asked[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_Antrag_When_ablehnen_Then_verspricht_die_Rueckfrage_keine_Benachrichtigung()
    {
        var (api, _) = Setup();
        var request = RosterSample.Request(RosterSample.Lena, RosterKind.Vacation, 8, 12);
        api.On("GET /api/absence-requests", new List<AbsenceRequestDto> { request });
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var requests = await Requests(await Loaded(api, dialogs: dialogs));

        await requests.Rows[0].RejectCommand.ExecuteAsync(null);

        Assert.Equal("Urlaub 8.3.2027 bis 12.3.2027 von Lena Huber wird nicht genehmigt. Im Dienstplan ändert sich nichts.", dialogs.Asked[0].Message);
    }

    // ---------- Rechte an der Karte ----------

    [Fact]
    public async Task Given_Stammdaten_und_Kalender_aber_kein_Recht_auf_den_Plan_einer_Person_When_die_Karte_oeffnet_Then_fehlt_der_Reiter_und_die_API_wird_nicht_gefragt()
    {
        var (api, _) = Setup();
        var team = await Loaded(api, WithRights(StaffRole.Manager, AccessRight.ManageCatalog, AccessRight.ViewCalendar));

        team.SelectedEntry = team.Entries.Single(e => e.Title == "Lena Huber");
        await Until(() => team.Card?.Entry.Title == "Lena Huber");

        Assert.Null(team.Card!.Roster);
        Assert.DoesNotContain(team.Card.Tabs, t => t.Id == PersonCardViewModel.RosterTab);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/roster?", StringComparison.Ordinal) && c.Route.Contains("employeeId", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(AccessRight.ManageLocation)]
    [InlineData(AccessRight.ViewStaffRecords)]
    public async Task Given_ein_Recht_auf_den_Plan_einer_Person_When_der_Reiter_geoeffnet_wird_Then_laedt_die_Karte_die_Woche(AccessRight right)
    {
        var (api, _) = Setup();
        var team = await Loaded(api, WithRights(StaffRole.Manager, AccessRight.ManageCatalog, right));

        var card = await LenasCard(team);

        Assert.Equal(["09:00–17:00"], card.Roster!.Rows[0].Cells[1].Lines);
        Assert.Null(card.Roster.LoadError);
    }

    [Fact]
    public async Task Given_die_API_lehnt_den_Plan_der_Person_mit_403_ab_When_der_Reiter_geoeffnet_wird_Then_steht_die_Meldung_statt_einer_leeren_Woche()
    {
        var (api, _) = Setup();
        api.On($"GET /api/locations/{RosterSample.Vienna.Id}/roster?from=2027-03-01&employeeId={RosterSample.Lena.Id}", new { title = "Keine Berechtigung." }, HttpStatusCode.Forbidden);
        var team = await Loaded(api);

        var card = await LenasCard(team);

        Assert.True(card.Roster!.HasLoadError);
        Assert.False(card.Roster.IsEmpty);
    }

    // ---------- Offener Tag ----------

    [Fact]
    public async Task Given_ein_Tag_mit_Aenderung_When_dieselbe_Zelle_noch_einmal_geoeffnet_wird_Then_bleibt_die_Eingabe()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Note = "getauscht";

        var again = await OpenDay(plan, "Lena Huber", 1);

        Assert.Same(day, again);
        Assert.Equal("getauscht", again.Note);
        Assert.True(plan.HasUnsavedChanges);
    }

    private static async Task<RosterDayEditorViewModel> DraftOnTuesday(RosterPlanViewModel plan)
    {
        var day = await OpenDay(plan, "Lena Huber", 1);
        day.Shifts[0].Start = new TimeOnly(12, 0);
        Assert.True(await day.SaveChangesAsync());
        Assert.Equal(1, plan.DraftCount);
        return day;
    }

    [Fact]
    public async Task Given_ein_offener_Tag_When_veroeffentlicht_Then_ist_der_Tag_zu_und_keine_Zelle_als_offen_markiert()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        await DraftOnTuesday(plan);

        await plan.PublishCommand.ExecuteAsync(null);

        Assert.Null(plan.Day);
        Assert.DoesNotContain(plan.Rows.SelectMany(r => r.Cells), c => c.IsOpen);
    }

    [Fact]
    public async Task Given_ein_offener_Tag_When_die_Entwuerfe_verworfen_werden_Then_ist_der_Tag_zu()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api, dialogs: new FakeDialogs().Answer(ConfirmChoice.Confirmed)));
        await DraftOnTuesday(plan);

        await plan.DiscardDraftsCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == $"POST /api/locations/{RosterSample.Vienna.Id}/roster/discard");
        Assert.Null(plan.Day);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_ein_offener_Tag_mit_Aenderung_When_die_Rueckfrage_abgebrochen_wird_Then_geht_nichts_an_die_API_und_der_Tag_bleibt(bool publish)
    {
        var (api, _) = Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var plan = await Plan(await Loaded(api, dialogs: dialogs));
        var day = await DraftOnTuesday(plan);
        day.Note = "noch nicht gespeichert";

        await (publish ? plan.PublishCommand : plan.DiscardDraftsCommand).ExecuteAsync(null);

        Assert.Equal("Änderungen speichern?", Assert.Single(dialogs.Asked).Title);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/roster/publish", StringComparison.Ordinal) || c.Route.EndsWith("/roster/discard", StringComparison.Ordinal));
        Assert.Same(day, plan.Day);
        Assert.Equal("noch nicht gespeichert", day.Note);
    }

    // ---------- Ladefehler und Zeitzone ----------

    [Fact]
    public async Task Given_ein_Entwurf_When_die_naechste_Woche_nicht_laedt_Then_gibt_es_nichts_mehr_zu_veroeffentlichen()
    {
        var (api, _) = Setup();
        var plan = await Plan(await Loaded(api));
        await DraftOnTuesday(plan);
        api.On($"GET /api/locations/{RosterSample.Vienna.Id}/roster?from=2027-03-08", new { title = "kaputt" }, HttpStatusCode.InternalServerError);

        await plan.NextWeekCommand.ExecuteAsync(null);

        Assert.True(plan.HasLoadError);
        Assert.Equal(0, plan.DraftCount);
        Assert.False(plan.CanPublish);
        await plan.PublishCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/roster/publish", StringComparison.Ordinal));
    }

    private sealed class SundayEveningClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2027, 3, 7, 20, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    /// <summary>Sonntag 21 Uhr in Wien ist schon Montag 9 Uhr in Auckland: die Woche und der Zeitpunkt richten sich nach dem Standort, nicht nach dem Rechner.</summary>
    [Fact]
    public async Task Given_ein_Standort_in_einer_anderen_Zeitzone_When_der_Plan_oeffnet_Then_gelten_Woche_und_Veroeffentlichung_in_seiner_Zeit()
    {
        var auckland = new LocationDto(Guid.NewGuid(), "Auckland", "Pacific/Auckland", "NZD", []);
        var monday = new DateOnly(2027, 3, 8);
        var api = new StubApi();
        _ = new RosterSample(api, auckland, monday, RosterSample.Marco);
        api.On($"GET /api/locations/{auckland.Id}/roster?from=2027-03-08",
            new RosterWeek(auckland.Id, monday, [.. Enumerable.Range(0, 7).Select(monday.AddDays)], [], 0, new DateTimeOffset(2027, 3, 7, 20, 0, 0, TimeSpan.Zero)));
        var context = new TeamContext(api.Client(), TestData.Owner(), new SundayEveningClock(), false) { Data = new([], [], [], [auckland], [], []) };

        var plan = new RosterPlanViewModel(context, null);
        await plan.LoadAsync();
        Assert.Equal(monday, plan.WeekStart);
        Assert.Contains("8.3.2027 09:00", plan.LastPublishedText, StringComparison.Ordinal);

        await plan.NextWeekCommand.ExecuteAsync(null);
        await plan.ThisWeekCommand.ExecuteAsync(null);
        Assert.Equal(monday, plan.WeekStart);
    }

    // ---------- Karte und Wochenplan bleiben gleich ----------

    private static async Task SaveShift(RosterPlanViewModel plan, int startHour, int endHour)
    {
        var row = plan.Rows.Single(r => r.Name == "Lena Huber");
        await plan.OpenDayAsync(row, row.Cells[1]);
        plan.Day!.Shifts[0].Start = new TimeOnly(startHour, 0);
        plan.Day.Shifts[0].End = new TimeOnly(endHour, 0);
        plan.Day.IsImmediate = true;
        Assert.True(await plan.Day.SaveChangesAsync());
    }

    [Fact]
    public async Task Given_der_Wochenplan_war_schon_offen_When_die_Karte_einen_Dienst_aendert_Then_zeigt_der_Wochenplan_danach_den_neuen_Dienst()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        await Plan(team);
        Assert.True(await team.SwitchToAsync(TeamViewModel.PeopleMode));
        var card = await LenasCard(team);

        await SaveShift(card.Roster!, 12, 18);
        var plan = await Plan(team);

        await Until(() => plan.Rows.Single(r => r.Name == "Lena Huber").Cells[1].Lines.SequenceEqual(["12:00–18:00"]));
    }

    [Fact]
    public async Task Given_die_Karte_war_schon_offen_When_der_Wochenplan_einen_Dienst_aendert_Then_zeigen_die_Karte_und_ihre_Zeiten_danach_den_neuen_Stand()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        var card = await LenasCard(team);
        card.SelectTab(PersonCardViewModel.TimesTab);
        await Until(() => card.Times!.IsLoaded);
        card.SelectTab(PersonCardViewModel.RosterTab);

        await SaveShift(await Plan(team), 12, 18);
        Assert.True(await team.SwitchToAsync(TeamViewModel.PeopleMode));

        await Until(() => card.Roster!.Rows[0].Cells[1].Lines.SequenceEqual(["12:00–18:00"]));
        card.SelectTab(PersonCardViewModel.TimesTab);
        await Until(() => card.Times!.Rows[0].Cells[1].HoursText.StartsWith("6 /", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_die_Zeiten_waren_schon_offen_When_die_Karte_einen_Dienst_aendert_Then_stimmt_danach_das_Soll_in_den_Zeiten()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);
        Assert.True(await team.SwitchToAsync(TeamViewModel.TimesMode));
        await Until(() => team.Times!.IsLoaded);
        Assert.True(await team.SwitchToAsync(TeamViewModel.PeopleMode));
        var card = await LenasCard(team);

        await SaveShift(card.Roster!, 12, 18);
        Assert.True(await team.SwitchToAsync(TeamViewModel.TimesMode));

        await Until(() => team.Times!.Rows.Single(r => r.Name == "Lena Huber").Cells[1].HoursText.StartsWith("6 /", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_der_Wochenplan_war_schon_offen_When_der_Sprung_von_der_Karte_kommt_Then_gelten_Woche_und_Standort_der_Karte()
    {
        var (api, _) = Setup();
        _ = new RosterSample(api, RosterSample.Graz, RosterSample.Monday, RosterSample.Lena, RosterSample.Marco);
        var team = await Loaded(api);
        await Plan(team);
        Assert.True(await team.SwitchToAsync(TeamViewModel.PeopleMode));
        var card = await LenasCard(team);
        var plan = card.Roster!;
        plan.SelectedLocation = plan.Locations.Single(l => l.Id == RosterSample.Graz.Id);
        await plan.NextWeekCommand.ExecuteAsync(null);
        await plan.OpenDayAsync(plan.Rows[0], plan.Rows[0].Cells[1]);
        plan.Day!.Note = "Entwurf in Graz";
        Assert.True(await plan.Day.SaveChangesAsync());

        plan.Day.OpenWeekPlanCommand.Execute(null);
        await Until(() => team.Mode == TeamViewModel.RosterMode);
        var week = team.Plan!;
        await Until(() => api.Calls.Any(c => c.Route == $"GET /api/locations/{RosterSample.Graz.Id}/roster?from=2027-03-08"));

        Assert.Equal(new DateOnly(2027, 3, 8), week.WeekStart);
        Assert.Equal(RosterSample.Graz.Id, week.SelectedLocation?.Id);
        await Until(() => week.DraftCount == 1);
    }

    [Fact]
    public async Task Given_eine_Person_When_der_Sprung_auf_ihren_Reiter_Zeiten_zeigt_Then_oeffnet_die_Karte_und_nicht_die_Uebersicht()
    {
        var (api, _) = Setup();
        var team = await Loaded(api);

        team.Open(RosterSample.Lena.Id, PersonCardViewModel.TimesTab, null);
        await Until(() => team.Card?.SelectedTab == PersonCardViewModel.TimesTab);

        Assert.True(team.ShowsPeople);
        Assert.Equal("Lena Huber", team.Card!.Title);
    }

    [Fact]
    public async Task Sprung_in_den_Wochenplan_zeigt_die_Woche_des_Tages()
    {
        // Slice 134 (Ä316): Personenzeile einer Antwort „Wer arbeitet am …?“ – Woche und Standort des Antworttags, auch wenn Team noch nie offen war.
        var (api, _) = Setup();
        _ = new RosterSample(api, RosterSample.Graz, RosterSample.Monday.AddDays(7), RosterSample.Lena, RosterSample.Marco);
        var shell = Shell(api, TestData.Owner());
        shell.SelectedPage = PageOf(shell, shell.AdminPage);

        shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Roster, "Im Wochenplan zeigen", "roster", null, RosterSample.Graz.Id, new DateOnly(2027, 3, 10),
            TargetId: RosterSample.Lena.Id));
        var team = shell.AdminPage.Team!;
        await team.EnsureLoadedAsync();
        await Until(() => team.Plan?.IsLoaded == true);

        Assert.Equal(TeamViewModel.RosterMode, team.Mode);
        Assert.Equal(new DateOnly(2027, 3, 8), team.Plan!.WeekStart);
        Assert.Equal(RosterSample.Graz.Id, team.Plan.SelectedLocation?.Id);

        // Steht der Wochenplan schon offen, springt er genauso und lädt die Woche.
        shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Roster, "Im Wochenplan zeigen", "roster", null, RosterSample.Vienna.Id, RosterSample.Monday.AddDays(2),
            TargetId: RosterSample.Lena.Id));
        await Until(() => team.Plan.IsLoaded && team.Plan.WeekStart == RosterSample.Monday);
        Assert.Equal(RosterSample.Vienna.Id, team.Plan.SelectedLocation?.Id);
    }

    // ---------- Kalender nach einer Änderung im Dienstplan ----------

    private static async Task<(ShellViewModel Shell, Func<int> CalendarCalls)> ShellOnCalendar(StubApi api)
    {
        var route = $"GET /api/locations/{RosterSample.Vienna.Id}/calendar?date=2027-03-02";
        api.On($"GET /api/locations/{RosterSample.Vienna.Id}/resources", TestData.Resources with { Location = RosterSample.Vienna })
            .On(route, TestData.Day(TestData.Appointment(10, TestData.Anna)));
        var shell = Shell(api, TestData.Owner());
        await shell.Calendar.InitializeAsync();
        await shell.Calendar.LoadAsync();
        return (shell, () => api.Calls.Count(c => c.Route == route));
    }

    private static NavItem PageOf(ShellViewModel shell, object page) => shell.Pages.Single(p => ReferenceEquals(p.Page, page));

    private static async Task<RosterPlanViewModel> PlanInAdmin(ShellViewModel shell)
    {
        shell.SelectedPage = PageOf(shell, shell.AdminPage);
        shell.AdminPage.Show("roster");
        var team = shell.AdminPage.Team!;
        await team.EnsureLoadedAsync();
        await Until(() => team.Plan!.IsLoaded);
        return team.Plan!;
    }

    [Fact]
    public async Task Given_ein_Dienst_wurde_sofort_gueltig_geaendert_When_der_Kalender_wieder_geoeffnet_wird_Then_laedt_er_den_Tag_neu()
    {
        var (api, _) = Setup();
        var (shell, calls) = await ShellOnCalendar(api);
        await SaveShift(await PlanInAdmin(shell), 12, 18);
        var before = calls();

        shell.SelectedPage = PageOf(shell, shell.Calendar);

        await Until(() => calls() == before + 1);
    }

    [Fact]
    public async Task Given_nichts_wurde_geaendert_When_der_Kalender_wieder_geoeffnet_wird_Then_laedt_er_nicht_neu()
    {
        var (api, _) = Setup();
        var (shell, calls) = await ShellOnCalendar(api);
        await PlanInAdmin(shell);
        var before = calls();

        shell.SelectedPage = PageOf(shell, shell.Calendar);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(before, calls());
    }

    [Fact]
    public async Task Given_ein_Termin_ist_offen_When_der_Kalender_nach_einer_Dienstaenderung_neu_laedt_Then_bleibt_das_Panel_offen()
    {
        var (api, _) = Setup();
        var (shell, calls) = await ShellOnCalendar(api);
        shell.Calendar.OpenAppointment(TestData.Appointment(10, TestData.Anna));
        Assert.NotNull(shell.Calendar.Selected);
        await SaveShift(await PlanInAdmin(shell), 12, 18);
        var before = calls();

        shell.SelectedPage = PageOf(shell, shell.Calendar);
        await Until(() => calls() == before + 1);

        Assert.NotNull(shell.Calendar.Selected);
    }
}
