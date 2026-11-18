using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Hilfen für die Tests der Seite „Schließzeiten“ (ADR 0098): die Übersicht der API (<c>closure-overview</c>) aus Listen von Schließzeiten und Raumsperren, so sortiert und gezählt
/// wie der Server (kommende nach Beginn aufsteigend, vergangene absteigend).
/// </summary>
internal static class ClosuresTestKit
{
    public static ClosureOverviewItem ToItem(ClosureDto c) =>
        new(c.Id, ClosureEntryType.Location, c.Kind.ToString(), c.LocationId, c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.From, c.Until, c.Phase, c.InternalReason ?? c.GuestText,
            c.OpenAppointments, c.OpenWaitlist);

    public static ClosureOverviewItem ToItem(RoomClosureDto c) =>
        new(c.Id, ClosureEntryType.Room, c.Kind.ToString(), c.LocationId, c.FromDate, c.FromTime, c.UntilDate, c.UntilTime, c.From, c.Until, c.Phase, c.Reason, c.OpenAppointments, 0,
            c.RoomId, c.RoomName);

    public static ClosureOverview Build(Guid locationId, IEnumerable<ClosureOverviewItem> all, ClosureScope scope)
    {
        var items = all.ToList();
        var upcoming = items.Where(i => i.Phase != ClosurePhase.Ended).OrderBy(i => i.From).ThenBy(i => i.Id).ToList();
        var past = items.Where(i => i.Phase == ClosurePhase.Ended).OrderByDescending(i => i.From).ThenBy(i => i.Id).ToList();
        List<ClosureOverviewItem> shown = scope switch
        {
            ClosureScope.Upcoming => upcoming,
            ClosureScope.Past => past,
            _ => [.. upcoming, .. past],
        };
        return new ClosureOverview(locationId, upcoming.Count, past.Count, shown);
    }

    /// <summary>Antwortet auf <c>closure-overview</c> mit dem aktuellen Stand der Listen (sie verändern sich mit POST, PUT und DELETE der Tests).</summary>
    public static StubApi OnOverview(this StubApi api, Guid locationId, Func<IEnumerable<ClosureOverviewItem>> items)
    {
        foreach (var scope in Enum.GetValues<ClosureScope>())
        {
            api.OnJson($"GET /api/locations/{locationId}/closure-overview?scope={scope}", _ => Build(locationId, items(), scope));
        }

        return api;
    }
}
