using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Stammdaten je Standort: Anschrift und Kontakt (auch im Kundenbereich und in Mails), Kategorie an der Behandlung.</summary>
public sealed class LocationSettingsTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly LocationContact Vienna = new("Kärntner Straße 1", "1010", "Wien", "+43 1 234 56", "wien@example.at");

    private static LocationInput Input(LocationDto location, LocationContact? contact = null) =>
        new(location.Name, location.TimeZone, location.Currency, location.OpeningHours, location.Region, contact);

    [Fact]
    public async Task Anschrift_wird_gespeichert_und_bleibt_ohne_Angabe_unveraendert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var path = $"/api/locations/{spa.Location.Id}";

        var saved = await spa.PutAsync<LocationDto>(path, Input(spa.Location, Vienna with { Street = " Kärntner Straße 1 ", Phone = "" }), Ct);
        // Ohne Kontakt (z. B. aus einer älteren Windows-App) bleibt er, wie er ist.
        var unchanged = await spa.PutAsync<LocationDto>(path, Input(saved), Ct);
        var invalid = await spa.Client.PutAsJsonAsync(path, Input(saved, Vienna with { Email = "keine-mail" }), TestSpa.Json, Ct);

        Assert.Equal(Vienna with { Phone = null }, unchanged.Contact);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("Keine gültige E-Mail-Adresse", await invalid.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Kundenbereich_und_Bestaetigung_nennen_die_Anschrift()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<LocationDto>($"/api/locations/{spa.Location.Id}", Input(spa.Location, Vienna with { Email = null }), Ct);
        var email = $"kundin-{Guid.NewGuid():N}@example.at";
        using var client = spa.OnlineClient();

        var location = (await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct))!
            .Single(l => l.Id == spa.Location.Id);
        var response = await client.PostAsJsonAsync("/api/online/bookings", new OnlineBookingRequest(spa.Location.Id, spa.MassageTreatment.Id,
            TestSpa.At(11), new OnlineContact("Lena", "Gast", email, null), null, true, "de"), TestSpa.Json, Ct);
        var confirmation = await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);

        Assert.Equal("Kärntner Straße 1", location.Contact.Street);
        Assert.Equal("1010", confirmation.Appointment.LocationContact.PostalCode);
        Assert.Contains("Wien\nKärntner Straße 1\n1010 Wien\n+43 1 234 56", mail.Text.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Behandlung_hat_eine_Kategorie_auch_im_Kundenbereich()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var t = spa.MassageTreatment;

        var saved = await spa.PutAsync<TreatmentDto>($"/api/treatments/{t.Id}", new TreatmentInput(t.Name, t.DurationMinutes, t.PreparationMinutes,
            t.FollowUpMinutes, t.Price, t.RequiredQualificationIds, t.RoomIds, t.EquipmentIds, " Massagen "), Ct);
        using var client = spa.OnlineClient();
        var online = await client.GetFromJsonAsync<List<OnlineTreatment>>($"/api/online/locations/{spa.Location.Id}/treatments", TestSpa.Json, Ct);

        Assert.Equal("Massagen", saved.Category);
        Assert.Equal("Massagen", Assert.Single(online!).Category);
    }

    private static TreatmentInput WithPrices(TreatmentDto t, params TreatmentLocationPriceDto[] prices) => new(t.Name, t.DurationMinutes,
        t.PreparationMinutes, t.FollowUpMinutes, t.BasePrice ?? t.Price, t.RequiredQualificationIds, t.RoomIds, t.EquipmentIds, t.Category, prices);

    [Fact]
    public async Task Preis_je_Standort_gilt_fuer_Buchung_Kalender_und_Kundenbereich()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var t = spa.MassageTreatment;

        var saved = await spa.PutAsync<TreatmentDto>($"/api/treatments/{t.Id}", WithPrices(t, new TreatmentLocationPriceDto(spa.Location.Id, 89m)), Ct);
        var resources = await spa.GetAsync<LocationResources>($"/api/locations/{spa.Location.Id}/resources", Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct);
        using var client = spa.OnlineClient();
        var online = await client.GetFromJsonAsync<List<OnlineTreatment>>($"/api/online/locations/{spa.Location.Id}/treatments", TestSpa.Json, Ct);
        // Ohne Angabe bleiben die Preise; gleich dem Grundpreis heißt keine Abweichung.
        var kept = await spa.PutAsync<TreatmentDto>($"/api/treatments/{t.Id}", WithPrices(saved) with { LocationPrices = null }, Ct);
        var cleared = await spa.PutAsync<TreatmentDto>($"/api/treatments/{t.Id}", WithPrices(saved, new TreatmentLocationPriceDto(spa.Location.Id, 79m)), Ct);

        Assert.Equal(79m, saved.Price);
        Assert.Equal([new TreatmentLocationPriceDto(spa.Location.Id, 89m)], saved.LocationPrices!);
        Assert.Equal(89m, resources.Treatments.Single(x => x.Id == t.Id).Price);
        Assert.Equal(79m, resources.Treatments.Single(x => x.Id == t.Id).BasePrice);
        Assert.Equal(89m, appointment.Price);
        Assert.Equal(89m, Assert.Single(online!).Price);
        Assert.Single(kept.LocationPrices!);
        Assert.Empty(cleared.LocationPrices!);
    }

    [Fact]
    public async Task Preis_fuer_unbekannten_Standort_wird_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await spa.Client.PutAsJsonAsync($"/api/treatments/{spa.MassageTreatment.Id}",
            WithPrices(spa.MassageTreatment, new TreatmentLocationPriceDto(Guid.NewGuid(), 89m)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Unbekannter Standort", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }
}
