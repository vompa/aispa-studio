using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>Nachricht in der Queue: beim Zahlungsanbieter die Zahlungsseite für eine Bestellung anlegen.</summary>
public sealed record StartOnlinePayment(PaymentStart Payment);

/// <summary>
/// Legt die Zahlung (Gutscheinbestellung oder Anzahlung) beim Anbieter an und merkt sich Kennung und Zahlungsseite. Scheitert der
/// Anbieter dauerhaft, wird die Bestellung storniert bzw. die Anzahlung abgebrochen (es wurde nichts bezahlt), damit niemand ewig wartet.
/// </summary>
public static partial class StartOnlinePaymentHandler
{
    public static async Task Handle(StartOnlinePayment message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var order = await online.VoucherOrders.FirstOrDefaultAsync(o => o.Id == message.Payment.OrderId, ct);
        if (order is not null)
        {
            if (order.Status != VoucherOrderStatus.Pending || order.PaymentUrl is not null)
            {
                return;
            }

            var (reference, url, error) = await StartAsync(services, order.Provider, message.Payment, ct);
            order.ProviderReference = reference;
            order.PaymentUrl = url;
            if (error is not null)
            {
                order.Status = VoucherOrderStatus.Cancelled;
                order.Error = error;
                StartFailed(services.GetRequiredService<ILogger<VoucherOrder>>(), order.Id, error);
            }

            await online.SaveChangesAsync(ct);
            return;
        }

        // Anzahlung auf einen Termin: ein Fehler beim Anbieter ist „abgebrochen“, der Gast kann neu beginnen.
        var deposit = await online.Deposits.FirstOrDefaultAsync(d => d.Id == message.Payment.OrderId, ct);
        if (deposit is null || deposit.Status != OnlineDepositStatus.Pending || deposit.Attempts != message.Payment.Attempt || deposit.PaymentUrl is not null)
        {
            return;
        }

        var (depositReference, depositUrl, depositError) = await StartAsync(services, deposit.Provider, message.Payment, ct);
        deposit.ProviderReference = depositReference;
        deposit.PaymentUrl = depositUrl;
        if (depositError is not null)
        {
            deposit.Status = OnlineDepositStatus.Failed;
            deposit.Error = depositError;
            StartFailed(services.GetRequiredService<ILogger<DepositOrder>>(), deposit.Id, depositError);
        }

        await online.SaveChangesAsync(ct);
    }

    private static async Task<(string? Reference, string? Url, string? Error)> StartAsync(IServiceProvider services, string providerName, PaymentStart payment,
        CancellationToken ct)
    {
        var provider = services.GetServices<IOnlinePaymentProvider>().LastOrDefault(p => p.Name == providerName);
        try
        {
            var session = provider is null
                ? throw new InvalidOperationException($"Zahlungsanbieter „{providerName}“ ist nicht eingerichtet.")
                : await provider.StartAsync(payment, ct);
            return (session.Reference, session.PaymentUrl, null);
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or KeyNotFoundException)
        {
            return (null, null, OnlineRefunds.Short($"Zahlungsseite konnte nicht angelegt werden: {e.Message}"));
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Online-Zahlung {OrderId} konnte nicht angelegt werden: {Error}")]
    private static partial void StartFailed(ILogger logger, Guid orderId, string error);
}
