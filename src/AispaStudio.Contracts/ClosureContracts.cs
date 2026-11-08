namespace AispaStudio.Contracts;

/// <summary>
/// Art einer Schließzeit (ADR 0073): <c>Vacation</c> ist der geplante Betriebsurlaub, <c>SpecialClosure</c> ein Feiertag oder eine Sonderschließung
/// (Brückentag, Betriebsfeier), <c>Outage</c> ein Ausfall (Schaden wie Wasser, Krankheit, Behörde), meist ungeplant.
/// </summary>
public enum ClosureKind
{
    Vacation,
    SpecialClosure,
    Outage,
}

/// <summary>Zustand einer Schließzeit am heutigen Tag des Standorts, gerechnet: <c>Planned</c> beginnt später, <c>Active</c> läuft, <c>Ended</c> ist vorbei.</summary>
public enum ClosurePhase
{
    Planned,
    Active,
    Ended,
}

/// <summary>
/// Schließzeit anlegen oder ändern. <c>FromDate</c> und <c>UntilDate</c> sind Kalendertage am Standort, beide einschließlich. Ohne <c>FromTime</c> beginnt
/// sie um 0:00 Uhr, ohne <c>UntilTime</c> reicht sie bis zum Ende des letzten Tages; mit Uhrzeiten entstehen halbe Tage („ab 13:00“). <c>InternalReason</c>
/// sieht nur das Team, <c>GuestText</c> ist der kurze Text für Gäste (Kundenbereich, Assistent); ohne ihn steht ein Standardtext der Art.
/// </summary>
public sealed record ClosureInput(
    DateOnly FromDate,
    TimeOnly? FromTime,
    DateOnly UntilDate,
    TimeOnly? UntilTime,
    ClosureKind Kind,
    string? InternalReason,
    string? GuestText);

/// <summary>
/// Eine Schließzeit mit Zustand. <c>From</c> und <c>Until</c> sind der Zeitraum in UTC (<c>Until</c> ausschließlich), <c>OpenAppointments</c> die noch offenen
/// Termine darin (gebucht oder eingecheckt, nicht vorbei), <c>CancelledAppointments</c> die wegen dieser Schließzeit abgesagten und <c>OpenWaitlist</c> die Wartelisten-Einträge,
/// deren Wunschzeitraum darin liegt und die noch nicht benachrichtigt sind (Ä48).
/// </summary>
public sealed record ClosureDto(
    Guid Id,
    Guid LocationId,
    DateOnly FromDate,
    TimeOnly? FromTime,
    DateOnly UntilDate,
    TimeOnly? UntilTime,
    DateTimeOffset From,
    DateTimeOffset Until,
    ClosureKind Kind,
    string? InternalReason,
    string? GuestText,
    ClosurePhase Phase,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    int OpenAppointments,
    int CancelledAppointments,
    int OpenWaitlist = 0);

/// <summary>
/// Schließband im Kalender: der Zeitraum über allen Spalten. <c>Title</c> ist die Art in Worten („Betriebsurlaub“), <c>Text</c> der Text für Gäste
/// (sonst der Standardtext), <c>InternalReason</c> der interne Grund. <c>OpenAppointments</c> zählt die noch offenen Termine im ganzen Zeitraum,
/// <c>WaitlistEntries</c> die Wartelisten-Einträge, deren Wunschzeitraum in die Schließzeit fällt und die noch nicht benachrichtigt sind (Ä48).
/// <c>Urgent</c>: die Schließzeit läuft oder beginnt in den nächsten 14 Tagen; die Meldung dazu ist dann ein Fehler, sonst eine Warnung (Ä49).
/// </summary>
public sealed record ClosureBand(
    Guid Id,
    DateTimeOffset From,
    DateTimeOffset Until,
    ClosureKind Kind,
    string Title,
    string Text,
    string? InternalReason,
    int OpenAppointments,
    int WaitlistEntries = 0,
    bool Urgent = false);

/// <summary>
/// Schließzeit für den Kundenbereich: Zeitraum in Ortszeit und der Text für Gäste (nie der interne Grund). <c>Text</c> ist leer, wenn das Spa keinen eigenen geschrieben hat;
/// der Kundenbereich nimmt dann den Standardsatz der Art in der Sprache des Gastes.
/// </summary>
public sealed record OnlineClosure(
    DateOnly FromDate,
    TimeOnly? FromTime,
    DateOnly UntilDate,
    TimeOnly? UntilTime,
    ClosureKind Kind,
    string? Text);

