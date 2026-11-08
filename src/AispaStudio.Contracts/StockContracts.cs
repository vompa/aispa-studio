namespace AispaStudio.Contracts;

/// <summary>
/// Lagerbewegung. Wareneingang, Inventur, Korrektur, Umbuchung und Nachfüllen erfasst die Leitung; Verbrauch (abgeschlossene Behandlung
/// laut Rezeptur) und Verkauf (Artikel an der Kassa) bucht das System selbst. <c>Refill</c>: vom Hauptlager (oder einem anderen Lagerort)
/// an einen Lagerort desselben Standorts, gebucht als zwei Bewegungen.
/// </summary>
public enum StockMovementKind
{
    GoodsReceipt,
    Consumption,
    Sale,
    Count,
    Correction,
    Transfer,
    Refill,
}

/// <summary>Art eines Lagerorts: Standardlager des Standorts, Schrank eines Behandlungsraums, Verkaufsregal oder sonstiger Ort.</summary>
public enum StoragePlaceKind
{
    Main,
    Room,
    Sales,
    Other,
}

/// <summary>
/// Lagerort eines Standorts. Jeder Standort hat genau einen Lagerort der Art <c>Main</c> („Hauptlager“, nicht löschbar und nicht
/// deaktivierbar), dazu höchstens neun weitere. <c>RoomId</c> nur bei <c>Room</c>. <c>Minimums</c>: Mindestmengen je Material an diesem Ort.
/// </summary>
public sealed record StoragePlaceDto(
    Guid Id,
    Guid LocationId,
    string Name,
    string? Description,
    StoragePlaceKind Kind,
    Guid? RoomId,
    bool IsActive,
    int Position,
    IReadOnlyList<StoragePlaceMinimumDto> Minimums);

public sealed record StoragePlaceInput(
    string Name,
    string? Description,
    StoragePlaceKind Kind,
    Guid? RoomId,
    bool IsActive = true,
    IReadOnlyList<StoragePlaceMinimumDto>? Minimums = null);

/// <summary><c>MaximumQuantity</c>: Höchstmenge des Materials an diesem Ort; die Summe der Lagerorte ist das Bestellziel des Standorts.</summary>
public sealed record StoragePlaceMinimumDto(Guid ItemId, decimal MinimumQuantity, decimal? MaximumQuantity = null);

/// <summary>Menge eines Materials an einem Lagerort (nur Orte mit Bestand ungleich 0).</summary>
public sealed record StockPlaceQuantityDto(Guid StoragePlaceId, decimal Quantity);

/// <summary>
/// Bestand eines Materials an einem Lagerort. <c>MinimumQuantity</c> nur, wenn für den Ort eine Mindestmenge eingetragen ist.
/// <c>TreatmentsCovered</c>: Behandlungen laut Richtwert des Materials, gerechnet mit der Menge an diesem Ort; ohne Richtwert <c>null</c> (T-021, Ä374).
/// </summary>
public sealed record StoragePlaceLevelDto(
    Guid ItemId,
    string Name,
    string Unit,
    decimal Quantity,
    decimal? MinimumQuantity,
    bool BelowMinimum,
    decimal Value,
    DateOnly? NextExpiry,
    IReadOnlyList<StockBatchDto> Batches,
    int? TreatmentsCovered = null);

public sealed record StoragePlaceLevelsDto(Guid StoragePlaceId, decimal TotalValue, IReadOnlyList<StoragePlaceLevelDto> Items);

/// <summary>
/// Material oder Verkaufsprodukt, gilt für alle Standorte. <c>UnitCost</c>: Einstandspreis netto je Einheit (gleitender Durchschnitt der
/// Wareneingänge). <c>ArticleId</c> verknüpft ein Verkaufsprodukt der Kassa; dann bucht jeder Verkauf ab.
/// <c>MinimumQuantity</c>: Mindestbestand je Standort. <c>Supplier</c> und <c>SupplierArticleNumber</c>: bei wem (Name aus dem Lieferantenstamm,
/// <c>SupplierId</c>) und unter welcher Nummer bestellt wird; der Bestellvorschlag fasst je Lieferant zusammen.
/// <c>PackUnit</c> und <c>PackSize</c>: Packung und ihre Menge in der Einheit (Flasche, 500); <c>CaseUnit</c> und <c>PacksPerCase</c>: Gebinde und Zahl
/// der Packungen darin (Karton, 4). <c>TreatmentDose</c>: Richtwert je Behandlung in der Einheit (abgebucht wird nach dem Rezept).
/// <c>ExpiryWarningDays</c>: so viele Tage vor dem Ablaufdatum gilt eine Charge als bald ablaufend. <c>Packages</c>: die buchbaren Ebenen, von der API gerechnet.
/// <c>Barcode</c>: Code auf der Verpackung (EAN oder Lieferantencode, 3 bis 50 Zeichen ohne Leerzeichen), je Spa eindeutig; ein Handscanner tippt ihn.
/// </summary>
public sealed record StockItemDto(Guid Id, string Name, string Unit, decimal UnitCost, decimal MinimumQuantity, Guid? ArticleId, bool IsActive,
    string? Supplier = null, string? SupplierArticleNumber = null, Guid? SupplierId = null, string? PackUnit = null, decimal? PackSize = null,
    string? CaseUnit = null, int? PacksPerCase = null, decimal? TreatmentDose = null, int ExpiryWarningDays = 30,
    IReadOnlyList<StockPackageDto>? Packages = null, string? Barcode = null);

