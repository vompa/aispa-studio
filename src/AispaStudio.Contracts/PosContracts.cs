namespace AispaStudio.Contracts;

/// <summary>Land, dessen Kassenrecht für eine Kasse gilt: AT Registrierkassensicherheitsverordnung, DE KassenSichV (TSE), CH ohne Signaturpflicht.</summary>
public enum FiscalCountry
{
    AT,
    DE,
    CH,
}

/// <summary>
/// Steuersatz-Gruppe. Die Prozentsätze hängen vom Land der Kasse ab:
/// AT Normal 20, Reduced1 10, Reduced2 13, Special 19 (Jungholz/Mittelberg), Zero 0; DE Normal 19, Reduced1 7, Zero 0;
/// CH Normal 8,1, Reduced1 2,6, Reduced2 3,8, Zero 0.
/// </summary>
public enum TaxCategory
{
    Normal,
    Reduced1,
    Reduced2,
    Special,
    Zero,
}

/// <summary>
/// Zahlungsart. Neue Arten kommen immer ans Ende, gespeichert wird die Zahl.
/// <c>DigitalEuro</c> ist vorbereitet und erst wählbar, wenn er ausgegeben wird und in der Konfiguration eingeschaltet ist (siehe <c>CashRegisterDto.PaymentMethods</c>).
/// </summary>
public enum PaymentMethod
{
    Cash,
    Card,
    Voucher,
    InstantTransfer,
    DigitalEuro,

    /// <summary>Vom Gast vorab über Treatwell bezahlt; Treatwell rechnet mit dem Spa ab (ADR 0062). Nur für Termine mit Vorauszahlung.</summary>
    Treatwell,
}

/// <summary>
/// Art eines Belegs. Neben Verkauf und Storno erzeugt die Kasse die Sonderbelege der RKSV selbst:
/// Startbeleg beim Anlegen, Monats- und Jahresbeleg beim ersten Beleg eines neuen Monats, Nullbeleg nach einem Ausfall der Signatureinrichtung,
/// Schlussbeleg beim Außerbetriebnehmen.
/// </summary>
public enum ReceiptKind
{
    Sale,
    Void,
    Start,
    Monthly,
    Yearly,
    Null,
    Closing,
}

/// <summary>
/// Mehrzweckgutschein (Wertgutschein): beim Verkauf nicht steuerbar, versteuert wird bei der Einlösung.
/// Einzweckgutschein (eine bestimmte Behandlung): beim Verkauf mit dem Steuersatz der Behandlung versteuert, bei der Einlösung nicht noch einmal.
/// </summary>
public enum VoucherKind
{
    MultiPurpose,
    SinglePurpose,
}

public enum VoucherStatus
{
    Active,
    Redeemed,
    Voided,
}

public enum BillStatus
{
    Open,
    Settled,
}

/// <summary>
/// <c>Deposit</c>: Anzahlung auf einen Termin (<c>AppointmentId</c>, <c>UnitPrice</c> als Betrag). Sie wird beim Kassieren mit dem
/// Steuersatz der Behandlung versteuert (Anzahlungsbeleg) und beim Beleg der Behandlung automatisch verrechnet.
/// </summary>
public enum BillLineKind
{
    Appointment,
    Article,
    Voucher,
    Custom,

    /// <summary>Eintritt in einen Bereich (Sauna, Ruheraum): abgerechnet wird der Artikel, der am Bereich hinterlegt ist, je Person einmal.</summary>
    AreaVisit,
    Deposit,
}

public enum CashMovementKind
{
    Deposit,
    Withdrawal,
}

/// <summary>Erlöskonto, optional mit Kostenstelle. Getrennte Geschäftsbereiche bekommen eigene Konten; jeder Umsatz wird voll versteuert und signiert.</summary>
public sealed record RevenueAccountInput(string Code, string Name, string? CostCenter);

public sealed record RevenueAccountDto(Guid Id, string Code, string Name, string? CostCenter);

/// <summary>Artikel für den Verkauf an der Kassa, z. B. Massageöl oder Saunaeintritt. Preise sind Bruttopreise.</summary>
public sealed record ArticleInput(string Name, decimal Price, TaxCategory TaxCategory, Guid RevenueAccountId);

public sealed record ArticleDto(Guid Id, string Name, decimal Price, TaxCategory TaxCategory, Guid RevenueAccountId);

