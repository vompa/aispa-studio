using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Kennzahlen des Management-Berichts als eine Tabelle (Export/Import, Aufgabe D): dieselben Zahlen wie die Seite, Recht der Seite „Berichte“.</summary>
public sealed class ReportExportTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Kennzahlen_mit_Wert_und_Vorperiode_wie_auf_der_Seite()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var report = await spa.GetAsync<ManagementReportDto>($"/api/reports/locations/{spa.Location.Id}/management?from=2027-03-01&until=2027-03-07", Ct);

        using var response = await spa.Client.GetAsync($"/api/reports/locations/{spa.Location.Id}/management/export?from=2027-03-01&until=2027-03-07&format=xlsx", Ct);
        var sheet = await ExportResponses.SheetAsync(response, Ct);

        Assert.Equal("Kennzahlen_2027-03-01.xlsx", ExportResponses.FileName(response));
        Assert.Equal(["Kennzahl", "Wert", "Vorperiode", "Einheit"], ExportResponses.Headers(sheet));
        Assert.Equal(report.Kpis.Count + 1, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal(report.Kpis.Select(k => (k.Label, (double)k.Value)), Enumerable.Range(2, report.Kpis.Count).Select(r => (sheet.Row(r).Cell(1).GetString(), sheet.Row(r).Cell(2).GetDouble())));

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Equal($"Export Kennzahlen (Excel, {report.Kpis.Count} Zeilen, 1.3.2027 bis 7.3.2027) von Test Person", Assert.Single(history).Text);
    }

    [Fact]
    public async Task Recht_der_Seite_und_nur_der_eigene_Mandant()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var stranger = await TestUsers.ClientAsync(fixture, Ct);
        var path = $"/api/reports/locations/{spa.Location.Id}/management/export?from=2027-03-01&until=2027-03-07&format=csv";

        using var forReception = await reception.GetAsync(path, Ct);
        using var forStranger = await stranger.GetAsync(path, Ct);
        using var tooLong = await spa.Client.GetAsync($"/api/reports/locations/{spa.Location.Id}/management/export?from=2027-03-01&until=2028-03-07&format=csv", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, forReception.StatusCode);
        Assert.Contains(forStranger.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }
}
