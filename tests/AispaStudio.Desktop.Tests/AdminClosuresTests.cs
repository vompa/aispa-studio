using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 95 (Ä206): native Maske „Schließzeiten“ nach ClosuresPage.tsx: Liste mit Reitern, Formular, Löschen, Termine absagen, Protokoll und Wartezeit.</summary>
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
        // Die Liste verändert sich mit POST und PUT wie die der API.
        var list = closures.ToList();
        return new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET {Route()}", list)
            .On($"GET /api/locations/{TestData.LocationId}/guest-message-delay", new GuestMessageDelayDto(10))
            .OnJson($"POST {Route()}", body =>
            {
                var created = FromInput(Guid.NewGuid(), body);
                list.Add(created);
                return created;
            })
            .OnJson($"PUT {Route("/" + ClosureId)}", body =>
            {
                var changed = FromInput(ClosureId, body);
                list[list.FindIndex(c => c.Id == ClosureId)] = changed;
                return changed;
            })
            .On($"DELETE {Route("/" + ClosureId)}", null, System.Net.HttpStatusCode.NoContent);
    }

    private static ClosureDto FromInput(Guid id, string? body)
    {
        var input = JsonSerializer.Deserialize<ClosureInput>(body!, ApiClient.Json)!;
        return Closure(id, input.FromDate, input.UntilDate, input.Kind, reason: input.InternalReason, guest: input.GuestText) with { FromTime = input.FromTime, UntilTime = input.UntilTime };
    }

    private static async Task<ClosuresViewModel> Open(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var closures = new ClosuresViewModel(api.Client(), user ?? TestData.Owner(), Now) { Dialogs = dialogs };
        await closures.EnsureLoadedAsync();
        return closures;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    private static int CallsTo(StubApi api, string route) => api.Calls.Count(c => c.Route == route);

    [Fact]
    public async Task Given_eine_Schliesszeit_24_bis_26_12_When_sie_angelegt_wird_Then_POST_mit_den_Tagen_und_der_Art()
    {
        var api = Api();
        var closures = await Open(api);

        await closures.NewCommand.ExecuteAsync(null);
        closures.Period.FromDate = "24.12.2027";
        closures.Period.UntilDate = "26.12.2027";
        closures.Kind = ClosureKind.SpecialClosure;
        closures.GuestText = "Frohe Weihnachten";
        Assert.True(await closures.SaveChangesAsync());

        Assert.Equal(new ClosureInput(new(2027, 12, 24), null, new(2027, 12, 26), null, ClosureKind.SpecialClosure, null, "Frohe Weihnachten"), Sent<ClosureInput>(api, $"POST {Route()}"));
        Assert.Equal("Gespeichert.", closures.StatusText);
        Assert.False(closures.HasUnsavedChanges);
        var row = Assert.Single(closures.Rows);
        Assert.Equal(("Sonderschließung", "24.12.2027 bis 26.12.2027"), (row.Title, row.Period));
        Assert.Same(row, closures.SelectedRow);
    }

    [Fact]
    public async Task Given_ein_neues_Formular_Then_Von_und_Bis_sind_heute_und_die_Art_ist_Betriebsurlaub()
    {
        var closures = await Open(Api());

        await closures.NewCommand.ExecuteAsync(null);

        Assert.True(closures.IsCreating);
        Assert.Equal(("01.03.2027", "01.03.2027", ClosureKind.Vacation, false), (closures.Period.FromDate, closures.Period.UntilDate, closures.Kind, closures.Period.IsPartial));
        Assert.False(closures.HasUnsavedChanges);
        Assert.Equal("Neue Schließzeit", closures.HeadingText);
    }

    [Fact]
    public async Task Given_bis_vor_von_When_gespeichert_wird_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = Api();
        var closures = await Open(api);
        await closures.NewCommand.ExecuteAsync(null);

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
        var closures = await Open(api);
        await closures.NewCommand.ExecuteAsync(null);

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
        var api = Api();
        var closures = await Open(api);
        await closures.NewCommand.ExecuteAsync(null);

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
        var closures = await Open(api);
        await closures.NewCommand.ExecuteAsync(null);
        closures.Period.FromDate = "24.12.2027";
        closures.Period.UntilDate = "24.12.2027";
        closures.Period.FromTime = "13:00";
        closures.Period.IsPartial = true;
        closures.InternalReason = "  Wasserschaden  ";

        Assert.True(await closures.SaveChangesAsync());
        Assert.Equal(new ClosureInput(new(2027, 12, 24), new(13, 0), new(2027, 12, 24), null, ClosureKind.Vacation, "Wasserschaden", null), Sent<ClosureInput>(api, $"POST {Route()}"));

        // Haken weg: Uhrzeiten bleiben im Formular stehen, gehen aber nicht mit.
        await closures.NewCommand.ExecuteAsync(null);
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
        var closures = await Open(Api());
        await closures.NewCommand.ExecuteAsync(null);

        closures.GuestText = new string('a', 201);
        closures.InternalReason = new string('b', 201);

        Assert.False(await closures.SaveChangesAsync());
        Assert.Equal(("Höchstens 200 Zeichen.", "Höchstens 200 Zeichen."), (closures.GuestTextError, closures.InternalReasonError));
    }

    [Fact]
    public async Task Given_die_Liste_When_geladen_Then_Reiter_Aktuell_und_Beendet_zaehlen_und_waehlen_die_Zeilen()
    {
        var closures = await Open(Api(Christmas, Past));

        Assert.Equal(("Aktuell (1)", "Beendet (1)"), (closures.CurrentTabLabel, closures.EndedTabLabel));
        Assert.Equal(["Betriebsurlaub"], closures.Rows.Select(r => r.Title));
        Assert.Equal("Betriebsurlaub, 24.12.2027 bis 26.12.2027", closures.HeadingText);
        Assert.Equal("Geplant", closures.PhaseText);

        await closures.ShowEndedCommand.ExecuteAsync(null);

        Assert.Equal(["Ausfall"], closures.Rows.Select(r => r.Title));
        Assert.Equal("Beendet", closures.PhaseText);
        Assert.Equal(("Ausfall", "Wasserschaden"), (closures.SelectedRow?.Title, closures.SelectedRow?.Detail));
    }

    [Fact]
    public async Task Given_keine_Schliesszeit_Then_die_Liste_sagt_es_und_das_Formular_fehlt()
    {
        var closures = await Open(Api());

        Assert.Empty(closures.Rows);
        Assert.False(closures.HasForm);
        Assert.Equal("Es ist keine Schließzeit eingetragen.", closures.EmptyText);
    }

    [Fact]
    public async Task Given_eine_Schliesszeit_mit_abgesagten_Terminen_When_die_Maske_sie_zeigt_Then_fehlt_Loeschen_und_das_Protokoll_ist_da()
    {
        var notice = Notice(ClosureNoticeState.Sent);
        var api = Api(Christmas with { CancelledAppointments = 1 }).On($"GET {Route("/" + ClosureId + "/notices")}", new List<ClosureNoticeDto> { notice });

        var closures = await Open(api);

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
        var closures = await Open(Api(Christmas));

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
        var closures = await Open(api, dialogs: dialogs);
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
    public async Task Given_eine_Absage_When_sie_durch_ist_Then_steht_die_Rueckmeldung_in_der_Maske_und_die_Liste_ist_neu_geladen()
    {
        var rows = new[] { ClosureTests.Row("Ida Muster", 10, ClosureChannel.Email) };
        var open = Christmas with { OpenAppointments = 1 };
        var dialogs = new FakeDialogs();
        var api = Api(open)
            .On($"GET {Route("/" + ClosureId + "/cancellation")}", ClosureTests.Preview(true, rows) with { Closure = open })
            .On($"POST {Route("/" + ClosureId + "/cancellation")}", new CancelClosureAppointmentsResult(1, 1, 0, 0, []));
        var closures = await Open(api, dialogs: dialogs);

        var asking = closures.CancelAppointmentsCommand.ExecuteAsync(null);
        (await Opened(dialogs, asking)).SendCommand.Execute(null);
        await asking;

        Assert.Equal("1 Termin abgesagt, 1 Nachricht", closures.Message);
        Assert.Equal(2, CallsTo(api, $"GET {Route()}"));
    }

    [Fact]
    public async Task Given_eine_Absage_ohne_Recht_zu_buchen_Then_fehlt_der_Knopf_und_ein_Hinweis_steht_da()
    {
        var user = TestData.Manager() with { Rights = [AccessRight.ViewCalendar, AccessRight.ManageLocation] };
        var closures = await Open(Api(Christmas with { OpenAppointments = 1 }), user);

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
        var closures = await Open(api);

        var row = Assert.Single(closures.Notices);
        Assert.True(row.IsWaiting);
        Assert.Equal("Nachricht an den Gast geht um 10:10 raus.", row.WaitingText);

        await row.SendNowCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"POST {Route("/" + ClosureId + "/notices/" + waiting.Id + "/send-now")}"));
        Assert.False(closures.Notices[0].IsWaiting);
        Assert.Equal("gesendet", closures.Notices[0].StateText);
    }

    [Fact]
    public async Task Given_eine_wartende_Nachricht_When_Nicht_senden_und_schon_gesendet_Then_steht_der_Hinweis_in_der_Maske()
    {
        var waiting = Notice(ClosureNoticeState.Queued) with { ScheduledFor = Now.GetUtcNow().AddMinutes(10) };
        var api = Api(Christmas with { CancelledAppointments = 1 })
            .On($"GET {Route("/" + ClosureId + "/notices")}", new List<ClosureNoticeDto> { waiting })
            .On($"POST {Route("/" + ClosureId + "/notices/" + waiting.Id + "/cancel")}", null, System.Net.HttpStatusCode.Conflict);
        var closures = await Open(api);

        await closures.Notices[0].CancelCommand.ExecuteAsync(null);

        Assert.Equal("Die Nachricht ist schon gesendet oder zurückgenommen.", closures.Message);
        Assert.Equal(ClosureNoticeState.Queued, closures.Notices[0].State);
    }

    [Fact]
    public async Task Given_eine_Schliesszeit_When_Loeschen_bestaetigt_wird_Then_Rueckfrage_und_DELETE()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        var api = Api(Christmas);
        var closures = await Open(api, dialogs: dialogs);

        await closures.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, CallsTo(api, $"DELETE {Route("/" + ClosureId)}"));

        await closures.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"DELETE {Route("/" + ClosureId)}"));
        var asked = dialogs.AssertRules().Asked[0];
        Assert.Equal("Schließzeit löschen?", asked.Title);
        Assert.Equal("Die Schließzeit „Betriebsurlaub, 24.12.2027 bis 26.12.2027“ ist danach weg. Termine werden nicht angefasst; ab dann gibt es in der Zeit wieder freie Zeiten.", asked.Message);
        Assert.Equal("Löschen", asked.ConfirmText);
        Assert.Equal("Gelöscht.", closures.Message);
    }

    [Fact]
    public async Task Given_ungespeicherte_Schliesszeit_When_eine_andere_gewaehlt_wird_Then_fragt_die_Maske_Aenderungen_speichern()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var second = Closure(Guid.NewGuid(), new(2028, 2, 1), new(2028, 2, 3));
        var closures = await Open(Api(Christmas, second), dialogs: dialogs);
        var first = closures.SelectedRow;
        closures.InternalReason = "geändert";

        // Abbrechen: bleibt bei der Schließzeit, die Eingabe bleibt.
        closures.SelectedRow = closures.Rows[1];
        await Task.Yield();
        Assert.Same(first, closures.SelectedRow);
        Assert.Equal("geändert", closures.InternalReason);

        // Verwerfen: wechselt, die Schließzeit steht wieder auf dem gespeicherten Stand.
        closures.SelectedRow = closures.Rows[1];
        await Task.Yield();
        Assert.Equal("01.02.2028", closures.Period.FromDate);
        Assert.Equal(["Änderungen speichern?", "Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
        Assert.Contains("Schließzeiten", dialogs.Asked[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_Sprung_mit_Zeitraum_When_die_Maske_offen_ist_Then_neues_Formular_mit_Von_und_Bis()
    {
        var closures = await Open(Api(Christmas));

        closures.Open(new AdminTarget(Location: TestData.LocationId, From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await Task.Yield();

        Assert.True(closures.IsCreating);
        Assert.Equal(("02.08.2027", "06.08.2027"), (closures.Period.FromDate, closures.Period.UntilDate));
        Assert.Equal(TestData.LocationId, closures.SelectedLocation?.Id);
    }

    [Fact]
    public async Task Given_ein_Sprung_mit_Zeitraum_When_Verwerfen_gewaehlt_wird_Then_bleibt_der_vorbelegte_Zeitraum_stehen()
    {
        var closures = await Open(Api(Christmas));
        closures.Open(new AdminTarget(Location: TestData.LocationId, From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await Task.Yield();
        closures.Period.FromDate = "10.08.2027";

        closures.DiscardCommand.Execute(null);

        Assert.True(closures.IsCreating);
        Assert.Equal(("02.08.2027", "06.08.2027"), (closures.Period.FromDate, closures.Period.UntilDate));
    }

    [Fact]
    public async Task Given_ein_Sprung_vor_dem_Laden_When_die_Maske_laedt_Then_gilt_der_Zeitraum_danach()
    {
        var closures = new ClosuresViewModel(Api(Christmas).Client(), TestData.Owner(), Now);

        closures.Open(new AdminTarget(From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await closures.EnsureLoadedAsync();

        Assert.True(closures.IsCreating);
        Assert.Equal("02.08.2027", closures.Period.FromDate);
    }

    [Fact]
    public async Task Given_die_Wartezeit_When_sie_geaendert_und_gespeichert_wird_Then_PUT_guest_message_delay()
    {
        var api = Api().OnJson($"PUT /api/locations/{TestData.LocationId}/guest-message-delay", body => JsonSerializer.Deserialize<GuestMessageDelayDto>(body!, ApiClient.Json)!);
        var closures = await Open(api);
        Assert.Equal(10m, closures.GuestDelayMinutes);
        Assert.False(closures.SaveGuestDelayCommand.CanExecute(null));

        closures.GuestDelayMinutes = 15m;
        await closures.SaveGuestDelayCommand.ExecuteAsync(null);

        Assert.Equal(new GuestMessageDelayDto(15), Sent<GuestMessageDelayDto>(api, $"PUT /api/locations/{TestData.LocationId}/guest-message-delay"));
        Assert.Equal("Gespeichert.", closures.GuestDelayStatus);
        Assert.False(closures.SaveGuestDelayCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_eine_Wartezeit_ueber_60_When_gespeichert_wird_Then_Fehler_und_kein_Aufruf()
    {
        var api = Api();
        var closures = await Open(api);

        closures.GuestDelayMinutes = 61m;
        await closures.SaveGuestDelayCommand.ExecuteAsync(null);

        Assert.Equal("Bitte eine ganze Zahl von 0 bis 60 eingeben.", closures.GuestDelayError);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_ein_Fehler_der_API_When_gespeichert_wird_Then_steht_er_ueber_der_Leiste()
    {
        var api = Api().On($"POST {Route()}", new { detail = "Zwei Schließzeiten desselben Standorts dürfen sich nicht überschneiden." }, System.Net.HttpStatusCode.BadRequest);
        var closures = await Open(api);
        await closures.NewCommand.ExecuteAsync(null);

        Assert.False(await closures.SaveChangesAsync());

        Assert.Equal("Zwei Schließzeiten desselben Standorts dürfen sich nicht überschneiden.", closures.Error);
    }

    [Fact]
    public void Given_Rezeptionistin_Then_hat_die_Verwaltung_keine_Maske_Schliesszeiten()
    {
        var page = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask).AdminPage;

        Assert.Null(page.Closures);
        Assert.DoesNotContain(page.Areas, a => a.Title == "Schließzeiten");
    }

    [Fact]
    public void Given_der_Kalender_When_der_Chip_Geschlossen_gewaehlt_wird_Then_ist_der_Bereich_Schliesszeiten_offen()
    {
        var shell = new ShellViewModel(Api(Christmas).Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        shell.AdminPage.Show("masterData", "closures");

        Assert.Equal("Schließzeiten", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(shell.AdminPage.Closures, shell.AdminPage.Content);
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
