using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>ADR 0104, Entscheidung 6: Abschlussseite „Fertig“ mit Bereitschaft und Probetermin.</summary>
public sealed partial class SetupWizardTests
{
    private const string ReadinessRoute = "GET /api/setup/progress?readiness=true";

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TestData.At(10);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
    }

    private static readonly DateTimeOffset SlotStart = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);

    private static SetupProgressDto WithReadiness(SetupProgressDto progress, SetupReadinessDto readiness) => progress with { Readiness = readiness };

    private static SetupReadinessDto Ready() => new(true, new SetupFirstSlotDto(LocationId, "Wien", Guid.NewGuid(), "Klassische Massage", SlotStart));

    /// <summary>Rot, wenn die Seite nicht von selbst aufgeht, der Platz nicht in der Zeitzone des Standorts steht oder der Knopf den Kalender nicht mit genau diesem Platz anfordert.</summary>
    [Fact]
    public async Task Given_alle_Schritte_erledigt_und_buchbar_When_der_Assistent_laedt_Then_zeigt_die_Abschlussseite_den_Platz_und_der_Knopf_fordert_den_Kalender_an()
    {
        var api = Api(Progress("location", "hours", "rooms", "treatments")).On(ReadinessRoute, WithReadiness(Progress("location", "hours", "rooms", "treatments"), Ready()));
        var wizard = await OpenAsync(api);
        SetupFirstSlotDto? requested = null;
        wizard.CalendarRequested += slot => requested = slot;

        Assert.True(wizard.IsFinishStep);
        Assert.False(wizard.HasStep);
        Assert.True(wizard.IsReady);
        Assert.Contains("10:00", wizard.FirstSlotWhen, StringComparison.Ordinal);
        Assert.Equal(SetupWizardDefault.OpenCalendar, wizard.DefaultButton);
        wizard.OpenCalendarCommand.Execute(null);

        Assert.Equal((LocationId, SlotStart), (requested!.LocationId, requested.Start));
    }

    /// <summary>Rot, wenn der Grund der API fehlt, „Zurück“ nicht der Hauptknopf ist oder der Sprung zu einem offenen Schritt woanders landet.</summary>
    [Fact]
    public async Task Given_nicht_buchbar_When_die_Abschlussseite_laedt_Then_steht_der_Grund_da_und_ein_offener_Schritt_ist_per_Sprung_erreichbar()
    {
        var progress = Progress("location", "hours", "rooms");
        var api = Api(progress with { Readiness = null }, RoomOne).On(ReadinessRoute, WithReadiness(progress, new SetupReadinessDto(false, null, "Es ist keine Behandlung angelegt.")));
        var wizard = await OpenAsync(api);
        await wizard.NextCommand.ExecuteAsync(null);

        Assert.True(wizard.IsFinishStep);
        Assert.Equal("Es ist keine Behandlung angelegt.", wizard.NotReadyReason);
        Assert.Equal(SetupWizardDefault.Back, wizard.DefaultButton);
        var open = Assert.Single(wizard.OpenSteps);
        wizard.GoToStepCommand.Execute(open);

        Assert.Equal("treatments", wizard.Current!.Key);
    }

    /// <summary>Rot, wenn ein Ladefehler der Bereitschaft verschluckt wird oder „Erneut laden“ den Fehler stehen lässt.</summary>
    [Fact]
    public async Task Given_die_Bereitschaft_laedt_nicht_When_erneut_geladen_wird_Then_steht_zuerst_die_Meldung_und_danach_der_Platz()
    {
        var progress = Progress("location", "hours", "rooms", "treatments");
        var api = Api(progress).On(ReadinessRoute, null, HttpStatusCode.InternalServerError);
        var wizard = await OpenAsync(api);

        Assert.Equal(SetupWizardViewModel.ReadinessNotLoaded, wizard.ReadinessError);
        Assert.False(wizard.IsReady);
        Assert.False(wizard.IsBusy);

        api.On(ReadinessRoute, WithReadiness(progress, Ready()));
        await wizard.LoadReadinessCommand.ExecuteAsync(null);

        Assert.Null(wizard.ReadinessError);
        Assert.True(wizard.IsReady);
    }

    /// <summary>Rot, wenn Tippen im Formular Index, IsLoaded oder ReadinessLoaded meldet: die Ansicht setzt darauf den Fokus und der Cursor sprang beim Tippen ins erste Feld.</summary>
    [Fact]
    public async Task Given_der_Schritt_Team_When_ein_Name_getippt_wird_Then_meldet_das_Modell_nichts_worauf_die_Ansicht_den_Fokus_setzt()
    {
        var api = CatalogApi(ProgressWithPeople("location", "hours", "rooms", "treatments"), RoomOne);
        var wizard = await OpenAsync(api);
        var focusRelevant = new List<string?>();
        wizard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SetupWizardViewModel.Index) or nameof(SetupWizardViewModel.IsLoaded) or nameof(SetupWizardViewModel.ReadinessLoaded))
            {
                focusRelevant.Add(e.PropertyName);
            }
        };

        wizard.FirstName = "M";
        wizard.LastName = "Muster";
        wizard.QualificationChoices.Clear();
        wizard.WorksAtOpeningHours = false;

        Assert.Empty(focusRelevant);
    }

    /// <summary>Rot, wenn der Kalender nach dem Sprung die nach dem Start geänderten Ressourcen nicht neu liest oder den Tag mehrfach lädt.</summary>
    [Fact]
    public async Task Given_Stammdaten_nach_dem_Start_geaendert_When_der_Kalender_einen_Tag_zeigen_soll_Then_liest_er_neu_und_laedt_den_Tag_genau_einmal()
    {
        var day = new DateOnly(2027, 3, 2);
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01", TestData.Day())
            .On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", TestData.Day());
        var calendar = new CalendarViewModel(api.Client(), TestData.Reception(), null, new FixedClock());
        await calendar.InitializeAsync();
        await calendar.LoadAsync();
        var resourcesBefore = api.Calls.Count(c => c.Route.EndsWith("/resources", StringComparison.Ordinal));
        var daysBefore = api.Calls.Count(c => c.Route.EndsWith("/calendar?date=2027-03-02", StringComparison.Ordinal));

        await calendar.ShowDayAsync(TestData.LocationId, day);

        Assert.Equal((day, TestData.LocationId), (calendar.Date, calendar.Location!.Id));
        Assert.Equal(resourcesBefore + 1, api.Calls.Count(c => c.Route.EndsWith("/resources", StringComparison.Ordinal)));
        Assert.Equal(daysBefore + 1, api.Calls.Count(c => c.Route.EndsWith("/calendar?date=2027-03-02", StringComparison.Ordinal)));
    }

    /// <summary>Rot, wenn erledigte Schritte mit Hinweis (fehlende Preise) auf der Abschlussseite fehlen oder der Hinweis nicht als Text erscheint.</summary>
    [Fact]
    public async Task Given_ein_erledigter_Schritt_mit_Hinweis_When_die_Abschlussseite_laedt_Then_steht_er_mit_dem_Hinweis_unter_den_offenen_Punkten()
    {
        var steps = new List<SetupStepDto>
        {
            new("location", "Standort anlegen", "Hinweis.", true, "masterData", "locations"),
            new("treatments", "Behandlungen anlegen", "Hinweis.", true, "masterData", "treatments", null, "Bei 2 Behandlungen fehlt der Preis."),
        };
        var api = Api(new SetupProgressDto(steps)).On(ReadinessRoute, new SetupProgressDto(steps, Ready()));
        var wizard = await OpenAsync(api);

        var open = Assert.Single(wizard.OpenSteps);
        Assert.Equal(("Behandlungen anlegen", "Bei 2 Behandlungen fehlt der Preis."), (open.Title, open.Missing));
    }
}
