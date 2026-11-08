using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>Preise nach Wochentag und Tageszeit je Behandlung.</summary>
internal static class PriceRuleEndpoints
{
    private const int MaxRules = 20;

    public static IEndpointRouteBuilder MapPriceRuleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/treatments/{treatmentId:guid}/price-rules").WithTags("Stammdaten");

        group.MapGet("", async Task<Results<Ok<List<TreatmentPriceRuleDto>>, NotFound>> (Guid treatmentId, AispaDbContext db, CancellationToken ct) =>
                await db.Treatments.AnyAsync(t => t.Id == treatmentId, ct)
                    ? TypedResults.Ok(await db.TreatmentPriceRules.AsNoTracking().Where(r => r.TreatmentId == treatmentId)
                        .OrderBy(r => r.Position).Select(r => ToDto(r)).ToListAsync(ct))
                    : TypedResults.NotFound())
            .WithName("ListTreatmentPriceRules")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("", async Task<Results<Ok<List<TreatmentPriceRuleDto>>, NotFound, ValidationProblem>> (
                Guid treatmentId, List<TreatmentPriceRuleDto> input, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                if (!await db.Treatments.AnyAsync(t => t.Id == treatmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var access = (await accessService.CurrentAsync(ct))!;
                var locations = await db.Locations.Select(l => l.Id).ToListAsync(ct);
                var errors = new Errors();
                errors.Check(input.Count <= MaxRules, "rules", $"Höchstens {MaxRules} Regeln je Behandlung.");
                foreach (var (rule, i) in input.Select((r, i) => (r, i + 1)))
                {
                    errors.Check(!string.IsNullOrWhiteSpace(rule.Name) && rule.Name.Trim().Length <= 100, "rules", $"Regel {i}: Bezeichnung angeben, z. B. „Abendpreis“.");
                    errors.Check(rule.Days is { Count: > 0 }, "rules", $"Regel {i}: mindestens einen Wochentag wählen.");
                    errors.Check(rule.From < rule.Until, "rules", $"Regel {i}: Beginn vor Ende.");
                    errors.Check(rule.Price >= 0 && rule.Price < 100_000 && decimal.Round(rule.Price, 2) == rule.Price, "rules",
                        $"Regel {i}: Preis ab 0 mit höchstens zwei Nachkommastellen.");
                    errors.Check(rule.LocationId is null || locations.Contains(rule.LocationId.Value) && access.CanSee(rule.LocationId.Value), "rules",
                        $"Regel {i}: Standort nicht gefunden oder nicht freigegeben.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Regeln an Standorten, die die Person nicht sieht, bleiben unverändert.
                var existing = await db.TreatmentPriceRules.Where(r => r.TreatmentId == treatmentId).ToListAsync(ct);
                var kept = existing.Where(r => r.LocationId is { } l && !access.CanSee(l)).ToList();
                db.TreatmentPriceRules.RemoveRange(existing.Except(kept));
                db.TreatmentPriceRules.AddRange(input.Select((r, i) => new TreatmentPriceRule
                {
                    TreatmentId = treatmentId,
                    LocationId = r.LocationId,
                    Position = i,
                    Name = r.Name.Trim(),
                    Days = r.Days.Aggregate(0, (mask, day) => mask | (1 << (int)day)),
                    From = r.From,
                    Until = r.Until,
                    Price = r.Price,
                }));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await db.TreatmentPriceRules.AsNoTracking().Where(r => r.TreatmentId == treatmentId)
                    .OrderBy(r => r.Position).Select(r => ToDto(r)).ToListAsync(ct));
            })
            .WithName("SetTreatmentPriceRules")
            .WithSummary("Ersetzt die Preise nach Wochentag und Tageszeit einer Behandlung (Reihenfolge = Vorrang). Gilt für neue Buchungen.")
            .RequirePermission(AccessRight.ManageCatalog);

        return routes;
    }

    private static TreatmentPriceRuleDto ToDto(TreatmentPriceRule r) =>
        new(r.Name, [.. Enum.GetValues<DayOfWeek>().Where(d => (r.Days & (1 << (int)d)) != 0)], r.From, r.Until, r.Price, r.LocationId);
}
