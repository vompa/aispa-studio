using AispaStudio.Api.Live;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AispaStudio.Api.System;

/// <summary>
/// Nachricht in der Queue: ein Ping, der im Hintergrund verarbeitet wird. <paramref name="UserId"/> ist der Aufrufer (Claim <c>NameIdentifier</c>);
/// bei Nachrichten aus der Zeit vor diesem Feld ist sie <c>null</c>.
/// </summary>
public sealed record PingRequested(Guid PingId, string Message, string? UserId);

/// <summary>Verarbeitet <see cref="PingRequested"/> entkoppelt vom HTTP-Aufruf und meldet das Ergebnis per SignalR nur an die Verbindungen des Aufrufers.</summary>
public static class PingRequestedHandler
{
    public static Task Handle(PingRequested ping, IHubContext<LiveHub, ILiveClient> hub, TimeProvider time, ILogger<LiveHub> logger)
    {
        // Ohne Aufrufer gibt es keinen Empfänger: Clients.User(null) wirft, und die Wiederholungen landeten in der Fehlerablage.
        if (string.IsNullOrEmpty(ping.UserId))
        {
            return Task.CompletedTask;
        }

        return logger.SendAsync("Ping", () => hub.Clients.User(ping.UserId).Pong(new PongEvent(ping.PingId, ping.Message, time.GetUtcNow())));
    }
}