/// <summary>Steuersatz und Erlöskonto einer Behandlung an der Kassa. Ohne Einstellung gilt der Normalsatz und das Konto „Behandlungen“.</summary>
public sealed record TreatmentPosSettings(TaxCategory TaxCategory, Guid RevenueAccountId);

/// <summary>
/// Neue Kasse an einem Standort. <c>Code</c> ist die Kassen-ID, die auf jedem Beleg steht und (AT) in FinanzOnline registriert wird.
/// <c>ReceiptHeader</c>: Firmenname, Anschrift und UID für den Belegkopf. IBAN und BIC braucht die Sofortüberweisung per QR-Code.
/// </summary>
public sealed record CashRegisterInput(
    Guid LocationId,
    string Code,
    FiscalCountry Country,
    string ReceiptHeader,
    string? PayeeName,
    string? Iban,
    string? Bic);

/// <summary>
/// Kasse mit Zustand. <c>Warnings</c> nennt, was die Rezeption oder Leitung tun muss, z. B. einen Ausfall an FinanzOnline melden.
/// <c>PaymentMethods</c>: an dieser Kasse wählbare Zahlungsarten (hängt vom Land und von der Konfiguration ab).
/// <c>OtherSite</c>: die Kasse gehört zu einer anderen Stelle (Einzelplatz oder Server) und kassiert nur dort (ADR 0041); hier nur lesen.
/// </summary>
public sealed record CashRegisterDto(
    Guid Id,
    Guid LocationId,
    string Code,
    FiscalCountry Country,
    string ReceiptHeader,
    bool IsActive,
    long LastReceiptNumber,
    DateTimeOffset? SignatureDeviceFailedSince,
    string? CertificateSerial,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<PaymentMethod> PaymentMethods,
    bool OtherSite = false);

/// <summary>
/// Position für eine offene Rechnung. Je nach <c>Kind</c>: Termin (<c>AppointmentId</c>), Artikel (<c>ArticleId</c>, <c>Quantity</c>),
/// Gutschein (<c>VoucherKind</c>, <c>UnitPrice</c> als Wert, bei Einzweckgutschein <c>VoucherTreatmentId</c>) oder frei
/// (<c>Description</c>, <c>UnitPrice</c>, <c>TaxCategory</c>, <c>RevenueAccountId</c>). <c>Discount</c> ist ein Betrag, der vom Positionspreis abgeht.
/// Paket (z. B. 10er-Block): Einzweckgutschein mit <c>Units</c> Behandlungen; <c>UnitPrice</c> ist der Paketpreis (ohne Angabe Einheiten × Preis),
/// <c>ValidUntil</c> ein freiwilliges Ablaufdatum. Ein Paket gehört dem Gast der Rechnung.
/// </summary>
public sealed record BillLineInput(
    BillLineKind Kind,
    Guid? AppointmentId,
    Guid? ArticleId,
    int Quantity,
    decimal? UnitPrice,
    decimal Discount,
    string? Description,
    TaxCategory? TaxCategory,
    Guid? RevenueAccountId,
    VoucherKind? VoucherKind,
    Guid? VoucherTreatmentId,
    Guid? AreaVisitId = null,
    int? Units = null,
    DateOnly? ValidUntil = null);

/// <summary>
/// Rabatt einer offenen Position: in Prozent (<c>Percent</c>, gerechnet auf Menge × Preis) oder als Betrag in der Währung der Kasse.
/// Über der Rabattgrenze des Spas braucht es die Kassenaufsicht.
/// </summary>
/// <param name="Approval">Freigabe über der Rabattgrenze durch eine Person mit Kassenaufsicht am selben Platz (Name und PIN).</param>
public sealed record LineDiscountInput(decimal Discount, decimal? Percent = null, SupervisorApproval? Approval = null);

/// <summary>Freigabe durch Inhaber oder Standortleitung mit ihrer PIN, ohne dass sie sich anmelden müssen.</summary>
public sealed record SupervisorApproval(string UserId, string Pin);

/// <summary>Kassa-Einstellungen des Spas.</summary>
/// <param name="DiscountLimitPercent">Bis zu diesem Rabatt in Prozent darf jede Person an der Kassa und in der Kundenkartei Nachlass geben; darüber nur die Kassenaufsicht (Inhaber, Standortleitung).</param>
/// <param name="MayExceedLimit">Ob die angemeldete Person die Grenze überschreiten darf.</param>
/// <param name="CashDiscountPercent">Barzahlungsrabatt in Prozent, 0 = aus. Gilt, wenn ein Beleg nur bar bezahlt wird; nicht auf verkaufte Gutscheine.</param>
public sealed record PosSettingsDto(decimal DiscountLimitPercent, bool MayExceedLimit, decimal CashDiscountPercent = 0);

