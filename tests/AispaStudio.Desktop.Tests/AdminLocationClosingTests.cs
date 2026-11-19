using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using static AispaStudio.Desktop.Tests.StockSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// ADR 0099, Punkt 10b: Standort schließen ab Datum und löschen im Menü „⋯“ (nur Inhaberin), „Standort anlegen“ fest über der Liste und der Schalter „Nicht mehr verwenden“ beim Material.
/// Die Regeln (offene Termine, letzter Standort, Abhängiges) prüft die API; hier steht, was gesendet und angezeigt wird.
/// </summary>
public sealed class AdminLocationClosingTests
{
    private static readonly Guid SecondId = Guid.NewGuid();
    private static readonly LocationDto Salzburg = new(SecondId, "Salzburg", "Europe/Vienna", "EUR", [], "AT-5", LocationContact.Empty);
    private static readonly DateOnly Day = new(2027, 4, 1);

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location, Salzburg })
        .On($"GET /api/locations/{TestData.LocationId}/resources", new LocationResources(TestData.Location, [TestData.Room], [], [], [], []))
        .On($"GET /api/locations/{SecondId}/resources", new LocationResources(Salzburg, [], [], [], [], []))
        .On("GET /api/holidays/regions", new List<HolidayRegionDto> { new("AT-9", FiscalCountry.AT, "Wien") })
        .On("GET /api/pos/articles", new List<ArticleDto>())
        .On($"GET /api/locations/{TestData.LocationId}/online-rules", new LocationOnlineRules(true, 120, 60, 24, 15, null, true))
        .On($"GET /api/locations/{SecondId}/online-rules", new LocationOnlineRules(true, 120, 60, 24, 15, null, true))
        .On($"GET /api/rooms/{TestData.Room.Id}/start-checklist", new List<RoomStartItemDto>());

    private static async Task<LocationsViewModel> Open(StubApi api, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var locations = new LocationsViewModel(api.Client(), user ?? TestData.Owner()) { Dialogs = dialogs };
        await locations.EnsureLoadedAsync();
        return locations;
    }

    [Fact]
    public async Task Given_ein_Standort_When_die_Inhaberin_Schliessen_ab_Datum_waehlt_und_bestaetigt_Then_geht_das_Datum_an_die_API_und_die_Liste_zeigt_es()
    {
        var api = Api().On($"POST /api/locations/{TestData.LocationId}/close", TestData.Location with { ClosedFrom = Day });
        var locations = await Open(api);
        Assert.True(locations.CanRemoveLocation);
        Assert.Equal("Schließen ab Datum …", locations.CloseMenuText);

        await locations.StartCloseCommand.ExecuteAsync(null);
        Assert.True(locations.IsClosing);
        locations.CloseDate = Day;
        await locations.ConfirmCloseCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<LocationCloseInput>(Assert.Single(api.Calls, c => c.Route == $"POST /api/locations/{TestData.LocationId}/close").Body!, ApiClient.Json)!;
        Assert.Equal(Day, sent.ClosedFrom);
        Assert.False(locations.IsClosing);
        Assert.True(locations.IsClosed);
        Assert.Equal("Wieder öffnen", locations.CloseMenuText);
        Assert.Equal("geschlossen ab 01.04.2027", locations.Choices[0].Note);
        Assert.Contains("01.04.2027", locations.ClosedText, StringComparison.Ordinal);
        Assert.Null(locations.Choices[1].Note);
    }

    [Fact]
    public async Task Given_die_API_lehnt_das_Schliessen_ab_When_bestaetigt_Then_steht_die_Meldung_an_der_Zeile_und_der_Standort_bleibt_offen()
    {
        var refusal = new[] { "Ab dem 01.04.2027 liegen noch 2 Termine an diesem Standort. Bitte zuerst verschieben oder stornieren." };
        var api = Api().On($"POST /api/locations/{TestData.LocationId}/close", new { errors = new { closedFrom = refusal } }, HttpStatusCode.BadRequest);
        var locations = await Open(api);
        await locations.StartCloseCommand.ExecuteAsync(null);
        locations.CloseDate = Day;

        await locations.ConfirmCloseCommand.ExecuteAsync(null);

        Assert.Equal(refusal[0], locations.CloseDateError);
        Assert.True(locations.IsClosing);
        Assert.False(locations.IsClosed);
        Assert.Null(locations.Choices[0].Note);
    }

    [Fact]
    public async Task Given_kein_Datum_When_bestaetigt_Then_kein_Aufruf_und_der_Fehler_nennt_das_Beispiel()
    {
        var api = Api();
        var locations = await Open(api);
        await locations.StartCloseCommand.ExecuteAsync(null);
        locations.CloseDate = null;

        await locations.ConfirmCloseCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.EndsWith("/close", StringComparison.Ordinal));
        Assert.Contains("z. B. 01.12.2026", locations.CloseDateError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ein_geschlossener_Standort_When_Wieder_oeffnen_gewaehlt_wird_Then_POST_reopen_und_die_Schliessung_entfaellt()
    {
        var api = Api()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location with { ClosedFrom = Day }, Salzburg })
            .On($"POST /api/locations/{TestData.LocationId}/reopen", TestData.Location);
        var locations = await Open(api);
        Assert.True(locations.IsClosed);
        Assert.Equal("geschlossen ab 01.04.2027", locations.Choices[0].Note);

        await locations.StartCloseCommand.ExecuteAsync(null);

        Assert.Single(api.Calls, c => c.Route == $"POST /api/locations/{TestData.LocationId}/reopen");
        Assert.False(locations.IsClosed);
        Assert.False(locations.IsClosing);
        Assert.Null(locations.Choices[0].Note);
    }

    [Fact]
    public async Task Given_ein_Standort_mit_Abhaengigem_When_Loeschen_gewaehlt_wird_Then_nennt_der_Dialog_die_Liste_und_bietet_Schliessen_an()
    {
        var check = new DeletionCheckDto(false, "Am Standort hängen Termine, Belege oder Einrichtung. Sie bleiben erhalten; der Standort lässt sich nur schließen.",
            [new DependentCount("rooms", 1, "1 Raum"), new DependentCount("appointments", 3, "3 Termine")], new DeletionAlternative("close", "Schließen ab Datum …"));
        var api = Api().On($"GET /api/locations/{TestData.LocationId}/deletion-check", check);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var locations = await Open(api, dialogs: dialogs);

        await locations.DeleteLocationCommand.ExecuteAsync(null);

        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal("„Wien“ lässt sich nicht löschen", asked.Title);
        Assert.Contains("• 3 Termine", asked.Message, StringComparison.Ordinal);
        Assert.Equal("Schließen ab Datum …", asked.ConfirmText);
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.True(locations.IsClosing);
    }

    [Fact]
    public async Task Given_ein_leerer_Standort_When_Loeschen_bestaetigt_wird_Then_DELETE_und_er_ist_aus_der_Liste()
    {
        var api = Api()
            .On($"GET /api/locations/{SecondId}/deletion-check", new DeletionCheckDto(true, "Am Standort hängt nichts. Er lässt sich löschen.", []))
            .On($"DELETE /api/locations/{SecondId}", null, HttpStatusCode.NoContent);
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Confirmed);
        var locations = await Open(api, dialogs: dialogs);
        locations.SelectedChoice = locations.Choices.Single(c => c.Id == SecondId);

        await locations.DeleteLocationCommand.ExecuteAsync(null);

        Assert.Equal("„Salzburg“ löschen?", Assert.Single(dialogs.Asked).Title);
        Assert.Single(api.Calls, c => c.Route == $"DELETE /api/locations/{SecondId}");
        Assert.DoesNotContain(locations.Choices, c => c.Id == SecondId);
        Assert.Equal("Wien", locations.SelectedChoice?.Title);
    }

    [Fact]
    public async Task Given_ein_leerer_Standort_When_der_Dialog_abgebrochen_wird_Then_kein_DELETE()
    {
        var api = Api().On($"GET /api/locations/{SecondId}/deletion-check", new DeletionCheckDto(true, "Am Standort hängt nichts. Er lässt sich löschen.", []));
        var locations = await Open(api, dialogs: new FakeDialogs().Answer(ConfirmChoice.Cancelled));
        locations.SelectedChoice = locations.Choices.Single(c => c.Id == SecondId);

        await locations.DeleteLocationCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("DELETE ", StringComparison.Ordinal));
        Assert.Contains(locations.Choices, c => c.Id == SecondId);
    }

    [Fact]
    public async Task Given_die_Standortleitung_When_die_Maske_offen_ist_Then_gibt_es_weder_Schliessen_noch_Loeschen_noch_Anlegen()
    {
        var locations = await Open(Api(), TestData.Manager());

        Assert.False(locations.CanRemoveLocation);
        Assert.False(locations.StartCloseCommand.CanExecute(null));
        Assert.False(locations.DeleteLocationCommand.CanExecute(null));
        Assert.False(locations.NewLocationCommand.CanExecute(null));
    }

    [Fact]
    public async Task Given_Standort_anlegen_ueber_der_Liste_When_gewaehlt_Then_zeigt_das_Formular_einen_neuen_Standort_ohne_Menue()
    {
        var locations = await Open(Api());

        locations.NewLocationCommand.Execute(null);

        Assert.True(locations.IsNew);
        Assert.Equal("Neuer Standort", locations.HeadingText);
        Assert.False(locations.CanRemoveLocation);
        Assert.DoesNotContain(locations.Choices, c => c.Id is null);
    }

    [Fact]
    public async Task Given_ein_leerer_Betrieb_ohne_Standort_When_die_Maske_oeffnet_Then_bleibt_nur_Anlegen_ohne_Fehler()
    {
        var locations = await Open(Api().On("GET /api/locations", new List<LocationDto>()));

        Assert.True(locations.IsLoaded);
        Assert.False(locations.HasLoadError);
        Assert.Empty(locations.Choices);
        Assert.True(locations.NewLocationCommand.CanExecute(null));
        Assert.False(locations.CanRemoveLocation);
        locations.NewLocationCommand.Execute(null);
        Assert.True(locations.IsNew);
    }

    // ── Material: Nicht mehr verwenden ──

    [Fact]
    public async Task Given_ein_Material_im_Formular_When_Nicht_mehr_verwenden_gewaehlt_wird_Then_PUT_mit_IsActive_false_und_den_gespeicherten_Werten()
    {
        var api = StockSample.Api();
        var stock = new StockViewModel(api.Client(), TestData.Owner(), new FixedTime());
        await stock.EnsureLoadedAsync();
        stock.ItemForm.Edit(Towel);
        stock.ItemForm.ItemName = "Handtuch groß";
        Assert.Equal("Nicht mehr verwenden", stock.ItemForm.ActiveMenuText);

        await stock.ItemForm.ToggleActiveCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<StockItemInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/stock/items/{TowelId}").Body!, ApiClient.Json)!;
        Assert.False(sent.IsActive);
        Assert.Equal("Handtuch", sent.Name);
        Assert.Equal((Towel.Unit, Towel.UnitCost, Towel.MinimumQuantity), (sent.Unit, sent.UnitCost, sent.MinimumQuantity));
        Assert.Equal("Handtuch wird nicht mehr verwendet", stock.Toast);
        Assert.False(stock.ItemForm.IsEditing);
    }

    [Fact]
    public async Task Given_ein_nicht_mehr_verwendetes_Material_When_Wieder_verwenden_gewaehlt_wird_Then_PUT_mit_IsActive_true()
    {
        var api = StockSample.Api();
        var stock = new StockViewModel(api.Client(), TestData.Owner(), new FixedTime());
        await stock.EnsureLoadedAsync();
        stock.ItemForm.Edit(Towel with { IsActive = false });
        Assert.True(stock.ItemForm.IsItemInactive);
        Assert.Equal("Wieder verwenden", stock.ItemForm.ActiveMenuText);

        await stock.ItemForm.ToggleActiveCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<StockItemInput>(Assert.Single(api.Calls, c => c.Route == $"PUT /api/stock/items/{TowelId}").Body!, ApiClient.Json)!;
        Assert.True(sent.IsActive);
    }
}
