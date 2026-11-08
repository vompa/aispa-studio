using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>„Freie Zeit suchen“: Raum-Filter und Weitersuchen an den Folgetagen im Endpunkt der freien Zeiten.</summary>
public sealed class FreeTimeSearchTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Route(TestSpa spa, string extra = "") =>
        $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={TestSpa.Monday:yyyy-MM-dd}{extra}";

    private static DateOnly DayOf(AvailableSlot slot) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(slot.Start, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    /// <summary>Beide Personen sind am Testmontag ganztägig gesperrt; Dienst haben sie nur montags, also gibt es erst am Montag danach wieder Zeit.</summary>
    private static async Task BlockMondayAsync(TestSpa spa)
    {
        foreach (var employee in new[] { spa.Anna, spa.Ben })
        {
            var response = await spa.Client.PostAsJsonAsync("/api/blocks",
                new BlockInput(ResourceKind.Employee, employee.Id, TestSpa.At(9), TestSpa.At(17), "Schulung"), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task Ohne_minSlots_bleibt_die_Suche_bei_einem_Tag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await BlockMondayAsync(spa);

        var slots = await spa.GetAsync<List<AvailableSlot>>(Route(spa, "&days=14"), Ct);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Mit_minSlots_sucht_die_Abfrage_an_den_Folgetagen_weiter()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await BlockMondayAsync(spa);

        var slots = await spa.GetAsync<List<AvailableSlot>>(Route(spa, "&minSlots=3&days=14"), Ct);

        Assert.True(slots.Count >= 3);
        // Erster Tag mit Dienst ist der Montag danach, ganz geliefert (nicht abgeschnitten bei drei Zeiten).
        Assert.All(slots, s => Assert.Equal(TestSpa.Monday.AddDays(7), DayOf(s)));
        Assert.True(slots.Count > 3);
    }

    [Fact]
    public async Task Genug_Zeiten_am_ersten_Tag_ergeben_keine_weiteren_Tage()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var slots = await spa.GetAsync<List<AvailableSlot>>(Route(spa, "&minSlots=3&days=14"), Ct);

        Assert.True(slots.Count >= 3);
        Assert.All(slots, s => Assert.Equal(TestSpa.Monday, DayOf(s)));
    }

    [Fact]
    public async Task Die_Suche_endet_nach_der_Tagesgrenze()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await BlockMondayAsync(spa);

        // Nur die ersten sieben Tage: der nächste Dienst liegt am achten.
        var slots = await spa.GetAsync<List<AvailableSlot>>(Route(spa, "&minSlots=3&days=7"), Ct);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Der_Raum_Filter_liefert_nur_Zeiten_dieses_Raums()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var all = await spa.GetAsync<List<AvailableSlot>>(Route(spa), Ct);
        var roomB = await spa.GetAsync<List<AvailableSlot>>(Route(spa, $"&roomId={spa.RoomB.Id}"), Ct);

        Assert.Contains(all, s => s.RoomId == spa.RoomA.Id);
        Assert.NotEmpty(roomB);
        Assert.All(roomB, s => Assert.Equal(spa.RoomB.Id, s.RoomId));
    }

    [Fact]
    public async Task Raum_Filter_und_Weitersuchen_gelten_zusammen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await BlockMondayAsync(spa);

        var slots = await spa.GetAsync<List<AvailableSlot>>(Route(spa, $"&roomId={spa.RoomA.Id}&minSlots=3&days=14"), Ct);

        Assert.True(slots.Count >= 3);
        Assert.All(slots, s => Assert.Equal(spa.RoomA.Id, s.RoomId));
        Assert.All(slots, s => Assert.True(DayOf(s) > TestSpa.Monday));
    }

    [Fact]
    public async Task Unbekannte_Behandlung_ist_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.GetAsync($"/api/locations/{spa.Location.Id}/availability?treatmentId={Guid.NewGuid()}&date={TestSpa.Monday:yyyy-MM-dd}&minSlots=3", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
