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

    public static readonly DayOfWeek[] WeekFromMonday =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public static readonly DayPart[] DayParts = [DayPart.Morning, DayPart.Afternoon, DayPart.Evening];

    /// <summary>Für Positionen ohne Termin oder Abläufe ohne Termine (Verlauf der Beispieldaten): befristete Aktionen brauchen keine.</summary>
    public static readonly IReadOnlyDictionary<Guid, AppointmentTiming> NoTimings = new Dictionary<Guid, AppointmentTiming>();

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
    /// Ob die Aktion auf diese Position wirken darf: offene Position ohne Rabatt (ein anderer Rabatt geht vor, Rabatte werden nicht gestapelt),
    /// mit passender Art und, bei kurzfristigen Terminen und schwachen Zeiten, mit passendem Termin (<paramref name="timings"/> nach Termin-Kennung).
    /// Behandlungen: Termin-Positionen; Pakete: verkaufte Pakete (Einzweckgutschein mit Anzahl) der Behandlung.
    /// </summary>
    public static bool Applies(Promotion promotion, BillLine line, IReadOnlyDictionary<Guid, AppointmentTiming> timings)
    {
        if (line.PaidAmount != 0 || line.Discount != 0)
        {
            return false;
        }

        var fitsLine = promotion.Scope switch
        {
            PromotionScope.AllTreatments => line.Kind == BillLineKind.Appointment,
            PromotionScope.SelectedTreatments => line.Kind == BillLineKind.Appointment && Selected(promotion, line.TreatmentId),
            PromotionScope.SelectedPackages => line is { Kind: BillLineKind.Voucher, VoucherUnits: not null } && Selected(promotion, line.TreatmentId),
            _ => false,
        };
        return fitsLine && FitsTiming(promotion, line.AppointmentId is { } id ? timings.GetValueOrDefault(id) : null);
    }

    /// <summary>
    /// Ob der Termin zur Art der Aktion passt. Befristet: immer. Kurzfristig frei: höchstens <see cref="Promotion.LeadHours"/> Stunden zwischen Buchung und Beginn.
    /// Schwache Zeiten: Wochentag und Tageszeit des Beginns am Standort liegen in einem gewählten Feld. Ohne Termin passen die beiden letzten nie.
    /// </summary>
    public static bool FitsTiming(Promotion promotion, AppointmentTiming? timing) => promotion.Kind switch
    {
        PromotionKind.Limited => true,
        PromotionKind.LastMinute => timing is { } t && promotion.LeadHours is { } hours && t.Start - t.BookedAt <= TimeSpan.FromHours(hours),
        PromotionKind.OffPeak => timing is { } t && (promotion.OffPeakSlots & SlotBit(t.LocalStart.DayOfWeek, PartOf(TimeOnly.FromDateTime(t.LocalStart)))) != 0,
        _ => false,
    };

    /// <summary>Tageszeit eines Beginns: vormittags bis 12 Uhr, nachmittags 12 bis 17 Uhr, abends ab 17 Uhr (wie die Warteliste).</summary>
    public static DayPart PartOf(TimeOnly time) => time.Hour < 12 ? DayPart.Morning : time.Hour < 17 ? DayPart.Afternoon : DayPart.Evening;

    /// <summary>Das Bit eines Felds im Wochenraster: je Wochentag (Sonntag = 0) drei Bits für Vormittag, Nachmittag und Abend.</summary>
    public static int SlotBit(DayOfWeek day, DayPart part) => 1 << (((int)day * 3) + (int)part - 1);

    /// <summary>Die Felder als Bits; <see cref="DayPart.Any"/> zählt nicht.</summary>
    public static int SlotsOf(IEnumerable<PromotionSlot> slots) =>
        slots.Where(s => s.Part != DayPart.Any).Aggregate(0, (bits, s) => bits | SlotBit(s.Day, s.Part));

    /// <summary>Die gesetzten Felder, Montag zuerst und je Tag vom Vormittag zum Abend.</summary>
    public static List<PromotionSlot> SlotList(int bits) =>
        [.. WeekFromMonday.SelectMany(day => DayParts.Select(part => new PromotionSlot(day, part))).Where(s => (bits & SlotBit(s.Day, s.Part)) != 0)];

    private static bool Selected(Promotion promotion, Guid? treatmentId) =>
        treatmentId is { } id && promotion.Treatments.Any(t => t.TreatmentId == id);

    /// <summary>Rabatt auf die Position (Betrag, kaufmännisch auf Cent gerundet); 0, wenn er nicht kleiner als der Positionsbetrag wäre.</summary>
    public static decimal DiscountOf(Promotion promotion, BillLine line) => DiscountOf(promotion, line.UnitPrice * line.Quantity);

    /// <summary>Rabatt auf einen Betrag (auch für die Vorschau des Gästepreises); 0, wenn er nicht kleiner als der Betrag wäre.</summary>
    public static decimal DiscountOf(Promotion promotion, decimal gross)
    {
        var discount = promotion.DiscountPercent is { } percent
            ? decimal.Round(gross * percent / 100, 2, MidpointRounding.AwayFromZero)
            : promotion.DiscountAmount ?? 0;
        return discount > 0 && discount < gross ? discount : 0;
    }

    /// <summary>Positionen, auf die die Aktion wirkt, mit ihrem Rabatt.</summary>
    public static List<(BillLine Line, decimal Discount)> Matches(Promotion promotion, IEnumerable<BillLine> lines, IReadOnlyDictionary<Guid, AppointmentTiming> timings) =>
        [.. lines.Where(l => Applies(promotion, l, timings)).Select(l => (Line: l, Discount: DiscountOf(promotion, l))).Where(m => m.Discount > 0)];

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
    public static List<BillLine> Apply(Promotion promotion, IEnumerable<BillLine> lines, IReadOnlyDictionary<Guid, AppointmentTiming> timings)
    {
        var applied = new List<BillLine>();
        foreach (var (line, discount) in Matches(promotion, lines, timings))
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

/// <summary>Termin einer Position: Beginn (auch in der Ortszeit des Standorts) und wann er gebucht wurde.</summary>
internal readonly record struct AppointmentTiming(DateTimeOffset Start, DateTime LocalStart, DateTimeOffset BookedAt);
