using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Gäste im Management-Bericht: beste Gäste des Zeitraums und abwandernde Stammgäste. Namen stehen nur in der Antwort an die App (Recht wie die Kundenliste),
/// nie in den Fakten für die KI (<see cref="ManagementSummaries.FactsText"/>) und nie in der Mail.
/// </summary>
internal sealed partial class ManagementReports
{
    /// <summary>So viele beste Gäste zeigt der Bericht.</summary>
    internal const int TopGuestCount = 10;

    /// <summary>Ab so vielen Besuchen am Standort ist ein Gast Stammgast.</summary>
    internal const int RegularVisits = 3;

    /// <summary>Seit so vielen Tagen ohne Besuch gilt ein Stammgast als abwandernd.</summary>
    internal const int LapsingDays = 90;

    /// <summary>Höchstens so viele abwandernde Stammgäste stehen in der Liste (die mit den meisten Besuchen); die Kennzahl zählt alle.</summary>
    internal const int LapsingListLimit = 50;

    /// <summary>
    /// Abwandernde Stammgäste, Stand <paramref name="now"/>: mindestens <see cref="RegularVisits"/> stattgefundene Termine am Standort, der letzte (an irgendeinem Standort)
    /// vor über <see cref="LapsingDays"/> Tagen, und kein stattfindender Termin danach. Weil der späteste Termin vor der Grenze liegt, gibt es auch keinen in der Zukunft.
    /// Gelöschte Gäste fehlen. Liefert die Anzahl und die Liste, nach Besuchen geordnet.
    /// </summary>
    private async Task<(int Count, List<LapsingGuest> List)> LapsingGuestsAsync(Guid locationId, TimeZoneInfo zone, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-LapsingDays);
        var lapsing = await db.Appointments.AsNoTracking()
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .GroupBy(a => a.CustomerId)
            .Select(g => new { CustomerId = g.Key, Visits = g.Sum(a => a.LocationId == locationId ? 1 : 0), Latest = g.Max(a => a.Start) })
            .Where(g => g.Visits >= RegularVisits && g.Latest < cutoff)
            .ToListAsync(ct);
        var ids = lapsing.Select(g => g.CustomerId).ToList();
        var names = (await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id) && c.DeletedAt == null).ToListAsync(ct))
            .ToDictionary(c => c.Id, c => c.InHouseName);
        var guests = lapsing.Where(g => names.ContainsKey(g.CustomerId))
            .Select(g => new LapsingGuest(g.CustomerId, names[g.CustomerId], LocalTime.LocalDate(g.Latest, zone), g.Visits))
            .OrderByDescending(g => g.Visits).ThenBy(g => g.LastVisit).ThenBy(g => g.Name, StringComparer.Ordinal)
            .ToList();
        return (guests.Count, [.. guests.Take(LapsingListLimit)]);
    }
}
