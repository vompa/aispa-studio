using AispaStudio.Api.Live;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AispaStudio.Api.Tests;

/// <summary>Live-Meldungen bei einem Ausfall von Redis: nur warnen, nicht wiederholen (ADR 0087); andere Fehler gehen weiter. Ohne Datenbank.</summary>
public sealed class LiveDeliveryTests
{
    private static ILogger Logger(LogCapture log) => log.CreateLogger("Live");

    [Fact]
    public async Task RedisException_wird_nur_gewarnt()
    {
        var log = new LogCapture();

        await Logger(log).SendAsync("Test", () => throw new RedisConnectionException(ConnectionFailureType.SocketFailure, "weg"));

        Assert.Single(log.Lines, l => l.Level == LogLevel.Warning && l.Message.Contains("nicht zugestellt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fehler_aus_dem_Redis_Client_ohne_RedisException_wird_nur_gewarnt()
    {
        // Bricht die Verbindung mitten im Lesen ab, wirft der Client eine InvalidOperationException aus seiner Pipe
        // („Reading is not allowed after reader was completed“, CI-Lauf 42466991420). Nachgestellt mit einem Fehler,
        // den der Client selbst wirft und der keine RedisException ist.
        var log = new LogCapture();

        await Logger(log).SendAsync("Test", () =>
        {
            ConfigurationOptions.Parse("abortConnect=vielleicht");
            return Task.CompletedTask;
        });

        Assert.Single(log.Lines, l => l.Level == LogLevel.Warning && l.Message.Contains("nicht zugestellt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Anderer_Fehler_geht_weiter()
    {
        var log = new LogCapture();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Logger(log).SendAsync("Test", () => throw new InvalidOperationException("kein Redis")));

        Assert.Empty(log.Lines);
    }
}
