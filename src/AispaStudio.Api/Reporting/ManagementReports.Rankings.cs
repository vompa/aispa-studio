using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Ranglisten zum Management-Cockpit, die der Assistent braucht („Top-Kunden im letzten Quartal“, „Auslastung je Raum“): dieselben Zeiträume, Stati und
/// Öffnungsminuten wie das Cockpit, nur nach Gast und Raum aufgeteilt. Beträge nur mit dem Recht „Berichte mit Beträgen“, schon hier und nicht erst in der Oberfläche.
/// </summary>
internal sealed partial class ManagementReports
{
    /// <summary>Öffnungsminuten des Standorts im Zeitraum (beide Tage eingeschlossen) nach den Wochenöffnungszeiten; Grundlage der Raumauslastung.</summary>
    internal static double OpenMinutes(Location location, DateOnly from, DateOnly until)
    {
        double minutes = 0;
        for (var day = from; day <= until; day = day.AddDays(1))
        {
            minutes += location.OpeningHours.Where(h => h.Day == day.DayOfWeek).Sum(h => (h.Closes - h.Opens).TotalMinutes);
        }

        return minutes;
    }

    /// <summary>
    /// Auslastung je Raum: belegte Minuten (Termine mit Vor- und Nachbereitung, ohne stornierte und nicht erschienene) ÷ Öffnungsminuten, in Prozent.
    /// Bereiche (Sauna, Ruheraum) zählen nicht; sie haben Eintritte statt Termine. <c>null</c>, wenn es den Standort nicht gibt.
    /// </summary>
    public async Task<IReadOnlyList<RoomKpi>?> RoomsAsync(Guid locationId, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var (start, _) = LocalTime.Day(from, zone);
        var (_, end) = LocalTime.Day(until, zone);
        var open = OpenMinutes(location, from, until);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive && !r.IsArea).OrderBy(r => r.Name).ToListAsync(ct);
        var held = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= start && a.Start < end && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .Select(a => new { a.RoomId, a.BlockedFrom, a.BlockedUntil })
            .ToListAsync(ct);
        return [.. rooms.Select(r =>
        {
            var own = held.Where(a => a.RoomId == r.Id).ToList();
            var minutes = own.Sum(a => (a.BlockedUntil - a.BlockedFrom).TotalMinutes);
            return new RoomKpi(r.Id, r.Name, open > 0 ? (int)Math.Round(minutes / open * 100) : 0, own.Count);
        })];
    }

    /// <summary>
    /// Beste Gäste des Zeitraums am Standort: nach Ausgaben (Summe der Belege, nur mit <paramref name="amounts"/>), sonst nach abgeschlossenen Besuchen.
    /// Ohne Beträge steht <see cref="GuestKpi.Spent"/> nicht in der Antwort. Höchstens <paramref name="take"/> Gäste.
    /// </summary>
    public async Task<IReadOnlyList<GuestKpi>> TopGuestsAsync(Guid locationId, DateOnly from, DateOnly until, bool amounts, int take, CancellationToken ct)
    {
        var zoneId = await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null)
        {
            return [];
        }

        var zone = LocalTime.Zone(zoneId);
        var (start, _) = LocalTime.Day(from, zone);
        var (_, end) = LocalTime.Day(until, zone);
        var visits = await db.Appointments.AsNoTracking()
            .Where(a => a.LocationId == locationId && a.Start >= start && a.Start < end && a.Status == AppointmentStatus.Completed)
            .GroupBy(a => a.CustomerId)
            .Select(g => new { CustomerId = g.Key, Visits = g.Count() })
            .ToListAsync(ct);
        var spent = new Dictionary<Guid, decimal>();
        if (amounts)
        {
            var registers = db.CashRegisters.Where(r => r.LocationId == locationId).Select(r => r.Id);
            spent = await db.Receipts.AsNoTracking()
                .Where(r => r.CustomerId != null && registers.Contains(r.RegisterId) && r.IssuedAt >= start && r.IssuedAt < end
                    && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
                .GroupBy(r => r.CustomerId!.Value)
                .Select(g => new { CustomerId = g.Key, Total = g.Sum(r => r.Total) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Total, ct);
        }

        var ids = visits.Select(v => v.CustomerId).Concat(spent.Keys).Distinct().ToList();
        var names = (await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct)).ToDictionary(c => c.Id, c => c.InHouseName);
        var ranked = ids.Select(id => new GuestKpi(id, names.GetValueOrDefault(id, "Unbekannt"), visits.FirstOrDefault(v => v.CustomerId == id)?.Visits ?? 0,
                amounts ? spent.GetValueOrDefault(id) : null))
            .Where(g => g.Visits > 0 || g.Spent > 0);
        return [.. (amounts
                ? ranked.OrderByDescending(g => g.Spent).ThenByDescending(g => g.Visits)
                : ranked.OrderByDescending(g => g.Visits))
            .ThenBy(g => g.Name, StringComparer.Ordinal)
            .Take(take)];
    }
}
