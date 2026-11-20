using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 95 (Ä206), seit ADR 0098 die Seite „Schließzeiten“: eine Liste mit Schließzeiten und Raumsperren aus der Übersicht der API, kommende zuerst, Vergangenes eingeklappt; das
/// Formular der Schließzeit in der Seitenleiste mit Löschen, Termine absagen und Protokoll; „Einrichtung“ mit der Wartezeit; Rückmeldungen als Toast. Die Raumsperre steht in
/// <see cref="AdminRoomClosuresTests"/>.
/// </summary>
public sealed class AdminClosuresTests
{
    private const string FormError = "Die Schließzeit konnte nicht gespeichert werden. Bitte die markierten Felder prüfen.";

    private static readonly Guid ClosureId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();
    private static readonly ManualTime Now = new(TestData.At(10));

    private static string Route(string tail = "") => $"/api/locations/{TestData.LocationId}/closures{tail}";

    private static DateTimeOffset Midnight(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static ClosureDto Closure(Guid id, DateOnly from, DateOnly until, ClosureKind kind = ClosureKind.Vacation, ClosurePhase phase = ClosurePhase.Planned,
        int open = 0, int cancelled = 0, int waitlist = 0, string? reason = null, string? guest = null) =>
        new(id, TestData.LocationId, from, null, until, null, Midnight(from), Midnight(until).AddDays(1), kind, reason, guest, phase, "Katrin", TestData.At(8), open, cancelled, waitlist);

    private static readonly ClosureDto Christmas = Closure(ClosureId, new(2027, 12, 24), new(2027, 12, 26), reason: "Inventur");
    private static readonly ClosureDto Past = Closure(OtherId, new(2027, 1, 4), new(2027, 1, 6), ClosureKind.Outage, ClosurePhase.Ended, guest: "Wasserschaden");

    private static StubApi Api(params ClosureDto[] closures)
    {
        // Die Liste verändert sich mit POST und PUT wie die der API; die Übersicht und der Einzelabruf lesen sie.
        var list = closures.ToList();
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .OnOverview(TestData.LocationId, () => list.Select(ClosuresTestKit.ToItem))
            .On($"GET /api/locations/{TestData.LocationId}/guest-message-delay", new GuestMessageDelayDto(10))
            .OnJson($"DELETE {Route("/" + ClosureId)}", _ =>
            {
                list.RemoveAll(c => c.Id == ClosureId);
                return new object();
            }, System.Net.HttpStatusCode.NoContent);
        void Serve(Guid id) => api.OnJson($"GET {Route("/" + id)}", _ => list.First(c => c.Id == id));
        foreach (var closure in list)
        {
            Serve(closure.Id);
        }

        return api
            .OnJson($"POST {Route()}", body =>
            {
                var created = FromInput(Guid.NewGuid(), body);
                list.Add(created);
                Serve(created.Id);
                return created;
            })
            .OnJson($"PUT {Route("/" + ClosureId)}", body =>
            {
                var changed = FromInput(ClosureId, body);
                list[list.FindIndex(c => c.Id == ClosureId)] = changed;
                return changed;
            });
    }

    private static ClosureDto FromInput(Guid id, string? body)
    {
        var input = JsonSerializer.Deserialize<ClosureInput>(body!, ApiClient.Json)!;
        return Closure(id, input.FromDate, input.UntilDate, input.Kind, reason: input.InternalReason, guest: input.GuestText) with { FromTime = input.FromTime, UntilTime = input.UntilTime };
    }

    private static async Task<ClosuresPageViewModel> Open(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var page = new ClosuresPageViewModel(api.Client(), user ?? TestData.Owner(), Now) { Dialogs = dialogs };
        await page.EnsureLoadedAsync();
        return page;
    }

    /// <summary>Die Seite mit dem Formular der ersten kommenden Schließzeit offen.</summary>
    private static async Task<ClosuresPageViewModel> OpenFirst(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var page = await Open(api, user, dialogs);
        await page.OpenRowAsync(page.Upcoming[0]);
        return page;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    private static int CallsTo(StubApi api, string route) => api.Calls.Count(c => c.Route == route);

    private static string OverviewRoute(ClosureScope scope) => $"GET /api/locations/{TestData.LocationId}/closure-overview?scope={scope}";

    [Fact]
    public async Task Given_eine_Schliesszeit_24_bis_26_12_When_sie_angelegt_wird_Then_POST_mit_den_Tagen_und_der_Art()
    {
        var api = Api();
        var page = await Open(api);
        var closures = page.ClosureEditor;

        await page.NewCommand.ExecuteAsync(null);
        closures.Period.FromDate = "24.12.2027";
        closures.Period.UntilDate = "26.12.2027";
        closures.Kind = ClosureKind.SpecialClosure;
        closures.GuestText = "Frohe Weihnachten";
        Assert.True(await closures.SaveChangesAsync());

        Assert.Equal(new ClosureInput(new(2027, 12, 24), null, new(2027, 12, 26), null, ClosureKind.SpecialClosure, null, "Frohe Weihnachten"), Sent<ClosureInput>(api, $"POST {Route()}"));
        Assert.False(closures.HasUnsavedChanges);
        var row = Assert.Single(page.Upcoming);
        Assert.Equal(("Sonderschließung", "24.12.2027 bis 26.12.2027", "Frohe Weihnachten", "Standort"), (row.Title, row.Period, row.Reason, row.TypeText));
        Assert.Same(row, page.SelectedUpcoming);
        Assert.Equal("Schließzeit gespeichert", page.Toast.Text);
    }

    [Fact]
    public async Task Given_ein_neues_Formular_Then_Von_und_Bis_sind_heute_und_die_Art_ist_Betriebsurlaub()
    {
        var page = await Open(Api());
        var closures = page.ClosureEditor;

        await page.NewCommand.ExecuteAsync(null);

        Assert.True(closures.IsCreating);
        Assert.Equal(("01.03.2027", "01.03.2027", ClosureKind.Vacation, false), (closures.Period.FromDate, closures.Period.UntilDate, closures.Kind, closures.Period.IsPartial));
        Assert.False(closures.HasUnsavedChanges);
        Assert.Equal("Neue Schließzeit", closures.HeadingText);
        Assert.True(page.IsPanelOpen);
        Assert.Same(closures, page.Editor);
        Assert.Equal("Neue Schließzeit", page.PanelTitle);
        Assert.False(page.NewIsRoom);
    }

    [Fact]
    public async Task Given_bis_vor_von_When_gespeichert_wird_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = Api();
        var page = await Open(api);
        var closures = page.ClosureEditor;
        await page.NewCommand.ExecuteAsync(null);

        closures.Period.FromDate = "26.12.2027";
        closures.Period.UntilDate = "24.12.2027";

        Assert.False(await closures.SaveChangesAsync());
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST ", StringComparison.Ordinal));
        Assert.Equal("Das Ende darf nicht vor dem Beginn liegen.", closures.Period.UntilDateError);
        Assert.Equal(FormError, closures.Error);
        Assert.Equal("UntilDateField", closures.FocusTarget);
    }

    [Fact]
    public async Task Given_ein_halber_Tag_mit_Ende_vor_Beginn_When_gespeichert_wird_Then_Fehler_an_der_Uhrzeit()
    {
        var api = Api();
        var page = await Open(api);
        var closures = page.ClosureEditor;
        await page.NewCommand.ExecuteAsync(null);

        closures.Period.IsPartial = true;
        closures.Period.FromTime = "13:00";
        closures.Period.UntilTime = "12:00";

        Assert.False(await closures.SaveChangesAsync());
        Assert.Equal("Die Uhrzeit des Endes muss nach der des Beginns liegen.", closures.Period.UntilTimeError);
        Assert.Equal("UntilTimeField", closures.FocusTarget);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_ein_leeres_Datum_When_gespeichert_wird_Then_steht_der_Fehler_am_Feld_wie_im_Web()
    {
        var page = await Open(Api());
        var closures = page.ClosureEditor;
        await page.NewCommand.ExecuteAsync(null);

        closures.Period.FromDate = "";
        closures.Period.UntilDate = "";

        Assert.False(await closures.SaveChangesAsync());
        Assert.Equal(("Bitte den ersten geschlossenen Tag wählen.", "Bitte den letzten geschlossenen Tag wählen."), (closures.Period.FromDateError, closures.Period.UntilDateError));
        Assert.Equal("FromDateField", closures.FocusTarget);

        closures.Period.FromDate = "24.12.2027";
        Assert.Null(closures.Period.FromDateError);
    }

    [Fact]
    public async Task Given_ein_halber_Tag_When_gespeichert_wird_Then_gehen_die_Uhrzeiten_mit_und_ohne_Haken_nicht()
    {
        var api = Api();
        var page = await Open(api);
        var closures = page.ClosureEditor;
        await page.NewCommand.ExecuteAsync(null);
        closures.Period.FromDate = "24.12.2027";
        closures.Period.UntilDate = "24.12.2027";
        closures.Period.FromTime = "13:00";
        closures.Period.IsPartial = true;
        closures.InternalReason = "  Wasserschaden  ";

        Assert.True(await closures.SaveChangesAsync());
        Assert.Equal(new ClosureInput(new(2027, 12, 24), new(13, 0), new(2027, 12, 24), null, ClosureKind.Vacation, "Wasserschaden", null), Sent<ClosureInput>(api, $"POST {Route()}"));

        // Haken weg: Uhrzeiten bleiben im Formular stehen, gehen aber nicht mit.
        await page.NewCommand.ExecuteAsync(null);
        closures.Period.FromTime = "13:00";
        closures.Period.IsPartial = false;
        Assert.True(await closures.SaveChangesAsync());
        Assert.Equal(2, CallsTo(api, $"POST {Route()}"));
        var second = JsonSerializer.Deserialize<ClosureInput>(api.Calls.Last(c => c.Route == $"POST {Route()}").Body!, ApiClient.Json)!;
        Assert.Null(second.FromTime);
    }

    [Fact]
    public async Task Given_zu_langer_Text_When_gespeichert_wird_Then_Fehler_am_Feld()
    {
        var page = await Open(Api());
        var closures = page.ClosureEditor;
        await page.NewCommand.ExecuteAsync(null);

        closures.GuestText = new string('a', 201);
        closures.InternalReason = new string('b', 201);

        Assert.False(await closures.SaveChangesAsync());
        Assert.Equal(("Höchstens 200 Zeichen.", "Höchstens 200 Zeichen."), (closures.GuestTextError, closures.InternalReasonError));
    }

    [Fact]
    public async Task Given_die_Liste_When_geladen_Then_zeigt_sie_die_kommenden_und_Vergangene_klappt_die_beendeten_auf()
    {
        // ADR 0098: Kommendes zuerst; Vergangenes ist eingeklappt und lädt erst beim Aufklappen (ein Aufruf mit „alle“).
        var api = Api(Christmas, Past);
        var page = await Open(api);

        Assert.Equal(["Betriebsurlaub"], page.Upcoming.Select(r => r.Title));
        Assert.Equal(("Vergangene (1)", true, false), (page.PastLabel, page.HasPast, page.IsPastOpen));
        Assert.Empty(page.Past);
        Assert.Equal(1, CallsTo(api, OverviewRoute(ClosureScope.Upcoming)));
        Assert.Equal(0, CallsTo(api, OverviewRoute(ClosureScope.All)));
        var row = page.Upcoming[0];
        Assert.Equal(("Standort", "Inventur", "keine offenen Termine", "Geplant", false), (row.TypeText, row.Reason, row.AppointmentsText, row.StatusText, row.IsEnded));

        await page.TogglePastCommand.ExecuteAsync(null);

        Assert.True(page.IsPastOpen);
        Assert.Equal(1, CallsTo(api, OverviewRoute(ClosureScope.All)));
        var ended = Assert.Single(page.Past);
        Assert.Equal(("Ausfall", "Wasserschaden", "Beendet", true), (ended.Title, ended.Reason, ended.StatusText, ended.IsEnded));
        Assert.Contains("beendet", ended.Name.ToLowerInvariant(), StringComparison.Ordinal);
        Assert.Equal(["Betriebsurlaub"], page.Upcoming.Select(r => r.Title));

        // Ein vergangener Eintrag öffnet sich wie jeder andere, mit seinem Zustand.
        await page.OpenRowAsync(ended);
        Assert.Equal(("Beendet", "Wasserschaden"), (page.ClosureEditor.PhaseText, page.ClosureEditor.GuestText));
        Assert.Same(ended, page.SelectedPast);
        Assert.Null(page.SelectedUpcoming);

        await page.TogglePastCommand.ExecuteAsync(null);
        Assert.False(page.IsPastOpen);
        Assert.Empty(page.Past);
    }

    [Fact]
    public async Task Given_ein_neues_Formular_When_Vergangene_aufgeklappt_wird_Then_bleiben_die_Eingaben()
    {
        var page = await Open(Api(Christmas, Past));
        await page.NewCommand.ExecuteAsync(null);
        page.ClosureEditor.InternalReason = "Inventur";

        await page.TogglePastCommand.ExecuteAsync(null);

        Assert.True(page.ClosureEditor.IsCreating);
        Assert.Equal("Inventur", page.ClosureEditor.InternalReason);
        Assert.Single(page.Past);
    }

    [Fact]
    public async Task Given_keine_Schliesszeit_Then_zeigt_die_Seite_den_Leerzustand_mit_der_ersten_Aktion()
    {
        var page = await Open(Api());

        Assert.Empty(page.Upcoming);
        Assert.True(page.ShowEmpty);
        Assert.False(page.ShowList);
        Assert.Equal($"{TestData.Location.Name} ist durchgehend offen", page.EmptyTitle);
        Assert.False(page.Editor?.HasForm == true);

        await page.NewLocationCommand.ExecuteAsync(null);
        Assert.True(page.ClosureEditor.IsCreating);
        Assert.False(page.NewIsRoom);
    }

    [Fact]
    public async Task Given_nur_vergangene_Eintraege_Then_steht_eine_ruhige_Zeile_statt_der_Aktionen()
    {
        var page = await Open(Api(Past));

        Assert.False(page.ShowEmpty);
        Assert.True(page.ShowNoUpcoming);
        Assert.True(page.ShowList);
        Assert.Equal("Vergangene (1)", page.PastLabel);
    }

    [Fact]
    public async Task Given_eine_Schliesszeit_mit_abgesagten_Terminen_When_das_Formular_sie_zeigt_Then_fehlt_Loeschen_und_das_Protokoll_ist_da()
    {
        var notice = Notice(ClosureNoticeState.Sent);
        var api = Api(Christmas with { CancelledAppointments = 1 }).On($"GET {Route("/" + ClosureId + "/notices")}", new List<ClosureNoticeDto> { notice });

        var closures = (await OpenFirst(api)).ClosureEditor;

        Assert.False(closures.CanDelete);
        Assert.False(closures.DeleteCommand.CanExecute(null));
        Assert.True(closures.HasProtocol);
        Assert.Equal("Protokoll der Absagen (1)", closures.ProtocolHeader);
        var row = Assert.Single(closures.Notices);
        Assert.Equal(("Ida Muster", "Mo. 1.3. 10:00, Massage", "Mail · DE", "gesendet", "Katrin, 01.03.2027 08:00"), (row.Guest, row.WhenText, row.ChannelText, row.StateText, row.ByText));
        Assert.Null(closures.NoDeleteHint);
    }

    [Fact]
    public async Task Given_eine_Schliesszeit_ohne_Absagen_Then_ist_Loeschen_moeglich_und_das_Protokoll_fehlt()
    {
        var closures = (await OpenFirst(Api(Christmas))).ClosureEditor;

        Assert.True(closures.CanDelete);
        Assert.False(closures.HasProtocol);
        Assert.Empty(closures.Notices);
        Assert.Equal("Wurden wegen dieser Schließzeit Termine abgesagt, bleibt sie als Verlauf stehen und lässt sich nicht löschen.", closures.NoDeleteHint);
    }

    [Fact]
    public async Task Given_eine_Schliesszeit_mit_offenen_Terminen_When_Termine_absagen_bestaetigt_wird_Then_POST_cancellation()
    {
        var rows = new[] { ClosureTests.Row("Ida Muster", 10, ClosureChannel.Email), ClosureTests.Row("Sonja Test", 12, ClosureChannel.Sms) };
        var open = Christmas with { OpenAppointments = 2, OpenWaitlist = 1 };
        var preview = ClosureTests.Preview(true, rows) with { Closure = open };
        var dialogs = new FakeDialogs();
        var api = Api(open)
            .On($"GET {Route("/" + ClosureId + "/cancellation")}", preview)
            .On($"POST {Route("/" + ClosureId + "/cancellation")}", new CancelClosureAppointmentsResult(2, 2, 0, 0, []));
        var closures = (await OpenFirst(api, dialogs: dialogs)).ClosureEditor;
        Assert.Equal("2 Termine liegen in der Schließzeit und bleiben gebucht, bis sie abgesagt werden. 1 Eintrag der Warteliste hat den Wunsch in der Schließzeit und wartet auf eine Nachricht.", closures.OpenNotice);

        var asking = closures.CancelAppointmentsCommand.ExecuteAsync(null);
        var dialog = await Opened(dialogs, asking);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST ", StringComparison.Ordinal));
        dialog.Rows[1].IsChecked = false;
        dialog.SendCommand.Execute(null);
        await asking;

        var post = Sent<CancelClosureAppointmentsRequest>(api, $"POST {Route("/" + ClosureId + "/cancellation")}");
        Assert.Equal([rows[0].AppointmentId], post.AppointmentIds);
        Assert.Equal("Absage Ihres Termins: {Behandlung}, {Zeit}", post.Subject);
        Assert.False(post.OfferRebooking);
    }

    [Fact]
    public async Task Given_eine_Absage_When_sie_durch_ist_Then_steht_die_Rueckmeldung_als_Toast_und_die_Liste_ist_neu_geladen()
    {
        var rows = new[] { ClosureTests.Row("Ida Muster", 10, ClosureChannel.Email) };
        var open = Christmas with { OpenAppointments = 1 };
        var dialogs = new FakeDialogs();
        var api = Api(open)
            .On($"GET {Route("/" + ClosureId + "/cancellation")}", ClosureTests.Preview(true, rows) with { Closure = open })
            .On($"POST {Route("/" + ClosureId + "/cancellation")}", new CancelClosureAppointmentsResult(1, 1, 0, 0, []));
        var page = await OpenFirst(api, dialogs: dialogs);

        var asking = page.ClosureEditor.CancelAppointmentsCommand.ExecuteAsync(null);
        (await Opened(dialogs, asking)).SendCommand.Execute(null);
        await asking;

        // Ein Erfolg ist ein Toast (eine Zeile, ohne Schlusspunkt), kein Balken im Formular (meldungen.md M1).
        Assert.Equal("1 Termin abgesagt, 1 Nachricht", page.Toast.Text);
        Assert.Null(page.ClosureEditor.Message);
        Assert.Equal(2, CallsTo(api, OverviewRoute(ClosureScope.Upcoming)));
    }

    [Fact]
    public async Task Given_eine_Absage_mit_Problemen_Then_bleibt_die_Warnung_im_Formular_stehen()
    {
        var rows = new[] { ClosureTests.Row("Ida Muster", 10, ClosureChannel.Email) };
        var open = Christmas with { OpenAppointments = 1 };
        var dialogs = new FakeDialogs();
        var api = Api(open)
            .On($"GET {Route("/" + ClosureId + "/cancellation")}", ClosureTests.Preview(true, rows) with { Closure = open })
            .On($"POST {Route("/" + ClosureId + "/cancellation")}", new CancelClosureAppointmentsResult(1, 1, 0, 1, []));
        var page = await OpenFirst(api, dialogs: dialogs);

        var asking = page.ClosureEditor.CancelAppointmentsCommand.ExecuteAsync(null);
        (await Opened(dialogs, asking)).SendCommand.Execute(null);
        await asking;

        Assert.Null(page.Toast.Text);
        Assert.Contains("nicht möglich", page.ClosureEditor.Message, StringComparison.Ordinal);
        Assert.Equal(AispaStudio.Desktop.Controls.InfoBarSeverity.Warning, page.ClosureEditor.MessageSeverity);
    }

    [Fact]
    public async Task Given_eine_Absage_ohne_Recht_zu_buchen_Then_fehlt_der_Knopf_und_ein_Hinweis_steht_da()
    {
        var user = TestData.Manager() with { Rights = [AccessRight.ViewCalendar, AccessRight.ManageLocation] };
        var closures = (await OpenFirst(Api(Christmas with { OpenAppointments = 1 }), user)).ClosureEditor;

        Assert.False(closures.CanBook);
        Assert.False(closures.CancelAppointmentsCommand.CanExecute(null));
        Assert.Equal("Absagen darf, wer buchen darf.", closures.CancelHint);
    }

    [Fact]
    public async Task Given_eine_wartende_Nachricht_When_Jetzt_senden_Then_POST_send_now_und_der_Zustand_ist_aktualisiert()
    {
        var waiting = Notice(ClosureNoticeState.Queued) with { ScheduledFor = Now.GetUtcNow().AddMinutes(10) };
        var api = Api(Christmas with { CancelledAppointments = 1 })
            .On($"GET {Route("/" + ClosureId + "/notices")}", new List<ClosureNoticeDto> { waiting })
            .On($"POST {Route("/" + ClosureId + "/notices/" + waiting.Id + "/send-now")}", waiting with { State = ClosureNoticeState.Sent });
        var closures = (await OpenFirst(api)).ClosureEditor;

        var row = Assert.Single(closures.Notices);
        Assert.True(row.IsWaiting);
        Assert.Equal("Nachricht an den Gast geht um 10:10 raus.", row.WaitingText);

        await row.SendNowCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"POST {Route("/" + ClosureId + "/notices/" + waiting.Id + "/send-now")}"));
        Assert.False(closures.Notices[0].IsWaiting);
        Assert.Equal("gesendet", closures.Notices[0].StateText);
    }

