using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// R05 Slice 95 (Ä207), seit ADR 0098 in der Liste der Seite „Schließzeiten“: das Formular der Raumsperre nach RoomClosuresPage.tsx (Raum, Zeitraum, Art und Grund), die Art der Zeile in der gemeinsamen
/// Liste, die Wahl „Ganzer Standort“ oder „Einzelner Raum“, Löschen mit Rückfrage und Sprungziele. Die Schließzeit steht in <see cref="AdminClosuresTests"/>.
/// </summary>
public sealed class AdminRoomClosuresTests
{
    private const string FormError = "Die Raumsperre konnte nicht gespeichert werden. Bitte die markierten Felder prüfen.";

    private static readonly Guid SecondId = Guid.NewGuid();
    private static readonly LocationDto Salzburg = new(SecondId, "Salzburg", "Europe/Vienna", "EUR", [], null, LocationContact.Empty);
    private static readonly RoomDto SaltRoom = new(Guid.NewGuid(), SecondId, "Raum Sole", 10);
    private static readonly Guid ClosureId = Guid.NewGuid();
    private static readonly ManualTime Now = new(TestData.At(10));

    private static string Route(string tail = "") => $"/api/locations/{TestData.LocationId}/room-closures{tail}";

    private static DateTimeOffset Midnight(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static RoomClosureDto Closure(Guid id, RoomDto room, DateOnly from, DateOnly until, RoomClosureKind kind = RoomClosureKind.Maintenance,
        ClosurePhase phase = ClosurePhase.Planned, int open = 0, string? reason = null) =>
        new(id, room.LocationId, room.Id, room.Name, from, null, until, null, Midnight(from), Midnight(until).AddDays(1), kind, reason, phase, "Katrin", TestData.At(8), open);

    private static readonly RoomClosureDto Maintenance = Closure(ClosureId, TestData.Room, new(2027, 12, 24), new(2027, 12, 26), reason: "Saunaofen");

    private static StubApi Api(params RoomClosureDto[] closures)
    {
        // Die Liste verändert sich mit POST und PUT wie die der API; die Übersicht und der Einzelabruf lesen sie.
        var list = closures.ToList();
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location, Salzburg })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{SecondId}/resources", new LocationResources(Salzburg, [SaltRoom], [], [], [], []))
            .OnOverview(TestData.LocationId, () => list.Select(ClosuresTestKit.ToItem))
            .OnOverview(SecondId, () => [])
            .On($"GET /api/locations/{TestData.LocationId}/guest-message-delay", new GuestMessageDelayDto(10))
            .On($"GET /api/locations/{SecondId}/guest-message-delay", new GuestMessageDelayDto(0))
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

    private static RoomClosureDto FromInput(Guid id, string? body)
    {
        var input = JsonSerializer.Deserialize<RoomClosureInput>(body!, ApiClient.Json)!;
        return Closure(id, TestData.Room, input.FromDate, input.UntilDate, input.Kind, reason: input.Reason) with { FromTime = input.FromTime, UntilTime = input.UntilTime };
    }

    private static async Task<ClosuresPageViewModel> Open(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var page = new ClosuresPageViewModel(api.Client(), user ?? TestData.Owner(), Now) { Dialogs = dialogs };
        await page.EnsureLoadedAsync();
        return page;
    }

    /// <summary>Die Seite mit dem Formular der ersten kommenden Raumsperre offen.</summary>
    private static async Task<ClosuresPageViewModel> OpenFirst(StubApi api, FakeDialogs? dialogs = null)
    {
        var page = await Open(api, dialogs: dialogs);
        await page.OpenRowAsync(page.Upcoming[0]);
        return page;
    }

