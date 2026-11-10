using System.Net;
using AispaStudio.Api.Reporting;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

public sealed class BenchmarkTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(TestSpa spa, string query = "") =>
        $"/api/reports/locations/{spa.Location.Id}/benchmark?until={TestSpa.Monday:yyyy-MM-dd}&days=7{query}";

    [Fact]
    public async Task Vergleicht_die_eigenen_Kennzahlen_mit_der_Vergleichsgruppe()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var done = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var missed = await spa.BookOkAsync(TestSpa.At(12), Ct, spa.Ben.Id);
        await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        await spa.SetStatusAsync(done.Id, AppointmentStatus.Completed, Ct);
        await spa.SetStatusAsync(missed.Id, AppointmentStatus.NoShow, Ct);

        var report = await spa.GetAsync<BenchmarkReport>(Url(spa), Ct);

        Assert.Equal((new DateOnly(2027, 2, 23), TestSpa.Monday), (report.From, report.Until));
        Assert.Equal("Day-Spas in Österreich mit bis zu 10 Mitarbeitenden", report.PeerGroup);
        Assert.True(report.IsSampleData);
        Assert.True(report.PeerCount >= 10);
        Assert.Contains(report.Notes, n => n.Contains("Beispieldaten", StringComparison.Ordinal));
        Assert.Contains(report.Notes, n => n.StartsWith("Nur 2 Behandlungen", StringComparison.Ordinal));
        Assert.Equal(8, report.Metrics.Count);

        var noShow = report.Metrics.Single(m => m.Key == "noShowRate");
        Assert.Equal(33.3m, noShow.Own);
        Assert.False(noShow.HigherIsBetter);
        Assert.Equal(BenchmarkRating.Bottom, noShow.Rating);
        Assert.Contains("Erinnerung", noShow.Tip, StringComparison.Ordinal);
        var perHour = report.Metrics.Single(m => m.Key == "revenuePerTreatmentHour");
        Assert.True(perHour.Own > 0);
        Assert.All(report.Metrics.Where(m => m.Rating is BenchmarkRating.Top or BenchmarkRating.AboveMedian), m => Assert.Null(m.Tip));
    }

    [Fact]
    public async Task Ohne_Termine_gibt_es_keine_eigenen_Werte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var report = await spa.GetAsync<BenchmarkReport>(Url(spa), Ct);

        Assert.NotEmpty(report.Metrics);
        Assert.All(report.Metrics, m => Assert.Equal((null, null, null), (m.Own, m.Rating, m.Tip)));
    }

    [Fact]
    public async Task Nur_mit_Finanzrecht_und_nur_fuer_eigene_Standorte()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync(Url(spa), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync(Url(spa), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await spa.Client.GetAsync($"/api/reports/locations/{spa.Location.Id}/benchmark?days=3", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await spa.Client.GetAsync($"/api/reports/locations/{spa.Location.Id}/benchmark", Ct)).StatusCode);
    }

    [Theory]
    [InlineData(110, true, BenchmarkRating.Top)]
    [InlineData(90, true, BenchmarkRating.AboveMedian)]
    [InlineData(80, true, BenchmarkRating.BelowMedian)]
    [InlineData(60, true, BenchmarkRating.Bottom)]
    [InlineData(2, false, BenchmarkRating.Top)]
    [InlineData(4, false, BenchmarkRating.AboveMedian)]
    [InlineData(6, false, BenchmarkRating.BelowMedian)]
    [InlineData(9, false, BenchmarkRating.Bottom)]
    public void Bewertung_beruecksichtigt_die_Richtung(int value, bool higherIsBetter, BenchmarkRating expected)
    {
        var quartiles = higherIsBetter ? new PeerQuartiles(72, 86, 101) : new PeerQuartiles(7.5m, 4.5m, 2.5m);

        Assert.Equal(expected, Benchmark.Rate(value, quartiles, higherIsBetter));
    }
}
