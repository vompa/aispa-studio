using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 98 (Ä191, Ä250, ADR 0084): Bereich „Team“ mit Personenkarte als native Maske der Verwaltung.</summary>
public sealed class TeamTests
{
    private static readonly QualificationDto HotStone = new(Guid.NewGuid(), "Hot Stone");
    private static readonly QualificationDto Cosmetics = new(Guid.NewGuid(), "Kosmetik");
    private static readonly LocationDto Vienna = new(Guid.NewGuid(), "Wien", "Europe/Vienna", "EUR", []);
    private static readonly EmployeeDto Lena = new(Guid.NewGuid(), "Lena Huber", "#2f6f5e", [HotStone.Id],
        [new WorkingHours(Vienna.Id, DayOfWeek.Monday, new(9, 0), new(17, 0))], ["en"], TherapistGender.Female, "Lena");
    private static readonly EmployeeDto Marco = new(Guid.NewGuid(), "Marco Berger", "#7a4fa3", [Cosmetics.Id], []);
    private static readonly EmployeeDto Sophie = new(Guid.NewGuid(), "Sophie Gruber", "#b5524a", [], []);
    private static readonly StaffMemberDto LenaAccess = TeamSample.Access("u-lena", Lena, "", StaffRole.Therapist, [Vienna.Id]);
    private static readonly StaffMemberDto RitaAccess = TeamSample.Access("u-rita", null, "Rita Wagner", StaffRole.Reception, [Vienna.Id]);
    private static readonly InvitationDto ElifInvitation = TeamSample.Invitation("Elif Demir", "elif@spa.test", StaffRole.Therapist, [Vienna.Id]);

    private static StubApi Api(bool standalone = false) => TeamSample.AddTeam(new StubApi()
            .On("GET /api/qualifications", new List<QualificationDto> { HotStone, Cosmetics })
            .On("GET /api/locations", new List<LocationDto> { Vienna }),
        [Lena, Marco], [Sophie], [LenaAccess, RitaAccess], standalone ? [] : [ElifInvitation], standalone);

    private static async Task<TeamViewModel> Loaded(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var team = new TeamViewModel(api.Client(), user ?? TestData.Owner(), TimeProvider.System) { Dialogs = dialogs };
        await team.LoadAsync();
        Assert.Null(team.LoadError);
        return team;
    }

    private static async Task<PersonCardViewModel> Choose(TeamViewModel team, string title)
    {
        team.SelectedEntry = team.Entries.Single(e => e.Title == title);
        await Until(() => team.Card?.Entry.Title == title);
        return team.Card!;
    }

    /// <summary>„Person hinzufügen“ steht über der Liste und öffnet die Karte einer neuen Person.</summary>
    private static async Task<PersonCardViewModel> Add(TeamViewModel team)
    {
        team.AddPersonCommand.Execute(null);
        await Until(() => team.Card?.Entry.Kind == TeamEntryKind.NewPerson);
        return team.Card!;
    }

