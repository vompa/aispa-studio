using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>Nachricht in der Queue: diese Erstattung beim Zahlungsanbieter auslösen.</summary>
public sealed record RefundOnlinePayment(Guid RefundId, int Attempt);

/// <summary>
/// Rückerstattung online bezahlter Belege (Online-Gutschein, Ausfallgebühr, Anzahlung). Erstattet wird nur nach dem Storno des Belegs an der
/// Kasse: so gibt es zu jeder Erstattung einen Stornobeleg, und ein schon eingelöster Gutschein lässt sich gar nicht erst
/// stornieren (<see cref="PosService.VoidAsync"/>). Das Geld gibt der Zahlungsanbieter im Hintergrund zurück.
/// </summary>
internal sealed class OnlineRefunds(AispaDbContext db, OnlineDbContext online, IMessageBus bus, TimeProvider time)
{
    /// <summary>Online-Zahlung hinter einem Beleg, mit der Rechnung, über die er kassiert wurde.</summary>
    public sealed record Payment(
        OnlinePaymentSource Source, Guid SourceId, Guid LocationId, Guid CustomerId, Guid? BillId, string Provider, string Reference, decimal Amount,
        string Currency, string Email, string Language);

    public abstract record RequestOutcome
    {
        public sealed record Started(OnlineRefundDto Refund) : RequestOutcome;

        public sealed record NotFound : RequestOutcome;

        public sealed record Refused(string Message) : RequestOutcome;
    }

    /// <summary>Sucht ohne Mandantenfilter (Schema „online“); wer das Ergebnis zeigt, prüft vorher den Beleg im eigenen Spa.</summary>
    public async Task<Payment?> PaymentOfAsync(Guid receiptId, CancellationToken ct)
    {
        var order = await online.VoucherOrders.AsNoTracking().FirstOrDefaultAsync(o => o.ReceiptId == receiptId, ct);
        if (order is { ProviderReference: { } orderReference })
        {
            return new Payment(OnlinePaymentSource.Voucher, order.Id, order.LocationId, order.CustomerId, order.BillId, order.Provider,
                orderReference, order.Amount, order.Currency, order.BuyerEmail, order.Language);
        }

        var guarantee = await online.CardGuarantees.AsNoTracking().FirstOrDefaultAsync(g => g.ReceiptId == receiptId, ct);
        if (guarantee is { ChargeReference: { } chargeReference })
        {
            return new Payment(OnlinePaymentSource.NoShowFee, guarantee.Id, guarantee.LocationId, guarantee.CustomerId, guarantee.BillId,
                guarantee.Provider, chargeReference, guarantee.FeeAmount, guarantee.Currency, guarantee.Email, guarantee.Language);
        }

        var deposit = await online.Deposits.AsNoTracking().FirstOrDefaultAsync(d => d.ReceiptId == receiptId, ct);
        return deposit is { ProviderReference: { } depositReference }
            ? new Payment(OnlinePaymentSource.Deposit, deposit.Id, deposit.LocationId, deposit.CustomerId, deposit.BillId,
                deposit.Provider, depositReference, deposit.Amount, deposit.Currency, deposit.Email, deposit.Language)
            : null;
    }

    /// <summary>Stand für das Team; <c>Found</c> ist falsch, wenn es den Beleg im eigenen Spa nicht gibt.</summary>
    public async Task<(bool Found, OnlineRefundDto? Refund)> StatusAsync(Guid receiptId, CancellationToken ct)
    {
        if (!await db.Receipts.AnyAsync(r => r.Id == receiptId, ct))
        {
            return (false, null);
        }

        if (await PaymentOfAsync(receiptId, ct) is not { } payment)
        {
            return (true, null);
        }

        var voided = await db.Receipts.AnyAsync(r => r.VoidsReceiptId == receiptId, ct);
        var refund = await online.Refunds.AsNoTracking().FirstOrDefaultAsync(r => r.ReceiptId == receiptId, ct);
        return (true, ToDto(receiptId, payment, voided, refund));
    }

