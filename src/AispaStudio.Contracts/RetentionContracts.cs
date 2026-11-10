namespace AispaStudio.Contracts;

/// <summary>
/// Einwilligung eines Gastes in Erinnerungen an die nächste Behandlung. Das ist Werbung (§ 7 UWG, § 174 TKG) und braucht eine
/// bestätigte Einwilligung (Double-Opt-in, ADR 0040).
/// </summary>
public enum MarketingConsentState
{
    None,
    Pending,
    Confirmed,
    Withdrawn,

    /// <summary>Der Gast hat am Empfang abgelehnt („Nein, keine Werbung“); anders als <see cref="Withdrawn"/> gab es vorher keine Einwilligung.</summary>
    Refused,
}

/// <summary>Was der Gast am Empfang gesagt hat: <see cref="MarketingConsentState.Confirmed"/> (Ja), <see cref="MarketingConsentState.Refused"/> (Nein) oder <see cref="MarketingConsentState.None"/> (nicht gefragt).</summary>
public sealed record MarketingConsentDecision(MarketingConsentState State);

/// <param name="Email">Adresse, an die angefragt wurde; Erinnerungen gehen nur an genau diese.</param>
/// <param name="Source">Woher die Einwilligung kommt: „Team“ (Anfrage per Mail mit Double-Opt-in), „Online“, „Rezeption“ (am Empfang erfasst) oder „Import“.</param>
/// <param name="RecordedBy">Bei „Rezeption“: wer „Ja“ oder „Nein“ am Empfang erfasst hat, aus dem Verlauf; sonst leer.</param>
public sealed record MarketingConsentDto(
    MarketingConsentState State,
    string? Email,
    string? Language,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? WithdrawnAt,
    string? Source,
    string? RecordedBy = null);

/// <param name="Language">Sprache der Mails an den Gast (de, en, fr, it); ohne Angabe Deutsch.</param>
public sealed record MarketingConsentRequest(string? Language);

/// <param name="Days">Nach so vielen Tagen seit der letzten abgeschlossenen Behandlung kommt die Erinnerung.</param>
public sealed record RebookIntervalDto(Guid TreatmentId, int Days);

public sealed record RebookSettingsDto(bool Enabled, IReadOnlyList<RebookIntervalDto> Intervals);

/// <summary>Eine Erinnerung an die nächste Behandlung, wie sie verschickt wurde (oder warum nicht).</summary>
/// <param name="AiWritten">Den persönlichen Teil hat die KI geschrieben; sonst der feste Text.</param>
public sealed record RebookReminderDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string TreatmentName,
    string State,
    bool AiWritten,
    string? Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string? Error);

/// <summary>Seite im Kundenbereich hinter dem Link aus der Mail: bestätigen oder widerrufen.</summary>
public sealed record OnlineConsentDto(string SpaName, string FirstName, MarketingConsentState State);
