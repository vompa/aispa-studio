using AispaStudio.Contracts;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Assistent „Betrieb einrichten“ (ADR 0104): ersetzt die Seite, Fokus im ersten Feld des Schritts, genau ein Standardknopf, Esc verliert keine Eingabe.</summary>
public sealed partial class ViewTests
{
    private static SetupProgressDto WizardProgress() => new(
    [
        new("location", "Standort anlegen", "Name, Zeitzone und Währung.", true, "masterData", "locations"),
        new("hours", "Öffnungszeiten eintragen", "Wann Gäste buchen können.", false, "masterData", "locations", "Es sind keine Öffnungszeiten eingetragen."),
        new("rooms", "Räume anlegen", "Behandlungsräume und Bereiche.", false, "masterData", "locations", "Es fehlt ein Behandlungsraum."),
    ]);

    /// <summary>Rot, wenn der Assistent die Seite nicht ersetzt, der Fokus nicht im ersten Feld steht, mehr oder weniger als ein Standardknopf sichtbar ist oder Esc eine Eingabe verwirft.</summary>
    [Fact]
    public Task Betrieb_einrichten_Assistent_zeigt_Oeffnungszeiten_mit_Fokus_und_einem_Standardknopf() => Render("hell", "betrieb-einrichten-oeffnungszeiten", async (window, main) =>
    {
        var shell = await ShowCustomers(window, main);
        await shell.OpenSetupWizardAsync();
        await Pump(Settling);

        var wizard = shell.SetupWizard!;
        var controls = window.GetVisualDescendants().OfType<Control>().ToList();
        Assert.True(controls.Single(c => c.Name == "SetupWizardHost").IsEffectivelyVisible);
        Assert.False(controls.Single(c => c.Name == "PageHost").IsVisible);
        Assert.Equal("Schritt 2 von 3", controls.OfType<TextBlock>().Single(t => t.Name == "StepText").Text);
        Assert.Equal($"Öffnungszeiten eintragen · {SpaSample.Vienna.Name}", controls.OfType<TextBlock>().Single(t => t.Name == "StepTitle").Text);

        var first = controls.OfType<CheckBox>().First(c => c.Name == "DayOpen" && c.IsEffectivelyVisible);
        Assert.Same(first, Focused(window));
        var defaults = controls.OfType<Button>().Where(b => b.IsDefault && b.IsEffectivelyVisible).Select(b => b.Name).ToList();
        Assert.Equal(["SaveButton"], defaults);
        Assert.Contains("primary", controls.OfType<Button>().Single(b => b.Name == "SaveButton").Classes);

        var original = wizard.Days[0].OpensText;
        wizard.Days[0].OpensText = "08:00";
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Pump();
        Assert.Same(wizard, shell.SetupWizard);

        wizard.Days[0].OpensText = original;
        return shell;
    }, Api().On("GET /api/setup/progress", WizardProgress())
        .On("GET /api/setup/catalog", new List<SetupCatalogPackageDto>()).On("GET /api/qualifications", new List<QualificationDto>())
        .On("GET /api/locations", new List<LocationDto> { SpaSample.Vienna with { OpeningHours = [] }, SpaSample.Salzburg }));

