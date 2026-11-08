using AispaStudio.Api.Live;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Nachricht in der Queue: ein Termin hat sich geändert und soll an alle Kalender des Standorts gehen.
/// <paramref name="PreviousStart"/> ist beim Verschieben der alte Beginn; die alte Zeit ist dann frei (Warteliste).
/// <paramref name="PreviousLocationId"/> ist gesetzt, wenn der Termin an einen anderen Standort verlegt wurde.
/// </summary>
public sealed record AppointmentChangedMessage(AppointmentDto Appointment, DateTimeOffset? PreviousStart = null, Guid? PreviousLocationId = null);

/// <summary>Verteilt Terminänderungen entkoppelt von der Buchung per SignalR an alle, die den Standort beobachten.</summary>
public static class AppointmentChangedHandler
{
    public static Task Handle(AppointmentChangedMessage message, IHubContext<LiveHub, ILiveClient> hub, ILogger<LiveHub> logger) =>
        logger.SendAsync("Terminänderung", async () =>
        {
            await hub.Clients.Group(LiveHubContract.LocationGroup(message.Appointment.LocationId)).AppointmentChanged(message.Appointment);
            // Die Tafel im Pausenraum bekommt nur den Anstoß zum Neuladen, nie den Termin (er enthält den Namen des Gastes).
            await hub.Clients.Group(LiveHubContract.DisplayGroup(message.Appointment.LocationId)).DisplayChanged();

            // Verlegt: der Kalender des alten Standorts nimmt den Termin heraus (er gehört dort nicht mehr hin).
            if (message.PreviousLocationId is { } previous && previous != message.Appointment.LocationId)
            {
                await hub.Clients.Group(LiveHubContract.LocationGroup(previous)).AppointmentChanged(message.Appointment);
                await hub.Clients.Group(LiveHubContract.DisplayGroup(previous)).DisplayChanged();
            }
        });
}