/// <summary>
/// Material anlegen oder ändern. <c>SupplierId</c> wählt den Lieferanten aus dem Stamm; <c>Supplier</c> nimmt ersatzweise einen Namen und legt den Lieferanten an,
/// wenn es ihn noch nicht gibt.
/// </summary>
public sealed record StockItemInput(string Name, string Unit, decimal UnitCost, decimal MinimumQuantity, Guid? ArticleId, bool IsActive = true,
    string? Supplier = null, string? SupplierArticleNumber = null, Guid? SupplierId = null, string? PackUnit = null, decimal? PackSize = null,
    string? CaseUnit = null, int? PacksPerCase = null, decimal? TreatmentDose = null, int ExpiryWarningDays = 30, string? Barcode = null);

/// <summary>In welcher Ebene ein Wareneingang erfasst wird; die Bewegung bleibt immer in der Einheit des Materials.</summary>
public enum StockPackageLevel
{
    Unit,
    Pack,
    Case,
}

/// <summary>Eine buchbare Ebene eines Materials: Bezeichnung (z. B. „Karton (4 × 500 ml)“) und Menge in der Einheit des Materials.</summary>
public sealed record StockPackageDto(StockPackageLevel Level, string Name, string Label, decimal BaseQuantity);

/// <summary>Wie beim Lieferanten bestellt wird.</summary>
public enum SupplierOrderChannel
{
    Email,
    Phone,
    Portal,
    Representative,
}

/// <summary>
/// Lieferant im Stamm, gilt für alle Standorte. <c>LeadTimeDays</c>: Lieferzeit in Tagen; <c>MinimumOrderValue</c>: Mindestbestellwert netto;
/// <c>OrderChannel</c>: Bestellweg. Löschen gibt es nicht, nur Deaktivieren.
/// </summary>
public sealed record StockSupplierDto(Guid Id, string Name, string? ContactPerson, string? Email, string? Phone, int? LeadTimeDays, decimal? MinimumOrderValue,
    SupplierOrderChannel OrderChannel, bool IsActive);

public sealed record StockSupplierInput(string Name, string? ContactPerson = null, string? Email = null, string? Phone = null, int? LeadTimeDays = null,
    decimal? MinimumOrderValue = null, SupplierOrderChannel OrderChannel = SupplierOrderChannel.Email, bool IsActive = true);

/// <summary>
/// Bestand eines Materials an einem Standort. <c>PlannedUse</c>: Verbrauch laut Rezeptur für die gebuchten Termine der nächsten
/// <c>StockOverviewDto.Days</c> Tage. <c>OnOrder</c>: offen bestellt (Entwürfe und bestellte, noch nicht gelieferte Mengen).
/// <c>Suggested</c>: vorgeschlagene Bestellmenge bis zum Bestellziel, nach Tagesverbrauch, Terminen und Lieferzeit, auf Packungen aufgerundet; offene Bestellungen sind abgezogen.
/// <c>TreatmentsCovered</c>: für so viele Behandlungen reicht der Bestand laut Richtwert des Materials; ohne Richtwert leer.
/// </summary>
public sealed record StockLevelDto(
    Guid ItemId,
    string Name,
    string Unit,
    decimal Quantity,
    decimal MinimumQuantity,
    decimal Value,
    decimal PlannedUse,
    decimal Suggested,
    bool BelowMinimum,
    decimal OnOrder = 0,
    string? Supplier = null,
    DateOnly? NextExpiry = null,
    IReadOnlyList<StockPlaceQuantityDto>? Places = null,
    int? TreatmentsCovered = null);

public sealed record StockOverviewDto(Guid LocationId, int Days, decimal TotalValue, IReadOnlyList<StockLevelDto> Items);

