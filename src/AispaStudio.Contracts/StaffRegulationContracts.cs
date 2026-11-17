namespace AispaStudio.Contracts;

/// <summary>Wer einen Hinweis zu einer Person sieht (R05 Slice 111, Ä252).</summary>
public enum StaffNoteVisibility
{
    /// <summary>Nur Inhaber und Manager.</summary>
    Leadership,

    /// <summary>Alle, die den Kalender oder den Dienstplan sehen.</summary>
    Team,
}

/// <summary>Art einer Sonderregelung. Sie warnt beim Planen und Buchen, sperrt aber nie (E10).</summary>
public enum StaffRuleKind
{
    /// <summary>Keine Termine und Dienste, die nach einer Uhrzeit enden.</summary>
    NoAppointmentsAfter,

    /// <summary>Höchstens so viele Stunden pro Tag.</summary>
    MaxHoursPerDay,

    /// <summary>An festen Wochentagen frei.</summary>
    FixedDaysOff,

    /// <summary>Im ganzen Zeitraum keine Dienste und Termine.</summary>
    ProtectionPeriod,
}

/// <summary>Vollmacht aus der festen Liste. Nur Kassa-Storno und Bestellungen auslösen prüft die API, die übrigen sind Anzeige (E11).</summary>
public enum StaffPowerKind
{
    CashVoid,
    CashClosing,
    Safe,
    BankDeposit,
    Keys,
    AcceptDeliveries,
    TriggerOrders,
    DeputyManagement,
}

/// <summary>Hinweis anlegen oder ändern. <c>ShowAsSymbol</c>: als Symbol mit Tooltip im Dienstplan und im Kopf der Kalenderspalte.</summary>
public sealed record StaffNoteInput(string Text, StaffNoteVisibility Visibility, bool ShowAsSymbol = false, DateOnly? ValidFrom = null, DateOnly? ValidUntil = null);

public sealed record StaffNoteDto(
    Guid Id, Guid EmployeeId, string Text, StaffNoteVisibility Visibility, bool ShowAsSymbol, DateOnly? ValidFrom, DateOnly? ValidUntil, bool IsExpired);

/// <summary>Sonderregelung anlegen oder ändern; von den Werten zählt nur der zur Art passende, die API speichert die anderen nicht.</summary>
/// <param name="LatestEnd">Nur <see cref="StaffRuleKind.NoAppointmentsAfter"/>: die Uhrzeit.</param>
/// <param name="MaxHours">Nur <see cref="StaffRuleKind.MaxHoursPerDay"/>: die Stunden pro Tag.</param>
/// <param name="DaysOff">Nur <see cref="StaffRuleKind.FixedDaysOff"/>: die freien Wochentage.</param>
/// <param name="Reason">Grund; steht nur in Antworten an Inhaber und Manager.</param>
public sealed record StaffRuleInput(
    StaffRuleKind Kind,
    DateOnly ValidFrom,
    DateOnly? ValidUntil = null,
    TimeOnly? LatestEnd = null,
    decimal? MaxHours = null,
    IReadOnlyList<DayOfWeek>? DaysOff = null,
    string? Reason = null);

/// <summary><c>Summary</c>: die Regelung in Worten, z. B. „keine Termine nach 16:00“; die Warnung beim Planen nennt denselben Text.</summary>
public sealed record StaffRuleDto(
    Guid Id,
    Guid EmployeeId,
    StaffRuleKind Kind,
    string Summary,
    DateOnly ValidFrom,
    DateOnly? ValidUntil,
    TimeOnly? LatestEnd,
    decimal? MaxHours,
    IReadOnlyList<DayOfWeek> DaysOff,
    string? Reason,
    bool IsExpired);

/// <summary>Vollmacht erteilen oder ändern; „erteilt von“ setzt die API aus der angemeldeten Person.</summary>
public sealed record StaffPowerInput(StaffPowerKind Kind, DateOnly ValidFrom, DateOnly? ValidUntil = null);

public sealed record StaffPowerDto(
    Guid Id, Guid EmployeeId, StaffPowerKind Kind, string Label, DateOnly ValidFrom, DateOnly? ValidUntil, string GrantedBy, bool IsExpired);

/// <summary>Hinweise, Sonderregelungen und Vollmachten einer Person, auch abgelaufene.</summary>
public sealed record StaffRegulationsDto(IReadOnlyList<StaffNoteDto> Notes, IReadOnlyList<StaffRuleDto> Rules, IReadOnlyList<StaffPowerDto> Powers);

/// <summary>
/// Kennzeichen einer Person: die heute gültigen Einträge in Worten, für Karte und Teamliste. <c>Duties</c> (R05 Slice 117): Zuständigkeiten, Warnung bei fehlendem
/// oder abgelaufenem verlangtem Zertifikat, und Zertifikate in der Erinnerungsfrist oder abgelaufen.
/// </summary>
public sealed record StaffFlagsDto(
    Guid EmployeeId, IReadOnlyList<string> Notes, IReadOnlyList<string> Rules, IReadOnlyList<string> Powers, IReadOnlyList<DutyFlagDto>? Duties = null);

/// <summary>Eine Person in „Wer darf was?“. <c>EntryId</c>: der Eintrag der Vollmacht, zum Entfernen unter <c>api/employees/{id}/regulations/powers/{entryId}</c>; fehlt in älteren Antworten.</summary>
public sealed record PowerHolderDto(Guid EmployeeId, string Name, DateOnly ValidFrom, DateOnly? ValidUntil, string GrantedBy, bool IsExpired)
{
    public Guid? EntryId { get; init; }
}

/// <summary>
/// Eine Vollmacht in „Wer darf was?“. <c>IsChecked</c>: die API prüft sie zusätzlich zur Rolle, sonst ist sie nur Anzeige.
/// Für die Tabelle „Rechte und Zuständigkeiten“ kommen fünf optionale Felder dazu (fehlen in älteren Antworten): <c>Group</c> (Thema der Tabelle) mit <c>GroupOrder</c> (Reihenfolge der Themen), <c>Hint</c> (ein Satz, wofür),
/// <c>Effect</c> („Schaltet frei“ oder „Nur Aushang“) und <c>IsUnstaffed</c> (heute hat niemand Aktives sie). Mit <c>locationId</c> in der Anfrage zählen nur Personen,
/// die an dem Standort arbeiten.
/// </summary>
public sealed record PowerOverviewRowDto(StaffPowerKind Kind, string Label, bool IsChecked, IReadOnlyList<PowerHolderDto> Holders)
{
    public string? Group { get; init; }

    public int? GroupOrder { get; init; }

    public string? Hint { get; init; }

    public string? Effect { get; init; }

    public bool? IsUnstaffed { get; init; }
}

/// <summary>Ein Hinweis als Symbol im Dienstplan und im Kopf der Kalenderspalte.</summary>
public sealed record StaffNoteSymbolDto(Guid EmployeeId, string Text);
