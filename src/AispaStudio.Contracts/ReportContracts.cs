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
public sealed record TodoItem(Guid? StepId, string Text, DateTimeOffset? DueAt, string Source, bool Done)
{
    /// <summary>Wahr beim Schritt „Gerät bereitstellen“: Der Haken läuft über <c>equipment-prepared</c> des Termins statt über einen Rezeptschritt.</summary>
    public bool? IsEquipment { get; init; }
}

public sealed record PastTreatment(DateTimeOffset Start, string TreatmentName, string EmployeeName, string? Note);

/// <summary>
/// Behandlungskarte: alles, was der Therapeut für einen Termin wissen und vorbereiten muss.
/// <c>VisitNumber</c> zählt bisherige Termine des Gastes (ohne Absagen); 1 heißt Erstbesuch.
/// <c>Anamnesis</c>: Stand des Gesundheitsfragebogens, nie dessen Inhalt (dafür der eigene, protokollierte Abruf).
/// <c>HandsOnMinutes</c> und <c>ProcessingMinutes</c>: Einwirkzeit (Schritt 13). Nach so vielen Minuten am Gast ist die Person für die Einwirkzeit frei; 0 heißt keine.
/// <c>CheckAnamnesis</c>: Die Behandlung verlangt „Anamnese prüfen“, und der letzte Bogen des Gastes enthält Angaben; nur ja oder nein.
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
    bool AllergyFlag = false,
    int HandsOnMinutes = 0,
    int ProcessingMinutes = 0,
    bool CheckAnamnesis = false,
    bool HasHealthHints = false)
{
    /// <summary>Name der Behandlung ohne die Dauer am Ende („Lava-Hot-Stone-Massage (60 Min.)“ wird „Lava-Hot-Stone-Massage“); die Dauer steht in <c>DurationMinutes</c>.</summary>
    public string? DisplayName { get; init; }
}

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

/// <param name="Previous">Wert im Vergleichszeitraum (<c>CompareFrom</c> bis <c>CompareUntil</c> des Berichts); leer bei Werten ohne Vergleich.</param>
/// <param name="HigherIsBetter">Ob ein höherer Wert gut ist (Umsatz) oder schlecht (No-Show-Quote); leer bei neutralen Werten wie „Offene Gutscheine“.</param>
/// <param name="Group">Überschrift der Gruppe, unter der die Kennzahl steht („Umsatz und Ertrag“, „Auslastung“, „Gäste“, „Ausfälle“); leer bei älteren Aufrufen.</param>
/// <param name="Note">Zusatz zur Kachel; „ganzer Betrieb“ bei „Offene Gutscheine“, wenn der Betrieb mehr als einen Standort hat (Gutscheine gelten betriebsweit); sonst leer.</param>
public sealed record Kpi(string Key, string Label, decimal Value, decimal? Previous, string Unit, bool? HigherIsBetter = null, string? Group = null, string? Note = null);

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
/// Wirkung einer Aktion im Zeitraum, aus den Kassenbelegen: kassierte Positionen mit dieser Aktion (Stornos abgezogen), Gäste darauf, davon zum
/// ersten Mal im Spa, gewährter Rabatt und Umsatz brutto nach Rabatt. Rabatt und Umsatz fehlen ohne das Recht „Berichte mit Beträgen“.
/// </summary>
public sealed record PromotionKpi(Guid PromotionId, string Name, int Positions, int Guests, int NewGuests, decimal? Discount, decimal? Revenue);

