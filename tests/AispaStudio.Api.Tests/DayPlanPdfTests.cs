using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Tagesplan als PDF (PDF-Ausgaben E, ADR 0091): A4 quer, eine Spalte je Person oder je Raum, die Termine mit Zeit, Gast, Behandlung und Status in Zeitzeilen, Pausen und
/// Sperrzeiten grau, Fußnote mit der Zahl auf der Warteliste. Recht wie der Kalender (<c>ViewCalendar</c>), fremder Mandant 404. Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class DayPlanPdfTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(Guid locationId, string query = "") => $"/api/locations/{locationId}/day-plan/pdf?date=2027-03-01{query}";

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid locationId, string query = "")
    {
        var response = await client.GetAsync(Url(locationId, query), Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    /// <summary>Anna hat um 10:00 Eva Muster zur Massage und um 12:00 Pause, Ben um 10:00 Paul Gast zur Massage (eingecheckt) und um 14:00 einen stornierten Termin.</summary>
    private static async Task<(TestSpa Spa, CustomerDto Paul)> BusyDayAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var paul = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Paul", $"Gast{Guid.NewGuid():N}"[..12], null, null, null, null), Ct);
        await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id, spa.RoomA.Id);
        var checkedIn = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Ben.Id, spa.RoomB.Id, paul.Id);
        await spa.SetStatusAsync(checkedIn.Id, AppointmentStatus.CheckedIn, Ct);
        var cancelled = await spa.BookOkAsync(TestSpa.At(14), Ct, spa.Ben.Id, spa.RoomB.Id);
        await spa.PostAsync<AppointmentDto>($"/api/appointments/{cancelled.Id}/cancel", new CancelAppointmentRequest(null), Ct);
        await spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Employee, spa.Anna.Id, TestSpa.At(12), TestSpa.At(12, 30), "Pause"), Ct);
        return (spa, paul);
    }

    [Fact]
    public async Task Das_Team_Querformat_nennt_je_Person_eine_Spalte_mit_Zeit_Gast_Behandlung_und_Status()
    {
        var (spa, paul) = await BusyDayAsync(fixture);

        var (response, pdf) = await PdfAsync(spa.Client, spa.Location.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Tagesplan-Wien-2027-03-01.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        PdfText.Keep(pdf, "tagesplan-team");
        using var document = PdfText.Open(pdf);
        Assert.True(document.Pages[0].Width.Point > document.Pages[0].Height.Point, "A4 quer");
        var text = PdfText.Text(pdf);
        Assert.Contains("Tagesplan Montag, 01.03.2027", text, StringComparison.Ordinal);
        Assert.Contains("Nach Team", text, StringComparison.Ordinal);
        Assert.Contains("Anna", text, StringComparison.Ordinal);
        Assert.Contains("Ben", text, StringComparison.Ordinal);
        Assert.Contains("10:00–10:50", text, StringComparison.Ordinal);
        Assert.Contains(spa.Customer.LastName, text, StringComparison.Ordinal);
        Assert.Contains(paul.LastName, text, StringComparison.Ordinal);
        Assert.Contains("Massage", text, StringComparison.Ordinal);
        Assert.Contains("Gebucht", text, StringComparison.Ordinal);
        Assert.Contains("Eingecheckt", text, StringComparison.Ordinal);
        Assert.Contains("12:00–12:30", text, StringComparison.Ordinal);
        Assert.Contains("Pause", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Raum A", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_stornierter_Termin_steht_nicht_im_Plan()
    {
        var (spa, _) = await BusyDayAsync(fixture);

        var (_, pdf) = await PdfAsync(spa.Client, spa.Location.Id);

        var text = PdfText.Text(pdf);
        Assert.DoesNotContain("14:00", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Storniert", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pausen_sind_grau_hinterlegt_und_Termine_nicht()
    {
        var (spa, _) = await BusyDayAsync(fixture);
        var empty = await TestSpa.CreateAsync(fixture, Ct);
        await empty.BookOkAsync(TestSpa.At(10), Ct, empty.Anna.Id, empty.RoomA.Id);

        var (_, busy) = await PdfAsync(spa.Client, spa.Location.Id);
        var (_, plain) = await PdfAsync(empty.Client, empty.Location.Id);

        Assert.True(PdfText.CountOperators(busy, "re") > PdfText.CountOperators(plain, "re"), "Die Pause ist als graues Rechteck hinterlegt.");
    }

    [Fact]
    public async Task Die_Raumansicht_nennt_je_Raum_eine_Spalte()
    {
        var (spa, _) = await BusyDayAsync(fixture);
        await spa.PostAsync<BlockDto>("/api/blocks", new BlockInput(ResourceKind.Room, spa.RoomB.Id, TestSpa.At(16), TestSpa.At(17), "Wartung Dampfbad"), Ct);

        var (response, pdf) = await PdfAsync(spa.Client, spa.Location.Id, "&by=rooms");

        response.EnsureSuccessStatusCode();
        PdfText.Keep(pdf, "tagesplan-raeume");
        var text = PdfText.Text(pdf);
        Assert.Contains("Nach Räumen", text, StringComparison.Ordinal);
        Assert.Contains("Raum A", text, StringComparison.Ordinal);
        Assert.Contains("Raum B", text, StringComparison.Ordinal);
        Assert.Contains("Wartung Dampfbad", text, StringComparison.Ordinal);
        Assert.Contains("16:00–17:00", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Die_Fussnote_nennt_die_Zahl_der_Wartenden_im_richtigen_Plural()
    {
        var (spa, _) = await BusyDayAsync(fixture);
        var (_, none) = await PdfAsync(spa.Client, spa.Location.Id);
        await JoinWaitlistAsync(spa, "Wanda");
        var (_, one) = await PdfAsync(spa.Client, spa.Location.Id);
        await JoinWaitlistAsync(spa, "Wolf");
        var (_, two) = await PdfAsync(spa.Client, spa.Location.Id);

        Assert.Contains("Niemand auf der Warteliste", PdfText.Text(none), StringComparison.Ordinal);
        Assert.Contains("1 Person auf der Warteliste", PdfText.Text(one), StringComparison.Ordinal);
        Assert.Contains("2 Personen auf der Warteliste", PdfText.Text(two), StringComparison.Ordinal);
    }

    internal static async Task JoinWaitlistAsync(TestSpa spa, string firstName, string phone = "+43 664 111")
    {
        using var online = spa.OnlineClient();
        var joined = await online.PostAsJsonAsync("/api/online/waitlist", new WaitlistRequest(spa.Location.Id, spa.MassageTreatment.Id, TestSpa.Monday.AddDays(1),
            TestSpa.Monday.AddDays(5), DayPart.Afternoon, new OnlineContact(firstName, "Warte", $"{firstName}-{Guid.NewGuid():N}@example.at", phone), true, "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
    }

    [Fact]
    public async Task Ein_Tag_ohne_Termine_ist_ein_Plan_mit_dem_Hinweis_Keine_Termine()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var (response, pdf) = await PdfAsync(spa.Client, spa.Location.Id);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Keine Termine an diesem Tag", PdfText.Text(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_unbekannte_Ansicht_ist_400()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var (response, _) = await PdfAsync(spa.Client, spa.Location.Id, "&by=sauna");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Der_Therapeut_darf_den_Plan_drucken_das_Backoffice_nicht_und_ein_fremdes_Spa_bekommt_404()
    {
        var (spa, _) = await BusyDayAsync(fixture);
        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Anna.Id);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);
        var other = await TestSpa.CreateAsync(fixture, Ct);

        var (allowed, _) = await PdfAsync(therapist, spa.Location.Id);
        var (forbidden, _) = await PdfAsync(backoffice, spa.Location.Id);
        var (foreign, _) = await PdfAsync(other.Client, spa.Location.Id);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
