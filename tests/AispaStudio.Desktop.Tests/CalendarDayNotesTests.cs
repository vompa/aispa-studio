using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Pausen, Abwesenheiten, Vorbereitung und Notizen im Tageskalender.</summary>
public sealed class CalendarDayNotesTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";

    private static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On(CalendarRoute, day);

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public void Krank_gemeldete_Person_bleibt_sichtbar_und_ihre_Termine_sind_zum_Umbuchen_markiert()
    {
        var booked = TestData.Appointment(10, TestData.Ben);
        var done = TestData.Appointment(13, TestData.Ben, AppointmentStatus.Completed);
        var day = TestData.Day(booked, done) with
        {
            Shifts = [new EmployeeShift(TestData.Anna.Id, TestData.At(9), TestData.At(17))],
            Absences = [new CalendarAbsence(TestData.Ben.Id, RosterKind.Sick, TestData.Monday.AddDays(2), null)],
        };

        var layout = DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna);

        var ben = layout.Columns.Single(c => c.Id == TestData.Ben.Id);
        Assert.True(ben.IsSick);
        Assert.Equal("krank gemeldet bis 3.3.", ben.Subtitle);
        Assert.Equal("1 Termin umbuchen", ben.AffectedText);
        Assert.True(ben.Items.Single(i => i.Id == booked.Id).IsAffected);
        Assert.False(ben.Items.Single(i => i.Id == done.Id).IsAffected);
        Assert.Contains("umbuchen", ben.Items.Single(i => i.Id == booked.Id).MetaText, StringComparison.Ordinal);
        // Wer fehlt, zeigt die Spalte; eine Tagesnotiz ist das nicht.
        Assert.Empty(DayLayout.DayNotes(day));
    }

    [Fact]
    public void Vorbereitung_Nachbereitung_und_Raumpflege_sind_Streifen_am_Termin()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var day = TestData.Day(appointment) with { Preparations = [new CalendarPreparation(TestData.MassageTreatment.Id, "Öl anwärmen")] };

        var team = DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna).Columns.Single(c => c.Id == TestData.Anna.Id);
        var rooms = DayLayout.Build(day, TestData.Resources, byRoom: true, Vienna).Columns.Single(c => c.Id == TestData.Room.Id);

        Assert.Equal(["Vorb. 5 Min.", "Nachb. 10 Min."], team.Stripes.Select(s => s.Text));
        Assert.Equal("Vorb. 5 · Öl anwärmen", team.Stripes[0].LongText);
        Assert.Equal(5 * DayLayout.PixelsPerMinute, team.Stripes[0].Height, 3);
        // Nachbereitung und Raumpflege sind in der Raumansicht ein einziger Streifen; der Text steht auch als Hinweis daran.
        Assert.Equal(["Vorb. 5 Min.", "Nachb. 10 · Raumpflege 10 Min."], rooms.Stripes.Select(s => s.Text));
        Assert.Equal(20 * DayLayout.PixelsPerMinute, rooms.Stripes[1].Height, 3);
        Assert.Equal(["Vorbereitung 5 Min.: Öl anwärmen", "Nachbereitung 10 Min. · Raumpflege 10 Min."], rooms.Stripes.Select(s => s.Tip));
        Assert.All(rooms.Stripes, s => Assert.Equal(appointment.Id, s.Appointment!.Id));
        var hover = team.Items.Single().HoverText!;
        Assert.DoesNotContain("Ablauf:", hover, StringComparison.Ordinal);
        Assert.Contains("Vorbereitung: Öl anwärmen", hover, StringComparison.Ordinal);
    }

    [Fact]
    public void Pause_ist_ein_eigener_Block_gesetzlich_markiert_und_die_verlangte_Pause_steht_in_der_Spalte_und_nicht_bei_den_Tagesnotizen()
    {
        var pause = new BlockDto(Guid.NewGuid(), ResourceKind.Employee, TestData.Anna.Id, TestData.At(12), TestData.At(12, 30), "Pause");
        var day = TestData.Day() with
        {
            Blocks = [pause],
            Breaks =
            [
                new CalendarBreak(TestData.Anna.Id, 30, 30, null),
                new CalendarBreak(TestData.Ben.Id, 30, 0, "Keine Zeit mehr für die gesetzliche Pause: 30 Min. bei 8 Std. Dienst."),
            ],
            Notes = [new CalendarNoteDto(Guid.NewGuid(), TestData.Monday, "Lieferung Öle um 11", null, null, null, false, TestData.At(8))],
        };

        var layout = DayLayout.Build(day, TestData.Resources, byRoom: false, Vienna);

        var block = layout.Columns.Single(c => c.Id == TestData.Anna.Id).Items.Single();
        Assert.True(block.IsBreak);
        Assert.True(block.IsLegalBreak);
        Assert.Equal("Pause, gesetzlich", block.Title);
        Assert.Equal(30, layout.Columns.Single(c => c.Id == TestData.Ben.Id).BreakMinutes);
        // Die Warnung zur Pause steht als Meldung in der Seitenleiste, nicht bei den Notizen.
        Assert.Equal("Lieferung Öle um 11", Assert.Single(DayLayout.DayNotes(day)).Text);
    }

    [Fact]
    public async Task Rechtsklick_traegt_Pause_ein_und_B_oeffnet_die_Sperrzeit()
    {
        var api = Api(TestData.Day() with { Breaks = [new CalendarBreak(TestData.Anna.Id, 30, 0, null)] })
            .On("POST /api/blocks", new BlockDto(Guid.NewGuid(), ResourceKind.Employee, TestData.Anna.Id, TestData.At(12), TestData.At(12, 30), "Pause"),
                HttpStatusCode.Created);
        var vm = await OpenAsync(api);
        var noon = vm.Hours.Single(h => h.Label == "12:00").Top + 1;

        var actions = vm.ActionsAt(10, noon);
        Assert.Contains(actions, a => a.Header == "Pause hier (30 Min.)");
        Assert.Contains(actions, a => a.Header == "Notiz hier" && a.Gesture == ",");
        Assert.Contains(actions, a => a.Header == "Sperrzeit hier" && a.Gesture == "B");
        Assert.Contains(actions, a => a.Header.StartsWith("Pause hier", StringComparison.Ordinal) && a.Gesture == "Umschalt+B");

        // „Pause hier“ öffnet das gemeinsame Fenster mit der Pause; eingetragen wird erst mit dem Knopf.
        actions.Single(a => a.Header.StartsWith("Pause hier", StringComparison.Ordinal)).Run!();
        Assert.Equal(CalendarEntryKind.Break, vm.Entry?.Kind);
        Assert.Equal("12:30", vm.Entry!.EndText);
        await vm.Entry.SaveAsync();
        await Until(() => api.Calls.Any(c => c.Route == "POST /api/blocks"));
        var body = api.Calls.Single(c => c.Route == "POST /api/blocks").Body!;
        Assert.Contains("\"reason\":\"Pause\"", body, StringComparison.Ordinal);
        Assert.Contains(TestData.Anna.Id.ToString(), body, StringComparison.Ordinal);

        Assert.True(vm.HandleKey(Key.B, KeyModifiers.None));
        Assert.Equal(CalendarEntryKind.Block, vm.Entry?.Kind);
        // Die Tagesnotiz hat keine Uhrzeit: „.“ öffnet die Seitenleiste „Meldungen“ mit dem Notizfeld (Runde 5, Ä31).
        Assert.True(vm.HandleKey(Key.OemPeriod, KeyModifiers.None));
        Assert.True(vm.MessagesPanel is { IsEditingNote: true });
    }

    [Fact]
    public async Task Notiz_hier_speichert_mit_Spalte_und_Uhrzeit_und_Klick_hakt_ab()
    {
        var note = new CalendarNoteDto(Guid.NewGuid(), TestData.Monday, "Herr Lang ruft zurück", ResourceKind.Employee, TestData.Anna.Id,
            TestData.At(14), false, TestData.At(9));
        var api = Api(TestData.Day() with { Notes = [note] })
            .On($"POST /api/locations/{TestData.LocationId}/calendar-notes", note, HttpStatusCode.Created)
            .On($"PATCH /api/calendar-notes/{note.Id}", note with { Done = true });
        var vm = await OpenAsync(api);

        var sticky = Assert.Single(vm.Columns.Single(c => c.Id == TestData.Anna.Id).Notes);
        Assert.Equal("14:00", sticky.Time);

        vm.OpenNoteAt(10, sticky.Top);
        var entry = vm.Entry!;
        Assert.Equal("14:00", entry.StartText);
        await entry.SaveAsync();
        Assert.Equal("Bitte einen Text eingeben.", entry.Error);
        entry.Text = "Herr Lang ruft zurück";
        await entry.SaveAsync();
        var posted = api.Calls.Single(c => c.Route.StartsWith("POST /api/locations", StringComparison.Ordinal)).Body!;
        Assert.Contains("\"kind\":\"Employee\"", posted, StringComparison.Ordinal);
        Assert.Null(vm.Entry);

        await vm.ToggleNoteAsync(sticky.Note);
        Assert.True(vm.Columns.Single(c => c.Id == TestData.Anna.Id).Notes.Single().Done);
    }

    [Fact]
    public async Task Klick_auf_einen_Zettel_oeffnet_ihn_zum_Bearbeiten_und_Speichern_aendert_Uhrzeit_Ende_Text_und_Erledigt()
    {
        // Auftraggeber, 23.10.2026: Zettel werden wie Termine bearbeitet; „Erledigt“ steht im Panel, nicht mehr am Klick.
        var note = new CalendarNoteDto(Guid.NewGuid(), TestData.Monday, "Herr Lang ruft zurück", ResourceKind.Employee, TestData.Anna.Id,
            TestData.At(14), false, TestData.At(9), TestData.At(14, 15));
        var api = Api(TestData.Day() with { Notes = [note] })
            .On($"PATCH /api/calendar-notes/{note.Id}", note with { Text = "Herr Lang hat angerufen", Done = true });
        var vm = await OpenAsync(api);
        var sticky = Assert.Single(vm.Columns.Single(c => c.Id == TestData.Anna.Id).Notes);

        vm.OpenNote(sticky.Note);

        var entry = vm.Entry!;
        Assert.True(entry.IsEditing);
        Assert.False(vm.NewEntry!.CanSwitch);
        Assert.Equal(("Notiz bearbeiten", "Speichern", "14:00", "14:15", "Herr Lang ruft zurück", false, true),
            (entry.Title, entry.SaveText, entry.StartText, entry.EndText, entry.Text, entry.Done, entry.ShowDone));
        Assert.Equal("Details öffnen", vm.ActionsFor(sticky.Note)[0].Header);

        entry.StartText = "15:00";
        entry.EndText = "15:30";
        entry.Text = "Herr Lang hat angerufen";
        entry.Done = true;
        await entry.SaveAsync();

        var patched = api.Calls.Single(c => c.Route == $"PATCH /api/calendar-notes/{note.Id}").Body!;
        Assert.Contains("\"text\":\"Herr Lang hat angerufen\"", patched, StringComparison.Ordinal);
        Assert.Contains("\"done\":true", patched, StringComparison.Ordinal);
        Assert.Contains("\"at\":\"2027-03-01T14:00:00+00:00\"", patched, StringComparison.Ordinal);
        Assert.Contains("\"until\":\"2027-03-01T14:30:00+00:00\"", patched, StringComparison.Ordinal);
        Assert.Null(vm.Entry);
    }

    private static readonly CalendarNoteDto ClosedDayNote = new(Guid.NewGuid(), TestData.Monday, "Handwerker kommen", ResourceKind.Employee, TestData.Anna.Id,
        TestData.At(10, 45), false, TestData.At(8), TestData.At(11));

    private static readonly BlockDto ClosedDayPause = new(Guid.NewGuid(), ResourceKind.Employee, TestData.Anna.Id, TestData.At(12), TestData.At(12, 30), "Pause");

    private static CalendarDay ClosedDay() => TestData.Day(TestData.Appointment(10, TestData.Anna)) with { Opening = [] };

    /// <summary>Schließtag ohne Einträge, ein Termin und die Leiste „Meldungen“ mit Filter stehen als Reiter bereit, sollen nach dem Speichern aber nicht aufgehen.</summary>
    private static async Task<(CalendarViewModel Vm, StubApi Api)> ClosedDayWithOpenTabsAsync()
    {
        var day = ClosedDay();
        var api = Api(day)
            .On($"POST /api/locations/{TestData.LocationId}/calendar-notes", ClosedDayNote, HttpStatusCode.Created)
            .On("POST /api/blocks", ClosedDayPause, HttpStatusCode.Created);
        var vm = await OpenAsync(api);
        vm.OpenAppointment(day.Appointments.Single());
        vm.OpenMessagesFor(vm.Columns.Single(c => c.Id == TestData.Anna.Id));
        return (vm, api);
    }

    private static CalendarColumn Anna(CalendarViewModel vm) => vm.Columns.Single(c => c.Id == TestData.Anna.Id);

    [Fact]
    public async Task Given_ein_Schliesstag_When_eine_Notiz_gespeichert_wird_Then_steht_sie_im_Kalender_und_es_geht_nichts_Fremdes_auf()
    {
        var (vm, api) = await ClosedDayWithOpenTabsAsync();
        vm.OpenNoteAt(10, vm.Hours.Single(h => h.Label == "10:00").Top + 1);
        vm.Entry!.Text = "Handwerker kommen";
        vm.Entry.StartText = "10:45";
        vm.Entry.EndText = "11:00";
        Assert.Empty(Anna(vm).Notes);
        api.On(CalendarRoute, ClosedDay() with { Notes = [ClosedDayNote] });

        await vm.Entry.SaveAsync();

        Assert.Contains(api.Calls, c => c.Route == $"POST /api/locations/{TestData.LocationId}/calendar-notes");
        Assert.Equal("Handwerker kommen", Assert.Single(Anna(vm).Notes).Note.Text);
        Assert.Equal("Notiz gespeichert", vm.Toast);
        Assert.Null(vm.Selected);
        Assert.Null(vm.MessagesPanel);
        Assert.Null(vm.ActivePanel);
    }

    [Fact]
    public async Task Given_ein_Schliesstag_When_eine_Pause_eingetragen_wird_Then_oeffnet_sich_die_Leiste_Meldungen_nicht()
    {
        var (vm, api) = await ClosedDayWithOpenTabsAsync();
        vm.ActionsAt(10, vm.Hours.Single(h => h.Label == "12:00").Top + 1)
            .Single(a => a.Header.StartsWith("Pause hier", StringComparison.Ordinal)).Run!();
        Assert.Equal(CalendarEntryKind.Break, vm.Entry?.Kind);
        Assert.DoesNotContain(Anna(vm).Items, i => i.IsBreak);
        api.On(CalendarRoute, ClosedDay() with { Blocks = [ClosedDayPause] });

        await vm.Entry!.SaveAsync();

        Assert.Contains(Anna(vm).Items, i => i.IsBreak);
        Assert.Null(vm.MessagesPanel);
        Assert.Null(vm.Selected);
        Assert.False(vm.HasPanel);
    }

    [Fact]
    public async Task Given_eine_gespeicherte_Notiz_When_der_Kalender_neu_geladen_wird_Then_steht_sie_weiter_dort()
    {
        var (vm, api) = await ClosedDayWithOpenTabsAsync();
        vm.OpenNoteAt(10, vm.Hours.Single(h => h.Label == "10:00").Top + 1);
        vm.Entry!.Text = "Handwerker kommen";
        api.On(CalendarRoute, ClosedDay() with { Notes = [ClosedDayNote] });
        await vm.Entry.SaveAsync();

        await vm.LoadAsync();

        Assert.Equal("Handwerker kommen", Assert.Single(Anna(vm).Notes).Note.Text);
    }

    [Fact]
    public async Task Notiz_loeschen_fragt_zuerst_nach_und_Abbrechen_laesst_sie_stehen()
    {
        var note = new CalendarNoteDto(Guid.NewGuid(), TestData.Monday, "Herr Lang ruft zurück", ResourceKind.Employee, TestData.Anna.Id,
            TestData.At(14), false, TestData.At(9));
        var api = Api(TestData.Day() with { Notes = [note] })
            .On($"DELETE /api/calendar-notes/{note.Id}", null, HttpStatusCode.NoContent);
        var vm = await OpenAsync(api);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        vm.Dialogs = dialogs;
        var delete = vm.ActionsFor(note).Single(a => a.Header == "Notiz löschen");
        Assert.True(delete.IsDanger);

        delete.Run!();
        Assert.Equal("Notiz löschen?", dialogs.Asked[0].Title);
        Assert.Contains("Herr Lang ruft zurück", dialogs.Asked[0].Message, StringComparison.Ordinal);
        dialogs.AssertRules();
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.Single(vm.Columns.Single(c => c.Id == TestData.Anna.Id).Notes);

        delete.Run!();
        for (var i = 0; i < 50 && vm.Columns.Single(c => c.Id == TestData.Anna.Id).Notes.Count > 0; i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Contains(api.Calls, c => c.Route == $"DELETE /api/calendar-notes/{note.Id}");
        Assert.Empty(vm.Columns.Single(c => c.Id == TestData.Anna.Id).Notes);
    }

    [Fact]
    public async Task Ausfall_Assistent_schlaegt_Ersatz_vor_und_uebernimmt_nur_die_gewaehlten()
    {
        var first = TestData.Appointment(10, TestData.Ben);
        var second = TestData.Appointment(14, TestData.Ben);
        var day = TestData.Day(first, second) with
        {
            Shifts = [new EmployeeShift(TestData.Anna.Id, TestData.At(9), TestData.At(17))],
            Absences = [new CalendarAbsence(TestData.Ben.Id, RosterKind.Sick, TestData.Monday, null)],
        };
        var moved = first with { EmployeeId = TestData.Anna.Id, UpdatedAt = TestData.At(10, 5) };
        var api = Api(day)
            .On($"POST /api/locations/{TestData.LocationId}/absence/proposal", new AbsenceProposal(TestData.Ben.Id, TestData.Monday,
            [
                new AbsenceProposalItem(first, AbsenceOption.SameTime, first.Start, TestData.Anna.Id, "Anna", TestData.Room.Id, "Raum Zeder",
                    "Anna hat die Qualifikation und ist um 10:00 frei; Raum Zeder bleibt."),
                new AbsenceProposalItem(second, AbsenceOption.None, null, null, null, null, null, "Bitte den Gast kontaktieren."),
            ]))
            .On($"POST /api/locations/{TestData.LocationId}/absence/apply", new AbsenceApplyResult([moved], [], 1));
        var vm = await OpenAsync(api);

        var ben = vm.Columns.Single(c => c.Id == TestData.Ben.Id);
        Assert.Equal("2 Termine umbuchen", ben.AffectedText);
        Assert.Contains(vm.ActionsForColumn(ben), a => a.Header == "Ersatz für die Termine suchen");
        Assert.DoesNotContain(vm.ActionsForColumn(ben), a => a.Header.EndsWith("krank melden", StringComparison.Ordinal));
        vm.OpenAbsence(ben);
        var assistant = vm.Absence!;
        await Until(() => assistant.Rows.Count == 2);

        Assert.True(assistant.Rows[0].IsChosen);
        Assert.Equal("gleiche Zeit bei Anna, Raum Zeder", assistant.Rows[0].After);
        Assert.False(assistant.Rows[1].CanApply);
        await assistant.ApplyAsync();

        var body = api.Calls.Single(c => c.Route.EndsWith("/absence/apply", StringComparison.Ordinal)).Body!;
        Assert.Contains(first.Id.ToString(), body, StringComparison.Ordinal);
        Assert.DoesNotContain(second.Id.ToString(), body, StringComparison.Ordinal);
        Assert.Contains("\"notifyCustomers\":true", body, StringComparison.Ordinal);
        Assert.Equal("1 Termin umgebucht, 1 Gast per Mail informiert.", assistant.Summary);
        Assert.Equal("Verschoben", assistant.Rows[0].Outcome);
    }

    [Fact]
    public async Task Ausfall_Assistent_zeigt_andere_Standorte_getrennt_und_waehlt_sie_nicht_vor()
    {
        var own = TestData.Appointment(10, TestData.Ben);
        var elsewhere = TestData.Appointment(14, TestData.Ben);
        var baden = Guid.NewGuid();
        var day = TestData.Day(own, elsewhere) with
        {
            Shifts = [new EmployeeShift(TestData.Anna.Id, TestData.At(9), TestData.At(17))],
            Absences = [new CalendarAbsence(TestData.Ben.Id, RosterKind.Sick, TestData.Monday, null)],
        };
        var moved = elsewhere with { LocationId = baden, UpdatedAt = TestData.At(10, 5) };
        var api = Api(day)
            .On($"POST /api/locations/{TestData.LocationId}/absence/proposal", new AbsenceProposal(TestData.Ben.Id, TestData.Monday,
            [
                new AbsenceProposalItem(own, AbsenceOption.SameTime, own.Start, TestData.Anna.Id, "Anna", TestData.Room.Id, "Raum Zeder",
                    "Anna hat die Qualifikation und ist um 10:00 frei; Raum Zeder bleibt."),
                new AbsenceProposalItem(elsewhere, AbsenceOption.OtherLocation, elsewhere.Start, Guid.NewGuid(), "Clara", Guid.NewGuid(), "Raum C",
                    "In Wien ist an diesem Tag niemand frei. In Baden geht es zur gleichen Zeit um 14:00 bei Clara in Raum C; nur nach Rücksprache mit dem Gast übernehmen.",
                    baden, "Baden"),
            ]))
            .On($"POST /api/locations/{TestData.LocationId}/absence/apply", new AbsenceApplyResult([moved], [], 0));
        var vm = await OpenAsync(api);

        vm.OpenAbsence(vm.Columns.Single(c => c.Id == TestData.Ben.Id));
        var assistant = vm.Absence!;
        await Until(() => assistant.HasRows);

        Assert.Single(assistant.Rows);
        var other = Assert.Single(assistant.OtherRows);
        Assert.True(assistant.HasOtherRows);
        Assert.Equal("in Baden zur gleichen Zeit bei Clara, Raum C", other.After);
        Assert.True(other.CanApply);
        Assert.False(other.IsChosen);

        // Ohne Häkchen wird nur der eigene Standort übernommen; mit Häkchen geht der Zielstandort in die Anfrage.
        await assistant.ApplyAsync();
        var first = api.Calls.Single(c => c.Route.EndsWith("/absence/apply", StringComparison.Ordinal)).Body!;
        Assert.DoesNotContain(elsewhere.Id.ToString(), first, StringComparison.Ordinal);

        other.IsChosen = true;
        await assistant.ApplyAsync();
        var second = api.Calls.Last(c => c.Route.EndsWith("/absence/apply", StringComparison.Ordinal)).Body!;
        Assert.Contains(elsewhere.Id.ToString(), second, StringComparison.Ordinal);
        Assert.Contains($"\"locationId\":\"{baden}\"", second, StringComparison.Ordinal);
        Assert.Equal("Verschoben", other.Outcome);
    }

    [Fact]
    public async Task Standortleitung_meldet_krank_und_der_Ausfall_Assistent_oeffnet_sich()
    {
        var open = TestData.Appointment(10, TestData.Ben);
        var api = Api(TestData.Day(open))
            .On($"PUT /api/locations/{TestData.LocationId}/roster/{TestData.Ben.Id}/2027-03-01",
                new RosterDayResult(new RosterCell(TestData.Monday, RosterSource.Plan, [new RosterEntry(RosterKind.Sick, null, null, null)], null, []), [open]))
            .On($"POST /api/locations/{TestData.LocationId}/absence/proposal", new AbsenceProposal(TestData.Ben.Id, TestData.Monday, []));
        var manager = TestData.Reception() with { Rights = [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ManageLocation] };
        var vm = new CalendarViewModel(api.Client(), manager, null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();

        var report = vm.ActionsForColumn(vm.Columns.Single(c => c.Id == TestData.Ben.Id)).Single(a => a.Header == "Ben krank melden");
        Assert.True(report.IsDanger);
        await vm.ReportSickAsync(vm.Columns.Single(c => c.Id == TestData.Ben.Id));

        var put = api.Calls.Single(c => c.Route.StartsWith("PUT /api/locations", StringComparison.Ordinal)).Body!;
        Assert.Contains("\"kind\":\"Sick\"", put, StringComparison.Ordinal);
        Assert.Equal("Ersatz für Ben", vm.Absence?.Title);
        Assert.Contains("krank gemeldet", vm.Toast, StringComparison.Ordinal);
    }
}