/// <summary>
/// Management-Cockpit für einen Zeitraum (Ortszeit, beide Tage eingeschlossen) mit Vergleich zum gleich langen Zeitraum davor (Standard), zum selben Abschnitt
/// des Vormonats oder zum selben Zeitraum des Vorjahres (<c>compare</c>); <c>CompareFrom</c> und <c>CompareUntil</c> nennen den Vergleichszeitraum.
/// Umsätze aus den signierten Belegen der Kassen des Standorts, Termine und Auslastung aus dem Kalender.
/// <c>AmountsHidden</c>: ohne das Recht „Berichte mit Beträgen“ fehlen alle Geldbeträge (Umsatz-Kennzahlen, Umsatz nach Konto und Zahlart,
/// Umsatz je Behandlung und Person, Margen); Auslastung, Anzahlen und Quoten bleiben.
/// <c>AiSummary</c>: Kommentar der KI aus genau diesen Angaben (nur mit KI und ab sieben Tagen); solange er geschrieben wird, ist <c>AiSummaryPending</c>
/// gesetzt. Die Zahlen kommen nie von der KI. <c>AiNotice</c> ist der Hinweis, der neben dem KI-Text stehen muss.
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
    int Cancellations = 0,
    string? AiSummary = null,
    bool AiSummaryPending = false,
    string? AiNotice = null,
    IReadOnlyList<PromotionKpi>? Promotions = null,
    DateOnly? CompareFrom = null,
    DateOnly? CompareUntil = null,
    IReadOnlyList<TrendPoint>? Trend = null)
{
    /// <summary>Auslastung Personal je Wochentag und Tageszeit im Zeitraum; nur Wochentage mit Diensten.</summary>
    public IReadOnlyList<WeekdayLoad>? LoadByWeekday { get; init; }

    /// <summary>Felder in <see cref="LoadByWeekday"/> unter diesem Wert (Prozent) sind schwach: Lücken für Aktionen.</summary>
    public decimal? WeakLoadBelow { get; init; }

    /// <summary>Auslastung je Raum im Zeitraum (ohne Bereiche wie Sauna).</summary>
    public IReadOnlyList<RoomKpi>? Rooms { get; init; }

    /// <summary>Stattfindende Termine des Zeitraums nach der Herkunft, die der Termin speichert (Rezeption, online, Mail …).</summary>
    public IReadOnlyList<ChannelKpi>? Channels { get; init; }

    /// <summary>Beste Gäste des Zeitraums (höchstens zehn); nur mit dem Recht, Kunden zu sehen, sonst leer. <c>Spent</c> nur mit „Berichte mit Beträgen“.</summary>
    public IReadOnlyList<GuestKpi>? TopGuests { get; init; }

    /// <summary>Abwandernde Stammgäste, Stand heute (Anzahl als Kennzahl <c>lapsingRegulars</c>); nur mit dem Recht, Kunden zu sehen, sonst leer.</summary>
    public IReadOnlyList<LapsingGuest>? LapsingGuests { get; init; }

    /// <summary>Gebuchter Stand der nächsten vier Wochen ab <see cref="ForecastAsOf"/>, je sieben Tage; rein gerechnet, unabhängig vom Zeitraum.</summary>
    public IReadOnlyList<ForecastWeek>? Forecast { get; init; }

    /// <summary>Tag, an dem die Vorschau gerechnet wurde („Stand heute“).</summary>
    public DateOnly? ForecastAsOf { get; init; }

    /// <summary>
    /// Ob „Mit KI auswerten“ (<c>POST …/management/analysis</c>) möglich ist; ohne Claude-Schlüssel <c>false</c>, dann ist der Knopf gesperrt
    /// (<see cref="ManagementAnalysisDto.UnavailableHint"/>).
    /// </summary>
    public bool AiAnalysisAvailable { get; init; }
}

/// <summary>Schwerpunkt der KI-Auswertung des Management-Berichts.</summary>
public enum ManagementAnalysisFocus
{
    /// <summary>Gesamt.</summary>
    Overall,

    /// <summary>Umsatz und Ertrag.</summary>
    RevenueAndProfit,

    /// <summary>Auslastung und Dienstplan.</summary>
    UtilizationAndRoster,

    /// <summary>Gäste und Bindung.</summary>
    GuestsAndLoyalty,

    /// <summary>Angebot und Preise.</summary>
    OfferAndPrices,
}

public enum ManagementAnalysisStatus
{
    /// <summary>Wird geschrieben; die App fragt nach.</summary>
    Pending,

    /// <summary>Fertig, <c>Result</c> ist gesetzt.</summary>
    Done,

    /// <summary>Gescheitert (KI nicht erreichbar oder Antwort mit Zahlen, die nicht im Bericht stehen); nichts anzeigen, „Erneut versuchen“ legt eine neue an.</summary>
    Failed,
}

/// <summary>
/// „Mit KI auswerten“: Zeitraum und Vergleich wie beim Bericht (<c>Compare</c>: previous, month, year), Schwerpunkt und optional eine eigene Frage
/// (höchstens 300 Zeichen; sie ist Text für die KI, keine Anweisung).
/// </summary>
public sealed record ManagementAnalysisRequest(
    DateOnly From,
    DateOnly Until,
    ManagementAnalysisFocus Focus = ManagementAnalysisFocus.Overall,
    string? Question = null,
    string Compare = "previous");

