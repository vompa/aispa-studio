using AispaStudio.Contracts;

namespace AispaStudio.Api.CalendarSubscriptions;

/// <summary>
/// Ein Kalender-Abo (ADR 0075): eine ICS-Adresse mit gesetzlichen Feiertagen, Schulferien, Hinweisen oder Sonstigem. Der Server holt die Adresse im Hintergrund,
/// speichert die Tage als lokale Kopie (<see cref="Entries"/>) und arbeitet ohne Netz mit der letzten Kopie weiter.
/// <see cref="IsActive"/> schaltet die Leitung (Abruf und Wirkung); die Anzeige im Kalender schaltet jeder Zugang für sich (<see cref="CalendarSubscriptionHidden"/>).
/// </summary>
public sealed class CalendarSubscription
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Leer: gilt für alle Standorte, bei denen <see cref="Region"/> passt.</summary>
    public Guid? LocationId { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Adresse mit <c>https://</c> (ein <c>webcal://</c> wird beim Speichern zu <c>https://</c>); leer: nur Datei-Import.</summary>
    public string Url { get; set; } = "";

    public SubscriptionKind Kind { get; set; }

    /// <summary>Land („AT“) oder Region („CH-ZH“); leer: alle.</summary>
    public string? Region { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Schlüssel eines vorab eingetragenen Abos (Land: „AT“, „DE“, „CH“); sonst leer. Solche Abos lassen sich nicht löschen.</summary>
    public string? Preset { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastFetchAt { get; set; }

    public SubscriptionFetchResult LastResult { get; set; }

    public string? LastError { get; set; }

    /// <summary>Wann der Hintergrundjob das Abo wieder abruft; leer: sobald der Job läuft (neu angelegt oder eingeschaltet).</summary>
    public DateTimeOffset? NextCheckAt { get; set; }

    public List<CalendarSubscriptionEntry> Entries { get; init; } = [];
}

/// <summary>Wie ein Eintrag zu bewerten ist: <c>Holiday</c> gilt überall, <c>Regional</c> nur in den genannten Bundesländern oder Kantonen, <c>Observance</c> (Gedenktag) ist kein Feiertag.</summary>
public enum EntryScope
{
    Holiday,
    Regional,
    Observance,
}

/// <summary>Ein gespeicherter Tag (oder Zeitraum) eines Abos. <see cref="Until"/> ist der letzte Tag einschließlich.</summary>
public sealed class CalendarSubscriptionEntry
{
    public const string FromFeed = "feed";
    public const string FromFile = "file";

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid SubscriptionId { get; init; }

    public DateOnly From { get; set; }

    public DateOnly Until { get; set; }

    public string Title { get; set; } = "";

    public bool AllDay { get; set; } = true;

    public EntryScope Scope { get; set; }

    /// <summary>Bei <see cref="EntryScope.Regional"/> die Namen der Bundesländer oder Kantone, durch Komma getrennt.</summary>
    public string? RegionNames { get; set; }

    /// <summary><see cref="FromFeed"/> oder <see cref="FromFile"/>.</summary>
    public string Source { get; set; } = FromFeed;
}

/// <summary>Dieser Zugang blendet das Abo im Kalender aus. Nur Anzeige: Öffnungszeiten, Buchung und Assistent kennen die Anzeige nicht.</summary>
public sealed class CalendarSubscriptionHidden
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid SubscriptionId { get; init; }

    public required string UserId { get; init; }
}

/// <summary>Entscheidung der Leitung zu einem Tag, an dem Abo und Regel-Feiertage sich widersprechen.</summary>
public sealed class CalendarSubscriptionDecision
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid SubscriptionId { get; init; }

    public DateOnly Date { get; init; }

    public SubscriptionDecision Decision { get; set; }

    public required string UserId { get; set; }

    public DateTimeOffset DecidedAt { get; set; }
}

/// <summary>
/// Meldung zu Abos (Änderung, Fehler, Erinnerung, „Falsch melden“). <see cref="Key"/> erkennt dieselbe Meldung wieder (ein offener Fehler je Abo,
/// eine Erinnerung je Jahr). Erledigt wird sie von einer Person oder vom nächsten erfolgreichen Abruf.
/// </summary>
public sealed class CalendarSubscriptionNotice
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid? SubscriptionId { get; init; }

    public Guid? LocationId { get; init; }

    public SubscriptionNoticeKind Kind { get; init; }

    public required string Key { get; init; }

    public required string Title { get; set; }

    public required string Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Nur für die Leitung (<c>ManageLocation</c>): „Falsch melden“ und Erinnerungen.</summary>
    public bool ForLeadership { get; init; }
}
