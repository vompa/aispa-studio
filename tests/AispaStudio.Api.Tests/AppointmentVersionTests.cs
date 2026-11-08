using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Ä281 (Slice 119): Wer einen Termin von einem veralteten Stand aus ändert, bekommt 409 mit dem aktuellen Termin; die Änderung eines anderen
/// Arbeitsplatzes wird nicht still überschrieben. Ohne <c>ExpectedVersion</c> ändert der Server wie bisher.
/// </summary>
public sealed class AppointmentVersionTests(ApiFixture fixture)
{
    private const string StaleMessage = "Der Termin wurde inzwischen an einem anderen Arbeitsplatz geändert. Bitte den aktuellen Stand prüfen.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string PathOf(AppointmentDto appointment, string action) => $"/api/appointments/{appointment.Id}/{action}";

    [Fact]
    public async Task Zweiter_Arbeitsplatz_verschiebt_von_altem_Stand_und_bekommt_409_mit_aktuellem_Termin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var seen = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var byA = await spa.Client.PostAsJsonAsync(PathOf(seen, "move"),
            new MoveAppointmentRequest(TestSpa.At(11), null, null, null, seen.Version), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, byA.StatusCode);

        var byB = await spa.Client.PostAsJsonAsync(PathOf(seen, "move"),
            new MoveAppointmentRequest(TestSpa.At(12), null, null, null, seen.Version), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, byB.StatusCode);
        var rejected = await byB.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        Assert.Equal(StaleMessage, rejected!.Message);
        Assert.Equal(TestSpa.At(11), rejected.Current!.Start);
        Assert.Equal(TestSpa.At(11), (await spa.GetAsync<AppointmentDto>($"/api/appointments/{seen.Id}", Ct)).Start);
    }

    [Fact]
    public async Task Verschieben_ohne_Version_verschiebt_wie_bisher()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var seen = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>(PathOf(seen, "move"), new MoveAppointmentRequest(TestSpa.At(11), null, null, null), Ct);

        var moved = await spa.PostAsync<AppointmentDto>(PathOf(seen, "move"), new MoveAppointmentRequest(TestSpa.At(12), null, null, null), Ct);

        Assert.Equal(TestSpa.At(12), moved.Start);
    }

    [Fact]
    public async Task Version_0_gilt_als_unbekannt_und_verschiebt_wie_bisher()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var seen = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.PutAsync<AppointmentDto>(PathOf(seen, "note"), new AppointmentNoteRequest("Vom anderen Arbeitsplatz"), Ct);

        var moved = await spa.PostAsync<AppointmentDto>(PathOf(seen, "move"), new MoveAppointmentRequest(TestSpa.At(12), null, null, null, 0), Ct);

        Assert.Equal(TestSpa.At(12), moved.Start);
    }

    [Fact]
    public async Task Jede_Aenderung_liefert_eine_neue_Version()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var changed = await spa.PutAsync<AppointmentDto>(PathOf(booked, "note"), new AppointmentNoteRequest("Fokus Nacken"), Ct);

        Assert.NotEqual(0u, booked.Version);
        Assert.NotEqual(booked.Version, changed.Version);
        Assert.Equal(changed.Version, (await spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.Id}", Ct)).Version);
    }

    public static TheoryData<string> PanelChanges => ["times", "note", "cancel", "status"];

    [Theory]
    [MemberData(nameof(PanelChanges))]
    public async Task Aenderung_im_Panel_von_altem_Stand_ergibt_409_und_aendert_nichts(string action)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var seen = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        await spa.PutAsync<AppointmentDto>(PathOf(seen, "note"), new AppointmentNoteRequest("Vom anderen Arbeitsplatz"), Ct);

        var response = action switch
        {
            "times" => await spa.Client.PutAsJsonAsync(PathOf(seen, action), new AppointmentTimesRequest(15, 15, seen.Version), TestSpa.Json, Ct),
            "note" => await spa.Client.PutAsJsonAsync(PathOf(seen, action), new AppointmentNoteRequest("Alter Stand", seen.Version), TestSpa.Json, Ct),
            "cancel" => await spa.Client.PostAsJsonAsync(PathOf(seen, action), new CancelAppointmentRequest(null, seen.Version), TestSpa.Json, Ct),
            _ => await spa.Client.PostAsJsonAsync(PathOf(seen, action),
                new AppointmentStatusChange(AppointmentStatus.CheckedIn, seen.Version), TestSpa.Json, Ct),
        };

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Vom anderen Arbeitsplatz", (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!.Current!.Note);
        var now = await spa.GetAsync<AppointmentDto>($"/api/appointments/{seen.Id}", Ct);
        Assert.Equal(("Vom anderen Arbeitsplatz", AppointmentStatus.Booked, seen.PreparationMinutes, seen.FollowUpMinutes),
            (now.Note, now.Status, now.PreparationMinutes, now.FollowUpMinutes));
    }

    [Fact]
    public async Task Zuruecknehmen_von_altem_Stand_ergibt_409_und_der_Termin_bleibt_storniert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var seen = await spa.PostAsync<AppointmentDto>(PathOf(booked, "cancel"), new CancelAppointmentRequest(null), Ct);
        await spa.PutAsync<AppointmentDto>(PathOf(booked, "note"), new AppointmentNoteRequest("Gast hat abgesagt, kein Ersatz"), Ct);

        var response = await spa.Client.PostAsJsonAsync(PathOf(booked, "restore"),
            new RestoreAppointmentRequest(ExpectedVersion: seen.Version), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AppointmentStatus.Cancelled, (await spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.Id}", Ct)).Status);
    }
}
