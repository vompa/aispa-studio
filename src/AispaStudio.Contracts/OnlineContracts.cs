namespace AispaStudio.Contracts;

// Kundenbereich im Web: Online-Buchung, Kundenkonto und Anamnesebogen. Diese Typen zeigen nur, was Kundinnen und Kunden
// sehen dürfen: keine internen Ressourcen, keine anderen Kunden, keine Notizen der Rezeption.

/// <summary>
/// Standort, wie er Kundinnen und Kunden angezeigt wird. <c>BookingEnabled</c>: Termine sind hier online buchbar (Gutscheine
/// gibt es unabhängig davon). <c>GuestNote</c>: Hinweis des Spas für Gäste, z. B. „Bitte 10 Minuten früher kommen“.
/// <c>CancellationDeadlineHours</c>: bis so viele Stunden vor Beginn ist Online-Storno möglich.
/// <c>NoShowFeePercent</c>: größer 0, wenn beim Buchen eine Karte hinterlegt wird; so viel Prozent des Preises kostet ein
/// Nichterscheinen oder eine Absage nach der Stornofrist. <c>DepositPercent</c>: größer 0, wenn beim Buchen so viel Prozent des
/// Preises angezahlt werden (100 = Vorauszahlung); dann wird keine Karte hinterlegt. <c>Closures</c>: laufende und kommende Schließzeiten
/// mit dem Text für Gäste (ADR 0073); in ihnen gibt es keine freien Zeiten.
/// </summary>
public sealed record OnlineLocation(
    Guid Id,
    string Name,
    string TimeZone,
    string Currency,
    IReadOnlyList<OpeningHours> OpeningHours,
    bool BookingEnabled,
    string? GuestNote,
    int CancellationDeadlineHours,
    LocationContact Contact,
    int NoShowFeePercent = 0,
    bool NoShowFeeNewCustomersOnly = false,
    int DepositPercent = 0,
    IReadOnlyList<OnlineClosure>? Closures = null);

/// <summary>
/// Online buchbare Behandlung an einem Standort. <c>Category</c> gruppiert das Angebot, z. B. „Massagen“. <c>NoShowFeePercent</c>:
/// Ausfallgebühr für diese Behandlung an diesem Standort (eigene Prozent der Behandlung oder Regel des Standorts; 0 = keine).
/// <c>DepositPercent</c>: Anzahlung beim Buchen in Prozent des Preises (100 = Vorauszahlung); geht der Kartenhinterlegung vor.
/// </summary>
public sealed record OnlineTreatment(Guid Id, string Name, int DurationMinutes, decimal Price, string Currency, string? Category,
    int NoShowFeePercent = 0, int DepositPercent = 0);

/// <summary>
/// Freie Startzeiten an einem Tag (UTC; angezeigt in der Zeitzone des Standorts). <c>Prices</c>: Preis je Zeit in derselben Reihenfolge,
/// nur wenn die Behandlung Preise nach Tageszeit oder Wochentag hat; sonst gilt der Preis der Behandlung.
/// </summary>
public sealed record OnlineDay(DateOnly Date, IReadOnlyList<DateTimeOffset> Times, IReadOnlyList<decimal>? Prices = null);

/// <summary>
/// Freie Zeiten für mehrere Tage. <c>FirstBookableDate</c>/<c>LastBookableDate</c> begrenzen, wie weit vor- und zurückgeblättert
/// werden kann (Vorlaufzeit und Buchungshorizont).
/// </summary>
public sealed record OnlineAvailability(
    Guid LocationId,
    Guid TreatmentId,
    string TimeZone,
    DateOnly FirstBookableDate,
    DateOnly LastBookableDate,
    IReadOnlyList<OnlineDay> Days);

/// <summary>Kontaktdaten für eine Buchung ohne Konto (oder für das erste Buchen mit neuem Konto).</summary>
public sealed record OnlineContact(string FirstName, string LastName, string Email, string? Phone);

