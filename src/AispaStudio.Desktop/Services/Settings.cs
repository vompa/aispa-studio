using System.Security.AccessControl;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AispaStudio.Desktop.Services;

/// <summary>Farbschema der App: wie Windows, immer hell oder immer dunkel.</summary>
public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>Einstellungen dieses Arbeitsplatzes, die nicht an der Person hängen (z. B. Farbschema am Empfangs-PC).</summary>
/// <param name="PrinterAddress">Bondrucker im Netz, z. B. „192.168.1.60“ oder „192.168.1.60:9100“; leer = kein Drucker.</param>
/// <param name="PrinterWidth">Zeichen je Zeile: 42 für 80-mm-Papier, 32 für 58 mm.</param>
/// <param name="PrintAutomatically">Nach dem Bezahlen gleich drucken.</param>
/// <param name="TerminalId">Kartenterminal im Netz, das dieser PC ansteuert (nur ein PC je Terminal).</param>
/// <param name="Standalone">Einzelplatz-Betrieb: Datenbank und API laufen auf diesem PC (ADR 0041); gesetzt mit <c>--standalone</c>.</param>
/// <param name="BackupFolder">Einzelplatz: Ordner für die tägliche Sicherung, am besten ein zweites Laufwerk oder ein Netzordner.</param>
/// <param name="LastBackupAt">Einzelplatz: Zeitpunkt der letzten gelungenen Sicherung.</param>
/// <param name="ServerAddress">Mehrplatz: Adresse des im Netz freigegebenen Haupt-PCs, z. B. „https://empfang-pc:55443/“.</param>
/// <param name="ServerCode">Mehrplatz: Verbindungscode des Haupt-PCs; nur dessen Zertifikat gilt (<see cref="NetworkShare"/>).</param>
/// <param name="CalendarZoom">Kalender: Pixel je Minute; leer heißt „Tag einpassen“.</param>
/// <param name="CalendarAllStaff">Kalender: „Alle Mitarbeitenden“, auch Mitarbeitende ohne Dienst an diesem Standort (schraffierte Spalten mit Grund).</param>
/// <param name="PosSalesPanelOpen">Kassa: Panel „Artikel und Gutscheine“ neben der Rechnung aufgeklappt; Standard zugeklappt (schmale Leiste, Slice 137). Löst den früheren Schlüssel <c>PosSalesColumnCollapsed</c> ab, dessen Wert einmalig verfällt.</param>
/// <param name="AssistantHistory">„Zuletzt gefragt“ im Assistenten: die Fragen der angemeldeten Person von heute, nur auf diesem Gerät; nie Antworten.</param>
/// <param name="StockExportFormat">Lager › Exportieren: zuletzt gewähltes Format, „xlsx“ oder „csv“; leer heißt Excel.</param>
/// <param name="StartChoiceMade">Auf diesem PC wurde die Betriebsart schon gewählt (Startauswahl, <c>--standalone</c> oder <c>--server</c>); ohne diesen Stand erscheint die Startauswahl (ADR 0104). <c>--demo</c> setzt ihn nicht.</param>
/// <param name="CompanionPages">„KI und Hilfe“ offen oder zu je Seite (Seitenkennung wie „kalender“); ohne Eintrag gilt der letzte Stand in <c>CompanionOpen</c>.</param>
public sealed record DesktopSettings(
    ThemeChoice Theme = ThemeChoice.System,
    string? PrinterAddress = null,
    int PrinterWidth = 42,
    bool PrintAutomatically = true,
    Guid? TerminalId = null,
    bool Standalone = false,
    string? BackupFolder = null,
    DateTimeOffset? LastBackupAt = null,
    string? ServerAddress = null,
    string? ServerCode = null,
    double? CalendarZoom = null,
    bool CompanionOpen = false,
    IReadOnlyDictionary<string, bool>? CompanionPages = null,
    bool CalendarAllStaff = false,
    bool PosSalesPanelOpen = false,
    AssistantHistory? AssistantHistory = null,
    string? StockExportFormat = null,
    bool StartChoiceMade = false);

/// <summary>Die Fragen von heute für „Zuletzt gefragt“, neueste zuerst; gilt nur für <paramref name="UserId"/> und den Kalendertag <paramref name="Day"/>.</summary>
public sealed record AssistantHistory(string UserId, DateOnly Day, IReadOnlyList<string> Questions);

