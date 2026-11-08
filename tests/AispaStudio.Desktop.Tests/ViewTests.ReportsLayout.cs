using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using static AispaStudio.Desktop.Tests.ReportSample;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// „Zahlen“ nach R07 Slice 144 (Ä352): eine Reiterebene mit Standort und Datum rechts im Kopf, Tagesstart als Übersicht (Status, Werte, Zeitleiste, Kassa), Mein Tag und Hygiene mit Status
/// und Werten oben; Berichte und Betriebsinfo nur mit Recht. Geprüft wird als Inhaberin, das Ausblenden als Rezeption.
/// </summary>
public sealed partial class ViewTests
{
    private static readonly Guid LenaId = Guid.Parse("10000000-0000-0000-0000-0000000000a1");

    private static BriefingDto QuietDay(params BriefingRegister[] registers) => Briefing() with
    {
        Appointments = 15,
        Utilization = 70,
        ActionNeeded = [],
        Staff = [new BriefingStaff(PersonId, "Lisa", "Dienst", "09:00–17:00"), new BriefingStaff(LenaId, "Lena Huber", "Urlaub", null)],
        Registers = registers,
    };

    /// <summary>Die sichtbaren Reiter von „Zahlen“ (nicht die Gruppen der Verwaltung darüber), in der Reihenfolge der Leiste.</summary>
    private static List<Controls.TabButton> VisibleTabs(Window window) =>
        [.. window.GetVisualDescendants().OfType<Controls.TabButton>().Where(t => t.IsEffectivelyVisible && t.Name is { } name && name.StartsWith("Report", StringComparison.Ordinal))];

