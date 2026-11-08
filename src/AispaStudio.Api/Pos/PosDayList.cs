using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Pos;

/// <summary>Wie ein Termin oder Eintritt auf Rechnungen steht: auf keiner, nur auf einer geparkten oder auf einer bezahlten. Anzahlungen zählen nicht.</summary>
internal enum PosDayBilling
{
    None,
    Parked,
    Settled,
}

/// <summary>Termin oder Eintritt von heute vor der Einteilung: Zeile, Ende, ob als abgeschlossen markiert, und wie er auf Rechnungen steht.</summary>
internal sealed record PosDayCandidate(PosDayEntryDto Entry, Guid Id, DateTimeOffset End, bool IsCompleted, PosDayBilling Billing);

/// <summary>
/// Liste „Heute abzurechnen“ der Kassa (Slice 137, Ä344): die Einteilung in „Wartet auf Abrechnung“, „Später heute“ und „Erledigt“ rechnet nur der Server,
/// die Windows-App zeichnet sie. Fertig ist, was als abgeschlossen markiert oder dessen Ende vorbei ist; erledigt, was auf einer bezahlten Rechnung steht.
/// </summary>
internal static class PosDayList
{
    /// <summary>Teilt die Kandidaten ein. Geparkte fehlen (sie stehen unter „Geparkte Rechnungen“); Gleichstand nach Gastname, dann Id.</summary>
    public static PosDayListDto Classify(IEnumerable<PosDayCandidate> candidates, DateTimeOffset now)
    {
        var all = candidates.Where(c => c.Billing != PosDayBilling.Parked).ToList();
        var open = all.Where(c => c.Billing == PosDayBilling.None).ToList();
        bool IsFinished(PosDayCandidate c) => c.IsCompleted || c.End <= now;

        var waiting = open.Where(IsFinished)
            .OrderBy(c => c.End).ThenBy(c => c.Entry.CustomerName, StringComparer.Ordinal).ThenBy(c => c.Id)
            .Select(c => c.Entry with { FinishedAt = c.End <= now ? c.End : null });
        return new PosDayListDto([.. waiting], ByStart(open.Where(c => !IsFinished(c))), ByStart(all.Where(c => c.Billing == PosDayBilling.Settled)));
    }

    private static List<PosDayEntryDto> ByStart(IEnumerable<PosDayCandidate> candidates) =>
    [
        .. candidates.OrderBy(c => c.Entry.Start).ThenBy(c => c.Entry.CustomerName, StringComparer.Ordinal).ThenBy(c => c.Id)
            .Select(c => c.Entry with { FinishedAt = null }),
    ];

    /// <summary>Termine und Eintritte eines Tages am Standort (ohne stornierte) mit Person, Raum, Preis und ihrem Stand auf Rechnungen.</summary>
    public static async Task<PosDayListDto> LoadAsync(AispaDbContext db, Guid locationId, DateOnly date, DateTimeOffset now, CancellationToken ct)
    {
        var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null)
        {
            return new PosDayListDto([], [], []);
        }

        var (from, until) = LocalTime.Day(date, LocalTime.Zone(zoneId));
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until && a.Status != AppointmentStatus.Cancelled)
            .ToListAsync(ct);
        var visits = await db.AreaVisits.AsNoTracking().Include(v => v.Customer)
            .Where(v => v.LocationId == locationId && v.From >= from && v.From < until && !v.IsCancelled)
            .ToListAsync(ct);

        var appointmentIds = appointments.Select(a => a.Id).ToList();
        var visitIds = visits.Select(v => v.Id).ToList();
        var lines = await db.BillLines.AsNoTracking()
            .Where(l => l.Kind != BillLineKind.Deposit
                && ((l.AppointmentId != null && appointmentIds.Contains(l.AppointmentId.Value)) || (l.AreaVisitId != null && visitIds.Contains(l.AreaVisitId.Value))))
            .Join(db.Bills, l => l.BillId, b => b.Id, (l, b) => new { l.AppointmentId, l.AreaVisitId, b.Status })
            .ToListAsync(ct);

        var employeeIds = appointments.Select(a => a.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.PreferredName ?? e.DisplayName, ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId)
            .ToDictionaryAsync(r => r.Id, r => new { r.Name, r.AdmissionArticleId }, ct);
        var articleIds = rooms.Values.Where(r => r.AdmissionArticleId is not null).Select(r => r.AdmissionArticleId!.Value).Distinct().ToList();
        var prices = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Price, ct);

        PosDayBilling Billing(IEnumerable<BillStatus> statuses)
        {
            var found = statuses.ToList();
            return found.Contains(BillStatus.Settled) ? PosDayBilling.Settled : found.Count > 0 ? PosDayBilling.Parked : PosDayBilling.None;
        }

        var candidates = appointments.Select(a => new PosDayCandidate(
                new PosDayEntryDto(a.Start, a.Customer.InHouseName, a.Treatment.Name, employees.GetValueOrDefault(a.EmployeeId),
                    rooms.GetValueOrDefault(a.RoomId)?.Name, a.Price, null, BookingService.ToDto(a, a.Treatment.Name), null),
                a.Id, a.End, a.Status == AppointmentStatus.Completed, Billing(lines.Where(l => l.AppointmentId == a.Id).Select(l => l.Status))))
            .Concat(visits.Select(v =>
            {
                var room = rooms.GetValueOrDefault(v.RoomId);
                var area = $"Eintritt {room?.Name ?? "Bereich"}";
                decimal? price = room?.AdmissionArticleId is { } article && prices.TryGetValue(article, out var each) ? each * v.Persons : null;
                return new PosDayCandidate(
                    new PosDayEntryDto(v.From, v.Customer.InHouseName, v.Persons == 1 ? area : $"{v.Persons} × {area}", null, room?.Name, price, null, null,
                        AreaVisitService.ToDto(v)),
                    v.Id, v.Until, false, Billing(lines.Where(l => l.AreaVisitId == v.Id).Select(l => l.Status)));
            }));
        return Classify(candidates, now);
    }
}
