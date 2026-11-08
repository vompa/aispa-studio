using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.HrFile;

/// <summary>Löscht einmal am Tag Verwarnungen nach ihrem Ablaufdatum (E9), für alle Mandanten; im Verlauf bleibt nur, dass gelöscht wurde. Der erste Lauf kommt, sobald Migration und Beispieldaten fertig sind.</summary>
internal sealed partial class HrFileCleanup(IServiceScopeFactory scopes, TimeProvider time, StartupState startup, ILogger<HrFileCleanup> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await startup.WaitUntilReadyAsync(stoppingToken))
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            try
            {
                await RunOnceAsync(time.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Failed(log, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Gibt die Zahl der gelöschten Verwarnungen zurück.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        List<Guid> tenantIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            tenantIds = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync(ct);
        }

        var removed = 0;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                removed += await RemoveExpiredAsync(tenantId, now, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                FailedForTenant(log, tenantId, ex);
            }
        }

        return removed;
    }

    private async Task<int> RemoveExpiredAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var warnings = await db.Set<HrEntry>().Where(e => e.Kind == HrEntryKind.Warning && e.ExpiresOn != null).ToListAsync(ct);
        if (warnings.Count == 0)
        {
            return 0;
        }

        var zone = await HrFileRules.ZoneAsync(db, ct);
        var expired = warnings.Where(e => HrFileRules.IsExpired(e.ExpiresOn!.Value, now, zone)).ToList();
        db.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
        return expired.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Abgelaufene Verwarnungen konnten nicht gelöscht werden.")]
    private static partial void Failed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Abgelaufene Verwarnungen des Mandanten {TenantId} konnten nicht gelöscht werden.")]
    private static partial void FailedForTenant(ILogger logger, Guid tenantId, Exception ex);
}
