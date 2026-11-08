using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>Gutscheine online kaufen; für das Team die Bestellungen eines Standorts, damit eine bezahlte, nicht ausgestellte auffällt.</summary>
internal static class VoucherShopEndpoints
{
    private const int MaxWebhookBytes = 256 * 1024;

    public static IEndpointRouteBuilder MapVoucherShopEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/online").WithTags("Kundenbereich").AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit);

        group.MapGet("/locations/{locationId:guid}/vouchers", async Task<Results<Ok<OnlineVoucherOffer>, NotFound>> (
                Guid locationId, VoucherShop shop, CancellationToken ct) =>
                await shop.OfferAsync(locationId, ct) is { } offer ? TypedResults.Ok(offer) : TypedResults.NotFound())
            .WithName("GetOnlineVoucherOffer")
            .WithSummary("Gutscheinangebot eines Standorts: Beträge, Behandlungen und ob der Online-Kauf gerade möglich ist.");

        group.MapPost("/vouchers/orders", async Task<Results<Created<VoucherOrderStarted>, ValidationProblem, NotFound, ProblemHttpResult>> (
                VoucherOrderRequest request, HttpContext http, VoucherShop shop, CancellationToken ct) =>
                await shop.OrderAsync(request, await CustomerAuth.TryGetAccountIdAsync(http), ct) switch
                {
                    VoucherShop.OrderOutcome.Started s => TypedResults.Created($"/api/online/vouchers/orders/{s.Order.Token}", s.Order),
                    VoucherShop.OrderOutcome.Invalid i => i.Errors.Problem(),
                    VoucherShop.OrderOutcome.Unavailable u => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: u.Reason),
                    _ => TypedResults.NotFound(),
                })
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("OrderOnlineVoucher")
            .WithSummary("Bestellt einen Gutschein und beginnt die Zahlung; ausgestellt und per Mail verschickt wird nach der Zahlung.");

        group.MapGet("/vouchers/orders/{token}", async Task<Results<Ok<VoucherOrderDto>, NotFound>> (
                string token, VoucherShop shop, CancellationToken ct) =>
                await shop.FindAsync(token, ct) is { } order ? TypedResults.Ok(order) : TypedResults.NotFound())
            .WithName("GetOnlineVoucherOrder")
            .WithSummary("Stand der Bestellung; der Gutscheincode erscheint, sobald die Kasse ihn ausgestellt hat.");

        group.MapPost("/vouchers/orders/{token}/test-payment", async Task<Results<Ok<VoucherOrderDto>, NotFound>> (
                string token, TestPaymentResult result, VoucherShop shop, CancellationToken ct) =>
                await shop.TestPaymentAsync(token, result.Paid, ct) is { } order ? TypedResults.Ok(order) : TypedResults.NotFound())
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("CompleteTestPayment")
            .WithSummary("Nur im Testbetrieb (OnlineVouchers:PaymentProvider=Test): Zahlung simulieren oder abbrechen.");

        // Webhook des Zahlungsanbieters. Ohne Mandant: die Bestellung steht im Kundenbereich, ausgestellt wird im Spa des Standorts.
        routes.MapPost("/api/hooks/payments/{provider}", async Task<Results<Ok, BadRequest<string>, NotFound>> (
                string provider, HttpRequest request, IEnumerable<IOnlinePaymentProvider> providers, VoucherShop shop, CardGuarantees cards,
                Deposits deposits, IMessageBus bus, CancellationToken ct) =>
            {
                if (providers.LastOrDefault(p => string.Equals(p.Name, provider, StringComparison.OrdinalIgnoreCase)) is not { IsTest: false } active)
                {
                    return TypedResults.NotFound();
                }

                if (await ReadBodyAsync(request, ct) is not { } body)
                {
                    return TypedResults.BadRequest("Zu groß.");
                }

                return await DispatchAsync(active.ReadWebhook(body, request.Headers), shop, cards, deposits, bus, ct);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Webhook für die eigenen Stripe-Schlüssel eines Spas (ADR 0054): signiert mit dessen Webhook-Geheimnis. Gemeldet wird nur,
        // was zu einem Standort dieses Spas gehört; mit dem eigenen Geheimnis lässt sich so keine Zahlung eines anderen Spas melden.
        routes.MapPost("/api/hooks/payments/stripe/{tenantId:guid}", async Task<Results<Ok, BadRequest<string>, NotFound>> (
                Guid tenantId, HttpRequest request, IEnumerable<IOnlinePaymentProvider> providers, TenantContext tenants,
                PaymentAccounts payments, AispaDbContext db, OnlineDbContext online, VoucherShop shop, CardGuarantees cards, Deposits deposits,
                IMessageBus bus, TimeProvider time, CancellationToken ct) =>
            {
                tenants.Use(tenantId);
                if (!providers.Any(p => p.Name == StripePaymentProvider.ProviderName) || await payments.OwnWebhookSecretAsync(ct) is not { } secret)
                {
                    return TypedResults.NotFound();
                }

                if (await ReadBodyAsync(request, ct) is not { } body)
                {
                    return TypedResults.BadRequest("Zu groß.");
                }

                if (!StripeWebhook.IsValid(secret, body, request.Headers["Stripe-Signature"].FirstOrDefault(), time.GetUtcNow(),
                        StripePaymentProvider.SignatureTolerance))
                {
                    return TypedResults.BadRequest("Signatur ungültig oder zu alt.");
                }

                var outcome = StripeWebhook.Read(body);
                Guid? locationId = outcome switch
                {
                    WebhookOutcome.Notified n => await online.VoucherOrders.Where(o => o.Id == n.Notification.OrderId).Select(o => (Guid?)o.LocationId).FirstOrDefaultAsync(ct)
                        ?? await online.Deposits.Where(d => d.Id == n.Notification.OrderId).Select(d => (Guid?)d.LocationId).FirstOrDefaultAsync(ct),
                    WebhookOutcome.CardSaved c => await online.CardGuarantees.Where(g => g.Id == c.GuaranteeId).Select(g => (Guid?)g.LocationId).FirstOrDefaultAsync(ct),
                    WebhookOutcome.CardSetupFailed f => await online.CardGuarantees.Where(g => g.Id == f.GuaranteeId).Select(g => (Guid?)g.LocationId).FirstOrDefaultAsync(ct),
                    _ => null,
                };
                if ((outcome is WebhookOutcome.Notified or WebhookOutcome.CardSaved or WebhookOutcome.CardSetupFailed)
                    && (locationId is not { } id || !await db.Locations.AnyAsync(l => l.Id == id, ct)))
                {
                    // Gehört nicht zu diesem Spa: bestätigen, damit Stripe nicht endlos wiederholt, aber nichts tun.
                    return TypedResults.Ok();
                }

                // Konto-Ereignisse gibt es hier nicht; sie kommen nur über den Connect-Webhook der Plattform.
                return await DispatchAsync(outcome is WebhookOutcome.AccountUpdated ? new WebhookOutcome.Ignored() : outcome, shop, cards, deposits, bus, ct);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Der Standort wird über das Fachmodell geprüft, damit der Mandantenfilter auch hier greift.
        routes.MapGet("/api/locations/{locationId:guid}/voucher-orders", async Task<Results<Ok<List<VoucherOrderStaffEntry>>, NotFound>> (
                Guid locationId, AispaDbContext db, OnlineDbContext online, CancellationToken ct) =>
                !await db.Locations.AnyAsync(l => l.Id == locationId, ct)
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(await online.VoucherOrders.AsNoTracking()
                    .Where(o => o.LocationId == locationId && o.Status != VoucherOrderStatus.Pending && o.Status != VoucherOrderStatus.Cancelled)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(100)
                    .Select(o => new VoucherOrderStaffEntry(o.Id, o.CreatedAt, o.Status, o.Kind, o.Amount, o.Currency, o.BuyerEmail,
                        o.RecipientName, o.ReceiptId, o.Error))
                    .ToListAsync(ct)))
            .RequirePermission(AccessRight.Book)
            .WithTags("Kassa")
            .WithName("ListOnlineVoucherOrders")
            .WithSummary("Bezahlte Online-Gutscheine eines Standorts, neueste zuerst; „Failed“ heißt bezahlt, aber nicht ausgestellt.");

        return routes;
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, ct);
        return body.Length > MaxWebhookBytes ? null : body.ToArray();
    }

    private static async Task<Results<Ok, BadRequest<string>, NotFound>> DispatchAsync(WebhookOutcome outcome, VoucherShop shop, CardGuarantees cards,
        Deposits deposits, IMessageBus bus, CancellationToken ct)
    {
        switch (outcome)
        {
            case WebhookOutcome.Invalid invalid:
                return TypedResults.BadRequest(invalid.Reason);
            // Die Kennung gehört zu einer Gutscheinbestellung oder einer Anzahlung; die jeweils andere Seite ändert nichts.
            case WebhookOutcome.Notified { Notification: { Result: PaymentResult.Paid } paid }:
                await shop.ConfirmPaymentAsync(paid.OrderId, paid.Reference, ct);
                await deposits.ConfirmPaymentAsync(paid.OrderId, paid.Reference, ct);
                break;
            case WebhookOutcome.Notified { Notification: var failed }:
                await shop.PaymentFailedAsync(failed.OrderId, failed.Reference, ct);
                await deposits.PaymentFailedAsync(failed.OrderId, failed.Reference, ct);
                break;
            case WebhookOutcome.CardSaved saved:
                await cards.CardSavedAsync(saved.GuaranteeId, saved.SessionReference, saved.CardReference, saved.ProviderCustomer, ct);
                break;
            case WebhookOutcome.CardSetupFailed setupFailed:
                await cards.SetupFailedAsync(setupFailed.GuaranteeId, setupFailed.SessionReference, ct);
                break;
            case WebhookOutcome.AccountUpdated updated:
                // Stand des verbundenen Kontos über die Queue abfragen; das Spa wird über die Kontonummer gefunden.
                await bus.PublishAsync(new CheckPaymentAccount(null, updated.AccountId));
                break;
        }

        return TypedResults.Ok();
    }
}