    /// <summary>Die Seite mit einem neuen Formular „Einzelner Raum“.</summary>
    private static async Task<ClosuresPageViewModel> OpenNewRoom(StubApi api, FakeDialogs? dialogs = null)
    {
        var page = await Open(api, dialogs: dialogs);
        await page.NewRoomCommand.ExecuteAsync(null);
        return page;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    private static int CallsTo(StubApi api, string route) => api.Calls.Count(c => c.Route == route);

    [Fact]
    public async Task Given_ein_Raum_When_eine_Raumsperre_angelegt_wird_Then_POST_mit_Raum_Zeitraum_und_Grund()
    {
        var api = Api();
        var page = await OpenNewRoom(api);
        var closures = page.RoomEditor;

        Assert.Same(closures, page.Editor);
        Assert.True(page.NewIsRoom);
        Assert.Equal(TestData.Room.Id, closures.SelectedRoom?.Id);
        closures.Period.FromDate = "24.12.2027";
        closures.Period.UntilDate = "26.12.2027";
        closures.Kind = RoomClosureKind.Damage;
        closures.Reason = "Wasserschaden, Trocknung";
        Assert.True(await closures.SaveChangesAsync());

        Assert.Equal(new RoomClosureInput(TestData.Room.Id, new(2027, 12, 24), null, new(2027, 12, 26), null, RoomClosureKind.Damage, "Wasserschaden, Trocknung"),
            Sent<RoomClosureInput>(api, $"POST {Route()}"));
        var row = Assert.Single(page.Upcoming);
        Assert.Equal(("Raum", "Raum Zeder, Schaden", "24.12.2027 bis 26.12.2027", "Wasserschaden, Trocknung"), (row.TypeText, row.Title, row.Period, row.Reason));
        Assert.Equal("Raumsperre gespeichert", page.Toast.Text);
    }

    [Fact]
    public async Task Given_ein_Standort_ohne_Raum_Then_gibt_es_nicht_die_Wahl_Einzelner_Raum()
    {
        var api = Api().On($"GET /api/locations/{TestData.LocationId}/resources", new LocationResources(TestData.Location, [], [], [], [], []));
        var page = await Open(api);

        Assert.False(page.CanSelectRoomKind);

        // „Raum sperren“ im Leerzustand öffnet dann die Schließzeit des Standorts statt eines Formulars ohne Raum.
        await page.NewRoomCommand.ExecuteAsync(null);
        Assert.Same(page.ClosureEditor, page.Editor);
        Assert.False(page.NewIsRoom);
    }

    [Fact]
    public async Task Given_bis_vor_von_When_gespeichert_wird_Then_Fehler_am_Feld_und_kein_Aufruf()
    {
        var api = Api();
        var closures = (await OpenNewRoom(api)).RoomEditor;

        closures.Period.FromDate = "26.12.2027";
        closures.Period.UntilDate = "24.12.2027";

        Assert.False(await closures.SaveChangesAsync());
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST ", StringComparison.Ordinal));
        Assert.Equal("Das Ende darf nicht vor dem Beginn liegen.", closures.Period.UntilDateError);
        Assert.Equal(FormError, closures.Error);
        Assert.Equal("UntilDateField", closures.FocusTarget);
    }

    [Fact]
    public async Task Given_ein_leeres_Datum_When_gespeichert_wird_Then_steht_der_Fehler_mit_dem_Wort_gesperrt_am_Feld()
    {
        var closures = (await OpenNewRoom(Api())).RoomEditor;

        closures.Period.FromDate = "";
        closures.Period.UntilDate = "";

        Assert.False(await closures.SaveChangesAsync());
        Assert.Equal(("Bitte den ersten gesperrten Tag wählen.", "Bitte den letzten gesperrten Tag wählen."), (closures.Period.FromDateError, closures.Period.UntilDateError));
    }

    [Fact]
    public async Task Given_ein_zu_langer_Grund_When_gespeichert_wird_Then_Fehler_am_Feld()
    {
        var closures = (await OpenNewRoom(Api())).RoomEditor;

        closures.Reason = new string('a', 201);

        Assert.False(await closures.SaveChangesAsync());
        Assert.Equal("Höchstens 200 Zeichen.", closures.ReasonError);
        Assert.Equal("ReasonField", closures.FocusTarget);
    }

    [Fact]
    public async Task Given_eine_Raumsperre_Then_ist_der_Raum_festgelegt_und_offene_Termine_stehen_als_Hinweis_da()
    {
        var page = await OpenFirst(Api(Maintenance with { OpenAppointments = 1 }));
        var closures = page.RoomEditor;

        Assert.False(closures.CanSelectRoom);
        Assert.Equal("Raum Zeder, Wartung, 24.12.2027 bis 26.12.2027", closures.HeadingText);
        Assert.Equal("1 Termin im gesperrten Raum bleibt gebucht. Umbuchen unter Meldungen.", closures.OpenNotice);
        Assert.Equal("1 Termin offen", page.Upcoming[0].AppointmentsText);

        await page.NewRoomCommand.ExecuteAsync(null);
        Assert.True(closures.CanSelectRoom);
    }

    [Fact]
    public async Task Given_eine_Raumsperre_When_Loeschen_bestaetigt_wird_Then_Rueckfrage_DELETE_und_Toast()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        var api = Api(Maintenance);
        var page = await OpenFirst(api, dialogs);
        var closures = page.RoomEditor;

        await closures.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, CallsTo(api, $"DELETE {Route("/" + ClosureId)}"));

