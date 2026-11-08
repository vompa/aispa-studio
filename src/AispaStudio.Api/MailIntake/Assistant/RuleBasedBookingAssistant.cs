using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>
/// Terminannahme ohne KI: erkennt Sprache, Behandlung, Datum und Uhrzeit mit festen Regeln und antwortet mit Textbausteinen.
/// Deterministisch und ohne Netz, deshalb die Implementierung für Tests und für Betrieb ohne API-Schlüssel.
/// Versteht einfache Mails („Massage am 12.03. um 10 Uhr“), keine freien Formulierungen wie „nach Ostern“.
/// </summary>
public sealed partial class RuleBasedBookingAssistant : IBookingAssistant
{
    private static readonly Dictionary<string, string[]> LanguageWords = new()
    {
        ["de"] = ["ich", "und", "termin", "bitte", "gerne", "möchte", "hätte", "uhr", "am", "für", "grüße", "hallo", "guten"],
        ["en"] = ["i", "would", "appointment", "please", "like", "the", "and", "book", "regards", "hello", "for", "at"],
        ["fr"] = ["je", "rendez-vous", "bonjour", "merci", "voudrais", "pour", "le", "la", "et", "cordialement", "à"],
        ["it"] = ["vorrei", "appuntamento", "buongiorno", "grazie", "per", "il", "alle", "e", "saluti", "prenotare"],
    };

    private static readonly string[] CancelWords = ["stornier", "storno", "absage", "abzusagen", "komme nicht", "cancel", "annuler", "annule", "disdire", "cancellare"];
    private static readonly string[] RescheduleWords = ["verschieb", "umbuch", "verlegen", "reschedule", "move my", "déplacer", "decaler", "décaler", "spostare"];
    private static readonly string[] BookingWords = ["termin", "buchen", "reserv", "appointment", "book", "rendez-vous", "prenot", "appuntamento"];

