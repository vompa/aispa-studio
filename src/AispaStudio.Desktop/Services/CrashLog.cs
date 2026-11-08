using System.Globalization;

namespace AispaStudio.Desktop.Services;

/// <summary>
/// Protokoll für Fehler, die niemand abgefangen hat (<c>protokolle\app.log</c> im Datenordner, neben <c>api.log</c> und <c>datenbank.log</c>).
/// Das Schreiben wirft nie: ein Fehler beim Protokollieren darf den Fehler, um den es geht, nicht noch verdecken.
/// </summary>
public sealed class CrashLog(string file, TimeProvider time)
{
    private readonly Lock _writing = new();

    public static CrashLog Default { get; } = new(Path.Combine(LocalServerPaths.Default.Logs, "app.log"), TimeProvider.System);

    public string File { get; } = file;

    /// <summary>Hängt Zeitpunkt, Herkunft und den ganzen Fehler (mit inneren Fehlern und Stapel) an.</summary>
    public void Write(string source, Exception? error)
    {
        var entry = $"{time.GetUtcNow().ToString("u", CultureInfo.InvariantCulture)} {source}{Environment.NewLine}{error}{Environment.NewLine}{Environment.NewLine}";
        try
        {
            lock (_writing)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(File)!);
                System.IO.File.AppendAllText(File, entry);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Protokoll nicht beschreibbar (Platte voll, gesperrt): der eigentliche Fehler zählt mehr.
        }
    }
}
