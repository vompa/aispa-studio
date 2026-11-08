using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Kalender-Abos (ADR 0075), Verwaltung und Abruf: die drei Voreinstellungen nach Land, Anlegen und Prüfen, Rechte (Aktiv nur Leitung, Zeigen jeder), Abruf im
/// Hintergrund mit Vergleich zur alten Kopie, Fehler mit „Erneut versuchen“, Datei-Import mit Horizont und die Erinnerung im November. Kein Test geht ins Netz:
/// der Abruf liefert, was <see cref="FakeCalendarFeed"/> vorgibt.
/// </summary>
public sealed class CalendarSubscriptionTests(ApiFixture fixture)
{
    private const string Path = "/api/calendar-subscriptions";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Alle Beispieltage liegen 2027, dem Jahr der Testdaten (im Horizont des laufenden Jahres plus zwei).</summary>
    private static DateOnly Day(int month, int day) => new(2027, month, day);

    private static string Ics(params (DateOnly Day, string Title)[] events) => FakeCalendarFeed.Ics([.. events.Select(e => (e.Day, e.Title, (string?)null))]);

    private static CalendarSubscriptionInput Input(string name, string url, SubscriptionKind kind = SubscriptionKind.PublicHoliday, string? region = null,
        Guid? location = null, bool active = true) => new(name, url, kind, region, location, active);

    private static async Task<CalendarSubscriptionDto> CreateAsync(TestSpa spa, string name, string url, SubscriptionKind kind = SubscriptionKind.PublicHoliday,
        string? region = null, Guid? location = null, bool active = true) =>
        await spa.PostAsync<CalendarSubscriptionDto>(Path, Input(name, url, kind, region, location, active), Ct);

    private static Task<List<CalendarSubscriptionDto>> ListAsync(HttpClient client, Guid? location = null) =>
        client.GetFromJsonAsync<List<CalendarSubscriptionDto>>(location is { } id ? $"{Path}?locationId={id}" : Path, TestSpa.Json, Ct)!;

    private static async Task<CalendarSubscriptionDto> WaitAsync(HttpClient client, Guid id, Func<CalendarSubscriptionDto, bool> done)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var sub = (await ListAsync(client)).Single(s => s.Id == id);
            if (done(sub))
            {
                return sub;
            }

