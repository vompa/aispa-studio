using Velopack;
using Velopack.Sources;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Holt Updates des Setups (ADR 0104, Entscheidung 1): prüft im Hintergrund, lädt einen neuen Stand herunter und lässt ihn
/// beim nächsten Start einspielen (<c>VelopackApp</c> wendet ein geladenes Update beim Start an). Es gibt keine Oberfläche.
/// Fehler (kein Netz, Quelle fehlt) landen im Protokoll und stören den Start nie.
/// </summary>
public sealed class AppUpdate(CrashLog log, string sourceUrl, TimeSpan startDelay)
{
    /// <summary>Feste Adresse des Releases „windows-setup“; dort liegen <c>releases.win.json</c> und das Vollpaket der neuesten Fassung.</summary>
    public const string ReleaseUrl = "https://github.com/vompa/aispa-studio/releases/download/windows-setup/";

    /// <summary>Wartezeit nach dem Start, damit Datenbank und Server nicht mit dem Download um das Netz und die Platte konkurrieren.</summary>
    private static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(30);

    public static AppUpdate Default { get; } = new(CrashLog.Default, ReleaseUrl, DefaultDelay);

    /// <summary>
    /// Ob <c>VelopackApp</c> ein geladenes Update schon beim Start einspielen darf. Das Einspielen beendet jeden Prozess im
    /// Programmordner, auch die offene App samt API und Datenbank; deshalb nicht in der Sicherung (<c>--sichern</c>) und
    /// nicht, solange eine andere Instanz der App läuft.
    /// </summary>
    public static bool MayApplyOnStartup(string[] args, Func<bool> otherAppRunning) =>
        !args.Contains(ScheduledBackup.Argument, StringComparer.OrdinalIgnoreCase) && !otherAppRunning();

    /// <summary>Tut nichts, wenn die App nicht über das Setup installiert ist (ZIP-Datei, Entwicklung, Tests).</summary>
    public async Task CheckAsync()
    {
        try
        {
            var manager = new UpdateManager(new SimpleWebSource(sourceUrl));
            if (!manager.IsInstalled)
            {
                return;
            }

            await Task.Delay(startDelay).ConfigureAwait(false);
            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is not null)
            {
                await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Bewusst alles: Netz, Quelle, Platte, gesperrte Datei. Ein Update darf nie den Start stören.
            log.Write("Update-Prüfung", ex);
        }
    }
}
