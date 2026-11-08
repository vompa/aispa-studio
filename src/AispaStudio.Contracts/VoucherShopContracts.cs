namespace AispaStudio.Contracts;

// Gutscheine online kaufen: Wertgutschein mit frei wählbarem Betrag oder Gutschein für eine Behandlung. Bezahlt wird über einen
// Zahlungsanbieter; erst nach der Zahlung stellt die Kasse des Standorts den Gutschein mit Beleg aus, wie beim Verkauf am Empfang.

/// <summary>
/// Gutscheinangebot eines Standorts. <c>Available</c> ist falsch, wenn dort online keine Gutscheine verkauft werden (keine aktive
/// Kasse oder kein Zahlungsanbieter); <c>Reason</c> sagt dann, warum. <c>SuggestedAmounts</c>: Vorschläge für Wertgutscheine.
/// </summary>
public sealed record OnlineVoucherOffer(
    Guid LocationId,
    string LocationName,
    string Currency,
    bool Available,
    string? Reason,
    decimal MinAmount,
    decimal MaxAmount,
    IReadOnlyList<decimal> SuggestedAmounts,
    IReadOnlyList<OnlineTreatment> Treatments,
    bool TestPayment);

/// <summary>
/// Bestellung. Wertgutschein (<c>MultiPurpose</c>): <c>Amount</c>. Behandlungsgutschein (<c>SinglePurpose</c>): <c>TreatmentId</c>,
/// der Preis kommt aus der Behandlung. <c>Buyer</c> bekommt den Gutschein per Mail; <c>RecipientName</c> und <c>Message</c> stehen darauf.
/// </summary>
public sealed record VoucherOrderRequest(
    Guid LocationId,
    VoucherKind Kind,
    decimal? Amount,
    Guid? TreatmentId,
    OnlineContact? Buyer,
    string? RecipientName,
    string? Message,
    bool AcceptPrivacy,
    string? Language);

public enum VoucherOrderStatus
{
    /// <summary>Wartet auf die Zahlung.</summary>
    Pending,

    /// <summary>Bezahlt; die Kasse stellt den Gutschein gerade aus.</summary>
    Paid,

    /// <summary>Gutschein ausgestellt und verschickt.</summary>
    Issued,

    /// <summary>Zahlung abgebrochen.</summary>
    Cancelled,

    /// <summary>Bezahlt, aber die Kasse konnte nicht ausstellen; das Team kümmert sich darum.</summary>
    Failed,

    /// <summary>Beleg an der Kasse storniert und das Geld über den Zahlungsanbieter zurückerstattet.</summary>
    Refunded,
}

/// <summary>
/// Bestellung angelegt; <c>PaymentUrl</c> führt weiter: im Testbetrieb zur Bestellseite, sonst zur Bestellseite, die zur
/// Zahlungsseite des Anbieters weiterleitet, sobald diese bereit ist. <c>Token</c> öffnet die Bestellung ohne Anmeldung.
/// </summary>
public sealed record VoucherOrderStarted(Guid OrderId, string Token, string PaymentUrl);

/// <summary>
/// Stand einer Bestellung. <c>Code</c> und <c>ValidUntil</c> erst, wenn der Gutschein ausgestellt ist.
/// <c>TestPayment</c>: Testbetrieb, auf der Seite kann die Zahlung simuliert werden. <c>PaymentUrl</c>: Zahlungsseite des
/// Anbieters, solange die Bestellung auf die Zahlung wartet (leer, bis der Anbieter sie geliefert hat).
/// </summary>
public sealed record VoucherOrderDto(
    Guid Id,
    VoucherOrderStatus Status,
    VoucherKind Kind,
    decimal Amount,
    string Currency,
    string LocationName,
    string? TreatmentName,
    string? RecipientName,
    string? Message,
    string? Code,
    DateOnly? ValidUntil,
    bool TestPayment,
    string? PaymentUrl);

/// <summary>Testzahlung: <c>Paid</c> simuliert eine erfolgreiche Zahlung, sonst einen Abbruch. Nur im Testbetrieb.</summary>
public sealed record TestPaymentResult(bool Paid);

/// <summary>Online-Gutscheinbestellung für das Team. <c>Error</c>: warum die Kasse nicht ausstellen konnte.</summary>
public sealed record VoucherOrderStaffEntry(
    Guid Id,
    DateTimeOffset CreatedAt,
    VoucherOrderStatus Status,
    VoucherKind Kind,
    decimal Amount,
    string Currency,
    string BuyerEmail,
    string? RecipientName,
    Guid? ReceiptId,
    string? Error);
