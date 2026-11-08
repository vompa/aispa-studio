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
    AssistantHistory? AssistantHistory = null);

/// <summary>Die Fragen von heute für „Zuletzt gefragt“, neueste zuerst; gilt nur für <paramref name="UserId"/> und den Kalendertag <paramref name="Day"/>.</summary>
public sealed record AssistantHistory(string UserId, DateOnly Day, IReadOnlyList<string> Questions);

public interface ISettingsStore
{
    DesktopSettings Load();

    void Save(DesktopSettings settings);
}

/// <summary>Nur im Speicher (Tests, Designer).</summary>
public sealed class MemorySettingsStore : ISettingsStore
{
    private DesktopSettings _settings = new();

    public DesktopSettings Load() => _settings;

    public void Save(DesktopSettings settings) => _settings = settings;
}

/// <summary>Als JSON im Benutzerprofil; eine kaputte oder fehlende Datei ergibt die Voreinstellung.</summary>
public sealed class FileSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

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
