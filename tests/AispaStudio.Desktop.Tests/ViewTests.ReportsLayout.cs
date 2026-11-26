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

        // „Berichte“ hat keine Unterreiter mehr (ADR 0098): Management und Pflichtberichte stehen als Abschnitte untereinander, die Seite hat eine einzige Schiene.
        reports.SelectTabCommand.Execute(ReportsViewModel.ManagementTab);
        await Pump(Settling);
        await Pump(Settling);
        Assert.True(Named<Controls.TabButton>(window, "ReportManagementTab").IsSelected);
        var view = window.GetVisualDescendants().OfType<ReportsView>().Single();
        Assert.Single(view.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("segmented") && b.IsEffectivelyVisible);
        Assert.Equal(5, VisibleTabs(window).Count);
        var management = Named<Control>(window, "ReportManagementSection");
        var compliance = Named<Control>(window, "ReportComplianceSection");
        Assert.True(management.IsEffectivelyVisible && compliance.IsEffectivelyVisible);
        Assert.True(TopOf(window, compliance) >= BottomOf(window, management));
        Assert.Equal(["Management", "Pflichtberichte"], view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("h1")).Select(t => t.Text));

        // Jeder Abschnitt hat seinen Kopf: Der Zeitraum steht bei Management, das Datum nur bei den Pflichtberichten, der Standort gilt für beide.
        Assert.True(Named<ComboBox>(window, "ReportLocationField").IsEffectivelyVisible);
        Assert.False(Named<Controls.DateField>(window, "ReportDateField").IsEffectivelyVisible);
        Assert.Contains(management, Named<Controls.DateField>(window, "ManagementFromField").GetVisualAncestors());
        Assert.Contains(compliance, Named<Controls.DateField>(window, "ReportComplianceDateField").GetVisualAncestors());

        // Die Betriebsinfo hat weder Standort noch Datum.
        reports.SelectTabCommand.Execute(ReportsViewModel.BusinessInfoTab);
        await Pump(Settling);
        await Pump(Settling);
        Assert.False(Named<ComboBox>(window, "ReportLocationField").IsEffectivelyVisible);
        Assert.True(Shows(window, "Neuer Eintrag") || Shows(window, "Suchen"));
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Management_Kacheln_zeigen_beim_Vorjahresvergleich_Pfeil_Zeichen_und_Screenreader_Namen() => Render("hell", "management-kacheln-vergleich", async (window, main) =>
    {
        // Given der Management-Bericht der Inhaberin bis heute, mit Vorjahr als Vergleich
        var (shell, reports) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
        reports.Management.SelectedComparison = ManagementReportViewModel.CompareChoices.Single(c => c.Key == "year");
        await Pump(Settling);
        await Pump(Settling);

        // Then steht der Hinweis auf den laufenden Tag, und jede Kachel nennt die Veränderung auch im Namen für Screenreader (Farbe nie allein)
        Assert.True(Named<TextBlock>(window, "ManagementTodayOpen").IsEffectivelyVisible);
        Assert.Equal("Vergleich: Vorjahr · 01.10.2025 bis 19.10.2025", Named<TextBlock>(window, "ManagementCompareInfo").Text);
        var tiles = window.GetVisualDescendants().OfType<Controls.StatTile>().ToList();
        var names = tiles.Select(t => Avalonia.Automation.AutomationProperties.GetName(t)).ToList();
        Assert.Contains("Umsatz brutto 19.250,97 €, 24 % unter dem Vorjahr", names);
        Assert.Contains("No-Show-Quote 5,0 %, 3,0 Prozentpunkte unter dem Vorjahr", names);

        // T-104: Vergleichszeitraum und „Heute läuft noch“ stehen in der Zeile von „Vergleichen mit“; die Veränderung ist eine Pille (ungünstig warn, günstig ok, sonst grau),
        // ohne grüne Fläche und loses Warnzeichen, jede Pille mit mindestens 4,5:1; die Kacheln füllen die Breite in gleich breiten Spalten.
        var comparison = Named<ComboBox>(window, "ManagementComparisonField");
        Assert.InRange(TopOf(window, Named<TextBlock>(window, "ManagementCompareInfo")), TopOf(window, comparison) - 24, BottomOf(window, comparison));
        Controls.StatTile Tile(string label) => tiles.Single(t => t.Label == label);
        Border Pill(string label) => Tile(label).GetVisualDescendants().OfType<Border>().Single(b => b.Name == "StatTileChange");
        Assert.Equal((true, false), (Pill("Umsatz brutto").Classes.Contains("warn"), Pill("Umsatz brutto").Classes.Contains("ok")));
        Assert.Contains("ok", Pill("No-Show-Quote").Classes);
        Assert.False(Pill("Gäste").Classes.Contains("ok") || Pill("Gäste").Classes.Contains("warn"));
        Assert.All(tiles, t => Assert.False(t.GetVisualDescendants().OfType<Controls.Icon>().Any(i => i.Name == "StatTileIcon" && i.IsVisible), t.Label));
        foreach (var pill in tiles.Select(t => t.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "StatTileChange")).Where(b => b.IsVisible))
        {
            var text = (TextBlock)pill.Child!;
            var contrast = Contrast(ColorOf(text.Foreground), ColorOf(pill.Background));
            Assert.True(contrast >= 4.5, $"„{text.Text}“: Kontrast {contrast:0.00}");
        }

        var grid = Tile("Umsatz brutto").GetVisualAncestors().OfType<Controls.TileGrid>().First();
        Assert.True(grid.Columns >= 2);
        Assert.Equal(LeftOf(window, grid) + grid.Bounds.Width, LeftOf(window, Tile("Umsatz brutto")) + Tile("Umsatz brutto").Bounds.Width * grid.Columns + 12 * (grid.Columns - 1), 1);
        return shell.AdminPage;
    }, ReportsApi().On(ManagementRoute(new DateOnly(2026, 10, 1), Today) + "&compare=year", new ManagementReportDto(LocationId, new DateOnly(2026, 10, 1), Today,
        [
            new Kpi("revenue", "Umsatz brutto", 19250.97m, 25371m, "€", true),
            new Kpi("revenueNet", "Umsatz netto", 16041.48m, 21142.50m, "€", true),
            new Kpi("appointments", "Behandlungen", 212m, 190m, "", true),
            new Kpi("staffUtilization", "Auslastung Personal", 71.5m, 68m, "%", true),
            new Kpi("noShowRate", "No-Show-Quote", 5m, 8m, "%", false),
            new Kpi("customers", "Gäste", 150m, 150m, "", true),
            new Kpi("voucherLiabilities", "Offene Gutscheine (heute)", 4200m, null, "€"),
        ],
        // Erlöskonten und Zahlungsarten ergeben den Umsatz der Kacheln (brutto 19.250,97 €, netto 16.041,48 €), damit das Bild nicht „Keine Umsätze im Zeitraum.“ daneben zeigt
        [new RevenueLine("Behandlungen (SPA)", null, 15420.47m, 12850.39m), new RevenueLine("Waren", null, 3830.50m, 3191.09m)],
        [new RevenueLine("Karte", null, 12680.97m, 0m), new RevenueLine("Bar", null, 4320.00m, 0m), new RevenueLine("Gutschein", null, 2250.00m, 0m)],
        [], [], [], false, CompareFrom: new DateOnly(2025, 10, 1), CompareUntil: new DateOnly(2025, 10, 19))));

    [Fact]
    public Task Management_zeigt_Kacheln_in_Gruppen_und_den_Verlauf_als_Säulen_mit_Tabelle() => Render("hell", "management-kennzahlen-verlauf", async (window, main) =>
    {
        // Given der Management-Bericht der Inhaberin mit Kennzahlen aus vier Gruppen und einem Verlauf über drei Tage
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
        await Pump(Settling);

        // Then stehen die Kacheln unter ihren Überschriften, und der höchste Umsatz füllt die Höhe, die Hälfte davon die halbe Höhe
        var groups = Named<ItemsControl>(window, "ManagementKpiGroups");
        Assert.Equal(["Umsatz und Ertrag", "Auslastung", "Gäste", "Ausfälle"],
            groups.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("h2")).Select(t => t.Text));
        var revenue = Named<Controls.TrendBars>(window, "TrendRevenueChart");
        var plot = revenue.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "TrendPlot");
        Assert.Equal([Controls.TrendBars.ChartHeight / 2, Controls.TrendBars.ChartHeight, 0], plot.Children.OfType<Border>().Select(b => b.Bounds.Height));
        Assert.Equal(Avalonia.Automation.AccessibilityView.Raw, Avalonia.Automation.AutomationProperties.GetAccessibilityView(plot));

        // And für Screenreader stehen dieselben Werte in der aufklappbaren Tabelle
        var toggle = revenue.GetVisualDescendants().OfType<Expander>().Single();
        Assert.Equal("Umsatz ohne Gutscheinverkauf (netto): Werte als Tabelle", Avalonia.Automation.AutomationProperties.GetName(toggle));
        toggle.IsExpanded = true;
        await Pump(Settling);
        Assert.Equal(["Mo 12.10.", "500,00 €", "Di 13.10.", "1.000,00 €", "Mi 14.10.", "0,00 €"],
            revenue.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "TrendTable").Children.OfType<TextBlock>().Select(t => t.Text));

        // Das Bild zeigt den Verlauf, nicht die Kacheln darüber.
        revenue.BringIntoView();
        await Pump(Settling);
        return shell.AdminPage;
    }, ReportsApi().On(ManagementRoute(new DateOnly(2026, 10, 1), Today), new ManagementReportDto(LocationId, new DateOnly(2026, 10, 1), Today,
        [
            new Kpi("revenue", "Umsatz brutto", 19250.97m, 25371m, "€", true, "Umsatz und Ertrag"),
            new Kpi("contributionMargin", "Deckungsbeitrag", 6120.40m, 5980m, "€", true, "Umsatz und Ertrag"),
            new Kpi("serviceRevenuePerShiftHour", "Umsatz je Dienststunde", 48.20m, 51m, "€", true, "Umsatz und Ertrag"),
            new Kpi("staffUtilization", "Auslastung Personal", 71.5m, 68m, "%", true, "Auslastung"),
            new Kpi("customers", "Gäste", 150m, 150m, "", true, "Gäste"),
            new Kpi("lostRevenueNoShow", "Entgangen durch No-Show", 316m, 158m, "€", false, "Ausfälle"),
        ], [], [], [], [], [], Trend:
        [
            new TrendPoint(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), 500m, 6, 60m),
            new TrendPoint(new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 13), 1000m, 11, 82.5m),
            new TrendPoint(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14), 0m, 0, 0m),
        ])));

    [Fact]
    public Task Management_zeigt_nach_dem_Verlauf_die_Tabellen_und_kennzeichnet_schwache_Tageszeiten_im_Text() => Render("hell", "management-tabellen", async (window, main) =>
    {
        // Given der Management-Bericht der Inhaberin mit Auslastung je Tageszeit, Vorschau, Räumen, Kanälen und Gästen
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
        await Pump(Settling);

        // Then stehen die Tabellen nach dem Verlauf, in dieser Reihenfolge und sichtbar
        var load = Named<StackPanel>(window, "ManagementLoadByWeekday");
        var page = (StackPanel)load.Parent!;
        string[] sections = ["ManagementTrend", "ManagementLoadByWeekday", "ManagementForecast", "ManagementRooms", "ManagementChannels", "ManagementTopGuests", "ManagementLapsingGuests"];
        var positions = sections.Select(name => page.Children.IndexOf(page.Children.Single(c => c.Name == name))).ToList();
        Assert.Equal(positions.Order(), positions);
        Assert.All(sections, name => Assert.True(Named<StackPanel>(window, name).IsVisible, name));

        // And ein schwaches Feld sagt „schwach“ im Text und für den Screenreader, nicht nur mit der Warnfläche; ohne Dienst steht ein Strich
        var cells = load.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("loadcell")).Take(4).ToList();
        Assert.Equal(["40,0 % · schwach", "72,5 %", "55,0 %", "–"], cells.Select(c => ((TextBlock)c.Child!).Text));
        Assert.Equal([true, false, false, false], cells.Select(c => c.Classes.Contains("weak")));
        Assert.Equal("Montag Vormittag: 40,0 %, schwach", Avalonia.Automation.AutomationProperties.GetName(cells[0]));

        load.BringIntoView();
        await Pump(Settling);
        return shell.AdminPage;
    }, ReportsApi().On(ManagementRoute(new DateOnly(2026, 10, 1), Today), new ManagementReportDto(LocationId, new DateOnly(2026, 10, 1), Today,
        [new Kpi("staffUtilization", "Auslastung Personal", 61.5m, 58m, "%", true, "Auslastung")], [], [], [], [], [],
        Trend: [new TrendPoint(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), 500m, 6, 60m)])
    {
        LoadByWeekday = [new WeekdayLoad(DayOfWeek.Monday, 40m, 72.5m, 55m, null), new WeekdayLoad(DayOfWeek.Saturday, 88m, 91.5m, 76m, 64m)],
        WeakLoadBelow = 50m,
        Forecast = [new ForecastWeek(Today, Today.AddDays(6), 42, 3318m, 68.5m), new ForecastWeek(Today.AddDays(7), Today.AddDays(13), 25, 1975m, 41m)],
        ForecastAsOf = Today,
        Rooms = [new RoomKpi(Guid.NewGuid(), "Raum Lotus", 64, 38)],
        Channels = [new ChannelKpi(AppointmentSource.Reception, "Rezeption", 51), new ChannelKpi(AppointmentSource.Online, "Online-Buchung", 23)],
        TopGuests = [new GuestKpi(Guid.NewGuid(), "Maria Berger", 4, 612m)],
        LapsingGuests = [new LapsingGuest(Guid.NewGuid(), "Karl Huber", new DateOnly(2026, 6, 30), 7)],
    }));

    [Fact]
    public Task Management_Kopf_hat_Titel_und_das_Menue_Ausgabe_mit_Drucken_PDF_Mail_Export_und_Mail_Kaestchen() => Render("hell", "management-kopf-ausgabe", async (window, main) =>
    {
        // Given der Management-Bericht der Inhaberin, die den Bericht per Mail bekommt
        var (shell, reports) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
        await Pump(Settling);

        // Then steht links der Titel, rechts in derselben Zeile „Ausgabe“ und „Mit KI auswerten“; die frühere Knopfreihe und „Exportieren“ gibt es nicht mehr
        var output = Named<Button>(window, "ManagementOutputButton");
        var title = Named<Grid>(window, "ManagementHeader").Children.OfType<TextBlock>().Single();
        Assert.Equal("Management", title.Text);
        Assert.True(LeftOf(window, output) > LeftOf(window, title) + title.Bounds.Width);
        Assert.InRange(TopOf(window, Named<Button>(window, "ManagementAnalyzeButton")), TopOf(window, output) - 1, TopOf(window, output) + 1);
        Assert.Empty(window.GetVisualDescendants().OfType<Controls.ExportButton>());
        Assert.Empty(window.GetVisualDescendants().OfType<Controls.DocumentActions>());

        // When sie „Ausgabe“ öffnet; Then stehen Drucken, PDF, Mail, die drei Exporte und unter einem Trenner das Kästchen für den Bericht per Mail
        output.Flyout!.ShowAt(output);
        await Pump(Settling);
        var items = window.GetVisualDescendants().OfType<MenuItem>().Where(i => i.IsEffectivelyVisible && i.Bounds.Height > 0).ToList();
        Assert.Equal(["Drucken …", "Als PDF speichern", "Per Mail senden …", "Bericht (Excel)", "Bericht (Word)", "Kennzahlen (CSV)", "Wochen- und Monatsbericht per Mail"],
            items.Select(i => i.Header as string));
        var mail = items[^1];
        Assert.Equal((MenuItemToggleType.CheckBox, true), (mail.ToggleType, mail.IsChecked));

        // And der Zustand ist in beiden Fällen sichtbar, nicht nur am Wortlaut (Fluent 2): gewählt ein Akzent-Kästchen mit Haken, abgewählt ein leeres Kästchen mit Rand
        // Das Kästchen steht in der Symbolspalte wie die Symbole der anderen Einträge; der Haken des Themes ist aus, damit das Menü einspaltig bleibt
        Border Box() => mail.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("menucheck") && b.IsEffectivelyVisible);
        bool Ticked() => Box().GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single().IsVisible;
        Assert.True(Ticked());
        Assert.DoesNotContain(mail.GetVisualDescendants().OfType<ContentControl>(), c => c.Name == "PART_ToggleIconPresenter" && c.IsVisible);
        Assert.Equal(LeftOf(window, Named<MenuItem>(window, "ManagementExcelItem").GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(c => c.Name == "PART_HeaderPresenter")),
            LeftOf(window, mail.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(c => c.Name == "PART_HeaderPresenter")), 1);

        // When sie das Kästchen anklickt; Then ist der Bericht per Mail abbestellt, und das Kästchen steht leer da (Rand mindestens 3:1 gegen die Menüfläche)
        mail.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        await Pump(Settling);
        Assert.Equal((false, false), (reports.Management.MailReports, mail.IsChecked));
        var empty = Box();
        Assert.False(Ticked());
        Assert.True(empty.BorderThickness.Left >= 1);
        var menuSurface = mail.GetVisualAncestors().OfType<MenuFlyoutPresenter>().First().Background;
        Assert.True(Contrast(ColorOf(empty.BorderBrush), ColorOf(menuSurface)) >= 3, "Rand des leeren Kästchens");
        return shell.AdminPage;
    }, ReportsApi().OnJson("PUT /api/reports/mail-subscription", body => new ReportMailSubscription(body!.Contains("true", StringComparison.Ordinal), true)));

    [Fact]
    public Task Management_Mit_KI_auswerten_ist_ohne_Claude_Schluessel_gesperrt_und_nennt_den_Grund() => Render("hell", "management-ki-auswertung-gesperrt", async (window, main) =>
    {
        // Given der Management-Bericht der Inhaberin ohne Claude-Schlüssel im Server (aiAnalysisAvailable fehlt)
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
        await Pump(Settling);

        // Then ist „Mit KI auswerten“ zu sehen, aber gesperrt, und der Tooltip sagt, wann es geht
        var button = Named<Button>(window, "ManagementAnalyzeButton");
        Assert.True(button.IsEffectivelyVisible);
        Assert.False(button.IsEffectivelyEnabled);
        Assert.Equal(ManagementReportViewModel.AnalysisUnavailableHint, ToolTip.GetTip(button));
        return shell.AdminPage;
    }, ReportsApi());

    [Fact]
    public Task Management_KI_Auswertung_steht_in_der_Seitenleiste_gegliedert_mit_Pille_und_Hinweis() => Render("hell", "verwaltung-berichte-management-ki-auswertung", async (window, main) =>
    {
        // Given der Management-Bericht der Inhaberin mit Claude-Schlüssel im Server
        var (shell, _) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
        await Pump(Settling);

        // When sie „Mit KI auswerten“ klickt; Then geht rechts die Seitenleiste mit 360 Pixel auf, „Auswerten“ fest im Fuß
        Named<Button>(window, "ManagementAnalyzeButton").Command!.Execute(null);
        await Pump(Settling);
        var panel = Named<Controls.SidePanel>(window, "ManagementAnalysisPanel");
        Assert.True(panel.IsEffectivelyVisible);
        Assert.Equal(360, panel.Bounds.Width);

        // When sie „Auswerten“ klickt; Then steht das Ergebnis in vier Teilen, das Formular ist zur Zeile mit Pille und „Ändern“ eingeklappt (T-104),
        // unter dem Ergebnis die Hinweiszeile mit dem Hinweis des Servers und „Warum?“ (Brief 5.28), im Fuß nur „Neu auswerten“
        Named<Button>(window, "AnalysisRun").Command!.Execute(null);
        await Pump(Settling);
        await Pump(Settling);
        var result = Named<StackPanel>(window, "AnalysisResult");
        Assert.True(result.IsEffectivelyVisible);
        Assert.False(Named<StackPanel>(window, "AnalysisForm").IsEffectivelyVisible);
        var summary = Named<Grid>(window, "AnalysisSummary");
        Assert.Contains(summary.GetVisualDescendants().OfType<AiPill>(), p => p.IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "AnalysisEdit").IsEffectivelyVisible);
        Assert.Equal(["Kernaussagen", "Auffälligkeiten", "Maßnahmen", "Was die Zahlen nicht zeigen"],
            result.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("h3")).Select(t => t.Text));
        var notice = Named<AiFooter>(window, "AnalysisNotice");
        Assert.True(result.IsVisualAncestorOf(notice));
        Assert.Equal(AnalysisNotice, notice.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DisclaimerText").Text);
        Assert.Contains(notice.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == AispaStudio.Desktop.Services.AiLabels.WhyTitle && t.IsEffectivelyVisible);
        Assert.True(TopOf(window, notice) < TopOf(window, Named<Button>(window, "AnalysisCopy")), "Hinweiszeile steht vor Kopieren.");
        Assert.Equal("Neu auswerten", Named<Button>(window, "AnalysisRun").Content);
        Assert.True(Named<Button>(window, "AnalysisAdopt").IsEffectivelyVisible);
        Assert.True(BottomOf(window, Named<Button>(window, "AnalysisRun")) <= BottomOf(window, panel), "„Auswerten“ steht im Fuß der Leiste.");

        // Der runde Knopf „KI und Hilfe“ verdeckt nichts im Fuß: 8 px Luft zum Knopf
        var fab = InWindow(window, Named<Button>(window, "CompanionFab")).Inflate(7);
        Assert.False(fab.Intersects(InWindow(window, Named<Button>(window, "AnalysisRun"))));

        // Die Auffälligkeit ist eine Karte mit Pille „Vermutung“; der Chip „Umsatz brutto“ markiert die Kachel im Bericht
        var findings = Named<ItemsControl>(window, "AnalysisFindings");
        Assert.Contains(findings.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Vermutung" && t.IsEffectivelyVisible);
        var chip = findings.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("chip"));
        Assert.True(chip.IsEffectivelyVisible);
        chip.Command!.Execute(null);
        await Pump(Settling);
        var tile = window.GetVisualDescendants().OfType<Controls.StatTile>().Single(t => t.Label == "Umsatz brutto");
        Assert.Contains("marked", tile.Classes);

        // Erlöskonto und Zahlungsart stehen nebeneinander und zeigen die Umsätze der Kachel
        Assert.False(Named<Controls.MainSideColumns>(window, "ManagementRevenueColumns").IsStacked);
        Assert.True(Shows(window, "Behandlungen (SPA)") && Shows(window, "Karte"));
        tile.BringIntoView();
        await Pump(Settling);
        return shell.AdminPage;
    }, ReportsApi()
        .On(ManagementRoute(new DateOnly(2026, 10, 1), Today), new ManagementReportDto(LocationId, new DateOnly(2026, 10, 1), Today,
            [
                new Kpi("revenue", "Umsatz brutto", 19250.97m, 25371m, "€", true, "Umsatz und Ertrag"),
                new Kpi("staffUtilization", "Auslastung Personal", 71.5m, 68m, "%", true, "Auslastung"),
            ],
            // Erlöskonten und Zahlungsarten ergeben zusammen den Umsatz der Kachel (19.250,97 €); vorher standen sie leer neben ihr („Keine Umsätze im Zeitraum.“)
            [new RevenueLine("Behandlungen (SPA)", null, 15420.47m, 12850.39m), new RevenueLine("Waren", null, 3830.50m, 3192.08m)],
            [new RevenueLine("Karte", null, 12680.97m, 0m), new RevenueLine("Bar", null, 4320.00m, 0m), new RevenueLine("Gutschein", null, 2250.00m, 0m)],
            [], [], [], false)
        {
            AiAnalysisAvailable = true,
        })
        .On($"POST /api/reports/locations/{LocationId}/management/analysis", new ManagementAnalysisDto(Guid.NewGuid(), LocationId, new DateOnly(2026, 10, 1), Today,
            "previous", ManagementAnalysisFocus.Overall, null, false, ManagementAnalysisStatus.Done, new ManagementAnalysisResult(
                ["Der Umsatz liegt 24 % unter der Vorperiode, die Auslastung Personal ist um 3,5 Prozentpunkte gestiegen.", "Mehr Termine bringen weniger Umsatz je Termin."],
                [new AnalysisFinding("Die Auslastung steigt, der Umsatz sinkt.", "Vermutlich mehr kürzere oder günstigere Behandlungen.", "Umsatz brutto")],
                [new AnalysisMeasure("Längere Behandlungen am Wochenende anbieten.", "Der Umsatz je Termin steigt im nächsten Monat.")],
                ["Ob Gäste wegen der Preise fernbleiben."]),
            AnalysisNotice, DateTimeOffset.UnixEpoch)));

    private const string AnalysisNotice = "Von der KI aus den Zahlen dieses Berichts ausgewertet. Ursachen sind Vermutungen; maßgeblich sind die Zahlen selbst.";

    [Fact]
    public Task Ein_Sprung_auf_die_Pflichtberichte_rollt_bei_Mindestfenster_zum_Abschnitt() => Render("hell", "zahlen-berichte-sprung", async (window, main) =>
    {
        // Given das Fenster am Minimum (1100 × 680) und „Berichte“ mit dem Vergleich: die Pflichtberichte liegen unter dem sichtbaren Bereich
        var (shell, reports) = await OpenReports(window, main, ReportsViewModel.ManagementTab);
        window.MinWidth = window.MinHeight = 0;
        (window.Width, window.Height) = (1100, 680);
        reports.Management.Compare = true;
        await Pump(Settling);
        await Pump(Settling);
        var scroll = Named<ScrollViewer>(window, "FormScroll");
        var compliance = Named<Control>(window, "ReportComplianceSection");
        Assert.True(TopOf(window, compliance) >= BottomOf(window, scroll), "Die Pflichtberichte müssen für den Test unter dem Bildrand liegen.");

        // When ein altes Sprungziel „compliance“ kommt (früher der Unterreiter)
        reports.Open(new AdminTarget(Section: ReportsViewModel.ComplianceTab));
        await Pump(Settling);
        await Pump(Settling);

        // Then bleibt „Berichte“ offen, und der Abschnitt beginnt im sichtbaren Bereich; die fünf Reiter stehen weiter in einer Zeile.
        Assert.True(Named<Controls.TabButton>(window, "ReportManagementTab").IsSelected);
        Assert.InRange(TopOf(window, compliance), TopOf(window, scroll) - 0.5, BottomOf(window, scroll) - 40);
        AssertTabsInOneLineWithinHeader(window);

        // Und „management“ rollt wieder nach oben.
        reports.Open(new AdminTarget(Section: ReportsViewModel.ManagementTab));
        await Pump(Settling);
        await Pump(Settling);
        Assert.InRange(TopOf(window, Named<Control>(window, "ReportManagementSection")), TopOf(window, scroll) - 0.5, BottomOf(window, scroll) - 40);
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
        Assert.True(Shows(window, "1 Vorbereitung offen, 1 Hinweis zum Tag"));
        Assert.True(Shows(window, "Raumwechsel um 11:00"));
        Assert.True(Shows(window, "Vorbereitung offen"));
        Assert.True(Shows(window, "10:00 Klassische Massage"));
        Assert.True(Shows(window, "60 Min."));

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