/// <param name="CashDiscountPercent">Ohne Angabe bleibt der bisherige Wert.</param>
public sealed record PosSettingsInput(decimal DiscountLimitPercent, decimal? CashDiscountPercent = null);

/// <summary>Neue offene Rechnung an einer Kasse, optional gleich mit Terminen aus dem Kalender.</summary>
public sealed record BillInput(Guid RegisterId, Guid? CustomerId, IReadOnlyList<Guid> AppointmentIds, IReadOnlyList<Guid>? AreaVisitIds = null);

/// <summary>Position einer Rechnung. <c>DepositCredit</c>: bezahlte Anzahlung auf diesen noch offenen Termin, die beim Kassieren abgezogen wird.</summary>
public sealed record BillLineDto(
    Guid Id,
    BillLineKind Kind,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Amount,
    decimal PaidAmount,
    TaxCategory TaxCategory,
    decimal TaxPercent,
    Guid RevenueAccountId,
    Guid? AppointmentId,
    Guid? AreaVisitId = null,
    string? DiscountNote = null,
    Guid? TreatmentId = null,
    decimal DepositCredit = 0);

/// <summary>
/// Offene oder bezahlte Rechnung. Teilzahlungen (Rechnungssplitting) erzeugen je einen eigenen Beleg. <c>DepositCredit</c>: schon
/// bezahlte Anzahlungen auf die Termine dieser Rechnung; sie werden beim Kassieren abgezogen, <c>Open</c> ist bereits darum verringert.
/// </summary>
/// <param name="CashDiscount">Um so viel ist das noch Offene günstiger, wenn es ganz bar bezahlt wird (Barzahlungsrabatt des Spas).</param>
/// <param name="PlatformPrepaid">Betrag der noch offenen Termine, den der Gast vorab über Treatwell bezahlt hat; als Zahlungsart „Über Treatwell bezahlt“ vorschlagen.</param>
/// <param name="PlatformReference">Treatwell-Buchungsnummer(n) dazu, für die Referenz der Zahlung.</param>
/// <param name="CustomerName">Name im Haus (Rufname statt Vorname); groß in der Kassa.</param>
/// <param name="CustomerLegalName">Gültiger Name für Belege mit Empfänger; klein darunter, wenn er vom Namen im Haus abweicht.</param>
public sealed record BillDto(
    Guid Id,
    Guid RegisterId,
    Guid? CustomerId,
    string? CustomerName,
    BillStatus Status,
    IReadOnlyList<BillLineDto> Lines,
    decimal Total,
    decimal Paid,
    decimal Open,
    IReadOnlyList<Guid> ReceiptIds,
    decimal CashDiscountPercent = 0,
    decimal CashDiscount = 0,
    decimal DepositCredit = 0,
    decimal PlatformPrepaid = 0,
    string? PlatformReference = null,
    string? CustomerLegalName = null);

/// <summary>Welcher Betrag einer Position mit diesem Beleg bezahlt wird (Rechnungssplitting).</summary>
public sealed record LineAllocation(Guid LineId, decimal Amount);

/// <summary>
/// Zahlung. Bar: <c>Amount</c> ist der gegebene Betrag, zu viel Gegebenes kommt als Rückgeld zurück.
/// Gutschein: <c>VoucherCode</c>; bei Einzweckgutscheinen rechnet der Server den Betrag selbst aus der passenden Behandlung.
/// Karte und Sofortüberweisung: <c>Reference</c> vom Terminal bzw. der Überweisung.
/// </summary>
/// <param name="CardPaymentId">Genehmigte Zahlung am angebundenen Kartenterminal; ohne Angabe gilt die Karte als an einem eigenständigen Gerät bezahlt.</param>
public sealed record PaymentInput(PaymentMethod Method, decimal Amount, string? VoucherCode, string? Reference, Guid? CardPaymentId = null);

/// <summary>Bezahlen. Ohne <c>Lines</c> wird alles noch Offene bezahlt, sonst nur die genannten Beträge.</summary>
public sealed record CheckoutRequest(IReadOnlyList<LineAllocation>? Lines, IReadOnlyList<PaymentInput> Payments);

