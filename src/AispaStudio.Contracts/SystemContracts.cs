namespace AispaStudio.Contracts;

/// <summary>Stand des Starts: Migration der Datenbank und (in der Entwicklung) Einspielen des Beispiel-Spas.</summary>
public enum StartupPhase
{
    Migrating,
    Seeding,
    Ready,
    Failed,
}

/// <summary>Startzustand mit Text für die Anzeige, z. B. „Beispiel-Spa wird eingerichtet (seit 12 s)“ oder der Fehler.</summary>
public sealed record StartupInfo(StartupPhase Phase, string Text);

/// <summary>Zustand des Servers, z. B. für die Statusanzeige in Web und Windows-App. Ohne <c>Startup</c> (älterer Server) gilt: bereit.</summary>
public sealed record SystemStatus(string Version, string Environment, DateTimeOffset ServerTime, StartupInfo? Startup = null);

/// <summary>
/// Wo die Web-Oberfläche liegt, wenn nicht unter der Adresse des Servers selbst: in der Entwicklung bei Vite (vom Aspire-AppHost
/// vorgegeben). Leer, wenn der Server sie selbst ausliefert oder keine kennt. Die Windows-App lädt dort die Hilfe und nennt dort die Seite „Anmelden“.
/// </summary>
public sealed record TeamWebInfo(string? Url);

/// <summary>Anfrage an den Server, eine Nachricht asynchron über die Queue zu verarbeiten.</summary>
public sealed record PingRequest(string Message);

/// <summary>Antwort auf <see cref="PingRequest"/>: die Nachricht ist angenommen, das Ergebnis kommt per SignalR.</summary>
public sealed record PingAccepted(Guid PingId);

/// <summary>Ergebnis einer asynchron verarbeiteten <see cref="PingRequest"/>, per SignalR an die Clients gesendet.</summary>
public sealed record PongEvent(Guid PingId, string Message, DateTimeOffset ProcessedAt);

/// <summary>
/// Betriebsart und eingeschaltete Webmodule der Installation (ADR 0041). Oberflächen blenden ausgeschaltete Module aus;
/// der Server antwortet dort mit 404.
/// </summary>
/// <param name="Standalone">Einzelplatz-Betrieb mit lokaler Datenbank und klassischer Benutzerverwaltung.</param>
/// <param name="OnlineBooking">Kundenbereich: Online-Buchung, Kundenkonten, Warteliste online, Gutscheinkauf, Kartenhinterlegung.</param>
/// <param name="BookingIntake">Terminannahme per Mail, SMS, WhatsApp, Anruf und Buchungsplattformen.</param>
/// <param name="Assistant">KI-Assistent.</param>
/// <param name="Reminders">Terminerinnerungen.</param>
/// <param name="Rebooking">Erinnerung an die nächste Behandlung.</param>
/// <param name="Benchmark">Vergleich mit anderen Spas.</param>
/// <param name="Dashboard">Dashboard für den Pausenraum: Anzeigen koppeln und die Tafel mit dem Tag (Slice 105).</param>
/// <param name="Plan">Abo-Stufe des angemeldeten Spas (ADR 0015); leer ohne Anmeldung. Die Module oben gelten dann schon für diese Stufe.</param>
public sealed record SystemModules(
    bool Standalone,
    bool OnlineBooking,
    bool BookingIntake,
    bool Assistant,
    bool Reminders,
    bool Rebooking,
    bool Benchmark,
    bool Dashboard,
    SubscriptionPlan? Plan = null);

/// <summary>
/// Abo-Stufe eines Spas (ADR 0015): Start, Spa, Spa Plus. Sie schaltet zuschaltbare Webmodule frei; der Kern (Kalender, Kassa,
/// Stammdaten, Dienstplan, Berichte) ist in jeder Stufe da.
/// </summary>
public enum SubscriptionPlan
{
    Start,
    Spa,
    SpaPlus,
}

/// <summary>Stand des Abgleichs eines Einzelplatzes mit der zentralen Datenbank (ADR 0041, Schritt 4).</summary>
/// <param name="Enabled">Ob eine zentrale Datenbank dazugeschaltet ist (<c>Sync:CentralConnectionString</c>).</param>
/// <param name="LastRun">Letzter Lauf, erfolgreich oder nicht.</param>
/// <param name="LastError">Fehler des letzten Laufs; leer, wenn er gelang.</param>
/// <param name="PendingChanges">Lokale Änderungen, die noch nicht gesendet sind.</param>
/// <param name="Conflicts">Zeilen, die eine Seite abgelehnt hat und die noch niemand erledigt hat.</param>
public sealed record SyncStatus(bool Enabled, DateTimeOffset? LastRun, string? LastError, long PendingChanges,
    IReadOnlyList<SyncConflict> Conflicts);

/// <summary>Eine beim Abgleich abgelehnte Zeile; sie bleibt lokal erhalten.</summary>
/// <param name="Area">Fachlicher Bereich, z. B. „Termine“ oder „Kunden“.</param>
public sealed record SyncConflict(long Id, string Area, string Table, string RowKey, string Reason, DateTimeOffset DetectedAt);

/// <summary>Zustand des lokalen KI-Modells (ADR 0057).</summary>
public enum LocalAiState
{
    /// <summary>Nicht eingerichtet: <c>Ai:Local:Url</c> ist leer.</summary>
    Off,

    /// <summary>Eingerichtet, der Dienst (z. B. der Ollama-Container) antwortet nicht.</summary>
    Unreachable,

    /// <summary>Dienst läuft, das Modell ist noch nicht geladen.</summary>
    Missing,

    /// <summary>Das Modell wird gerade heruntergeladen.</summary>
    Downloading,

    /// <summary>Bereit.</summary>
    Ready,
}

/// <summary>KI des Servers: Claude über das Internet und das optionale lokale Modell (ADR 0057).</summary>
/// <param name="Claude">Ob Claude eingeschaltet ist (API-Schlüssel gesetzt).</param>
/// <param name="Local">Zustand des lokalen Modells.</param>
/// <param name="LocalModel">Name des lokalen Modells, z. B. <c>gemma3:4b</c>; leer, wenn nicht eingerichtet.</param>
/// <param name="DownloadPercent">Fortschritt beim Herunterladen in Prozent.</param>
/// <param name="LocalError">Letzter Fehler des lokalen Modells, verständlich für die Leitung.</param>
/// <param name="CheckedAt">Wann der Server das lokale Modell zuletzt geprüft hat.</param>
public sealed record AiStatus(
    bool Claude,
    LocalAiState Local,
    string? LocalModel,
    int? DownloadPercent,
    string? LocalError,
    DateTimeOffset? CheckedAt);
