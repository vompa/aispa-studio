using System.Globalization;
using AispaStudio.Api.Pos;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Promotions;

/// <summary>
/// Wann eine Aktion läuft, auf welche Positionen sie wirkt und wie viel Rabatt sie gibt (ADR 0071). Reine Rechenschritte ohne Datenbank:
/// dieselben nutzen <see cref="PosService"/> im Betrieb und der Verlauf der Beispieldaten im Speicher.
/// </summary>
internal static class PromotionRules
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>Läuft die Aktion an diesem Standort an diesem Kalendertag: aktiv, nicht archiviert, im Zeitraum, am Standort gültig.</summary>
    public static bool IsRunning(Promotion promotion, Guid locationId, DateOnly date) =>
        promotion is { IsActive: true, ArchivedAt: null }
        && promotion.ValidFrom <= date && date <= promotion.ValidUntil
        && (promotion.AllLocations || promotion.Locations.Any(l => l.LocationId == locationId));

    /// <summary>Läuft die Aktion an diesem Standort an mindestens einem Kalendertag von <paramref name="from"/> bis <paramref name="until"/> (beide eingeschlossen).</summary>
    public static bool IsRunningDuring(Promotion promotion, Guid locationId, DateOnly from, DateOnly until) =>
        promotion is { IsActive: true, ArchivedAt: null }
        && promotion.ValidFrom <= until && from <= promotion.ValidUntil
        && (promotion.AllLocations || promotion.Locations.Any(l => l.LocationId == locationId));

    /// <summary>Zustand am Tag <paramref name="today"/>: vorbei vor pausiert vor geplant vor läuft.</summary>
    public static PromotionPhase PhaseOf(Promotion promotion, DateOnly today) =>
        today > promotion.ValidUntil ? PromotionPhase.Expired
        : !promotion.IsActive ? PromotionPhase.Paused
        : today < promotion.ValidFrom ? PromotionPhase.Planned
        : PromotionPhase.Running;

    /// <summary>
    /// Ob die Aktion auf diese Position wirken darf: offene Position ohne Rabatt (ein anderer Rabatt geht vor, Rabatte werden nicht gestapelt)
    /// und mit passender Art. Behandlungen: Termin-Positionen; Pakete: verkaufte Pakete (Einzweckgutschein mit Anzahl) der Behandlung.
    /// </summary>
    public static bool Applies(Promotion promotion, BillLine line)
    {
        if (line.PaidAmount != 0 || line.Discount != 0)
        {
            return false;
        }

        return promotion.Scope switch
        {
            PromotionScope.AllTreatments => line.Kind == BillLineKind.Appointment,
            PromotionScope.SelectedTreatments => line.Kind == BillLineKind.Appointment && Selected(promotion, line.TreatmentId),
            PromotionScope.SelectedPackages => line is { Kind: BillLineKind.Voucher, VoucherUnits: not null } && Selected(promotion, line.TreatmentId),
            _ => false,
        };
    }

    private static bool Selected(Promotion promotion, Guid? treatmentId) =>
        treatmentId is { } id && promotion.Treatments.Any(t => t.TreatmentId == id);

    /// <summary>Rabatt auf die Position (Betrag, kaufmännisch auf Cent gerundet); 0, wenn er nicht kleiner als der Positionsbetrag wäre.</summary>
    public static decimal DiscountOf(Promotion promotion, BillLine line)
    {
        var gross = line.UnitPrice * line.Quantity;
        var discount = promotion.DiscountPercent is { } percent
            ? decimal.Round(gross * percent / 100, 2, MidpointRounding.AwayFromZero)
            : promotion.DiscountAmount ?? 0;
        return discount > 0 && discount < gross ? discount : 0;
    }

    /// <summary>Positionen, auf die die Aktion wirkt, mit ihrem Rabatt.</summary>
    public static List<(BillLine Line, decimal Discount)> Matches(Promotion promotion, IEnumerable<BillLine> lines) =>
        [.. lines.Where(l => Applies(promotion, l)).Select(l => (Line: l, Discount: DiscountOf(promotion, l))).Where(m => m.Discount > 0)];

    /// <summary>
    /// Bezeichnung auf dem Beleg, z. B. „Aktion Herbst 10 %“. Wie bei jedem Rabatt nennt der Beleg den Prozentsatz, auch bei einem Betrag
    /// (ein Prozentwert der Position, auf eine Stelle gerundet); der Name steht zuerst, damit die Zeile (höchstens 60 Zeichen) nicht den Namen abschneidet.
    /// </summary>
    public static string Note(Promotion promotion, BillLine line, decimal discount)
    {
        var gross = line.UnitPrice * line.Quantity;
        var percent = promotion.DiscountPercent ?? decimal.Round(discount * 100 / gross, 1, MidpointRounding.AwayFromZero);
        return $"Aktion {promotion.Name} {PosService.PercentText(percent)} %";
    }

    /// <summary>Setzt den Rabatt der Aktion auf alle passenden Positionen; gibt die Positionen zurück (leer, wenn nichts passt).</summary>
    public static List<BillLine> Apply(Promotion promotion, IEnumerable<BillLine> lines)
    {
        var applied = new List<BillLine>();
        foreach (var (line, discount) in Matches(promotion, lines))
        {
            line.Discount = discount;
            line.DiscountNote = Note(promotion, line, discount);
            line.Amount = (line.UnitPrice * line.Quantity) - discount;
            applied.Add(line);
        }

        return applied;
    }

    /// <summary>Der Rabatt in Worten, z. B. „−10 %“ oder „−5,00 €“.</summary>
    public static string DiscountText(Promotion promotion, string currency) =>
        promotion.DiscountPercent is { } percent
            ? $"−{PosService.PercentText(percent)} %"
            : $"−{(promotion.DiscountAmount ?? 0).ToString("0.00", German)} {CurrencySymbol(currency)}";

    private static string CurrencySymbol(string currency) => currency.Equals("EUR", StringComparison.OrdinalIgnoreCase) ? "€" : currency;
}
