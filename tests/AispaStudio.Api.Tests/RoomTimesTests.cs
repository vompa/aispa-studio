using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Slice 136 (Ä318): freie Zeiten und Sperren eines Raums im Assistenten. „Jetzt“ ist Montag, der 1. März 2027, 11:15 Uhr in Wien; der Standort hat Montag bis Samstag
/// von 9 bis 20 Uhr geöffnet. Belegungen sind Sperrzeiten des Raums im Kalender, Raumsperren (ADR 0076) die gesperrten Tage.
/// </summary>
public sealed class RoomTimesTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset MondayNow = new(2027, 3, 1, 10, 15, 0, TimeSpan.Zero);

    private static readonly DateOnly Tuesday = TestSpa.Monday.AddDays(1);

    private readonly AnswerKit _kit = new(fixture);

    private static Task<RoomDto> RoomAsync(TestSpa spa, string name) =>
        spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput(name, 0), Ct);

    private static Task<BlockDto> BusyAsync(TestSpa spa, RoomDto room, DateOnly day, int from, int fromMinute, int until, int untilMinute = 0) =>
        spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Room, room.Id, TestSpa.At(from, fromMinute, day), TestSpa.At(until, untilMinute, day), "Reinigung"), Ct);

    [Fact]
    public async Task Paarraum_morgen_ab_15_Uhr_frei_nennt_die_freien_Zeiten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var room = await RoomAsync(spa, "Paarraum");
        await BusyAsync(spa, room, Tuesday, 9, 0, 15);

        var block = await _kit.AnswerAsync(spa, "Wann ist der Paarraum morgen frei?", MondayNow);

        Assert.Equal("Paarraum ist am Dienstag, 02.03.2027 frei: 15:00–20:00 Uhr.", block.Text);
        Assert.Equal("Belegung (Kalender), Öffnungszeiten und Raumsperren des Standorts Wien", block.Source);
    }

    [Fact]
    public async Task Raumsperre_zaehlt_als_belegt_und_nennt_den_Grund()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var room = await RoomAsync(spa, "Paarraum");
        await spa.PostAsync<RoomClosureDto>($"/api/locations/{spa.Location.Id}/room-closures",
            new RoomClosureInput(room.Id, Tuesday, null, Tuesday, null, RoomClosureKind.Maintenance, "Wasserschaden"), Ct);

        var block = await _kit.AnswerAsync(spa, "Wann ist der Paarraum morgen frei?", MondayNow);

        Assert.Equal("Paarraum ist am Dienstag, 02.03.2027 gesperrt (Wartung: Wasserschaden).", block.Text);
    }

    [Fact]
    public async Task Hamam_ohne_Sperre_sagt_dass_keine_Sperre_eingetragen_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await RoomAsync(spa, "Hamam");

        var block = await _kit.AnswerAsync(spa, "Ist der Hamam morgen gesperrt?", MondayNow);

        Assert.Equal("Hamam ist am Dienstag, 02.03.2027 nicht gesperrt: keine Raumsperre eingetragen.", block.Text);
    }

    [Fact]
    public async Task Hamam_mit_Sperre_nennt_den_Zeitraum_und_den_Grund()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var room = await RoomAsync(spa, "Hamam");
        await spa.PostAsync<RoomClosureDto>($"/api/locations/{spa.Location.Id}/room-closures",
            new RoomClosureInput(room.Id, Tuesday, null, Tuesday.AddDays(2), null, RoomClosureKind.Renovation, "Neue Fliesen"), Ct);

        var block = await _kit.AnswerAsync(spa, "Ist der Hamam morgen gesperrt?", MondayNow);

        Assert.Equal("Hamam ist vom 02.03.2027 bis 04.03.2027 gesperrt (Umbau: Neue Fliesen).", block.Text);
    }

    [Fact]
    public async Task Heute_beginnen_die_freien_Zeiten_erst_ab_jetzt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var room = await RoomAsync(spa, "Paarraum");
        await BusyAsync(spa, room, TestSpa.Monday, 12, 0, 13);

        var block = await _kit.AnswerAsync(spa, "Wann ist der Paarraum heute frei?", MondayNow);

        Assert.Equal("Paarraum ist am Montag, 01.03.2027 frei: 11:15–12:00 und 13:00–20:00 Uhr.", block.Text);
    }

    [Fact]
    public async Task Unbekannter_Raum_wird_nicht_gefunden()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var block = await _kit.AnswerAsync(spa, "Wann ist der Zauberraum morgen frei?", MondayNow);

        Assert.Equal(AssistantBlockKind.NotFound, block.Kind);
    }
}
