using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 110 (Ä252): Reiter „Personalakte“ der Personenkarte und die Erinnerung im Kalender. Rechte, Verschlüsselung und Fristen prüft die API (<c>HrFileTests</c> dort);
/// hier zählt, was die App zeigt, schickt und sagt.
/// </summary>
public sealed class HrFileTests
{
    private const string FollowUpsRoute = "GET /api/hr-file/follow-ups";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static readonly EmployeeDto Lena = RosterSample.Lena;
    private static readonly string HrPath = $"/api/employees/{Lena.Id}/hr-file";

    private static readonly HrEntryDto Conversation = new(Guid.NewGuid(), HrEntryKind.Conversation, new DateOnly(2027, 2, 1), "Ziele besprochen", HrConversationKind.Annual, "Katrin, Lena",
        [new("Kurs Hot Stone buchen", false), new("Mehr Samstage", true)], new DateOnly(2027, 3, 15), false, null, false, null);

    private static readonly HrEntryDto Warning = new(Guid.NewGuid(), HrEntryKind.Warning, new DateOnly(2027, 1, 10), "Dreimal zu spät", null, null, [], null, false, "Katrin", true, new DateOnly(2029, 1, 10));

    private static StubApi Api(params HrEntryDto[] entries)
    {
        var (api, _) = RosterTests.Setup();
        api.On($"GET {HrPath}", entries.ToList());
        return api;
    }

    private static HrEntryInput Sent(StubApi api, string route) =>
        JsonSerializer.Deserialize<HrEntryInput>(Assert.Single(api.Calls, c => c.Route == route).Body!, ApiClient.Json)!;

    private static async Task<PersonHrFileViewModel> Open(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var team = await RosterTests.Loaded(api, user, dialogs);
        team.Open(Lena.Id, PersonCardViewModel.HrFileTab, null);
        var file = team.Card!.HrFile!;
        await RosterTests.Until(() => file.IsLoaded);
        return file;
    }

    [Fact]
    public async Task Given_eine_Rezeptionistin_When_sie_die_Karte_einer_Kollegin_oeffnet_Then_fehlt_der_Reiter_Personalakte()
    {
        var team = await RosterTests.Loaded(Api(), RosterTests.WithRights(StaffRole.Reception, AccessRight.ManageCatalog, AccessRight.ViewStaffRecords));

        team.Open(Lena.Id, PersonCardViewModel.HrFileTab, null);

        Assert.Null(team.Card!.HrFile);
        Assert.DoesNotContain(team.Card.Tabs, t => t.Id == PersonCardViewModel.HrFileTab);
        Assert.Equal(PersonCardViewModel.DetailsTab, team.Card.SelectedTab);
    }

    [Fact]
    public async Task Given_die_Inhaberin_When_sie_den_Reiter_oeffnet_Then_stehen_die_Eintraege_mit_ihren_Angaben_da()
    {
        var file = await Open(Api(Conversation, Warning));

        Assert.Equal(["Jahresgespräch, 1.2.2027", "Verwarnung, 10.1.2027"], file.Rows.Select(r => r.Title));
        Assert.Equal(
            ["Teilnehmende: Katrin, Lena", "Offen: Kurs Hot Stone buchen", "Erledigt: Mehr Samstage", "Folgetermin: 15.3.2027"],
            file.Rows[0].Lines);
        Assert.Equal(["Ausgesprochen von: Katrin", "Schriftlich übergeben", "Gültig bis: 10.1.2029"], file.Rows[1].Lines);
        Assert.False(file.ShowsEmpty);
    }

