namespace AispaStudio.Contracts;

// Warteliste im Kundenbereich: wer keinen passenden Termin findet, trägt sich für einen Zeitraum ein und bekommt eine Mail,
// sobald durch eine Stornierung etwas frei wird. Gebucht wird dann ganz normal über die Online-Buchung.

/// <summary>Tageszeit in der Zeitzone des Standorts: vormittags bis 12 Uhr, nachmittags 12 bis 17 Uhr, abends ab 17 Uhr.</summary>
public enum DayPart
{
    Any,
    Morning,
    Afternoon,
    Evening,
}

public enum WaitlistStatus
{
    /// <summary>Wartet auf einen frei werdenden Termin.</summary>
    Waiting,

    /// <summary>Im gewünschten Zeitraum wurde ein passender Termin gebucht.</summary>
    Booked,

    /// <summary>Von der Kundin oder dem Kunden selbst ausgetragen.</summary>
    Cancelled,

    /// <summary>Der Zeitraum ist vorbei.</summary>
    Expired,
}

/// <summary>
/// Eintrag auf die Warteliste. <c>From</c>/<c>Until</c>: gewünschte Kalendertage am Standort (höchstens 60 Tage).
/// Gäste geben <c>Contact</c> an, angemeldete Kundinnen und Kunden nur, solange ihr Konto noch keine Kundendaten hat.
/// </summary>
public sealed record WaitlistRequest(
    Guid LocationId,
    Guid TreatmentId,
    DateOnly From,
    DateOnly Until,
    DayPart DayPart,
    OnlineContact? Contact,
    bool AcceptPrivacy,
    string? Language);

/// <summary>Eintrag aus Sicht der Kundin oder des Kunden.</summary>
public sealed record WaitlistEntryDto(
    Guid Id,
    Guid LocationId,
    string LocationName,
    Guid TreatmentId,
    string TreatmentName,
    DateOnly From,
    DateOnly Until,
    DayPart DayPart,
    WaitlistStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastOfferedAt);

/// <summary>Bestätigung des Eintrags. <c>ManageToken</c> steht auch in der Mail und öffnet den Eintrag ohne Anmeldung.</summary>
public sealed record WaitlistConfirmation(WaitlistEntryDto Entry, string ManageToken);

/// <summary>Eintrag für das Team am Standort, mit Kunde und wie oft schon ein freier Termin gemeldet wurde.</summary>
public sealed record WaitlistStaffEntry(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid TreatmentId,
    string TreatmentName,
    DateOnly From,
    DateOnly Until,
    DayPart DayPart,
    WaitlistStatus Status,
    DateTimeOffset CreatedAt,
    int OfferCount,
    DateTimeOffset? LastOfferedAt,
    string? Phone = null);

/// <summary>Warum die Rezeption einen Eintrag schließt: ein Termin ist vereinbart (auch am Telefon), oder der Gast braucht keinen mehr.</summary>
public enum WaitlistCloseReason
{
    Arranged,
    NoLongerWanted,
}

/// <summary>Eintrag von der Warteliste nehmen. <c>Arranged</c> gilt als gebucht, <c>NoLongerWanted</c> wie ausgetragen; den Grund speichert der Server nur so.</summary>
public sealed record WaitlistCloseInput(WaitlistCloseReason Reason);
