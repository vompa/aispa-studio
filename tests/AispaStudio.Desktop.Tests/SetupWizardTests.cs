using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>ADR 0104, Entscheidungen 4 bis 6: Assistent „Betrieb einrichten“ mit den Schritten Öffnungszeiten und Räume.</summary>
public sealed partial class SetupWizardTests
{
    private static readonly Guid LocationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly LocationDto Location = new(LocationId, "Wien", "Europe/Vienna", "EUR",
        [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday }.Select(d => new OpeningHours(d, new TimeOnly(9, 0), new TimeOnly(18, 0)))], "AT-9");

    private static SetupProgressDto Progress(params string[] done) => new(
    [
        Step("location", "Standort anlegen", "masterData", "locations", done),
        Step("hours", "Öffnungszeiten eintragen", "masterData", "locations", done, "Es fehlen Öffnungszeiten."),
        Step("rooms", "Räume anlegen", "masterData", "locations", done, "Es fehlt ein Behandlungsraum."),
        Step("treatments", "Behandlungen anlegen", "masterData", "treatments", done, "Es fehlen Behandlungen."),
    ]);

    private static SetupStepDto Step(string key, string title, string page, string tab, string[] done, string? missing = null) =>
        new(key, title, "Hinweis.", done.Contains(key), page, tab, done.Contains(key) ? null : missing);

    private static StubApi Api(SetupProgressDto progress, params RoomDto[] rooms) => new StubApi()
        .On("GET /api/setup/progress?includeRegister=false", progress)
        .On("GET /api/locations", new List<LocationDto> { Location })
        .On("GET /api/setup/catalog", new List<SetupCatalogPackageDto>())
        .On("GET /api/employees", new List<EmployeeDto>())
        .On("GET /api/qualifications", new List<QualificationDto>())
        .On($"GET /api/locations/{LocationId}/resources", new LocationResources(Location, rooms, [], [], [], []));

    private static async Task<SetupWizardViewModel> OpenAsync(StubApi api)
    {
        var wizard = new SetupWizardViewModel(api.Client(), new ToastState(TimeProvider.System));
        await wizard.LoadAsync();
        return wizard;
    }

    /// <summary>Rot, wenn der Assistent nicht beim ersten offenen Schritt beginnt, Hinweis oder Standardknopf nicht zum Zustand passen oder ein Schritt ohne eigene Eingabe nicht zur Verwaltung führt.</summary>
    [Fact]
    public async Task Given_offene_Oeffnungszeiten_When_der_Assistent_laedt_Then_beginnt_er_dort_und_die_Schritte_ohne_Eingabe_fuehren_in_die_Verwaltung()
    {
        var wizard = await OpenAsync(Api(Progress("location")));

        Assert.Equal(("hours", "Schritt 2 von 4"), (wizard.Current!.Key, wizard.StepText));
        Assert.Equal("Es fehlen Öffnungszeiten.", wizard.Missing);
        Assert.True(wizard.IsHoursStep);
        Assert.Equal(SetupWizardDefault.Save, wizard.DefaultButton);
        Assert.True(wizard.ShowSkip);
        Assert.False(wizard.ShowNext);
        Assert.Equal(["Montag", "Dienstag", "Mittwoch"], wizard.Days.Where(d => d.IsOpen).Select(d => d.Label));

        wizard.NextCommand.Execute(null);
        wizard.NextCommand.Execute(null);
        SetupStepDto? requested = null;
        wizard.AdminRequested += step => requested = step;
        Assert.True(wizard.IsAdminStep);
        Assert.Equal(SetupWizardDefault.OpenAdmin, wizard.DefaultButton);
        wizard.OpenAdminCommand.Execute(null);

        Assert.Equal(("masterData", "treatments"), (requested!.Page, requested.Tab));
    }

    /// <summary>Rot, wenn „für alle übernehmen“ nur den ersten Tag setzt oder das Speichern andere Zeiten, einen anderen Standort oder keinen neuen Stand sendet.</summary>
    [Fact]
    public async Task Given_geaenderte_Zeiten_When_gespeichert_wird_Then_geht_PUT_mit_den_Zeiten_an_den_Standort_und_der_Stand_wird_neu_geladen()
    {
        var api = Api(Progress("location"));
        var wizard = await OpenAsync(api);
        api.On($"PUT /api/locations/{LocationId}", Location).On("GET /api/setup/progress?includeRegister=false", Progress("location", "hours"));

        wizard.Days[0].OpensText = "08:00";
        wizard.Days[0].ClosesText = "17:30";
        wizard.Days[5].IsOpen = true;
        wizard.CopyFirstDayCommand.Execute(null);
        Assert.True(wizard.HasUnsavedHours);
        await wizard.SaveHoursCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<LocationInput>(api.Calls.Single(c => c.Route == $"PUT /api/locations/{LocationId}").Body!, ApiClient.Json)!;
        Assert.Equal(["Monday", "Tuesday", "Wednesday", "Saturday"], sent.OpeningHours.Select(h => h.Day.ToString()));
        Assert.All(sent.OpeningHours, h => Assert.Equal((new TimeOnly(8, 0), new TimeOnly(17, 30)), (h.Opens, h.Closes)));
        Assert.Equal(("Wien", "AT-9", null), (sent.Name, sent.Region, sent.Contact));
        Assert.Equal(2, api.Calls.Count(c => c.Route == "GET /api/setup/progress?includeRegister=false"));
        Assert.True(wizard.IsDone);
        Assert.False(wizard.HasUnsavedHours);
    }

    /// <summary>Rot, wenn „Raum hinzufügen“ ohne Namen anlegt, den Raum nicht als Behandlungsraum sendet oder danach den Stand nicht neu lädt.</summary>
    [Fact]
    public async Task Given_ein_Name_When_Raum_hinzugefuegt_wird_Then_legt_POST_einen_Behandlungsraum_an_und_der_Stand_wird_neu_geladen()
    {
        var api = Api(Progress("location", "hours"));
        var wizard = await OpenAsync(api);
        var room = new RoomDto(Guid.NewGuid(), LocationId, "Meerblick", 10);
        api.On($"POST /api/locations/{LocationId}/rooms", room)
            .On($"GET /api/locations/{LocationId}/resources", new LocationResources(Location, [room], [], [], [], []))
            .On("GET /api/setup/progress?includeRegister=false", Progress("location", "hours", "rooms"));

        Assert.Equal("rooms", wizard.Current!.Key);
        await wizard.AddRoomCommand.ExecuteAsync(null);
        Assert.Equal(SetupWizardViewModel.RoomNameMissing, wizard.Error);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST", StringComparison.Ordinal));

        wizard.NewRoomName = " Meerblick ";
        await wizard.AddRoomCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<RoomInput>(api.Calls.Single(c => c.Route == $"POST /api/locations/{LocationId}/rooms").Body!, ApiClient.Json)!;
        Assert.Equal(("Meerblick", false), (sent.Name, sent.IsArea));
        Assert.Equal(["Meerblick"], wizard.Rooms);
        Assert.Equal("", wizard.NewRoomName);
        Assert.True(wizard.IsDone);
        Assert.Null(wizard.Error);
    }

    /// <summary>Rot, wenn Enter im erledigten Schritt Öffnungszeiten weiterspringt statt zu speichern (das Zeitfeld übernimmt erst bei Enter).</summary>
    [Fact]
    public async Task Given_erledigter_Schritt_Oeffnungszeiten_When_der_Standardknopf_ausloest_Then_genau_ein_PUT_und_kein_Weiterspringen()
    {
        var api = Api(Progress("location", "hours"));
        var wizard = await OpenAsync(api);
        wizard.BackCommand.Execute(null);
        api.On($"PUT /api/locations/{LocationId}", Location);

        Assert.Equal(("hours", SetupWizardDefault.Save), (wizard.Current!.Key, wizard.DefaultButton));
        await wizard.SaveHoursCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == $"PUT /api/locations/{LocationId}");
        Assert.Equal("hours", wizard.Current.Key);
    }

    /// <summary>Rot, wenn Schrittwechsel oder Raum-Anlage ungespeicherte Zeiten verwerfen oder Esc sie wegschließt.</summary>
    [Fact]
    public async Task Given_ungespeicherte_Zeiten_When_der_Schritt_wechselt_und_ein_Raum_angelegt_wird_Then_bleiben_die_Zeiten_und_Esc_schliesst_nicht()
    {
        var api = Api(Progress("location"));
        var wizard = await OpenAsync(api);
        var closed = false;
        wizard.CloseRequested += () => closed = true;
        api.On($"POST /api/locations/{LocationId}/rooms", new RoomDto(Guid.NewGuid(), LocationId, "Meerblick", 10));

        wizard.Days[0].OpensText = "08:00";
        wizard.NextCommand.Execute(null);
        wizard.NewRoomName = "Meerblick";
        await wizard.AddRoomCommand.ExecuteAsync(null);

        Assert.Equal("08:00", wizard.Days[0].OpensText);
        Assert.True(wizard.HasUnsavedHours);
        Assert.False(wizard.TryCloseWithEscape());
        Assert.False(closed);
    }

    /// <summary>Rot, wenn ein gescheitertes Neuladen nach dem POST verschluckt wird oder der angelegte Raum in der Liste fehlt.</summary>
    [Fact]
    public async Task Given_Neuladen_scheitert_nach_erfolgreichem_POST_When_Raum_hinzugefuegt_Then_Meldung_und_Raum_steht_in_der_Liste()
    {
        var api = Api(Progress("location", "hours"));
        var wizard = await OpenAsync(api);
        api.On($"POST /api/locations/{LocationId}/rooms", new RoomDto(Guid.NewGuid(), LocationId, "Meerblick", 10))
            .On("GET /api/setup/progress?includeRegister=false", null, System.Net.HttpStatusCode.InternalServerError);

        wizard.NewRoomName = "Meerblick";
        await wizard.AddRoomCommand.ExecuteAsync(null);

        Assert.Equal(SetupWizardViewModel.NotLoaded, wizard.Error);
        Assert.Equal(["Meerblick"], wizard.Rooms);
        Assert.False(wizard.IsBusy);
    }

    /// <summary>Rot, wenn der Assistent nach „Spa einrichten“ nicht von selbst aufgeht oder die Karte ihn nicht startet.</summary>
    [Fact]
    public async Task Given_Spa_einrichten_abgeschlossen_When_die_Shell_steht_Then_ist_der_Assistent_offen_und_die_Karte_startet_ihn_erneut()
    {
        var owner = TestData.Owner();
        var api = Api(Progress("location"))
            .On("GET /api/system/status", new SystemStatus("0.9.0", "Test", DateTimeOffset.UtcNow))
            .On("GET /api/auth/me", null, System.Net.HttpStatusCode.Unauthorized)
            .On("GET /api/system/modules", new SystemModules(true, false, false, false, false, false, false, false))
            .On("GET /api/auth/standalone/setup", new StandaloneSetupState(true))
            .On("POST /api/auth/standalone/setup", owner)
            .On("GET /api/tenant", new TenantDto(Guid.NewGuid(), "Spa", "AT", CompanyProfile.Empty))
            .On("GET /api/tenant/embedding", new EmbeddingSettings([], null))
            .On("GET /api/tenant/history", new List<HistoryEntry>());
        var main = new MainViewModel(api.Client());
        await main.StartAsync();
        var login = Assert.IsType<LoginViewModel>(main.Content);
        login.SpaName = "Spa am See";
        login.LocationName = "Wien";
        login.OwnerName = "Eva Huber";
        login.UserName = "eva";
        login.Password = login.NewPasswordRepeat = "seeblick-2026";

        await login.CompleteSetupCommand.ExecuteAsync(null);

        var shell = Assert.IsType<ShellViewModel>(main.Content);
        Assert.Equal("hours", shell.SetupWizard!.Current!.Key);
        shell.SetupWizard.CloseCommand.Execute(null);
        Assert.Null(shell.SetupWizard);

        await shell.AdminPage.Company!.EnsureLoadedAsync();
        shell.AdminPage.Company.StartSetupWizardCommand.Execute(null);
        Assert.NotNull(shell.SetupWizard);
    }
}
