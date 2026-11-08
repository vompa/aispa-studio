using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Assistant.Answers;

/// <summary>
/// Erkennt Betriebsfragen ohne KI, an Schlüsselwörtern und Mustern (ADR 0072). Deterministisch und billig: trifft eine Regel, braucht es kein Modell, und
/// mit und ohne API-Schlüssel kommt dieselbe Antwort. Was keine Regel trifft, ordnet (wenn vorhanden) das Sprachmodell ein, mit demselben Ergebnis in Bausteinen
/// (<see cref="BusinessQuestion"/>). Fragen nach der Bedienung („Wie mache ich …?“) gehören dem Handbuch und kommen hier nie vor.
/// </summary>
internal static partial class BusinessIntents
{
    public static AssistantIntent? Detect(string question, DateOnly today)
    {
        var text = question.ToLowerInvariant();
        // „Was muss bestellt werden?“ beginnt wie eine Frage zur Bedienung („Was muss ich tun?“), fragt aber nach dem Lager.
        if ((RuleBasedAssistantClassifier.IsHowTo(text) && !WhatToOrderWords().IsMatch(text)) || TopicOf(text) is not { } topic)
        {
            return null;
        }

        var q = Build(topic, question, text, today);
        return new AssistantIntent(AssistantTask.Business, q.From, q.Until, null, null, q.Topic, q.Subject, q.Detail);
    }

    /// <summary>Die Betriebsfrage aus einer Absicht, die das Modell oder die Regeln geliefert haben; was das Modell offen lässt, ergänzen die Regeln.</summary>
    public static BusinessQuestion? Question(AssistantIntent intent, string question, DateOnly today)
    {
        if (intent.Task != AssistantTask.Business || intent.Topic is not { } topic)
        {
            return null;
        }

        var rules = Build(topic, question, question.ToLowerInvariant(), today);
        return rules with
        {
            Subject = intent.Subject ?? rules.Subject,
            From = intent.From ?? rules.From,
            Until = intent.Until ?? intent.From ?? rules.Until,
            Detail = intent.Detail ?? rules.Detail,
        };
    }

    private static BusinessTopic? TopicOf(string t)
    {
        if (Voucher().IsMatch(t))
        {
            return BusinessTopic.VoucherBalance;
        }

        // Ein Gutscheincode in der Frage meint diesen Gutschein, auch mit Bezug auf den Gast („hat sie noch etwas auf gutschein 1234“).
        var code = VoucherCode(t);
        if (code is not null && Guest(t))
        {
            return BusinessTopic.VoucherBalance;
        }

        if (StaffRoomStockTopic(t) is { } staffRoomStock)
        {
            return staffRoomStock;
        }

        if (WaitlistWords().IsMatch(t))
        {
            return BusinessTopic.Waitlist;
        }

        if (CancelledTodayWords().IsMatch(t) && !CountWords().IsMatch(t))
        {
            return BusinessTopic.CancelledToday;
        }

        // „Wie hoch ist die Ausfallgebühr bei Nichterscheinen?“ fragt nach der Regel; „fällt eine Gebühr an, wenn sie jetzt absagt“ nach dem Termin.
        if (CancellationFeeWords().IsMatch(t))
        {
            return FeeGeneralWords().IsMatch(t) && !FeeReference(t) ? BusinessTopic.CancellationRule : BusinessTopic.CancellationFee;
        }

        if (NotCheckedInWords().IsMatch(t) && !TeamWords().IsMatch(t))
        {
            return BusinessTopic.NotCheckedIn;
        }

        if (NextArrivalWords().IsMatch(t) && !NamedGuest().IsMatch(t) && !GuestReference().IsMatch(t) && !TeamWords().IsMatch(t) && !OfPerson().IsMatch(t))
        {
            return BusinessTopic.NextArrivals;
        }

        if (((AnamnesisWords().IsMatch(t) && Guest(t)) || (AnamnesisNouns().IsMatch(t) && ShortWithName(t))) && (!GeneralGuestWords().IsMatch(t) || NamedGuest().IsMatch(t)))
        {
            return BusinessTopic.GuestAnamnesis;
        }

        if (code is null && IsBalanceQuestion(t))
        {
            return BusinessTopic.GuestBalance;
        }

        if (BirthdayWords().IsMatch(t))
        {
            return BusinessTopic.Birthdays;
        }

        if (IsContactQuestion(t))
        {
            return BusinessTopic.BusinessContacts;
        }

        if (HolidayWords().IsMatch(t))
        {
            return BusinessTopic.Holiday;
        }

        if (SchoolHolidayWords().IsMatch(t))
        {
            return BusinessTopic.SchoolHolidays;
        }

        if (DirectionsWords().IsMatch(t))
        {
            return BusinessTopic.Directions;
        }

        if (CancellationWords().IsMatch(t))
        {
            return BusinessTopic.CancellationRule;
        }

        if (PromotionWords().IsMatch(t))
        {
            return BusinessTopic.Promotions;
        }

        if (NoShowWords().IsMatch(t))
        {
            return BusinessTopic.NoShows;
        }

        if (TopTherapistWords().IsMatch(t))
        {
            return BusinessTopic.TopTherapist;
        }

        if (TopGuestWords().IsMatch(t))
        {
            return BusinessTopic.TopGuests;
        }

        if (TopTreatmentWords().IsMatch(t))
        {
            return BusinessTopic.TopTreatments;
        }

        if (RevenueByTreatmentWords().IsMatch(t))
        {
            return BusinessTopic.RevenueByTreatment;
        }

        if (UtilizationWords().IsMatch(t))
        {
            return BusinessTopic.Utilization;
        }

        if (WhoWorksWords().IsMatch(t))
        {
            return BusinessTopic.WhoWorks;
        }

        if (PersonName(t, t) is not null && PersonWords().IsMatch(t))
        {
            return BusinessTopic.PersonAvailable;
        }

        if (DayLoadWords().IsMatch(t))
        {
            return BusinessTopic.DayLoad;
        }

        if (RemainingWords().IsMatch(t))
        {
            return BusinessTopic.RemainingAppointments;
        }

        if (AreaWords().IsMatch(t))
        {
            return BusinessTopic.AreaLoad;
        }

        if (RoomWords().IsMatch(t))
        {
            return BusinessTopic.RoomFree;
        }

        if (OpeningWords().IsMatch(t) && !NotOpening().IsMatch(t))
        {
            return BusinessTopic.OpeningHours;
        }

        if (ContactWords().IsMatch(t) && (OwnWords().IsMatch(t) || !GuestTitles().IsMatch(t)))
        {
            return BusinessTopic.Contact;
        }

        // Ein Gast steht in der Frage als Anrede („Herr“, „Hr.“) oder als Bezug („sie“, „der Gast“) auf den offenen Termin oder die Kundenkarte (Ä315).
        var guest = Guest(t);
        if (HistoryWords().IsMatch(t) && !NotHistory().IsMatch(t) && (guest || (HistoryNouns().IsMatch(t) && ShortWithName(t))))
        {
            return BusinessTopic.GuestHistory;
        }

        if (GuestNextWords().IsMatch(t) && guest)
        {
            return BusinessTopic.GuestNextVisit;
        }

        if (GuestLastWords().IsMatch(t) && guest)
        {
            return BusinessTopic.GuestLastVisit;
        }

        // „wann kommt steiner“, „termin herr steinr“: so, wie die Rezeption tippt.
        if ((GuestComes().IsMatch(t) || (TerminWord().IsMatch(t) && GuestTitles().IsMatch(t))) && GuestName(t) is not null)
        {
            return BusinessTopic.GuestNextVisit;
        }

        if (WhoOffersWords().IsMatch(t))
        {
            return BusinessTopic.WhoOffers;
        }

        return TreatmentInfoWords().IsMatch(t) ? BusinessTopic.TreatmentInfo : null;
    }