/// <summary>
/// Eine KI-Auswertung des Management-Berichts. Gilt für genau die Zahlen, aus denen sie geschrieben wurde; wer dieselben Zahlen mit demselben Schwerpunkt
/// und derselben Frage auswerten lässt, bekommt diese zurück. <c>AmountsHidden</c>: ohne das Recht „Berichte mit Beträgen“ geschrieben, ohne Beträge.
/// <c>Notice</c> ist der Hinweis, der neben jeder Auswertung stehen muss.
/// </summary>
public sealed record ManagementAnalysisDto(
    Guid Id,
    Guid LocationId,
    DateOnly From,
    DateOnly Until,
    string Compare,
    ManagementAnalysisFocus Focus,
    string? Question,
    bool AmountsHidden,
    ManagementAnalysisStatus Status,
    ManagementAnalysisResult? Result,
    string Notice,
    DateTimeOffset RequestedAt)
{
    /// <summary>Text für den gesperrten Knopf und die Antwort, wenn keine KI eingerichtet ist.</summary>
    public const string UnavailableHint = "Die KI-Auswertung ist nicht verfügbar: Es ist kein Claude-Schlüssel eingetragen.";
}

/// <summary>
/// Ergebnis der KI-Auswertung: drei bis fünf Kernaussagen, Auffälligkeiten (Beobachtung, mögliche Ursache als Vermutung, betroffene Kennzahl),
/// höchstens fünf Maßnahmen nach Wirkung gereiht, je mit „Woran Sie den Erfolg sehen“, und was die Zahlen nicht zeigen.
/// </summary>
public sealed record ManagementAnalysisResult(
    IReadOnlyList<string> KeyFindings,
    IReadOnlyList<AnalysisFinding> Findings,
    IReadOnlyList<AnalysisMeasure> Measures,
    IReadOnlyList<string> NotShown);

/// <param name="PossibleCause">Vermutung der KI, keine Feststellung.</param>
/// <param name="Kpi">Name der betroffenen Kennzahl wie im Bericht.</param>
public sealed record AnalysisFinding(string Observation, string PossibleCause, string Kpi);

/// <param name="SuccessSign">Woran Sie den Erfolg sehen.</param>
public sealed record AnalysisMeasure(string Action, string SuccessSign);

/// <summary>
/// Auslastung Personal an einem Wochentag je Tageszeit, in Prozent: Behandlungsminuten ÷ Dienstminuten in diesem Abschnitt. Vormittag bis 11 Uhr, Mittag 11 bis 14 Uhr,
/// Nachmittag 14 bis 17 Uhr, Abend ab 17 Uhr (Ortszeit). Leer, wenn in diesem Abschnitt niemand Dienst hatte.
/// </summary>
public sealed record WeekdayLoad(DayOfWeek Day, decimal? Morning, decimal? Midday, decimal? Afternoon, decimal? Evening);

/// <summary>Stattfindende Termine (ohne stornierte und nicht erschienene) mit dieser Herkunft im Zeitraum.</summary>
public sealed record ChannelKpi(AppointmentSource Source, string Label, int Appointments);

/// <summary>
/// Abwandernder Stammgast: mindestens drei Besuche am Standort, seit über 90 Tagen keiner (an keinem Standort) und kein Termin in der Zukunft.
/// <c>LastVisit</c> ist der Tag des letzten Besuchs, <c>Visits</c> die Besuche am Standort.
/// </summary>
public sealed record LapsingGuest(Guid CustomerId, string Name, DateOnly LastVisit, int Visits);

/// <summary>
/// Eine Woche der Vorschau (sieben Tage ab <c>From</c>): gebuchte Termine, ihr Terminpreis brutto (<c>BookedRevenue</c>, leer ohne „Berichte mit Beträgen“) und die gebuchte
/// Auslastung Personal in Prozent (Behandlungsminuten ÷ geplante Dienstminuten).
/// </summary>
public sealed record ForecastWeek(DateOnly From, DateOnly Until, int Appointments, decimal? BookedRevenue, decimal StaffUtilization);

/// <summary>
/// Ein Punkt im Verlauf des Management-Berichts: ein Tag, bei Zeiträumen über 62 Tagen eine Woche von Montag bis Sonntag (am Rand gekürzt).
/// <c>RevenueNet</c> ist der Umsatz ohne Gutscheinverkauf netto, leer ohne das Recht „Berichte mit Beträgen“; <c>StaffUtilization</c> in Prozent.
/// </summary>
public sealed record TrendPoint(DateOnly From, DateOnly Until, decimal? RevenueNet, int Appointments, decimal StaffUtilization);

/// <summary>Wochen- und Monatsbericht per Mail für die angemeldete Person: <c>Enabled</c>, ob sie ihn bekommt; <c>Available</c>, ob ihre Rolle ihn überhaupt bekommt und, im Einzelplatz, ein Mailversand eingerichtet ist.</summary>
public sealed record ReportMailSubscription(bool Enabled, bool Available);

/// <summary>Ein- oder abbestellen.</summary>
public sealed record ReportMailSubscriptionInput(bool Enabled);

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