/// <summary>Auf welchem Weg eine Absage den Gast erreicht: per Mail oder SMS, oder gar nicht (<c>None</c>), wenn er weder Mailadresse noch Telefonnummer hat.</summary>
public enum ClosureChannel
{
    None,
    Email,
    Sms,
}

/// <summary>Zustand einer Absage: <c>Queued</c> liegt in der Queue, <c>Sent</c> ist zugestellt, <c>Skipped</c> hatte keinen Kontakt, <c>Failed</c> ist gescheitert (die Queue wiederholt), <c>Cancelled</c> hat das Team vor dem Versand zurückgenommen („Nicht senden“, Ä95).</summary>
public enum ClosureNoticeState
{
    Queued,
    Sent,
    Skipped,
    Failed,
    Cancelled,
}

/// <summary>
/// Protokoll einer Absage wegen einer Schließzeit: wer sie wann über welchen Kanal ausgelöst hat und was der Gast bekam. <c>Channel</c> ist <c>None</c>, wenn der
/// Gast weder Mailadresse noch Telefonnummer hat. <c>AiWritten</c>: der Text wurde mit KI umformuliert und trägt den Hinweis darauf.
/// Absage eines Termins: <c>AppointmentId</c> und <c>AppointmentStart</c>. Benachrichtigung zur Warteliste (Ä48): <c>WaitlistEntryId</c> und der Wunschzeitraum
/// <c>WishFrom</c> bis <c>WishUntil</c>; der Eintrag bleibt auf der Liste. <c>Language</c>: Sprache der Nachricht (de, en, fr, it).
/// <c>RefundAmount</c>: die Anzahlung, die dabei automatisch retourniert wurde, mit dem Storno-Beleg <c>RefundReceiptId</c>; <c>RefundError</c>, wenn das nicht ging
/// (dann nennt die Nachricht keine Rückzahlung und die Rezeption erstattet an der Kassa). <c>RefundWay</c>: der Zahlungsweg der Rückzahlung („Bar“, „Karte“); bei „Bar“ bekommt der Gast
/// das Geld an der Rezeption zurück, und die Nachricht sagt es.
/// </summary>
public sealed record ClosureNoticeDto(
    Guid Id,
    Guid ClosureId,
    Guid? AppointmentId,
    string CustomerName,
    string TreatmentName,
    DateTimeOffset? AppointmentStart,
    ClosureChannel Channel,
    ClosureNoticeState State,
    string? Error,
    string? Subject,
    string Text,
    bool AiWritten,
    bool OfferedRebooking,
    string SentBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string Language = "de",
    Guid? WaitlistEntryId = null,
    DateOnly? WishFrom = null,
    DateOnly? WishUntil = null,
    decimal? RefundAmount = null,
    Guid? RefundReceiptId = null,
    string? RefundError = null,
    string? RefundWay = null,
    DateTimeOffset? ScheduledFor = null);

/// <summary>
/// Ein Termin in der Schließzeit für die Rückfrage zum Absagen. <c>Channel</c>: der Weg, auf dem der Gast benachrichtigt würde (<c>None</c>: kein Kontakt).
/// <c>Language</c>: Sprache der Nachricht (aus den Kundendaten, sonst „de“). <c>HasDeposit</c>: der Gast hat eine Anzahlung geleistet; <c>DepositAmount</c> ist der Betrag,
/// der mit der Absage automatisch über die Kassa retourniert wird (Storno-Beleg, Zahlungsweg der Einzahlung); fehlt er bei <c>HasDeposit</c>, ist die Zahlung noch nicht
/// verbucht. <c>PrepaidOnPlatform</c>: bei Treatwell vorab bezahlt, das erstattet die Rezeption dort.
/// </summary>
public sealed record ClosureAppointmentDto(
    Guid AppointmentId,
    string CustomerName,
    string TreatmentName,
    DateTimeOffset Start,
    DateTimeOffset End,
    string EmployeeName,
    AppointmentStatus Status,
    ClosureChannel Channel,
    bool HasDeposit,
    bool PrepaidOnPlatform,
    string Language = "de",
    decimal? DepositAmount = null);