/// <param name="Amount">Bezahlter Betrag der Position nach Rabatt.</param>
/// <param name="Discount">Rabatt auf die Position; der Beleg zeigt den Preis davor und den Rabatt als eigene Zeile.</param>
/// <param name="DiscountNote">Bezeichnung des Rabatts, z. B. „Kundenrabatt 10 %“.</param>
public sealed record ReceiptLineDto(
    string Description,
    int Quantity,
    decimal Amount,
    TaxCategory TaxCategory,
    decimal TaxPercent,
    Guid RevenueAccountId,
    string RevenueAccountCode,
    Guid? AppointmentId,
    decimal Discount = 0,
    string? DiscountNote = null);

public sealed record TaxLineDto(TaxCategory TaxCategory, decimal Percent, decimal Gross, decimal Net, decimal Tax);

public sealed record PaymentDto(PaymentMethod Method, decimal Amount, string? VoucherCode, string? Reference);

public sealed record VoucherDto(
    Guid Id,
    string Code,
    VoucherKind Kind,
    decimal InitialValue,
    decimal Balance,
    Guid? TreatmentId,
    VoucherStatus Status,
    DateOnly? ValidUntil,
    int? Units = null,
    int? UnitsLeft = null,
    Guid? CustomerId = null);

/// <summary>
/// Signierter Beleg. <c>FiscalCode</c> ist der Inhalt des QR-Codes (AT: maschinenlesbarer Code nach RKSV).
/// <c>SignatureDeviceFailed</c>: der Beleg muss den Hinweis „Sicherheitseinrichtung ausgefallen“ tragen.
/// <c>Company</c>: Unternehmensangaben des Standorts (UID, Firmenbuch) für den Druck. <c>Recipient</c>: Rechnungsempfänger, falls ergänzt.
/// <c>SmallInvoiceLimit</c>: bis zu diesem Gesamtbetrag gilt der Beleg als Kleinbetragsrechnung (AT 400 €, DE 250 €, CH 400 CHF);
/// darüber braucht eine Rechnung an ein Unternehmen Name und Anschrift des Empfängers.
/// <c>IsDepositReceipt</c>: der Beleg ist die Einzahlung einer Anzahlung; ihn storniert nur die Leitung (Inhaber und Manager, ADR 0073 Ä51).
/// </summary>
public sealed record ReceiptDto(
    Guid Id,
    Guid RegisterId,
    string RegisterCode,
    long Number,
    ReceiptKind Kind,
    DateTimeOffset IssuedAt,
    string ReceiptHeader,
    Guid? BillId,
    Guid? CustomerId,
    IReadOnlyList<ReceiptLineDto> Lines,
    IReadOnlyList<TaxLineDto> Taxes,
    IReadOnlyList<PaymentDto> Payments,
    decimal Total,
    decimal Change,
    Guid? VoidsReceiptId,
    Guid? VoidedByReceiptId,
    string? VoidReason,
    string? FiscalCode,
    bool SignatureDeviceFailed,
    IReadOnlyList<VoucherDto> IssuedVouchers,
    InvoiceRecipient? Recipient = null,
    LocationCompany? Company = null,
    decimal? SmallInvoiceLimit = null,
    bool IsDepositReceipt = false);

/// <summary>
/// Empfänger einer Rechnung zu einem Beleg, z. B. für Firmenkunden: Name und Anschrift, bei Unternehmen die UID-Nummer.
/// Wird einmal ergänzt und danach nicht mehr geändert; eine Korrektur geht über Storno und neuen Beleg.
/// <c>VatIdCheckId</c>: gültige UID-Abfrage zu genau dieser UID-Nummer als Nachweis (in Österreich über 10.000 € geboten).
/// </summary>
public sealed record InvoiceRecipient(string Name, string Address, string? VatId = null, Guid? VatIdCheckId = null);

public enum VatIdCheckStatus
{
    Pending,
    Valid,
    Invalid,

    /// <summary>VIES oder der Dienst des Mitgliedstaats hat nicht geantwortet; später nochmals prüfen.</summary>
    Failed,
}

/// <summary>UID-Nummer über VIES prüfen; anfragend ist das Unternehmen des Standorts dieser Kasse.</summary>
public sealed record VatIdCheckInput(string VatId, Guid RegisterId);

