using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Eingabefelder nach Fluent 2 (Design-Brief 5.16 bis 5.20) auf Ebene der ViewModels: eine falsche Uhrzeit ist ein Fehlertext, nie eine Ausnahme
/// (Wunsch des Auftraggebers Ä20), tolerante Eingaben wie „930“ werden gelesen.
/// </summary>
public sealed class FieldInputTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => Vienna;
    }

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location })
        .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
        .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
        .On("POST /api/blocks", new BlockDto(Guid.NewGuid(), ResourceKind.Employee, TestData.Anna.Id, TestData.At(12), TestData.At(13), "Arzt"), HttpStatusCode.Created);

    private static async Task<(CalendarViewModel Calendar, StubApi Api)> OpenAsync()
    {
        var api = Api();
        var vm = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedTime());
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return (vm, api);
    }

    /// <summary>Regressionstest Ä20: früher warf „abc“ im Ende-Feld eine FormatException (ungültiges Einzelzeichen-Format „H“).</summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("24")]
    [InlineData("9:60")]
    [InlineData("12345")]
    public async Task Falsche_Uhrzeit_im_Panel_ergibt_einen_Fehlertext_statt_einer_Ausnahme(string text)
    {
        var (calendar, api) = await OpenAsync();
        calendar.OpenBlockAt(10, calendar.Hours.Single(h => h.Label == "12:00").Top + 1);
        var entry = calendar.Entry!;
        Assert.Equal(CalendarEntryKind.Block, entry.Kind);
        entry.Text = "Arzt";

        entry.EndText = text;
        await entry.SaveAsync();
        Assert.NotNull(entry.Error);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/blocks");

        entry.EndText = "13:00";
        entry.StartText = text;
        await entry.SaveAsync();
        Assert.NotNull(entry.Error);
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/blocks");
    }

    [Theory]
    [InlineData("1200", "1300")]
    [InlineData("12", "13")]
    [InlineData("12.00", "13,00")]
    [InlineData("12:00", "13:0")]
    public async Task Tolerante_Uhrzeiten_im_Panel_werden_gelesen(string from, string until)
    {
        var (calendar, api) = await OpenAsync();
        calendar.OpenBlockAt(10, calendar.Hours.Single(h => h.Label == "10:00").Top + 1);
        var entry = calendar.Entry!;
        entry.Text = "Arzt";
        entry.StartText = from;
        entry.EndText = until;

        await entry.SaveAsync();

        Assert.Null(entry.Error);
        var body = api.Calls.Single(c => c.Route == "POST /api/blocks").Body!;
        Assert.Contains("\"from\":\"2027-03-01T11:00:00", body, StringComparison.Ordinal);
        Assert.Contains("\"until\":\"2027-03-01T12:00:00", body, StringComparison.Ordinal);
    }
}