/// <summary>
/// Buchungswunsch aus dem Kundenbereich. Angemeldete Kundinnen und Kunden brauchen <c>Contact</c> nur, solange ihr Konto
/// noch keine Kundendaten hat. <c>AcceptPrivacy</c> muss gesetzt sein (Datenschutzhinweis gelesen).
/// <c>Language</c> („de“, „en“, „fr“ oder „it“; andere Sprachen bekommen Englisch) bestimmt die Sprache der Bestätigung.
/// <c>AcceptNoShowFee</c> ist Pflicht, wenn der Standort eine Ausfallgebühr verlangt (<see cref="OnlineLocation.NoShowFeePercent"/>)
/// oder eine Anzahlung (<see cref="OnlineTreatment.DepositPercent"/>): die Zustimmung gilt dann den Bedingungen der Anzahlung.
/// </summary>
public sealed record OnlineBookingRequest(
    Guid LocationId,
    Guid TreatmentId,
    DateTimeOffset Start,
    OnlineContact? Contact,
    string? Note,
    bool AcceptPrivacy,
    string? Language,
    bool AcceptNoShowFee = false,
    string? VoucherCode = null);

public enum OnlineAppointmentState
{
    Booked,
    Cancelled,
    Past,
}

/// <summary>
/// Stand der Kartenhinterlegung: <c>Pending</c> wartet auf die Karte, <c>Saved</c> hinterlegt, <c>Failed</c> abgebrochen oder
/// abgelaufen (neu starten möglich). <c>Charging</c>, <c>Charged</c>, <c>ChargeFailed</c>: das Spa hat die Ausfallgebühr belastet.
/// </summary>
public enum CardGuaranteeStatus
{
    Pending,
    Saved,
    Failed,
    Charging,
    Charged,
    ChargeFailed,

    /// <summary>Belastet und nach einem Storno an der Kassa zurückerstattet.</summary>
    Refunded,

    /// <summary>Von der Rezeption per Link angefordert; der Gast hat der Ausfallgebühr noch nicht zugestimmt.</summary>
    AwaitingConsent,

    /// <summary>Online gebucht, aber in der Frist keine Karte hinterlegt: der Termin wurde freigegeben.</summary>
    Released,
}

/// <summary>
/// Kartenhinterlegung zu einem Online-Termin. <c>FeeAmount</c>: Ausfallgebühr, der beim Buchen zugestimmt wurde.
/// <c>SetupUrl</c>: Seite des Zahlungsanbieters zum Hinterlegen, solange die Karte fehlt. <c>TestMode</c>: Testbetrieb ohne Anbieter.
/// <c>DeadlineHours</c>: Stornofrist, nach der die Gebühr fällig wird (für die Zustimmung bei <c>AwaitingConsent</c>).
/// </summary>
public sealed record OnlineCardGuarantee(
    decimal FeeAmount, string Currency, int FeePercent, CardGuaranteeStatus Status, string? SetupUrl, bool TestMode, int DeadlineHours = 0,
    DateTimeOffset? HoldUntil = null);

/// <summary>
/// Stand der Anzahlung: <c>Pending</c> wartet auf die Zahlung, <c>Paid</c> bezahlt und wird gerade an der Kasse verbucht,
/// <c>Receipted</c> mit Anzahlungsbeleg, <c>Failed</c> abgebrochen oder abgelaufen (neu starten möglich), <c>Refunded</c> nach
/// Storno des Belegs zurückerstattet, <c>Released</c> Termin freigegeben, weil in der Frist nicht bezahlt wurde.
/// </summary>
public enum OnlineDepositStatus
{
    Pending,
    Paid,
    Receipted,
    Failed,
    Cancelled,
    Refunded,
    Released,
}

