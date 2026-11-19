using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>KI-Kommentar zum Managementbericht: über die Queue, nur Zahlen aus dem Bericht, erst ab sieben Tagen.</summary>
public sealed class ManagementSummaryTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Path(TestSpa spa, DateOnly from, DateOnly until) =>
        $"/api/reports/locations/{spa.Location.Id}/management?from={from:yyyy-MM-dd}&until={until:yyyy-MM-dd}";

    private static async Task<ManagementReportDto> WaitAsync(TestSpa spa, string path, Func<ManagementReportDto, bool> done)
    {
        for (var i = 0; ; i++)
        {
            var report = await spa.GetAsync<ManagementReportDto>(path, Ct);
            if (done(report) || i == 100)
            {
                return report;
            }

            await Task.Delay(100, Ct);
        }
    }

    [Fact]
    public async Task KI_Kommentar_kommt_ueber_die_Queue_aus_den_Zahlen_des_Berichts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var (from, until) = (TestSpa.Monday, TestSpa.Monday.AddDays(6));
        var period = $"Zeitraum: {from:dd.MM.yyyy} bis {until:dd.MM.yyyy}, verglichen mit dem gleich langen Zeitraum davor.";

        var report = await WaitAsync(spa, Path(spa, from, until), r => r.AiSummary is not null);

        Assert.Equal($"Kommentar zu: {period}", report.AiSummary);
        Assert.Equal(ManagementSummaries.Notice, report.AiNotice);
        Assert.False(report.AiSummaryPending);
        var facts = FakeManagementSummarizer.Requests.First(f => f.StartsWith(period, StringComparison.Ordinal));
        Assert.Contains("Kennzahlen:", facts, StringComparison.Ordinal);
        Assert.Contains("- Behandlungen: ", facts, StringComparison.Ordinal);
        Assert.Contains("(davor ", facts, StringComparison.Ordinal);
        Assert.Contains("Nicht erschienen: 0; storniert: 0", facts, StringComparison.Ordinal);
        // Keine Gästenamen an die KI.
        Assert.DoesNotContain(spa.Customer.LastName, facts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Kommentar_mit_erfundener_Zahl_wird_verworfen_und_ein_einzelner_Tag_bekommt_keinen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var (from, until) = (TestSpa.Monday.AddDays(7), TestSpa.Monday.AddDays(20));
        FakeManagementSummarizer.Answers[$"Zeitraum: {from:dd.MM.yyyy} bis {until:dd.MM.yyyy}, verglichen mit dem gleich langen Zeitraum davor."] =
            "Der Umsatz stieg um 12345 Euro.";

        var rejected = await WaitAsync(spa, Path(spa, from, until), r => !r.AiSummaryPending);
        var day = await spa.GetAsync<ManagementReportDto>(Path(spa, TestSpa.Monday, TestSpa.Monday), Ct);

        Assert.Null(rejected.AiSummary);
        Assert.False(rejected.AiSummaryPending);
        Assert.Null(day.AiSummary);
        Assert.False(day.AiSummaryPending);
    }
}
