namespace AispaStudio.Contracts;

/// <summary>
/// Art eines Kalender-Abos (ADR 0075): <c>PublicHoliday</c> gesetzliche Feiertage (wirken wie die Regel-Feiertage),
/// <c>SchoolHoliday</c> Schulferien, <c>Notice</c> Hinweise (Messe, Straßensperre), <c>Other</c> Sonstiges. Die drei letzten sind nur zum Anzeigen da.
/// </summary>
public enum SubscriptionKind
{
    PublicHoliday,
    SchoolHoliday,
    Notice,
    Other,
}

/// <summary>Ergebnis des letzten Abrufs. <c>Never</c>: noch nie abgerufen (oder nur Datei, ohne Adresse).</summary>
public enum SubscriptionFetchResult
{
    Never,
    Ok,
    Failed,
}

/// <summary>Entscheidung der Leitung zu einem Widerspruch zwischen Abo und Regel-Feiertagen: übernehmen oder ignorieren.</summary>
public enum SubscriptionDecision
{
    Accepted,
    Ignored,
}

/// <summary>Worin sich Abo und Regel-Feiertage unterscheiden: der Tag steht nur im Abo oder nur in den Regeln.</summary>
public enum SubscriptionConflictKind
{
    OnlySubscription,
    OnlyRules,
}

/// <summary>Art einer Meldung zu Abos: Änderung nach dem Abruf, Fehler beim Abruf, Erinnerung im November, „Falsch melden“ einer Person.</summary>
public enum SubscriptionNoticeKind
{
    Changed,
    Failed,
    Reminder,
    Report,
}

/// <summary>
/// Abo anlegen oder ändern. <c>Url</c>: <c>https://</c> oder <c>webcal://</c>, ohne Anmeldedaten; leer heißt „nur Datei“ (Import statt Abruf).
/// <c>Region</c>: Land („AT“) oder Region („CH-ZH“) nach ISO 3166, leer für alle. <c>LocationId</c> leer: gilt für alle Standorte, bei denen die Region passt.
/// <c>IsActive</c> ändert nur, wer den Standort verwaltet (<c>ManageLocation</c>).
/// </summary>
public sealed record CalendarSubscriptionInput(string Name, string Url, SubscriptionKind Kind, string? Region, Guid? LocationId, bool IsActive);

/// <summary>
/// Ein Abo mit Stand des Abrufs. <c>ShowInCalendar</c> gilt nur für den abfragenden Zugang (Anzeige im Kalender, ändert nichts an Buchung und Öffnungszeiten).
/// <c>IsPreset</c>: vorab eingetragenes Abo (Feiertage nach Land), lässt sich ändern und ausschalten, nicht löschen.
/// <c>OpenConflicts</c>: Tage, die laut Abo und laut Regel-Feiertagen verschieden sind und noch keine Entscheidung haben.
/// </summary>
public sealed record CalendarSubscriptionDto(
    Guid Id,
    string Name,
    string Url,
    SubscriptionKind Kind,
    string? Region,
    Guid? LocationId,
    bool IsActive,
    bool IsPreset,
    bool ShowInCalendar,
    DateTimeOffset? LastFetchAt,
    SubscriptionFetchResult LastResult,
    string? LastError,
    DateTimeOffset? NextCheckAt,
    int DayCount,
    DateOnly? FirstDate,
    DateOnly? LastDate,
    int OpenConflicts);

/// <summary>Abo ein- oder ausschalten (Abruf und Wirkung); nur mit <c>ManageLocation</c>.</summary>
public sealed record SubscriptionActiveInput(bool IsActive);

/// <summary>„Im Kalender zeigen“ für den eigenen Zugang.</summary>
public sealed record SubscriptionShowInput(bool Show);

/// <summary>Quellenvorschlag je Land oder Region; der Betrieb bestätigt oder tauscht die Quelle. <c>CheckedOn</c>: Tag der Prüfung der Adresse.</summary>
public sealed record SubscriptionSource(string Id, string Label, string Url, SubscriptionKind Kind, string? Region, string Note, DateOnly CheckedOn);

/// <summary>Ein Widerspruch zwischen Abo und Regel-Feiertagen, mit den betroffenen Standorten und der Entscheidung (leer: offen).</summary>
public sealed record SubscriptionConflictDto(
    DateOnly Date,
    string Name,
    SubscriptionConflictKind Kind,
    string? RuleName,
    IReadOnlyList<string> Locations,
    SubscriptionDecision? Decision);

/// <summary>Entscheidung zu einem Tag; <c>Decision</c> leer nimmt die Entscheidung zurück.</summary>
public sealed record SubscriptionDecisionInput(DateOnly Date, SubscriptionDecision? Decision);

/// <summary>Datei zum Import (ICS oder CSV „Datum;Titel“ bzw. „Von;Bis;Titel“) als Text; höchstens 2 MB.</summary>
public sealed record SubscriptionImportInput(string FileName, string Content);

/// <summary>Ergebnis eines Imports oder Abrufs: wie viele Tage neu, entfernt und insgesamt gespeichert sind; <c>Message</c> ist der Satz für die Rückmeldung.</summary>
public sealed record SubscriptionRefreshResult(int Added, int Removed, int Days, string Message, SubscriptionFetchResult Result);

/// <summary>Meldung zu Abos für die Seitenleiste „Meldungen“; <c>CanRetry</c> bei einem Fehler des Abrufs („Erneut versuchen“).</summary>
public sealed record SubscriptionNoticeDto(
    Guid Id,
    Guid? SubscriptionId,
    Guid? LocationId,
    SubscriptionNoticeKind Kind,
    string Title,
    string Text,
    DateTimeOffset CreatedAt,
    bool CanRetry);

/// <summary>„Falsch melden“: ein Tag eines Abos, der nicht stimmt, mit Grund; geht als Meldung an die Leitung.</summary>
public sealed record SubscriptionReportInput(DateOnly Date, string Reason, Guid? LocationId = null);

/// <summary>
/// Ein Tagesfakt im Kalenderkopf: Feiertag (aus den Regeln oder einem Abo), Schulferien, Hinweis. <c>From</c> und <c>Until</c> sind der Zeitraum des Eintrags
/// (für „Herbstferien bis 2.11.“), <c>Source</c> sagt woher („Abo ‚Feiertage Salzburg‘“ oder „Feiertagsregeln Österreich“), <c>AsOf</c> den Stand des Abrufs.
/// </summary>
public sealed record CalendarDayFact(
    Guid? SubscriptionId,
    SubscriptionKind Kind,
    string Title,
    DateOnly From,
    DateOnly Until,
    string Source,
    DateTimeOffset? AsOf);
