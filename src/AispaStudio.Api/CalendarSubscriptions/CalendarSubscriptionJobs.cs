using System.Net;
using AispaStudio.Api.Data;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.CalendarSubscriptions;

internal static class CalendarSubscriptionSetup
{
    /// <summary>Kalender-Abos (ADR 0075): Abruf als Wolverine-Nachricht, der Planer sucht fällige Abos (<c>CalendarSubscriptions:PollSeconds</c>, 0 = aus).</summary>
    public static IServiceCollection AddCalendarSubscriptions(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        services.Configure<CalendarSubscriptionOptions>(configuration.GetSection(CalendarSubscriptionOptions.Section));
        services.AddScoped<CalendarSubscriptionService>();
        services.AddScoped<CalendarSubscriptionFetcher>();
        services.AddScoped<HolidayCalendar>();
        services.AddHttpClient(SubscriptionUrl.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                ConnectCallback = HttpCalendarFeedClient.Connect,
            });
        services.AddSingleton<ICalendarFeedClient, HttpCalendarFeedClient>();
        services.AddSingleton<CalendarSubscriptionScheduler>();
        services.AddBackgroundService<CalendarSubscriptionScheduler>(modules);
        return services;
    }
}

/// <summary>Queue: dieses Abo jetzt abrufen.</summary>
public sealed record FetchCalendarSubscription(Guid SubscriptionId);

public static class FetchCalendarSubscriptionHandler
{
    public static async Task Handle(FetchCalendarSubscription message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.CalendarSubscriptions.Where(s => s.Id == message.SubscriptionId), ct) is not { } tenantId)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        await scope.ServiceProvider.GetRequiredService<CalendarSubscriptionFetcher>().FetchAsync(message.SubscriptionId, ct);
    }
}

/// <summary>
/// Sucht regelmäßig fällige Abos in allen Spas und stellt den Abruf in die Queue; im November legt er je Spa einmal die Erinnerung
/// „Feiertage für das übernächste Jahr prüfen“ an. Ohne Internet bleibt die letzte Kopie gültig.
/// </summary>
internal sealed partial class CalendarSubscriptionScheduler(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<CalendarSubscriptionOptions> options,
    StartupState startup,
    ILogger<CalendarSubscriptionScheduler> log) : BackgroundService
{
    /// <summary>Bis zum Ergebnis des Abrufs gilt ein Abo als „in Arbeit“, damit es nicht doppelt in der Queue liegt; schlägt der Abruf ganz fehl, kommt es danach wieder dran.</summary>
    private static readonly TimeSpan InFlight = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PollSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30, options.Value.PollSeconds)));
        do
        {
            if (!await startup.WaitUntilReadyAsync(stoppingToken))
            {
                return;
            }

            try
            {
                await RunOnceAsync(time.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Failed(log, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Ein Durchlauf über alle Spas; liefert die Zahl der in die Queue gestellten Abrufe.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        List<Guid> tenants;
        await using (var scope = scopes.CreateAsyncScope())
        {
            tenants = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants.Select(t => t.Id).ToListAsync(ct);
        }

        var queued = 0;
        foreach (var tenantId in tenants)
        {
            queued += await RunForTenantAsync(tenantId, now, ct);
        }

        return queued;
    }

    /// <summary>Der Durchlauf für ein Spa: fällige Abos in die Queue stellen und im November die Erinnerung anlegen. Liefert die Zahl der Abrufe.</summary>
    public async Task<int> RunForTenantAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
        var due = await db.CalendarSubscriptions.Where(s => s.IsActive && s.Url != "" && (s.NextCheckAt == null || s.NextCheckAt <= now)).ToListAsync(ct);
        foreach (var sub in due)
        {
            sub.NextCheckAt = now + InFlight;
        }

        await AddNovemberReminderAsync(db, now, ct);
        await db.SaveChangesAsync(ct);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        foreach (var sub in due)
        {
            await bus.PublishAsync(new FetchCalendarSubscription(sub.Id));
        }

        return due.Count;
    }

    /// <summary>Ab November einmal je Jahr: Feiertage des übernächsten Jahres prüfen (die Abos reichen bis dahin; die Leitung bestätigt, dass Abos und Regeln stimmen).</summary>
    internal static async Task AddNovemberReminderAsync(AispaDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        if (now.Month < 11)
        {
            return;
        }

        var year = now.Year + 2;
        var key = $"reminder:{year}";
        if (await db.CalendarSubscriptionNotices.AnyAsync(n => n.Key == key, ct)
            || !await db.CalendarSubscriptions.AnyAsync(s => s.IsActive && s.Kind == SubscriptionKind.PublicHoliday, ct))
        {
            return;
        }

        db.CalendarSubscriptionNotices.Add(new CalendarSubscriptionNotice
        {
            Kind = SubscriptionNoticeKind.Reminder,
            Key = key,
            Title = $"Feiertage für {year} prüfen",
            Text = $"Die Kalender-Abos für Feiertage reichen bis {year}. Bitte prüfen, ob Abos und Feiertagsregeln für {year} stimmen (Verwaltung, Kalender-Abos, Widersprüche).",
            CreatedAt = now,
            ForLeadership = true,
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kalender-Abos konnten nicht geprüft werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}
