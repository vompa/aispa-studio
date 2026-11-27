using System.Net;
using AispaStudio.Contracts;
using ClosedXML.Excel;

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

        Assert.Equal("Kennzahlen_2027-03-01_bis_2027-03-07.xlsx", ExportResponses.FileName(response));
        var revenueRow = report.Kpis.ToList().FindIndex(k => k.Key == "revenue") + 2;
        var appointmentsRow = report.Kpis.ToList().FindIndex(k => k.Key == "appointments") + 2;
        Assert.Equal(("#,##0.00", "0.####"), (sheet.Row(revenueRow).Cell(2).Style.NumberFormat.Format, sheet.Row(appointmentsRow).Cell(2).Style.NumberFormat.Format));
        Assert.Equal(["Kennzahl", "Wert", "Vorperiode", "Einheit"], ExportResponses.Headers(sheet));
        Assert.Equal(report.Kpis.Count + 1, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal(report.Kpis.Select(k => (k.Label, (double)k.Value)), Enumerable.Range(2, report.Kpis.Count).Select(r => (sheet.Row(r).Cell(1).GetString(), sheet.Row(r).Cell(2).GetDouble())));

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.Equal($"Export Kennzahlen (Excel, {report.Kpis.Count} Zeilen, 1.3.2027 bis 7.3.2027) von Test Person", Assert.Single(history).Text);
    }

    [Fact]
    public async Task Kennzahl_mit_Zusatz_traegt_ihn_in_der_Excel_Zeile()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await AnswerKit.LocationAsync(spa, "Zweiter Standort", null);

        using var response = await spa.Client.GetAsync($"/api/reports/locations/{spa.Location.Id}/management/export?from=2027-03-01&until=2027-03-07&format=xlsx", Ct);
        var sheet = await ExportResponses.SheetAsync(response, Ct);

        // Given zwei Standorte; Then steht der Zusatz der Gutschein-Kachel in Klammern hinter ihrem Namen (T-129)
        Assert.Contains("Offene Gutscheine (heute) (ganzer Betrieb)", Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1).Select(r => sheet.Row(r).Cell(1).GetString()));
    }

    [Fact]
    public async Task Bericht_als_Arbeitsmappe_mit_Blaettern_Formaten_und_Druckeinrichtung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        _ = await spa.BookOkAsync(TestSpa.At(10), Ct);
        var report = await spa.GetAsync<ManagementReportDto>($"/api/reports/locations/{spa.Location.Id}/management?from=2027-03-01&until=2027-03-07", Ct);

        using var response = await spa.Client.GetAsync($"/api/reports/locations/{spa.Location.Id}/management/export?from=2027-03-01&until=2027-03-07&format=xlsx&content=report", Ct);
        using var workbook = await WorkbookAsync(response);

        Assert.Equal("Bericht_2027-03-01_bis_2027-03-07.xlsx", ExportResponses.FileName(response));
        var names = workbook.Worksheets.Select(s => s.Name).ToList();
        Assert.Equal(("Übersicht", "Hinweise"), (names[0], names[^1]));
        Assert.Contains("Gäste", names);
        var overview = workbook.Worksheet("Übersicht");
        Assert.Equal(["Kennzahl", "Wert", "Vorperiode", "Einheit"], ExportResponses.Headers(overview).Take(4));
        var revenueRow = report.Kpis.ToList().FindIndex(k => k.Key == "revenue") + 2;
        var utilizationRow = report.Kpis.ToList().FindIndex(k => k.Key == "staffUtilization") + 2;
        Assert.Equal(("#,##0.00", "0.0%"), (overview.Row(revenueRow).Cell(2).Style.NumberFormat.Format, overview.Row(utilizationRow).Cell(2).Style.NumberFormat.Format));
        Assert.Equal((1, XLPageOrientation.Landscape), (overview.SheetView.SplitRow, overview.PageSetup.PageOrientation));
        Assert.Contains(overview.ConditionalFormats, f => f.ConditionalFormatType == XLConditionalFormatType.IconSet);
        Assert.Contains(workbook.Worksheet("Verlauf").ConditionalFormats, f => f.ConditionalFormatType == XLConditionalFormatType.DataBar);

        var history = await spa.GetAsync<List<HistoryEntry>>("/api/exports/history", Ct);
        Assert.StartsWith("Export Bericht (Excel, ", Assert.Single(history).Text);
    }

    [Fact]
    public async Task Bericht_ohne_Recht_der_Kundenliste_ohne_Gaesteblatt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);

        using var response = await backoffice.GetAsync($"/api/reports/locations/{spa.Location.Id}/management/export?from=2027-03-01&until=2027-03-07&format=xlsx&content=report", Ct);
        using var workbook = await WorkbookAsync(response);

        var names = workbook.Worksheets.Select(s => s.Name).ToList();
        Assert.DoesNotContain("Gäste", names);
        Assert.Contains("Verlauf", names);
    }

    private static async Task<XLWorkbook> WorkbookAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        SampleFiles.Save(ExportResponses.FileName(response), bytes);
        return new XLWorkbook(new MemoryStream(bytes));
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
