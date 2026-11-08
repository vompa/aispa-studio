using System.Net;
using System.Net.Sockets;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Die Live-Verbindung ist eine Zugabe: scheitert ihr Aufbau (Server antwortet nicht, steht im Debugger, Aufbau abgebrochen),
/// bleiben Kalender und Posteingang ohne Ausnahme nutzbar.
/// </summary>
public sealed class LiveUpdatesTests
{
    [Fact]
    public async Task Abgebrochener_Verbindungsaufbau_wirft_nicht()
    {
        using var server = SilentServer();
        var live = new LiveUpdates(ClientFor(server));
        var watch = live.WatchAsync(Guid.NewGuid());
        var inbox = live.WatchInboxAsync();

        await live.DisposeAsync();

        Assert.Null(await Record.ExceptionAsync(() => watch.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)));
        Assert.False(await inbox.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    /// <summary>Nimmt Verbindungen an (Warteschlange des Betriebssystems), antwortet aber nie.</summary>
    private static TcpListener SilentServer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static ApiClient ClientFor(TcpListener server) =>
        new(new Uri($"http://127.0.0.1:{((IPEndPoint)server.LocalEndpoint).Port}/"), new MemorySessionStore());
}
