using System.Globalization;
using System.Net;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Management-Bericht als Word-Datei (T-095, Paket D3): aus demselben Berichtskern wie PDF und Arbeitsmappe, mit dem Open XML SDK zurückgelesen. Der Verlauf steht als Tabelle,
/// nach jeder Kennzahlen-Gruppe folgt „Anmerkungen:“, die Fußzeile trägt die Seitenzahl, der KI-Text nie ohne seinen Hinweis.
/// </summary>
public sealed class ManagementReportWordTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Bericht_als_Word_mit_Titel_Kennzahl_Anmerkungen_und_Seitenzahl_im_Export_Protokoll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var root = $"/api/reports/locations/{spa.Location.Id}/management";
        var report = await spa.GetAsync<ManagementReportDto>($"{root}?from=2027-03-01&until=2027-03-07", Ct);

        using var response = await spa.Client.GetAsync($"{root}/export?from=2027-03-01&until=2027-03-07&format=docx", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordDocumentBuilder.ContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Management-Bericht-Wien-2027-03-01-bis-2027-03-07.docx", response.Content.Headers.ContentDisposition?.FileNameStar);
        var bytes = Keep(await response.Content.ReadAsByteArrayAsync(Ct), "management-bericht");
        using var word = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var paragraphs = Paragraphs(word);
        Assert.Contains("Management-Bericht Wien", paragraphs);
        Assert.Contains("Anmerkungen:", paragraphs);
        Assert.Contains("Verlauf", paragraphs);
        var appointments = report.Kpis.Single(k => k.Key == "appointments");
        var row = word.MainDocumentPart!.Document!.Body!.Descendants<TableRow>().Select(r => r.Descendants<TableCell>().Select(c => c.InnerText).ToList())
            .First(cells => cells[0] == appointments.Label);
        Assert.Equal(((int)appointments.Value).ToString(CultureInfo.GetCultureInfo("de-AT")), row[2]);
        Assert.Contains(word.MainDocumentPart.FooterParts.Single().Footer!.Descendants<SimpleField>(), f => f.Instruction == "NUMPAGES");

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.StartsWith("Export Bericht (Word, ", Assert.Single(history).Text);
    }

    [Fact]
    public void KI_Text_steht_in_der_Word_Datei_immer_mit_seinem_Hinweis()
    {
        var header = new DocumentHeader("Test-Spa", [], ["Wien"], DateTimeOffset.UtcNow, TimeZoneInfo.Utc, "Lena");
        var report = new ManagementReportDto(Guid.NewGuid(), new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 7), [], [], [], [], [], [], false);
        var withAi = ManagementReportDocument.Build(report, header, "previous", false, null, "Die Auslastung stieg.", "Von der KI erstellt, bitte prüfen.");
        var withoutNotice = ManagementReportDocument.Build(report, header, "previous", false, null, "Die Auslastung stieg.", null);

        using var word = WordprocessingDocument.Open(new MemoryStream(Keep(ManagementReportWord.Build(withAi), "management-bericht-ki")), false);
        using var plain = WordprocessingDocument.Open(new MemoryStream(ManagementReportWord.Build(withoutNotice)), false);

        Assert.Contains("Die Auslastung stieg.", Paragraphs(word));
        Assert.Contains("Von der KI erstellt, bitte prüfen.", Paragraphs(word));
        Assert.DoesNotContain("Die Auslastung stieg.", Paragraphs(plain));
    }

    private static List<string> Paragraphs(WordprocessingDocument word) => [.. word.MainDocumentPart!.Document!.Body!.Descendants<Paragraph>().Select(p => p.InnerText)];

    /// <summary>Legt die Datei zur Sichtprüfung ab, wenn <c>AISPA_PDF_SAMPLES</c> einen Ordner nennt (wie <see cref="PdfText.Keep"/>); sonst nichts.</summary>
    private static byte[] Keep(byte[] word, string name)
    {
        if (Environment.GetEnvironmentVariable("AISPA_PDF_SAMPLES") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, $"{name}.docx"), word);
        }

        return word;
    }
}
