using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>R05 Slice 95 (Ä205): native Maske „Standorte &amp; Räume“ mit Öffnungszeiten, Räumen, Tagesstart-Kontrolle, Geräten und Online-Buchung.</summary>
public sealed class AdminLocationsTests
{
    private static readonly Guid SecondId = Guid.NewGuid();
    private static readonly LocationDto Salzburg = new(SecondId, "Salzburg", "Europe/Vienna", "EUR", [], "AT-5", LocationContact.Empty);
    private static readonly RoomDto Sauna = new(Guid.NewGuid(), TestData.LocationId, "Sauna", 15, 12, true, Guid.NewGuid());
    private static readonly ArticleDto Entry = new(Sauna.AdmissionArticleId!.Value, "Eintritt Sauna", 12m, TaxCategory.Normal, Guid.NewGuid());
    private static readonly LocationOnlineRules Rules = new(true, 120, 60, 24, 15, null, true);

    private static StubApi Api() => new StubApi()
        .On("GET /api/locations", new List<LocationDto> { TestData.Location, Salzburg })
        .On($"GET /api/locations/{TestData.LocationId}/resources",
            new LocationResources(TestData.Location, [TestData.Room, Sauna], [new EquipmentDto(Guid.NewGuid(), TestData.LocationId, "Liege")], [], [], []))
        .On("GET /api/holidays/regions", new List<HolidayRegionDto> { new("AT-9", FiscalCountry.AT, "Wien"), new("AT-5", FiscalCountry.AT, "Salzburg") })
        .On("GET /api/pos/articles", new List<ArticleDto> { Entry })
        .On($"GET /api/locations/{TestData.LocationId}/online-rules", Rules)
        .On($"GET /api/rooms/{TestData.Room.Id}/start-checklist", new List<RoomStartItemDto> { new(Guid.NewGuid(), "Fenster öffnen") })
        .OnJson($"PUT /api/locations/{TestData.LocationId}", body => ToDto(TestData.LocationId, body))
        .OnJson("POST /api/locations", body => ToDto(Guid.NewGuid(), body));

    private static LocationDto ToDto(Guid id, string? body)
    {
        var input = JsonSerializer.Deserialize<LocationInput>(body!, ApiClient.Json)!;
        return new LocationDto(id, input.Name, input.TimeZone, input.Currency, input.OpeningHours, input.Region, input.Contact, input.Company);
    }

    private static async Task<LocationsViewModel> Open(StubApi? api = null, CurrentUser? user = null, FakeDialogs? dialogs = null)
    {
        var locations = new LocationsViewModel((api ?? Api()).Client(), user ?? TestData.Owner()) { Dialogs = dialogs };
        await locations.EnsureLoadedAsync();
        return locations;
    }

    private static T Sent<T>(StubApi api, string route) => JsonSerializer.Deserialize<T>(api.Calls.Single(c => c.Route == route).Body!, ApiClient.Json)!;

    [Fact]
    public async Task Given_ein_Standort_When_die_Inhaberin_den_Samstag_auf_9_bis_14_Uhr_setzt_und_speichert_Then_geht_Samstag_09_00_bis_14_00_an_die_API()
    {
        var api = Api();
        var locations = await Open(api);
        Assert.Equal("Wien", locations.SelectedChoice?.Title);
        var saturday = locations.Days.Single(d => d.Day == DayOfWeek.Saturday);
        Assert.False(saturday.IsOpen);

        saturday.IsOpen = true;
        saturday.OpensText = "09:00";
        saturday.ClosesText = "14:00";
        Assert.True(locations.HasUnsavedChanges);
        Assert.True(await locations.SaveChangesAsync());

        var input = Sent<LocationInput>(api, $"PUT /api/locations/{TestData.LocationId}");
        Assert.Equal(
            [new OpeningHours(DayOfWeek.Monday, new(9, 0), new(20, 0)), new OpeningHours(DayOfWeek.Saturday, new(9, 0), new(14, 0))],
            input.OpeningHours);
        Assert.False(locations.HasUnsavedChanges);
        Assert.Equal("Gespeichert.", locations.StatusText);
    }

