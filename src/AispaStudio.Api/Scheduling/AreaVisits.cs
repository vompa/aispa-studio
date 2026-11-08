using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Plätze eines Bereichs (Sauna, Ruheraum) vergeben. Jede Person belegt einen Platz für die ganze Zeit; die Ausschlussbedingung je Ressource
/// und Platz verhindert Überbuchung auch bei gleichzeitigen Buchungen. Eine Sperrzeit (Wartung) sperrt den ganzen Bereich.
/// </summary>
internal static class AreaPlanner
{
    /// <summary>Freie Plätze für den Zeitraum, kleinste zuerst; <c>null</c>, wenn nicht genug frei sind.</summary>
    public static IReadOnlyList<int>? FreePlaces(int places, IEnumerable<ResourceBooking> bookings, DateTimeOffset from, DateTimeOffset until, int persons)
    {
        var overlapping = bookings.Where(b => b.From < until && from < b.Until).ToList();
        if (overlapping.Any(b => b.AreaVisitId is null && b.AppointmentId is null))
        {
            return null;
        }

        var taken = overlapping.Select(b => b.Slot).ToHashSet();
        var free = Enumerable.Range(0, places).Where(p => !taken.Contains(p)).Take(persons).ToList();
        return free.Count == persons ? free : null;
    }

    /// <summary>Belegte Plätze im Tagesverlauf, nur an den Stellen, an denen sich etwas ändert.</summary>
    public static IReadOnlyList<AreaLoad> Load(IEnumerable<(DateTimeOffset From, DateTimeOffset Until, int Persons)> visits)
    {
        var changes = visits.SelectMany(v => new[] { (At: v.From, Delta: v.Persons), (At: v.Until, Delta: -v.Persons) })
            .GroupBy(c => c.At)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, Delta: g.Sum(c => c.Delta)));
        var load = new List<AreaLoad>();
        var occupied = 0;
        foreach (var (at, delta) in changes)
        {
            if (delta == 0)
            {
                continue;
            }

            occupied += delta;
            load.Add(new AreaLoad(at, occupied));
        }

        return load;
    }
}

/// <summary>Eintritte in Bereiche buchen, stornieren und den Tag eines Standorts anzeigen.</summary>
internal sealed class AreaVisitService(AispaDbContext db, TimeProvider time)
{
    private const int MaxPersons = 20;
    private static readonly TimeSpan MaxStay = TimeSpan.FromHours(12);

    public async Task<IReadOnlyList<AreaDay>?> DayAsync(Guid locationId, DateOnly date, CancellationToken ct)
    {
        var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null)
        {
            return null;
        }

