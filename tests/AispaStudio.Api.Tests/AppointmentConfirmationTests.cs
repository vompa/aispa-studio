using System.Net;
using System.Net.Http.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Terminbestätigung als PDF und per Mail (PDF-Ausgaben C, ADR 0091): Gast, Tag, Uhrzeit, Dauer, Person nur mit Vorname, Standort mit Adresse, Preis, Stornofrist und
/// „Was mitbringen“; bei einer Gruppenbuchung alle Teile; der Versand legt die Mail mit dem PDF in die Queue und steht danach im Verlauf des Termins; fremder Mandant 404.
/// </summary>
public sealed class AppointmentConfirmationTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly LocationContact Address = new("Kärntner Straße 1", "1010", "Wien", null, null);

    private static async Task<TestSpa> SpaAsync(ApiFixture fixture)
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var t = spa.MassageTreatment;
        await spa.PutAsync<TreatmentDto>($"/api/treatments/{t.Id}", new TreatmentInput(t.Name, t.DurationMinutes, t.PreparationMinutes,
            t.FollowUpMinutes, t.Price, t.RequiredQualificationIds, t.RoomIds, t.EquipmentIds, GuestHint: "Bitte Badekleidung mitbringen."), Ct);
        var l = spa.Location;
        await spa.PutAsync<LocationDto>($"/api/locations/{l.Id}", new LocationInput(l.Name, l.TimeZone, l.Currency, l.OpeningHours, l.Region, Address), Ct);
        await spa.PutAsync<LocationOnlineRules>($"/api/locations/{l.Id}/online-rules", new LocationOnlineRules(true, 60, 365, 24, 15, null, false, 0, 0, false, 0, "Bei Verspätung kürzen wir die Behandlung."), Ct);
        return spa;
    }

    private static async Task<(HttpResponseMessage Response, byte[] Pdf)> PdfAsync(HttpClient client, Guid appointmentId)
    {
        var response = await client.GetAsync($"/api/appointments/{appointmentId}/confirmation/pdf", Ct);
        return (response, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(Ct) : []);
    }

    [Fact]
    public async Task Die_Bestaetigung_nennt_Gast_Tag_Zeit_Dauer_Person_Standort_Preis_Stornofrist_und_Mitbringen()
    {
        var spa = await SpaAsync(fixture);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var (response, pdf) = await PdfAsync(spa.Client, appointment.Id);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Terminbestätigung-01-03-2027.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        PdfText.Keep(pdf, "terminbestaetigung");
        var text = PdfText.Text(pdf);
        Assert.Contains("Terminbestätigung", text, StringComparison.Ordinal);
        Assert.Contains(spa.Customer.LastName, text, StringComparison.Ordinal);
        Assert.Contains("Montag, 01.03.2027", text, StringComparison.Ordinal);
        Assert.Contains("10:00", text, StringComparison.Ordinal);
        Assert.Contains("50 Min.", text, StringComparison.Ordinal);
        Assert.Contains("Massage", text, StringComparison.Ordinal);
        Assert.Contains("Anna", text, StringComparison.Ordinal);
        Assert.Contains("Kärntner Straße 1", text, StringComparison.Ordinal);
        Assert.Contains("1010 Wien", text, StringComparison.Ordinal);
        Assert.Contains("79,00", text, StringComparison.Ordinal);
        Assert.Contains("28.02.2027 10:00", text, StringComparison.Ordinal);
        Assert.Contains("24 Stunden vor dem Termin", text, StringComparison.Ordinal);
        Assert.Contains("Verspätung kürzen wir die Behandlung.", text, StringComparison.Ordinal);
        Assert.Contains("Was mitbringen", text, StringComparison.Ordinal);
        Assert.Contains("Bitte Badekleidung mitbringen.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Hinweis_der_Behandlung_fehlt_der_Absatz_Was_mitbringen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var (_, pdf) = await PdfAsync(spa.Client, appointment.Id);

        Assert.DoesNotContain("Was mitbringen", PdfText.Text(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Von_der_Person_steht_nur_der_Vorname_in_der_Bestaetigung()
    {
        var spa = await SpaAsync(fixture);
        var staff = await spa.CreateEmployeeAsync("Lena Huber", [spa.Massage.Id], spa.Location.Id, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, staff.Id);

        var (_, pdf) = await PdfAsync(spa.Client, appointment.Id);

        var text = PdfText.Text(pdf);
        Assert.Contains("Lena", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Huber", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eine_Gruppenbuchung_zeigt_beide_Teile_mit_Gast_und_Summe()
    {
        var spa = await SpaAsync(fixture);
        var suite = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Paarsuite", 10, 2), Ct);
        var t = spa.MassageTreatment;
        await spa.PutAsync<TreatmentDto>($"/api/treatments/{t.Id}", new TreatmentInput(t.Name, t.DurationMinutes, t.PreparationMinutes,
            t.FollowUpMinutes, t.Price, t.RequiredQualificationIds, [.. t.RoomIds, suite.Id], t.EquipmentIds, GuestHint: "Bitte Badekleidung mitbringen."), Ct);
        var partner = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Paul", $"Gast{Guid.NewGuid():N}", null, null, null, null), Ct);
        var response = await spa.Client.PostAsJsonAsync("/api/appointment-groups", new GroupBookingRequest(spa.Location.Id, TestSpa.At(10),
            [new GroupBookingItem(t.Id, spa.Customer.Id, 0), new GroupBookingItem(t.Id, partner.Id, 0)], 0), TestSpa.Json, Ct);
        var group = await TestSpa.ReadAsync<GroupBookingResult>(response, Ct);

        var (_, pdf) = await PdfAsync(spa.Client, group.Appointments[0].Id);

        PdfText.Keep(pdf, "terminbestaetigung-gruppe");
        var text = PdfText.Text(pdf);
        Assert.Contains("Ihre Termine", text, StringComparison.Ordinal);
        Assert.Contains("Eva Muster", text, StringComparison.Ordinal);
        Assert.Contains("Paul", text, StringComparison.Ordinal);
        Assert.Contains("Summe", text, StringComparison.Ordinal);
        Assert.Contains("158,00", text, StringComparison.Ordinal);
        Assert.Single(text.Split("Bitte Badekleidung mitbringen.").Skip(1));
    }

    [Fact]
    public async Task Die_Vorbelegung_nennt_die_Adresse_des_Gastes_Betreff_und_Text()
    {
        var spa = await SpaAsync(fixture);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var draft = await spa.GetAsync<DocumentMailDraft>($"/api/appointments/{appointment.Id}/confirmation/send", Ct);

        Assert.Equal("eva@example.at", draft.To);
        Assert.Equal("Ihre Terminbestätigung: Massage am 01.03.2027", draft.Subject);
        Assert.Contains("Ihres Termins am 01.03.2027 um 10:00 Uhr", draft.Text, StringComparison.Ordinal);
        Assert.Contains("Freundliche Grüße", draft.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Senden_legt_die_Mail_mit_dem_PDF_in_die_Queue_und_steht_danach_im_Verlauf_des_Termins()
    {
        var spa = await SpaAsync(fixture);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        var to = $"eva-{Guid.NewGuid():N}@example.at";
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id], spa.Anna.Id);

        var response = await reception.PostAsJsonAsync($"/api/appointments/{appointment.Id}/confirmation/send", new SendDocumentRequest(to), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(to, m => m.Attachments is { Count: > 0 }, Ct);
        var attachment = Assert.Single(mail.Attachments!);
        Assert.Equal("Terminbestätigung-01-03-2027.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Contains("Terminbestätigung", PdfText.Text(attachment.Content), StringComparison.Ordinal);
        Assert.Equal("Ihre Terminbestätigung: Massage am 01.03.2027", mail.Subject);

        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/appointments/{appointment.Id}/history", Ct);
        var entry = Assert.Single(history, h => h.Text.StartsWith("Bestätigung per Mail", StringComparison.Ordinal));
        Assert.Equal($"Bestätigung per Mail an {to} gesendet von Anna", entry.Text);
        Assert.InRange(entry.At, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(entry, history[0]);
    }

    [Fact]
    public async Task Eine_ungueltige_Adresse_ist_400_und_vermerkt_nichts()
    {
        var spa = await SpaAsync(fixture);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var response = await spa.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/confirmation/send", new SendDocumentRequest("eva@"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var history = await spa.GetAsync<List<HistoryEntry>>($"/api/appointments/{appointment.Id}/history", Ct);
        Assert.DoesNotContain(history, h => h.Text.StartsWith("Bestätigung per Mail", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ein_Termin_eines_fremden_Spas_ist_404_fuer_PDF_Vorbelegung_und_Versand()
    {
        var spa = await SpaAsync(fixture);
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);

        var pdf = await other.Client.GetAsync($"/api/appointments/{appointment.Id}/confirmation/pdf", Ct);
        var draft = await other.Client.GetAsync($"/api/appointments/{appointment.Id}/confirmation/send", Ct);
        var send = await other.Client.PostAsJsonAsync($"/api/appointments/{appointment.Id}/confirmation/send", new SendDocumentRequest("eva@example.at"), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, pdf.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
    }

    [Fact]
    public async Task Ohne_Recht_den_Kalender_zu_sehen_ist_die_Bestaetigung_403()
    {
        var spa = await SpaAsync(fixture);
        var appointment = await spa.BookOkAsync(TestSpa.At(10), Ct, spa.Anna.Id);
        using var backoffice = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Backoffice, [spa.Location.Id]);

        var response = await backoffice.GetAsync($"/api/appointments/{appointment.Id}/confirmation/pdf", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