    /// <summary>„Person einladen“ bzw. im Einzelplatz „Zugang anlegen“ steht über der Liste und öffnet die Karte des neuen Zugangs.</summary>
    private static async Task<PersonCardViewModel> Invite(TeamViewModel team)
    {
        team.InviteCommand.Execute(null);
        await Until(() => team.Card?.Entry.Kind == TeamEntryKind.NewAccess);
        return team.Card!;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static ShellViewModel Shell(StubApi api, CurrentUser user) =>
        new(api.Client(), user, null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

    /// <summary>Standortleitung mit Stammdaten und Zugängen, aber ohne Verlauf (der gehört Inhaber und Manager).</summary>
    private static CurrentUser LocationManager() =>
        new("u4", "leitung@aispa.test", "Markus", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, false, [Vienna.Id]), null,
            [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus, AccessRight.ManageLocation, AccessRight.ManageCatalog, AccessRight.ManageStaff]);

    [Fact]
    public async Task Given_Lena_mit_Zugang_When_die_Inhaberin_Team_oeffnet_Then_steht_Lena_mit_Rolle_und_Status_aktiv_in_der_Liste()
    {
        var team = await Loaded(Api());

        var lena = Assert.Single(team.Entries, e => e.Title == "Lena Huber");
        Assert.Equal("Therapeut:in", lena.Role);
        Assert.Null(lena.Badge);
        Assert.Equal(Lena.Id, lena.EmployeeId);
        Assert.Equal("u-lena", lena.UserId);
        var marco = team.Entries.Single(e => e.Title == "Marco Berger");
        // Ohne Zugang ist kein Kennzeichen in der Liste (Auftraggeber, 17.11.2026), sondern ein Filter darunter.
        Assert.Equal(("", null, true), (marco.Role, marco.Badge, marco.HasNoAccess));
        Assert.False(lena.HasNoAccess);
        var rita = team.Entries.Single(e => e.Title == "Rita Wagner");
        Assert.Equal(("Rezeption", null), (rita.Role, rita.Badge));
        // „Person hinzufügen“ und „Person einladen“ stehen fest über der Liste, nicht als letzte Zeilen darin (ADR 0099, Regel 1).
        Assert.Equal(["Lena Huber", "Marco Berger", "Rita Wagner", "Elif Demir"], team.Entries.Select(e => e.Title));
        Assert.True(team.CanAddPerson);
        Assert.True(team.CanInvite);
        Assert.Equal("Person einladen", team.InviteLabel);
    }

    [Fact]
    public async Task Given_offene_Einladung_ohne_Person_When_die_Liste_geladen_ist_Then_steht_sie_als_eingeladen_darin()
    {
        var team = await Loaded(Api());

        var elif = Assert.Single(team.Entries, e => e.Title == "Elif Demir");
        Assert.Equal(("Therapeut:in", "Eingeladen"), (elif.Role, elif.Badge));
        Assert.Equal(TeamEntryKind.Invitation, elif.Kind);
        var card = await Choose(team, "Elif Demir");
        Assert.Equal([PersonCardViewModel.ProfileArea], card.Tabs.Select(t => t.Id));
        Assert.Equal([PersonCardViewModel.AccessTab], card.Sections);
        Assert.False(card.ShowsTabs);
        Assert.Equal("elif@spa.test · Therapeut:in · gültig bis 22.10.2026", card.Access?.InvitationText);
    }

    [Fact]
    public async Task Given_ausgeschiedene_Person_When_Ausgeschiedene_zeigen_an_Then_steht_sie_als_ausgeschieden_in_der_Liste_sonst_nicht()
    {
        var team = await Loaded(Api());
        Assert.True(team.CanShowFormer);
        Assert.DoesNotContain(team.Entries, e => e.Title == "Sophie Gruber");

        team.ShowFormer = true;

        var sophie = Assert.Single(team.Entries, e => e.Title == "Sophie Gruber");
        Assert.Equal("Ausgeschieden", sophie.Badge);
        var card = await Choose(team, "Sophie Gruber");
        Assert.True(card.IsReadOnly);
        Assert.False(card.HasSaveBar);
        Assert.False(card.Details?.IsEditable);
    }

    [Fact]
    public async Task Given_Suche_Lena_When_getippt_Then_zeigt_die_Liste_nur_Treffer()
    {
        var team = await Loaded(Api());

        team.Search = "lena";
        Assert.Equal(["Lena Huber"], team.Entries.Select(e => e.Title));
        Assert.True(team.CanAddPerson);

        team.Search = "elif@spa";
        Assert.Equal(["Elif Demir"], team.Entries.Select(e => e.Title));
    }

    [Fact]
    public async Task Given_Lena_gewaehlt_When_Name_geaendert_und_gespeichert_Then_PUT_employees_mit_neuem_Namen()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        Assert.Equal([PersonCardViewModel.OverviewArea, PersonCardViewModel.ProfileArea, PersonCardViewModel.WorkTimeArea, PersonCardViewModel.PayAndFileArea],
            card.Tabs.Select(t => t.Id));
        Assert.Equal(["Übersicht", "Profil", "Arbeitszeit", "Lohn und Akte"], card.Tabs.Select(t => t.Label));
        Assert.Equal([PersonCardViewModel.DetailsTab, PersonCardViewModel.AccessTab, PersonCardViewModel.QualificationsTab, PersonCardViewModel.DutiesTab,
            PersonCardViewModel.RosterTab, PersonCardViewModel.WorkingHoursTab, PersonCardViewModel.TimesTab, PersonCardViewModel.PayrollTab, PersonCardViewModel.HrFileTab, PersonCardViewModel.RegulationsTab], card.Sections);
        Assert.Equal(["Zugang und Rolle", "Dienstplan und Abwesenheiten", "Personalakte", "Regelungen und Vollmachten", "Zuständigkeiten und Zertifikate"],
            card.HistorySections.Select(h => h.Title));
        Assert.Equal(PersonCardViewModel.OverviewArea, card.SelectedTab);

        card.Details!.Name = "Lena Maria Huber";
        Assert.True(team.HasUnsavedChanges);
        Assert.True(await team.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/employees/{Lena.Id}");
        var input = JsonSerializer.Deserialize<EmployeeInput>(put.Body!, ApiClient.Json)!;
        Assert.Equal("Lena Maria Huber", input.DisplayName);
        Assert.Equal([HotStone.Id], input.QualificationIds);
        Assert.Equal(["en"], input.Languages);
        Assert.Equal("Lena", input.PreferredName);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT /api/staff/", StringComparison.Ordinal));
        Assert.False(team.HasUnsavedChanges);
        Assert.Equal("Gespeichert", team.Toast.Text);
        Assert.Contains(team.Entries, e => e.Title == "Lena Maria Huber");
    }

    [Fact]
    public async Task Given_Lena_gewaehlt_When_Qualifikation_Hot_Stone_abgewaehlt_und_gespeichert_Then_PUT_ohne_Hot_Stone()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        card.SelectTab(PersonCardViewModel.QualificationsTab);
        Assert.True(card.ShowsQualifications);

        card.Qualifications!.Options.Single(o => o.Label == "Hot Stone").IsChecked = false;
        Assert.True(team.HasUnsavedChanges);
        Assert.True(await team.SaveChangesAsync());

        var put = Assert.Single(api.Calls, c => c.Route == $"PUT /api/employees/{Lena.Id}");
        Assert.Empty(JsonSerializer.Deserialize<EmployeeInput>(put.Body!, ApiClient.Json)!.QualificationIds);
        Assert.False(team.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_neue_Mitarbeiterin_When_mit_Mailadresse_eingeladen_Then_POST_invitations_und_steht_als_eingeladen_in_der_Liste()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Invite(team);
        var access = card.Access!;
        Assert.True(access.IsInvite);

        // Given leere Felder, When „Einladung senden“, Then Fehler an beiden Feldern, Fokus im Namen und keine Einladung an die API.
        string? focus = null;
        access.FocusRequested += field => focus = field;
        Assert.True(access.InviteCommand.CanExecute(null));
        await access.InviteCommand.ExecuteAsync(null);
        Assert.Equal("Bitte einen Namen eintragen.", access.InviteNameError);
        Assert.Equal("Bitte eine E-Mail-Adresse mit @ eintragen, z. B. lena@spa.at.", access.InviteEmailError);
        Assert.Equal(PersonAccessViewModel.InviteNameField, focus);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/staff/invitations");

        access.InviteName = "Nina Koller";
        Assert.Null(access.InviteNameError);
        access.InviteEmail = "nina@spa.test";
        access.Role = RoleChoice.All.Single(r => r.Role == StaffRole.Therapist);
        await access.InviteCommand.ExecuteAsync(null);

        var post = Assert.Single(api.Calls, c => c.Route == "POST /api/staff/invitations");
        var input = JsonSerializer.Deserialize<InvitationInput>(post.Body!, ApiClient.Json)!;
        Assert.Equal(("nina@spa.test", "Nina Koller", StaffRole.Therapist), (input.Email, input.DisplayName, input.Role));
        var nina = Assert.Single(team.Entries, e => e.Title == "Nina Koller");
        Assert.Equal(("Therapeut:in", "Eingeladen"), (nina.Role, nina.Badge));
        Assert.Equal("Einladung an nina@spa.test geschickt.", team.Card?.Access?.SentText);
        Assert.Equal($"{TeamSample.PublicUrl}/?einladung={TeamSample.Token}", team.Card?.Access?.InviteLink);

        // Die Felder leeren sich wie im Web, sonst schickt ein zweiter Klick dieselbe Einladung noch einmal.
        Assert.Equal("", access.InviteEmail);
        Assert.Equal("", access.InviteName);
    }

    [Fact]
    public async Task Given_Managerin_When_sie_einer_Rezeptionistin_die_Rolle_Inhaberin_gibt_und_speichert_Then_zeigt_die_Maske_den_Grund_der_API()
    {
        var api = Api().On("PUT /api/staff/u-rita", new { title = "Eingabe ungültig", errors = new Dictionary<string, string[]> { ["Role"] = [TeamSample.RejectRole] } },
            HttpStatusCode.BadRequest);
        var team = await Loaded(api, TestData.Owner(StaffRole.GeneralManager));
        var card = await Choose(team, "Rita Wagner");
        Assert.Equal([PersonCardViewModel.ProfileArea], card.Tabs.Select(t => t.Id));
        Assert.Equal([PersonCardViewModel.AccessTab], card.Sections);
        Assert.Equal(["Zugang und Rolle"], card.HistorySections.Select(h => h.Title));

        card.Access!.Role = RoleChoice.All.Single(r => r.Role == StaffRole.Owner);
        Assert.True(team.HasUnsavedChanges);
        Assert.False(await team.SaveChangesAsync());

        Assert.Equal(TeamSample.RejectRole, team.Error);
        Assert.True(team.HasError);
        Assert.True(team.HasUnsavedChanges);
        Assert.Equal("Inhaber", card.Access.Role.Label);
    }

    [Fact]
    public async Task Given_ungespeicherte_Aenderung_When_andere_Person_gewaehlt_Then_Rueckfrage()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var team = await Loaded(Api(), dialogs: dialogs);
        var card = await Choose(team, "Lena Huber");
        card.Details!.Name = "Lena Maria Huber";

        team.SelectedEntry = team.Entries.Single(e => e.Title == "Marco Berger");
        await Until(() => dialogs.Asked.Count == 1);
        await Task.Yield();
        Assert.Same(card, team.Card);
        Assert.Equal("Lena Huber", team.SelectedEntry?.Title);
        Assert.Equal("Lena Maria Huber", card.Details.Name);

        team.SelectedEntry = team.Entries.Single(e => e.Title == "Marco Berger");
        await Until(() => team.Card?.Title == "Marco Berger");
        Assert.Equal(["Änderungen speichern?", "Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
    }

    [Fact]
    public void Given_Rezeption_ohne_Verwaltungsrecht_When_Verwaltung_Then_Team_nur_mit_Wochenplan_und_ohne_Personen()
    {
        var shell = Shell(Api(), TestData.Reception());

        // Slice 99: der Dienstplan steht im Bereich Team; Personen, Karten und Zugänge bleiben der Verwaltung vorbehalten.
        Assert.Contains(shell.AdminPage.Areas, a => a.Title == "Team");
        Assert.Equal(["Wochenplan"], shell.AdminPage.Team!.Modes.Select(m => m.Label));
        Assert.DoesNotContain(shell.AdminPage.Team.Modes, m => m.Id == TeamViewModel.PeopleMode);
        shell.Dispose();
    }

    [Fact]
    public async Task Given_Standortleitung_When_Lena_offen_Then_kein_Reiter_Verlauf()
    {
        var api = Api();
        var team = await Loaded(api, LocationManager());
        var card = await Choose(team, "Lena Huber");

        Assert.Equal([PersonCardViewModel.OverviewArea, PersonCardViewModel.ProfileArea, PersonCardViewModel.WorkTimeArea], card.Tabs.Select(t => t.Id));
        Assert.Equal([PersonCardViewModel.DetailsTab, PersonCardViewModel.AccessTab, PersonCardViewModel.QualificationsTab, PersonCardViewModel.RosterTab, PersonCardViewModel.WorkingHoursTab,
            PersonCardViewModel.TimesTab], card.Sections);
        Assert.False(card.HasHistory);
        Assert.Null(card.AccessHistory);
        Assert.Null(card.RosterHistory);
        Assert.False(card.Access?.CanGrant);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("history", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_Einzelplatz_When_die_Inhaberin_Team_oeffnet_Then_Zugang_anlegen_statt_Einladen_und_keine_Abfrage_der_Einladungen()
    {
        var api = Api(standalone: true);
        var team = await Loaded(api);

        Assert.Equal("Zugang anlegen", team.InviteLabel);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("GET /api/staff/invitations", StringComparison.Ordinal));
        var card = await Invite(team);
        Assert.True(card.Access?.IsAccount);
        var lena = await Choose(team, "Lena Huber");
        lena.SelectTab(PersonCardViewModel.AccessTab);
        Assert.True(lena.Access?.CanResetPassword);
    }

    /// <summary>Das Fragezeichen der Überschrift hängt am Fall; der Scan in <see cref="HelpTests"/> sieht nur feste Abschnitte.</summary>
    [Theory]
    [InlineData(AccessMode.Member)]
    [InlineData(AccessMode.Own)]
    [InlineData(AccessMode.Invite)]
    [InlineData(AccessMode.Account)]
    [InlineData(AccessMode.Invitation)]
    public void Given_ein_Fall_des_Reiters_Zugang_When_das_Fragezeichen_gezeigt_wird_Then_gibt_es_den_Abschnitt_in_der_Hilfe(AccessMode mode)
    {
        var context = new TeamContext(Api().Client(), TestData.Owner(), TimeProvider.System, false);

        var access = new PersonAccessViewModel(context, mode);

        Assert.True(HelpContent.Find("zugaenge")!.HasSection(access.HelpSection), access.HelpSection);
    }

    [Fact]
    public async Task Given_Inhaberin_mit_nur_Zugangsrecht_When_Verwaltung_Then_Team_ohne_Ausgeschiedene_und_ohne_Stammdaten()
    {
        var user = TestData.Owner() with { Rights = [AccessRight.ViewCalendar, AccessRight.ManageStaff] };
        var api = Api();
        var team = await Loaded(api, user);

        Assert.False(team.CanShowFormer);
        Assert.DoesNotContain(api.Calls, c => c.Route == "GET /api/employees/former");
        Assert.False(team.CanAddPerson);
        var card = await Choose(team, "Lena Huber");
        Assert.False(card.HasSection(PersonCardViewModel.DetailsTab));
        Assert.False(card.CanRemove);
    }

    [Fact]
    public async Task Given_Aenderung_an_Lena_und_Suche_filtert_Lena_weg_When_Marco_gewaehlt_Then_Rueckfrage_und_Eingabe_bleibt()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var team = await Loaded(Api(), dialogs: dialogs);
        var card = await Choose(team, "Lena Huber");
        card.Details!.Name = "Lena Maria Huber";

        team.Search = "marco";
        Assert.Null(team.SelectedEntry);
        team.SelectedEntry = team.Entries.Single(e => e.Title == "Marco Berger");

        await Until(() => dialogs.Asked.Count == 1);
        await Task.Yield();
        Assert.Same(card, team.Card);
        Assert.Equal("Lena Maria Huber", card.Details.Name);
    }

    [Fact]
    public async Task Given_Stammdaten_gueltig_und_Zugang_abgelehnt_When_gespeichert_Then_Liste_neu_und_Meldung_nennt_beides_und_naechstes_Speichern_nur_den_Zugang()
    {
        var api = Api().On("PUT /api/staff/u-lena", new { title = "Eingabe ungültig", errors = new Dictionary<string, string[]> { ["Role"] = [TeamSample.RejectRole] } },
            HttpStatusCode.BadRequest);
        var team = await Loaded(api);
        var saved = 0;
        team.Saved += () => saved++;
        var card = await Choose(team, "Lena Huber");
        card.Details!.Name = "Lena Maria Huber";
        card.Access!.Role = RoleChoice.All.Single(r => r.Role == StaffRole.Owner);

        Assert.False(await team.SaveChangesAsync());

        Assert.Equal(1, saved);
        Assert.Contains(team.Entries, e => e.Title == "Lena Maria Huber");
        Assert.Same(card, team.Card);
        Assert.Equal($"Stammdaten und Qualifikationen sind gespeichert. Der Zugang ist nicht gespeichert: {TeamSample.RejectRole}", team.Error);
        Assert.True(team.HasUnsavedChanges);

        api.On("PUT /api/staff/u-lena", LenaAccess with { Role = StaffRole.Owner });
        Assert.True(await team.SaveChangesAsync());

        Assert.Single(api.Calls, c => c.Route == $"PUT /api/employees/{Lena.Id}");
        Assert.Equal(2, api.Calls.Count(c => c.Route == "PUT /api/staff/u-lena"));
        Assert.Null(team.Error);
        Assert.Equal("Lena Maria Huber", card.Details.Name);
    }

    [Fact]
    public async Task Given_Speichern_laedt_die_Liste_neu_When_derweil_Marco_gewaehlt_wird_Then_bleibt_Marco_offen_und_gewaehlt()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        var switched = false;
        api.OnJson("GET /api/employees", _ =>
        {
            if (!switched)
            {
                switched = true;
                team.SelectedEntry = team.Entries.Single(e => e.Title == "Marco Berger");
            }

            return new List<EmployeeDto> { Lena, Marco };
        });
        card.Details!.PreferredName = "Lena M.";

        Assert.True(await team.SaveChangesAsync());

        Assert.True(switched);
        Assert.Equal("Marco Berger", team.Card?.Title);
        Assert.Equal("Marco Berger", team.SelectedEntry?.Title);
    }

    [Fact]
    public async Task Given_Speichern_laeuft_When_ein_zweites_Speichern_kommt_Then_nur_ein_POST()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Add(team);
        var again = false;
        Task<bool>? second = null;
        api.OnJson("POST /api/employees", body =>
        {
            if (!again)
            {
                again = true;
                second = team.SaveChangesAsync();
            }

            return TeamSample.Employee(Guid.NewGuid(), body);
        });
        card.Details!.Name = "Nina Koller";

        await team.SaveChangesAsync();
        await second!;

        Assert.Single(api.Calls, c => c.Route == "POST /api/employees");
    }

