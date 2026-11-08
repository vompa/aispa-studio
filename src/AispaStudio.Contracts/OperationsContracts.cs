namespace AispaStudio.Contracts;

// Betrieb im Haus: „Zum Termin bereitstellen“ (Getränke, Früchteteller, Extras), Rezeptionshandbuch des Spas und Bedarfsprüfung des Lagers.

// ---------- Zum Termin bereitstellen ----------

/// <summary>Wann ein Extra bereitgestellt wird: bei der Ankunft (Terminbeginn), einige Minuten vor Ende der Behandlung oder danach.</summary>
public enum ExtraTiming
{
    OnArrival,
    BeforeEnd,
    AfterTreatment,
}

/// <summary>
/// Eine Zeile von „Zum Termin bereitstellen“ je Behandlung, z. B. „Früchteteller“ 10 Minuten vor Ende. <c>MinutesBeforeEnd</c> gilt nur bei
/// <see cref="ExtraTiming.BeforeEnd"/>. Mit <c>StockItemId</c> und <c>Quantity</c> (je Termin) bucht jede abgeschlossene Behandlung die Menge im Lager ab.
/// </summary>
public sealed record TreatmentExtraInput(string Name, ExtraTiming Timing, int? MinutesBeforeEnd = null, Guid? StockItemId = null, decimal Quantity = 0);

public sealed record TreatmentExtraDto(
    Guid Id, string Name, ExtraTiming Timing, int? MinutesBeforeEnd, Guid? StockItemId, string? StockItemName, string? Unit, decimal Quantity);

/// <summary>
/// Ein „Bereitstellen“-Eintrag eines Termins mit Uhrzeit (<c>DueAt</c>). <c>Wishes</c>: freie „Wünsche des Gastes“ von der Kundenkarte (z. B. Teesorte),
/// nie Gesundheitsdaten; <c>AnamnesisOnFile</c> weist nur darauf hin, dass ein Gesundheitsfragebogen vorliegt („Anamnese beachten“), ohne Inhalt.
/// </summary>
public sealed record ServingItemDto(
    Guid AppointmentId,
    Guid ExtraId,
    string Text,
    DateTimeOffset DueAt,
    string TreatmentName,
    Guid RoomId,
    string RoomName,
    Guid CustomerId,
    string CustomerName,
    string? Wishes,
    bool AnamnesisOnFile,
    bool Done,
    DateTimeOffset? DoneAt,
    string? DoneBy);

/// <summary>Summe eines Eintrags über den Tag: „6 Früchteteller“ mit den Uhrzeiten (aufsteigend).</summary>
public sealed record ServingTotalDto(string Text, int Count, IReadOnlyList<DateTimeOffset> DueTimes);

public sealed record ServingDayDto(Guid LocationId, DateOnly Date, IReadOnlyList<ServingItemDto> Items, IReadOnlyList<ServingTotalDto> Totals);

public sealed record ServingCheck(bool Done);

// ---------- Rezeptionshandbuch des Spas ----------

/// <summary>
/// Eintrag im Rezeptionshandbuch des Spas (Wissensbasis des Hauses, z. B. „Gäste mit Hund“, „Schlüssel Saunabereich“).
/// <c>RemindAtDayStart</c>: „wichtig – am Tagesstart erinnern“; <c>Days</c>: nur an diesen Wochentagen, leer heißt täglich.
/// </summary>
public sealed record HandbookEntryInput(string Title, string Text, string? Area, bool RemindAtDayStart, IReadOnlyList<DayOfWeek> Days, bool IsActive = true);

public sealed record HandbookEntryDto(
    Guid Id, string Title, string Text, string? Area, bool RemindAtDayStart, IReadOnlyList<DayOfWeek> Days, bool IsActive, DateTimeOffset UpdatedAt);

// ---------- Bedarfsprüfung des Lagers ----------

