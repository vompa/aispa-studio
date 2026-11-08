namespace AispaStudio.Contracts;

/// <summary>Art eines Dienstplaneintrags.</summary>
public enum RosterKind
{
    /// <summary>Dienst an einem Standort, mit Beginn und Ende.</summary>
    Work,

    /// <summary>Urlaub.</summary>
    Vacation,

    /// <summary>Krankenstand.</summary>
    Sick,

    /// <summary>Frei (z. B. Zeitausgleich, getauschter Dienst).</summary>
    DayOff,

    /// <summary>Schulung außer Haus.</summary>
    Training,
}

/// <summary>Woher die Zeiten eines Tages stammen: aus der regelmäßigen Wochenarbeitszeit oder aus dem Dienstplan für genau diesen Tag.</summary>
public enum RosterSource
{
    Pattern,
    Plan,
}

/// <summary>Ein Eintrag im Dienstplan. Bei <see cref="RosterKind.Work"/> sind Standort, Beginn und Ende Pflicht, sonst leer.</summary>
public sealed record RosterEntry(RosterKind Kind, Guid? LocationId, TimeOnly? Start, TimeOnly? End);

/// <summary>
/// Dienstplan einer Person für einen Tag. Ersetzt das Wochenmuster für diesen Tag an allen Standorten.
/// <c>Draft</c>: nur als Entwurf speichern; er gilt erst nach dem Veröffentlichen (sonst sofort, z. B. für einen Krankenstand).
/// <c>RulesConfirmed</c>: trotz einer Sonderregelung der Person speichern (R05 Slice 111, E10); ohne sie antwortet die API mit 409 und der Regelung.
/// </summary>
public sealed record RosterDayInput(IReadOnlyList<RosterEntry> Entries, string? Note, bool Draft = false, bool RulesConfirmed = false);

/// <summary><c>Draft</c>: Die Zelle zeigt einen noch nicht veröffentlichten Entwurf; Kalender und Buchung richten sich noch nach dem alten Stand.</summary>
public sealed record RosterCell(
    DateOnly Date,
    RosterSource Source,
    IReadOnlyList<RosterEntry> Entries,
    string? Note,
    IReadOnlyList<string> Warnings,
    bool Draft = false);

public sealed record RosterRow(Guid EmployeeId, string DisplayName, string Color, IReadOnlyList<RosterCell> Days, double PlannedHours, IReadOnlyList<string> Warnings);

/// <summary>
/// Dienstplan eines Standorts für mehrere Tage. Enthält auch Dienste der Personen an anderen Standorten, damit Überschneidungen und Ruhezeiten sichtbar sind.
/// <c>Drafts</c>: Entwürfe dieses Standorts im Zeitraum, die noch zu veröffentlichen sind. <c>LastPublishedAt</c>: letzte Veröffentlichung des Standorts.
/// </summary>
public sealed record RosterWeek(
    Guid LocationId,
    DateOnly From,
    IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<RosterRow> Employees,
    int Drafts = 0,
    DateTimeOffset? LastPublishedAt = null);

/// <summary>Ergebnis einer Änderung: der neue Tag und Termine, die jetzt außerhalb der Dienstzeit liegen (bitte verschieben).</summary>
public sealed record RosterDayResult(RosterCell Day, IReadOnlyList<AppointmentDto> AppointmentsOutsideShift);

/// <summary>Entwürfe eines Standorts im Zeitraum [From, Until] veröffentlichen oder verwerfen (höchstens 62 Tage).</summary>
public sealed record RosterPublishRequest(DateOnly From, DateOnly Until);

/// <summary>
/// Ergebnis der Veröffentlichung: wie viele Tage jetzt gelten, Hinweise auf kurzfristige Änderungen (Ankündigungsfrist des Landes)
/// und Termine, die jetzt außerhalb eines Dienstes liegen.
/// </summary>
public sealed record RosterPublishResult(
    int Days,
    DateTimeOffset PublishedAt,
    IReadOnlyList<string> ShortNotice,
    IReadOnlyList<AppointmentDto> AppointmentsOutsideShift);

/// <summary>
/// Ein offener Abwesenheitsantrag aus der Handy-App für Mitarbeitende (Slice 106), den die Windows-App unter „Team“ genehmigt oder ablehnt (Slice 99).
/// <c>Kind</c> ist Urlaub, Krank, Frei oder Schulung; <c>AffectedAppointments</c> zählt die Termine der Person im Zeitraum, die danach außerhalb eines Dienstes lägen.
/// </summary>
public sealed record AbsenceRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    RosterKind Kind,
    DateOnly From,
    DateOnly Until,
    string? Note,
    DateTimeOffset RequestedAt,
    int AffectedAppointments);