/// <summary>
/// Ergebnis einer UID-Abfrage. Mit <c>RequesterVatId</c> ist sie qualifiziert: <c>Name</c> und <c>Address</c> laut Register und
/// <c>ConsultationNumber</c> als Nachweis der Abfrage.
/// </summary>
public sealed record VatIdCheckDto(
    Guid Id,
    string VatId,
    string? RequesterVatId,
    VatIdCheckStatus Status,
    string? Name,
    string? Address,
    string? ConsultationNumber,
    string? Message,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CheckedAt);

/// <summary>Druckformat für Bondrucker: Text in fester Breite oder ESC/POS-Befehle.</summary>
public enum ReceiptPrintFormat
{
    Text,
    EscPos,
}

/// <summary>Storno eines Belegs. Der Grund ist Pflicht und steht auf dem Stornobeleg.</summary>
public sealed record VoidRequest(string Reason);

/// <summary>
/// Die offene Anzahlung eines Termins, die sich retournieren lässt (Ä47): noch nicht beim Beleg der Behandlung verrechnet und der Anzahlungsbeleg nicht storniert. <c>Way</c>
/// ist der Zahlungsweg der Einzahlung („Karte“, „Bar“), auf dem das Geld zurückgeht (der Storno-Beleg spiegelt die Zahlung). <c>CanReturn</c>: die angemeldete Person darf
/// retournieren (nur Inhaber und Manager); die Oberfläche blendet den Knopf sonst aus, der Server prüft es bei jedem Aufruf. <c>Currency</c>: Währung des Standorts.
/// </summary>
public sealed record DepositReturnInfo(Guid ReceiptId, string ReceiptText, decimal Amount, string Way, DateTimeOffset PaidAt, bool CanReturn, string Currency = "EUR");

/// <summary>Anzahlung eines Termins retournieren: Storno-Beleg über die Kassa. Der Grund ist Pflicht und steht auf dem Stornobeleg.</summary>
public sealed record DepositReturnRequest(string Reason);

/// <summary>Einlage oder Entnahme in der Kassenlade, z. B. Wechselgeld am Morgen oder Abschöpfung zur Bank.</summary>
public sealed record CashMovementInput(CashMovementKind Kind, decimal Amount, string Reason);

public sealed record CashMovementDto(Guid Id, CashMovementKind Kind, decimal Amount, string Reason, DateTimeOffset At);

public sealed record DayClosingInput(decimal CountedCash, string? Note);

public sealed record PaymentTotal(PaymentMethod Method, decimal Amount);

public sealed record AccountTotal(Guid RevenueAccountId, string Code, string Name, string? CostCenter, decimal Gross, decimal Net);

/// <summary>Tagesabschluss: Umsätze seit dem letzten Abschluss nach Zahlungsart, Steuersatz und Erlöskonto, Soll- und Ist-Bestand der Kassenlade.</summary>
public sealed record DayClosingDto(
    Guid Id,
    Guid RegisterId,
    DateTimeOffset From,
    DateTimeOffset Until,
    int ReceiptCount,
    int VoidCount,
    decimal Revenue,
    IReadOnlyList<PaymentTotal> Payments,
    IReadOnlyList<TaxLineDto> Taxes,
    IReadOnlyList<AccountTotal> Accounts,
    decimal OpeningCash,
    decimal CashDeposits,
    decimal CashWithdrawals,
    decimal ExpectedCash,
    decimal CountedCash,
    decimal Difference,
    string? Note);

/// <summary>Inhalt für einen EPC-QR-Code („GiroCode“): Kunde scannt mit der Banking-App und überweist sofort.</summary>
public sealed record InstantTransferQr(string Payload, decimal Amount, string Iban, string PayeeName);

/// <summary>Datenerfassungsprotokoll (DEP) nach RKSV im Exportformat: alle Belege einer Kasse in JWS-Kompaktdarstellung.</summary>
public sealed record DepExport(
    [property: System.Text.Json.Serialization.JsonPropertyName("Belege-Gruppe")] IReadOnlyList<DepGroup> Groups);

public sealed record DepGroup(
    [property: System.Text.Json.Serialization.JsonPropertyName("Signaturzertifikat")] string Certificate,
    [property: System.Text.Json.Serialization.JsonPropertyName("Zertifizierungsstellen")] IReadOnlyList<string> CertificateChain,
    [property: System.Text.Json.Serialization.JsonPropertyName("Belege-kompakt")] IReadOnlyList<string> Receipts);

