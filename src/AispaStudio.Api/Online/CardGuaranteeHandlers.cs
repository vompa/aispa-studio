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

/// <summary>Nachricht in der Queue: beim Zahlungsanbieter die Seite zum Hinterlegen der Karte anlegen.</summary>
public sealed record StartCardGuarantee(CardSetupStart Setup);

/// <summary>Nachricht in der Queue: die Kassenaufsicht hat die Ausfallgebühr ausgelöst.</summary>
public sealed record ChargeNoShowFee(Guid GuaranteeId);

/// <summary>Legt die Seite zum Hinterlegen an. Scheitert der Anbieter, ist die Hinterlegung „abgebrochen“ und kann neu beginnen.</summary>
public static partial class StartCardGuaranteeHandler
{
    public static async Task Handle(StartCardGuarantee message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var guarantee = await online.CardGuarantees.FirstOrDefaultAsync(g => g.Id == message.Setup.GuaranteeId, ct);
        if (guarantee is null || guarantee.Status != CardGuaranteeStatus.Pending || guarantee.Attempts != message.Setup.Attempt
            || guarantee.SetupUrl is not null)
        {
            return;
        }

        var provider = services.GetServices<IOnlinePaymentProvider>().LastOrDefault(p => p.Name == guarantee.Provider);
        try
        {
            var session = provider is null
                ? throw new InvalidOperationException($"Zahlungsanbieter „{guarantee.Provider}“ ist nicht eingerichtet.")
                : await provider.StartCardSetupAsync(message.Setup, ct);
            guarantee.SessionReference = session.Reference;
            guarantee.SetupUrl = session.PaymentUrl;
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or KeyNotFoundException)
        {
            guarantee.Status = CardGuaranteeStatus.Failed;
            guarantee.Error = Short($"Seite zum Hinterlegen konnte nicht angelegt werden: {e.Message}");
            StartFailed(services.GetRequiredService<ILogger<CardGuarantee>>(), guarantee.Id, e.Message);
        }

        await online.SaveChangesAsync(ct);
    }

    internal static string Short(string text) => text.Length > 500 ? text[..500] : text;

    [LoggerMessage(Level = LogLevel.Error, Message = "Kartenhinterlegung {GuaranteeId} konnte nicht angelegt werden: {Error}")]
    private static partial void StartFailed(ILogger logger, Guid guaranteeId, string error);
}

