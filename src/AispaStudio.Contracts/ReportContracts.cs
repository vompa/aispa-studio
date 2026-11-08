namespace AispaStudio.Contracts;

// ---------- Behandlungskarte und Tagesplan (Therapeuten) ----------

/// <summary>Schritt im „Behandlungsrezept“: was vor der Behandlung vorzubereiten ist; <c>MinutesBefore</c> z. B. 30 für „Steine aufheizen“.</summary>
public sealed record PreparationStepInput(string Text, int? MinutesBefore, Guid? StockItemId = null);

/// <summary>
/// Schritt der Vorbereitung. <c>StockItemId</c>: Lagerartikel, den der Schritt braucht; die Menge je Termin steht schon im Rezept der Behandlung
/// (Material bei den Kosten oder „Zum Termin bereitstellen“) und wird dort abgebucht, nicht hier – so wird nichts doppelt gebucht.
/// <c>Quantity</c> und <c>Unit</c> sind diese Menge zur Anzeige.
/// </summary>
public sealed record PreparationStepDto(
    Guid Id, string Text, int? MinutesBefore, Guid? StockItemId = null, string? StockItemName = null, decimal? Quantity = null, string? Unit = null);

/// <summary>
/// Vorlieben eines Gastes, standortübergreifend. Bewusst keine Gesundheitsdaten: Allergien und Kontraindikationen
/// stehen verschlüsselt im Gesundheitsfragebogen.
/// </summary>
public sealed record CustomerPreferences(
    string? Pressure,
    string? Scent,
    string? RoomTemperature,
    string? Music,
    bool? PrefersQuiet,
    string? FocusAreas,
    string? AvoidAreas,
    string? Other);

/// <summary>Ein Punkt der Vorbereitungs-Todo; <c>DueAt</c> = Behandlungsbeginn minus Vorlauf. <c>Source</c>: Rezept, Vorliebe, Anlass, Notiz.</summary>
public sealed record TodoItem(Guid? StepId, string Text, DateTimeOffset? DueAt, string Source, bool Done);

public sealed record PastTreatment(DateTimeOffset Start, string TreatmentName, string EmployeeName, string? Note);

/// <summary>
/// Behandlungskarte: alles, was der Therapeut für einen Termin wissen und vorbereiten muss.
/// <c>VisitNumber</c> zählt bisherige Termine des Gastes (ohne Absagen); 1 heißt Erstbesuch.
/// <c>Anamnesis</c>: Stand des Gesundheitsfragebogens, nie dessen Inhalt (dafür der eigene, protokollierte Abruf).
/// <c>Restock</c>: Raumcheck nach der Behandlung (ADR 0068), nur bei abgeschlossenem Termin mit Material unter der Mindestmenge des Raumschranks.
/// </summary>
public sealed record TreatmentCard(
    Guid AppointmentId,
    DateTimeOffset Start,
    DateTimeOffset End,
    AppointmentStatus Status,
    string TreatmentName,
    int DurationMinutes,
    string RoomName,
    string? EquipmentName,
    string EmployeeName,
    Guid CustomerId,
    string CustomerName,
    int VisitNumber,
    DateTimeOffset? LastVisit,
    string? Occasion,
    string? BookingNote,
    string AnamnesisStatus,
    CustomerPreferences Preferences,
    IReadOnlyList<PastTreatment> History,
    IReadOnlyList<TodoItem> Todo,
    string? FollowUpNote,
    IReadOnlyList<RestockLine>? Restock = null,
    DateTimeOffset? StartedAt = null,
    bool AllergyFlag = false);

public sealed record PreparationCheck(bool Done);

/// <summary>Nachbereitung: Notiz für die nächste Behandlung, erscheint auf der Karte der Kollegen.</summary>
public sealed record FollowUpNote(string Note);

public sealed record ShiftDto(DateTimeOffset Start, DateTimeOffset End);

