using System.Globalization;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Online;

/// <summary>
/// Texte der Mails aus dem Kundenbereich auf Deutsch, Englisch, Französisch oder Italienisch (<see cref="OnlineLanguage"/>).
/// Bewusst schlicht: Text zuerst, gut vorlesbar.
/// </summary>
internal static class OnlineMails
{
    /// <summary>Standort mit Anschrift und Telefon, soweit gepflegt, eine Angabe je Zeile.</summary>
    public static string Place(OnlineAppointment appointment)
    {
        var c = appointment.LocationContact;
        var town = string.Join(" ", new[] { c.PostalCode, c.City }.Where(x => !string.IsNullOrEmpty(x)));
        return string.Join("\n", new[] { appointment.LocationName, c.Street, town, c.Phone }.Where(x => !string.IsNullOrEmpty(x)));
    }

    /// <summary>
    /// Zeile „Anreise“ der Terminbestätigung: der Anreise-Text des Standorts und, wenn eine Anschrift gepflegt ist, der Link zur Routenplanung (Ziel: die Anschrift, Start:
    /// der Standort des Geräts, den die Karten-App selbst erfragt). Ohne Anreise-Text entfällt der Abschnitt ganz.
    /// </summary>
    public static string Route(OnlineAppointment appointment, string lang)
    {
        var c = appointment.LocationContact;
        if (string.IsNullOrWhiteSpace(c.Directions))
        {
            return "";
        }

        var (directions, plan) = lang switch
        {
            "en" => ("Getting here", "Plan your route"),
            "fr" => ("Accès", "Planifier l’itinéraire"),
            "it" => ("Come arrivare", "Pianifica il percorso"),
            _ => ("Anreise", "Route planen"),
        };
        var town = string.Join(" ", new[] { c.PostalCode, c.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var destination = string.Join(", ", new[] { c.Street, town }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var link = destination.Length == 0 ? "" : $"\n{plan}: https://www.google.com/maps/dir/?api=1&destination={Uri.EscapeDataString(destination)}";
        return $"\n\n{directions}: {c.Directions.Trim()}{link}";
    }

    public static OutgoingMail SignInCode(string to, string code, string? language) => GuestLanguage.Normalize(language) switch
    {
        "en" => new(to, $"Your sign-in code: {code}",
            $"""
             Your sign-in code is: {code}

             It is valid for 10 minutes. If you did not request it, you can ignore this email.
             """),
        "fr" => new(to, $"Votre code de connexion : {code}",
            $"""
             Votre code de connexion est : {code}

             Il est valable 10 minutes. Si vous ne l’avez pas demandé, vous pouvez ignorer cet e-mail.
             """),
        "it" => new(to, $"Il suo codice di accesso: {code}",
            $"""
             Il suo codice di accesso è: {code}

             È valido per 10 minuti. Se non l’ha richiesto, può ignorare questa e-mail.
             """),
        _ => new(to, $"Ihr Anmeldecode: {code}",
            $"""
             Ihr Anmeldecode lautet: {code}

             Er gilt 10 Minuten. Falls Sie ihn nicht angefordert haben, können Sie diese Mail ignorieren.
             """),
    };

    public static OutgoingMail Confirmation(
        string to, GuestName guest, OnlineAppointment appointment, string manageUrl, string anamnesisUrl, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var when = Format(appointment.Start, appointment.TimeZone, lang);
        var deadline = Format(appointment.CancellableUntil, appointment.TimeZone, lang);
        var price = appointment.Price.ToString("0.00", OnlineLanguage.Culture(lang));
        var card = DepositText(appointment, deadline, manageUrl, lang) + CardText(appointment, deadline, manageUrl, lang);
        var hint = appointment.GuestHint is { } h ? $"{h}\n\n" : "";
        return lang switch
        {
            "en" => new(to, $"Booking confirmed: {appointment.TreatmentName} on {when}",
                $"""
                 Hello {guest.FirstName},

                 your appointment is booked:

                 {appointment.TreatmentName}
                 {when} ({appointment.TimeZone})
                 {Place(appointment)}
                 {price} {appointment.Currency}{Route(appointment, lang)}

                 {hint}Please fill in the short health questionnaire before your visit:
                 {anamnesisUrl}

                 {card}View or cancel your appointment (online until {deadline}):
                 {manageUrl}

                 We look forward to seeing you.
                 """),
            "fr" => new(to, $"Rendez-vous confirmé : {appointment.TreatmentName} le {when}",
                $"""
                 Bonjour {guest.FirstName},

                 Votre rendez-vous est réservé :

                 {appointment.TreatmentName}
                 {when} ({appointment.TimeZone})
                 {Place(appointment)}
                 {price} {appointment.Currency}{Route(appointment, lang)}

                 {hint}Merci de remplir le court questionnaire de santé avant votre visite :
                 {anamnesisUrl}

                 {card}Consulter ou annuler votre rendez-vous (en ligne jusqu’au {deadline}) :
                 {manageUrl}

                 Nous nous réjouissons de vous accueillir.
                 """),
            "it" => new(to, $"Appuntamento confermato: {appointment.TreatmentName}, {when}",
                $"""
                 Buongiorno {guest.FirstName},

                 il suo appuntamento è prenotato:

                 {appointment.TreatmentName}
                 {when} ({appointment.TimeZone})
                 {Place(appointment)}
                 {price} {appointment.Currency}{Route(appointment, lang)}

                 {hint}Prima della visita la preghiamo di compilare il breve questionario sulla salute:
                 {anamnesisUrl}

                 {card}Visualizzare o annullare l’appuntamento (online fino a {deadline}):
                 {manageUrl}

                 Saremo lieti di accoglierla.
                 """),
            _ => new(to, $"Termin bestätigt: {appointment.TreatmentName} am {when}",
                $"""
                 Guten Tag {guest.GreetingName},

                 Ihr Termin ist gebucht:

                 {appointment.TreatmentName}
                 {when} Uhr ({appointment.TimeZone})
                 {Place(appointment)}
                 {price} {appointment.Currency}{Route(appointment, lang)}

                 {hint}Bitte füllen Sie vor Ihrem Besuch den kurzen Gesundheitsfragebogen aus:
                 {anamnesisUrl}

                 {card}Termin ansehen oder stornieren (online bis {deadline} Uhr):
                 {manageUrl}

                 Wir freuen uns auf Sie.
                 """),
        };
    }

    public static OutgoingMail Cancelled(string to, GuestName guest, OnlineAppointment appointment, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var when = Format(appointment.Start, appointment.TimeZone, lang);
        return lang switch
        {
            "en" => new(to, $"Appointment cancelled: {appointment.TreatmentName} on {when}",
                $"""
                 Hello {guest.FirstName},

                 your appointment "{appointment.TreatmentName}" on {when} at {appointment.LocationName} has been cancelled.
                 """),
            "fr" => new(to, $"Rendez-vous annulé : {appointment.TreatmentName} le {when}",
                $"""
                 Bonjour {guest.FirstName},

                 Votre rendez-vous « {appointment.TreatmentName} » du {when} ({appointment.LocationName}) a été annulé.
                 """),
            "it" => new(to, $"Appuntamento annullato: {appointment.TreatmentName}, {when}",
                $"""
                 Buongiorno {guest.FirstName},

                 il suo appuntamento «{appointment.TreatmentName}» presso {appointment.LocationName}, previsto per {when}, è stato annullato.
                 """),
            _ => new(to, $"Termin storniert: {appointment.TreatmentName} am {when}",
                $"""
                 Guten Tag {guest.GreetingName},

                 Ihr Termin „{appointment.TreatmentName}“ am {when} Uhr in {appointment.LocationName} ist storniert.
                 """),
        };
    }

    public static OutgoingMail Moved(string to, GuestName guest, OnlineAppointment appointment, string manageUrl, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var when = Format(appointment.Start, appointment.TimeZone, lang);
        return lang switch
        {
            "en" => new(to, $"Appointment moved: {appointment.TreatmentName} on {when}",
                $"""
                 Hello {guest.FirstName},

                 your appointment "{appointment.TreatmentName}" at {appointment.LocationName} is now on {when}.

                 View, confirm or cancel your appointment:
                 {manageUrl}
                 """),
            "fr" => new(to, $"Rendez-vous déplacé : {appointment.TreatmentName} le {when}",
                $"""
                 Bonjour {guest.FirstName},

                 Votre rendez-vous « {appointment.TreatmentName} » ({appointment.LocationName}) a lieu désormais le {when}.

                 Consulter, confirmer ou annuler votre rendez-vous :
                 {manageUrl}
                 """),
            "it" => new(to, $"Appuntamento spostato: {appointment.TreatmentName}, {when}",
                $"""
                 Buongiorno {guest.FirstName},

                 il suo appuntamento «{appointment.TreatmentName}» presso {appointment.LocationName} è ora previsto per {when}.

                 Visualizzare, confermare o annullare l’appuntamento:
                 {manageUrl}
                 """),
            _ => new(to, $"Termin verschoben: {appointment.TreatmentName} am {when}",
                $"""
                 Guten Tag {guest.GreetingName},

                 Ihr Termin „{appointment.TreatmentName}“ in {appointment.LocationName} ist jetzt am {when} Uhr.

                 Termin ansehen, bestätigen oder absagen:
                 {manageUrl}
                 """),
        };
    }

    public static OutgoingMail WaitlistJoined(string to, GuestName guest, WaitlistEntryDto entry, string manageUrl, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var range = Range(entry, lang);
        return lang switch
        {
            "en" => new(to, $"You are on the waiting list: {entry.TreatmentName}",
                $"""
                 Hello {guest.FirstName},

                 you are on the waiting list for "{entry.TreatmentName}" at {entry.LocationName}, {range}.
                 As soon as an appointment becomes available, we will email you. Whoever books first gets it.

                 View or leave the waiting list:
                 {manageUrl}
                 """),
            "fr" => new(to, $"Vous êtes sur la liste d’attente : {entry.TreatmentName}",
                $"""
                 Bonjour {guest.FirstName},

                 Vous figurez sur la liste d’attente pour « {entry.TreatmentName} » ({entry.LocationName}), {range}.
                 Dès qu’un rendez-vous se libère, nous vous écrivons. Il revient à la première personne qui réserve.

                 Consulter votre inscription ou quitter la liste d’attente :
                 {manageUrl}
                 """),
            "it" => new(to, $"È in lista d’attesa: {entry.TreatmentName}",
                $"""
                 Buongiorno {guest.FirstName},

                 è in lista d’attesa per «{entry.TreatmentName}» presso {entry.LocationName}, {range}.
                 Non appena si libera un appuntamento, le scriveremo. L’appuntamento va alla prima persona che prenota.

                 Visualizzare l’iscrizione o uscire dalla lista d’attesa:
                 {manageUrl}
                 """),
            _ => new(to, $"Sie stehen auf der Warteliste: {entry.TreatmentName}",
                $"""
                 Guten Tag {guest.GreetingName},

                 Sie stehen auf der Warteliste für „{entry.TreatmentName}“ in {entry.LocationName}, {range}.
                 Sobald ein Termin frei wird, schreiben wir Ihnen. Wer zuerst bucht, bekommt ihn.

                 Eintrag ansehen oder austragen:
                 {manageUrl}
                 """),
        };
    }

    public static OutgoingMail WaitlistOffer(
        string to, GuestName guest, string treatmentName, string locationName, string timeZone,
        IReadOnlyList<DateTimeOffset> times, string bookUrl, string manageUrl, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var zone = LocalTime.Zone(timeZone);
        var culture = OnlineLanguage.Culture(lang);
        var day = TimeZoneInfo.ConvertTime(times[0], zone).ToString(lang == "de" ? "dddd, d. MMMM yyyy" : lang == "en" ? "dddd, d MMMM yyyy" : "dddd d MMMM yyyy", culture);
        var list = string.Join(", ", times.Select(t => TimeZoneInfo.ConvertTime(t, zone).ToString("HH:mm", culture)));
        return lang switch
        {
            "en" => new(to, $"Appointment available: {treatmentName} on {day}",
                $"""
                 Hello {guest.FirstName},

                 an appointment for "{treatmentName}" at {locationName} has become available on {day}.
                 Free times: {list} ({timeZone})

                 Book now (whoever books first gets it):
                 {bookUrl}

                 Leave the waiting list:
                 {manageUrl}
                 """),
            "fr" => new(to, $"Rendez-vous disponible : {treatmentName} le {day}",
                $"""
                 Bonjour {guest.FirstName},

                 Un rendez-vous pour « {treatmentName} » ({locationName}) s’est libéré le {day}.
                 Horaires libres : {list} ({timeZone})

                 Réserver maintenant (le rendez-vous revient à la première personne qui réserve) :
                 {bookUrl}

                 Quitter la liste d’attente :
                 {manageUrl}
                 """),
            "it" => new(to, $"Appuntamento disponibile: {treatmentName}, {day}",
                $"""
                 Buongiorno {guest.FirstName},

                 si è liberato un appuntamento per «{treatmentName}» presso {locationName}: {day}.
                 Orari liberi: {list} ({timeZone})

                 Prenotare ora (l’appuntamento va alla prima persona che prenota):
                 {bookUrl}

                 Uscire dalla lista d’attesa:
                 {manageUrl}
                 """),
            _ => new(to, $"Termin frei: {treatmentName} am {day}",
                $"""
                 Guten Tag {guest.GreetingName},

                 für „{treatmentName}“ in {locationName} ist am {day} ein Termin frei geworden.
                 Freie Zeiten: {list} Uhr ({timeZone})

                 Jetzt buchen (wer zuerst bucht, bekommt den Termin):
                 {bookUrl}

                 Von der Warteliste austragen:
                 {manageUrl}
                 """),
        };
    }

    /// <summary>Absatz zur Ausfallgebühr in der Bestätigung, nur wenn der Standort eine Karte verlangt.</summary>
    private static string CardText(OnlineAppointment appointment, string deadline, string manageUrl, string lang)
    {
        if (appointment.CardGuarantee is not { } g)
        {
            return "";
        }

        var fee = g.FeeAmount.ToString("0.00", OnlineLanguage.Culture(lang));
        var hold = g.HoldUntil is { } until ? Format(until, appointment.TimeZone, lang) : null;
        return HoldText(hold, lang) + lang switch
        {
            "en" => $"No-show fee: if you do not come or cancel after {deadline}, we charge {fee} {g.Currency} ({g.FeePercent} % of the price). "
                + $"Please save a card for this; it is only charged in that case:\n{manageUrl}\n\n",
            "fr" => $"Frais d’annulation : en cas d’absence ou d’annulation après le {deadline}, nous facturons {fee} {g.Currency} ({g.FeePercent} % du prix). "
                + $"Merci d’enregistrer une carte à cet effet ; elle n’est débitée que dans ce cas :\n{manageUrl}\n\n",
            "it" => $"Penale: in caso di mancata presentazione o di annullamento dopo il {deadline} addebitiamo {fee} {g.Currency} ({g.FeePercent} % del prezzo). "
                + $"La preghiamo di registrare una carta; viene addebitata solo in questo caso:\n{manageUrl}\n\n",
            _ => $"Ausfallgebühr: Wenn Sie nicht kommen oder nach {deadline} Uhr absagen, berechnen wir {fee} {g.Currency} ({g.FeePercent} % des Preises). "
                + $"Bitte hinterlegen Sie dafür eine Karte; belastet wird nur in diesem Fall:\n{manageUrl}\n\n",
        };
    }

    private static string HoldText(string? hold, string lang) => hold is null
        ? ""
        : lang switch
        {
            "en" => $"Please save the card by {hold}; otherwise we release the appointment for other guests.\n",
            "fr" => $"Merci d’enregistrer la carte avant le {hold} ; sinon, nous libérons le rendez-vous pour d’autres clients.\n",
            "it" => $"La preghiamo di registrare la carta entro il {hold}; altrimenti liberiamo l’appuntamento per altri ospiti.\n",
            _ => $"Bitte hinterlegen Sie die Karte bis {hold} Uhr, sonst geben wir den Termin für andere Gäste frei.\n",
        };

    /// <summary>Absatz zur Anzahlung in der Bestätigung, nur wenn eine verlangt wird.</summary>
    private static string DepositText(OnlineAppointment appointment, string deadline, string manageUrl, string lang)
    {
        if (appointment.Deposit is not { } d)
        {
            return "";
        }

        var amount = d.Amount.ToString("0.00", OnlineLanguage.Culture(lang));
        var hold = d.PayUntil is { } until ? Format(until, appointment.TimeZone, lang) : null;
        var full = d.Percent >= 100;
        var holdText = hold is null
            ? ""
            : lang switch
            {
                "en" => $"Please pay by {hold}; otherwise we release the appointment for other guests.\n",
                "fr" => $"Merci de payer avant le {hold} ; sinon, nous libérons le rendez-vous pour d’autres clients.\n",
                "it" => $"La preghiamo di pagare entro il {hold}; altrimenti liberiamo l’appuntamento per altri ospiti.\n",
                _ => $"Bitte bezahlen Sie bis {hold} Uhr, sonst geben wir den Termin für andere Gäste frei.\n",
            };
        return holdText + lang switch
        {
            "en" => $"{(full ? "Prepayment" : "Deposit")}: {amount} {d.Currency} ({d.Percent} % of the price), deducted from the price at the spa. "
                + $"If you cancel by {deadline}, you get it back; if you do not come or cancel later, it is kept. Pay here:\n{manageUrl}\n\n",
            "fr" => $"{(full ? "Prépaiement" : "Acompte")} : {amount} {d.Currency} ({d.Percent} % du prix), déduit du prix sur place. "
                + $"En cas d’annulation avant le {deadline}, il vous est remboursé ; en cas d’absence ou d’annulation tardive, il est conservé. Payer ici :\n{manageUrl}\n\n",
            "it" => $"{(full ? "Pagamento anticipato" : "Acconto")}: {amount} {d.Currency} ({d.Percent} % del prezzo), detratto dal prezzo in sede. "
                + $"Se annulla entro il {deadline} le viene rimborsato; in caso di mancata presentazione o annullamento tardivo viene trattenuto. Paghi qui:\n{manageUrl}\n\n",
            _ => $"{(full ? "Vorauszahlung" : "Anzahlung")}: {amount} {d.Currency} ({d.Percent} % des Preises), wird im Spa vom Preis abgezogen. "
                + $"Bei Absage bis {deadline} Uhr bekommen Sie sie zurück; wenn Sie nicht kommen oder später absagen, bleibt sie beim Spa. Hier bezahlen:\n{manageUrl}\n\n",
        };
    }

    /// <summary>Anzahlungsbeleg nach der Zahlung (Belegerteilung per Mail).</summary>
    public static OutgoingMail DepositReceived(
        string to, GuestName guest, string treatment, DateTimeOffset start, string timeZone, string location, decimal amount, string currency,
        int percent, string receipt, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var when = Format(start, timeZone, lang);
        var sum = amount.ToString("0.00", OnlineLanguage.Culture(lang));
        var full = percent >= 100;
        return lang switch
        {
            "en" => new(to, $"{(full ? "Prepayment" : "Deposit")} received for {treatment}",
                $"""
                 Hello {guest.FirstName},

                 thank you, we have received your {(full ? "prepayment" : "deposit")} of {sum} {currency} for this appointment:

                 {treatment}
                 {when}
                 {location}

                 Receipt: {receipt}

                 It will be deducted from the price when you pay at the spa.
                 """),
            "fr" => new(to, $"{(full ? "Prépaiement" : "Acompte")} reçu pour {treatment}",
                $"""
                 Bonjour {guest.FirstName},

                 Merci, nous avons bien reçu votre {(full ? "prépaiement" : "acompte")} de {sum} {currency} pour ce rendez-vous :

                 {treatment}
                 {when}
                 {location}

                 Quittance : {receipt}

                 Il sera déduit du prix lors du paiement sur place.
                 """),
            "it" => new(to, $"{(full ? "Pagamento anticipato" : "Acconto")} ricevuto per {treatment}",
                $"""
                 Buongiorno {guest.FirstName},

                 grazie, abbiamo ricevuto {(full ? "il suo pagamento anticipato" : "il suo acconto")} di {sum} {currency} per questo appuntamento:

                 {treatment}
                 {when}
                 {location}

                 Ricevuta: {receipt}

                 Verrà detratto dal prezzo al momento del pagamento in sede.
                 """),
            _ => new(to, $"{(full ? "Vorauszahlung" : "Anzahlung")} erhalten: {treatment}",
                $"""
                 Guten Tag {guest.GreetingName},

                 danke, wir haben Ihre {(full ? "Vorauszahlung" : "Anzahlung")} von {sum} {currency} für diesen Termin erhalten:

                 {treatment}
                 {when} Uhr
                 {location}

                 Beleg: {receipt}

                 Beim Bezahlen im Spa ziehen wir sie vom Preis ab.
                 """),
        };
    }

    /// <summary>Der online gebuchte Termin wurde freigegeben, weil in der Frist keine Karte hinterlegt wurde.</summary>
    public static OutgoingMail Released(string to, GuestName guest, OnlineAppointment appointment, string bookUrl, string? language, bool deposit = false)
    {
        var lang = GuestLanguage.Normalize(language);
        var when = Format(appointment.Start, appointment.TimeZone, lang);
        if (deposit)
        {
            return ReleasedUnpaid(to, guest, appointment, when, bookUrl, lang);
        }

        return lang switch
        {
            "en" => new(to, $"Appointment released: {appointment.TreatmentName} on {when}",
                $"""
                 Hello {guest.FirstName},

                 no card was saved for your appointment "{appointment.TreatmentName}" on {when} at {appointment.LocationName}, so we have released it. Nothing was charged.

                 You are welcome to book again:
                 {bookUrl}
                 """),
            "fr" => new(to, $"Rendez-vous libéré : {appointment.TreatmentName} le {when}",
                $"""
                 Bonjour {guest.FirstName},

                 Aucune carte n’ayant été enregistrée pour votre rendez-vous « {appointment.TreatmentName} » du {when} ({appointment.LocationName}), nous l’avons libéré. Rien n’a été débité.

                 Vous pouvez réserver à nouveau :
                 {bookUrl}
                 """),
            "it" => new(to, $"Appuntamento liberato: {appointment.TreatmentName}, {when}",
                $"""
                 Buongiorno {guest.FirstName},

                 per il suo appuntamento «{appointment.TreatmentName}» presso {appointment.LocationName}, previsto per {when}, non è stata registrata alcuna carta, quindi lo abbiamo liberato. Non è stato addebitato nulla.

                 Può prenotare di nuovo qui:
                 {bookUrl}
                 """),
            _ => new(to, $"Termin freigegeben: {appointment.TreatmentName} am {when}",
                $"""
                 Guten Tag {guest.GreetingName},

                 für Ihren Termin „{appointment.TreatmentName}“ am {when} Uhr in {appointment.LocationName} wurde keine Karte hinterlegt. Wir haben ihn deshalb freigegeben; abgebucht wurde nichts.

                 Gerne können Sie neu buchen:
                 {bookUrl}
                 """),
        };
    }

    private static OutgoingMail ReleasedUnpaid(string to, GuestName guest, OnlineAppointment appointment, string when, string bookUrl, string lang) => lang switch
    {
        "en" => new(to, $"Appointment released: {appointment.TreatmentName} on {when}",
            $"""
             Hello {guest.FirstName},

             the deposit for your appointment "{appointment.TreatmentName}" on {when} at {appointment.LocationName} was not paid, so we have released it. Nothing was charged.

             You are welcome to book again:
             {bookUrl}
             """),
        "fr" => new(to, $"Rendez-vous libéré : {appointment.TreatmentName} le {when}",
            $"""
             Bonjour {guest.FirstName},

             L’acompte pour votre rendez-vous « {appointment.TreatmentName} » du {when} ({appointment.LocationName}) n’ayant pas été payé, nous l’avons libéré. Rien n’a été débité.

             Vous pouvez réserver à nouveau :
             {bookUrl}
             """),
        "it" => new(to, $"Appuntamento liberato: {appointment.TreatmentName}, {when}",
            $"""
             Buongiorno {guest.FirstName},

             l’acconto per il suo appuntamento «{appointment.TreatmentName}» presso {appointment.LocationName}, previsto per {when}, non è stato pagato, quindi lo abbiamo liberato. Non è stato addebitato nulla.

             Può prenotare di nuovo qui:
             {bookUrl}
             """),
        _ => new(to, $"Termin freigegeben: {appointment.TreatmentName} am {when}",
            $"""
             Guten Tag {guest.GreetingName},

             die Anzahlung für Ihren Termin „{appointment.TreatmentName}“ am {when} Uhr in {appointment.LocationName} wurde nicht bezahlt. Wir haben ihn deshalb freigegeben; abgebucht wurde nichts.

             Gerne können Sie neu buchen:
             {bookUrl}
             """),
    };

    /// <summary>
    /// Die Rezeption bittet nach einer Buchung am Telefon oder vor Ort, eine Karte für die Ausfallgebühr zu hinterlegen. Zugestimmt
    /// wird erst auf der Seite hinter dem Link.
    /// </summary>
    public static OutgoingMail CardRequest(
        string to, GuestName guest, string treatment, DateTimeOffset start, string timeZone, string location, decimal amount, string currency,
        int percent, int deadlineHours, string url, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var when = Format(start, timeZone, lang);
        var fee = amount.ToString("0.00", OnlineLanguage.Culture(lang));
        return lang switch
        {
            "en" => new(to, $"Please save a card for {treatment}",
                $"""
                 Hello {guest.FirstName},

                 for your appointment

                 {treatment}
                 {when}
                 {location}

                 we ask you to save a card. If you do not come or cancel less than {deadlineHours} hours beforehand, we charge a no-show fee of {fee} {currency} ({percent} % of the price). Otherwise the card is not charged.

                 Agree and save a card:
                 {url}
                 """),
            "fr" => new(to, $"Merci d’enregistrer une carte pour {treatment}",
                $"""
                 Bonjour {guest.FirstName},

                 Pour votre rendez-vous

                 {treatment}
                 {when}
                 {location}

                 nous vous demandons d’enregistrer une carte. En cas d’absence ou d’annulation moins de {deadlineHours} heures à l’avance, nous facturons des frais d’annulation de {fee} {currency} ({percent} % du prix). Sinon, la carte n’est pas débitée.

                 Accepter et enregistrer une carte :
                 {url}
                 """),
            "it" => new(to, $"Registri una carta per {treatment}",
                $"""
                 Buongiorno {guest.FirstName},

                 per il suo appuntamento

                 {treatment}
                 {when}
                 {location}

                 le chiediamo di registrare una carta. In caso di mancata presentazione o di annullamento con meno di {deadlineHours} ore di anticipo addebitiamo una penale di {fee} {currency} ({percent} % del prezzo). Altrimenti la carta non viene addebitata.

                 Accettare e registrare una carta:
                 {url}
                 """),
            _ => new(to, $"Bitte Karte hinterlegen: {treatment}",
                $"""
                 Guten Tag {guest.GreetingName},

                 für Ihren Termin

                 {treatment}
                 {when} Uhr
                 {location}

                 bitten wir Sie, eine Karte zu hinterlegen. Wenn Sie nicht kommen oder weniger als {deadlineHours} Stunden vorher absagen, berechnen wir eine Ausfallgebühr von {fee} {currency} ({percent} % des Preises). Sonst wird die Karte nicht belastet.

                 Zustimmen und Karte hinterlegen:
                 {url}
                 """),
        };
    }

    /// <summary>Beleg zur belasteten Ausfallgebühr (Belegerteilung per Mail).</summary>
    public static OutgoingMail NoShowFeeCharged(
        string to, GuestName guest, string treatment, DateTimeOffset start, string timeZone, string location, decimal amount, string currency,
        string receipt, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var when = Format(start, timeZone, lang);
        var fee = amount.ToString("0.00", OnlineLanguage.Culture(lang));
        return lang switch
        {
            "en" => new(to, $"No-show fee for {treatment}",
                $"""
                 Hello {guest.FirstName},

                 as agreed when booking, we have charged the no-show fee of {fee} {currency} to your saved card for this appointment:

                 {treatment}
                 {when}
                 {location}

                 Receipt: {receipt}

                 If you have any questions, please contact us.
                 """),
            "fr" => new(to, $"Frais d’annulation pour {treatment}",
                $"""
                 Bonjour {guest.FirstName},

                 Comme convenu lors de la réservation, nous avons débité les frais d’annulation de {fee} {currency} sur votre carte enregistrée pour ce rendez-vous :

                 {treatment}
                 {when}
                 {location}

                 Quittance : {receipt}

                 Pour toute question, n’hésitez pas à nous contacter.
                 """),
            "it" => new(to, $"Penale per {treatment}",
                $"""
                 Buongiorno {guest.FirstName},

                 come concordato alla prenotazione, abbiamo addebitato la penale di {fee} {currency} sulla sua carta registrata per questo appuntamento:

                 {treatment}
                 {when}
                 {location}

                 Ricevuta: {receipt}

                 Per qualsiasi domanda non esiti a contattarci.
                 """),
            _ => new(to, $"Ausfallgebühr für {treatment}",
                $"""
                 Guten Tag {guest.GreetingName},

                 wie bei der Buchung vereinbart, haben wir die Ausfallgebühr von {fee} {currency} für diesen Termin von Ihrer hinterlegten Karte abgebucht:

                 {treatment}
                 {when} Uhr
                 {location}

                 Beleg: {receipt}

                 Bei Fragen melden Sie sich gerne bei uns.
                 """),
        };
    }

    /// <summary>Bestätigt die Erstattung einer Online-Zahlung, mit dem Stornobeleg (Belegerteilung per Mail).</summary>
    public static OutgoingMail PaymentRefunded(
        string to, GuestName guest, OnlinePaymentSource source, string location, decimal amount, string currency, string receipt, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var sum = amount.ToString("0.00", OnlineLanguage.Culture(lang));
        var what = (source, lang) switch
        {
            (OnlinePaymentSource.Voucher, "en") => "your gift voucher",
            (OnlinePaymentSource.Deposit, "en") => "your deposit",
            (_, "en") => "the no-show fee",
            (OnlinePaymentSource.Voucher, "fr") => "votre bon cadeau",
            (OnlinePaymentSource.Deposit, "fr") => "votre acompte",
            (_, "fr") => "les frais d’annulation",
            (OnlinePaymentSource.Voucher, "it") => "il suo buono regalo",
            (OnlinePaymentSource.Deposit, "it") => "il suo acconto",
            (_, "it") => "la penale",
            (OnlinePaymentSource.Voucher, _) => "Ihren Gutschein",
            (OnlinePaymentSource.Deposit, _) => "Ihre Anzahlung",
            _ => "die Ausfallgebühr",
        };
        return lang switch
        {
            "en" => new(to, $"Refund from {location}",
                $"""
                 Hello {guest.FirstName},

                 we have cancelled {what} and refunded {sum} {currency} to the original payment method. Depending on your bank, it may take a few days to appear.

                 Cancellation receipt: {receipt}

                 If you have any questions, please contact us.
                 """),
            "fr" => new(to, $"Remboursement de {location}",
                $"""
                 Bonjour {guest.FirstName},

                 Nous avons annulé {what} et remboursé {sum} {currency} sur le moyen de paiement d’origine. Selon votre banque, le crédit peut prendre quelques jours.

                 Quittance d’annulation : {receipt}

                 Pour toute question, n’hésitez pas à nous contacter.
                 """),
            "it" => new(to, $"Rimborso da {location}",
                $"""
                 Buongiorno {guest.FirstName},

                 abbiamo annullato {what} e rimborsato {sum} {currency} sul metodo di pagamento originale. A seconda della banca, l’accredito può richiedere alcuni giorni.

                 Ricevuta di storno: {receipt}

                 Per qualsiasi domanda non esiti a contattarci.
                 """),
            _ => new(to, $"Rückerstattung von {location}",
                $"""
                 Guten Tag {guest.GreetingName},

                 wir haben {what} storniert und {sum} {currency} auf das ursprüngliche Zahlungsmittel zurückerstattet. Je nach Bank dauert die Gutschrift einige Tage.

                 Stornobeleg: {receipt}

                 Bei Fragen melden Sie sich gerne bei uns.
                 """),
        };
    }

    public static OutgoingMail VoucherIssued(string to, GuestName guest, VoucherOrderDto order, string receipt, string bookUrl, string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var amount = order.Amount.ToString("0.00", OnlineLanguage.Culture(lang));
        var what = (order.TreatmentName, lang) switch
        {
            ({ } treatment, "en") => $"for \"{treatment}\"",
            ({ } treatment, "fr") => $"pour « {treatment} »",
            ({ } treatment, "it") => $"per «{treatment}»",
            ({ } treatment, _) => $"für „{treatment}“",
            (null, "en") => $"worth {amount} {order.Currency}",
            (null, "fr") => $"d’une valeur de {amount} {order.Currency}",
            (null, "it") => $"del valore di {amount} {order.Currency}",
            (null, _) => $"im Wert von {amount} {order.Currency}",
        };
        var recipient = order.RecipientName is { } name
            ? lang switch { "en" => $"For: {name}\n", "fr" => $"Pour : {name}\n", "it" => $"Per: {name}\n", _ => $"Für: {name}\n" }
            : "";
        var message = order.Message is { } text ? $"\n{text}\n" : "";
        var expiry = order.ValidUntil is { } until
            ? lang switch
            {
                "en" => $"Redeemable until: {until.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}\n",
                "fr" => $"Valable jusqu’au : {until.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}\n",
                "it" => $"Da utilizzare entro il: {until.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}\n",
                _ => $"Einzulösen bis {until.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}\n",
            }
            : "";
        return lang switch
        {
            "en" => new(to, $"Your gift voucher for {order.LocationName}",
                $"""
                 Hello {guest.FirstName},

                 thank you for your purchase. Here is your gift voucher {what} ({amount} {order.Currency} paid):

                 Voucher code: {order.Code}
                 {expiry}{recipient}{message}
                 Redeemable at {order.LocationName}, when booking at reception or on site.
                 Book an appointment: {bookUrl}

                 Receipt: {receipt}
                 """),
            "fr" => new(to, $"Votre bon cadeau pour {order.LocationName}",
                $"""
                 Bonjour {guest.FirstName},

                 Merci pour votre achat. Voici votre bon cadeau {what} ({amount} {order.Currency} payés) :

                 Code du bon : {order.Code}
                 {expiry}{recipient}{message}
                 Valable à {order.LocationName}, à présenter lors du paiement à la réception.
                 Réserver un rendez-vous : {bookUrl}

                 Quittance : {receipt}
                 """),
            "it" => new(to, $"Il suo buono regalo per {order.LocationName}",
                $"""
                 Buongiorno {guest.FirstName},

                 grazie per il suo acquisto. Ecco il suo buono regalo {what} ({amount} {order.Currency} pagati):

                 Codice del buono: {order.Code}
                 {expiry}{recipient}{message}
                 Valido presso {order.LocationName}, da presentare al momento del pagamento alla reception.
                 Prenotare un appuntamento: {bookUrl}

                 Ricevuta: {receipt}
                 """),
            _ => new(to, $"Ihr Gutschein für {order.LocationName}",
                $"""
                 Guten Tag {guest.GreetingName},

                 vielen Dank für Ihren Einkauf. Hier ist Ihr Gutschein {what} ({amount} {order.Currency} bezahlt):

                 Gutscheincode: {order.Code}
                 {expiry}{recipient}{message}
                 Einlösbar in {order.LocationName}, beim Bezahlen an der Rezeption.
                 Termin buchen: {bookUrl}

                 Beleg: {receipt}
                 """),
        };
    }

    private static string Range(WaitlistEntryDto entry, string lang)
    {
        var culture = OnlineLanguage.Culture(lang);
        var pattern = lang switch { "de" => "d. M. yyyy", "en" => "d MMM yyyy", _ => "d.M.yyyy" };
        var from = entry.From.ToString(pattern, culture);
        var until = entry.Until.ToString(pattern, culture);
        var part = (entry.DayPart, lang) switch
        {
            (DayPart.Morning, "en") => ", mornings",
            (DayPart.Morning, "fr") => ", le matin",
            (DayPart.Morning, "it") => ", al mattino",
            (DayPart.Morning, _) => ", vormittags",
            (DayPart.Afternoon, "en") => ", afternoons",
            (DayPart.Afternoon, "fr") => ", l’après-midi",
            (DayPart.Afternoon, "it") => ", al pomeriggio",
            (DayPart.Afternoon, _) => ", nachmittags",
            (DayPart.Evening, "en") => ", evenings",
            (DayPart.Evening, "fr") => ", le soir",
            (DayPart.Evening, "it") => ", la sera",
            (DayPart.Evening, _) => ", abends",
            _ => "",
        };
        // Italienisch ohne „dal/al“, weil vor 8 und 11 sonst „dall’/all’“ nötig wäre.
        return lang switch
        {
            "en" => $"{from} to {until}{part}",
            "fr" => $"du {from} au {until}{part}",
            "it" => $"nel periodo {from}–{until}{part}",
            _ => $"{from} bis {until}{part}",
        };
    }

    /// <summary>Wochentag, Datum und Uhrzeit in der Zeitzone des Standorts; „Uhr“ setzt der deutsche Text selbst dazu.</summary>
    private static string Format(DateTimeOffset utc, string timeZone, string lang)
    {
        var local = TimeZoneInfo.ConvertTime(utc, LocalTime.Zone(timeZone));
        var pattern = lang switch
        {
            "en" => "dddd, d MMMM yyyy, HH:mm",
            "fr" => "dddd d MMMM yyyy 'à' HH:mm",
            "it" => "dddd d MMMM yyyy 'alle' HH:mm",
            _ => "dddd, d. MMMM yyyy, HH:mm",
        };
        return local.ToString(pattern, OnlineLanguage.Culture(lang));
    }
}
