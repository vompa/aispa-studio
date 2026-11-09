namespace AispaStudio.Contracts;

/// <summary>
/// Bestellung an einen Lieferanten: <c>Draft</c> lässt sich ändern, <c>Ordered</c> ist beim Lieferanten bestellt (per Mail aus dem Programm oder vom Spa selbst geschickt),
/// Lieferungen buchen den Wareneingang (<c>PartiallyReceived</c>, <c>Received</c>). <c>Cancelled</c>: storniert, ohne dass etwas kam.
/// </summary>
public enum PurchaseOrderStatus
{
    Draft,
    Ordered,
    PartiallyReceived,
    Received,
    Cancelled,
}

/// <summary>Position einer Bestellung; ohne Preis gilt der aktuelle Einstandspreis des Materials.</summary>
public sealed record PurchaseOrderLineInput(Guid ItemId, decimal Quantity, decimal? UnitCost = null);

public sealed record PurchaseOrderInput(string Supplier, IReadOnlyList<PurchaseOrderLineInput> Lines, string? Note = null);

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    string Unit,
    string? SupplierArticleNumber,
    decimal Quantity,
    decimal UnitCost,
    decimal Received,
    string? PackageText = null);

/// <summary>
/// Bestellung mit laufender Nummer je Unternehmen; <c>Total</c> netto zu den Bestellpreisen. <c>MinimumOrderValue</c>: Mindestbestellwert des Lieferanten,
/// <c>MissingToMinimum</c>: was bis dahin fehlt (sonst 0). <c>SupplierEmail</c>: Mailadresse des Lieferanten für „Bestellen und senden“.
/// <c>SentMails</c>: die Vermerke „Gesendet am … an …“ der Bestellung als PDF per Mail (PDF-Ausgaben F), damit Doppelsendungen auffallen.
/// </summary>
public sealed record PurchaseOrderDto(
    Guid Id,
    int Number,
    Guid LocationId,
    string Supplier,
    PurchaseOrderStatus Status,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? OrderedAt,
    DateTimeOffset? ClosedAt,
    decimal Total,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    decimal? MinimumOrderValue = null,
    decimal MissingToMinimum = 0,
    string? SupplierEmail = null,
    IReadOnlyList<SentDocumentDto>? SentMails = null);

/// <summary>
/// Eine Zeile des Bestellvorschlags mit dem Rechenweg: <c>DailyUse</c> aus Verbrauch und Verkauf der letzten 90 Tage, <c>BookedUse</c> der gebuchten Termine bis zur Lieferung,
/// <c>Target</c> Bestellziel (Summe der Höchstmengen, sonst Mindestbestand), <c>Suggested</c> in der Einheit des Materials, auf Packungen aufgerundet. <c>LeadTimeDays</c> leer: keine Lieferzeit bekannt.
/// </summary>
public sealed record ReorderSuggestionDto(
    Guid ItemId,
    string ItemName,
    string Unit,
    string? Supplier,
    decimal OnHand,
    decimal DailyUse,
    int? LeadTimeDays,
    decimal BookedUse,
    decimal OnOrder,
    decimal Target,
    decimal Suggested,
    string? PackageText,
    decimal Cost);

/// <summary>Gelieferte Menge einer Position, optional mit dem Preis laut Lieferschein (sonst der Bestellpreis).</summary>
public sealed record PurchaseOrderDeliveryLineInput(Guid LineId, decimal Quantity, decimal? UnitCost = null, string? Batch = null, DateOnly? ExpiresOn = null);

/// <summary>Lieferung zu einer Bestellung; <c>DeliveryNote</c> ist die Nummer des Lieferscheins und steht als Grund an den Bewegungen.</summary>
public sealed record PurchaseOrderDeliveryInput(IReadOnlyList<PurchaseOrderDeliveryLineInput> Lines, string? DeliveryNote = null);
