using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Schließtage (ADR 0073, Nachtrag Runde R03): An einem Tag ohne Öffnungszeit, in einer ganztägigen Schließzeit und an einem Tag ohne Dienst
/// trägt die Rezeption alles ein; die Antwort trägt nur den Hinweis. Gäste bekommen online weiter keine Zeiten.
/// </summary>
public sealed class ClosedDayBookingTests(ApiFixture fixture)
{
    private const string Notice = "Der Standort hat an diesem Tag geschlossen.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Der Sonntag nach dem Testmontag: kein Eintrag im Wochenplan des Standorts.</summary>
    private static DateOnly Sunday => TestSpa.Monday.AddDays(6);

    /// <summary>Der Dienstag nach dem Testmontag: Öffnungszeit 9 bis 20 Uhr, aber niemand hat Dienst (die Testpersonen arbeiten nur montags).</summary>
    private static DateOnly Tuesday => TestSpa.Monday.AddDays(1);

    private static async Task<int> OnlineTimesAsync(TestSpa spa, DateOnly day)
    {
        var json = await spa.OnlineClient().GetStringAsync(
            $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={day:yyyy-MM-dd}&days=1", Ct);
        return JsonSerializer.Deserialize<OnlineAvailability>(json, TestSpa.Json)!.Days[0].Times.Count;
    }

    [Fact]
    public async Task Given_ein_Schliesstag_When_die_Rezeption_einen_Termin_bucht_Then_wird_er_gespeichert_und_die_Antwort_traegt_den_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.BookAsync(TestSpa.At(10, 0, Sunday), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booked = await TestSpa.ReadAsync<AppointmentDto>(response, Ct);
        Assert.Equal([Notice], booked.Warnings);
        Assert.Equal(TestSpa.At(10, 0, Sunday), booked.Start);
        var day = await spa.GetAsync<CalendarDay>($"/api/locations/{spa.Location.Id}/calendar?date={Sunday:yyyy-MM-dd}", Ct);
        Assert.Contains(day.Appointments, a => a.Id == booked.Id && a.Status == AppointmentStatus.Booked);
    }

    [Fact]
    public async Task Given_ein_Schliesstag_When_ein_Gast_online_Zeiten_abfragt_Then_gibt_es_keine_buchbaren_Zeiten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10, 0, Sunday), Ct);

        Assert.Equal(0, await OnlineTimesAsync(spa, Sunday));
        Assert.Equal(0, await OnlineTimesAsync(spa, Tuesday));
        Assert.Empty(await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={Sunday:yyyy-MM-dd}", Ct));
    }

    [Fact]
    public async Task Given_eine_ganztaegige_Schliesszeit_When_die_Rezeption_bucht_Then_wird_gespeichert_mit_Hinweis_und_online_bleibt_es_zu()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = TestSpa.Monday.AddDays(7);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(day, kind: ClosureKind.Vacation));

        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, day), Ct);

        Assert.Equal([Notice], booked.Warnings);
        Assert.Equal(0, await OnlineTimesAsync(spa, day));
    }

    [Fact]
    public async Task Given_ein_Tag_ohne_Dienst_When_die_Rezeption_bucht_Then_wird_gespeichert_mit_Hinweis_und_eine_Person_gewaehlt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, Tuesday), Ct);

        Assert.Equal([Notice], booked.Warnings);
        Assert.Contains(booked.EmployeeId, new[] { spa.Anna.Id, spa.Ben.Id });
        var explicitly = await spa.BookOkAsync(TestSpa.At(14, 0, Tuesday), Ct, employee: spa.Ben.Id, room: spa.RoomB.Id);
        Assert.Equal((spa.Ben.Id, spa.RoomB.Id), (explicitly.EmployeeId, explicitly.RoomId));
    }

    [Fact]
    public async Task Ein_offener_Tag_mit_Dienst_bekommt_keinen_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var booked = await spa.BookOkAsync(TestSpa.At(10, 0), Ct);

        Assert.True(booked.Warnings is null or { Count: 0 });
    }

    [Fact]
    public async Task Am_Schliesstag_gilt_die_Doppelbelegung_weiter()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10, 0, Sunday), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id);

        var again = await spa.BookAsync(TestSpa.At(10, 0, Sunday), Ct, employee: spa.Anna.Id, room: spa.RoomA.Id);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Ein_halber_Tag_Schliesszeit_lehnt_die_Rezeption_weiter_ab()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var day = TestSpa.Monday.AddDays(7);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(day, kind: ClosureKind.Outage, fromTime: new TimeOnly(13, 0)));

        Assert.Equal(HttpStatusCode.Conflict, (await spa.BookAsync(TestSpa.At(15, 0, day), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(10, 0, day), Ct)).StatusCode);
    }

    [Fact]
    public async Task Ein_Tag_ohne_Dienst_hebt_die_Oeffnungszeit_und_eine_halbe_Schliesszeit_nicht_auf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(Tuesday.AddDays(7), kind: ClosureKind.Outage, fromTime: new TimeOnly(13, 0)));

        Assert.Equal(HttpStatusCode.Conflict, (await spa.BookAsync(TestSpa.At(3, 0, Tuesday), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await spa.BookAsync(TestSpa.At(15, 0, Tuesday.AddDays(7)), Ct)).StatusCode);
    }

    [Fact]
    public async Task Am_Tag_ohne_Dienst_waehlt_die_Rezeption_keine_Person_im_Urlaub()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{spa.Anna.Id}/{Tuesday:yyyy-MM-dd}",
            new RosterDayInput([new RosterEntry(RosterKind.Vacation, null, null, null)], null), Ct);

        var booked = await spa.BookOkAsync(TestSpa.At(10, 0, Tuesday), Ct);

        Assert.Equal(spa.Ben.Id, booked.EmployeeId);
    }

    [Fact]
    public async Task Ein_Termin_laesst_sich_auf_einen_Schliesstag_verschieben_mit_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var booked = await spa.BookOkAsync(TestSpa.At(10, 0), Ct);

        var response = await spa.Client.PostAsJsonAsync($"/api/appointments/{booked.Id}/move", new MoveAppointmentRequest(TestSpa.At(11, 0, Sunday), null, null, null), TestSpa.Json, Ct);

        var moved = await TestSpa.ReadAsync<AppointmentDto>(response, Ct);
        Assert.Equal(TestSpa.At(11, 0, Sunday), moved.Start);
        Assert.Equal([Notice], moved.Warnings);
    }

    [Fact]
    public async Task Sperrzeit_und_Notiz_lassen_sich_an_einem_Schliesstag_eintragen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await spa.Client.PostAsJsonAsync("/api/blocks",
            new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(10, 0, Sunday), TestSpa.At(11, 0, Sunday), "Inventur"), TestSpa.Json, Ct);
        var note = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/calendar-notes",
            new CalendarNoteInput(Sunday, "Schlüssel abholen", ResourceKind.Employee, spa.Anna.Id, TestSpa.At(12, 0, Sunday)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, block.StatusCode);
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
    }

    [Fact]
    public async Task Given_ein_Schliesstag_When_die_Rezeption_einen_Sauna_Eintritt_bucht_Then_wird_er_gespeichert_mit_Hinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var sauna = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Sauna", 0, 3, IsArea: true), Ct);

        var response = await spa.Client.PostAsJsonAsync("/api/area-visits",
            new AreaVisitRequest(spa.Location.Id, sauna.Id, spa.Customer.Id, TestSpa.At(10, 0, Sunday), TestSpa.At(12, 0, Sunday), 1), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([Notice], (await TestSpa.ReadAsync<AreaVisitDto>(response, Ct)).Warnings);
    }
}