        await closures.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"DELETE {Route("/" + ClosureId)}"));
        var asked = dialogs.AssertRules().Asked[0];
        Assert.Equal("Raumsperre löschen?", asked.Title);
        Assert.Equal("Die Raumsperre „Raum Zeder, Wartung, 24.12.2027 bis 26.12.2027“ ist danach weg. Termine werden nicht angefasst; der Raum ist in der Zeit sofort wieder buchbar.", asked.Message);
        Assert.Equal("Löschen", asked.ConfirmText);
        Assert.Equal("Raumsperre gelöscht", page.Toast.Text);
        Assert.False(page.IsPanelOpen);
        Assert.Empty(page.Upcoming);
    }

    [Fact]
    public async Task Given_Schliesszeit_und_Raumsperre_Then_stehen_beide_in_einer_Liste_nach_Beginn_und_der_Filter_trennt_sie()
    {
        // ADR 0098: eine Liste; die Art ist ein Kennzeichen (Standort oder Raum), der Filter zeigt nur eine Art.
        var closure = new ClosureDto(Guid.NewGuid(), TestData.LocationId, new(2027, 12, 1), null, new(2027, 12, 3), null, Midnight(new(2027, 12, 1)), Midnight(new(2027, 12, 4)),
            ClosureKind.Vacation, "Inventur", null, ClosurePhase.Planned, "Katrin", TestData.At(8), 0, 0, 0);
        var api = Api(Maintenance).OnOverview(TestData.LocationId, () => [ClosuresTestKit.ToItem(Maintenance), ClosuresTestKit.ToItem(closure)])
            .On($"GET /api/locations/{TestData.LocationId}/closures/{closure.Id}", closure);
        var page = await Open(api);

        Assert.Equal([("Standort", "Betriebsurlaub"), ("Raum", "Raum Zeder, Wartung")], page.Upcoming.Select(r => (r.TypeText, r.Title)));
        Assert.Equal(["Alle 2", "Standort geschlossen 1", "Raum gesperrt 1"], page.Filters.Select(f => f.Label));

        page.Filters[2].SelectCommand.Execute(null);
        Assert.Equal(["Raum Zeder, Wartung"], page.Upcoming.Select(r => r.Title));
        Assert.Equal([false, false, true], page.Filters.Select(f => f.IsOn));

        page.Filters[1].SelectCommand.Execute(null);
        Assert.Equal(["Betriebsurlaub"], page.Upcoming.Select(r => r.Title));

        page.Filters[0].SelectCommand.Execute(null);
        Assert.Equal(2, page.Upcoming.Count);

        // Eine Zeile öffnet das Formular ihrer Art.
        await page.OpenRowAsync(page.Upcoming[1]);
        Assert.Same(page.RoomEditor, page.Editor);
        await page.OpenRowAsync(page.Upcoming[0]);
        Assert.Same(page.ClosureEditor, page.Editor);
        Assert.False(page.RoomEditor.HasForm);
    }

    [Fact]
    public async Task Given_zwei_Standorte_When_der_zweite_gewaehlt_wird_Then_zeigt_die_Liste_seine_Eintraege_und_Raeume()
    {
        var api = Api(Maintenance);
        var page = await Open(api);
        await page.OpenRowAsync(page.Upcoming[0]);
        Assert.Single(page.Upcoming);

        page.SelectedLocation = page.Locations.Single(l => l.Id == SecondId);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Empty(page.Upcoming);
        Assert.False(page.IsPanelOpen);
        Assert.Equal(1, CallsTo(api, $"GET /api/locations/{SecondId}/closure-overview?scope=Upcoming"));
        await page.NewRoomCommand.ExecuteAsync(null);
        Assert.Equal([SaltRoom.Id], page.RoomEditor.Rooms.Select(r => r.Id));
    }

    [Fact]
    public async Task Given_ungespeicherte_Raumsperre_When_der_Kalender_Raum_sperren_waehlt_Then_fragt_die_Seite_Aenderungen_speichern()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var page = await OpenFirst(Api(Maintenance), dialogs);
        page.RoomEditor.Reason = "geändert";

        page.Open(new AdminTarget(Id: TestData.Room.Id));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Änderungen speichern?", Assert.Single(dialogs.Asked).Title);
        Assert.False(page.RoomEditor.IsCreating);
        Assert.Equal("geändert", page.RoomEditor.Reason);
    }

    [Fact]
    public async Task Given_der_Kalender_When_Raum_sperren_Then_neues_Formular_mit_diesem_Raum()
    {
        var shell = new ShellViewModel(Api(Maintenance).Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        shell.AdminPage.Show("masterData", "roomClosures", SaltRoom.Id);
        await shell.AdminPage.InitializeAsync();
        var page = shell.AdminPage.ClosuresPage!;
        await page.EnsureLoadedAsync();

        // ADR 0098: Das alte Ziel öffnet die Seite „Schließzeiten“ mit einem neuen Formular „Einzelner Raum“.
        Assert.Equal("Schließzeiten", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(page, shell.AdminPage.Content);
        var closures = page.RoomEditor;
        Assert.True(closures.IsCreating);
        Assert.Same(closures, page.Editor);
        Assert.True(page.NewIsRoom);
        Assert.True(page.IsPanelOpen);
        Assert.Equal(SecondId, page.SelectedLocation?.Id);
        Assert.Equal(SaltRoom.Id, closures.SelectedRoom?.Id);
    }

    [Fact]
    public async Task Given_der_Assistent_mit_Zeitraum_When_die_Seite_offen_ist_Then_neues_Formular_der_Schliesszeit_mit_Von_und_Bis()
    {
        var page = await Open(Api(Maintenance));

        page.Open(new AdminTarget(Location: TestData.LocationId, From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await Task.Yield();
        await Task.Yield();

        Assert.True(page.ClosureEditor.IsCreating);
        Assert.Equal(("02.08.2027", "06.08.2027"), (page.ClosureEditor.Period.FromDate, page.ClosureEditor.Period.UntilDate));
    }

    [Fact]
    public async Task Given_ein_Sprung_mit_Raum_und_Zeitraum_When_Verwerfen_gewaehlt_wird_Then_bleiben_Raum_und_Zeitraum_stehen()
    {
        var page = await Open(Api(Maintenance));
        page.Open(new AdminTarget(Id: TestData.Room.Id, Location: TestData.LocationId, From: new(2027, 8, 2), Until: new(2027, 8, 6), Section: ClosuresPageViewModel.RoomClosuresSection));
        await Task.Yield();
        await Task.Yield();
        var closures = page.RoomEditor;
        closures.Reason = "Wartung";
        closures.Period.FromDate = "10.08.2027";

        closures.DiscardCommand.Execute(null);

        Assert.True(closures.IsCreating);
        Assert.Equal(TestData.Room.Id, closures.SelectedRoom?.Id);
        Assert.Equal(("02.08.2027", "06.08.2027"), (closures.Period.FromDate, closures.Period.UntilDate));
        Assert.Equal("", closures.Reason);
    }
}
