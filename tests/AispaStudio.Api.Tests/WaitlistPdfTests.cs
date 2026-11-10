using System.Net;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Warteliste eines Tages als PDF (PDF-Ausgaben E, ADR 0092): Gast, Telefon, Wunsch, Zeitraum und Stand der offenen Wünsche, die an dem Tag gelten. Recht wie die Liste im
/// Kalender (<c>ViewCalendar</c>), fremder Mandant 404. Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class WaitlistPdfTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid locationId, string date = "2027-03-03")
    {
        var response = await client.GetAsync($"/api/locations/{locationId}/waitlist/pdf?date={date}", Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    [Fact]
    public async Task Die_Tagesliste_nennt_Gast_Telefon_Wunsch_Zeitraum_und_Stand()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await DayPlanPdfTests.JoinWaitlistAsync(spa, "Wanda");

        var (response, pdf) = await PdfAsync(spa.Client, spa.Location.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Warteliste-Wien-2027-03-03.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        PdfText.Keep(pdf, "warteliste");
        var text = PdfText.Text(pdf);
        Assert.Contains("Warteliste Mittwoch, 03.03.2027", text, StringComparison.Ordinal);
        Assert.Contains("Wanda Warte", text, StringComparison.Ordinal);
        Assert.Contains("+43 664 111", text, StringComparison.Ordinal);
        Assert.Contains("Massage", text, StringComparison.Ordinal);
        Assert.Contains("02.03.2027 bis 06.03.2027", text, StringComparison.Ordinal);
        Assert.Contains("nachmittags", text, StringComparison.Ordinal);
        Assert.Contains("Wartet", text, StringComparison.Ordinal);
        Assert.Contains("1 offener Wunsch", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nur_Wuensche_die_an_dem_Tag_gelten_stehen_in_der_Liste()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await DayPlanPdfTests.JoinWaitlistAsync(spa, "Wanda");
        await DayPlanPdfTests.JoinWaitlistAsync(spa, "Wolf");

        var (_, before) = await PdfAsync(spa.Client, spa.Location.Id, "2027-03-01");
        var (_, inside) = await PdfAsync(spa.Client, spa.Location.Id);
        var (_, after) = await PdfAsync(spa.Client, spa.Location.Id, "2027-03-07");

        Assert.Contains("Keine offenen Wünsche an diesem Tag", PdfText.Text(before), StringComparison.Ordinal);
        Assert.Contains("2 offene Wünsche", PdfText.Text(inside), StringComparison.Ordinal);
        Assert.Contains("Wolf Warte", PdfText.Text(inside), StringComparison.Ordinal);
        Assert.Contains("Keine offenen Wünsche an diesem Tag", PdfText.Text(after), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_geschlossener_Wunsch_steht_nicht_mehr_in_der_Liste()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await DayPlanPdfTests.JoinWaitlistAsync(spa, "Wanda");
        var entries = await spa.GetAsync<List<WaitlistStaffEntry>>($"/api/locations/{spa.Location.Id}/waitlist", Ct);
        await spa.PostAsync<WaitlistEntryDto>($"/api/waitlist/{entries.Single().Id}/close", new WaitlistCloseInput(WaitlistCloseReason.NoLongerWanted), Ct);

        var (_, pdf) = await PdfAsync(spa.Client, spa.Location.Id);

        Assert.DoesNotContain("Wanda Warte", PdfText.Text(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Rezeption_darf_die_Liste_drucken_das_Backoffice_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id], spa.Anna.Id);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (allowed, _) = await PdfAsync(reception, spa.Location.Id);
        var (forbidden, _) = await PdfAsync(backoffice, spa.Location.Id);
        var (foreign, _) = await PdfAsync(other.Client, spa.Location.Id);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
