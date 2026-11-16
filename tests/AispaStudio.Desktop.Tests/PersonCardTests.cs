using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>ADR 0098: Personenkarte mit Übersicht, vier Bereichen, Abschnitten als Sprungzielen und dem Verlauf als Seitenleiste.</summary>
public sealed class PersonCardTests
{
    private static readonly QualificationDto HotStone = new(Guid.NewGuid(), "Hot Stone");
    private static readonly LocationDto Vienna = new(SpaSample.LocationId, "Wien Innere Stadt", "Europe/Vienna", "EUR", []);
    private static readonly EmployeeDto Lena = new(Guid.NewGuid(), "Lena Huber", "#2f6f5e", [HotStone.Id],
        [new WorkingHours(Vienna.Id, DayOfWeek.Monday, new(9, 0), new(17, 0))], ["en"], TherapistGender.Female, "Lena");
    private static readonly EmployeeDto Marco = new(Guid.NewGuid(), "Marco Berger", "#7a4fa3", [], []);
    private static readonly EmployeeDto Sophie = new(Guid.NewGuid(), "Sophie Gruber", "#b5524a", [], []);
    private static readonly StaffMemberDto LenaAccess = TeamSample.Access("u-lena", Lena, "", StaffRole.Therapist, [Vienna.Id]);

