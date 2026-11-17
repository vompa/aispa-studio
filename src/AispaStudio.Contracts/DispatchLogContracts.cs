namespace AispaStudio.Contracts;

/// <summary>Art einer Nachricht im Versandprotokoll.</summary>
public enum DispatchKind
{
    /// <summary>Terminerinnerung an den Gast (keine Werbung).</summary>
    Reminder,

    /// <summary>Wiederbuchungs-Hinweis (Werbung, nur mit Einwilligung).</summary>
    Rebook,
}

/// <summary>Eine Zeile des Versandprotokolls: eine Terminerinnerung oder ein Wiederbuchungs-Hinweis, wie er verschickt wurde (oder warum nicht).</summary>
/// <param name="Recipient">Name des Gastes.</param>
/// <param name="Subject">Die Behandlung.</param>
/// <param name="State">Queued, Sent, Skipped oder Failed.</param>
/// <param name="At">Gesendet am, sonst angelegt am.</param>
/// <param name="AppointmentStart">Nur bei einer Terminerinnerung: der Termin, an den sie erinnert.</param>
/// <param name="Text">Nur bei einem Wiederbuchungs-Hinweis: der persönliche Teil, wie er verschickt wurde.</param>
public sealed record DispatchLogEntryDto(
    Guid Id,
    DispatchKind Kind,
    Guid CustomerId,
    string Recipient,
    string Subject,
    ReminderChannel Channel,
    string State,
    DateTimeOffset At,
    DateTimeOffset? AppointmentStart,
    string? Text,
    string? Error);

/// <summary>Zahlen einer Art im Zeitraum; „Entfallen“ sind Nachrichten, die nicht nötig waren oder nicht gingen (z. B. Termin abgesagt, keine Adresse).</summary>
public sealed record DispatchKindSummaryDto(int Sent, int Failed, int Skipped);

/// <summary>Zusammenfassung des Versandprotokolls der letzten Tage, je Art.</summary>
public sealed record DispatchSummaryDto(int Days, DispatchKindSummaryDto Reminders, DispatchKindSummaryDto Rebook);