public interface ISettingsStore
{
    DesktopSettings Load();

    void Save(DesktopSettings settings);
}

/// <summary>Nur im Speicher (Tests, Designer).</summary>
public sealed class MemorySettingsStore : ISettingsStore, IPreparedSettingsStore
{
    private DesktopSettings _settings = new();

    public DesktopSettings Load() => _settings;

    public void Save(DesktopSettings settings) => _settings = settings;

    PreparedSettingsWrite IPreparedSettingsStore.Prepare(Func<DesktopSettings, DesktopSettings> update)
    {
        var expected = JsonSerializer.SerializeToUtf8Bytes(update(Load()), FileSettingsStore.Json);
        return new(() => Save(JsonSerializer.Deserialize<DesktopSettings>(expected, FileSettingsStore.Json)!),
            () => PreparedSettingsWrite.Verify(expected, JsonSerializer.SerializeToUtf8Bytes(Load(), FileSettingsStore.Json)));
    }
}

/// <summary>Als JSON im Benutzerprofil; eine kaputte oder fehlende Datei ergibt die Voreinstellung.</summary>
public sealed class FileSettingsStore(string path) : ISettingsStore, IPreparedSettingsStore
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    PreparedSettingsWrite IPreparedSettingsStore.Prepare(Func<DesktopSettings, DesktopSettings> update)
    {
        var target = Path.GetFullPath(path);
        var name = Path.GetFileName(target);
        if (name.Length == 0 || OperatingSystem.IsWindows() && IsWindowsDevice(target, name))
        {
            throw new IOException("Die Einstellungen benötigen einen regulären Dateipfad.");
        }

        var directory = Path.GetDirectoryName(target) ?? throw new IOException("Der Einstellungsordner fehlt.");
        Directory.CreateDirectory(directory);
        DesktopSettings previous;
        try
        {
            var attributes = File.GetAttributes(target);
            if ((attributes & (FileAttributes.Device | FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.ReadOnly)) != 0)
            {
                throw new IOException("Die Einstellungen benötigen eine beschreibbare reguläre Datei.");
            }

            if (OperatingSystem.IsWindows())
            {
                // Auch ein Leser mit Löschfreigabe kann den späteren Austausch blockieren. Vor dem Widerruf exklusiv prüfen.
                using var probe = new FileInfo(target).Create(FileMode.Open, FileSystemRights.Delete, FileShare.None, 4096, FileOptions.None, null);
            }

            using var source = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (!source.CanSeek)
            {
                throw new IOException("Die Einstellungen können von diesem Dateityp nicht gelesen werden.");
            }

            previous = JsonSerializer.Deserialize<DesktopSettings>(source, Json) ?? throw new JsonException("Leere Einstellungen.");
        }
        catch (FileNotFoundException)
        {
            previous = new();
        }

        var expected = JsonSerializer.SerializeToUtf8Bytes(update(previous), Json);
        var temporary = Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp");
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            try
            {
                stream.Write(expected);
                stream.Flush(flushToDisk: true);
            }
            catch
            {
                stream.Dispose();
                PreparedSettingsWrite.Discard(temporary);
                throw;
            }
        }

        var write = new PreparedSettingsWrite(() => File.Move(temporary, target, overwrite: true),
            () => PreparedSettingsWrite.Verify(expected, File.ReadAllBytes(target)), () => PreparedSettingsWrite.Discard(temporary));
        try
        {
            PreparedSettingsWrite.Verify(expected, File.ReadAllBytes(temporary));
            return write;
        }
        catch
        {
            write.Dispose();
            throw;
        }
    }

    private static bool IsWindowsDevice(string path, string fileName)
    {
        var stem = fileName.Split('.')[0].TrimEnd().ToUpperInvariant();
        return path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.StartsWith(@"\\?\GLOBALROOT\", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(':', StringComparison.Ordinal) || stem is "CON" or "NUL" or "AUX" or "PRN" or "CONIN$" or "CONOUT$"
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && "0123456789¹²³".Contains(stem[3], StringComparison.Ordinal);
    }

    public static FileSettingsStore Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AispaStudio", "einstellungen.json"));

    public DesktopSettings Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save(DesktopSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nicht gespeichert: beim nächsten Start gilt wieder die Voreinstellung.
        }
    }
}
