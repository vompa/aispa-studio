using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.Runtime;

namespace AispaStudio.Api.Online;

/// <summary>Queue: die Erstattung zu einem stornierten Beleg nachholen. Eigene Nachricht, damit ein Kartenterminal (ebenfalls auf <see cref="ReceiptVoided"/>) nicht ein zweites Mal gutschreibt.</summary>
public sealed record ResumeOnlineRefund(ReceiptVoided Voided);

public static class ResumeOnlineRefundHandler
{
    public static Task Handle(ResumeOnlineRefund message, IServiceScopeFactory scopes, CancellationToken ct) =>
        OnlineReceiptVoidedHandler.Handle(message.Voided, scopes, ct);
}

/// <summary>
/// Status und Nachricht werden in zwei Schritten gespeichert. Stürzt die API dazwischen ab oder landet die Nachricht in der Fehlerablage, wäre das
/// Geld da, aber ohne Beleg, Gutschein oder Erstattung; eine zweite Meldung des Anbieters ändert daran nichts mehr. Dieser Lauf stellt solche Fälle
/// erneut in die Queue (<c>OnlineBooking:PaymentRecoveryPollSeconds</c>). Die Handler sind wiederholbar, bei der Erstattung schützt der Idempotenzschlüssel.
/// </summary>
internal sealed partial class PaymentRecovery(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<OnlineBookingOptions> options,
    StartupState startup,
    ILogger<PaymentRecovery> log) : BackgroundService
{
    /// <summary>So lange gehört ein Fall noch der ursprünglichen Nachricht.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(15);

    /// <summary>Danach kein neuer Versuch mehr, sonst füllt ein dauerhaft scheiternder Fall die Fehlerablage; der Betreiber sieht ihn weiter.</summary>
    private static readonly TimeSpan RetryWindow = TimeSpan.FromDays(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PaymentRecoveryPollSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(60, options.Value.PaymentRecoveryPollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken) && await startup.WaitUntilReadyAsync(stoppingToken))
        {
            try
            {
                var now = time.GetUtcNow();
                var recovered = await RunOnceAsync(now, null, stoppingToken);
                if (recovered > 0)
                {
                    Recovered(log, recovered);
                }

                var health = await HealthAsync(now, stoppingToken);
                if (health.DeadLetters > 0)
                {
                    DeadLetters(log, health.DeadLetters);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Failed(log, ex);
            }
        }
    }

    /// <summary>Stellt jeden hängenden Fall in die Queue und gibt deren Zahl zurück; mit <paramref name="locationId"/> nur diesen Standort.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, Guid? locationId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var online = scope.ServiceProvider.GetRequiredService<OnlineDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var until = now - Grace;
        var from = now - RetryWindow;

        // Mit Fehler hat der Handler das Problem gemeldet, das Team sieht es an der Anzahlung bzw. Bestellung; nur stille Fälle hängen.
        var deposits = await online.Deposits.AsNoTracking()
            .Where(d => d.Status == OnlineDepositStatus.Paid && d.Error == null && d.PaidAt <= until && d.PaidAt >= from
                && (locationId == null || d.LocationId == locationId))
            .Select(d => d.Id).ToListAsync(ct);
        var vouchers = await online.VoucherOrders.AsNoTracking()
            .Where(o => o.Status == VoucherOrderStatus.Paid && o.Error == null && o.PaidAt <= until && o.PaidAt >= from
                && (locationId == null || o.LocationId == locationId))
            .Select(o => o.Id).ToListAsync(ct);
        var refunds = await online.Refunds.AsNoTracking()
            .Where(r => r.Status == OnlineRefundStatus.Pending && r.RequestedAt <= until && r.RequestedAt >= from
                && (locationId == null || r.LocationId == locationId))
            .Select(r => new RefundOnlinePayment(r.Id, r.Attempts)).ToListAsync(ct);
        var voids = await VoidsWithoutRefundAsync(scope.ServiceProvider.GetRequiredService<AispaDbContext>(), online, from, until, locationId, ct);

        foreach (var id in deposits)
        {
            await bus.PublishAsync(new ReceiptOnlineDeposit(id));
        }

        foreach (var id in vouchers)
        {
            await bus.PublishAsync(new IssueOnlineVoucher(id));
        }

        foreach (var refund in refunds)
        {
            await bus.PublishAsync(refund);
        }

        foreach (var voided in voids)
        {
            await bus.PublishAsync(new ResumeOnlineRefund(voided));
        }

        return deposits.Count + vouchers.Count + refunds.Count + voids.Count;
    }

    /// <summary>Für den Betreiber, über alle Spas und ohne Altersgrenze nach oben: was das Wiederholfenster überschritten hat, hängt hier weiter sichtbar.</summary>
    public async Task<OperatorQueueHealth> HealthAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var online = scope.ServiceProvider.GetRequiredService<OnlineDbContext>();
        var until = now - Grace;
        var counts = await scope.ServiceProvider.GetRequiredService<IWolverineRuntime>().Storage.Admin.FetchCountsAsync();
        return new OperatorQueueHealth(
            counts.DeadLetter,
            await online.Deposits.CountAsync(d => d.Status == OnlineDepositStatus.Paid && d.PaidAt <= until, ct),
            await online.VoucherOrders.CountAsync(o => o.Status == VoucherOrderStatus.Paid && o.PaidAt <= until, ct),
            await online.Refunds.CountAsync(r => r.Status == OnlineRefundStatus.Pending && r.RequestedAt <= until, ct));
    }

    /// <summary>Stornobelege zu online bezahlten Belegen, zu denen es noch keine Erstattung gibt: das Ereignis <see cref="ReceiptVoided"/> ging verloren.</summary>
    private static async Task<List<ReceiptVoided>> VoidsWithoutRefundAsync(
        AispaDbContext db, OnlineDbContext online, DateTimeOffset from, DateTimeOffset until, Guid? locationId, CancellationToken ct)
    {
        var voids = await db.Receipts.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.VoidsReceiptId != null && r.IssuedAt <= until && r.IssuedAt >= from)
            .Select(r => new { r.Id, ReceiptId = r.VoidsReceiptId!.Value, r.VoidReason, r.CreatedByUserId })
            .ToListAsync(ct);
        if (voids.Count == 0)
        {
            return [];
        }

        var receiptIds = voids.ConvertAll(v => v.ReceiptId);
        var paidOnline = await online.VoucherOrders.AsNoTracking()
            .Where(o => o.ReceiptId != null && receiptIds.Contains(o.ReceiptId.Value) && (locationId == null || o.LocationId == locationId))
            .Select(o => o.ReceiptId!.Value)
            .Concat(online.CardGuarantees.AsNoTracking()
                .Where(g => g.ReceiptId != null && receiptIds.Contains(g.ReceiptId.Value) && (locationId == null || g.LocationId == locationId))
                .Select(g => g.ReceiptId!.Value))
            .Concat(online.Deposits.AsNoTracking()
                .Where(d => d.ReceiptId != null && receiptIds.Contains(d.ReceiptId.Value) && (locationId == null || d.LocationId == locationId))
                .Select(d => d.ReceiptId!.Value))
            .ToListAsync(ct);
        var refunded = await online.Refunds.AsNoTracking().Where(r => receiptIds.Contains(r.ReceiptId)).Select(r => r.ReceiptId).ToListAsync(ct);
        return [.. voids
            .Where(v => paidOnline.Contains(v.ReceiptId) && !refunded.Contains(v.ReceiptId))
            .Select(v => new ReceiptVoided(v.ReceiptId, v.Id, v.VoidReason ?? "", v.CreatedByUserId))];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} Online-Zahlungen hingen und wurden erneut in die Queue gestellt.")]
    private static partial void Recovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Count} Nachrichten liegen in der Fehlerablage der Queue (Betreiber: /api/operator/queue-health).")]
    private static partial void DeadLetters(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hängende Online-Zahlungen konnten nicht nachgeholt werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}
