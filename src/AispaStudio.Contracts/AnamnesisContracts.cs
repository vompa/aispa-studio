namespace AispaStudio.Contracts;

// Anamnese für das Team: am Tablet ausfüllen lassen, Papierbogen drucken, ausgefüllten Papierbogen fotografieren und von der KI
// übernehmen lassen. Alle Wege enden im selben verschlüsselten Bogen (AnamnesisRecordDto).

/// <summary>
/// Stand der Anamnese eines Kunden. <c>ValidUntil</c>: bis dahin gilt der letzte Bogen; danach bitte neu ausfüllen lassen.
/// <c>OpenScans</c>: fotografierte Papierbögen, die noch gelesen werden oder auf Prüfung warten.
/// </summary>
public sealed record AnamnesisOverview(
    DateTimeOffset? LastSubmittedAt,
    string? Channel,
    DateTimeOffset? ValidUntil,
    IReadOnlyList<AnamnesisScanSummary> OpenScans);

public sealed record AnamnesisScanSummary(Guid Id, AnamnesisScanStatus Status, DateTimeOffset UploadedAt);

/// <summary>
/// <c>Check</c>: Die Behandlung trägt „Anamnese prüfen“, und der letzte Bogen des Gastes enthält Angaben (ein Ja oder Text unter
/// „Sonstiges“). Mehr erfährt die Rezeption hier nicht; den Bogen selbst liest, wer ihn öffnet.
/// </summary>
public sealed record AnamnesisCheck(bool Check);

/// <summary>Für welchen Termin (optional) und in welcher Sprache („de“, „en“, „fr“ oder „it“) Tablet-Link oder Papierbogen gelten.</summary>
public sealed record AnamnesisLinkRequest(Guid? AppointmentId, string? Language);

/// <summary>Link für das Tablet am Empfang: kurz gültig und nur einmal verwendbar.</summary>
public sealed record AnamnesisTabletLink(string Url, DateTimeOffset ExpiresAt);

/// <summary>Alles für den Papierbogen: Fragen, für wen, und ein QR-Code-Link, falls die Kundin lieber am Handy ausfüllt.</summary>
public sealed record AnamnesisPaperSheet(
    AnamnesisFormDefinition Form,
    string CustomerName,
    string? LocationName,
    string? TreatmentName,
    DateTimeOffset? Start,
    string? TimeZone,
    string Url,
    DateTimeOffset ExpiresAt);

public enum AnamnesisScanStatus
{
    /// <summary>Foto ist hochgeladen, die KI liest es gerade.</summary>
    Reading,

    /// <summary>Vorschlag liegt vor; jemand aus dem Team prüft ihn mit dem Foto und bestätigt.</summary>
    ReadyForReview,

    /// <summary>Bestätigt und als Anamnesebogen gespeichert.</summary>
    Confirmed,
}

/// <summary>
/// Von der KI gelesene Antwort. <c>Uncertain</c>: bitte mit dem Foto vergleichen (unleserlich, durchgestrichen, beide Kästchen
/// angekreuzt …); <c>Reason</c> sagt, warum.
/// </summary>
public sealed record ExtractedAnswer(string Key, bool? Yes, string? Details, bool Uncertain, string? Reason);

/// <summary>
/// Fotografierter Papierbogen mit dem Vorschlag der KI. <c>Reader</c>: wer gelesen hat („Claude“, oder „Manuell“, wenn keine KI
/// eingerichtet ist und alles von Hand übertragen wird).
/// </summary>
public sealed record AnamnesisScanDto(
    Guid Id,
    Guid CustomerId,
    Guid? AppointmentId,
    AnamnesisScanStatus Status,
    DateTimeOffset UploadedAt,
    string? Reader,
    IReadOnlyList<ExtractedAnswer> Answers,
    string? Signature,
    bool SignatureUncertain,
    bool? ConsentChecked,
    string? Note,
    Guid? RecordId);

/// <summary>
/// Geprüfte Antworten eines Papierbogens. <c>ConsentOnPaper</c> und <c>SignedOnPaper</c>: die Einwilligung ist auf dem Papier
/// angekreuzt und der Bogen unterschrieben (bestätigt die Person aus dem Team mit Blick auf das Foto).
/// </summary>
public sealed record AnamnesisScanConfirmation(
    IReadOnlyList<AnamnesisAnswer> Answers, bool ConsentOnPaper, bool SignedOnPaper, string Signature, string? Language);