    /// <summary>
    /// Beginnt die Erstattung für einen stornierten Beleg, oder versucht sie nach einer Ablehnung erneut. Der Beleg muss im eigenen
    /// Spa liegen und storniert sein; das prüft, wer aufruft (Storno-Ereignis oder <see cref="RetryAsync"/>).
    /// </summary>
    public async Task<RequestOutcome> RequestAsync(Guid receiptId, Guid voidReceiptId, string? reason, string? userId, CancellationToken ct)
    {
        if (await PaymentOfAsync(receiptId, ct) is not { } payment)
        {
            return new RequestOutcome.NotFound();
        }

        var refund = await online.Refunds.FirstOrDefaultAsync(r => r.ReceiptId == receiptId, ct);
        switch (refund?.Status)
        {
            case OnlineRefundStatus.Pending:
                return new RequestOutcome.Refused("Die Erstattung läuft schon.");
            case OnlineRefundStatus.Refunded:
                return new RequestOutcome.Refused("Schon zurückerstattet.");
        }

        if (refund is null)
        {
            refund = new OnlineRefund
            {
                ReceiptId = receiptId,
                Source = payment.Source,
                SourceId = payment.SourceId,
                LocationId = payment.LocationId,
                Provider = payment.Provider,
                PaymentReference = payment.Reference,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Email = payment.Email,
                Language = payment.Language,
                Reason = Short(reason),
            };
            online.Refunds.Add(refund);
        }

        refund.VoidReceiptId = voidReceiptId;
        refund.Status = OnlineRefundStatus.Pending;
        refund.Error = null;
        refund.RequestedBy = userId;
        refund.RequestedAt = time.GetUtcNow();
        refund.Attempts++;
        try
        {
            await online.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Storno-Ereignis und erneuter Versuch gleichzeitig: der andere war schneller.
            return new RequestOutcome.Refused("Die Erstattung läuft schon.");
        }

        await bus.PublishAsync(new RefundOnlinePayment(refund.Id, refund.Attempts));
        return new RequestOutcome.Started(ToDto(receiptId, payment, true, refund));
    }

    /// <summary>Erneut versuchen, wenn der Anbieter abgelehnt hat oder die Erstattung nach dem Storno nie begonnen hat.</summary>
    public async Task<RequestOutcome> RetryAsync(Guid receiptId, string? userId, CancellationToken ct)
    {
        if (!await db.Receipts.AnyAsync(r => r.Id == receiptId, ct))
        {
            return new RequestOutcome.NotFound();
        }

        var voidId = await db.Receipts.Where(r => r.VoidsReceiptId == receiptId).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        var reason = await db.Receipts.Where(r => r.VoidsReceiptId == receiptId).Select(r => r.VoidReason).FirstOrDefaultAsync(ct);
        return voidId is null
            ? new RequestOutcome.Refused("Zuerst den Beleg an der Kassa stornieren; erstattet wird danach.")
            : await RequestAsync(receiptId, voidId.Value, reason, userId, ct);
    }

    public async Task RefundedAsync(Guid refundId, string? reference, CancellationToken ct)
    {
        var refund = await online.Refunds.FirstAsync(r => r.Id == refundId, ct);
        refund.Status = OnlineRefundStatus.Refunded;
        refund.RefundReference = reference;
        refund.RefundedAt = time.GetUtcNow();
        refund.Error = null;
        switch (refund.Source)
        {
            case OnlinePaymentSource.Voucher:
                await online.VoucherOrders.Where(o => o.Id == refund.SourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(o => o.Status, VoucherOrderStatus.Refunded), ct);
                break;
            case OnlinePaymentSource.Deposit:
                await online.Deposits.Where(d => d.Id == refund.SourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, OnlineDepositStatus.Refunded).SetProperty(d => d.Error, (string?)null), ct);
                break;
            default:
                await online.CardGuarantees.Where(g => g.Id == refund.SourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(g => g.Status, CardGuaranteeStatus.Refunded), ct);
                break;
        }

        await online.SaveChangesAsync(ct);
    }

    internal static string? Short(string? text) => text is { Length: > 500 } ? text[..500] : text;

    private static OnlineRefundDto ToDto(Guid receiptId, Payment payment, bool voided, OnlineRefund? refund) =>
        new(receiptId, payment.Source, payment.Amount, payment.Currency, payment.Provider, voided, refund?.Status, refund?.RefundReference,
            refund?.RefundedAt, refund?.Error, voided && refund?.Status is null or OnlineRefundStatus.Failed);
}