/// <summary>
/// Belastet die hinterlegte Karte und verbucht die Gebühr an der Kasse des Standorts: Rechnung mit der Position „Ausfallgebühr“
/// (Steuersatz und Erlöskonto der Behandlung), bezahlt mit „Karte“ und der Referenz des Anbieters. So gibt es Beleg, Signatur und
/// Tagesabschluss wie für jeden Umsatz. Kennung der Belastung und Rechnung werden zuerst gemerkt: läuft die Nachricht erneut,
/// wird weder doppelt belastet noch doppelt verbucht.
/// </summary>
public static partial class ChargeNoShowFeeHandler
{
    public static async Task Handle(ChargeNoShowFee message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var db = services.GetRequiredService<AispaDbContext>();
        var pos = services.GetRequiredService<PosService>();
        var logger = services.GetRequiredService<ILogger<CardGuarantee>>();

        var guarantee = await online.CardGuarantees.FirstOrDefaultAsync(g => g.Id == message.GuaranteeId, ct);
        if (guarantee is null || guarantee.Status != CardGuaranteeStatus.Charging)
        {
            return;
        }

        // Im Hintergrund gibt es keinen angemeldeten Mandanten: die Kasse schreibt im Spa des Standorts.
        if (await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == guarantee.LocationId), ct) is not { } tenantId)
        {
            await FailAsync(online, guarantee, "Standort nicht gefunden.", logger, ct);
            return;
        }

        services.GetRequiredService<TenantContext>().Use(tenantId);
        var appointment = await db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
            .FirstAsync(a => a.Id == guarantee.AppointmentId, ct);
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == guarantee.LocationId, ct);
        var local = TimeZoneInfo.ConvertTime(appointment.Start, LocalTime.Zone(location.TimeZone));
        var description = $"Ausfallgebühr {appointment.Treatment.Name} {local:dd.MM.yyyy HH:mm}";

        var register = await db.CashRegisters.AsNoTracking()
            .Where(r => r.LocationId == guarantee.LocationId && r.IsActive).OrderBy(r => r.Code).FirstOrDefaultAsync(ct);
        if (register is null && guarantee.BillId is null)
        {
            await FailAsync(online, guarantee, guarantee.ChargeReference is null
                ? "An diesem Standort gibt es keine aktive Kasse; es wurde nichts belastet."
                : "Belastet, aber nicht verbucht: an diesem Standort gibt es keine aktive Kasse.", logger, ct);
            return;
        }

        if (guarantee.ChargeReference is null)
        {
            var provider = services.GetServices<IOnlinePaymentProvider>().LastOrDefault(p => p.Name == guarantee.Provider);
            CardChargeResult result;
            try
            {
                result = provider is null || guarantee.CardReference is null
                    ? new CardChargeResult(false, null, $"Zahlungsanbieter „{guarantee.Provider}“ ist nicht eingerichtet.")
                    : await provider.ChargeSavedCardAsync(new SavedCardCharge(tenantId, guarantee.Id, guarantee.CardReference, guarantee.ProviderCustomer,
                        guarantee.FeeAmount, guarantee.Currency, $"{description}, {location.Name}"), ct);
            }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException or KeyNotFoundException)
            {
                result = new CardChargeResult(false, null, e.Message);
            }

            if (!result.Succeeded)
            {
                await FailAsync(online, guarantee, $"Nicht belastet: {result.Error}", logger, ct);
                return;
            }

            guarantee.ChargeReference = result.Reference ?? $"{guarantee.Provider}-{guarantee.Id:N}";
            await online.SaveChangesAsync(ct);
        }

        if (guarantee.BillId is null)
        {
            var settings = await db.TreatmentPosSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TreatmentId == appointment.TreatmentId, ct);
            var bill = await pos.CreateBillAsync(new BillInput(register!.Id, guarantee.CustomerId, []), ct);
            if (bill.Error is { } billError)
            {
                await FailAsync(online, guarantee, $"Belastet, aber nicht verbucht: {billError.Message}", logger, ct);
                return;
            }

            var line = await pos.AddLineAsync(bill.Value!.Id, new BillLineInput(
                BillLineKind.Custom, null, null, 1, guarantee.FeeAmount, 0, description,
                settings?.TaxCategory ?? TaxCategory.Normal, settings?.RevenueAccountId ?? PosModel.Treatments.Id, null, null), supervisor: true, ct);
            if (line.Error is { } lineError)
            {
                await pos.DeleteBillAsync(bill.Value.Id, ct);
                await FailAsync(online, guarantee, $"Belastet, aber nicht verbucht: {lineError.Message}", logger, ct);
                return;
            }

            guarantee.BillId = bill.Value.Id;
            await online.SaveChangesAsync(ct);
        }

        var current = await pos.GetBillAsync(guarantee.BillId.Value, ct);
        Guid receiptId;
        if (current is { Status: BillStatus.Settled, ReceiptIds.Count: > 0 })
        {
            receiptId = current.ReceiptIds[0];
        }
        else
        {
            var receipt = await pos.CheckoutAsync(guarantee.BillId.Value, new CheckoutRequest(null,
                [new PaymentInput(PaymentMethod.Card, guarantee.FeeAmount, null, $"Online {guarantee.Provider} {guarantee.ChargeReference}")]),
                guarantee.ChargedBy, ct);
            if (receipt.Error is { } checkoutError)
            {
                await FailAsync(online, guarantee, $"Belastet, aber nicht verbucht: {checkoutError.Message}", logger, ct);
                return;
            }

            receiptId = receipt.Value!.Id;
        }

        guarantee.ReceiptId = receiptId;
        guarantee.ChargedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
        guarantee.Status = CardGuaranteeStatus.Charged;
        guarantee.Error = null;
        await online.SaveChangesAsync(ct);

        var receiptDto = await pos.GetReceiptAsync(receiptId, ct);
        var receiptText = receiptDto is null ? receiptId.ToString() : $"{receiptDto.RegisterCode} Nr. {receiptDto.Number}, {receiptDto.IssuedAt:dd.MM.yyyy}";
        await services.GetRequiredService<IMessageBus>().PublishGuestMailAsync(services.GetRequiredService<TenantContext>(), OnlineMails.NoShowFeeCharged(
            guarantee.Email, GuestName.Of(appointment.Customer, location.TimeZone), appointment.Treatment.Name, appointment.Start, location.TimeZone, location.Name,
            guarantee.FeeAmount,
            guarantee.Currency, receiptText, guarantee.Language));
    }

    private static async Task FailAsync(OnlineDbContext online, CardGuarantee guarantee, string error, ILogger logger, CancellationToken ct)
    {
        guarantee.Status = CardGuaranteeStatus.ChargeFailed;
        guarantee.Error = StartCardGuaranteeHandler.Short(error);
        await online.SaveChangesAsync(ct);
        ChargeFailed(logger, guarantee.Id, error);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ausfallgebühr {GuaranteeId} nicht abgeschlossen: {Error}")]
    private static partial void ChargeFailed(ILogger logger, Guid guaranteeId, string error);
}
