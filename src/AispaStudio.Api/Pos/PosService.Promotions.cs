using AispaStudio.Api.Promotions;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos;

// ---------- Aktionen an der Kassa (ADR 0071) ----------
internal sealed partial class PosService
{
    /// <summary>
    /// Vorschläge für eine offene Rechnung: laufende Aktionen am Standort der Kasse, die zu Positionen passen, mit dem Rabatt, den die Kassa dafür
    /// rechnen würde. Null, wenn es die Rechnung nicht gibt; eine bezahlte Rechnung bekommt keine Vorschläge. Es wird nichts verändert.
    /// </summary>
    public async Task<List<PromotionOfferDto>?> PromotionOffersAsync(Guid billId, CancellationToken ct)
    {
        var bill = await db.Bills.AsNoTracking().Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == billId, ct);
        if (bill is null)
        {
            return null;
        }

        if (bill.Status != BillStatus.Open)
        {
            return [];
        }

        var (locationId, today, currency) = await PromotionDayAsync(bill.RegisterId, ct);
        return OffersFor(await PromotionService.RunningAsync(db, locationId, today, ct), bill.Lines, currency);
    }

    /// <summary>Die Vorschläge zu einer Liste laufender Aktionen: nur Aktionen mit mindestens einer passenden Position, größter Rabatt zuerst.</summary>
    internal static List<PromotionOfferDto> OffersFor(IEnumerable<Promotion> promotions, IEnumerable<BillLine> lines, string currency)
    {
        var ordered = lines.OrderBy(l => l.Position).ToList();
        return [.. promotions.Select(p => (Promotion: p, Matches: PromotionRules.Matches(p, ordered)))
            .Where(x => x.Matches.Count > 0)
            .Select(x => new PromotionOfferDto(x.Promotion.Id, x.Promotion.Name, PromotionRules.DiscountText(x.Promotion, currency),
                [.. x.Matches.Select(m => new PromotionOfferLineDto(m.Line.Id, m.Line.Description, m.Discount))], x.Matches.Sum(m => m.Discount)))
            .OrderByDescending(o => o.TotalDiscount).ThenBy(o => o.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Wendet eine laufende Aktion auf die passenden Positionen einer offenen Rechnung an: die Kassa setzt den Rabatt der Position, der Beleg nennt
    /// die Aktion („Aktion Herbst 10 %“). Wirkt nur auf Klick, nur auf offene Positionen ohne anderen Rabatt, und braucht keine Freigabe über der
    /// Rabattgrenze, weil Inhaberin oder Leitung die Aktion festgelegt haben. Umsatz, Steuer und Signatur laufen unverändert über den Beleg.
    /// </summary>
    public async Task<PosResult<BillDto>> ApplyPromotionAsync(Guid billId, Guid promotionId, CancellationToken ct) =>
        await BillTransactionAsync(await RegisterOfBillAsync(billId, ct), async register =>
        {
            var bill = await LoadOpenBillAsync(register, billId, ct);
            var promotion = await db.Promotions.AsNoTracking().Include(p => p.Locations).Include(p => p.Treatments).AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == promotionId, ct) ?? throw PosFailure.NotFound("Aktion");
            var (locationId, today, _) = await PromotionDayAsync(register.Id, ct);
            ApplyPromotion(bill, promotion, locationId, today);
            return bill.Id;
        }, ct);

    /// <summary>Prüft Lauf und Passung und setzt den Rabatt auf die Positionen (ohne Datenbank; der Verlauf der Beispieldaten im Speicher nutzt dasselbe).</summary>
    internal static void ApplyPromotion(Bill bill, Promotion promotion, Guid locationId, DateOnly today)
    {
        if (!PromotionRules.IsRunning(promotion, locationId, today))
        {
            throw PosFailure.Invalid("promotionId", "Die Aktion läuft heute an diesem Standort nicht.");
        }

        if (PromotionRules.Apply(promotion, bill.Lines).Count == 0)
        {
            throw PosFailure.Invalid("promotionId", "Die Aktion passt zu keiner offenen Position dieser Rechnung (Positionen mit anderem Rabatt bleiben, wie sie sind).");
        }
    }

    /// <summary>Standort der Kasse, dessen heutiger Kalendertag und Währung.</summary>
    private async Task<(Guid LocationId, DateOnly Today, string Currency)> PromotionDayAsync(Guid registerId, CancellationToken ct)
    {
        var location = await db.CashRegisters.AsNoTracking().Where(r => r.Id == registerId)
            .Join(db.Locations.AsNoTracking(), r => r.LocationId, l => l.Id, (_, l) => new { l.Id, l.TimeZone, l.Currency }).FirstAsync(ct);
        return (location.Id, LocalTime.LocalDate(time.GetUtcNow(), LocalTime.Zone(location.TimeZone)), location.Currency);
    }
}