    [Fact]
    public async Task Given_ein_neues_Gespraech_mit_zwei_Vereinbarungen_When_gespeichert_wird_Then_geht_es_vollstaendig_an_die_API()
    {
        var api = Api().On($"POST {HrPath}", Conversation);
        var file = await Open(api);

        file.AddConversationCommand.Execute(null);
        Assert.True(file.IsConversation);
        Assert.Equal("Neues Gespräch", file.FormTitle);
        file.SelectedConversation = file.ConversationKinds.Single(k => k.Kind == HrConversationKind.Feedback);
        file.Participants = " Katrin, Lena ";
        file.Text = "Ziele besprochen";
        file.AddAgreementCommand.Execute(null);
        file.Agreements[0].Text = "Kurs buchen";
        file.AddAgreementCommand.Execute(null);
        file.Agreements[1].Text = "Mehr Samstage";
        file.Agreements[1].IsDone = true;
        file.AddAgreementCommand.Execute(null);
        file.FollowUpOn = new DateOnly(2027, 3, 15);
        Assert.True(file.HasUnsavedChanges);

        await file.SaveCommand.ExecuteAsync(null);

        var sent = Sent(api, $"POST {HrPath}");
        Assert.Equal(HrEntryKind.Conversation, sent.Kind);
        Assert.Equal(HrConversationKind.Feedback, sent.ConversationKind);
        Assert.Equal("Katrin, Lena", sent.Participants);
        Assert.Equal([new HrAgreement("Kurs buchen", false), new HrAgreement("Mehr Samstage", true)], sent.Agreements);
        Assert.Equal(new DateOnly(2027, 3, 15), sent.FollowUpOn);
        Assert.False(file.IsEditing);
        Assert.False(file.HasUnsavedChanges);
        Assert.Equal("Gespeichert.", file.StatusText);
    }

