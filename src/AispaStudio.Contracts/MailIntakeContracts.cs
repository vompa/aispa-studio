namespace AispaStudio.Contracts;

/// <summary>Woher eine Anfrage oder Buchung kommt. Weitere Kanäle (z. B. Reserve with Google) kommen als neue Werte dazu.</summary>
public enum BookingChannel
{
    Email,
    Treatwell,
    Sms,
    WhatsApp,

    /// <summary>Anruf: Aufnahme oder Mailbox, in Text umgewandelt. Geantwortet wird per SMS.</summary>
    Phone,
}

/// <summary>Bearbeitungsstand einer eingegangenen Mail.</summary>
public enum InboundMailStatus
{
    /// <summary>Eingegangen, wartet in der Queue auf die Verarbeitung.</summary>
    Received,

    /// <summary>Termin ist eingetragen.</summary>
    Booked,

    /// <summary>Freie Zeiten sind vorgeschlagen; die Rezeption bucht einen Vorschlag oder der Gast antwortet.</summary>
    Proposed,

    /// <summary>Termin wurde storniert (z. B. Treatwell-Stornierung).</summary>
    Cancelled,

    /// <summary>Die Rezeption muss entscheiden; der Grund steht in <c>Note</c>.</summary>
    NeedsReview,

    /// <summary>Nichts zu tun (z. B. dieselbe Treatwell-Buchung zum zweiten Mal oder von der Rezeption erledigt).</summary>
    Ignored,

    /// <summary>Verarbeitung fehlgeschlagen (z. B. KI nicht erreichbar); „retry“ versucht es erneut.</summary>
    Failed,
}

/// <summary>Was die Mail will, erkannt von der KI oder aus dem Treatwell-Format.</summary>
public enum InboundMailKind
{
    Unknown,
    BookingRequest,
    Cancellation,
    Reschedule,
    Question,
    Other,
    TreatwellBooking,
    TreatwellCancellation,
    TreatwellChange,
}

public enum ReplyStatus
{
    None,
    Draft,
    Sending,
    Sent,
}

/// <summary>Zuordnung eines Namens auf einer Plattform zu eigenen Stammdaten.</summary>
public enum ChannelMappingKind
{
    Location,
    Treatment,
    Employee,
}

/// <summary>Eine Zeile im Posteingang der Terminannahme (Mail, SMS, WhatsApp oder Anruf).</summary>
public sealed record InboundMailSummary(
    Guid Id,
    BookingChannel Channel,
    string From,
    string? FromName,
    string Subject,
    DateTimeOffset ReceivedAt,
    InboundMailStatus Status,
    InboundMailKind Kind,
    ReplyStatus ReplyStatus,
    string? Summary,
    Guid? AppointmentId);

/// <summary>
/// Eingegangene Mail mit Ergebnis der Verarbeitung: erkannte Absicht, Vorschläge, Termin und Antwortentwurf.
/// <c>AiDisclosure</c> wird beim Senden immer unter die Antwort gesetzt (AI Act Art. 50) und lässt sich nicht entfernen.
/// </summary>
public sealed record InboundMailDto(
    Guid Id,
    BookingChannel Channel,
    string From,
    string? FromName,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt,
    InboundMailStatus Status,
    InboundMailKind Kind,
    string? Language,
    string? Summary,
    string? Note,
    Guid? LocationId,
    Guid? TreatmentId,
    Guid? CustomerId,
    Guid? AppointmentId,
    IReadOnlyList<AvailableSlot> Proposals,
    ReplyStatus ReplyStatus,
    string? ReplyTo,
    string? ReplySubject,
    string? ReplyBody,
    string? AiDisclosure,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset? RepliedAt,
    MessageTranslationState Translation = MessageTranslationState.None,
    string? TranslatedSubject = null,
    string? TranslatedBody = null,
    string? ReplyTranslation = null,
    bool ReplyRevising = false,
    bool CanUndoReply = false,
    string? ReplyNotice = null,
    bool CanRevise = false,
    Guid? PreviousMailId = null,
    IReadOnlyList<DateTimeOffset>? RejectedStarts = null,
    int ProposalRound = 0,
    string? WishNote = null,
    bool Reproposing = false,
    bool CanRepropose = false,
    bool CanCancelAppointment = false,
    IReadOnlyList<CustomerCandidateDto>? CustomerCandidates = null,
    bool CustomerConfirmed = false,
    string? PlatformReference = null,
    PlatformPayment PlatformPayment = PlatformPayment.None,
    decimal? PrepaidAmount = null,
    bool AcceptInPlatformPending = false,
    bool CustomerAssignmentOpen = false,
    string? SenderRulePattern = null,
    CustomerSalutation? SuggestedSalutation = null,
    string? SuggestedTitlesBefore = null,
    DateTimeOffset? HeldStart = null);

