using System.Globalization;

namespace AispaStudio.AppHost;

/// <summary>Namen und Ablage der Datenbanksicherungen (reine Logik, ohne Aspire; daher testbar).</summary>
internal static class DatabaseBackupFiles
{
    public const string FolderName = ".aispa-backups";
    public const string Prefix = "aispadb-";
    public const string Extension = ".dump";

    public static string CreateFileName(DateTime now) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}{now:yyyy-MM-dd-HHmmss}{Extension}");

    /// <summary>Dateinamen der Sicherungen, neueste zuerst (der Zeitstempel im Namen sortiert chronologisch).</summary>
    public static IReadOnlyList<string> ListNewestFirst(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, $"{Prefix}*{Extension}")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderByDescending(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Liefert den vollen Pfad der gewählten Sicherung, ohne Auswahl der neuesten.
    /// Gibt <see langword="null"/> mit Fehlertext zurück, wenn es keine passende Datei gibt.
    /// </summary>
    public static (string? Path, string? Error) Resolve(string folder, string? selectedName)
    {
        var available = ListNewestFirst(folder);
        if (available.Count == 0)
        {
            return (null, $"Keine Sicherung vorhanden (Ordner {FolderName}).");
        }

        var name = string.IsNullOrWhiteSpace(selectedName) ? available[0] : selectedName;
        if (!available.Contains(name, StringComparer.Ordinal))
        {
            return (null, $"Die Sicherung „{name}“ gibt es nicht im Ordner {FolderName}.");
        }

        return (System.IO.Path.Combine(folder, name), null);
    }

    public static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => string.Create(CultureInfo.GetCultureInfo("de-AT"), $"{bytes} Byte"),
            < 1024 * 1024 => string.Create(CultureInfo.GetCultureInfo("de-AT"), $"{bytes / 1024.0:0.#} KB"),
            _ => string.Create(CultureInfo.GetCultureInfo("de-AT"), $"{bytes / 1024.0 / 1024.0:0.#} MB"),
        };
}
