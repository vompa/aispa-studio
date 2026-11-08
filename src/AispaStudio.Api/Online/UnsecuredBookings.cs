using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>Queue: diesen online gebuchten Termin freigeben, weil bis <c>At</c> (Zeitpunkt des Laufs) keine Karte hinterlegt wurde.</summary>
public sealed record ReleaseUnsecuredBooking(Guid GuaranteeId, DateTimeOffset At);

/// <summary>
/// Sucht regelmäßig Online-Termine, für die bis <see cref="CardGuarantee.ReleaseAt"/> keine Karte hinterlegt oder bis
/// <see cref="DepositOrder.ReleaseAt"/> keine Anzahlung bezahlt wurde (<c>OnlineBooking:CardHoldPollSeconds</c>). Die Frist ist die Regel des Standorts beim Buchen (<c>CardHoldHours</c>).
/// </summary>
internal sealed partial class UnsecuredBookingRelease(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<OnlineBookingOptions> options,
    StartupState startup,
    ILogger<UnsecuredBookingRelease> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.CardHoldPollSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, options.Value.CardHoldPollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken) && await startup.WaitUntilReadyAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(time.GetUtcNow(), null, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Failed(log, ex);
            }
        }
    }

    /// <summary>Stellt jeden fälligen Termin in die Queue und gibt deren Zahl zurück; mit <paramref name="locationId"/> nur diesen Standort.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, Guid? locationId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var due = await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().CardGuarantees.AsNoTracking()
            .Where(g => g.ReleaseAt != null && g.ReleaseAt <= now
                && (g.Status == CardGuaranteeStatus.Pending || g.Status == CardGuaranteeStatus.Failed)
                && (locationId == null || g.LocationId == locationId))
            .Select(g => g.Id)
            .ToListAsync(ct);
        var unpaid = await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().Deposits.AsNoTracking()
            .Where(d => d.ReleaseAt != null && d.ReleaseAt <= now
                && (d.Status == OnlineDepositStatus.Pending || d.Status == OnlineDepositStatus.Failed)
                && (locationId == null || d.LocationId == locationId))
            .Select(d => d.Id)
            .ToListAsync(ct);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        foreach (var id in due)
        {
            await bus.PublishAsync(new ReleaseUnsecuredBooking(id, now));
        }

        foreach (var id in unpaid)
        {
            await bus.PublishAsync(new ReleaseUnpaidDeposit(id, now));
        }

        return due.Count + unpaid.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Online-Termine ohne Karte konnten nicht zur Freigabe eingeplant werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}

/// <summary>
/// Gibt den Termin frei: Zuerst wechselt die Hinterlegung auf „freigegeben“ (nur aus „ausstehend“ oder „abgebrochen“), damit eine
/// gleichzeitig gemeldete Karte gewinnt; dann wird storniert, die Warteliste rückt nach, und der Gast bekommt eine Mail.
/// </summary>
public static class ReleaseUnsecuredBookingHandler
{
    public static async Task Handle(ReleaseUnsecuredBooking message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var db = services.GetRequiredService<AispaDbContext>();
        var now = message.At;
        var guarantee = await online.CardGuarantees.AsNoTracking().FirstOrDefaultAsync(g => g.Id == message.GuaranteeId, ct);
        if (guarantee is not { ReleaseAt: { } releaseAt } || releaseAt > now
            || await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == guarantee.LocationId), ct) is not { } tenantId)
        {
            return;
        }

        var tenants = services.GetRequiredService<TenantContext>();
        tenants.Use(tenantId);
        var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == guarantee.AppointmentId, ct);
        if (appointment is null || appointment.Status != AppointmentStatus.Booked || appointment.Start <= now)
        {
            return;
        }

        var claimed = await online.CardGuarantees
            .Where(g => g.Id == guarantee.Id && (g.Status == CardGuaranteeStatus.Pending || g.Status == CardGuaranteeStatus.Failed))
            .ExecuteUpdateAsync(u => u.SetProperty(g => g.Status, CardGuaranteeStatus.Released).SetProperty(g => g.SetupUrl, (string?)null), ct);
        if (claimed == 0)
        {
            return;
        }

        var booking = services.GetRequiredService<BookingService>();
        if (await booking.CancelAsync(appointment.Id, "Freigegeben: keine Karte für die Ausfallgebühr hinterlegt", ct) is not BookingOutcome.Success)
        {
            // Termin wurde gerade geändert: Hinterlegung zurücksetzen, der nächste Lauf versucht es erneut.
            await online.CardGuarantees.Where(g => g.Id == guarantee.Id && g.Status == CardGuaranteeStatus.Released)
                .ExecuteUpdateAsync(u => u.SetProperty(g => g.Status, CardGuaranteeStatus.Pending), ct);
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
            guarantee.Email, GuestName.Of(customer, cancelled.TimeZone), cancelled, portal.Url("", tenantId), record?.Language ?? guarantee.Language));
    }
}