    [Fact]
    public async Task Given_eine_wartende_Nachricht_When_Nicht_senden_und_schon_gesendet_Then_steht_der_Hinweis_im_Formular()
    {
        var waiting = Notice(ClosureNoticeState.Queued) with { ScheduledFor = Now.GetUtcNow().AddMinutes(10) };
        var api = Api(Christmas with { CancelledAppointments = 1 })
            .On($"GET {Route("/" + ClosureId + "/notices")}", new List<ClosureNoticeDto> { waiting })
            .On($"POST {Route("/" + ClosureId + "/notices/" + waiting.Id + "/cancel")}", null, System.Net.HttpStatusCode.Conflict);
        var closures = (await OpenFirst(api)).ClosureEditor;

        await closures.Notices[0].CancelCommand.ExecuteAsync(null);

        Assert.Equal("Die Nachricht ist schon gesendet oder zurückgenommen.", closures.Message);
        Assert.Equal(ClosureNoticeState.Queued, closures.Notices[0].State);
    }

    [Fact]
    public async Task Given_eine_Schliesszeit_When_Loeschen_bestaetigt_wird_Then_Rueckfrage_DELETE_Toast_und_die_Leiste_schliesst()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        var api = Api(Christmas);
        var page = await OpenFirst(api, dialogs: dialogs);
        var closures = page.ClosureEditor;

