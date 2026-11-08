using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.TimeTracking;

/// <summary>Liest Ist-Zeiten je Person und Kalendertag (in der Zeitzone des Standorts, an dem gestempelt wurde).</summary>
internal sealed class TimeTrackingService(AispaDbContext db, TimeProvider time)
{
    /// <summary>Ein Abschnitt, auf einen Kalendertag bezogen: Beginn und Ende ab Mitternacht dieses Tages (über Mitternacht mehr als 24 Stunden).</summary>
    public sealed record DaySpan(TimeRecord Record, TimeSpan Start, TimeSpan End, bool Running);

    /// <summary>
    /// Abschnitte im Zeitraum [from, until] je Person und Tag, ohne gelöschte. Ein Abschnitt zählt zum Tag, an dem er beginnt;
    /// ein laufender Abschnitt endet vorläufig jetzt.
    /// </summary>
    public async Task<Dictionary<(Guid EmployeeId, DateOnly Date), List<DaySpan>>> DaysAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var zones = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => LocalTime.Zone(l.TimeZone), ct);
        // Grob in UTC eingrenzen (±1 Tag für alle Zeitzonen), genau je Standort.
        var rangeFrom = new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeUntil = new DateTimeOffset(until.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var records = await db.TimeRecords.AsNoTracking().Include(r => r.Changes)
            .Where(r => employeeIds.Contains(r.EmployeeId) && !r.IsDeleted && r.Start >= rangeFrom && r.Start < rangeUntil)
            .OrderBy(r => r.Start)
            .ToListAsync(ct);

        var now = time.GetUtcNow();
        var days = new Dictionary<(Guid, DateOnly), List<DaySpan>>();
        foreach (var record in records)
        {
            var zone = zones[record.LocationId];
            var date = LocalTime.LocalDate(record.Start, zone);
            if (date < from || date > until)
            {
                continue;
            }

            var midnight = LocalTime.ToUtc(date, TimeOnly.MinValue, zone);
            var end = record.End ?? (now > record.Start ? now : record.Start);
            var key = (record.EmployeeId, date);
            if (!days.TryGetValue(key, out var list))
            {
                days[key] = list = [];
            }

            list.Add(new(record, record.Start - midnight, end - midnight, record.End is null));
        }

        return days;
    }

    public static double Hours(IEnumerable<DaySpan> spans) => spans.Sum(s => (s.End - s.Start).TotalHours);

    /// <summary>Pausen: Lücken zwischen den Abschnitten eines Tages.</summary>
    public static int BreakMinutes(IReadOnlyList<DaySpan> spans)
    {
        var ordered = spans.OrderBy(s => s.Start).ToList();
        return (int)Math.Round(ordered.Zip(ordered.Skip(1)).Sum(p => Math.Max(0, (p.Second.Start - p.First.End).TotalMinutes)));
    }

    /// <summary>Abgeschlossene Abschnitte als Uhrzeiten je Kalendertag und Standort, über Mitternacht geteilt (für die Lohnverrechnung).</summary>
    public async Task<Dictionary<(Guid EmployeeId, DateOnly Date), List<(Guid LocationId, TimeOnly Start, TimeOnly End)>>> LocalIntervalsAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var days = await DaysAsync(employeeIds, from.AddDays(-1), until, ct);
        var result = new Dictionary<(Guid, DateOnly), List<(Guid, TimeOnly, TimeOnly)>>();
        void Add(Guid employeeId, DateOnly date, Guid locationId, TimeSpan start, TimeSpan end)
        {
            if (date < from || date > until || end <= start)
            {
                return;
            }

            if (!result.TryGetValue((employeeId, date), out var list))
            {
                result[(employeeId, date)] = list = [];
            }

            list.Add((locationId, TimeOnly.FromTimeSpan(start), end >= TimeSpan.FromDays(1) ? TimeOnly.MaxValue : TimeOnly.FromTimeSpan(end)));
        }

        foreach (var ((employeeId, date), spans) in days)
        {
            foreach (var span in spans.Where(s => !s.Running))
            {
                var day = TimeSpan.FromDays(1);
                Add(employeeId, date, span.Record.LocationId, span.Start, span.End < day ? span.End : day);
                if (span.End > day)
                {
                    Add(employeeId, date.AddDays(1), span.Record.LocationId, TimeSpan.Zero, span.End - day);
                }
            }
        }

        return result;
    }

    public static TimeRecordDto ToDto(TimeRecord r) =>
        new(r.Id, r.EmployeeId, r.LocationId, r.Start, r.End, r.Source, r.Changes.Any(c => c.Action != TimeRecordAction.Created));
}