    private static readonly Dictionary<string, DayOfWeek> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["montag"] = DayOfWeek.Monday,
        ["dienstag"] = DayOfWeek.Tuesday,
        ["mittwoch"] = DayOfWeek.Wednesday,
        ["donnerstag"] = DayOfWeek.Thursday,
        ["freitag"] = DayOfWeek.Friday,
        ["samstag"] = DayOfWeek.Saturday,
        ["sonntag"] = DayOfWeek.Sunday,
        ["monday"] = DayOfWeek.Monday,
        ["tuesday"] = DayOfWeek.Tuesday,
        ["wednesday"] = DayOfWeek.Wednesday,
        ["thursday"] = DayOfWeek.Thursday,
        ["friday"] = DayOfWeek.Friday,
        ["saturday"] = DayOfWeek.Saturday,
        ["sunday"] = DayOfWeek.Sunday,
        ["lundi"] = DayOfWeek.Monday,
        ["mardi"] = DayOfWeek.Tuesday,
        ["mercredi"] = DayOfWeek.Wednesday,
        ["jeudi"] = DayOfWeek.Thursday,
        ["vendredi"] = DayOfWeek.Friday,
        ["samedi"] = DayOfWeek.Saturday,
        ["dimanche"] = DayOfWeek.Sunday,
        ["lunedì"] = DayOfWeek.Monday,
        ["martedì"] = DayOfWeek.Tuesday,
        ["mercoledì"] = DayOfWeek.Wednesday,
        ["giovedì"] = DayOfWeek.Thursday,
        ["venerdì"] = DayOfWeek.Friday,
        ["sabato"] = DayOfWeek.Saturday,
        ["domenica"] = DayOfWeek.Sunday,
    };

    private static readonly (string[] Words, int Days)[] RelativeDays =
    [
        (["übermorgen", "day after tomorrow", "après-demain", "dopodomani"], 2),
        (["morgen", "tomorrow", "demain", "domani"], 1),
        (["heute", "today", "aujourd'hui", "oggi"], 0),
    ];

    private static readonly (string[] Words, TimeOnly From, TimeOnly Until)[] DayParts =
    [
        (["vormittag", "morgens", "in der früh", "morning", "matin", "mattina", "mattino"], new(9, 0), new(12, 0)),
        // „nachmittag“ vor „mittag“, weil es das Wort enthält.
        (["nachmittag", "afternoon", "après-midi", "pomeriggio"], new(12, 0), new(18, 0)),
        (["mittag", "noon", "midi", "mezzogiorno"], new(11, 30), new(14, 0)),
        (["abend", "evening", "soir", "sera"], new(17, 0), new(21, 0)),
    ];

    private static readonly string[] Greetings =
    [
        "liebe grüße", "viele grüße", "beste grüße", "freundliche grüße", "mit freundlichen grüßen", "lg", "vg", "glg", "danke",
        "best regards", "kind regards", "regards", "best", "thanks", "thank you", "cheers",
        "cordialement", "bien à vous", "merci", "cordiali saluti", "saluti", "grazie", "distinti saluti",
    ];

    public Task<MailAnalysis> AnalyzeAsync(MailAnalysisRequest request, CancellationToken ct)
    {
        var text = $"{request.Subject}\n{request.Body}";
        var lower = text.ToLowerInvariant();
        var language = DetectLanguage(lower);

        var treatment = FindByName(lower, request.Treatments);
        var location = FindByName(lower, request.Locations);
        var employee = FindByName(lower, request.Employees);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.ReceivedAt, zone).DateTime);
        var dates = FindDates(lower, today);
        var (from, until) = FindTimes(lower);
        if (request.Earlier?.ToLowerInvariant() is { } earlier)
        {
            // Antwort auf einen Vorschlag oder Hinweis der Rezeption: was die neue Nachricht nicht sagt, gilt von vorher.
            treatment ??= FindByName(earlier, request.Treatments);
            location ??= FindByName(earlier, request.Locations);
            employee ??= FindByName(earlier, request.Employees);
            dates = dates.Count > 0 ? dates : FindDates(earlier, today);
            (from, until) = from is null && until is null ? FindTimes(earlier) : (from, until);
        }

        var wishes = dates.Select(d => new RequestedTime(d, from, until)).ToList();

        var intent = Contains(lower, CancelWords) ? MailIntent.Cancellation
            : Contains(lower, RescheduleWords) ? MailIntent.Reschedule
            : Contains(lower, BookingWords) || treatment is not null || wishes.Count > 0 ? MailIntent.BookingRequest
            : lower.Contains('?', StringComparison.Ordinal) ? MailIntent.Question
            : MailIntent.Other;

        var (firstName, lastName, salutation, titles) = FindName(request.Body, request.FromName);
        var phone = PhonePattern().Match(request.Body) is { Success: true } p ? p.Value.Trim() : null;

        var summary = intent switch
        {
            MailIntent.BookingRequest =>
                $"Terminwunsch: {treatment?.Name ?? "Behandlung unklar"}"
                + (wishes.Count > 0 ? $", {string.Join(", ", wishes.Select(Describe))}" : ", ohne Datum"),
            MailIntent.Cancellation => "Möchte einen Termin absagen.",
            MailIntent.Reschedule => "Möchte einen Termin verschieben.",
            MailIntent.Question => "Frage ohne Terminwunsch.",
            _ => "Kein Terminwunsch erkannt.",
        };

        return Task.FromResult(new MailAnalysis(
            intent, language, firstName, lastName, phone, treatment?.Id, location?.Id, employee?.Id, wishes, summary, salutation, titles));
    }

    public Task<ReplyDraft> DraftReplyAsync(ReplyRequest request, CancellationToken ct)
    {
        var language = GuestLanguage.Normalize(request.Language);
        var culture = CultureInfo.GetCultureInfo(language switch { "de" => "de-AT", "fr" => "fr-CH", "it" => "it-CH", _ => "en-GB" });
        var format = language switch
        {
            "de" => "dddd, d. MMMM yyyy, HH:mm",
            "en" => "dddd, d MMMM yyyy 'at' HH:mm",
            _ => "dddd d MMMM yyyy, HH:mm",
        };
        string When(DateTime t) => t.ToString(format, culture);
        var treatment = request.TreatmentName ?? "";
        var place = request.LocationName is null ? "" : $" ({request.LocationName})";
        var list = string.Join("\n", request.Alternatives.Select(a => $"- {When(a)}"));
        var missing = string.Join(", ", request.Missing);

        var (greeting, closing) = language switch
        {
            "de" => ($"Guten Tag {request.GuestName},", $"Herzliche Grüße\n{request.SpaName}"),
            "fr" => ($"Bonjour {request.GuestName},", $"Meilleures salutations\n{request.SpaName}"),
            "it" => ($"Buongiorno {request.GuestName},", $"Cordiali saluti\n{request.SpaName}"),
            _ => ($"Hello {request.GuestName},", $"Kind regards\n{request.SpaName}"),
        };

        var main = (language, request.Case) switch
        {
            ("de", ReplyCase.Booked) => $"vielen Dank für Ihre Anfrage. Wir haben Ihren Termin für {treatment}{place} am {When(request.Booked!.Value)} Uhr eingetragen. Wir freuen uns auf Sie!",
            ("de", ReplyCase.Proposed) => $"vielen Dank für Ihre Anfrage. Zu Ihrem Wunschtermin für {treatment}{place} können wir Ihnen folgende freie Zeiten anbieten:\n{list}\n\nAntworten Sie einfach mit der passenden Zeit, dann tragen wir Sie ein.",
            ("de", ReplyCase.NoAvailability) => $"vielen Dank für Ihre Anfrage. Für {treatment}{place} ist in den nächsten Tagen leider nichts frei. Unser Team meldet sich bei Ihnen mit einem Vorschlag.",
            ("de", ReplyCase.NeedsDetails) => $"vielen Dank für Ihre Nachricht. Damit wir Ihnen einen Termin anbieten können, brauchen wir noch: {missing}.",
            ("de", ReplyCase.Cancelled) => $"vielen Dank für Ihre Nachricht. Wir haben Ihren Termin für {treatment}{place} am {When(request.Booked!.Value)} Uhr storniert. Wir freuen uns, wenn Sie bald wieder buchen.",
            ("de", ReplyCase.Held) => $"vielen Dank für Ihre Anfrage. Für {treatment}{place} halten wir Ihnen diesen Termin frei: {When(request.Booked!.Value)} Uhr. Passt er Ihnen? Dann antworten Sie kurz, und der Termin ist fest gebucht.",
            ("de", _) => "vielen Dank für Ihre Nachricht. Wir haben sie erhalten und unser Team meldet sich in Kürze bei Ihnen.",
            ("fr", ReplyCase.Booked) => $"merci pour votre demande. Votre rendez-vous pour {treatment}{place} est réservé le {When(request.Booked!.Value)}. Nous nous réjouissons de vous accueillir !",
            ("fr", ReplyCase.Proposed) => $"merci pour votre demande. Pour {treatment}{place}, nous pouvons vous proposer les créneaux suivants :\n{list}\n\nRépondez simplement avec le créneau qui vous convient.",
            ("fr", ReplyCase.NoAvailability) => $"merci pour votre demande. Malheureusement, il n'y a pas de disponibilité pour {treatment}{place} dans les prochains jours. Notre équipe vous contactera.",
            ("fr", ReplyCase.NeedsDetails) => $"merci pour votre message. Pour vous proposer un rendez-vous, il nous manque : {missing}.",
            ("fr", ReplyCase.Cancelled) => $"merci pour votre message. Nous avons annulé votre rendez-vous pour {treatment}{place} du {When(request.Booked!.Value)}. Au plaisir de vous revoir bientôt.",
            ("fr", ReplyCase.Held) => $"merci pour votre demande. Pour {treatment}{place}, nous vous réservons ce créneau : {When(request.Booked!.Value)}. Vous convient-il ? Répondez simplement et le rendez-vous est confirmé.",
            ("fr", _) => "merci pour votre message. Nous l'avons bien reçu et notre équipe vous répondra rapidement.",
            ("it", ReplyCase.Booked) => $"grazie per la sua richiesta. Abbiamo prenotato il suo appuntamento per {treatment}{place} il {When(request.Booked!.Value)}. La aspettiamo!",
            ("it", ReplyCase.Proposed) => $"grazie per la sua richiesta. Per {treatment}{place} possiamo offrirle questi orari liberi:\n{list}\n\nRisponda semplicemente con l'orario che preferisce.",
            ("it", ReplyCase.NoAvailability) => $"grazie per la sua richiesta. Purtroppo nei prossimi giorni non ci sono orari liberi per {treatment}{place}. Il nostro team la contatterà.",
            ("it", ReplyCase.NeedsDetails) => $"grazie per il suo messaggio. Per proporle un appuntamento ci serve ancora: {missing}.",
            ("it", ReplyCase.Cancelled) => $"grazie per il suo messaggio. Abbiamo annullato il suo appuntamento per {treatment}{place} del {When(request.Booked!.Value)}. Speriamo di rivederla presto.",
            ("it", ReplyCase.Held) => $"grazie per la sua richiesta. Per {treatment}{place} le teniamo libero questo orario: {When(request.Booked!.Value)}. Le va bene? Risponda brevemente e l'appuntamento è confermato.",
            ("it", _) => "grazie per il suo messaggio. L'abbiamo ricevuto e il nostro team le risponderà a breve.",
            (_, ReplyCase.Booked) => $"thank you for your request. Your appointment for {treatment}{place} is booked for {When(request.Booked!.Value)}. We look forward to seeing you!",
            (_, ReplyCase.Proposed) => $"thank you for your request. For {treatment}{place} we can offer these free times:\n{list}\n\nJust reply with the time that suits you and we will book it.",
            (_, ReplyCase.NoAvailability) => $"thank you for your request. Unfortunately there is nothing free for {treatment}{place} in the coming days. Our team will get back to you.",
            (_, ReplyCase.NeedsDetails) => $"thank you for your message. To offer you an appointment we still need: {missing}.",
            (_, ReplyCase.Cancelled) => $"thank you for your message. We have cancelled your appointment for {treatment}{place} on {When(request.Booked!.Value)}. We hope to see you again soon.",
            (_, ReplyCase.Held) => $"thank you for your request. For {treatment}{place} we are holding this time for you: {When(request.Booked!.Value)}. Does it suit you? Just reply briefly and the appointment is confirmed.",
            _ => "thank you for your message. We have received it and our team will get back to you shortly.",
        };

        var subject = request.OriginalSubject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? request.OriginalSubject
            : $"Re: {request.OriginalSubject}";
        return Task.FromResult(request.ShortMessage
            ? new ReplyDraft(subject, $"{greeting} {main}\n{request.SpaName}")
            : new ReplyDraft(subject, $"{greeting}\n\n{main}\n\n{closing}"));
    }

    private static string Describe(RequestedTime w) =>
        w.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
        + (w.From is { } f ? w.Until is { } u ? $" {f:HH\\:mm}–{u:HH\\:mm}" : $" {f:HH\\:mm}" : "");

    private static string DetectLanguage(string lower)
    {
        var words = WordPattern().Matches(lower).Select(m => m.Value).ToList();
        return LanguageWords
            .Select(l => (l.Key, Score: words.Count(w => l.Value.Contains(w))))
            .OrderByDescending(l => l.Score)
            .ThenBy(l => l.Key == "de" ? 0 : 1)
            .First().Key;
    }

    private static bool Contains(string lower, string[] words) => words.Any(w => lower.Contains(w, StringComparison.Ordinal));

    /// <summary>Längster Name, der im Text vorkommt; sonst ein Name, dessen markantes Wort (ab 5 Buchstaben) vorkommt.</summary>
    private static NamedOption? FindByName(string lower, IReadOnlyList<NamedOption> options)
    {
        var exact = options
            .Where(o => lower.Contains(o.Name.ToLowerInvariant(), StringComparison.Ordinal))
            .MaxBy(o => o.Name.Length);
        if (exact is not null)
        {
            return exact;
        }

        var words = WordPattern().Matches(lower).Select(m => m.Value).ToHashSet();
        var partial = options
            .Select(o => (Option: o, Hits: WordPattern().Matches(o.Name.ToLowerInvariant())
                .Select(m => m.Value).Count(w => w.Length >= 5 && words.Contains(w))))
            .Where(o => o.Hits > 0)
            .OrderByDescending(o => o.Hits)
            .ToList();

        // Nur eindeutige Treffer: „Massage“ passt auf mehrere Massagen und bleibt dann offen.
        return partial.Count == 1 || (partial.Count > 1 && partial[0].Hits > partial[1].Hits) ? partial[0].Option : null;
    }

    private static List<DateOnly> FindDates(string lower, DateOnly today)
    {
        var dates = new List<DateOnly>();

        foreach (Match m in NumericDatePattern().Matches(lower))
        {
            var day = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
            var year = m.Groups["y"].Success ? int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture) : today.Year;
            year = year < 100 ? 2000 + year : year;
            if (month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
            {
                var date = new DateOnly(year, month, day);
                // Ohne Jahr und schon vorbei: nächstes Jahr gemeint.
                dates.Add(!m.Groups["y"].Success && date < today ? date.AddYears(1) : date);
            }
        }

        foreach (Match m in IsoDatePattern().Matches(lower))
        {
            if (DateOnly.TryParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                dates.Add(date);
            }
        }

        if (dates.Count == 0)
        {
            foreach (var (words, days) in RelativeDays)
            {
                if (words.Any(w => Regex.IsMatch(lower, $@"(?<![\p{{L}}]){Regex.Escape(w)}(?![\p{{L}}])")))
                {
                    dates.Add(today.AddDays(days));
                    break;
                }
            }
        }

        if (dates.Count == 0)
        {
            foreach (Match m in WordPattern().Matches(lower))
            {
                if (Weekdays.TryGetValue(m.Value, out var weekday))
                {
                    var offset = ((int)weekday - (int)today.DayOfWeek + 7) % 7;
                    dates.Add(today.AddDays(offset == 0 ? 7 : offset));
                }
            }
        }

        return [.. dates.Where(d => d >= today).Distinct().Order()];
    }

    internal static (TimeOnly? From, TimeOnly? Until) FindTimes(string lower)
    {
        if (TimeRangePattern().Match(lower) is { Success: true } range
            && ToTime(range.Groups["h1"].Value, range.Groups["m1"].Value) is { } from
            && ToTime(range.Groups["h2"].Value, range.Groups["m2"].Value) is { } until
            && from < until)
        {
            return (from, until);
        }

        // Uhrzeit mit Minuten („10:30“, „10.30 Uhr“) oder mit Signalwort („um 10“, „at 3 pm“, „10 Uhr“).
        foreach (var match in new[] { ClockPattern().Match(lower), HourPattern().Match(lower) })
        {
            if (match.Success && ToTime(match.Groups["h"].Value, match.Groups["m"].Value, match.Groups["ampm"].Value) is { } at)
            {
                return (at, null);
            }
        }

        foreach (var (words, partFrom, partUntil) in DayParts)
        {
            if (words.Any(w => lower.Contains(w, StringComparison.Ordinal)))
            {
                return (partFrom, partUntil);
            }
        }

        return (null, null);
    }

    private static TimeOnly? ToTime(string hours, string minutes, string ampm = "")
    {
        var h = int.Parse(hours, CultureInfo.InvariantCulture);
        var m = string.IsNullOrEmpty(minutes) ? 0 : int.Parse(minutes, CultureInfo.InvariantCulture);
        if (ampm == "pm" && h < 12)
        {
            h += 12;
        }
        else if (ampm == "am" && h == 12)
        {
            h = 0;
        }

        return h is >= 0 and <= 23 && m is >= 0 and <= 59 ? new TimeOnly(h, m) : null;
    }

    /// <summary>
    /// Name aus der Grußformel am Ende („Liebe Grüße\nEva Muster“ oder „Liebe Grüße, Eva Muster“), sonst aus dem Absendernamen.
    /// Frau/Herr und vorangestellte Titel („Dr. Stefanie Huber“) stehen nie im Namen, sondern als Vorschlag daneben.
    /// </summary>
    private static (string? First, string? Last, CustomerSalutation? Salutation, string? Titles) FindName(string body, string? fromName)
    {
        var lines = body.Split('\n').Select(l => l.Trim().TrimEnd(',', '!', '.')).Where(l => l.Length > 0).ToList();
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var comma = lines[i].IndexOf(',', StringComparison.Ordinal);
            var signature = Greetings.Contains(lines[i].ToLowerInvariant()) ? (i + 1 < lines.Count ? lines[i + 1] : null)
                : comma > 0 && Greetings.Contains(lines[i][..comma].ToLowerInvariant()) ? lines[i][(comma + 1)..].Trim()
                : null;
            if (signature is not null && Signed(signature, strict: true) is { } signed)
            {
                return signed;
            }
        }

        return string.IsNullOrWhiteSpace(fromName) ? (null, null, null, null) : Signed(fromName, strict: false) ?? (null, null, null, null);
    }

    private static (string? First, string? Last, CustomerSalutation? Salutation, string? Titles)? Signed(string signature, bool strict)
    {
        var words = signature.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        CustomerSalutation? salutation = words.Count > 1 ? words[0] switch
        {
            "Frau" => CustomerSalutation.Female,
            "Herr" => CustomerSalutation.Male,
            _ => null,
        } : null;
        var titles = PersonNames.LeadingTitles(string.Join(' ', words.Skip(salutation is null ? 0 : 1)));
        var name = PersonNames.WithoutTitles(string.Join(' ', words.Skip(salutation is null ? 0 : 1)));
        if (strict && !NameLine().IsMatch(name))
        {
            return null;
        }

        var (first, last) = Split(name);
        return (first, last, salutation, titles);
    }

    private static (string? First, string? Last) Split(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (string.Join(' ', parts[..^1]), parts[^1]),
        };
    }

    [GeneratedRegex(@"[\p{L}][\p{L}'-]*")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})\.(?<m>\d{1,2})\.(?<y>\d{2,4})?(?![\d:])")]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex IsoDatePattern();

    [GeneratedRegex(@"(?:\b(?:zwischen|von|between|from|entre|de|tra|dalle)\s+)?(?<![\d.:])(?<h1>\d{1,2})(?:[:.](?<m1>\d{2}))?\s*(?:uhr|h)?\s*(?:-|–|\bbis\b|\bund\b|\band\b|\bto\b|\bet\b|\bà\b|\ba\b|\balle\b)\s*(?<h2>\d{1,2})(?:[:.](?<m2>\d{2}))?\s*(?:uhr\b|h\b)")]
    private static partial Regex TimeRangePattern();

    [GeneratedRegex(@"(?<![\d.])(?<h>\d{1,2})[:.](?<m>\d{2})(?!\d|\.\d)\s*(?<ampm>am|pm)?")]
    private static partial Regex ClockPattern();

    [GeneratedRegex(@"(?:\b(?:um|at|à|alle|ore|gegen)\s+(?<h>\d{1,2})\s*(?<ampm>am|pm)?(?:\s*(?:uhr|h|o'clock))?\b)|(?:\b(?<h>\d{1,2})\s*(?:(?<ampm>am|pm)|uhr|h)\b)")]
    private static partial Regex HourPattern();

    [GeneratedRegex(@"\+?\d[\d /-]{6,}\d")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^\p{Lu}[\p{L}'-]+(?: \p{Lu}[\p{L}'-]+){0,3}$")]
    private static partial Regex NameLine();
}
