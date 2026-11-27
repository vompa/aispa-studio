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
}
