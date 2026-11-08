namespace AispaStudio.Contracts;

/// <summary>Art eines Eintrags der Personalakte (R05 Slice 110, Ä252).</summary>
public enum HrEntryKind
{
    Conversation,
    Note,
    Warning,
}

/// <summary>Anlass eines Mitarbeitergesprächs.</summary>
public enum HrConversationKind
{
    Annual,
    Feedback,
    Return,
    Occasion,
}

/// <summary>Eine Vereinbarung aus einem Mitarbeitergespräch.</summary>
public sealed record HrAgreement(string Text, bool IsDone);

/// <summary>Eintrag anlegen oder ändern. Die Art ändert sich nach dem Anlegen nicht; Felder einer anderen Art speichert die API nicht.</summary>
/// <param name="Text">Notizen des Gesprächs, Text der Anmerkung oder Grund der Verwarnung.</param>
/// <param name="ExpiresOn">Nur Verwarnung: letzter Tag; leer heißt die Vorgabe von 2 Jahren ab dem Datum, die Regel steht in der API.</param>
public sealed record HrEntryInput(
    HrEntryKind Kind,
    DateOnly Date,
    string Text,
    HrConversationKind? ConversationKind = null,
    string? Participants = null,
    IReadOnlyList<HrAgreement>? Agreements = null,
    DateOnly? FollowUpOn = null,
    bool FollowUpDone = false,
    string? IssuedBy = null,
    bool HandedOverInWriting = false,
    DateOnly? ExpiresOn = null);

public sealed record HrEntryDto(
    Guid Id,
    HrEntryKind Kind,
    DateOnly Date,
    string Text,
    HrConversationKind? ConversationKind,
    string? Participants,
    IReadOnlyList<HrAgreement> Agreements,
    DateOnly? FollowUpOn,
    bool FollowUpDone,
    string? IssuedBy,
    bool HandedOverInWriting,
    DateOnly? ExpiresOn);

/// <summary>Ein fälliger Folgetermin als Erinnerung bei der Leitung, z. B. „Folgegespräch mit Lena“.</summary>
public sealed record HrFollowUpDto(Guid EntryId, Guid EmployeeId, string Title, DateOnly FollowUpOn);
