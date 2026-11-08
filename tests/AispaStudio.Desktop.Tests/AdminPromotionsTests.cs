using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.PromotionSample;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 101 (Ä191): native Maske „Aktionen“ nach web/src/promotions (Liste mit Zustand, Formular, Pausieren, Archivieren). Das ViewModel prüft den gesendeten Inhalt, die Regeln am Feld und
/// die Rechte, nicht die Rabattrechnung der Kassa (vorhandene API-Tests <c>PromotionTests</c> und <c>BusinessPromotionTests</c>).
/// </summary>
public sealed class AdminPromotionsTests
{
    private const string Create = "POST /api/promotions";

    private static readonly DateOnly Today = new(2027, 3, 1);

    private static async Task<PromotionsViewModel> Open(StubApi api, FakeDialogs? dialogs = null)
    {
        var promotions = new PromotionsViewModel(api.Client(), new FixedTime()) { Dialogs = dialogs };
        await promotions.EnsureLoadedAsync();
        return promotions;
    }

    private static PromotionRow Row(PromotionsViewModel promotions, Guid id) => promotions.Rows.Single(r => r.Id == id);

    private static void FillValid(PromotionsViewModel promotions)
    {
        promotions.PromotionName = "Herbst";
        promotions.GuestText = "10 % auf alle Massagen im Oktober";
        promotions.ValidFrom = new DateOnly(2027, 10, 1);
        promotions.ValidUntil = new DateOnly(2027, 10, 31);
        promotions.DiscountValue = "10";
    }

    private static ShellViewModel Shell(CurrentUser user, StubApi? api = null) =>
        new((api ?? new StubApi()).Client(), user, null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

    private static CurrentUser WithRights(params AccessRight[] rights) =>
        new("u9", "recht@aispa.test", "Rolf", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, true, []), null, rights);

    // ── Anlegen ──

    [Fact]
    public async Task Given_eine_Aktion_Herbst_minus_10_Prozent_When_sie_nativ_angelegt_wird_Then_POST_mit_Prozent_Geltung_und_Zeitraum()
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        Assert.True(promotions.IsCreating);
        promotions.PromotionName = "  Herbst ";
        promotions.GuestText = " 10 % auf alle Massagen im Oktober ";
        promotions.ValidFrom = new DateOnly(2027, 10, 1);
        promotions.ValidUntil = new DateOnly(2027, 10, 31);
        promotions.DiscountValue = "10";
        Assert.True(promotions.HasUnsavedChanges);

        Assert.True(await promotions.SaveChangesAsync());

