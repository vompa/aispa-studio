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
/// Art einer Aktion (Nachtrag zu ADR 0071, nach dem Muster der Plattformen): <c>Limited</c> gilt an jedem Tag des Zeitraums (befristete Aktion),
/// <c>LastMinute</c> nur für Termine, die höchstens <c>LeadHours</c> Stunden vor Beginn gebucht wurden (kurzfristig freie Termine), <c>OffPeak</c> nur für
/// Termine in den gewählten Feldern des Wochenrasters aus Wochentag und Tageszeit (schwache Zeiten). Alle drei schlägt die Kassa nur vor und setzt sie auf Klick.
/// </summary>
public enum PromotionKind
{
    Limited,
    LastMinute,
    OffPeak,
}

/// <summary>Ein Feld des Wochenrasters schwacher Zeiten: Wochentag und Tageszeit am Standort (vormittags bis 12 Uhr, nachmittags 12 bis 17 Uhr, abends ab 17 Uhr).</summary>
public sealed record PromotionSlot(DayOfWeek Day, DayPart Part);

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
    bool IsActive)
{
    /// <summary>Art der Aktion; leer heißt befristet (wie vor dem Nachtrag zu ADR 0071).</summary>
    public PromotionKind? Kind { get; init; }

    /// <summary>Nur bei <c>LastMinute</c>: höchstens so viele Stunden zwischen Buchung und Beginn des Termins (1 bis 168).</summary>
    public int? LeadHours { get; init; }

    /// <summary>Nur bei <c>OffPeak</c>: die Felder des Wochenrasters, in denen der Rabatt gilt (mindestens eines).</summary>
    public IReadOnlyList<PromotionSlot>? Slots { get; init; }
}

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
    PromotionPhase Phase)
{
    /// <summary>Art der Aktion; leer bei Antworten älterer Server (dann befristet).</summary>
    public PromotionKind? Kind { get; init; }

    /// <summary>Bei <c>LastMinute</c>: höchstens so viele Stunden zwischen Buchung und Beginn.</summary>
    public int? LeadHours { get; init; }

    /// <summary>Bei <c>OffPeak</c>: die gewählten Felder des Wochenrasters.</summary>
    public IReadOnlyList<PromotionSlot>? Slots { get; init; }
}

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

/// <summary>Eine Behandlung in der Vorschau des Gästepreises: Preis laut Katalog und Preis mit dem Rabatt der Aktion.</summary>
public sealed record PromotionPreviewLineDto(Guid TreatmentId, string Name, decimal Price, decimal GuestPrice);

/// <summary>
/// Vorschau „So sieht es der Gast“: für ein Beispiel (<c>Example</c>, etwa „Dienstag, 24.11., Vormittag“) die ersten betroffenen Behandlungen mit Preis und Gästepreis.
/// Gerechnet wie an der Kassa (<c>PromotionRules.DiscountOf</c>); <c>Note</c> erklärt, wann der Rabatt gilt und dass zwei Rabatte nicht zusammen gelten.
/// </summary>
public sealed record PromotionPreviewDto(string Example, IReadOnlyList<PromotionPreviewLineDto> Lines, string Note);
