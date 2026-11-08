using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.System;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Abschnitt <c>Sync</c>: zentrale PostgreSQL für den Abgleich eines Einzelplatzes (ADR 0041, Schritt 4). Ohne
/// <c>CentralConnectionString</c> gibt es keinen Abgleich.
/// </summary>
public sealed class SyncOptions
{
    public const string Section = "Sync";

    /// <summary>Verbindung zur zentralen Datenbank (TLS, eigenes Konto). Nur über User-Secrets oder Umgebung, nie im Repository.</summary>
    public string? CentralConnectionString { get; set; }

    /// <summary>Name dieser Stelle im Protokoll der zentralen Seite; Standard ist der Rechnername.</summary>
    public string? SiteId { get; set; }

    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// So viele Tage bleiben Änderungen im Protokoll der zentralen Datenbank (Standard 90, 0 = nie aufräumen). Eine Stelle, die länger
    /// nicht geholt hat, und jeder neue PC holen danach den ganzen Bestand ihres Spas.
    /// </summary>
    public int CentralRetentionDays { get; set; } = 90;
}

internal static class SyncSetup
{
    public static IServiceCollection AddSync(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        services.Configure<SyncOptions>(configuration.GetSection(SyncOptions.Section));
        // Löschjob des Änderungsverlaufs (ADR 0082); läuft auch ohne Abgleich. Der Abgleich selbst gehört zur Instanz (Einzelplatz).
        services.AddSingleton<HistoryCleanup>();
        services.AddBackgroundService<HistoryCleanup>(modules);
        if (!string.IsNullOrWhiteSpace(configuration[$"{SyncOptions.Section}:CentralConnectionString"]))
        {
            services.AddHostedService<SyncWorker>();
        }

        return services;
    }
}

/// <summary>Gleicht im Abstand von <c>Sync:IntervalSeconds</c> mit der zentralen Datenbank ab; ohne Verbindung beim nächsten Mal.</summary>
internal sealed partial class SyncWorker(IConfiguration configuration, IOptions<SyncOptions> options, IOptions<HealthDataOptions> health,
    HealthKeyRing ring, StartupState startup, ILogger<SyncWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var runner = new SyncRunner(configuration.GetConnectionString("aispadb")!, settings.CentralConnectionString!,
            string.IsNullOrWhiteSpace(settings.SiteId) ? Environment.MachineName : settings.SiteId, HealthKeyExchange.From(health.Value, ring),
            TimeSpan.FromDays(settings.CentralRetentionDays));

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, settings.IntervalSeconds)));
        // Während Migration und Einspielen der Beispieldaten ändert sich die Datenbank im Takt; abgeglichen wird erst danach.
        await startup.WaitUntilReadyAsync(stoppingToken);
        do
        {
            try
            {
                var result = await runner.RunOnceAsync(stoppingToken);
                if (result.Conflicts > 0)
                {
                    ConflictsFound(log, result.Conflicts);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Failed(log, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken) && await startup.WaitUntilReadyAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Abgleich mit der zentralen Datenbank fehlgeschlagen; nächster Versuch beim nächsten Lauf.")]
    private static partial void Failed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Abgleich: {Count} Zeilen abgelehnt, siehe sync.conflicts.")]
    private static partial void ConflictsFound(ILogger logger, int count);
}
