using AispaStudio.Contracts;
using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Management-Bericht, Aufgabe T-093: Schnellwahl und Vergleich im Reiter, Veränderung mit Richtung auf den Kacheln. Wie die Zahlen entstehen, prüfen die API-Tests
/// (<c>ReportTests</c>); hier zählt, welche Anfrage die Maske stellt und was sie daraus zeigt.
/// </summary>
public sealed class ManagementCompareTests
{
    private sealed class Today(DateOnly day) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(day.Year, day.Month, day.Day, 10, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly LocationDto Location = StockSample.Salzburg;

    private static string Route(string range, string compare = "") =>
        $"GET /api/reports/locations/{Location.Id}/management?{range}{compare}";

    private static ManagementReportDto Report(params Kpi[] kpis) =>
        new(Location.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 26), kpis, [], [], [], [], [], false,
            CompareFrom: new DateOnly(2025, 11, 1), CompareUntil: new DateOnly(2025, 11, 26));

    private static async Task WaitForAsync(StubApi api, string route)
    {
        for (var i = 0; i < 200 && !api.Calls.Any(c => c.Route == route); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Schnellwahl_setzt_den_Zeitraum_und_der_Vergleich_geht_als_compare_an_die_API()
    {
        var empty = Report();
        var api = new StubApi()
            .On(Route("from=2026-11-01&until=2026-11-26"), empty)
            .On(Route("from=2026-10-01&until=2026-10-31"), empty)
            .On(Route("from=2026-10-01&until=2026-10-31", "&compare=year"), empty);
        var report = new ManagementReportViewModel(api.Client(), new Today(new DateOnly(2026, 11, 26)));
        report.SetRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 26));
        await report.ShowAsync(Location, new DateOnly(2026, 11, 26));

        // Given der Zeitraum reicht bis heute; Then steht der Hinweis, dass heute noch läuft
        Assert.True(report.IsTodayOpen);

        // When die Inhaberin „Letzter Monat“ wählt; Then gilt der ganze Oktober, der Hinweis entfällt, und die Anfrage bleibt ohne compare (Standard)
        report.SelectedQuickRange = ManagementReportViewModel.QuickRanges.Single(r => r.Label == "Letzter Monat");
        await WaitForAsync(api, Route("from=2026-10-01&until=2026-10-31"));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), (report.From, report.Until));
        Assert.False(report.IsTodayOpen);

        // When sie mit dem Vorjahr vergleicht; Then fragt die Maske denselben Zeitraum mit compare=year, die Schnellwahl bleibt stehen
        report.SelectedComparison = ManagementReportViewModel.CompareChoices.Single(c => c.Key == "year");
        await WaitForAsync(api, Route("from=2026-10-01&until=2026-10-31", "&compare=year"));
        Assert.Contains(api.Calls, c => c.Route == Route("from=2026-10-01&until=2026-10-31", "&compare=year"));
        Assert.NotNull(report.SelectedQuickRange);

        // When sie ein Datum von Hand ändert; Then verlässt die Schnellwahl ihren Zeitraum und zeigt „Eigener Zeitraum“ statt eines leeren Felds (T-104)
        report.From = new DateOnly(2026, 10, 5);
        Assert.Same(ManagementReportViewModel.CustomRange, report.SelectedQuickRange);
    }

    [Fact]
    public async Task Kachel_zeigt_Veraenderung_mit_Pfeil_Richtung_und_Screenreader_Satz()
    {
        var api = new StubApi().On(Route("from=2026-11-01&until=2026-11-26", "&compare=year"), Report(
            new Kpi("revenue", "Umsatz brutto", 19250.97m, 25371m, "€", true),
            new Kpi("noShowRate", "No-Show-Quote", 5m, 8m, "%", false),
            new Kpi("voucherLiabilities", "Offene Gutscheine (heute)", 100m, 50m, "€"),
            new Kpi("appointments", "Behandlungen", 40m, 40m, "", true),
            new Kpi("openRequests", "Offene Anfragen (heute)", 3m, null, "")));
        var report = new ManagementReportViewModel(api.Client(), new Today(new DateOnly(2026, 11, 26)))
        {
            SelectedComparison = ManagementReportViewModel.CompareChoices.Single(c => c.Key == "year"),
        };
        report.SetRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 26));
        await report.ShowAsync(Location, new DateOnly(2026, 11, 26));

        var tiles = report.Kpis.ToDictionary(k => k.Label);

        // Then ist weniger Umsatz ein Rückgang mit Warnzeichen („Vorjahr“ statt „Vorperiode“), weniger No-Shows in Prozentpunkten ein Fortschritt mit Haken,
        // ein neutraler Wert hat einen Pfeil ohne Zeichen, gleiche Werte stehen als „unverändert“, und ohne Vergleich bleibt nur der Wert
        Assert.Equal(("↓ 24 %", StatTileState.Warn, "24 % unter dem Vorjahr"), (tiles["Umsatz brutto"].Change, tiles["Umsatz brutto"].State, tiles["Umsatz brutto"].Spoken));
        Assert.Equal("Vorjahr 25.371,00 €", tiles["Umsatz brutto"].Previous);
        Assert.Equal(("↓ 3,0 Prozentpunkte", StatTileState.Ok, "3,0 Prozentpunkte unter dem Vorjahr"),
            (tiles["No-Show-Quote"].Change, tiles["No-Show-Quote"].State, tiles["No-Show-Quote"].Spoken));
        Assert.Equal(("↑ 100 %", StatTileState.None), (tiles["Offene Gutscheine (heute)"].Change, tiles["Offene Gutscheine (heute)"].State));
        Assert.Equal(("→ unverändert", StatTileState.None), (tiles["Behandlungen"].Change, tiles["Behandlungen"].State));
        Assert.Null(tiles["Offene Anfragen (heute)"].Hint);
        Assert.Equal("Vergleich: Vorjahr · 01.11.2025 bis 26.11.2025", report.CompareInfo);
    }

    [Fact]
    public async Task Management_Bericht_hat_Drucken_PDF_speichern_und_Senden_fuer_Zeitraum_und_Vergleich_der_Seite()
    {
        var api = new StubApi().On(Route("from=2026-11-01&until=2026-11-26", "&compare=year"), Report());
        var report = new ManagementReportViewModel(api.Client(), new Today(new DateOnly(2026, 11, 26)))
        {
            SelectedComparison = ManagementReportViewModel.CompareChoices.Single(c => c.Key == "year"),
        };
        report.SetRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 26));
        await report.ShowAsync(Location, new DateOnly(2026, 11, 26));

        // Then steht der Knopfblock mit Senden; PDF und Versand nehmen Standort, Zeitraum und Vergleich der Seite (ohne KI-Kommentar ohne ai=true)
        var document = Assert.IsType<DocumentActionsViewModel>(report.Document);
        Assert.True(document.HasSend);
        var root = $"api/reports/locations/{Location.Id}/management";
        Assert.Equal(($"{root}/pdf?from=2026-11-01&until=2026-11-26&compare=year", $"{root}/send?from=2026-11-01&until=2026-11-26&compare=year"),
            (document.Source.PdfPath, document.Source.SendPath));
    }
}
