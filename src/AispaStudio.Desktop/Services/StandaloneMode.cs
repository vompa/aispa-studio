namespace AispaStudio.Desktop.Services;

/// <summary>
/// Betriebsart der Windows-App (ADR 0041). <c>--standalone</c> schaltet den Einzelplatz-Betrieb ein und merkt ihn sich,
/// <c>--server</c> schaltet zurück auf den Server-Betrieb. Ohne Schalter gilt der gemerkte Stand.
/// </summary>
public static class StandaloneMode
{
    public static bool Resolve(IReadOnlyList<string> args, ISettingsStore settings)
    {
        var current = settings.Load();
        bool? requested = args.Contains("--standalone", StringComparer.OrdinalIgnoreCase) ? true
            : args.Contains("--server", StringComparer.OrdinalIgnoreCase) ? false
            : null;
        if (requested is { } value && value != current.Standalone)
        {
            settings.Save(current with { Standalone = value });
            return value;
        }

        return current.Standalone;
    }
}