    /// <summary>
    /// Kassa-Stand eines Gastes: Rechnung, Anzahlung, Rabatt, Guthaben oder „bezahlt“ zählen immer; Gutschein und Paket nur, wenn die Frage nicht nach Termin, Raum oder
    /// Preis fragt („wann kommt Frau Huber wegen ihres Pakets“). Dazu ein Gast mit Anrede oder Bezug oder ein kurzer Name („rechnung steiner“).
    /// </summary>
    private static bool IsBalanceQuestion(string t) =>
        (BalanceWords().IsMatch(t) || (BalanceProductWords().IsMatch(t) && !NotBalance().IsMatch(t))) && (Guest(t) || ShortWithName(t));

    /// <summary>Die Gebühr meint einen bestimmten Termin: der Gast mit Anrede oder Bezug („sie“, „er“), „den Termin“ oder „jetzt“.</summary>
    private static bool FeeReference(string t) => NamedGuest().IsMatch(t) || GuestReference().IsMatch(t) || ThisAppointment().IsMatch(t);

    /// <summary>Den Gast der Gebühr nennt die Frage mit Anrede („Herrn Steiner“) oder als kurze Eingabe („gebühr steiner“); andere Wörter eines Satzes sind kein Name.</summary>
    private static string? FeeGuestName(string question, string t) =>
        NamedGuest().IsMatch(t) || ShortWithName(t) ? GuestName(StripTopicWords(question)) : null;

    /// <summary>Ein Gast steht in der Frage als Anrede („Herr“, „Hr.“, „der Gast“) oder als Bezug („sie“, „er“) auf den offenen Termin oder die Kundenkarte (Ä315).</summary>
    private static bool Guest(string t) => GuestTitles().IsMatch(t) || (GuestReference().IsMatch(t) && !FreeWord().IsMatch(t));

    /// <summary>
    /// Kurze Eingabe wie an der Rezeption getippt, „historie christian steiner“: höchstens vier Wörter, davon mindestens eines ein Name. Längere Sätze ohne Anrede
    /// („wie lange gilt eine anamnese“) meinen keinen bestimmten Gast.
    /// </summary>
    private static bool ShortWithName(string t) => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4 && GuestName(StripTopicWords(t)) is not null;

    /// <summary>
    /// Verlauf, Kassa-Stand oder Fragebogen eines Gastes, den die Frage nur als Wort ohne Anrede und ohne Bezug nennt („rechnung steiner“). Ob das Wort ein Name ist oder eine
    /// Frage zur Bedienung („rechnung splitten“), entscheidet erst der Kundenstamm: gibt es keinen solchen Gast, gilt die Frage als von den Regeln nicht erkannt.
    /// </summary>
    internal static bool NamesGuestOnlyByWord(AssistantIntent intent, string question) =>
        intent is { Task: AssistantTask.Business, Topic: BusinessTopic.GuestBalance or BusinessTopic.GuestHistory or BusinessTopic.GuestAnamnesis, Subject: not null }
        && !Guest(question.ToLowerInvariant());

    /// <summary>Die Wörter der Themen zu Gästen (Ä317) aus der Frage nehmen, damit nur der Name bleibt („offene rechnung herr steiner“ → „herr steiner“).</summary>
    private static string StripTopicWords(string question) => TopicNoise().Replace(question, " ");

    private static BusinessQuestion Build(BusinessTopic topic, string question, string t, DateOnly today) =>
        BuildTopic(topic, question, t, today) with { Part = AnswerDates.Part(t) };

