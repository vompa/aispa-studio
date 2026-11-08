using System.Collections.Concurrent;
using AispaStudio.Api.Data;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Tenancy;

/// <summary>
/// Sperre eines Spas durch den Betreiber (ADR 0039). Gesperrt heißt: das Team sieht und exportiert weiter alles (Kassa,
/// Buchhaltung, Berichte; Aufbewahrungspflicht), kann aber nichts ändern; Gäste buchen online nichts Neues, verwalten aber
/// ihre Termine. Der Stand wird je Spa kurz zwischengespeichert, damit nicht jede Anfrage die Datenbank fragt; ebenso die
/// Abo-Stufe, die die Webmodule freischaltet (ADR 0015, <see cref="Plans"/>).
/// </summary>
internal sealed class TenantLocks(IServiceScopeFactory scopes, TimeProvider time)
{
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, (bool Locked, string? Reason, SubscriptionPlan Plan, DateTimeOffset Until)> _cache = new();

    public async ValueTask<(bool Locked, string? Reason)> GetAsync(Guid tenantId, CancellationToken ct)
    {
        var state = await LoadAsync(tenantId, ct);
        return (state.Locked, state.Reason);
    }

    /// <summary>Abo-Stufe des Spas; für ein unbekanntes Spa die kleinste.</summary>
    public async ValueTask<SubscriptionPlan> PlanAsync(Guid tenantId, CancellationToken ct) => (await LoadAsync(tenantId, ct)).Plan;

    private async ValueTask<(bool Locked, string? Reason, SubscriptionPlan Plan, DateTimeOffset Until)> LoadAsync(Guid tenantId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (_cache.TryGetValue(tenantId, out var hit) && hit.Until > now)
        {
            return hit;
        }

        await using var scope = scopes.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId).Select(t => new { t.LockedAt, t.LockReason, t.Plan }).FirstOrDefaultAsync(ct);
        var state = (row?.LockedAt is not null, row?.LockReason, row?.Plan ?? SubscriptionPlan.Start, now + Fresh);
        _cache[tenantId] = state;
        return state;
    }

    /// <summary>
    /// Nach einer Änderung sofort neu lesen. Laufen mehrere API-Instanzen, gelten Sperre und Abo-Stufe dort spätestens nach 30 Sekunden.
    /// </summary>
    public void Forget(Guid tenantId) => _cache.TryRemove(tenantId, out _);

    public static void MapOperator(RouteGroupBuilder group)
    {
        group.MapPost("/spas/{tenantId:guid}/lock", async Task<Results<NoContent, NotFound, ValidationProblem>> (
            Guid tenantId, OperatorLockInput input, AispaDbContext db, TenantLocks locks, TimeProvider time, CancellationToken ct) =>
        {
            var reason = input.Reason?.Trim() ?? "";
            if (reason.Length is 0 or > 500)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(input.Reason)] = ["Grund angeben, höchstens 500 Zeichen; das Team des Spas sieht ihn."],
                });
            }

            var now = time.GetUtcNow();
            var changed = await db.Tenants.Where(t => t.Id == tenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.LockedAt, t => t.LockedAt ?? now).SetProperty(t => t.LockReason, reason), ct);
            locks.Forget(tenantId);
            return changed > 0 ? TypedResults.NoContent() : TypedResults.NotFound();
        })
            .WithName("LockOperatorSpa")
            .WithSummary("Betreiber: Spa sperren. Das Team sieht und exportiert weiter alles, kann aber nichts ändern; online keine neuen Buchungen.");

        group.MapDelete("/spas/{tenantId:guid}/lock", async Task<Results<NoContent, NotFound>> (
            Guid tenantId, AispaDbContext db, TenantLocks locks, CancellationToken ct) =>
        {
            var changed = await db.Tenants.Where(t => t.Id == tenantId)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.LockedAt, (DateTimeOffset?)null).SetProperty(t => t.LockReason, (string?)null), ct);
            locks.Forget(tenantId);
            return changed > 0 ? TypedResults.NoContent() : TypedResults.NotFound();
        })
            .WithName("UnlockOperatorSpa")
            .WithSummary("Betreiber: Sperre aufheben.");

        group.MapPut("/spas/{tenantId:guid}/plan", async Task<Results<NoContent, NotFound, ValidationProblem>> (
            Guid tenantId, OperatorPlanInput input, AispaDbContext db, TenantLocks locks, CancellationToken ct) =>
        {
            if (!Enum.IsDefined(input.Plan))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(input.Plan)] = ["Start, Spa oder SpaPlus."] });
            }

            var changed = await db.Tenants.Where(t => t.Id == tenantId).ExecuteUpdateAsync(u => u.SetProperty(t => t.Plan, input.Plan), ct);
            locks.Forget(tenantId);
            return changed > 0 ? TypedResults.NoContent() : TypedResults.NotFound();
        })
            .WithName("SetOperatorSpaPlan")
            .WithSummary("Betreiber: Abo-Stufe eines Spas setzen (Start, Spa, SpaPlus). Schaltet die Webmodule frei oder ab; Daten bleiben.");
    }
}

/// <summary>Middleware zur Sperre; nach Anmeldung und Kundenbereich, damit der Mandant feststeht.</summary>
internal static class TenantLockMiddleware
{
    /// <summary>
    /// Weist ändernde Anfragen eines gesperrten Spas mit 403 ab. Lesen (GET), Anmelden, Webhooks und der Betreiber bleiben frei;
    /// im Kundenbereich nur neue Buchungen, Gutscheinbestellungen und Wartelisten-Einträge gesperrt.
    /// </summary>
    public static IApplicationBuilder UseTenantLock(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (Guarded(context.Request) && context.RequestServices.GetRequiredService<TenantContext>().TenantId is { } tenantId)
            {
                var (locked, reason) = await context.RequestServices.GetRequiredService<TenantLocks>().GetAsync(tenantId, context.RequestAborted);
                if (locked)
                {
                    var online = context.Request.Path.StartsWithSegments("/api/online");
                    await Results.Problem(
                        online
                            ? "Dieses Spa nimmt gerade keine Online-Buchungen an. Bestehende Termine lassen sich weiter verwalten."
                            : $"Dieses Spa ist gesperrt{(string.IsNullOrWhiteSpace(reason) ? "" : $": {reason}")}. Ansehen und Exportieren geht weiter, Ändern nicht.",
                        statusCode: StatusCodes.Status403Forbidden, title: "Spa gesperrt").ExecuteAsync(context);
                    return;
                }
            }

            await next(context);
        });

    private static bool Guarded(HttpRequest request)
    {
        var path = request.Path;
        if (!path.StartsWithSegments("/api") || HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || path.StartsWithSegments("/api/auth") || path.StartsWithSegments("/api/operator") || path.StartsWithSegments("/api/hooks"))
        {
            return false;
        }

        return !path.StartsWithSegments("/api/online") || NewOnlineBusiness.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly PathString[] NewOnlineBusiness = ["/api/online/bookings", "/api/online/vouchers/orders", "/api/online/waitlist"];
}
