using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Wirkung der Kalender-Abos (ADR 0075): Feiertage aktiver Abos ergänzen die Regel-Feiertage für Feiertagsliste, Serienvorschau, Assistent und Kalenderkopf,
/// aber nur wenn das Abo aktiv ist. „Im Kalender zeigen“ ändert nur die Anzeige für den eigenen Zugang. Widersprüche zu den Regeln stehen in einer Liste und werden
/// nie stumm überschrieben. Die Lohnverrechnung bleibt bei den Regeln. Der Abruf liefert, was <see cref="FakeCalendarFeed"/> vorgibt.
/// </summary>
public sealed class CalendarSubscriptionEffectTests(ApiFixture fixture)
{
    private const string Path = "/api/calendar-subscriptions";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly AnswerKit _kit = new(fixture);

    private static DateOnly Day(int month, int day) => new(2027, month, day);

    /// <summary>Ein Abo, dessen Kopie schon gespeichert ist (der Abruf lief über die Queue).</summary>
    private async Task<CalendarSubscriptionDto> SubscribeAsync(TestSpa spa, string name, string ics, SubscriptionKind kind = SubscriptionKind.PublicHoliday,
        string? region = "AT", Guid? location = null)
    {
        var url = FakeCalendarFeed.NewUrl();
        fixture.CalendarFeed.Serve(url, ics);
        var created = await spa.PostAsync<CalendarSubscriptionDto>(Path, new CalendarSubscriptionInput(name, url, kind, region, location, true), Ct);
        return await WaitFetchedAsync(spa.Client, created.Id);
    }

    private static async Task<CalendarSubscriptionDto> WaitFetchedAsync(HttpClient client, Guid id)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var sub = (await client.GetFromJsonAsync<List<CalendarSubscriptionDto>>(Path, TestSpa.Json, Ct))!.Single(s => s.Id == id);
            if (sub.LastResult != SubscriptionFetchResult.Never)
            {
                return sub;
            }