/// <summary>Wie die Software mit einem Kartenterminal spricht.</summary>
public enum TerminalProvider
{
    /// <summary>Testterminal ohne Gerät: genehmigt jeden Betrag außer solchen, die auf 13 Cent enden.</summary>
    Test,

    /// <summary>Gerät im Netz am Empfang (ZVT oder OPI); die Windows-App holt die Aufträge ab und meldet das Ergebnis.</summary>
    Local,
}

public enum CardPaymentStatus
{
    Pending,
    Approved,
    Declined,
    Cancelled,
    ReversalPending,
    Reversed,

    /// <summary>Der Beleg wurde storniert: der Betrag wird am Terminal auf die Karte gutgeschrieben.</summary>
    RefundPending,
    Refunded,

    /// <summary>Gutschrift am Terminal abgelehnt oder nicht möglich; am Terminal direkt gutschreiben.</summary>
    RefundFailed,
}

/// <summary>Protokoll eines Geräts im Netz (<see cref="TerminalProvider.Local"/>), wie es der Zahlungsdienstleister am Terminal eingerichtet hat.</summary>
public enum TerminalProtocol
{
    /// <summary>ZVT über TCP, in Deutschland und Österreich verbreitet (übliche Port 20007).</summary>
    Zvt,

    /// <summary>OPI (Open Payment Initiative, XML über TCP), z. B. bei Worldline und in der Schweiz verbreitet (üblicher Port 20002).</summary>
    Opi,
}

/// <param name="Address">Bei <see cref="TerminalProvider.Local"/> IP-Adresse und Port im Netz am Empfang, z. B. „192.168.1.50:20007“.</param>
/// <param name="Protocol">Bei <see cref="TerminalProvider.Local"/>: ZVT (Vorgabe) oder OPI.</param>
public sealed record PaymentTerminalInput(Guid LocationId, string Name, TerminalProvider Provider, string? Address,
    TerminalProtocol Protocol = TerminalProtocol.Zvt);

public sealed record PaymentTerminalDto(Guid Id, Guid LocationId, string Name, TerminalProvider Provider, string? Address, bool IsActive,
    TerminalProtocol Protocol = TerminalProtocol.Zvt);

public sealed record CardPaymentRequest(Guid TerminalId, decimal Amount);

public sealed record CardPaymentDto(
    Guid Id,
    Guid BillId,
    Guid TerminalId,
    decimal Amount,
    string Currency,
    CardPaymentStatus Status,
    string? Message,
    string? CardBrand,
    string? MaskedPan,
    string? AuthorizationCode,
    string? Reference,
    Guid? ReceiptId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Ergebnis vom Terminal, gemeldet von der Windows-App (bei Storno oder Gutschrift: ob sie geklappt hat).</summary>
public sealed record CardTerminalResult(bool Approved, string? Message, string? CardBrand, string? MaskedPan, string? AuthorizationCode, string? Reference);

/// <summary>
/// Zeile der Liste „Heute abzurechnen“: ein Termin (<c>Appointment</c>) oder ein Eintritt (<c>Visit</c>) mit allem, was die Zeile zeigt.
/// <c>What</c> ist die Behandlung bzw. „Eintritt Sauna“, <c>Price</c> beim Eintritt der Artikel des Bereichs mal Personen (fehlt ohne Artikel).
/// <c>FinishedAt</c> nennt bei Wartenden das Ende, seit dem der Gast fertig ist; leer, wenn der Termin vor seinem Ende abgeschlossen wurde.
/// </summary>
public sealed record PosDayEntryDto(
    DateTimeOffset Start,
    string CustomerName,
    string What,
    string? EmployeeName,
    string? RoomName,
    decimal? Price,
    DateTimeOffset? FinishedAt,
    AppointmentDto? Appointment,
    AreaVisitDto? Visit);

/// <summary>
/// Liste „Heute abzurechnen“ an der Kassa, eingeteilt vom Server: <c>Waiting</c> sind fertige Behandlungen und Eintritte, die noch auf keiner
/// Rechnung stehen (wer am längsten wartet, zuerst), <c>Later</c> die übrigen von heute, <c>Done</c> die schon bezahlten. Was auf einer geparkten
/// Rechnung steht, fehlt hier; eine Anzahlung zählt nicht als abgerechnet.
/// </summary>
public sealed record PosDayListDto(IReadOnlyList<PosDayEntryDto> Waiting, IReadOnlyList<PosDayEntryDto> Later, IReadOnlyList<PosDayEntryDto> Done);
