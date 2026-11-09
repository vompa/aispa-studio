using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Stundenliste einer Person als PDF (PDF-Ausgaben F, ADR 0091): je Tag Kommen, Gehen, Pausen, Ist, Soll und Saldo, dazu die Monatssumme und Unterschriftenzeilen. Die Zeiten kommen aus
/// derselben Rechnung wie die Zeiten-Maske (<c>TimesheetDay</c>). Recht Leitung (<c>ManageLocation</c>, wie die Zeiten), fremder Mandant 404. Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class TimesheetPdfTests(ApiFixture fixture)
{
    /// <summary>Ein vergangener Montag, an dem Anna laut Wochenarbeitszeit 9 bis 17 Uhr arbeitet (Juni 2026: die Montage 1., 8., 15., 22. und 29.).</summary>
    private static readonly DateOnly June1 = new(2026, 6, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(Guid employeeId, string month = "2026-06") => $"/api/employees/{employeeId}/timesheet/pdf?month={month}";

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid employeeId, string month = "2026-06")
    {
        var response = await client.GetAsync(Url(employeeId, month), Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    private static async Task AddAsync(TestSpa spa, Guid employeeId, DateOnly date, int fromHour, int fromMinute, int untilHour, int untilMinute)
    {
        var response = await spa.Client.PostAsJsonAsync($"/api/locations/{spa.Location.Id}/time-records",
            new TimeRecordInput(employeeId, TestSpa.At(fromHour, fromMinute, date), TestSpa.At(untilHour, untilMinute, date), "Stempeln vergessen"), TestSpa.Json, Ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Anna war am 1. Juni von 9:00 bis 13:00 und von 13:30 bis 17:30 da (Ist 8, Pause 30) und am 8. Juni nur von 9:00 bis 12:00 (Ist 3 statt 8).</summary>
    private static async Task<TestSpa> WorkedAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await AddAsync(spa, spa.Anna.Id, June1, 9, 0, 13, 0);
        await AddAsync(spa, spa.Anna.Id, June1, 13, 30, 17, 30);
        await AddAsync(spa, spa.Anna.Id, June1.AddDays(7), 9, 0, 12, 0);
        return spa;
    }

    [Fact]
    public async Task Die_Stundenliste_nennt_je_Tag_Kommen_Gehen_Pause_Ist_Soll_Saldo_und_die_Monatssumme()
    {
        var spa = await WorkedAsync(fixture);

        var (response, pdf) = await PdfAsync(spa.Client, spa.Anna.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Stundenliste-Anna-2026-06.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        PdfText.Keep(pdf, "stundenliste");
        var text = PdfText.Text(pdf);
        Assert.Contains("Stundenliste Juni 2026", text, StringComparison.Ordinal);
        Assert.Contains("Anna", text, StringComparison.Ordinal);
        foreach (var heading in new[] { "Kommen", "Gehen", "Pause", "Ist", "Soll", "Saldo" })
        {
            Assert.Contains(heading, text, StringComparison.Ordinal);
        }

        Assert.Contains("01.06.2026", text, StringComparison.Ordinal);
        Assert.Contains("09:00", text, StringComparison.Ordinal);
        Assert.Contains("17:30", text, StringComparison.Ordinal);
        Assert.Contains("30 min", text, StringComparison.Ordinal);
        Assert.Contains("08.06.2026", text, StringComparison.Ordinal);
        Assert.Contains("12:00", text, StringComparison.Ordinal);
        Assert.Contains("-5", text, StringComparison.Ordinal);
        Assert.Contains("Monatssumme", text, StringComparison.Ordinal);
        // Fünf Montage mit je 8 Stunden Soll (40), gearbeitet 8 + 3 Stunden (11), Saldo -29.
        Assert.Contains("40", text, StringComparison.Ordinal);
        Assert.Contains("11", text, StringComparison.Ordinal);
        Assert.Contains("-29", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Stundenliste_hat_Unterschriftenzeilen_fuer_die_Person_und_die_Leitung()
    {
        var spa = await WorkedAsync(fixture);

        var (_, pdf) = await PdfAsync(spa.Client, spa.Anna.Id);

        var text = PdfText.Text(pdf);
        Assert.Contains("Unterschrift Anna", text, StringComparison.Ordinal);
        Assert.Contains("Unterschrift Leitung", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tage_ohne_Dienst_und_ohne_Zeiten_stehen_nicht_in_der_Liste_ein_Monat_ohne_Zeiten_zeigt_nur_das_Soll()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var (response, pdf) = await PdfAsync(spa.Client, spa.Anna.Id);

        response.EnsureSuccessStatusCode();
        var text = PdfText.Text(pdf);
        Assert.Contains("01.06.2026", text, StringComparison.Ordinal);
        Assert.DoesNotContain("02.06.2026", text, StringComparison.Ordinal);
        Assert.Contains("Tage ohne Dienst und ohne Zeiten sind nicht aufgeführt.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_ungueltiger_Monat_ist_400_und_eine_unbekannte_Person_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var invalid = await spa.Client.GetAsync(Url(spa.Anna.Id, "Juni"), Ct);
        var month13 = await spa.Client.GetAsync(Url(spa.Anna.Id, "2026-13"), Ct);
        var unknown = await spa.Client.GetAsync(Url(Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, month13.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Die_Leitung_und_das_Backoffice_duerfen_die_Stundenliste_drucken_die_Rezeption_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await WorkedAsync(fixture);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var anna = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (managerResponse, _) = await PdfAsync(manager, spa.Anna.Id);
        var (backofficeResponse, _) = await PdfAsync(backoffice, spa.Anna.Id);
        var (receptionResponse, _) = await PdfAsync(reception, spa.Anna.Id);
        var (annaResponse, _) = await PdfAsync(anna, spa.Anna.Id);
        var (foreign, _) = await PdfAsync(other.Client, spa.Anna.Id);

        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, backofficeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, receptionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, annaResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