    [Fact]
    public async Task Given_ein_Standort_ohne_Namen_When_gespeichert_wird_Then_kein_Aufruf_und_der_Fehler_steht_am_Feld()
    {
        var api = Api();
        var locations = await Open(api);

        locations.Name = "  ";

        Assert.False(await locations.SaveChangesAsync());
        Assert.DoesNotContain(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal));
        Assert.Equal("Der Name des Standorts fehlt. Trage ihn ein, damit Team und Gäste den Standort erkennen.", locations.NameError);
        Assert.Equal(LocationsViewModel.NameField, locations.FocusTarget);
    }

    [Fact]
    public async Task Given_ungespeicherter_Standort_When_ein_anderer_Standort_gewaehlt_wird_Then_fragt_die_Maske_Aenderungen_speichern()
    {
        var dialogs = new FakeDialogs().Answer(ConfirmChoice.Cancelled).Answer(ConfirmChoice.Alternative);
        var locations = await Open(dialogs: dialogs);
        var vienna = locations.SelectedChoice;
        locations.Name = "Wien Mitte";

        // Abbrechen: bleibt beim Standort, die Eingabe bleibt.
        locations.SelectedChoice = locations.Choices.Single(c => c.Title == "Salzburg");
        await Task.Yield();
        Assert.Equal(vienna, locations.SelectedChoice);
        Assert.Equal("Wien Mitte", locations.Name);

        // Verwerfen: wechselt, der Standort steht wieder auf dem gespeicherten Stand.
        locations.SelectedChoice = locations.Choices.Single(c => c.Title == "Salzburg");
        await Task.Yield();
        Assert.Equal("Salzburg", locations.SelectedChoice?.Title);
        Assert.Equal("Salzburg", locations.Name);
        Assert.Equal(["Änderungen speichern?", "Änderungen speichern?"], dialogs.AssertRules().Asked.Select(a => a.Title));
    }

    [Fact]
    public async Task Given_ein_neuer_Raum_When_hinzugefuegt_wird_Then_POST_mit_Raumpflege_Plaetzen_und_Bereich()
    {
        var api = Api().OnJson($"POST /api/locations/{TestData.LocationId}/rooms", body =>
        {
            var input = JsonSerializer.Deserialize<RoomInput>(body!, ApiClient.Json)!;
            return new RoomDto(Guid.NewGuid(), TestData.LocationId, input.Name, input.CleaningMinutes, input.Places ?? 1, input.IsArea ?? false, input.AdmissionArticleId);
        });
        var locations = await Open(api);
        var row = locations.NewRoom!;
        Assert.False(row.CanSave);

        row.Name = "Ruheraum";
        row.CleaningMinutes = 5;
        row.Places = 20;
        row.IsArea = true;
        row.Admission = row.Articles.Single(a => a.Id == Entry.Id);
        Assert.True(row.CanSave);
        await row.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new RoomInput("Ruheraum", 5, 20, true, Entry.Id), Sent<RoomInput>(api, $"POST /api/locations/{TestData.LocationId}/rooms"));
        Assert.Contains(locations.Rooms, r => r.Name == "Ruheraum");
        Assert.Equal("", locations.NewRoom!.Name);
    }

    [Fact]
    public async Task Given_ein_Raum_When_er_deaktiviert_wird_Then_DELETE()
    {
        var api = Api().On($"DELETE /api/rooms/{Sauna.Id}", null, System.Net.HttpStatusCode.NoContent);
        var locations = await Open(api);

        await locations.Rooms.Single(r => r.Id == Sauna.Id).DeactivateCommand.ExecuteAsync(null);

        Assert.Equal(1, api.Calls.Count(c => c.Route == $"DELETE /api/rooms/{Sauna.Id}"));
        Assert.DoesNotContain(locations.Rooms, r => r.Id == Sauna.Id);
    }

    [Fact]
    public async Task Given_die_Checkliste_eines_Raums_When_gespeichert_wird_Then_PUT_mit_einem_Punkt_je_Zeile()
    {
        var api = Api().OnJson($"PUT /api/rooms/{TestData.Room.Id}/start-checklist",
            body => JsonSerializer.Deserialize<List<RoomStartItemInput>>(body!, ApiClient.Json)!.Select(i => new RoomStartItemDto(Guid.NewGuid(), i.Text)).ToList());
        var locations = await Open(api);
        var checklist = locations.Checklist!;
        Assert.Equal("Raum Zeder", checklist.SelectedRoom?.Name);
        await checklist.LoadAsync(checklist.SelectedRoom);
        Assert.Equal("Fenster öffnen", checklist.Text);

        checklist.Text = "Fenster öffnen\n  Handtücher auffüllen  \n\nLicht prüfen\n";
        await checklist.SaveCommand.ExecuteAsync(null);

        Assert.Equal(
            [new RoomStartItemInput("Fenster öffnen"), new RoomStartItemInput("Handtücher auffüllen"), new RoomStartItemInput("Licht prüfen")],
            Sent<List<RoomStartItemInput>>(api, $"PUT /api/rooms/{TestData.Room.Id}/start-checklist"));
        Assert.Equal("Checkliste gespeichert.", checklist.StatusText);
    }

    [Fact]
    public async Task Given_die_Online_Regeln_When_der_Vorlauf_geaendert_und_gespeichert_wird_Then_PUT_mit_den_Regeln()
    {
        var api = Api().OnJson($"PUT /api/locations/{TestData.LocationId}/online-rules", body => JsonSerializer.Deserialize<LocationOnlineRules>(body!, ApiClient.Json)! with { IsDefault = false });
        var locations = await Open(api);
        var rules = locations.OnlineRules!;
        await rules.LoadAsync();
        Assert.True(rules.IsDefault);

        rules.MinLeadMinutes = 90;
        rules.CancellationPolicy = "Absage bis 24 Stunden vorher ist kostenlos.";
        await rules.SaveCommand.ExecuteAsync(null);

        var sent = Sent<LocationOnlineRules>(api, $"PUT /api/locations/{TestData.LocationId}/online-rules");
        Assert.Equal(90, sent.MinLeadMinutes);
        Assert.Equal("Absage bis 24 Stunden vorher ist kostenlos.", sent.CancellationPolicy);
        Assert.Null(sent.GuestNote);
        Assert.False(rules.IsDefault);
        Assert.Equal("Gespeichert.", rules.StatusText);
    }

    [Fact]
    public async Task Given_Standortleitung_ohne_ManageOrganization_When_die_Maske_laedt_Then_fehlt_Standort_anlegen()
    {
        var manager = await Open(user: TestData.Manager());
        var owner = await Open();

        Assert.False(manager.CanCreate);
        Assert.DoesNotContain(manager.Choices, c => c.Id is null);
        Assert.True(owner.CanCreate);
        Assert.Equal(["Wien", "Salzburg", "Standort anlegen"], owner.Choices.Select(c => c.Title));
    }

    [Fact]
    public async Task Given_Standort_anlegen_When_Name_eingetragen_und_gespeichert_Then_POST_und_der_neue_Standort_ist_gewaehlt()
    {
        var api = Api();
        var locations = await Open(api);

        locations.SelectedChoice = locations.Choices.Single(c => c.Id is null);
        locations.Name = "Linz";
        Assert.True(await locations.SaveChangesAsync());

        var input = Sent<LocationInput>(api, "POST /api/locations");
        Assert.Equal(("Linz", "Europe/Vienna", "EUR"), (input.Name, input.TimeZone, input.Currency));
        Assert.Equal(6, input.OpeningHours.Count);
        Assert.Equal("Linz", locations.SelectedChoice?.Title);
        Assert.Equal(["Wien", "Salzburg", "Linz", "Standort anlegen"], locations.Choices.Select(c => c.Title));
    }

    [Fact]
    public async Task Given_ein_Sprung_aus_dem_Kalender_When_Raum_bearbeiten_Then_ist_der_Standort_des_Raums_gewaehlt_und_die_Zeile_hat_den_Fokus()
    {
        var locations = new LocationsViewModel(Api().Client(), TestData.Owner());

        locations.Open(new AdminTarget(Id: Sauna.Id));
        await locations.EnsureLoadedAsync();

        Assert.Equal("Wien", locations.SelectedChoice?.Title);
        Assert.Equal($"room:{Sauna.Id}", locations.FocusTarget);
    }

    [Fact]
    public async Task Given_ein_Raum_im_zweiten_Standort_When_Raum_bearbeiten_aus_dem_Kalender_springt_Then_wechselt_die_Maske_von_Wien_auf_Salzburg_und_der_Raum_hat_den_Fokus()
    {
        var lounge = new RoomDto(Guid.NewGuid(), SecondId, "Ruheraum", 8, 6, true, null);
        var api = Api().On($"GET /api/locations/{SecondId}/resources", new LocationResources(Salzburg, [lounge], [], [], [], []));
        var locations = new LocationsViewModel(api.Client(), TestData.Owner());
        await locations.EnsureLoadedAsync();
        Assert.Equal("Wien", locations.SelectedChoice?.Title);

        locations.Open(new AdminTarget(Id: lounge.Id));

        Assert.Equal("Salzburg", locations.SelectedChoice?.Title);
        Assert.Equal("Salzburg", locations.Name);
        Assert.Equal($"room:{lounge.Id}", locations.FocusTarget);
        Assert.Contains("Ruheraum", locations.Rooms.Select(r => r.Name));
        Assert.DoesNotContain("Sauna", locations.Rooms.Select(r => r.Name));
    }

    [Fact]
    public async Task Given_ein_Sprung_des_Assistenten_When_Standort_und_Feld_genannt_sind_Then_ist_der_Standort_gewaehlt_und_das_Feld_hat_den_Fokus()
    {
        var locations = await Open();

        locations.Open(new AdminTarget(Location: SecondId, Field: "location.directions"));

        Assert.Equal("Salzburg", locations.SelectedChoice?.Title);
        Assert.Equal("location.directions", locations.FocusTarget);
    }
}
