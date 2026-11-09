using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Stammblatt einer Kundin oder eines Kunden als PDF (PDF-Ausgaben E, ADR 0091): Kontakt, Wünsche und Vermerke, die nächsten Termine und die letzten fünf Besuche;
/// keine Gesundheitsdaten (nur der feste Satz, dass es welche gibt). Recht wie die Kundenkarte (<c>ViewCalendar</c>), fremder Mandant 404.
/// </summary>
public sealed class CustomerCardPdfTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid customerId)
    {
        var response = await client.GetAsync($"/api/customers/{customerId}/card/pdf", Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    private static async Task SubmitAnamnesisAsync(TestSpa spa, Guid customerId)
    {
        var link = await spa.PostAsync<AnamnesisTabletLink>($"/api/customers/{customerId}/anamnesis/tablet", new AnamnesisLinkRequest(null, "de"), Ct);
        using var tablet = spa.Fixture.CreateClient();
        var token = link.Url.Split("#/anamnese/")[1].Split('?')[0];
        List<AnamnesisAnswer> answers =
        [
            new("allergies", true, "Nüsse"), new("skin", false, null), new("circulation", false, null),
            new("pregnancy", false, null), new("medication", false, null), new("surgery", false, null), new("other", null, null),
        ];
        var submitted = await tablet.PostAsJsonAsync($"/api/online/anamnesis/{token}", new AnamnesisSubmission(answers, true, "Eva Muster", "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);
    }

    private static async Task<TestSpa> SpaWithGuestAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PutAsync<CustomerDto>($"/api/customers/{spa.Customer.Id}", new CustomerInput("Eva", spa.Customer.LastName, "eva@example.at", "+43 664 111", new DateOnly(1985, 4, 12),
            "Mag Lavendel", Address: new CustomerAddress("Graben 1", "1010", "Wien", "AT")), Ct);
        await spa.PutAsync<CustomerPreferences>($"/api/reports/customers/{spa.Customer.Id}/preferences",
            new CustomerPreferences("fest", "Lavendel", "24 °C", null, true, "Nacken", "Füße", null), Ct);
        await spa.Client.PutAsJsonAsync($"/api/customers/{spa.Customer.Id}/reception-note", new CustomerReceptionNoteInput("Kommt mit Hund"), TestSpa.Json, Ct);
        return spa;
    }

    [Fact]
    public async Task Das_Stammblatt_nennt_Kontakt_Wuensche_und_Vermerke()
    {
        var spa = await SpaWithGuestAsync(fixture);

        var (response, pdf) = await PdfAsync(spa.Client, spa.Customer.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"Stammblatt-Eva-{spa.Customer.LastName}.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        PdfText.Keep(pdf, "kundenkarte-stammblatt");
        var text = PdfText.Text(pdf);
        Assert.Contains($"Stammblatt Eva {spa.Customer.LastName}", text, StringComparison.Ordinal);
        Assert.Contains("eva@example.at", text, StringComparison.Ordinal);
        Assert.Contains("+43 664 111", text, StringComparison.Ordinal);
        Assert.Contains("Graben 1", text, StringComparison.Ordinal);
        Assert.Contains("1010 Wien", text, StringComparison.Ordinal);
        Assert.Contains("Mag Lavendel", text, StringComparison.Ordinal);
        Assert.Contains("Kommt mit Hund", text, StringComparison.Ordinal);
        Assert.Contains("Lavendel", text, StringComparison.Ordinal);
        Assert.Contains("Nacken", text, StringComparison.Ordinal);
        Assert.Contains("Keine Termine geplant", text, StringComparison.Ordinal);
        Assert.Contains("Noch keine Besuche", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Es_stehen_die_naechsten_Termine_und_nur_die_letzten_fuenf_Besuche_darin()
    {
        var spa = await SpaWithGuestAsync(fixture);
        for (var day = 0; day < 6; day++)
        {
            var visit = await spa.BookOkAsync(TestSpa.At(10, date: TestSpa.Monday.AddDays(day)), Ct, spa.Anna.Id);
            await spa.SetStatusAsync(visit.Id, AppointmentStatus.Completed, Ct);
        }

        await spa.BookOkAsync(TestSpa.At(14, date: TestSpa.Monday.AddDays(7)), Ct, spa.Ben.Id);

        var (_, pdf) = await PdfAsync(spa.Client, spa.Customer.Id);

        PdfText.Keep(pdf, "kundenkarte-termine");
        var text = PdfText.Text(pdf);
        Assert.Contains("Nächste Termine", text, StringComparison.Ordinal);
        Assert.Contains("08.03.2027 14:00", text, StringComparison.Ordinal);
        Assert.Contains("Ben", text, StringComparison.Ordinal);
        Assert.Contains("Letzte Besuche", text, StringComparison.Ordinal);
        Assert.DoesNotContain("01.03.2027 10:00", text, StringComparison.Ordinal);
        foreach (var date in new[] { "02.03.2027", "03.03.2027", "04.03.2027", "05.03.2027", "06.03.2027" })
        {
            Assert.Contains($"{date} 10:00", text, StringComparison.Ordinal);
        }

        Assert.Contains("Erledigt", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Termine geplant", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Termin_im_Singular_und_ein_stornierter_Termin_zaehlt_nicht_als_naechster()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var cancelled = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Anna.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest(null), Ct);

        var (_, pdf) = await PdfAsync(spa.Client, spa.Customer.Id);

        var text = PdfText.Text(pdf);
        Assert.Contains("01.03.2027 10:00", text, StringComparison.Ordinal);
        Assert.DoesNotContain("01.03.2027 14:00", text, StringComparison.Ordinal);
        Assert.Contains("1 Termin geplant", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gesundheitsdaten_stehen_nie_im_Stammblatt_auch_nicht_fuer_die_Inhaberin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await SubmitAnamnesisAsync(spa, spa.Customer.Id);

        var (_, pdf) = await PdfAsync(spa.Client, spa.Customer.Id);

        var text = PdfText.Text(pdf);
        Assert.DoesNotContain("Nüsse", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Anamnese", text, StringComparison.Ordinal);
        Assert.Contains("Gesundheitsangaben: vorhanden, Auskunft durch die Leitung", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Gesundheitsangaben_fehlt_auch_der_feste_Satz()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var (_, pdf) = await PdfAsync(spa.Client, spa.Customer.Id);

        Assert.DoesNotContain("Gesundheitsangaben", PdfText.Text(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Rezeption_darf_das_Stammblatt_drucken_das_Backoffice_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id], spa.Anna.Id);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (allowed, _) = await PdfAsync(reception, spa.Customer.Id);
        var (forbidden, _) = await PdfAsync(backoffice, spa.Customer.Id);
        var (foreign, _) = await PdfAsync(other.Client, spa.Customer.Id);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