    [Fact]
    public async Task Given_eine_leere_Verwarnung_When_gespeichert_wird_Then_nennt_das_Feld_den_Grund_und_nichts_geht_an_die_API()
    {
        var api = Api();
        var file = await Open(api);

        file.AddWarningCommand.Execute(null);
        Assert.Equal("Grund", file.TextLabel);
        Assert.False(file.HasUnsavedChanges);
        file.IssuedBy = "";
        file.Date = new DateOnly(2027, 2, 1);
        file.ExpiresOn = new DateOnly(2027, 1, 1);

        await file.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Trage den Grund ein.", file.TextError);
        Assert.Equal("Trage ein, wer die Verwarnung ausgesprochen hat.", file.IssuedByError);
        Assert.Equal("Wähle ein Ablaufdatum ab dem Datum der Verwarnung.", file.ExpiresError);
        Assert.True(file.IsEditing);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_eine_Verwarnung_ohne_Ablaufdatum_When_gespeichert_wird_Then_bleibt_die_Vorgabe_bei_der_API_und_der_Name_ist_vorbelegt()
    {
        var api = Api().On($"POST {HrPath}", Warning);
        var file = await Open(api);

        file.AddWarningCommand.Execute(null);
        Assert.Equal("Katrin", file.IssuedBy);
        file.Text = "Dreimal zu spät";
        file.HandedOverInWriting = true;
        await file.SaveCommand.ExecuteAsync(null);

        var sent = Sent(api, $"POST {HrPath}");
        Assert.Equal(HrEntryKind.Warning, sent.Kind);
        Assert.Null(sent.ExpiresOn);
        Assert.Equal("Katrin", sent.IssuedBy);
        Assert.True(sent.HandedOverInWriting);
        Assert.Null(sent.ConversationKind);
    }

    [Fact]
    public async Task Given_ein_Gespraech_When_eine_Vereinbarung_abgehakt_wird_Then_geht_die_Aenderung_per_PUT_an_den_Eintrag()
    {
        var route = $"PUT {HrPath}/{Conversation.Id}";
        var api = Api(Conversation).On(route, Conversation);
        var file = await Open(api);

        file.Rows[0].EditCommand.Execute(null);
        Assert.Equal("Gespräch bearbeiten", file.FormTitle);
        Assert.False(file.HasUnsavedChanges);
        file.Agreements[0].IsDone = true;
        Assert.True(file.HasUnsavedChanges);
        await file.SaveCommand.ExecuteAsync(null);

        Assert.True(Sent(api, route).Agreements![0].IsDone);
    }

    [Fact]
    public async Task Given_die_API_lehnt_ab_When_gespeichert_wird_Then_steht_ihre_Meldung_da_und_die_Eingaben_bleiben()
    {
        var api = Api().On($"POST {HrPath}", new { title = "Der Schlüssel fehlt." }, HttpStatusCode.ServiceUnavailable);
        var file = await Open(api);

        file.AddNoteCommand.Execute(null);
        file.Text = "Hat gut vertreten";
        await file.SaveCommand.ExecuteAsync(null);

        Assert.Contains("Der Schlüssel fehlt.", file.Error, StringComparison.Ordinal);
        Assert.True(file.IsEditing);
        Assert.Equal("Hat gut vertreten", file.Text);
        Assert.True(file.ShowsFormError);
        Assert.False(file.ShowsListError);
    }

    [Fact]
    public async Task Given_ein_offenes_Formular_When_die_Zeilen_gezeigt_werden_Then_sind_Aendern_und_Loeschen_gesperrt_bis_es_zu_ist()
    {
        var file = await Open(Api(Conversation));

        file.AddNoteCommand.Execute(null);
        file.Text = "Halb fertig";

        Assert.False(file.Rows[0].EditCommand.CanExecute(null));
        Assert.False(file.Rows[0].DeleteCommand.CanExecute(null));

        file.CancelCommand.Execute(null);

        Assert.True(file.Rows[0].EditCommand.CanExecute(null));
        Assert.True(file.Rows[0].DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_das_Laden_scheitert_When_der_Reiter_offen_ist_Then_steht_nur_der_Fehler_da()
    {
        var (api, _) = RosterTests.Setup();
        api.On($"GET {HrPath}", new { title = "Kaputt" }, HttpStatusCode.ServiceUnavailable);
        var team = await RosterTests.Loaded(api);

        team.Open(Lena.Id, PersonCardViewModel.HrFileTab, null);
        var file = team.Card!.HrFile!;
        await RosterTests.Until(() => file.HasLoadError);

        Assert.False(file.IsLoaded);
        Assert.False(file.ShowsLoading);
    }

    [Fact]
    public async Task Given_ein_Eintrag_When_die_Rueckfrage_bestaetigt_wird_Then_ist_er_geloescht_und_bei_Abbrechen_nicht()
    {
        var route = $"DELETE {HrPath}/{Warning.Id}";
        var api = Api(Warning).On(route, null, HttpStatusCode.NoContent);
        var dialogs = new FakeDialogs();
        var file = await Open(api, dialogs: dialogs);

        await file.Rows[0].DeleteCommand.ExecuteAsync(null);
        Assert.DoesNotContain(api.Calls, c => c.Route == route);

        dialogs.Answer(ConfirmChoice.Confirmed);
        await file.Rows[0].DeleteCommand.ExecuteAsync(null);
        Assert.Contains(api.Calls, c => c.Route == route);
        Assert.Equal("Gelöscht.", file.StatusText);
        dialogs.AssertRules();
    }

    [Fact]
    public async Task Given_der_Export_When_gespeichert_wird_Then_ist_die_Datei_Byte_fuer_Byte_das_PDF_der_API()
    {
        byte[] pdf = [0x25, 0x50, 0x44, 0x46];
        var api = Api().OnBytes($"GET {HrPath}/export", pdf);
        var files = new FakeFileSaver();
        var file = await Open(api);
        file.Files = files;

        await file.ExportCommand.ExecuteAsync(null);

        var (name, content) = Assert.Single(files.Saved);
        Assert.Equal($"personalakte-{Lena.DisplayName}.pdf", name);
        Assert.Equal(pdf, content);
        Assert.Equal($"{name} gespeichert.", file.Message);
    }

    [Fact]
    public async Task Given_eine_ausgeschiedene_Person_When_der_Reiter_offen_ist_Then_ist_er_nur_lesbar_und_exportierbar()
    {
        var api = TeamSample.AddTeam(new StubApi()
                .On("GET /api/qualifications", new List<QualificationDto>())
                .On("GET /api/locations", new List<LocationDto> { RosterSample.Vienna }),
            [RosterSample.Marco], [Lena], [], []);
        api.On($"GET {HrPath}", new List<HrEntryDto> { Warning });
        var team = await RosterTests.Loaded(api);
        team.ShowFormer = true;

        team.SelectedEntry = team.Entries.Single(e => e.Kind == TeamEntryKind.Former);
        team.Card!.SelectTab(PersonCardViewModel.HrFileTab);

        Assert.True(team.Card.IsReadOnly);
        var file = team.Card.HrFile!;
        await RosterTests.Until(() => file.IsLoaded);
        Assert.False(file.ShowsAddButtons);
        Assert.False(file.Rows[0].IsEditable);
        Assert.False(team.Card.HasSaveBar);
    }

    [Fact]
    public async Task Given_ein_offener_Eintrag_When_die_Karte_verworfen_wird_Then_ist_das_Formular_zu_und_nichts_mehr_ungespeichert()
    {
        var team = await RosterTests.Loaded(Api());
        team.Open(Lena.Id, PersonCardViewModel.HrFileTab, null);
        var card = team.Card!;
        await RosterTests.Until(() => card.HrFile!.IsLoaded);

        card.HrFile!.AddNoteCommand.Execute(null);
        card.HrFile.Text = "Hat gut vertreten";
        Assert.True(card.HasUnsavedChanges);
        Assert.False(card.HasSaveBar);

        card.Discard();

        Assert.False(card.HrFile.IsEditing);
        Assert.False(card.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_die_Karte_When_der_Verlauf_offen_ist_Then_hat_er_den_Abschnitt_Personalakte_mit_der_Adresse_der_API()
    {
        var route = $"GET {HrPath}/history";
        var api = Api().On(route, new List<HistoryEntry>());
        var team = await RosterTests.Loaded(api);
        team.Open(Lena.Id, PersonCardViewModel.HistoryTab, null);

        Assert.True(team.Card!.HasHrHistory);
        await team.Card.HrHistory!.LoadAsync();

        Assert.Contains(api.Calls, c => c.Route == route);
    }

    // ---------- Erinnerung im Kalender ----------

    private static StubApi CalendarApi(params HrFollowUpDto[] followUps) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On($"GET /api/locations/{TestData.LocationId}/appointment-holds", new List<AppointmentHoldDto>())
        .On(FollowUpsRoute, followUps.ToList());

    [Fact]
    public async Task Given_ein_faelliger_Folgetermin_When_die_Inhaberin_den_Kalender_oeffnet_Then_erinnert_eine_Meldung_ohne_Inhalt_und_fuehrt_zur_Personalakte()
    {
        var employeeId = Guid.NewGuid();
        var api = CalendarApi(new HrFollowUpDto(Guid.NewGuid(), employeeId, "Folgegespräch mit Lena", new DateOnly(2027, 3, 1)));
        var calendar = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime());
        Guid? opened = null;
        calendar.HrFileRequested = id => opened = id;

        await calendar.InitializeAsync();
        await calendar.LoadAsync();

        var message = Assert.Single(calendar.Messages, m => m.Kind == MessageKind.HrFollowUp);
        Assert.Equal("Folgegespräch mit Lena", message.Title);
        Assert.Equal("Fällig am 1.3.2027.", message.Text);
        Assert.Equal("Personalakte", message.KindText);
        Assert.Equal(MessageSeverity.Notice, message.Severity);
        Assert.Equal("Zur Personalakte", message.ActionLabel);
        message.ActionCommand!.Execute(null);
        Assert.Equal(employeeId, opened);
    }

    [Fact]
    public async Task Given_eine_Rezeptionistin_When_sie_den_Kalender_oeffnet_Then_fragt_die_App_die_Folgetermine_gar_nicht_ab()
    {
        var api = CalendarApi();
        var calendar = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());

        await calendar.InitializeAsync();
        await calendar.LoadAsync();

        Assert.DoesNotContain(api.Calls, c => c.Route == FollowUpsRoute);
        Assert.DoesNotContain(calendar.Messages, m => m.Kind == MessageKind.HrFollowUp);
    }
}
