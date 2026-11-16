namespace AispaStudio.Contracts;

/// <summary>
/// Kennzahlen zu einer Person für die Übersicht der Personenkarte. Jeder Teil gilt mit dem Recht des jeweiligen Einzel-Endpunkts und ist ohne dieses Recht <c>null</c>:
/// <c>NextShift</c>, <c>WeekPlannedHours</c>, <c>WeekActualHours</c> und <c>Leave</c> mit Standortleitung oder Personalunterlagen, <c>OpenAbsenceRequests</c> mit Standortleitung,
/// <c>DueCertificates</c> und <c>ExpiredCertificates</c> mit der Personalakte. <c>WeekPlannedHours</c> und <c>WeekActualHours</c> gelten für die laufende Woche (Montag bis Sonntag)
/// über alle Standorte, die die angemeldete Person sehen darf; <c>ExpiredCertificates</c> ist der abgelaufene Teil von <c>DueCertificates</c>.
/// </summary>
public sealed record EmployeeOverviewDto(
    Guid EmployeeId,
    EmployeeNextShiftDto? NextShift,
    decimal? WeekPlannedHours,
    decimal? WeekActualHours,
    EmployeeLeaveSummaryDto? Leave,
    int? OpenAbsenceRequests,
    int? DueCertificates,
    int? ExpiredCertificates);

/// <summary>Der nächste veröffentlichte Dienst der Person, in Ortszeit des Standorts.</summary>
public sealed record EmployeeNextShiftDto(DateOnly Date, TimeOnly Start, TimeOnly End, Guid LocationId, string LocationName);

/// <summary>Urlaubskonto des laufenden Kalenderjahres in Arbeitstagen; <c>Remaining</c> fehlt, solange kein Anspruch hinterlegt ist.</summary>
public sealed record EmployeeLeaveSummaryDto(int Year, decimal? Remaining, decimal Planned, decimal Taken);
