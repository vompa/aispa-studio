using System.Globalization;
using AispaStudio.Api.Assistant;
using AispaStudio.Api.Assistant.Answers;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Absicht der Betriebsfragen ohne KI (ADR 0072): die Regeln erkennen Thema, Namen und Zeitraum; Fragen zur Bedienung, Kundensuche und die bisherigen
/// Auswertungen bleiben, wie sie waren. „Heute“ ist hier Mittwoch, der 3. März 2027.
/// </summary>
public sealed class BusinessIntentTests
{
    private static readonly DateOnly Today = new(2027, 3, 3);

    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("Wie lange haben wir heute offen?", BusinessTopic.OpeningHours)]
    [InlineData("Haben wir am Samstag offen?", BusinessTopic.OpeningHours)]
    [InlineData("Wann sperren wir morgen zu?", BusinessTopic.OpeningHours)]
    [InlineData("Ist am 1. November offen?", BusinessTopic.OpeningHours)]
    [InlineData("Was sind unsere Öffnungszeiten?", BusinessTopic.OpeningHours)]
    [InlineData("Sind wir im Jänner geschlossen?", BusinessTopic.OpeningHours)]
    [InlineData("Haben wir im März Betriebsurlaub?", BusinessTopic.OpeningHours)]
    [InlineData("Gibt es nächste Woche eine Schließzeit?", BusinessTopic.OpeningHours)]
    [InlineData("Unsere Kontaktdaten", BusinessTopic.Contact)]
    [InlineData("Wie lautet unsere Telefonnummer?", BusinessTopic.Contact)]
    [InlineData("Wie ist unsere Adresse?", BusinessTopic.Contact)]
    [InlineData("Welche Mailadresse haben wir?", BusinessTopic.Contact)]
    [InlineData("Wann ist der nächste Feiertag?", BusinessTopic.Holiday)]
    [InlineData("Ist der 1. November ein Feiertag?", BusinessTopic.Holiday)]
    [InlineData("Wie kommt der Gast zu uns, Parkplätze?", BusinessTopic.Directions)]
    [InlineData("Gibt es Parkplätze?", BusinessTopic.Directions)]
    [InlineData("Was ist unsere Stornoregel?", BusinessTopic.CancellationRule)]
    [InlineData("Bis wann kann man kostenlos absagen?", BusinessTopic.CancellationRule)]
    [InlineData("Wer arbeitet heute?", BusinessTopic.WhoWorks)]
    [InlineData("Wer arbeitet morgen?", BusinessTopic.WhoWorks)]
    [InlineData("Wer hat am Freitag Dienst?", BusinessTopic.WhoWorks)]
    [InlineData("Ist Lena nächste Woche da?", BusinessTopic.PersonAvailable)]
    [InlineData("Hat Lena nächste Woche Urlaub?", BusinessTopic.PersonAvailable)]
    [InlineData("Wie viele Termine haben wir heute noch?", BusinessTopic.RemainingAppointments)]
    [InlineData("Wie voll sind wir am Freitag?", BusinessTopic.DayLoad)]
    [InlineData("Welcher Raum ist jetzt frei?", BusinessTopic.RoomFree)]
    [InlineData("Ist die Sauna voll?", BusinessTopic.AreaLoad)]
    [InlineData("Was kostet Hot Stone und wie lange dauert sie?", BusinessTopic.TreatmentInfo)]
    [InlineData("Wer bietet Thai-Massage an?", BusinessTopic.WhoOffers)]
    [InlineData("Wann war Frau Müller zuletzt da?", BusinessTopic.GuestLastVisit)]
    [InlineData("Hat Herr Berger nächste Woche einen Termin?", BusinessTopic.GuestNextVisit)]
    [InlineData("Wie viel ist auf Gutschein 1234 noch drauf?", BusinessTopic.VoucherBalance)]
    [InlineData("Welche Rabatte oder Aktionen laufen gerade?", BusinessTopic.Promotions)]
    [InlineData("Gibt es im November ein Angebot?", BusinessTopic.Promotions)]
    [InlineData("Gilt die Aktion auch für Pakete?", BusinessTopic.Promotions)]
    [InlineData("Top-Behandlungen diesen Monat", BusinessTopic.TopTreatments)]
    [InlineData("Top-Kunden im letzten Quartal", BusinessTopic.TopGuests)]
    [InlineData("Welche Therapeutin hat die meisten Termine?", BusinessTopic.TopTherapist)]
    [InlineData("Auslastung letzte Woche je Person und Raum", BusinessTopic.Utilization)]
    [InlineData("Umsatz nach Behandlung im September", BusinessTopic.RevenueByTreatment)]
    [InlineData("Wie viele Nicht-erschienen hatten wir?", BusinessTopic.NoShows)]
    public void Betriebsfragen_werden_dem_Thema_zugeordnet(string question, BusinessTopic topic)
    {
        var intent = BusinessIntents.Detect(question, Today);
        Assert.NotNull(intent);
        Assert.Equal((AssistantTask.Business, topic), (intent.Task, intent.Topic!.Value));
    }

    [Theory]
    [InlineData("wann hat herr steiner seinen termin", BusinessTopic.GuestNextVisit, "steiner")]
    [InlineData("wann kommt steiner", BusinessTopic.GuestNextVisit, "steiner")]
    [InlineData("termin herr steinr", BusinessTopic.GuestNextVisit, "steinr")]
    [InlineData("wann war sie zuletzt da", BusinessTopic.GuestLastVisit, null)]
    public void Kleingeschriebene_Gastfragen_erkennen_Thema_und_Namen(string question, BusinessTopic topic, string? name)
    {
        var intent = BusinessIntents.Detect(question, Today);

        Assert.NotNull(intent);
        Assert.Equal((topic, name), (intent.Topic, intent.Subject));
    }

    [Theory]
    [InlineData("Hat sie diese Woche noch einen Termin frei?")]
    [InlineData("Ist er morgen frei?")]
    public void Freie_Zeiten_mit_Pronomen_sind_keine_Gastfrage(string question)
    {
        var intent = BusinessIntents.Detect(question, Today);

        Assert.False(intent?.Topic is BusinessTopic.GuestNextVisit or BusinessTopic.GuestLastVisit, $"{question} → {intent?.Topic}");
    }

    [Theory]
    [InlineData("wann hat herr steiner seinen termin")]
    [InlineData("wann kommt steiner")]
    public void Ohne_Zeitraum_fragt_die_Gastfrage_nach_dem_naechsten_Termin(string question)
    {
        var intent = BusinessIntents.Detect(question, Today)!;

        Assert.Equal("next", BusinessIntents.Question(intent, question, Today)!.Detail);
    }

    [Theory]
    [InlineData("Wie verschiebe ich einen Termin?")]
    [InlineData("Wie lege ich eine Aktion an?")]
    [InlineData("Wo stelle ich den Barzahlungsrabatt ein?")]
    [InlineData("How do I void a receipt?")]
    [InlineData("Wie war der Umsatz letzte Woche?")]
    [InlineData("Auslastung diesen Monat")]
    [InlineData("Wer ist Frau Gruber?")]
    [InlineData("Telefonnummer von Frau Gruber")]
    [InlineData("Wann ist eine Hot-Stone frei?")]
    [InlineData("Wie viele Termine habe ich heute?")]
    [InlineData("Was steht morgen an?")]
    [InlineData("Wie spät ist es in Tokio?")]
    [InlineData("Ist am 01.03.2027 um 10 Uhr eine Massage bei Anna frei?")]
    public void Andere_Fragen_bleiben_beim_Handbuch_den_Kunden_und_den_bisherigen_Auswertungen(string question) =>
        Assert.Null(BusinessIntents.Detect(question, Today));

    [Fact]
    public void Namen_Tage_und_Zeitraeume_stehen_in_den_Bausteinen()
    {
        BusinessQuestion Q(string text) => BusinessIntents.Question(BusinessIntents.Detect(text, Today)!, text, Today)!;

        Assert.Equal(new DateOnly(2027, 3, 6), Q("Haben wir am Samstag offen?").From);
        Assert.Equal(new DateOnly(2027, 3, 4), Q("Wann sperren wir morgen zu?").From);
        Assert.Equal(Today, Q("Wie lange haben wir heute offen?").From);
        Assert.Equal(new DateOnly(2027, 11, 1), Q("Ist am 1. November offen?").From);
        Assert.Equal("week", Q("Was sind unsere Öffnungszeiten?").Detail);
        Assert.Equal("phone", Q("Wie lautet unsere Telefonnummer?").Detail);
        Assert.Equal("date", Q("Ist der 1. November ein Feiertag?").Detail);
        Assert.Equal("next", Q("Wann ist der nächste Feiertag?").Detail);

        var lena = Q("Ist Lena nächste Woche da?");
        Assert.Equal(("Lena", new DateOnly(2027, 3, 8), new DateOnly(2027, 3, 14)), (lena.Subject, lena.From, lena.Until));
        Assert.Equal("Müller", Q("Wann war Frau Müller zuletzt da?").Subject);
        var berger = Q("Hat Herr Berger nächste Woche einen Termin?");
        Assert.Equal(("Berger", new DateOnly(2027, 3, 8), new DateOnly(2027, 3, 14)), (berger.Subject, berger.From, berger.Until));
        Assert.Equal("1234", Q("Wie viel ist auf Gutschein 1234 noch drauf?").Subject);
        Assert.Equal("remaining", Q("Wie viele Termine haben wir heute noch?").Detail);
        Assert.Equal("sauna", Q("Ist die Sauna voll?").Subject);

        // Auswertungen enden heute; ohne Zeitangabe gilt der Monat bis heute.
        var top = Q("Top-Behandlungen diesen Monat");
        Assert.Equal((new DateOnly(2027, 3, 1), Today), (top.From, top.Until));
        var guests = Q("Top-Kunden im letzten Quartal");
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)), (guests.From, guests.Until));
        var september = Q("Umsatz nach Behandlung im September");
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), (september.From, september.Until));
        Assert.Equal((new DateOnly(2027, 3, 1), Today), (Q("Wie viele Nicht-erschienen hatten wir?").From, Q("Wie viele Nicht-erschienen hatten wir?").Until));
        var utilization = Q("Auslastung letzte Woche je Person und Raum");
        Assert.Equal(("both", new DateOnly(2027, 2, 22), new DateOnly(2027, 2, 28)), (utilization.Detail, utilization.From, utilization.Until));
        Assert.Equal("room", Q("Auslastung der Räume letzte Woche").Detail);
        Assert.Equal("person", Q("Auslastung je Person im Februar").Detail);

        // Aktionen schauen nach vorn: der November ist der kommende.
        var november = Q("Gibt es im November ein Angebot?");
        Assert.Equal((new DateOnly(2027, 11, 1), new DateOnly(2027, 11, 30)), (november.From, november.Until));
        Assert.Equal("packages", Q("Gilt die Aktion auch für Pakete?").Detail);

        // Schließzeiten (ADR 0073): ein Zeitraum mit Wörtern zur Schließung fragt nach den Schließzeiten, der Jänner ist der kommende; ohne solche Wörter bleibt es bei heute.
        var january = Q("Sind wir im Jänner geschlossen?");
        Assert.Equal(("range", new DateOnly(2028, 1, 1), new DateOnly(2028, 1, 31)), (january.Detail, january.From, january.Until));
        var closureWeek = Q("Gibt es nächste Woche eine Schließzeit?");
        Assert.Equal(("range", new DateOnly(2027, 3, 8), new DateOnly(2027, 3, 14)), (closureWeek.Detail, closureWeek.From, closureWeek.Until));
        Assert.Null(Q("Wie lange haben wir diese Woche offen?").Detail);
        Assert.Null(Q("Ist am Montag geschlossen?").Detail);
    }

    [Theory]
    [InlineData("historie christian steiner", BusinessTopic.GuestHistory, null)]
    [InlineData("hat sie noch einen gutschein", BusinessTopic.GuestBalance, "voucher")]
    [InlineData("offene rechnung frau moser", BusinessTopic.GuestBalance, "bill")]
    [InlineData("hat herr steiner die anamnese ausgefüllt", BusinessTopic.GuestAnamnesis, null)]
    [InlineData("wer kommt als nächstes", BusinessTopic.NextArrivals, null)]
    [InlineData("wer ist noch nicht da", BusinessTopic.NotCheckedIn, null)]
    [InlineData("wer hat heute abgesagt", BusinessTopic.CancelledToday, null)]
    [InlineData("fällt eine gebühr an", BusinessTopic.CancellationFee, null)]
    [InlineData("wer steht auf der warteliste", BusinessTopic.Waitlist, null)]
    public void Fragen_zu_Gaesten_Terminen_und_Absagen_haben_ihr_Thema(string question, BusinessTopic topic, string? detail)
    {
        var intent = BusinessIntents.Detect(question, Today);

        Assert.NotNull(intent);
        Assert.Equal((topic, detail), (intent.Topic, intent.Detail));
    }

    [Theory]
    [InlineData("Wie viel ist auf Gutschein 1234 noch drauf?")]
    [InlineData("hat sie noch etwas auf gutschein 1234")]
    public void Gutscheincode_bleibt_beim_Gutscheinstand(string question) =>
        Assert.Equal(BusinessTopic.VoucherBalance, BusinessIntents.Detect(question, Today)?.Topic);

    [Theory]
    [InlineData("Was ist unsere Stornoregel?")]
    [InlineData("Wie hoch ist die Ausfallgebühr?")]
    public void Frage_nach_der_Stornoregel_bleibt_bei_der_Regel(string question) =>
        Assert.Equal(BusinessTopic.CancellationRule, BusinessIntents.Detect(question, Today)?.Topic);

    [Theory]
    [InlineData("Wie hoch ist die Ausfallgebühr bei Nichterscheinen?")]
    [InlineData("Wie hoch ist die Stornogebühr für eine Massage?")]
    [InlineData("Ab wann fällt eine Stornogebühr an?")]
    [InlineData("Was zahlt man an Stornogebühr?")]
    [InlineData("Kostet ein Storno am selben Tag etwas?")]
    [InlineData("Zahlt der Gast bei kurzfristiger Absage eine Gebühr?")]
    [InlineData("Fällt bei Nichterscheinen eine Gebühr an?")]
    public void Gebuehr_ohne_Termin_oder_Gast_fragt_nach_der_Stornoregel(string question) =>
        Assert.Equal(BusinessTopic.CancellationRule, BusinessIntents.Detect(question, Today)?.Topic);

    [Theory]
    [InlineData("Hat Frau Moser einen offenen Termin?", BusinessTopic.GuestNextVisit)]
    [InlineData("Wann kommt Frau Huber wegen ihres Pakets?", BusinessTopic.GuestNextVisit)]
    [InlineData("Ist Raum 3 frei für den Gast mit Paket?", BusinessTopic.RoomFree)]
    [InlineData("Gibt es heute noch offene Termine für einen Gast?", BusinessTopic.RemainingAppointments)]
    [InlineData("Was kostet ein Paket mit fünf Massagen für den Gast?", BusinessTopic.TreatmentInfo)]
    public void Fragen_zu_Termin_Raum_und_Preis_sind_kein_Kassa_Stand(string question, BusinessTopic topic) =>
        Assert.Equal(topic, BusinessIntents.Detect(question, Today)?.Topic);

    [Theory]
    [InlineData("rechnung steiner", "bill")]
    [InlineData("gutschein steiner", "voucher")]
    [InlineData("Rabatt Steiner", "discount")]
    public void Kassa_Stand_mit_Namen_ohne_Anrede_wird_erkannt(string question, string detail)
    {
        var intent = BusinessIntents.Detect(question, Today);

        Assert.NotNull(intent);
        Assert.Equal((BusinessTopic.GuestBalance, detail, "steiner"), (intent.Topic, intent.Detail, intent.Subject?.Trim().ToLowerInvariant()));
    }

    [Theory]
    [InlineData("Fällt eine Gebühr an, wenn man 12 Stunden vorher absagt?")]
    [InlineData("Fällt eine Gebühr an, wenn sie jetzt absagt?")]
    public void Gebuehr_ohne_Namen_sucht_keinen_Gast_aus_Fuellwoertern(string question)
    {
        var intent = BusinessIntents.Detect(question, Today);

        Assert.NotNull(intent);
        Assert.Equal((BusinessTopic.CancellationFee, (string?)null), (intent.Topic, intent.Subject));
    }

    [Fact]
    public void Gebuehr_mit_Anrede_und_Namen_sucht_den_Gast()
    {
        var intent = BusinessIntents.Detect("Fällt bei Herrn Steiner eine Gebühr an?", Today);

        Assert.NotNull(intent);
        Assert.Equal((BusinessTopic.CancellationFee, "Steiner"), (intent.Topic, intent.Subject));
    }

    [Theory]
    [InlineData("Wer wird benachrichtigt, wenn ein Termin storniert wird?", BusinessTopic.CancelledToday)]
    [InlineData("Wer hat den Termin von Frau Moser storniert?", BusinessTopic.CancelledToday)]
    [InlineData("Wer fehlt noch im Dienstplan?", BusinessTopic.NotCheckedIn)]
    [InlineData("Wer kommt heute nachmittag zum Dienst?", BusinessTopic.NextArrivals)]
    [InlineData("Wer wartet im Ruheraum?", BusinessTopic.Waitlist)]
    [InlineData("Wann kommt der nächste Kunde von Anna?", BusinessTopic.NextArrivals)]
    [InlineData("Muss jeder Gast eine Anamnese ausfüllen?", BusinessTopic.GuestAnamnesis)]
    [InlineData("Darf die Kundin bei Allergie in die Sauna?", BusinessTopic.GuestAnamnesis)]
    [InlineData("Welche Behandlung empfehlen Sie einer Kundin mit Allergie?", BusinessTopic.GuestAnamnesis)]
    [InlineData("gutschein einlösen", BusinessTopic.GuestBalance)]
    [InlineData("rechnung stornieren", BusinessTopic.GuestBalance)]
    public void Allgemeine_Fragen_treffen_kein_Thema_zu_Gaesten_und_Ankuenften(string question, BusinessTopic notTopic) =>
        Assert.NotEqual(notTopic, BusinessIntents.Detect(question, Today)?.Topic);

    [Theory]
    [InlineData("Muss Frau Moser die Anamnese neu ausfüllen?")]
    [InlineData("Soll Herr Steiner den Fragebogen erneuern?")]
    public void Muss_mit_genanntem_Gast_fragt_nach_seinem_Fragebogen(string question) =>
        Assert.Equal(BusinessTopic.GuestAnamnesis, BusinessIntents.Detect(question, Today)?.Topic);

    [Fact]
    public void Heute_nachmittag_ist_der_Zeitraum_von_zwoelf_bis_siebzehn_Uhr()
    {
        const string Text = "Wie viele Termine haben wir heute nachmittag?";
        var question = BusinessIntents.Question(BusinessIntents.Detect(Text, Today)!, Text, Today)!;

        var window = AnswerDates.Window(question, Today, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"));

        Assert.Equal((new DateTimeOffset(2027, 3, 3, 11, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 3, 3, 16, 0, 0, TimeSpan.Zero)), window);
    }

    [Theory]
    [InlineData("heute", "2027-03-03", "2027-03-03")]
    [InlineData("gestern", "2027-03-02", "2027-03-02")]
    [InlineData("diese Woche", "2027-03-01", "2027-03-03")]
    [InlineData("letzte Woche", "2027-02-22", "2027-02-28")]
    [InlineData("nächste Woche", "2027-03-08", "2027-03-14")]
    [InlineData("diesen Monat", "2027-03-01", "2027-03-03")]
    [InlineData("letzten Monat", "2027-02-01", "2027-02-28")]
    [InlineData("dieses Quartal", "2027-01-01", "2027-03-03")]
    [InlineData("letztes Quartal", "2026-10-01", "2026-12-31")]
    [InlineData("im 1. Quartal 2026", "2026-01-01", "2026-03-31")]
    [InlineData("im 3. Quartal", "2026-07-01", "2026-09-30")]
    [InlineData("dieses Jahr", "2027-01-01", "2027-03-03")]
    [InlineData("letztes Jahr", "2026-01-01", "2026-12-31")]
    [InlineData("im September", "2026-09-01", "2026-09-30")]
    [InlineData("im Februar", "2027-02-01", "2027-02-28")]
    [InlineData("im März", "2027-03-01", "2027-03-03")]
    [InlineData("im Dezember 2025", "2025-12-01", "2025-12-31")]
    [InlineData("seit 1.10.", "2026-10-01", "2027-03-03")]
    [InlineData("seit 15.2.2027", "2027-02-15", "2027-03-03")]
    [InlineData("von 01.02.2027 bis 14.02.2027", "2027-02-01", "2027-02-14")]
    [InlineData("in den letzten 30 Tagen", "2027-02-02", "2027-03-03")]
    public void Zeitraeume_der_Auswertungen_enden_heute_und_werden_in_Datumsgrenzen_uebersetzt(string text, string from, string until)
    {
        var range = AnswerDates.Range(text, Today, toToday: true, future: false);
        Assert.NotNull(range);
        Assert.Equal((D(from), D(until)), (range.Value.From, range.Value.Until));
    }

    [Theory]
    [InlineData("im November", "2027-11-01", "2027-11-30")]
    [InlineData("im Februar", "2028-02-01", "2028-02-29")]
    [InlineData("im März", "2027-03-01", "2027-03-31")]
    [InlineData("diesen Monat", "2027-03-01", "2027-03-31")]
    [InlineData("nächste Woche", "2027-03-08", "2027-03-14")]
    public void Zeitraeume_der_Plaene_und_Aktionen_reichen_bis_zum_Ende(string text, string from, string until)
    {
        var range = AnswerDates.Range(text, Today, toToday: false, future: true);
        Assert.NotNull(range);
        Assert.Equal((D(from), D(until)), (range.Value.From, range.Value.Until));
    }

    [Theory]
    [InlineData("am Samstag", "2027-03-06")]
    [InlineData("am Mittwoch", "2027-03-03")]
    [InlineData("nächsten Mittwoch", "2027-03-10")]
    [InlineData("übermorgen", "2027-03-05")]
    [InlineData("am 1.11.", "2027-11-01")]
    [InlineData("am 1.3.", "2028-03-01")]
    [InlineData("am 1. November 2028", "2028-11-01")]
    public void Einzelne_Tage_werden_erkannt(string text, string expected) =>
        Assert.Equal(D(expected), AnswerDates.Day(text, Today));

    [Fact]
    public void Zeitraeume_werden_in_Worten_genannt()
    {
        Assert.Equal("September 2026", AnswerDates.Describe(new(2026, 9, 1), new(2026, 9, 30)));
        Assert.Equal("3. Quartal 2026", AnswerDates.Describe(new(2026, 7, 1), new(2026, 9, 30)));
        Assert.Equal("am 03.03.2027", AnswerDates.Describe(new(2027, 3, 3), new(2027, 3, 3)));
        Assert.Equal("01.10.2026 bis 22.10.2026", AnswerDates.Describe(new(2026, 10, 1), new(2026, 10, 22)));
        Assert.Equal("Mittwoch, 03.03.2027", AnswerDates.Long(Today));
    }

    [Fact]
    public async Task Die_Regeln_gelten_auch_im_regelbasierten_Klassifikator()
    {
        var intent = await new RuleBasedAssistantClassifier().ClassifyAsync("Wer arbeitet morgen?", new DateTimeOffset(2027, 3, 3, 9, 0, 0, TimeSpan.Zero), "Europe/Vienna", TestContext.Current.CancellationToken);
        Assert.Equal((AssistantTask.Business, BusinessTopic.WhoWorks, new DateOnly(2027, 3, 4)), (intent.Task, intent.Topic!.Value, intent.From!.Value));
    }

    [Theory]
    [InlineData("Thai-Massage", "Wer bietet Thai-Massage an?", true)]
    [InlineData("Massage", "Wer bietet Thai-Massage an?", false)]
    [InlineData("Massage", "Was kostet eine Massage?", true)]
    [InlineData("Massage", "Wer macht Massagen?", true)]
    [InlineData("Thai Massage", "Was kostet Thai-Massage?", true)]
    [InlineData("Hot Stone", "Dauer von hot-stone?", true)]
    [InlineData("Gesichtsbehandlung", "Was kostet die Gesichtsbehandlung bei uns", true)]
    [InlineData("Öl", "Was kostet Öl?", false)]
    public void Namen_aus_dem_Katalog_werden_im_Satz_gefunden(string name, string text, bool expected) =>
        Assert.Equal(expected, NameMatch.Mentions(text, name));
}
