using AispaStudio.Api.Access;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace AispaStudio.Api.Live;

/// <summary>Methoden, die der Server bei den verbundenen Clients aufruft.</summary>
public interface ILiveClient
{
    Task Pong(PongEvent pong);

    Task AppointmentChanged(AppointmentDto appointment);

    Task InboxChanged(InboxChangedEvent change);

    /// <summary>Die Tafel im Pausenraum lädt neu; ohne Nutzdaten, damit nie ein Gastname bei der Anzeige ankommt.</summary>
    Task DisplayChanged();

    Task OperationsChanged(Guid locationId, OperationsChangeKind kind);
}

/// <summary>Echtzeit-Kanal zu Web-Oberfläche und Windows-App.</summary>
[Authorize]
public sealed class LiveHub : Hub<ILiveClient>
{
    /// <summary>Terminänderungen eines Standorts empfangen (z. B. der Kalender an der Rezeption). Nur mit Zugang zu diesem Standort.</summary>
    public async Task WatchLocation(Guid locationId, AccessService access)
    {
        var user = await access.GetAsync(Context.User!, Context.ConnectionAborted);
        if (user is null || !user.Has(AccessRight.ViewCalendar) || !user.CanSee(locationId))
        {
            throw new HubException("Kein Zugang zu diesem Standort.");
        }

        await JoinAsync(LiveHubContract.LocationGroup(locationId));
    }

    /// <summary>Änderungen im Posteingang des eigenen Spas empfangen. Nur mit Buchungsrecht, wie der Posteingang selbst.</summary>
    public async Task WatchInbox(AccessService access)
    {
        var user = await access.GetAsync(Context.User!, Context.ConnectionAborted);
        // Der Mandant aus dem Anmelde-Cookie der Verbindung (im Hub gibt es keinen HttpContext für TenantContext).
        var tenant = Context.User?.FindFirst(TenantContext.ClaimType)?.Value;
        if (user is null || !user.Has(AccessRight.Book) || !Guid.TryParse(tenant, out var tenantId))
        {
            throw new HubException("Kein Zugang zum Posteingang.");
        }

        await JoinAsync(LiveHubContract.InboxGroup(tenantId));
    }

    /// <summary>Die gekoppelte Anzeige hört auf Änderungen ihres Standorts (aus ihrer Anmeldung, nie aus einem Parameter).</summary>
    public async Task WatchDisplay()
    {
        if (Channels.Of(Context.User!) != AccessChannel.Display || !Guid.TryParse(Context.User!.FindFirst(DisplayDevices.LocationClaim)?.Value, out var locationId))
        {
            throw new HubException("Nur für die Anzeige im Pausenraum.");
        }

        await JoinAsync(LiveHubContract.DisplayGroup(locationId));
    }

    /// <summary>
    /// Trägt die Verbindung in die Gruppe ein. Scheitert das (mit der Backplane: Redis nicht erreichbar), steht sie sonst schon in der Gruppenliste, ohne dass die
    /// Gruppe abonniert ist, und dieser Instanz käme für die Gruppe nie wieder etwas an. Deshalb zurückrollen und den Fehler weitergeben; der Client meldet sich erneut an.
    /// </summary>
    private async Task JoinAsync(string group)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }
        catch
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
            }
            catch (Exception rollback) when (rollback is RedisException or RedisTimeoutException)
            {
                // Redis ist weiterhin nicht erreichbar; die Verbindung endet mit dem Client, der Fehler des Eintragens geht unten weiter.
            }

            throw;
        }
    }

    public Task UnwatchLocation(Guid locationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, LiveHubContract.LocationGroup(locationId));
}
