using AispaStudio.Contracts;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>
/// Adressen der vorab eingetragenen Abos und die Quellenvorschläge der Verwaltung (ADR 0075). Der Betrieb bestätigt die Quelle oder tauscht sie.
/// <see cref="CheckedOn"/> ist der Tag, an dem die Adressen abgerufen und gelesen wurden (22.10.2026); die Anbieter können sie ändern, ein Fehler zeigt sich dann
/// als Meldung „Abo … konnte nicht abgerufen werden“.
/// </summary>
internal static class CalendarFeedSources
{
    public static readonly DateOnly CheckedOn = new(2026, 10, 22);

    /// <summary>
    /// Öffentliche Feiertagskalender von Google, je Land. Quelle: calendar.google.com (öffentlich, ohne Anmeldung). Geprüft am 22.10.2026: Österreich 297,
    /// Deutschland 462 und Schweiz 325 Einträge für 2021 bis 2031. Beschreibung „Gesetzlicher Feiertag“, „Feiertag in …“ (nur dort) oder „Gedenktag“.
    /// </summary>
    public const string GoogleAustria = "https://calendar.google.com/calendar/ical/de.austrian%23holiday%40group.v.calendar.google.com/public/basic.ics";
    public const string GoogleGermany = "https://calendar.google.com/calendar/ical/de.german%23holiday%40group.v.calendar.google.com/public/basic.ics";
    public const string GoogleSwitzerland = "https://calendar.google.com/calendar/ical/de.ch%23holiday%40group.v.calendar.google.com/public/basic.ics";

    /// <summary>
    /// OpenHolidays (openholidaysapi.org, öffentlich, ohne Anmeldung) liefert iCalendar, wenn der Abruf <c>Accept: text/calendar</c> sendet (das tut der Abruf).
    /// Der Zeitraum steht in der Adresse und gehört jährlich im November fortgeschrieben (Erinnerung). Geprüft am 22.10.2026 für Wien (AT-9): 16 Ferientermine 2026 bis 2028.
    /// </summary>
    public const string SchoolHolidaysPattern = "https://openholidaysapi.org/SchoolHolidays?countryIsoCode={LAND}&subdivisionCode={REGION}&languageIsoCode=DE&validFrom={VON}&validTo={BIS}";

    public static string SchoolHolidays(string country, string subdivision, int fromYear, int untilYear) =>
        SchoolHolidaysPattern.Replace("{LAND}", country, StringComparison.Ordinal).Replace("{REGION}", subdivision, StringComparison.Ordinal)
            .Replace("{VON}", $"{fromYear}-01-01", StringComparison.Ordinal).Replace("{BIS}", $"{untilYear}-12-31", StringComparison.Ordinal);

    /// <summary>Die drei vorab eingetragenen Feiertags-Abos: (Schlüssel = Land, Name, Adresse).</summary>
    public static readonly IReadOnlyList<(string Country, string Name, string Url)> Presets =
    [
        ("AT", "Feiertage Österreich", GoogleAustria),
        ("DE", "Feiertage Deutschland", GoogleGermany),
        ("CH", "Feiertage Schweiz", GoogleSwitzerland),
    ];

    /// <summary>Vorschläge für die Auswahlliste der Maske, je Land und Region. Schulferien und Hinweise sind Muster, die der Betrieb bestätigt.</summary>
    public static IReadOnlyList<SubscriptionSource> Suggestions(int year) =>
    [
        new("google-at", "Feiertage Österreich (Google)", GoogleAustria, SubscriptionKind.PublicHoliday, "AT",
            "Öffentlicher Kalender von Google; gesetzliche Feiertage und Gedenktage, die Gedenktage zeigt AISPA nur im Hinweis.", CheckedOn),
        new("google-de", "Feiertage Deutschland (Google)", GoogleGermany, SubscriptionKind.PublicHoliday, "DE",
            "Öffentlicher Kalender von Google; regionale Feiertage gelten nur im jeweiligen Bundesland des Standorts.", CheckedOn),
        new("google-ch", "Feiertage Schweiz (Google)", GoogleSwitzerland, SubscriptionKind.PublicHoliday, "CH",
            "Öffentlicher Kalender von Google; kantonale Feiertage gelten nur im Kanton des Standorts (Region am Standort eintragen).", CheckedOn),
        new("feiertagskalender-ch", "Feiertage Schweiz nach Kanton (feiertagskalender.ch)", "https://www.feiertagskalender.ch/",
            SubscriptionKind.PublicHoliday, "CH-ZH",
            "Muster: Die Seite verlangt eine Sicherheitsprüfung, ein automatischer Abruf ist dort nicht vorgesehen. Die ICS-Adresse des Kantons auf der Seite holen und hier eintragen oder die Datei importieren; die Quelle bestätigt der Betrieb.",
            CheckedOn),
        new("schulferien-at-wien", "Schulferien Wien (OpenHolidays)", SchoolHolidays("AT", "AT-9", year, year + 2), SubscriptionKind.SchoolHoliday, "AT-9",
            "Öffentliche Schnittstelle openholidaysapi.org; das Bundesland (subdivisionCode AT-1 bis AT-9) und den Zeitraum in der Adresse anpassen.", CheckedOn),
        new("schulferien-de-muster", "Schulferien Bundesland Deutschland (OpenHolidays, Muster)", SchoolHolidays("DE", "DE-BY", year, year + 2),
            SubscriptionKind.SchoolHoliday, "DE-BY",
            "Muster für Bayern; für andere Bundesländer den subdivisionCode ändern (DE-BW, DE-NW …). Der Betrieb bestätigt die Quelle.", CheckedOn),
        new("schulferien-ch-muster", "Schulferien Kanton Schweiz (OpenHolidays, Muster)", SchoolHolidays("CH", "CH-ZH", year, year + 2),
            SubscriptionKind.SchoolHoliday, "CH-ZH",
            "Muster für Zürich; für andere Kantone den subdivisionCode ändern. Der Betrieb bestätigt die Quelle.", CheckedOn),
    ];
}