/// <summary>
/// Wie ernst eine Meldung der Bedarfsprüfung ist: <c>Insufficient</c> der Bestand reicht nicht für alle gebuchten Termine, <c>BelowMinimum</c>
/// danach bleibt weniger als die Mindestmenge, <c>LowCoverage</c> danach reicht der Rest nur noch für wenige Behandlungen.
/// </summary>
public enum StockWarningKind
{
    Insufficient,
    BelowMinimum,
    LowCoverage,
}

/// <summary>
/// Bedarf eines Materials in einem Zeitraum aus den gebuchten, noch offenen Terminen (Material der Behandlung und Extras). <c>TreatmentsLeft</c>:
/// für wie viele weitere Behandlungen der Bestand nach diesen Terminen noch reicht (nach durchschnittlichem Verbrauch je Behandlung).
/// <c>ShortFrom</c>: ab diesem Termin fehlt das Material (nur bei <see cref="StockWarningKind.Insufficient"/>). <c>Shortfall</c>: was nach allen Terminen fehlt, sonst 0.
/// </summary>
public sealed record StockDemandLine(
    Guid ItemId,
    string ItemName,
    string Unit,
    decimal OnHand,
    decimal Needed,
    decimal MinimumQuantity,
    int Appointments,
    int TreatmentsLeft,
    StockWarningKind? Warning,
    DateTimeOffset? ShortFrom,
    string? Message,
    decimal Shortfall = 0);

public sealed record StockDemandDto(
    Guid LocationId,
    DateOnly From,
    DateOnly Until,
    int Appointments,
    IReadOnlyList<StockDemandLine> Lines,
    IReadOnlyList<StockDemandLine> Warnings);

// ---------- Raumpflege, Tagesstart-Kontrolle und Abnahme ----------

/// <summary>Raumpflege eines Termins: offen, erledigt (abgehakt) oder überfällig (Pflegezeit vorbei, nicht abgehakt).</summary>
public enum RoomCareState
{
    Open,
    Done,
    Overdue,
}

/// <summary>
/// Aufgabe „Raum X pflegen bis HH:MM“ nach einem Termin. <c>AppointmentId</c> ist der Termin, an dem sie hängt; belegen mehrere Termine den
/// Raum gleichzeitig (Paarsuite), sind sie eine Aufgabe, und <c>AppointmentIds</c> nennt alle. <c>From</c> bis <c>Until</c> ist die Pflegezeit des Raums,
/// <c>EmployeeId</c> die Person des Termins.
/// </summary>
public sealed record RoomCareTaskDto(
    Guid AppointmentId,
    IReadOnlyList<Guid> AppointmentIds,
    Guid RoomId,
    string RoomName,
    string Text,
    DateTimeOffset From,
    DateTimeOffset Until,
    Guid EmployeeId,
    RoomCareState State,
    DateTimeOffset? DoneAt,
    string? DoneBy,
    IReadOnlyList<RestockLine>? Restock = null);

/// <summary>Antwort auf eine Zeile des Raumchecks: <c>Open</c> noch keine, <c>Refilled</c> aus dem Hauptlager aufgefüllt, <c>Enough</c> „reicht noch“.</summary>
public enum RestockState
{
    Open,
    Refilled,
    Enough,
}

/// <summary>
/// Zeile des Raumchecks nach der Behandlung (ADR 0068): ein Material des Raumschranks mit Bestand und Mindestmenge des Raums, z. B. „Massageöl neutral:
/// 200 von 500 ml“. <c>Suggested</c> ist die Menge bis zur Mindestmenge (nie unter 0).
/// </summary>
public sealed record RestockLine(Guid ItemId, string ItemName, string Unit, decimal Quantity, decimal Minimum, decimal Suggested, RestockState State);

/// <summary>Antwort auf eine Zeile: <c>Refilled</c> bucht <c>Quantity</c> (ohne Angabe die vorgeschlagene Menge) vom Hauptlager in den Raumschrank, <c>Enough</c> bucht nichts.</summary>
public sealed record RestockAnswer(RestockState State, decimal? Quantity = null);

/// <summary>Raumausstattung am Tagesstart: <c>Complete</c>, wenn der Raumschrank bei keinem Material unter seiner Mindestmenge liegt; <c>Below</c> nennt sie sonst.</summary>
public sealed record RoomEquipmentDto(bool Complete, IReadOnlyList<RestockLine> Below);

