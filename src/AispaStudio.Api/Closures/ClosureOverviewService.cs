using AispaStudio.Contracts;

namespace AispaStudio.Api.Closures;

/// <summary>
/// Die gemeinsame Übersicht von Schließzeiten und Raumsperren (ADR 0098): baut auf den Listen der beiden Dienste auf, damit Zustand und Zahlen offener Termine überall dieselben sind.
/// </summary>
internal sealed class ClosureOverviewService(ClosureService closures, RoomClosureService roomClosures)
{
    /// <summary>Die Übersicht des Standorts; <c>null</c>, wenn es ihn nicht gibt.</summary>
    public async Task<ClosureOverview?> GetAsync(Guid locationId, ClosureScope scope, CancellationToken ct)
    {
        if (await closures.ListAsync(locationId, ct) is not { } locationClosures || await roomClosures.ListAsync(locationId, ct) is not { } rooms)
        {
            return null;
        }

        var all = locationClosures.Select(ToItem).Concat(rooms.Select(ToItem)).ToList();
        var upcoming = all.Where(i => i.Phase != ClosurePhase.Ended).OrderBy(i => i.From).ThenBy(i => i.Until).ThenBy(i => i.Id).ToList();
        var past = all.Where(i => i.Phase == ClosurePhase.Ended).OrderByDescending(i => i.From).ThenByDescending(i => i.Until).ThenBy(i => i.Id).ToList();
        List<ClosureOverviewItem> items = scope switch
        {
            ClosureScope.Upcoming => upcoming,
            ClosureScope.Past => past,
            _ => [.. upcoming, .. past],
        };
        return new ClosureOverview(locationId, upcoming.Count, past.Count, items);
    }

    private static ClosureOverviewItem ToItem(ClosureDto c) =>
        new(c.Id, ClosureEntryType.Location, c.Kind.ToString(), c.LocationId, c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.From, c.Until, c.Phase, c.InternalReason ?? c.GuestText,
            c.OpenAppointments, c.OpenWaitlist);

    private static ClosureOverviewItem ToItem(RoomClosureDto c) =>
        new(c.Id, ClosureEntryType.Room, c.Kind.ToString(), c.LocationId, c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.From, c.Until, c.Phase, c.Reason, c.OpenAppointments, 0,
            c.RoomId, c.RoomName);
}
