using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;

namespace AispaStudio.Desktop.Tests;

/// <summary>Live-Verbindung ohne Server: Tests lösen Meldungen selbst aus.</summary>
internal sealed class FakeLive : ILiveUpdates
{
    public event Action<AppointmentDto>? AppointmentChanged;

    public event Action<Guid>? InboxChanged;

    public event Action<Guid, OperationsChangeKind>? OperationsChanged;

    public Guid? Watched { get; private set; }

    public bool WatchingInbox { get; private set; }

    public Task WatchAsync(Guid locationId)
    {
        Watched = locationId;
        return Task.CompletedTask;
    }

    public Task<bool> WatchInboxAsync()
    {
        WatchingInbox = true;
        return Task.FromResult(true);
    }

    public void Send(AppointmentDto appointment) => AppointmentChanged?.Invoke(appointment);

    /// <summary>Wie die Nachricht des Servers für den beobachteten Standort.</summary>
    public void SendOperationsChanged(OperationsChangeKind kind) => OperationsChanged?.Invoke(Watched ?? Guid.Empty, kind);

    public void SendInbox(Guid mailId) => InboxChanged?.Invoke(mailId);
}
