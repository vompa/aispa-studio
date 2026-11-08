namespace AispaStudio.Contracts;

/// <summary>
/// Art einer Raumsperre (ADR 0076): <c>Maintenance</c> ist die geplante Wartung, <c>Damage</c> ein Schaden (Wasserschaden, Defekt), <c>Renovation</c> ein Umbau oder
/// Renovierung, <c>Other</c> alles übrige.
/// </summary>
public enum RoomClosureKind
{
    Maintenance,
    Damage,
    Renovation,
    Other,
}

/// <summary>
/// Raumsperre anlegen oder ändern. <c>RoomId</c> ist der gesperrte Raum, <c>FromDate</c> und <c>UntilDate</c> sind Kalendertage am Standort, beide einschließlich. Ohne
/// <c>FromTime</c> beginnt sie um 0:00 Uhr, ohne <c>UntilTime</c> reicht sie bis zum Ende des letzten Tages; mit Uhrzeiten entstehen halbe Tage („ab 13:00“).
/// <c>Reason</c> ist der Grund für das Team („Wasserschaden, Trocknung bis Freitag“).
/// </summary>
public sealed record RoomClosureInput(
    Guid RoomId,
    DateOnly FromDate,
    TimeOnly? FromTime,
    DateOnly UntilDate,
    TimeOnly? UntilTime,
    RoomClosureKind Kind,
    string? Reason);

/// <summary>
/// Eine Raumsperre mit Zustand. <c>From</c> und <c>Until</c> sind der Zeitraum in UTC (<c>Until</c> ausschließlich), <c>OpenAppointments</c> die noch offenen Termine
/// im gesperrten Raum darin (gebucht oder eingecheckt, nicht vorbei).
/// </summary>
public sealed record RoomClosureDto(
    Guid Id,
    Guid LocationId,
    Guid RoomId,
    string RoomName,
    DateOnly FromDate,
    TimeOnly? FromTime,
    DateOnly UntilDate,
    TimeOnly? UntilTime,
    DateTimeOffset From,
    DateTimeOffset Until,
    RoomClosureKind Kind,
    string? Reason,
    ClosurePhase Phase,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    int OpenAppointments);

/// <summary>
/// Raumsperre im Kalender: der Zeitraum in der Spalte des Raums (schraffiert, Grund im Kopf). <c>Title</c> ist die Art in Worten („Wartung“), <c>Reason</c> der Grund,
/// <c>OpenAppointments</c> zählt die noch offenen Termine im Raum im ganzen Zeitraum. <c>Urgent</c>: die Sperre läuft oder beginnt in den nächsten 14 Tagen; die Meldung
/// dazu ist dann ein Fehler, sonst eine Warnung (wie bei den Schließzeiten, ADR 0073 Ä49).
/// </summary>
public sealed record RoomClosureBand(
    Guid Id,
    Guid RoomId,
    string RoomName,
    DateTimeOffset From,
    DateTimeOffset Until,
    RoomClosureKind Kind,
    string Title,
    string? Reason,
    int OpenAppointments,
    bool Urgent = false);
