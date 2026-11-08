namespace AispaStudio.Contracts;

/// <summary>Zielprogramm der Lohnverrechnung: DATEV LODAS (ASCII-Import), DATEV Lohn und Gehalt (Bewegungsdaten) oder BMD Lohn (CSV).</summary>
public enum PayrollFormat
{
    DatevLodas,
    DatevLohnUndGehalt,
    BmdLohn,
}

/// <summary>Was je Person und Monat an die Lohnverrechnung geht. Stunden aus den Diensten, Abwesenheiten in Tagen.</summary>
public enum PayrollItem
{
    /// <summary>Gearbeitete Stunden gesamt.</summary>
    Hours,

    /// <summary>Davon an Sonntagen (ohne Feiertage).</summary>
    SundayHours,

    /// <summary>Davon an gesetzlichen Feiertagen.</summary>
    HolidayHours,

    /// <summary>Davon in der Nachtzeit (siehe <c>NightStart</c>/<c>NightEnd</c>).</summary>
    NightHours,

    VacationDays,
    SickDays,
    TrainingDays,
}

/// <summary>
/// Einstellungen für den Lohnexport, eine Zeile je Unternehmen. <c>Country</c> bestimmt die gesetzlichen Feiertage (bundesweit).
/// Je Position die Lohnart der Steuerberatung bzw. Lohnverrechnung; leer heißt „nicht übergeben“.
/// </summary>
public sealed record PayrollSettingsDto(
    FiscalCountry Country,
    string? DatevConsultantNumber,
    string? DatevClientNumber,
    TimeOnly NightStart,
    TimeOnly NightEnd,
    string? HoursWageType,
    string? SundayWageType,
    string? HolidayWageType,
    string? NightWageType,
    string? VacationWageType,
    string? SickWageType,
    string? TrainingWageType);

/// <summary>Person mit Personalnummer der Lohnverrechnung (Dienstnehmernummer in BMD, Personalnummer in DATEV).</summary>
public sealed record PayrollEmployeeDto(Guid EmployeeId, string DisplayName, bool IsActive, string? PersonnelNumber);

public sealed record PersonnelNumberInput(string? PersonnelNumber);

/// <summary>Eine Position des Exports: Menge in Stunden (h) oder Tagen.</summary>
public sealed record PayrollLine(
    Guid EmployeeId,
    string DisplayName,
    string? PersonnelNumber,
    PayrollItem Item,
    decimal Quantity,
    string Unit,
    string? WageType);

/// <summary>Vorschau des Lohnexports eines Monats. <c>Warnings</c> nennt, was vor der Übergabe zu klären ist.</summary>
public sealed record PayrollPreviewDto(int Year, int Month, FiscalCountry Country, IReadOnlyList<PayrollLine> Lines, IReadOnlyList<string> Warnings);