    /// <summary>Rot, wenn der Schritt Behandlungen den Fokus nicht auf das erste Paket setzt oder nicht genau einen Standardknopf zeigt (ohne Auswahl die Verwaltung, mit Auswahl „Behandlungen anlegen“).</summary>
    [Fact]
    public Task Betrieb_einrichten_Assistent_zeigt_den_Startkatalog_mit_einem_Standardknopf() => Render("hell", "betrieb-einrichten-behandlungen", async (window, main) =>
    {
        var shell = await ShowCustomers(window, main);
        await shell.OpenSetupWizardAsync();
        await Pump(Settling);

        var wizard = shell.SetupWizard!;
        List<string?> Defaults() => [.. window.GetVisualDescendants().OfType<Button>().Where(b => b.IsDefault && b.IsEffectivelyVisible).Select(b => b.Name)];
        var first = window.GetVisualDescendants().OfType<CheckBox>().First(c => c.Name == "PackageCheck" && c.IsEffectivelyVisible);
        Assert.Same(first, Focused(window));
        Assert.Equal(["ApplyCatalogButton"], Defaults());
        Assert.False(window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ApplyCatalogButton").IsEffectivelyEnabled);

        wizard.Packages[0].IsSelected = true;
        await Pump();
        Assert.Equal(["ApplyCatalogButton"], Defaults());
        Assert.True(window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ApplyCatalogButton").IsEffectivelyEnabled);

        // Bild des Schritts Team, danach zurück zum Behandlungs-Bild.
        wizard.NextCommand.Execute(null);
        await Pump(Settling);
        Assert.Equal(["AddPersonButton"], Defaults());
        SaveShot(window, "betrieb-einrichten-team", "hell");

        // Tippen im zweiten Feld darf den Fokus nicht ins erste Feld holen.
        var lastName = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "LastNameBox");
        lastName.Focus();
        lastName.Text = "Muster";
        await Pump();
        Assert.Same(lastName, Focused(window));
        lastName.Text = "";
        wizard.BackCommand.Execute(null);
        await Pump(Settling);
        return shell;
    }, Api().On("GET /api/setup/progress", new SetupProgressDto(
    [
        new("location", "Standort anlegen", "Name und Zeitzone.", true, "masterData", "locations"),
        new("treatments", "Behandlungen anlegen", "Was das Spa anbietet.", false, "masterData", "treatments", "Es fehlen Behandlungen."),
        new("people", "Team anlegen", "Wer behandelt.", false, "masterData", "team", "Es fehlt eine Person."),
    ])).On("GET /api/qualifications", new List<QualificationDto> { new(Guid.NewGuid(), "Masseur"), new(Guid.NewGuid(), "Kosmetikerin") }).On("GET /api/setup/catalog", new List<SetupCatalogPackageDto>
    {
        new("massage", "Massagen", "Klassiker für den Start.", "Masseur", [new("Klassische Massage", 60, 5, 5, "Massage", false), new("Fußreflexzonenmassage", 30, 0, 0, "Massage", true)]),
        new("beauty", "Kosmetik", "Gesichtsbehandlungen.", "Kosmetikerin", [new("Gesichtsbehandlung", 60, 0, 0, "Kosmetik", false)]),
    }));

    /// <summary>Rot, wenn die Abschlussseite den Fokus nicht auf „Probetermin im Kalender“ setzt, nicht genau einen Standardknopf zeigt oder der Knopf den Assistenten nicht schließt und den Kalender nicht auf Tag und Standort stellt.</summary>
    [Fact]
    public Task Betrieb_einrichten_Assistent_zeigt_die_Abschlussseite_und_der_Probetermin_oeffnet_den_Kalender()
    {
        var stub = FinishApi();
        return Render("hell", "betrieb-einrichten-probetermin", async (window, main) =>
    {
        var shell = await ShowCustomers(window, main);
        await shell.OpenSetupWizardAsync();
        await Pump(Settling);

        var wizard = shell.SetupWizard!;
        await wizard.NextCommand.ExecuteAsync(null);
        await Pump(Settling);

        Assert.True(wizard.IsFinishStep);
        var controls = window.GetVisualDescendants().OfType<Control>().ToList();
        Assert.Same(controls.OfType<Button>().Single(b => b.Name == "OpenCalendarButton"), Focused(window));
        Assert.Equal(["OpenCalendarButton"], controls.OfType<Button>().Where(b => b.IsDefault && b.IsEffectivelyVisible).Select(b => b.Name).ToList());
        Assert.True(controls.Single(c => c.Name == "OpenStepsPanel").IsEffectivelyVisible);
        SaveShot(window, "betrieb-einrichten-abschluss", "hell");

        // Nicht buchbar: „Zurück“ ist der Hauptknopf (genau ein Standardknopf, nicht zugleich ghost).
        stub.On("GET /api/setup/progress?readiness=true", FinishProgress(new SetupReadinessDto(false, null, "Es ist keine Behandlung angelegt.")));
        await wizard.LoadReadinessCommand.ExecuteAsync(null);
        await Pump(Settling);
        controls = window.GetVisualDescendants().OfType<Control>().ToList();
        var back = controls.OfType<Button>().Single(b => b.Name == "BackButton");
        Assert.Equal(["BackButton"], controls.OfType<Button>().Where(b => b.IsDefault && b.IsEffectivelyVisible).Select(b => b.Name).ToList());
        Assert.DoesNotContain("ghost", back.Classes);
        Assert.Contains("primary", back.Classes);
        SaveShot(window, "betrieb-einrichten-nicht-buchbar", "hell");
        stub.On("GET /api/setup/progress?readiness=true", FinishProgress(FinishReady()));
        await wizard.LoadReadinessCommand.ExecuteAsync(null);
        await Pump(Settling);

        wizard.OpenCalendarCommand.Execute(null);
        await Pump();
        Assert.Null(shell.SetupWizard);
        Assert.Equal((new DateOnly(2026, 12, 1), SpaSample.LocationId), (shell.Calendar.Date, shell.Calendar.Location!.Id));
        return shell;
    }, stub);
    }

    private static StubApi FinishApi() => Api().On("GET /api/setup/progress", FinishProgress(null))
        .On("GET /api/setup/progress?readiness=true", FinishProgress(FinishReady()))
        .On("GET /api/setup/catalog", new List<SetupCatalogPackageDto>()).On("GET /api/qualifications", new List<QualificationDto>());

    private static SetupReadinessDto FinishReady() => new(true, new SetupFirstSlotDto(SpaSample.LocationId, SpaSample.Vienna.Name, SpaSample.Treatments[0].Id, SpaSample.Treatments[0].Name,
        new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero)));

    private static SetupProgressDto FinishProgress(SetupReadinessDto? readiness) => new(
    [
        new("location", "Standort anlegen", "Name und Zeitzone.", true, "masterData", "locations"),
        new("treatments", "Behandlungen anlegen", "Was das Spa anbietet.", true, "masterData", "treatments", null, "Bei 2 Behandlungen fehlt der Preis."),
        new("register", "Kassa einrichten", "Kassa-ID und Belegkopf.", false, "masterData", "kassa", "Es ist keine Kassa eingerichtet."),
    ], readiness);
}
