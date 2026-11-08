using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Feiertage ändern nichts an Buchung und Öffnungszeiten (ADR 0075, Nachtrag Ä50: „Feiertage sind für Spa-Betriebe die umsatzstärksten Tage.“): freie Zeiten,
/// Buchung, Serienvorschau und Kundenbereich verhalten sich an einem Feiertag wie an einem Werktag. Ostermontag 2027 ist der 29. März, ein Montag wie der 1. März.
/// </summary>
public sealed class HolidayNeutralityTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly EasterMonday = new(2027, 3, 29);

    private static List<TimeOnly> TimesOf(IEnumerable<DateTimeOffset> starts) =>
        [.. starts.Select(s => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(s, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime))];

    [Fact]
    public async Task Freie_Zeitsuche_und_Buchung_an_einem_Feiertag_gehen_wie_an_einem_Werktag()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var holidays = await spa.GetAsync<List<PublicHolidayDto>>($"/api/locations/{spa.Location.Id}/holidays?year=2027", Ct);
        Assert.Contains(new PublicHolidayDto(EasterMonday, "Ostermontag"), holidays);

        async Task<List<TimeOnly>> SlotsAsync(DateOnly day) => TimesOf((await spa.GetAsync<List<AvailableSlot>>(
            $"/api/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&date={day:yyyy-MM-dd}", Ct)).Select(s => s.Start));

        var workday = await SlotsAsync(TestSpa.Monday);
        var holiday = await SlotsAsync(EasterMonday);
        Assert.NotEmpty(workday);
        Assert.Equal(workday, holiday);

        var booked = await spa.BookOkAsync(TestSpa.At(10, date: EasterMonday), Ct, employee: spa.Anna.Id);
        Assert.Equal(TestSpa.At(10, date: EasterMonday), booked.Start);
    }

    [Fact]
    public async Task Die_Serienvorschau_laesst_einen_Termin_am_Feiertag_frei_und_angehakt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var first = await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);

        var preview = await spa.PostAsync<List<SeriesOccurrence>>($"/api/appointments/{first.Id}/series/preview", new SeriesInput(4, Count: 1), Ct);

        Assert.Equal(EasterMonday, preview[0].Date);
        Assert.Equal(SeriesOccurrenceState.Free, preview[0].State);
        Assert.Null(preview[0].Problem);
    }

    [Fact]
    public async Task Der_Kundenbereich_zeigt_an_einem_Feiertag_Zeiten_und_keinen_Feiertagshinweis()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = spa.OnlineClient();

        async Task<string> JsonAsync(DateOnly day) => await client.GetStringAsync(
            $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={day:yyyy-MM-dd}&days=1", Ct);

        var workday = JsonSerializer.Deserialize<OnlineAvailability>(await JsonAsync(TestSpa.Monday), TestSpa.Json)!;
        var holidayJson = await JsonAsync(EasterMonday);
        var holiday = JsonSerializer.Deserialize<OnlineAvailability>(holidayJson, TestSpa.Json)!;

        Assert.Equal(TimesOf(workday.Days[0].Times), TimesOf(holiday.Days[0].Times));
        Assert.NotEmpty(holiday.Days[0].Times);
        Assert.DoesNotContain("holiday", holidayJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("feiertag", holidayJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ostermontag", holidayJson, StringComparison.Ordinal);

        var locations = await client.GetStringAsync("/api/online/locations", Ct);
        Assert.DoesNotContain("holiday", locations, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("feiertag", locations, StringComparison.OrdinalIgnoreCase);
    }
}
