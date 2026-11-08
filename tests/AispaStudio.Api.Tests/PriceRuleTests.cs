using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Preise nach Wochentag und Tageszeit: Buchung hält den Preis zum Beginn fest, Online zeigt ihn je Zeit.</summary>
public sealed class PriceRuleTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Termin_bekommt_den_Preis_seiner_Uhrzeit_und_behaelt_ihn_beim_Verschieben()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var url = $"/api/treatments/{spa.MassageTreatment.Id}/price-rules";
        var invalid = await spa.Client.PutAsJsonAsync(url, new[]
        {
            new TreatmentPriceRuleDto("", [], new(12, 0), new(10, 0), -1m),
        }, TestSpa.Json, Ct);
        var saved = await spa.PutAsync<List<TreatmentPriceRuleDto>>(url, new[]
        {
            new TreatmentPriceRuleDto("Vormittag", [DayOfWeek.Monday, DayOfWeek.Tuesday], new(9, 0), new(12, 0), 69m),
            new TreatmentPriceRuleDto("Früh in Wien", [DayOfWeek.Monday], new(9, 0), new(10, 0), 59m, spa.Location.Id),
        }, Ct);

        var early = await spa.BookOkAsync(TestSpa.At(9, 15), Ct, spa.Anna.Id);
        var morning = await spa.BookOkAsync(TestSpa.At(11), Ct, spa.Ben.Id);
        var afternoon = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        var moved = await spa.Client.PostAsJsonAsync($"/api/appointments/{morning.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(15), spa.Ben.Id, null, null), TestSpa.Json, Ct);
        using var online = spa.OnlineClient();
        var availability = await online.GetFromJsonAsync<OnlineAvailability>(
            $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={TestSpa.Monday:yyyy-MM-dd}&days=1",
            TestSpa.Json, Ct);
        var listed = await spa.GetAsync<List<TreatmentPriceRuleDto>>(url, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(2, saved.Count);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], listed[0].Days);
        Assert.Equal((59m, 69m, 79m), (early.Price, morning.Price, afternoon.Price));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(69m, (await moved.Content.ReadFromJsonAsync<AppointmentDto>(TestSpa.Json, Ct))!.Price);
        var day = availability!.Days[0];
        Assert.NotNull(day.Prices);
        Assert.Equal(day.Times.Count, day.Prices.Count);
        Assert.All(day.Times.Zip(day.Prices), p => Assert.Equal(p.First < TestSpa.At(10) ? 59m : p.First < TestSpa.At(12) ? 69m : 79m, p.Second));
    }
}
