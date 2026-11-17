using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>ADR 0099, Entscheidung 6: die Karte „Betrieb einrichten“ oben auf „Firmendaten“, solange Schritte offen sind; die API zählt, jeder Schritt springt in die passende Maske.</summary>
public sealed class AdminSetupCardTests
{
    private static SetupProgressDto Progress(params string[] done) => new(
    [
        Step("location", "Standort anlegen", "masterData", "locations", done),
        Step("hours", "Öffnungszeiten eintragen", "masterData", "locations", done),
        Step("rooms", "Räume anlegen", "masterData", "locations", done),
        Step("treatments", "Behandlungen anlegen", "masterData", "treatments", done),
        Step("people", "Team anlegen", "masterData", "team", done),
        Step("register", "Kassa einrichten", "masterData", "kassa", done),
    ]);

    private static SetupStepDto Step(string key, string title, string page, string tab, string[] done) => new(key, title, "Hinweis.", done.Contains(key), page, tab);

    private static StubApi Api(SetupProgressDto? progress) => progress is null
        ? ApiWithoutSetup()
        : ApiWithoutSetup().On("GET /api/setup/progress", progress);

    private static StubApi ApiWithoutSetup() => new StubApi()
        .On("GET /api/tenant", new TenantDto(Guid.NewGuid(), "Day-Spa Wien", "AT", CompanyProfile.Empty))
        .On("GET /api/tenant/embedding", new EmbeddingSettings([], null))
        .On("GET /api/tenant/history", new List<HistoryEntry>());

    private static async Task<CompanyViewModel> Open(StubApi api)
    {
        var company = new CompanyViewModel(api.Client(), TimeZoneInfo.Utc);
        await company.EnsureLoadedAsync();
        return company;
    }

    [Fact]
    public async Task Given_zwei_von_sechs_Schritten_erledigt_When_die_Firmendaten_oeffnen_Then_zeigt_die_Karte_den_Fortschritt_und_die_offenen_Schritte()
    {
        var company = await Open(Api(Progress("location", "hours")));

        Assert.True(company.ShowSetup);
        Assert.Equal("2 von 6 Schritten erledigt", company.SetupSummary);
        Assert.Equal(100.0 / 3, company.SetupPercent, 0.01);
        Assert.Equal(["location", "hours"], company.SetupSteps.Where(s => s.IsDone).Select(s => s.Step.Key));
        Assert.Equal("Räume anlegen, noch offen", company.SetupSteps[2].AccessibleName);
        Assert.Equal("Standort anlegen, erledigt", company.SetupSteps[0].AccessibleName);
    }

    [Fact]
    public async Task Given_alle_Schritte_erledigt_When_die_Firmendaten_oeffnen_Then_verschwindet_die_Karte()
    {
        var company = await Open(Api(Progress("location", "hours", "rooms", "treatments", "people", "register")));

        Assert.False(company.ShowSetup);
        Assert.Equal("6 von 6 Schritten erledigt", company.SetupSummary);
    }

    [Fact]
    public async Task Given_die_API_liefert_den_Stand_nicht_When_die_Firmendaten_oeffnen_Then_bleibt_die_Karte_weg_und_die_Maske_ohne_Fehler()
    {
        var company = await Open(Api(null));

        Assert.True(company.IsLoaded);
        Assert.False(company.HasLoadError);
        Assert.False(company.ShowSetup);
        Assert.Empty(company.SetupSteps);
    }

    [Fact]
    public async Task Given_ein_offener_Schritt_When_Oeffnen_gewaehlt_wird_Then_nennt_das_Ereignis_Seite_und_Reiter_des_Ziels()
    {
        var company = await Open(Api(Progress("location")));
        SetupStepDto? requested = null;
        company.SetupStepRequested += step => requested = step;

        company.OpenSetupStepCommand.Execute(company.SetupSteps.Single(s => s.Step.Key == "treatments"));

        Assert.Equal(("masterData", "treatments"), (requested!.Page, requested.Tab));
    }

    [Fact]
    public void Given_die_Verwaltung_When_die_Karte_den_Schritt_Kassa_meldet_Then_oeffnet_die_Seite_die_Maske_Kassa()
    {
        var admin = new AdminPageViewModel(ApiWithoutSetup().Client(), TestData.Owner(), TimeProvider.System);

        admin.Company!.OpenSetupStepCommand.Execute(new SetupStepItem(Step("register", "Kassa einrichten", "masterData", "kassa", [])));

        Assert.Equal("Kassa", admin.SelectedArea?.Title);
    }
}
