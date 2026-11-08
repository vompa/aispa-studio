using System.Globalization;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Feste Texte zu Schließzeiten (ADR 0073): Art in Worten, Standardtext für Gäste und die Vorlage der Absage je Art. Die Texte stehen hier an einer Stelle, damit
/// Kalender, Kundenbereich, Assistent und Absage dieselben Wörter benutzen. Die Texte für Gäste (Absage, Warteliste, Rückzahlung) gibt es in den Sprachen des
/// Kundenbereichs (de, en, fr, it, Ä46); der Rest, auch die Vorlage, die das Team bearbeitet, ist Deutsch.
/// </summary>
internal static class ClosureText
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Die Art in Worten für Chip, Band und Meldung: „Betriebsurlaub“, „Sonderschließung“, „Ausfall“.</summary>
    public static string Title(ClosureKind kind) => kind switch
    {
        ClosureKind.Vacation => "Betriebsurlaub",
        ClosureKind.SpecialClosure => "Sonderschließung",
        _ => "Ausfall",
    };

    /// <summary>Text für Gäste, wenn die Schließzeit keinen eigenen hat.</summary>
    public static string DefaultGuestText(ClosureKind kind) => kind switch
    {
        ClosureKind.Vacation => "Wegen Betriebsurlaub geschlossen.",
        ClosureKind.SpecialClosure => "In dieser Zeit ist geschlossen.",
        _ => "Vorübergehend geschlossen.",
    };

    /// <summary>Der Text für Gäste: der eigene, sonst der Standardtext der Art.</summary>
    public static string GuestText(ClosureKind kind, string? own) => string.IsNullOrWhiteSpace(own) ? DefaultGuestText(kind) : own.Trim();

    /// <summary>
    /// Der Zeitraum in Worten, „24. Dezember bis 2. Jänner“; ein einzelner Tag „24. Dezember“; mit Uhrzeit für halbe Tage „24. Dezember ab 13:00 Uhr“.
    /// Das Jahr steht dabei, wenn der Zeitraum nicht im Jahr von <paramref name="today"/> liegt.
    /// </summary>
    public static string Period(DateOnly fromDate, TimeOnly? fromTime, DateOnly untilDate, TimeOnly? untilTime, DateOnly today, string language = "de")
    {
        if (GuestLanguage.Normalize(language) != "de")
        {
            return ForeignPeriod(fromDate, fromTime, untilDate, untilTime, today, GuestLanguage.Normalize(language));
        }

        string Day(DateOnly d) => d.ToString(d.Year == today.Year ? "d. MMMM" : "d. MMMM yyyy", German);
        string Clock(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);

        if (fromDate == untilDate)
        {
            return (fromTime, untilTime) switch
            {
                ({ } a, { } b) => $"{Day(fromDate)}, {Clock(a)} bis {Clock(b)} Uhr",
                ({ } a, null) => $"{Day(fromDate)} ab {Clock(a)} Uhr",
                (null, { } b) => $"{Day(fromDate)} bis {Clock(b)} Uhr",
                _ => Day(fromDate),
            };
        }

        var start = fromTime is { } f ? $"{Day(fromDate)}, {Clock(f)} Uhr" : Day(fromDate);
        var end = untilTime is { } u ? $"{Day(untilDate)}, {Clock(u)} Uhr" : Day(untilDate);
        return $"{start} bis {end}";
    }

    /// <summary>Der Zeitraum für englische, französische und italienische Texte: „24 December to 2 January“, „du 24 décembre au 2 janvier“, „dal 24 dicembre al 2 gennaio“.</summary>
    private static string ForeignPeriod(DateOnly fromDate, TimeOnly? fromTime, DateOnly untilDate, TimeOnly? untilTime, DateOnly today, string language)
    {
        var culture = OnlineLanguage.Culture(language);
        string Day(DateOnly d) => d.ToString(d.Year == today.Year ? "d MMMM" : "d MMMM yyyy", culture);
        string Clock(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);

        if (fromDate == untilDate)
        {
            return (fromTime, untilTime) switch
            {
                ({ } a, { } b) => language switch
                {
                    "en" => $"{Day(fromDate)}, {Clock(a)} to {Clock(b)}",
                    "fr" => $"{Day(fromDate)}, de {Clock(a)} à {Clock(b)}",
                    _ => $"{Day(fromDate)}, dalle {Clock(a)} alle {Clock(b)}",
                },
                ({ } a, null) => language switch
                {
                    "en" => $"{Day(fromDate)} from {Clock(a)}",
                    "fr" => $"{Day(fromDate)} à partir de {Clock(a)}",
                    _ => $"{Day(fromDate)} dalle {Clock(a)}",
                },
                (null, { } b) => language switch
                {
                    "en" => $"{Day(fromDate)} until {Clock(b)}",
                    "fr" => $"{Day(fromDate)} jusqu’à {Clock(b)}",
                    _ => $"{Day(fromDate)} fino alle {Clock(b)}",
                },
                _ => Day(fromDate),
            };
        }

        var start = fromTime is { } f ? $"{Day(fromDate)}, {Clock(f)}" : Day(fromDate);
        var end = untilTime is { } u ? $"{Day(untilDate)}, {Clock(u)}" : Day(untilDate);
        return language switch
        {
            "en" => $"{start} to {end}",
            "fr" => $"du {start} au {end}",
            _ => $"dal {start} al {end}",
        };
    }

    /// <summary>Kurzer Zeitraum für Chip und Band, „bis 2.1.“ ohne Wörter: der letzte Tag als „2.1.“.</summary>
    public static string ShortDay(DateOnly day) => day.ToString("d.M.", CultureInfo.InvariantCulture);

    /// <summary>
    /// Die Schließzeit aus Sicht eines Tages in wenigen Worten: „Betriebsurlaub bis 2.1.“, „Ausfall ab 13:00 Uhr“, „Sonderschließung bis 14:00 Uhr“. Für Antworten des Assistenten
    /// und die Meldung; der Tag ist der Kalendertag, um den es geht.
    /// </summary>
    public static string Describe(Closure closure, DateOnly day)
    {
        string Clock(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);
        var parts = new List<string> { Title(closure.Kind) };
        if (closure.FromDate == day && closure.FromTime is { } from)
        {
            parts.Add($"ab {Clock(from)} Uhr");
        }

        if (closure.UntilDate > day)
        {
            parts.Add($"bis {ShortDay(closure.UntilDate)}");
        }
        else if (closure.UntilTime is { } until)
        {
            parts.Add($"bis {Clock(until)} Uhr");
        }

        return string.Join(' ', parts);
    }

    /// <summary>Schließt einen Satz mit einem Punkt, außer er endet schon auf einen (die Kurzform „2.1.“ endet auf einen).</summary>
    public static string Sentence(string text) => text.EndsWith('.') ? text : text + ".";

    /// <summary>Der Satz, warum geschlossen ist: der eigene Text der Schließzeit (nur Deutsch), sonst der Standardsatz der Art in der Sprache.</summary>
    public static string Reason(ClosureKind kind, string? guestText, string period, string language)
    {
        var reason = language == "de" && !string.IsNullOrWhiteSpace(guestText)
            ? guestText.Trim()
            : (language, kind) switch
            {
                ("en", ClosureKind.Vacation) => $"We are closed for our company holiday ({period}).",
                ("en", ClosureKind.SpecialClosure) => $"We are closed during this period ({period}).",
                ("en", _) => $"Unfortunately we have to close temporarily because of an outage ({period}).",
                ("fr", ClosureKind.Vacation) => $"Nous sommes fermés pour congés annuels ({period}).",
                ("fr", ClosureKind.SpecialClosure) => $"Nous sommes fermés pendant cette période ({period}).",
                ("fr", _) => $"Nous devons malheureusement fermer temporairement en raison d’un incident ({period}).",
                ("it", ClosureKind.Vacation) => $"Siamo chiusi per le ferie aziendali ({period}).",
                ("it", ClosureKind.SpecialClosure) => $"Siamo chiusi in questo periodo ({period}).",
                ("it", _) => $"Purtroppo dobbiamo chiudere temporaneamente a causa di un imprevisto ({period}).",
                (_, ClosureKind.Vacation) => $"Wegen unseres Betriebsurlaubs ({period}) haben wir geschlossen.",
                (_, ClosureKind.SpecialClosure) => $"In der Zeit {period} haben wir geschlossen.",
                _ => $"Leider müssen wir wegen eines Ausfalls vorübergehend schließen ({period}).",
            };
        return reason.EndsWith('.') || reason.EndsWith('!') || reason.EndsWith('?') ? reason : reason + ".";
    }

    /// <summary>
    /// Vorschlag für Betreff und Text der Absage in der Sprache des Gastes; <c>{Anrede}</c>, <c>{Behandlung}</c> und <c>{Zeit}</c> setzt der Server je Gast ein.
    /// Deutsch ist die Vorlage, die das Team bearbeitet; die anderen Sprachen sind Standardvorlagen (ohne den eigenen Text für Gäste, der deutsch ist).
    /// </summary>
    public static (string Subject, string Text) CancellationTemplate(ClosureKind kind, string? guestText, string period, string spa, string language = "de")
    {
        language = GuestLanguage.Normalize(language);
        var reason = Reason(kind, guestText, period, language);
        var text = language switch
        {
            "en" => $$"""
                Hello {Anrede},

                {{reason}} We therefore have to cancel your appointment:

                {Behandlung}
                {Zeit}

                Thank you for your understanding.
                {{spa}}
                """,
            "fr" => $$"""
                Bonjour {Anrede},

                {{reason}} Nous devons donc malheureusement annuler votre rendez-vous :

                {Behandlung}
                {Zeit}

                Nous vous remercions de votre compréhension.
                {{spa}}
                """,
            "it" => $$"""
                Buongiorno {Anrede},

                {{reason}} Siamo quindi spiacenti di dover annullare il suo appuntamento:

                {Behandlung}
                {Zeit}

                La ringraziamo per la comprensione.
                {{spa}}
                """,
            _ => $$"""
                Guten Tag {Anrede},

                {{reason}} Deshalb müssen wir Ihren Termin leider absagen:

                {Behandlung}
                {Zeit}

                Wir bitten um Ihr Verständnis.
                {{spa}}
                """,
        };
        var subject = language switch
        {
            "en" => "Cancellation of your appointment: {Behandlung}, {Zeit}",
            "fr" => "Annulation de votre rendez-vous : {Behandlung}, {Zeit}",
            "it" => "Annullamento del suo appuntamento: {Behandlung}, {Zeit}",
            _ => "Absage Ihres Termins: {Behandlung}, {Zeit}",
        };
        return (subject, text);
    }

    /// <summary>
    /// Die Korrektur an einen Gast, der die Absage schon bekommen hat, deren Zurücknehmen aber das Team beschlossen hat (Ä95): die Absage war ein Irrtum, der Termin besteht
    /// (oder liegt neu). Fertig ausgefüllt, in der Sprache des Gastes.
    /// </summary>
    public static (string Subject, string Text) Correction(string greeting, string treatment, string when, string spa, string language)
    {
        language = GuestLanguage.Normalize(language);
        var text = language switch
        {
            "en" => $"""
                Hello {greeting},

                our cancellation of your appointment was a mistake, and we apologise. Your appointment takes place:

                {treatment}
                {when}

                We look forward to seeing you.
                {spa}
                """,
            "fr" => $"""
                Bonjour {greeting},

                l’annulation de votre rendez-vous était une erreur, et nous nous en excusons. Votre rendez-vous a lieu :

                {treatment}
                {when}

                Nous nous réjouissons de vous accueillir.
                {spa}
                """,
            "it" => $"""
                Buongiorno {greeting},

                la cancellazione del suo appuntamento è stata un errore, e ce ne scusiamo. Il suo appuntamento si svolge:

                {treatment}
                {when}

                Siamo lieti di accoglierla.
                {spa}
                """,
            _ => $"""
                Guten Tag {greeting},

                die Absage Ihres Termins war ein Irrtum, wir bitten um Entschuldigung. Ihr Termin findet statt:

                {treatment}
                {when}

                Wir freuen uns auf Sie.
                {spa}
                """,
        };
        var subject = language switch
        {
            "en" => $"Your appointment takes place: {treatment}, {when}",
            "fr" => $"Votre rendez-vous a lieu : {treatment}, {when}",
            "it" => $"Il suo appuntamento si svolge: {treatment}, {when}",
            _ => $"Ihr Termin findet statt: {treatment}, {when}",
        };
        return (subject, text);
    }

    /// <summary>
    /// Die kurze Nachricht an einen Wartelisten-Eintrag, dessen Wunschzeitraum in die Schließzeit fällt (Ä48), in seiner Sprache. Der Eintrag bleibt auf der Liste, das sagt der
    /// Text. Fertig ausgefüllt: <paramref name="wish"/> ist sein Wunschzeitraum, <paramref name="greeting"/> die Anrede wie in den Mails des Spas.
    /// </summary>
    public static (string Subject, string Text) WaitlistNotice(ClosureKind kind, string? guestText, string closurePeriod, string wish, string greeting, string treatment, string spa, string language)
    {
        language = GuestLanguage.Normalize(language);
        var reason = Reason(kind, guestText, closurePeriod, language);
        return language switch
        {
            "en" => ("Your waiting-list request falls within a closure", $"""
                Hello {greeting},

                your waiting-list request falls within a closure:

                {treatment}
                {wish}

                {reason}
                You stay on the waiting list: as soon as an appointment becomes free in your requested period outside the closure, we will get in touch. You can change or leave the list at any time using the link in your confirmation.
                {spa}
                """),
            "fr" => ("Votre demande sur la liste d’attente tombe pendant une fermeture", $"""
                Bonjour {greeting},

                votre demande sur la liste d’attente tombe pendant une fermeture :

                {treatment}
                {wish}

                {reason}
                Vous restez sur la liste d’attente : dès qu’un rendez-vous se libère dans la période souhaitée en dehors de la fermeture, nous vous contactons. Vous pouvez modifier ou quitter la liste à tout moment via le lien de votre confirmation.
                {spa}
                """),
            "it" => ("La sua richiesta in lista d’attesa cade in un periodo di chiusura", $"""
                Buongiorno {greeting},

                la sua richiesta in lista d’attesa cade in un periodo di chiusura:

                {treatment}
                {wish}

                {reason}
                Resta in lista d’attesa: appena si libera un appuntamento nel periodo desiderato al di fuori della chiusura, la contatteremo. Può modificare o lasciare la lista in qualsiasi momento tramite il link nella conferma.
                {spa}
                """),
            _ => ("Ihr Wartelisten-Wunsch fällt in eine Schließzeit", $"""
                Guten Tag {greeting},

                Ihr Wartelisten-Wunsch fällt in eine Schließzeit:

                {treatment}
                {wish}

                {reason}
                Sie bleiben auf der Warteliste: Sobald in Ihrem Wunschzeitraum außerhalb der Schließzeit ein Termin frei wird, melden wir uns. Ändern oder austragen können Sie sich jederzeit über den Link in Ihrer Bestätigung.
                {spa}
                """),
        };
    }

    /// <summary>
    /// Der Satz zur Rückzahlung der Anzahlung, der an die Absage gehängt wird, wenn sie über die Kassa retourniert wurde (Ä47). Hat der Gast bar bezahlt (<paramref name="cash"/>), sagt er,
    /// dass er das Geld bar an der Rezeption bekommt (Ä53); sonst geht es auf dem Zahlungsweg der Einzahlung zurück.
    /// </summary>
    public static string RefundSentence(decimal amount, string currency, string language, bool cash = false)
    {
        language = GuestLanguage.Normalize(language);
        var money = Money(amount, currency, language);
        if (cash)
        {
            return language switch
            {
                "en" => $"You will receive the amount of {money} back in cash at reception.",
                "fr" => $"Vous recevrez le montant de {money} en espèces à la réception.",
                "it" => $"Riceverà l’importo di {money} in contanti alla reception.",
                _ => $"Sie erhalten den Betrag von {money} bar an der Rezeption zurück.",
            };
        }

        return language switch
        {
            "en" => $"Your deposit of {money} will be refunded to the payment method you used.",
            "fr" => $"Votre acompte de {money} sera remboursé par le moyen de paiement utilisé.",
            "it" => $"Il suo acconto di {money} le sarà rimborsato con il metodo di pagamento utilizzato.",
            _ => $"Ihre Anzahlung von {money} wird auf dem Zahlungsweg der Einzahlung retourniert.",
        };
    }

    /// <summary>
    /// Fügt einen Absatz vor dem letzten Absatz ein, der als Gruß gilt („Wir bitten um Ihr Verständnis.“, „Ihr Team“): so steht die Rückzahlung bei der Absage und nicht hinter der
    /// Unterschrift. Hat der Text weniger als drei Absätze, kommt sie ans Ende.
    /// </summary>
    public static string WithBeforeClosing(string body, string paragraph)
    {
        var paragraphs = body.Split("\n\n");
        if (paragraphs.Length < 3)
        {
            return $"{body}\n\n{paragraph}";
        }

        paragraphs[^1] = $"{paragraph}\n\n{paragraphs[^1]}";
        return string.Join("\n\n", paragraphs);
    }

    /// <summary>Der Satz mit dem Link zum Neubuchen, den die Absage auf Wunsch trägt.</summary>
    public static string RebookingSentence(string link, string language) => GuestLanguage.Normalize(language) switch
    {
        "en" => $"You can book a new appointment here: {link}",
        "fr" => $"Vous pouvez réserver un nouveau rendez-vous ici : {link}",
        "it" => $"Può prenotare un nuovo appuntamento qui: {link}",
        _ => $"Einen neuen Termin können Sie hier buchen: {link}",
    };

    /// <summary>Betrag mit Währung in der Schreibweise der Sprache: „30,00 €“, in Englisch „€30.00“; andere Währungen mit Kürzel („30.00 CHF“).</summary>
    public static string Money(decimal amount, string currency, string language)
    {
        // Zwei Nachkommastellen, in Deutsch, Französisch und Italienisch mit Komma (nicht nach dem Schweizer Muster der Kundenbereich-Kultur), ohne Tausendertrennzeichen.
        var number = amount.ToString("0.00", CultureInfo.InvariantCulture);
        number = GuestLanguage.Normalize(language) == "en" ? number : number.Replace('.', ',');
        return currency switch
        {
            "EUR" when GuestLanguage.Normalize(language) == "en" => $"€{number}",
            "EUR" => $"{number} €",
            _ => $"{number} {currency}",
        };
    }

    /// <summary>Die Platzhalter der Vorlage, die der Server je Gast ersetzt.</summary>
    public static readonly string[] Placeholders = ["{Anrede}", "{Behandlung}", "{Zeit}"];

    /// <summary>Setzt die Platzhalter für einen Gast ein.</summary>
    public static string Fill(string template, string greeting, string treatment, string when) =>
        template.Replace("{Anrede}", greeting, StringComparison.Ordinal)
            .Replace("{Behandlung}", treatment, StringComparison.Ordinal)
            .Replace("{Zeit}", when, StringComparison.Ordinal);
}
