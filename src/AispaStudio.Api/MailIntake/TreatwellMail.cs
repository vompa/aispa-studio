using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Contracts;

namespace AispaStudio.Api.MailIntake;

/// <summary>Eine Treatwell-Benachrichtigung, zerlegt in ihre Felder. Datum und Uhrzeit in Ortszeit des Standorts.</summary>
public sealed record TreatwellBooking(
    InboundMailKind Kind,
    string Reference,
    DateOnly? Date,
    TimeOnly? Time,
    string? Service,
    string? Staff,
    string? Customer,
    string? Email,
    string? Phone,
    string? Venue,
    PlatformPayment Payment = PlatformPayment.None,
    decimal? Total = null,
    int ServiceCount = 1);

/// <summary>
/// Liest Buchungs-, Änderungs- und Stornomails von Treatwell (Deutsch und Englisch). Treatwell hat keine offene API;
/// so übernehmen auch andere Anbieter die Buchungen, bis ein Partnervertrag Echtzeit erlaubt.
/// Erwartet Zeilen der Form „Bezeichnung: Wert“; HTML-Mails werden vorher in Text umgewandelt (<see cref="PlainText"/>). Das genaue
/// Format echter Mails ist nicht öffentlich und muss mit Beispielen aus einem Partnerkonto nachgeschärft werden; die Bezeichnungen stehen
/// deshalb gesammelt in <see cref="Labels"/>. Dies ist der Rückfall (ADR 0062), wenn die KI die Mail nicht lesen kann.
/// </summary>
public static partial class TreatwellMail
{
    private static readonly Dictionary<string, string[]> Labels = new()
    {
        ["reference"] = ["buchungsnummer", "buchungsreferenz", "buchungs-id", "buchungs-nr", "referenz", "referenznummer", "ref", "bestellnummer",
            "booking reference", "booking number", "booking id", "reference", "order number"],
        ["date"] = ["datum", "termin", "datum & uhrzeit", "datum und uhrzeit", "date", "appointment date", "date & time", "date and time"],
        ["time"] = ["uhrzeit", "zeit", "beginn", "time", "start time", "start"],
        ["service"] = ["leistung", "behandlung", "dienstleistung", "service", "treatment"],
        ["staff"] = ["mitarbeiter", "mitarbeiterin", "mitarbeiter/in", "mitarbeiter:in", "therapeut", "therapeutin", "staff", "staff member", "stylist", "therapist"],
        ["customer"] = ["kunde", "kundin", "kunde/kundin", "kund:in", "name", "client", "customer", "client name"],
        ["email"] = ["e-mail", "email", "e-mail-adresse"],
        ["phone"] = ["telefon", "telefonnummer", "handy", "phone", "mobile", "phone number"],
        ["venue"] = ["salon", "standort", "filiale", "venue", "location"],
        ["payment"] = ["zahlungsstatus", "zahlung", "status", "bezahlung", "zahlungsart", "payment", "payment status", "payment method"],
        ["total"] = ["gesamtbetrag", "gesamtpreis", "gesamt", "summe", "zu kassieren", "zu zahlen", "betrag", "preis", "total", "total price", "amount", "amount due"],
    };

    private static readonly string[] OnSiteWords = ["im salon", "im spa", "im studio", "vor ort", "barzahlung", "nicht bezahlt", "unbezahlt", "noch offen", "noch zu zahlen",
        "pay at", "pay in", "pay on", "on site", "on-site", "at the salon", "in salon", "to be paid", "unpaid", "not paid"];

    private static readonly string[] PrepaidWords = ["bezahlt", "treatwell pay", "vorab", "vorauszahlung", "prepaid", "paid", "pre-paid"];

    private static readonly string[] CancelWords = ["storniert", "stornierung", "abgesagt", "absage", "cancelled", "canceled", "cancellation"];
    private static readonly string[] ChangeWords = ["geändert", "änderung", "verschoben", "umgebucht", "rescheduled", "changed", "amended", "updated"];
    private static readonly CultureInfo[] Cultures = [CultureInfo.GetCultureInfo("de-AT"), CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("en-GB")];

    /// <summary>Text der Mail: HTML (Tabellenzellen, Listen, Absätze) wird zu Zeilen „Bezeichnung: Wert“, Skripte und Formatvorlagen fallen weg; Text bleibt unverändert.</summary>
    public static string PlainText(string body) => LooksLikeHtml().IsMatch(body) ? Mailboxes.MimeMail.HtmlToText(body) : body;

