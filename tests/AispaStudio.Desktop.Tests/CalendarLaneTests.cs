using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Keine Überlagerung in der Spalte (Schritt 18, Ä22) und Notizen mit Endzeit (Ä23).</summary>
public sealed class CalendarLaneTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static readonly double Width = new CalendarScale(DayLayout.PixelsPerMinute, DayLayout.ColumnWidth).ItemWidth;

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static CalendarNoteDto Note(int hour, int minute, DateTimeOffset? until = null, string text = "Herr Lang ruft zurück", Guid? resource = null) =>
        new(Guid.NewGuid(), TestData.Monday, text, ResourceKind.Employee, resource ?? TestData.Anna.Id, TestData.At(hour, minute), false, TestData.At(8), until);

    private static CalendarColumn Anna(CalendarDay day, bool byRoom = false) =>
        DayLayout.Build(day, TestData.Resources, byRoom, Vienna).Columns.Single(c => c.Id == (byRoom ? TestData.Room.Id : TestData.Anna.Id));

    // ---------- Bahnen ----------

    [Fact]
    public void Ohne_Kollision_haben_Termin_und_Zettel_die_volle_Breite()
    {
        var day = TestData.Day(TestData.Appointment(10, TestData.Anna)) with { Notes = [Note(14, 0)] };

        var column = Anna(day);

        Assert.Equal(LanePlace.Alone, Assert.Single(column.Items).Place);
        Assert.Equal(LanePlace.Alone, Assert.Single(column.Notes).Place);
        Assert.Equal(Width, DayLayout.LaneWidth(Width, LanePlace.Alone));
    }

    [Fact]
    public void Zettel_neben_einem_Termin_bekommt_die_schmale_rechte_Bahn_und_der_Termin_steht_links()
    {
        var day = TestData.Day(TestData.Appointment(10, TestData.Anna)) with { Notes = [Note(10, 20)] };

        var column = Anna(day);

        var appointment = Assert.Single(column.Items).Place;
        var note = Assert.Single(column.Notes).Place;
        Assert.Equal(new LanePlace(0, 1, InSide: false, HasSide: true), appointment);
        Assert.Equal(new LanePlace(0, 1, InSide: true, HasSide: true), note);
        // Ein Drittel der Termin-Breite, mindestens 72 Pixel; der Termin bekommt den Rest, ohne dass sich beide berühren.
        var side = DayLayout.SideLaneWidth(Width);
        Assert.Equal(72, side);
        Assert.Equal(Width - side - 2, DayLayout.LaneWidth(Width, appointment), 3);
        Assert.Equal(0, DayLayout.LaneLeft(Width, appointment), 3);
        Assert.Equal(side - 2, DayLayout.LaneWidth(Width, note), 3);
        Assert.Equal(Width - side, DayLayout.LaneLeft(Width, note), 3);
        Assert.True(DayLayout.LaneLeft(Width, appointment) + DayLayout.LaneWidth(Width, appointment) <= DayLayout.LaneLeft(Width, note));
    }

    [Fact]
    public void Die_schmale_Bahn_ist_ein_Drittel_und_bei_breiten_Spalten_mehr_als_72_Pixel()
    {
        Assert.Equal(100, DayLayout.SideLaneWidth(300), 3);
        Assert.Equal(72, DayLayout.SideLaneWidth(202), 3);
        // Nie mehr als die Hälfte, damit der Termin nicht verdrängt wird.
        Assert.Equal(40, DayLayout.SideLaneWidth(80), 3);
    }

    [Fact]
    public void Sperrzeit_neben_einem_Termin_steht_wie_ein_Zettel_rechts()
    {
        var appointment = TestData.Appointment(10, TestData.Anna);
        var block = new BlockDto(Guid.NewGuid(), ResourceKind.Employee, TestData.Anna.Id, TestData.At(10, 30), TestData.At(11, 30), "Schulung");
        var day = TestData.Day(appointment) with { Blocks = [block] };

        var column = Anna(day);

        Assert.Equal(new LanePlace(0, 1, false, true), column.Items.Single(i => i.Appointment is not null).Place);
        Assert.Equal(new LanePlace(0, 1, true, true), column.Items.Single(i => i.IsBlock).Place);
    }

    [Fact]
    public void Zwei_Termine_stehen_je_halb_der_frueheren_links_und_ein_Zettel_daneben_rechts()
    {
        var first = TestData.Appointment(10, TestData.Anna);
        var second = TestData.Appointment(10, TestData.Anna) with { Start = TestData.At(10, 20), End = TestData.At(10, 40), BlockedFrom = TestData.At(10, 20), BlockedUntil = TestData.At(10, 40) };
        var day = TestData.Day(first, second) with { Notes = [Note(10, 25)] };

        var column = Anna(day);

        Assert.Equal(new LanePlace(0, 2, false, true), column.Items.Single(i => i.Id == first.Id).Place);
        Assert.Equal(new LanePlace(1, 2, false, true), column.Items.Single(i => i.Id == second.Id).Place);
        Assert.Equal(new LanePlace(0, 1, true, true), Assert.Single(column.Notes).Place);
        var main = Width - DayLayout.SideLaneWidth(Width);
        Assert.Equal(main / 2 - 2, DayLayout.LaneWidth(Width, column.Items[0].Place), 3);
        Assert.Equal(main / 2, DayLayout.LaneLeft(Width, column.Items.Single(i => i.Id == second.Id).Place), 3);
    }

    [Fact]
    public void Zwei_Zettel_zur_selben_Zeit_teilen_sich_die_Spalte_statt_uebereinander_zu_stehen()
    {
        var day = TestData.Day() with { Notes = [Note(10, 0, text: "A"), Note(10, 5, text: "B")] };

        var notes = Anna(day).Notes;

        Assert.Equal([new LanePlace(0, 2), new LanePlace(1, 2)], notes.OrderBy(n => n.Top).Select(n => n.Place));
    }

    [Fact]
    public void Aneinander_anschliessende_Elemente_stehen_nicht_nebeneinander()
    {
        // 10:00–10:50 Termin mit Nachbereitung bis 11:00; der Zettel um 11:00 liegt danach (Höhe 22 Pixel ab 11:00).
        var appointment = TestData.Appointment(10, TestData.Anna) with { End = TestData.At(11), BlockedUntil = TestData.At(11) };
        var day = TestData.Day(appointment) with { Notes = [Note(11, 0)] };

        var column = Anna(day);

        Assert.Equal(LanePlace.Alone, column.Items.Single().Place);
        Assert.Equal(LanePlace.Alone, column.Notes.Single().Place);
    }

    [Fact]
    public void Auch_in_der_Raumansicht_stehen_Zettel_neben_dem_Termin()
    {
        var note = new CalendarNoteDto(Guid.NewGuid(), TestData.Monday, "Steine heizen", ResourceKind.Room, TestData.Room.Id, TestData.At(10, 15), false, TestData.At(8));
        var day = TestData.Day(TestData.Appointment(10, TestData.Anna)) with { Notes = [note] };

        var column = Anna(day, byRoom: true);

        Assert.True(Assert.Single(column.Items).Place.HasSide);
        Assert.True(Assert.Single(column.Notes).Place.InSide);
    }

    // ---------- Notiz mit Ende ----------

    [Fact]
    public void Notiz_mit_Ende_reicht_ueber_die_Dauer_und_nennt_Beginn_und_Ende()
    {
        var day = TestData.Day() with { Notes = [Note(10, 0, TestData.At(10, 30))] };

        var sticky = Assert.Single(Anna(day).Notes);

        Assert.Equal(30 * DayLayout.PixelsPerMinute, sticky.Height, 3);
        Assert.Equal("10:00–10:30", sticky.Time);
        Assert.Equal("10:00–10:30 Herr Lang ruft zurück", sticky.Tip);
    }

    [Fact]
    public void Notiz_mit_sehr_kurzem_Ende_ist_mindestens_18_Pixel_hoch_und_ohne_Ende_22()
    {
        var day = TestData.Day() with { Notes = [Note(10, 0, TestData.At(10, 5)), Note(14, 0)] };

        var notes = Anna(day).Notes.OrderBy(n => n.Top).ToList();

        Assert.Equal(DayLayout.MinNoteHeight, notes[0].Height, 3);
        Assert.Equal(22, notes[1].Height, 3);
        Assert.Equal("14:00", notes[1].Time);
    }

    [Fact]
    public void Eine_Notiz_mit_Ende_neben_einem_Termin_nimmt_die_ganze_Dauer_der_schmalen_Bahn()
    {
        var first = TestData.Appointment(10, TestData.Anna);
        var later = TestData.Appointment(12, TestData.Anna);
        var day = TestData.Day(first, later) with { Notes = [Note(10, 30, TestData.At(12, 30))] };

        var column = Anna(day);

        // Die Notiz schneidet beide Termine; beide stehen links, die Notiz rechts.
        Assert.All(column.Items, i => Assert.Equal(new LanePlace(0, 1, false, true), i.Place));
        Assert.Equal(new LanePlace(0, 1, true, true), Assert.Single(column.Notes).Place);
    }

    // ---------- Eingabe ----------

    private static StubApi Api(CalendarDay day) => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", day);

    private static async Task<CalendarViewModel> OpenAsync(StubApi api)
    {
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Notiz_hier_hat_ein_Feld_Bis_und_schickt_das_Ende_mit()
    {
        var saved = Note(14, 0, TestData.At(14, 30));
        var api = Api(TestData.Day()).On($"POST /api/locations/{TestData.LocationId}/calendar-notes", saved, HttpStatusCode.Created);
        var vm = await OpenAsync(api);
        vm.OpenNoteAt(10, vm.Hours.Single(h => h.Label == "14:00").Top + 1);
        var entry = vm.Entry!;

        // Das Ende ist mit 15 Minuten vorbelegt (Auftraggeber, 23.10.2026) und lässt sich ändern oder leeren.
        Assert.Equal(("Uhrzeit", "Bis", true, "14:15"), (entry.StartLabel, entry.EndLabel, entry.ShowEnd, entry.EndText));
        entry.Text = "Herr Lang ruft zurück";
        entry.EndText = "14:30";
        await entry.SaveAsync();

        var body = api.Calls.Single(c => c.Route.StartsWith("POST /api/locations", StringComparison.Ordinal)).Body!;
        Assert.Contains("\"until\":\"2027-03-01T13:30:00+00:00\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Notiz_ohne_Ende_schickt_keines_und_ein_Ende_vor_dem_Beginn_wird_abgelehnt()
    {
        var api = Api(TestData.Day()).On($"POST /api/locations/{TestData.LocationId}/calendar-notes", Note(14, 0), HttpStatusCode.Created);
        var vm = await OpenAsync(api);
        vm.OpenNoteAt(10, vm.Hours.Single(h => h.Label == "14:00").Top + 1);
        var entry = vm.Entry!;
        entry.Text = "Anruf";
        entry.EndText = "13:30";

        await entry.SaveAsync();

        Assert.Equal("Das Ende muss nach dem Beginn liegen.", entry.Error);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("POST /api/locations", StringComparison.Ordinal));

        entry.EndText = " ";
        await entry.SaveAsync();

        var body = api.Calls.Single(c => c.Route.StartsWith("POST /api/locations", StringComparison.Ordinal)).Body!;
        Assert.Contains("\"until\":null", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sperrzeit_und_Pause_behalten_das_Pflichtfeld_Ende()
    {
        var vm = await OpenAsync(Api(TestData.Day()));
        vm.OpenNoteAt(10, vm.Hours.Single(h => h.Label == "14:00").Top + 1);

        Assert.True(vm.SelectNewEntry(4));

        Assert.Equal(("Beginn", "Ende", true), (vm.Entry!.StartLabel, vm.Entry.EndLabel, vm.Entry.ShowEnd));
        Assert.NotEqual("", vm.Entry.EndText);
    }
}
