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
                await RemindOnceAsync(time.GetUtcNow(), null, stoppingToken);
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

    /// <summary>Unter dieser Frist lohnt keine Erinnerung: Die Bestätigung mit der Frist ist gerade erst angekommen.</summary>
    public static readonly TimeSpan ShortestRemindedHold = TimeSpan.FromHours(2);

    /// <summary>
    /// Erinnert wird einmal, wenn drei Viertel der Frist um sind (bei 24 Stunden also sechs Stunden vor der Freigabe).
    /// <paramref name="from"/> ist der Zeitpunkt der Buchung; ohne ihn oder bei einer Frist unter zwei Stunden gibt es keine Erinnerung.
    /// </summary>
    internal static bool ReminderDue(DateTimeOffset? from, DateTimeOffset releaseAt, DateTimeOffset now) =>
        // Eine Minute Spielraum: Zustimmung und Frist werden beim Buchen nacheinander gestempelt, die Frist ist darum um Millisekunden kürzer.
        from is { } start && releaseAt - start >= ShortestRemindedHold - TimeSpan.FromMinutes(1) && now < releaseAt && now >= releaseAt - ((releaseAt - start) / 4);

    /// <summary>
    /// Stellt für jeden Termin, dessen Frist für Karte oder Anzahlung bald abläuft, einmal eine Erinnerung an den Gast in die Queue
    /// und gibt deren Zahl zurück; mit <paramref name="locationId"/> nur diesen Standort.
    /// </summary>
    public async Task<int> RemindOnceAsync(DateTimeOffset now, Guid? locationId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var online = scope.ServiceProvider.GetRequiredService<OnlineDbContext>();
        var cards = (await online.CardGuarantees.AsNoTracking()
                .Where(g => g.ReleaseAt != null && g.ReleaseAt > now && g.ReminderSentAt == null
                    && (g.Status == CardGuaranteeStatus.Pending || g.Status == CardGuaranteeStatus.Failed)
                    && (locationId == null || g.LocationId == locationId))
                .Select(g => new { g.Id, g.AcceptedAt, g.ReleaseAt })
                .ToListAsync(ct))
            .Where(g => ReminderDue(g.AcceptedAt, g.ReleaseAt!.Value, now)).Select(g => g.Id).ToList();
        var deposits = (await online.Deposits.AsNoTracking()
                .Where(d => d.ReleaseAt != null && d.ReleaseAt > now && d.ReminderSentAt == null
                    && (d.Status == OnlineDepositStatus.Pending || d.Status == OnlineDepositStatus.Failed)
                    && (locationId == null || d.LocationId == locationId))
                .Select(d => new { d.Id, d.AcceptedAt, d.ReleaseAt })
                .ToListAsync(ct))
            .Where(d => ReminderDue(d.AcceptedAt, d.ReleaseAt!.Value, now)).Select(d => d.Id).ToList();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var queued = 0;
        foreach (var id in cards)
        {
            // Erst vermerken, dann senden: Ein zweiter Lauf erinnert nicht noch einmal.
            if (await online.CardGuarantees.Where(g => g.Id == id && g.ReminderSentAt == null)
                    .ExecuteUpdateAsync(u => u.SetProperty(g => g.ReminderSentAt, now), ct) == 1)
            {
                await bus.PublishAsync(new RemindUnsecuredBooking(id, Deposit: false, now));
                queued++;
            }
        }

        foreach (var id in deposits)
        {
            if (await online.Deposits.Where(d => d.Id == id && d.ReminderSentAt == null)
                    .ExecuteUpdateAsync(u => u.SetProperty(d => d.ReminderSentAt, now), ct) == 1)
            {
                await bus.PublishAsync(new RemindUnsecuredBooking(id, Deposit: true, now));
                queued++;
            }
        }

        return queued;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Online-Termine ohne Karte konnten nicht zur Freigabe eingeplant werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}

/// <summary>Queue: den Gast erinnern, dass für seinen Termin noch die Karte (oder mit <c>Deposit</c> die Anzahlung) fehlt und die Frist bald abläuft.</summary>
public sealed record RemindUnsecuredBooking(Guid Id, bool Deposit, DateTimeOffset At);

/// <summary>
/// Schickt die Erinnerung mit Frist und Link zum Termin. Ist inzwischen hinterlegt, bezahlt, storniert oder die Frist vorbei, geht nichts hinaus.
/// </summary>
public static class RemindUnsecuredBookingHandler
{
    public static async Task Handle(RemindUnsecuredBooking message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var db = services.GetRequiredService<AispaDbContext>();
        Guid appointmentId, locationId;
        string email, language;
        DateTimeOffset releaseAt;
        if (message.Deposit)
        {
            var deposit = await online.Deposits.AsNoTracking().FirstOrDefaultAsync(d => d.Id == message.Id, ct);
            if (deposit is not { ReleaseAt: { } until, Status: OnlineDepositStatus.Pending or OnlineDepositStatus.Failed })
            {
                return;
            }

            (appointmentId, locationId, email, language, releaseAt) = (deposit.AppointmentId, deposit.LocationId, deposit.Email, deposit.Language, until);
        }
        else
        {
            var guarantee = await online.CardGuarantees.AsNoTracking().FirstOrDefaultAsync(g => g.Id == message.Id, ct);
            if (guarantee is not { ReleaseAt: { } until, Status: CardGuaranteeStatus.Pending or CardGuaranteeStatus.Failed })
            {
                return;
            }

            (appointmentId, locationId, email, language, releaseAt) = (guarantee.AppointmentId, guarantee.LocationId, guarantee.Email, guarantee.Language, until);
        }

        if (releaseAt <= message.At || await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == locationId), ct) is not { } tenantId)
        {
            return;
        }

        var tenants = services.GetRequiredService<TenantContext>();
        tenants.Use(tenantId);
        var portal = services.GetRequiredService<OnlinePortal>();
        var appointment = await portal.LoadAppointmentAsync(appointmentId, ct);
        if (appointment is null || appointment.Status != AppointmentStatus.Booked || appointment.Start <= message.At)
        {
            return;
        }

        var shown = await portal.ToOnlineAsync(appointment, ct);
        var record = await online.OnlineBookings.AsNoTracking().FirstOrDefaultAsync(b => b.AppointmentId == appointmentId, ct);
        var token = services.GetRequiredService<AccessLinks>().Create(AccessLinkPurpose.ManageAppointment, appointment.CustomerId, appointmentId, appointment.End.AddDays(30));
        await online.SaveChangesAsync(ct);
        await services.GetRequiredService<IMessageBus>().PublishGuestMailAsync(tenants, OnlineMails.HoldReminder(
            email, GuestName.Of(appointment.Customer, shown.TimeZone), shown, releaseAt, portal.Url($"termin/{token}", tenantId),
            record?.Language ?? language, message.Deposit));
    }
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
