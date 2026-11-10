using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

public sealed partial class OnlineBookingTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"kundin-{Guid.NewGuid():N}@example.at";

    private static OnlineBookingRequest Request(TestSpa spa, DateTimeOffset start, string? email = null, bool privacy = true) =>
        new(spa.Location.Id, spa.MassageTreatment.Id, start, new OnlineContact("Lena", "Gast", email ?? NewEmail(), "+43 1 234"),
            "Bitte ruhige Musik", privacy, "de");

    private static async Task<OnlineBookingConfirmation> BookAsGuestAsync(TestSpa spa, DateTimeOffset start, string email)
    {
        using var client = spa.OnlineClient();
        var response = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, start, email), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TestSpa.ReadAsync<OnlineBookingConfirmation>(response, Ct);
    }

    /// <summary>Meldet per Code an (wie im Kundenbereich) und liefert das Kunden-Cookie.</summary>
    private async Task<string> SignInWithCodeAsync(string email, TestSpa? spa = null)
    {
        using var client = spa?.OnlineClient() ?? fixture.CreateClient();
        var request = await client.PostAsJsonAsync("/api/online/auth/code", new SignInCodeRequest(email, "de"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);

        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Ihr Anmeldecode", StringComparison.Ordinal), Ct);
        var code = SixDigits().Match(mail.Subject).Value;
        var verify = await client.PostAsJsonAsync("/api/online/auth/code/verify", new SignInCodeVerification(email, code), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return string.Join("; ", verify.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).Where(c => c.StartsWith("aispa.kunde=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Angebot_zeigt_Standort_und_nur_Behandlungen_mit_qualifiziertem_Team()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var other = await spa.PostAsync<QualificationDto>("/api/qualifications", new QualificationInput($"Ayurveda {Guid.NewGuid():N}"), Ct);
        await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput("Ayurveda-Ölguss", 60, 0, 0, 120m, [other.Id], [spa.RoomA.Id], []), Ct);
        using var client = spa.OnlineClient();

        var locations = await client.GetFromJsonAsync<List<OnlineLocation>>("/api/online/locations", TestSpa.Json, Ct);
        var treatments = await client.GetFromJsonAsync<List<OnlineTreatment>>($"/api/online/locations/{spa.Location.Id}/treatments", TestSpa.Json, Ct);

        Assert.Contains(locations!, l => l.Id == spa.Location.Id && l.TimeZone == "Europe/Vienna");
        var massage = Assert.Single(treatments!);
        Assert.Equal(spa.MassageTreatment.Id, massage.Id);
        Assert.Equal(79m, massage.Price);
        Assert.Equal("EUR", massage.Currency);
    }

    [Fact]
    public async Task Freie_Zeiten_ohne_Anmeldung_und_ohne_interne_Ressourcen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        // Beide Personen um 10 Uhr belegt: 10:00 darf online nicht mehr erscheinen.
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Anna.Id);
        await spa.BookOkAsync(TestSpa.At(10), Ct, employee: spa.Ben.Id);
        using var client = spa.OnlineClient();

        var json = await client.GetStringAsync(
            $"/api/online/locations/{spa.Location.Id}/availability?treatmentId={spa.MassageTreatment.Id}&from={TestSpa.Monday:yyyy-MM-dd}&days=2", Ct);
        var availability = JsonSerializer.Deserialize<OnlineAvailability>(json, TestSpa.Json)!;

        var monday = availability.Days[0];
        Assert.Equal(TestSpa.Monday, monday.Date);
        Assert.Contains(TestSpa.At(11, 15), monday.Times);
        Assert.DoesNotContain(TestSpa.At(10), monday.Times);
        // Arbeitszeit bis 17 Uhr, 10 Minuten Nachbereitung: letzter Beginn 16:00.
        Assert.Equal(TestSpa.At(16), monday.Times[^1]);
        Assert.Empty(availability.Days[1].Times);
        Assert.DoesNotContain(spa.Anna.Id.ToString(), json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bestaetigung_kommt_aus_dem_Postfach_des_Spas_oder_mit_dessen_Antwortadresse()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var address = $"termine-{Guid.NewGuid():N}@spa.example";
        var mailbox = new SpaMailboxInput(MailboxKind.Imap, address, true, "imap.spa.example", 993, MailboxSecurity.SslOnConnect,
            address, null, null, null, null, "geheim-123");
        await spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", mailbox, Ct);

        // Ohne eigenen Versand: gemeinsamer Versand im Namen des Spas, Antworten gehen an sein Postfach.
        var first = NewEmail();
        await BookAsGuestAsync(spa, TestSpa.At(11), first);
        var shared = await fixture.Mailbox.WaitForAsync(first, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        Assert.Equal(address, shared.ReplyTo);
        Assert.False(string.IsNullOrWhiteSpace(shared.FromName));

        await spa.PutAsync<SpaMailboxDto>("/api/intake/mailbox", mailbox with { SendReplies = true, SmtpHost = "smtp.spa.example" }, Ct);
        var second = NewEmail();
        await BookAsGuestAsync(spa, TestSpa.At(13), second);
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!fixture.SpaMailboxes.Sent.Any(s => s.Mail.To == second) && DateTime.UtcNow < until)
        {
            await Task.Delay(50, Ct);
        }

        var (settings, own) = Assert.Single(fixture.SpaMailboxes.Sent, s => s.Mail.To == second);
        Assert.StartsWith("Termin bestätigt", own.Subject, StringComparison.Ordinal);
        Assert.Equal("smtp.spa.example", settings.SmtpHost);
        Assert.Empty(fixture.Mailbox.To(second));
    }

    [Fact]
    public async Task Gast_bucht_bekommt_Bestaetigung_mit_Links_und_die_Rezeption_sieht_den_Termin()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();

        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(11), email);

        Assert.Equal(OnlineAppointmentState.Booked, confirmation.Appointment.State);
        Assert.Equal("Massage", confirmation.Appointment.TreatmentName);
        Assert.True(confirmation.Appointment.CanCancel);
        Assert.False(confirmation.Appointment.AnamnesisSubmitted);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        Assert.Contains($"/buchen/#/termin/{confirmation.ManageToken}", mail.Text, StringComparison.Ordinal);
        Assert.Contains($"/buchen/#/anamnese/{confirmation.AnamnesisToken}", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Montag, 1. März 2027, 11:00", mail.Text, StringComparison.Ordinal);

        var staffView = await spa.GetAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}", Ct);
        Assert.Equal("Lena Gast", staffView.CustomerName);
        Assert.Equal("Bitte ruhige Musik", staffView.Note);
        Assert.Equal(AppointmentSource.Online, staffView.Source);
        var customer = await spa.GetAsync<CustomerDto>($"/api/customers/{staffView.CustomerId}", Ct);
        Assert.Equal(email, customer.Email);
    }

    [Fact]
    public async Task Die_Bestaetigung_nennt_die_Anreise_mit_Routenlink_nur_wenn_ein_Text_hinterlegt_ist()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var without = NewEmail();
        var with = NewEmail();

        await BookAsGuestAsync(spa, TestSpa.At(10), without);
        var location = spa.Location;
        await spa.PutAsync<LocationDto>($"/api/locations/{location.Id}",
            new LocationInput(location.Name, location.TimeZone, location.Currency, location.OpeningHours, location.Region,
                new LocationContact("Graben 1", "1010", "Wien", null, null, "U1 bis Stephansplatz, Garage Am Graben 2 Minuten zu Fuß.")), Ct);
        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(12), with);

        var plain = await fixture.Mailbox.WaitForAsync(without, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        var mail = await fixture.Mailbox.WaitForAsync(with, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        Assert.DoesNotContain("Anreise", plain.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Route planen", plain.Text, StringComparison.Ordinal);
        Assert.Contains("Anreise: U1 bis Stephansplatz, Garage Am Graben 2 Minuten zu Fuß.", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Route planen: https://www.google.com/maps/dir/?api=1&destination=Graben%201%2C%201010%20Wien", mail.Text, StringComparison.Ordinal);
        // Der Termin liefert dieselben Angaben für „Route planen“ im Kundenbereich.
        Assert.Equal("U1 bis Stephansplatz, Garage Am Graben 2 Minuten zu Fuß.", confirmation.Appointment.LocationContact.Directions);
    }

    [Fact]
    public void Die_Anreisezeile_der_Bestaetigung_gibt_es_in_allen_Sprachen_und_ohne_Anschrift_ohne_Link()
    {
        var contact = new LocationContact(null, null, null, null, null, "Bus 3 bis Hanuschplatz.");
        var appointment = new OnlineAppointment(Guid.NewGuid(), Guid.NewGuid(), "Salzburg", "Europe/Vienna", "Massage", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 79m, "EUR",
            OnlineAppointmentState.Booked, DateTimeOffset.UtcNow, true, false, contact);

        Assert.Equal("\n\nAnreise: Bus 3 bis Hanuschplatz.", OnlineMails.Route(appointment, "de"));
        Assert.Equal("\n\nGetting here: Bus 3 bis Hanuschplatz.", OnlineMails.Route(appointment, "en"));
        Assert.Equal("\n\nAccès: Bus 3 bis Hanuschplatz.", OnlineMails.Route(appointment, "fr"));
        Assert.Equal("\n\nCome arrivare: Bus 3 bis Hanuschplatz.", OnlineMails.Route(appointment, "it"));
        Assert.Equal("", OnlineMails.Route(appointment with { LocationContact = contact with { Directions = "  " } }, "de"));
    }

    [Fact]
    public async Task Gast_mit_bekannter_Adresse_landet_in_der_bestehenden_Kundenkartei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        var known = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Eva", "Stamm", email, null, null, null), Ct);

        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(11), email.ToUpperInvariant());

        var staffView = await spa.GetAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}", Ct);
        Assert.Equal(known.Id, staffView.CustomerId);
    }

    [Fact]
    public async Task Buchung_braucht_Datenschutzhinweis_und_gueltige_Zeit()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var client = spa.OnlineClient();

        var noPrivacy = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(11), privacy: false), TestSpa.Json, Ct);
        var past = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, DateTimeOffset.UtcNow.AddMinutes(30)), TestSpa.Json, Ct);
        var closed = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(7)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, noPrivacy.StatusCode);
        Assert.Contains("acceptPrivacy", await noPrivacy.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        var rejected = await closed.Content.ReadFromJsonAsync<BookingRejected>(TestSpa.Json, Ct);
        Assert.Empty(rejected!.Conflicts);
    }

    [Fact]
    public async Task Abgelehnte_Gastbuchung_legt_keinen_Kunden_an()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var response = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(7), email), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var found = await spa.GetAsync<List<CustomerDto>>($"/api/customers?search={Uri.EscapeDataString(email)}", Ct);
        Assert.Empty(found);
    }

    [Fact]
    public async Task Termin_per_Link_ansehen_und_stornieren_gibt_die_Zeit_frei()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(11), email);
        using var client = spa.OnlineClient();

        var view = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{confirmation.ManageToken}", TestSpa.Json, Ct);
        var cancel = await client.PostAsync($"/api/online/bookings/{confirmation.ManageToken}/cancel", null, Ct);
        var wrong = await client.GetAsync($"/api/online/bookings/{confirmation.AnamnesisToken}", Ct);

        Assert.Equal(confirmation.Appointment.Id, view!.Id);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(OnlineAppointmentState.Cancelled, (await TestSpa.ReadAsync<OnlineAppointment>(cancel, Ct)).State);
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Equal(AppointmentStatus.Cancelled, (await spa.GetAsync<AppointmentDto>($"/api/appointments/{view.Id}", Ct)).Status);
        await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin storniert", StringComparison.Ordinal), Ct);
        await BookAsGuestAsync(spa, TestSpa.At(11), NewEmail());
    }

    [Fact]
    public async Task Hoechstens_drei_offene_Online_Termine_und_nach_zwei_No_Shows_nur_telefonisch()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        using var client = spa.OnlineClient();
        var booked = new List<Guid>();
        foreach (var hour in new[] { 10, 12, 13 })
        {
            booked.Add((await BookAsGuestAsync(spa, TestSpa.At(hour), email)).Appointment.Id);
        }

        var fourth = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(14), email), TestSpa.Json, Ct);
        var other = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(14), NewEmail()), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, fourth.StatusCode);
        Assert.Contains("3 offene Termine", await fourth.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);

        // Zwei davon nicht erschienen: ohne Gebühr oder Anzahlung am Standort geht es nur noch telefonisch.
        foreach (var missed in booked.Take(2))
        {
            await spa.SetStatusAsync(missed, AppointmentStatus.NoShow, Ct);
        }

        var phoneOnly = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(15), email), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, phoneOnly.StatusCode);
        Assert.Contains("Bitte rufen Sie uns an", await phoneOnly.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gast_sagt_per_Link_zu_und_Verschieben_hebt_die_Zusage_auf()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var t = spa.MassageTreatment;
        await spa.PutAsync<TreatmentDto>($"/api/treatments/{t.Id}", new TreatmentInput(t.Name, t.DurationMinutes, t.PreparationMinutes,
            t.FollowUpMinutes, t.Price, t.RequiredQualificationIds, t.RoomIds, t.EquipmentIds, GuestHint: "  Bitte 15 Minuten früher kommen.  "), Ct);
        var email = NewEmail();
        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(11), email);
        using var client = spa.OnlineClient();

        var first = await client.PostAsync($"/api/online/bookings/{confirmation.ManageToken}/confirm", null, Ct);
        var again = await client.PostAsync($"/api/online/bookings/{confirmation.ManageToken}/confirm", null, Ct);
        var wrong = await client.PostAsync($"/api/online/bookings/{confirmation.AnamnesisToken}/confirm", null, Ct);

        Assert.Equal("Bitte 15 Minuten früher kommen.", confirmation.Appointment.GuestHint);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin bestätigt", StringComparison.Ordinal), Ct);
        Assert.Contains("Bitte 15 Minuten früher kommen.", mail.Text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var confirmedAt = (await TestSpa.ReadAsync<OnlineAppointment>(first, Ct)).ConfirmedAt;
        Assert.NotNull(confirmedAt);
        Assert.Equal(confirmedAt, (await TestSpa.ReadAsync<OnlineAppointment>(again, Ct)).ConfirmedAt);
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.NotNull((await spa.GetAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}", Ct)).ConfirmedAt);

        var moved = await spa.PostAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}/move",
            new MoveAppointmentRequest(TestSpa.At(14), null, null, null), Ct);
        Assert.Null(moved.ConfirmedAt);

        var cancel = await client.PostAsync($"/api/online/bookings/{confirmation.ManageToken}/cancel", null, Ct);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var late = await client.PostAsync($"/api/online/bookings/{confirmation.ManageToken}/confirm", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    [Fact]
    public async Task Gast_verschiebt_selbst_hoechstens_zweimal_und_nur_auf_freie_Zeiten()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(11), email);
        // Anna und Ben sind um 13 Uhr belegt, also ist diese Zeit online nicht frei.
        await spa.BookOkAsync(TestSpa.At(13), Ct, spa.Anna.Id, spa.RoomA.Id);
        await spa.BookOkAsync(TestSpa.At(13), Ct, spa.Ben.Id, spa.RoomB.Id);
        using var client = spa.OnlineClient();
        var path = $"/api/online/bookings/{confirmation.ManageToken}/move";

        Assert.True(confirmation.Appointment.CanMove);
        var taken = await client.PostAsJsonAsync(path, new OnlineMoveRequest(TestSpa.At(13)), TestSpa.Json, Ct);
        var offGrid = await client.PostAsJsonAsync(path, new OnlineMoveRequest(TestSpa.At(15).AddMinutes(7)), TestSpa.Json, Ct);
        var first = await client.PostAsJsonAsync(path, new OnlineMoveRequest(TestSpa.At(15)), TestSpa.Json, Ct);
        var second = await client.PostAsJsonAsync(path, new OnlineMoveRequest(TestSpa.At(16)), TestSpa.Json, Ct);
        var third = await client.PostAsJsonAsync(path, new OnlineMoveRequest(TestSpa.At(14, 30)), TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, offGrid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(TestSpa.At(15), (await TestSpa.ReadAsync<OnlineAppointment>(first, Ct)).Start);
        var afterSecond = await TestSpa.ReadAsync<OnlineAppointment>(second, Ct);
        Assert.Equal(TestSpa.At(16), afterSecond.Start);
        Assert.False(afterSecond.CanMove);
        Assert.True(afterSecond.CanCancel);
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        Assert.Equal(TestSpa.At(16), (await spa.GetAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}", Ct)).Start);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Termin verschoben", StringComparison.Ordinal)
            && m.Text.Contains("16:00", StringComparison.Ordinal), Ct);
        Assert.Contains("/buchen/#/termin/", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nach_der_Stornofrist_geht_online_kein_Storno_mehr()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(11), NewEmail());
        // Zweite API-Instanz auf derselben Datenbank mit einer Frist, die den Termin im März 2027 schon einschließt.
        await using var strict = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("OnlineBooking:CancellationDeadlineHours", "100000"));
        using var client = strict.CreateClient();

        var view = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{confirmation.ManageToken}", TestSpa.Json, Ct);
        var cancel = await client.PostAsync($"/api/online/bookings/{confirmation.ManageToken}/cancel", null, Ct);

        Assert.False(view!.CanCancel);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal(AppointmentStatus.Booked, (await spa.GetAsync<AppointmentDto>($"/api/appointments/{view.Id}", Ct)).Status);
    }

    [Fact]
    public async Task Anmeldung_per_Code_eigene_Termine_und_Storno()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        var cookie = await SignInWithCodeAsync(email, spa);
        using var client = spa.OnlineClient(cookie);

        var before = await client.GetFromJsonAsync<CustomerSession>("/api/online/me", TestSpa.Json, Ct);
        // Neues Konto ohne Kundendaten: Name ist bei der ersten Buchung Pflicht, die E-Mail kommt aus dem Konto.
        var withoutName = await client.PostAsJsonAsync("/api/online/bookings",
            new OnlineBookingRequest(spa.Location.Id, spa.MassageTreatment.Id, TestSpa.At(11), null, null, true, "de"), TestSpa.Json, Ct);
        var booked = await client.PostAsJsonAsync("/api/online/bookings",
            new OnlineBookingRequest(spa.Location.Id, spa.MassageTreatment.Id, TestSpa.At(11),
                new OnlineContact("Mia", "Konto", "egal@example.at", null), null, true, "de"), TestSpa.Json, Ct);
        var after = await client.GetFromJsonAsync<CustomerSession>("/api/online/me", TestSpa.Json, Ct);
        var mine = await client.GetFromJsonAsync<List<OnlineAppointment>>("/api/online/me/appointments", TestSpa.Json, Ct);
        var cancel = await client.PostAsync($"/api/online/me/appointments/{mine![0].Id}/cancel", null, Ct);

        Assert.False(before!.HasProfile);
        Assert.Equal(email, before.Email);
        Assert.Equal(HttpStatusCode.BadRequest, withoutName.StatusCode);
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.True(after!.HasProfile);
        Assert.Equal("Mia", after.FirstName);
        Assert.Single(mine);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var staffView = await spa.GetAsync<AppointmentDto>($"/api/appointments/{mine[0].Id}", Ct);
        Assert.Equal(email, (await spa.GetAsync<CustomerDto>($"/api/customers/{staffView.CustomerId}", Ct)).Email);
    }

    [Fact]
    public async Task Konto_findet_Termine_die_die_Rezeption_unter_derselben_Adresse_gebucht_hat()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        var customer = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput("Sara", "Stamm", email, null, null, null), Ct);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(14), Ct, customer: customer.Id), Ct);

        using var client = spa.OnlineClient(await SignInWithCodeAsync(email, spa));
        var session = await client.GetFromJsonAsync<CustomerSession>("/api/online/me", TestSpa.Json, Ct);
        var mine = await client.GetFromJsonAsync<List<OnlineAppointment>>("/api/online/me/appointments", TestSpa.Json, Ct);

        Assert.True(session!.HasProfile);
        Assert.Equal("Sara", session.FirstName);
        Assert.Equal(appointment.Id, Assert.Single(mine!).Id);
    }

    [Fact]
    public async Task Falscher_Code_wird_nach_fuenf_Versuchen_gesperrt()
    {
        var email = NewEmail();
        using var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/online/auth/code", new SignInCodeRequest(email, "en"), TestSpa.Json, Ct);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Your sign-in code", StringComparison.Ordinal), Ct);
        var code = SixDigits().Match(mail.Subject).Value;
        var wrong = code == "000000" ? "000001" : "000000";

        for (var i = 0; i < 5; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/online/auth/code/verify", new SignInCodeVerification(email, wrong), TestSpa.Json, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, attempt.StatusCode);
        }

        var right = await client.PostAsJsonAsync("/api/online/auth/code/verify", new SignInCodeVerification(email, code), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, right.StatusCode);
    }

    [Fact]
    public async Task Code_Anforderung_verraet_nicht_ob_es_ein_Konto_gibt_und_ist_gedrosselt()
    {
        var email = NewEmail();
        using var client = fixture.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/online/auth/code", new SignInCodeRequest(email, "de"), TestSpa.Json, Ct)).StatusCode);
        }

        await fixture.Mailbox.WaitForAsync(email, _ => fixture.Mailbox.To(email).Count >= 5, Ct);
        await Task.Delay(300, Ct);
        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.Accepted, s));
        Assert.Equal(5, fixture.Mailbox.To(email).Count);
    }

    [Fact]
    public async Task Kundenkonto_und_Teamkonto_sind_getrennt()
    {
        var customerCookie = await SignInWithCodeAsync(NewEmail());
        var staffCookie = await TestUsers.RegisterAsync(fixture, Ct);
        using var customer = fixture.CreateClient(customerCookie);
        using var staff = fixture.CreateClient(staffCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/customers", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/auth/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/online/me", Ct)).StatusCode);

        // Die zweite Identity darf die Dienste des Teams nicht ersetzen (z. B. die Prüfung des Sicherheitsstempels).
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.IsType<SecurityStampValidator<AispaUser>>(scope.ServiceProvider.GetRequiredService<ISecurityStampValidator>());
    }

    [Fact]
    public async Task Anamnese_per_Link_wird_verschluesselt_gespeichert_und_das_Team_liest_sie()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var confirmation = await BookAsGuestAsync(spa, TestSpa.At(11), NewEmail());
        using var client = spa.OnlineClient();
        var form = await client.GetFromJsonAsync<AnamnesisFormDefinition>("/api/online/anamnesis/form?language=en", TestSpa.Json, Ct);
        var answers = form!.Questions.Select(q => q.Kind == AnamnesisQuestionKind.YesNo
            ? new AnamnesisAnswer(q.Key, q.Key == "allergies", q.Key == "allergies" ? "Nüsse und Lavendelöl" : null)
            : new AnamnesisAnswer(q.Key, null, "Leichte Rückenschmerzen")).ToList();

        var info = await client.GetFromJsonAsync<AnamnesisLinkInfo>($"/api/online/anamnesis/{confirmation.AnamnesisToken}", TestSpa.Json, Ct);
        var incomplete = await client.PostAsJsonAsync($"/api/online/anamnesis/{confirmation.AnamnesisToken}",
            new AnamnesisSubmission(answers.Skip(1).ToList(), true, "Lena Gast", "en"), TestSpa.Json, Ct);
        var noConsent = await client.PostAsJsonAsync($"/api/online/anamnesis/{confirmation.AnamnesisToken}",
            new AnamnesisSubmission(answers, false, "Lena Gast", "en"), TestSpa.Json, Ct);
        var submitted = await client.PostAsJsonAsync($"/api/online/anamnesis/{confirmation.AnamnesisToken}",
            new AnamnesisSubmission(answers, true, "Lena Gast", "en"), TestSpa.Json, Ct);

        Assert.Equal("en", form.Language);
        Assert.Equal("Lena", info!.FirstName);
        Assert.Equal("Massage", info.TreatmentName);
        Assert.Null(info.LastSubmittedAt);
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, submitted.StatusCode);

        var staffView = await spa.GetAsync<AppointmentDto>($"/api/appointments/{confirmation.Appointment.Id}", Ct);
        var record = await spa.GetAsync<AnamnesisRecordDto>($"/api/customers/{staffView.CustomerId}/anamnesis", Ct);
        Assert.Equal("Nüsse und Lavendelöl", record.Answers.Single(a => a.Key == "allergies").Details);
        Assert.Equal("Link", record.Channel);
        Assert.Equal("Lena Gast", record.Signature);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().AnamnesisRecords.SingleAsync(r => r.Id == record.Id, Ct);
        Assert.DoesNotContain("Lavendel", Encoding.UTF8.GetString(stored.Payload), StringComparison.Ordinal);

        var view = await client.GetFromJsonAsync<OnlineAppointment>($"/api/online/bookings/{confirmation.ManageToken}", TestSpa.Json, Ct);
        Assert.True(view!.AnamnesisSubmitted);
    }

    [Fact]
    public async Task Franzoesische_Buchung_bekommt_franzoesische_Bestaetigung()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var email = NewEmail();
        using var client = spa.OnlineClient();

        var response = await client.PostAsJsonAsync("/api/online/bookings", Request(spa, TestSpa.At(11), email) with { Language = "fr-CH" }, TestSpa.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mail = await fixture.Mailbox.WaitForAsync(email, m => m.Subject.StartsWith("Rendez-vous confirmé : Massage", StringComparison.Ordinal), Ct);
        Assert.Contains("Bonjour Lena,", mail.Text, StringComparison.Ordinal);
        Assert.Contains("lundi 1 mars 2027 à 11:00", mail.Text, StringComparison.Ordinal);
        Assert.Contains("questionnaire de santé", mail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Uhr", mail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anamnesebogen_auf_Italienisch_hat_dieselben_Fragen_wie_auf_Deutsch()
    {
        using var client = fixture.CreateClient();

        var german = await client.GetFromJsonAsync<AnamnesisFormDefinition>("/api/online/anamnesis/form?language=de", TestSpa.Json, Ct);
        var italian = await client.GetFromJsonAsync<AnamnesisFormDefinition>("/api/online/anamnesis/form?language=it", TestSpa.Json, Ct);
        var other = await client.GetFromJsonAsync<AnamnesisFormDefinition>("/api/online/anamnesis/form?language=es", TestSpa.Json, Ct);

        Assert.Equal("it", italian!.Language);
        Assert.Equal(german!.Version, italian.Version);
        Assert.Equal(german.Questions.Select(q => (q.Key, q.Kind, q.Required)), italian.Questions.Select(q => (q.Key, q.Kind, q.Required)));
        Assert.Equal(german.Questions.Select(q => q.DetailsLabel is null), italian.Questions.Select(q => q.DetailsLabel is null));
        Assert.Equal("È incinta o sta allattando?", italian.Questions.Single(q => q.Key == "pregnancy").Label);
        Assert.Contains("revocare il consenso", italian.ConsentText, StringComparison.Ordinal);
        Assert.Equal("de", other!.Language);
    }

    [Theory]
    [InlineData(null, "de")]
    [InlineData("", "de")]
    [InlineData("de-CH", "de")]
    [InlineData("EN", "en")]
    [InlineData("fr-CH", "fr")]
    [InlineData("it", "it")]
    [InlineData("es", "de")]
    public void Sprache_des_Kundenbereichs_faellt_auf_Deutsch_zurueck(string? language, string expected) =>
        Assert.Equal(expected, GuestLanguage.Normalize(language));

    [Fact]
    public async Task Team_ohne_Anmeldung_liest_keine_Anamnese()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/customers/{Guid.NewGuid()}/anamnesis", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Verschluesselung_bindet_die_Daten_an_den_Datensatz()
    {
        var protector = fixture.Factory.Services.GetRequiredService<HealthDataProtector>();
        var id = Guid.NewGuid();

        var payload = protector.Protect("{\"allergies\":true}", id);

        Assert.Equal("{\"allergies\":true}", protector.Unprotect(payload, id));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(payload, Guid.NewGuid()));
    }

    [Fact]
    public async Task Kundenbereich_hat_keine_fehlende_Migration()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OnlineDbContext>();

        Assert.False(db.Database.HasPendingModelChanges(),
            "Modell geändert: 'dotnet ef migrations add <Name> -p src/AispaStudio.Api --context OnlineDbContext -o Online/Migrations' ausführen.");
    }

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex SixDigits();
}
