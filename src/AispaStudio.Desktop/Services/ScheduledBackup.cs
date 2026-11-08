using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Text;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Sicherung, während die App nicht läuft (ADR 0041, Schritt 5): Eine Windows-Aufgabe startet die App jeden Abend mit
/// <see cref="Argument"/>. Sie öffnet dann kein Fenster, sichert in den gewählten Ordner, wenn die letzte Sicherung mindestens
/// <see cref="DueAfter"/> zurückliegt, und beendet sich wieder. Die Aufgabe läuft im angemeldeten Windows-Konto, weil nur dieses die
/// Zugangsdaten der lokalen Datenbank lesen kann (DPAPI); ist der PC zur geplanten Zeit aus, holt Windows sie beim nächsten Start nach.
/// </summary>
public static class ScheduledBackup
{
    public const string Argument = "--sichern";

    /// <summary>Kürzer als ein Tag, damit eine Sicherung der App am Nachmittag die am Abend nicht um einen Tag verschiebt.</summary>
    public static readonly TimeSpan DueAfter = TimeSpan.FromHours(12);

    /// <summary>Aus <c>Program.Main</c>: ohne Fenster sichern, Ergebnis ins Protokoll, Rückgabe ist der Exit-Code.</summary>
    public static int RunFromCommandLine()
    {
        var paths = LocalServerPaths.Default;
        Directory.CreateDirectory(paths.Logs);
        using var log = new StreamWriter(Path.Combine(paths.Logs, "sicherung.log"), append: true);
        var local = new LocalServer(paths, new SystemProcessRunner(), OperatingSystem.IsWindows() ? new DpapiProtector() : new PlainProtector());
        return RunAsync(local, FileSettingsStore.Default, TimeProvider.System, log, OtherAppRunning).GetAwaiter().GetResult();
    }

    /// <summary>Sichert, wenn Einzelplatz, Ordner und Wiederherstellungsschlüssel da sind und die letzte Sicherung fällig ist.</summary>
    public static async Task<int> RunAsync(LocalServer local, ISettingsStore settings, TimeProvider time, TextWriter log, Func<bool> appRunning,
        CancellationToken ct = default)
    {
        var current = settings.Load();
        var now = time.GetUtcNow();
        if (!current.Standalone || current.BackupFolder is not { } folder || !local.HasRecoveryKey)
        {
            await Log(log, now, "Keine Sicherung eingerichtet.").ConfigureAwait(false);
            return 0;
        }

        if (current.LastBackupAt is { } last && now - last < DueAfter)
        {
            await Log(log, now, $"Nicht fällig, letzte Sicherung {last.ToString("u", CultureInfo.InvariantCulture)}.").ConfigureAwait(false);
            return 0;
        }

        try
        {
            var file = await local.BackupUnattendedAsync(folder, appRunning, ct).ConfigureAwait(false);
            settings.Save(settings.Load() with { LastBackupAt = time.GetUtcNow() });
            await Log(log, now, $"Gesichert: {file}").ConfigureAwait(false);
            return 0;
        }
        catch (LocalServerException ex)
        {
            // Die App erinnert, sobald die letzte Sicherung älter als sieben Tage ist.
            await Log(log, now, $"Fehlgeschlagen: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static Task Log(TextWriter log, DateTimeOffset at, string text) =>
        log.WriteLineAsync($"{at.ToString("u", CultureInfo.InvariantCulture)} {text}");

    /// <summary>Läuft die App schon (oder wieder), braucht sie die Datenbank; dann bleibt sie an.</summary>
    private static bool OtherAppRunning()
    {
        using var self = Process.GetCurrentProcess();
        var others = Process.GetProcessesByName(self.ProcessName);
        try
        {
            return others.Any(p => p.Id != self.Id);
        }
        finally
        {
            foreach (var process in others)
            {
                process.Dispose();
            }
        }
    }
}

/// <summary>Legt die Windows-Aufgabe für die abendliche Sicherung an (oder ersetzt sie).</summary>
public interface IBackupScheduler
{
    /// <summary>Gibt <c>null</c> zurück, wenn die Aufgabe steht, sonst den Grund, warum nicht.</summary>
    Task<string?> RegisterAsync(CancellationToken ct = default);
}

/// <param name="appPath">Die App selbst; die Aufgabe startet sie mit <see cref="ScheduledBackup.Argument"/>.</param>
/// <param name="tempFolder">Für die Beschreibung der Aufgabe, die <c>schtasks</c> als Datei braucht.</param>
public sealed class WindowsBackupScheduler(IProcessRunner runner, string appPath, string tempFolder) : IBackupScheduler
{
    public const string TaskName = "AISPA Studio Sicherung";

    /// <summary>Abends, wenn das Spa meist zu hat.</summary>
    public static readonly TimeOnly At = new(23, 0);

    /// <summary>Nur unter Windows; sonst gibt es keine Aufgabe und die App sichert, solange sie läuft.</summary>
    public static IBackupScheduler? ForThisPc() =>
        OperatingSystem.IsWindows() && Environment.ProcessPath is { } app ? new WindowsBackupScheduler(new SystemProcessRunner(), app, Path.GetTempPath()) : null;

    public async Task<string?> RegisterAsync(CancellationToken ct = default)
    {
        var file = Path.Combine(tempFolder, $"aispa-sicherung-{Guid.NewGuid():N}.xml");
        try
        {
            // UTF-16, wie es die Beschreibung angibt; schtasks liest andere Kodierungen nicht zuverlässig.
            await File.WriteAllTextAsync(file, TaskXml(appPath), Encoding.Unicode, ct).ConfigureAwait(false);
            var result = await runner.RunAsync("schtasks", ["/Create", "/TN", TaskName, "/XML", file, "/F"], ct: ct).ConfigureAwait(false);
            return result.ExitCode == 0 ? null : result.Output.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return ex.Message;
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Täglich um <see cref="At"/>, nachgeholt, wenn der PC aus war, nur im angemeldeten Konto (die Zugangsdaten sind an das Konto
    /// gebunden), auch im Akkubetrieb, höchstens zwei Stunden und nie doppelt.
    /// </summary>
    public static string TaskXml(string appPath) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>Tägliche Sicherung von AISPA Studio in den Ordner aus „Sicherung“, auch wenn die App geschlossen ist.</Description>
          </RegistrationInfo>
          <Triggers>
            <CalendarTrigger>
              <StartBoundary>2026-01-01T{At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}</StartBoundary>
              <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>
            </CalendarTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>LeastPrivilege</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <StartWhenAvailable>true</StartWhenAvailable>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <ExecutionTimeLimit>PT2H</ExecutionTimeLimit>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{SecurityElement.Escape(appPath)}</Command>
              <Arguments>{ScheduledBackup.Argument}</Arguments>
            </Exec>
          </Actions>
        </Task>
        """;
}