        var (from, until) = LocalTime.Day(date, LocalTime.Zone(zoneId));
        var areas = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive && r.IsArea).OrderBy(r => r.Name).ToListAsync(ct);
        var visits = await db.AreaVisits.AsNoTracking().Include(v => v.Customer)
            .Where(v => v.LocationId == locationId && v.From < until && from < v.Until)
            .OrderBy(v => v.From).ThenBy(v => v.Id)
            .ToListAsync(ct);
        return [.. areas.Select(a =>
        {
            var own = visits.Where(v => v.RoomId == a.Id).ToList();
            return new AreaDay(a.Id, a.Name, a.Places, [.. own.Select(ToDto)],
                AreaPlanner.Load(own.Where(v => !v.IsCancelled).Select(v => (v.From, v.Until, v.Persons))));
        })];
    }

    public async Task<BookingOutcome> BookAsync(AreaVisitRequest request, CancellationToken ct)
    {
        // Wiederholte Buchung mit derselben Id (vorgemerkt ohne Verbindung, Abgleich nach Abbruch): den bestehenden Eintritt zurückgeben.
        if (request.ClientId is { } clientId)
        {
            if (await db.AreaVisits.AsNoTracking().Include(v => v.Customer).FirstOrDefaultAsync(v => v.Id == clientId, ct) is { } existing)
            {
                return existing.CustomerId == request.CustomerId && existing.LocationId == request.LocationId
                    ? new BookingOutcome.AreaVisitBooked(ToDto(existing))
                    : new BookingOutcome.Invalid(nameof(request.ClientId), "Diese Id gehört schon zu einem anderen Eintritt.");
            }

            if (clientId == Guid.Empty || await db.AreaVisits.IgnoreQueryFilters().AnyAsync(v => v.Id == clientId, ct))
            {
                return new BookingOutcome.Invalid(nameof(request.ClientId), "Diese Id ist nicht verwendbar.");
            }
        }

        var from = request.From.ToUniversalTime();
        var until = request.Until.ToUniversalTime();
        if (until <= from || until - from > MaxStay)
        {
            return new BookingOutcome.Invalid("until", "Ende nach dem Beginn, höchstens 12 Stunden.");
        }

        if (request.Persons is < 1 or > MaxPersons)
        {
            return new BookingOutcome.Invalid("persons", $"Zwischen 1 und {MaxPersons} Personen.");
        }

        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).FirstOrDefaultAsync(l => l.Id == request.LocationId, ct);
        var area = await db.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && r.LocationId == request.LocationId && r.IsActive && r.IsArea, ct);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (location is null || area is null)
        {
            return new BookingOutcome.NotFound("Bereich");
        }

        if (customer is null)
        {
            return new BookingOutcome.NotFound("Kunde");
        }

        var zone = LocalTime.Zone(location.TimeZone);
        // Die Rezeption trägt an einem Schließtag trotzdem ein (ADR 0073, Nachtrag R03): ohne Öffnungsfenster am Tag gibt es nur den Hinweis.
        var closedDay = await IsClosedDayAsync(location, from, zone, ct);
        if (!closedDay && await ClosureService.FindAsync(db, location.Id, from, until, zone, ct) is { } closure)
        {
            return new BookingOutcome.Rejected(new BookingRejected($"Der Standort ist in dieser Zeit geschlossen ({ClosureText.Title(closure.Kind)}).", []));
        }

        if (await RoomClosureService.FindAsync(db, location.Id, area.Id, from, until, zone, ct) is { } roomClosed)
        {
            return new BookingOutcome.Rejected(new BookingRejected($"Der Bereich {area.Name} ist in dieser Zeit gesperrt ({RoomClosureText.Title(roomClosed.Kind)}).",
                [new BookingConflict(ResourceKind.Room, area.Id, area.Name, RoomClosureText.Reason(roomClosed.Kind, roomClosed.Reason), roomClosed.From, roomClosed.Until, null, true)]));
        }

        if (!closedDay && !IsOpen(location, from, until))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Der Standort hat in dieser Zeit nicht durchgehend geöffnet.", []));
        }

        var bookings = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.ResourceId == area.Id && b.From < until && from < b.Until)
            .ToListAsync(ct);
        if (AreaPlanner.FreePlaces(area.Places, bookings, from, until, request.Persons) is not { } places)
        {
            var occupied = bookings.Count(b => b.AreaVisitId is not null);
            var reason = bookings.Any(b => b.AreaVisitId is null && b.AppointmentId is null)
                ? "ist in dieser Zeit gesperrt"
                : $"ist in dieser Zeit voll ({Math.Min(occupied, area.Places)} von {area.Places} Plätzen belegt)";
            return new BookingOutcome.Rejected(new BookingRejected($"Im Bereich {area.Name} ist nicht genug Platz.",
                [new BookingConflict(ResourceKind.Room, area.Id, area.Name, reason)]));
        }

        var now = time.GetUtcNow();
        var visit = new AreaVisit
        {
            Id = request.ClientId ?? Guid.CreateVersion7(),
            LocationId = location.Id,
            RoomId = area.Id,
            CustomerId = customer.Id,
            Customer = customer,
            Persons = request.Persons,
            From = from,
            Until = until,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        foreach (var place in places)
        {
            var booking = new ResourceBooking { Kind = ResourceKind.Room, ResourceId = area.Id, Slot = place, From = from, Until = until, AreaVisitId = visit.Id };
            db.ResourceBookings.Add(booking);
            visit.Bookings.Add(booking);
        }

        db.AreaVisits.Add(visit);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23P01" })
        {
            return new BookingOutcome.Rejected(new BookingRejected("Die Plätze wurden gerade anderweitig vergeben. Bitte erneut versuchen.", []));
        }

        return new BookingOutcome.AreaVisitBooked(closedDay ? ToDto(visit) with { Warnings = [ResourcePlanner.ClosedDayNotice] } : ToDto(visit));
    }

    /// <summary>Am Ortstag gibt es kein Öffnungsfenster: kein Wochenplan oder eine ganztägige Schließzeit.</summary>
    private async Task<bool> IsClosedDayAsync(Location location, DateTimeOffset at, TimeZoneInfo zone, CancellationToken ct)
    {
        var date = LocalTime.LocalDate(at, zone);
        var (dayFrom, dayUntil) = LocalTime.Day(date, zone);
        var closed = await ClosureService.SpansAsync(db, location.Id, dayFrom, dayUntil, zone, ct);
        return !ResourcePlanner.OpeningWindows(location.OpeningHours, zone, date, closed).Any();
    }

    public async Task<BookingOutcome> CancelAsync(Guid locationId, Guid visitId, CancellationToken ct)
    {
        var visit = await db.AreaVisits.Include(v => v.Bookings).Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.Id == visitId && v.LocationId == locationId, ct);
        if (visit is null)
        {
            return new BookingOutcome.NotFound("Eintritt");
        }

        if (!visit.IsCancelled)
        {
            visit.IsCancelled = true;
            visit.UpdatedAt = time.GetUtcNow();
            db.ResourceBookings.RemoveRange(visit.Bookings);
            await db.SaveChangesAsync(ct);
        }

        return new BookingOutcome.AreaVisitBooked(ToDto(visit));
    }

    private static bool IsOpen(Location location, DateTimeOffset from, DateTimeOffset until)
    {
        var zone = LocalTime.Zone(location.TimeZone);
        var date = LocalTime.LocalDate(from, zone);
        return location.OpeningHours
            .Where(o => o.Day == date.DayOfWeek)
            .Any(o => LocalTime.ToUtc(date, o.Opens, zone) <= from && until <= LocalTime.ToUtc(date, o.Closes, zone));
    }

    public static AreaVisitDto ToDto(AreaVisit v) => new(v.Id, v.LocationId, v.RoomId, v.CustomerId,
        v.Customer.InHouseName, v.Persons, v.From, v.Until, v.IsCancelled, v.Note, v.GroupId);
}
