using System.Diagnostics;
using StackExchange.Redis;

namespace AispaStudio.Api.Live;

/// <summary>
/// Zustellung der Live-Meldungen an die Clients. Fällt Redis (die Backplane, ADR 0087) aus, fehlen nur die Live-Updates: Die Meldung ist
/// verloren, die Clients sehen den Stand beim nächsten Laden. Sie in der Queue zu wiederholen würde nichts bringen, denn jede Wiederholung
/// trifft denselben Ausfall, und die Fehlerablage füllte sich mit Meldungen, die niemand mehr braucht. Die Behandlung steht deshalb im Handler
/// und nicht als Wolverine-Fehlerregel: Mehrere Handler derselben Nachricht laufen in einem Lauf, ein verworfener Lauf ließe die übrigen
/// (Warteliste, Raumpflege) ungetan.
/// </summary>
internal static partial class LiveDelivery
{
    /// <summary>Führt die Zustellung aus. Scheitert sie an Redis, schreibt sie eine Warnung ins Log, ohne Wiederholung und ohne Fehlerablage; andere Fehler bleiben.</summary>
    public static async Task SendAsync(this ILogger logger, string what, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            NotDelivered(logger, what, exception.GetType().Name, exception.Message);
        }
    }

    /// <summary>
    /// Ein Fehler der Backplane: RedisTimeoutException (Redis hängt statt abzulehnen) erbt von TimeoutException, nicht von RedisException.
    /// Bricht die Verbindung mitten im Lesen ab, wirft der Client außerdem Fehler ohne Redis-Typ, etwa eine InvalidOperationException
    /// aus seiner Pipe („Reading is not allowed after reader was completed“); sie erkennt man daran, dass sie im Client entstanden sind.
    /// </summary>
    internal static bool IsRedisFailure(Exception exception) =>
        exception is RedisException or RedisTimeoutException || RaisedInRedisClient(exception);

    private static bool RaisedInRedisClient(Exception exception) =>
        new StackTrace(exception, fNeedFileInfo: false).GetFrames()
            .Any(frame => frame.GetMethod()?.DeclaringType?.Assembly == typeof(RedisException).Assembly);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Live-Meldung „{What}“ nicht zugestellt: Redis (Live:Backplane) ist nicht erreichbar ({Reason}: {Message}). Keine Wiederholung; die Clients sehen die Änderung erst beim nächsten Laden.")]
    private static partial void NotDelivered(ILogger logger, string what, string reason, string message);
}
