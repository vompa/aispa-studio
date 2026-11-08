using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>Nachricht in der Queue: diese bezahlte Anzahlung an der Kasse des Standorts verbuchen (Anzahlungsbeleg).</summary>
public sealed record ReceiptOnlineDeposit(Guid DepositId);

/// <summary>Nachricht in der Queue: der Gast hat rechtzeitig abgesagt oder der Betrieb hat abgesagt; Anzahlungsbeleg stornieren und erstatten. <c>Reason</c>: Stornogrund, sonst der des Gastes.</summary>
public sealed record RefundOnlineDeposit(Guid DepositId, string? Reason = null);

/// <summary>Queue: diesen online gebuchten Termin freigeben, weil bis <c>At</c> (Zeitpunkt des Laufs) die Anzahlung nicht bezahlt wurde.</summary>
public sealed record ReleaseUnpaidDeposit(Guid DepositId, DateTimeOffset At);

/// <summary>
/// Stellt den Anzahlungsbeleg aus: offene Rechnung mit der Position „Anzahlung …“ (Steuersatz und Erlöskonto der Behandlung), bezahlt
/// mit „Karte“ und der Referenz des Zahlungsanbieters. So steht die Anzahlung mit Signatur im Tagesabschluss, und die Kasse zieht sie
/// beim Kassieren der Behandlung ab. Die Rechnung wird zuerst gemerkt: läuft die Nachricht ein zweites Mal, wird nichts doppelt verbucht.
/// </summary>
public static partial class ReceiptOnlineDepositHandler
{
    public const string OnlineUser = "online-anzahlung";

