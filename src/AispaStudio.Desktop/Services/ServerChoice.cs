using System.Globalization;

namespace AispaStudio.Desktop.Services;

/// <summary>Womit die App verbunden ist.</summary>
public enum ServerSource
{
    /// <summary>Nichts eingetragen: <c>http://localhost:5086/</c>.</summary>
    Default,

    /// <summary>Umgebungsvariable <c>AISPA_API_URL</c> (Entwicklung, Aspire); hat Vorrang.</summary>
    Environment,

    /// <summary>Haupt-PC im eigenen Netz (Adresse und Verbindungscode, ADR 0049).</summary>
    MainPc,

    /// <summary>Server mit gültigem Zertifikat, in der App eingetragen (ADR 0084, Punkt 5).</summary>
    Server,
}

/// <summary>Auswahl der API-Adresse im Server-Betrieb.</summary>
public static class ServerChoice
{
    private static readonly Uri DefaultAddress = new("http://localhost:5086/");

    public sealed record Choice(Uri Address, string? Code, ServerSource Source);

    /// <summary>Adresse eines Servers aus der Eingabe: „spa.example.at“ oder vollständig mit https; unverschlüsselt (http) gibt es nicht.</summary>
    public static Uri? Normalize(string input)
    {
        var text = input.Trim();
        if (text.Length == 0 || text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host.Length == 0)
        {
            return null;
        }

        var port = uri.IsDefaultPort ? "" : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        return new Uri($"https://{uri.Host}{port}/");
    }

    /// <summary>Umgebungsvariable vor dem eingetragenen Eintrag vor <c>localhost</c>. Ein Eintrag mit Code ist ein Haupt-PC, ohne Code ein Server.</summary>
    public static Choice Resolve(DesktopSettings settings, string? environmentUrl) =>
        !string.IsNullOrWhiteSpace(environmentUrl) ? new Choice(new Uri(environmentUrl), null, ServerSource.Environment)
        : settings.ServerAddress is not { } address ? new Choice(DefaultAddress, null, ServerSource.Default)
        : settings.ServerCode is { } code ? new Choice(new Uri(address), code, ServerSource.MainPc)
        // Ein von Hand eingetragenes http:// ohne Code würde unverschlüsselt arbeiten: das gilt nicht.
        : Uri.TryCreate(address, UriKind.Absolute, out var server) && server.Scheme == Uri.UriSchemeHttps ? new Choice(server, null, ServerSource.Server)
        : new Choice(DefaultAddress, null, ServerSource.Default);
}
