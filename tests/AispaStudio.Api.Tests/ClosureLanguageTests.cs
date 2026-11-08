using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Closures;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Absage in der Sprache des Gastes (ADR 0073, Ä46): Vorlagen je Art in Deutsch, Englisch, Französisch und Italienisch mit den Platzhaltern der Mail-Vorlagen, die Sprache kommt aus den
/// Kundendaten (Rückfall Deutsch), die KI behält die Sprache des Textes, der Hinweis auf die KI steht nur in deutschen Nachrichten, und die Rückfrage zeigt je Gast die Sprache.
/// </summary>
public sealed class ClosureLanguageTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vienna).DateTime);

    private static readonly DateOnly Day = TestSpa.Monday.AddDays(7);

    // ---------- Texte ----------

    [Theory]
    [InlineData(null, "de")]
    [InlineData("", "de")]
    [InlineData("de", "de")]
    [InlineData("en", "en")]
    [InlineData("EN-gb", "en")]
    [InlineData("fr", "fr")]
    [InlineData("it", "it")]
    [InlineData("es", "de")]
    [InlineData("tr", "de")]
    public void Die_Sprache_kommt_aus_den_Kundendaten_und_faellt_auf_Deutsch_zurueck(string? code, string expected) =>
        Assert.Equal(expected, GuestLanguage.Normalize(code));

    [Theory]
    [InlineData("de", "Guten Tag {Anrede},", "Deshalb müssen wir Ihren Termin leider absagen:", "Absage Ihres Termins: {Behandlung}, {Zeit}")]
    [InlineData("en", "Hello {Anrede},", "We therefore have to cancel your appointment:", "Cancellation of your appointment: {Behandlung}, {Zeit}")]
    [InlineData("fr", "Bonjour {Anrede},", "Nous devons donc malheureusement annuler votre rendez-vous :", "Annulation de votre rendez-vous : {Behandlung}, {Zeit}")]
    [InlineData("it", "Buongiorno {Anrede},", "Siamo quindi spiacenti di dover annullare il suo appuntamento:", "Annullamento del suo appuntamento: {Behandlung}, {Zeit}")]
    public void Jede_Vorlage_hat_in_jeder_Sprache_Anrede_Behandlung_und_Zeit_je_Art_einen_eigenen_Satz(string language, string greeting, string consequence, string subject)
    {
        var reasons = new HashSet<string>();
        foreach (var kind in Enum.GetValues<ClosureKind>())
        {
            var (actualSubject, text) = ClosureText.CancellationTemplate(kind, null, "PERIOD", "Spa Wien", language);

            Assert.Equal(subject, actualSubject);
            Assert.StartsWith(greeting, text, StringComparison.Ordinal);
            Assert.Contains(consequence, text, StringComparison.Ordinal);
            Assert.Contains("{Behandlung}\n{Zeit}", text, StringComparison.Ordinal);
            Assert.Contains("PERIOD", text, StringComparison.Ordinal);
            Assert.EndsWith("Spa Wien", text, StringComparison.Ordinal);
            reasons.Add(text.Split('\n')[2]);
        }

        Assert.Equal(3, reasons.Count);
    }

    [Fact]
    public void Der_eigene_Text_fuer_Gaeste_ist_deutsch_und_gilt_nur_in_der_deutschen_Vorlage()
    {
        var (_, german) = ClosureText.CancellationTemplate(ClosureKind.Outage, "Wegen eines Wasserschadens bleibt das Spa zu", "PERIOD", "Spa Wien", "de");
        var (_, english) = ClosureText.CancellationTemplate(ClosureKind.Outage, "Wegen eines Wasserschadens bleibt das Spa zu", "PERIOD", "Spa Wien", "en");

        Assert.Contains("Wegen eines Wasserschadens bleibt das Spa zu. Deshalb", german, StringComparison.Ordinal);
        Assert.DoesNotContain("Wasserschaden", english, StringComparison.Ordinal);
        Assert.Contains("Unfortunately we have to close temporarily because of an outage (PERIOD).", english, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Zeitraum_steht_in_der_Sprache_des_Gastes()
    {
        var today = new DateOnly(2027, 3, 1);
        var from = new DateOnly(2027, 3, 24);
        var until = new DateOnly(2027, 4, 2);

        Assert.Equal("24. März bis 2. April", ClosureText.Period(from, null, until, null, today));
        Assert.Equal("24 March to 2 April", ClosureText.Period(from, null, until, null, today, "en"));
        Assert.Equal("du 24 mars au 2 avril", ClosureText.Period(from, null, until, null, today, "fr"));
        Assert.Equal("dal 24 marzo al 2 aprile", ClosureText.Period(from, null, until, null, today, "it"));
        Assert.Equal("24 March 2027 to 2 April 2027", ClosureText.Period(from, null, until, null, new DateOnly(2026, 10, 1), "en"));
        Assert.Equal("24 March from 13:00", ClosureText.Period(from, new TimeOnly(13, 0), from, null, today, "en"));
        Assert.Equal("24 mars jusqu’à 14:00", ClosureText.Period(from, null, from, new TimeOnly(14, 0), today, "fr"));
        Assert.Equal("24 marzo, dalle 09:00 alle 12:00", ClosureText.Period(from, new TimeOnly(9, 0), from, new TimeOnly(12, 0), today, "it"));
        Assert.Equal("24. März ab 13:00 Uhr", ClosureText.Period(from, new TimeOnly(13, 0), from, null, today, "de"));
    }

    [Fact]
    public void Der_Satz_zur_Rueckzahlung_nennt_den_Betrag_und_den_Zahlungsweg_in_jeder_Sprache()
    {
        Assert.Equal("Ihre Anzahlung von 30,00 € wird auf dem Zahlungsweg der Einzahlung retourniert.", ClosureText.RefundSentence(30m, "EUR", "de"));
        Assert.Equal("Your deposit of €30.00 will be refunded to the payment method you used.", ClosureText.RefundSentence(30m, "EUR", "en"));
        Assert.Contains("30,00 €", ClosureText.RefundSentence(30m, "EUR", "fr"), StringComparison.Ordinal);
        Assert.Contains("30,00 €", ClosureText.RefundSentence(30m, "EUR", "it"), StringComparison.Ordinal);
        Assert.Contains("1234,50 CHF", ClosureText.RefundSentence(1234.5m, "CHF", "de"), StringComparison.Ordinal);
    }

    // ---------- Absage in der Sprache des Gastes ----------

    private sealed record Guest(string First, string? Language, string Email, CustomerDto Customer);

    private async Task<(TestSpa Spa, List<Guest> Guests, List<AppointmentDto> Appointments, ClosureDto Closure)> SetupAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var suffix = Guid.NewGuid().ToString("N");
        var guests = new List<Guest>();
        var appointments = new List<AppointmentDto>();
        // Fünf Gäste am selben Montag: ohne Sprache, Englisch, Französisch, Italienisch und Spanisch (die es im Kundenbereich nicht gibt).
        var plan = new (string First, string? Language, int Hour, int Minute)[] { ("Ida", null, 9, 30), ("Emma", "en", 11, 0), ("Chloé", "fr", 13, 0), ("Giulia", "it", 14, 30), ("Lucía", "es", 16, 0) };
        foreach (var (first, language, hour, minute) in plan)
        {
            var email = $"{first.ToLowerInvariant().Replace("é", "e", StringComparison.Ordinal).Replace("í", "i", StringComparison.Ordinal)}-{suffix}@example.at";
            var customer = await spa.PostAsync<CustomerDto>("/api/customers", new CustomerInput(first, $"Test{suffix}", email, null, null, null, Language: language), Ct);
            guests.Add(new Guest(first, language, email, customer));
            appointments.Add(await TestSpa.ReadAsync<AppointmentDto>(await spa.BookAsync(TestSpa.At(hour, minute, Day), Ct, customer: customer.Id), Ct));
        }

        var closure = await ClosureTests.CreateAsync(spa, ClosureTests.Input(Day, Day.AddDays(2), ClosureKind.Vacation, reason: "Inventur"));
        return (spa, guests, appointments, closure);
    }

    private static string Path(TestSpa spa, ClosureDto closure, string tail = "") => $"{ClosureTests.Path(spa)}/{closure.Id}{tail}";

    [Fact]
    public async Task Die_Rueckfrage_zeigt_je_Gast_die_Sprache_und_die_Absage_geht_in_dieser_Sprache_hinaus()
    {
        var (spa, guests, appointments, closure) = await SetupAsync();

        var preview = await spa.GetAsync<ClosureCancellationPreview>(Path(spa, closure, "/cancellation"), Ct);
        Assert.Equal(["de", "en", "fr", "it", "de"], preview.Appointments.Select(a => a.Language));
        // Die bearbeitbare Vorlage ist die deutsche.
        Assert.StartsWith("Guten Tag {Anrede},", preview.SuggestedText, StringComparison.Ordinal);

        var request = new CancelClosureAppointmentsRequest([.. appointments.Select(a => a.Id)], "Absage: {Behandlung}", "Guten Tag {Anrede},\n\nIhr Termin ({Behandlung}, {Zeit}) fällt aus.\n\nIhr Team", false, false);
        var result = await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), request, Ct);
        Assert.Equal((5, 5, 0, 0), (result.Cancelled, result.Queued, result.WithoutContact, result.Failed));
        Assert.Equal(["de", "en", "fr", "it", "de"], result.Notices.OrderBy(n => n.AppointmentStart).Select(n => n.Language));

        var german = await fixture.Mailbox.WaitForAsync(guests[0].Email, m => m.Subject.StartsWith("Absage", StringComparison.Ordinal), Ct);
        Assert.Equal("Absage: Massage", german.Subject);
        Assert.StartsWith($"Guten Tag Ida {guests[0].Customer.LastName},\n\nIhr Termin (Massage, Montag, 8. März um 09:30 Uhr) fällt aus.", german.Text, StringComparison.Ordinal);

        var englishPeriod = ClosureText.Period(Day, null, Day.AddDays(2), null, Today, "en");
        var english = await fixture.Mailbox.WaitForAsync(guests[1].Email, m => m.Subject.StartsWith("Cancellation", StringComparison.Ordinal), Ct);
        Assert.Equal("Cancellation of your appointment: Massage, Monday, 8 March at 11:00", english.Subject);
        Assert.StartsWith("Hello Emma,\n\n", english.Text, StringComparison.Ordinal);
        Assert.Contains($"We are closed for our company holiday ({englishPeriod}). We therefore have to cancel your appointment:\n\nMassage\nMonday, 8 March at 11:00\n\nThank you for your understanding.\nWien", english.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ihr Termin", english.Text, StringComparison.Ordinal);

        var french = await fixture.Mailbox.WaitForAsync(guests[2].Email, m => m.Subject.StartsWith("Annulation", StringComparison.Ordinal), Ct);
        Assert.Equal("Annulation de votre rendez-vous : Massage, lundi 8 mars à 13:00", french.Subject);
        Assert.StartsWith("Bonjour Chloé,\n\nNous sommes fermés pour congés annuels (", french.Text, StringComparison.Ordinal);
        Assert.Contains($"({ClosureText.Period(Day, null, Day.AddDays(2), null, Today, "fr")})", french.Text, StringComparison.Ordinal);
        Assert.EndsWith("Nous vous remercions de votre compréhension.\nWien", french.Text, StringComparison.Ordinal);

        var italian = await fixture.Mailbox.WaitForAsync(guests[3].Email, m => m.Subject.StartsWith("Annullamento", StringComparison.Ordinal), Ct);
        Assert.Equal("Annullamento del suo appuntamento: Massage, lunedì 8 marzo alle 14:30", italian.Subject);
        Assert.StartsWith("Buongiorno Giulia,\n\nSiamo chiusi per le ferie aziendali (", italian.Text, StringComparison.Ordinal);

        // Eine Sprache, die es im Kundenbereich nicht gibt, bekommt die deutsche Vorlage (Rückfall Deutsch).
        var spanish = await fixture.Mailbox.WaitForAsync(guests[4].Email, m => m.Subject.StartsWith("Absage", StringComparison.Ordinal), Ct);
        Assert.StartsWith($"Guten Tag Lucía {guests[4].Customer.LastName},", spanish.Text, StringComparison.Ordinal);

        var notices = await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), Ct);
        Assert.Equal(["de", "en", "fr", "it", "de"], notices.OrderBy(n => n.AppointmentStart).Select(n => n.Language));
    }

    [Fact]
    public async Task Der_Hinweis_auf_die_KI_steht_nur_in_der_deutschen_Nachricht_die_anderen_sind_Standardvorlagen()
    {
        var (spa, guests, appointments, closure) = await SetupAsync();
        var request = new CancelClosureAppointmentsRequest([appointments[0].Id, appointments[1].Id, appointments[2].Id], "Absage", "Guten Tag {Anrede}, {Behandlung}, {Zeit}", true, true);

        await spa.PostAsync<CancelClosureAppointmentsResult>(Path(spa, closure, "/cancellation"), request, Ct);

        var german = await fixture.Mailbox.WaitForAsync(guests[0].Email, m => m.Subject == "Absage", Ct);
        Assert.Contains($"Einen neuen Termin können Sie hier buchen: http://localhost:5173/buchen/?spa={spa.TenantId}#/", german.Text, StringComparison.Ordinal);
        Assert.EndsWith(ReplyLanguages.Disclosure("de"), german.Text, StringComparison.Ordinal);

        var english = await fixture.Mailbox.WaitForAsync(guests[1].Email, m => m.Subject.StartsWith("Cancellation", StringComparison.Ordinal), Ct);
        Assert.Contains($"You can book a new appointment here: http://localhost:5173/buchen/?spa={spa.TenantId}#/", english.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("AI assistant", english.Text, StringComparison.Ordinal);
        var french = await fixture.Mailbox.WaitForAsync(guests[2].Email, m => m.Subject.StartsWith("Annulation", StringComparison.Ordinal), Ct);
        Assert.Contains("Vous pouvez réserver un nouveau rendez-vous ici : ", french.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("assistant IA", french.Text, StringComparison.Ordinal);

        var notices = await spa.GetAsync<List<ClosureNoticeDto>>(Path(spa, closure, "/notices"), Ct);
        Assert.Equal([true, false, false], notices.OrderBy(n => n.AppointmentStart).Select(n => n.AiWritten));
    }

    // ---------- KI ----------

    [Fact]
    public async Task Die_KI_bekommt_die_Sprache_des_Textes_und_behaelt_sie_Zahlen_und_Platzhalter_bleiben()
    {
        var (spa, _, _, closure) = await SetupAsync();
        var revise = Path(spa, closure, "/cancellation/revise");
        var (_, french) = ClosureText.CancellationTemplate(ClosureKind.Vacation, null, "du 8 au 10 mars", "Spa Wien", "fr");
        var (_, german) = ClosureText.CancellationTemplate(ClosureKind.Vacation, null, "8. bis 10. März", "Spa Wien", "de");

        // Ohne Angabe ist es die deutsche Vorlage; mit Angabe die Sprache des Gastes: das Modell bekommt sie genannt und der Text behält sie.
        var formalGerman = await spa.PostAsync<ReviseClosureTextResult>(revise, new ReviseClosureTextRequest(german, ReplyRevisionKind.Formal), Ct);
        var formalFrench = await spa.PostAsync<ReviseClosureTextResult>(revise, new ReviseClosureTextRequest(french, ReplyRevisionKind.Formal, "fr"), Ct);
        var formalEnglish = await spa.PostAsync<ReviseClosureTextResult>(revise, new ReviseClosureTextRequest("Hello {Anrede}, {Behandlung} on {Zeit}", ReplyRevisionKind.Formal, "en-GB"), Ct);

        Assert.StartsWith("Sehr geehrter Gast, Guten Tag {Anrede},", formalGerman.Text, StringComparison.Ordinal);
        Assert.StartsWith("Cher client, Bonjour {Anrede},", formalFrench.Text, StringComparison.Ordinal);
        Assert.StartsWith("Dear guest, Hello", formalEnglish.Text, StringComparison.Ordinal);
        Assert.All(new[] { formalGerman.Text, formalFrench.Text, formalEnglish.Text }, text => Assert.Contains("{Behandlung}", text, StringComparison.Ordinal));

        // Erfindet die KI eine Zahl, verwirft der Server die Fassung: der Text bleibt, wie er war.
        var invented = await spa.Client.PostAsJsonAsync(revise, new ReviseClosureTextRequest("PARKPLATZ {Anrede} {Behandlung} {Zeit}", ReplyRevisionKind.Apology, "it"), TestSpa.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invented.StatusCode);
        Assert.Contains("text", (await invented.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!.Errors.Keys);
    }
}
