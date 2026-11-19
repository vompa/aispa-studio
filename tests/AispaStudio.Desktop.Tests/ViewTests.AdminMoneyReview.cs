using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AispaStudio.Desktop.Tests;

/// <summary>Durchsicht der Verwaltung „Kassa &amp; Geld“, „Kontakt“ und „Zahlen“ (Befunde 106 bis 119): Zahlenspalten, Beschriftungen, Knopfformen.</summary>
public sealed partial class ViewTests
{
    private static readonly string[] ExportButtons = ["ShowPostingsButton", "ExportDatevButton", "ExportBmdButton", "ExportRzlButton", "ExportSwissButton"];

    private static TextBlock TextBlockShowing(Window window, string text) =>
        window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible && t.Text == text);

    private static double RightEdge(Window window, Control control) =>
        (control.TranslatePoint(new Point(control.Bounds.Width, 0), window) ?? throw new InvalidOperationException("Nicht im Fenster.")).X;

    private static double LeftEdge(Window window, Control control) =>
        (control.TranslatePoint(new Point(0, 0), window) ?? throw new InvalidOperationException("Nicht im Fenster.")).X;

    /// <summary>Beschriftungen der sichtbaren Felder: Klammern nur für Einheiten, Erklärungen stehen im Hilfetext (design.md 5.2).</summary>
    private static List<string> FieldLabels(Window window) =>
        [.. window.GetVisualDescendants().OfType<Controls.FormField>().Where(f => f.IsEffectivelyVisible && f.Label is not null).Select(f => f.Label!)];

    [Fact]
    public Task Pflichtberichte_Zahlenspalten_der_Arbeitszeit_stehen_samt_Kopf_rechtsbuendig() => Render("hell", "geld-pflichtberichte-zahlen", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.ComplianceTab);

        // Die Werte „160,5“ (Stunden) und der Kopf „Stunden“ teilen dieselbe rechte Kante (design.md 5.7).
        var head = TextBlockShowing(window, "Stunden");
        var value = TextBlockShowing(window, "160,5");
        Assert.Equal(HorizontalAlignment.Right, head.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Right, value.HorizontalAlignment);
        Assert.Equal(RightEdge(window, head), RightEdge(window, value), 0.5);

        // Die Zahl der Pakete steht in der richtigen Mehrzahl.
        Assert.True(Shows(window, "1 offenes Paket mit 4 Behandlungen"));
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Kassa_Barzahlungsrabatt_hat_nur_die_Einheit_in_der_Klammer_und_die_Erklaerung_als_Hilfetext() => Render("hell", "geld-kassa-beschriftung", async (window, main) =>
    {
        var settings = await OpenSettingsArea<PosSettingsViewModel>(window, main, "Kassa");

        var labels = FieldLabels(window);
        Assert.Contains("Barzahlungsrabatt (%)", labels);
        Assert.DoesNotContain(labels, l => l.Contains("0 = aus", StringComparison.Ordinal));
        Assert.True(Shows(window, "0 schaltet den Barzahlungsrabatt aus."));
        return settings;
    }, SettingsApi());

    [Fact]
    public Task Erinnerungen_Beschriftungen_ohne_Erklaerung_und_Versandprotokoll_als_Knopf() => Render("hell", "geld-erinnerungen-beschriftung", async (window, main) =>
    {
        var reminders = await OpenSettingsArea<RemindersViewModel>(window, main, "Erinnerungen");

        var labels = FieldLabels(window);
        Assert.Contains("Erste Erinnerung (Stunden)", labels);
        Assert.Contains("Zweite Erinnerung (Stunden)", labels);
        Assert.All(labels, l => Assert.DoesNotContain("0 = keine", l, StringComparison.Ordinal));

        // „Versandprotokoll“ ist eine Aktion und sieht wie ein Knopf aus (sekundär, nicht Ghost; design.md 5.1).
        Assert.DoesNotContain("ghost", Named<Button>(window, "ReminderLogButton").Classes);

        // Der Hinweis zur Wiederbuchung bleibt bei einer Zeile; Einwilligung und KI stehen im Fragezeichen.
        var rebookHint = TextBlockShowing(window, "Gäste bekommen nach dem Abstand ohne neuen Termin eine Mail mit Buchungslink.");
        Assert.True(rebookHint.Bounds.Height < 24);
        return reminders;
    }, SettingsApi());

    [Fact]
    public Task Buchhaltung_Kontenzuordnung_hat_keine_Erklaerung_in_der_Beschriftung_und_gleich_gestaltete_Exporte() => Render("hell", "geld-buchhaltung-beschriftung", async (window, main) =>
    {
        var shell = await OpenArea(window, main, "Buchhaltung");

        var labels = FieldLabels(window);
        Assert.Contains("Karte", labels);
        Assert.Contains("Kassa bar", labels);
        Assert.All(labels.Where(l => l != "Beginn Wirtschaftsjahr (Monat)"), l => Assert.DoesNotContain("(", l, StringComparison.Ordinal));
        Assert.True(Shows(window, "Verrechnung des Kartenterminals."));

        // Kein Export ist hervorgehoben: kein primärer Knopf in der Reihe.
        foreach (var name in ExportButtons)
        {
            Assert.DoesNotContain("primary", Named<Button>(window, name).Classes);
        }

        // Kurze Werte stehen in kurzen Feldern (5.2): Kontonummer 120, Kostenstelle 160.
        Assert.Equal(120, Named<TextBox>(window, "RevenueAccountCodeField").Bounds.Width, 0.5);
        Assert.Equal(160, Named<TextBox>(window, "RevenueAccountCenterField").Bounds.Width, 0.5);
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Postfach_Passwort_zeigt_den_Zustand_als_Hilfetext_und_Entfernen_steht_rechts_getrennt() => Render("hell", "geld-postfach-fuss", async (window, main) =>
    {
        var shell = await OpenIntake(window, main);

        // Das Passwortfeld hat keinen Platzhalter; der Zustand steht als Hilfetext darunter.
        Assert.True(string.IsNullOrEmpty(Named<TextBox>(window, "SecretField").PlaceholderText));
        Assert.True(Shows(window, "Gespeichert. Leer lassen, um es zu behalten."));

        // „Jetzt prüfen“ ist ein Knopf; „Postfach entfernen“ steht rechts außen, nicht neben Speichern.
        var check = Named<Button>(window, "CheckButton");
        var remove = Named<Button>(window, "RemoveButton");
        Assert.DoesNotContain("ghost", check.Classes);
        var gap = LeftEdge(window, remove) - RightEdge(window, check);
        Assert.True(gap > 48, $"Abstand zwischen „Jetzt prüfen“ und „Postfach entfernen“: {gap}");
        return shell.AdminPage;
    }, IntakeApi());

    [Fact]
    public Task Kosten_Geldfelder_tragen_die_Einheit_und_der_Name_der_Behandlung_sieht_nicht_wie_ein_Knopf_aus() => Render("hell", "geld-kosten-einheit", async (window, main) =>
    {
        var shell = await OpenArea(window, main, "Kosten");

        var labels = FieldLabels(window);
        Assert.Contains("Raumpflege pro Stunde (€)", labels);
        Assert.Contains("Standard-Stundensatz Personal (€)", labels);
        Assert.Contains("Gemeinkosten pro Monat (€)", labels);
        Assert.True(Shows(window, "Miete und Nebenkosten pro Monat (€)"));
        Assert.True(Shows(window, "Pflegematerial je Reinigung (€)"));

        // Der Name öffnet die Herleitung, hat aber keine Knopffläche (Klasse „more“ wie ein Ghost-Knopf).
        var toggle = window.GetVisualDescendants().OfType<ToggleButton>().First(t => t.IsEffectivelyVisible && t.Content as string == "Klassische Massage");
        Assert.Contains("more", toggle.Classes);

        // Die Meldung nennt den Weg: wo Sätze, Räume und Material ergänzt werden.
        Assert.True(Shows(window, "trägt man weiter unten ein"));
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Versandprotokoll_Zeilen_haben_einen_Namen_und_einen_Hinweis_am_Pfeil() => Render("hell", "geld-versandprotokoll-namen", async (window, main) =>
    {
        var reminders = await OpenSettingsArea<RemindersViewModel>(window, main, "Erinnerungen");
        await reminders.Log.OpenAsync(DispatchFilter.All);
        await Pump(Settling);

        // Die Zeile mit Text oder Grund sagt, was der Pfeil „▸“ tut; ohne Detail gibt es keinen Hinweis.
        var withDetail = reminders.Log.Rows.First(r => r.HasDetail);
        Assert.Equal("Text der Nachricht anzeigen", withDetail.ToggleTip);
        Assert.StartsWith(withDetail.Recipient, withDetail.ToggleName, StringComparison.Ordinal);
        Assert.Contains("Text der Nachricht anzeigen", withDetail.ToggleName, StringComparison.Ordinal);
        withDetail.ToggleCommand.Execute(null);
        Assert.Equal("Text der Nachricht ausblenden", withDetail.ToggleTip);
        return reminders;
    }, SettingsApi());

    [Fact]
    public Task Betriebsinfo_Neuer_Eintrag_ist_der_primaere_Knopf_der_Liste() => Render("hell", "geld-betriebsinfo-neu", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.BusinessInfoTab);

        Assert.Contains("primary", Named<Button>(window, "NewBusinessContactButton").Classes);
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Mein_Tag_Bearbeiten_der_Vorlieben_ist_ein_Knopf_und_kein_Ghost() => Render("hell", "geld-mein-tag-bearbeiten", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.DayTab);

        var edit = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Content as string == "Bearbeiten");
        Assert.DoesNotContain("ghost", edit.Classes);
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Hygiene_Protokollknopf_sieht_wie_ein_Knopf_aus_und_die_Ausstattung_widerspricht_sich_nicht() => Render("hell", "geld-hygiene-knopf", async (window, main) =>
    {
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.HygieneTab);

        Assert.DoesNotContain("ghost", Named<Button>(window, "HygieneLogButton").Classes);
        Assert.True(Shows(window, "Ausstattung unvollständig: 1 Material unter Minimum"));
        Assert.False(Shows(window, "Ausstattung vollständig"));
        return shell.AdminPage;
    }, ReportsApi());
}