    [Fact]
    public Task Zahlen_hat_eine_Reiterebene_mit_Standort_und_Datum_rechts_im_Kopf() => Render("hell", "zahlen-reiter", async (window, main) =>
    {
        // Given die Inhaberin öffnet „Zahlen“; When der Tagesstart offen ist
        var (shell, reports) = await OpenReports(window, main, ReportsViewModel.BriefingTab);

        // Then gibt es genau eine Reiterebene (kein zweiter Streifen der Verwaltung), und Standort und Datum stehen rechts von den Reitern.
        Assert.False(Named<Controls.OverflowStrip>(window, "AreaStrip").IsEffectivelyVisible);
        Assert.Equal(["Tagesstart", "Mein Tag", "Hygiene", "Berichte", "Betriebsinfo"], VisibleTabs(window).Select(t => t.Content as string));
        var last = Named<Controls.TabButton>(window, "ReportBusinessInfoTab");
        Assert.True(LeftOf(window, Named<ComboBox>(window, "ReportLocationField")) >= LeftOf(window, last) + last.Bounds.Width);
        Assert.True(LeftOf(window, Named<Controls.DateField>(window, "ReportDateField")) > LeftOf(window, Named<ComboBox>(window, "ReportLocationField")));

        // „Berichte“ hat Management und Pflichtberichte als Unterreiter, sonst nirgends.
        Assert.DoesNotContain(VisibleTabs(window), t => t.Name == "ReportComplianceTab");
        reports.SelectTabCommand.Execute(ReportsViewModel.ReportsTab);
        await Pump(Settling);
        Assert.Equal(["Management", "Pflichtberichte"], VisibleTabs(window).Where(t => t.Name is "ReportManagementSubTab" or "ReportComplianceTab").Select(t => t.Content as string));
        Assert.True(Named<Controls.TabButton>(window, "ReportManagementTab").IsSelected);

        // Die Betriebsinfo hat weder Standort noch Datum.
        reports.SelectTabCommand.Execute(ReportsViewModel.BusinessInfoTab);
        await Pump(Settling);
        await Pump(Settling);
        Assert.False(Named<ComboBox>(window, "ReportLocationField").IsEffectivelyVisible);
        Assert.True(Shows(window, "Neuer Eintrag") || Shows(window, "Suchen"));
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Zahlen_blendet_der_Rezeption_Berichte_aus_und_zeigt_die_Betriebsinfo() => Render("hell", "zahlen-rezeption", async (window, main) =>
    {
        // Given eine Rezeptionistin ohne Berichtsrecht, die buchen darf; When sie „Zahlen“ öffnet
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.BriefingTab);

        // Then sieht sie Tagesstart, Mein Tag, Hygiene und die Betriebsinfo (T-025), aber nicht „Berichte“.
        Assert.Equal(["Tagesstart", "Mein Tag", "Hygiene", "Betriebsinfo"], VisibleTabs(window).Select(t => t.Content as string));
        return shell.AdminPage;
    }, ReportsApi().On("GET /api/auth/me", Reception()));

    [Theory]
    [InlineData("hell")]
    [InlineData("dunkel")]
    public Task Tagesstart_zeigt_Status_Werte_Zeitleiste_und_Kassa(string theme) => Render(theme, $"zahlen-tagesstart-{theme}", async (window, main) =>
    {
        // Given 15 Behandlungen, Lisa im Dienst, Lena Huber im Urlaub, ein Anfangsbestand und eine Warnung der Kassa
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.BriefingTab);

        // Then steht oben der ruhige Status, darunter die Werte, das Personal mit Zeitleiste und Chip „Urlaub“ und die Kassa mit Wert und Warnband.
        Assert.True(Shows(window, "Nichts Dringendes offen"));
        var values = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("bigvalue")).Select(t => t.Text).ToList();
        Assert.Equal(["15", "70 %", "1", "1", "250,00 €"], values);
        Assert.True(Shows(window, "Behandlungen"));
        Assert.True(Shows(window, "Lena Huber"));
        var chip = Named<Border>(window, "StaffSection").GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("pill") && b.IsEffectivelyVisible);
        Assert.Contains(chip.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Urlaub");
        Assert.True(Shows(window, "Kassa AT1: Signatureinheit nicht erreichbar."));

        // Die Zeitleiste von Lisa hat einen sichtbaren Dienstabschnitt, 3:1 gegen die Leiste.
        var bar = Assert.Single(window.GetVisualDescendants().OfType<Controls.StaffBar>(), b => b.IsEffectivelyVisible);
        var lane = bar.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("lane"));
        var span = bar.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("span"));
        Assert.True(span.Bounds.Width > 0 && span.Bounds.Width < lane.Bounds.Width);
        Assert.True(Contrast(ColorOf(span.Background), ColorOf(lane.Background)) >= 3);

        // Werte stehen in der Hauptfarbe, Bezeichnungen und Chip haben mindestens 4,5:1 gegen ihre Fläche.
        var view = window.GetVisualDescendants().OfType<ReportsView>().First();
        var fg = ColorOf(view, "FgBrush");
        var texts = view.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text) && t.Foreground is ISolidColorBrush && (t.Classes.Contains("bigvalue") || t.Classes.Contains("muted")
                || t.GetVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("pill"))))
            .ToList();
        Assert.True(texts.Count >= 8, $"Nur {texts.Count} Texte gefunden");
        Assert.All(texts.Where(t => t.Classes.Contains("bigvalue")), t => Assert.Equal(fg, ColorOf(t.Foreground)));
        Assert.All(texts, t =>
        {
            var behind = t.GetVisualAncestors().Select(a => a switch { Border b => b.Background, Panel p => p.Background, _ => null })
                .OfType<ISolidColorBrush>().FirstOrDefault(b => b.Color.A == 255 && b.Opacity >= 1)?.Color ?? ColorOf(view, "BgBrush");
            var contrast = Contrast(ColorOf(t.Foreground), behind);
            Assert.True(contrast >= 4.5, $"„{t.Text}“: Kontrast {contrast:0.00}");
        });
        return shell.AdminPage;
    }, ReportsApi().On(BriefingRoute, QuietDay(
        new BriefingRegister(Guid.Empty, "AT1", "EUR", 250m, ["Signatureinheit nicht erreichbar."]))));

    [Fact]
    public Task Tagesstart_mit_offenen_Punkten_zeigt_die_Warnung_und_die_Punkte() => Render("hell", "zahlen-tagesstart-warnung", async (window, main) =>
    {
        // Given zwei Punkte, die Aufmerksamkeit brauchen; When man den Tagesstart ansieht
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.BriefingTab);

        // Then steht oben „2 Punkte brauchen Aufmerksamkeit“ als Warnung und darunter beide Punkte, statt „Nichts Dringendes offen“.
        Assert.True(Shows(window, "2 Punkte brauchen Aufmerksamkeit"));
        Assert.False(Shows(window, "Nichts Dringendes offen"));
        Assert.True(Shows(window, "Anamnese fehlt bei Anna Berg"));
        Assert.True(Shows(window, "Saunatür klemmt"));
        return shell.AdminPage;
    }, ReportsApi().On(BriefingRoute, QuietDay() with { ActionNeeded = [new BriefingItem(AppointmentId, At(9), "Anamnese fehlt bei Anna Berg"), new BriefingItem(null, null, "Saunatür klemmt")] }));

    [Fact]
    public Task Mein_Tag_und_Hygiene_zeigen_oben_Status_und_Werte() => Render("hell", "zahlen-mein-tag-hygiene", async (window, main) =>
    {
        // When die Inhaberin „Mein Tag“ öffnet
        var (shell, reports) = await OpenReports(window, main, ReportsViewModel.DayTab);

        // Then stehen oben der Hinweis des Tages und die Werte, darunter der Termin als Abschnitt.
        Assert.True(Shows(window, "1 Hinweis zum Tag"));
        Assert.True(Shows(window, "Raumwechsel um 11:00"));
        Assert.True(Shows(window, "Vorbereitung offen"));
        Assert.True(Shows(window, "Klassische Massage (60 Min.), Raum 1"));

        // Und bei „Hygiene“ der Stand der Punkte mit den Werten.
        reports.SelectTabCommand.Execute(ReportsViewModel.HygieneTab);
        await Pump(Settling);
        await Pump(Settling);
        Assert.True(Shows(window, "2 Punkte offen, 1 überfällig"));
        Assert.True(Shows(window, "Überfällig"));
        Assert.True(Shows(window, "Tagesstart je Raum"));
        return shell.AdminPage;
    }, ReportsApi());
}