    public static async Task Handle(ReceiptOnlineDeposit message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var db = services.GetRequiredService<AispaDbContext>();
        var pos = services.GetRequiredService<PosService>();
        var logger = services.GetRequiredService<ILogger<DepositOrder>>();

        var deposit = await online.Deposits.FirstOrDefaultAsync(d => d.Id == message.DepositId, ct);
        if (deposit is null || deposit.Status != OnlineDepositStatus.Paid)
        {
            return;
        }

        // Im Hintergrund gibt es keinen angemeldeten Mandanten: die Kasse schreibt im Spa des Standorts.
        if (await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == deposit.LocationId), ct) is not { } tenantId)
        {
            await FailAsync(online, deposit, "Standort nicht gefunden.", logger, ct);
            return;
        }

        services.GetRequiredService<TenantContext>().Use(tenantId);
        if (deposit.BillId is null)
        {
            var register = await db.CashRegisters.AsNoTracking()
                .Where(r => r.LocationId == deposit.LocationId && r.IsActive).OrderBy(r => r.Code).FirstOrDefaultAsync(ct);
            if (register is null)
            {
                await FailAsync(online, deposit, "An diesem Standort gibt es keine aktive Kasse mehr.", logger, ct);
                return;
            }

            var bill = await pos.CreateBillAsync(new BillInput(register.Id, deposit.CustomerId, []), ct);
            if (bill.Error is { } billError)
            {
                await FailAsync(online, deposit, billError.Message, logger, ct);
                return;
            }

            var line = await pos.AddLineAsync(bill.Value!.Id, new BillLineInput(
                BillLineKind.Deposit, deposit.AppointmentId, null, 1, deposit.Amount, 0, null, null, null, null, null), supervisor: false, ct);
            if (line.Error is { } lineError)
            {
                await pos.DeleteBillAsync(bill.Value.Id, ct);
                await FailAsync(online, deposit, lineError.Message, logger, ct);
                return;
            }

            deposit.BillId = bill.Value.Id;
            await online.SaveChangesAsync(ct);
        }

        var current = await pos.GetBillAsync(deposit.BillId.Value, ct);
        Guid receiptId;
        if (current is { Status: BillStatus.Settled, ReceiptIds.Count: > 0 })
        {
            receiptId = current.ReceiptIds[0];
        }
        else
        {
            var receipt = await pos.CheckoutAsync(deposit.BillId.Value, new CheckoutRequest(null,
                [new PaymentInput(PaymentMethod.Card, deposit.Amount, null, $"Online {deposit.Provider} {deposit.ProviderReference}")]), OnlineUser, ct);
            if (receipt.Error is { } checkoutError)
            {
                await FailAsync(online, deposit, checkoutError.Message, logger, ct);
                return;
            }

            receiptId = receipt.Value!.Id;
        }

        deposit.ReceiptId = receiptId;
        deposit.ReceiptedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        deposit.Status = OnlineDepositStatus.Receipted;
        deposit.Error = null;
        await online.SaveChangesAsync(ct);

        var appointment = await db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
            .FirstAsync(a => a.Id == deposit.AppointmentId, ct);

        // Inzwischen online abgesagt oder freigegeben: die Zahlung kam zu spät und geht über das Storno ihres Belegs zurück. Ebenso, wenn der Betrieb inzwischen abgesagt hat
        // (Schließzeit, ADR 0073): auch dann ist die Anzahlung zu retournieren.
        if (appointment.Status == AppointmentStatus.Cancelled && appointment.CancelledByBusiness)
        {
            await services.GetRequiredService<IMessageBus>().PublishAsync(new RefundOnlineDeposit(deposit.Id, RefundOnlineDepositHandler.BusinessReason));
            return;
        }

        if (appointment.Status == AppointmentStatus.Cancelled
            && await online.OnlineBookings.AnyAsync(b => b.AppointmentId == appointment.Id && b.CancelledAt != null, ct))
        {
            await services.GetRequiredService<IMessageBus>().PublishAsync(new RefundOnlineDeposit(deposit.Id));
            return;
        }

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == deposit.LocationId, ct);
        var receiptDto = await pos.GetReceiptAsync(receiptId, ct);
        var receiptText = receiptDto is null ? receiptId.ToString() : $"{receiptDto.RegisterCode} Nr. {receiptDto.Number}, {receiptDto.IssuedAt:dd.MM.yyyy}";
        await services.GetRequiredService<IMessageBus>().PublishGuestMailAsync(services.GetRequiredService<TenantContext>(),
            OnlineMails.DepositReceived(deposit.Email, GuestName.Of(appointment.Customer, location.TimeZone), appointment.Treatment.Name, appointment.Start, location.TimeZone,
                location.Name, deposit.Amount, deposit.Currency, deposit.Percent, receiptText, deposit.Language));
    }

    private static async Task FailAsync(OnlineDbContext online, DepositOrder deposit, string error, ILogger logger, CancellationToken ct)
    {
        // Bezahlt bleibt bezahlt: der Fehler steht am Termin, die Nachricht läuft nach einer Korrektur erneut.
        deposit.Error = error.Length > 500 ? error[..500] : error;
        await online.SaveChangesAsync(ct);
        ReceiptFailed(logger, deposit.Id, error);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Anzahlung {DepositId} ist bezahlt, der Anzahlungsbeleg konnte aber nicht erstellt werden: {Error}")]
    private static partial void ReceiptFailed(ILogger logger, Guid depositId, string error);
}

/// <summary>
/// Nach einer rechtzeitigen Absage des Gastes: storniert den Anzahlungsbeleg an der Kasse (mit Grund, als Kassenaufsicht des Systems)
/// und meldet das Storno wie die Kasse selbst, worauf der Kundenbereich erstattet (ADR 0038). So gibt es zu jeder Erstattung einen Stornobeleg.
/// </summary>
public static partial class RefundOnlineDepositHandler
{
    public const string Reason = "Online rechtzeitig storniert; Anzahlung wird erstattet";

    /// <summary>Stornogrund, wenn die Zahlung erst nach der Absage durch den Betrieb (Schließzeit) eintrifft.</summary>
    public const string BusinessReason = "Vom Betrieb abgesagt (Schließzeit); Anzahlung wird erstattet";

