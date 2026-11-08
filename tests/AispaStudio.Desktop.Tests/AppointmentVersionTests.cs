using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ä281 (Slice 119): Kalender, Termin-Panel und Offline-Warteschlange schicken die Version des Stands mit, von dem aus geändert wird;
/// ein veralteter Stand führt zur Meldung und zum aktuellen Termin statt zum stillen Überschreiben.
/// </summary>
public sealed class AppointmentVersionTests
{
    private const string StaleMessage = "Der Termin wurde inzwischen an einem anderen Arbeitsplatz geändert. Bitte den aktuellen Stand prüfen.";

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AppointmentDto Seen() => TestData.Appointment(10, TestData.Anna) with { Version = 7 };

    private static StubApi Api(AppointmentDto appointment) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day(appointment));

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static T LastBody<T>(StubApi api, string suffix) =>
        JsonSerializer.Deserialize<T>(api.Calls.Last(c => c.Route.EndsWith(suffix, StringComparison.Ordinal)).Body!, ApiClient.Json)!;

    private static async Task DragOneHourLaterAsync(CalendarViewModel vm)
    {
        vm.BeginDrag(vm.Columns[0].Items[0], grabOffset: 0);
        vm.DragTo(10, vm.Columns[0].Items[0].Top + (60 * DayLayout.PixelsPerMinute));
        await vm.DropAsync();
    }

    [Fact]
    public async Task Ziehen_von_altem_Stand_zeigt_Konflikt_und_den_aktuellen_Termin()
    {
        var seen = Seen();
        var current = seen with
        {
            Start = TestData.At(14),
            End = TestData.At(14, 50),
            BlockedFrom = TestData.At(14).AddMinutes(-5),
            BlockedUntil = TestData.At(15),
            UpdatedAt = TestData.At(9),
            Version = 8,
        };
        var api = Api(seen).On($"POST /api/appointments/{seen.Id}/move", new BookingRejected(StaleMessage, [], Current: current), HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);

        await DragOneHourLaterAsync(vm);

        Assert.Equal(7u, LastBody<MoveAppointmentRequest>(api, "/move").ExpectedVersion);
        Assert.Equal(("Nicht verschoben", StaleMessage), (vm.Problem!.Title, vm.Problem.Message));
        Assert.Equal(TestData.At(14), Assert.Single(vm.Columns[0].Items).Appointment!.Start);
    }

    [Fact]
    public async Task Rueckgaengig_nach_Verschieben_schickt_die_Version_des_neuen_Stands()
    {
        var seen = Seen();
        var moved = seen with { Start = TestData.At(11), End = TestData.At(11, 50), UpdatedAt = TestData.At(9), Version = 8 };
        var api = Api(seen).On($"POST /api/appointments/{seen.Id}/move", moved);
        var vm = await OpenAsync(api);
        await DragOneHourLaterAsync(vm);

        await vm.UndoMoveCommand.ExecuteAsync(null);

        Assert.Equal(8u, LastBody<MoveAppointmentRequest>(api, "/move").ExpectedVersion);
    }

    [Fact]
    public async Task Live_Update_waehrend_der_Bearbeitung_behaelt_die_Version_des_Beginns()
    {
        var seen = Seen();
        var api = Api(seen).On($"PUT /api/appointments/{seen.Id}/note", new BookingRejected(StaleMessage, [], Current: seen with { Version = 9 }),
            HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);
        vm.Open(vm.Columns[0].Items[0]);
        var panel = vm.Selected!;
        panel.BeginEditNote();

        vm.ApplyLive(seen with { Note = "Vom anderen Arbeitsplatz", UpdatedAt = TestData.At(9), Version = 9 });

        Assert.Same(panel, vm.Selected);
        Assert.True(panel.IsEditingNote);
        panel.NoteText = "Lavendelöl";
        await panel.SaveNoteAsync();
        Assert.Equal(7u, LastBody<AppointmentNoteRequest>(api, "/note").ExpectedVersion);
    }

    [Fact]
    public async Task Nach_abgebrochener_Bearbeitung_zeigt_das_Panel_den_neuen_Stand()
    {
        var seen = Seen();
        var vm = await OpenAsync(Api(seen));
        vm.Open(vm.Columns[0].Items[0]);
        vm.Selected!.BeginEditNote();
        vm.ApplyLive(seen with { Note = "Vom anderen Arbeitsplatz", UpdatedAt = TestData.At(9), Version = 9 });

        vm.Selected.CancelEditNote();

        Assert.Equal((9u, "Vom anderen Arbeitsplatz"), (vm.Selected!.Appointment.Version, vm.Selected.Appointment.Note));
    }

    [Fact]
    public async Task Rueckgaengig_von_altem_Stand_meldet_den_Konflikt_ohne_Erneut_versuchen()
    {
        var seen = Seen();
        var moved = seen with { Start = TestData.At(11), End = TestData.At(11, 50), UpdatedAt = TestData.At(9), Version = 8 };
        var api = Api(seen).On($"POST /api/appointments/{seen.Id}/move", moved);
        var vm = await OpenAsync(api);
        await DragOneHourLaterAsync(vm);
        api.On($"POST /api/appointments/{seen.Id}/move",
            new BookingRejected(StaleMessage, [], Current: moved with { Note = "Vom anderen Arbeitsplatz", UpdatedAt = TestData.At(9, 30), Version = 9 }),
            HttpStatusCode.Conflict);

        await vm.UndoMoveCommand.ExecuteAsync(null);

        Assert.Equal(("Nicht zurückgenommen", StaleMessage), (vm.Problem!.Title, vm.Problem.Message));
        Assert.Null(vm.Problem.Action);
    }

    [Fact]
    public async Task Veraltete_Vormerkung_steht_nach_dem_Abgleich_unter_nicht_uebernommen()
    {
        var seen = Seen();
        var stub = new StubApi { Down = true };
        var cache = new OfflineCache(new MemoryOfflineStore(), new FixedTime());
        var api = stub.Client(cache);
        var synced = new List<AppointmentDto>();
        api.Synced += synced.Add;
        await api.PostOrQueueAsync($"api/appointments/{seen.Id}/move", new MoveAppointmentRequest(TestData.At(11), null, null, null, seen.Version),
            "Eva Muster, 01.03.2027: verschieben auf 11:00", seen with { Start = TestData.At(11) }, ct: Ct);

        stub.Down = false;
        stub.On($"POST /api/appointments/{seen.Id}/move", new BookingRejected(StaleMessage, [], Current: seen with { Version = 8 }), HttpStatusCode.Conflict);
        await api.SyncAsync(Ct);

        var failed = Assert.Single(cache.Failed);
        Assert.Equal(("Eva Muster, 01.03.2027: verschieben auf 11:00", StaleMessage), (failed.Description, failed.Reason));
        Assert.Empty(cache.Pending);
        Assert.Empty(synced);
    }

    [Fact]
    public async Task Zwei_eigene_Vormerkungen_am_selben_Termin_gehen_beim_Abgleich_beide_durch()
    {
        var seen = Seen();
        var stub = new StubApi { Down = true };
        var cache = new OfflineCache(new MemoryOfflineStore(), new FixedTime());
        var api = stub.Client(cache);
        var path = $"api/appointments/{seen.Id}/move";
        await api.PostOrQueueAsync(path, new MoveAppointmentRequest(TestData.At(11), null, null, null, seen.Version), "erste", seen with { Start = TestData.At(11) }, ct: Ct);
        await api.PostOrQueueAsync(path, new MoveAppointmentRequest(TestData.At(12), null, null, null, seen.Version), "zweite", seen with { Start = TestData.At(12) }, ct: Ct);

        stub.Down = false;
        stub.OnJson($"POST /{path}", body =>
        {
            var request = JsonSerializer.Deserialize<MoveAppointmentRequest>(body!, ApiClient.Json)!;
            return seen with { Start = request.Start, Version = request.ExpectedVersion!.Value + 1 };
        });
        await api.SyncAsync(Ct);

        Assert.Empty(cache.Failed);
        Assert.Equal(8u, LastBody<MoveAppointmentRequest>(stub, "/move").ExpectedVersion);
    }
}