    [Fact]
    public async Task Given_Aenderungen_in_allen_Reitern_When_Verwerfen_Then_steht_jeder_Reiter_wieder_auf_dem_gespeicherten_Stand()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        card.Details!.Name = "Anders";
        card.Qualifications!.Options.Single(o => o.Label == "Hot Stone").IsChecked = false;
        card.Access!.Role = RoleChoice.All.Single(r => r.Role == StaffRole.Reception);
        Assert.True(team.HasUnsavedChanges);

        team.Discard();

        Assert.Equal("Lena Huber", card.Details.Name);
        Assert.True(card.Qualifications.Options.Single(o => o.Label == "Hot Stone").IsChecked);
        Assert.Equal(StaffRole.Therapist, card.Access.Role.Role);
        Assert.False(team.HasUnsavedChanges);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_eigener_Zugang_When_die_Karte_offen_ist_Then_nur_lesbar_ohne_Felder_und_ohne_Leiste()
    {
        var team = await Loaded(Api(), TestData.Owner() with { Id = "u-rita" });

        var card = await Choose(team, "Rita Wagner");

        Assert.True(card.Access?.IsOwn);
        Assert.False(card.Access?.ShowsFields);
        Assert.False(card.HasSaveBar);
        Assert.False(team.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_offene_Einladung_When_zurueckgezogen_und_bestaetigt_Then_DELETE_und_die_Zeile_ist_weg()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var api = Api();
        var team = await Loaded(api, dialogs: dialogs);
        var card = await Choose(team, "Elif Demir");

        await card.Access!.RevokeCommand.ExecuteAsync(null);

        Assert.Equal("Einladung zurückziehen?", Assert.Single(dialogs.AssertRules().Asked).Title);
        Assert.Single(api.Calls, c => c.Route == $"DELETE /api/staff/invitations/{ElifInvitation.Id}");
        Assert.DoesNotContain(team.Entries, e => e.Title == "Elif Demir");
    }

    [Fact]
    public async Task Given_Marco_When_aus_dem_Team_genommen_und_bestaetigt_Then_DELETE_und_die_Zeile_ist_weg()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var api = Api();
        var team = await Loaded(api, dialogs: dialogs);
        await Choose(team, "Marco Berger");

        await team.RemoveCommand.ExecuteAsync(null);

        Assert.Equal("Aus dem Team nehmen?", Assert.Single(dialogs.AssertRules().Asked).Title);
        Assert.Single(api.Calls, c => c.Route == $"DELETE /api/employees/{Marco.Id}");
        Assert.DoesNotContain(team.Entries, e => e.Title == "Marco Berger");
    }