        await closures.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, CallsTo(api, $"DELETE {Route("/" + ClosureId)}"));
        Assert.True(page.IsPanelOpen);

        await closures.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"DELETE {Route("/" + ClosureId)}"));
        var asked = dialogs.AssertRules().Asked[0];
        Assert.Equal("Schließzeit löschen?", asked.Title);
        Assert.Equal("Die Schließzeit „Betriebsurlaub, 24.12.2027 bis 26.12.2027“ ist danach weg. Termine werden nicht angefasst; ab dann gibt es in der Zeit wieder freie Zeiten.", asked.Message);
        Assert.Equal("Löschen", asked.ConfirmText);
        // Statt des Balkens „Gelöscht.“ ein Toast (meldungen.md M1); die Seitenleiste ist zu, die Zeile weg.
        Assert.Equal("Schließzeit gelöscht", page.Toast.Text);
        Assert.Null(closures.Message);
        Assert.False(page.IsPanelOpen);
        Assert.Empty(page.Upcoming);
        Assert.True(page.ShowEmpty);
    }

    [Fact]
    public async Task Given_ungespeicherte_Schliesszeit_When_eine_andere_gewaehlt_wird_Then_fragt_die_Seite_Aenderungen_speichern()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var second = Closure(Guid.NewGuid(), new(2028, 2, 1), new(2028, 2, 3));
        var page = await OpenFirst(Api(Christmas, second), dialogs: dialogs);
        var closures = page.ClosureEditor;
        var first = page.SelectedUpcoming;
        closures.InternalReason = "geändert";

        // Abbrechen: bleibt bei der Schließzeit, die Eingabe bleibt.
        page.SelectedUpcoming = page.Upcoming[1];
        await Task.Yield();
        await Task.Yield();
        Assert.Same(first, page.SelectedUpcoming);
        Assert.Equal("geändert", closures.InternalReason);

        // Verwerfen: wechselt, die Schließzeit steht wieder auf dem gespeicherten Stand.
        page.SelectedUpcoming = page.Upcoming[1];
        await Task.Yield();
        await Task.Yield();
        Assert.Equal("01.02.2028", closures.Period.FromDate);
        Assert.Equal(["Änderungen speichern?", "Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
        Assert.Contains("Schließzeiten", dialogs.Asked[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ungespeicherte_Aenderungen_When_die_Leiste_geschlossen_wird_Then_fragt_die_Seite_und_oeffnet_sie_bei_Abbrechen_wieder()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var page = await OpenFirst(Api(Christmas), dialogs: dialogs);
        page.ClosureEditor.InternalReason = "geändert";

        page.IsPanelOpen = false;
        await Task.Yield();
        await Task.Yield();
        Assert.True(page.IsPanelOpen);
        Assert.Equal("geändert", page.ClosureEditor.InternalReason);

        page.IsPanelOpen = false;
        await Task.Yield();
        await Task.Yield();
        Assert.False(page.IsPanelOpen);
        Assert.False(page.HasUnsavedChanges);
        Assert.Null(page.SelectedUpcoming);
        Assert.Equal(2, dialogs.Asked.Count);
    }

    [Fact]
    public async Task Given_ein_offenes_neues_Formular_When_zwischen_Standort_und_Raum_gewechselt_wird_Then_oeffnet_das_andere_Formular()
    {
        var page = await Open(Api());
        await page.NewCommand.ExecuteAsync(null);
        Assert.Same(page.ClosureEditor, page.Editor);
        Assert.True(page.NewIsLocation);

        page.NewIsRoom = true;
        await Task.Yield();
        await Task.Yield();

        // Die Testdaten haben Räume: das Formular „Einzelner Raum“ gilt, das der Schließzeit ist leer.
        Assert.Same(page.RoomEditor, page.Editor);
        Assert.True(page.RoomEditor.IsCreating);
        Assert.False(page.ClosureEditor.HasForm);
        Assert.Equal("Neue Raumsperre", page.PanelTitle);
    }

    [Fact]
    public async Task Given_ein_Sprung_mit_Zeitraum_When_die_Seite_offen_ist_Then_neues_Formular_mit_Von_und_Bis()
    {
        var page = await Open(Api(Christmas));

        page.Open(new AdminTarget(Location: TestData.LocationId, From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await Task.Yield();
        await Task.Yield();

        var closures = page.ClosureEditor;
        Assert.True(closures.IsCreating);
        Assert.Equal(("02.08.2027", "06.08.2027"), (closures.Period.FromDate, closures.Period.UntilDate));
        Assert.Equal(TestData.LocationId, page.SelectedLocation?.Id);
        Assert.True(page.IsPanelOpen);
    }

    [Fact]
    public async Task Given_ein_Sprung_mit_Zeitraum_When_Verwerfen_gewaehlt_wird_Then_bleibt_der_vorbelegte_Zeitraum_stehen()
    {
        var page = await Open(Api(Christmas));
        page.Open(new AdminTarget(Location: TestData.LocationId, From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await Task.Yield();
        await Task.Yield();
        var closures = page.ClosureEditor;
        closures.Period.FromDate = "10.08.2027";

        closures.DiscardCommand.Execute(null);

        Assert.True(closures.IsCreating);
        Assert.Equal(("02.08.2027", "06.08.2027"), (closures.Period.FromDate, closures.Period.UntilDate));
    }

    [Fact]
    public async Task Given_ein_Sprung_vor_dem_Laden_When_die_Seite_laedt_Then_gilt_der_Zeitraum_danach()
    {
        var page = new ClosuresPageViewModel(Api(Christmas).Client(), TestData.Owner(), Now);

        page.Open(new AdminTarget(From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await page.EnsureLoadedAsync();

        Assert.True(page.ClosureEditor.IsCreating);
        Assert.Equal("02.08.2027", page.ClosureEditor.Period.FromDate);
    }

    [Fact]
    public async Task Given_die_Wartezeit_When_sie_in_der_Einrichtung_geaendert_und_gespeichert_wird_Then_PUT_guest_message_delay_und_Toast()
    {
        var api = Api().OnJson($"PUT /api/locations/{TestData.LocationId}/guest-message-delay", body => JsonSerializer.Deserialize<GuestMessageDelayDto>(body!, ApiClient.Json)!);
        var page = await Open(api);
        var setup = page.Setup;
        Assert.Equal(10m, setup.GuestDelayMinutes);
        Assert.True(setup.HasGuestDelay);
        Assert.False(setup.SaveGuestDelayCommand.CanExecute(null));

        await page.OpenSetupCommand.ExecuteAsync(null);
        Assert.True(page.IsSetupOpen);
        Assert.False(page.IsPanelOpen);
        setup.GuestDelayMinutes = 15m;
        await setup.SaveGuestDelayCommand.ExecuteAsync(null);

        Assert.Equal(new GuestMessageDelayDto(15), Sent<GuestMessageDelayDto>(api, $"PUT /api/locations/{TestData.LocationId}/guest-message-delay"));
        Assert.Equal("Wartezeit gespeichert", page.Toast.Text);
        Assert.False(setup.SaveGuestDelayCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_eine_Wartezeit_ueber_60_When_gespeichert_wird_Then_Fehler_und_kein_Aufruf()
    {
        var api = Api();
        var page = await Open(api);

        page.Setup.GuestDelayMinutes = 61m;
        await page.Setup.SaveGuestDelayCommand.ExecuteAsync(null);

        Assert.Equal("Bitte eine ganze Zahl von 0 bis 60 eingeben.", page.Setup.GuestDelayError);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_ein_offenes_Formular_mit_Aenderungen_When_Einrichtung_gewaehlt_wird_Then_fragt_die_Seite_zuerst()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var page = await OpenFirst(Api(Christmas), dialogs: dialogs);
        page.ClosureEditor.InternalReason = "geändert";

        await page.OpenSetupCommand.ExecuteAsync(null);

        Assert.False(page.IsSetupOpen);
        Assert.True(page.IsPanelOpen);
        Assert.Equal("Änderungen speichern?", Assert.Single(dialogs.Asked).Title);
    }

    [Fact]
    public async Task Given_ein_Fehler_der_API_When_gespeichert_wird_Then_steht_er_ueber_der_Leiste()
    {
        var api = Api().On($"POST {Route()}", new { detail = "Zwei Schließzeiten desselben Standorts dürfen sich nicht überschneiden." }, System.Net.HttpStatusCode.BadRequest);
        var page = await Open(api);
        var closures = page.ClosureEditor;
        await page.NewCommand.ExecuteAsync(null);

        Assert.False(await closures.SaveChangesAsync());

        Assert.Equal("Zwei Schließzeiten desselben Standorts dürfen sich nicht überschneiden.", closures.Error);
        Assert.Null(page.Toast.Text);
    }

    [Fact]
    public async Task Given_ein_uebergebener_Toast_When_gespeichert_wird_Then_zeigt_die_Seite_ihn_dort()
    {
        var shared = new ToastState(Now);
        var page = new ClosuresPageViewModel(Api(Christmas).Client(), TestData.Owner(), Now, shared);
        await page.EnsureLoadedAsync();
        await page.OpenRowAsync(page.Upcoming[0]);
        page.ClosureEditor.InternalReason = "neu";
        await page.ClosureEditor.SaveChangesAsync();

        Assert.Same(shared, page.Toast);
        Assert.Equal("Schließzeit gespeichert", shared.Text);
    }

    [Fact]
    public async Task Given_ein_Toast_When_er_geschlossen_wird_Then_ist_er_weg_und_ein_neuer_ersetzt_den_alten()
    {
        var page = await OpenFirst(Api(Christmas));
        page.ClosureEditor.InternalReason = "neu";
        await page.ClosureEditor.SaveChangesAsync();
        Assert.Equal("Schließzeit gespeichert", page.Toast.Text);

        page.Toast.Hold(true);
        page.Toast.Hold(false);
        page.ClosureEditor.InternalReason = "noch neuer";
        await page.ClosureEditor.SaveChangesAsync();
        Assert.Equal("Schließzeit gespeichert", page.Toast.Text);
        Assert.True(page.Toast.IsShown);

        page.Toast.CloseCommand.Execute(null);
        Assert.Null(page.Toast.Text);
        Assert.False(page.Toast.IsShown);
    }

    [Fact]
    public void Given_Rezeptionistin_Then_hat_die_Verwaltung_keine_Seite_Schliesszeiten()
    {
        var page = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask).AdminPage;

        Assert.Null(page.ClosuresPage);
        Assert.DoesNotContain(page.Areas, a => a.Title == "Schließzeiten");
    }

    [Fact]
    public void Given_der_Kalender_When_der_Chip_Geschlossen_gewaehlt_wird_Then_ist_der_Bereich_Schliesszeiten_offen()
    {
        var shell = new ShellViewModel(Api(Christmas).Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        shell.AdminPage.Show("masterData", "closures");

        Assert.Equal("Schließzeiten", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.ClosuresPage, shell.AdminPage.Content);
    }

    private static ClosureNoticeDto Notice(ClosureNoticeState state) =>
        new(Guid.NewGuid(), ClosureId, Guid.NewGuid(), "Ida Muster", "Massage", TestData.At(10), ClosureChannel.Email, state, null, "Absage", "Text", false, false, "Katrin", TestData.At(8), null);

    private static async Task<CancelClosureDialogViewModel> Opened(FakeDialogs dialogs, Task asking)
    {
        for (var i = 0; i < 300; i++)
        {
            if (dialogs.Open is CancelClosureDialogViewModel dialog)
            {
                return dialog;
            }

            if (asking.IsFaulted)
            {
                await asking;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new Xunit.Sdk.XunitException("Die Rückfrage ging nicht auf.");
    }
}