    /// <summary>Was die Frage zu Gutschein, Paket, Rechnung, Anzahlung oder Rabatt wissen will; nennt sie mehrere oder keines, <c>null</c> (dann alles).</summary>
    private static string? BalanceDetail(string t)
    {
        string[] found =
        [
            .. Regex.IsMatch(t, @"gutschein|paket|guthaben") ? ["voucher"] : Array.Empty<string>(),
            .. Regex.IsMatch(t, @"rechnung|offene[nrs]?\b|bezahlt|noch (etwas|was) offen") ? ["bill"] : Array.Empty<string>(),
            .. Regex.IsMatch(t, @"anzahlung") ? ["deposit"] : Array.Empty<string>(),
            .. Regex.IsMatch(t, @"rabatt") ? ["discount"] : Array.Empty<string>(),
        ];
        return found.Length == 1 ? found[0] : null;
    }

    private static BusinessQuestion BuildTopic(BusinessTopic topic, string question, string t, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        (DateOnly From, DateOnly Until) Report() => AnswerDates.Range(t, today, toToday: true, future: false) ?? (monthStart, today);
        var day = AnswerDates.Day(t, today);

        switch (topic)
        {
            case BusinessTopic.OpeningHours:
                var openingRange = day is null ? AnswerDates.Range(t, today, toToday: false, future: true) : null;
                // „Sind wir im Jänner geschlossen?“: ein Zeitraum mit Wörtern zur Schließung fragt nach den Schließzeiten (ADR 0073).
                if (openingRange is { } closedRange && ClosureWords().IsMatch(t))
                {
                    return new(topic, question, null, closedRange.From, closedRange.Until, "range");
                }

                var wholeWeek = day is null && openingRange is null && OpeningHoursNoun().IsMatch(t);
                var hoursDay = day ?? today;
                return new(topic, question, null, hoursDay, hoursDay, wholeWeek ? "week" : null);
            case BusinessTopic.Holiday:
            case BusinessTopic.SchoolHolidays:
                // Bei Ferien nennt der Name („Herbstferien“) die gesuchten Ferien; „Ferien“ und „Schulferien“ allein meinen alle.
                var subject = topic == BusinessTopic.SchoolHolidays && NamedHolidays().Match(t) is { Success: true } named && named.Value is not ("ferien" or "schulferien")
                    ? named.Value
                    : null;
                if (day is { } holidayDay)
                {
                    return new(topic, question, subject, holidayDay, holidayDay, "date");
                }

                if (Regex.Match(t, @"\b(20\d{2})\b") is { Success: true } year)
                {
                    var y = int.Parse(year.Groups[1].Value, CultureInfo.InvariantCulture);
                    return new(topic, question, subject, new DateOnly(y, 1, 1), new DateOnly(y, 12, 31), "year");
                }

                return AnswerDates.Range(t, today, toToday: false, future: true) is { } holidayRange
                    ? new(topic, question, subject, holidayRange.From, holidayRange.Until, "range")
                    : new(topic, question, subject, today, null, "next");
            case BusinessTopic.Contact:
                return new(topic, question, null, null, null, Regex.IsMatch(t, @"telefon|rufnummer|anrufen|nummer") ? "phone"
                    : Regex.IsMatch(t, @"mail") ? "mail" : Regex.IsMatch(t, @"adresse|anschrift|straße|wo sind wir") ? "address" : null);
            case BusinessTopic.WhoWorks:
                {
                    var range = AnswerDates.Range(t, today, toToday: false, future: true);
                    var first = range?.From ?? day ?? today;
                    return new(topic, question, null, first, range?.Until ?? first);
                }

            case BusinessTopic.PersonAvailable:
                {
                    var range = AnswerDates.Range(t, today, toToday: false, future: true);
                    var first = range?.From ?? day ?? today;
                    return new(topic, question, PersonName(question, t), first, range?.Until ?? first);
                }

            case BusinessTopic.RemainingAppointments:
                var counted = day ?? today;
                return new(topic, question, null, counted, counted, counted == today && t.Contains("noch", StringComparison.Ordinal) ? "remaining" : null);
            case BusinessTopic.DayLoad:
                var loaded = day ?? today;
                return new(topic, question, null, loaded, loaded);
            case BusinessTopic.AreaLoad:
                return new(topic, question, AreaWords().Match(t).Groups["a"].Value, day ?? today, day ?? today);
            case BusinessTopic.GuestLastVisit:
                return new(topic, question, GuestName(question), null, null);
            case BusinessTopic.GuestNextVisit:
                {
                    var range = AnswerDates.Range(t, today, toToday: false, future: true);
                    var first = range?.From ?? day ?? today;
                    // Ohne Tag oder Zeitraum („wann hat er seinen Termin“) ist der nächste Termin gemeint, ohne kommenden der letzte Besuch.
                    return new(topic, question, GuestName(question), first, range?.Until ?? first, range is null && day is null ? "next" : null);
                }

            case BusinessTopic.VoucherBalance:
                return new(topic, question, VoucherCode(t), null, null);
            case BusinessTopic.GuestHistory:
            case BusinessTopic.GuestAnamnesis:
                return new(topic, question, GuestName(StripTopicWords(question)), null, null);
            case BusinessTopic.CancellationFee:
                return new(topic, question, FeeGuestName(question, t), null, null);
            case BusinessTopic.GuestBalance:
                return new(topic, question, GuestName(StripTopicWords(question)), null, null, BalanceDetail(t));
            case BusinessTopic.NextArrivals:
            case BusinessTopic.NotCheckedIn:
            case BusinessTopic.CancelledToday:
                return new(topic, question, null, day ?? today, day ?? today);
            case BusinessTopic.Waitlist:
                return new(topic, question, null, day, day);
            case BusinessTopic.PersonBreak:
                return new(topic, question, BreakPerson(question, t), day ?? today, day ?? today);
            case BusinessTopic.DutyHolders:
                return new(topic, question, null, day ?? today, day ?? today, DutyDetail(t));
            case BusinessTopic.RoomFreeTimes:
                {
                    // „Ist der Hamam gesperrt?“ ohne Tag fragt nach den nächsten zwei Monaten; „wann ist der Paarraum frei“ ohne Tag nach heute.
                    var closure = RoomClosureWords().IsMatch(t);
                    var range = AnswerDates.Range(t, today, toToday: false, future: true);
                    var first = range?.From ?? day ?? today;
                    return new(topic, question, question, first, range?.Until ?? (closure && day is null ? today.AddDays(60) : first), closure ? "closure" : "free");
                }
            case BusinessTopic.Birthdays:
                {
                    var range = AnswerDates.Range(t, today, toToday: false, future: true);
                    var first = range?.From ?? day ?? today;
                    return new(topic, question, null, first, range?.Until ?? first);
                }

            case BusinessTopic.BusinessContacts:
                // Die Kategorie, wenn die Frage genau eine nennt; gesucht wird in der ganzen Frage (Bezeichnung, Firma, Ansprechperson, Zuständigkeit).
                return new(topic, question, question, null, null, ContactCategory(t));
            case BusinessTopic.Promotions:
                {
                    var range = AnswerDates.Range(t, today, toToday: false, future: true);
                    var first = range?.From ?? day ?? today;
                    return new(topic, question, null, first, range?.Until ?? first, t.Contains("paket", StringComparison.Ordinal) ? "packages" : null);
                }

            case BusinessTopic.TopGuests:
                {
                    var (from, until) = Report();
                    return new(topic, question, null, from, until, Regex.IsMatch(t, @"ausgab|ausgegeben|umsatz|bezahlt|geld") ? "spending" : null);
                }

            case BusinessTopic.Utilization:
                {
                    var (from, until) = Report();
                    var rooms = Regex.IsMatch(t, @"raum|räume|räumen");
                    var people = Regex.IsMatch(t, @"person|mitarbeit|therapeut|team|personal");
                    return new(topic, question, null, from, until, rooms && !people ? "room" : people && !rooms ? "person" : "both");
                }

            case BusinessTopic.TopTreatments:
            case BusinessTopic.TopTherapist:
            case BusinessTopic.RevenueByTreatment:
            case BusinessTopic.NoShows:
                {
                    var (from, until) = Report();
                    return new(topic, question, null, from, until);
                }

            default:
                // Preis, Dauer und Angebot: den Namen der Behandlung sucht der Antwortgeber im ganzen Satz.
                return new(topic, question, question, null, null);
        }
    }

