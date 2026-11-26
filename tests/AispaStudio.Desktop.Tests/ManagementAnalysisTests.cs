using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// „Mit KI auswerten“ (T-096 E2): die Seitenleiste legt die Auswertung an, fragt nach, bis sie fertig oder gescheitert ist, und hängt die übernommene Auswertung an
/// PDF, Word und Excel. Was die KI schreiben darf, prüfen die API-Tests (<c>ManagementAnalysisTests</c> dort); hier zählt, welche Anfragen die Maske stellt und was sie zeigt.
/// </summary>
public sealed class ManagementAnalysisTests
{
    private static readonly LocationDto Location = StockSample.Salzburg;
    private static readonly DateOnly From = new(2026, 11, 1);
    private static readonly DateOnly Until = new(2026, 11, 26);
    private static readonly Guid FailedId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid DoneId = Guid.Parse("c0000000-0000-0000-0000-000000000002");

    private static string Root => $"/api/reports/locations/{Location.Id}/management";

    private static ManagementAnalysisDto Analysis(Guid id, ManagementAnalysisStatus status, ManagementAnalysisResult? result = null) =>
        new(id, Location.Id, From, Until, "previous", ManagementAnalysisFocus.Overall, null, false, status, result, "Von der KI ausgewertet.", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Auswertung_laeuft_scheitert_ohne_Inhalt_und_ist_nach_Erneut_versuchen_fertig_und_im_PDF()
    {
        var gate = new TaskCompletionSource();
        var posts = 0;
        var result = new ManagementAnalysisResult(["Die Auslastung ist gestiegen."], [new AnalysisFinding("Montags wenig los.", "Vermutlich das Wetter.", "Auslastung Personal")],
            [new AnalysisMeasure("Montags eine Aktion.", "Mehr Termine am Montag.")], ["Warum Gäste fernbleiben."]);
        var api = new StubApi()
            .On($"GET {Root}?from=2026-11-01&until=2026-11-26",
                new ManagementReportDto(Location.Id, From, Until, [], [], [], [], [], [], false) { AiAnalysisAvailable = true })
            .OnJson($"POST {Root}/analysis", _ => ++posts == 1 ? Analysis(FailedId, ManagementAnalysisStatus.Failed) : Analysis(DoneId, ManagementAnalysisStatus.Pending))
            .On($"GET {Root}/analysis/{DoneId}", Analysis(DoneId, ManagementAnalysisStatus.Done, result))
            .Hold($"POST {Root}/analysis", gate.Task);
        var report = new ManagementReportViewModel(api.Client(), TimeProvider.System);
        report.SetRange(From, Until);
        await report.ShowAsync(Location, Until);
        var analysis = report.Analysis;
        analysis.PollDelay = TimeSpan.FromMilliseconds(10);
        Assert.True(report.AiAnalysisAvailable);

        // When die Inhaberin „Auswerten“ wählt; Then läuft die Auswertung, und „Auswerten“ ist gesperrt, bis sie zurück ist
        var first = analysis.AnalyzeCommand.ExecuteAsync(null);
        Assert.True(analysis.IsRunning);
        Assert.False(analysis.AnalyzeCommand.CanExecute(null));
        gate.SetResult();
        await first;

        // Then ist sie gescheitert: der Grund mit zwei Wegen, kein Text der KI, nichts zu übernehmen
        Assert.True(analysis.IsFailed);
        Assert.Equal(ManagementAnalysisViewModel.FailedText, analysis.FailureText);
        Assert.Empty(analysis.KeyFindings);
        Assert.False(analysis.CanAdopt);

        // When „Erneut versuchen“; Then fragt die Leiste nach, bis die Auswertung fertig ist, und zeigt sie gegliedert mit Hinweis
        await analysis.AnalyzeCommand.ExecuteAsync(null);
        Assert.True(analysis.IsDone);
        Assert.Equal(["Die Auslastung ist gestiegen."], analysis.KeyFindings);
        Assert.Equal("Vermutlich das Wetter.", Assert.Single(analysis.Findings).Cause);
        Assert.Equal("Woran Sie den Erfolg sehen: Mehr Termine am Montag.", Assert.Single(analysis.Measures).SuccessSign);
        Assert.Equal("Von der KI ausgewertet.", analysis.Notice);
        Assert.Contains(api.Calls, c => c.Route == $"GET {Root}/analysis/{DoneId}");

        // When sie die Auswertung in den Bericht übernimmt; Then tragen PDF und Versand die Auswertung als analysisId
        analysis.AdoptCommand.Execute(null);
        Assert.True(analysis.IsAdopted);
        Assert.EndsWith($"&analysisId={DoneId}", report.Document!.Source.PdfPath, StringComparison.Ordinal);
    }

    /// <summary>Bericht mit einer Kachel „Umsatz brutto“ und eine fertige Auswertung mit zwei Auffälligkeiten: eine zur Kachel, eine ohne Kachel.</summary>
    private static async Task<ManagementReportViewModel> AnalyzedReport()
    {
        var result = new ManagementAnalysisResult(["Weniger Umsatz."],
            [new AnalysisFinding("Der Umsatz sinkt.", "Vermutlich kürzere Behandlungen.", "umsatz brutto"), new AnalysisFinding("Weniger Stammgäste.", "Vermutlich der Urlaub.", "Stammgäste")],
            [], []);
        var api = new StubApi()
            .On($"GET {Root}?from=2026-11-01&until=2026-11-26",
                new ManagementReportDto(Location.Id, From, Until, [new Kpi("revenue", "Umsatz brutto", 900m, 1000m, "€", true)], [], [], [], [], [], false) { AiAnalysisAvailable = true })
            .On($"POST {Root}/analysis", Analysis(DoneId, ManagementAnalysisStatus.Done, result));
        var report = new ManagementReportViewModel(api.Client(), TimeProvider.System);
        report.SetRange(From, Until);
        await report.ShowAsync(Location, Until);
        await report.Analysis.AnalyzeCommand.ExecuteAsync(null);
        Assert.True(report.Analysis.IsDone);
        return report;
    }

    [Fact]
    public async Task Nach_der_Auswertung_ist_das_Formular_eingeklappt_und_Aendern_klappt_es_wieder_auf()
    {
        // Given eine fertige Auswertung (T-104); Then stehen Schwerpunkt und Frage nicht mehr offen, der Knopf heißt „Neu auswerten“, der Untertitel nennt den Zeitraum
        var analysis = (await AnalyzedReport()).Analysis;
        Assert.True(analysis.IsFormCollapsed);
        Assert.Equal("Neu auswerten", analysis.RunLabel);
        Assert.Equal("Management-Bericht · 01.11.–26.11.2026", analysis.Subtitle);

        // When sie „Ändern“ wählt; Then sind Schwerpunkt und Frage wieder offen, das Ergebnis bleibt
        analysis.EditCommand.Execute(null);
        Assert.True(analysis.IsFormOpen);
        Assert.True(analysis.IsDone);
    }

    [Fact]
    public async Task Kennzahl_Chip_markiert_die_passende_Kachel_und_ist_ohne_Kachel_nur_Text()
    {
        // Given eine Auffälligkeit zur Kachel „Umsatz brutto“ (die KI schreibt sie klein) und eine ohne Kachel
        var report = await AnalyzedReport();
        var (revenue, guests) = (report.Analysis.Findings[0], report.Analysis.Findings[1]);

        // Then hat nur die erste einen Befehl; When sie ihn auslöst; Then ist die Kachel mit ihrer Beschriftung markiert
        Assert.False(guests.HasTile);
        Assert.True(revenue.HasTile);
        revenue.Mark!.Execute(null);
        Assert.Equal("Umsatz brutto", report.MarkedKpi);
    }
}