/// <summary>
/// Anzahlung zu einem Online-Termin. <c>PaymentUrl</c>: Seite des Zahlungsanbieters, solange nicht bezahlt ist. <c>TestMode</c>:
/// Testbetrieb ohne Anbieter. <c>DeadlineHours</c>: Stornofrist; bei Nichterscheinen oder einer Absage danach bleibt die Anzahlung
/// beim Spa. <c>PayUntil</c>: bis dahin bezahlen, sonst wird der Termin freigegeben. <c>Receipt</c>: Anzahlungsbeleg, sobald verbucht.
/// </summary>
public sealed record OnlineDeposit(
    decimal Amount, string Currency, int Percent, OnlineDepositStatus Status, string? PaymentUrl, bool TestMode, int DeadlineHours,
    DateTimeOffset? PayUntil, string? Receipt);

/// <summary>
/// Was die Rezeption per Link verlangen kann: Ausfallgebühr laut Regel des Standorts, oder warum es nicht geht. <c>CancellationDeadlineAt</c>:
/// bis wann der Gast ohne Gebühr absagt (Beginn minus Stornofrist des Standorts), auch wenn keine Karte verlangt werden kann; ohne Frist leer.
/// </summary>
public sealed record CardRequestOffer(bool Possible, decimal FeeAmount, string Currency, int FeePercent, string? Reason, DateTimeOffset? CancellationDeadlineAt = null);

/// <summary>Nur im Testbetrieb: Hinterlegen der Karte simulieren (<c>Saved</c>) oder abbrechen.</summary>
public sealed record TestCardResult(bool Saved);

/// <summary>
/// Termin aus Sicht der Kundin oder des Kunden. <c>CancellableUntil</c>: bis dahin ist Online-Storno kostenlos möglich, danach
/// telefonisch oder bei <c>CanCancelLate</c> online gegen die Gebühr <c>LateCancelFee</c>. <c>CardGuarantee</c>: hinterlegte Karte,
/// wenn der Standort eine Ausfallgebühr verlangt.
/// </summary>
public sealed record OnlineAppointment(
    Guid Id,
    Guid LocationId,
    string LocationName,
    string TimeZone,
    string TreatmentName,
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal Price,
    string Currency,
    OnlineAppointmentState State,
    DateTimeOffset CancellableUntil,
    bool CanCancel,
    bool AnamnesisSubmitted,
    LocationContact LocationContact,
    OnlineCardGuarantee? CardGuarantee = null,
    OnlineDeposit? Deposit = null,
    DateTimeOffset? ConfirmedAt = null,
    string? GuestHint = null,
    Guid? TreatmentId = null,
    bool CanMove = false,
    decimal? LateCancelFee = null,
    bool CanCancelLate = false);

/// <summary>
/// Eigener Gutschein in der Kunden-App. <c>Balance</c>: Restwert; bei einem Paket zählen <c>Units</c> und <c>UnitsLeft</c>.
/// <c>TreatmentName</c>: nur bei einem Einzweckgutschein.
/// </summary>
public sealed record OnlineOwnedVoucher(
    string Code,
    VoucherKind Kind,
    decimal InitialValue,
    decimal Balance,
    string Currency,
    VoucherStatus Status,
    DateOnly? ValidUntil,
    string? TreatmentName,
    int? Units,
    int? UnitsLeft);

/// <summary>Neuer Beginn für einen Termin, den der Gast selbst verschiebt (eine der freien Zeiten aus der Online-Verfügbarkeit).</summary>
public sealed record OnlineMoveRequest(DateTimeOffset Start);

/// <summary>
/// Bestätigung einer Online-Buchung. Die beiden Codes sind nur hier und in der Bestätigungsmail im Klartext sichtbar;
/// sie öffnen den Termin bzw. den Anamnesebogen ohne Anmeldung.
/// </summary>
public sealed record OnlineBookingConfirmation(OnlineAppointment Appointment, string ManageToken, string AnamnesisToken);

/// <summary>Angemeldete Kundin oder angemeldeter Kunde. <c>HasProfile</c>: Name ist hinterlegt, Buchen geht ohne weitere Angaben.</summary>
public sealed record CustomerSession(
    string Email,
    string? FirstName,
    string? LastName,
    string? Phone,
    bool HasProfile,
    int PasskeyCount);

