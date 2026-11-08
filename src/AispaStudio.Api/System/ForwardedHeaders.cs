using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.System;

/// <summary>Ob und wie die Instanz weitergeleitete Kopfzeilen von Proxys auswertet (<c>Hosting:ForwardedHeaders</c>, ADR 0087).</summary>
internal enum ForwardedHeadersMode
{
    /// <summary>Standard: Adresse und Schema der Verbindung gelten, wie bisher.</summary>
    Off,

    /// <summary>Eigener Schalter an: <c>X-Forwarded-For</c> und <c>X-Forwarded-Proto</c> gelten, aber nur von vertrauten Proxys.</summary>
    On,

    /// <summary>
    /// Eigener Schalter und Schlüssel von ASP.NET Core zugleich (Azure App Service unter Linux setzt ihn selbst). Den Startfilter von ASP.NET Core
    /// gibt es dann schon; ein zweites <c>UseForwardedHeaders</c> ließe die Middleware doppelt laufen und verdoppelte <c>ForwardLimit</c>.
    /// Der Startfilter nutzt die Optionen, die <c>AddForwardedHeaders</c> ersetzt hat.
    /// </summary>
    OnViaAspNetKey,

    /// <summary>
    /// Nur der Schlüssel von ASP.NET Core ist gesetzt. Er leert die Listen der vertrauten Proxys, ASP.NET Core vertraut dann jedem Absender.
    /// Das lässt sich nicht abschalten, der Start meldet es.
    /// </summary>
    AspNetKeyWithoutSwitch,
}

/// <summary>
/// Registrierung und Pipeline für <c>X-Forwarded-For</c> und <c>X-Forwarded-Proto</c> hinter Proxy und Ingress. Ohne die Einstellung bleibt alles wie heute.
/// Vertraut wird nur <c>KnownProxies</c> und <c>KnownNetworks</c>, ohne Angaben nur Loopback; „jedem vertrauen“ gibt es nicht.
/// <c>X-Forwarded-Host</c> wertet die API nicht aus: nginx reicht den <c>Host</c> des Clients ohnehin unverändert weiter.
/// </summary>
internal static partial class ForwardedHeadersSetup
{
    private const string Section = "Hosting:ForwardedHeaders";

    private const int MinimumPrefixLength = 8;

    /// <summary>Der Schlüssel von ASP.NET Core selbst (Umgebungsvariable <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> oder jede Konfigurationsquelle).</summary>
    private const string AspNetKey = "ForwardedHeaders_Enabled";

    /// <summary>
    /// Liest den Abschnitt und prüft ihn beim Start, auch beim Befehl <c>migrate</c>: ein ungültiger Eintrag bricht mit einer Meldung ab, die den Wert nennt.
    /// Der Einzelplatz prüft „nur vom eigenen PC“ an der Adresse der Verbindung; deshalb bricht er mit dem eigenen Schalter und mit dem
    /// Schlüssel von ASP.NET Core ab, denn mit einem gefälschten <c>X-Forwarded-For: 127.0.0.1</c> ließe sich die Prüfung aushebeln.
    /// </summary>
    public static ForwardedHeadersMode AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        var enabled = EnabledFrom(configuration);
        var aspNetKey = string.Equals(configuration[AspNetKey], "true", StringComparison.OrdinalIgnoreCase);
        if (modules.Standalone && (enabled || aspNetKey))
        {
            throw new InvalidOperationException(
                "Der Einzelplatz läuft nie hinter einem Proxy; Hosting:Standalone und Hosting:ForwardedHeaders:Enabled bzw. ForwardedHeaders_Enabled "
                + "(ASPNETCORE_FORWARDEDHEADERS_ENABLED) schließen sich aus, sonst ließe sich die Prüfung „nur vom eigenen PC“ mit einem gefälschten "
                + "X-Forwarded-For umgehen. Die Einstellung weglassen oder Hosting:Standalone ausschalten.");
        }

        if (!enabled)
        {
            return aspNetKey ? ForwardedHeadersMode.AspNetKeyWithoutSwitch : ForwardedHeadersMode.Off;
        }

        var networks = ListOf(configuration, "KnownNetworks").Select(ParseNetwork).ToList();
        var proxies = ListOf(configuration, "KnownProxies").Select(ParseProxy).ToList();
        var limit = LimitFrom(configuration);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = limit;
            // Auch leeren, wenn der Schlüssel von ASP.NET Core die Listen schon geleert hat (= jeder Absender): ohne Angaben gilt der Standard, nur Loopback.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            if (networks.Count == 0 && proxies.Count == 0)
            {
                options.KnownProxies.Add(IPAddress.IPv6Loopback);
                options.KnownIPNetworks.Add(new global::System.Net.IPNetwork(IPAddress.Loopback, 8));
            }

