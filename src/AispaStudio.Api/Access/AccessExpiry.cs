using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Access;

/// <summary>Abschnitt <c>AccessExpiry</c>: wann vor dem Ende eines befristeten Zugangs Bescheid gegeben wird (PollSeconds 0 = gar nicht).</summary>
public sealed class AccessExpiryOptions
{
    public const string Section = "AccessExpiry";

    public int PollSeconds { get; set; } = 3600;

    public int NoticeDays { get; set; } = 7;
}

internal static class AccessExpirySetup
{
    public static IServiceCollection AddAccessExpiry(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        services.Configure<AccessExpiryOptions>(configuration.GetSection(AccessExpiryOptions.Section));
        services.AddScoped<AccessExpiryPlanner>();
        services.AddSingleton<AccessExpiryScheduler>();
        services.AddBackgroundService<AccessExpiryScheduler>(modules);
        return services;
    }
}

/// <summary>Sucht regelmäßig in allen Spas befristete Zugänge, die bald enden (<c>AccessExpiry:PollSeconds</c>).</summary>
internal sealed partial class AccessExpiryScheduler(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<AccessExpiryOptions> options,
    StartupState startup,
    ILogger<AccessExpiryScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PollSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(60, options.Value.PollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken) && await startup.WaitUntilReadyAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(time.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Failed(log, ex);
            }
        }
    }

    /// <summary>Ein Durchlauf über alle nicht gesperrten Spas; liefert die Zahl der Zugänge, zu denen Bescheid ging.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        List<Guid> tenants;
        await using (var scope = scopes.CreateAsyncScope())
        {
            // Gesperrte Spas können nichts verlängern; ein Hinweis hilft dort nicht.
            tenants = await scope.ServiceProvider.GetRequiredService<AispaDbContext>().Tenants
                .Where(t => t.LockedAt == null).Select(t => t.Id).ToListAsync(ct);
        }

        var noticed = 0;
        foreach (var tenantId in tenants)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);
            noticed += await scope.ServiceProvider.GetRequiredService<AccessExpiryPlanner>().PlanAsync(now, ct);
        }

        return noticed;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hinweise vor Ablauf befristeter Zugänge konnten nicht verschickt werden.")]
    private static partial void Failed(ILogger logger, Exception ex);
}

/// <summary>
/// Schreibt der Person und allen, die ihren Zugang verwalten dürfen, einmal je Ende eine Mail, bevor ein befristeter Zugang endet.
/// So verlängert die Leitung rechtzeitig, und die Aushilfe steht am letzten Tag nicht überrascht vor der Anmeldung.
/// </summary>
internal sealed class AccessExpiryPlanner(AispaDbContext db, IMessageBus bus, IOptions<AccessExpiryOptions> options)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public async Task<int> PlanAsync(DateTimeOffset now, CancellationToken ct)
    {
        var horizon = now.AddDays(Math.Max(1, options.Value.NoticeDays));
        var due = await db.StaffAccess.AsNoTracking()
            .Where(a => a.IsActive && a.ExpiresAt > now && a.ExpiresAt <= horizon
                && (a.ExpiryNoticeFor == null || a.ExpiryNoticeFor != a.ExpiresAt))
            .ToListAsync(ct);
        if (due.Count == 0)
        {
            return 0;
        }

        var all = await (
                from a in db.StaffAccess.AsNoTracking()
                join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                where a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now)
                select new { Access = a, u.Email, u.DisplayName })
            .ToListAsync(ct);
        var ids = due.Select(d => d.UserId).ToList();
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);
        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct);
        var spa = await db.Tenants.AsNoTracking().Select(t => t.Name).FirstAsync(ct);

        var noticed = 0;
        foreach (var access in due)
        {
            // Erst beanspruchen, dann senden: zwei Läufe zugleich schicken so nicht doppelt.
            var claimed = await db.StaffAccess
                .Where(a => a.UserId == access.UserId && a.ExpiresAt == access.ExpiresAt
                    && (a.ExpiryNoticeFor == null || a.ExpiryNoticeFor != a.ExpiresAt))
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.ExpiryNoticeFor, a => a.ExpiresAt), ct);
            if (claimed == 0 || !people.TryGetValue(access.UserId, out var person))
            {
                continue;
            }

            var places = access.AllLocations ? locations : locations.Where(l => access.LocationIds.Contains(l.Id)).ToList();
            var zone = LocalTime.Zone(places.FirstOrDefault()?.TimeZone ?? locations.FirstOrDefault()?.TimeZone ?? "Europe/Vienna");
            var lastDay = LocalTime.LocalDate(access.ExpiresAt!.Value.AddTicks(-1), zone).ToString("dddd, d. MMMM yyyy", German);
            var where = access.AllLocations || places.Count == 0 ? "alle Standorte" : string.Join(", ", places.Select(l => l.Name));

            if (!string.IsNullOrWhiteSpace(person.Email))
            {
                await bus.PublishAsync(new SendMail(new OutgoingMail(person.Email,
                    $"Dein Zugang bei {spa} endet bald",
                    $"""
                    Hallo {person.DisplayName},

                    dein Zugang zu AISPA Studio bei {spa} ist befristet und gilt bis einschließlich {lastDay}. Danach ist keine Anmeldung mehr möglich.

                    Brauchst du ihn länger, sag bitte deiner Standortleitung Bescheid; sie kann ihn unter „Zugänge“ verlängern.
                    """,
                    FromName: spa)));
            }

            var managers = all
                .Where(m => m.Access.UserId != access.UserId && !string.IsNullOrWhiteSpace(m.Email)
                    && ToUserAccess(m.Access).CanManage(access.Role, access.AllLocations, access.LocationIds))
                .ToList();
            foreach (var manager in managers)
            {
                await bus.PublishAsync(new SendMail(new OutgoingMail(manager.Email!,
                    $"Zugang von {person.DisplayName} endet bald",
                    $"""
                    Hallo {manager.DisplayName},

                    der befristete Zugang von {person.DisplayName} ({person.Email}, {RoleLabel(access.Role)}, {where}) gilt bis einschließlich {lastDay}.

                    Soll er länger gelten, ändere das Datum unter „Zugänge“ in AISPA Studio. Sonst ist nichts zu tun: der Zugang endet von selbst.
                    """,
                    FromName: spa)));
            }

            noticed++;
        }

        return noticed;
    }

    private static UserAccess ToUserAccess(StaffAccess a) => new(a.UserId, a.Role, a.AllLocations, a.LocationIds.ToHashSet(), null, a.AmountReports, a.PlainReports);

    private static string RoleLabel(StaffRole role) => role switch
    {
        StaffRole.Owner => "Inhaber",
        StaffRole.GeneralManager => "Manager",
        StaffRole.Manager => "Standortleitung",
        StaffRole.Reception => "Rezeption",
        StaffRole.Therapist => "Therapeut:in",
        StaffRole.Backoffice => "Backoffice",
        _ => role.ToString(),
    };
}
