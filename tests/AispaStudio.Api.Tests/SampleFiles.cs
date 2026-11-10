namespace AispaStudio.Api.Tests;

/// <summary>
/// Legt eine im Test erzeugte Datei zum Ansehen von Hand ab (Excel in Excel oder openpyxl, vCard in Outlook, iCal im Kalender), wenn <c>AISPA_EXPORT_SAMPLES</c> auf einen
/// Ordner zeigt; sonst tut der Aufruf nichts. Muster wie <c>AISPA_SCREENSHOTS</c> der Ansichtstests.
/// </summary>
internal static class SampleFiles
{
    public static void Save(string name, byte[] content)
    {
        if (Environment.GetEnvironmentVariable("AISPA_EXPORT_SAMPLES") is not { Length: > 0 } folder)
        {
            return;
        }

        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name), content);
    }
}