    /// <summary>
    /// Frage nach einem Kontakt des Betriebs (ADR 0074): eine Kategorie („Versicherung“, „Installateur“, „Lohnverrechnung“) mit einer Frage nach Person oder Nummer, die Frage
    /// nach der Zuständigkeit („Wer ist für die Heizung zuständig?“, „Wer kümmert sich um …“, „Wen rufe ich bei … an?“) oder „Nummer von …“ (nicht von Gästen, Belegen, dem Spa).
    /// </summary>
    private static bool IsContactQuestion(string t) =>
        (ContactCategory(t) is not null && ContactAsk().IsMatch(t))
        || ResponsibleWords().IsMatch(t)
        || (NumberOfWords().IsMatch(t) && !OwnWords().IsMatch(t) && !GuestTitles().IsMatch(t));

    /// <summary>
    /// Die Kategorie der Betriebsinformationen, die die Frage nennt, als Name von <see cref="BusinessContactCategory"/>; <c>null</c>, wenn sie keine oder mehrere nennt
    /// (dann entscheiden die Wörter der Frage).
    /// </summary>
    internal static string? ContactCategory(string t)
    {
        var found = new List<BusinessContactCategory>();
        void Add(Regex pattern, BusinessContactCategory category)
        {
            if (pattern.IsMatch(t))
            {
                found.Add(category);
            }
        }

        Add(AuthorityContactWords(), BusinessContactCategory.Authority);
        Add(InsuranceContactWords(), BusinessContactCategory.Insurance);
        Add(PayrollContactWords(), BusinessContactCategory.Payroll);
        Add(TaxContactWords(), BusinessContactCategory.TaxAdvisor);
        Add(AccountingContactWords(), BusinessContactCategory.Accounting);
        Add(TrustedContactWords(), BusinessContactCategory.TrustedPerson);
        Add(EmergencyContactWords(), BusinessContactCategory.Emergency);
        Add(SupplierContactWords(), BusinessContactCategory.Supplier);
        Add(CraftsmanContactWords(), BusinessContactCategory.Craftsman);
        return found.Count == 1 ? found[0].ToString() : null;
    }

    /// <summary>
    /// Name eines Gastes ohne Anrede, Titel und Füllwörter, in jeder Schreibung („wann hat herr steiner seinen termin“ → „steiner“); höchstens zwei Wörter
    /// (<see cref="NameMatch.ParseName"/>). <c>null</c>, wenn die Frage keinen Namen nennt („wann war sie zuletzt da“).
    /// </summary>
    internal static string? GuestName(string question) =>
        NameMatch.ParseName(question) is { IsEmpty: false } name ? string.Join(' ', name.Words) : null;

    /// <summary>Der Vorname einer Person aus dem Team in Fragen wie „Ist Lena nächste Woche da?“; <c>null</c>, wenn kein Name im Satz steht.</summary>
    internal static string? PersonName(string question, string lower)
    {
        foreach (Match match in PersonLead().Matches(lower))
        {
            var word = question.Substring(match.Groups["n"].Index, match.Groups["n"].Length);
            if (!Stop.Contains(word.ToLowerInvariant()) && !PersonStop.Contains(word.ToLowerInvariant()))
            {
                return word;
            }
        }

        return null;
    }

    private static string? VoucherCode(string lower) =>
        VoucherNumber().Match(lower) is { Success: true } match ? match.Groups["c"].Value.ToUpperInvariant() : null;