/// <summary>
/// Von Hand erfasste Bewegung. <c>GoodsReceipt</c>: Menge &gt; 0, optional Einstandspreis je Einheit. <c>Count</c>: Menge ist der gezählte
/// Bestand, gebucht wird die Abweichung. <c>Correction</c>: Menge mit Vorzeichen, Grund nötig (z. B. Bruch, abgelaufen).
/// <c>Transfer</c>: Menge &gt; 0 an <c>TargetLocationId</c> (landet im Hauptlager des Ziels) oder an <c>TargetStoragePlaceId</c> (Lagerort
/// desselben Standorts). <c>Refill</c>: Menge &gt; 0 vom Lagerort <c>StoragePlaceId</c> an <c>TargetStoragePlaceId</c>, Charge und Ablaufdatum
/// wandern mit. <c>StoragePlaceId</c>: Lagerort der Bewegung, Pflicht außer beim Wareneingang (dort Hauptlager). <c>Batch</c> und <c>ExpiresOn</c>: Charge und Ablaufdatum, von Hand nur beim
/// Wareneingang; Verbrauch und Verkauf tragen die Charge, aus der das System gebucht hat (zuerst ablaufend zuerst).
/// <c>PackageLevel</c>: nur beim Wareneingang; Menge und Preis gelten dann je Packung oder Gebinde, die API rechnet sie in die Einheit des Materials um.
/// </summary>
public sealed record StockMovementInput(
    Guid ItemId,
    StockMovementKind Kind,
    decimal Quantity,
    decimal? UnitCost = null,
    Guid? TargetLocationId = null,
    string? Reason = null,
    string? Batch = null,
    DateOnly? ExpiresOn = null,
    Guid? StoragePlaceId = null,
    Guid? TargetStoragePlaceId = null,
    StockPackageLevel PackageLevel = StockPackageLevel.Unit);

public sealed record StockMovementDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid LocationId,
    Guid StoragePlaceId,
    string StoragePlaceName,
    StockMovementKind Kind,
    decimal Quantity,
    decimal UnitCost,
    DateTimeOffset At,
    string? Reason,
    Guid? AppointmentId,
    Guid? ReceiptId,
    Guid? PurchaseOrderId = null,
    string? Batch = null,
    DateOnly? ExpiresOn = null);

/// <summary>
/// Noch vorhandene Menge einer Charge mit Ablaufdatum an einem Lagerort. Das Lager rechnet „zuerst ablaufend, zuerst verbraucht“: der
/// Bestand gehört den zuletzt ablaufenden Wareneingängen, ältere Chargen sind damit aufgebraucht. <c>Expired</c>: Ablaufdatum vor heute;
/// <c>ExpiresSoon</c>: innerhalb von <c>StockBatchesDto.WarningDays</c> Tagen.
/// </summary>
public sealed record StockBatchDto(
    Guid ItemId,
    string ItemName,
    string Unit,
    string? Batch,
    DateOnly ExpiresOn,
    decimal Quantity,
    DateTimeOffset ReceivedAt,
    bool Expired,
    bool ExpiresSoon,
    Guid StoragePlaceId,
    string StoragePlaceName);

public sealed record StockBatchesDto(Guid LocationId, DateOnly Today, int WarningDays, IReadOnlyList<StockBatchDto> Batches);

/// <summary>
/// Eine Charge in den Aufgaben des Lagers. <c>DaysLeft</c>: Tage bis zum Ablaufdatum; <c>ArticleName</c> und <c>SalePrice</c>: der verknüpfte, aktive Artikel der Kassa.
/// </summary>
public sealed record StockExpiryTaskDto(
    Guid ItemId,
    string ItemName,
    string Unit,
    string? Batch,
    DateOnly ExpiresOn,
    int DaysLeft,
    decimal Quantity,
    Guid StoragePlaceId,
    string StoragePlaceName,
    string? ArticleName = null,
    decimal? SalePrice = null);

/// <summary>
/// Aufgaben des Lagers zu Ablaufdaten, zuerst ablaufend zuerst. <c>Expiring</c>: innerhalb der Frist des Materials und noch gültig; <c>SellOff</c>: der Teil davon,
/// der an der Kassa verkauft werden kann; <c>Expired</c>: abgelaufen, zum Ausbuchen.
/// </summary>
public sealed record StockExpiryTasksDto(Guid LocationId, DateOnly Today, IReadOnlyList<StockExpiryTaskDto> Expiring, IReadOnlyList<StockExpiryTaskDto> SellOff,
    IReadOnlyList<StockExpiryTaskDto> Expired);