            networks.ForEach(options.KnownIPNetworks.Add);
            proxies.ForEach(options.KnownProxies.Add);
        });
        return aspNetKey ? ForwardedHeadersMode.OnViaAspNetKey : ForwardedHeadersMode.On;
    }

    /// <summary>
    /// Erste Middleware der Anwendung, noch vor der Fehlerbehandlung: Rate Limiting, Cookies und Signaturadressen sehen die Werte des Clients.
    /// <c>WebApplication</c> setzt <c>UseRouting</c> automatisch davor; Endpunkte mit <c>ShortCircuit</c> (Health in den Service-Defaults) sehen die unveränderten Werte.
    /// </summary>
    public static void UseConfiguredForwardedHeaders(this WebApplication app, ForwardedHeadersMode mode)
    {
        switch (mode)
        {
            case ForwardedHeadersMode.On:
                var options = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
                var networks = string.Join(", ", options.KnownIPNetworks);
                var proxies = string.Join(", ", options.KnownProxies);
                Trusting(app.Logger, networks, proxies, options.ForwardLimit ?? 0);
                app.UseForwardedHeaders();
                break;
            case ForwardedHeadersMode.OnViaAspNetKey:
                ViaStartupFilter(app.Logger);
                break;
            case ForwardedHeadersMode.AspNetKeyWithoutSwitch:
                AspNetKeyIsOpen(app.Logger);
                break;
        }
    }

    private static bool EnabledFrom(IConfiguration configuration)
    {
        var value = configuration[$"{Section}:Enabled"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return bool.TryParse(value.Trim(), out var enabled)
            ? enabled
            : throw new InvalidOperationException($"{Section}:Enabled „{value}“ ist ungültig; erlaubt sind true und false.");
    }

    private static int LimitFrom(IConfiguration configuration)
    {
        var value = configuration[$"{Section}:ForwardLimit"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return 1;
        }

        return int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var limit) && limit >= 1
            ? limit
            : throw new InvalidOperationException(
                $"{Section}:ForwardLimit „{value}“ ist ungültig; erlaubt ist eine ganze Zahl ab 1, die Zahl der Proxys vor der API (Standard 1).");
    }

    /// <summary>Mehrere Einträge einzeln (<c>:0</c>, <c>:1</c>) oder, bequem in Umgebungsvariablen, als ein Text mit Kommas.</summary>
    private static IEnumerable<string> ListOf(IConfiguration configuration, string name)
    {
        var section = configuration.GetSection($"{Section}:{name}");
        return new[] { section.Value }.Concat(section.GetChildren().Select(c => c.Value))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .SelectMany(v => v!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Ein Netz kürzer als /8 (auch <c>0.0.0.0/0</c> und <c>::/0</c>) hieße, praktisch jedem Absender zu vertrauen.</summary>
    private static global::System.Net.IPNetwork ParseNetwork(string value)
    {
        if (!global::System.Net.IPNetwork.TryParse(value, out var network))
        {
            throw new InvalidOperationException(
                $"{Section}:KnownNetworks „{value}“ ist kein Netz in CIDR-Schreibweise; erwartet wird zum Beispiel 10.0.0.0/8 (Adresse ohne Hostanteil, dann /Präfixlänge).");
        }

        return network.PrefixLength >= MinimumPrefixLength
            ? network
            : throw new InvalidOperationException(
                $"{Section}:KnownNetworks „{value}“ ist zu weit; erlaubt sind Netze ab /{MinimumPrefixLength}. Nur das Netz des Proxys eintragen, nicht jedem vertrauen.");
    }

    /// <summary><c>IPAddress.TryParse</c> nähme auch „1“ als 0.0.0.1 an; eine Adresse hat Punkte oder Doppelpunkte.</summary>
    private static IPAddress ParseProxy(string value) =>
        (value.Contains('.', StringComparison.Ordinal) || value.Contains(':', StringComparison.Ordinal)) && IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException($"{Section}:KnownProxies „{value}“ ist keine IP-Adresse; erwartet wird zum Beispiel 10.0.0.5. Netze gehören nach KnownNetworks.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Weitergeleitete Kopfzeilen: vertraut werden die Netze [{Networks}] und die Proxys [{Proxies}], ForwardLimit {Limit}")]
    private static partial void Trusting(ILogger logger, string networks, string proxies, int limit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Hosting:ForwardedHeaders:Enabled und ForwardedHeaders_Enabled (ASPNETCORE_FORWARDEDHEADERS_ENABLED) sind beide gesetzt: Der Startfilter von ASP.NET Core "
            + "hängt die Middleware ein, mit den Netzen und Proxys aus Hosting:ForwardedHeaders. Die Variable lässt sich entfernen.")]
    private static partial void ViaStartupFilter(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ForwardedHeaders_Enabled (ASPNETCORE_FORWARDEDHEADERS_ENABLED) ist gesetzt: ASP.NET Core vertraut damit den Kopfzeilen X-Forwarded-* jedes Absenders. "
            + "Die Variable entfernen und Hosting:ForwardedHeaders:Enabled mit KnownNetworks verwenden.")]
    private static partial void AspNetKeyIsOpen(ILogger logger);
}
