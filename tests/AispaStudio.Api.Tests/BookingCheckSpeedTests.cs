using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Allein: CommandCounterTests leert mit NpgsqlConnection.ClearAllPools() alle Pools des Prozesses, und die Wanduhrprüfung braucht Ruhe.</summary>
[CollectionDefinition(DisableParallelization = true)]
public sealed class BookingCheckAlone;

/// <summary>
/// Prüfung beim Buchen schneller (Slice 78, Ä84): Eine Zeit außerhalb der Öffnungszeit oder in einer Schließzeit lehnt der Server ab, bevor er den Planungskontext lädt.
/// Öffnungszeiten und Schließzeiten kommen aus einem Zwischenspeicher je Standort und Woche, der bei jeder Änderung verfällt. Die Zahl der Datenbankbefehle ist das
/// stabile Maß (vor Slice 78: 22 je abgelehnter Buchung); die Wanduhr hängt unter Last von parallelen Läufen ab und gilt hier nur für den Median von fünf Läufen.
/// </summary>
[Collection(typeof(BookingCheckAlone))]
public sealed class BookingCheckSpeedTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly NextMonday => TestSpa.Monday.AddDays(7);

    private static DateOnly Tuesday => TestSpa.Monday.AddDays(1);

    private static async Task<string> RejectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!.Message;
    }

    [Fact]
    public async Task Given_ein_Schliesstag_When_der_Termin_geprueft_wird_Then_liegt_die_Antwort_in_unter_300_Millisekunden_vor()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(NextMonday, kind: ClosureKind.Outage, fromTime: new TimeOnly(13, 0)));
        // Erster Aufruf füllt den Zwischenspeicher.
        Assert.Equal("Der Standort ist in dieser Zeit geschlossen (Ausfall).", await RejectedAsync(await spa.BookAsync(TestSpa.At(15, 0, NextMonday), Ct)));

        using var counter = new CommandCounter();
        spa.Client.DefaultRequestHeaders.Add("traceparent", counter.Traceparent);
        var message = await RejectedAsync(await spa.BookAsync(TestSpa.At(15, 0, NextMonday), Ct));
        var commands = counter.Count;

        var times = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await RejectedAsync(await spa.BookAsync(TestSpa.At(15, 0, NextMonday), Ct));
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        Assert.Equal("Der Standort ist in dieser Zeit geschlossen (Ausfall).", message);
        Assert.InRange(commands, 1, 6);
        Assert.InRange(times.Order().ElementAt(2), 0, 299);
    }

    [Fact]
    public async Task Given_geaenderte_Schliesszeiten_When_erneut_geprueft_wird_Then_gilt_der_neue_Stand()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(NextMonday, kind: ClosureKind.Outage, fromTime: new TimeOnly(13, 0)));
        await RejectedAsync(await spa.BookAsync(TestSpa.At(14, 0, NextMonday), Ct));

        // Die Schließzeit beginnt jetzt erst um 16 Uhr: 14 Uhr ist wieder offen.
        await spa.PutAsync<ClosureDto>($"{ClosureTests.Path(spa)}/{closure.Id}", ClosureTests.Input(NextMonday, kind: ClosureKind.Outage, fromTime: new TimeOnly(16, 0)), Ct);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(14, 0, NextMonday), Ct)).StatusCode);
        await RejectedAsync(await spa.BookAsync(TestSpa.At(16, 0, NextMonday), Ct));

        // Gelöscht: auch 16 Uhr ist offen.
        Assert.Equal(HttpStatusCode.NoContent, (await spa.Client.DeleteAsync($"{ClosureTests.Path(spa)}/{closure.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(16, 0, NextMonday), Ct)).StatusCode);
    }

    [Fact]
    public async Task Given_eine_neue_Schliesszeit_When_erneut_geprueft_wird_Then_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Eine Ablehnung außerhalb der Öffnungszeit füllt den Zwischenspeicher der Woche ohne Schließzeit.
        Assert.Equal("Der Standort hat zu dieser Zeit geschlossen.", await RejectedAsync(await spa.BookAsync(TestSpa.At(3, 0, NextMonday), Ct)));

        await ClosureTests.CreateAsync(spa, ClosureTests.Input(NextMonday, kind: ClosureKind.Vacation, fromTime: new TimeOnly(13, 0)));

        Assert.Equal("Der Standort ist in dieser Zeit geschlossen (Betriebsurlaub).", await RejectedAsync(await spa.BookAsync(TestSpa.At(14, 0, NextMonday), Ct)));
    }

    [Fact]
    public async Task Neue_Oeffnungszeiten_gelten_sofort()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RejectedAsync(await spa.BookAsync(TestSpa.At(20, 30, Tuesday), Ct));

        var location = spa.Location;
        await spa.PutAsync<LocationDto>($"/api/locations/{location.Id}",
            new LocationInput(location.Name, location.TimeZone, location.Currency, [.. location.OpeningHours.Select(o => o with { Closes = new TimeOnly(22, 0) })]), Ct);

        // Dienstag hat niemand Dienst: in der neuen Öffnungszeit trägt die Rezeption mit Hinweis ein.
        var booked = await spa.BookOkAsync(TestSpa.At(20, 30, Tuesday), Ct);
        Assert.Equal(TestSpa.At(20, 30, Tuesday), booked.Start);
    }
}
