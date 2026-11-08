using AispaStudio.Contracts;

namespace AispaStudio.Api.Pos;

// Fachmodell der Kassa. Belege sind unveränderlich: Korrekturen nur per Stornobeleg mit Begründung.
// Es gibt bewusst keinen Weg, Umsätze ohne Beleg oder Signatur zu erfassen.

public sealed class RevenueAccount
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? CostCenter { get; set; }
}

public sealed class Article
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public TaxCategory TaxCategory { get; set; }
    public Guid RevenueAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Kassa-Einstellung einer Behandlung; liegt neben dem Kalendermodell, damit die Behandlung selbst unverändert bleibt.</summary>
public sealed class TreatmentPosSetting
{
    public Guid TreatmentId { get; init; }
    public TaxCategory TaxCategory { get; set; }
    public Guid RevenueAccountId { get; set; }
}

public sealed class CashRegister
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid LocationId { get; init; }

    /// <summary>Kassen-ID auf jedem Beleg, z. B. „WIEN-1“.</summary>
    public required string Code { get; init; }

    public FiscalCountry Country { get; init; }
    public required string ReceiptHeader { get; set; }
    public string? PayeeName { get; set; }
    public string? Iban { get; set; }
    public string? Bic { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; }

    public long LastReceiptNumber { get; set; }
    public DateTimeOffset? LastReceiptAt { get; set; }

    /// <summary>Summe aller Belege in Cent (AT: Umsatzzähler, verschlüsselt auf jedem Beleg).</summary>
    public long TurnoverCounter { get; set; }

    /// <summary>Verkettung: AT die JWS-Signatur des letzten Belegs, CH ein Hash über den letzten Beleg.</summary>
    public string? LastChainValue { get; set; }

    /// <summary>AT: AES-256-Schlüssel für den Umsatzzähler (Base64), wird bei FinanzOnline registriert.</summary>
    public string? TurnoverKey { get; set; }

    /// <summary>Seriennummer des Signaturzertifikats bzw. Kennung der TSE.</summary>
    public string? CertificateSerial { get; set; }

    public DateTimeOffset? SignatureDeviceFailedSince { get; set; }

    /// <summary>
    /// Stelle, an der die Kasse kassiert (<c>sync.site</c> ihrer Datenbank). Beim Abgleich darf nur diese Stelle Belege ausstellen,
    /// damit die Signaturkette lückenlos bleibt (ADR 0041); <c>null</c> bei älteren Kassen bis zum ersten Beleg.
    /// </summary>
    public Guid? SiteId { get; set; }
}

public sealed class Bill
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RegisterId { get; init; }
    public Guid? CustomerId { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Open;
    public DateTimeOffset CreatedAt { get; init; }
    public List<BillLine> Lines { get; init; } = [];
}

public sealed class BillLine
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid BillId { get; init; }
    public int Position { get; init; }
    public BillLineKind Kind { get; init; }
    public required string Description { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Discount { get; set; }

    /// <summary>Bezeichnung des Rabatts für den Beleg, z. B. „Kundenrabatt 10 %“.</summary>
    public string? DiscountNote { get; set; }

    /// <summary>Wer einen Rabatt über der Grenze des Spas per PIN freigegeben hat (Nachvollziehbarkeit).</summary>
    public string? DiscountApprovedByUserId { get; set; }

    /// <summary>Zu zahlender Bruttobetrag der Position: Menge × Preis − Rabatt.</summary>
    public decimal Amount { get; set; }

    /// <summary>Bereits mit Belegen bezahlter Teil; ein Storno zieht ihn wieder ab.</summary>
    public decimal PaidAmount { get; set; }

    public TaxCategory TaxCategory { get; init; }
    public decimal TaxPercent { get; init; }
    public Guid RevenueAccountId { get; init; }

    /// <summary>Ein Termin steht auf höchstens einer Rechnung (eindeutiger Index), so wird nichts doppelt abgerechnet.</summary>
    public Guid? AppointmentId { get; init; }

    public Guid? TreatmentId { get; init; }
    public Guid? ArticleId { get; init; }
    public VoucherKind? VoucherKind { get; init; }

    /// <summary>Paket: Zahl der Behandlungen und freiwilliges Ablaufdatum; der Gutschein entsteht erst beim Bezahlen.</summary>
    public int? VoucherUnits { get; init; }

    public DateOnly? VoucherValidUntil { get; init; }

    /// <summary>Ein Eintritt steht auf höchstens einer Rechnung (eindeutiger Index).</summary>
    public Guid? AreaVisitId { get; init; }
}

public sealed class Receipt
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RegisterId { get; init; }
    public long Number { get; init; }
    public ReceiptKind Kind { get; init; }
    public DateTimeOffset IssuedAt { get; init; }
    public Guid? BillId { get; init; }
    public Guid? CustomerId { get; init; }
    public decimal Total { get; init; }
    public decimal Change { get; init; }
    public Guid? VoidsReceiptId { get; init; }
    public string? VoidReason { get; init; }

    /// <summary>Inhalt des QR-Codes (AT: maschinenlesbarer Code inkl. Signatur).</summary>
    public string? FiscalCode { get; set; }

    /// <summary>AT: der Beleg in JWS-Kompaktdarstellung für das Datenerfassungsprotokoll (DEP).</summary>
    public string? FiscalSignature { get; set; }

    public bool SignatureDeviceFailed { get; set; }
    public string? CreatedByUserId { get; init; }
    public List<ReceiptLine> Lines { get; init; } = [];
    public List<ReceiptPayment> Payments { get; init; } = [];
}

