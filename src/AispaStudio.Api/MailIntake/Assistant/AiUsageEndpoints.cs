using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>Verbrauch von Claude des Spas (T-112a): Summen je Aufgabe und Modell, nur Tokens, keine Geldbeträge.</summary>
internal static class AiUsageEndpoints
{
    private const int MaxDays = 366;

    public static void MapAiUsageEndpoints(this RouteGroupBuilder group) =>
        group.MapGet("/usage", async Task<Results<Ok<AiUsageReport>, ValidationProblem>> (AispaDbContext db, TimeProvider time, CancellationToken ct, int days = 30) =>
            {
                var errors = new Errors();
                errors.Check(days is >= 1 and <= MaxDays, nameof(days), $"Der Zeitraum muss zwischen 1 und {MaxDays} Tagen liegen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var from = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(1 - days);
                var rows = await db.Set<AiUsageDay>().AsNoTracking()
                    .Where(u => u.Day >= from)
                    .GroupBy(u => new { u.Task, u.Model })
                    .Select(g => new AiUsageRow(g.Key.Task, g.Key.Model, g.Sum(u => u.Calls), g.Sum(u => u.InputTokens), g.Sum(u => u.OutputTokens),
                        g.Sum(u => u.CacheReadTokens), g.Sum(u => u.CacheWriteTokens)))
                    .ToListAsync(ct);
                return TypedResults.Ok(new AiUsageReport(days, [.. rows.OrderBy(r => r.Task).ThenBy(r => r.Model, StringComparer.Ordinal)]));
            })
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("GetAiUsage")
            .WithSummary("Verbrauch von Claude des Spas über die letzten Tage (Standard 30, heute eingeschlossen, Tage in UTC): Aufrufe und Tokens je Aufgabe und Modell, ohne Geldbeträge.");
}