    /// <summary>Füllwörter in Fragen, die nie ein Name sind; auch die Namenssuche (<see cref="NameMatch.ParseName"/>) nimmt sie heraus.</summary>
    internal static readonly HashSet<string> Stop =
    [
        "wann", "war", "wer", "wie", "was", "wo", "ist", "hat", "hatte", "haben", "hatten", "kommt", "kam", "frau", "herr", "herrn", "kunde", "kundin", "kunden",
        "gast", "gäste", "zuletzt", "letzte", "letzten", "letzter", "nächste", "nächsten", "nächster", "woche", "monat", "jahr", "tag", "termin", "termine", "besuch",
        "heute", "morgen", "gestern", "bei", "uns", "da", "hier", "noch", "wieder", "einen", "eine", "ein", "der", "die", "das", "dem", "den", "und", "oder", "auch",
        "massage", "behandlung", "anwendung", "familie", "dr", "dr.", "diese", "dieser", "diesen", "kommende", "kommenden", "wochenende", "montag", "dienstag",
        "mittwoch", "donnerstag", "freitag", "samstag", "sonntag", "januar", "februar", "märz", "april", "mai", "juni", "juli", "august", "september", "oktober",
        "november", "dezember", "im", "am", "um", "zu", "von", "vom", "für", "mit", "nicht", "kein", "keinen", "wohl", "schon", "denn",
    ];

    private static readonly HashSet<string> PersonStop =
    [
        "es", "alles", "etwas", "jemand", "niemand", "man", "wir", "ich", "sie", "er", "ihr", "uns", "dir", "mir", "sich", "spa", "standort", "team", "heute", "morgen",
        "übermorgen", "gestern", "nächste", "nächsten", "diese", "dieser", "am", "im", "in", "um", "bei", "mit", "von", "zu", "was", "wann", "wer", "wie", "wo",
    ];

    [GeneratedRegex(@"\b(?:ist|war|hat|hatte|arbeitet|kommt|wird)\s+(?<n>[\p{L}'-]{2,})\b")]
    private static partial Regex PersonLead();

    [GeneratedRegex(@"\b(da|anwesend|im dienst|dienst|im urlaub|urlaub|krank|im haus|im krankenstand|abwesend|im spa|arbeitet|arbeiten|eingeteilt)\b")]
    private static partial Regex PersonWords();

    [GeneratedRegex(@"gutschein\s+(?:(?:nr|nummer|code)\.?\s*)?(?<c>[a-z0-9-]*\d[a-z0-9-]*)")]
    private static partial Regex VoucherNumber();

    [GeneratedRegex(@"^(?=.*gutschein)(?=.*(drauf|restwert|guthaben|wie ?viel|übrig|saldo|offen))")]
    private static partial Regex Voucher();

    [GeneratedRegex(@"geburtstag")]
    private static partial Regex BirthdayWords();

    [GeneratedRegex(@"warteliste\w*|\bwer wartet\b(?!\s+(im|in|vor|an|am|bei|beim)\b)")]
    private static partial Regex WaitlistWords();

    /// <summary>„Wer hat heute abgesagt?“, „Absagen heute“; die Zahl der Absagen („wie viele …“) gehört zu den Nichterscheinen und Stornos.</summary>
    [GeneratedRegex(@"\bwer (hat|haben)\b(\s+(heute|gestern|schon|alles|denn|noch|bisher|so))*\s+(abgesagt|storniert)\b|\b(absagen|stornos|stornierungen)\b.*\bheute\b|\bheute\b.*\b(abgesagt|storniert)\b")]
    private static partial Regex CancelledTodayWords();

    [GeneratedRegex(@"wie ?viele|anzahl|bis wann")]
    private static partial Regex CountWords();

    /// <summary>„Fällt eine Gebühr an?“, „Kostet die Absage etwas?“, „Ausfallgebühr für den Termin“: für einen bestimmten Termin, nicht die Regel an sich.</summary>
    [GeneratedRegex(@"\b(fällt|fallen|fiele|wäre|würde|kostet|kosten|zahlt|zahlen)\b.*gebühr|\bkostet\b.*\b(absage|absagen|storno|stornieren|stornierung)\b|\b(ausfall|storno)gebühr\w* (für|bei|von)\b")]
    private static partial Regex CancellationFeeWords();

    /// <summary>Wörter einer allgemeinen Frage nach der Stornoregel: Höhe, ab wann, „zahlt man“, Nichterscheinen, kurzfristig, eine Behandlung an sich.</summary>
    [GeneratedRegex(@"\bwie hoch\b|\bab wann\b|\bzahlt man\b|nicht ?erschein\w*|no-?shows?|kurzfristig\w*|\b(selben|gleichen) tag\b|\bfür (eine|einen|ein)\b|\bgrundsätzlich\b|\ballgemein\b")]
    private static partial Regex FeeGeneralWords();

    [GeneratedRegex(@"\b(den|dem|diese[nmr]?|ihre[nmr]?|seine[nmr]?) termin\b|\bjetzt\b")]
    private static partial Regex ThisAppointment();

    /// <summary>Fragen zum Team und Dienstplan, nicht zu Gästen („wer fehlt noch im Dienstplan“, „wer kommt heute zum Dienst“).</summary>
    [GeneratedRegex(@"\bdienst\w*|\bschicht\w*|\bteam\b|\bmitarbeiter\w*|\bpersonal\b|\bkolleg\w*")]
    private static partial Regex TeamWords();

    /// <summary>„der nächste Kunde von Anna“: die Gäste einer Person, nicht die nächsten Ankünfte im Standort.</summary>
    [GeneratedRegex(@"\b(von|bei)\s+(?!uns\b|heute\b|jetzt\b)\p{L}{2,}")]
    private static partial Regex OfPerson();

