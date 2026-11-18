using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AispaStudio.Desktop.Services;

/// <summary>Was die App über diesen PC wissen muss, um ihre Voraussetzungen zu prüfen; ein Fake macht die Regeln ohne Windows testbar.</summary>
public interface ISystemFacts
{
    /// <summary>Build-Nummer von Windows (19045 ist Windows 10 22H2, ab 22000 Windows 11).</summary>
    int WindowsBuild { get; }

    bool Is64BitOperatingSystem { get; }

    /// <summary>Eingebauter Arbeitsspeicher in Byte; <c>null</c>, wenn er sich nicht ermitteln lässt.</summary>
    long? TotalMemoryBytes { get; }

    /// <summary>Freier Platz auf dem Laufwerk des Datenordners in Byte; <c>null</c>, wenn er sich nicht ermitteln lässt.</summary>
    long? FreeDiskBytes { get; }

    /// <summary>Laufwerk des Datenordners, z. B. „C:“.</summary>
    string DataDrive { get; }

    bool HasWebView2 { get; }

    bool HasDocker { get; }
}

/// <summary>Schwere eines Fundes der Startprüfung.</summary>
public enum StartupSeverity
{
    /// <summary>Die App startet nicht.</summary>
    Blocking,

    /// <summary>Die App startet; der Fund steht als Hinweis da.</summary>
    Hint,
}

/// <summary>Ein Ergebnis der Startprüfung: kurzer Titel, Text mit Problem und Weg.</summary>
public sealed record StartupFinding(StartupSeverity Severity, string Title, string Text);

/// <summary>
/// Startprüfung der Windows-App gegen die Systemvoraussetzungen (docs/installation/README.md, „Windows-PC am Empfang“). Reine Regeln über
/// <see cref="ISystemFacts"/>; installiert oder startet nichts.
/// </summary>
public static class StartupRequirements
{
    /// <summary>Windows 10 22H2.</summary>
    public const int MinimumBuild = 19045;

    /// <summary>Erste Windows-11-Build.</summary>
    public const int Windows11Build = 22000;

    public const long MinimumMemoryBytes = 4L * 1024 * 1024 * 1024;

    public const long MinimumFreeDiskBytes = 1L * 1024 * 1024 * 1024;

    private const double BytesPerGb = 1024.0 * 1024 * 1024;

    public static IReadOnlyList<StartupFinding> Check(ISystemFacts facts, bool standalone)
    {
        var findings = new List<StartupFinding>();
        if (!facts.Is64BitOperatingSystem)
        {
            findings.Add(new(StartupSeverity.Blocking, "Kein 64-Bit-Windows",
                "AISPA Studio läuft nur unter 64-Bit-Windows. Dieser PC hat ein 32-Bit-Windows. Ein PC mit 64-Bit-Windows 10 22H2 oder Windows 11 ist nötig."));
        }

        if (facts.WindowsBuild < MinimumBuild)
        {
            findings.Add(new(StartupSeverity.Blocking, "Windows ist zu alt",
                $"AISPA Studio braucht Windows 10 22H2 oder neuer. Dieser PC hat Windows Build {facts.WindowsBuild}. Windows über Windows Update auf Version 22H2 oder auf Windows 11 aktualisieren."));
        }

        if (standalone && facts.FreeDiskBytes is { } free && free < MinimumFreeDiskBytes)
        {
            findings.Add(new(StartupSeverity.Blocking, "Zu wenig freier Speicherplatz",
                $"Der Einzelplatz braucht mindestens 1 GB frei auf dem Laufwerk {facts.DataDrive}, dort liegen die Daten. Frei sind {Gb(free)} GB. Platz schaffen und AISPA Studio neu starten."));
        }

        if (facts.TotalMemoryBytes is { } memory && memory < MinimumMemoryBytes)
        {
            findings.Add(new(StartupSeverity.Hint, "Wenig Arbeitsspeicher",
                $"Dieser PC hat {Gb(memory)} GB Arbeitsspeicher. AISPA Studio braucht mindestens 4 GB und läuft hier womöglich langsam."));
        }

        if (!facts.HasWebView2)
        {
            findings.Add(new(StartupSeverity.Hint, "Hilfe nicht verfügbar",
                "Die Hilfe in der App lässt sich nicht öffnen, weil die WebView2-Runtime fehlt. Alles andere funktioniert. Die „WebView2 Runtime“ (Evergreen) bei Microsoft kostenlos laden und installieren."));
        }

        if (facts.WindowsBuild is >= MinimumBuild and < Windows11Build)
        {
            findings.Add(new(StartupSeverity.Hint, "Windows 10",
                "Für Windows 10 gibt es Sicherheitsupdates nur noch mit erweitertem Support. Für einen PC mit Kassa ist Windows 11 besser."));
        }

        return findings;
    }