/// <summary>Tagesplan einer Person: Dienste und Behandlungskarten in zeitlicher Reihenfolge; <c>Gaps</c> markiert Raumwechsel.</summary>
public sealed record DayPlanDto(
    Guid EmployeeId,
    string EmployeeName,
    DateOnly Date,
    IReadOnlyList<ShiftDto> Shifts,
    IReadOnlyList<TreatmentCard> Cards,
    IReadOnlyList<string> Hints);

// ---------- Tagesstart (Rezeption) ----------

public sealed record BriefingStaff(Guid EmployeeId, string Name, string State, string? Times);

public sealed record BriefingItem(Guid? AppointmentId, DateTimeOffset? At, string Text);

/// <summary>
/// Eine aktive Kassa des Standorts im Tagesstart. <c>OpeningCash</c> ist der gezählte Betrag des letzten Tagesabschlusses, leer ohne Abschluss. <c>Currency</c> folgt dem Land der Kasse
/// (CHF in der Schweiz, sonst EUR). <c>Warnings</c> sind die Hinweise der Kasse (z. B. Signatureinheit nicht erreichbar), ohne den Kassennamen davor.
/// </summary>
public sealed record BriefingRegister(Guid RegisterId, string Code, string Currency, decimal? OpeningCash, IReadOnlyList<string> Warnings);
/// <summary>
/// Tagesbriefing eines Standorts. <c>Summary</c> fasst in zwei, drei Sätzen zusammen, worauf heute zu achten ist.
/// <c>Utilization</c> = gebuchte Behandlungsminuten ÷ Dienstminuten in Prozent.
/// <c>AiSummary</c>: Zusammenfassung der KI aus genau diesen Angaben (nur mit KI); solange sie geschrieben wird, ist <c>AiSummaryPending</c> gesetzt.
/// Die Zahlen kommen nie von der KI. <c>AiNotice</c> ist der Hinweis, der neben dem KI-Text stehen muss.
/// <c>Registers</c>: die Kassen mit Anfangsbestand und Hinweisen als Felder; <c>Register</c> bleibt als Text für die KI und ältere Clients.
/// <c>Servings</c>: Summen des Tages zum Bereitstellen („6 Früchteteller: 11:20, 12:40 …“). <c>Reminders</c>: Einträge des Rezeptionshandbuchs, die an diesem Tag
/// als „wichtig – am Tagesstart erinnern“ gelten.
/// </summary>
public sealed record BriefingDto(
    Guid LocationId,
    DateOnly Date,
    string Summary,
    int Appointments,
    int Utilization,
    IReadOnlyList<BriefingStaff> Staff,
    IReadOnlyList<BriefingItem> ActionNeeded,
    IReadOnlyList<BriefingItem> Guests,
    IReadOnlyList<BriefingItem> Rooms,
    IReadOnlyList<BriefingItem> Register,
    string? AiSummary = null,
    bool AiSummaryPending = false,
    string? AiNotice = null,
    IReadOnlyList<ServingTotalDto>? Servings = null,
    IReadOnlyList<HandbookEntryDto>? Reminders = null,
    IReadOnlyList<BriefingRegister>? Registers = null);

// ---------- Management ----------

public sealed record Kpi(string Key, string Label, decimal Value, decimal? Previous, string Unit);

public sealed record RevenueLine(string Label, string? CostCenter, decimal Gross, decimal Net);

/// <param name="RevenuePerHour">Netto-Umsatz je Behandlungsstunde; leer ohne das Recht „Berichte mit Beträgen“.</param>
/// <param name="Appointments">Termine der Person im Zeitraum (ohne stornierte und nicht erschienene).</param>
public sealed record EmployeeKpi(Guid EmployeeId, string Name, decimal ShiftHours, decimal TreatmentHours, int Utilization, decimal? RevenuePerHour, int Appointments = 0);