    /// <summary>Zerlegt die Mail; <c>null</c>, wenn keine Buchungsnummer zu finden ist.</summary>
    public static TreatwellBooking? Parse(string subject, string body)
    {
        body = PlainText(body);
        var fields = new Dictionary<string, string>();
        var payments = new List<string>();
        foreach (var line in body.Split('\n'))
        {
            if (FieldLine().Match(line) is not { Success: true } m)
            {
                continue;
            }

            var label = m.Groups["label"].Value.Trim().ToLowerInvariant();
            var key = Labels.FirstOrDefault(l => l.Value.Contains(label)).Key;
            if (key == "payment")
            {
                payments.Add(m.Groups["value"].Value.Trim());
            }
            else if (key is not null && !fields.ContainsKey(key))
            {
                fields[key] = m.Groups["value"].Value.Trim();
            }
        }

        if (!fields.TryGetValue("reference", out var reference) || reference.Length == 0)
        {
            return null;
        }

        var kind = KindOf(subject, body);

        var date = fields.TryGetValue("date", out var d) ? ParseDate(d) : null;
        var time = fields.TryGetValue("time", out var t) ? ParseTime(t) : null;
        // Manche Mails schreiben Datum und Uhrzeit in eine Zeile („Termin: 01.03.2027, 10:00 Uhr“).
        time ??= d is null ? null : ParseTime(d);

        return new TreatwellBooking(
            kind,
            reference.TrimStart('#').Trim(),
            date,
            time,
            fields.GetValueOrDefault("service"),
            fields.GetValueOrDefault("staff"),
            fields.GetValueOrDefault("customer"),
            fields.GetValueOrDefault("email"),
            fields.GetValueOrDefault("phone"),
            fields.GetValueOrDefault("venue"),
            payments.Select(PaymentOf).FirstOrDefault(p => p != PlatformPayment.None),
            fields.TryGetValue("total", out var total) ? ParseMoney(total) : null);
    }

    /// <summary>Neu, geändert oder abgesagt: der Betreff entscheidet zuerst, sonst der Text.</summary>
    public static InboundMailKind KindOf(string subject, string body)
    {
        static InboundMailKind? Of(string text) =>
            CancelWords.Any(w => text.Contains(w, StringComparison.Ordinal)) ? InboundMailKind.TreatwellCancellation
            : ChangeWords.Any(w => text.Contains(w, StringComparison.Ordinal)) ? InboundMailKind.TreatwellChange
            : null;

        return Of(subject.ToLowerInvariant()) ?? Of(PlainText(body).ToLowerInvariant()) ?? InboundMailKind.TreatwellBooking;
    }

    /// <summary>„Online bezahlt (Treatwell Pay)“ ist vorab bezahlt, „Zahlung im Salon“ wird vor Ort bezahlt; alles andere bleibt unbekannt.</summary>
    public static PlatformPayment PaymentOf(string? value)
    {
        var text = value?.ToLowerInvariant() ?? "";
        return OnSiteWords.Any(w => text.Contains(w, StringComparison.Ordinal)) || BareWord().IsMatch(text) ? PlatformPayment.OnSite
            : PrepaidWords.Any(w => text.Contains(w, StringComparison.Ordinal)) ? PlatformPayment.Prepaid
            : PlatformPayment.None;
    }

    /// <summary>Betrag aus „35,00 €“, „€35.00“, „EUR 1.234,50“; <c>null</c>, wenn keiner zu lesen ist.</summary>
    public static decimal? ParseMoney(string? value)
    {
        if (value is null || Amount().Match(value) is not { Success: true } m)
        {
            return null;
        }

        var text = m.Value.Replace(" ", "", StringComparison.Ordinal);
        var last = text.LastIndexOfAny([',', '.']);
        if (last >= 0)
        {
            var decimals = text.Length - last - 1;
            var integer = text[..last].Replace(".", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal);
            text = decimals is >= 1 and <= 2 ? $"{integer}.{text[(last + 1)..]}" : integer + text[(last + 1)..];
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var money) ? money : null;
    }

    private static DateOnly? ParseDate(string value)
    {
        if (NumericDate().Match(value) is { Success: true } n
            && DateOnly.TryParseExact(n.Value, ["d.M.yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var numeric))
        {
            return numeric;
        }

        // „Montag, 1. März 2027“ oder „Monday, 1 March 2027“: Wochentag und Uhrzeit abschneiden.
        var cleaned = WeekdayPrefix().Replace(value, "");
        cleaned = TimeSuffix().Replace(cleaned, "").Trim().TrimEnd(',');
        foreach (var culture in Cultures)
        {
            if (DateTime.TryParse(cleaned, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                return DateOnly.FromDateTime(parsed);
            }
        }

        return null;
    }

    private static TimeOnly? ParseTime(string value) =>
        Clock().Match(value) is { Success: true } m
        && int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture) is var h and <= 23
        && int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) is var min and <= 59
            ? new TimeOnly(h, min)
            : null;

    [GeneratedRegex(@"^\s*\**\s*(?<label>[\p{L}][\p{L} /:\-&]{1,30}?)\s*\**\s*:\s*\**\s*(?<value>.+?)\s*$")]
    private static partial Regex FieldLine();

    [GeneratedRegex(@"</?\s*(html|body|table|tr|td|p|div|br|ul|li|span|strong|a)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LooksLikeHtml();

    [GeneratedRegex(@"\bbar\b")]
    private static partial Regex BareWord();

    [GeneratedRegex(@"\d[\d.,\s]*\d|\d")]
    private static partial Regex Amount();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}|\d{1,2}[./]\d{1,2}[./]\d{4}")]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"^\s*[\p{L}]+,\s*")]
    private static partial Regex WeekdayPrefix();

    [GeneratedRegex(@"(?:,|\bum\b|\bat\b)?\s*\d{1,2}[:.]\d{2}.*$")]
    private static partial Regex TimeSuffix();

    [GeneratedRegex(@"(?<!\d)(?<h>\d{1,2})[:.](?<m>\d{2})(?!\d|\.\d)")]
    private static partial Regex Clock();
}
