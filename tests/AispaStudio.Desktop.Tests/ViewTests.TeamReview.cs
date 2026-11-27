using AispaStudio.Desktop.Controls;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Durchsicht der Verwaltung „Team“ (Befunde 85 bis 96): jeder Test hält einen Befund fest.</summary>
public sealed partial class ViewTests
{
    private static readonly (string Effect, string Tip)[] EffectTips =
    [
        ("Schaltet frei", "Die App prüft diese Vollmacht zusätzlich zur Rolle."),
        ("Nur Aushang", "Der Eintrag zeigt nur, wer zuständig ist. Die App prüft nichts."),
    ];

    private static readonly string[] TimeActionLabels = ["Schließen", "Ändern", "Verlauf"];

    [Fact]
    public Task Team_neue_Person_ohne_Namen_meldet_beim_Speichern_den_Fehler_am_Feld_und_setzt_den_Fokus()
    {
        var api = TeamApi();
        return Render("hell", "verwaltung-team-neue-person", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickNew(window, "AddPersonButton");

        // 86a: Speichern ist aktiv; der Klick ohne Pflichtname setzt den Fehler am Feld und den Fokus dorthin (masken.md R5, R8).
        var save = Named<Button>(window, "SaveButton");
        Assert.True(save.IsEffectivelyEnabled);
        Assert.Equal("Speichern (Strg+S)", ToolTip.GetTip(save)?.ToString());

        await team.SaveCommand.ExecuteAsync(null);
        await Pump(Settling);
        Assert.Equal("Der Name der Person fehlt. Trage den vollständigen Namen ein.", team.Card!.Details!.NameError);
        Assert.Same(Named<TextBox>(window, "PersonNameField"), TopLevel.GetTopLevel(window)!.FocusManager!.GetFocusedElement());
        Assert.DoesNotContain(api.Calls, c => c.Route == "POST /api/employees");
        return team;
    }, api);
    }

    [Fact]
    public Task Team_Lohn_der_Karte_hat_ein_Fragezeichen_Spaltenkoepfe_und_der_Ladefehler_nennt_den_Weg() => Render("hell", "verwaltung-team-karte-lohn-durchsicht", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        await ClickCardTab(window, "Lohn");
        await Pump(Settling);
        Assert.True(team.Card!.Payroll!.IsLoaded);

        // 89a: ein Fragezeichen für die Gruppe, keine zweite Überschrift „Personalnummer“.
        var section = Named<Control>(window, "PayrollSection");
        Assert.Single(section.GetVisualDescendants().OfType<HelpButton>(), b => b.IsEffectivelyVisible);
        Assert.DoesNotContain(section.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("h3") && t.Text == "Personalnummer");

        // 89b: die Monatswerte haben Spaltenköpfe, Menge rechtsbündig wie die Zahlen darunter.
        var head = Named<Control>(window, "PersonPayrollHead");
        Assert.True(head.IsEffectivelyVisible);
        var heads = head.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Equal(["Position", "Menge", "Lohnart"], heads.Select(t => t.Text));
        var quantity = Named<Control>(window, "PersonPayrollRows").GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "16,00 h");
        Assert.Equal(LeftOf(window, quantity) + quantity.Bounds.Width, LeftOf(window, heads[1]) + heads[1].Bounds.Width, 1);

        // 89c: die Personalakte ist in dieser Stub-API nicht erreichbar; die Meldung nennt trotzdem Problem und Weg.
        Assert.Equal("Nicht gefunden. Verbindung prüfen und „Erneut laden“ wählen.", team.Card.HrFile!.LoadError);
        return team;
    }, PayrollApi());

    [Fact]
    public Task Team_Lohn_hat_Spaltenkoepfe_Felder_nach_Breite_und_die_Meldung_bietet_Ordner_oeffnen() => Render("hell", "verwaltung-team-lohn-durchsicht", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        var files = new StockExportDialogTests.PathSaver();
        var folders = new StockExportDialogTests.FakeFolders();
        await ClickSection(window, "Lohn");
        await Pump(Settling);
        team.Payroll!.Files = files;
        team.Payroll.FolderOpener = folders;

        await ClickButton(window, VisibleNamed<Button>(window, "PayrollExportBmdButton"));
        await Pump(Settling);

        // 94a: die Meldung nennt die Datei und bietet den Ort an.
        var bar = VisibleNamed<InfoBar>(window, "PayrollSavedBar");
        Assert.Equal("lohn-BmdLohn-2026-09.csv gespeichert.", bar.Message);
        var open = bar.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == "Ordner öffnen");
        ClickCenter(window, open);
        await Pump(Settling);
        Assert.Equal(files.LastSavedPath, Assert.Single(folders.Revealed));

        // 89b und 94a: Spaltenköpfe über den Monatswerten.
        var head = VisibleNamed<Control>(window, "PayrollTableHead");
        Assert.Equal(["Person", "Personalnummer", "Position", "Menge", "Lohnart"], head.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

        // 94b: Breiten nach design.md 5.2: Uhrzeit 100, Nummern und Lohnarten 120, alle am linken Rand der Gruppe.
        var night = Named<TextBox>(window, "PayrollNightStartField");
        var consultant = Named<TextBox>(window, "PayrollConsultantField");
        var wage = Named<TextBox>(window, "PayrollHoursWageField");
        Assert.Equal([100, 120, 120], [night.Bounds.Width, consultant.Bounds.Width, wage.Bounds.Width]);
        Assert.Equal(LeftOf(window, night), LeftOf(window, consultant), 1);
        Assert.Equal(LeftOf(window, night), LeftOf(window, wage), 1);
        return team;
    }, PayrollApi());

    [Fact]
    public Task Team_Wochenplan_Besetzung_und_Stunden_sind_erklaert_und_der_Antrag_mit_Terminen_ist_ein_Warnhinweis() => Render("hell", "verwaltung-team-wochenplan-durchsicht", async (window, main) =>
    {
        var team = await OpenWeekPlan(window, main);

        // 91c und 88c: Besetzung und Stunden tragen einen Hinweis.
        Assert.Equal("Wie viele Personen an dem Tag am Standort im Dienst sind", ToolTip.GetTip(Named<Border>(window, "RosterCoverage"))?.ToString());
        var hours = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "RosterHours" && t.IsEffectivelyVisible);
        Assert.Equal("Geplante Stunden dieser Woche von der Wochenarbeitszeit (Soll)", ToolTip.GetTip(hours)?.ToString());
        Assert.Contains(window.GetVisualDescendants().OfType<HelpButton>(), b => b.IsEffectivelyVisible && b.Topic == "seite-roster");

        // 92b: die Termine in der Zeit sind ein Warnhinweis vor den Knöpfen.
        await ClickButton(window, Named<Button>(window, "RequestsButton"));
        var panel = Named<SidePanel>(window, "RequestsPanel");
        var warning = panel.GetVisualDescendants().OfType<InfoBar>().Single(b => b.Name == "RequestAffected" && b.IsEffectivelyVisible);
        Assert.Equal(InfoBarSeverity.Warning, warning.Severity);
        Assert.Equal("In dieser Zeit liegen 2 Termine.", warning.Message);
        var approve = panel.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == "Genehmigen");
        Assert.True(TopOf(window, warning) + warning.Bounds.Height <= TopOf(window, approve), "Der Warnhinweis steht vor den Knöpfen");
        return team;
    }, RequestsApi());

    [Fact]
    public Task Team_Karte_Arbeitszeit_hat_ein_Fragezeichen_je_Gruppe_und_Aktionen_als_Knoepfe() => Render("hell", "verwaltung-team-karte-arbeitszeit-durchsicht", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        await ClickCardArea(window, "Arbeitszeit");

        // 88a: das Fragezeichen im Kopf der Karte gilt für den Wochenplan; der Plan zeigt kein eigenes, das Urlaubskonto als eigene Gruppe seines.
        var rosterHelp = Named<Control>(window, "RosterSection").GetVisualDescendants().OfType<HelpButton>().Where(b => b.IsEffectivelyVisible).ToList();
        Assert.Equal(["seite-roster", "urlaubskonto"], rosterHelp.Select(b => b.Topic));
        Assert.DoesNotContain(Named<Control>(window, "RosterHost").GetVisualDescendants().OfType<HelpButton>(), b => b.IsEffectivelyVisible);
        Assert.Equal("seite-time", Named<Control>(window, "TimesSection").GetVisualDescendants().OfType<HelpButton>().Single(b => b.IsEffectivelyVisible).Topic);

        // 88c: die Stunden der Woche sind erklärt.
        var hours = Named<Control>(window, "RosterHost").GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "RosterHours");
        Assert.NotNull(ToolTip.GetTip(hours));

        // 88b: „Schließen“ im Tagesfeld ist ein Knopf mit Rand, kein Text.
        await ClickCell(window, "Lena Huber", "Di.");
        var close = VisibleNamed<Button>(window, "CloseDayButton");
        Assert.Contains("small", close.Classes);
        Assert.DoesNotContain("ghost", close.Classes);
        return team;
    }, TeamApi());

    [Fact]
    public Task Team_Zeiten_erklaeren_die_Abkuerzungen_und_Aktionen_sind_Knoepfe() => Render("hell", "verwaltung-team-zeiten-durchsicht", async (window, main) =>
    {
        var team = await OpenTimes(window, main);
        await ClickTimesCell(window, "Lena Huber", "Di.");

        // 93a: Gesetze ausgeschrieben, Textgröße 14 statt Klein.
        var legend = Named<TextBlock>(window, "TimesLegend");
        Assert.Contains("Österreich (Arbeitszeitgesetz, Arbeitsruhegesetz)", legend.Text);
        Assert.DoesNotContain("AZG", legend.Text);
        Assert.DoesNotContain("small", legend.Classes);

        // 93b: Schließen, Ändern und Verlauf haben die Form eines Knopfes.
        Assert.All(TimeActionLabels, label =>
        {
            var button = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == label);
            Assert.Contains("small", button.Classes);
            Assert.DoesNotContain("ghost", button.Classes);
        });
        return team;
    }, TimesApi().Api);

    [Fact]
    public Task Team_Rechte_erklaeren_die_Wirkung_nennen_den_Ort_der_Karte_und_haben_Knoepfe_fuer_Personen() => Render("hell", "verwaltung-team-rechte-durchsicht", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickSection(window, "Rechte und Zuständigkeiten");
        await Pump(Settling);

        // 95b: jede Wirkung trägt einen Hinweis.
        var table = Named<Control>(window, "PowerOverview");
        foreach (var (effect, tip) in EffectTips)
        {
            var pill = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == effect).GetVisualAncestors().OfType<Border>().First();
            Assert.Equal(tip, ToolTip.GetTip(pill)?.ToString());
        }

        // 95c: der Verweis nennt die Orte so, wie sie heute heißen.
        Assert.True(Shows(window, "Zuständigkeiten im Bereich „Profil“, Vollmachten im Bereich „Lohn und Akte“ unter „Regelungen und Vollmachten“"));

        // 95a: „+ Person“ ist ein Knopf mit Rand.
        var add = table.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == "+ Person");
        Assert.Contains("small", add.Classes);
        Assert.DoesNotContain("link", add.Classes);
        return team;
    }, DutiesApi());

    [Fact]
    public Task Team_Zustaendigkeiten_fuehren_vom_ablaufenden_Zertifikat_zur_Karte_und_haben_ein_Fragezeichen() => Render("hell", "verwaltung-team-zustaendigkeiten-durchsicht", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickSection(window, "Rechte und Zuständigkeiten");
        await Pump(Settling);

        // 96a: ein Fragezeichen für Zertifikate und Liste.
        var duties = VisibleNamed<Control>(window, "DutiesSection");
        var help = Assert.Single(duties.GetVisualDescendants().OfType<HelpButton>(), b => b.IsEffectivelyVisible);
        Assert.Equal("liste", help.Section);

        // 96b: die Warnung führt zur Karte der Person.
        var open = Named<Control>(window, "ExpiringRows").GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible);
        Assert.Equal("Karte von Lena Huber öffnen", Avalonia.Automation.AutomationProperties.GetName(open));
        ClickCenter(window, open);
        await Pump(Settling);
        Assert.True(team.ShowsPeople);
        Assert.Equal("Lena Huber", team.Card!.Title);
        Assert.True(team.Card.ShowsDuties);
        return team;
    }, DutiesApi());

    [Fact]
    public Task Team_Profil_nennt_Passkeys_richtig_und_der_Hilfetext_zum_Namen_im_Haus_hat_eine_Zeile() => Render("hell", "verwaltung-team-profil-durchsicht", async (window, main) =>
    {
        var team = await OpenTeam(window, main);
        await ClickRow(window, "Lena Huber");
        await ClickCardArea(window, "Profil");

        // 87c
        var info = Named<TextBlock>(window, "AccessInfo");
        Assert.Matches(@"· \d+ Passkeys?$", info.Text);
        Assert.DoesNotContain("(s)", info.Text);
        Assert.False(string.IsNullOrEmpty(ToolTip.GetTip(info)?.ToString()));

        // 87b: eine Zeile (Klein hat die Zeilenhöhe 16).
        var help = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == "Für Kalender und Gäste, z. B. Lena.");
        Assert.True(help.Bounds.Height <= 17, $"Hilfetext {help.Bounds.Height:0} hoch");
        return team;
    }, TeamApi());
}