            await Task.Delay(150, timeout.Token);
        }
    }

    private static Task<CalendarSubscriptionDto> FetchedAsync(HttpClient client, Guid id, DateTimeOffset? after = null) =>
        WaitAsync(client, id, s => s.LastResult != SubscriptionFetchResult.Never && (after is null || s.LastFetchAt > after || s.LastResult == SubscriptionFetchResult.Failed));

    private static Task<List<SubscriptionNoticeDto>> NoticesAsync(HttpClient client, Guid? location = null) =>
        client.GetFromJsonAsync<List<SubscriptionNoticeDto>>(location is { } id ? $"{Path}/notices?locationId={id}" : $"{Path}/notices", TestSpa.Json, Ct)!;

    private async Task<HttpClient> JoinAsync(TestSpa spa, StaffRole role, bool allLocations = true, Guid? employee = null) =>
        await new AnswerKit(fixture).JoinAsync(spa, role, allLocations: allLocations, employee: employee);

    // ---------- Voreinstellungen ----------

    [Fact]
    public async Task Jedes_Spa_hat_die_drei_Feiertags_Abos_nach_Land_und_nur_das_eigene_Land_ist_aktiv()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var presets = (await ListAsync(spa.Client)).Where(s => s.IsPreset).OrderBy(s => s.Region, StringComparer.Ordinal).ToList();
        Assert.Equal(["AT", "CH", "DE"], presets.Select(s => s.Region));
        Assert.Equal(["Feiertage Österreich", "Feiertage Schweiz", "Feiertage Deutschland"], presets.Select(s => s.Name));
        Assert.All(presets, s => Assert.Equal((SubscriptionKind.PublicHoliday, (Guid?)null, SubscriptionFetchResult.Never), (s.Kind, s.LocationId, s.LastResult)));
        Assert.All(presets, s => Assert.StartsWith("https://calendar.google.com/calendar/ical/", s.Url, StringComparison.Ordinal));
        Assert.Equal([true, false, false], presets.Select(s => s.IsActive));

        // Ein Spa in Deutschland bekommt dieselben drei, aktiv ist das deutsche.
        using var anonymous = fixture.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/tenant/signup", new TenantSignup("Day Spa Köln", "DE", $"koeln-{Guid.NewGuid():N}@aispa.test", "Karin Inhaberin"), TestSpa.Json, Ct);
        await TestSpa.ReadAsync<CurrentUser>(response, Ct);
        using var german = fixture.CreateClient(string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        var germanPresets = (await ListAsync(german)).OrderBy(s => s.Region, StringComparer.Ordinal).ToList();
        Assert.Equal([("AT", false), ("CH", false), ("DE", true)], germanPresets.Select(s => (s.Region!, s.IsActive)));
    }

    [Fact]
    public async Task Eine_Voreinstellung_laesst_sich_aendern_und_ausschalten_aber_nicht_loeschen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var german = (await ListAsync(spa.Client)).Single(s => s.Region == "DE");

        var changed = await spa.PutAsync<CalendarSubscriptionDto>($"{Path}/{german.Id}",
            Input("Feiertage Deutschland (Betrieb)", FakeCalendarFeed.NewUrl(), SubscriptionKind.PublicHoliday, "DE", null, false), Ct);
        Assert.Equal(("Feiertage Deutschland (Betrieb)", true, false), (changed.Name, changed.IsPreset, changed.IsActive));

        var kind = await spa.Client.PutAsJsonAsync($"{Path}/{german.Id}", Input("x", german.Url, SubscriptionKind.SchoolHoliday, "DE", null, false), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, kind.StatusCode);

        var delete = await spa.Client.DeleteAsync($"{Path}/{german.Id}", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Contains("nur ausschalten", await delete.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var austria = (await ListAsync(spa.Client)).Single(s => s.Region == "AT");
        var off = await spa.PostAsync<CalendarSubscriptionDto>($"{Path}/{austria.Id}/active", new SubscriptionActiveInput(false), Ct);
        Assert.False(off.IsActive);
    }

    // ---------- Anlegen und Prüfen ----------

    [Fact]
    public async Task Anlegen_prueft_Adresse_Name_Region_und_Standort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        async Task<List<string>> FieldsAsync(CalendarSubscriptionInput input)
        {
            var response = await spa.Client.PostAsJsonAsync(Path, input, TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return [.. (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!.Errors.Keys.Order(StringComparer.Ordinal)];
        }

        Assert.Equal(["url"], await FieldsAsync(Input("Ohne https", "http://feeds.example.org/a.ics")));
        Assert.Equal(["url"], await FieldsAsync(Input("Mit Passwort", "https://hans:geheim@feeds.example.org/a.ics")));
        Assert.Equal(["url"], await FieldsAsync(Input("Mit Schlüssel", "https://feeds.example.org/a.ics?token=abc")));
        Assert.Equal(["url"], await FieldsAsync(Input("Intern", "https://127.0.0.1/a.ics")));
        Assert.Equal(["name"], await FieldsAsync(Input(" ", FakeCalendarFeed.NewUrl())));
        Assert.Equal(["name"], await FieldsAsync(Input(new string('x', 121), FakeCalendarFeed.NewUrl())));
        Assert.Equal(["region"], await FieldsAsync(Input("Region", FakeCalendarFeed.NewUrl(), region: "Wien")));
        Assert.Equal(["locationId"], await FieldsAsync(Input("Standort", FakeCalendarFeed.NewUrl(), location: Guid.NewGuid())));
        Assert.Equal(["kind"], await FieldsAsync(Input("Art", FakeCalendarFeed.NewUrl(), (SubscriptionKind)42)));

        var webcal = await CreateAsync(spa, "Mit webcal", "webcal://feeds.example.org/b.ics", active: false);
        Assert.Equal("https://feeds.example.org/b.ics", webcal.Url);
        var fileOnly = await CreateAsync(spa, "Nur Datei", "", SubscriptionKind.Notice, "at", spa.Location.Id);
        Assert.Equal(("", "AT", spa.Location.Id), (fileOnly.Url, fileOnly.Region, fileOnly.LocationId));

        // Eine Adresse nachträglich auf eine unerlaubte ändern geht auch nicht.
        var bad = await spa.Client.PutAsJsonAsync($"{Path}/{webcal.Id}", Input("Mit webcal", "http://feeds.example.org/b.ics", active: false), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Quellenvorschlaege_nennen_Land_Region_und_Tag_der_Pruefung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var sources = await spa.GetAsync<List<SubscriptionSource>>($"{Path}/sources", Ct);

        Assert.Contains(sources, s => s.Region == "AT" && s.Kind == SubscriptionKind.PublicHoliday && s.Url == CalendarFeedSources.GoogleAustria);
        Assert.Contains(sources, s => s.Region == "CH-ZH" && s.Kind == SubscriptionKind.PublicHoliday);
        Assert.Contains(sources, s => s.Region == "AT-9" && s.Kind == SubscriptionKind.SchoolHoliday && s.Url.Contains("subdivisionCode=AT-9", StringComparison.Ordinal));
        Assert.All(sources, s => Assert.Equal(CalendarFeedSources.CheckedOn, s.CheckedOn));
        // Jede Adresse, die der Betrieb übernimmt, besteht die Prüfung.
        Assert.All(sources, s => Assert.True(SubscriptionUrl.TryNormalize(s.Url, out _, out _), s.Id));
    }

    // ---------- Rechte ----------

    [Fact]
    public async Task Aktiv_schaltet_nur_die_Leitung_und_Zeigen_darf_jeder_mit_Kalenderrecht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sub = await CreateAsync(spa, "Feiertage Test", FakeCalendarFeed.NewUrl(), active: false);
        using var reception = await JoinAsync(spa, StaffRole.Reception);
        using var therapist = await JoinAsync(spa, StaffRole.Therapist, employee: spa.Anna.Id);

        foreach (var client in new[] { reception, therapist })
        {
            Assert.Contains(await ListAsync(client), s => s.Id == sub.Id);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Path, Input("Neu", FakeCalendarFeed.NewUrl()), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Path}/{sub.Id}/active", new SubscriptionActiveInput(true), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Path}/{sub.Id}", Input("Anders", sub.Url), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Path}/{sub.Id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Path}/{sub.Id}/fetch", null, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Path}/{sub.Id}/conflicts", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Path}/sources", Ct)).StatusCode);
        }

        // Zeigen: gespeichert je Zugang, die anderen sehen ihren eigenen Stand.
        var hidden = await TestSpa.ReadAsync<CalendarSubscriptionDto>(
            await reception.PutAsJsonAsync($"{Path}/{sub.Id}/show", new SubscriptionShowInput(false), TestSpa.Json, Ct), Ct);
        Assert.False(hidden.ShowInCalendar);
        Assert.False((await ListAsync(reception)).Single(s => s.Id == sub.Id).ShowInCalendar);
        Assert.True((await ListAsync(therapist)).Single(s => s.Id == sub.Id).ShowInCalendar);
        Assert.True((await ListAsync(spa.Client)).Single(s => s.Id == sub.Id).ShowInCalendar);
        Assert.False((await ListAsync(reception)).Single(s => s.Id == sub.Id).IsActive);

        var shown = await TestSpa.ReadAsync<CalendarSubscriptionDto>(
            await reception.PutAsJsonAsync($"{Path}/{sub.Id}/show", new SubscriptionShowInput(true), TestSpa.Json, Ct), Ct);
        Assert.True(shown.ShowInCalendar);
        Assert.Equal(HttpStatusCode.NotFound, (await reception.PutAsJsonAsync($"{Path}/{Guid.NewGuid()}/show", new SubscriptionShowInput(false), TestSpa.Json, Ct)).StatusCode);

        // Die Leitung schaltet ein, das Abo ist dann für alle aktiv.
        var on = await spa.PostAsync<CalendarSubscriptionDto>($"{Path}/{sub.Id}/active", new SubscriptionActiveInput(true), Ct);
        Assert.True(on.IsActive);
        Assert.True((await ListAsync(reception)).Single(s => s.Id == sub.Id).IsActive);
    }

    [Fact]
    public async Task Eine_Standortleitung_verwaltet_nur_Abos_ihres_Standorts_und_keine_fuer_alle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var preset = (await ListAsync(spa.Client)).First(s => s.IsPreset);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        using var manager = await JoinAsync(spa, StaffRole.Manager, allLocations: false);

        // Für alle Standorte oder für fremde Standorte: nein.
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync(Path, Input("Für alle", FakeCalendarFeed.NewUrl()), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync(Path, Input("Fremd", FakeCalendarFeed.NewUrl(), location: other.Id), TestSpa.Json, Ct)).StatusCode);
        var own = await TestSpa.ReadAsync<CalendarSubscriptionDto>(
            await manager.PostAsJsonAsync(Path, Input("Eigener Standort", FakeCalendarFeed.NewUrl(), location: spa.Location.Id, active: false), TestSpa.Json, Ct), Ct);
        Assert.Equal(spa.Location.Id, own.LocationId);

        // Das Abo für alle (die Voreinstellung) schaltet sie nicht um; sie sieht es, ändert es aber nicht.
        Assert.Contains(await ListAsync(manager), s => s.Id == preset.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"{Path}/{preset.Id}/active", new SubscriptionActiveInput(false), TestSpa.Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsJsonAsync($"{Path}/{preset.Id}", Input("Anders", preset.Url, region: "AT"), TestSpa.Json, Ct)).StatusCode);

        // Ein Abo eines fremden Standorts kennt sie gar nicht.
        var foreign = await CreateAsync(spa, "Fremder Standort", FakeCalendarFeed.NewUrl(), location: other.Id, active: false);
        Assert.DoesNotContain(await ListAsync(manager), s => s.Id == foreign.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"{Path}/{foreign.Id}/active", new SubscriptionActiveInput(true), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Die_Liste_je_Standort_zeigt_nur_Abos_mit_passendem_Standort_und_Land()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var forFirst = await CreateAsync(spa, "Nur Wien", "", SubscriptionKind.Notice, location: spa.Location.Id);
        var forOther = await CreateAsync(spa, "Nur Zweiter", "", SubscriptionKind.Notice, location: other.Id);

        var atFirst = await ListAsync(spa.Client, spa.Location.Id);

        Assert.Contains(atFirst, s => s.Id == forFirst.Id);
        Assert.DoesNotContain(atFirst, s => s.Id == forOther.Id);
        // Die Voreinstellung für Österreich gilt hier, die für Deutschland und die Schweiz nicht.
        Assert.Equal(["AT"], atFirst.Where(s => s.IsPreset).Select(s => s.Region));
        Assert.Equal(["AT", "CH", "DE"], (await ListAsync(spa.Client)).Where(s => s.IsPreset).Select(s => s.Region).Order(StringComparer.Ordinal));
    }

    // ---------- Abruf ----------

    [Fact]
    public async Task Der_Abruf_speichert_die_Kopie_und_meldet_spaeter_neue_und_entfernte_Tage()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = FakeCalendarFeed.NewUrl();
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Eins"), (Day(2, 18), "Zwei")));

        // Anlegen stellt den Abruf in die Queue; beim ersten Mal gibt es keine Änderungsmeldung.
        var created = await CreateAsync(spa, "Feiertage Salzburg", url);
        var first = await FetchedAsync(spa.Client, created.Id);
        Assert.Equal((SubscriptionFetchResult.Ok, 2, Day(2, 17), Day(2, 18), (string?)null), (first.LastResult, first.DayCount, first.FirstDate, first.LastDate, first.LastError));
        Assert.NotNull(first.LastFetchAt);
        Assert.InRange(first.NextCheckAt!.Value - first.LastFetchAt!.Value, TimeSpan.FromHours(23), TimeSpan.FromHours(25));
        Assert.DoesNotContain(await NoticesAsync(spa.Client), n => n.SubscriptionId == created.Id);

        // Zwei Tage kommen dazu, einer fällt weg.
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Eins"), (Day(2, 19), "Drei"), (Day(2, 20), "Vier")));
        Assert.Equal(HttpStatusCode.Accepted, (await spa.Client.PostAsync($"{Path}/{created.Id}/fetch", null, Ct)).StatusCode);
        var second = await WaitAsync(spa.Client, created.Id, s => s.LastFetchAt > first.LastFetchAt);

        Assert.Equal((3, Day(2, 17), Day(2, 20)), (second.DayCount, second.FirstDate, second.LastDate));
        var notice = Assert.Single(await NoticesAsync(spa.Client), n => n.SubscriptionId == created.Id);
        Assert.Equal(SubscriptionNoticeKind.Changed, notice.Kind);
        Assert.Contains("Abo ‚Feiertage Salzburg‘: 2 neue Tage, 1 Tag entfernt.", notice.Text, StringComparison.Ordinal);
        Assert.Contains("Neu: 19.2.2027 Drei, 20.2.2027 Vier", notice.Text, StringComparison.Ordinal);
        Assert.False(notice.CanRetry);

        // Ein weiterer Abruf mit demselben Inhalt ändert nichts und macht keine zweite Meldung.
        await spa.Client.PostAsync($"{Path}/{created.Id}/fetch", null, Ct);
        await WaitAsync(spa.Client, created.Id, s => s.LastFetchAt > second.LastFetchAt);
        Assert.Single(await NoticesAsync(spa.Client), n => n.SubscriptionId == created.Id);

        // Erledigen für alle.
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"{Path}/notices/{notice.Id}/resolve", null, Ct)).StatusCode);
        Assert.DoesNotContain(await NoticesAsync(spa.Client), n => n.Id == notice.Id);
    }

    [Fact]
    public async Task Ein_Fehler_beim_Abruf_laesst_die_letzte_Kopie_gelten_und_meldet_mit_Erneut_versuchen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = FakeCalendarFeed.NewUrl();

        // Noch nie abgerufen und die Adresse ist nicht erreichbar.
        fixture.CalendarFeed.Fail(url, "Zeitüberschreitung beim Abruf.");
        var created = await CreateAsync(spa, "Schulferien", url, SubscriptionKind.SchoolHoliday);
        var failed = await FetchedAsync(spa.Client, created.Id);
        Assert.Equal((SubscriptionFetchResult.Failed, 0, "Zeitüberschreitung beim Abruf."), (failed.LastResult, failed.DayCount, failed.LastError));
        Assert.InRange(failed.NextCheckAt!.Value - DateTimeOffset.UtcNow, TimeSpan.FromHours(5), TimeSpan.FromHours(7));
        var notice = Assert.Single(await NoticesAsync(spa.Client), n => n.SubscriptionId == created.Id);
        Assert.Equal((SubscriptionNoticeKind.Failed, true), (notice.Kind, notice.CanRetry));
        Assert.Contains("Abo ‚Schulferien‘ konnte nicht abgerufen werden: Zeitüberschreitung beim Abruf. Es gibt noch keine Kopie.", notice.Text, StringComparison.Ordinal);

        // „Erneut versuchen“: der Abruf klappt, die Fehlermeldung verschwindet von selbst.
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Frühlingsferien")));
        await spa.Client.PostAsync($"{Path}/{created.Id}/fetch", null, Ct);
        var ok = await WaitAsync(spa.Client, created.Id, s => s.LastResult == SubscriptionFetchResult.Ok);
        Assert.Equal((1, (string?)null), (ok.DayCount, ok.LastError));
        Assert.DoesNotContain(await NoticesAsync(spa.Client), n => n.SubscriptionId == created.Id);

        // Ohne Internet gilt die letzte Kopie weiter; die Meldung nennt ihren Stand, es gibt nur eine offene Meldung.
        fixture.CalendarFeed.Fail(url, "Der Kalender ist nicht erreichbar (ConnectionError).");
        await spa.Client.PostAsync($"{Path}/{created.Id}/fetch", null, Ct);
        var again = await WaitAsync(spa.Client, created.Id, s => s.LastResult == SubscriptionFetchResult.Failed);
        Assert.Equal(1, again.DayCount);
        var offline = Assert.Single(await NoticesAsync(spa.Client), n => n.SubscriptionId == created.Id);
        Assert.Contains("Es gilt die Kopie vom", offline.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_leerer_oder_falscher_Inhalt_ersetzt_eine_vorhandene_Kopie_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = FakeCalendarFeed.NewUrl();
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Eins"), (Day(2, 18), "Zwei")));
        var created = await CreateAsync(spa, "Hinweise", url, SubscriptionKind.Notice);
        await FetchedAsync(spa.Client, created.Id);

        // Ein Kalender ohne Einträge im Horizont (die Quelle ist kaputt oder abgeräumt).
        fixture.CalendarFeed.Serve(url, FakeCalendarFeed.Ics());
        await spa.Client.PostAsync($"{Path}/{created.Id}/fetch", null, Ct);
        var empty = await WaitAsync(spa.Client, created.Id, s => s.LastResult == SubscriptionFetchResult.Failed);
        Assert.Equal(2, empty.DayCount);
        Assert.Contains("keine Einträge für", empty.LastError, StringComparison.Ordinal);
        Assert.Contains("bisherige Kopie bleibt", empty.LastError, StringComparison.Ordinal);

        // Eine Fehlerseite statt eines Kalenders.
        fixture.CalendarFeed.Serve(url, "<html><body>Zugriff verweigert</body></html>");
        await spa.Client.PostAsync($"{Path}/{created.Id}/fetch", null, Ct);
        var html = await WaitAsync(spa.Client, created.Id, s => s.LastError!.Contains("keinen Kalender", StringComparison.Ordinal));
        Assert.Equal(2, html.DayCount);
    }

    [Fact]
    public async Task Ausgeschaltete_Abos_und_Abos_ohne_Adresse_werden_nicht_abgerufen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = FakeCalendarFeed.NewUrl();
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Eins")));
        var off = await CreateAsync(spa, "Aus", url, active: false);
        var fileOnly = await CreateAsync(spa, "Nur Datei", "");

        var noUrl = await spa.Client.PostAsync($"{Path}/{fileOnly.Id}/fetch", null, Ct);
        var disabled = await spa.Client.PostAsync($"{Path}/{off.Id}/fetch", null, Ct);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (noUrl.StatusCode, disabled.StatusCode));
        Assert.Contains("keine Adresse", await noUrl.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Contains("ausgeschaltet", await disabled.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        await Task.Delay(300, Ct);
        Assert.DoesNotContain(url, fixture.CalendarFeed.Requests);
        Assert.Equal(SubscriptionFetchResult.Never, (await ListAsync(spa.Client)).Single(s => s.Id == off.Id).LastResult);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsync($"{Path}/{Guid.NewGuid()}/fetch", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Der_Planer_stellt_faellige_Abos_in_die_Queue_und_im_November_kommt_die_Erinnerung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var scheduler = fixture.Factory.Services.GetRequiredService<CalendarSubscriptionScheduler>();
        var url = FakeCalendarFeed.NewUrl();
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Eins")));
        var created = await CreateAsync(spa, "Fälliges Abo", url, active: false);
        await spa.PostAsync<CalendarSubscriptionDto>($"{Path}/{created.Id}/active", new SubscriptionActiveInput(true), Ct);
        await FetchedAsync(spa.Client, created.Id);

        // Der Abruf rechnet die nächste Prüfung ab der echten Uhr; der Planer bekommt zwei Oktober-Zeitpunkte davor und danach, die Erinnerung gibt es erst ab November.
        var thisYear = DateTimeOffset.UtcNow.Year;

        // Im Oktober vor der nächsten Prüfung: das Abo ist nicht fällig, es gibt keine Erinnerung.
        await scheduler.RunForTenantAsync(spa.TenantId, new DateTimeOffset(thisYear - 1, 10, 15, 8, 0, 0, TimeSpan.Zero), Ct);
        Assert.DoesNotContain(await NoticesAsync(spa.Client), n => n.Kind == SubscriptionNoticeKind.Reminder);

        // Im Oktober nach der nächsten Prüfung ist es fällig und wird abgerufen, weiter ohne Erinnerung.
        var requestsBefore = fixture.CalendarFeed.Requests.Count(r => r == url);
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Eins"), (Day(2, 18), "Zwei")));
        Assert.True(await scheduler.RunForTenantAsync(spa.TenantId, new DateTimeOffset(thisYear + 1, 10, 15, 8, 0, 0, TimeSpan.Zero), Ct) >= 1);
        Assert.DoesNotContain(await NoticesAsync(spa.Client), n => n.Kind == SubscriptionNoticeKind.Reminder);
        await WaitAsync(spa.Client, created.Id, s => s.DayCount == 2);
        Assert.True(fixture.CalendarFeed.Requests.Count(r => r == url) > requestsBefore);

        // Im November, Uhr fest: einmal „Feiertage für das übernächste Jahr prüfen“, nur für die Leitung.
        var november = new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
        await scheduler.RunForTenantAsync(spa.TenantId, november, Ct);
        await scheduler.RunForTenantAsync(spa.TenantId, november.AddDays(5), Ct);
        var reminder = Assert.Single(await NoticesAsync(spa.Client), n => n.Kind == SubscriptionNoticeKind.Reminder);
        Assert.Equal("Feiertage für 2028 prüfen", reminder.Title);
        Assert.Contains("bis 2028", reminder.Text, StringComparison.Ordinal);
        using var reception = await JoinAsync(spa, StaffRole.Reception);
        Assert.DoesNotContain(await NoticesAsync(reception), n => n.Kind == SubscriptionNoticeKind.Reminder);

        // Erledigt bleibt erledigt, im nächsten Jahr kommt eine neue.
        await spa.Client.PostAsync($"{Path}/notices/{reminder.Id}/resolve", null, Ct);
        await scheduler.RunForTenantAsync(spa.TenantId, november.AddDays(10), Ct);
        Assert.DoesNotContain(await NoticesAsync(spa.Client), n => n.Kind == SubscriptionNoticeKind.Reminder);
        await scheduler.RunForTenantAsync(spa.TenantId, new DateTimeOffset(2027, 11, 2, 8, 0, 0, TimeSpan.Zero), Ct);
        Assert.Equal("Feiertage für 2029 prüfen", Assert.Single(await NoticesAsync(spa.Client), n => n.Kind == SubscriptionNoticeKind.Reminder).Title);
    }

    // ---------- Import ----------

    [Fact]
    public async Task Datei_Import_nutzt_denselben_Leser_ersetzt_nur_Dateieintraege_und_beachtet_den_Horizont()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = FakeCalendarFeed.NewUrl();
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Aus dem Abruf")));
        var sub = await CreateAsync(spa, "Gemischt", url, SubscriptionKind.Notice);
        await FetchedAsync(spa.Client, sub.Id);

        // CSV mit Kopfzeile: zwei Tage im Horizont, einer weit dahinter.
        var csv = await spa.PostAsync<SubscriptionRefreshResult>($"{Path}/{sub.Id}/import",
            new SubscriptionImportInput("tage.csv", "Datum;Titel\n18.02.2027;Fortbildung\n2027-02-19;Messe\n01.01.2040;Zu weit\n"), Ct);
        Assert.Equal((2, 0, 3, SubscriptionFetchResult.Ok), (csv.Added, csv.Removed, csv.Days, csv.Result));
        Assert.Contains("außerhalb von 2026 bis 2028", csv.Message, StringComparison.Ordinal);
        Assert.Equal(3, (await ListAsync(spa.Client)).Single(s => s.Id == sub.Id).DayCount);

        // ICS ersetzt die Dateieinträge, der Abruf bleibt.
        var ics = await spa.PostAsync<SubscriptionRefreshResult>($"{Path}/{sub.Id}/import",
            new SubscriptionImportInput("ferien.ics", Ics((Day(2, 22), "Ferientag"))), Ct);
        Assert.Equal((1, 2, 2), (ics.Added, ics.Removed, ics.Days));
        var after = (await ListAsync(spa.Client)).Single(s => s.Id == sub.Id);
        Assert.Equal((2, Day(2, 17), Day(2, 22)), (after.DayCount, after.FirstDate, after.LastDate));

        // Nichts Lesbares, nichts im Horizont, leer: abgelehnt, die Kopie bleibt.
        async Task<HttpValidationProblemDetails> RejectAsync(string content)
        {
            var response = await spa.Client.PostAsJsonAsync($"{Path}/{sub.Id}/import", new SubscriptionImportInput("x.txt", content), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
        }

        Assert.Contains("weder ein Kalender", (await RejectAsync("Das ist keine Liste von Tagen")).Errors["content"][0], StringComparison.Ordinal);
        Assert.Contains("keine Einträge für 2026 bis 2028", (await RejectAsync("Datum;Titel\n01.01.2040;Zu weit")).Errors["content"][0], StringComparison.Ordinal);
        Assert.Contains("leer", (await RejectAsync(" ")).Errors["content"][0], StringComparison.Ordinal);
        Assert.Contains("größer als 2 MB", (await RejectAsync(new string('x', FeedReader.MaxBytes + 1))).Errors["content"][0], StringComparison.Ordinal);
        Assert.Equal(2, (await ListAsync(spa.Client)).Single(s => s.Id == sub.Id).DayCount);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PostAsJsonAsync($"{Path}/{Guid.NewGuid()}/import", new SubscriptionImportInput("x", "a"), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Ein_Abo_ohne_Adresse_laeuft_nur_ueber_den_Import()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sub = await CreateAsync(spa, "Betriebsurlaub-Liste", "", SubscriptionKind.Other);

        var result = await spa.PostAsync<SubscriptionRefreshResult>($"{Path}/{sub.Id}/import",
            new SubscriptionImportInput("liste.csv", "Von;Bis;Titel\n01.03.2027;05.03.2027;Betriebsurlaub\n"), Ct);

        Assert.Equal((5, 0, 5), (result.Days, result.Removed, result.Added));
        Assert.Equal("Abo ‚Betriebsurlaub-Liste‘: 5 Tage übernommen.", result.Message);
        var listed = (await ListAsync(spa.Client)).Single(s => s.Id == sub.Id);
        Assert.Equal((SubscriptionFetchResult.Never, 5), (listed.LastResult, listed.DayCount));
    }

    // ---------- Falsch melden ----------

    [Fact]
    public async Task Falsch_melden_macht_eine_Meldung_an_die_Leitung_und_aendert_das_Abo_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = FakeCalendarFeed.NewUrl();
        fixture.CalendarFeed.Serve(url, Ics((Day(2, 17), "Landesfeiertag")));
        var sub = await CreateAsync(spa, "Feiertage Salzburg", url);
        await FetchedAsync(spa.Client, sub.Id);
        using var reception = await JoinAsync(spa, StaffRole.Reception);

        var report = await reception.PostAsJsonAsync($"{Path}/{sub.Id}/report", new SubscriptionReportInput(Day(2, 17), "Bei uns ist am 17.2. normal geöffnet.", spa.Location.Id), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);
        var notice = Assert.Single(await NoticesAsync(spa.Client), n => n.Kind == SubscriptionNoticeKind.Report);
        Assert.Equal("Abo ‚Feiertage Salzburg‘: 17.02.2027 falsch gemeldet", notice.Title);
        Assert.Contains("Reception Person meldet den 17.02.2027: Bei uns ist am 17.2. normal geöffnet.", notice.Text, StringComparison.Ordinal);
        Assert.Equal((sub.Id, spa.Location.Id), (notice.SubscriptionId, notice.LocationId));
        // Nur die Leitung liest Meldungen dieser Art; das Abo selbst ist unverändert.
        Assert.DoesNotContain(await NoticesAsync(reception), n => n.Kind == SubscriptionNoticeKind.Report);
        Assert.Equal(1, (await ListAsync(spa.Client)).Single(s => s.Id == sub.Id).DayCount);
        Assert.Equal(HttpStatusCode.NotFound, (await reception.PostAsync($"{Path}/notices/{notice.Id}/resolve", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.PostAsync($"{Path}/notices/{notice.Id}/resolve", null, Ct)).StatusCode);

        // Grund und Datum werden geprüft, ein fremdes Abo gibt es nicht.
        var tooShort = await reception.PostAsJsonAsync($"{Path}/{sub.Id}/report", new SubscriptionReportInput(Day(2, 17), "?"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var tooLong = await reception.PostAsJsonAsync($"{Path}/{sub.Id}/report", new SubscriptionReportInput(Day(2, 17), new string('x', 301)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reception.PostAsJsonAsync($"{Path}/{Guid.NewGuid()}/report", new SubscriptionReportInput(Day(2, 17), "Grund"), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Meldungen_je_Standort_zeigen_nur_die_des_Standorts_und_die_fuer_alle()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        var urlOne = FakeCalendarFeed.NewUrl();
        var urlTwo = FakeCalendarFeed.NewUrl();
        fixture.CalendarFeed.Fail(urlOne, "Kaputt eins.");
        fixture.CalendarFeed.Fail(urlTwo, "Kaputt zwei.");
        var one = await CreateAsync(spa, "Abo eins", urlOne, location: spa.Location.Id);
        var two = await CreateAsync(spa, "Abo zwei", urlTwo, location: other.Id);
        await FetchedAsync(spa.Client, one.Id);
        await FetchedAsync(spa.Client, two.Id);

        var atFirst = await NoticesAsync(spa.Client, spa.Location.Id);

        Assert.Contains(atFirst, n => n.SubscriptionId == one.Id);
        Assert.DoesNotContain(atFirst, n => n.SubscriptionId == two.Id);
    }
}
