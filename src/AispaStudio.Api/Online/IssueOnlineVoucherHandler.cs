using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>Nachricht in der Queue: diese bezahlte Bestellung an der Kasse ausstellen und den Gutschein verschicken.</summary>
public sealed record IssueOnlineVoucher(Guid OrderId);

/// <summary>
/// Stellt einen online bezahlten Gutschein an der Kasse des Standorts aus: offene Rechnung mit der Gutscheinposition, bezahlt mit
/// „Karte“ und der Referenz des Zahlungsanbieters. So entstehen Beleg, Signatur, Erlöskonto und Gutscheincode genau wie beim Verkauf
/// am Empfang, und der Umsatz steht im Tagesabschluss. Die Rechnung wird zuerst gemerkt: läuft die Nachricht ein zweites Mal,
/// wird dieselbe Rechnung fertig bezahlt statt ein zweiter Gutschein verkauft.
/// </summary>
public static partial class IssueOnlineVoucherHandler
{
    public const string OnlineUser = "online-shop";

    public static async Task Handle(IssueOnlineVoucher message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var db = services.GetRequiredService<AispaDbContext>();
        var pos = services.GetRequiredService<PosService>();
        var logger = services.GetRequiredService<ILogger<VoucherOrder>>();

        var order = await online.VoucherOrders.FirstOrDefaultAsync(o => o.Id == message.OrderId, ct);
        if (order is null || order.Status != VoucherOrderStatus.Paid)
        {
            return;
        }

        // Im Hintergrund gibt es keinen angemeldeten Mandanten: die Kasse schreibt im Spa des Standorts.
        if (await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == order.LocationId), ct) is not { } tenantId)
        {
            await FailAsync(online, order, "Standort nicht gefunden.", logger, ct);
            return;
        }

        services.GetRequiredService<TenantContext>().Use(tenantId);

        if (order.BillId is null)
        {
            var register = await db.CashRegisters.AsNoTracking()
                .Where(r => r.LocationId == order.LocationId && r.IsActive).OrderBy(r => r.Code).FirstOrDefaultAsync(ct);
            if (register is null)
            {
                await FailAsync(online, order, "An diesem Standort gibt es keine aktive Kasse mehr.", logger, ct);
                return;
            }

            var bill = await pos.CreateBillAsync(new BillInput(register.Id, order.CustomerId, []), ct);
            if (bill.Error is { } billError)
            {
                await FailAsync(online, order, billError.Message, logger, ct);
                return;
            }

            var line = await pos.AddLineAsync(bill.Value!.Id, new BillLineInput(
                BillLineKind.Voucher, null, null, 1, order.Amount, 0, null, null, null, order.Kind, order.TreatmentId), supervisor: true, ct);
            if (line.Error is { } lineError)
            {
                await pos.DeleteBillAsync(bill.Value.Id, ct);
                await FailAsync(online, order, lineError.Message, logger, ct);
                return;
            }

            order.BillId = bill.Value.Id;
            await online.SaveChangesAsync(ct);
        }

        var current = await pos.GetBillAsync(order.BillId.Value, ct);
        Guid receiptId;
        if (current is { Status: BillStatus.Settled, ReceiptIds.Count: > 0 })
        {
            receiptId = current.ReceiptIds[0];
        }
        else
        {
            var receipt = await pos.CheckoutAsync(order.BillId.Value, new CheckoutRequest(null,
                [new PaymentInput(PaymentMethod.Card, order.Amount, null, $"Online {order.Provider} {order.ProviderReference}")]), OnlineUser, ct);
            if (receipt.Error is { } checkoutError)
            {
                await FailAsync(online, order, checkoutError.Message, logger, ct);
                return;
            }

            receiptId = receipt.Value!.Id;
        }

        var voucher = await db.Vouchers.AsNoTracking().FirstAsync(v => v.SoldOnReceiptId == receiptId, ct);
        order.ReceiptId = receiptId;
        order.VoucherId = voucher.Id;
        order.Status = VoucherOrderStatus.Issued;
        order.IssuedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        order.Error = null;
        await online.SaveChangesAsync(ct);

        var shop = services.GetRequiredService<VoucherShop>();
        var dto = await shop.ToDtoAsync(order, ct);
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == order.LocationId, ct);
        var guest = GuestName.Of(await db.Customers.AsNoTracking().FirstAsync(c => c.Id == order.CustomerId, ct), location.TimeZone);
        var link = services.GetRequiredService<OnlinePortal>().Url("");
        var receiptDto = await pos.GetReceiptAsync(receiptId, ct);
        var receiptText = receiptDto is null ? receiptId.ToString() : $"{receiptDto.RegisterCode} Nr. {receiptDto.Number}, {receiptDto.IssuedAt:dd.MM.yyyy}";
        var mail = OnlineMails.VoucherIssued(order.BuyerEmail, guest, dto, receiptText, link, order.Language);
        await services.GetRequiredService<IMessageBus>().PublishGuestMailAsync(services.GetRequiredService<TenantContext>(), mail);

        // Vermerk für die Gutscheinliste (ADR 0092): die Mail ging an den Käufer, ohne dass jemand vom Team sie ausgelöst hat.
        db.SentDocuments.Add(new SentDocument
        {
            Kind = SentDocument.VoucherKind,
            TargetId = voucher.Id,
            To = order.BuyerEmail,
            Subject = mail.Subject,
            SentAt = services.GetRequiredService<TimeProvider>().GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task FailAsync(OnlineDbContext online, VoucherOrder order, string error, ILogger logger, CancellationToken ct)
    {
        order.Status = VoucherOrderStatus.Failed;
        order.Error = error.Length > 500 ? error[..500] : error;
        await online.SaveChangesAsync(ct);
        IssueFailed(logger, order.Id, error);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Online-Gutschein {OrderId} ist bezahlt, konnte aber nicht ausgestellt werden: {Error}")]
    private static partial void IssueFailed(ILogger logger, Guid orderId, string error);
}
