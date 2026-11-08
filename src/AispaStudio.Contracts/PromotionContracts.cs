namespace AispaStudio.Contracts;

/// <summary>
/// Wofür eine Aktion gilt: alle Behandlungen, ausgewählte Behandlungen oder die Pakete (Mehrfachkarten) ausgewählter Behandlungen.
/// Die Aktion senkt immer nur den Preis der Position an der Kassa; verkauft wird nichts an der Kassa vorbei (ADR 0010, ADR 0071).
/// </summary>
public enum PromotionScope
{
    AllTreatments,
    SelectedTreatments,
    SelectedPackages,
}

/// <summary>
/// Zustand einer Aktion am heutigen Tag des Standorts: <c>Running</c> läuft, <c>Planned</c> beginnt später, <c>Paused</c> ist angehalten
/// (und noch nicht abgelaufen), <c>Expired</c> ist vorbei. Archivierte Aktionen zeigt die Liste nur auf Wunsch.
/// </summary>
public enum PromotionPhase
{
    Running,
    Planned,
    Paused,
    Expired,
}

/// <summary>
/// Aktion anlegen oder ändern. Genau eines von <c>DiscountPercent</c> und <c>DiscountAmount</c> ist gesetzt: Prozent vom Preis der Position
/// oder ein fester Betrag je Position. <c>ValidFrom</c> und <c>ValidUntil</c> sind Kalendertage am Standort (beide einschließlich).
/// <c>LocationIds</c> bleibt bei <c>AllLocations</c> leer; <c>TreatmentIds</c> nennt die Behandlungen bei <c>SelectedTreatments</c> und
/// <c>SelectedPackages</c> und bleibt sonst leer.
/// </summary>
/// <param name="GuestText">Kurzer Text für Gäste und den Assistenten, z. B. „10 % auf alle Massagen im Oktober“.</param>
public sealed record PromotionInput(
    string Name,
    string GuestText,
    DateOnly ValidFrom,
    DateOnly ValidUntil,
    bool AllLocations,
    IReadOnlyList<Guid>? LocationIds,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    PromotionScope Scope,
    IReadOnlyList<Guid>? TreatmentIds,
    bool IsActive);

/// <param name="IsActive">Aktiv oder pausiert; eine pausierte Aktion wird an der Kassa nicht vorgeschlagen.</param>
/// <param name="IsArchived">Archiviert: bleibt im Verlauf, läuft aber nie mehr.</param>
public sealed record PromotionDto(
    Guid Id,
    string Name,
    string GuestText,
    DateOnly ValidFrom,
    DateOnly ValidUntil,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    PromotionScope Scope,
    IReadOnlyList<Guid> TreatmentIds,
    bool IsActive,
    bool IsArchived,
    PromotionPhase Phase);

public sealed record PromotionTreatmentDto(Guid Id, string Name);

/// <summary>
/// Laufende Aktion an einem Standort und Tag; Grundlage für Kassa und Assistent. <c>DiscountText</c> ist der Rabatt in Worten
/// („−10 %“ oder „−5,00 €“), <c>Treatments</c> leer bei <c>AllTreatments</c>.
/// </summary>
public sealed record ActivePromotionDto(
    Guid Id,
    string Name,
    string GuestText,
    DateOnly ValidFrom,
    DateOnly ValidUntil,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    string DiscountText,
    PromotionScope Scope,
    IReadOnlyList<PromotionTreatmentDto> Treatments);

/// <summary>Position einer offenen Rechnung, auf die ein Vorschlag wirkt, mit dem Rabatt, den die Kassa dafür rechnet.</summary>
public sealed record PromotionOfferLineDto(Guid LineId, string Description, decimal Discount);

/// <summary>
/// Vorschlag der Kassa: eine laufende Aktion, die zu Positionen der offenen Rechnung passt. Angewendet wird nur auf Klick
/// (<c>POST /api/pos/bills/{billId}/promotions/{promotionId}/apply</c>); den Rabatt rechnet die Kassa, nicht die Oberfläche.
/// </summary>
public sealed record PromotionOfferDto(Guid PromotionId, string Name, string DiscountText, IReadOnlyList<PromotionOfferLineDto> Lines, decimal TotalDiscount);
