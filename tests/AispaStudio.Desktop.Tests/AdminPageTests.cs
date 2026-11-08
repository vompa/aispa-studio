using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 93 (Ä191, ADR 0084): native Seite „Verwaltung“ mit Bereichen nach dem Masken-Inventar; seit Slice 103 hat jeder Bereich eine native Maske.</summary>
public sealed class AdminPageTests
{
    /// <summary>Wählt den Bereich und gibt zurück, ob darunter eine native Maske steht.</summary>
    private static bool OpensNative(ShellViewModel shell, string title)
    {
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == title);
        return shell.AdminPage.Content is INativeArea;
    }

    private static ShellViewModel Shell(CurrentUser user, StubApi? stub = null) =>
        new((stub ?? new StubApi()).Client(), user, null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

    [Fact]
    public void Given_Rezeptionistin_ohne_ManageOrganization_When_sie_die_Verwaltung_oeffnet_Then_fehlt_der_Bereich_Firmendaten()
    {
        var shell = Shell(TestData.Reception());

        // Slice 99: der Dienstplan ist der Wochenplan im Bereich Team, auch für die Rezeption.
        Assert.Equal(["Team", "Lager", "Zahlen"], shell.AdminPage.Areas.Select(a => a.Title));
        Assert.Null(shell.AdminPage.Company);
    }

    [Fact]
    public void Given_Inhaberin_When_sie_die_Verwaltung_oeffnet_Then_alle_Bereiche_nach_dem_Inventar_und_Firmendaten_zuerst()
    {
        var shell = Shell(TestData.Owner());

        Assert.Equal(
            ["Firmendaten", "Standorte & Räume", "Schließzeiten", "Raumsperren", "Kalender-Abos", "Handbuch", "Behandlungen", "Team",
                "Erinnerungen", "Postfach & Nummern", "Zahlungen", "Kassa", "KI", "Lager", "Aktionen", "Zahlen",
                "Kosten", "Buchhaltung"],
            shell.AdminPage.Areas.Select(a => a.Title));
        Assert.Equal("Firmendaten", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Company, shell.AdminPage.Content);
        Assert.Same(shell.AdminPage, Assert.Single(shell.Pages, p => p.Title == "Verwaltung").Page);
    }

    // R05 Slice 125 (Ä293): Gruppen und Reihenfolge sind die Festlegung des Auftraggebers vom 31.10.2026, hier wörtlich aus der Slice-Datei.
    [Fact]
    public void Given_Inhaberin_When_sie_die_Verwaltung_oeffnet_Then_sieben_Gruppen_mit_ihren_Masken()
    {
        var page = Shell(TestData.Owner()).AdminPage;

        Assert.Equal(
            [
                "Betrieb: Firmendaten, Standorte & Räume, Schließzeiten, Raumsperren, Kalender-Abos, Handbuch",
                "Angebot: Behandlungen, Aktionen",
                "Team: Team",
                "Lager: Lager",
                "Kassa & Geld: Kassa, Zahlungen, Kosten, Buchhaltung",
                "Kontakt: Erinnerungen, Postfach & Nummern, KI",
                "Zahlen: Zahlen",
            ],
            page.Groups.Select(g => $"{g.Title}: {string.Join(", ", g.Areas.Select(a => a.Title))}"));
        Assert.Equal("Betrieb", page.SelectedGroup?.Title);
    }

    [Fact]
    public void Given_Betrieb_mit_Schliesszeiten_When_zu_Lager_und_zurueck_Then_wieder_Schliesszeiten()
    {
        var page = Shell(TestData.Owner()).AdminPage;
        page.SelectAreaCommand.Execute(page.Areas.Single(a => a.Title == "Schließzeiten"));

        page.SelectGroupCommand.Execute(Group(page, "Lager"));
        Assert.Equal("Lager", page.SelectedArea?.Title);
        Assert.Same(page.Stock, page.Content);

        page.SelectGroupCommand.Execute(Group(page, "Betrieb"));
        Assert.Equal("Schließzeiten", page.SelectedArea?.Title);
        Assert.Same(page.Closures, page.Content);
    }

    [Fact]
    public void Given_Rezeption_ohne_Recht_auf_ganze_Gruppen_When_sie_oeffnet_Then_fehlen_diese_Gruppen()
    {
        var page = Shell(TestData.Reception()).AdminPage;

        Assert.Equal(["Team", "Lager", "Zahlen"], page.Groups.Select(g => g.Title));
        Assert.Equal("Team", page.SelectedGroup?.Title);
    }

    [Theory]
    [InlineData("masterData", "closures", "Betrieb", "Schließzeiten")]
    [InlineData("masterData", "treatments", "Angebot", "Behandlungen")]
    [InlineData("roster", null, "Team", "Team")]
    [InlineData("masterData", "qualifications", "Team", "Team")]
    [InlineData("stock", null, "Lager", "Lager")]
    [InlineData("costs", null, "Kassa & Geld", "Kosten")]
    [InlineData("masterData", "ai", "Kontakt", "KI")]
    [InlineData("businessInfo", null, "Zahlen", "Zahlen")]
    public void Given_Sprungziel_When_es_geoeffnet_wird_Then_richtige_Gruppe_und_Maske(string targetPage, string? tab, string group, string area)
    {
        var page = Shell(TestData.Owner()).AdminPage;

        page.Show(targetPage, tab);

        Assert.Equal(group, page.SelectedGroup?.Title);
        Assert.Equal(area, page.SelectedArea?.Title);
    }

    [Fact]
    public async Task Given_ungespeicherte_Aenderungen_When_Gruppenwechsel_abgebrochen_Then_bleiben_Gruppe_und_gemerkte_Maske()
    {
        var tenant = new TenantDto(Guid.NewGuid(), "Day-Spa Wien", "AT", CompanyProfile.Empty);
        var shell = Shell(TestData.Owner(), new StubApi().On("GET /api/tenant", tenant).On("GET /api/tenant/embedding", new EmbeddingSettings([])));
        shell.AdminPage.Dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var page = shell.AdminPage;
        await page.InitializeAsync();
        page.Company!.City = "Salzburg";

        page.SelectGroupCommand.Execute(Group(page, "Zahlen"));
        await Task.Yield();

        Assert.Equal("Betrieb", page.SelectedGroup?.Title);
        Assert.Equal("Firmendaten", page.SelectedArea?.Title);
        Assert.Equal("Firmendaten", Group(page, "Betrieb").LastArea.Title);
        Assert.Equal("Zahlen", Group(page, "Zahlen").LastArea.Title);
    }

    private static AdminGroup Group(AdminPageViewModel page, string title) => page.Groups.Single(g => g.Title == title);

    [Fact]
    public void Given_Inhaberin_When_sie_die_Verwaltung_oeffnet_Then_haben_Zahlen_Kosten_und_Buchhaltung_eine_native_Maske()
    {
        var shell = Shell(TestData.Owner());

        Assert.All(["Zahlen", "Kosten", "Buchhaltung"], title => Assert.True(OpensNative(shell, title)));
    }

    [Fact]
    public void Given_Inhaberin_When_sie_die_Verwaltung_oeffnet_Then_haben_Handbuch_Postfach_und_KI_eine_native_Maske()
    {
        var shell = Shell(TestData.Owner());

        Assert.All(["Handbuch", "Postfach & Nummern", "KI"], title => Assert.True(OpensNative(shell, title)));
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Postfach & Nummern");
        Assert.Same(shell.AdminPage.Intake, shell.AdminPage.Content);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "KI");
        Assert.Same(shell.AdminPage.Ai, shell.AdminPage.Content);
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Handbuch");
        Assert.Same(shell.AdminPage.Handbook, shell.AdminPage.Content);
    }

    [Fact]
    public void Given_Rezeptionistin_und_Inhaberin_When_sie_die_Verwaltung_oeffnen_Then_hat_Lager_eine_native_Maske()
    {
        var owner = Shell(TestData.Owner());
        var reception = Shell(TestData.Reception());

        Assert.True(OpensNative(owner, "Lager"));
        owner.AdminPage.SelectedArea = owner.AdminPage.Areas.Single(a => a.Title == "Lager");
        Assert.Same(owner.AdminPage.Stock, owner.AdminPage.Content);
        Assert.NotNull(reception.AdminPage.Stock);
        Assert.True(reception.AdminPage.Stock.CanBook);
        Assert.False(reception.AdminPage.Stock.CanManage);
    }

    [Fact]
    public void Given_Inhaberin_When_sie_die_Verwaltung_oeffnet_Then_hat_Aktionen_eine_native_Maske()
    {
        var owner = Shell(TestData.Owner());

        Assert.True(OpensNative(owner, "Aktionen"));
        owner.AdminPage.SelectedArea = owner.AdminPage.Areas.Single(a => a.Title == "Aktionen");
        Assert.Same(owner.AdminPage.Promotions, owner.AdminPage.Content);
    }

    [Fact]
    public void Given_Inhaberin_When_sie_die_Verwaltung_oeffnet_Then_gibt_es_keinen_Bereich_Lohn_weil_der_Lohn_im_Team_liegt()
    {
        var shell = Shell(TestData.Owner());

        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title == "Lohn");
        Assert.All(shell.AdminPage.Areas.Select(a => a.Title).ToList(), title => Assert.True(OpensNative(shell, title), title));
    }

    [Fact]
    public async Task Given_ungespeicherte_Firmendaten_When_ein_anderer_Bereich_gewaehlt_wird_Then_fragt_die_Verwaltung_vorher()
    {
        var tenant = new TenantDto(Guid.NewGuid(), "Day-Spa Wien", "AT", CompanyProfile.Empty);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var shell = Shell(TestData.Owner(), new StubApi().On("GET /api/tenant", tenant).On("GET /api/tenant/embedding", new EmbeddingSettings([])));
        shell.AdminPage.Dialogs = dialogs;
        await shell.AdminPage.InitializeAsync();
        var company = shell.AdminPage.Company!;
        company.City = "Salzburg";

        // Abbrechen: bleibt bei den Firmendaten, die Eingabe bleibt.
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Kosten");
        await Task.Yield();
        Assert.Equal("Firmendaten", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(company, shell.AdminPage.Content);
        Assert.Equal("Salzburg", company.City);

        // Verwerfen: wechselt, die Firmendaten stehen wieder auf dem gespeicherten Stand.
        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Kosten");
        await Task.Yield();
        Assert.Equal("Kosten", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Costs, shell.AdminPage.Content);
        Assert.Equal("", company.City);
        Assert.Equal(["Änderungen speichern?", "Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
    }

    [Fact]
    public void Given_der_Kalender_springt_zu_einer_Behandlung_When_die_Verwaltung_oeffnet_Then_im_nativen_Bereich_Behandlungen()
    {
        var shell = Shell(TestData.Owner());
        var treatment = Guid.NewGuid();

        shell.ShowAdmin("masterData", "treatments", treatment);

        Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
        Assert.Equal("Behandlungen", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Treatments, shell.AdminPage.Content);
    }

    [Fact]
    public async Task Given_der_Kalender_When_Raum_bearbeiten_Then_waehlt_die_Verwaltung_Standorte_und_den_Standort_des_Raums()
    {
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/online-rules", new LocationOnlineRules(true, 120, 60, 24, 15, null, true));
        var shell = Shell(TestData.Owner(), stub);

        shell.ShowAdmin("masterData", "locations", TestData.Room.Id);
        await shell.AdminPage.InitializeAsync();

        Assert.Same(shell.AdminPage, shell.SelectedPage?.Page);
        Assert.Equal("Standorte & Räume", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Locations, shell.AdminPage.Content);
        Assert.Equal("Wien", shell.AdminPage.Locations!.SelectedChoice?.Title);
        Assert.Equal($"room:{TestData.Room.Id}", shell.AdminPage.Locations.FocusTarget);
    }

    [Fact]
    public async Task Given_der_Assistent_nennt_Standort_und_Feld_When_Jetzt_ergaenzen_Then_waehlt_die_Verwaltung_Standorte_mit_dem_Feld()
    {
        var other = new LocationDto(Guid.NewGuid(), "Salzburg", "Europe/Vienna", "EUR", []);
        var stub = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location, other })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources);
        var shell = Shell(TestData.Owner(), stub);

        shell.ShowAssistantLink(new AssistantLink(AssistantLinkKind.Setting, "Anreise eintragen", "masterData", "locations", other.Id, Field: "location.directions"));
        await shell.AdminPage.InitializeAsync();

        Assert.Equal("Standorte & Räume", shell.AdminPage.SelectedArea?.Title);
        Assert.Equal("Salzburg", shell.AdminPage.Locations!.SelectedChoice?.Title);
        Assert.Equal("location.directions", shell.AdminPage.Locations.FocusTarget);
    }

    [Fact]
    public void Given_Standortleitung_ohne_ManageOrganization_When_sie_die_Verwaltung_oeffnet_Then_hat_sie_Standorte_aber_keine_Firmendaten()
    {
        var shell = Shell(TestData.Manager());

        Assert.Contains(shell.AdminPage.Areas, a => a.Title == "Standorte & Räume");
        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title == "Firmendaten");
        Assert.NotNull(shell.AdminPage.Locations);
        Assert.Null(shell.AdminPage.Company);
        Assert.False(shell.AdminPage.Locations.CanCreate);
    }

    /// <summary>R05 Slice 96: Kassa, Zahlungen und Erinnerungen sind native Masken, auch beim Sprung aus Assistent oder Kalender.</summary>
    [Theory]
    [InlineData("Kassa", "kassa")]
    [InlineData("Zahlungen", "payments")]
    [InlineData("Erinnerungen", "reminders")]
    public void Given_Inhaberin_When_ein_Einstellungsbereich_gewaehlt_wird_Then_oeffnet_die_native_Maske(string title, string tab)
    {
        var shell = Shell(TestData.Owner());
        INativeArea? expected = tab switch
        {
            "kassa" => shell.AdminPage.PosSettings,
            "payments" => shell.AdminPage.Payments,
            _ => shell.AdminPage.Reminders,
        };

        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == title);
        Assert.NotNull(expected);
        Assert.Same(expected, shell.AdminPage.Content);

        shell.AdminPage.SelectedArea = shell.AdminPage.Areas.Single(a => a.Title == "Kosten");
        shell.ShowAdmin("masterData", tab, null);
        Assert.Equal(title, shell.AdminPage.SelectedArea?.Title);
        Assert.Same(expected, shell.AdminPage.Content);
    }

    /// <summary>R05 Slice 98: Qualifikationen und Zugänge stehen auf der Personenkarte im Bereich Team; Sprünge dorthin öffnen Team beim passenden Reiter.</summary>
    [Theory]
    [InlineData("staff", null, "zugang")]
    [InlineData("masterData", "qualifications", "qualifikationen")]
    public async Task Given_ein_Sprung_auf_Zugaenge_oder_Qualifikationen_When_die_Verwaltung_oeffnet_Then_Team_beim_passenden_Reiter(string page, string? tab, string section)
    {
        var lena = new EmployeeDto(Guid.NewGuid(), "Lena Huber", "#2f6f5e", [], []);
        var stub = TeamSample.AddTeam(new StubApi()
                .On("GET /api/qualifications", new List<QualificationDto>())
                .On("GET /api/locations", new List<LocationDto> { TestData.Location }),
            [lena], [], [TeamSample.Access("u-lena", lena, "", StaffRole.Therapist, [TestData.LocationId])], []);
        var shell = Shell(TestData.Owner(), stub);

        shell.ShowAdmin(page, tab, null);
        await shell.AdminPage.InitializeAsync();
        for (var i = 0; i < 200 && shell.AdminPage.Team?.Card is null; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal("Team", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Team, shell.AdminPage.Content);
        Assert.Equal("Lena Huber", shell.AdminPage.Team!.Card?.Title);
        Assert.Equal(section, shell.AdminPage.Team.Card?.SelectedTab);
        Assert.DoesNotContain(shell.AdminPage.Areas, a => a.Title is "Qualifikationen" or "Zugänge");
    }

    [Fact]
    public void Given_nur_das_Recht_auf_Zugaenge_When_die_Verwaltung_entsteht_Then_gibt_es_den_nativen_Bereich_Team()
    {
        var shell = Shell(TestData.Owner() with { Rights = [AccessRight.ViewCalendar, AccessRight.ManageStaff] });

        Assert.True(OpensNative(shell, "Team"));
        Assert.NotNull(shell.AdminPage.Team);
    }

    [Fact]
    public void Given_Rezeptionistin_When_die_Verwaltung_entsteht_Then_gibt_es_keine_Einstellungsmasken()
    {
        var shell = Shell(TestData.Reception());

        Assert.Null(shell.AdminPage.PosSettings);
        Assert.Null(shell.AdminPage.Payments);
        Assert.Null(shell.AdminPage.Reminders);
    }
}
