namespace AispaStudio.Api.System;

/// <summary>
/// Geordnetes Beenden im Einzelplatz-Betrieb (ADR 0041): Die Windows-App startet die API ohne Konsole und kann ihr deshalb kein
/// Strg+C schicken. Stattdessen legt sie die Datei aus <c>Hosting:StopFile</c> an; die API sieht sie, beendet laufende Anfragen,
/// Hintergrundjobs und den Abgleich wie bei Strg+C und endet. Die Datei liegt im Benutzerprofil, nur das eigene Windows-Konto kann
/// sie anlegen; ein HTTP-Endpunkt dafür wäre von jedem Programm auf dem PC erreichbar.
/// </summary>
internal sealed partial class StopFileWatcher(IConfiguration configuration, IHostApplicationLifetime lifetime, ILogger<StopFileWatcher> log)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(300);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var file = configuration["Hosting:StopFile"]!;
        // Rest eines früheren Laufs, der nicht mehr dazu kam, die Datei zu löschen.
        File.Delete(file);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (File.Exists(file))
            {
                File.Delete(file);
                Stopping(log);
                lifetime.StopApplication();
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Die Windows-App beendet die API; laufende Arbeit wird abgeschlossen.")]
    private static partial void Stopping(ILogger logger);
}

internal static class StopFile
{
    public static IServiceCollection AddStopFile(this IServiceCollection services, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["Hosting:StopFile"]))
        {
            services.AddHostedService<StopFileWatcher>();
        }

        return services;
    }
}