    [Fact]
    public async Task Given_Person_anlegen_When_ohne_Namen_gespeichert_Then_Fehler_am_Namensfeld_und_kein_POST()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Add(team);

        Assert.True(team.SaveCommand.CanExecute(null));
        await team.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Der Name der Person fehlt. Trage den vollständigen Namen ein.", card.Details!.NameError);
        Assert.Equal(PersonDetailsViewModel.NameField, card.Details.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/employees");
    }

    [Fact]
    public async Task Given_Person_anlegen_When_Name_eingetragen_und_gespeichert_Then_POST_und_die_neue_Person_ist_offen()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Add(team);
        card.Details!.Name = "Nina Koller";

        Assert.True(await team.SaveChangesAsync());

        var post = Assert.Single(api.Calls, c => c.Route == "POST /api/employees");
        Assert.Equal("Nina Koller", JsonSerializer.Deserialize<EmployeeInput>(post.Body!, ApiClient.Json)!.DisplayName);
        Assert.Equal("Nina Koller", team.Card?.Title);
        Assert.Equal("Nina Koller", team.SelectedEntry?.Title);
        Assert.Equal("Gespeichert", team.Toast.Text);
    }

    [Fact]
    public async Task Given_Einzelplatz_When_Zugang_angelegt_Then_POST_accounts_Felder_leer_und_nichts_ungespeichert()
    {
        var api = Api(standalone: true);
        var team = await Loaded(api);
        var access = (await Invite(team)).Access!;
        access.AccountName = "Nina Koller";
        access.UserName = "nina";
        access.Password = "start-1234";
        Assert.True(team.HasUnsavedChanges);

        await access.CreateAccountCommand.ExecuteAsync(null);

        var input = JsonSerializer.Deserialize<StaffAccountInput>(Assert.Single(api.Calls, c => c.Route == "POST /api/staff/accounts").Body!, ApiClient.Json)!;
        Assert.Equal(("nina", "Nina Koller"), (input.UserName, input.DisplayName));
        Assert.Contains("Nina Koller kann sich jetzt", access.CreatedText, StringComparison.Ordinal);
        Assert.Equal("", access.Password);
        Assert.False(team.HasUnsavedChanges);
        Assert.Contains(team.Entries, e => e.Title == "Nina Koller");
    }

    [Fact]
    public async Task Given_leerer_Name_When_Qualifikation_hinzugefuegt_oder_umbenannt_Then_Fehler_am_Feld_Fokus_und_kein_Aufruf()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        card.SelectTab(PersonCardViewModel.QualificationsTab);
        var qualifications = card.Qualifications!;
        string? focus = null;
        qualifications.FocusRequested += field => focus = field;

        Assert.True(qualifications.AddCommand.CanExecute(null));
        await qualifications.AddCommand.ExecuteAsync(null);
        Assert.Equal("Bitte einen Namen für die Qualifikation eintragen.", qualifications.NewNameError);
        Assert.Equal(PersonQualificationsViewModel.NewNameField, focus);

        var row = qualifications.Rows[0];
        row.Name = " ";
        Assert.True(row.CanRename);
        await row.RenameCommand.ExecuteAsync(null);
        Assert.Equal("Bitte einen Namen für die Qualifikation eintragen.", row.NameError);
        Assert.Equal(PersonQualificationsViewModel.RowNameField, focus);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/qualifications" || c.Route.StartsWith("PUT /api/qualifications/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_Einzelplatz_When_Passwort_zurueckgesetzt_Then_POST_password_und_das_Feld_ist_leer()
    {
        var api = Api(standalone: true);
        var team = await Loaded(api);
        var access = (await Choose(team, "Lena Huber")).Access!;
        access.NewPassword = "kurz";
        string? focus = null;
        access.FocusRequested += field => focus = field;
        await access.ResetPasswordCommand.ExecuteAsync(null);
        Assert.Equal("Das Startpasswort braucht mindestens 8 Zeichen.", access.NewPasswordError);
        Assert.Equal(PersonAccessViewModel.NewPasswordField, focus);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/staff/u-lena/password");
        access.NewPassword = "neu-12345";
        Assert.Null(access.NewPasswordError);

        await access.ResetPasswordCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == "POST /api/staff/u-lena/password");
        Assert.Equal("", access.NewPassword);
        Assert.True(access.HasResetText);
    }

    [Fact]
    public async Task Given_befristeter_Zugang_When_verlaengert_Then_30_Tage_ab_dem_spaeteren_Tag_und_gilt_erst_mit_Speichern()
    {
        var api = Api();
        var team = await Loaded(api);
        var access = (await Choose(team, "Lena Huber")).Access!;
        Assert.False(access.CanExtend);
        var today = DateOnly.FromDateTime(DateTime.Now);

        access.Until = today.AddDays(10);
        access.ExtendCommand.Execute(null);
        Assert.Equal(today.AddDays(40), access.Until);
        access.Until = today.AddDays(-5);
        access.ExtendCommand.Execute(null);
        Assert.Equal(today.AddDays(30), access.Until);
        Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/staff/u-lena");

        Assert.True(await team.SaveChangesAsync());
        Assert.NotNull(JsonSerializer.Deserialize<StaffAccessInput>(Assert.Single(api.Calls, c => c.Route == "PUT /api/staff/u-lena").Body!, ApiClient.Json)!.ExpiresAt);
    }

    [Fact]
    public async Task Given_Lena_When_gesperrt_und_gespeichert_Then_zeigt_die_Kopfzeile_den_neuen_Stand()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");
        Assert.Equal("Therapeut:in · Wien", card.HeadLine);
        Assert.Equal("Zugang aktiv", card.PillText);
        Assert.True(card.PillIsOk);

        card.Access!.IsActive = false;
        Assert.True(await team.SaveChangesAsync());

        Assert.Same(card, team.Card);
        Assert.Equal("Gesperrt", card.PillText);
        Assert.True(card.PillIsCritical);
        Assert.False(card.PillIsOk);
    }

    [Fact]
    public async Task Given_Marco_ohne_Zugang_When_eingeladen_Then_Reiter_zeigt_die_offene_Einladung_und_der_Link_bleibt()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Marco Berger");
        var access = card.Access!;
        Assert.True(access.IsInvite);
        access.InviteEmail = "marco@spa.test";

        await access.InviteCommand.ExecuteAsync(null);

        Assert.Same(card, team.Card);
        Assert.Equal("Eingeladen", card.PillText);
        Assert.True(card.PillIsNeutral);
        Assert.True(access.IsInvitation);
        Assert.Equal("Offene Einladung", access.Heading);
        Assert.False(access.ShowsFields);
        Assert.True(access.HasSent);
        Assert.Equal($"{TeamSample.PublicUrl}/?einladung={TeamSample.Token}", access.InviteLink);
        Assert.False(team.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_angefangene_Einladung_When_andere_Zeile_gewaehlt_Then_Rueckfrage_Speichern_sendet_nicht_und_Verwerfen_wechselt()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed).Answer(ConfirmChoice.Alternative);
        var api = Api();
        var team = await Loaded(api, dialogs: dialogs);
        var card = await Invite(team);
        card.Access!.InviteEmail = "nina@spa.test";

        team.SelectedEntry = team.Entries.Single(e => e.Title == "Lena Huber");
        await Until(() => dialogs.Asked.Count == 1 && team.HasError);
        await Task.Yield();
        Assert.Same(card, team.Card);
        Assert.Equal("nina@spa.test", card.Access.InviteEmail);
        Assert.Equal("Die Einladung ist noch nicht gesendet. Sende sie oder verwirf die Eingaben.", team.Error);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/staff/invitations");

        team.SelectedEntry = team.Entries.Single(e => e.Title == "Lena Huber");
        await Until(() => team.Card?.Title == "Lena Huber");
    }

    private static StubApi TeamWith(StaffMemberDto access) => TeamSample.AddTeam(new StubApi()
            .On("GET /api/qualifications", new List<QualificationDto> { HotStone, Cosmetics })
            .On("GET /api/locations", new List<LocationDto> { Vienna }),
        [Lena, Marco], [Sophie], [access], []);

    [Theory]
    [InlineData(3, "Endet bald, bis ")]
    [InlineData(30, "bis ")]
    public async Task Given_befristeter_Zugang_When_die_Liste_geladen_ist_Then_steht_das_Ende_in_der_Zeile(int days, string expected)
    {
        var end = DateTimeOffset.UtcNow.AddDays(days);
        var team = await Loaded(TeamWith(LenaAccess with { ExpiresAt = end }));

        var text = TimeZoneInfo.ConvertTime(end.AddTicks(-1), TimeZoneInfo.Local).ToString("d.M.yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var lena = team.Entries.Single(e => e.Title == "Lena Huber");
        Assert.Equal($"{expected}{text}", lena.Badge);
        Assert.Equal(EntryBadgeKind.Warning, lena.BadgeKind);
    }

    [Fact]
    public async Task Given_abgelaufener_oder_gesperrter_Zugang_When_die_Liste_geladen_ist_Then_steht_kein_Datum_in_der_Zeile()
    {
        var past = await Loaded(TeamWith(LenaAccess with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) }));
        var blocked = await Loaded(TeamWith(LenaAccess with { IsActive = false, ExpiresAt = DateTimeOffset.UtcNow.AddDays(3) }));

        Assert.Equal(("Abgelaufen", EntryBadgeKind.Critical), (past.Entries.Single(e => e.Title == "Lena Huber").Badge, past.Entries.Single(e => e.Title == "Lena Huber").BadgeKind));
        Assert.Equal(("Gesperrt", EntryBadgeKind.Critical), (blocked.Entries.Single(e => e.Title == "Lena Huber").Badge, blocked.Entries.Single(e => e.Title == "Lena Huber").BadgeKind));
    }

    [Fact]
    public async Task Given_Module_nicht_lesbar_When_geladen_Then_LoadError_und_Erneut_laden_liest_neu()
    {
        var api = Api(standalone: true).On("GET /api/system/modules", new { title = "Fehler" }, HttpStatusCode.InternalServerError);
        var team = new TeamViewModel(api.Client(), TestData.Owner(), TimeProvider.System);

        await team.LoadAsync();

        Assert.True(team.HasLoadError);
        Assert.False(team.IsLoaded);

        api.On("GET /api/system/modules", new SystemModules(true, false, false, false, false, false, false, false));
        await team.LoadAsync();

        Assert.False(team.HasLoadError);
        Assert.Equal("Zugang anlegen", team.InviteLabel);
    }

    [Fact]
    public async Task Given_Zugang_ohne_Person_When_Marco_zugeordnet_und_gespeichert_Then_oeffnet_die_Karte_von_Marco()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Rita Wagner");
        card.Access!.Person = card.Access.People.Single(p => p.Id == Marco.Id);

        Assert.True(await team.SaveChangesAsync());

        Assert.DoesNotContain(team.Entries, e => e.Title == "Rita Wagner");
        Assert.Equal("Marco Berger", team.Card?.Title);
        Assert.Equal("Marco Berger", team.SelectedEntry?.Title);
        Assert.Equal("u-rita", team.Card?.Entry.UserId);
        Assert.Equal("Gespeichert", team.Toast.Text);
    }

    [Fact]
    public async Task Given_Arbeitszeit_an_einem_Standort_den_die_Leitung_nicht_sieht_When_gespeichert_Then_bleibt_die_Id_erhalten()
    {
        var graz = Guid.NewGuid();
        var hours = new WorkingHours(graz, DayOfWeek.Tuesday, new(9, 0), new(17, 0));
        var both = Lena with { WorkingHours = [.. Lena.WorkingHours, hours] };
        var api = TeamSample.AddTeam(new StubApi()
                .On("GET /api/qualifications", new List<QualificationDto> { HotStone, Cosmetics })
                .On("GET /api/locations", new List<LocationDto> { Vienna }),
            [both], [], [], []);
        var team = await Loaded(api, LocationManager());
        var card = await Choose(team, "Lena Huber");
        Assert.False(team.HasUnsavedChanges);
        Assert.Equal("Anderer Standort", card.Details!.WorkingHours[1].Location?.Label);

        card.Details.PreferredName = "Lena M.";
        Assert.True(await team.SaveChangesAsync());

        var input = JsonSerializer.Deserialize<EmployeeInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/employees/{Lena.Id}").Body!, ApiClient.Json)!;
        Assert.Equal([Vienna.Id, graz], input.WorkingHours.Select(h => h.LocationId));
    }

    [Fact]
    public async Task Given_zwei_Standorte_When_ein_Standort_gewaehlt_Then_zeigt_die_Liste_nur_Personen_dieses_Standorts()
    {
        var graz = new LocationDto(Guid.NewGuid(), "Graz Zentrum", "Europe/Vienna", "EUR", []);
        var inGraz = Marco with { WorkingHours = [new WorkingHours(graz.Id, DayOfWeek.Friday, new(9, 0), new(17, 0))] };
        var api = TeamSample.AddTeam(new StubApi()
                .On("GET /api/qualifications", new List<QualificationDto> { HotStone, Cosmetics })
                .On("GET /api/locations", new List<LocationDto> { Vienna, graz }),
            [Lena, inGraz], [], [], []);
        var team = await Loaded(api);

        Assert.True(team.ShowsLocationChips);
        Assert.Equal(["Alle", "Wien", "Graz Zentrum"], team.LocationChips.Select(c => c.Label));
        Assert.Equal(["Lena Huber", "Marco Berger"], team.Entries.Select(e => e.Title));

        team.LocationChips[2].SelectCommand.Execute(null);
        Assert.Equal(["Marco Berger"], team.Entries.Select(e => e.Title));
        Assert.True(team.LocationChips[2].IsOn);
        Assert.False(team.LocationChips[0].IsOn);

        team.LocationChips[0].SelectCommand.Execute(null);
        Assert.Equal(["Lena Huber", "Marco Berger"], team.Entries.Select(e => e.Title));
    }

    [Fact]
    public async Task Given_ein_Standort_When_die_Liste_geladen_ist_Then_gibt_es_keine_Standort_Chips()
    {
        var team = await Loaded(Api());

        Assert.False(team.ShowsLocationChips);
    }

    [Fact]
    public async Task Given_Person_hinzufuegen_When_gedrueckt_Then_oeffnet_die_Karte_ohne_dass_eine_Zeile_der_Liste_gewaehlt_ist()
    {
        var team = await Loaded(Api());
        Assert.NotNull(team.ListSelection);

        var card = await Add(team);

        Assert.Equal("Neue Person", card.Title);
        Assert.Null(team.ListSelection);
        Assert.DoesNotContain(team.Entries, e => e.IsNew);

        // Die Suche baut die Liste neu; die offene neue Person bleibt offen.
        team.Search = "lena";
        Assert.Same(card, team.Card);
        Assert.Equal(TeamEntryKind.NewPerson, team.SelectedEntry?.Kind);
    }

    [Fact]
    public async Task Given_leeres_Team_When_geladen_Then_Leerzustand_und_Person_hinzufuegen_ist_die_erste_Aktion()
    {
        var api = TeamSample.AddTeam(new StubApi()
                .On("GET /api/qualifications", new List<QualificationDto>())
                .On("GET /api/locations", new List<LocationDto> { Vienna }),
            [], [], [], []);
        var team = await Loaded(api);

        Assert.True(team.HasNoEntries);
        Assert.Null(team.Card);
        Assert.True(team.CanAddPerson);
        var card = await Add(team);
        Assert.Equal("Neue Person", card.Title);
    }

    [Fact]
    public async Task Given_Lena_When_Regelarbeitszeit_geaendert_Then_steht_sie_im_Bereich_Arbeitszeit_und_die_Leiste_speichert_sie_mit_den_Stammdaten()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");

        card.SelectTab(PersonCardViewModel.ProfileArea);
        Assert.False(card.ShowsWorkingHours);
        card.SelectTab(PersonCardViewModel.WorkingHoursTab);
        Assert.Equal(PersonCardViewModel.WorkTimeArea, card.SelectedTab);
        Assert.True(card.ShowsWorkingHours);
        Assert.False(card.ShowsDetails);
        Assert.False(card.HasSaveBar);
        Assert.Equal("Frei: Dienstag, Mittwoch, Donnerstag, Freitag, Samstag, Sonntag", card.Details!.FreeDaysText);

        card.Details.AddHoursCommand.Execute(null);
        Assert.Equal("Frei: Mittwoch, Donnerstag, Freitag, Samstag, Sonntag", card.Details.FreeDaysText);
        Assert.True(card.HasSaveBar);
        Assert.True(await team.SaveChangesAsync());

        var input = JsonSerializer.Deserialize<EmployeeInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/employees/{Lena.Id}").Body!, ApiClient.Json)!;
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], input.WorkingHours.Select(h => h.Day));
    }

    [Fact]
    public async Task Given_Lena_mit_Zugang_When_Zugang_sperren_im_Menue_Then_steht_der_Schalter_aus_und_gespeichert_wird_mit_der_Leiste()
    {
        var api = Api();
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");

        Assert.True(card.HasMoreMenu);
        Assert.NotNull(card.ToggleAccessMenu);
        Assert.NotNull(card.RemoveMenu);
        Assert.Equal("Zugang sperren", card.ToggleAccessLabel);

        card.ToggleAccessCommand.Execute(null);

        Assert.False(card.Access!.IsActive);
        Assert.Equal("Zugang freigeben", card.ToggleAccessLabel);
        Assert.True(team.HasUnsavedChanges);
        Assert.DoesNotContain(api.Calls, c => c.Route == "PUT /api/staff/u-lena");
        Assert.True(await team.SaveChangesAsync());
        Assert.False(JsonSerializer.Deserialize<StaffAccessInput>(Assert.Single(api.Calls, c => c.Route == "PUT /api/staff/u-lena").Body!, ApiClient.Json)!.IsActive);
    }

    [Fact]
    public async Task Given_Personen_mit_und_ohne_Zugang_When_Nur_ohne_Zugang_gewaehlt_Then_bleibt_nur_Marco()
    {
        var team = await Loaded(Api());
        Assert.True(team.CanFilterWithoutAccess);

        team.OnlyWithoutAccess = true;

        Assert.Equal(["Marco Berger"], team.Entries.Select(e => e.Title));
        team.OnlyWithoutAccess = false;
        Assert.Contains(team.Entries, e => e.Title == "Lena Huber");
    }

    [Fact]
    public async Task Given_Marco_ohne_Zugang_When_Karte_offen_Then_Menue_hat_nur_Aus_dem_Team_nehmen()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Marco Berger");

        Assert.True(card.HasMoreMenu);
        Assert.Null(card.ToggleAccessMenu);
        Assert.NotNull(card.RemoveMenu);
        Assert.Equal("Ohne Zugang", card.PillText);
        Assert.True(card.PillIsNeutral);
    }

    [Fact]
    public async Task Given_neue_Person_When_Karte_offen_Then_kein_Menue_und_kein_Kennzeichen()
    {
        var team = await Loaded(Api());
        var card = await Add(team);

        Assert.False(card.HasMoreMenu);
        Assert.False(card.HasPill);
        Assert.False(card.HasHeadLine);
    }
}