            await Task.Delay(150, timeout.Token);
        }
    }

    private static string Ics(params (DateOnly Day, string Title, string? Description)[] events) => FakeCalendarFeed.Ics(events);

    private static Task<CalendarSubscriptionDto> SetActiveAsync(TestSpa spa, Guid id, bool active) =>
        spa.PostAsync<CalendarSubscriptionDto>($"{Path}/{id}/active", new SubscriptionActiveInput(active), Ct);

    private static async Task<List<PublicHolidayDto>> HolidaysAsync(HttpClient client, Guid location, int year = 2027) =>
        (await client.GetFromJsonAsync<List<PublicHolidayDto>>($"/api/locations/{location}/holidays?year={year}", TestSpa.Json, Ct))!;

    private static Task<CalendarDay> CalendarAsync(HttpClient client, Guid location, DateOnly date) =>
        client.GetFromJsonAsync<CalendarDay>($"/api/locations/{location}/calendar?date={date:yyyy-MM-dd}", TestSpa.Json, Ct)!;

    // ---------- Feiertage ----------

    [Fact]
    public async Task Tage_eines_aktiven_Feiertags_Abos_gelten_wie_Regel_Feiertage_und_ein_ausgeschaltetes_Abo_gilt_nirgends()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sub = await SubscribeAsync(spa, "Feiertage Salzburg", Ics((Day(3, 8), "Betriebsfeiertag", "Gesetzlicher Feiertag"), (Day(12, 20), "Winterfeiertag", "Gesetzlicher Feiertag")));

        // Feiertagsliste: Regel-Feiertage bleiben, der Abo-Tag kommt dazu.
        var holidays = await HolidaysAsync(spa.Client, spa.Location.Id);
        Assert.Contains(new PublicHolidayDto(Day(3, 8), "Betriebsfeiertag"), holidays);
        Assert.Contains(new PublicHolidayDto(Day(3, 29), "Ostermontag"), holidays);

        // Serienvorschau: der Termin eine Woche nach dem ersten fällt auf den Abo-Feiertag und bleibt frei (Feiertage wirken nicht auf Buchungen, Ä50).
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var preview = await spa.PostAsync<List<SeriesOccurrence>>($"/api/appointments/{first.Id}/series/preview", new SeriesInput(1, Count: 2), Ct);
        Assert.Equal(SeriesOccurrenceState.Free, preview[0].State);
        Assert.Equal(SeriesOccurrenceState.Free, preview[1].State);

        // Assistent: Antwort und Quelle nennen den Feiertag, das Abo und den Stand.
        var ask = await _kit.AnswerAsync(spa, "Ist der 8. März 2027 ein Feiertag?", now: DateTimeOffset.UtcNow);
        Assert.StartsWith("Ja, am Montag, 08.03.2027 ist Betriebsfeiertag", ask.Text, StringComparison.Ordinal);
        Assert.Contains("ergänzt aus dem Abo ‚Feiertage Salzburg‘, Stand heute", ask.Source, StringComparison.Ordinal);
        var hours = await _kit.AnswerAsync(spa, "Ist am 8. März 2027 offen?", now: DateTimeOffset.UtcNow);
        Assert.Contains("gesetzlicher Feiertag (Betriebsfeiertag)", hours.Text, StringComparison.Ordinal);
        Assert.Contains("Feiertag aus dem Abo ‚Feiertage Salzburg‘, Stand heute", hours.Source, StringComparison.Ordinal);
        var listed = await _kit.AnswerAsync(spa, "Feiertage im Dezember");
        Assert.Contains(listed.Table!.Rows, row => row[1] == "Winterfeiertag");
        Assert.Contains("Abo ‚Feiertage Salzburg‘", listed.Source, StringComparison.Ordinal);

        // Ausgeschaltet gilt der Tag nirgends mehr; die Kopie bleibt gespeichert.
        await SetActiveAsync(spa, sub.Id, false);
        Assert.DoesNotContain(await HolidaysAsync(spa.Client, spa.Location.Id), h => h.Name == "Betriebsfeiertag");
        var off = await _kit.AnswerAsync(spa, "Ist der 8. März 2027 ein Feiertag?");
        Assert.StartsWith("Nein, am Montag, 08.03.2027 ist in Österreich kein gesetzlicher Feiertag", off.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Abo", off.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("Feiertag aus dem Abo", (await _kit.AnswerAsync(spa, "Ist am 8. März 2027 offen?")).Source, StringComparison.Ordinal);
        Assert.Equal(2, (await spa.GetAsync<List<CalendarSubscriptionDto>>(Path, Ct)).Single(s => s.Id == sub.Id).DayCount);
        Assert.Empty((await CalendarAsync(spa.Client, spa.Location.Id, Day(3, 8))).Facts!);

        // Wieder eingeschaltet gilt die gespeicherte Kopie sofort wieder.
        await SetActiveAsync(spa, sub.Id, true);
        Assert.Contains(await HolidaysAsync(spa.Client, spa.Location.Id), h => h.Name == "Betriebsfeiertag");
    }

    [Fact]
    public async Task Nur_Feiertage_zaehlen_Gedenktage_nicht_und_regionale_nur_im_genannten_Bundesland()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var vienna = await AnswerKit.LocationAsync(spa, "Wien", "AT-9");
        var salzburg = await AnswerKit.LocationAsync(spa, "Salzburg", "AT-5");
        var sub = await SubscribeAsync(spa, "Feiertage Österreich (Google)", Ics(
            (Day(2, 17), "Landesfeiertag (regionaler Feiertag)", "Feiertag in Wien, Niederösterreich\\nWenn Sie Feiertage ausblenden"),
            (Day(2, 18), "Muttertag", "Gedenktag\\nWenn Sie Gedenktage ausblenden möchten"),
            (Day(2, 19), "Allgemeiner Feiertag", "Gesetzlicher Feiertag")));

        var inVienna = (await HolidaysAsync(spa.Client, vienna.Id)).Select(h => h.Name).ToList();
        var inSalzburg = (await HolidaysAsync(spa.Client, salzburg.Id)).Select(h => h.Name).ToList();

        Assert.Contains("Landesfeiertag", inVienna);
        Assert.DoesNotContain("Landesfeiertag", inSalzburg);
        Assert.All(new[] { inVienna, inSalzburg }, names => Assert.Contains("Allgemeiner Feiertag", names));
        Assert.All(new[] { inVienna, inSalzburg }, names => Assert.DoesNotContain("Muttertag", names));
        // Der Gedenktag zählt auch nicht als Tag des Abos.
        Assert.Equal(2, sub.DayCount);
        // Der Titel trägt den Zusatz „(regionaler Feiertag)“ nicht.
        Assert.DoesNotContain(inVienna, name => name.Contains("regionaler", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ein_Abo_gilt_nur_am_Standort_und_im_Land_der_Region()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var vienna = await AnswerKit.LocationAsync(spa, "Wien", "AT-9");
        var munich = await AnswerKit.LocationAsync(spa, "München", "DE-BY");
        await SubscribeAsync(spa, "Nur Österreich", Ics((Day(2, 17), "Österreich-Tag", null)), region: "AT");
        await SubscribeAsync(spa, "Nur Wien", Ics((Day(2, 18), "Wien-Tag", null)), region: "AT-9");
        await SubscribeAsync(spa, "Nur München", Ics((Day(2, 19), "München-Tag", null)), region: null, location: munich.Id);

        Assert.Equal(["Österreich-Tag", "Wien-Tag"], (await HolidaysAsync(spa.Client, vienna.Id)).Where(h => h.Date.Month == 2).Select(h => h.Name));
        Assert.Equal(["München-Tag"], (await HolidaysAsync(spa.Client, munich.Id)).Where(h => h.Date.Month == 2).Select(h => h.Name));
    }

    [Fact]
    public async Task Schulferien_Hinweise_und_Sonstiges_aendern_keinen_Feiertag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SubscribeAsync(spa, "Schulferien Wien", Ics((Day(2, 17), "Semesterferien", null)), SubscriptionKind.SchoolHoliday);
        await SubscribeAsync(spa, "Hinweise", Ics((Day(2, 18), "Straßensperre", null)), SubscriptionKind.Notice);
        await SubscribeAsync(spa, "Sonstiges", Ics((Day(2, 19), "Messe", null)), SubscriptionKind.Other);

        var holidays = (await HolidaysAsync(spa.Client, spa.Location.Id)).Select(h => h.Name).ToList();
        var ask = await _kit.AnswerAsync(spa, "Ist der 17. Februar 2027 ein Feiertag?");

        Assert.DoesNotContain(holidays, name => name is "Semesterferien" or "Straßensperre" or "Messe");
        Assert.StartsWith("Nein, am Mittwoch, 17.02.2027", ask.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_Assistent_nennt_Schulferien_aus_aktiven_Abos_und_sagt_ohne_Abo_dass_nichts_hinterlegt_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var none = await _kit.AnswerAsync(spa, "Wann sind Ferien?");
        Assert.Equal(AssistantBlockKind.NotStored, none.Kind);
        Assert.Contains("Dazu ist nichts hinterlegt: Schulferien", none.Text, StringComparison.Ordinal);
        Assert.Equal(("Kalender-Abo für Schulferien anlegen", "masterData", "subscriptions"), (none.Link!.Label, none.Link.Page, none.Link.Tab));

        var sub = await SubscribeAsync(spa, "Schulferien Wien", RangeIcs((Day(3, 20), Day(3, 29), "Osterferien"), (Day(5, 15), Day(5, 17), "Pfingstferien")), SubscriptionKind.SchoolHoliday);

        // Mittwoch, 3. März 2027: die nächsten Ferien sind die Osterferien in 17 Tagen.
        var next = await _kit.AnswerAsync(spa, "Wann sind die nächsten Schulferien?");
        Assert.Equal("Die nächsten Schulferien: Osterferien, 20.03. bis 29.03.2027 (in 17 Tagen).", next.Text);
        Assert.StartsWith("Abo ‚Schulferien Wien‘, Stand ", next.Source, StringComparison.Ordinal);
        Assert.Equal(["20.03. bis 29.03.2027", "Osterferien"], next.Table!.Rows[0]);
        Assert.Equal(["15.05. bis 17.05.2027", "Pfingstferien"], next.Table.Rows[1]);

        Assert.StartsWith("Ja, am Montag, 22.03.2027 sind Ferien: Osterferien, 20.03. bis 29.03.2027.", (await _kit.AnswerAsync(spa, "Sind am 22. März 2027 Ferien?")).Text, StringComparison.Ordinal);
        var outside = await _kit.AnswerAsync(spa, "Sind am 1. April 2027 Ferien?");
        Assert.Equal("Nein, am Donnerstag, 01.04.2027 sind laut Abo keine Schulferien. Die nächsten sind Pfingstferien, 15.05. bis 17.05.2027.", outside.Text);

        // Mit Namen: nur diese Ferien.
        var named = await _kit.AnswerAsync(spa, "Wann sind die Pfingstferien?");
        Assert.Equal("Die nächsten Pfingstferien: Pfingstferien, 15.05. bis 17.05.2027 (in 73 Tagen).", named.Text);
        Assert.Single(named.Table!.Rows);
        var may = await _kit.AnswerAsync(spa, "Gibt es im Mai Ferien?");
        Assert.Equal("Mai 2027", may.Period);
        Assert.Equal(["15.05. bis 17.05.2027", "Pfingstferien"], Assert.Single(may.Table!.Rows));

        // Ausgeschaltet gilt das Abo nirgends: wieder nichts hinterlegt.
        await SetActiveAsync(spa, sub.Id, false);
        Assert.Equal(AssistantBlockKind.NotStored, (await _kit.AnswerAsync(spa, "Wann sind Ferien?")).Kind);

        // Rezeption sieht die Antwort, aber ohne Verweis in die Verwaltung (kein Recht dazu).
        await SetActiveAsync(spa, sub.Id, true);
        Assert.Null((await _kit.AnswerAsync(spa, "Wann sind Ferien?", role: StaffRole.Reception)).Link);
        var withoutSub = await TestSpa.CreateAsync(fixture, Ct);
        Assert.Null((await _kit.AnswerAsync(withoutSub, "Wann sind Ferien?", role: StaffRole.Reception)).Link);
        Assert.Contains("Eintragen kann das die Standortleitung", (await _kit.AnswerAsync(withoutSub, "Wann sind Ferien?", role: StaffRole.Reception)).Text, StringComparison.Ordinal);
    }

    /// <summary>Kalender mit mehrtägigen ganztägigen Einträgen (Beginn, letzter Tag einschließlich, Titel).</summary>
    private static string RangeIcs(params (DateOnly From, DateOnly Until, string Title)[] events) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n"
        + string.Concat(events.Select((e, i) => $"BEGIN:VEVENT\r\nUID:r{i}@test\r\nDTSTART;VALUE=DATE:{e.From:yyyyMMdd}\r\nDTEND;VALUE=DATE:{e.Until.AddDays(1):yyyyMMdd}\r\nSUMMARY:{e.Title}\r\nEND:VEVENT\r\n"))
        + "END:VCALENDAR\r\n";

    // ---------- Anzeige im Kalender ----------

    [Fact]
    public async Task Der_Kalendertag_liefert_Feiertag_Ferien_und_Hinweise_mit_Quelle_und_Stand()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var vienna = await AnswerKit.LocationAsync(spa, "Wien", "AT-9");
        var holiday = await SubscribeAsync(spa, "Feiertage Wien", Ics((Day(2, 17), "Rathaus-Tag", "Gesetzlicher Feiertag")));
        var school = await SubscribeAsync(spa, "Schulferien Wien", FakeCalendarFeed.Ics((Day(2, 15), "Semesterferien", null), (Day(2, 16), "Semesterferien", null),
            (Day(2, 17), "Semesterferien", null)), SubscriptionKind.SchoolHoliday);
        var notice = await SubscribeAsync(spa, "Hinweise", Ics((Day(2, 17), "Straßensperre vor dem Haus", null)), SubscriptionKind.Notice);

        // Feiertag aus den Regeln: Fronleichnam 2027 (27. Mai) in Wien, ohne Abo-Id.
        var rule = (await CalendarAsync(spa.Client, vienna.Id, Day(5, 27))).Facts!;
        var fronleichnam = Assert.Single(rule);
        Assert.Equal((null, SubscriptionKind.PublicHoliday, "Fronleichnam", Day(5, 27), Day(5, 27), "Feiertagsregeln Österreich, Wien", (DateTimeOffset?)null),
            (fronleichnam.SubscriptionId, fronleichnam.Kind, fronleichnam.Title, fronleichnam.From, fronleichnam.Until, fronleichnam.Source, fronleichnam.AsOf));

        // Ein Tag mit allem: Feiertag zuerst, dann die Ferien, dann der Hinweis; jede Zeile nennt das Abo und den Stand des Abrufs.
        var facts = (await CalendarAsync(spa.Client, vienna.Id, Day(2, 17))).Facts!;
        Assert.Equal(["Rathaus-Tag", "Semesterferien", "Straßensperre vor dem Haus"], facts.Select(f => f.Title));
        Assert.Equal([SubscriptionKind.PublicHoliday, SubscriptionKind.SchoolHoliday, SubscriptionKind.Notice], facts.Select(f => f.Kind));
        var ferien = facts[1];
        // Die drei einzelnen Tage der Quelle sind zu einem Zeitraum zusammengefasst.
        Assert.Equal((Day(2, 15), Day(2, 17), "Abo ‚Schulferien Wien‘", school.Id), (ferien.From, ferien.Until, ferien.Source, ferien.SubscriptionId));
        Assert.All(facts, f => Assert.True(f.AsOf > DateTimeOffset.UtcNow.AddMinutes(-5)));
        Assert.Equal((holiday.Id, notice.Id), (facts[0].SubscriptionId, facts[2].SubscriptionId));

        // Ein Tag ohne alles hat keine Fakten.
        Assert.Empty((await CalendarAsync(spa.Client, vienna.Id, Day(2, 24))).Facts!);
    }

    [Fact]
    public async Task Zeitraeume_der_Ferien_stehen_als_ein_Eintrag_mit_Beginn_und_Ende()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var csv = await spa.PostAsync<CalendarSubscriptionDto>(Path, new CalendarSubscriptionInput("Ferien (Datei)", "", SubscriptionKind.SchoolHoliday, null, null, true), Ct);
        await spa.PostAsync<SubscriptionRefreshResult>($"{Path}/{csv.Id}/import", new SubscriptionImportInput("ferien.csv", "Von;Bis;Titel\n15.02.2027;21.02.2027;Semesterferien\n"), Ct);

        var fact = Assert.Single((await CalendarAsync(spa.Client, spa.Location.Id, Day(2, 18))).Facts!);

        Assert.Equal(("Semesterferien", Day(2, 15), Day(2, 21)), (fact.Title, fact.From, fact.Until));
        Assert.Null(fact.AsOf);
        Assert.Empty((await CalendarAsync(spa.Client, spa.Location.Id, Day(2, 22))).Facts!);
    }

    [Fact]
    public async Task Ausblenden_gilt_nur_fuer_die_Anzeige_des_eigenen_Zugangs_nicht_fuer_Buchung_und_Assistent()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var holiday = await SubscribeAsync(spa, "Feiertage Salzburg", Ics((Day(3, 8), "Betriebsfeiertag", "Gesetzlicher Feiertag")));
        var school = await SubscribeAsync(spa, "Schulferien", Ics((Day(3, 8), "Frühjahrsferien", null)), SubscriptionKind.SchoolHoliday);
        using var reception = await _kit.JoinAsync(spa, StaffRole.Reception);

        // Vorher sehen beide beides.
        Assert.Equal(["Betriebsfeiertag", "Frühjahrsferien"], (await CalendarAsync(reception, spa.Location.Id, Day(3, 8))).Facts!.Select(f => f.Title));

        // Die Rezeption blendet beide Abos aus: für sie fehlen die Fakten, für die Inhaberin nicht.
        foreach (var id in new[] { holiday.Id, school.Id })
        {
            (await reception.PutAsJsonAsync($"{Path}/{id}/show", new SubscriptionShowInput(false), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        }

        Assert.Empty((await CalendarAsync(reception, spa.Location.Id, Day(3, 8))).Facts!);
        Assert.Equal(["Betriebsfeiertag", "Frühjahrsferien"], (await CalendarAsync(spa.Client, spa.Location.Id, Day(3, 8))).Facts!.Select(f => f.Title));

        // Feiertagsliste und Assistent ändern sich nicht: das Abo gilt für alle weiter.
        Assert.Contains(await HolidaysAsync(reception, spa.Location.Id), h => h.Name == "Betriebsfeiertag");
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        var preview = await spa.PostAsync<List<SeriesOccurrence>>($"/api/appointments/{first.Id}/series/preview", new SeriesInput(1, Count: 1), Ct);
        Assert.Equal(SeriesOccurrenceState.Free, preview[0].State);
        Assert.StartsWith("Ja, am Montag, 08.03.2027 ist Betriebsfeiertag", (await _kit.AnswerAsync(spa, "Ist der 8. März 2027 ein Feiertag?", role: StaffRole.Reception)).Text, StringComparison.Ordinal);
        Assert.All((await spa.GetAsync<List<CalendarSubscriptionDto>>(Path, Ct)).Where(s => s.Id == holiday.Id || s.Id == school.Id), s => Assert.True(s.IsActive));

        // Wieder einblenden.
        (await reception.PutAsJsonAsync($"{Path}/{holiday.Id}/show", new SubscriptionShowInput(true), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(["Betriebsfeiertag"], (await CalendarAsync(reception, spa.Location.Id, Day(3, 8))).Facts!.Select(f => f.Title));
    }

    [Fact]
    public async Task Ein_Feiertag_aus_den_Regeln_bleibt_im_Kalender_auch_wenn_das_Abo_ausgeblendet_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sub = await SubscribeAsync(spa, "Feiertage Österreich", Ics((Day(3, 29), "Ostermontag (Google)", "Gesetzlicher Feiertag")));
        (await spa.Client.PutAsJsonAsync($"{Path}/{sub.Id}/show", new SubscriptionShowInput(false), TestSpa.Json, Ct)).EnsureSuccessStatusCode();

        var fact = Assert.Single((await CalendarAsync(spa.Client, spa.Location.Id, Day(3, 29))).Facts!);

        // Regel und Abo nennen denselben Tag: es zählt die Regel (kein doppelter Eintrag), ausgeblendet ist nur das Abo.
        Assert.Equal(("Ostermontag", (Guid?)null), (fact.Title, fact.SubscriptionId));
        Assert.Single(await HolidaysAsync(spa.Client, spa.Location.Id), h => h.Date == Day(3, 29));
    }

    // ---------- Widersprüche ----------

    [Fact]
    public async Task Widersprueche_stehen_in_einer_Liste_und_die_Entscheidung_je_Tag_wirkt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sub = await SubscribeAsync(spa, "Feiertage Österreich", Ics(
            (Day(1, 1), "Neujahr", "Gesetzlicher Feiertag"),
            (Day(3, 8), "Betriebsfeiertag", "Gesetzlicher Feiertag"),
            (Day(10, 26), "Nationalfeiertag", "Gesetzlicher Feiertag")), region: "AT");

        var conflicts = await spa.GetAsync<List<SubscriptionConflictDto>>($"{Path}/{sub.Id}/conflicts", Ct);

        // Neujahr und der Nationalfeiertag stimmen überein. Der Betriebsfeiertag steht nur im Abo, der Ostermontag nur in den Regeln.
        Assert.DoesNotContain(conflicts, c => c.Date == Day(1, 1) || c.Date == Day(10, 26));
        var onlySub = Assert.Single(conflicts, c => c.Kind == SubscriptionConflictKind.OnlySubscription);
        Assert.Equal((Day(3, 8), "Betriebsfeiertag", (string?)null, (SubscriptionDecision?)null), (onlySub.Date, onlySub.Name, onlySub.RuleName, onlySub.Decision));
        Assert.Equal([spa.Location.Name], onlySub.Locations);
        var onlyRules = Assert.Single(conflicts, c => c.Date == Day(3, 29));
        Assert.Equal((SubscriptionConflictKind.OnlyRules, "Ostermontag", "Ostermontag"), (onlyRules.Kind, onlyRules.Name, onlyRules.RuleName));
        Assert.Equal(conflicts.Count, (await spa.GetAsync<List<CalendarSubscriptionDto>>(Path, Ct)).Single(s => s.Id == sub.Id).OpenConflicts);

        // Ignorieren: der Tag nur im Abo gilt nicht, nichts wird überschrieben; die Liste zeigt die Entscheidung, der offene Zähler sinkt.
        (await spa.Client.PutAsJsonAsync($"{Path}/{sub.Id}/decisions", new SubscriptionDecisionInput(Day(3, 8), SubscriptionDecision.Ignored), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await HolidaysAsync(spa.Client, spa.Location.Id), h => h.Name == "Betriebsfeiertag");
        var after = await spa.GetAsync<List<SubscriptionConflictDto>>($"{Path}/{sub.Id}/conflicts", Ct);
        Assert.Equal(SubscriptionDecision.Ignored, after.Single(c => c.Date == Day(3, 8)).Decision);
        Assert.Equal(conflicts.Count - 1, (await spa.GetAsync<List<CalendarSubscriptionDto>>(Path, Ct)).Single(s => s.Id == sub.Id).OpenConflicts);
        // Die Regel-Feiertage bleiben, auch der nur in den Regeln (die Lohnverrechnung braucht ihn).
        Assert.Contains(await HolidaysAsync(spa.Client, spa.Location.Id), h => h.Name == "Ostermontag");

        // Übernehmen: der Tag gilt, die Entscheidung steht in der Liste.
        (await spa.Client.PutAsJsonAsync($"{Path}/{sub.Id}/decisions", new SubscriptionDecisionInput(Day(3, 8), SubscriptionDecision.Accepted), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        Assert.Contains(await HolidaysAsync(spa.Client, spa.Location.Id), h => h.Name == "Betriebsfeiertag");
        Assert.Equal(SubscriptionDecision.Accepted, (await spa.GetAsync<List<SubscriptionConflictDto>>($"{Path}/{sub.Id}/conflicts", Ct)).Single(c => c.Date == Day(3, 8)).Decision);

        // Zurücknehmen: wieder offen (und weil nicht ignoriert, wirksam).
        (await spa.Client.PutAsJsonAsync($"{Path}/{sub.Id}/decisions", new SubscriptionDecisionInput(Day(3, 8), null), TestSpa.Json, Ct)).EnsureSuccessStatusCode();
        Assert.Null((await spa.GetAsync<List<SubscriptionConflictDto>>($"{Path}/{sub.Id}/conflicts", Ct)).Single(c => c.Date == Day(3, 8)).Decision);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.PutAsJsonAsync($"{Path}/{Guid.NewGuid()}/decisions", new SubscriptionDecisionInput(Day(3, 8), SubscriptionDecision.Ignored), TestSpa.Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Widersprueche_gibt_es_nur_fuer_Feiertags_Abos_und_nur_fuer_Standorte_an_denen_das_Abo_gilt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var school = await SubscribeAsync(spa, "Ferien", Ics((Day(2, 17), "Ferien", null)), SubscriptionKind.SchoolHoliday);
        Assert.Empty(await spa.GetAsync<List<SubscriptionConflictDto>>($"{Path}/{school.Id}/conflicts", Ct));

        // Ein Abo für Deutschland vergleicht nur mit den deutschen Standorten.
        var munich = await AnswerKit.LocationAsync(spa, "München", "DE-BY");
        var german = await SubscribeAsync(spa, "Feiertage Deutschland", Ics(
            (Day(1, 1), "Neujahrstag", "Gesetzlicher Feiertag"),
            (Day(12, 24), "Heiligabend (halber Tag)", "Gesetzlicher Feiertag\\nHalbtägiger Feiertag"),
            (Day(1, 6), "Heilige Drei Könige (regionaler Feiertag)", "Feiertag in Baden-Württemberg, Bayern, Sachsen-Anhalt")), region: "DE");

        var conflicts = await spa.GetAsync<List<SubscriptionConflictDto>>($"{Path}/{german.Id}/conflicts", Ct);

        var halfDay = Assert.Single(conflicts, c => c.Date == Day(12, 24));
        Assert.Equal((SubscriptionConflictKind.OnlySubscription, "Heiligabend (halber Tag)"), (halfDay.Kind, halfDay.Name));
        Assert.Equal(["München"], halfDay.Locations);
        Assert.All(conflicts, c => Assert.Equal(["München"], c.Locations));
        // Der Dreikönigstag steht in den Regeln für Bayern und im Abo als regionaler Feiertag: kein Widerspruch.
        Assert.DoesNotContain(conflicts, c => c.Date == Day(1, 6));
        Assert.Contains(await HolidaysAsync(spa.Client, munich.Id), h => h.Name == "Heiligabend (halber Tag)");
        Assert.DoesNotContain(await HolidaysAsync(spa.Client, spa.Location.Id), h => h.Name == "Heiligabend (halber Tag)");
    }

    // ---------- Beispieldaten ----------

    [Fact]
    public async Task Das_Beispiel_Spa_hat_ein_Feiertags_Abo_je_Standort_und_Schulferien_in_Wien_ohne_Netzabruf()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        _ = await DemoData.SeedAsync(scope.ServiceProvider, TimeProvider.System, Ct);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var subs = await db.CalendarSubscriptions.AsNoTracking().Include(s => s.Entries).AsSplitQuery().ToListAsync(Ct);
        var locations = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Name, l => l.Id, Ct);

        // Drei Voreinstellungen nach Land (nur Österreich aktiv, noch nicht abgerufen) und vier Abos mit gespeicherter Kopie.
        Assert.Equal(3, subs.Count(s => s.Preset is not null));
        var withCopy = subs.Where(s => s.Preset is null).OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
        Assert.Equal(["Feiertage Graz", "Feiertage Salzburg", "Feiertage Wien", "Schulferien Wien"], withCopy.Select(s => s.Name));
        Assert.All(withCopy, s => Assert.Equal((true, SubscriptionFetchResult.Ok), (s.IsActive, s.LastResult)));
        Assert.Equal(locations["Wien Innere Stadt"], withCopy.Single(s => s.Name == "Schulferien Wien").LocationId);
        Assert.Equal([locations["Graz Zentrum"], locations["Salzburg Altstadt"], locations["Wien Innere Stadt"]], withCopy.Where(s => s.Name.StartsWith("Feiertage", StringComparison.Ordinal)).Select(s => s.LocationId!.Value));
        // Kein Netzabruf beim Einspielen (andere Tests rufen ihre eigenen Adressen ab, die Adresse der Schulferien kommt nur hier vor).
        Assert.DoesNotContain(fixture.CalendarFeed.Requests, r => r.StartsWith("https://openholidaysapi.org/", StringComparison.Ordinal));

        // Schulferien Wien: die Herbstferien 2026; die Feiertage kommen ohne Widerspruch zu den Regeln (die Widerspruchsliste ist in der Vorführung leer).
        Assert.Contains(withCopy.Single(s => s.Name == "Schulferien Wien").Entries, e => e.Title == "Herbstferien" && e.From == new DateOnly(2026, 10, 27) && e.Until == new DateOnly(2026, 10, 31));
        var vienna = withCopy.Single(s => s.Name == "Feiertage Wien");
        Assert.Contains(vienna.Entries, e => e.Source == CalendarSubscriptionEntry.FromFeed && e.Title == "Nationalfeiertag");
        var places = await PublicHolidays.LocationsAsync(db, Ct);
        var conflicts = CalendarSubscriptionService.ConflictsOf(vienna, places, new Dictionary<DateOnly, SubscriptionDecision>(), new DateOnly(DateTime.UtcNow.Year, 1, 1));
        Assert.Empty(conflicts);
        Assert.DoesNotContain(withCopy.SelectMany(s => s.Entries), e => e.Source == CalendarSubscriptionEntry.FromFile);
    }

    // ---------- Lohnverrechnung ----------

    [Fact]
    public async Task Die_Lohnverrechnung_bleibt_bei_den_Regel_Feiertagen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<PayrollEmployeeDto>($"/api/payroll/employees/{spa.Anna.Id}", new PersonnelNumberInput("1001"), Ct);
        var before = await spa.GetAsync<PayrollPreviewDto>("/api/payroll/preview?year=2027&month=3", Ct);

        // Ein Abo-Feiertag am Montag, 22. März, an dem Anna arbeitet.
        await SubscribeAsync(spa, "Feiertage Test", Ics((Day(3, 22), "Betriebsfeiertag", "Gesetzlicher Feiertag")));
        var holidays = await HolidaysAsync(spa.Client, spa.Location.Id);
        var after = await spa.GetAsync<PayrollPreviewDto>("/api/payroll/preview?year=2027&month=3", Ct);

        Assert.Contains(holidays, h => h.Date == Day(3, 22));
        decimal Hours(PayrollPreviewDto preview) => preview.Lines.Single(l => l.EmployeeId == spa.Anna.Id && l.Item == PayrollItem.HolidayHours).Quantity;
        Assert.Equal(Hours(before), Hours(after));
        Assert.Equal(8m, Hours(after));
    }
}
