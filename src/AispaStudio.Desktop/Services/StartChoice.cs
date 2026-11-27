namespace AispaStudio.Desktop.Services;

/// <summary>
/// Wann die Startauswahl erscheint (ADR 0104, Entscheidung 2): auf einem PC, auf dem noch keine Betriebsart gewählt wurde. Die Regel kennt nur
/// Eingaben, die der Start schon hat; ob auf diesem PC ein Server antwortet, prüft die App danach (<c>MainViewModel</c>).
/// </summary>
public static class StartChoice
{
    private static readonly string[] Switches = ["--standalone", "--server", "--demo"];

    /// <summary>Auf diesem PC ist die Betriebsart gewählt: ausdrücklich gemerkt oder durch Bestand (Einzelplatz, gespeicherte Verbindung).</summary>
    public static bool IsMade(DesktopSettings settings) =>
        settings.StartChoiceMade || settings.Standalone || !string.IsNullOrWhiteSpace(settings.ServerAddress);

    /// <summary>Kein Schalter, keine Adresse aus <c>AISPA_API_URL</c> und noch nichts gewählt.</summary>
    public static bool IsDue(IReadOnlyList<string> args, DesktopSettings settings, string? apiUrl) =>
        !args.Any(a => Switches.Contains(a, StringComparer.OrdinalIgnoreCase))
        && string.IsNullOrWhiteSpace(apiUrl)
        && !IsMade(settings);
}
