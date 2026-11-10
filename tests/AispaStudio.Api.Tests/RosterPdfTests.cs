using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Dienstplan-Aushang als PDF (PDF-Ausgaben F, ADR 0092): A4 quer, Personen als Zeilen, die sieben Tage der Woche als Spalten mit Diensten, Abwesenheiten und Notizen. Es gilt nur der
/// veröffentlichte Stand; Entwürfe stehen nicht auf dem Aushang. Recht Leitung (<c>ManageLocation</c>), fremder Mandant 404. Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class RosterPdfTests(ApiFixture fixture)
{
    private static readonly DateOnly Monday = TestSpa.Monday;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(Guid locationId, string week = "2027-03-03") => $"/api/locations/{locationId}/roster/pdf?week={week}";

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid locationId, string week = "2027-03-03")
    {
        var response = await client.GetAsync(Url(locationId, week), Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    private static Task<RosterDayResult> PlanAsync(TestSpa spa, Guid employeeId, DateOnly date, RosterEntry[] entries, string? note = null, bool draft = false) =>
        spa.PutAsync<RosterDayResult>($"/api/locations/{spa.Location.Id}/roster/{employeeId}/{date:yyyy-MM-dd}", new RosterDayInput(entries, note, draft), Ct);

    /// <summary>
    /// Anna arbeitet montags 9 bis 17 Uhr (Wochenmuster), am Dienstag 10 bis 18 Uhr mit der Notiz „Schlüssel mitbringen“; Ben hat am Montag Urlaub und am Freitag eine Schulung.
    /// Annas Krankenstand am Mittwoch ist nur ein Entwurf.
    /// </summary>
    private static async Task<TestSpa> PlannedWeekAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await PlanAsync(spa, spa.Anna.Id, Monday.AddDays(1), [new(RosterKind.Work, spa.Location.Id, new TimeOnly(10, 0), new TimeOnly(18, 0))], "Schlüssel mitbringen");
        await PlanAsync(spa, spa.Ben.Id, Monday, [new(RosterKind.Vacation, null, null, null)]);
        await PlanAsync(spa, spa.Ben.Id, Monday.AddDays(4), [new(RosterKind.Training, null, null, null)]);
        await PlanAsync(spa, spa.Anna.Id, Monday.AddDays(2), [new(RosterKind.Sick, null, null, null)], draft: true);
        return spa;
    }

    [Fact]
    public async Task Der_Aushang_ist_quer_nennt_die_Woche_je_Person_eine_Zeile_und_die_Dienste_Abwesenheiten_und_Notizen()
    {
        var spa = await PlannedWeekAsync(fixture);

        var (response, pdf) = await PdfAsync(spa.Client, spa.Location.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Dienstplan-Wien-2027-03-01.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        PdfText.Keep(pdf, "dienstplan-aushang");
        using var document = PdfText.Open(pdf);
        Assert.True(document.Pages[0].Width.Point > document.Pages[0].Height.Point, "A4 quer");
        var text = PdfText.Text(pdf);
        Assert.Contains("Dienstplan 01.03. – 07.03.2027", text, StringComparison.Ordinal);
        Assert.Contains("Anna", text, StringComparison.Ordinal);
        Assert.Contains("Ben", text, StringComparison.Ordinal);
        Assert.Contains("09:00–17:00", text, StringComparison.Ordinal);
        Assert.Contains("10:00–18:00", text, StringComparison.Ordinal);
        Assert.Contains("Schlüssel", text, StringComparison.Ordinal);
        Assert.Contains("mitbringen", text, StringComparison.Ordinal);
        Assert.Contains("Urlaub", text, StringComparison.Ordinal);
        Assert.Contains("Schulung", text, StringComparison.Ordinal);
        Assert.Contains("07.03.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Entwurf_steht_nicht_auf_dem_Aushang_erst_nach_dem_Veroeffentlichen()
    {
        var spa = await PlannedWeekAsync(fixture);

        var (_, before) = await PdfAsync(spa.Client, spa.Location.Id);
        await spa.PostAsync<RosterPublishResult>($"/api/locations/{spa.Location.Id}/roster/publish", new RosterPublishRequest(Monday, Monday.AddDays(6)), Ct);
        var (_, after) = await PdfAsync(spa.Client, spa.Location.Id);

        Assert.DoesNotContain("Krank", PdfText.Text(before), StringComparison.Ordinal);
        Assert.Contains("Krank", PdfText.Text(after), StringComparison.Ordinal);
        Assert.Contains("Veröffentlicht am", PdfText.Text(after), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Jeder_Tag_der_Woche_gleicht_den_Montag_der_Woche_und_die_Woche_davor_ist_eine_andere_Datei()
    {
        var spa = await PlannedWeekAsync(fixture);

        var (monday, _) = await PdfAsync(spa.Client, spa.Location.Id, "2027-03-01");
        var (sunday, _) = await PdfAsync(spa.Client, spa.Location.Id, "2027-03-07");
        var (previous, previousPdf) = await PdfAsync(spa.Client, spa.Location.Id, "2027-02-24");

        Assert.Equal("Dienstplan-Wien-2027-03-01.pdf", monday.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("Dienstplan-Wien-2027-03-01.pdf", sunday.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("Dienstplan-Wien-2027-02-22.pdf", previous.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Contains("Dienstplan 22.02. – 28.02.2027", PdfText.Text(previousPdf), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Woche_ist_400_und_ein_unbekannter_Standort_404()
    {
        var spa = await PlannedWeekAsync(fixture);

        var missing = await spa.Client.GetAsync($"/api/locations/{spa.Location.Id}/roster/pdf", Ct);
        var unknown = await spa.Client.GetAsync(Url(Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Nur_die_Leitung_darf_den_Aushang_drucken_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await PlannedWeekAsync(fixture);
        using var manager = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Manager, [spa.Location.Id]);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (allowed, _) = await PdfAsync(manager, spa.Location.Id);
        var (receptionResponse, _) = await PdfAsync(reception, spa.Location.Id);
        var (therapistResponse, _) = await PdfAsync(therapist, spa.Location.Id);
        var (foreign, _) = await PdfAsync(other.Client, spa.Location.Id);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, receptionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, therapistResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
