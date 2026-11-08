using AispaStudio.Api.Live;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.SignalR;
using Wolverine;

namespace AispaStudio.Api.Operations;

/// <summary>
/// Nachricht in der Queue: im Betrieb eines Standorts hat sich etwas geändert (Lager, Raumpflege, Tagesstart, Handbuch-Erinnerungen) und soll an alle
/// Kalender des Standorts gehen, damit Meldungen ohne Neuladen entstehen und verschwinden. Pausen und Abwesenheiten brauchen sie nicht: sie kommen mit
/// <see cref="Scheduling.AppointmentChangedMessage"/> bzw. dem Kalenderabruf.
/// </summary>
public sealed record OperationsChangedMessage(Guid TenantId, Guid LocationId, OperationsChangeKind Kind);

/// <summary>Verteilt die Änderung entkoppelt vom Speichern per SignalR an alle, die den Standort beobachten; die Oberfläche lädt die betroffene Quelle selbst nach.</summary>
public static class OperationsChangedHandler
{
    public static Task Handle(OperationsChangedMessage message, IHubContext<LiveHub, ILiveClient> hub, ILogger<LiveHub> logger) =>
        logger.SendAsync("Betrieb", () => hub.Clients.Group(LiveHubContract.LocationGroup(message.LocationId)).OperationsChanged(message.LocationId, message.Kind));
}

/// <summary>Jede Terminänderung kann Raumpflege-Aufgaben entstehen oder wegfallen lassen (Abschluss der Behandlung, Storno, Verschieben); die Kalender laden sie neu.</summary>
public static class RoomCareAppointmentChangedHandler
{
    // Der Mandant steht für die Weitergabe nicht an: sie geht nur an die Gruppe des Standorts.
    public static OperationsChangedMessage Handle(AppointmentChangedMessage message) =>
        new(Guid.Empty, message.Appointment.LocationId, OperationsChangeKind.RoomCare);
}

internal static class OperationsChangedExtensions
{
    /// <summary>Meldet nach dem Speichern, dass sich im Betrieb des Standorts etwas geändert hat.</summary>
    public static ValueTask PublishOperationsChangedAsync(this IMessageBus bus, TenantContext tenants, Guid locationId, OperationsChangeKind kind) =>
        bus.PublishAsync(new OperationsChangedMessage(tenants.TenantId ?? Guid.Empty, locationId, kind));
}
