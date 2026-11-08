using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 99 (Ä191, Ä250, ADR 0084): Zeiten im Bereich „Team“ (Ansicht „Zeiten“, Reiter „Zeiten“ der Personenkarte). Die Regeln (Überschneidungen, Höchstdauer, Hinweise nach dem
/// Arbeitszeitrecht) prüft die API (<c>TimeTrackingTests</c>); hier zählt, was die App von ihr nimmt, schickt und sagt: Grund als Pflicht am Feld, Folgetag, Zeitzone des Standorts,
/// Entfernen mit Rückfrage, Verlauf, fremde Standorte nur lesbar und Lesen ohne Recht zum Ändern.
/// </summary>
public sealed class TimesheetTests
{
    private static readonly DateOnly Tuesday = RosterSample.Monday.AddDays(1);
    private static readonly EmployeeDto Lena = RosterSample.Lena;
    private static readonly string RecordsRoute = $"/api/locations/{RosterSample.Vienna.Id}/time-records";

    private static async Task<TimesheetViewModel> Times(TeamViewModel team)
    {
        Assert.True(await team.SwitchToAsync(TeamViewModel.TimesMode));
        var times = team.Times!;
        await RosterTests.Until(() => times.IsLoaded);
        return times;
    }

    private static async Task<TimeDayEditorViewModel> OpenDay(TimesheetViewModel times, string person, int dayIndex)
    {
        var row = times.Rows.Single(r => r.Name == person);
        await times.OpenDayAsync(row, row.Cells[dayIndex]);
        return times.Day!;
    }

    private static TimeRecordInput Body(StubApi api, string route) =>
        JsonSerializer.Deserialize<TimeRecordInput>(Assert.Single(api.Calls, c => c.Route == route).Body!, ApiClient.Json)!;

    private static bool WroteRecords(StubApi api) =>
        api.Calls.Any(c => c.Route.Contains("time-records", StringComparison.Ordinal) && !c.Route.StartsWith("GET ", StringComparison.Ordinal));

    // ---------- Übersicht ----------

    [Fact]
    public async Task Given_Zeiten_When_geladen_Then_zeigt_die_Woche_mit_Soll_Ist_Hinweis_und_Wochensumme()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30));
        roster.TimeWarning = "Pause zu kurz.";
        var team = await RosterTests.Loaded(api);

        var times = await Times(team);

        Assert.Equal("Zeiten ab Mo. 1.3.", times.WeekTitle);
        Assert.Equal(7, times.DayHeads.Count);
        var row = Assert.Single(times.Rows);
        Assert.Equal("Lena Huber", row.Name);
        Assert.Equal("8 / 8,5 h", row.HoursText);
        Assert.Equal("8 / 8,5", row.Cells[1].HoursText);
        Assert.True(row.Cells[1].HasRecords);
        Assert.Equal(["Pause zu kurz."], row.Cells[1].Warnings);
        Assert.Contains("Warnung: Pause zu kurz.", row.Cells[1].AutomationName, StringComparison.Ordinal);
        Assert.True(row.Cells[0].HasNoHours);
        Assert.Contains("Österreich (AZG, ARG)", times.Legend, StringComparison.Ordinal);
        Assert.Contains("anklicken", times.Legend, StringComparison.Ordinal);
        Assert.Contains(api.Calls, c => c.Route == $"GET /api/locations/{RosterSample.Vienna.Id}/timesheet?from=2027-03-01");
    }

    [Fact]
    public async Task Given_ein_laufender_Abschnitt_When_geladen_Then_traegt_die_Zelle_den_Lauf_Pfeil()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), null);
        var times = await Times(await RosterTests.Loaded(api));

        var cell = times.Rows[0].Cells[1];

        Assert.True(cell.IsRunning);
        Assert.EndsWith("▶", cell.HoursText, StringComparison.Ordinal);
        Assert.Contains("läuft", cell.AutomationName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_Zeiten_When_die_Woche_gewechselt_wird_Then_laedt_die_Maske_die_neue_Woche_vom_Server()
    {
        var (api, _) = RosterTests.Setup();
        var times = await Times(await RosterTests.Loaded(api));

        await times.NextWeekCommand.ExecuteAsync(null);
        Assert.Equal("Zeiten ab Mo. 8.3.", times.WeekTitle);
        await times.ThisWeekCommand.ExecuteAsync(null);

        Assert.Equal("Zeiten ab Mo. 1.3.", times.WeekTitle);
        Assert.Contains(api.Calls, c => c.Route == $"GET /api/locations/{RosterSample.Vienna.Id}/timesheet?from=2027-03-08");
    }

    [Fact]
    public async Task Given_ein_Serverfehler_bei_den_Zeiten_When_geladen_Then_meldet_die_Maske_den_Fehler_und_zeigt_nicht_leer()
    {
        var (api, _) = RosterTests.Setup();
        api.On($"GET /api/locations/{RosterSample.Vienna.Id}/timesheet?from=2027-03-01", null, HttpStatusCode.InternalServerError);
        var team = await RosterTests.Loaded(api);
        await team.SwitchToAsync(TeamViewModel.TimesMode);
        var times = team.Times!;
        await RosterTests.Until(() => times.IsLoaded);

        Assert.True(times.HasLoadError);
        Assert.False(times.IsEmpty);
        Assert.Empty(times.Rows);
    }

    // ---------- Nachtragen und Ändern ----------

    [Fact]
    public async Task Given_ein_Tag_ohne_Zeiten_When_geoeffnet_Then_ist_Zeit_nachtragen_offen_und_9_bis_17_vorbelegt()
    {
        var (api, _) = RosterTests.Setup();
        var times = await Times(await RosterTests.Loaded(api));

        var day = await OpenDay(times, "Lena Huber", 2);

        Assert.True(day.IsEditing);
        Assert.Equal("Zeit nachtragen", day.EditorTitle);
        Assert.Equal(new TimeOnly(9, 0), day.Start);
        Assert.Equal(new TimeOnly(17, 0), day.End);
        Assert.False(day.HasUnsavedChanges);
        Assert.True(day.HasNoRecords);
        Assert.False(day.CanRemove);
        Assert.Equal("Lena Huber, Mi. 3.3.", day.Heading);
    }

    [Fact]
    public async Task Given_ein_Abschnitt_When_ohne_Grund_gespeichert_Then_Meldung_am_Feld_und_kein_Aufruf()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30));
        var times = await Times(await RosterTests.Loaded(api));
        var day = await OpenDay(times, "Lena Huber", 1);
        day.Records[0].ChangeCommand.Execute(null);
        day.End = new TimeOnly(18, 0);
        Assert.True(day.HasUnsavedChanges);

        var saved = await day.SaveChangesAsync();

        Assert.False(saved);
        Assert.Equal("Grund angeben.", day.ReasonError);
        Assert.True(day.IsEditing);
        Assert.False(WroteRecords(api));
        day.Reason = "  ";
        Assert.False(await day.SaveChangesAsync());
        Assert.Equal("Grund angeben.", day.ReasonError);
        day.Reason = "Gehen vergessen";
        Assert.Null(day.ReasonError);
    }

    [Fact]
    public async Task Given_Ende_vor_Beginn_When_nachgetragen_Then_Ende_am_Folgetag_und_der_Hinweis_steht_da()
    {
        var (api, _) = RosterTests.Setup();
        var times = await Times(await RosterTests.Loaded(api));
        var day = await OpenDay(times, "Lena Huber", 2);
        day.Start = new TimeOnly(22, 0);
        day.End = new TimeOnly(2, 0);
        day.Reason = "Nachtdienst";

        Assert.Equal("Endet am Folgetag.", day.NextDayHint);
        Assert.True(await day.SaveChangesAsync());

        var input = Body(api, $"POST {RecordsRoute}");
        Assert.Equal(Lena.Id, input.EmployeeId);
        Assert.Equal(new DateTimeOffset(2027, 3, 3, 21, 0, 0, TimeSpan.Zero), input.Start);
        Assert.Equal(new DateTimeOffset(2027, 3, 4, 1, 0, 0, TimeSpan.Zero), input.End);
        Assert.Equal("Nachtdienst", input.Reason);
        Assert.False(day.IsEditing);
        Assert.Equal(TimeDayEditorViewModel.Saved, day.StatusText);
        Assert.Equal("22:00–02:00", Assert.Single(day.Records).SpanText);
        Assert.False(times.HasUnsavedChanges);
        Assert.Equal(2, api.Calls.Count(c => c.Route == $"GET /api/locations/{RosterSample.Vienna.Id}/timesheet?from=2027-03-01"));
    }

    [Fact]
    public async Task Given_Ende_nach_Beginn_When_nachgetragen_Then_kein_Hinweis_Folgetag_und_beide_Zeiten_am_selben_Tag()
    {
        var (api, _) = RosterTests.Setup();
        var times = await Times(await RosterTests.Loaded(api));
        var day = await OpenDay(times, "Lena Huber", 2);
        day.Reason = "Stempeln vergessen";

        Assert.Null(day.NextDayHint);
        Assert.True(await day.SaveChangesAsync());

        var input = Body(api, $"POST {RecordsRoute}");
        Assert.Equal(new DateTimeOffset(2027, 3, 3, 8, 0, 0, TimeSpan.Zero), input.Start);
        Assert.Equal(new DateTimeOffset(2027, 3, 3, 16, 0, 0, TimeSpan.Zero), input.End);
    }

    [Fact]
    public async Task Given_ein_Abschnitt_When_geaendert_und_gespeichert_Then_PUT_mit_Grund_und_der_Abschnitt_traegt_korrigiert()
    {
        var (api, roster) = RosterTests.Setup();
        var record = roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30));
        var times = await Times(await RosterTests.Loaded(api));
        var day = await OpenDay(times, "Lena Huber", 1);
        day.Records[0].ChangeCommand.Execute(null);
        Assert.Equal("Abschnitt ändern", day.EditorTitle);
        Assert.Equal(new TimeOnly(17, 30), day.End);
        Assert.True(day.CanRemove);

        day.End = new TimeOnly(18, 15);
        day.Reason = "Gehen vergessen";
        Assert.True(await day.SaveChangesAsync());

        var input = Body(api, $"PUT {RecordsRoute}/{record.Id}");
        Assert.Equal(new DateTimeOffset(2027, 3, 2, 17, 15, 0, TimeSpan.Zero), input.End);
        Assert.Equal("Gehen vergessen", input.Reason);
        var row = Assert.Single(day.Records);
        Assert.Equal("09:00–18:15", row.SpanText);
        Assert.Equal("korrigiert", row.DetailText);
        Assert.Equal("8 / 9,25", times.Rows[0].Cells[1].HoursText);
    }

    [Fact]
    public async Task Given_der_Server_lehnt_ab_When_gespeichert_Then_bleibt_der_Editor_offen_und_die_Maske_meldet_den_Fehler()
    {
        var (api, _) = RosterTests.Setup();
        api.On($"POST {RecordsRoute}", null, HttpStatusCode.BadRequest);
        var times = await Times(await RosterTests.Loaded(api));
        var day = await OpenDay(times, "Lena Huber", 2);
        day.Reason = "Stempeln vergessen";

        Assert.False(await day.SaveChangesAsync());

        Assert.True(day.HasError);
        Assert.True(day.IsEditing);
        Assert.True(day.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_Standort_in_New_York_When_nachgetragen_Then_gilt_die_Zeitzone_des_Standorts_nicht_die_des_Rechners()
    {
        var newYork = new LocationDto(Guid.NewGuid(), "New York", "America/New_York", "USD", []);
        var lena = Lena with { WorkingHours = [new WorkingHours(newYork.Id, DayOfWeek.Tuesday, new(9, 0), new(17, 0))] };
        var api = TeamSample.AddTeam(new StubApi().On("GET /api/qualifications", new List<QualificationDto>()).On("GET /api/locations", new List<LocationDto> { newYork }),
            [lena], [], [], []);
        var roster = new RosterSample(api, newYork, lena);
        var record = roster.AddRecord(lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        var times = await Times(await RosterTests.Loaded(api));
        var day = await OpenDay(times, "Lena Huber", 1);

        Assert.Equal("09:00–17:00", Assert.Single(day.Records).SpanText);
        day.Records[0].ChangeCommand.Execute(null);
        Assert.Equal(new TimeOnly(9, 0), day.Start);
        day.Start = new TimeOnly(8, 0);
        day.Reason = "Früher gekommen";
        Assert.True(await day.SaveChangesAsync());

        var input = Body(api, $"PUT /api/locations/{newYork.Id}/time-records/{record.Id}");
        Assert.Equal(new DateTimeOffset(2027, 3, 2, 13, 0, 0, TimeSpan.Zero), input.Start);
    }

    // ---------- Entfernen ----------

    [Fact]
    public async Task Given_Entfernen_ohne_Grund_When_geklickt_Then_Meldung_am_Feld_und_keine_Rueckfrage()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30));
        var dialogs = new FakeDialogs();
        var times = await Times(await RosterTests.Loaded(api, dialogs: dialogs));
        var day = await OpenDay(times, "Lena Huber", 1);
        day.Records[0].ChangeCommand.Execute(null);

        await day.RemoveCommand.ExecuteAsync(null);

        Assert.Equal("Grund angeben.", day.ReasonError);
        Assert.Empty(dialogs.Asked);
        Assert.False(WroteRecords(api));
    }

    [Fact]
    public async Task Given_Entfernen_mit_Grund_When_bestaetigt_Then_fragt_die_Maske_mit_Zeitspanne_und_Grund_und_DELETE_geht_mit_dem_Grund_raus()
    {
        var (api, roster) = RosterTests.Setup();
        var record = roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var times = await Times(await RosterTests.Loaded(api, dialogs: dialogs));
        var day = await OpenDay(times, "Lena Huber", 1);
        day.Records[0].ChangeCommand.Execute(null);
        day.Reason = "doppelt gestempelt";

        await day.RemoveCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal("Abschnitt entfernen?", asked.Title);
        Assert.Equal("Entfernen", asked.ConfirmText);
        Assert.Contains("09:00–17:30", asked.Message, StringComparison.Ordinal);
        Assert.Contains("„doppelt gestempelt“", asked.Message, StringComparison.Ordinal);
        FakeDialogs.AssertRules(asked);
        var call = Assert.Single(api.Calls, c => c.Route == $"DELETE {RecordsRoute}/{record.Id}");
        Assert.Equal("doppelt gestempelt", JsonSerializer.Deserialize<TimeRecordDeletion>(call.Body!, ApiClient.Json)!.Reason);
        Assert.True(day.HasNoRecords);
        Assert.False(day.IsEditing);
        Assert.Equal(TimeDayEditorViewModel.Removed, day.StatusText);
        Assert.Equal("8 / –", times.Rows[0].Cells[1].HoursText);
    }

    [Fact]
    public async Task Given_Entfernen_mit_Grund_When_abgebrochen_Then_bleibt_der_Abschnitt()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30));
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var times = await Times(await RosterTests.Loaded(api, dialogs: dialogs));
        var day = await OpenDay(times, "Lena Huber", 1);
        day.Records[0].ChangeCommand.Execute(null);
        day.Reason = "doppelt gestempelt";

        await day.RemoveCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Asked);
        Assert.False(WroteRecords(api));
        Assert.Single(day.Records);
        Assert.True(day.IsEditing);
    }

    // ---------- Verlauf ----------

    [Fact]
    public async Task Given_ein_nachgetragener_Abschnitt_When_Verlauf_geklickt_Then_zeigt_die_Maske_das_Protokoll_und_ein_zweiter_Klick_blendet_es_aus()
    {
        var (api, roster) = RosterTests.Setup();
        var record = roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30), TimeRecordSource.Manual);
        var times = await Times(await RosterTests.Loaded(api));
        var day = await OpenDay(times, "Lena Huber", 1);
        var row = day.Records[0];
        Assert.True(row.CanHistory);
        Assert.Equal("nachgetragen", row.DetailText);

        await row.HistoryCommand.ExecuteAsync(null);

        Assert.Equal(["01.03. 10:00 Marco Berger nachgetragen: Stempeln vergessen"], row.History);
        Assert.Contains(api.Calls, c => c.Route == $"GET {RecordsRoute}/{record.Id}/changes");
        await row.HistoryCommand.ExecuteAsync(null);
        Assert.False(row.HasHistory);
    }

    [Fact]
    public async Task Given_ein_selbst_gestempelter_Abschnitt_When_der_Tag_offen_ist_Then_gibt_es_keinen_Verlauf()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30));
        var times = await Times(await RosterTests.Loaded(api));

        var day = await OpenDay(times, "Lena Huber", 1);

        Assert.False(day.Records[0].CanHistory);
        Assert.True(day.Records[0].CanChange);
        Assert.False(day.Records[0].HasDetail);
    }

    // ---------- Fremde Standorte und Rechte ----------

    [Fact]
    public async Task Given_ein_Abschnitt_in_Graz_When_der_Tag_in_Wien_offen_ist_Then_traegt_er_den_Standort_und_ist_nur_lesbar()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(8, 0), new TimeOnly(9, 0), TimeRecordSource.Manual, RosterSample.Graz.Id);
        roster.AddRecord(Lena, Tuesday, new TimeOnly(10, 0), new TimeOnly(17, 0));
        var times = await Times(await RosterTests.Loaded(api));

        var day = await OpenDay(times, "Lena Huber", 1);

        var graz = day.Records.Single(r => r.SpanText == "08:00–09:00");
        Assert.Equal("Graz · nachgetragen", graz.DetailText);
        Assert.False(graz.CanChange);
        Assert.False(graz.CanHistory);
        Assert.True(day.Records.Single(r => r.SpanText == "10:00–17:00").CanChange);
    }

    [Fact]
    public async Task Given_nur_ViewStaffRecords_When_Zeiten_geoeffnet_Then_nur_lesbar_aber_mit_Verlauf()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30), TimeRecordSource.Manual);
        var team = await RosterTests.Loaded(api, RosterTests.WithRights(StaffRole.Backoffice, AccessRight.ViewStaffRecords));
        var times = await Times(team);

        var day = await OpenDay(times, "Lena Huber", 1);

        Assert.False(times.IsEditable);
        Assert.False(day.CanEdit);
        Assert.False(day.CanAdd);
        Assert.False(day.IsEditing);
        Assert.False(day.Records[0].CanChange);
        Assert.True(day.Records[0].CanHistory);
        Assert.DoesNotContain("anklicken", times.Legend, StringComparison.Ordinal);
        await day.Records[0].HistoryCommand.ExecuteAsync(null);
        Assert.True(day.Records[0].HasHistory);
        var empty = await OpenDay(times, "Lena Huber", 2);
        Assert.False(empty.IsEditing);
        day.AddCommand.Execute(null);
        Assert.False(day.IsEditing);
        Assert.False(WroteRecords(api));
    }

    [Fact]
    public async Task Given_nur_ViewCalendar_When_Team_geoeffnet_wird_Then_gibt_es_die_Ansicht_Zeiten_nicht()
    {
        var (api, _) = RosterTests.Setup();

        var team = await RosterTests.Loaded(api, TestData.Reception());

        Assert.Null(team.Times);
        Assert.DoesNotContain(team.Modes, m => m.Id == TeamViewModel.TimesMode);
        Assert.False(await team.SwitchToAsync(TeamViewModel.TimesMode));
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("timesheet", StringComparison.Ordinal));
    }

    // ---------- Ungespeichertes ----------

    [Fact]
    public async Task Given_ein_ungespeicherter_Abschnitt_When_die_Ansicht_gewechselt_wird_Then_fragt_die_Maske_und_bleibt_bei_Abbrechen()
    {
        var (api, _) = RosterTests.Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var team = await RosterTests.Loaded(api, dialogs: dialogs);
        var times = await Times(team);
        var day = await OpenDay(times, "Lena Huber", 2);
        day.Reason = "Stempeln vergessen";
        Assert.True(team.HasUnsavedChanges);

        var switched = await team.SwitchToAsync(TeamViewModel.RosterMode);

        Assert.False(switched);
        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal("Änderungen speichern?", asked.Title);
        Assert.Contains("Zeiten", asked.Message, StringComparison.Ordinal);
        Assert.Equal(TeamViewModel.TimesMode, team.Mode);
        Assert.Same(day, times.Day);
    }

    [Fact]
    public async Task Given_ein_ungespeicherter_Abschnitt_When_die_Woche_gewechselt_wird_Then_fragt_die_Maske_und_die_Woche_bleibt()
    {
        var (api, _) = RosterTests.Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var times = await Times(await RosterTests.Loaded(api, dialogs: dialogs));
        var day = await OpenDay(times, "Lena Huber", 2);
        day.Reason = "Stempeln vergessen";

        await times.NextWeekCommand.ExecuteAsync(null);

        Assert.Equal("Änderungen speichern?", Assert.Single(dialogs.Asked).Title);
        Assert.Equal("Zeiten ab Mo. 1.3.", times.WeekTitle);
        Assert.Same(day, times.Day);
    }

    [Fact]
    public async Task Given_ein_ungespeicherter_Abschnitt_When_verwerfen_gewaehlt_wird_Then_geht_die_Woche_weiter_ohne_Aufruf()
    {
        var (api, _) = RosterTests.Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Alternative);
        var times = await Times(await RosterTests.Loaded(api, dialogs: dialogs));
        var day = await OpenDay(times, "Lena Huber", 2);
        day.Reason = "Stempeln vergessen";

        await times.NextWeekCommand.ExecuteAsync(null);

        Assert.Equal("Zeiten ab Mo. 8.3.", times.WeekTitle);
        Assert.Null(times.Day);
        Assert.False(WroteRecords(api));
    }

    // ---------- Personenkarte ----------

    private static async Task<PersonCardViewModel> CardOf(TeamViewModel team, string person)
    {
        team.SelectedEntry = team.Entries.Single(e => e.Title == person);
        await RosterTests.Until(() => team.Card?.Entry.Title == person);
        var card = team.Card!;
        card.SelectTab(PersonCardViewModel.TimesTab);
        await RosterTests.Until(() => card.Times!.IsLoaded);
        return card;
    }

    [Fact]
    public async Task Given_Lena_gewaehlt_When_der_Reiter_Zeiten_geoeffnet_wird_Then_laedt_er_nur_sie_mit_employeeId()
    {
        var (api, roster) = RosterTests.Setup();
        roster.AddRecord(Lena, Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 30));
        var team = await RosterTests.Loaded(api);

        var card = await CardOf(team, "Lena Huber");

        Assert.Equal("Zeiten", card.Tabs.Single(t => t.Id == PersonCardViewModel.TimesTab).Label);
        Assert.True(card.ShowsTimes);
        var times = card.Times!;
        Assert.True(times.IsCard);
        Assert.False(times.ShowsNames);
        Assert.Equal("Lena Huber", Assert.Single(times.Rows).Name);
        Assert.Contains(api.Calls, c => c.Route == $"GET /api/locations/{RosterSample.Vienna.Id}/timesheet?from=2027-03-01&employeeId={Lena.Id}");
        Assert.False(card.HasSaveBar);
    }

    [Fact]
    public async Task Given_Marco_ohne_Dienst_am_Standort_When_sein_Reiter_Zeiten_offen_ist_Then_laedt_er_nur_ihn_und_zeigt_sieben_leere_Tage()
    {
        var (api, _) = RosterTests.Setup();
        var team = await RosterTests.Loaded(api);

        var card = await CardOf(team, "Marco Berger");

        var row = Assert.Single(card.Times!.Rows);
        Assert.Equal("Marco Berger", row.Name);
        Assert.Equal(7, row.Cells.Count);
        Assert.All(row.Cells, c => Assert.True(c.HasNoHours));
    }

    [Fact]
    public async Task Given_ein_ungespeicherter_Abschnitt_When_eine_andere_Person_gewaehlt_wird_Then_fragt_die_Maske()
    {
        var (api, _) = RosterTests.Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var team = await RosterTests.Loaded(api, dialogs: dialogs);
        var card = await CardOf(team, "Lena Huber");
        var times = card.Times!;
        await times.OpenDayAsync(times.Rows[0], times.Rows[0].Cells[2]);
        times.Day!.Reason = "Stempeln vergessen";
        Assert.True(card.HasUnsavedChanges);
        Assert.True(team.HasUnsavedChanges);
        Assert.False(card.HasSaveBar);

        team.SelectedEntry = team.Entries.Single(e => e.Title == "Marco Berger");
        await RosterTests.Until(() => dialogs.Asked.Count == 1);

        Assert.Equal("Änderungen speichern?", dialogs.Asked[0].Title);
        Assert.Contains("Lena Huber", dialogs.Asked[0].Message, StringComparison.Ordinal);
        Assert.Same(card, team.Card);
    }

    [Fact]
    public async Task Given_ein_ungespeicherter_Abschnitt_When_die_Karte_gespeichert_wird_Then_geht_er_mit_an_die_API()
    {
        var (api, _) = RosterTests.Setup();
        var team = await RosterTests.Loaded(api);
        var card = await CardOf(team, "Lena Huber");
        var times = card.Times!;
        await times.OpenDayAsync(times.Rows[0], times.Rows[0].Cells[2]);
        times.Day!.Reason = "Stempeln vergessen";

        Assert.True(await team.SaveChangesAsync());

        Assert.Equal("Stempeln vergessen", Body(api, $"POST {RecordsRoute}").Reason);
        Assert.False(card.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ein_ungespeicherter_Abschnitt_ohne_Grund_When_die_Karte_gespeichert_wird_Then_bleibt_die_Karte_im_Reiter_Zeiten_mit_Meldung_am_Feld()
    {
        var (api, _) = RosterTests.Setup();
        var team = await RosterTests.Loaded(api);
        var card = await CardOf(team, "Lena Huber");
        var times = card.Times!;
        await times.OpenDayAsync(times.Rows[0], times.Rows[0].Cells[2]);
        times.Day!.Start = new TimeOnly(8, 0);
        card.SelectTab(PersonCardViewModel.DetailsTab);

        Assert.False(await team.SaveChangesAsync());

        Assert.Equal(PersonCardViewModel.TimesTab, card.SelectedTab);
        Assert.Equal("Grund angeben.", times.Day.ReasonError);
        Assert.False(WroteRecords(api));
    }

    // ---------- Sprünge ----------

    [Fact]
    public async Task Given_Zeiten_When_auf_den_Namen_geklickt_wird_Then_oeffnet_die_Karte_im_Reiter_Zeiten()
    {
        var (api, _) = RosterTests.Setup();
        var team = await RosterTests.Loaded(api);
        var times = await Times(team);

        Assert.True(times.Rows[0].CanOpenPerson);
        times.Rows[0].OpenPersonCommand.Execute(null);
        await RosterTests.Until(() => team.ShowsPeople && team.Card?.Title == "Lena Huber");

        Assert.Equal(PersonCardViewModel.TimesTab, team.Card!.SelectedTab);
    }

    [Fact]
    public async Task Given_ein_ungespeicherter_Abschnitt_When_auf_einen_Namen_geklickt_wird_Then_fragt_die_Maske_und_bleibt_in_den_Zeiten()
    {
        var (api, _) = RosterTests.Setup();
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var team = await RosterTests.Loaded(api, dialogs: dialogs);
        var times = await Times(team);
        var day = await OpenDay(times, "Lena Huber", 2);
        day.Reason = "Stempeln vergessen";

        times.Rows[0].OpenPersonCommand.Execute(null);
        await RosterTests.Until(() => dialogs.Asked.Count == 1);

        Assert.Equal(TeamViewModel.TimesMode, team.Mode);
    }

    [Fact]
    public async Task Given_Sprung_zu_den_Zeiten_When_Show_time_gerufen_wird_Then_oeffnet_Team_in_der_Ansicht_Zeiten()
    {
        var (api, _) = RosterTests.Setup();
        var shell = new ShellViewModel(api.Client(), TestData.Owner(), null, new RosterTests.RosterClock(), new ThemeController(new MemorySettingsStore()), "Test",
            () => Task.CompletedTask);

        shell.AdminPage.Show("time");

        Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
        var team = shell.AdminPage.Team!;
        Assert.Equal(TeamViewModel.TimesMode, team.Mode);
        await team.EnsureLoadedAsync();
        await RosterTests.Until(() => team.Times!.IsLoaded);
        Assert.Equal("Lena Huber", Assert.Single(team.Times!.Rows).Name);
        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title == "Zeiten");
    }

    [Fact]
    public async Task Given_Backoffice_When_Show_time_gerufen_wird_Then_oeffnet_Team_in_den_Zeiten_ohne_Personenliste()
    {
        var (api, _) = RosterTests.Setup();
        var shell = new ShellViewModel(api.Client(), RosterTests.WithRights(StaffRole.Backoffice, AccessRight.ViewStaffRecords), null, new RosterTests.RosterClock(),
            new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        shell.AdminPage.Show("time");

        var team = shell.AdminPage.Team!;
        Assert.Equal(TeamViewModel.TimesMode, team.Mode);
        await team.EnsureLoadedAsync();
        await RosterTests.Until(() => team.Times!.IsLoaded);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("GET /api/staff", StringComparison.Ordinal));
    }
}