    public static async Task Handle(RefundOnlineDeposit message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var db = services.GetRequiredService<AispaDbContext>();
        var deposit = await online.Deposits.AsNoTracking().FirstOrDefaultAsync(d => d.Id == message.DepositId, ct);
        if (deposit is not { Status: OnlineDepositStatus.Receipted, ReceiptId: { } receiptId }
            || await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == deposit.LocationId), ct) is not { } tenantId)
        {
            return;
        }

        services.GetRequiredService<TenantContext>().Use(tenantId);
        if (await db.Receipts.AnyAsync(r => r.VoidsReceiptId == receiptId, ct))
        {
            return;
        }

        var reason = message.Reason ?? Reason;
        var voided = await services.GetRequiredService<PosService>().VoidAsync(receiptId, reason, null, supervisor: true, ct);
        if (voided.Error is { } error)
        {
            VoidFailed(services.GetRequiredService<ILogger<DepositOrder>>(), deposit.Id, error.Message);
            await online.Deposits.Where(d => d.Id == deposit.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(d => d.Error, OnlineRefunds.Short($"Nicht erstattet: {error.Message}")), ct);
            return;
        }

        await services.GetRequiredService<IMessageBus>().PublishAsync(new ReceiptVoided(receiptId, voided.Value!.Id, reason, null));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anzahlungsbeleg zu {DepositId} konnte nicht storniert werden: {Error}")]
    private static partial void VoidFailed(ILogger logger, Guid depositId, string error);
}

/// <summary>
/// Gibt den Termin frei, weil die Anzahlung in der Frist nicht bezahlt wurde: zuerst wechselt die Anzahlung auf „freigegeben“ (nur aus
/// „ausstehend“ oder „abgebrochen“), damit eine gleichzeitig gemeldete Zahlung gewinnt; dann wird storniert, die Warteliste rückt
/// nach, und der Gast bekommt eine Mail.
/// </summary>
public static class ReleaseUnpaidDepositHandler
{
    public static async Task Handle(ReleaseUnpaidDeposit message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var db = services.GetRequiredService<AispaDbContext>();
        var now = message.At;
        var deposit = await online.Deposits.AsNoTracking().FirstOrDefaultAsync(d => d.Id == message.DepositId, ct);
        if (deposit is not { ReleaseAt: { } releaseAt } || releaseAt > now
            || await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == deposit.LocationId), ct) is not { } tenantId)
        {
            return;
        }

        var tenants = services.GetRequiredService<TenantContext>();
        tenants.Use(tenantId);
        var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == deposit.AppointmentId, ct);
        if (appointment is null || appointment.Status != AppointmentStatus.Booked || appointment.Start <= now)
        {
            return;
        }

        var claimed = await online.Deposits
            .Where(d => d.Id == deposit.Id && (d.Status == OnlineDepositStatus.Pending || d.Status == OnlineDepositStatus.Failed))
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, OnlineDepositStatus.Released).SetProperty(d => d.PaymentUrl, (string?)null), ct);
        if (claimed == 0)
        {
            return;
        }

        var booking = services.GetRequiredService<BookingService>();
        if (await booking.CancelAsync(appointment.Id, "Freigegeben: Anzahlung nicht bezahlt", ct) is not BookingOutcome.Success)
        {
            await online.Deposits.Where(d => d.Id == deposit.Id && d.Status == OnlineDepositStatus.Released)
                .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, OnlineDepositStatus.Pending), ct);
            return;
        }

        var record = await online.OnlineBookings.FirstOrDefaultAsync(b => b.AppointmentId == appointment.Id, ct);
        if (record is not null)
        {
            record.CancelledAt = now;
            await online.SaveChangesAsync(ct);
        }

        var portal = services.GetRequiredService<OnlinePortal>();
        var cancelled = await portal.ToOnlineAsync((await portal.LoadAppointmentAsync(appointment.Id, ct))!, ct);
        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == appointment.CustomerId, ct);
        await services.GetRequiredService<IMessageBus>().PublishGuestMailAsync(tenants, OnlineMails.Released(
            deposit.Email, GuestName.Of(customer, cancelled.TimeZone), cancelled, portal.Url("", tenantId), record?.Language ?? deposit.Language, deposit: true));
    }
}