/// <summary>
/// Nach dem Storno eines online bezahlten Belegs: die wieder geöffnete Rechnung schließen (sonst ließe sich der Gutschein an der
/// Kasse ein zweites Mal verkaufen) und die Erstattung beginnen.
/// </summary>
public static partial class OnlineReceiptVoidedHandler
{
    public static async Task Handle(ReceiptVoided message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var refunds = services.GetRequiredService<OnlineRefunds>();
        if (await refunds.PaymentOfAsync(message.ReceiptId, ct) is not { } payment)
        {
            return;
        }

        var db = services.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == payment.LocationId), ct) is not { } tenantId)
        {
            return;
        }

        services.GetRequiredService<TenantContext>().Use(tenantId);
        if (payment.BillId is { } billId && await services.GetRequiredService<PosService>().CloseAfterVoidAsync(billId, ct) is { } error)
        {
            CloseFailed(services.GetRequiredService<ILogger<OnlineRefund>>(), billId, error.Message);
        }

        await refunds.RequestAsync(message.ReceiptId, message.VoidReceiptId, message.Reason, message.UserId, ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rechnung {BillId} nach Storno einer Online-Zahlung nicht geschlossen: {Error}")]
    private static partial void CloseFailed(ILogger logger, Guid billId, string error);
}

/// <summary>Bittet den Zahlungsanbieter um die Erstattung und schickt dem Gast danach den Stornobeleg per Mail.</summary>
public static partial class RefundOnlinePaymentHandler
{
    public static async Task Handle(RefundOnlinePayment message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var logger = services.GetRequiredService<ILogger<OnlineRefund>>();
        var refund = await online.Refunds.AsNoTracking().FirstOrDefaultAsync(r => r.Id == message.RefundId, ct);
        if (refund is null || refund.Status != OnlineRefundStatus.Pending || refund.Attempts != message.Attempt)
        {
            return;
        }

        var db = services.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == refund.LocationId), ct) is not { } tenantId)
        {
            return;
        }

        var provider = services.GetServices<IOnlinePaymentProvider>().LastOrDefault(p => p.Name == refund.Provider);
        RefundResult result;
        try
        {
            result = provider is null
                ? new RefundResult(false, null, $"Zahlungsanbieter „{refund.Provider}“ ist nicht eingerichtet.")
                : await provider.RefundAsync(new PaymentRefund(tenantId, refund.Id, refund.PaymentReference, refund.Amount, refund.Currency), ct);
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or KeyNotFoundException)
        {
            result = new RefundResult(false, null, e.Message);
        }

        if (!result.Succeeded)
        {
            var error = OnlineRefunds.Short($"Nicht erstattet: {result.Error}");
            await online.Refunds.Where(r => r.Id == refund.Id && r.Attempts == message.Attempt)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, OnlineRefundStatus.Failed).SetProperty(r => r.Error, error), ct);
            RefundFailed(logger, refund.Id, result.Error ?? "");
            return;
        }

        await services.GetRequiredService<OnlineRefunds>().RefundedAsync(refund.Id, result.Reference, ct);

        services.GetRequiredService<TenantContext>().Use(tenantId);
        var customerId = refund.Source switch
        {
            OnlinePaymentSource.Voucher => await online.VoucherOrders.Where(o => o.Id == refund.SourceId).Select(o => o.CustomerId).FirstAsync(ct),
            OnlinePaymentSource.Deposit => await online.Deposits.Where(d => d.Id == refund.SourceId).Select(d => d.CustomerId).FirstAsync(ct),
            _ => await online.CardGuarantees.Where(g => g.Id == refund.SourceId).Select(g => g.CustomerId).FirstAsync(ct),
        };
        var guest = GuestName.Of(await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customerId, ct),
            await db.Locations.AsNoTracking().Where(l => l.Id == refund.LocationId).Select(l => l.TimeZone).FirstAsync(ct));
        var location = await db.Locations.AsNoTracking().Where(l => l.Id == refund.LocationId).Select(l => l.Name).FirstAsync(ct);
        var voidReceipt = refund.VoidReceiptId is { } voidId ? await services.GetRequiredService<PosService>().GetReceiptAsync(voidId, ct) : null;
        var receiptText = voidReceipt is null ? "" : $"{voidReceipt.RegisterCode} Nr. {voidReceipt.Number}, {voidReceipt.IssuedAt:dd.MM.yyyy}";
        await services.GetRequiredService<IMessageBus>().PublishAsync(new SendMail(OnlineMails.PaymentRefunded(
            refund.Email, guest, refund.Source, location, refund.Amount, refund.Currency, receiptText, refund.Language)));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erstattung {RefundId} abgelehnt: {Error}")]
    private static partial void RefundFailed(ILogger logger, Guid refundId, string error);
}
