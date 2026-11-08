namespace AispaStudio.Contracts;

// Rückerstattung online bezahlter Belege (Online-Gutschein, Ausfallgebühr): erst das Storno an der Kasse, dann gibt der
// Zahlungsanbieter das Geld zurück. So steht jede Erstattung mit Stornobeleg im Tagesabschluss.

/// <summary>Was online bezahlt wurde.</summary>
public enum OnlinePaymentSource
{
    Voucher,
    NoShowFee,

    /// <summary>Anzahlung oder Vorauszahlung auf einen Online-Termin.</summary>
    Deposit,
}

public enum OnlineRefundStatus
{
    /// <summary>Storniert; der Zahlungsanbieter wird gerade um die Erstattung gebeten.</summary>
    Pending,

    Refunded,

    /// <summary>Der Anbieter hat abgelehnt oder war nicht erreichbar; die Kassenaufsicht kann es erneut versuchen.</summary>
    Failed,
}

/// <summary>
/// Online-Zahlung hinter einem Beleg. <c>Voided</c>: Beleg ist storniert. <c>Status</c> fehlt, solange keine Erstattung begonnen hat.
/// <c>CanRetry</c>: storniert, aber das Geld ist noch nicht zurück (Erstattung fehlgeschlagen oder nie begonnen).
/// </summary>
public sealed record OnlineRefundDto(
    Guid ReceiptId,
    OnlinePaymentSource Source,
    decimal Amount,
    string Currency,
    string Provider,
    bool Voided,
    OnlineRefundStatus? Status,
    string? Reference,
    DateTimeOffset? RefundedAt,
    string? Error,
    bool CanRetry);
