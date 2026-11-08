using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 95 (Ä191): native Maske „Kalender-Abos“ nach SubscriptionsPage.tsx: Liste mit Stand des Abrufs, Formular (Quelle, Name, Adresse, Art, Region, Geltung, Aktiv), Speichern und
/// Verwerfen, „Jetzt abrufen“ mit Beobachtung, Datei-Import, Widersprüche zu den Feiertagsregeln, „Im Kalender zeigen“, Löschen mit Rückfrage und Sprungziele.
/// </summary>
public sealed class AdminSubscriptionsTests
{
    private const string FormError = "Das Abo konnte nicht gespeichert werden. Bitte die markierten Felder prüfen.";

    private static readonly Guid SecondId = Guid.NewGuid();
    private static readonly LocationDto Salzburg = new(SecondId, "Salzburg", "Europe/Vienna", "EUR", [], null, LocationContact.Empty);
    private static readonly Guid HolidayId = Guid.NewGuid();
    private static readonly Guid SchoolId = Guid.NewGuid();
    private static readonly Guid PresetId = Guid.NewGuid();
    private static readonly ManualTime Now = new(TestData.At(10));

    private static readonly SubscriptionSource AustriaSource = new("at-feiertage", "Feiertage Österreich", "https://example.org/at.ics", SubscriptionKind.PublicHoliday, "AT",
        "Offizielle Liste.", new DateOnly(2027, 2, 1));

    private static readonly SubscriptionSource ViennaSource = new("wien-ferien", "Schulferien Wien", "https://example.org/wien.ics", SubscriptionKind.SchoolHoliday, "AT-9",
        "Ferien der Stadt.", new DateOnly(2027, 2, 1));

    private static CalendarSubscriptionDto Sub(Guid id, string name, SubscriptionKind kind = SubscriptionKind.PublicHoliday, string url = "https://example.org/x.ics", string? region = "AT",
        Guid? location = null, bool active = true, bool preset = false, bool show = true, DateTimeOffset? fetched = null, SubscriptionFetchResult result = SubscriptionFetchResult.Ok,
        string? error = null, int days = 136, int conflicts = 0) =>
        new(id, name, url, kind, region, location, active, preset, show, fetched ?? TestData.At(8), result, error, null, days, new DateOnly(2027, 1, 1), new DateOnly(2028, 12, 26), conflicts);

    private static readonly CalendarSubscriptionDto Holidays = Sub(HolidayId, "Feiertage Wien", conflicts: 2);
    private static readonly CalendarSubscriptionDto School = Sub(SchoolId, "Schulferien Wien", SubscriptionKind.SchoolHoliday, region: "AT-9", location: TestData.LocationId, days: 40);

    private static readonly List<SubscriptionConflictDto> OpenConflicts =
    [
        new(new DateOnly(2027, 5, 14), "Pfingstfreitag", SubscriptionConflictKind.OnlySubscription, null, ["Wien", "Salzburg"], null),
        new(new DateOnly(2027, 11, 1), "Allerheiligen", SubscriptionConflictKind.OnlyRules, "Allerheiligen", [], SubscriptionDecision.Ignored),
    ];

    private static string Route(string tail = "") => $"/api/calendar-subscriptions{tail}";