public sealed class ReceiptLine
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ReceiptId { get; init; }
    public int Position { get; init; }
    public required string Description { get; init; }
    public int Quantity { get; init; }

    /// <summary>Bezahlter Bruttobetrag nach Rabatt; nur er zählt für Summe, Steuer und Signatur.</summary>
    public decimal Amount { get; init; }

    /// <summary>Rabatt, der in <see cref="Amount"/> schon abgezogen ist; nur zur Anzeige auf dem Beleg.</summary>
    public decimal Discount { get; init; }

    public string? DiscountNote { get; init; }
    public TaxCategory TaxCategory { get; init; }
    public decimal TaxPercent { get; init; }
    public Guid RevenueAccountId { get; init; }
    public Guid? BillLineId { get; init; }
    public Guid? AppointmentId { get; init; }

    /// <summary>Verkaufter Gutschein oder eingelöster Einzweckgutschein.</summary>
    public Guid? VoucherId { get; init; }

    /// <summary>Verrechnete Anzahlung (negative Zeile auf dem Beleg der Behandlung).</summary>
    public Guid? DepositId { get; init; }
}

/// <summary>
/// Bezahlte Anzahlung auf einen Termin, mit Anzahlungsbeleg (<see cref="ReceiptId"/>) und dem Steuersatz der Behandlung versteuert.
/// Beim Kassieren der Behandlung zieht die Kasse sie als negative Zeile mit demselben Steuersatz ab (<see cref="SettledOnReceiptId"/>).
/// Wird der Anzahlungsbeleg storniert, ist sie weg (<see cref="VoidedAt"/>); ein Storno des Behandlungsbelegs macht sie wieder verrechenbar.
/// </summary>
public sealed class AppointmentDeposit
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AppointmentId { get; init; }
    public decimal Amount { get; init; }
    public TaxCategory TaxCategory { get; init; }
    public decimal TaxPercent { get; init; }
    public Guid RevenueAccountId { get; init; }
    public Guid ReceiptId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid? SettledOnReceiptId { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
}

/// <summary>Rechnungsempfänger zu einem Verkaufsbeleg; höchstens einer je Beleg und unveränderlich (Rechnung nach § 11 UStG).</summary>
public sealed class ReceiptRecipient
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ReceiptId { get; init; }
    public required string Name { get; init; }
    public required string Address { get; init; }
    public string? VatId { get; init; }
    public Guid? VatIdCheckId { get; init; }
    public DateTimeOffset AddedAt { get; init; }
    public string? UserId { get; init; }
}

public sealed class ReceiptPayment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ReceiptId { get; init; }
    public PaymentMethod Method { get; init; }

    /// <summary>Angerechneter Betrag; bei Bar ohne Rückgeld.</summary>
    public decimal Amount { get; init; }

    public Guid? VoucherId { get; init; }
    public string? Reference { get; init; }
}

public sealed class Voucher
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Code auf dem Gutschein, z. B. „K7QM-2XPA-9RDT“.</summary>
    public required string Code { get; init; }

    public VoucherKind Kind { get; init; }
    public decimal InitialValue { get; init; }
    public decimal Balance { get; set; }

    /// <summary>Einzweckgutschein: die Behandlung, für die er gilt.</summary>
    public Guid? TreatmentId { get; init; }

    public VoucherStatus Status { get; set; } = VoucherStatus.Active;
    public DateOnly? ValidUntil { get; init; }

    /// <summary>Paket (z. B. 10er-Block): Zahl der Behandlungen; <c>null</c> bei gewöhnlichen Gutscheinen.</summary>
    public int? Units { get; init; }

    /// <summary>Paket: noch offene Behandlungen. Jede Einlösung zieht eine ab, ein Storno gibt sie zurück.</summary>
    public int? UnitsLeft { get; set; }

    /// <summary>Paket: der Gast, dem es gehört (für Kalender und Kassa).</summary>
    public Guid? CustomerId { get; init; }
    public Guid SoldOnReceiptId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Optimistische Sperre: zwei Kassen können denselben Gutschein nicht gleichzeitig einlösen.</summary>
    public uint Version { get; init; }
}

public sealed class CashMovement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RegisterId { get; init; }
    public CashMovementKind Kind { get; init; }
    public decimal Amount { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset At { get; init; }
    public string? UserId { get; init; }
}

/// <summary>Tagesabschluss; der Bericht wird beim Abschluss festgeschrieben und ändert sich danach nicht mehr.</summary>
public sealed class DayClosing
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid RegisterId { get; init; }
    public DateTimeOffset From { get; init; }
    public DateTimeOffset Until { get; init; }
    public decimal CountedCash { get; init; }
    public string? UserId { get; init; }

    /// <summary><see cref="DayClosingDto"/> als JSON.</summary>
    public required string Report { get; init; }
}

/// <summary>Schlüssel der Test-Signatureinrichtung (nur Entwicklung und Tests; eine echte Signaturkarte oder ein HSM hält den Schlüssel selbst).</summary>
public sealed class TestSignatureKey
{
    public Guid RegisterId { get; init; }
    public required string PrivateKey { get; init; }
    public required string Certificate { get; init; }
}
