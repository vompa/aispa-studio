using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Kalender-Abos im Kalender der Windows-App (ADR 0075, Block K4): der Feiertag im Datumstitel, Ferien und Hinweise im Hinweis am Datum und im Tagesüberblick,
/// der Chip „Kalender“ mit den zwei Schaltern (Zeigen je Zugang, Aktiv für alle nur Leitung), „Falsch melden“ und die Meldungen zu den Abos.
/// </summary>
public sealed class CalendarSubscriptionTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";
    private static readonly string ListRoute = $"GET /api/calendar-subscriptions?locationId={TestData.LocationId}";
    private static readonly string NoticesRoute = $"GET /api/calendar-subscriptions/notices?locationId={TestData.LocationId}";

    private static readonly CalendarDayFact Holiday = new(null, SubscriptionKind.PublicHoliday, "Staatsfeiertag", TestData.Monday, TestData.Monday, "Feiertagsregeln Österreich, Wien", null);
    private static readonly CalendarDayFact AboHoliday = new(Guid.NewGuid(), SubscriptionKind.PublicHoliday, "Betriebsfeiertag", TestData.Monday, TestData.Monday,
        "Abo ‚Feiertage Wien‘", TestData.At(10).AddDays(-1));
    private static readonly CalendarDayFact School = new(Guid.NewGuid(), SubscriptionKind.SchoolHoliday, "Semesterferien", TestData.Monday.AddDays(-2), TestData.Monday.AddDays(5),
        "Abo ‚Schulferien Wien‘", TestData.At(10).AddDays(-3));
    private static readonly CalendarDayFact Notice = new(Guid.NewGuid(), SubscriptionKind.Notice, "Straßensperre", TestData.Monday, TestData.Monday, "Abo ‚Hinweise‘", TestData.At(10));

    private static CalendarSubscriptionDto Sub(string name, SubscriptionKind kind = SubscriptionKind.PublicHoliday, bool show = true, bool active = true, Guid? location = null,
        SubscriptionFetchResult result = SubscriptionFetchResult.Ok, string? error = null, int days = 136) =>
        new(Guid.NewGuid(), name, "https://example.org/a.ics", kind, "AT", location, active, false, show, TestData.At(10).AddDays(-1), result, error, null, days,
            new DateOnly(2027, 1, 1), new DateOnly(2029, 12, 26), 0);

    private static CurrentUser Leader(bool allLocations = true) =>
        new("u9", "leitung@aispa.test", "Lena", 1, new CurrentAccess(Guid.NewGuid(), "Test-Spa", StaffRole.Manager, allLocations, allLocations ? [] : [TestData.LocationId]), null,
            [AccessRight.ViewCalendar, AccessRight.Book, AccessRight.ManageLocation]);

    private static StubApi Api(CalendarDay day, params CalendarSubscriptionDto[] subscriptions) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On(CalendarRoute, day)
        .On(ListRoute, subscriptions.ToList())
        .On(NoticesRoute, new List<SubscriptionNoticeDto>());

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, CurrentUser? user = null, IConfirmDialogs? dialogs = null)
    {
        var vm = new CalendarViewModel(api.Client(), user ?? TestData.Reception(), null, new FixedTime()) { Dialogs = dialogs };
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static JsonElement BodyOf(StubApi api, string route) => JsonDocument.Parse(api.Calls.Last(c => c.Route == route).Body!).RootElement;

    // ---------- Datumstitel, Hinweis, Tagesüberblick ----------

    [Fact]
    public async Task Der_Feiertag_steht_im_Datumstitel_Ferien_und_Hinweise_nur_im_Hinweis_und_im_Tagesueberblick()
    {
        var day = TestData.Day() with { Facts = [Holiday, AboHoliday, School, Notice] };
        var vm = await OpenAsync(Api(day));

        // Titel: nur gesetzliche Feiertage, mit „ · “ getrennt.
        Assert.Equal("Staatsfeiertag · Betriebsfeiertag", vm.DayFactsText);
        Assert.True(vm.HasDayFactsText);
        Assert.DoesNotContain("Semesterferien", vm.DayFactsText, StringComparison.Ordinal);
        Assert.DoesNotContain("Straßensperre", vm.DayFactsText, StringComparison.Ordinal);

        // Hinweis am Datum: je Eintrag eine Zeile mit Quelle und Stand; Ferien mit „bis“; zuletzt das Kürzel zum Datumsfenster.
        Assert.Equal(
            [
                "Staatsfeiertag · Feiertagsregeln Österreich, Wien",
                "Betriebsfeiertag · Abo ‚Feiertage Wien‘, Stand gestern",
                "Semesterferien bis 6.3. · Abo ‚Schulferien Wien‘, Stand vor 3 Tagen",
                "Straßensperre · Abo ‚Hinweise‘, Stand heute",
                "Datum wählen (G)",
            ],
            vm.DayFactsTip.Split(Environment.NewLine));

        // Tagesüberblick: eine Zeile über allem, am heutigen Tag mit „Heute“.
        Assert.True(vm.HasOverviewDayLine);
        Assert.Equal("Heute: Staatsfeiertag · Betriebsfeiertag · Semesterferien bis 6.3. · Straßensperre", vm.OverviewDayLine);
    }

    [Fact]
    public async Task Ohne_Tagesfakten_bleibt_der_Titel_leer_und_der_Hinweis_der_alte()
    {
        var vm = await OpenAsync(Api(TestData.Day()));

        Assert.False(vm.HasDayFactsText);
        Assert.Equal("", vm.DayFactsText);
        Assert.Equal("Datum wählen (G)", vm.DayFactsTip);
        Assert.False(vm.HasOverviewDayLine);
        Assert.Equal("", vm.OverviewDayLine);
    }

    [Fact]
    public async Task Eine_Schliesszeit_steht_hinter_dem_Feiertag_im_Titel_und_der_Chip_bleibt()
    {
        var midnight = TestData.At(1).AddHours(-1);
        var closure = new ClosureBand(Guid.NewGuid(), midnight, midnight.AddDays(2), ClosureKind.Vacation, "Betriebsurlaub", "Wegen Betriebsurlaub geschlossen.", null, 0);
        var vm = await OpenAsync(Api(TestData.Day() with { Facts = [Holiday], Closures = [closure] }));

        Assert.Equal("Staatsfeiertag · Geschlossen: Betriebsurlaub", vm.DayFactsText);
        Assert.True(vm.HasClosureChip);

        var onlyClosure = await OpenAsync(Api(TestData.Day() with { Closures = [closure] }));
        Assert.Equal("Geschlossen: Betriebsurlaub", onlyClosure.DayFactsText);
        Assert.True(onlyClosure.HasDayFactsText);
    }

    [Fact]
    public async Task Nur_Schulferien_stehen_nicht_im_Titel_aber_im_Hinweis_und_im_Tagesueberblick()
    {
        var vm = await OpenAsync(Api(TestData.Day() with { Facts = [School] }));

        Assert.False(vm.HasDayFactsText);
        Assert.Contains("Semesterferien bis 6.3.", vm.DayFactsTip, StringComparison.Ordinal);
        Assert.Equal("Heute: Semesterferien bis 6.3.", vm.OverviewDayLine);
    }

    [Fact]
    public async Task Die_Tagesfakten_gelten_nur_fuer_den_geladenen_Tag_und_nicht_in_der_Sieben_Tage_Ansicht()
    {
        var api = Api(TestData.Day() with { Facts = [Holiday] });
        var vm = await OpenAsync(api);
        Assert.Equal("Staatsfeiertag", vm.DayFactsText);

        // Noch am alten Tag: der neue Tag zeigt nichts, bis seine Daten da sind (kein falscher Feiertag am falschen Datum).
        vm.Date = TestData.Monday.AddDays(1);
        Assert.Equal("", vm.DayFactsText);
        Assert.Equal("Datum wählen (G)", vm.DayFactsTip);
    }

    // ---------- Texte ----------

    [Fact]
    public void Der_Stand_steht_in_Worten_nach_Ortszeit_und_die_Zustandszeile_nennt_Datei_Aus_und_Fehler()
    {
        var now = TestData.At(10);
        Assert.Equal("noch nicht abgerufen", SubscriptionTexts.Stand(null, now, Vienna));
        Assert.Equal("Stand heute", SubscriptionTexts.Stand(now.AddHours(-3), now, Vienna));
        Assert.Equal("Stand gestern", SubscriptionTexts.Stand(now.AddDays(-1), now, Vienna));
        Assert.Equal("Stand vor 3 Tagen", SubscriptionTexts.Stand(now.AddDays(-3), now, Vienna));
        Assert.Equal("Stand vor 14 Tagen", SubscriptionTexts.Stand(now.AddDays(-14), now, Vienna));
        Assert.Equal("Stand 14.02.2027", SubscriptionTexts.Stand(now.AddDays(-15), now, Vienna));

        Assert.Equal("Stand gestern · 136 Tage", SubscriptionTexts.Status(Sub("A"), now, Vienna));
        Assert.Equal("Ausgeschaltet · Kopie mit 136 Tage", SubscriptionTexts.Status(Sub("A", active: false), now, Vienna));
        Assert.Equal("Nur Datei · 5 Tage", SubscriptionTexts.Status(Sub("A", days: 5) with { Url = "" }, now, Vienna));
        Assert.Equal("Abruf fehlgeschlagen · es gilt die Kopie (Stand gestern)", SubscriptionTexts.Status(Sub("A", result: SubscriptionFetchResult.Failed, error: "x"), now, Vienna));
        Assert.Equal("Noch nicht abgerufen", SubscriptionTexts.Status(Sub("A", result: SubscriptionFetchResult.Never, days: 0), now, Vienna));
        Assert.Equal("Semesterferien bis 6.3.", SubscriptionTexts.WithUntil(School, TestData.Monday));
        Assert.Equal("Semesterferien bis 6.3.2028", SubscriptionTexts.WithUntil(School with { Until = new DateOnly(2028, 3, 6) }, TestData.Monday));
        Assert.Equal("Staatsfeiertag", SubscriptionTexts.WithUntil(Holiday with { Until = TestData.Monday.AddDays(3) }, TestData.Monday));
    }

    // ---------- Chip und Schalter ----------

    [Fact]
    public async Task Die_Abos_des_Standorts_stehen_im_Fenster_und_der_Punkt_zeigt_ausgeblendete()
    {
        var shown = Sub("Feiertage Wien");
        var hidden = Sub("Schulferien", SubscriptionKind.SchoolHoliday, show: false, days: 40);
        var vm = await OpenAsync(Api(TestData.Day(), shown, hidden));

        Assert.True(vm.HasSubscriptions);
        Assert.Equal(["Feiertage Wien", "Schulferien"], vm.Subscriptions.Select(s => s.Name));
        Assert.Equal(["Gesetzliche Feiertage · Stand gestern · 136 Tage", "Schulferien · Stand gestern · 40 Tage"], vm.Subscriptions.Select(s => s.KindAndStatus));
        Assert.Equal([true, false], vm.Subscriptions.Select(s => s.Show));
        Assert.True(vm.HasHiddenSubscriptions);
        Assert.Equal("Kalender-Abos, einige ausgeblendet", vm.SubscriptionsName);
        Assert.Contains("einige sind ausgeblendet", vm.SubscriptionsTip, StringComparison.Ordinal);
        // Nur Feiertags-Abos tragen den Hinweis zur Wirkung.
        Assert.Equal([true, false], vm.Subscriptions.Select(s => s.IsHoliday));
        Assert.Equal("Nur zur Information, ändert keine Buchung; aktiv/inaktiv nur Leitung", SubscriptionRowViewModel.HolidayNote);

        var noneHidden = await OpenAsync(Api(TestData.Day(), shown));
        Assert.False(noneHidden.HasHiddenSubscriptions);
        Assert.Equal("Kalender-Abos", noneHidden.SubscriptionsName);

        // Ohne Abos fehlt der Chip.
        Assert.False((await OpenAsync(Api(TestData.Day()))).HasSubscriptions);
    }

    [Fact]
    public async Task Ein_Fehler_beim_Laden_der_Abos_laesst_den_Kalender_stehen_wie_er_ist()
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On(CalendarRoute, TestData.Day());

        var vm = await OpenAsync(api);

        Assert.Null(vm.Error);
        Assert.False(vm.HasSubscriptions);
        Assert.Empty(vm.Messages);
    }

    [Fact]
    public async Task Zeigen_wirkt_sofort_ruft_nur_die_Anzeige_und_holt_den_Tag_neu()
    {
        var feiertage = Sub("Feiertage Wien");
        var api = Api(TestData.Day() with { Facts = [AboHoliday] }, feiertage)
            .On($"PUT /api/calendar-subscriptions/{feiertage.Id}/show", feiertage with { ShowInCalendar = false });
        var vm = await OpenAsync(api);
        var row = vm.Subscriptions.Single();
        var loadsBefore = api.Calls.Count(c => c.Route == CalendarRoute);
        Assert.Equal("Betriebsfeiertag", vm.DayFactsText);

        // Der Server filtert die Tagesfakten nach den ausgeblendeten Abos; der Kalender holt den Tag danach neu.
        api.On(CalendarRoute, TestData.Day() with { Facts = [] });
        row.Show = false;
        await Until(() => api.Calls.Count(c => c.Route == CalendarRoute) > loadsBefore);

        Assert.False(BodyOf(api, $"PUT /api/calendar-subscriptions/{feiertage.Id}/show").GetProperty("show").GetBoolean());
        Assert.True(vm.HasHiddenSubscriptions);
        await Until(() => vm.DayFactsText == "");
        // Nur die Anzeige: es gibt keinen Aufruf, der das Abo ein- oder ausschaltet.
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/active", StringComparison.Ordinal));
        Assert.True(row.Dto.IsActive);
    }

    [Fact]
    public async Task Ein_Fehler_beim_Zeigen_stellt_den_Schalter_zurueck_und_meldet_es()
    {
        var sub = Sub("Feiertage Wien");
        var api = Api(TestData.Day(), sub).On($"PUT /api/calendar-subscriptions/{sub.Id}/show", new { title = "kaputt" }, HttpStatusCode.InternalServerError);
        var vm = await OpenAsync(api);
        var row = vm.Subscriptions.Single();

        row.Show = false;
        await Until(() => vm.Problem is not null);

        Assert.True(row.Show);
        Assert.Equal("Abo nicht umgestellt", vm.Problem!.Title);
        Assert.False(vm.HasHiddenSubscriptions);
        // Der Rückschritt löst keinen zweiten Aufruf aus.
        Assert.Single(api.Calls, c => c.Route == $"PUT /api/calendar-subscriptions/{sub.Id}/show");
    }

    [Fact]
    public async Task Aktiv_fuer_alle_schaltet_nur_die_Leitung_und_der_Kalender_holt_den_Tag_neu()
    {
        var sub = Sub("Feiertage Wien");
        var api = Api(TestData.Day() with { Facts = [AboHoliday] }, sub)
            .On($"POST /api/calendar-subscriptions/{sub.Id}/active", sub with { IsActive = false });

        // Die Rezeption sieht die Zeile und „Zeigen“, nicht den Schalter „Aktiv (für alle)“.
        var reception = await OpenAsync(api);
        Assert.False(reception.Subscriptions.Single().CanManage);
        Assert.False(reception.CanManageSubscriptions);

        var leader = await OpenAsync(api, Leader());
        var row = leader.Subscriptions.Single();
        Assert.True(row.CanManage);
        Assert.True(row.IsActive);
        var loadsBefore = api.Calls.Count(c => c.Route == CalendarRoute);
        api.On(CalendarRoute, TestData.Day());

        row.IsActive = false;
        await Until(() => api.Calls.Count(c => c.Route == CalendarRoute) > loadsBefore);

        Assert.False(BodyOf(api, $"POST /api/calendar-subscriptions/{sub.Id}/active").GetProperty("isActive").GetBoolean());
        await Until(() => leader.Toast is { Length: > 0 });
        Assert.Equal("Feiertage Wien: ausgeschaltet", leader.Toast);
        Assert.Contains("Ausgeschaltet", row.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Abo_fuer_alle_Standorte_schaltet_nur_wer_alle_Standorte_sieht()
    {
        var forAll = Sub("Für alle");
        var forHere = Sub("Nur Wien", location: TestData.LocationId);
        var vm = await OpenAsync(Api(TestData.Day(), forAll, forHere), Leader(allLocations: false));

        Assert.Equal([false, true], vm.Subscriptions.Select(s => s.CanManage));
    }

    // ---------- Falsch melden ----------

    [Fact]
    public async Task Falsch_melden_fragt_nach_dem_Grund_und_schickt_Tag_Grund_und_Standort_an_die_Leitung()
    {
        var sub = Sub("Feiertage Wien");
        var api = Api(TestData.Day(), sub).On($"POST /api/calendar-subscriptions/{sub.Id}/report", null, HttpStatusCode.NoContent);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed, "  Am Staatsfeiertag haben wir offen.  ");
        var vm = await OpenAsync(api, dialogs: dialogs);

        await vm.Subscriptions.Single().ReportCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal(("Falsch melden?", "Melden", "Grund", false), (asked.Title, asked.ConfirmText, asked.InputLabel, asked.IsDestructive));
        Assert.Contains("Abo ‚Feiertage Wien‘ für den 01.03.2027", asked.Message, StringComparison.Ordinal);
        dialogs.AssertRules();
        var body = BodyOf(api, $"POST /api/calendar-subscriptions/{sub.Id}/report");
        Assert.Equal("2027-03-01", body.GetProperty("date").GetString());
        Assert.Equal("Am Staatsfeiertag haben wir offen.", body.GetProperty("reason").GetString());
        Assert.Equal(TestData.LocationId, body.GetProperty("locationId").GetGuid());
        Assert.Equal("Gemeldet, die Leitung sieht Tag und Grund", vm.Toast);
    }

    [Fact]
    public async Task Ohne_Grund_oder_mit_Abbrechen_wird_nichts_gesendet_und_ein_Hinweis_fuehrt_zurueck_zur_Rueckfrage()
    {
        var sub = Sub("Feiertage Wien");
        var api = Api(TestData.Day(), sub).On($"POST /api/calendar-subscriptions/{sub.Id}/report", null, HttpStatusCode.NoContent);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed, " x ").Answer(ConfirmChoice.Confirmed, "Hier ist offen");
        var vm = await OpenAsync(api, dialogs: dialogs);
        var row = vm.Subscriptions.Single();

        await row.ReportCommand.ExecuteAsync(null);
        Assert.Null(vm.Problem);

        await row.ReportCommand.ExecuteAsync(null);
        Assert.Equal("Meldung nicht gesendet", vm.Problem!.Title);
        Assert.Equal("Grund eintragen", vm.Problem.ActionText);
        Assert.DoesNotContain(api.Calls, c => c.Route.Contains("/report", StringComparison.Ordinal));

        // „Grund eintragen“ öffnet die Rückfrage noch einmal.
        vm.Problem.Action!.Execute(null);
        await Until(() => api.Calls.Any(c => c.Route.Contains("/report", StringComparison.Ordinal)));
        Assert.Equal(3, dialogs.Asked.Count);
    }

    [Fact]
    public async Task Ein_Fehler_beim_Melden_steht_als_Hinweis_mit_Erneut_versuchen()
    {
        var sub = Sub("Feiertage Wien");
        var api = Api(TestData.Day(), sub).On($"POST /api/calendar-subscriptions/{sub.Id}/report", new { title = "kaputt" }, HttpStatusCode.InternalServerError);
        var vm = await OpenAsync(api, dialogs: new FakeDialogs().Answer(ConfirmChoice.Confirmed, "Falscher Tag"));

        await vm.Subscriptions.Single().ReportCommand.ExecuteAsync(null);

        Assert.Equal("Meldung nicht gesendet", vm.Problem!.Title);
        Assert.Equal("Erneut versuchen", vm.Problem.ActionText);
    }

    // ---------- Meldungen ----------

    private static SubscriptionNoticeDto NoticeOf(SubscriptionNoticeKind kind, string title, string text, Guid? subscription = null, bool retry = false) =>
        new(Guid.NewGuid(), subscription ?? Guid.NewGuid(), TestData.LocationId, kind, title, text, TestData.At(9), retry);

    [Fact]
    public async Task Meldungen_zu_Abos_stehen_in_der_Seitenleiste_Fehler_als_Warnung_mit_Erneut_versuchen_nur_fuer_die_Leitung()
    {
        var sub = Sub("Feiertage Salzburg");
        var failed = NoticeOf(SubscriptionNoticeKind.Failed, "Kalender-Abo nicht abgerufen", "Abo ‚Feiertage Salzburg‘ konnte nicht abgerufen werden: Zeitüberschreitung. Es gilt die Kopie vom 21.10.2026.", sub.Id, retry: true);
        var changed = NoticeOf(SubscriptionNoticeKind.Changed, "Kalender-Abo geändert", "Abo ‚Feiertage Salzburg‘: 2 neue Tage, 1 Tag entfernt.");
        var api = Api(TestData.Day(), sub)
            .On(NoticesRoute, new List<SubscriptionNoticeDto> { failed, changed })
            .On($"POST /api/calendar-subscriptions/{sub.Id}/fetch", null, HttpStatusCode.Accepted)
            .On($"POST /api/calendar-subscriptions/notices/{changed.Id}/resolve", null, HttpStatusCode.NoContent);

        // Die Leitung: Warnung mit „Erneut versuchen“, Hinweis mit „Erledigt“.
        var leader = await OpenAsync(api, Leader());
        var warning = leader.Messages.Single(m => m.Kind == MessageKind.Subscription && m.Severity == MessageSeverity.Warning);
        Assert.Equal(("Kalender-Abo nicht abgerufen", "Erneut versuchen", "Kalender-Abo"), (warning.Title, warning.ActionLabel, warning.KindText));
        Assert.Equal(("kalender-abos-anzeigen", "meldungen"), warning.Help);
        var notice = leader.Messages.Single(m => m.Kind == MessageKind.Subscription && m.Severity == MessageSeverity.Notice);
        Assert.Equal("Erledigt", notice.ActionLabel);

        warning.ActionCommand!.Execute(null);
        await Until(() => api.Calls.Any(c => c.Route == $"POST /api/calendar-subscriptions/{sub.Id}/fetch"));
        await Until(() => leader.Toast == "Der Abruf läuft im Hintergrund");

        notice.ActionCommand!.Execute(null);
        await Until(() => leader.Messages.All(m => m.Key != notice.Key));
        Assert.Contains(leader.Messages, m => m.Key == warning.Key);

        // Die Rezeption sieht die Meldung, kann aber nichts anstoßen.
        var reception = await OpenAsync(api);
        Assert.Null(reception.Messages.Single(m => m.Kind == MessageKind.Subscription && m.Severity == MessageSeverity.Warning).ActionLabel);
    }

    [Fact]
    public async Task Eine_Meldung_zu_einem_Abo_bleibt_nach_dem_Wegklicken_nur_bis_sich_der_Server_aendert()
    {
        var reminder = NoticeOf(SubscriptionNoticeKind.Reminder, "Feiertage für 2029 prüfen", "Die Kalender-Abos für Feiertage reichen bis 2029.");
        var api = Api(TestData.Day(), Sub("A")).On(NoticesRoute, new List<SubscriptionNoticeDto> { reminder });

        var vm = await OpenAsync(api, Leader());

        var message = Assert.Single(vm.Messages, m => m.Kind == MessageKind.Subscription);
        Assert.Equal(("Feiertage für 2029 prüfen", MessageSeverity.Notice), (message.Title, message.Severity));
        message.DismissCommand!.Execute(null);
        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.Subscription);
    }
}