    /// <summary>Allgemeine Fragen mit „Gast“ oder „Kundin“: „muss jeder Gast …“, „darf die Kundin …“, „welche Behandlung empfehlen Sie einer Kundin …“.</summary>
    [GeneratedRegex(@"\b(jede[rsnm]?|alle|einer|einem|muss|müssen|darf|dürfen|soll|sollte|sollen|empfehl\w*|geeignet)\b")]
    private static partial Regex GeneralGuestWords();

    [GeneratedRegex(@"nicht eingecheckt|\bwer\b.*\b(noch nicht (da|hier|gekommen|eingetroffen)|fehlt noch|ist zu spät|kommt zu spät|ist spät dran)\b")]
    private static partial Regex NotCheckedInWords();

    [GeneratedRegex(@"\bwer kommt (als )?(nächste[snr]?|gleich|jetzt)\b|\bnächste[rns]? (gast|gäste|kunde|kundin|kunden|ankunft)\b|\bwer kommt\b.*\b(vormittags?|nachmittags?|abends?)\b")]
    private static partial Regex NextArrivalWords();

    /// <summary>Ein bestimmter Gast mit Anrede; „der nächste Gast“ ist keiner.</summary>
    [GeneratedRegex(@"\b(frau|herr|herrn|familie)\b|\b(hr|fr)\.")]
    private static partial Regex NamedGuest();

    [GeneratedRegex(@"\b(anamnese\w*|fragebogen|fragebögen|gesundheitsfragebogen|allergi\w*)")]
    private static partial Regex AnamnesisWords();

    [GeneratedRegex(@"\b(anamnese\w*|fragebogen|fragebögen|gesundheitsfragebogen)\b")]
    private static partial Regex AnamnesisNouns();

    [GeneratedRegex(@"\b(rechnung\w*|anzahlung\w*|rabatt\w*|guthaben|bezahlt)\b|noch (etwas|was) offen")]
    private static partial Regex BalanceWords();

    [GeneratedRegex(@"\b(gutschein\w*|paket\w*)\b")]
    private static partial Regex BalanceProductWords();

    /// <summary>Fragen zu Termin, Raum oder Preis, auch wenn ein Gutschein oder Paket darin vorkommt.</summary>
    [GeneratedRegex(@"\btermin\w*|\braum\b|\bräume\w*|\bkommt\b|\bkommen\b|\bkostet\b|\bkosten\b|\bpreis\w*|\bfrei\b")]
    private static partial Regex NotBalance();

    [GeneratedRegex(@"\b(historie|verlauf|besuchs?historie|kundenhistorie|besuche)\b|\bwie oft\b.*\b(da|hier|bei uns|gekommen)\b|\bwar\b.*\bschon\b.*\b(da|hier|bei uns)\b")]
    private static partial Regex HistoryWords();

    [GeneratedRegex(@"\b(historie|verlauf|besuchs?historie|kundenhistorie)\b")]
    private static partial Regex HistoryNouns();

    /// <summary>Verlauf von etwas anderem als einem Gast: Dienstplan, Zugriffe, Änderungen, Kassa, Belege, Kalender, ein Termin, eine Behandlung.</summary>
    [GeneratedRegex(@"dienstplan|zugang|zugriff|änderung|protokoll|kassa|beleg|kalender|\btermins\b|behandlung|chat|nachricht")]
    private static partial Regex NotHistory();

