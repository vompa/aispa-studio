using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Terminannahme per Mail mit der regelbasierten KI (ohne API-Schlüssel) und dem Test-Postfach.
/// Jeder Test hat einen eigenen Standort, eine eigene Behandlung und einen eigenen Gast mit eindeutigen Namen,
/// damit die Erkennung nicht auf Daten anderer Tests in derselben Datenbank trifft.
/// </summary>
public sealed partial class MailIntakeTests(ApiFixture fixture)
{
    /// <summary>Ein Donnerstag vor dem Testmontag; Mails kommen „heute“ an.</summary>
    private static readonly DateTimeOffset Received = TestSpa.At(8, 0, TestSpa.Monday.AddDays(-4));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Freier_Wunschtermin_wird_eingetragen_und_die_Bestaetigung_entworfen()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Terminanfrage",
            $"Hallo,\nich hätte gerne eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}.\n\nLiebe Grüße\nEva Sommer", Ct);

        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.Equal(InboundMailKind.BookingRequest, mail.Kind);
        Assert.Equal("de", mail.Language);
        var appointment = await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{mail.AppointmentId}", Ct);
        Assert.Equal(TestSpa.At(10), appointment.Start);
        Assert.Equal(spa.Treatment.Id, appointment.TreatmentId);
        Assert.Equal("Eva Sommer", appointment.CustomerName);
        var customer = await spa.Spa.GetAsync<CustomerDto>($"/api/customers/{appointment.CustomerId}", Ct);
        Assert.Equal(spa.Guest, customer.Email);

        Assert.Equal(ReplyStatus.Draft, mail.ReplyStatus);
        Assert.Equal(spa.Guest, mail.ReplyTo);
        Assert.Equal("Re: Terminanfrage", mail.ReplySubject);
        Assert.Contains("eingetragen", mail.ReplyBody, StringComparison.Ordinal);
        Assert.Contains("10:00", mail.ReplyBody, StringComparison.Ordinal);
        Assert.Contains("KI-Assistent", mail.AiDisclosure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Posteingang_braucht_Buchungsrecht_und_Zugang_zum_Standort_der_Mail()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var mail = await spa.ReceiveAsync(spa.Guest, "Terminanfrage",
            $"Hallo, bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}.\nEva Sommer", Ct);
        Assert.Equal(spa.Location.Id, mail.LocationId);

        using var therapist = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Employee.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await therapist.GetAsync("/api/inbox", Ct)).StatusCode);

        var other = await spa.Spa.CreateLocationAsync($"Anderswo {spa.Code}", [DayOfWeek.Monday], Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [other.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync($"/api/inbox/{mail.Id}", Ct)).StatusCode);
        var list = await reception.GetFromJsonAsync<List<InboundMailSummary>>("/api/inbox", TestSpa.Json, Ct);
        Assert.DoesNotContain(list!, m => m.Id == mail.Id);

        using var own = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [spa.Location.Id]);
        Assert.Equal(HttpStatusCode.OK, (await own.GetAsync($"/api/inbox/{mail.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Belegter_Wunschtermin_ergibt_Vorschlaege_die_die_Rezeption_buchen_und_senden_kann()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        await spa.BookAsync(TestSpa.At(10), Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Massage",
            $"Guten Tag, gibt es am 1.3.2027 um 10:00 einen Termin für {spa.Treatment.Name} im {spa.Location.Name}? Danke\nLukas Berg", Ct);

        Assert.Equal(InboundMailStatus.Proposed, mail.Status);
        Assert.Null(mail.AppointmentId);
        Assert.InRange(mail.Proposals.Count, 1, 3);
        Assert.All(mail.Proposals, p => Assert.NotEqual(TestSpa.At(10), p.Start));
        Assert.Contains(mail.Proposals, p => p.Start >= TestSpa.At(7) && p.Start <= TestSpa.At(13));
        Assert.Contains("freie Zeiten", mail.ReplyBody, StringComparison.Ordinal);

        var accepted = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/accept",
            new AcceptProposalRequest(mail.Proposals[0].Start, null, null, null), Ct);
        Assert.Equal(InboundMailStatus.Booked, accepted.Status);
        Assert.NotNull(accepted.AppointmentId);

        // Die Bestätigung wird in der Queue neu entworfen.
        var confirmed = await spa.WaitAsync(mail.Id, m => m.ReplyBody!.Contains("eingetragen", StringComparison.Ordinal), Ct);
        var send = await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/send", null, Ct);
        Assert.Equal(HttpStatusCode.Accepted, send.StatusCode);
        var sent = await spa.WaitAsync(mail.Id, m => m.ReplyStatus == ReplyStatus.Sent, Ct);
        Assert.NotNull(sent.RepliedAt);

        var reply = Assert.Single(fixture.Mailbox.To(spa.Guest));
        Assert.StartsWith(confirmed.ReplyBody!, reply.Text, StringComparison.Ordinal);
        Assert.EndsWith(confirmed.AiDisclosure!, reply.Text, StringComparison.Ordinal);
        Assert.NotNull(reply.InReplyTo);
    }

    [Fact]
    public async Task Englische_Mail_mit_Zeitraum_bekommt_englische_Vorschlaege_im_Zeitraum()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Booking",
            $"Hello, I would like to book a {spa.Treatment.Name} at {spa.Location.Name} on Monday between 13 and 16 h.\n\nBest regards\nJane Doe", Ct);

        Assert.Equal(InboundMailStatus.Proposed, mail.Status);
        Assert.Equal("en", mail.Language);
        Assert.All(mail.Proposals, p => Assert.InRange(p.Start, TestSpa.At(13), TestSpa.At(16)));
        Assert.StartsWith("Hello Jane Doe,", mail.ReplyBody, StringComparison.Ordinal);
        Assert.Contains("This message was written by an AI assistant", mail.AiDisclosure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_erkennbare_Behandlung_fragt_die_Antwort_nach()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Termin",
            $"Bonjour, je voudrais un rendez-vous à {spa.Location.Name} le 01.03.2027 à 14h. Merci", Ct);

        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Equal("fr", mail.Language);
        Assert.Contains("le soin souhaité", mail.ReplyBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Absage_geht_an_die_Rezeption_und_der_Gast_bekommt_eine_Eingangsbestaetigung()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Absage", "Hallo, ich muss meinen Termin leider absagen. LG", Ct);

        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Equal(InboundMailKind.Cancellation, mail.Kind);
        Assert.Contains("absagen", mail.Note, StringComparison.Ordinal);
        Assert.Contains("unser Team meldet sich", mail.ReplyBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Absage_findet_den_naechsten_Termin_und_die_Rezeption_storniert_ihn_mit_einem_Klick()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var customer = await spa.Spa.PostAsync<CustomerDto>("/api/customers",
            new CustomerInput("Lena", $"Gruber{Guid.NewGuid():N}", spa.Guest, null, null, null), Ct);
        var appointment = await TestSpa.ReadAsync<AppointmentDto>(
            await spa.Spa.BookAsync(TestSpa.At(10), Ct, treatment: spa.Treatment, location: spa.Location.Id, customer: customer.Id), Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Montag", "Hallo, ich komme nicht, bitte Storno. Danke, Lena", Ct);

        Assert.Equal(InboundMailKind.Cancellation, mail.Kind);
        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Equal(appointment.Id, mail.AppointmentId);
        Assert.True(mail.CanCancelAppointment);
        Assert.Contains("am 01.03.2027 um 10:00 Uhr", mail.Note, StringComparison.Ordinal);
        Assert.Equal(AppointmentStatus.Booked, (await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{appointment.Id}", Ct)).Status);

        var other = await spa.Spa.CreateLocationAsync($"Anderswo {spa.Code}", [DayOfWeek.Monday], Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception, [other.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/inbox/{mail.Id}/cancel-appointment", null, Ct)).StatusCode);

        var cancelled = await spa.Spa.PostAsync<InboundMailDto>($"/api/inbox/{mail.Id}/cancel-appointment", new { }, Ct);
        var again = await spa.Spa.Client.PostAsync($"/api/inbox/{mail.Id}/cancel-appointment", null, Ct);

        Assert.Equal(InboundMailStatus.Cancelled, cancelled.Status);
        Assert.False(cancelled.CanCancelAppointment);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        var stored = await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{appointment.Id}", Ct);
        Assert.Equal(AppointmentStatus.Cancelled, stored.Status);
        var redrafted = await spa.WaitAsync(mail.Id, m => m.ReplyBody!.Contains("storniert", StringComparison.Ordinal), Ct);
        Assert.Contains("Montag, 1. März 2027, 10:00 Uhr storniert", redrafted.ReplyBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Frage_bekommt_einen_Antwortvorschlag_der_KI_aus_den_Angaben_des_Spas_zur_Pruefung()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var subject = $"Öffnung {Guid.NewGuid():N}";

        var mail = await spa.ReceiveAsync(spa.Guest, subject, "Guten Tag, wie lange haben Sie abends geöffnet?", Ct);

        Assert.Equal(InboundMailKind.Question, mail.Kind);
        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Equal(ReplyStatus.Draft, mail.ReplyStatus);
        Assert.Equal(MailIntakeProcessor.SuggestedNote, mail.Note);
        Assert.Contains("Öffnungszeiten: Montag 09:00–20:00", mail.ReplyBody, StringComparison.Ordinal);
        Assert.NotNull(mail.AiDisclosure);

        var facts = FakeQuestionAnswerer.Requests[subject].Facts;
        Assert.Contains($"Standort {spa.Location.Name}", facts, StringComparison.Ordinal);
        Assert.Contains($"{spa.Treatment.Name}, 50 Minuten, 89,00 EUR", facts, StringComparison.Ordinal);
        Assert.Contains("Kostenlos stornieren bis", facts, StringComparison.Ordinal);
        Assert.Contains("/buchen/?spa=", facts, StringComparison.Ordinal);
        Assert.Contains("Sonntag geschlossen", facts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Antwort_in_den_Angaben_oder_mit_erfundener_Zahl_bleibt_es_beim_Weiterleiten()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        foreach (var body in new[] { "Gibt es bei Ihnen einen Parkplatz?", "Können Sie mir einen Preis erfinden?" })
        {
            var mail = await spa.ReceiveAsync(spa.Guest, $"Frage {Guid.NewGuid():N}", body, Ct);
            Assert.Equal(InboundMailKind.Question, mail.Kind);
            Assert.Equal("Frage des Gasts, bitte beantworten.", mail.Note);
            Assert.DoesNotContain("12345", mail.ReplyBody, StringComparison.Ordinal);
            Assert.Equal(ReplyStatus.Draft, mail.ReplyStatus);
        }

        Assert.True(SpaFacts.OnlyKnownNumbers("Montag 09:00 bis 18:00, 89,00 EUR", "Montag 09:00–18:00; Aroma, 50 Minuten, 89,00 EUR"));
        Assert.False(SpaFacts.OnlyKnownNumbers("Heute 20 % günstiger", "Montag 09:00–18:00"));
    }

    [Fact]
    public async Task Dieselbe_Mail_wird_nur_einmal_verarbeitet()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var messageId = $"<{Guid.NewGuid():N}@example.at>";

        var first = await spa.DeliverAsync(spa.Guest, "Frage", "Haben Sie am Sonntag offen?", Ct, messageId: messageId);
        var second = await spa.DeliverAsync(spa.Guest, "Frage", "Haben Sie am Sonntag offen?", Ct, messageId: messageId);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Treatwell_Buchung_wird_als_Termin_uebernommen_einmalig_und_storniert()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var reference = $"TW-{Guid.NewGuid():N}"[..14];

        var booked = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung über Treatwell", spa.TreatwellMail(reference, "14:00"), Ct);

        Assert.Equal(InboundMailStatus.Booked, booked.Status);
        Assert.Equal(BookingChannel.Treatwell, booked.Channel);
        Assert.Equal(InboundMailKind.TreatwellBooking, booked.Kind);
        Assert.Equal(ReplyStatus.None, booked.ReplyStatus);
        var appointment = await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.AppointmentId}", Ct);
        Assert.Equal(TestSpa.At(14), appointment.Start);
        Assert.Equal(spa.Employee.Id, appointment.EmployeeId);
        Assert.Equal($"Treatwell-Buchung {reference}", appointment.Note);
        Assert.Equal("Max Mustermann", appointment.CustomerName);
        Assert.True(appointment.AcceptInPlatformPending);
        Assert.Equal(PlatformPayment.None, appointment.PlatformPayment);

        var again = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung über Treatwell", spa.TreatwellMail(reference, "14:00"), Ct);
        Assert.Equal(InboundMailStatus.Ignored, again.Status);
        Assert.Equal(booked.AppointmentId, again.AppointmentId);

        var moved = await spa.ReceiveAsync("buchungen@treatwell.at", "Buchung geändert", spa.TreatwellMail(reference, "15:30"), Ct);
        Assert.Equal(InboundMailStatus.Booked, moved.Status);
        Assert.Equal(booked.AppointmentId, moved.AppointmentId);
        Assert.Equal(TestSpa.At(15, 30), (await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.AppointmentId}", Ct)).Start);

        var cancelled = await spa.ReceiveAsync("buchungen@treatwell.at", "Buchung storniert", spa.TreatwellMail(reference, "15:30"), Ct);
        Assert.Equal(InboundMailStatus.Cancelled, cancelled.Status);
        var after = await spa.Spa.GetAsync<AppointmentDto>($"/api/appointments/{booked.AppointmentId}", Ct);
        Assert.Equal(AppointmentStatus.Cancelled, after.Status);
    }

    [Fact]
    public async Task Treatwell_Mail_ohne_bestaetigten_Absender_wird_nicht_uebernommen()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);

        var mail = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung", spa.TreatwellMail("TW-FAKE", "14:00"), Ct, verified: false);

        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Null(mail.AppointmentId);
        Assert.Contains("Absender nicht bestätigt", mail.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Treatwell_Buchung_auf_belegte_Zeit_geht_mit_Alternativen_an_die_Rezeption()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        await spa.BookAsync(TestSpa.At(14), Ct);

        var mail = await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung", spa.TreatwellMail($"TW-{Guid.NewGuid():N}"[..14], "14:00", "max@example.at"), Ct);

        Assert.Equal(InboundMailStatus.NeedsReview, mail.Status);
        Assert.Contains("kollidiert mit", mail.Note, StringComparison.Ordinal);
        Assert.Contains("In Treatwell umbuchen oder absagen", mail.Note, StringComparison.Ordinal);
        Assert.Null(mail.AppointmentId);
        Assert.NotEmpty(mail.Proposals);
        // An Treatwell-Gäste geht nichts hinaus: die Buchung steht bei Treatwell, dort wird umgebucht.
        Assert.Null(mail.ReplyTo);
        Assert.Equal(ReplyStatus.None, mail.ReplyStatus);
    }

    [Fact]
    public async Task Leistungsname_von_Treatwell_wird_ueber_die_Zuordnung_erkannt()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        await spa.Spa.PostAsync<ChannelMappingDto>("/api/channels/Treatwell/mappings",
            new ChannelMappingInput(ChannelMappingKind.Treatment, $"Wohlfühlmassage {spa.Code} (50 Min.)", spa.Treatment.Id), Ct);
        var body = spa.TreatwellMail($"TW-{Guid.NewGuid():N}"[..14], "11:00").Replace($"Leistung: {spa.Treatment.Name}", $"Leistung: Wohlfühlmassage {spa.Code} (50 Min.)", StringComparison.Ordinal);

        var mail = await spa.ReceiveAsync("no-reply@treatwell.de", "Neue Buchung", body, Ct);

        Assert.Equal(InboundMailStatus.Booked, mail.Status);
        Assert.Equal(spa.Treatment.Id, mail.TreatmentId);
    }

    [Fact]
    public async Task Ics_Feed_zeigt_die_Belegung_ohne_Gastnamen_und_ohne_Treatwells_eigene_Termine()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var feed = await spa.Spa.PostAsync<CalendarFeedDto>($"/api/locations/{spa.Location.Id}/calendar-feeds",
            new CalendarFeedInput("Treatwell Anna", spa.Employee.Id, BookingChannel.Treatwell), Ct);
        // Die Termine liegen in der Zukunft relativ zum Testlauf, damit sie im Feed-Zeitraum sind.
        var monday = NextMonday();
        await spa.BookAsync(TestSpa.At(10, 0, monday), Ct);
        await spa.ReceiveAsync("buchungen@treatwell.at", "Neue Buchung",
            spa.TreatwellMail($"TW-{Guid.NewGuid():N}"[..14], "14:00", date: monday), Ct, received: DateTimeOffset.UtcNow);

        using var anonymous = fixture.CreateClient();
        var response = await anonymous.GetAsync(feed.Path, Ct);
        var ics = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        var start = TestSpa.At(9, 55, monday).UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        Assert.Contains($"DTSTART:{start}", ics, StringComparison.Ordinal);
        Assert.Equal(1, ics.Split("BEGIN:VEVENT").Length - 1);
        Assert.DoesNotContain("Sommer", ics, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/ics/unbekannt.ics", Ct)).StatusCode);
        await spa.Spa.Client.DeleteAsync($"/api/calendar-feeds/{feed.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(feed.Path, Ct)).StatusCode);
    }

    [Fact]
    public async Task Posteingang_braucht_eine_Anmeldung()
    {
        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/inbox", Ct)).StatusCode);
    }

    private static DateOnly NextMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7 + 7);
    }


    /// <summary>Eigener Standort mit Raum, einer Person (Mo 9–17 Uhr) und einer Behandlung mit eindeutigem Namen.</summary>
    private sealed class MailSpa
    {
        public required TestSpa Spa { get; init; }
        public required string Code { get; init; }
        public required LocationDto Location { get; init; }
        public required EmployeeDto Employee { get; init; }
        public required TreatmentDto Treatment { get; init; }
        public required string Guest { get; init; }

        public static async Task<MailSpa> CreateAsync(ApiFixture fixture, CancellationToken ct)
        {
            var spa = await TestSpa.CreateAsync(fixture, ct);
            // Nur Buchstaben, damit die Erkennung den Namen als Wort sieht.
            static string NewCode() => string.Concat(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(6)).ToUpperInvariant().PadRight(6, 'X');
            var code = NewCode();
            var location = await spa.CreateLocationAsync($"Spa {code}",
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday], ct);
            var room = await spa.PostAsync<RoomDto>($"/api/locations/{location.Id}/rooms", new RoomInput("Raum", 0), ct);
            var employee = await spa.CreateEmployeeAsync($"Anna {code}", [spa.Massage.Id], location.Id, ct);
            var treatment = await spa.PostAsync<TreatmentDto>("/api/treatments",
                new TreatmentInput($"Aroma {NewCode()}", 50, 5, 10, 89m, [spa.Massage.Id], [room.Id], []), ct);
            return new MailSpa
            {
                Spa = spa,
                Code = code,
                Location = location,
                Employee = employee,
                Treatment = treatment,
                Guest = $"gast-{Guid.NewGuid():N}@example.at",
            };
        }

        public async Task BookAsync(DateTimeOffset start, CancellationToken ct)
        {
            var response = await Spa.BookAsync(start, ct, treatment: Treatment, location: Location.Id);
            await TestSpa.ReadAsync<AppointmentDto>(response, ct);
        }

        public string TreatwellMail(string reference, string time, string? email = null, DateOnly? date = null) =>
            $"""
             Hallo,

             Sie haben eine neue Buchung über Treatwell erhalten.

             Buchungsnummer: {reference}
             Salon: {Location.Name}
             Leistung: {Treatment.Name}
             Datum: {(date ?? TestSpa.Monday).ToString("dddd, d. MMMM yyyy", CultureInfo.GetCultureInfo("de-AT"))}
             Uhrzeit: {time} Uhr
             Mitarbeiter: Anna {Code}
             Kunde: Max Mustermann
             Telefon: +43 660 {Guid.NewGuid().GetHashCode() & 0xFFFFFF}
             {(email is null ? "" : $"E-Mail: {email}")}

             Ihr Treatwell-Team
             """;

        public async Task<Guid> DeliverAsync(string from, string subject, string body, CancellationToken ct,
            bool verified = true, string? messageId = null, DateTimeOffset? received = null)
        {
            var response = await Spa.Client.PostAsJsonAsync("/api/inbox/dev/mails", new
            {
                from,
                fromName = (string?)null,
                subject,
                body,
                messageId,
                receivedAt = received ?? Received,
                senderVerified = verified,
            }, TestSpa.Json, ct);
            return (await TestSpa.ReadAsync<Delivered>(response, ct)).Id;
        }

        /// <summary>Mail einwerfen und warten, bis die Queue sie verarbeitet hat.</summary>
        public async Task<InboundMailDto> ReceiveAsync(string from, string subject, string body, CancellationToken ct,
            bool verified = true, DateTimeOffset? received = null)
        {
            var id = await DeliverAsync(from, subject, body, ct, verified, received: received);
            return await WaitAsync(id, m => m.Status != InboundMailStatus.Received, ct);
        }

        /// <summary>Wartet auf die verarbeitete Nachricht eines Absenders, die über einen Webhook kam (ohne bekannte Id).</summary>
        public async Task<InboundMailDto> WaitForSenderAsync(string from, CancellationToken ct)
        {
            var until = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                var list = await Spa.GetAsync<List<InboundMailSummary>>("/api/inbox", ct);
                if (list.FirstOrDefault(m => m.From == from) is { } found)
                {
                    return await WaitAsync(found.Id, m => m.Status != InboundMailStatus.Received, ct);
                }

                if (DateTime.UtcNow > until)
                {
                    throw new TimeoutException($"Keine Nachricht von {from} im Posteingang.");
                }

                await Task.Delay(100, ct);
            }
        }

        public async Task<InboundMailDto> WaitAsync(Guid id, Func<InboundMailDto, bool> done, CancellationToken ct)
        {
            var until = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                var mail = await Spa.GetAsync<InboundMailDto>($"/api/inbox/{id}", ct);
                if (done(mail))
                {
                    return mail;
                }

                if (DateTime.UtcNow > until)
                {
                    throw new TimeoutException($"Mail {id} ist nicht fertig: {mail.Status} {mail.Note}");
                }

                await Task.Delay(100, ct);
            }
        }

        private sealed record Delivered(Guid Id);
    }
}
