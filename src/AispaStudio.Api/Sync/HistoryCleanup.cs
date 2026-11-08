using AispaStudio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Löscht einmal am Tag Verlaufseinträge, die älter als <see cref="ChangeHistory.RetentionYears"/> Jahre sind (ADR 0082), für alle Mandanten.
/// Der erste Lauf kommt, sobald Migration und Beispieldaten fertig sind.
/// </summary>
internal sealed partial class HistoryCleanup(IServiceScopeFactory scopes, TimeProvider time, StartupState startup, ILogger<HistoryCleanup> log) : BackgroundService
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

    /// <summary>Gibt die Zahl der gelöschten Einträge zurück.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var cutoff = now.AddYears(-ChangeHistory.RetentionYears);
        return await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Database
            .ExecuteSqlAsync($"DELETE FROM sync.history WHERE changed_at < {cutoff}", ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Alte Einträge des Änderungsverlaufs konnten nicht gelöscht werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}
