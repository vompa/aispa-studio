using System.Net;
using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>ADR 0104, Entscheidungen 4 und 5: Schritte Behandlungen (Startkatalog) und Team im Assistenten „Betrieb einrichten“.</summary>
public sealed partial class SetupWizardTests
{
    private static readonly string[] Rejection = ["Für Massagen fehlt ein Raum."];

    private const string ApplyRoute = "POST /api/setup/catalog/apply";

    private static readonly RoomDto RoomOne = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), LocationId, "Meerblick", 10);
    private static readonly RoomDto RoomTwo = new(Guid.Parse("33333333-3333-3333-3333-333333333333"), LocationId, "Waldblick", 10);

    private static IReadOnlyList<SetupCatalogPackageDto> Catalog() =>
    [
        new("massage", "Massagen", "Klassiker.", "Masseur", [new("Klassische Massage", 60, 5, 5, "Massage", false), new("Fußreflexzonenmassage", 30, 0, 0, "Massage", true)]),
        new("beauty", "Kosmetik", "Gesicht.", "Kosmetikerin", [new("Gesichtsbehandlung", 60, 0, 0, "Kosmetik", false)]),
    ];

    private static SetupProgressDto ProgressWithPeople(params string[] done)
    {
        var steps = Progress(done).Steps.ToList();
        steps.Add(Step("people", "Team anlegen", "masterData", "team", done, "Es fehlt eine Person."));
        return new SetupProgressDto(steps);
    }

    private static StubApi CatalogApi(SetupProgressDto progress, params RoomDto[] rooms) => Api(progress, rooms)
        .On("GET /api/setup/catalog", Catalog())
        .On("GET /api/employees", new List<EmployeeDto>())
        .On("GET /api/qualifications", new List<QualificationDto>());

    /// <summary>Rot, wenn „Behandlungen anlegen“ andere Pakete oder Räume sendet, die Auswahl nicht leert oder den Stand nicht neu lädt.</summary>
    [Fact]
    public async Task Given_ein_Paket_und_ein_Raum_gewaehlt_When_Behandlungen_angelegt_werden_Then_gehen_Schluessel_und_Raum_an_die_API_und_der_Stand_wird_neu_geladen()
    {
        var api = CatalogApi(Progress("location", "hours", "rooms"), RoomOne, RoomTwo);
        var wizard = await OpenAsync(api);
        api.On(ApplyRoute, new SetupCatalogApplyResult([new("massage", 1, 1)]));

        Assert.True(wizard.IsTreatmentsStep);
        Assert.False(wizard.ApplyCatalogCommand.CanExecute(null));
        Assert.All(wizard.RoomChoices, r => Assert.True(r.IsSelected));
        wizard.RoomChoices[1].IsSelected = false;
        wizard.Packages[0].IsSelected = true;
        Assert.Equal(SetupWizardDefault.ApplyCatalog, wizard.DefaultButton);
        await wizard.ApplyCatalogCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<SetupCatalogApplyInput>(api.Calls.Single(c => c.Route == ApplyRoute).Body!, ApiClient.Json)!;
        var choice = Assert.Single(sent.Packages);
        Assert.Equal("massage", choice.Key);
        Assert.Equal(RoomOne.Id, Assert.Single(choice.RoomIds));
        Assert.Equal(2, api.Calls.Count(c => c.Route == "GET /api/setup/progress"));
        Assert.DoesNotContain(wizard.Packages, p => p.IsSelected);
        Assert.NotNull(wizard.CatalogMessage);
    }

    /// <summary>Rot, wenn ohne Behandlungsraum trotzdem angelegt werden kann oder der Hinweis auf den Schritt Räume fehlt.</summary>
    [Fact]
    public async Task Given_kein_Behandlungsraum_When_ein_Paket_gewaehlt_ist_Then_ist_der_Knopf_gesperrt_und_der_Hinweis_steht_im_Schritt()
    {
        var api = CatalogApi(Progress("location", "hours", "rooms"));
        var wizard = await OpenAsync(api);

        wizard.Packages[0].IsSelected = true;
        await wizard.ApplyCatalogCommand.ExecuteAsync(null);

        Assert.True(wizard.ShowNoRoomHint);
        Assert.False(wizard.ApplyCatalogCommand.CanExecute(null));
        Assert.DoesNotContain(api.Calls, c => c.Route == ApplyRoute);
    }

    /// <summary>Rot, wenn die Meldung der API nicht im Schritt erscheint oder ein Fehler die Auswahl verwirft.</summary>
    [Fact]
    public async Task Given_die_API_lehnt_ab_When_Behandlungen_angelegt_werden_Then_steht_der_Klartext_im_Schritt_und_die_Auswahl_bleibt()
    {
        var api = CatalogApi(Progress("location", "hours", "rooms"), RoomOne);
        var wizard = await OpenAsync(api);
        api.On(ApplyRoute, new { errors = new { packages = Rejection } }, HttpStatusCode.BadRequest);

        wizard.Packages[0].IsSelected = true;
        await wizard.ApplyCatalogCommand.ExecuteAsync(null);

        Assert.Equal("Für Massagen fehlt ein Raum.", wizard.Error);
        Assert.True(wizard.Packages[0].IsSelected);
        Assert.False(wizard.TryCloseWithEscape());
        Assert.False(wizard.IsBusy);
    }

    /// <summary>Rot, wenn die Person ohne Qualifikationen, ohne die Öffnungszeiten als Arbeitszeit oder an den falschen Standort gesendet wird.</summary>
    [Fact]
    public async Task Given_Name_und_Qualifikation_When_Person_hinzugefuegt_wird_Then_geht_POST_mit_Qualifikation_und_Oeffnungszeiten_an_die_API()
    {
        var qualification = new QualificationDto(Guid.NewGuid(), "Masseur");
        var api = CatalogApi(ProgressWithPeople("location", "hours", "rooms", "treatments"), RoomOne)
            .On("GET /api/qualifications", new List<QualificationDto> { qualification, new(Guid.NewGuid(), "Kosmetikerin") });
        var wizard = await OpenAsync(api);
        var created = new EmployeeDto(Guid.NewGuid(), "Eva Huber", "#2f6f5e", [], []);
        api.On("POST /api/employees", created).On("GET /api/employees", new List<EmployeeDto> { created });

        Assert.True(wizard.IsPeopleStep);
        wizard.FirstName = "Eva";
        wizard.LastName = "Huber";
        wizard.QualificationChoices.Single(q => q.Id != qualification.Id).IsSelected = false;
        Assert.Equal(SetupWizardDefault.AddPerson, wizard.DefaultButton);
        await wizard.AddPersonCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<EmployeeInput>(api.Calls.Single(c => c.Route == "POST /api/employees").Body!, ApiClient.Json)!;
        Assert.Equal("Eva Huber", sent.DisplayName);
        Assert.Equal(qualification.Id, Assert.Single(sent.QualificationIds));
        Assert.Equal(Location.OpeningHours.Count, sent.WorkingHours.Count);
        Assert.All(sent.WorkingHours, h => Assert.Equal(LocationId, h.LocationId));
        Assert.Equal(["Eva Huber"], wizard.People);
        Assert.Equal(("", ""), (wizard.FirstName, wizard.LastName));
    }

    /// <summary>Rot, wenn ein gescheitertes Neuladen der Personen nach dem POST die Person aus der Liste nimmt oder keine Meldung zeigt.</summary>
    [Fact]
    public async Task Given_Neuladen_der_Personen_scheitert_nach_erfolgreichem_POST_When_Person_hinzugefuegt_Then_bleibt_sie_in_der_Liste_und_die_Meldung_steht_im_Schritt()
    {
        var api = CatalogApi(ProgressWithPeople("location", "hours", "rooms", "treatments"), RoomOne);
        var wizard = await OpenAsync(api);
        api.On("POST /api/employees", new EmployeeDto(Guid.NewGuid(), "Eva Huber", "#2f6f5e", [], []))
            .On("GET /api/employees", null, HttpStatusCode.InternalServerError);

        wizard.FirstName = "Eva";
        wizard.LastName = "Huber";
        await wizard.AddPersonCommand.ExecuteAsync(null);

        Assert.Equal(SetupWizardViewModel.NotLoaded, wizard.Error);
        Assert.Equal(["Eva Huber"], wizard.People);
    }

    /// <summary>Rot, wenn ein zweiter Aufruf bei laufender Anlage eine zweite Person sendet (ExecuteAsync umgeht CanExecute).</summary>
    [Fact]
    public async Task Given_die_Antwort_ist_verzoegert_When_zweimal_ohne_Warten_ausgeloest_wird_Then_geht_genau_ein_POST_an_die_API()
    {
        var gate = new TaskCompletionSource();
        var api = CatalogApi(ProgressWithPeople("location", "hours", "rooms", "treatments"), RoomOne);
        var wizard = await OpenAsync(api);
        api.On("POST /api/employees", new EmployeeDto(Guid.NewGuid(), "Eva Huber", "#2f6f5e", [], [])).Hold("POST /api/employees", gate.Task);
        wizard.FirstName = "Eva";
        wizard.LastName = "Huber";

        var first = wizard.AddPersonCommand.ExecuteAsync(null);
        var second = wizard.AddPersonCommand.ExecuteAsync(null);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Single(api.Calls, c => c.Route == "POST /api/employees");
    }

    /// <summary>Rot, wenn eine Person nur mit Namen nicht alle Qualifikationen bekommt (Vorgabe: alle gewählt).</summary>
    [Fact]
    public async Task Given_nur_Vor_und_Nachname_When_Person_hinzugefuegt_wird_Then_sendet_sie_alle_Qualifikationen()
    {
        var api = CatalogApi(ProgressWithPeople("location", "hours", "rooms", "treatments"), RoomOne)
            .On("GET /api/qualifications", new List<QualificationDto> { new(Guid.NewGuid(), "Masseur"), new(Guid.NewGuid(), "Kosmetikerin") });
        var wizard = await OpenAsync(api);
        api.On("POST /api/employees", new EmployeeDto(Guid.NewGuid(), "Eva Huber", "#2f6f5e", [], []));

        wizard.FirstName = "Eva";
        wizard.LastName = "Huber";
        await wizard.AddPersonCommand.ExecuteAsync(null);

        var sent = JsonSerializer.Deserialize<EmployeeInput>(api.Calls.Single(c => c.Route == "POST /api/employees").Body!, ApiClient.Json)!;
        Assert.Equal(2, sent.QualificationIds.Count);
    }
}