/// <summary>Auslastung eines Raums im Zeitraum: belegte Minuten (mit Vor- und Nachbereitung) ÷ Öffnungsminuten in Prozent, und seine Termine.</summary>
public sealed record RoomKpi(Guid RoomId, string Name, int Utilization, int Appointments);

/// <summary>
/// Gast in der Rangliste eines Zeitraums: abgeschlossene Besuche am Standort und, mit dem Recht „Berichte mit Beträgen“, die Summe seiner Belege dort;
/// <c>Spent</c> ist sonst leer.
/// </summary>
public sealed record GuestKpi(Guid CustomerId, string Name, int Visits, decimal? Spent);

/// <param name="NetRevenue">Netto-Umsatz der Behandlung; leer ohne das Recht „Berichte mit Beträgen“. Dann ist die Liste nach Anzahl geordnet.</param>
public sealed record TreatmentKpi(Guid TreatmentId, string Name, int Appointments, decimal? NetRevenue, decimal? MarginPerAppointment);

/// <summary>
/// Management-Cockpit für einen Zeitraum (Ortszeit, beide Tage eingeschlossen) mit Vergleich zum gleich langen Zeitraum davor.
/// Umsätze aus den signierten Belegen der Kassen des Standorts, Termine und Auslastung aus dem Kalender.
/// <c>AmountsHidden</c>: ohne das Recht „Berichte mit Beträgen“ fehlen alle Geldbeträge (Umsatz-Kennzahlen, Umsatz nach Konto und Zahlart,
/// Umsatz je Behandlung und Person, Margen); Auslastung, Anzahlen und Quoten bleiben.
/// </summary>
public sealed record ManagementReportDto(
    Guid LocationId,
    DateOnly From,
    DateOnly Until,
    IReadOnlyList<Kpi> Kpis,
    IReadOnlyList<RevenueLine> RevenueByAccount,
    IReadOnlyList<RevenueLine> RevenueByPayment,
    IReadOnlyList<EmployeeKpi> Employees,
    IReadOnlyList<TreatmentKpi> Treatments,
    IReadOnlyList<string> Alerts,
    bool AmountsHidden = false,
    int NoShows = 0,
    int Cancellations = 0);

// ---------- Pflichtberichte ----------

public sealed record RegisterCompliance(
    Guid RegisterId,
    string Code,
    FiscalCountry Country,
    bool IsActive,
    string? CertificateSerial,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastMonthlyReceipt,
    DateTimeOffset? LastYearlyReceipt,
    IReadOnlyList<string> Duties,
    string? ExportPath);

public sealed record WorkingTimeRecord(Guid EmployeeId, string Name, decimal PlannedHours, int WorkDays, int VacationDays, int SickDays, IReadOnlyList<string> Warnings);

/// <summary>
/// Pflichtberichte eines Standorts: Kassen mit ihren Pflichten je Land (RKSV, KassenSichV/§ 146a AO), Gutscheinverbindlichkeiten zum Stichtag
/// und Arbeitszeitaufzeichnungen des Monats aus dem Dienstplan. Fristen stehen in <c>Reminders</c>.
/// <c>VoucherLiabilities</c> (Summe offener Gutscheine) fehlt ohne das Recht „Berichte mit Beträgen“ (<c>AmountsHidden</c>).
/// <c>OpenPackages</c> und <c>PackageUnitsLeft</c>: noch nicht verbrauchte Pakete (z. B. 10er-Blöcke) und deren offene Behandlungen.
/// </summary>
public sealed record ComplianceReportDto(
    Guid LocationId,
    DateOnly Date,
    IReadOnlyList<RegisterCompliance> Registers,
    decimal? VoucherLiabilities,
    int OpenVouchers,
    IReadOnlyList<WorkingTimeRecord> WorkingTime,
    IReadOnlyList<string> Reminders,
    int OpenPackages = 0,
    int PackageUnitsLeft = 0,
    bool AmountsHidden = false);