    /// <summary>Wörter der Themen zu Gästen (Ä317), die nie ein Name sind.</summary>
    [GeneratedRegex(@"\b(historie|verlauf|besuchs?historie|kundenhistorie|besuche|besuchs?|oft|anamnese\w*|fragebogen|fragebögen|gesundheitsfragebogen|ausgefüllt|allergi\w*|gutschein\w*|paket\w*|rechnung\w*|anzahlung\w*|rabatt\w*|kundenrabatt|guthaben|offen\w*|bezahlt|welche\w*|gebühr\w*|ausfallgebühr\w*|stornogebühr\w*|fällt|fallen|kostet|absage|storno\w*|an|etwas|was|wäre|zeig\w*|gekommen|wenn|man|jetzt|kaufen|verkaufen|einlösen|buchen|anlegen|erstellen|drucken|stornieren|ausstellen|bezahlen|zahlen|teilen|aufteilen|verschenken|anzeigen|öffnen|suchen|finden|ändern|löschen|eintragen|übertragen|verrechnen|retournieren)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TopicNoise();

    [GeneratedRegex(@"\b(behörde\w*|finanzamt\w*|magistrat\w*|arbeitsinspektorat\w*|sozialversicherung\w*|bezirkshauptmannschaft\w*|ögk|svs)\b")]
    private static partial Regex AuthorityContactWords();

    [GeneratedRegex(@"\b(?<!sozial)(versicherung\w*|versicherer\w*|polizze\w*|schadenshotline\w*|haftpflicht\w*)")]
    private static partial Regex InsuranceContactWords();

    [GeneratedRegex(@"\b(lohnverrechn\w*|lohnbuchhalt\w*|lohnabrechnung\w*|personalverrechn\w*|gehaltsverrechn\w*)")]
    private static partial Regex PayrollContactWords();

    [GeneratedRegex(@"\b(steuerberat\w*|wirtschaftstreuhänd\w*)")]
    private static partial Regex TaxContactWords();

    [GeneratedRegex(@"\b(buchhaltung\w*|buchhalter\w*)")]
    private static partial Regex AccountingContactWords();

    [GeneratedRegex(@"\bvertrauensperson\w*")]
    private static partial Regex TrustedContactWords();

    [GeneratedRegex(@"\b(notfall\w*|notruf\w*|notdienst\w*|störungsdienst\w*|bereitschaftsdienst\w*)")]
    private static partial Regex EmergencyContactWords();

    [GeneratedRegex(@"\blieferant\w*")]
    private static partial Regex SupplierContactWords();

    [GeneratedRegex(@"\b(installateur\w*|klempner\w*|elektriker\w*|handwerker\w*|haustechnik\w*|techniker\w*|schlosser\w*|tischler\w*|maler\w*|rauchfangkehrer\w*|schlüsseldienst\w*|heizungsbauer\w*|reparaturdienst\w*)")]
    private static partial Regex CraftsmanContactWords();

    [GeneratedRegex(@"\b(wer|wen|wem|nummer\w*|telefon\w*|kontakt\w*|ansprech\w*|mail\w*|erreich\w*|anruf\w*|anrufen|rufe|hotline|adresse|haben wir|gibt es|gibt's|hinterlegt)\b|nummer")]
    private static partial Regex ContactAsk();

    [GeneratedRegex(@"\bzuständig\w*|\bkümmer\w*\s+(sich|uns)\b|\bwen\b[^?]*\b(rufe|rufen|anrufen|anruf|kontaktiere\w*|verständige\w*|erreiche\w*|informiere\w*)\b|\bansprechpartner\w*|\bansprechperson\w*")]
    private static partial Regex ResponsibleWords();

    [GeneratedRegex(@"\b(telefon|handy|mobil)?nummer\s+(von|vom|der|des|dem)\s+(?!unser|euer|gutschein|beleg|rechnung|termin|kund|gast|kassa|artikel|behandlung|spa|standort|salon|rezeption|frau|herr)\p{L}")]
    private static partial Regex NumberOfWords();

    [GeneratedRegex(@"feiertag")]
    private static partial Regex HolidayWords();

    /// <summary>„Ferien“, „Schulferien“, „Herbstferien“; Betriebsferien sind keine Daten aus einem Abo.</summary>
    [GeneratedRegex(@"(?<!betriebs)ferien")]
    private static partial Regex SchoolHolidayWords();

    [GeneratedRegex(@"\b\w*ferien\b")]
    private static partial Regex NamedHolidays();

    [GeneratedRegex(@"\b(anreise|anfahrt|parkplatz|parkplätze|parkmöglichkeit\w*|parken|öffentliche[nr]? verkehrsmittel|öffis|zufahrt|wegbeschreibung)\b|wie kommt (der gast|man|ein gast|die gäste|der kunde|die kunden) .*zu uns")]
    private static partial Regex DirectionsWords();

    [GeneratedRegex(@"storno(regel|bedingung|frist|fristen|gebühr|politik)\w*|stornierungs(regel|bedingung|frist|gebühr)\w*|absage(regel|frist)\w*|ausfallgebühr|no-?show-?gebühr|bis wann .*(absagen|stornieren|abgesagt|storniert)|kostenlos\w* (stornier|absag)\w*")]
    private static partial Regex CancellationWords();

    [GeneratedRegex(@"^(?=.*\b(aktion|aktionen|rabatt|rabatte|angebot|angebote|sonderangebot|sonderangebote|aktionspreis\w*)\b)(?=.*(welche|gibt es|gibt ?'?s|laufen|läuft|laufende|gilt|gelten|haben wir|aktuell|gerade|derzeit|momentan|ab wann|bis wann|im (januar|jänner|februar|märz|april|mai|juni|juli|august|september|oktober|november|dezember)))")]
    private static partial Regex PromotionWords();

    [GeneratedRegex(@"(wie ?viele|anzahl|hatten wir|gab es|waren es).*(no[- ]?shows?|nicht[- ]?erschienen\w*|nichterschienen\w*|stornos|stornierungen|absagen)|(no[- ]?shows?|nicht[- ]?erschienen\w*).*(wie ?viele|anzahl|hatten wir|gab es)")]
    private static partial Regex NoShowWords();

    [GeneratedRegex(@"(therapeut\w*|mitarbeiter\w*|masseur\w*|kosmetiker\w*|person\b|personen|wer\b).*(meisten|häufigsten|die meiste)\b.*(termine|behandlungen|gäste|kunden)|(meisten|häufigsten) termine")]
    private static partial Regex TopTherapistWords();

    [GeneratedRegex(@"top[- ]?(kunden|kundinnen|gäste)|beste[nr]? (kunden|gäste|kundinnen)|(kunden|gäste|kundinnen)\b.*(meisten|ausgegeben|ausgaben|besuche)|stammkunden|wer .* am meisten (ausgegeben|bezahlt)")]
    private static partial Regex TopGuestWords();

    [GeneratedRegex(@"top[- ]?behandlung|(beliebtest|gefragtest|häufigst|meistgebucht|meistverkauft)\w*.*(behandlung|massage|anwendung)|(behandlung\w*|massage\w*|anwendung\w*).*(beliebtest|gefragtest|häufigst|meisten|am besten)|am häufigsten gebucht")]
    private static partial Regex TopTreatmentWords();

    [GeneratedRegex(@"umsatz.*(nach|je|pro|von jeder|der einzelnen)\s*behandlung|behandlungsumsatz|(was|wie ?viel) (bringt|bringen|machen|macht|verdienen).*behandlung|umsatz.*behandlungen")]
    private static partial Regex RevenueByTreatmentWords();

    [GeneratedRegex(@"(auslastung|ausgelastet|belegung|belegt)\b.*\b(je|pro|nach|der|von)\s*(person|personen|mitarbeit\w*|therapeut\w*|raum|räume|räumen|team|personal)|raumauslastung|personalauslastung|team-?auslastung|(personal|team|räume|räumen|mitarbeit\w*|therapeut\w*)\b.*(auslastung|ausgelastet)")]
    private static partial Regex UtilizationWords();

    [GeneratedRegex(@"\bwer\s+(arbeitet|(hat|macht)\s+(heute\s+|morgen\s+|am\s+\w+\s+)?dienst|ist\s+(alles\s+)?(heute\s+|morgen\s+|am\s+\w+\s+)?(da|im dienst|anwesend|im haus|eingeteilt|im spa))\b|dienstplan\s+(für\s+)?(heute|morgen)")]
    private static partial Regex WhoWorksWords();

    [GeneratedRegex(@"\bwie\s+(voll|ausgelastet|ausgebucht|gut gebucht|stark gebucht|stark belegt)\b|wie viel los|ausgebucht|\b(auslastung|belegung)\s+(am|heute|morgen|übermorgen|für|vom)\b")]
    private static partial Regex DayLoadWords();

    [GeneratedRegex(@"wie ?viele termine\b.*(haben wir|gibt es|stehen|sind es|hat das spa|hat der standort|insgesamt|noch)|(termine)\b.*(haben wir|gibt es)\b.*(heute|morgen|noch)|\bnoch (offene )?termine")]
    private static partial Regex RemainingWords();

    [GeneratedRegex(@"(?<a>sauna|ruheraum|dampfbad|dampfsauna|infrarotkabine|pool|schwimmbecken|whirlpool|saunabereich|saunalandschaft|wellnessbereich|ruhebereich)\w*.*(voll|frei|belegt|los|leer|besucht|plätze|platz|auslastung|ausgelastet|besetzt)|(wie ?viele|wie viel).*(?<a>sauna|ruheraum|pool|dampfbad)")]
    private static partial Regex AreaWords();

    [GeneratedRegex(@"(welche[rsn]?|ist (ein|der|die|das)|gibt es (einen|ein)?|haben wir (einen|noch einen)?)\b.*(raum|räume|räumen|zimmer|kabine|behandlungsraum)\w*.*(frei|verfügbar|leer|unbelegt)|(raum|kabine|zimmer)\s+[\p{L}\d]+\s+(jetzt |gerade )?frei")]
    private static partial Regex RoomWords();

    [GeneratedRegex(@"\bbetriebs(urlaub|ferien)\w*|\b(schließ|schliess)zeit\w*|\bsonderschließung\w*|\bgeschlossen\b|\bzugesperrt\b|\bhaben wir zu\b|\bsind wir zu\b")]
    private static partial Regex ClosureWords();

    [GeneratedRegex(@"\b(offen|geöffnet|aufhaben|geschlossen|zugesperrt|ruhetag|aufsperren|zusperren|zumachen|aufmachen|öffnungszeit\w*|öffnungszeiten|betriebs(?:urlaub|ferien)\w*|(?:schließ|schliess)zeit\w*|sonderschließung\w*)\b|\bsperr\w*\s+.*\bzu\b|\bwann\s+(schließen|schliessen|öffnen|sperren|sperren wir auf)\b|\bwie lange\b.*\b(auf|offen)\b|\bbis wann\b.*\b(offen|auf|geöffnet)\b|\bhaben wir\b.*\bauf\b")]
    private static partial Regex OpeningWords();

    [GeneratedRegex(@"termin|aufgabe|beleg|rechnung|bestellung|kassa|warteliste|nachricht|mail\b")]
    private static partial Regex NotOpening();

    [GeneratedRegex(@"öffnungszeiten")]
    private static partial Regex OpeningHoursNoun();

    [GeneratedRegex(@"\b(kontaktdaten|telefonnummer|telefon|rufnummer|adresse|anschrift|mailadresse|e-?mail-?adresse|kontakt)\b")]
    private static partial Regex ContactWords();

    [GeneratedRegex(@"\b(unsere[nrms]?|unser|wir|euch|eure[nrms]?)\b|\b(spa|standort|salon|rezeption)\b")]
    private static partial Regex OwnWords();

    [GeneratedRegex(@"\b(frau|herr|herrn|kunde|kundin|gast|familie)\b|\b(hr|fr)\.")]
    private static partial Regex GuestTitles();

    [GeneratedRegex(@"\b(sie|er|ihn|ihm|ihr|sein|seine[nmrs]?|ihre[nmrs]?)\b")]
    private static partial Regex GuestReference();

    /// <summary>„Hat sie diese Woche noch einen Termin frei?“ fragt nach freien Zeiten, nicht nach einem Gast.</summary>
    [GeneratedRegex(@"\bfrei\b")]
    private static partial Regex FreeWord();

    /// <summary>Die Frage bezieht sich mit „sie“, „er“, „ihr“ … auf einen Gast (Ä315).</summary>
    internal static bool RefersToGuest(string text) => GuestReference().IsMatch(text.ToLowerInvariant());

    [GeneratedRegex(@"^\W*wann\s+kommt\b")]
    private static partial Regex GuestComes();

    [GeneratedRegex(@"\btermin")]
    private static partial Regex TerminWord();

    [GeneratedRegex(@"\bhat\b.*\btermin|\bkommt\b.*\b(nächste|kommende|diese)|\btermin\b.*\b(nächste|kommende|diese)")]
    private static partial Regex GuestNextWords();

    [GeneratedRegex(@"\b(wann)\b.*\b(zuletzt|letzte[nr]?\s+(mal|besuch|termin))|zuletzt (da|hier|gewesen|bei uns)|letzter besuch|\bwar\b.*\bzuletzt\b")]
    private static partial Regex GuestLastWords();

    [GeneratedRegex(@"\bwer\s+(?:\w+\s+){0,2}(bietet|macht|kann|führt|behandelt|massiert|durch)\b")]
    private static partial Regex WhoOffersWords();

    [GeneratedRegex(@"was kostet|wie ?viel kostet|wie teuer|was zahlt man|preis (von|für|der|einer)|kostet (eine|ein|die|der)|wie lange dauert|dauer (von|der|einer)")]
    private static partial Regex TreatmentInfoWords();
}
