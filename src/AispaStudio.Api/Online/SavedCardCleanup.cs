using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>Queue: die hinterlegte Karte dieser Hinterlegung beim Zahlungsanbieter löschen.</summary>
public sealed record RemoveSavedCard(Guid GuaranteeId);

/// <summary>
/// Sucht regelmäßig Karten, deren Termin länger als <see cref="OnlineBookingOptions.CardRetentionDays"/> vorbei ist, und lässt sie
/// beim Anbieter löschen (<c>OnlineBooking:CardCleanupPollSeconds</c>). Bis dahin kann die Kassenaufsicht die Ausfallgebühr noch
/// belasten; danach braucht niemand die Karte mehr. Nur während einer laufenden Belastung wird gewartet.
/// </summary>
internal sealed partial class SavedCardCleanup(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<OnlineBookingOptions> options,
    StartupState startup,
    ILogger<SavedCardCleanup> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.CardCleanupPollSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(60, options.Value.CardCleanupPollSeconds)));
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

    /// <summary>
    /// Stellt für jede fällige Karte eine Nachricht in die Queue und gibt deren Zahl zurück; mit <paramref name="locationId"/> nur
    /// für diesen Standort.
    /// </summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, Guid? locationId, CancellationToken ct)
    {
        var cutoff = now.AddDays(-Math.Max(0, options.Value.CardRetentionDays));
        List<(Guid Id, Guid AppointmentId, Guid LocationId)> candidates;
        await using (var scope = scopes.CreateAsyncScope())
        {
            candidates = (await scope.ServiceProvider.GetRequiredService<OnlineDbContext>().CardGuarantees.AsNoTracking()
                .Where(g => g.CardRemovedAt == null && (g.CardReference != null || g.ProviderCustomer != null)
                    && g.Status != CardGuaranteeStatus.Charging && g.SavedAt < cutoff && (locationId == null || g.LocationId == locationId))
                .Select(g => new { g.Id, g.AppointmentId, g.LocationId })
                .ToListAsync(ct)).Select(g => (g.Id, g.AppointmentId, g.LocationId)).ToList();
        }

        var queued = 0;
        foreach (var location in candidates.GroupBy(c => c.LocationId))
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            if (await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == location.Key), ct) is not { } tenantId)
            {
                continue;
            }

            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            var appointmentIds = location.Select(c => c.AppointmentId).ToList();
            var over = await db.Appointments.AsNoTracking().Where(a => appointmentIds.Contains(a.Id) && a.End <= cutoff)
                .Select(a => a.Id).ToListAsync(ct);
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            foreach (var due in location.Where(c => over.Contains(c.AppointmentId)))
            {
                await bus.PublishAsync(new RemoveSavedCard(due.Id));
                queued++;
            }
        }

        return queued;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Abgelaufene Karten konnten nicht zum Löschen eingeplant werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}

/// <summary>
/// Löscht die Karte beim Anbieter und vergisst danach ihre Kennungen. Scheitert der Anbieter, bleibt alles, wie es ist, und der
/// nächste Lauf versucht es erneut.
/// </summary>
public static partial class RemoveSavedCardHandler
{
    public static async Task Handle(RemoveSavedCard message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = services.GetRequiredService<OnlineDbContext>();
        var logger = services.GetRequiredService<ILogger<CardGuarantee>>();
        var guarantee = await online.CardGuarantees.AsNoTracking().FirstOrDefaultAsync(g => g.Id == message.GuaranteeId, ct);
        if (guarantee is null || guarantee.CardRemovedAt is not null || guarantee.Status == CardGuaranteeStatus.Charging)
        {
            return;
        }

        var db = services.GetRequiredService<AispaDbContext>();
        var tenantId = await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == guarantee.LocationId), ct) ?? Guid.Empty;
        var provider = services.GetServices<IOnlinePaymentProvider>().LastOrDefault(p => p.Name == guarantee.Provider);
        if (provider is null)
        {
            RemoveFailed(logger, guarantee.Id, $"Zahlungsanbieter „{guarantee.Provider}“ ist nicht eingerichtet.");
            return;
        }

        try
        {
            await provider.RemoveSavedCardAsync(new SavedCardRemoval(tenantId, guarantee.Id, guarantee.CardReference, guarantee.ProviderCustomer), ct);
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException)
        {
            RemoveFailed(logger, guarantee.Id, e.Message);
            return;
        }

        await online.CardGuarantees
            .Where(g => g.Id == guarantee.Id && g.CardRemovedAt == null && g.Status != CardGuaranteeStatus.Charging)
            .ExecuteUpdateAsync(u => u
                .SetProperty(g => g.CardRemovedAt, services.GetRequiredService<TimeProvider>().GetUtcNow())
                .SetProperty(g => g.CardReference, (string?)null)
                .SetProperty(g => g.ProviderCustomer, (string?)null), ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Karte der Hinterlegung {GuaranteeId} konnte beim Anbieter nicht gelöscht werden: {Error}")]
    private static partial void RemoveFailed(ILogger logger, Guid guaranteeId, string error);
}
