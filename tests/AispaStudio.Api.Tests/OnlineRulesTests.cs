using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class OnlineRulesTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Path(TestSpa spa) => $"/api/locations/{spa.Location.Id}/online-rules";

    private static OnlineBookingRequest Request(TestSpa spa, DateTimeOffset start) =>
        new(spa.Location.Id, spa.MassageTreatment.Id, start, new OnlineContact("Lena", "Gast", $"regeln-{Guid.NewGuid():N}@example.at", null),
            null, true, "de");

    private static LocationOnlineRules Rules(bool enabled = true, int lead = 60, int days = 365, int cancel = 48, int step = 30, string? note = null) =>
        new(enabled, lead, days, cancel, step, note, false);

    [Fact]
    public async Task Eigene_Regeln_gelten_fuer_Raster_Horizont_und_Stornofrist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = spa.OnlineClient();

        var defaults = await spa.GetAsync<LocationOnlineRules>(Path(spa), Ct);
        Assert.True(defaults.IsDefault);
        Assert.Equal((120, 24, 15), (defaults.MinLeadMinutes, defaults.CancellationDeadlineHours, defaults.SlotStepMinutes));

        var saved = await spa.PutAsync<LocationOnlineRules>(Path(spa), Rules(note: "  Bitte 10 Minuten früher kommen.  "), Ct);
        Assert.False(saved.IsDefault);
        Assert.Equal("Bitte 10 Minuten früher kommen.", saved.GuestNote);

        var availability = await client.GetFromJsonAsync<OnlineAvailability>(
            $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={TestSpa.Monday:yyyy-MM-dd}&days=1", TestSpa.Json, Ct);
        var times = availability!.Days[0].Times;
        Assert.Contains(TestSpa.At(11), times);
        Assert.All(times, t => Assert.Equal(0, t.Minute % 30));

        var booked = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(11)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var confirmation = await TestSpa.ReadAsync<OnlineBookingConfirmation>(booked, Ct);
        Assert.Equal(TestSpa.At(11).AddHours(-48), confirmation.Appointment.CancellableUntil);

        var location = Assert.Single((await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct))!);
        Assert.Equal((true, "Bitte 10 Minuten früher kommen.", 48), (location.BookingEnabled, location.GuestNote, location.CancellationDeadlineHours));

        // Horizont kürzer als der Abstand zum Testtag im März 2027: online nicht mehr buchbar.
        await spa.PutAsync<LocationOnlineRules>(Path(spa), Rules(days: 30), Ct);
        var tooFar = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(13)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooFar.StatusCode);

        var reset = await spa.Client.DeleteAsync(Path(spa), Ct);
        Assert.True((await TestSpa.ReadAsync<LocationOnlineRules>(reset, Ct)).IsDefault);
    }

    [Fact]
    public async Task Ausgeschaltete_Online_Buchung_bietet_nichts_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = spa.OnlineClient();

        await spa.PutAsync<LocationOnlineRules>(Path(spa), Rules(enabled: false), Ct);

        var location = Assert.Single((await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct))!);
        Assert.False(location.BookingEnabled);
        Assert.Empty((await client.GetFromJsonAsync<List<OnlineTreatment>>($"/api/online/locations/{spa.Location.Id}/treatments", TestSpa.Json, Ct))!);
        var availability = await client.GetAsync(
            $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={TestSpa.Monday:yyyy-MM-dd}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, availability.StatusCode);
        var booking = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(11)), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NotFound, booking.StatusCode);
    }

    [Fact]
    public async Task Regeln_werden_geprueft_und_nur_mit_Standortrecht_geaendert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        async Task<HttpStatusCode> PutAsync(HttpClient client, LocationOnlineRules rules) =>
            (await client.PutAsJsonAsync(Path(spa), rules, TestSpa.Json, Ct)).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(spa.Client, Rules(step: 7)));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(spa.Client, Rules(days: 0)));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(spa.Client, Rules(cancel: 400)));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(spa.Client, Rules(lead: -1)));
        Assert.Equal(HttpStatusCode.BadRequest, await PutAsync(spa.Client, Rules(note: new string('x', 301))));
        Assert.Equal(HttpStatusCode.Forbidden, await PutAsync(reception, Rules()));
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync(Path(spa), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, await PutAsync(other.Client, Rules()));
    }
}
