using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Standort schließen ab Datum und Standort löschen (ADR 0099, Punkt 10b): ab dem Tag keine Buchung mehr, auch nicht von der Rezeption; abgelehnt, solange offene Termine ab dem Tag
/// daran liegen oder es der letzte offene Standort wäre; wieder öffnen; Löschen nur ohne Abhängiges, sonst 409 mit dem Weg „Schließen ab Datum“.
/// </summary>
public sealed class LocationClosingTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly NextMonday = TestSpa.Monday.AddDays(7);

    private static string Path(Guid locationId, string action) => $"/api/locations/{locationId}/{action}";

    private static Task<HttpResponseMessage> CloseAsync(TestSpa spa, Guid locationId, DateOnly from) =>
        spa.Client.PostAsJsonAsync(Path(locationId, "close"), new LocationCloseInput(from), TestSpa.Json, Ct);

    [Fact]
    public async Task Given_zwei_Standorte_When_einer_ab_dem_naechsten_Montag_geschlossen_wird_Then_bucht_er_davor_und_nicht_danach()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);

        var response = await CloseAsync(spa, spa.Location.Id, NextMonday);
        var location = await TestSpa.ReadAsync<LocationDto>(response, Ct);

        Assert.Equal(NextMonday, location.ClosedFrom);
        Assert.Contains(await spa.GetAsync<List<LocationDto>>("/api/locations", Ct), l => l.Id == spa.Location.Id && l.ClosedFrom == NextMonday);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(10), Ct)).StatusCode);
        var refused = await spa.BookAsync(TestSpa.At(10, 0, NextMonday), Ct);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal($"Der Standort ist ab dem {NextMonday:dd.MM.yyyy} geschlossen.", (await refused.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct))!.Message);
        Assert.Equal(HttpStatusCode.Conflict, (await spa.BookAsync(TestSpa.At(10, 0, NextMonday.AddDays(14)), Ct)).StatusCode);
    }

    [Fact]
    public async Task Given_geschlossener_Standort_When_er_wieder_geoeffnet_wird_Then_ist_Buchen_wieder_moeglich()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        _ = await CloseAsync(spa, spa.Location.Id, NextMonday);
        Assert.Equal(HttpStatusCode.Conflict, (await spa.BookAsync(TestSpa.At(10, 0, NextMonday), Ct)).StatusCode);

        var reopened = await TestSpa.ReadAsync<LocationDto>(await spa.Client.PostAsync(Path(spa.Location.Id, "reopen"), null, Ct), Ct);

        Assert.Null(reopened.ClosedFrom);
        Assert.Equal(HttpStatusCode.Created, (await spa.BookAsync(TestSpa.At(10, 0, NextMonday), Ct)).StatusCode);
    }

    [Fact]
    public async Task Given_offener_Termin_nach_dem_Tag_When_geschlossen_wird_Then_abgelehnt_mit_Zahl_und_Termin_vor_dem_Tag_stoert_nicht()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10, 0, NextMonday), Ct);

        var early = await CloseAsync(spa, spa.Location.Id, TestSpa.Monday);
        var problem = await early.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct);
        var later = await CloseAsync(spa, spa.Location.Id, NextMonday.AddDays(1));

        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Equal([$"Ab dem {TestSpa.Monday:dd.MM.yyyy} liegen noch 2 Termine an diesem Standort. Bitte zuerst verschieben oder stornieren."], problem!.Errors["closedFrom"]);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
    }

    [Fact]
    public async Task Given_nur_ein_offener_Standort_When_er_geschlossen_wird_Then_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var response = await CloseAsync(spa, spa.Location.Id, NextMonday);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["Der letzte offene Standort lässt sich nicht schließen."], problem!.Errors["closedFrom"]);
        Assert.Equal(HttpStatusCode.NotFound, (await CloseAsync(spa, Guid.NewGuid(), NextMonday)).StatusCode);
    }

    [Fact]
    public async Task Given_Standort_mit_Raeumen_und_Terminen_When_geprueft_und_geloescht_Then_409_mit_Liste_und_dem_Weg_Schliessen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct);

        var check = await spa.GetAsync<DeletionCheckDto>(Path(spa.Location.Id, "deletion-check"), Ct);
        var delete = await spa.Client.DeleteAsync($"/api/locations/{spa.Location.Id}", Ct);
        var conflict = await delete.Content.ReadFromJsonAsync<DeletionCheckDto>(TestSpa.Json, Ct);

        Assert.False(check.CanDelete);
        Assert.Contains("2 Räume", check.Dependents.Select(d => d.Label));
        Assert.Contains("1 Termin", check.Dependents.Select(d => d.Label));
        Assert.Equal(new DeletionAlternative("close", "Schließen ab Datum …"), check.Alternative);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal(check.Dependents.Select(d => d.Label), conflict!.Dependents.Select(d => d.Label));
        Assert.Contains(await spa.GetAsync<List<LocationDto>>("/api/locations", Ct), l => l.Id == other.Id);
    }

    [Fact]
    public async Task Given_neuer_Standort_ohne_Abhaengiges_When_geloescht_Then_ist_er_weg_und_der_letzte_Standort_bleibt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var empty = await spa.CreateLocationAsync("Ohne alles", [DayOfWeek.Monday], Ct);

        var check = await spa.GetAsync<DeletionCheckDto>(Path(empty.Id, "deletion-check"), Ct);
        var delete = await spa.Client.DeleteAsync($"/api/locations/{empty.Id}", Ct);

        Assert.True(check.CanDelete);
        Assert.Empty(check.Dependents);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(await spa.GetAsync<List<LocationDto>>("/api/locations", Ct), l => l.Id == empty.Id);

        var last = await spa.GetAsync<DeletionCheckDto>(Path(spa.Location.Id, "deletion-check"), Ct);
        Assert.False(last.CanDelete);
        Assert.Equal("Der letzte Standort lässt sich nicht löschen.", last.Summary);
        Assert.Equal(HttpStatusCode.NotFound, (await spa.Client.GetAsync(Path(empty.Id, "deletion-check"), Ct)).StatusCode);
    }

    [Fact]
    public async Task Given_Standortleitung_und_Rezeption_When_sie_schliessen_oder_loeschen_wollen_Then_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.CreateLocationAsync("Zweiter Standort", [DayOfWeek.Monday], Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id, other.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        foreach (var client in new[] { manager, reception })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Path(other.Id, "close"), new LocationCloseInput(NextMonday), TestSpa.Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(Path(other.Id, "reopen"), null, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path(other.Id, "deletion-check"), Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/locations/{other.Id}", Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task Given_ein_eingerichteter_Betrieb_When_der_Stand_der_Einrichtung_gelesen_wird_Then_sind_alle_Schritte_ausser_der_Kassa_erledigt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var progress = await spa.GetAsync<SetupProgressDto>("/api/setup/progress", Ct);

        Assert.Equal(["location", "hours", "rooms", "treatments", "people", "register"], progress.Steps.Select(s => s.Key));
        Assert.Equal(["location", "hours", "rooms", "treatments", "people"], progress.Steps.Where(s => s.Done).Select(s => s.Key));
        Assert.Equal(("masterData", "kassa"), (progress.Steps[^1].Page, progress.Steps[^1].Tab));
    }

    [Fact]
    public async Task Given_ein_leerer_Betrieb_When_der_Stand_der_Einrichtung_gelesen_wird_Then_ist_kein_Schritt_erledigt()
    {
        using var owner = await TestUsers.ClientAsync(fixture, Ct);

        var progress = await TestSpa.ReadAsync<SetupProgressDto>(await owner.GetAsync("/api/setup/progress", Ct), Ct);

        Assert.Equal(6, progress.Steps.Count);
        Assert.All(progress.Steps, step => Assert.False(step.Done, step.Key));
    }

    [Fact]
    public async Task Given_Standortleitung_und_Rezeption_When_sie_den_Stand_der_Einrichtung_lesen_wollen_Then_403()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/setup/progress", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/setup/progress", Ct)).StatusCode);
    }
}
