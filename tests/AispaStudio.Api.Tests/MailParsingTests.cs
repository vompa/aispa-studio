using AispaStudio.Api.MailIntake;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>Erkennung ohne Datenbank: Treatwell-Format und die regelbasierte Terminannahme.</summary>
public sealed class MailParsingTests
{
    private static readonly NamedOption HotStone = new(Guid.NewGuid(), "Hot-Stone-Massage");
    private static readonly NamedOption Classic = new(Guid.NewGuid(), "Klassische Massage");
    private static readonly NamedOption Facial = new(Guid.NewGuid(), "Gesichtsbehandlung");
    private static readonly NamedOption Vienna = new(Guid.NewGuid(), "Wien Innere Stadt");
    private static readonly NamedOption Salzburg = new(Guid.NewGuid(), "Salzburg Altstadt");
    private static readonly NamedOption Lena = new(Guid.NewGuid(), "Lena Huber");

    /// <summary>Donnerstag, 25. Februar 2027, 08:00 Uhr in Wien.</summary>
    private static readonly DateTimeOffset Thursday = new(2027, 2, 25, 7, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Treatwell_Mail_auf_Englisch_mit_Datum_und_Uhrzeit_in_einer_Zeile()
    {
        var parsed = TreatwellMail.Parse("Booking confirmed", """
            You have a new booking!
            **Booking reference:** #A1B2C3
            Venue: Wien Innere Stadt
            Service: Hot Stone Massage 75 min
            Date: Monday, 1 March 2027 at 14:30
            Staff member: Lena
            Client: Jane Doe
            Email: jane@example.com
            """);

        Assert.NotNull(parsed);
        Assert.Equal(InboundMailKind.TreatwellBooking, parsed.Kind);
        Assert.Equal("A1B2C3", parsed.Reference);
        Assert.Equal(new DateOnly(2027, 3, 1), parsed.Date);
        Assert.Equal(new TimeOnly(14, 30), parsed.Time);
        Assert.Equal("Lena", parsed.Staff);
        Assert.Equal("jane@example.com", parsed.Email);
    }

    [Theory]
    [InlineData("Buchung storniert", InboundMailKind.TreatwellCancellation)]
    [InlineData("Your booking was rescheduled", InboundMailKind.TreatwellChange)]
    [InlineData("Neue Buchung", InboundMailKind.TreatwellBooking)]
    public void Treatwell_Art_kommt_aus_Betreff_oder_Text(string subject, InboundMailKind kind) =>
        Assert.Equal(kind, TreatwellMail.Parse(subject, "Buchungsnummer: 123\nDatum: 01.03.2027\nUhrzeit: 10:00")!.Kind);

    [Fact]
    public void Ohne_Buchungsnummer_ist_es_keine_Treatwell_Buchung() =>
        Assert.Null(TreatwellMail.Parse("Newsletter", "Entdecken Sie unsere Angebote!\nDatum: heute"));

    [Theory]
    [InlineData("treatwell.de", "buchungen@treatwell.de", true)]
    [InlineData("treatwell.com", "bookings@partner.treatwell.com", true)]
    [InlineData("treatwell.com", "Treatwell <no-reply@mail.TREATWELL.com>", true)]
    [InlineData("treatwell.com", "betrug@treatwell.com.example.net", false)]
    [InlineData("treatwell.com", "gast@nottreatwell.com", false)]
    [InlineData("buchungen@treatwell.at", "buchungen@treatwell.at", true)]
    [InlineData("buchungen@treatwell.at", "andere@treatwell.at", false)]
    [InlineData("buchungen@treatwell.at", "buchungen@partner.treatwell.at", false)]
    public void Absender_Regel_gilt_fuer_genaue_Adresse_oder_Domain_mit_Unterdomains(string pattern, string from, bool expected) =>
        Assert.Equal(expected, SenderRules.Specificity(pattern, from) > 0);

    [Fact]
    public void Genaue_Adresse_geht_vor_Domain_und_laengere_Domain_vor_kuerzerer()
    {
        static SenderRule Rule(string pattern, SenderHandling handling = SenderHandling.Automatic, bool active = true) =>
            new() { Pattern = pattern, Handling = handling, Active = active };
        var rules = new[] { Rule("treatwell.com"), Rule("partner.treatwell.com", SenderHandling.Suggest), Rule("bookings@partner.treatwell.com", SenderHandling.Ignore) };

        Assert.Equal(SenderHandling.Ignore, SenderRules.Best(rules, "bookings@partner.treatwell.com")!.Handling);
        Assert.Equal(SenderHandling.Suggest, SenderRules.Best(rules, "x@partner.treatwell.com")!.Handling);
        Assert.Equal(SenderHandling.Automatic, SenderRules.Best(rules, "x@treatwell.com")!.Handling);
        Assert.Null(SenderRules.Best([Rule("treatwell.com", active: false)], "x@treatwell.com"));
    }

    [Theory]
    [InlineData("Treatwell.DE", "treatwell.de")]
    [InlineData("@treatwell.de", "treatwell.de")]
    [InlineData("*.treatwell.de", "treatwell.de")]
    [InlineData(" Buchungen@Treatwell.at ", "buchungen@treatwell.at")]
    [InlineData("treatwell", null)]
    [InlineData("nur ein text", null)]
    [InlineData("a@b", null)]
    [InlineData("", null)]
    public void Absender_Muster_wird_vereinheitlicht_oder_abgelehnt(string input, string? expected) =>
        Assert.Equal(expected, SenderRules.NormalizePattern(input));

    private static readonly DateTimeOffset FixtureReceived = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Synthetische_HTML_Mail_mit_Tabelle_wird_von_den_Regeln_gelesen()
    {
        var html = MailFixtures.Load(MailFixtures.BookingTable, "TW-9823711", "24. Oktober 2026 um 14:00 Uhr");

        var parsed = TreatwellMail.Parse("Neue Buchung", html);

        Assert.NotNull(parsed);
        Assert.Equal(InboundMailKind.TreatwellBooking, parsed.Kind);
        Assert.Equal("TW-9823711", parsed.Reference);
        Assert.Equal(new DateOnly(2026, 10, 24), parsed.Date);
        Assert.Equal(new TimeOnly(14, 0), parsed.Time);
        Assert.Equal("Max Mustermann", parsed.Customer);
        Assert.Equal("+43 660 1234567", parsed.Phone);
        Assert.Equal("Lena", parsed.Staff);
        Assert.Equal("Hot-Stone-Massage", parsed.Service);
        Assert.Equal(PlatformPayment.Prepaid, parsed.Payment);
        Assert.Equal(35.00m, parsed.Total);
    }

    [Fact]
    public void Synthetische_HTML_Mail_mit_Liste_wird_von_den_Regeln_gelesen()
    {
        var html = MailFixtures.Load(MailFixtures.BookingList, "TW-4410293", "28. Oktober 2026 um 10:30 Uhr");

        var parsed = TreatwellMail.Parse("Neue Buchung", html);

        Assert.NotNull(parsed);
        Assert.Equal("TW-4410293", parsed.Reference);
        Assert.Equal(new DateOnly(2026, 10, 28), parsed.Date);
        Assert.Equal(new TimeOnly(10, 30), parsed.Time);
        Assert.Equal("Anna Schmidt", parsed.Customer);
        Assert.Equal(PlatformPayment.OnSite, parsed.Payment);
        Assert.Equal(129.00m, parsed.Total);
    }

    [Theory]
    [InlineData(MailFixtures.Cancellation, "Buchung storniert", InboundMailKind.TreatwellCancellation)]
    [InlineData(MailFixtures.Change, "Buchung geändert", InboundMailKind.TreatwellChange)]
    [InlineData(MailFixtures.BookingTable, "Neue Buchung", InboundMailKind.TreatwellBooking)]
    public void Synthetische_Mails_zu_Absage_Aenderung_und_Neubuchung_werden_unterschieden(string fixture, string subject, InboundMailKind kind)
    {
        var parsed = TreatwellMail.Parse(subject, MailFixtures.Load(fixture, "TW-1234567", "24. Oktober 2026 um 14:00 Uhr"));

        Assert.NotNull(parsed);
        Assert.Equal(kind, parsed.Kind);
        Assert.Equal("TW-1234567", parsed.Reference);
    }

    [Fact]
    public void HTML_ohne_Skripte_Formatvorlagen_und_Kommentare_wird_zu_Zeilen()
    {
        var text = TreatwellMail.PlainText(MailFixtures.Load(MailFixtures.BookingTable, "TW-1", "24. Oktober 2026 um 14:00 Uhr"));

        Assert.Contains("Kunde: Max Mustermann", text, StringComparison.Ordinal);
        Assert.Contains("Datum & Uhrzeit: 24. Oktober 2026 um 14:00 Uhr", text, StringComparison.Ordinal);
        Assert.DoesNotContain("padding", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETISCH", text, StringComparison.Ordinal);
        Assert.Contains("Annehmen", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Online bezahlt (Treatwell Pay)", PlatformPayment.Prepaid)]
    [InlineData("Bereits bezahlt", PlatformPayment.Prepaid)]
    [InlineData("Paid online", PlatformPayment.Prepaid)]
    [InlineData("Zahlung im Salon", PlatformPayment.OnSite)]
    [InlineData("Noch nicht bezahlt", PlatformPayment.OnSite)]
    [InlineData("Vor Ort bezahlen", PlatformPayment.OnSite)]
    [InlineData("Bar", PlatformPayment.OnSite)]
    [InlineData("Bestätigt", PlatformPayment.None)]
    public void Zahlungsangabe_wird_als_vorab_oder_vor_Ort_erkannt(string text, PlatformPayment expected) =>
        Assert.Equal(expected, TreatwellMail.PaymentOf(text));

    [Theory]
    [InlineData("35,00 €", 35.00)]
    [InlineData("129,00 €", 129.00)]
    [InlineData("€35.00", 35.00)]
    [InlineData("EUR 1.234,50", 1234.50)]
    [InlineData("1.234", 1234)]
    [InlineData("12,5", 12.5)]
    public void Betrag_wird_in_verschiedenen_Schreibweisen_gelesen(string text, double expected) =>
        Assert.Equal((decimal)expected, TreatwellMail.ParseMoney(text));

    [Fact]
    public void KI_Ergebnis_wird_nur_angenommen_wenn_es_in_der_Mail_steht_und_plausibel_ist()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var text = TreatwellMail.PlainText(MailFixtures.Load(MailFixtures.BookingTable, "TW-9823711", "24. Oktober 2026 um 14:00 Uhr"));
        var good = new TreatwellExtraction("new", "#TW-9823711", "Max Mustermann", "+43 660 1234567", null, "2026-10-24", "14:00",
            [new TreatwellExtractionTreatment("Hot-Stone-Massage", 60, 35m)], "Lena", 35m, "prepaid", "Wien Innere Stadt");

        var (booking, problem) = TreatwellReader.Validate(good, "Neue Buchung", text, FixtureReceived, zone);
        Assert.Null(problem);
        Assert.Equal("TW-9823711", booking!.Reference);
        Assert.Equal(PlatformPayment.Prepaid, booking.Payment);
        Assert.Equal(35m, booking.Total);

        string? Problem(TreatwellExtraction e, string subject = "Neue Buchung") => TreatwellReader.Validate(e, subject, text, FixtureReceived, zone).Problem;
        Assert.Contains("steht nicht in der Mail", Problem(good with { BookingNumber = "HACKED-1" }), StringComparison.Ordinal);
        Assert.Contains("keine Buchungsnummer", Problem(good with { BookingNumber = " " }), StringComparison.Ordinal);
        Assert.Contains("nicht plausibel", Problem(good with { Date = "2031-01-01" }), StringComparison.Ordinal);
        Assert.Contains("nicht plausibel", Problem(good with { Date = "2026-01-01" }), StringComparison.Ordinal);
        Assert.Contains("nicht lesbar", Problem(good with { Time = null }), StringComparison.Ordinal);
        Assert.Contains("keine Behandlung", Problem(good with { Treatments = [] }), StringComparison.Ordinal);
        Assert.Contains("Name des Gasts", Problem(good with { GuestName = "Frau Unbekannt" }), StringComparison.Ordinal);
        Assert.Contains("Art der Mail", Problem(good with { Kind = "cancelled" }), StringComparison.Ordinal);
        Assert.Null(Problem(good with { Kind = "cancelled", Date = null, Time = null, Treatments = null }, "Buchung storniert"));
    }

    [Fact]
    public async Task Italienisch_morgen_nachmittag_mit_Wunschperson()
    {
        var analysis = await AnalyzeAsync("Prenotazione",
            "Buongiorno, vorrei prenotare una Hot-Stone-Massage domani pomeriggio con Lena Huber a Salzburg Altstadt. Grazie");

        Assert.Equal(MailIntent.BookingRequest, analysis.Intent);
        Assert.Equal("it", analysis.Language);
        Assert.Equal(HotStone.Id, analysis.TreatmentId);
        Assert.Equal(Salzburg.Id, analysis.LocationId);
        Assert.Equal(Lena.Id, analysis.EmployeeId);
        var wish = Assert.Single(analysis.Wishes);
        Assert.Equal(new RequestedTime(new DateOnly(2027, 2, 26), new TimeOnly(12, 0), new TimeOnly(18, 0)), wish);
    }

    [Fact]
    public async Task Titel_und_Anrede_aus_der_Signatur_sind_nur_ein_Vorschlag_und_nicht_im_Namen()
    {
        var analysis = await AnalyzeAsync("Termin",
            "Sehr geehrte Damen und Herren,\n\nich möchte eine Massage am 3.1. um 18:30.\n\nMit freundlichen Grüßen\nMag. Stefanie Huber");

        Assert.Equal(("Stefanie", "Huber"), (analysis.FirstName, analysis.LastName));
        Assert.Equal("Mag.", analysis.TitlesBefore);
        Assert.Null(analysis.Salutation);
    }

    [Fact]
    public async Task Grussformel_und_Name_in_einer_Zeile_mit_Dr_und_Frau()
    {
        var withTitle = await AnalyzeAsync("Termin", "Ich möchte eine Massage am 3.1. um 18:30.\n\nMit freundlichen Grüßen, Dr. Stefanie Huber");
        var withSalutation = await AnalyzeAsync("Termin", "Ich möchte eine Massage am 3.1. um 18:30.\n\nLiebe Grüße\nFrau Eva Muster");

        Assert.Equal(("Stefanie", "Huber", "Dr."), (withTitle.FirstName, withTitle.LastName, withTitle.TitlesBefore));
        Assert.Equal(("Eva", "Muster", null, CustomerSalutation.Female), (withSalutation.FirstName, withSalutation.LastName, withSalutation.TitlesBefore, withSalutation.Salutation));
    }

    [Fact]
    public async Task Behandlung_aus_einem_markanten_Wort_und_Datum_ohne_Jahr_liegt_in_der_Zukunft()
    {
        var analysis = await AnalyzeAsync("Termin", "Hallo, ich möchte eine Massage am 3.1. um 18:30.\n\nViele Grüße\nEva Muster");

        Assert.Equal(Classic.Id, analysis.TreatmentId);
        Assert.Equal(new RequestedTime(new DateOnly(2028, 1, 3), new TimeOnly(18, 30), null), Assert.Single(analysis.Wishes));
        Assert.Equal(("Eva", "Muster"), (analysis.FirstName, analysis.LastName));
    }

    [Fact]
    public async Task Antwort_in_der_Sprache_des_Gasts_mit_den_Alternativen()
    {
        var draft = await new RuleBasedBookingAssistant().DraftReplyAsync(new ReplyRequest(
            "fr", ReplyCase.Proposed, "Claire", "Rendez-vous", "", "Spa Test", "Gesichtsbehandlung", "Wien Innere Stadt", null,
            [new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Unspecified)], []), Ct);

        Assert.Equal("Re: Rendez-vous", draft.Subject);
        Assert.StartsWith("Bonjour Claire,", draft.Body, StringComparison.Ordinal);
        Assert.Contains("lundi 1 mars 2027, 10:00", draft.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de", "KI-Assistenten")]
    [InlineData("fr", "assistant IA")]
    [InlineData("es", "KI-Assistenten")]
    public void KI_Hinweis_gibt_es_in_jeder_Sprache(string language, string expected) =>
        Assert.Contains(expected, ReplyLanguages.Disclosure(language), StringComparison.Ordinal);

    private static Task<MailAnalysis> AnalyzeAsync(string subject, string body) =>
        new RuleBasedBookingAssistant().AnalyzeAsync(new MailAnalysisRequest(
            "gast@example.at", null, subject, body, Thursday, "Europe/Vienna",
            [HotStone, Classic, Facial], [Vienna, Salzburg], [Lena]), Ct);
}
