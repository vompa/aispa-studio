using AispaStudio.Api.System;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace AispaStudio.Api.Live;

/// <summary>Wie die Instanzen ihre Live-Meldungen untereinander austauschen (<c>Live:Backplane</c>, ADR 0087).</summary>
internal enum LiveBackplaneMode
{
    /// <summary>Standard, wie bisher: eine Meldung erreicht nur die Clients, die an derselben Instanz hängen.</summary>
    None,

    /// <summary>Redis (Pub/Sub) verteilt jede Meldung an alle Instanzen, auch an den Worker und von ihm aus.</summary>
    Redis,
}

/// <summary>
/// Registrierung der SignalR-Backplane. Ohne die Einstellung bleibt alles wie heute. Der Aufruf gilt für jede Rolle: auch der Worker
/// schickt Meldungen (Postfach-Abruf, Freigabe ungesicherter Buchungen), die sonst an keinen Client kämen.
/// </summary>
internal static partial class LiveBackplane
{
    private const string Key = "Live:Backplane";

    private const string ConnectionName = "redis";

    private const int ConnectTimeoutMilliseconds = 1000;

    private const int CommandTimeoutMilliseconds = 2000;

    /// <summary>Liest den Schalter und prüft ihn beim Start, auch beim Befehl <c>migrate</c>: Ein ungültiger Wert bricht mit einer deutschen Meldung ab.</summary>
    public static LiveBackplaneMode AddLiveBackplane(this ISignalRServerBuilder signalR, IConfiguration configuration, ModuleSwitches modules)
    {
        var mode = ModeFrom(configuration);
        if (mode == LiveBackplaneMode.None)
        {
            return mode;
        }

        if (modules.Standalone)
        {
            throw new InvalidOperationException(
                "Der Einzelplatz hat nur eine Instanz und keinen Redis; Hosting:Standalone und Live:Backplane=Redis schließen sich aus. "
                + "Live:Backplane weglassen oder Hosting:Standalone ausschalten.");
        }

        var connection = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                "Live:Backplane=Redis braucht die Verbindungszeichenfolge ConnectionStrings:redis (Umgebungsvariable ConnectionStrings__redis), "
                + "zum Beispiel redis-host:6379,password=… Der Worker braucht denselben Redis.");
        }

        // Früh und ohne den Wert zu nennen (er kann ein Kennwort enthalten): sonst scheitert erst der erste Client oder die erste Meldung.
        var options = ParseConnection(connection);
        signalR.AddStackExchangeRedis(o =>
        {
            o.Configuration = options;
            // Kanäle der Instanzen dieser Installation. Ein channelPrefix= in der Verbindungszeichenfolge gilt vor dem Standard: Eine zweite Installation
            // (Test und Betrieb, zwei Kunden) auf demselben Redis braucht ihn, sonst erreichen ihre Meldungen die Clients der anderen.
            if (o.Configuration.ChannelPrefix.IsNullOrEmpty)
            {
                o.Configuration.ChannelPrefix = RedisChannel.Literal("aispa");
            }

            // AbortOnConnectFail bleibt beim Standard (true): Scheitert die erste Verbindung, bleibt der Manager von SignalR uninitialisiert und baut Verbindung
            // und die einmaligen Abonnements (all, ack, return) beim nächsten Aufruf neu auf; die Instanz läuft auch so weiter. Der Grund war der Rundruf an alle Verbindungen
            // (mit false erreichte er eine im Ausfall gestartete Instanz nie mehr). Seit T-050 gibt es keinen Rundruf mehr; Benutzer- und Gruppenkanäle
            // (Abonnement erst beim Verbinden bzw. Beitritt) liefen im Test auch mit false. Der Standard bleibt, ohne dass ein Test ihn noch erzwingt.
            // Ohne Backlog-Richtlinie FailFast überbrückt StackExchange.Redis einen kurzen Aussetzer bis zum Zeitlimit; ein sofort scheiterndes
            // Abonnieren ließe eine Gruppe dauerhaft ohne Abonnement. Beide Zeitlimits sind niedriger als die Standardwerte (5 s), damit ein Handler im Ausfall nicht lange blockiert.
            // Nur als Standard: connectTimeout= und asyncTimeout= in der Verbindungszeichenfolge gelten vor ihm (ein verwalteter Redis mit TLS
            // über Zonen hinweg braucht für den Verbindungsaufbau womöglich mehr als 1 s). Gesetzte Werte lassen sich von den Standardwerten
            // der Bibliothek nicht unterscheiden, deshalb wird die Zeichenfolge selbst geprüft.
            if (!Contains(connection, "connectTimeout"))
            {
                o.Configuration.ConnectTimeout = ConnectTimeoutMilliseconds;
            }

            if (!Contains(connection, "asyncTimeout"))
            {
                o.Configuration.AsyncTimeout = CommandTimeoutMilliseconds;
            }
        });
        return mode;
    }

    /// <summary>Eine Zeile je Instanz beim Start. Die Verbindungszeichenfolge steht nie im Log, sie kann ein Kennwort enthalten.</summary>
    public static void LogLiveBackplane(this WebApplication app, LiveBackplaneMode mode)
    {
        if (mode == LiveBackplaneMode.Redis)
        {
            RedisInUse(app.Logger);
        }
        else
        {
            Off(app.Logger);
        }
    }

    private static LiveBackplaneMode ModeFrom(IConfiguration configuration)
    {
        var value = configuration[Key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return LiveBackplaneMode.None;
        }

        // Nur die Namen, keine Zahlen: Enum.TryParse nähme auch „1“ an.
        return value.Trim().ToLowerInvariant() switch
        {
            "none" => LiveBackplaneMode.None,
            "redis" => LiveBackplaneMode.Redis,
            _ => throw new InvalidOperationException($"{Key} „{value}“ ist ungültig; erlaubt sind None (Standard) und Redis."),
        };
    }

    /// <summary>Ob die durch Komma getrennten Teile der Verbindungszeichenfolge den Schlüssel <c>schlüssel=</c> enthalten, ohne Rücksicht auf Groß- und Kleinschreibung.</summary>
    private static bool Contains(string connection, string key) =>
        connection.Split(',', StringSplitOptions.TrimEntries).Any(part => part.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));

    private static ConfigurationOptions ParseConnection(string connection)
    {
        try
        {
            return ConfigurationOptions.Parse(connection);
        }
        // ArgumentException für die meisten Optionen, FormatException (UriFormatException) etwa für tunnel=.
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionName} ist keine gültige Redis-Verbindungszeichenfolge; erwartet wird zum Beispiel redis-host:6379,password=…");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Live-Backplane: Redis; Meldungen gehen an alle Instanzen")]
    private static partial void RedisInUse(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Live-Backplane: aus; Meldungen erreichen nur die Clients dieser Instanz")]
    private static partial void Off(ILogger logger);
}
