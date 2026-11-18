using AispaStudio.Api.Data;
using AispaStudio.Api.Promotions;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Aktionen für das Beispiel-Spa (ADR 0071): „Herbstzauber“ läuft gerade an allen Standorten, „Sommerfrische“ ist seit drei Tagen vorbei und
/// galt nur in Wien. Dazu je eine der beiden anderen Arten (Nachtrag zu ADR 0071): „Kurzfristig frei“ für Termine, die höchstens 24 Stunden vorher gebucht werden,
/// und „Ruhige Vormittage“ von Montag bis Mittwoch; der Verlauf wendet nur befristete Aktionen an. Sie liegen vor dem Verlauf (<see cref="DemoHistory"/>), damit die Kassa sie an den Tagen ihrer Laufzeit anwendet und die Belege den
/// Aktionsnamen tragen. Die Tage rechnen sich vom Einspielzeitpunkt aus; was später kommt, ist in der Verwaltung als abgelaufen archivierbar.
/// </summary>
internal static class DemoPromotions
{
    public const string RunningName = "Herbstzauber";
    public const string ExpiredName = "Sommerfrische";
    public const string LastMinuteName = "Kurzfristig frei";
    public const string OffPeakName = "Ruhige Vormittage";

    public static async Task AddAsync(AispaDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct);
        var treatments = await db.Treatments.AsNoTracking().ToDictionaryAsync(t => t.Name, t => t.Id, ct);
        var today = LocalTime.LocalDate(now, LocalTime.Zone(locations[0].TimeZone));

        db.Promotions.Add(New(RunningName, "10 % auf Klassische Massage, Hot-Stone-, Thai- und Express-Rückenmassage – an allen Standorten.",
            today.AddDays(-10), today.AddDays(20), now.AddDays(-12), percent: 10m, amount: null, onlyAt: null,
            names: ["Klassische Massage", "Lava-Hot-Stone-Massage (60 Min.)", "Traditionelle Thai-Massage (60 Min.)", "Rückenmassage Express"]));
        db.Promotions.Add(New(ExpiredName, "5 € Sommerrabatt auf die Gesichtsbehandlung Klassik in Wien.",
            today.AddDays(-40), today.AddDays(-3), now.AddDays(-45), percent: null, amount: 5m,
            onlyAt: [locations.First(l => l.Name.StartsWith("Wien", StringComparison.Ordinal)).Id], names: ["Gesichtsbehandlung Klassik"]));
        var lastMinute = New(LastMinuteName, "15 % auf Termine, die Sie höchstens 24 Stunden vorher buchen.",
            today.AddDays(-10), today.AddDays(170), now.AddDays(-12), percent: 15m, amount: null, onlyAt: null, names: []);
        lastMinute.Scope = PromotionScope.AllTreatments;
        lastMinute.Kind = PromotionKind.LastMinute;
        lastMinute.LeadHours = 24;
        db.Promotions.Add(lastMinute);
        var offPeak = New(OffPeakName, "10 % auf Termine am Vormittag, Montag bis Mittwoch.",
            today.AddDays(-10), today.AddDays(170), now.AddDays(-12), percent: 10m, amount: null, onlyAt: null, names: []);
        offPeak.Scope = PromotionScope.AllTreatments;
        offPeak.Kind = PromotionKind.OffPeak;
        offPeak.OffPeakSlots = PromotionRules.SlotsOf([new(DayOfWeek.Monday, DayPart.Morning), new(DayOfWeek.Tuesday, DayPart.Morning), new(DayOfWeek.Wednesday, DayPart.Morning)]);
        db.Promotions.Add(offPeak);
        await db.SaveChangesAsync(ct);

        Promotion New(string promotionName, string guestText, DateOnly from, DateOnly until, DateTimeOffset created, decimal? percent, decimal? amount,
            Guid[]? onlyAt, string[] names)
        {
            var promotion = new Promotion
            {
                Name = promotionName,
                GuestText = guestText,
                ValidFrom = from,
                ValidUntil = until,
                AllLocations = onlyAt is null,
                DiscountPercent = percent,
                DiscountAmount = amount,
                Scope = PromotionScope.SelectedTreatments,
                CreatedAt = created,
            };
            foreach (var locationId in onlyAt ?? [])
            {
                promotion.Locations.Add(new PromotionLocation { PromotionId = promotion.Id, LocationId = locationId });
            }

            foreach (var name in names)
            {
                promotion.Treatments.Add(new PromotionTreatment { PromotionId = promotion.Id, TreatmentId = treatments[name] });
            }

            return promotion;
        }
    }
}