/// <summary>
/// Ein Eintrag der Warteliste, dessen Wunschzeitraum in die Schließzeit fällt (Ä48): er bekommt eine kurze Nachricht in seiner Sprache und bleibt auf der Liste.
/// <c>Channel</c> und <c>Language</c> wie bei den Terminen.
/// </summary>
public sealed record ClosureWaitlistDto(
    Guid EntryId,
    string CustomerName,
    string TreatmentName,
    DateOnly From,
    DateOnly Until,
    DayPart DayPart,
    ClosureChannel Channel,
    string Language);

/// <summary>
/// Die Rückfrage zum Absagen: die offenen Termine der Schließzeit, der Vorschlag für den Text je Art (bearbeitbar; <c>{Anrede}</c>, <c>{Behandlung}</c> und
/// <c>{Zeit}</c> setzt der Server je Gast ein) und ob eine KI zum Umformulieren bereitsteht.
/// </summary>
public sealed record ClosureCancellationPreview(
    ClosureDto Closure,
    IReadOnlyList<ClosureAppointmentDto> Appointments,
    string SuggestedSubject,
    string SuggestedText,
    bool AiAvailable,
    IReadOnlyList<ClosureWaitlistDto>? Waitlist = null);

/// <summary>
/// Termine einer Schließzeit absagen und die Gäste benachrichtigen. Nichts geschieht ohne diese Anfrage. <c>AppointmentIds</c> sind die ausgewählten Termine
/// (jeder muss in der Schließzeit liegen und offen sein). <c>Text</c> und <c>Subject</c> sind die bearbeitete Vorlage; <c>OfferRebooking</c> hängt den Link in den
/// Kundenbereich an; <c>AiWritten</c> setzt die Oberfläche, wenn die KI den Text umformuliert hat, dann hängt der Server den Hinweis auf die KI an.
/// <c>Subject</c> und <c>Text</c> gelten als deutsche Vorlage: Gäste mit einer anderen Sprache (en, fr, it) bekommen die Standardvorlage ihrer Sprache, und der Hinweis auf
/// die KI steht nur in deutschen Nachrichten. <c>WaitlistEntryIds</c>: Wartelisten-Einträge der Schließzeit, die mitbenachrichtigt werden (Standardvorlage je Sprache,
/// sie bleiben auf der Liste); es braucht mindestens einen Termin oder einen Eintrag.
/// </summary>
public sealed record CancelClosureAppointmentsRequest(
    IReadOnlyList<Guid> AppointmentIds,
    string Subject,
    string Text,
    bool OfferRebooking,
    bool AiWritten,
    IReadOnlyList<Guid>? WaitlistEntryIds = null);

/// <summary>
/// <c>Cancelled</c> Termine sind abgesagt, <c>Queued</c> Nachrichten liegen in der Queue, <c>WithoutContact</c> Gäste haben keinen Kontakt (abgesagt, aber nicht
/// benachrichtigt), <c>Failed</c> Termine ließen sich nicht absagen (inzwischen geändert); sie bleiben offen und stehen weiter in der Meldung.
/// <c>Cancelled</c> zählt nur Termine; <c>Queued</c> und <c>WithoutContact</c> zählen Termine und Wartelisten-Einträge zusammen, <c>WaitlistNotified</c> die Einträge davon.
/// <c>Refunded</c> Anzahlungen wurden über die Kassa retourniert (Storno-Beleg), <c>RefundsFailed</c> nicht: die Rezeption erstattet sie an der Kassa.
/// </summary>
public sealed record CancelClosureAppointmentsResult(int Cancelled, int Queued, int WithoutContact, int Failed, IReadOnlyList<ClosureNoticeDto> Notices,
    int WaitlistNotified = 0, int Refunded = 0, int RefundsFailed = 0);

/// <summary>
/// KI umformulieren lassen (nur wenn <c>AiAvailable</c>); Zahlen dürfen nicht dazukommen. <c>Language</c>: Sprache des Textes (de, en, fr, it; leer: Deutsch, die Sprache der
/// bearbeitbaren Vorlage); das Modell behält sie.
/// </summary>
public sealed record ReviseClosureTextRequest(string Text, ReplyRevisionKind Kind, string? Language = null);

public sealed record ReviseClosureTextResult(string Text);

/// <summary>
/// Wartezeit vor der Nachricht an den Gast, wenn das Team absagt (Ä95, ADR 0081): <c>Minutes</c> von 0 bis 60, Standard 10, je Standort. Gäste, die selbst auslösen,
/// bekommen ihre Nachricht sofort; beginnt der Termin in weniger als 2 Stunden, auch die des Teams.
/// </summary>
public sealed record GuestMessageDelayDto(int Minutes);
