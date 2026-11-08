using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>
/// Gutscheine online kaufen: Angebot, Bestellung, Zahlung. Ausgestellt wird erst nach bestätigter Zahlung, über die Kasse des
/// Standorts (<see cref="IssueOnlineVoucherHandler"/>), damit jeder Online-Verkauf einen ordentlichen Beleg hat.
/// </summary>
internal sealed class VoucherShop(
    AispaDbContext db,
    OnlineDbContext online,
    OnlinePortal portal,
    CustomerAccounts accounts,
    IMessageBus bus,
    TimeProvider time,
    IOptions<OnlineVoucherOptions> voucherOptions,
    IOptions<OnlineBookingOptions> bookingOptions,
    IEnumerable<IOnlinePaymentProvider> providers,
    PaymentAccounts payments,
    TenantContext tenants)
{
    public const int MaxRecipientLength = 100;
    public const int MaxMessageLength = 300;

    /// <summary>Die Testzahlung, falls der Server sie anbietet; welcher Anbieter für neue Zahlungen gilt, sagt <see cref="PaymentAccounts"/>.</summary>
    private readonly IOnlinePaymentProvider? _test = providers.LastOrDefault(p => p.IsTest);

    private OnlineVoucherOptions Options => voucherOptions.Value;

    public abstract record OrderOutcome
    {
        public sealed record Started(VoucherOrderStarted Order) : OrderOutcome;

        public sealed record Invalid(Errors Errors) : OrderOutcome;

        public sealed record NotFound : OrderOutcome;

        public sealed record Unavailable(string Reason) : OrderOutcome;
    }

    public async Task<OnlineVoucherOffer?> OfferAsync(Guid locationId, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var reason = await UnavailableReasonAsync(locationId, ct);
        var provider = await payments.ActiveProviderAsync(ct);
        var treatments = (await OnlineBookingEndpoints.OfferedTreatmentsAsync(db, locationId, ct))
            .Select(t => new OnlineTreatment(t.Id, t.Name, t.DurationMinutes, t.PriceAt(locationId), location.Currency, t.Category)).ToList();
        return new OnlineVoucherOffer(location.Id, location.Name, location.Currency, reason is null, reason,
            Options.MinAmount, Options.MaxAmount, Options.SuggestedAmounts, treatments, provider?.IsTest == true);
    }

    public async Task<OrderOutcome> OrderAsync(VoucherOrderRequest request, Guid? accountId, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.LocationId, ct);
        if (location is null)
        {
            return new OrderOutcome.NotFound();
        }

        if (await UnavailableReasonAsync(location.Id, ct) is { } reason)
        {
            return new OrderOutcome.Unavailable(reason);
        }

        var account = accountId is { } aid ? await online.Users.FirstOrDefaultAsync(a => a.Id == aid, ct) : null;
        var errors = new Errors();
        errors.Check(request.AcceptPrivacy, nameof(request.AcceptPrivacy), "Bitte den Datenschutzhinweis bestätigen.");
        errors.Check((request.RecipientName?.Length ?? 0) <= MaxRecipientLength, nameof(request.RecipientName), $"Höchstens {MaxRecipientLength} Zeichen.");
        errors.Check((request.Message?.Length ?? 0) <= MaxMessageLength, nameof(request.Message), $"Höchstens {MaxMessageLength} Zeichen.");
        decimal amount = 0;
        Treatment? treatment = null;
        if (request.Kind == VoucherKind.SinglePurpose)
        {
            treatment = (await OnlineBookingEndpoints.OfferedTreatmentsAsync(db, location.Id, ct)).FirstOrDefault(t => t.Id == request.TreatmentId);
            errors.Check(treatment is not null, nameof(request.TreatmentId), "Bitte eine Behandlung wählen.");
            amount = treatment?.PriceAt(location.Id) ?? 0;
        }
        else
        {
            amount = request.Amount ?? 0;
            errors.Check(amount >= Options.MinAmount && amount <= Options.MaxAmount && decimal.Round(amount, 2) == amount,
                nameof(request.Amount), $"Bitte einen Betrag von {Options.MinAmount:0} bis {Options.MaxAmount:0} {location.Currency} wählen.");
        }

        if (account?.CustomerId is null)
        {
            CustomerAccounts.ValidateContact(errors, request.Buyer, requireEmail: account is null);
        }

        if (errors.Any)
        {
            return new OrderOutcome.Invalid(errors);
        }

        var provider = (await payments.ActiveProviderAsync(ct))!;
        var tenantId = await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == location.Id), ct) ?? Guid.Empty;
        var (customerId, email) = await accounts.ResolveCustomerAsync(account, request.Buyer, ct);
        var token = AccessLinks.NewToken();
        var now = time.GetUtcNow();
        var order = new VoucherOrder
        {
            TokenHash = AccessLinks.Hash(token),
            LocationId = location.Id,
            CustomerId = customerId,
            CustomerAccountId = account?.Id,
            Kind = request.Kind,
            Amount = amount,
            Currency = location.Currency,
            TreatmentId = treatment?.Id,
            BuyerEmail = email,
            RecipientName = Blank(request.RecipientName),
            Message = Blank(request.Message),
            Language = GuestLanguage.Normalize(request.Language),
            CreatedAt = now,
            PrivacyAcceptedAt = now,
            PrivacyNoticeVersion = bookingOptions.Value.PrivacyNoticeVersion,
            Status = VoucherOrderStatus.Pending,
            Provider = provider.Name,
        };
        var description = treatment is null ? $"Wertgutschein {location.Name}" : $"Gutschein {treatment.Name}, {location.Name}";
        var orderPage = portal.Url($"gutschein/{token}", tenants.TenantId);
        var payment = new PaymentStart(tenantId, order.Id, amount, location.Currency, description, email, orderPage);
        online.VoucherOrders.Add(order);
        if (provider.IsTest)
        {
            // Die Testzahlung läuft lokal ohne Netz und ist sofort bereit.
            var session = await provider.StartAsync(payment, ct);
            order.ProviderReference = session.Reference;
            await online.SaveChangesAsync(ct);
        }
        else
        {
            // Der Anbieter wird über die Queue angefragt; die Bestellseite leitet weiter, sobald seine Zahlungsseite bereit ist.
            await online.SaveChangesAsync(ct);
            await bus.PublishAsync(new StartOnlinePayment(payment));
        }

        return new OrderOutcome.Started(new VoucherOrderStarted(order.Id, token, orderPage));
    }

    public async Task<VoucherOrderDto?> FindAsync(string token, CancellationToken ct) =>
        await FindOrderAsync(token, ct) is { } order ? await ToDtoAsync(order, ct) : null;

    /// <summary>Nur im Testbetrieb: Zahlung simulieren oder abbrechen.</summary>
    public async Task<VoucherOrderDto?> TestPaymentAsync(string token, bool paid, CancellationToken ct)
    {
        var order = await FindOrderAsync(token, ct);
        if (order is null || _test is null || order.Provider != _test.Name)
        {
            return null;
        }

        if (order.Status == VoucherOrderStatus.Pending)
        {
            if (paid)
            {
                await ConfirmPaymentAsync(order.Id, order.ProviderReference!, ct);
                await online.Entry(order).ReloadAsync(ct);
            }
            else
            {
                order.Status = VoucherOrderStatus.Cancelled;
                await online.SaveChangesAsync(ct);
            }
        }

        return await ToDtoAsync(order, ct);
    }

    /// <summary>
    /// Der Zahlungsanbieter hat die Zahlung bestätigt (Webhook oder Testzahlung). Idempotent: eine zweite Meldung ändert nichts.
    /// Das Ausstellen läuft entkoppelt über die Queue.
    /// </summary>
    public async Task ConfirmPaymentAsync(Guid orderId, string providerReference, CancellationToken ct)
    {
        var updated = await online.VoucherOrders
            .Where(o => o.Id == orderId && o.Status == VoucherOrderStatus.Pending && o.ProviderReference == providerReference)
            .ExecuteUpdateAsync(u => u.SetProperty(o => o.Status, VoucherOrderStatus.Paid).SetProperty(o => o.PaidAt, time.GetUtcNow()), ct);
        if (updated == 1)
        {
            await bus.PublishAsync(new IssueOnlineVoucher(orderId));
        }
    }

    /// <summary>Der Anbieter meldet: abgebrochen, abgelaufen oder abgelehnt. Nur eine wartende Bestellung wird storniert.</summary>
    public Task PaymentFailedAsync(Guid orderId, string providerReference, CancellationToken ct) =>
        online.VoucherOrders
            .Where(o => o.Id == orderId && o.Status == VoucherOrderStatus.Pending && o.ProviderReference == providerReference)
            .ExecuteUpdateAsync(u => u.SetProperty(o => o.Status, VoucherOrderStatus.Cancelled), ct);

    public async Task<VoucherOrderDto> ToDtoAsync(VoucherOrder order, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == order.LocationId, ct);
        var treatmentName = order.TreatmentId is { } tid
            ? await db.Treatments.AsNoTracking().Where(t => t.Id == tid).Select(t => t.Name).FirstOrDefaultAsync(ct)
            : null;
        var voucher = order.VoucherId is { } vid && order.Status == VoucherOrderStatus.Issued
            ? await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid, ct)
            : null;
        var test = _test is not null && order.Provider == _test.Name;
        return new VoucherOrderDto(order.Id, order.Status, order.Kind, order.Amount, order.Currency, location.Name, treatmentName,
            order.RecipientName, order.Message, voucher?.Code, voucher?.ValidUntil, test,
            order.Status == VoucherOrderStatus.Pending && !test ? order.PaymentUrl : null);
    }

    /// <summary>Warum hier gerade keine Gutscheine online verkauft werden, oder <c>null</c>.</summary>
    private async Task<string?> UnavailableReasonAsync(Guid locationId, CancellationToken ct)
    {
        if (await payments.ActiveProviderAsync(ct) is null)
        {
            return "Gutscheine können hier noch nicht online gekauft werden.";
        }

        return await db.CashRegisters.AnyAsync(r => r.LocationId == locationId && r.IsActive, ct)
            ? null
            : "An diesem Standort gibt es noch keine Kasse für den Gutscheinverkauf.";
    }

    private async Task<VoucherOrder?> FindOrderAsync(string token, CancellationToken ct)
    {
        if (token.Length is < 20 or > 100)
        {
            return null;
        }

        var hash = AccessLinks.Hash(token);
        return await online.VoucherOrders.FirstOrDefaultAsync(o => o.TokenHash == hash, ct);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
