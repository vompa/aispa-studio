using System.Globalization;
using System.Text;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Importing;

/// <summary>
/// Vorschläge der Zuordnung nach Spaltenköpfen und das Lesen einzelner Werte (Export/Import, Aufgabe E). Kopfnamen deutsch und englisch, dazu die belegten Köpfe des Kunden-Exports
/// von Shore (<c>first_name, last_name, gender, email, mobile, birthday, adress_street, adress_postal_code, adress_city</c>, help.shore.com, abgerufen 09.11.2026). Für Treatwell und
/// Phorest sind keine Köpfe öffentlich belegt; deren Spalten ordnet man von Hand zu.
/// </summary>
public static class CustomerImportFields
{
    private static readonly Dictionary<string, CustomerImportField> Headers = Build(
        (CustomerImportField.FirstName, ["vorname", "firstname", "givenname", "forename"]),
        (CustomerImportField.LastName, ["nachname", "familienname", "zuname", "lastname", "surname", "familyname"]),
        (CustomerImportField.Email, ["email", "emailadresse", "mail", "mailadresse", "emailaddress"]),
        (CustomerImportField.Phone, ["telefon", "telefonnummer", "tel", "handy", "handynummer", "mobil", "mobilnummer", "mobiltelefon", "phone", "phonenumber", "mobile",
            "mobilephone", "mobilenumber", "cellphone"]),
        (CustomerImportField.BirthDate, ["geburtstag", "geburtsdatum", "birthday", "birthdate", "dateofbirth", "dob"]),
        (CustomerImportField.Salutation, ["anrede", "geschlecht", "gender", "salutation"]),
        (CustomerImportField.Street, ["strasse", "adresse", "anschrift", "street", "address", "adressstreet", "addressstreet"]),
        (CustomerImportField.PostalCode, ["plz", "postleitzahl", "zip", "zipcode", "postalcode", "postcode", "adresspostalcode", "addresspostalcode"]),
        (CustomerImportField.City, ["ort", "stadt", "wohnort", "city", "town", "adresscity", "addresscity"]),
        (CustomerImportField.Country, ["land", "country"]),
        (CustomerImportField.MarketingConsent, ["newsletter", "werbung", "werbeeinwilligung", "einwilligung", "marketing", "marketingeinwilligung", "optin",
            "newsletteroptin", "emailmarketing", "acceptsmarketing"]));

    private static readonly HashSet<string> Yes = ["ja", "j", "yes", "y", "x", "1", "true", "wahr"];

    // Punkt heißt immer Tag zuerst, Jahr zuerst ist eindeutig; nur beim Schrägstrich entscheidet das Format, ob der Tag oder der Monat vorne steht.
    private static readonly string[] Dotted = ["d.M.yyyy", "dd.MM.yyyy", "d.M.yy", "dd.MM.yy"];
    private static readonly string[] DaySlash = ["d/M/yyyy", "dd/MM/yyyy"];
    private static readonly string[] YearFirst = ["yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd"];
    private static readonly string[] MonthSlash = ["M/d/yyyy", "MM/dd/yyyy", "M/d/yy"];

    /// <summary>Das Feld zum Spaltenkopf, oder <c>null</c>, wenn der Kopf keinem bekannten Namen entspricht.</summary>
    public static CustomerImportField? Suggest(string header) => Headers.TryGetValue(Key(header), out var field) ? field : null;

    /// <summary>Vorschlag für die ganze Kopfzeile; jedes Feld höchstens einmal (die erste passende Spalte gewinnt).</summary>
    public static List<ImportColumnMapping> SuggestAll(IReadOnlyList<string> headers)
    {
        var used = new HashSet<CustomerImportField>();
        var mapping = new List<ImportColumnMapping>();
        for (var i = 0; i < headers.Count; i++)
        {
            if (Suggest(headers[i]) is { } field && used.Add(field))
            {
                mapping.Add(new ImportColumnMapping(i, field));
            }
        }

        return mapping;
    }

    /// <summary>Erkennt das Datumsformat an den Werten: Punkt oder Schrägstrich mit Tag zuerst, Bindestrich mit Jahr zuerst; Monat zuerst nur, wenn es nicht anders geht.</summary>
    public static ImportDateFormat Detect(IEnumerable<string> values)
    {
        var filled = values.Where(v => v.Length > 0).Take(200).ToList();
        if (filled.Count == 0)
        {
            return ImportDateFormat.DayMonthYear;
        }

        return new[] { ImportDateFormat.DayMonthYear, ImportDateFormat.YearMonthDay, ImportDateFormat.MonthDayYear }
            .OrderByDescending(format => filled.Count(v => TryParse(v, Patterns(format, strict: true)) is not null)).First();
    }

    /// <summary>Liest ein Datum im gewählten Format; TT.MM.JJJJ und JJJJ-MM-TT gelten immer (so stehen Datumszellen aus Excel im <see cref="ImportSheet"/>).</summary>
    public static DateOnly? ParseDate(string value, ImportDateFormat format) => TryParse(value, Patterns(format, strict: false));

    /// <summary>Streng (zum Erkennen) nur die Schreibweisen des Formats; zum Lesen dazu alle eindeutigen (Punkt, Jahr zuerst), damit gemischte Spalten nicht scheitern.</summary>
    private static string[] Patterns(ImportDateFormat format, bool strict) => (format, strict) switch
    {
        (ImportDateFormat.YearMonthDay, true) => YearFirst,
        (ImportDateFormat.MonthDayYear, true) => MonthSlash,
        (_, true) => [.. Dotted, .. DaySlash],
        (ImportDateFormat.MonthDayYear, false) => [.. Dotted, .. YearFirst, .. MonthSlash],
        _ => [.. Dotted, .. YearFirst, .. DaySlash],
    };

    private static DateOnly? TryParse(string value, string[] patterns)
    {
        // Uhrzeit hinter dem Datum (Excel-Export „31.12.1988 00:00“) zählt nicht.
        var date = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return DateOnly.TryParseExact(date, patterns, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    /// <summary>„Frau“, „w“, „weiblich“, „female“, „f“ ergeben Frau usw.; Unbekanntes bleibt ohne Anrede.</summary>
    public static CustomerSalutation Salutation(string value) => Key(value) switch
    {
        "frau" or "w" or "weiblich" or "female" or "f" or "ms" or "mrs" or "miss" => CustomerSalutation.Female,
        "herr" or "m" or "mannlich" or "maennlich" or "male" or "mr" => CustomerSalutation.Male,
        "divers" or "d" or "diverse" or "x" or "nonbinary" => CustomerSalutation.Diverse,
        _ => CustomerSalutation.None,
    };

    public static bool IsYes(string value) => Yes.Contains(value.Trim().ToLowerInvariant());

    /// <summary>Klein, ohne Leerzeichen, Binde- und Unterstriche, Umlaute ausgeschrieben: „E-Mail-Adresse“ und „email_address“ werden vergleichbar.</summary>
    private static string Key(string header)
    {
        var text = new StringBuilder();
        foreach (var c in header.Trim().ToLowerInvariant())
        {
            text.Append(c switch
            {
                'ä' => "ae",
                'ö' => "oe",
                'ü' => "ue",
                'ß' => "ss",
                _ when char.IsLetterOrDigit(c) => c.ToString(),
                _ => "",
            });
        }

        return text.ToString();
    }

    private static Dictionary<string, CustomerImportField> Build(params (CustomerImportField Field, string[] Names)[] entries) =>
        entries.SelectMany(e => e.Names.Select(n => (n, e.Field))).ToDictionary(e => e.n, e => e.Field);
}
