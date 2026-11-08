using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using Avalonia.Input;

namespace AispaStudio.Desktop.Tests;

/// <summary>Runde 5, Ä33: die Notiz zum Termin im Termin-Panel schreiben, ändern und entfernen; sie ist nicht die Kundennotiz und steht in der Hover-Karte.</summary>
public sealed class CalendarAppointmentNoteTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(8);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";

    private static string NoteRoute(AppointmentDto appointment) => $"PUT /api/appointments/{appointment.Id}/note";

    /// <summary>Die Notiz im Rumpf des PUT (null heißt: löschen).</summary>
    private static string? SentNote(StubApi api, AppointmentDto appointment) =>
        System.Text.Json.JsonDocument.Parse(api.Calls.Single(c => c.Route == NoteRoute(appointment)).Body!).RootElement.GetProperty("note").GetString();

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, AppointmentDto appointment, CurrentUser? user = null)
    {
        api.On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On(CalendarRoute, TestData.Day(appointment));
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        vm.OpenAppointment(appointment);
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
    public async Task Ohne_Notiz_steht_der_Link_und_Schreiben_speichert_per_PUT_und_zeigt_die_Notiz_im_Panel_und_in_der_Hover_Karte()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var saved = appointment with { Note = "Lavendelöl, wenig Gespräch", UpdatedAt = TestData.At(9) };
        var api = new StubApi().On(NoteRoute(appointment), saved);
        var vm = await OpenAsync(api, appointment);
        var focus = new List<string>();
        vm.PanelFocusRequested += focus.Add;
        var panel = vm.Selected!;

        Assert.True(panel.CanAddNote);
        Assert.False(panel.ShowNote);
        Assert.False(panel.IsEditingNote);

        panel.BeginEditNoteCommand.Execute(null);

        Assert.True(panel.IsEditingNote);
        Assert.False(panel.CanAddNote);
        Assert.Equal("", panel.NoteText);
        Assert.Equal([AppointmentViewModel.NoteBox], focus);
        Assert.False(((ISidePanel)panel).HasUnsavedChanges);

        panel.NoteText = "  Lavendelöl, wenig Gespräch  ";
        Assert.True(((ISidePanel)panel).HasUnsavedChanges);
        await panel.SaveNoteAsync();

        Assert.Equal("Lavendelöl, wenig Gespräch", SentNote(api, appointment));
        var reopened = vm.Selected!;
        Assert.NotSame(panel, reopened);
        Assert.True(reopened.ShowNote);
        Assert.True(reopened.CanChangeNote);
        Assert.False(reopened.CanAddNote);
        Assert.Equal("Lavendelöl, wenig Gespräch", reopened.Note);
        Assert.Equal("Lavendelöl, wenig Gespräch", vm.Columns[0].Items.Single().Hover!.Note);
        Assert.Contains("Notiz: Lavendelöl, wenig Gespräch", vm.Columns[0].Items.Single().HoverText, StringComparison.Ordinal);
        Assert.Equal([AppointmentViewModel.NoteBox, AppointmentViewModel.EditNoteButton], focus);
    }

    [Fact]
    public async Task Mit_Notiz_laedt_das_Aendern_den_Text_Leeren_und_Speichern_entfernt_sie()
    {
        var appointment = TestData.Appointment(10, TestData.Anna) with { Note = "Fokus Nacken" };
        var cleared = appointment with { Note = null, UpdatedAt = TestData.At(9) };
        var api = new StubApi().On(NoteRoute(appointment), cleared);
        var vm = await OpenAsync(api, appointment);
        var panel = vm.Selected!;

        Assert.True(panel.ShowNote);
        Assert.False(panel.CanAddNote);
        panel.BeginEditNoteCommand.Execute(null);
        Assert.Equal("Fokus Nacken", panel.NoteText);
        Assert.False(panel.ShowNote);
        Assert.False(((ISidePanel)panel).HasUnsavedChanges);

        panel.NoteText = "";
        await panel.SaveNoteAsync();

        Assert.Null(SentNote(api, appointment));
        Assert.Null(vm.Selected!.Note);
        Assert.True(vm.Selected.CanAddNote);
        Assert.Null(vm.Columns[0].Items.Single().Hover!.Note);
    }

    [Fact]
    public async Task Esc_bricht_das_Schreiben_ab_ohne_zu_speichern_und_das_Panel_bleibt()
    {
        var appointment = TestData.Appointment(10, TestData.Anna) with { Note = "Fokus Nacken" };
        var api = new StubApi();
        var vm = await OpenAsync(api, appointment);
        var focus = new List<string>();
        vm.PanelFocusRequested += focus.Add;
        var panel = vm.Selected!;
        panel.BeginEditNoteCommand.Execute(null);
        panel.NoteText = "Etwas anderes";
        focus.Clear();

        Assert.True(vm.HandleKey(Key.Escape, KeyModifiers.None));

        Assert.False(panel.IsEditingNote);
        Assert.Same(panel, vm.Selected);
        Assert.Equal("Fokus Nacken", panel.Note);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Equal([AppointmentViewModel.EditNoteButton], focus);

        // Ohne Notiz geht der Fokus zum Link zurück.
        var empty = TestData.Appointment(13, TestData.Anna);
        var other = await OpenAsync(new StubApi(), empty);
        var otherFocus = new List<string>();
        other.PanelFocusRequested += otherFocus.Add;
        other.Selected!.BeginEditNoteCommand.Execute(null);
        otherFocus.Clear();
        other.Selected.CancelEditNoteCommand.Execute(null);
        Assert.Equal([AppointmentViewModel.AddNoteLink], otherFocus);
    }

    [Fact]
    public async Task Ein_zu_langer_Text_und_eine_Ablehnung_bleiben_als_Fehler_am_Feld_und_der_Text_bleibt()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var api = new StubApi().On(NoteRoute(appointment), null, HttpStatusCode.BadRequest);
        var vm = await OpenAsync(api, appointment);
        var panel = vm.Selected!;
        panel.BeginEditNoteCommand.Execute(null);

        panel.NoteText = new string('a', AppointmentViewModel.MaxNoteLength + 1);
        await panel.SaveNoteAsync();
        Assert.Equal("Höchstens 500 Zeichen.", panel.NoteError);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));

        panel.NoteText = "Gültig";
        await panel.SaveNoteAsync();
        Assert.True(panel.IsEditingNote);
        Assert.NotNull(panel.NoteError);
        Assert.Equal("Gültig", panel.NoteText);
        Assert.Same(panel, vm.Selected);
    }

    [Fact]
    public async Task Ohne_Buchungsrecht_gibt_es_weder_Link_noch_Stift_und_Schreiben_tut_nichts()
    {
        var plain = TestData.Appointment(10, TestData.Anna);
        var withNote = TestData.Appointment(13, TestData.Anna) with { Note = "Fokus Nacken" };
        var api = new StubApi();

        var vm = await OpenAsync(api, plain, ViewOnly());
        Assert.False(vm.Selected!.CanAddNote);
        vm.Selected.BeginEditNoteCommand.Execute(null);
        Assert.False(vm.Selected.IsEditingNote);

        var reading = await OpenAsync(new StubApi(), withNote, ViewOnly());
        Assert.True(reading.Selected!.ShowNote);
        Assert.False(reading.Selected.CanChangeNote);
        Assert.Equal("Fokus Nacken", reading.Selected.Note);
    }

    [Fact]
    public async Task Notiz_und_Zeiten_aendern_schliessen_sich_gegenseitig_aus_damit_Strg_Enter_nie_das_falsche_speichert()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var vm = await OpenAsync(new StubApi(), appointment);
        var panel = vm.Selected!;

        panel.BeginEditTimesCommand.Execute(null);
        panel.BeginEditNoteCommand.Execute(null);
        Assert.True(panel.IsEditingTimes);
        Assert.False(panel.IsEditingNote);

        panel.CancelEditTimesCommand.Execute(null);
        panel.BeginEditNoteCommand.Execute(null);
        panel.BeginEditTimesCommand.Execute(null);
        Assert.True(panel.IsEditingNote);
        Assert.False(panel.IsEditingTimes);
    }

    [Fact]
    public async Task Auch_ein_erledigter_Termin_bekommt_nachtraeglich_eine_Notiz()
    {
        var done = TestData.Appointment(10, TestData.Anna, AppointmentStatus.Completed);
        var saved = done with { Note = "Kommt wieder", UpdatedAt = TestData.At(9) };
        var api = new StubApi().On(NoteRoute(done), saved);
        var vm = await OpenAsync(api, done);

        Assert.True(vm.Selected!.CanAddNote);
        vm.Selected.BeginEditNoteCommand.Execute(null);
        vm.Selected.NoteText = "Kommt wieder";
        await vm.Selected.SaveNoteAsync();

        await Until(() => vm.Selected is { Note: "Kommt wieder" });
    }
}
