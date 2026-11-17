namespace AispaStudio.Contracts;

/// <summary>Was ein Eintrag der Übersicht ist: eine Schließzeit des ganzen Standorts oder die Sperre eines einzelnen Raums.</summary>
public enum ClosureEntryType
{
    Location,
    Room,
}

/// <summary>Welche Einträge die Übersicht liefert: die kommenden (laufende und geplante), die vergangenen (beendete) oder beide.</summary>
public enum ClosureScope
{
    Upcoming,
    Past,
    All,
}

/// <summary>
/// Ein Eintrag der gemeinsamen Übersicht von Schließzeiten und Raumsperren. <c>Type</c> sagt, was es ist, und damit, wie <c>Kind</c> zu lesen ist: der Name der Art, bei <c>Location</c> ein Wert
/// von <c>ClosureKind</c> (<c>Vacation</c>, <c>SpecialClosure</c>, <c>Outage</c>), bei <c>Room</c> ein Wert von <c>RoomClosureKind</c> (<c>Maintenance</c>, <c>Damage</c>, <c>Renovation</c>,
/// <c>Other</c>); bei <c>Room</c> stehen außerdem der Raum in <c>RoomId</c> und <c>RoomName</c>. <c>Reason</c> ist der Grund für das Team (bei einer Schließzeit ersatzweise der Text für Gäste).
/// <c>OpenAppointments</c> zählt die noch offenen Termine im Zeitraum (bei einer Raumsperre nur die im Raum), <c>OpenWaitlist</c> die Wartelisten-Einträge einer Schließzeit.
/// Die Einzelheiten und das Bearbeiten laufen über die Wege der jeweiligen Art.
/// </summary>
public sealed record ClosureOverviewItem(
    Guid Id,
    ClosureEntryType Type,
    string Kind,
    Guid LocationId,
    DateOnly FromDate,
    TimeOnly? FromTime,
    DateOnly UntilDate,
    TimeOnly? UntilTime,
    DateTimeOffset From,
    DateTimeOffset Until,
    ClosurePhase Phase,
    string? Reason,
    int OpenAppointments,
    int OpenWaitlist,
    Guid? RoomId = null,
    string? RoomName = null);

/// <summary>
/// Übersicht der Schließzeiten und Raumsperren eines Standorts. <c>Items</c> folgen dem gewählten Umfang: die kommenden nach Beginn aufsteigend, die vergangenen nach Beginn absteigend
/// (bei „alle“ zuerst die kommenden). <c>UpcomingCount</c> und <c>PastCount</c> zählen unabhängig vom Umfang alle Einträge der Art, damit die Maske „Vergangene (3)“ zeigen kann, ohne sie zu laden.
/// </summary>
public sealed record ClosureOverview(Guid LocationId, int UpcomingCount, int PastCount, List<ClosureOverviewItem> Items);
