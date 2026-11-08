using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Slice 71 (Ä88, Ä89): Notiz- und Pausenbalken lassen sich am oberen und unteren Rand in der Höhe ziehen, im Raster von 5 Minuten wie beim Termin.
/// Testtag: Anna arbeitet 9 bis 17 Uhr; Zettel „Herr Lang ruft zurück“ 14:00 bis 14:30, Pause 13:00 bis 13:30.
/// </summary>
public sealed class CalendarEdgeDragTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static readonly CalendarNoteDto Note =
        new(Guid.NewGuid(), TestData.Monday, "Herr Lang ruft zurück", ResourceKind.Employee, TestData.Anna.Id, TestData.At(14), false, TestData.At(8), TestData.At(14, 30));

    private static readonly BlockDto Break = new(Guid.NewGuid(), ResourceKind.Employee, TestData.Anna.Id, TestData.At(13), TestData.At(13, 30), "Pause");

    private static CalendarDay Day() => TestData.Day() with { Notes = [Note], Blocks = [Break] };

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        api.On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources);
        var vm = new CalendarViewModel(api.Client(), TestData.Owner(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static StubApi Api() => new StubApi().On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", Day());

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Last(c => c.Route == route).Body!, ApiClient.Json)!;

    [Fact]
    public async Task Notizrand_unten_30_Minuten_ziehen_schickt_neues_Ende_und_die_Bearbeitung_zeigt_Bis_30_Minuten_spaeter()
    {
        var api = Api().On($"PATCH /api/calendar-notes/{Note.Id}", Note with { Until = TestData.At(15) });
        var vm = await OpenAsync(api);
        var note = vm.Columns[0].Notes.Single();
        Assert.True(vm.CanResize(note));
        var bottom = note.Top + note.Height;

        // Gegriffen 2 Pixel über dem unteren Rand, 30 Minuten nach unten gezogen.
        vm.BeginResize(note, ResizeEdge.Bottom, grabOffset: -2);
        Assert.True(vm.IsDragging);
        vm.DragTo(10, bottom - 2 + (30 * vm.Scale.PixelsPerMinute));
        Assert.StartsWith("14:00–15:00", vm.Ghost!.Text, StringComparison.Ordinal);
        await vm.DropAsync();

        Assert.False(vm.IsDragging);
        var change = Sent<CalendarNoteChange>(api, $"PATCH /api/calendar-notes/{Note.Id}");
        Assert.Equal(TestData.At(15), change.Until);
        Assert.Null(change.At);
        Assert.Equal("14:00–15:00", vm.Columns[0].Notes.Single().Time);

        vm.OpenNote(vm.Columns[0].Notes.Single().Note);
        Assert.Equal("14:00", vm.Entry!.StartText);
        Assert.Equal("15:00", vm.Entry.EndText);
    }

    [Fact]
    public async Task Notizrand_oben_ziehen_schickt_neue_Uhrzeit_und_das_Ende_bleibt_mindestens_5_Minuten_danach()
    {
        var api = Api().On($"PATCH /api/calendar-notes/{Note.Id}", Note with { At = TestData.At(14, 25) });
        var vm = await OpenAsync(api);
        var note = vm.Columns[0].Notes.Single();

        // Über das Ende hinaus nach unten: der Beginn bleibt 5 Minuten vor dem Ende stehen.
        vm.BeginResize(note, ResizeEdge.Top, grabOffset: 2);
        vm.DragTo(10, note.Top + 2 + (60 * vm.Scale.PixelsPerMinute));
        await vm.DropAsync();

        var change = Sent<CalendarNoteChange>(api, $"PATCH /api/calendar-notes/{Note.Id}");
        Assert.Equal(TestData.At(14, 25), change.At);
        Assert.Null(change.Until);
    }

    [Fact]
    public async Task Pause_oberen_Rand_15_Minuten_hochziehen_schickt_neuen_Beginn_und_laedt_den_Tag_neu()
    {
        var moved = Break with { From = TestData.At(12, 45) };
        var api = Api().On($"PATCH /api/blocks/{Break.Id}", moved);
        var vm = await OpenAsync(api);
        var item = vm.Columns[0].Items.Single(i => i.IsBreak);
        Assert.True(vm.CanResize(item));

        var dayRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";
        var loadsBefore = api.Calls.Count(c => c.Route == dayRoute);

        vm.BeginResize(item, ResizeEdge.Top, grabOffset: 3);
        vm.DragTo(10, item.Top + 3 - (15 * vm.Scale.PixelsPerMinute));
        await vm.DropAsync();

        var change = Sent<BlockChange>(api, $"PATCH /api/blocks/{Break.Id}");
        Assert.Equal(TestData.At(12, 45), change.From);
        Assert.Equal(TestData.At(13, 30), change.Until);
        Assert.Equal(loadsBefore + 1, api.Calls.Count(c => c.Route == dayRoute));
        Assert.Equal("Pause geändert", vm.Toast);
    }

    [Fact]
    public async Task Abgelehnte_Pause_springt_zurueck_und_meldet_Nicht_geaendert()
    {
        var api = Api().On($"PATCH /api/blocks/{Break.Id}",
            new BookingRejected("Zeitraum ist belegt.", [new BookingConflict(ResourceKind.Employee, TestData.Anna.Id, "Eva Muster", "hat hier einen Termin", TestData.At(13, 30), TestData.At(14))]),
            HttpStatusCode.Conflict);
        var vm = await OpenAsync(api);
        var item = vm.Columns[0].Items.Single(i => i.IsBreak);

        vm.BeginResize(item, ResizeEdge.Bottom, grabOffset: 0);
        vm.DragTo(10, item.Top + item.Height + (30 * vm.Scale.PixelsPerMinute));
        await vm.DropAsync();

        Assert.Equal("Nicht geändert", vm.Problem?.Title);
        Assert.Equal(item.Top, vm.Columns[0].Items.Single(i => i.IsBreak).Top);
        Assert.Equal(item.Height, vm.Columns[0].Items.Single(i => i.IsBreak).Height);
    }

    [Fact]
    public async Task Termine_und_andere_Sperrzeiten_haben_keine_ziehbaren_Raender()
    {
        var block = new BlockDto(Guid.NewGuid(), ResourceKind.Employee, TestData.Anna.Id, TestData.At(15), TestData.At(16), "Schulung");
        var api = new StubApi().On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01",
            TestData.Day(TestData.Appointment(10, TestData.Anna)) with { Blocks = [block] });
        var vm = await OpenAsync(api);

        Assert.All(vm.Columns[0].Items, item => Assert.False(vm.CanResize(item)));
    }
}