    private static StubApi Api(params CalendarSubscriptionDto[] subscriptions)
    {
        // Die Liste verändert sich mit POST und PUT wie die der API.
        var list = (subscriptions.Length > 0 ? subscriptions : [Holidays, School]).ToList();
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location, Salzburg })
            .On($"GET {Route()}", list)
            .On($"GET {Route("/sources")}", new List<SubscriptionSource> { AustriaSource, ViennaSource })
            .On("GET /api/holidays/regions", new List<HolidayRegionDto> { new("AT-9", FiscalCountry.AT, "Wien"), new("CH-ZH", FiscalCountry.CH, "Zürich") })
            .OnJson($"POST {Route()}", body =>
            {
                var input = JsonSerializer.Deserialize<CalendarSubscriptionInput>(body!, ApiClient.Json)!;
                var created = Sub(Guid.NewGuid(), input.Name, input.Kind, input.Url, input.Region, input.LocationId, input.IsActive, fetched: null, result: SubscriptionFetchResult.Never, days: 0);
                list.Add(created);
                return created;
            }, System.Net.HttpStatusCode.Created);
        foreach (var sub in list.ToList())
        {
            api.OnJson($"PUT {Route("/" + sub.Id)}", body =>
                {
                    var input = JsonSerializer.Deserialize<CalendarSubscriptionInput>(body!, ApiClient.Json)!;
                    var changed = list.Find(s => s.Id == sub.Id)! with { Name = input.Name, Url = input.Url, Kind = input.Kind, Region = input.Region, LocationId = input.LocationId, IsActive = input.IsActive };
                    list[list.FindIndex(s => s.Id == sub.Id)] = changed;
                    return changed;
                })
                .On($"DELETE {Route("/" + sub.Id)}", null, System.Net.HttpStatusCode.NoContent)
                .On($"GET {Route("/" + sub.Id + "/conflicts")}", sub.Kind == SubscriptionKind.PublicHoliday ? OpenConflicts : new List<SubscriptionConflictDto>())
                .On($"PUT {Route("/" + sub.Id + "/decisions")}", null, System.Net.HttpStatusCode.NoContent)
                .OnJson($"PUT {Route("/" + sub.Id + "/show")}", body => sub with { ShowInCalendar = JsonDocument.Parse(body!).RootElement.GetProperty("show").GetBoolean() });
        }

        return api;
    }

    private static async Task<SubscriptionsViewModel> Open(StubApi api, FakeDialogs? dialogs = null, FakeFilePicker? files = null)
    {
        var subscriptions = new SubscriptionsViewModel(api.Client(), Now) { Dialogs = dialogs, Files = files, WatchInterval = TimeSpan.FromMilliseconds(5) };
        await subscriptions.EnsureLoadedAsync();
        return subscriptions;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    private static int CallsTo(StubApi api, string route) => api.Calls.Count(c => c.Route == route);

    [Fact]
    public async Task Given_zwei_Abos_When_die_Maske_laedt_Then_zeigt_die_Liste_Art_Stand_und_Geltung()
    {
        var subscriptions = await Open(Api());

        Assert.Equal(["Feiertage Wien", "Schulferien Wien"], subscriptions.Rows.Select(r => r.Title));
        Assert.Equal(("2 Widersprüche", "Gesetzliche Feiertage · Stand heute · 136 Tage", "Alle Standorte · AT"),
            (subscriptions.Rows[0].Badges, subscriptions.Rows[0].Detail, subscriptions.Rows[0].Places));
        Assert.Equal(("", "Schulferien · Stand heute · 40 Tage", "Wien · AT-9"), (subscriptions.Rows[1].Badges, subscriptions.Rows[1].Detail, subscriptions.Rows[1].Places));
        Assert.Equal("Feiertage Wien", subscriptions.SelectedRow?.Title);
        Assert.False(subscriptions.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_die_Regionen_der_API_When_die_Maske_laedt_Then_schlaegt_das_Feld_Region_AT_DE_CH_und_die_Regionen_vor_und_Freitext_bleibt_erlaubt()
    {
        var subscriptions = await Open(Api());

        Assert.Equal(["AT", "DE", "CH", "AT-9", "CH-ZH"], subscriptions.RegionSuggestions.Select(r => r.Code));
        Assert.Equal("Wien", subscriptions.RegionSuggestions.Single(r => r.Code == "AT-9").Name);

        subscriptions.Region = "at-5xyz";
        Assert.False(await subscriptions.SaveChangesAsync());
        Assert.Equal("Region als Ländercode oder ISO-Code, z. B. „AT“ oder „CH-ZH“.", subscriptions.RegionError);
    }

    [Fact]
    public async Task Given_ein_ausgeschaltetes_und_ein_fehlgeschlagenes_Abo_Then_stehen_Aus_und_Fehler_in_der_Liste()
    {
        var off = Sub(Guid.NewGuid(), "Alt", active: false, days: 12);
        var failed = Sub(Guid.NewGuid(), "Messe", SubscriptionKind.Notice, result: SubscriptionFetchResult.Failed, error: "Zeitüberschreitung beim Abruf.", days: 3);
        var subscriptions = await Open(Api(off, failed));

        Assert.Equal("Aus", subscriptions.Rows[0].Badges);
        Assert.Equal("Gesetzliche Feiertage · Ausgeschaltet · Kopie mit 12 Tage", subscriptions.Rows[0].Detail); // wie im Web (daysText) und im Kalender-Chip
        Assert.Equal("Fehler", subscriptions.Rows[1].Badges);
        Assert.Equal("Hinweise · Abruf fehlgeschlagen · es gilt die Kopie (Stand heute)", subscriptions.Rows[1].Detail);
    }

    [Fact]
    public async Task Given_ein_Abo_When_gespeichert_wird_Then_POST_mit_Quelle_Art_Region_und_Geltung()
    {
        var api = Api();
        var subscriptions = await Open(api);

        await subscriptions.NewCommand.ExecuteAsync(null);
        subscriptions.SelectedSource = subscriptions.Sources.Single(s => s.Source == AustriaSource);
        subscriptions.SelectedLocation = subscriptions.Locations.Single(l => l.Id == SecondId);
        Assert.True(await subscriptions.SaveChangesAsync());

        Assert.Equal(new CalendarSubscriptionInput("Feiertage Österreich", "https://example.org/at.ics", SubscriptionKind.PublicHoliday, "AT", SecondId, true),
            Sent<CalendarSubscriptionInput>(api, $"POST {Route()}"));
        Assert.Equal("Gespeichert.", subscriptions.StatusText);
        Assert.Equal("Feiertage Österreich", subscriptions.SelectedRow?.Title);
        Assert.False(subscriptions.HasUnsavedChanges);
    }

    [Fact]
    public async Task Given_ein_Name_When_eine_Quelle_gewaehlt_wird_Then_bleibt_der_Name_und_Adresse_Art_und_Region_kommen_aus_der_Quelle()
    {
        var subscriptions = await Open(Api());
        await subscriptions.NewCommand.ExecuteAsync(null);

        subscriptions.Name = "Unsere Ferien";
        subscriptions.SelectedSource = subscriptions.Sources.Single(s => s.Source == ViennaSource);

        Assert.Equal(("Unsere Ferien", "https://example.org/wien.ics", SubscriptionKind.SchoolHoliday, "AT-9"), (subscriptions.Name, subscriptions.Url, subscriptions.SelectedKind?.Kind, subscriptions.Region));
    }

    [Fact]
    public async Task Given_eine_kleingeschriebene_Region_When_gespeichert_wird_Then_geht_sie_in_Grossbuchstaben_und_ohne_Adresse_leer_an_die_API()
    {
        var api = Api();
        var subscriptions = await Open(api);
        await subscriptions.NewCommand.ExecuteAsync(null);

        subscriptions.Name = "Nur Datei";
        subscriptions.Region = " ch-zh ";
        Assert.True(await subscriptions.SaveChangesAsync());

        var sent = Sent<CalendarSubscriptionInput>(api, $"POST {Route()}");
        Assert.Equal(("Nur Datei", "", "CH-ZH", true), (sent.Name, sent.Url, sent.Region, sent.IsActive));
    }

    [Fact]
    public async Task Given_ein_Abo_ohne_Namen_When_gespeichert_wird_Then_kein_Aufruf_und_der_Fehler_steht_am_Feld()
    {
        var api = Api();
        var subscriptions = await Open(api);
        await subscriptions.NewCommand.ExecuteAsync(null);

        Assert.False(await subscriptions.SaveChangesAsync());

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST ", StringComparison.Ordinal));
        Assert.Equal("Name mit höchstens 120 Zeichen angeben.", subscriptions.NameError);
        Assert.Equal(FormError, subscriptions.Error);
        Assert.Equal("NameField", subscriptions.FocusTarget);
    }

    [Theory]
    [InlineData("ftp://example.org/a.ics", "Die Adresse muss mit https:// oder webcal:// beginnen.")]
    [InlineData("https://nutzer:geheim@example.org/a.ics", "Die Adresse darf keinen Benutzernamen und kein Passwort enthalten.")]
    public async Task Given_eine_unzulaessige_Adresse_When_gespeichert_wird_Then_Fehler_am_Feld_und_kein_Aufruf(string url, string expected)
    {
        var api = Api();
        var subscriptions = await Open(api);
        await subscriptions.NewCommand.ExecuteAsync(null);
        subscriptions.Name = "Test";
        subscriptions.Url = url;

        Assert.False(await subscriptions.SaveChangesAsync());

        Assert.Equal(expected, subscriptions.UrlError);
        Assert.Equal("UrlField", subscriptions.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_eine_ungueltige_Region_When_gespeichert_wird_Then_Fehler_am_Feld()
    {
        var subscriptions = await Open(Api());
        await subscriptions.NewCommand.ExecuteAsync(null);
        subscriptions.Name = "Test";
        subscriptions.Region = "Österreich";

        Assert.False(await subscriptions.SaveChangesAsync());

        Assert.Equal("Region als Ländercode oder ISO-Code, z. B. „AT“ oder „CH-ZH“.", subscriptions.RegionError);
        Assert.Equal("RegionField", subscriptions.FocusTarget);
    }

    [Fact]
    public async Task Given_ein_Abo_When_der_Name_geaendert_und_gespeichert_wird_Then_PUT_und_Verwerfen_setzt_zurueck()
    {
        var api = Api();
        var subscriptions = await Open(api);
        Assert.Equal(("Feiertage Wien", "https://example.org/x.ics", "AT", true), (subscriptions.Name, subscriptions.Url, subscriptions.Region, subscriptions.IsActive));

        subscriptions.Name = "Verworfen";
        Assert.True(subscriptions.HasUnsavedChanges);
        subscriptions.DiscardCommand.Execute(null);
        Assert.Equal(("Feiertage Wien", false), (subscriptions.Name, subscriptions.HasUnsavedChanges));

        subscriptions.Name = "Feiertage Österreich";
        subscriptions.IsActive = false;
        Assert.True(await subscriptions.SaveChangesAsync());

        var sent = Sent<CalendarSubscriptionInput>(api, $"PUT {Route("/" + HolidayId)}");
        Assert.Equal(("Feiertage Österreich", false, null), (sent.Name, sent.IsActive, sent.LocationId));
        Assert.Equal("Feiertage Österreich", subscriptions.SelectedRow?.Title);
    }

    [Fact]
    public async Task Given_ungespeichertes_Abo_When_ein_anderes_gewaehlt_wird_Then_fragt_die_Maske_Aenderungen_speichern()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var subscriptions = await Open(Api(), dialogs: dialogs);
        subscriptions.Name = "geändert";

        subscriptions.SelectedRow = subscriptions.Rows[1];
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Änderungen speichern?", Assert.Single(dialogs.AssertRules().Asked).Title);
        Assert.Equal("geändert", subscriptions.Name);
        Assert.Equal(HolidayId, subscriptions.SelectedRow?.Id);
    }

    [Fact]
    public async Task Given_ein_Feiertags_Abo_Then_stehen_die_Widersprueche_mit_Kind_und_Standorten_da()
    {
        var subscriptions = await Open(Api());

        Assert.Equal(["14.05.2027", "01.11.2027"], subscriptions.Conflicts.Select(c => c.Date));
        Assert.Equal(("Pfingstfreitag", "Nur im Abo, nicht in den Feiertagsregeln · Wien, Salzburg"), (subscriptions.Conflicts[0].Title, subscriptions.Conflicts[0].Detail));
        Assert.Equal("Nur in den Feiertagsregeln, nicht im Abo", subscriptions.Conflicts[1].Detail);
        Assert.True(subscriptions.Conflicts[1].IsIgnored);
        Assert.Equal("1 Tag ohne Entscheidung. Nichts wird überschrieben: Tage nur im Abo gelten, bis sie ignoriert werden; die Feiertagsregeln bleiben für die Lohnverrechnung.", subscriptions.ConflictsHint);

        subscriptions.SelectedRow = subscriptions.Rows[1];
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(subscriptions.Conflicts);
    }

    [Fact]
    public async Task Given_ein_Widerspruch_When_Uebernehmen_gewaehlt_wird_Then_PUT_der_Entscheidung_und_nochmal_nimmt_sie_zurueck()
    {
        var api = Api();
        var subscriptions = await Open(api);
        var conflict = subscriptions.Conflicts[0];

        await conflict.AcceptCommand.ExecuteAsync(null);

        var first = api.Calls.Single(c => c.Route == $"PUT {Route("/" + HolidayId + "/decisions")}");
        Assert.Equal(new SubscriptionDecisionInput(new DateOnly(2027, 5, 14), SubscriptionDecision.Accepted), JsonSerializer.Deserialize<SubscriptionDecisionInput>(first.Body!, ApiClient.Json));
        Assert.True(conflict.IsAccepted);
        Assert.Equal("Tag übernommen", subscriptions.Message);

        await conflict.AcceptCommand.ExecuteAsync(null);

        var second = JsonSerializer.Deserialize<SubscriptionDecisionInput>(api.Calls.Last(c => c.Route.EndsWith("/decisions", StringComparison.Ordinal)).Body!, ApiClient.Json)!;
        Assert.Null(second.Decision);
        Assert.False(conflict.IsAccepted);
        Assert.Equal("Entscheidung zurückgenommen", subscriptions.Message);
    }

    [Fact]
    public async Task Given_ein_Abo_When_Jetzt_abrufen_Then_POST_fetch_und_die_Rueckmeldung_steht_in_der_Maske()
    {
        var fetched = TestData.At(9);
        var api = Api(Holidays);
        api.OnJson($"POST {Route("/" + HolidayId + "/fetch")}", _ =>
        {
            // Der Server holt im Hintergrund: erst danach steht der neue Stand in der Liste.
            api.On($"GET {Route()}", new List<CalendarSubscriptionDto> { Holidays with { LastFetchAt = fetched, DayCount = 150 } });
            return new { };
        }, System.Net.HttpStatusCode.Accepted);
        var subscriptions = await Open(api);
        Assert.True(subscriptions.FetchNowCommand.CanExecute(null));

        await subscriptions.FetchNowCommand.ExecuteAsync(null);
        Assert.Equal("Der Abruf läuft im Hintergrund.", subscriptions.Message);
        await subscriptions.Watching!;

        Assert.Equal(1, CallsTo(api, $"POST {Route("/" + HolidayId + "/fetch")}"));
        Assert.Equal("Abgerufen: Stand heute · 150 Tage", subscriptions.Message);
        Assert.Equal(InfoBarSeverity.Success, subscriptions.MessageSeverity);
    }

    [Fact]
    public async Task Given_ein_Abruf_der_scheitert_When_beobachtet_wird_Then_steht_der_Fehler_als_Meldung_da()
    {
        var api = Api(Holidays);
        api.On($"POST {Route("/" + HolidayId + "/fetch")}", new { }, System.Net.HttpStatusCode.Accepted);
        var subscriptions = await Open(api);
        api.On($"GET {Route()}", new List<CalendarSubscriptionDto> { Holidays with { LastResult = SubscriptionFetchResult.Failed, LastError = "Zeitüberschreitung beim Abruf." } });

        await subscriptions.FetchNowCommand.ExecuteAsync(null);
        await subscriptions.Watching!;

        Assert.Equal("Abruf fehlgeschlagen: Zeitüberschreitung beim Abruf.", subscriptions.Message);
        Assert.Equal(InfoBarSeverity.Error, subscriptions.MessageSeverity);
    }

    [Fact]
    public async Task Given_ungespeicherte_Aenderungen_Then_ist_Jetzt_abrufen_nicht_bedienbar()
    {
        var subscriptions = await Open(Api());

        subscriptions.Url = "https://example.org/neu.ics";

        Assert.False(subscriptions.FetchNowCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_eine_gewaehlte_Datei_When_importiert_wird_Then_POST_import_mit_Name_und_Inhalt_und_die_Rueckmeldung_der_API_steht_da()
    {
        var api = Api(Holidays).OnJson($"POST {Route("/" + HolidayId + "/import")}", _ => new SubscriptionRefreshResult(10, 2, 148, "10 Tage neu, 2 entfernt.", SubscriptionFetchResult.Ok));
        var files = new FakeFilePicker { File = new PickedFile("feiertage.ics", 31, "BEGIN:VCALENDAR") };
        var subscriptions = await Open(api, files: files);

        await subscriptions.ImportCommand.ExecuteAsync(null);

        Assert.Equal(new SubscriptionImportInput("feiertage.ics", "BEGIN:VCALENDAR"), Sent<SubscriptionImportInput>(api, $"POST {Route("/" + HolidayId + "/import")}"));
        Assert.Equal("10 Tage neu, 2 entfernt.", subscriptions.Message);
        Assert.Equal(2_000_000L, files.Asked.Single().MaxBytes);
        Assert.Equal(["*.ics", "*.csv"], files.Asked.Single().Patterns);
    }

    [Fact]
    public async Task Given_eine_zu_grosse_Datei_When_importiert_wird_Then_kein_Aufruf_und_der_Fehler_steht_da()
    {
        var api = Api(Holidays);
        var files = new FakeFilePicker { File = new PickedFile("alles.ics", 2_500_000, "") };
        var subscriptions = await Open(api, files: files);

        await subscriptions.ImportCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/import", StringComparison.Ordinal));
        Assert.Equal("Die Datei ist größer als 2 MB.", subscriptions.Message);
        Assert.Equal(InfoBarSeverity.Error, subscriptions.MessageSeverity);
    }

    [Fact]
    public async Task Given_kein_gewaehlte_Datei_When_importiert_wird_Then_geschieht_nichts()
    {
        var api = Api(Holidays);
        var subscriptions = await Open(api, files: new FakeFilePicker());

        await subscriptions.ImportCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/import", StringComparison.Ordinal));
        Assert.Null(subscriptions.Message);
    }

    [Fact]
    public async Task Given_ein_Abo_When_Loeschen_bestaetigt_wird_Then_Rueckfrage_und_DELETE()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        var api = Api();
        var subscriptions = await Open(api, dialogs: dialogs);

        await subscriptions.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, CallsTo(api, $"DELETE {Route("/" + HolidayId)}"));

        await subscriptions.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"DELETE {Route("/" + HolidayId)}"));
        var asked = dialogs.AssertRules().Asked[0];
        Assert.Equal("Abo löschen?", asked.Title);
        Assert.Equal("Das Abo „Feiertage Wien“ und seine gespeicherte Kopie werden gelöscht. Die Feiertagsregeln des Programms bleiben. Ausschalten behält die Kopie.", asked.Message);
        Assert.Equal("Löschen", asked.ConfirmText);
        Assert.Equal("Gelöscht.", subscriptions.Message);
    }

    [Fact]
    public async Task Given_ein_voreingestelltes_Abo_Then_ist_Loeschen_nicht_bedienbar_und_die_Art_gesperrt()
    {
        var subscriptions = await Open(Api(Sub(PresetId, "Feiertage Österreich", preset: true)));

        Assert.False(subscriptions.DeleteCommand.CanExecute(null));
        Assert.True(subscriptions.IsPreset);
        Assert.False(subscriptions.CanChooseKind);
    }

    [Fact]
    public async Task Given_ein_Abo_When_Im_Kalender_zeigen_abgewaehlt_wird_Then_PUT_show_und_bei_Fehler_springt_es_zurueck()
    {
        var api = Api();
        var subscriptions = await Open(api);
        Assert.True(subscriptions.ShowInCalendar);

        await subscriptions.ToggleShowCommand.ExecuteAsync(null);

        Assert.False(JsonDocument.Parse(api.Calls.Single(c => c.Route == $"PUT {Route("/" + HolidayId + "/show")}").Body!).RootElement.GetProperty("show").GetBoolean());
        Assert.False(subscriptions.ShowInCalendar);

        api.On($"PUT {Route("/" + HolidayId + "/show")}", null, System.Net.HttpStatusCode.InternalServerError);
        await subscriptions.ToggleShowCommand.ExecuteAsync(null);

        Assert.False(subscriptions.ShowInCalendar);
        Assert.Equal(InfoBarSeverity.Error, subscriptions.MessageSeverity);
    }

    [Fact]
    public async Task Given_der_Assistent_mit_einem_Abo_When_die_Maske_offen_ist_Then_wird_das_Abo_gewaehlt()
    {
        var subscriptions = await Open(Api());

        subscriptions.Open(new AdminTarget(Id: SchoolId));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Schulferien Wien", subscriptions.SelectedRow?.Title);
        Assert.Equal(SchoolId, subscriptions.CurrentId);
    }

    [Fact]
    public async Task Given_ein_Server_der_ablehnt_When_gespeichert_wird_Then_steht_die_Meldung_ueber_der_Leiste()
    {
        var api = Api().On($"PUT {Route("/" + HolidayId)}", new { title = "Die Adresse ist nicht erreichbar." }, System.Net.HttpStatusCode.BadRequest);
        var subscriptions = await Open(api);
        subscriptions.Name = "Neu";

        Assert.False(await subscriptions.SaveChangesAsync());

        Assert.False(string.IsNullOrEmpty(subscriptions.Error));
        Assert.True(subscriptions.HasUnsavedChanges);
    }

    [Fact]
    public void Given_Rezeptionistin_Then_hat_die_Verwaltung_keine_Maske_Kalender_Abos()
    {
        var page = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask).AdminPage;

        Assert.Null(page.Subscriptions);
        Assert.DoesNotContain(page.Areas, a => a.Title == "Kalender-Abos");
    }

    [Fact]
    public async Task Given_der_Assistent_When_Kalender_Abos_Then_waehlt_die_Verwaltung_die_native_Maske()
    {
        var shell = new ShellViewModel(Api().Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        shell.AdminPage.Show("masterData", "subscriptions", SchoolId);
        await shell.AdminPage.InitializeAsync();
        var subscriptions = shell.AdminPage.Subscriptions!;
        await subscriptions.EnsureLoadedAsync();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Kalender-Abos", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(subscriptions, shell.AdminPage.Content);
        Assert.Equal(SchoolId, subscriptions.CurrentId);
    }
}
