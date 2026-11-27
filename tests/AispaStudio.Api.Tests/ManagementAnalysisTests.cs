using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>„Mit KI auswerten“ (T-096 E1): über die Queue, nur Zahlen aus den Fakten, Beträge nur mit Recht, keine Gästenamen, gespeichert je Fingerabdruck.</summary>
public sealed class ManagementAnalysisTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Path(TestSpa spa) => $"/api/reports/locations/{spa.Location.Id}/management/analysis";

    private static ManagementAnalysisRequest Week(string question, ManagementAnalysisFocus focus = ManagementAnalysisFocus.Overall) =>
        new(TestSpa.Monday, TestSpa.Monday.AddDays(6), focus, question);

    private static async Task<ManagementAnalysisDto> RequestAsync(HttpClient client, TestSpa spa, ManagementAnalysisRequest request) =>
        await TestSpa.ReadAsync<ManagementAnalysisDto>(await client.PostAsJsonAsync(Path(spa), request, TestSpa.Json, Ct), Ct);

    private static async Task<ManagementAnalysisDto> WaitAsync(HttpClient client, TestSpa spa, Guid id)
    {
        for (var i = 0; ; i++)
        {
            var analysis = (await client.GetFromJsonAsync<ManagementAnalysisDto>($"{Path(spa)}/{id}", TestSpa.Json, Ct))!;
            if (analysis.Status != ManagementAnalysisStatus.Pending || i == 100)
            {
                return analysis;
            }

            await Task.Delay(100, Ct);
        }
    }

    private static string FactsFor(string question) => FakeManagementAnalyst.Requests.Single(r => r.Question == question).Facts;

    [Fact]
    public async Task Auswertung_kommt_ueber_die_Queue_und_der_Handler_setzt_den_Mandanten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var requested = await RequestAsync(spa.Client, spa, Week("Was lief gut?"));
        var done = await WaitAsync(spa.Client, spa, requested.Id);

        Assert.Equal(ManagementAnalysisStatus.Done, done.Status);
        Assert.StartsWith("Ausgewertet: Zeitraum: ", Assert.Single(done.Result!.KeyFindings), StringComparison.Ordinal);
        Assert.Equal(ManagementAnalyses.Notice, done.Notice);
    }

    [Fact]
    public async Task Auswertung_mit_einer_Zahl_die_nicht_in_den_Fakten_steht_scheitert()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var question = "Fremde Zahl, bitte";
        FakeManagementAnalyst.Answers[question] = new ManagementAnalysisResult(
            ["Die Auslastung war ordentlich."], [], [new AnalysisMeasure("Eine Aktion am Dienstag.", "Der Umsatz steigt um 98765 Euro.")], []);

        var requested = await RequestAsync(spa.Client, spa, Week(question));
        var failed = await WaitAsync(spa.Client, spa, requested.Id);

        Assert.Equal(ManagementAnalysisStatus.Failed, failed.Status);
        Assert.Null(failed.Result);
    }

    [Fact]
    public async Task Ohne_Recht_auf_Betraege_stehen_keine_Betraege_in_den_Fakten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        var question = "Ohne Beträge";

        var requested = await RequestAsync(manager, spa, Week(question));
        var done = await WaitAsync(manager, spa, requested.Id);

        Assert.True(done.AmountsHidden);
        var facts = FactsFor(question);
        Assert.Contains("Behandlungen: ", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("€", facts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fakten_nennen_das_Personal_mit_dem_Namen_im_Haus_und_keine_Gaeste()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(spa.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            var anna = await db.Employees.SingleAsync(e => e.Id == spa.Anna.Id, Ct);
            anna.DisplayName = "Anna Huberbauer";
            anna.PreferredName = "Anni";
            await db.SaveChangesAsync(Ct);
        }

        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var question = "Wie war das Team?";

        var requested = await RequestAsync(spa.Client, spa, Week(question));
        await WaitAsync(spa.Client, spa, requested.Id);

        var facts = FactsFor(question);
        Assert.Contains("- Anni: Auslastung ", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("Huberbauer", facts, StringComparison.Ordinal);
        Assert.DoesNotContain(spa.Customer.LastName, facts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gleiche_Zahlen_gleicher_Schwerpunkt_und_gleiche_Frage_rufen_die_KI_nicht_erneut()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var question = "Zweimal dasselbe";

        var first = await RequestAsync(spa.Client, spa, Week(question));
        await WaitAsync(spa.Client, spa, first.Id);
        var again = await RequestAsync(spa.Client, spa, Week(question));
        var otherFocus = await RequestAsync(spa.Client, spa, Week(question, ManagementAnalysisFocus.GuestsAndLoyalty));
        await WaitAsync(spa.Client, spa, otherFocus.Id);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(ManagementAnalysisStatus.Done, again.Status);
        Assert.NotEqual(first.Id, otherFocus.Id);
        Assert.Equal(2, FakeManagementAnalyst.Requests.Count(r => r.Question == question));
    }

    private static string Document(TestSpa spa, string tail, Guid analysisId, string until = "2027-03-07") =>
        $"/api/reports/locations/{spa.Location.Id}/management/{tail}{(tail.Contains('?', StringComparison.Ordinal) ? "&" : "?")}from=2027-03-01&until={until}&analysisId={analysisId}";

    [Fact]
    public async Task Uebernommene_Auswertung_steht_mit_Hinweis_in_PDF_Word_und_Arbeitsmappe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var question = "Was nehmen wir in den Bericht?";
        FakeManagementAnalyst.Answers[question] = new ManagementAnalysisResult(
            ["Die Woche war ruhig."], [new AnalysisFinding("Wenig Termine am Montag.", "Vermutlich der Feiertag davor.", "Behandlungen")],
            [new AnalysisMeasure("Montags eine Aktion anbieten.", "Mehr Termine am Montag.")], ["Warum Gäste fernbleiben."]);
        var done = await WaitAsync(spa.Client, spa, (await RequestAsync(spa.Client, spa, Week(question))).Id);

        using var pdf = await spa.Client.GetAsync(Document(spa, "pdf", done.Id), Ct);
        using var word = await spa.Client.GetAsync(Document(spa, "export?format=docx", done.Id), Ct);
        using var excel = await spa.Client.GetAsync(Document(spa, "export?format=xlsx&content=report", done.Id), Ct);

        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var pdfText = PdfText.Text(PdfText.Keep(await pdf.Content.ReadAsByteArrayAsync(Ct), "management-bericht-ki-auswertung"));
        Assert.Contains("Kernaussage: Die Woche war ruhig.", pdfText, StringComparison.Ordinal);
        Assert.Contains("Mögliche Ursache (Vermutung)", pdfText, StringComparison.Ordinal);
        Assert.Contains("Von der KI aus den Zahlen", pdfText, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, word.StatusCode);
        using var document = WordprocessingDocument.Open(new MemoryStream(await word.Content.ReadAsByteArrayAsync(Ct)), false);
        var wordText = document.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.Contains("Woran Sie den Erfolg sehen: Mehr Termine am Montag.", wordText, StringComparison.Ordinal);
        Assert.Contains(ManagementAnalyses.Notice, wordText, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(await excel.Content.ReadAsByteArrayAsync(Ct)));
        var notes = workbook.Worksheet(ManagementReportDocument.NotesName).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Was die Zahlen nicht zeigen: Warum Gäste fernbleiben.", notes);
        Assert.Contains(ManagementAnalyses.Notice, notes);
    }

    [Fact]
    public async Task Auswertung_zu_anderem_Zeitraum_anderer_Sicht_oder_gescheitert_wird_fuer_den_Bericht_abgelehnt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var failedQuestion = "Scheitert für den Bericht";
        FakeManagementAnalyst.Answers[failedQuestion] = new ManagementAnalysisResult(["Plus 4711 Gäste."], [], [], []);
        var done = await WaitAsync(spa.Client, spa, (await RequestAsync(spa.Client, spa, Week("Passt nur zu dieser Woche"))).Id);
        var failed = await WaitAsync(spa.Client, spa, (await RequestAsync(spa.Client, spa, Week(failedQuestion))).Id);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);

        using var otherPeriod = await spa.Client.GetAsync(Document(spa, "pdf", done.Id, until: "2027-03-08"), Ct);
        using var notDone = await spa.Client.GetAsync(Document(spa, "export?format=xlsx&content=report", failed.Id), Ct);
        using var otherView = await manager.GetAsync(Document(spa, "export?format=docx", done.Id), Ct);

        Assert.Equal(ManagementAnalysisStatus.Failed, failed.Status);
        Assert.Equal(HttpStatusCode.BadRequest, otherPeriod.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notDone.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, otherView.StatusCode);
        Assert.Contains("passt nicht zu diesem Bericht", await otherPeriod.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }
}