public sealed record CustomerProfileInput(string FirstName, string LastName, string? Phone);

public sealed record SignInCodeRequest(string Email, string? Language);

public sealed record SignInCodeVerification(string Email, string Code);

/// <summary>Anmeldewege außer Code und Passkey, die dieser Betrieb eingerichtet hat, z. B. „Google“, „Apple“.</summary>
public sealed record CustomerSignInProviders(IReadOnlyList<string> External);

public enum AnamnesisQuestionKind
{
    /// <summary>Ja/Nein; bei „Ja“ wird um Details gebeten.</summary>
    YesNo,

    /// <summary>Freitext.</summary>
    Text,
}

public sealed record AnamnesisQuestion(string Key, AnamnesisQuestionKind Kind, string Label, string? DetailsLabel, bool Required);

/// <summary>Anamnesebogen in einer Sprache. <c>Version</c> wird mit jeder Antwort gespeichert, damit alte Bögen lesbar bleiben.</summary>
public sealed record AnamnesisFormDefinition(string Version, string Language, string Intro, string ConsentText, IReadOnlyList<AnamnesisQuestion> Questions);

public sealed record AnamnesisAnswer(string Key, bool? Yes, string? Details);

/// <summary>
/// Ausgefüllter Anamnesebogen. <c>ConsentHealthData</c>: ausdrückliche Einwilligung in die Verarbeitung von Gesundheitsdaten
/// (Art. 9 Abs. 2 lit. a DSGVO). <c>Signature</c>: Vor- und Nachname als Bestätigung.
/// </summary>
public sealed record AnamnesisSubmission(IReadOnlyList<AnamnesisAnswer> Answers, bool ConsentHealthData, string Signature, string? Language);

/// <summary>
/// Was ein Anamnese-Link öffnet: für wen und für welchen Termin, und ob schon ein gültiger Bogen vorliegt.
/// <c>Channel</c>: „Link“ (Mail, Papier-QR) oder „Tablet“ (am Empfang; nach dem Abgeben zurückgeben).
/// </summary>
public sealed record AnamnesisLinkInfo(
    string FirstName, string? TreatmentName, DateTimeOffset? Start, string? TimeZone, DateTimeOffset? LastSubmittedAt, string Channel);

/// <summary>Anamnesebogen für das Team (entschlüsselt). <c>Channel</c>: „Link“, „Tablet“ oder „Papier“.</summary>
public sealed record AnamnesisRecordDto(
    Guid Id,
    Guid CustomerId,
    Guid? AppointmentId,
    string FormVersion,
    string Channel,
    DateTimeOffset SubmittedAt,
    string Signature,
    IReadOnlyList<AnamnesisAnswer> Answers);

/// <summary>
/// Regeln der Online-Buchung je Standort, gepflegt in den Stammdaten. Ohne eigene Einstellung gelten die Vorgaben aus der
/// Konfiguration (<c>IsDefault</c>). <c>MinLeadMinutes</c>: Vorlauf bis zum spätesten Online-Termin; <c>MaxDaysAhead</c>:
/// Buchungshorizont; <c>CancellationDeadlineHours</c>: Stornofrist; <c>SlotStepMinutes</c>: Raster der angebotenen Startzeiten.
/// <c>NoShowFeePercent</c>: Ausfallgebühr in Prozent des Preises (0 = keine); dann wird beim Online-Buchen eine Karte hinterlegt.
/// Eine Behandlung kann eigene Prozent haben. <c>NoShowFeeNewCustomersOnly</c>: nur von Gästen ohne abgeschlossene Behandlung im
/// Spa. Wirksam nur mit eingerichtetem Zahlungsanbieter. <c>CardHoldHours</c>: ein online gebuchter Termin wird freigegeben, wenn so
/// viele Stunden nach dem Buchen noch keine Karte hinterlegt oder keine Anzahlung bezahlt ist (0 = nie). <c>DepositPercent</c>:
/// Anzahlung beim Online-Buchen in Prozent des Preises (0 = keine, 100 = Vorauszahlung); sie ersetzt die Kartenhinterlegung und
/// braucht eine aktive Kasse am Standort für den Anzahlungsbeleg. Eine Behandlung kann eigene Prozent haben.
/// </summary>
public sealed record LocationOnlineRules(
    bool BookingEnabled,
    int MinLeadMinutes,
    int MaxDaysAhead,
    int CancellationDeadlineHours,
    int SlotStepMinutes,
    string? GuestNote,
    bool IsDefault,
    int NoShowFeePercent = 0,
    int CardHoldHours = 0,
    bool NoShowFeeNewCustomersOnly = false,
    int DepositPercent = 0,
    string? CancellationPolicy = null);