    /// <summary>Montag, 19.10.2026, 8:00 in Wien.</summary>
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);

    private static StubApi Api() => TeamSample.AddTeam(new StubApi()
            .On("GET /api/qualifications", new List<QualificationDto> { HotStone })
            .On("GET /api/locations", new List<LocationDto> { Vienna }),
        [Lena, Marco], [Sophie], [LenaAccess], []);

    private static readonly Dictionary<string, string> AreaOf = new()
    {
        [PersonCardViewModel.DetailsTab] = PersonCardViewModel.ProfileArea,
        [PersonCardViewModel.AccessTab] = PersonCardViewModel.ProfileArea,
        [PersonCardViewModel.QualificationsTab] = PersonCardViewModel.ProfileArea,
        [PersonCardViewModel.DutiesTab] = PersonCardViewModel.ProfileArea,
        [PersonCardViewModel.RosterTab] = PersonCardViewModel.WorkTimeArea,
        [PersonCardViewModel.TimesTab] = PersonCardViewModel.WorkTimeArea,
        [PersonCardViewModel.PayrollTab] = PersonCardViewModel.PayAndFileArea,
        [PersonCardViewModel.HrFileTab] = PersonCardViewModel.PayAndFileArea,
        [PersonCardViewModel.RegulationsTab] = PersonCardViewModel.PayAndFileArea,
    };

    private static string OverviewRoute(EmployeeDto employee) => $"GET /api/employees/{employee.Id}/overview";

    private static async Task<TeamViewModel> Loaded(StubApi api, CurrentUser? user = null)
    {
        var team = new TeamViewModel(api.Client(), user ?? TestData.Owner(), new ManualTime(Monday));
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

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    /// <summary>Wie <see cref="TestData.Manager"/>, aber mit Stammdaten: Standortleitung sieht Dienstplan, Zeiten und Anträge, nicht die Personalakte.</summary>
    private static CurrentUser LocationManager() =>
        new("u4", "leitung@aispa.test", "Markus", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, false, [Vienna.Id]), null,
            [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ChangeStatus, AccessRight.ManageLocation, AccessRight.ManageCatalog, AccessRight.ManageStaff]);

    [Fact]
    public async Task Inhaberin_oeffnet_Lena_und_landet_in_der_Uebersicht_mit_fuenf_Kacheln()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");
        await Until(() => card.Overview!.Tiles.Count > 0);

        Assert.Equal(PersonCardViewModel.OverviewArea, card.SelectedTab);
        Assert.True(card.ShowsOverview);
        Assert.False(card.ShowsDetails);
        Assert.Null(card.FocusedSection);
        var tiles = card.Overview!.Tiles;
        Assert.Equal(["Nächster Dienst", "Diese Woche", "Resturlaub 2026", "Offene Anträge", "Zertifikate"], tiles.Select(t => t.Label));
        Assert.Equal(["Di. 20.10., 09:00–17:00", "32 h geplant", "24,5 Tage", "1", "2 fällig"], tiles.Select(t => t.Value));
        Assert.Equal(["Wien Innere Stadt", "28,5 h erfasst", "2 Tage geplant", null, "davon 1 abgelaufen"], tiles.Select(t => t.Hint));
        Assert.Equal([StatTileState.None, StatTileState.None, StatTileState.None, StatTileState.Warn, StatTileState.Warn], tiles.Select(t => t.State));
    }

    [Fact]
    public async Task Teile_ohne_Recht_bekommen_keine_Kachel()
    {
        // Die Standortleitung bekommt vom Server keine Zertifikate; ohne Personalunterlagen fehlt auch das Urlaubskonto nicht, hier fehlt es zur Probe.
        var api = Api().On(OverviewRoute(Lena), TeamSample.Overview(Lena.Id) with { Leave = null, DueCertificates = null, ExpiredCertificates = null });
        var team = await Loaded(api, LocationManager());
        var card = await Choose(team, "Lena Huber");
        await Until(() => card.Overview!.Tiles.Count > 0);

        Assert.Equal(["Nächster Dienst", "Diese Woche", "Offene Anträge"], card.Overview!.Tiles.Select(t => t.Label));
    }

    [Fact]
    public async Task Nur_Zertifikate_ohne_Dienstplanrecht_zeigt_nur_die_Kachel_Zertifikate()
    {
        var api = Api().On(OverviewRoute(Lena), new EmployeeOverviewDto(Lena.Id, null, null, null, null, null, 0, 0));
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        await Until(() => card.Overview!.Tiles.Count > 0);

        var tile = Assert.Single(card.Overview!.Tiles);
        Assert.Equal(("Zertifikate", "Alle gültig", StatTileState.Ok), (tile.Label, tile.Value, tile.State));
    }

    [Fact]
    public async Task Ohne_Dienst_ohne_Anspruch_und_heute_stehen_die_Ersatztexte()
    {
        var empty = new EmployeeOverviewDto(Lena.Id, null, 0m, 0m, new EmployeeLeaveSummaryDto(2026, null, 1m, 0m), 0, 0, 0);
        var api = Api().On(OverviewRoute(Lena), empty);
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        await Until(() => card.Overview!.Tiles.Count > 0);

        var tiles = card.Overview!.Tiles;
        Assert.Equal(("Keiner geplant", "in den nächsten 31 Tagen"), (tiles[0].Value, tiles[0].Hint));
        Assert.Equal(("0 h geplant", "0 h erfasst"), (tiles[1].Value, tiles[1].Hint));
        Assert.Equal(("Kein Anspruch hinterlegt", "1 Tag geplant"), (tiles[2].Value, tiles[2].Hint));
        Assert.Equal(("0", StatTileState.None), (tiles[3].Value, tiles[3].State));

        var today = TeamSample.Overview(Lena.Id) with
        {
            NextShift = new EmployeeNextShiftDto(new DateOnly(2026, 10, 19), new(9, 0), new(17, 0), Vienna.Id, Vienna.Name),
            Leave = new EmployeeLeaveSummaryDto(2026, -1m, 0m, 26m),
        };
        api.On(OverviewRoute(Lena), today);
        await card.Overview.LoadAsync();

        Assert.Equal("Heute, 09:00–17:00", card.Overview.Tiles[0].Value);
        Assert.Equal(("-1 Tage", StatTileState.Warn), (card.Overview.Tiles[2].Value, card.Overview.Tiles[2].State));
    }

    [Fact]
    public async Task Ladefehler_der_Uebersicht_zeigt_den_Hinweis_und_Erneut_laden_holt_die_Kacheln()
    {
        var api = Api().On(OverviewRoute(Lena), new { title = "Die Übersicht ist gerade nicht erreichbar." }, HttpStatusCode.InternalServerError);
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        await Until(() => card.Overview!.HasLoadError);

        Assert.Empty(card.Overview!.Tiles);
        Assert.False(card.Overview.ShowsLoading);

        api.On(OverviewRoute(Lena), TeamSample.Overview(Lena.Id));
        await card.Overview.LoadCommand.ExecuteAsync(null);

        Assert.False(card.Overview.HasLoadError);
        Assert.Equal(5, card.Overview.Tiles.Count);
    }

    [Theory]
    [InlineData(0, PersonCardViewModel.WorkTimeArea, PersonCardViewModel.RosterTab)]
    [InlineData(1, PersonCardViewModel.WorkTimeArea, PersonCardViewModel.TimesTab)]
    [InlineData(2, PersonCardViewModel.WorkTimeArea, PersonCardViewModel.RosterTab)]
    [InlineData(4, PersonCardViewModel.ProfileArea, PersonCardViewModel.DutiesTab)]
    public async Task Klick_auf_eine_Kachel_springt_in_ihren_Abschnitt(int tile, string area, string section)
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");
        await Until(() => card.Overview!.Tiles.Count > 0);
        var requested = new List<string>();
        card.SectionRequested += requested.Add;

        card.Overview!.Tiles[tile].Command.Execute(null);

        Assert.Equal(area, card.SelectedTab);
        Assert.Equal(section, card.FocusedSection);
        Assert.Equal([section], requested);
        Assert.False(card.ShowsOverview);
    }

    [Fact]
    public async Task Klick_auf_Offene_Antraege_oeffnet_die_Ansicht_Antraege()
    {
        var team = await Loaded(Api().On("GET /api/absence-requests", new List<AbsenceRequestDto>()));
        var card = await Choose(team, "Lena Huber");
        await Until(() => card.Overview!.Tiles.Count > 0);

        card.Overview!.Tiles.Single(t => t.Label == "Offene Anträge").Command.Execute(null);

        await Until(() => team.Mode == TeamViewModel.RequestsMode);
    }

    [Theory]
    [InlineData(PersonCardViewModel.DetailsTab, PersonCardViewModel.ProfileArea)]
    [InlineData(PersonCardViewModel.AccessTab, PersonCardViewModel.ProfileArea)]
    [InlineData(PersonCardViewModel.QualificationsTab, PersonCardViewModel.ProfileArea)]
    [InlineData(PersonCardViewModel.DutiesTab, PersonCardViewModel.ProfileArea)]
    [InlineData(PersonCardViewModel.RosterTab, PersonCardViewModel.WorkTimeArea)]
    [InlineData(PersonCardViewModel.TimesTab, PersonCardViewModel.WorkTimeArea)]
    [InlineData(PersonCardViewModel.PayrollTab, PersonCardViewModel.PayAndFileArea)]
    [InlineData(PersonCardViewModel.HrFileTab, PersonCardViewModel.PayAndFileArea)]
    [InlineData(PersonCardViewModel.RegulationsTab, PersonCardViewModel.PayAndFileArea)]
    public async Task Jede_alte_Reiter_Id_oeffnet_ihren_Bereich_und_zeigt_den_Abschnitt(string section, string area)
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");

        team.Open(Lena.Id, section, null);

        Assert.Equal(area, card.SelectedTab);
        Assert.Equal(section, card.FocusedSection);
        Assert.Single(card.Tabs, t => t.IsOn && t.Id == area);
        var shown = new Dictionary<string, bool>
        {
            [PersonCardViewModel.DetailsTab] = card.ShowsDetails,
            [PersonCardViewModel.AccessTab] = card.ShowsAccess,
            [PersonCardViewModel.QualificationsTab] = card.ShowsQualifications,
            [PersonCardViewModel.DutiesTab] = card.ShowsDuties,
            [PersonCardViewModel.RosterTab] = card.ShowsRoster,
            [PersonCardViewModel.TimesTab] = card.ShowsTimes,
            [PersonCardViewModel.PayrollTab] = card.ShowsPayroll,
            [PersonCardViewModel.HrFileTab] = card.ShowsHrFile,
            [PersonCardViewModel.RegulationsTab] = card.ShowsRegulations,
        };
        Assert.True(shown[section]);
        // Sichtbar ist genau, was im selben Bereich steht.
        Assert.All(shown, s => Assert.Equal(AreaOf[s.Key] == area, s.Value));
        Assert.False(card.IsHistoryOpen);
    }

    [Fact]
    public async Task Verlauf_oeffnet_die_Seitenleiste_und_laesst_den_Bereich()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");
        card.SelectTab(PersonCardViewModel.PayAndFileArea);

        team.Open(Lena.Id, PersonCardViewModel.HistoryTab, null);

        Assert.True(card.IsHistoryOpen);
        Assert.Equal(PersonCardViewModel.PayAndFileArea, card.SelectedTab);
        card.ToggleHistoryCommand.Execute(null);
        Assert.False(card.IsHistoryOpen);
    }

    [Fact]
    public async Task Derselbe_Abschnitt_zweimal_angesprungen_meldet_sich_zweimal()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");
        var requested = new List<string>();
        card.SectionRequested += requested.Add;

        card.SelectTab(PersonCardViewModel.TimesTab);
        card.SelectTab(PersonCardViewModel.TimesTab);
        card.SelectTab(PersonCardViewModel.WorkTimeArea);

        Assert.Equal([PersonCardViewModel.TimesTab, PersonCardViewModel.TimesTab], requested);
        Assert.Null(card.FocusedSection);
    }

    [Theory]
    [InlineData("unbekannt")]
    [InlineData(null)]
    public async Task Unbekanntes_Ziel_faellt_auf_die_Uebersicht_zurueck(string? id)
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");
        card.SelectTab(PersonCardViewModel.ProfileArea);

        card.SelectTab(id);

        Assert.Equal(PersonCardViewModel.OverviewArea, card.SelectedTab);
        Assert.Null(card.FocusedSection);
    }

    [Fact]
    public async Task Nicht_erlaubter_Abschnitt_faellt_auf_den_Startbereich_zurueck()
    {
        var team = await Loaded(Api(), LocationManager());
        var card = await Choose(team, "Lena Huber");
        card.SelectTab(PersonCardViewModel.ProfileArea);

        card.SelectTab(PersonCardViewModel.HrFileTab);

        Assert.False(card.HasSection(PersonCardViewModel.HrFileTab));
        Assert.Equal(PersonCardViewModel.OverviewArea, card.SelectedTab);
        Assert.False(card.ShowsHrFile);
    }

    [Fact]
    public async Task Wechsel_der_Person_schliesst_den_Verlauf()
    {
        var team = await Loaded(Api());
        var lena = await Choose(team, "Lena Huber");
        lena.IsHistoryOpen = true;

        var marco = await Choose(team, "Marco Berger");

        Assert.NotSame(lena, marco);
        Assert.False(marco.IsHistoryOpen);
    }

    [Fact]
    public async Task Speicherleiste_steht_nur_in_Bereichen_mit_Abschnitten_der_Karte_oder_bei_offenen_Aenderungen()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");

        var bars = new Dictionary<string, bool>();
        foreach (var area in card.Tabs.Select(t => t.Id))
        {
            card.SelectTab(area);
            bars[area] = card.HasSaveBar;
        }

        Assert.Equal(new Dictionary<string, bool>
        {
            [PersonCardViewModel.OverviewArea] = false,
            [PersonCardViewModel.ProfileArea] = true,
            [PersonCardViewModel.WorkTimeArea] = false,
            [PersonCardViewModel.PayAndFileArea] = true,
        }, bars);

        card.SelectTab(PersonCardViewModel.ProfileArea);
        card.Details!.PreferredName = "Lena M.";
        card.SelectTab(PersonCardViewModel.WorkTimeArea);
        Assert.True(card.HasSaveBar);
        Assert.True(team.HasSaveBar);
        card.SelectTab(PersonCardViewModel.OverviewArea);
        Assert.True(card.HasSaveBar);

        team.Discard();
        Assert.False(card.HasSaveBar);
    }

    [Fact]
    public async Task Neue_Person_startet_im_Profil_ohne_Uebersicht_und_ohne_Schiene()
    {
        var api = Api();
        var team = await Loaded(api);
        // Die erste Person ist schon gewählt und hat ihre Übersicht geladen; gezählt wird erst ab dem Wechsel.
        var before = api.Calls.Count;
        var card = await Choose(team, "Person anlegen");

        Assert.Null(card.Overview);
        Assert.Equal([PersonCardViewModel.ProfileArea], card.Tabs.Select(t => t.Id));
        Assert.Equal([PersonCardViewModel.DetailsTab, PersonCardViewModel.QualificationsTab], card.Sections);
        Assert.Equal(PersonCardViewModel.ProfileArea, card.SelectedTab);
        Assert.False(card.ShowsTabs);
        Assert.True(card.ShowsDetails);
        Assert.False(card.HasHistory);
        Assert.DoesNotContain(api.Calls.Skip(before), c => c.Route.EndsWith("/overview", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ausgeschiedene_hat_keine_Uebersicht_und_ist_nur_lesbar()
    {
        var team = await Loaded(Api());
        team.ShowFormer = true;
        var card = await Choose(team, "Sophie Gruber");

        Assert.Null(card.Overview);
        Assert.Equal([PersonCardViewModel.ProfileArea, PersonCardViewModel.PayAndFileArea], card.Tabs.Select(t => t.Id));
        Assert.Equal([PersonCardViewModel.DetailsTab, PersonCardViewModel.DutiesTab, PersonCardViewModel.HrFileTab, PersonCardViewModel.RegulationsTab], card.Sections);
        Assert.Equal(PersonCardViewModel.ProfileArea, card.SelectedTab);
        Assert.True(card.IsReadOnly);
        foreach (var area in card.Tabs.Select(t => t.Id))
        {
            card.SelectTab(area);
            Assert.False(card.HasSaveBar, area);
        }
    }

    [Fact]
    public async Task Abgelehnter_Zugang_beim_Speichern_springt_aus_der_Arbeitszeit_zum_Abschnitt_Zugang()
    {
        var api = Api().On("PUT /api/staff/u-lena", new { title = "Eingabe ungültig", errors = new Dictionary<string, string[]> { ["Role"] = [TeamSample.RejectRole] } },
            HttpStatusCode.BadRequest);
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        card.Access!.Role = RoleChoice.All.Single(r => r.Role == StaffRole.Owner);
        card.SelectTab(PersonCardViewModel.WorkTimeArea);

        Assert.False(await team.SaveChangesAsync());

        Assert.Equal(PersonCardViewModel.ProfileArea, card.SelectedTab);
        Assert.Equal(PersonCardViewModel.AccessTab, card.FocusedSection);
        Assert.True(card.ShowsAccess);
    }

    [Fact]
    public async Task Leerer_Name_beim_Speichern_springt_zum_Abschnitt_Stammdaten()
    {
        var team = await Loaded(Api());
        var card = await Choose(team, "Lena Huber");
        card.Details!.Name = "";
        card.SelectTab(PersonCardViewModel.OverviewArea);

        Assert.False(await team.SaveChangesAsync());

        Assert.Equal(PersonCardViewModel.ProfileArea, card.SelectedTab);
        Assert.Equal(PersonCardViewModel.DetailsTab, card.FocusedSection);
        Assert.True(card.ShowsDetails);
    }

    [Fact]
    public async Task Gespeichertes_Urlaubskonto_laedt_die_Uebersicht_neu()
    {
        DayOfWeek[] workdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
        var route = $"/api/employees/{Lena.Id}/leave-account/2026";
        var api = Api()
            .On($"GET {route}", new LeaveAccountDto(Lena.Id, 2026, 25, 0, 0, 0, 25, 0, workdays, false))
            .On($"PUT {route}", new LeaveAccountDto(Lena.Id, 2026, 27, 0, 0, 0, 27, 0, workdays, false));
        var team = await Loaded(api);
        var card = await Choose(team, "Lena Huber");
        await Until(() => card.Overview!.Tiles.Count > 0);
        card.SelectTab(PersonCardViewModel.WorkTimeArea);
        await Until(() => card.Leave!.IsLoaded);
        api.On(OverviewRoute(Lena), TeamSample.Overview(Lena.Id) with { Leave = new EmployeeLeaveSummaryDto(2026, 27m, 0m, 0m) });

        card.Leave!.EntitlementText = "27";
        Assert.True(await card.Leave.SaveChangesAsync());

        await Until(() => card.Overview!.Tiles.Any(t => t.Value == "27 Tage"));
    }
}