        var sent = Sent(api, Create);
        Assert.Equal(("Herbst", "10 % auf alle Massagen im Oktober", new DateOnly(2027, 10, 1), new DateOnly(2027, 10, 31), true, 10m, (decimal?)null, PromotionScope.AllTreatments, true),
            (sent.Name, sent.GuestText, sent.ValidFrom, sent.ValidUntil, sent.AllLocations, sent.DiscountPercent, sent.DiscountAmount, sent.Scope, sent.IsActive));
        Assert.Empty(sent.LocationIds ?? []);
        Assert.Empty(sent.TreatmentIds ?? []);
        Assert.Equal("Gespeichert.", promotions.StatusText);
        Assert.False(promotions.IsCreating);
        Assert.False(promotions.HasUnsavedChanges);
        Assert.Equal("Herbst", promotions.SelectedRow?.Name);
        Assert.NotEqual(AutumnId, promotions.SelectedRow?.Id);
    }

    [Fact]
    public async Task Given_ein_neues_Formular_When_die_Maske_laedt_Then_gilt_30_Tage_ab_heute_fuer_alle_Standorte_und_Prozent()
    {
        var promotions = await Open(Api(Today, two: true));

        Assert.Equal((Today, Today.AddDays(30), true, true, true, true), (promotions.ValidFrom, promotions.ValidUntil, promotions.IsPercent, promotions.AllLocations, promotions.ScopeAll, promotions.IsActive));
        Assert.Equal(["Wien", "Salzburg"], promotions.Locations.Select(l => l.Name));
        Assert.Equal("Rabatt (%)", promotions.ValueLabel);
    }

    [Fact]
    public async Task Given_nur_ein_Standort_When_das_Formular_neu_ist_Then_ist_dieser_gewaehlt_und_nicht_alle_Standorte()
    {
        var api = Api(Today);
        var promotions = await Open(api);
        FillValid(promotions);

        Assert.False(promotions.AllLocations);
        Assert.True(promotions.SomeLocations);
        Assert.True(await promotions.SaveChangesAsync());

        var sent = Sent(api, Create);
        Assert.False(sent.AllLocations);
        Assert.Equal([Vienna.Id], sent.LocationIds);
    }

    [Fact]
    public async Task Given_ein_Betrag_statt_Prozent_When_gespeichert_Then_DiscountAmount_gesetzt_und_Prozent_null()
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        promotions.IsAmount = true;
        promotions.DiscountValue = "5,50";

        Assert.False(promotions.IsPercent);
        Assert.Equal("Rabatt (€)", promotions.ValueLabel);
        Assert.True(await promotions.SaveChangesAsync());

        var sent = Sent(api, Create);
        Assert.Equal((5.5m, (decimal?)null), (sent.DiscountAmount, sent.DiscountPercent));
    }

    // ── Regeln am Feld ──

    [Theory]
    [InlineData(true, "", "Rabatt in Prozent zwischen 0,01 und 99,99.")]
    [InlineData(true, "0", "Rabatt in Prozent zwischen 0,01 und 99,99.")]
    [InlineData(true, "100", "Rabatt in Prozent zwischen 0,01 und 99,99.")]
    [InlineData(true, "abc", "Rabatt in Prozent zwischen 0,01 und 99,99.")]
    [InlineData(true, "12,345", "Rabatt in Prozent zwischen 0,01 und 99,99.")]
    [InlineData(false, "", "Rabatt als Betrag größer als 0 mit höchstens zwei Nachkommastellen.")]
    [InlineData(false, "0", "Rabatt als Betrag größer als 0 mit höchstens zwei Nachkommastellen.")]
    [InlineData(false, "100000", "Rabatt als Betrag größer als 0 mit höchstens zwei Nachkommastellen.")]
    [InlineData(false, "1,234", "Rabatt als Betrag größer als 0 mit höchstens zwei Nachkommastellen.")]
    public async Task Given_kein_gueltiger_Rabatt_When_gespeichert_Then_Fehler_am_Feld_Fokus_und_kein_POST(bool percent, string value, string message)
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        promotions.IsAmount = !percent;
        promotions.DiscountValue = value;

        Assert.False(await promotions.SaveChangesAsync());

        Assert.Equal(message, promotions.ValueError);
        Assert.Equal(PromotionsViewModel.ValueField, promotions.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == Create);
    }

    [Fact]
    public async Task Given_ein_Rabattfehler_When_die_Art_gewechselt_wird_Then_verschwindet_der_Fehler()
    {
        var promotions = await Open(Api(Today, two: true));
        FillValid(promotions);
        promotions.DiscountValue = "";
        await promotions.SaveChangesAsync();
        Assert.NotNull(promotions.ValueError);

        promotions.IsAmount = true;

        Assert.Null(promotions.ValueError);
    }

    [Fact]
    public async Task Given_Von_nach_Bis_When_gespeichert_Then_Fehler_am_Ende_und_kein_POST_gleicher_Tag_ist_erlaubt()
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        promotions.ValidFrom = new DateOnly(2027, 10, 31);
        promotions.ValidUntil = new DateOnly(2027, 10, 1);

        Assert.False(await promotions.SaveChangesAsync());

        Assert.Equal("Das Ende darf nicht vor dem Beginn liegen.", promotions.UntilError);
        Assert.Equal(PromotionsViewModel.UntilField, promotions.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == Create);

        promotions.ValidUntil = new DateOnly(2027, 10, 31);
        Assert.Null(promotions.UntilError);
        Assert.True(await promotions.SaveChangesAsync());
    }

    [Fact]
    public async Task Given_Beginn_oder_Ende_fehlt_When_gespeichert_Then_Fehler_am_Datum_und_kein_POST()
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        promotions.ValidFrom = null;
        promotions.ValidUntil = null;

        Assert.False(await promotions.SaveChangesAsync());

        Assert.Equal("Trage den Beginn ein.", promotions.FromError);
        Assert.Equal("Trage das Ende ein.", promotions.UntilError);
        Assert.Equal(PromotionsViewModel.FromField, promotions.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == Create);
    }

    [Theory]
    [InlineData("", "Text", "Name mit höchstens 40 Zeichen angeben.", null)]
    [InlineData("   ", "Text", "Name mit höchstens 40 Zeichen angeben.", null)]
    [InlineData("0123456789012345678901234567890123456789X", "Text", "Name mit höchstens 40 Zeichen angeben.", null)]
    [InlineData("Herbst", "", null, "Gästetext mit höchstens 200 Zeichen angeben.")]
    [InlineData("", "", "Name mit höchstens 40 Zeichen angeben.", "Gästetext mit höchstens 200 Zeichen angeben.")]
    public async Task Given_Name_oder_Gaestetext_fehlt_oder_ist_zu_lang_When_gespeichert_Then_Fehler_am_Feld_und_kein_POST(string name, string text, string? nameError, string? textError)
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        promotions.PromotionName = name;
        promotions.GuestText = text;

        Assert.False(await promotions.SaveChangesAsync());

        Assert.Equal((nameError, textError), (promotions.NameError, promotions.GuestTextError));
        Assert.Equal(nameError is not null ? PromotionsViewModel.NameField : PromotionsViewModel.GuestTextField, promotions.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == Create);
    }

    [Fact]
    public async Task Given_ein_Gaestetext_mit_201_Zeichen_When_gespeichert_Then_Fehler()
    {
        var promotions = await Open(Api(Today, two: true));
        FillValid(promotions);
        promotions.GuestText = new string('x', 201);

        Assert.False(await promotions.SaveChangesAsync());

        Assert.NotNull(promotions.GuestTextError);
    }

    // ── Geltung und Standorte ──

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ausgewaehlte_Behandlungen_oder_Pakete_ohne_Behandlung_When_gespeichert_Then_Fehler_und_mit_Behandlung_gesendet(bool packages)
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        if (packages)
        {
            promotions.ScopePackages = true;
        }
        else
        {
            promotions.ScopeTreatments = true;
        }

        Assert.False(await promotions.SaveChangesAsync());
        Assert.Equal("Mindestens eine Behandlung wählen.", promotions.TreatmentsError);
        Assert.DoesNotContain(api.Calls, c => c.Route == Create);

        promotions.PickedTreatment = promotions.CatalogTreatments.Single(t => t.Id == Massage.Id);
        Assert.Null(promotions.TreatmentsError);
        Assert.True(await promotions.SaveChangesAsync());

        var sent = Sent(api, Create);
        Assert.Equal(packages ? PromotionScope.SelectedPackages : PromotionScope.SelectedTreatments, sent.Scope);
        Assert.Equal([Massage.Id], sent.TreatmentIds);
    }

    [Fact]
    public async Task Given_eine_Behandlung_When_sie_gewaehlt_zweimal_gewaehlt_und_entfernt_wird_Then_steht_sie_einmal_in_der_Liste_der_gewaehlten()
    {
        var promotions = await Open(Api(Today, two: true));
        promotions.ScopeTreatments = true;
        Assert.True(promotions.ShowTreatmentPicker);

        promotions.PickedTreatment = promotions.CatalogTreatments.Single(t => t.Id == Massage.Id);

        Assert.Equal([Massage.Id], promotions.Chosen.Select(c => c.Id));
        Assert.Null(promotions.PickedTreatment);

        promotions.PickedTreatment = promotions.CatalogTreatments.Single(t => t.Id == Massage.Id);
        Assert.Single(promotions.Chosen);
        Assert.Null(promotions.PickedTreatment);
        Assert.Equal("Klassische Massage entfernen", promotions.Chosen[0].RemoveName);

        promotions.Chosen[0].RemoveCommand.Execute(null);

        Assert.Empty(promotions.Chosen);
    }

    [Fact]
    public async Task Given_Behandlungen_gewaehlt_When_die_Geltung_auf_alle_zurueckgestellt_wird_Then_sendet_die_Maske_keine_Behandlungen()
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        promotions.ScopeTreatments = true;
        promotions.PickedTreatment = promotions.CatalogTreatments.First();
        promotions.ScopeAll = true;

        Assert.False(promotions.ShowTreatmentPicker);
        Assert.True(await promotions.SaveChangesAsync());

        var sent = Sent(api, Create);
        Assert.Equal(PromotionScope.AllTreatments, sent.Scope);
        Assert.Empty(sent.TreatmentIds ?? []);
    }

    [Fact]
    public async Task Given_ausgewaehlte_Standorte_ohne_Haken_When_gespeichert_Then_Fehler_und_mit_Haken_gesendet()
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        promotions.SomeLocations = true;

        Assert.False(promotions.AllLocations);
        Assert.False(await promotions.SaveChangesAsync());
        Assert.Equal("Mindestens einen Standort wählen.", promotions.LocationsError);
        Assert.Equal("SomeLocationsOption", promotions.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route == Create);

        promotions.Locations.Single(l => l.Id == Salzburg.Id).IsSelected = true;
        Assert.Null(promotions.LocationsError);
        Assert.True(await promotions.SaveChangesAsync());

        var sent = Sent(api, Create);
        Assert.False(sent.AllLocations);
        Assert.Equal([Salzburg.Id], sent.LocationIds);
    }

    // ── Liste, Phasen, Bearbeiten ──

    [Fact]
    public async Task Given_Aktionen_in_allen_Zustaenden_When_die_Maske_laedt_Then_zeigt_die_Liste_Zustand_Rabatt_Zeitraum_und_Standorte_ohne_Archiv()
    {
        var promotions = await Open(Api(Today));

        Assert.Equal(["Herbst", "Frühling", "Sommerpause", "Ostern"], promotions.Rows.Select(r => r.Name));
        Assert.Equal(["Läuft", "Geplant", "Pausiert", "Abgelaufen"], promotions.Rows.Select(r => r.PhaseText));
        Assert.Equal("10 % · 19.2.2027 bis 21.3.2027", Row(promotions, AutumnId).Detail);
        Assert.Equal("5,00 € · 31.3.2027 bis 30.4.2027", Row(promotions, SpringId).Detail);
        Assert.Equal(["alle Standorte", "Wien"], promotions.Rows.Take(2).Select(r => r.PlacesText));
        Assert.True(promotions.IsCreating);
    }

    [Fact]
    public async Task Given_archivierte_Aktionen_When_Archivierte_anzeigen_gesetzt_wird_Then_erscheinen_sie_mit_Zustand_Archiviert_und_verschwinden_wieder()
    {
        var api = Api(Today);
        var promotions = await Open(api);
        Assert.DoesNotContain(promotions.Rows, r => r.Id == WinterId);

        promotions.ShowArchived = true;
        Assert.Equal("Archiviert", Row(promotions, WinterId).PhaseText);

        promotions.ShowArchived = false;
        Assert.DoesNotContain(promotions.Rows, r => r.Id == WinterId);
        Assert.Equal(1, api.Calls.Count(c => c.Route == "GET /api/promotions?includeArchived=true"));
    }

    [Fact]
    public async Task Given_eine_Aktion_gewaehlt_When_die_Maske_sie_zeigt_Then_stehen_alle_Felder_im_Formular()
    {
        var promotions = await Open(Api(Today, two: true));

        promotions.SelectedRow = Row(promotions, SpringId);

        Assert.Equal(("Frühling", "5 € auf die Klassische Massage", true, "5", Today.AddDays(30), Today.AddDays(60), false, true, false),
            (promotions.PromotionName, promotions.GuestText, promotions.IsAmount, promotions.DiscountValue, promotions.ValidFrom, promotions.ValidUntil, promotions.AllLocations, promotions.ScopeTreatments,
                promotions.HasUnsavedChanges));
        Assert.Equal(["Wien"], promotions.Locations.Where(l => l.IsSelected).Select(l => l.Name));
        Assert.Equal([Massage.Name], promotions.Chosen.Select(c => c.Name));
        Assert.Equal("Frühling", promotions.HeadingText);
        Assert.Equal("Geplant", promotions.PhaseText);
    }

    [Fact]
    public async Task Given_eine_gewaehlte_Aktion_When_Name_und_Rabatt_geaendert_und_gespeichert_werden_Then_PUT_mit_dem_neuen_Stand()
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        promotions.SelectedRow = Row(promotions, AutumnId);
        Assert.Equal("10", promotions.DiscountValue);

        promotions.PromotionName = "Herbst 2";
        promotions.DiscountValue = "12,5";
        Assert.True(promotions.HasUnsavedChanges);
        Assert.True(await promotions.SaveChangesAsync());

        var sent = Sent(api, $"PUT /api/promotions/{AutumnId}");
        Assert.Equal(("Herbst 2", 12.5m, (decimal?)null, true), (sent.Name, sent.DiscountPercent, sent.DiscountAmount, sent.AllLocations));
        Assert.DoesNotContain(api.Calls, c => c.Route == Create);
        Assert.False(promotions.HasUnsavedChanges);
        Assert.Equal("Herbst 2", promotions.SelectedRow?.Name);
    }

    [Fact]
    public async Task Given_geaenderte_Felder_When_verworfen_Then_steht_der_gespeicherte_Stand_wieder_da()
    {
        var promotions = await Open(Api(Today, two: true));
        promotions.SelectedRow = Row(promotions, AutumnId);
        promotions.PromotionName = "Geändert";
        promotions.ScopeTreatments = true;

        promotions.Discard();

        Assert.Equal(("Herbst", true, false), (promotions.PromotionName, promotions.ScopeAll, promotions.HasUnsavedChanges));
    }

    // ── Pausieren, Archivieren, Zurückholen ──

    [Fact]
    public async Task Given_eine_laufende_Aktion_When_Archivieren_Then_POST_archive_ohne_Rueckfrage_und_sie_verschwindet_aus_der_Liste()
    {
        var api = Api(Today);
        var dialogs = new FakeDialogs();
        var promotions = await Open(api, dialogs);
        promotions.SelectedRow = Row(promotions, AutumnId);
        Assert.True(promotions.CanArchive);

        await promotions.ArchiveCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"POST /api/promotions/{AutumnId}/archive"));
        Assert.Empty(dialogs.Asked);
        Assert.Equal("Archiviert.", promotions.Message);
        Assert.DoesNotContain(promotions.Rows, r => r.Id == AutumnId);
    }

    [Fact]
    public async Task Given_eine_archivierte_Aktion_When_sie_gewaehlt_und_zurueckgeholt_wird_Then_POST_unarchive_und_sie_ist_wieder_in_der_Liste()
    {
        var api = Api(Today);
        var promotions = await Open(api);
        promotions.ShowArchived = true;
        promotions.SelectedRow = Row(promotions, WinterId);

        Assert.False(promotions.IsEditable);
        Assert.False(promotions.CanArchive);
        Assert.True(promotions.CanUnarchive);
        Assert.False(promotions.CanPause);
        Assert.Equal("Archiviert", promotions.PhaseText);

        await promotions.UnarchiveCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"POST /api/promotions/{WinterId}/unarchive"));
        Assert.Equal("Zurückgeholt.", promotions.Message);
        promotions.ShowArchived = false;
        Assert.Equal("Abgelaufen", Row(promotions, WinterId).PhaseText);
        Assert.True(promotions.IsEditable);
    }

    [Fact]
    public async Task Given_eine_laufende_Aktion_When_Pausieren_und_Fortsetzen_Then_PUT_mit_geaendertem_Schalter_und_Zustand_wechselt()
    {
        var api = Api(Today);
        var promotions = await Open(api);
        promotions.SelectedRow = Row(promotions, AutumnId);
        Assert.Equal("Pausieren", promotions.PauseText);

        await promotions.TogglePauseCommand.ExecuteAsync(null);

        var sent = Sent(api, $"PUT /api/promotions/{AutumnId}");
        Assert.Equal(("Herbst", 10m, false), (sent.Name, sent.DiscountPercent, sent.IsActive));
        Assert.Equal("Pausiert.", promotions.Message);
        Assert.Equal("Pausiert", promotions.PhaseText);
        Assert.Equal("Fortsetzen", promotions.PauseText);
        Assert.False(promotions.IsActive);
        Assert.False(promotions.HasUnsavedChanges);
    }

    // ── Rückfragen, Rechte, Sprungziel ──

    [Fact]
    public async Task Given_ungespeicherte_Aenderung_When_eine_andere_Aktion_gewaehlt_wird_Then_fragt_die_Maske_und_Abbrechen_bleibt_bei_der_Aktion()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var promotions = await Open(Api(Today), dialogs);
        promotions.SelectedRow = Row(promotions, AutumnId);
        promotions.PromotionName = "Geändert";

        promotions.SelectedRow = Row(promotions, SpringId);
        await Task.Yield();
        Assert.Equal("Geändert", promotions.PromotionName);

        promotions.SelectedRow = Row(promotions, SpringId);
        await Task.Yield();
        await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.Equal("Frühling", promotions.PromotionName);
        Assert.Equal(["Änderungen speichern?", "Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
        Assert.Contains("Aktionen", dialogs.Asked[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ungespeicherte_Aenderung_When_Neue_Aktion_gewaehlt_wird_Then_fragt_die_Maske_und_Speichern_legt_zuerst_die_Aenderung_ab()
    {
        var api = Api(Today);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var promotions = await Open(api, dialogs);
        promotions.SelectedRow = Row(promotions, AutumnId);
        promotions.PromotionName = "Herbst 3";

        await promotions.NewCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"PUT /api/promotions/{AutumnId}"));
        Assert.True(promotions.IsCreating);
        Assert.Equal("", promotions.PromotionName);
        Assert.Equal("Neue Aktion", promotions.HeadingText);
    }

    [Fact]
    public void Given_Rezeption_Standortleitung_ohne_Katalogrecht_und_Inhaberin_When_sie_die_Verwaltung_oeffnen_Then_fehlt_der_Bereich_Aktionen_nur_ohne_ManageCatalog()
    {
        var reception = Shell(TestData.Reception());
        var manager = Shell(TestData.Manager());
        var catalog = Shell(WithRights(AccessRight.ManageCatalog));
        var owner = Shell(TestData.Owner());

        Assert.DoesNotContain(reception.AdminPage.Areas, a => a.Title == "Aktionen");
        Assert.DoesNotContain(manager.AdminPage.Areas, a => a.Title == "Aktionen");
        Assert.Null(reception.AdminPage.Promotions);
        Assert.Null(manager.AdminPage.Promotions);
        Assert.NotNull(catalog.AdminPage.Promotions);
        Assert.NotNull(owner.AdminPage.Promotions);
        owner.AdminPage.SelectedArea = owner.AdminPage.Areas.Single(a => a.Title == "Aktionen");
        Assert.Same(owner.AdminPage.Promotions, owner.AdminPage.Content);
    }

    [Fact]
    public async Task Given_ein_Sprung_vom_Assistenten_When_Show_promotions_Then_oeffnet_die_Verwaltung_die_native_Maske_und_waehlt_die_Aktion()
    {
        var api = Api(Today);
        var shell = Shell(TestData.Owner(), api);

        shell.AdminPage.Show("promotions", null, SpringId);
        await shell.AdminPage.InitializeAsync();
        await shell.AdminPage.Promotions!.EnsureLoadedAsync();

        Assert.Equal("Aktionen", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Promotions, shell.AdminPage.Content);
        Assert.Equal(SpringId, shell.AdminPage.Promotions.SelectedRow?.Id);
        Assert.Equal("Frühling", shell.AdminPage.Promotions.PromotionName);
    }

    [Fact]
    public async Task Given_die_Maske_steht_und_das_Ziel_ist_archiviert_When_Open_Then_werden_Archivierte_angezeigt_und_die_Aktion_gewaehlt()
    {
        var promotions = await Open(Api(Today));

        promotions.Open(WinterId, null, null);

        Assert.True(promotions.ShowArchived);
        Assert.Equal(WinterId, promotions.SelectedRow?.Id);
        Assert.False(promotions.IsEditable);
    }

    [Fact]
    public async Task Given_die_Liste_ist_nicht_ladbar_When_die_Maske_oeffnet_Then_steht_ein_Fehler_und_Erneut_laden_geht()
    {
        var api = new StubApi().On("GET /api/locations", new List<LocationDto> { Vienna }).On("GET /api/treatments", SpaSample.Treatments.ToList());
        var promotions = new PromotionsViewModel(api.Client(), new FixedTime());

        await promotions.EnsureLoadedAsync();

        Assert.False(promotions.IsLoaded);
        Assert.NotNull(promotions.LoadError);

        Api(Today, api);
        await promotions.LoadAsync();

        Assert.True(promotions.IsLoaded);
        Assert.Null(promotions.LoadError);
        Assert.Equal(4, promotions.Rows.Count);
    }

    [Fact]
    public async Task Given_Aktion_gespeichert_aber_das_Neuladen_scheitert_When_erneut_gespeichert_wird_Then_PUT_und_kein_zweiter_POST()
    {
        var api = Api(Today, two: true);
        var promotions = await Open(api);
        FillValid(promotions);
        api.On("GET /api/promotions?includeArchived=true", new { }, System.Net.HttpStatusCode.InternalServerError);

        Assert.True(await promotions.SaveChangesAsync());
        Assert.False(promotions.IsCreating);
        promotions.GuestText = "Neuer Text";
        await promotions.SaveChangesAsync();

        Assert.Equal(1, api.Calls.Count(c => c.Route == Create));
        Assert.Contains(api.Calls, c => c.Route.StartsWith("PUT /api/promotions/", StringComparison.Ordinal));
    }
}