/// <summary>
/// Kartenhinterlegung eines Termins für das Team. <c>CanCharge</c>: die Ausfallgebühr darf jetzt belastet werden (Karte
/// hinterlegt und Termin „nicht erschienen“ oder nach der Stornofrist storniert); sonst sagt <c>Hint</c>, warum nicht.
/// <c>ReceiptId</c>: Beleg der Kasse über die belastete Gebühr. <c>CardRemovedAt</c>: Karte nach Ablauf der Frist beim
/// Anbieter gelöscht. <c>ReleaseAt</c>: ohne Karte wird der Termin dann freigegeben. <c>CancellationDeadlineAt</c>: bis wann der Gast ohne
/// Gebühr absagt (Beginn minus der Frist, der er zugestimmt hat).
/// </summary>
public sealed record CardGuaranteeStaffDto(
    Guid Id,
    CardGuaranteeStatus Status,
    decimal FeeAmount,
    string Currency,
    int FeePercent,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? SavedAt,
    bool CanCharge,
    string? Hint,
    DateTimeOffset? ChargedAt,
    Guid? ReceiptId,
    string? Error,
    DateTimeOffset? CardRemovedAt = null,
    DateTimeOffset? ReleaseAt = null,
    DateTimeOffset? CancellationDeadlineAt = null);

/// <summary>
/// Anzahlung eines Termins für das Team. <c>ReceiptId</c>: Anzahlungsbeleg der Kasse. <c>Settled</c>: beim Kassieren der
/// Behandlung verrechnet (<c>SettledReceiptId</c>). <c>Kept</c>: der Gast ist nicht erschienen oder hat nach der Stornofrist
/// abgesagt, die Anzahlung bleibt beim Spa. <c>Voided</c>: der Anzahlungsbeleg wurde storniert. <c>Hint</c> erklärt den Stand.
/// </summary>
public sealed record DepositStaffDto(
    Guid Id,
    OnlineDepositStatus Status,
    decimal Amount,
    string Currency,
    int Percent,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? PaidAt,
    Guid? ReceiptId,
    bool Settled,
    Guid? SettledReceiptId,
    bool Kept,
    bool Voided,
    string? Hint,
    string? Error,
    DateTimeOffset? ReleaseAt);

/// <summary>
/// Impressum der Online-Buchung (§ 5 ECG, § 25 MedienG, § 5 DDG), aus den Firmendaten des Spas. Fehlt dort die Anschrift oder der
/// Kontakt, stehen die Angaben des ersten Standorts. <c>Complete</c> ist falsch, solange Firmenwortlaut, Anschrift oder E-Mail fehlen.
/// </summary>
public sealed record OnlineImprint(
    string SpaName,
    string Country,
    string? LegalName,
    string? Street,
    string? PostalCode,
    string? City,
    string? Phone,
    string? Email,
    string? Website,
    string? VatId,
    string? CompanyRegisterNumber,
    string? CompanyRegisterCourt,
    string? BusinessPurpose,
    string? SupervisoryAuthority,
    string? ChamberAndRules,
    bool Complete);
