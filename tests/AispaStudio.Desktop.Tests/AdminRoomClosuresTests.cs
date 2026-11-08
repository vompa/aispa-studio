using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 95 (Ä207): native Maske „Raumsperren“ nach RoomClosuresPage.tsx: Liste mit Reitern je Standort, Formular mit Raum, Zeitraum, Art und Grund, Löschen mit Rückfrage und Sprungziele.</summary>
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
        // Die Liste verändert sich mit POST und PUT wie die der API.
        var list = closures.ToList();
        return new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location, Salzburg })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{SecondId}/resources", new LocationResources(Salzburg, [SaltRoom], [], [], [], []))
            .On($"GET {Route()}", list)
            .On($"GET /api/locations/{SecondId}/room-closures", new List<RoomClosureDto>())
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

    private static RoomClosureDto FromInput(Guid id, string? body)
    {
        var input = JsonSerializer.Deserialize<RoomClosureInput>(body!, ApiClient.Json)!;
        return Closure(id, TestData.Room, input.FromDate, input.UntilDate, input.Kind, reason: input.Reason) with { FromTime = input.FromTime, UntilTime = input.UntilTime };
    }

    private static async Task<RoomClosuresViewModel> Open(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var closures = new RoomClosuresViewModel(api.Client(), user ?? TestData.Owner(), Now) { Dialogs = dialogs };
        await closures.EnsureLoadedAsync();
        return closures;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    private static int CallsTo(StubApi api, string route) => api.Calls.Count(c => c.Route == route);

    [Fact]
    public async Task Given_ein_Raum_When_eine_Raumsperre_angelegt_wird_Then_POST_mit_Raum_Zeitraum_und_Grund()
    {
        var api = Api();
        var closures = await Open(api);

        await closures.NewCommand.ExecuteAsync(null);
        Assert.Equal(TestData.Room.Id, closures.SelectedRoom?.Id);
        closures.Period.FromDate = "24.12.2027";
        closures.Period.UntilDate = "26.12.2027";
        closures.Kind = RoomClosureKind.Damage;
        closures.Reason = "Wasserschaden, Trocknung";
        Assert.True(await closures.SaveChangesAsync());

        Assert.Equal(new RoomClosureInput(TestData.Room.Id, new(2027, 12, 24), null, new(2027, 12, 26), null, RoomClosureKind.Damage, "Wasserschaden, Trocknung"),
            Sent<RoomClosureInput>(api, $"POST {Route()}"));
        Assert.Equal("Gespeichert.", closures.StatusText);
        var row = Assert.Single(closures.Rows);
        Assert.Equal(("Raum Zeder, Schaden", "24.12.2027 bis 26.12.2027", "Wasserschaden, Trocknung"), (row.Title, row.Period, row.Detail));
    }

    [Fact]
    public async Task Given_ein_Standort_ohne_Raum_Then_gibt_es_keine_neue_Raumsperre()
    {
        var api = Api().On($"GET /api/locations/{TestData.LocationId}/resources", new LocationResources(TestData.Location, [], [], [], [], []));
        var closures = await Open(api);

        Assert.False(closures.NewCommand.CanExecute(null));
        Assert.Equal("Es ist kein Raum gesperrt.", closures.EmptyText);
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
    public async Task Given_ein_leeres_Datum_When_gespeichert_wird_Then_steht_der_Fehler_mit_dem_Wort_gesperrt_am_Feld()
    {
        var closures = await Open(Api());
        await closures.NewCommand.ExecuteAsync(null);

        closures.Period.FromDate = "";
        closures.Period.UntilDate = "";

        Assert.False(await closures.SaveChangesAsync());
        Assert.Equal(("Bitte den ersten gesperrten Tag wählen.", "Bitte den letzten gesperrten Tag wählen."), (closures.Period.FromDateError, closures.Period.UntilDateError));
    }

    [Fact]
    public async Task Given_ein_zu_langer_Grund_When_gespeichert_wird_Then_Fehler_am_Feld()
    {
        var closures = await Open(Api());
        await closures.NewCommand.ExecuteAsync(null);

        closures.Reason = new string('a', 201);

        Assert.False(await closures.SaveChangesAsync());
        Assert.Equal("Höchstens 200 Zeichen.", closures.ReasonError);
        Assert.Equal("ReasonField", closures.FocusTarget);
    }

    [Fact]
    public async Task Given_eine_Raumsperre_Then_ist_der_Raum_festgelegt_und_offene_Termine_stehen_als_Hinweis_da()
    {
        var closures = await Open(Api(Maintenance with { OpenAppointments = 1 }));

        Assert.False(closures.CanSelectRoom);
        Assert.Equal("Raum Zeder, Wartung, 24.12.2027 bis 26.12.2027", closures.HeadingText);
        Assert.Equal("1 Termin liegt im gesperrten Raum und bleibt gebucht, bis er umgebucht wird. Die Umbuchungshilfe steht im Kalender: in der Seitenleiste Meldungen auf „Umbuchen …“ klicken.", closures.OpenNotice);

        await closures.NewCommand.ExecuteAsync(null);
        Assert.True(closures.CanSelectRoom);
    }

    [Fact]
    public async Task Given_eine_Raumsperre_When_Loeschen_bestaetigt_wird_Then_Rueckfrage_und_DELETE()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Confirmed);
        var api = Api(Maintenance);
        var closures = await Open(api, dialogs: dialogs);

        await closures.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, CallsTo(api, $"DELETE {Route("/" + ClosureId)}"));

        await closures.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, CallsTo(api, $"DELETE {Route("/" + ClosureId)}"));
        var asked = dialogs.AssertRules().Asked[0];
        Assert.Equal("Raumsperre löschen?", asked.Title);
        Assert.Equal("Die Raumsperre „Raum Zeder, Wartung, 24.12.2027 bis 26.12.2027“ ist danach weg. Termine werden nicht angefasst; der Raum ist in der Zeit sofort wieder buchbar.", asked.Message);
        Assert.Equal("Löschen", asked.ConfirmText);
        Assert.Equal("Gelöscht.", closures.Message);
    }

    [Fact]
    public async Task Given_zwei_Standorte_When_der_zweite_gewaehlt_wird_Then_zeigt_die_Liste_seine_Raumsperren_und_Raeume()
    {
        var api = Api(Maintenance);
        var closures = await Open(api);
        Assert.Single(closures.Rows);

        closures.SelectedLocation = closures.Locations.Single(l => l.Id == SecondId);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Empty(closures.Rows);
        Assert.Equal(1, CallsTo(api, $"GET /api/locations/{SecondId}/room-closures"));
        await closures.NewCommand.ExecuteAsync(null);
        Assert.Equal([SaltRoom.Id], closures.Rooms.Select(r => r.Id));
    }

    [Fact]
    public async Task Given_ungespeicherte_Raumsperre_When_der_Kalender_Raum_sperren_waehlt_Then_fragt_die_Maske_Aenderungen_speichern()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled);
        var closures = await Open(Api(Maintenance), dialogs: dialogs);
        closures.Reason = "geändert";

        closures.Open(new AdminTarget(Id: TestData.Room.Id));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Änderungen speichern?", Assert.Single(dialogs.Asked).Title);
        Assert.False(closures.IsCreating);
        Assert.Equal("geändert", closures.Reason);
    }

    [Fact]
    public async Task Given_der_Kalender_When_Raum_sperren_Then_neues_Formular_mit_diesem_Raum()
    {
        var shell = new ShellViewModel(Api(Maintenance).Client(), TestData.Owner(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask);

        shell.AdminPage.Show("masterData", "roomClosures", SaltRoom.Id);
        await shell.AdminPage.InitializeAsync();
        var closures = shell.AdminPage.RoomClosures!;
        await closures.EnsureLoadedAsync();

        Assert.Equal("Raumsperren", shell.AdminPage.SelectedArea?.Title);
        Assert.Same(closures, shell.AdminPage.Content);
        Assert.True(closures.IsCreating);
        Assert.Equal(SecondId, closures.SelectedLocation?.Id);
        Assert.Equal(SaltRoom.Id, closures.SelectedRoom?.Id);
    }

    [Fact]
    public async Task Given_der_Assistent_mit_Zeitraum_When_die_Maske_offen_ist_Then_neues_Formular_mit_Von_und_Bis()
    {
        var closures = await Open(Api(Maintenance));

        closures.Open(new AdminTarget(Location: TestData.LocationId, From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await Task.Yield();

        Assert.True(closures.IsCreating);
        Assert.Equal(("02.08.2027", "06.08.2027"), (closures.Period.FromDate, closures.Period.UntilDate));
        Assert.Equal(TestData.Room.Id, closures.SelectedRoom?.Id);
    }

    [Fact]
    public async Task Given_ein_Sprung_mit_Raum_und_Zeitraum_When_Verwerfen_gewaehlt_wird_Then_bleiben_Raum_und_Zeitraum_stehen()
    {
        var closures = await Open(Api(Maintenance));
        closures.Open(new AdminTarget(Id: TestData.Room.Id, Location: TestData.LocationId, From: new(2027, 8, 2), Until: new(2027, 8, 6)));
        await Task.Yield();
        closures.Reason = "Wartung";
        closures.Period.FromDate = "10.08.2027";

        closures.DiscardCommand.Execute(null);

        Assert.True(closures.IsCreating);
        Assert.Equal(TestData.Room.Id, closures.SelectedRoom?.Id);
        Assert.Equal(("02.08.2027", "06.08.2027"), (closures.Period.FromDate, closures.Period.UntilDate));
        Assert.Equal("", closures.Reason);
    }

    [Fact]
    public void Given_Rezeptionistin_Then_hat_die_Verwaltung_keine_Maske_Raumsperren()
    {
        var page = new ShellViewModel(new StubApi().Client(), TestData.Reception(), null, TimeProvider.System, new ThemeController(new MemorySettingsStore()), "Test", () => Task.CompletedTask).AdminPage;

        Assert.Null(page.RoomClosures);
        Assert.DoesNotContain(page.Areas, a => a.Title == "Raumsperren");
    }
}