/// <summary>
/// Wie der Gast eine Plattformbuchung bezahlt: <c>Prepaid</c> vorab über die Plattform (die Plattform rechnet mit dem Spa ab),
/// <c>OnSite</c> im Spa. <c>None</c>: keine Plattformbuchung oder aus der Mail nicht zu lesen.
/// </summary>
public enum PlatformPayment
{
    None,
    Prepaid,
    OnSite,
}

/// <summary>Für welche Plattform eine Absender-Regel gilt.</summary>
public enum SenderPlatform
{
    Treatwell,
    Other,
}

/// <summary>Was mit Mails eines Absenders geschieht: Buchung automatisch übernehmen, nur als Vorschlag in den Posteingang oder ignorieren.</summary>
public enum SenderHandling
{
    Automatic,
    Suggest,
    Ignore,
}

/// <summary>Absender-Regel eines Spas (ADR 0062). <c>Pattern</c> ist eine genaue Mailadresse oder eine Domain (gilt mit Unterdomains).</summary>
public sealed record SenderRuleDto(Guid Id, string Pattern, SenderPlatform Platform, SenderHandling Handling, bool Active, string? Note);

public sealed record SenderRuleInput(string Pattern, SenderPlatform Platform, SenderHandling Handling, bool Active = true, string? Note = null);

/// <summary>
/// Vorschlag, wer der Absender einer Nachricht ist. <c>Stage</c>: 1 = Mail oder Nummer gleich (aber mehrere Kunden teilen sie),
/// 2 = vom Gast genannte Telefonnummer passt, 3 = Name plus ein zweites Merkmal, 4 = nur ähnlicher Name. <c>Reason</c> ist der
/// deutsche Kurztext für die Rezeption.
/// </summary>
public sealed record CustomerCandidateDto(Guid CustomerId, string Name, int Stage, string Reason);

/// <summary>Kunde einer Nachricht zuordnen; <c>RememberContact</c> merkt Mailadresse oder Nummer des Absenders in der Kundenkarte.</summary>
public sealed record AssignCustomerInput(Guid CustomerId, bool RememberContact = false);

/// <summary>Neuen Kunden aus der Nachricht anlegen; leere Felder werden aus der Nachricht übernommen (Name, Telefon, Mail).</summary>
public sealed record NewCustomerFromMailInput(string? FirstName = null, string? LastName = null, string? Email = null, string? Phone = null);

/// <summary>Angaben eines neuen Kunden, zu denen es schon einen geben könnte.</summary>
public sealed record DuplicateCheckInput(string? FirstName, string? LastName, string? Email, string? Phone, DateOnly? BirthDate = null);

/// <summary>Weitere Mailadresse eines Kunden (die Hauptadresse steht im Kunden selbst).</summary>
public sealed record CustomerEmailDto(Guid Id, string Address);

public sealed record CustomerEmailInput(string Address);

/// <summary>„Neu vorschlagen“: optional ein Hinweis der Rezeption, der den Wunsch ergänzt („lieber am Abend“).</summary>
public sealed record ReproposeInput(string? Hint = null);

