using System.Text.Json.Serialization;
using AispaStudio.Contracts;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Desktop.Services;

/// <summary>Terminänderungen anderer Arbeitsplätze und neue Nachrichten im Posteingang in Echtzeit.</summary>
public interface ILiveUpdates
{
    event Action<AppointmentDto>? AppointmentChanged;

    /// <summary>Im Betrieb des Standorts (Id) hat sich Lager, Raumpflege, Tagesstart oder Handbuch geändert: die Meldungen laden die Quelle neu.</summary>
    event Action<Guid, OperationsChangeKind>? OperationsChanged;

    /// <summary>Eine Nachricht im Posteingang ist neu oder hat sich geändert (Id der Nachricht).</summary>
    event Action<Guid>? InboxChanged;

    Task WatchAsync(Guid locationId);

    /// <summary>Meldet den Posteingang an; <c>false</c>, wenn keine Live-Verbindung besteht.</summary>
    Task<bool> WatchInboxAsync();
}

/// <summary>SignalR-Verbindung mit dem Anmelde-Cookie; verbindet sich nach Unterbrechungen selbst neu und meldet den Standort erneut an.</summary>
public sealed class LiveUpdates : ILiveUpdates, IAsyncDisposable
{
    private readonly HubConnection _connection;
    private Guid? _watched;
    private bool _inbox;

    public LiveUpdates(IWorkstationApi api)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(api.BaseAddress, LiveHubContract.Path.TrimStart('/')), o =>
            {
                if (api.SessionCookie is { } cookie)
                {
                    o.Headers["Cookie"] = $"aispa.auth={cookie}";
                }

                // Mehrplatz im Netz: dasselbe Zertifikat wie für die übrigen Aufrufe, für WebSocket und den Rückfall auf HTTP.
                if (api.CertificateCode is { } code)
                {
                    o.HttpMessageHandlerFactory = inner =>
                    {
                        if (inner is HttpClientHandler http)
                        {
                            http.ServerCertificateCustomValidationCallback = (_, certificate, _, _) => NetworkShare.Matches(certificate, code);
                        }

                        return inner;
                    };
                    o.WebSocketConfiguration = socket =>
                        socket.RemoteCertificateValidationCallback = (_, certificate, _, _) => NetworkShare.Matches(certificate, code);
                }
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .WithAutomaticReconnect()
            .Build();

        // Ereignisse kommen auf einem Hintergrund-Thread; die Oberfläche darf nur im UI-Thread geändert werden.
        _connection.On<AppointmentDto>(LiveHubContract.AppointmentChanged,
            appointment => Dispatcher.UIThread.Post(() => AppointmentChanged?.Invoke(appointment)));
        _connection.On<Guid, OperationsChangeKind>(LiveHubContract.OperationsChanged,
            (locationId, kind) => Dispatcher.UIThread.Post(() => OperationsChanged?.Invoke(locationId, kind)));
        _connection.On<InboxChangedEvent>(LiveHubContract.InboxChanged,
            change => Dispatcher.UIThread.Post(() => InboxChanged?.Invoke(change.MailId)));
        _connection.Reconnected += async _ =>
        {
            if (_watched is { } id)
            {
                await _connection.InvokeAsync(LiveHubContract.WatchLocation, id).ConfigureAwait(false);
            }

            if (_inbox)
            {
                await _connection.InvokeAsync(LiveHubContract.WatchInbox).ConfigureAwait(false);
            }

            // Was während der Unterbrechung kam, ist nicht live angekommen: einmal alles neu laden lassen.
            Dispatcher.UIThread.Post(() => InboxChanged?.Invoke(Guid.Empty));
            if (_watched is { } watched)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var kind in Enum.GetValues<OperationsChangeKind>())
                    {
                        OperationsChanged?.Invoke(watched, kind);
                    }
                });
            }
        };
    }

    public event Action<AppointmentDto>? AppointmentChanged;

    public event Action<Guid, OperationsChangeKind>? OperationsChanged;

    public event Action<Guid>? InboxChanged;

    public async Task<bool> WatchInboxAsync()
    {
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync().ConfigureAwait(false);
            }

            await _connection.InvokeAsync(LiveHubContract.WatchInbox).ConfigureAwait(false);
            _inbox = true;
            return true;
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            return false;
        }
    }

    public async Task WatchAsync(Guid locationId)
    {
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync().ConfigureAwait(false);
            }

            if (_watched is { } previous && previous != locationId)
            {
                await _connection.InvokeAsync(LiveHubContract.UnwatchLocation, previous).ConfigureAwait(false);
            }

            await _connection.InvokeAsync(LiveHubContract.WatchLocation, locationId).ConfigureAwait(false);
            _watched = locationId;
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            // Ohne Live-Verbindung bleibt der Kalender nutzbar; „Aktualisieren“ lädt neu.
        }
    }

    /// <summary>Server nicht erreichbar, Aufbau abgebrochen oder nach 15 s ohne Handshake (SignalR wirft dann OperationCanceledException), Aufruf abgelehnt.</summary>
    private static bool IsConnectionFailure(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException or HubException or InvalidOperationException;

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
