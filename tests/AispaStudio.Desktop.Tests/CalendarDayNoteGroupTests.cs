using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde 5, Ä31: kein „Heute“-Band mehr; die Tagesnotizen stehen als Gruppe „Notizen heute“ ganz oben in der Seitenleiste „Meldungen“.</summary>
public sealed class CalendarDayNoteGroupTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";
    private static readonly string CreateRoute = $"POST /api/locations/{TestData.LocationId}/calendar-notes";

    private static CalendarNoteDto Note(string text, bool done = false) =>
        new(Guid.NewGuid(), TestData.Monday, text, null, null, null, done, TestData.At(8));

    private static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On(CalendarRoute, day);

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static CurrentUser ViewOnly() => TestData.Reception() with { Rights = [AccessRight.ViewCalendar] };

    [Fact]
    public async Task Die_Gruppe_zeigt_die_Tagesnotizen_ohne_Uhrzeit_und_keine_Zettel_und_zaehlt_nicht_als_Meldung()
    {
        var open = Note("Lieferung 10 Uhr");
        var done = Note("Sauna-Wartung erledigt", done: true);
        var sticky = new CalendarNoteDto(Guid.NewGuid(), TestData.Monday, "Herr Lang ruft zurück", ResourceKind.Employee, TestData.Anna.Id, TestData.At(14), false, TestData.At(9));
        var vm = await OpenAsync(Api(TestData.Day() with { Notes = [open, done, sticky] }));

        Assert.Equal([open.Text, done.Text], vm.DayNotes.Select(n => n.Text));
        Assert.Equal("2 Notizen heute", vm.DayNotesText);
        Assert.Equal(0, vm.MessageCount);
        Assert.Equal("Meldungen", vm.MessagesButtonText);
        Assert.Equal("Meldungen, 2 Notizen heute", vm.MessagesName);
        Assert.Contains("2 Notizen heute", vm.MessagesTip, StringComparison.Ordinal);

        vm.OpenMessagesCommand.Execute(null);
        var panel = vm.MessagesPanel!;
        Assert.True(panel.ShowNotes);
        Assert.Equal([(open.Text, false), (done.Text, true)], panel.Notes.Select(n => (n.Text, n.IsDone)));
        Assert.True(panel.IsEmpty, "Notizen sind keine Meldungen: die Seitenleiste meldet weiter „Keine Meldungen“");
        Assert.Equal(2, panel.NotesCount);
    }

    [Fact]
    public async Task Ohne_Notizen_zeigt_die_Gruppe_nur_den_Knopf_und_ohne_Buchungsrecht_gar_nichts()
    {
        var vm = await OpenAsync(Api(TestData.Day()));
        Assert.False(vm.HasDayNotes);
        Assert.Equal("Meldungen", vm.MessagesName);
        vm.OpenMessagesCommand.Execute(null);
        Assert.True(vm.MessagesPanel!.ShowNotes);
        Assert.False(vm.MessagesPanel.HasNotes);
        Assert.True(vm.MessagesPanel.CanAddNote);

        var viewOnly = await OpenAsync(Api(TestData.Day()), ViewOnly());
        viewOnly.OpenMessagesCommand.Execute(null);
        Assert.False(viewOnly.MessagesPanel!.ShowNotes);

        // Mit Notizen, aber ohne Buchungsrecht: lesen, nicht ändern.
        var reading = await OpenAsync(Api(TestData.Day() with { Notes = [Note("Gruppe kommt um 14 Uhr")] }), ViewOnly());
        reading.OpenMessagesCommand.Execute(null);
        var row = Assert.Single(reading.MessagesPanel!.Notes);
        Assert.True(reading.MessagesPanel.ShowNotes);
        Assert.False(reading.MessagesPanel.CanAddNote);
        Assert.False(row.CanChange);
    }

    [Fact]
    public async Task Abwesende_ohne_Termine_stehen_nicht_bei_den_Notizen_und_mit_Terminen_als_Meldung()
    {
        var booked = TestData.Appointment(10, TestData.Ben);
        var day = TestData.Day(booked) with { Absences = [new CalendarAbsence(TestData.Ben.Id, RosterKind.Sick, TestData.Monday, null)], Notes = [] };
        var vm = await OpenAsync(Api(day));

        Assert.Empty(vm.DayNotes);
        Assert.Contains(vm.Messages, m => m.Kind == MessageKind.Absence);
    }

    [Fact]
    public async Task Der_Punkt_oeffnet_die_Seitenleiste_mit_dem_Notizfeld_und_fragt_den_Fokus_an()
    {
        var vm = await OpenAsync(Api(TestData.Day() with { Notes = [Note("Lieferung 10 Uhr")] }));
        var focus = new List<string>();
        vm.PanelFocusRequested += focus.Add;

        Assert.True(vm.HandleKey(Key.OemPeriod, KeyModifiers.None));

        Assert.True(vm.HasPanel);
        Assert.True(vm.MessagesPanel is { IsEditingNote: true });
        Assert.Equal([MessagesPanelViewModel.NoteBox], focus);
        Assert.Equal("Notiz speichern", vm.MessagesPanel!.NoteSaveText);

        // Ein zweiter Punkt lässt das offene Feld mit seinem Text stehen.
        vm.MessagesPanel.NoteText = "Angefangen";
        Assert.True(vm.HandleKey(Key.OemPeriod, KeyModifiers.None));
        Assert.Equal("Angefangen", vm.MessagesPanel.NoteText);
        Assert.Equal(2, focus.Count);
    }

    [Fact]
    public async Task Der_Punkt_hebt_den_Filter_der_Seitenleiste_auf_damit_die_Notizen_da_sind()
    {
        var vm = await OpenAsync(Api(TestData.Day()));
        vm.OpenMessagesFor(vm.Columns[0]);
        Assert.False(vm.MessagesPanel!.ShowNotes);

        vm.OpenDayNoteCommand.Execute(null);

        Assert.False(vm.MessagesPanel!.HasFilter);
        Assert.True(vm.MessagesPanel.IsEditingNote);
    }

    [Fact]
    public async Task Ohne_Buchungsrecht_oeffnet_der_Punkt_nichts()
    {
        var vm = await OpenAsync(Api(TestData.Day()), ViewOnly());

        Assert.False(vm.HandleKey(Key.OemPeriod, KeyModifiers.None));
        Assert.False(vm.HasPanel);
    }

    [Fact]
    public async Task Notiz_speichern_sendet_Text_und_Tag_zeigt_sie_in_der_Gruppe_und_meldet_es_im_Toast()
    {
        var created = Note("Lieferung 10 Uhr");
        var api = Api(TestData.Day()).On(CreateRoute, created, HttpStatusCode.Created);
        var vm = await OpenAsync(api);
        vm.OpenDayNoteCommand.Execute(null);
        var panel = vm.MessagesPanel!;
        var focus = new List<string>();
        vm.PanelFocusRequested += focus.Add;

        // Ein leerer Text bleibt ein Fehler am Feld; nichts wird gesendet.
        await panel.SaveNoteAsync();
        Assert.Equal("Bitte einen Text eingeben.", panel.NoteError);
        Assert.DoesNotContain(api.Calls, c => c.Route == CreateRoute);

        panel.NoteText = "  Lieferung 10 Uhr  ";
        Assert.True(panel.HasUnsavedChanges);
        await panel.SaveNoteAsync();

        var body = api.Calls.Single(c => c.Route == CreateRoute).Body!;
        Assert.Contains("\"text\":\"Lieferung 10 Uhr\"", body, StringComparison.Ordinal);
        Assert.Contains("\"date\":\"2027-03-01\"", body, StringComparison.Ordinal);
        Assert.False(panel.IsEditingNote);
        Assert.False(panel.HasUnsavedChanges);
        Assert.Equal(["Lieferung 10 Uhr"], panel.Notes.Select(n => n.Text));
        Assert.True(vm.HasDayNotes);
        Assert.Equal("Notiz gespeichert", vm.Toast);
        Assert.Equal([MessagesPanelViewModel.AddNoteButton], focus);
    }

    [Fact]
    public async Task Ein_zu_langer_Text_und_eine_Ablehnung_des_Servers_bleiben_am_Feld_der_Text_bleibt()
    {
        var api = Api(TestData.Day()).On(CreateRoute, null, HttpStatusCode.BadRequest);
        var vm = await OpenAsync(api);
        vm.OpenDayNoteCommand.Execute(null);
        var panel = vm.MessagesPanel!;

        panel.NoteText = new string('a', MessagesPanelViewModel.MaxNoteLength + 1);
        await panel.SaveNoteAsync();
        Assert.Equal("Höchstens 500 Zeichen.", panel.NoteError);
        Assert.DoesNotContain(api.Calls, c => c.Route == CreateRoute);

        panel.NoteText = "Gültig";
        await panel.SaveNoteAsync();
        Assert.True(panel.IsEditingNote);
        Assert.NotNull(panel.NoteError);
        Assert.Equal("Gültig", panel.NoteText);
        Assert.Empty(panel.Notes);
    }

    [Fact]
    public async Task Notiz_aendern_laedt_den_Text_ins_Feld_und_speichert_per_PATCH()
    {
        var note = Note("Lieferung 10 Uhr");
        var api = Api(TestData.Day() with { Notes = [note] }).On($"PATCH /api/calendar-notes/{note.Id}", note with { Text = "Lieferung 11 Uhr" });
        var vm = await OpenAsync(api);
        vm.OpenMessagesCommand.Execute(null);
        var panel = vm.MessagesPanel!;
        var row = panel.Notes.Single();

        row.EditCommand!.Execute(null);

        Assert.True(panel.IsEditingNote);
        Assert.Equal("Lieferung 10 Uhr", panel.NoteText);
        Assert.Equal("Änderung speichern", panel.NoteSaveText);
        Assert.False(panel.HasUnsavedChanges);
        Assert.False(panel.CanChangeNotes);
        panel.NoteText = "Lieferung 11 Uhr";
        Assert.True(panel.HasUnsavedChanges);

        await panel.SaveNoteAsync();

        Assert.Contains("\"text\":\"Lieferung 11 Uhr\"", api.Calls.Single(c => c.Route.StartsWith("PATCH ", StringComparison.Ordinal)).Body!, StringComparison.Ordinal);
        Assert.Equal(["Lieferung 11 Uhr"], panel.Notes.Select(n => n.Text));
        Assert.False(panel.IsEditingNote);
        Assert.Equal("Notiz geändert", vm.Toast);
    }

    [Fact]
    public async Task Abhaken_geht_aus_der_Gruppe_und_Loeschen_fragt_zuerst_nach()
    {
        var note = Note("Lieferung 10 Uhr");
        var api = Api(TestData.Day() with { Notes = [note] })
            .On($"PATCH /api/calendar-notes/{note.Id}", note with { Done = true })
            .On($"DELETE /api/calendar-notes/{note.Id}", null, HttpStatusCode.NoContent);
        var vm = await OpenAsync(api);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        vm.Dialogs = dialogs;
        vm.OpenMessagesCommand.Execute(null);
        var panel = vm.MessagesPanel!;

        panel.Notes.Single().ToggleCommand!.Execute(null);
        await Until(() => panel.Notes.Single().IsDone);

        panel.Notes.Single().DeleteCommand!.Execute(null);
        await Until(() => dialogs.Asked.Count == 1);
        Assert.Equal("Notiz löschen?", dialogs.Asked[0].Title);
        dialogs.AssertRules();
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.Single(panel.Notes);

        panel.Notes.Single().DeleteCommand!.Execute(null);
        await Until(() => panel.Notes.Count == 0);
        Assert.Contains(api.Calls, c => c.Route == $"DELETE /api/calendar-notes/{note.Id}");
        Assert.False(vm.HasDayNotes);
    }

    [Fact]
    public async Task Esc_bricht_nur_das_Schreiben_ab_die_Seitenleiste_bleibt_und_der_Text_ist_verworfen()
    {
        var vm = await OpenAsync(Api(TestData.Day()));
        vm.OpenDayNoteCommand.Execute(null);
        var panel = vm.MessagesPanel!;
        panel.NoteText = "Angefangen";
        var focus = new List<string>();
        vm.PanelFocusRequested += focus.Add;

        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));

        Assert.False(panel.IsEditingNote);
        Assert.Equal("", panel.NoteText);
        Assert.NotNull(vm.MessagesPanel);
        Assert.Equal([MessagesPanelViewModel.AddNoteButton], focus);

        // Das nächste Esc schließt die Seitenleiste wie sonst.
        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));
        Assert.Null(vm.MessagesPanel);
    }

    [Fact]
    public async Task Ein_angefangener_Text_ist_eine_ungespeicherte_Aenderung_der_Seite()
    {
        var vm = await OpenAsync(Api(TestData.Day()));
        vm.OpenDayNoteCommand.Execute(null);
        var panel = vm.MessagesPanel!;
        Assert.False(((ISidePanel)panel).HasUnsavedChanges);

        panel.NoteText = "Angefangen";
        Assert.True(((ISidePanel)panel).HasUnsavedChanges);

        panel.CancelNoteCommand.Execute(null);
        Assert.False(((ISidePanel)panel).HasUnsavedChanges);
    }
}