    /// <summary>
    /// Der Weg, wenn der Server fehlt: nur für eine Adresse auf diesem PC (<c>localhost</c> oder <c>127.0.0.1</c>), sonst <c>null</c>;
    /// eine fremde Adresse kann auch ein Server im Netz sein, den dieser PC nicht starten kann.
    /// </summary>
    public static string? ServerAdvice(ISystemFacts facts, Uri address)
    {
        if (!address.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && address.Host != "127.0.0.1")
        {
            return null;
        }

        return facts.HasDocker
            ? "Den Server im Ordner von AISPA Studio mit „docker compose up -d“ starten, oder die App mit „--standalone“ als Einzelplatz starten."
            : "Auf diesem PC läuft kein Server von AISPA Studio. Zum Ausprobieren ohne Server die App mit „--standalone“ starten. "
              + "Für einen eigenen Server Docker Desktop installieren; die Anleitung steht unter github.com/vompa/aispa-studio, docs/installation/server.md.";
    }

    private static string Gb(long bytes) => (bytes / BytesPerGb).ToString("0.0", CultureInfo.GetCultureInfo("de-AT"));
}

/// <summary>Liest die Angaben von diesem PC.</summary>
public sealed class SystemFacts : ISystemFacts
{
    private const string WebView2ClientKey = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    private readonly string _dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudio");

    public int WindowsBuild => Environment.OSVersion.Version.Build;

    public bool Is64BitOperatingSystem => Environment.Is64BitOperatingSystem;

    public long? TotalMemoryBytes
    {
        get
        {
            // Der eingebaute Speicher (4 GB bleiben 4 GB); die Menge, die Windows meldet, ist um den Anteil der Grafik kleiner.
            if (OperatingSystem.IsWindows() && GetPhysicallyInstalledSystemMemory(out var kilobytes) && kilobytes > 0)
            {
                return kilobytes * 1024;
            }

            var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return available > 0 ? available : null;
        }
    }

    public long? FreeDiskBytes
    {
        get
        {
            try
            {
                return new DriveInfo(DataDrive).AvailableFreeSpace;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public string DataDrive => Path.GetPathRoot(_dataFolder)?.TrimEnd('\\') ?? "";

    public bool HasWebView2 => OperatingSystem.IsWindows() && WebView2Installed();

    public bool HasDocker => (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(folder => File.Exists(Path.Combine(folder, "docker.exe")));

    /// <summary>Die Runtime trägt ihre Version unter „EdgeUpdate“ ein, je Rechner (32-Bit-Sicht) oder je Benutzer.</summary>
    [SupportedOSPlatform("windows")]
    private static bool WebView2Installed()
    {
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(WebView2ClientKey);
                if (key?.GetValue("pv") is string { Length: > 0 } version && version != "0.0.0.0")
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Nicht lesbar: wie nicht vorhanden behandeln, die Hilfe öffnet sich dann ohnehin nicht.
            }
        }

        return false;
    }

    // DllImport statt LibraryImport: das bräuchte unsicheren Code im ganzen Projekt, für einen einzigen Aufruf.
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);
}