/// <summary>
/// Eine Buchung zu einer Charge: Eingang, Verbrauch eines Termins (mit Kunde und Behandlung), Verkauf oder Storno eines Belegs (mit Kunde,
/// wenn bekannt), Korrektur. <c>Quantity</c> mit Vorzeichen wie die Lagerbewegung, Bewegungen desselben Termins oder Belegs sind zusammengefasst.
/// </summary>
public sealed record StockBatchUsageEntry(
    DateTimeOffset At,
    StockMovementKind Kind,
    Guid LocationId,
    string LocationName,
    decimal Quantity,
    Guid? CustomerId,
    string? CustomerName,
    string? Detail,
    Guid? AppointmentId,
    Guid? ReceiptId);

/// <summary>Rückverfolgung einer Charge über alle Standorte: was kam herein, was ging an wen (für einen Rückruf). <c>Customers</c> zählt, wer nach Stornos noch Material dieser Charge hat.</summary>
public sealed record StockBatchUsageDto(
    Guid ItemId,
    string ItemName,
    string Unit,
    string Batch,
    DateOnly? ExpiresOn,
    decimal Received,
    decimal Used,
    IReadOnlyList<StockBatchUsageEntry> Entries,
    int Customers = 0);

/// <summary>„Passt dazu“: ein zur Behandlung empfohlener Verkaufsartikel mit Bestand am Verkaufsregal (Slice 115); ein Klick legt ihn auf den Beleg.</summary>
public sealed record RecommendedArticleDto(Guid ArticleId, string Name, decimal Price, decimal ShelfQuantity, string Unit);

/// <summary>
/// Eine Behandlung, die einen Artikel im Zeitraum verbraucht hat: <c>Planned</c> der gebuchte Verbrauch laut Rezept und Extras, <c>Appointments</c> die Zahl der Termine.
/// </summary>
public sealed record StockUsageTreatmentDto(Guid TreatmentId, string Name, decimal Planned, int Appointments);

/// <summary>
/// Soll und Ist eines Artikels. <c>Planned</c> (Soll): gebuchter Verbrauch laut Rezept. <c>Excess</c> (Mehrverbrauch, Schwund): was die Inventuren darüber hinaus
/// fehlen sahen, negativ, wenn sie mehr fanden. <c>Actual</c> (Ist) = Soll + Mehrverbrauch. <c>ExcessValue</c>: Mehrverbrauch zum Einstandspreis, nur mit dem Recht
/// „Berichte mit Beträgen“. <c>Treatments</c>: die Behandlungen, die den Artikel nutzen, mit ihrem Soll.
/// </summary>
public sealed record StockUsageLineDto(
    Guid ItemId,
    string Name,
    string Unit,
    decimal Planned,
    decimal Actual,
    decimal Excess,
    decimal? ExcessValue,
    IReadOnlyList<StockUsageTreatmentDto> Treatments);

/// <summary>Bericht Soll-Ist-Verbrauch eines Standorts (beide Tage eingeschlossen, Ortszeit); <c>AmountsHidden</c>: ohne das Recht „Berichte mit Beträgen“ fehlen die Werte in Euro.</summary>
public sealed record StockUsageReportDto(
    Guid LocationId,
    DateOnly From,
    DateOnly Until,
    IReadOnlyList<StockUsageLineDto> Lines,
    decimal? ExcessValue,
    bool AmountsHidden);

/// <summary>Stand einer Zählung vom Handy: offen (wird gezählt), abgeschickt (wartet auf die Leitung), geschlossen (gebucht) oder verworfen.</summary>
public enum StockCountSessionStatus
{
    Open,
    Submitted,
    Closed,
    Discarded,
}

/// <summary>Zwischenstand einer Zeile: gezählte Menge (ab 0, höchstens drei Nachkommastellen); <c>null</c> löscht die Zeile.</summary>
public sealed record StockCountLineInput(decimal? Counted);

/// <summary>Eine gezählte Zeile. <c>Expected</c>: Bestand des Lagerorts zum Abruf, nur in der Ansicht der Leitung (das Handy zählt blind).</summary>
public sealed record StockCountLineDto(Guid ItemId, string Name, string Unit, decimal Counted, decimal? Expected);

/// <summary>Zählung eines Lagerorts vom Handy. <c>StartedBy</c>: Name der zählenden Person.</summary>
public sealed record StockCountSessionDto(
    Guid Id,
    Guid LocationId,
    Guid StoragePlaceId,
    string StoragePlaceName,
    StockCountSessionStatus Status,
    string? StartedBy,
    DateTimeOffset StartedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<StockCountLineDto> Lines);