/// <summary>
/// Stand der Tagesstart-Kontrolle eines Raums an einem Tag: <c>NoChecklist</c> der Raum hat keine Checkliste, <c>Open</c> nichts abgehakt,
/// <c>Partial</c> einiges, <c>Checked</c> alles abgehakt (Raum freigegeben), <c>Approved</c> von der Leitung abgenommen.
/// </summary>
public enum RoomStartState
{
    NoChecklist,
    Open,
    Partial,
    Checked,
    Approved,
}

/// <summary>Zustand eines Raums am Tag für den Kalender: Tagesstart-Kontrolle und Raumpflege mit den Abnahmen.</summary>
public sealed record RoomDayStateDto(
    Guid RoomId,
    string RoomName,
    RoomStartState Start,
    int StartItems,
    int StartChecked,
    DateTimeOffset? StartApprovedAt,
    string? StartApprovedBy,
    int CareOpen,
    int CareOverdue,
    DateTimeOffset? CareApprovedAt,
    string? CareApprovedBy);

/// <summary><c>RoomsBelowMinimum</c>: Räume des Standorts, deren Raumschrank bei mindestens einem Material unter der Mindestmenge liegt (Betriebsleiste).</summary>
public sealed record RoomCareDay(IReadOnlyList<RoomCareTaskDto> Tasks, IReadOnlyList<RoomDayStateDto> Rooms, int RoomsBelowMinimum = 0);

public sealed record RoomCareCheckInput(bool Done);

public sealed record RoomStartItemInput(string Text);

public sealed record RoomStartItemDto(Guid Id, string Text);

public sealed record RoomStartCheckInput(DateOnly Date, bool Done);

public sealed record RoomStartItemStateDto(Guid ItemId, string Text, bool Done, DateTimeOffset? DoneAt, string? DoneBy);

public sealed record RoomStartRoomDto(
    Guid RoomId,
    string RoomName,
    RoomStartState State,
    IReadOnlyList<RoomStartItemStateDto> Items,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    RoomEquipmentDto? Equipment = null);

public sealed record RoomStartDayDto(Guid LocationId, DateOnly Date, IReadOnlyList<RoomStartRoomDto> Rooms);

/// <summary>Was die Standortleitung abnimmt: den Tagesstart eines Raums oder die Raumpflege eines Raums an einem Tag.</summary>
public enum RoomApprovalKind
{
    DayStart,
    RoomCare,
}

/// <summary>
/// Abnahme durch die Standortleitung. Hat die angemeldete Person das Recht „Standort verwalten“ nicht, bestätigt die Leitung mit Name und PIN
/// (<c>Approval</c>) am selben Platz, ohne sich anzumelden.
/// </summary>
public sealed record RoomApprovalInput(DateOnly Date, RoomApprovalKind Kind, SupervisorApproval? Approval = null);

public sealed record RoomApprovalDto(Guid RoomId, DateOnly Date, RoomApprovalKind Kind, DateTimeOffset ApprovedAt, string? ApprovedBy);

/// <summary>Eine Zeile des Hygieneprotokolls: ein Punkt der Tagesstart-Kontrolle oder eine Raumpflege, mit Erledigung und Abnahme.</summary>
public sealed record HygieneRowDto(
    DateOnly Date,
    Guid RoomId,
    string RoomName,
    RoomApprovalKind Kind,
    string Item,
    DateTimeOffset? Due,
    bool Done,
    DateTimeOffset? DoneAt,
    string? DoneBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy);

public sealed record HygieneLogDto(Guid LocationId, DateOnly From, DateOnly Until, IReadOnlyList<HygieneRowDto> Rows);

// ---------- Live ----------

/// <summary>Was sich im Betrieb eines Standorts geändert hat; die Oberfläche lädt danach nur diese Quelle der Meldungen nach.</summary>
public enum OperationsChangeKind
{
    Stock,
    RoomCare,
    RoomStart,
    Reminders,
}
