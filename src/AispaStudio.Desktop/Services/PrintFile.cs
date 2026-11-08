namespace AispaStudio.Desktop.Services;

/// <summary>
/// Kurzlebige Datei für den Druck über den Standardbrowser. Sie enthält Name, Termin und Link, nie Antworten, und liegt nur so lange, wie der Browser zum Laden braucht:
/// nach fünf Minuten löscht die App sie; was ein Absturz hinterlässt, räumt der nächste Druck weg.
/// </summary>
public static class PrintFile
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static string DefaultFolder => Path.Combine(Path.GetTempPath(), "aispa-print");

    public static Uri Write(string html, TimeProvider time, string? folder = null)
    {
        var target = folder ?? DefaultFolder;
        Directory.CreateDirectory(target);
        RemoveOld(target);
        var path = Path.Combine(target, $"{Guid.NewGuid():N}.html");
        File.WriteAllText(path, html);
        _ = DeleteLaterAsync(path, time);
        return new Uri(path);
    }

    private static void RemoveOld(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*.html"))
        {
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > Lifetime)
            {
                TryDelete(file);
            }
        }
    }

    private static async Task DeleteLaterAsync(string path, TimeProvider time)
    {
        await Task.Delay(Lifetime, time).ConfigureAwait(false);
        TryDelete(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Der Browser hält die Datei noch; der nächste Druck räumt sie weg.
        }
    }
}