/// <summary>
/// Übersetzung einer Nachricht in die Sprache des Spas (Deutsch). Ohne KI gibt es keine; weichen Zahlen (Datum, Uhrzeit, Preis)
/// zwischen Original und Übersetzung ab, zeigt der Posteingang das Original.
/// </summary>
public enum MessageTranslationState
{
    /// <summary>Schon auf Deutsch oder noch nicht verarbeitet.</summary>
    None,
    Pending,
    Done,

    /// <summary>Ohne API-Schlüssel oder fehlgeschlagen: das Original steht da, mit „Übersetzung nicht verfügbar“.</summary>
    Unavailable,

    /// <summary>Eine Zahl stimmt nicht mit dem Original überein; die Übersetzung wird nur auf Wunsch gezeigt.</summary>
    Mismatch,
}

/// <summary>Knöpfe unter dem Antwortentwurf: wie die KI ihn überarbeiten soll.</summary>
public enum ReplyRevisionKind
{
    Friendlier,
    Shorter,
    Formal,
    Informal,
    Apology,

    /// <summary>Eigener Hinweis, z. B. „bitte Parkplatz erwähnen“.</summary>
    Custom,
}

/// <param name="Instruction">Nur bei <see cref="ReplyRevisionKind.Custom"/>: der Hinweis der Rezeption.</param>
public sealed record ReplyRevisionInput(ReplyRevisionKind Kind, string? Instruction = null);

/// <summary>Einen Vorschlag (oder eine andere freie Zeit) für die Mail buchen. Ohne Person, Raum oder Gerät wählt der Server.</summary>
public sealed record AcceptProposalRequest(DateTimeOffset Start, Guid? EmployeeId, Guid? RoomId, Guid? EquipmentId);

/// <summary>
/// Eine freie Zeit für den Gast der Anfrage vormerken und ihm vorschlagen (ADR 0083): der Termin belegt den Kalender sofort, bis die Rezeption
/// ihn bestätigt oder freigibt. Ohne Person, Raum oder Gerät wählt der Server.
/// </summary>
public sealed record HoldTimeRequest(Guid LocationId, Guid TreatmentId, DateTimeOffset Start, Guid? EmployeeId, Guid? RoomId, Guid? EquipmentId);

/// <summary>
/// Offene Vormerkung ohne Antwort des Gastes (ADR 0083); ab <c>RemindAt</c> zeigt der Kalender „Gast hat sich nicht gemeldet“. Den Zeitpunkt rechnet der Server.
/// </summary>
public sealed record AppointmentHoldDto(Guid AppointmentId, Guid? MailId, string CustomerName, string TreatmentName, DateTimeOffset Start, DateTimeOffset RemindAt);

/// <summary>Antwortentwurf bearbeiten, bevor er gesendet wird.</summary>
public sealed record ReplyDraftInput(string Subject, string Body);

public sealed record ChannelMappingInput(ChannelMappingKind Kind, string ExternalName, Guid TargetId);

public sealed record ChannelMappingDto(Guid Id, BookingChannel Channel, ChannelMappingKind Kind, string ExternalName, Guid TargetId);

/// <summary>
/// Neuer ICS-Feed mit der Belegung eines Standorts oder einer Person. Mit <c>Channel</c> fehlen im Feed die Termine,
/// die von diesem Kanal selbst kommen (Treatwell kennt seine eigenen Buchungen schon).
/// </summary>
public sealed record CalendarFeedInput(string Name, Guid? EmployeeId, BookingChannel? Channel);

/// <summary>ICS-Feed. <c>Path</c> ist geheim (enthält den Zugangsschlüssel) und wird bei der Plattform als Kalender-URL eingetragen.</summary>
public sealed record CalendarFeedDto(Guid Id, Guid LocationId, Guid? EmployeeId, string Name, BookingChannel? Channel, string Path, DateTimeOffset CreatedAt);

/// <summary>SignalR: im Posteingang ist diese Nachricht neu oder geändert. Den Inhalt lädt die Oberfläche selbst über die API.</summary>
public sealed record InboxChangedEvent(Guid MailId);
