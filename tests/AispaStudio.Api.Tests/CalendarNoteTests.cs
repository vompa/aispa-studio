using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Zettel mit Endzeit (Schritt 18, Ä23): optional, nur mit Uhrzeit und nach ihr.</summary>
public sealed class CalendarNoteTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Notiz_mit_Ende_speichert_es_und_liefert_es_im_Kalender()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var note = await spa.PostAsync<CalendarNoteDto>($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "Herr Lang ruft zurück", ResourceKind.Employee, spa.Anna.Id, TestSpa.At(10), TestSpa.At(10, 30)), Ct);

        Assert.Equal((TestSpa.At(10), TestSpa.At(10, 30)), (note.At, note.Until));
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Equal(TestSpa.At(10, 30), Assert.Single(day.Notes!).Until);
    }

    [Fact]
    public async Task Notiz_ohne_Ende_hat_keines()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var note = await spa.PostAsync<CalendarNoteDto>($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "Anruf", ResourceKind.Employee, spa.Anna.Id, TestSpa.At(10)), Ct);

        Assert.Null(note.Until);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(9, 30)]
    public async Task Ende_nicht_nach_dem_Beginn_wird_abgelehnt(int hour, int minute)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "Anruf", ResourceKind.Employee, spa.Anna.Id, TestSpa.At(10), TestSpa.At(hour, minute)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Das Ende muss nach dem Beginn liegen.", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ende_ohne_Uhrzeit_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "Lieferung", Until: TestSpa.At(10)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ende_nachtraeglich_setzen_und_vor_dem_Beginn_ablehnen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var note = await spa.PostAsync<CalendarNoteDto>($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "Anruf", ResourceKind.Employee, spa.Anna.Id, TestSpa.At(10)), Ct);

        var changed = await spa.PatchAsync<CalendarNoteDto>($"/api/calendar-notes/{note.Id}", new CalendarNoteChange(Until: TestSpa.At(11)), Ct);
        var before = await spa.Client.PatchAsJsonAsync($"/api/calendar-notes/{note.Id}", new CalendarNoteChange(Until: TestSpa.At(9)), TestSpa.Json, Ct);
        var done = await spa.PatchAsync<CalendarNoteDto>($"/api/calendar-notes/{note.Id}", new CalendarNoteChange(Done: true), Ct);

        Assert.Equal(TestSpa.At(11), changed.Until);
        Assert.Equal(HttpStatusCode.BadRequest, before.StatusCode);
        Assert.Equal(TestSpa.At(11), done.Until);
    }

    [Fact]
    public async Task Uhrzeit_nachtraeglich_verschieben_nimmt_ein_Ende_davor_mit_weg_und_geht_nicht_bei_Tagesnotizen()
    {
        // Auftraggeber, 23.10.2026: Zettel werden im Panel bearbeitet, auch die Uhrzeit.
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var note = await spa.PostAsync<CalendarNoteDto>($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "Anruf", ResourceKind.Employee, spa.Anna.Id, TestSpa.At(10), TestSpa.At(10, 15)), Ct);
        var dayNote = await spa.PostAsync<CalendarNoteDto>($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(TestSpa.Monday, "Lieferung"), Ct);

        var moved = await spa.PatchAsync<CalendarNoteDto>($"/api/calendar-notes/{note.Id}", new CalendarNoteChange(At: TestSpa.At(14), Until: TestSpa.At(14, 30)), Ct);
        var later = await spa.PatchAsync<CalendarNoteDto>($"/api/calendar-notes/{note.Id}", new CalendarNoteChange(At: TestSpa.At(15)), Ct);
        var dayMoved = await spa.Client.PatchAsJsonAsync($"/api/calendar-notes/{dayNote.Id}", new CalendarNoteChange(At: TestSpa.At(14)), TestSpa.Json, Ct);

        Assert.Equal((TestSpa.At(14), TestSpa.At(14, 30)), (moved.At, moved.Until));
        Assert.Equal((TestSpa.At(15), (DateTimeOffset?)null), (later.At, later.Until));
        Assert.Equal(HttpStatusCode.BadRequest, dayMoved.StatusCode);
    }
}
