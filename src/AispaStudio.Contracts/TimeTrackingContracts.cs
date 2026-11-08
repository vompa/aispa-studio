namespace AispaStudio.Contracts;

public enum TimeRecordSource
{
    /// <summary>Selbst gestempelt (Kommen, Pause, Gehen).</summary>
    Clock,

    /// <summary>Von der Standortleitung nachgetragen oder geändert.</summary>
    Manual,
}

/// <summary>Ein Zeitabschnitt gearbeiteter Zeit. Pausen sind die Lücken zwischen zwei Abschnitten. Ohne Ende läuft er noch.</summary>
public sealed record TimeRecordDto(
    Guid Id,
    Guid EmployeeId,
    Guid LocationId,
    DateTimeOffset Start,
    DateTimeOffset? End,
    TimeRecordSource Source,
    bool Changed);

/// <summary>Stempeln: Standort für „Kommen“ und „Weiter nach der Pause“.</summary>
public sealed record ClockInput(Guid LocationId);

/// <summary>Eigener Stand: läuft gerade ein Abschnitt, und was wurde heute schon erfasst.</summary>
public sealed record MyTimeStatus(
    Guid? EmployeeId,
    TimeRecordDto? Running,
    IReadOnlyList<TimeRecordDto> Today,
    double HoursToday,
    int BreakMinutesToday);

/// <summary>Nachtragen oder Ändern durch die Standortleitung. Der Grund ist Pflicht und bleibt im Änderungsprotokoll.</summary>
public sealed record TimeRecordInput(Guid EmployeeId, DateTimeOffset Start, DateTimeOffset? End, string Reason);

public sealed record TimeRecordDeletion(string Reason);

public enum TimeRecordAction
{
    Created,
    Changed,
    Deleted,
}

/// <summary>Eine Zeile im Änderungsprotokoll eines Abschnitts.</summary>
public sealed record TimeRecordChangeDto(
    DateTimeOffset ChangedAt,
    string ChangedBy,
    TimeRecordAction Action,
    DateTimeOffset? OldStart,
    DateTimeOffset? OldEnd,
    DateTimeOffset? NewStart,
    DateTimeOffset? NewEnd,
    string? Reason);

/// <summary>Soll und Ist einer Person an einem Tag.</summary>
public sealed record TimesheetDay(
    DateOnly Date,
    double PlannedHours,
    double ActualHours,
    int BreakMinutes,
    IReadOnlyList<TimeRecordDto> Records,
    IReadOnlyList<string> Warnings);

public sealed record TimesheetRow(
    Guid EmployeeId,
    string DisplayName,
    string Color,
    IReadOnlyList<TimesheetDay> Days,
    double PlannedHours,
    double ActualHours,
    IReadOnlyList<string> Warnings);

/// <summary>Zeiterfassung eines Standorts: alle Personen, die dort eingeteilt sind oder gestempelt haben.</summary>
public sealed record Timesheet(Guid LocationId, string Country, DateOnly From, IReadOnlyList<DateOnly> Dates, IReadOnlyList<TimesheetRow> Employees);
