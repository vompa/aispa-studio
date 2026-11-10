using AispaStudio.Api.Closures;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>Paar- und Spa-Tag-Buchungen: mehrere Behandlungen, gemeinsam gebucht oder gar nicht.</summary>
internal sealed partial class BookingService
{
    private const int MaxGroupItems = 8;
    private static readonly TimeSpan GroupStep = TimeSpan.FromMinutes(15);

    /// <summary>Startzeiten eines Tages, zu denen alle Behandlungen passen. <c>CustomerId</c> unterscheidet hier nur die Gäste.</summary>
    public async Task<BookingOutcome> GroupAvailabilityAsync(Guid locationId, GroupAvailabilityRequest request, CancellationToken ct)
    {
        if (Validate(request.Items, request.GapMinutes, request.Areas, request.FlexibleOrder) is { } invalid)
        {
            return invalid;
        }

        var parts = await LoadPartsAsync(locationId, request.Items, request.Date, ct);
        if (parts is null)
        {
            return new BookingOutcome.NotFound("Behandlung");
        }

        if (await LoadAreaPartsAsync(locationId, request.Areas ?? [], request.Date, [], ct) is not { } areas)
        {
            return new BookingOutcome.NotFound("Bereich");
        }

        var planner = new GroupPlanner(parts, TimeSpan.FromMinutes(request.GapMinutes), areas, request.FlexibleOrder);
        return new BookingOutcome.GroupSlots([.. planner.FreeStarts(request.Date, GroupStep).Select(p => p.ToSlot())]);
    }

    public async Task<BookingOutcome> BookGroupAsync(GroupBookingRequest request, CancellationToken ct)
    {
        // Wiederholte Buchung mit derselben Id (vorgemerkt ohne Verbindung, Abgleich nach Abbruch): die bestehenden Termine zurückgeben.
        if (request.ClientId is { } clientId)
        {
            var existing = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
                .Where(a => a.GroupId == clientId)
                .OrderBy(a => a.Start).ThenBy(a => a.Id)
                .ToListAsync(ct);
            if (existing.Count > 0)
            {
                var booked = await db.AreaVisits.AsNoTracking().Include(v => v.Customer).Where(v => v.GroupId == clientId).OrderBy(v => v.From).ToListAsync(ct);
                return existing.All(a => a.LocationId == request.LocationId)
                    ? new BookingOutcome.GroupBooked(new GroupBookingResult(clientId, [.. existing.Select(a => ToDto(a, a.Treatment.Name))],
                        booked.Count == 0 ? null : [.. booked.Select(AreaVisitService.ToDto)]))
                    : new BookingOutcome.Invalid(nameof(request.ClientId), "Diese Id gehört schon zu einer anderen Buchung.");
            }

            if (clientId == Guid.Empty || await db.Appointments.IgnoreQueryFilters().AnyAsync(a => a.GroupId == clientId, ct))
            {
                return new BookingOutcome.Invalid(nameof(request.ClientId), "Diese Id ist nicht verwendbar.");
            }
        }

        if (Validate(request.Items, request.GapMinutes, request.Areas, request.FlexibleOrder) is { } invalid)
        {
            return invalid;
        }

        var customerIds = request.Items.Select(i => i.CustomerId).Concat((request.Areas ?? []).Select(a => a.CustomerId)).Distinct().ToList();
        var customers = await db.Customers.Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        if (customers.Count != customerIds.Count)
        {
            return new BookingOutcome.NotFound("Kunde");
        }

        if (customers.Values.Any(c => c.DeletedAt is not null))
        {
            return new BookingOutcome.Invalid("items", CustomerAnonymizer.NotSelectableMessage);
        }

        var zoneId = await db.Locations.Where(l => l.Id == request.LocationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null)
        {
            return new BookingOutcome.NotFound("Standort");
        }

        var start = request.Start.ToUniversalTime();
        var date = LocalTime.LocalDate(start, LocalTime.Zone(zoneId));
        var parts = await LoadPartsAsync(request.LocationId, request.Items, date, ct);
        if (parts is null)
        {
            return new BookingOutcome.NotFound("Behandlung");
        }

        if (await LoadAreaPartsAsync(request.LocationId, request.Areas ?? [], date, [], ct) is not { } areaParts)
        {
            return new BookingOutcome.NotFound("Bereich");
        }

        var result = new GroupPlanner(parts, TimeSpan.FromMinutes(request.GapMinutes), areaParts, request.FlexibleOrder).Plan(start);
        if (result.Plan is not { } plan)
        {
            return new BookingOutcome.Rejected(new BookingRejected(result.Message!, result.Conflicts));
        }

        var now = time.GetUtcNow();
        var groupId = request.ClientId ?? Guid.CreateVersion7();
        var appointments = new List<Appointment>();
        for (var i = 0; i < parts.Count; i++)
        {
            var appointment = new Appointment
            {
                LocationId = request.LocationId,
                TreatmentId = parts[i].Context.Treatment.Id,
                CustomerId = parts[i].Item.CustomerId,
                Customer = customers[parts[i].Item.CustomerId],
                GroupId = groupId,
                PlaceholderGuest = parts[i].Item.PlaceholderGuest,
                Price = parts[i].Context.Treatment.PriceAt(request.LocationId, plan.Appointments[i].Times.Start, parts[i].Context.Zone),
                Note = request.Note,
                CreatedAt = now,
            };
            Apply(appointment, plan.Appointments[i], now);
            db.Appointments.Add(appointment);
            appointments.Add(appointment);
        }

        var visits = new List<AreaVisit>();
        foreach (var area in plan.Areas)
        {
            var visit = new AreaVisit
            {
                LocationId = request.LocationId,
                RoomId = area.Area.Id,
                CustomerId = area.Item.CustomerId,
                Customer = customers[area.Item.CustomerId],
                Persons = 1,
                GroupId = groupId,
                Note = request.Note,
                CreatedAt = now,
            };
            ApplyArea(visit, area, now);
            db.AreaVisits.Add(visit);
            visits.Add(visit);
        }

        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(TakenMeanwhile);
        }

        return await PublishGroupAsync(groupId, appointments.Select((a, i) => (a, parts[i].Context.Treatment.Name)), visits: visits);
    }

    /// <summary>Storniert alle noch offenen Termine einer gemeinsamen Buchung an diesem Standort.</summary>
    public async Task<BookingOutcome> CancelGroupAsync(Guid locationId, Guid groupId, string? reason, CancellationToken ct)
    {
        var appointments = await db.Appointments.Include(a => a.Bookings).Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.GroupId == groupId && a.LocationId == locationId)
            .OrderBy(a => a.Start).ThenBy(a => a.Id)
            .ToListAsync(ct);
        if (appointments.Count == 0)
        {
            return new BookingOutcome.NotFound("Gemeinsame Buchung");
        }

        var now = time.GetUtcNow();
        foreach (var appointment in appointments.Where(a => a.Status is AppointmentStatus.Booked or AppointmentStatus.CheckedIn))
        {
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelReason = reason;
            appointment.UpdatedAt = now;
            db.ResourceBookings.RemoveRange(appointment.Bookings);
        }

        var visits = await GroupVisitsAsync(groupId, locationId, ct);
        foreach (var visit in visits.Where(v => !v.IsCancelled))
        {
            visit.IsCancelled = true;
            visit.UpdatedAt = now;
            db.ResourceBookings.RemoveRange(visit.Bookings);
        }

        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(new BookingRejected("Ein Termin der Buchung wurde gerade von jemand anderem geändert.", []));
        }

        return await PublishGroupAsync(groupId, appointments.Select(a => (a, a.Treatment.Name)), visits: visits);
    }

    /// <summary>
    /// Verschiebt eine ganze gemeinsame Buchung auf einen neuen Beginn: gleiche Behandlungen, Gäste, Schritte und Pause wie bisher.
    /// Personen und Räume bleiben, wenn sie zur neuen Zeit frei sind, sonst wählt der Planer passende andere. Alle oder keiner.
    /// </summary>
    public async Task<BookingOutcome> MoveGroupAsync(Guid locationId, Guid groupId, DateTimeOffset newStart, CancellationToken ct)
    {
        var appointments = await db.Appointments.Include(a => a.Bookings).Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.GroupId == groupId && a.LocationId == locationId && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.Start).ThenBy(a => a.Id)
            .ToListAsync(ct);
        if (appointments.Count == 0)
        {
            return new BookingOutcome.NotFound("Gemeinsame Buchung");
        }

        if (appointments.Any(a => a.Status != AppointmentStatus.Booked))
        {
            return new BookingOutcome.Invalid("status", "Gäste sind schon da oder ein Termin ist abgeschlossen; bitte die Termine einzeln verschieben.");
        }

        var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstAsync(ct);
        var start = newStart.ToUniversalTime();
        var visits = (await GroupVisitsAsync(groupId, locationId, ct)).Where(v => !v.IsCancelled).OrderBy(v => v.From).ThenBy(v => v.Id).ToList();
        var (keep, free, areaItems, gap) = Reconstruct(appointments, visits);
        var ignore = appointments.Select(a => a.Id).ToList();
        var date = LocalTime.LocalDate(start, LocalTime.Zone(zoneId));
        if (await LoadAreaPartsAsync(locationId, areaItems, date, [.. visits.Select(v => v.Id)], ct) is not { } areaParts)
        {
            return new BookingOutcome.NotFound("Bereich");
        }

        GroupPlanResult? result = null;
        List<GroupPart>? parts = null;
        foreach (var items in new[] { keep, free })
        {
            parts = await LoadPartsAsync(locationId, items, date, ct, ignore);
            if (parts is null)
            {
                return new BookingOutcome.NotFound("Behandlung");
            }

            // Jeder Termin plant mit seinen eigenen Vor- und Nachbereitungszeiten (Schritt 12).
            parts = [.. parts.Select((p, i) => p with { Context = WithTimesOf(appointments[i], p.Context) })];
            result = new GroupPlanner(parts, gap, areaParts).Plan(start);
            if (result.Plan is not null)
            {
                break;
            }
        }

        if (result?.Plan is not { } plan)
        {
            return new BookingOutcome.Rejected(new BookingRejected(result!.Message!, result.Conflicts));
        }

        var now = time.GetUtcNow();
        var previous = appointments.ToDictionary(a => a.Id, a => a.Start);
        for (var i = 0; i < appointments.Count; i++)
        {
            db.ResourceBookings.RemoveRange(appointments[i].Bookings);
            Apply(appointments[i], plan.Appointments[i], now);
        }

        for (var i = 0; i < visits.Count; i++)
        {
            db.ResourceBookings.RemoveRange(visits[i].Bookings);
            ApplyArea(visits[i], plan.Areas[i], now);
        }

        if (!await TrySaveAsync(ct))
        {
            return new BookingOutcome.Rejected(TakenMeanwhile);
        }

        return await PublishGroupAsync(groupId, appointments.Select(a => (a, a.Treatment.Name)),
            a => previous[a.Id] == a.Start ? null : previous[a.Id], visits);
    }

    /// <summary>
    /// Aufbau einer gebuchten Buchung zurückgewinnen: Termine mit gleichem Beginn bilden einen Schritt, die Pause ist der kleinste Abstand
    /// zwischen dem Ende eines Schritts und dem Beginn des nächsten. <c>Keep</c> wünscht die bisherigen Personen und Räume, <c>Free</c> nichts.
    /// </summary>
    private static (List<GroupBookingItem> Keep, List<GroupBookingItem> Free, List<GroupAreaItem> Areas, TimeSpan Gap) Reconstruct(
        IReadOnlyList<Appointment> appointments, IReadOnlyList<AreaVisit> visits)
    {
        var parts = appointments.Select(a => (Start: a.Start, End: a.End)).Concat(visits.Select(v => (Start: v.From, End: v.Until))).ToList();
        var starts = parts.Select(p => p.Start).Distinct().Order().ToList();
        var gaps = starts.Skip(1).Select((s, i) => s - parts.Where(p => p.Start == starts[i]).Max(p => p.End)).ToList();
        var gap = gaps.Count == 0 ? TimeSpan.Zero : gaps.Min();
        return (
            [.. appointments.Select(a => new GroupBookingItem(a.TreatmentId, a.CustomerId, starts.IndexOf(a.Start), a.EmployeeId, a.RoomId, a.PlaceholderGuest))],
            [.. appointments.Select(a => new GroupBookingItem(a.TreatmentId, a.CustomerId, starts.IndexOf(a.Start), PlaceholderGuest: a.PlaceholderGuest))],
            [.. visits.Select(v => new GroupAreaItem(v.RoomId, v.CustomerId, starts.IndexOf(v.From), (int)(v.Until - v.From).TotalMinutes))],
            gap < TimeSpan.Zero ? TimeSpan.Zero : gap);
    }

    private static BookingOutcome.Invalid? Validate(IReadOnlyList<GroupBookingItem>? items, int gapMinutes, IReadOnlyList<GroupAreaItem>? areas = null,
        bool flexible = false)
    {
        areas ??= [];
        if (items is not { Count: >= 1 } || items.Count + areas.Count is < 2 or > MaxGroupItems)
        {
            return new BookingOutcome.Invalid("items", $"Mindestens eine Behandlung und zusammen mit Sauna oder Ruheraum zwei bis {MaxGroupItems} Teile angeben.");
        }

        if (areas.Any(a => a.Minutes is < 15 or > 240))
        {
            return new BookingOutcome.Invalid("areas", "Zeit in einem Bereich zwischen 15 und 240 Minuten.");
        }

        if (items.Any(i => i.PlaceholderGuest is < 1 or > MaxGroupItems))
        {
            return new BookingOutcome.Invalid("items", $"Gäste ohne Namen sind von 1 bis {MaxGroupItems} nummeriert.");
        }

        // Ein Gast ist ein Kunde oder ein Gast ohne Namen (Nummer); Gäste ohne Namen tragen die Id der buchenden Person.
        var all = items.Select(i => (i.Step, Guest: (i.CustomerId, i.PlaceholderGuest)))
            .Concat(areas.Select(a => (a.Step, Guest: (a.CustomerId, (int?)null)))).ToList();
        var steps = all.Select(i => i.Step).Distinct().Order().ToList();
        if (steps[0] != 0 || steps[^1] != steps.Count - 1)
        {
            return new BookingOutcome.Invalid("items", "Die Schritte müssen bei 0 beginnen und lückenlos aufeinander folgen.");
        }

        if (all.GroupBy(i => i.Step).Any(s => s.Select(i => i.Guest).Distinct().Count() < s.Count()))
        {
            return new BookingOutcome.Invalid("items", "Ein Gast kann nicht zwei Dinge gleichzeitig haben.");
        }

        if (flexible && steps.Count > GroupPlanner.MaxFlexibleSteps)
        {
            return new BookingOutcome.Invalid("flexibleOrder", $"Die Reihenfolge schlägt der Planer für höchstens {GroupPlanner.MaxFlexibleSteps} Abschnitte vor.");
        }

        return gapMinutes is >= 0 and <= 120 ? null : new BookingOutcome.Invalid("gapMinutes", "Zwischen 0 und 120 Minuten.");
    }

    /// <summary>Bereiche des Standorts mit ihren Belegungen am Tag; <c>null</c>, wenn ein Bereich fehlt.</summary>
    private async Task<List<GroupAreaPart>?> LoadAreaPartsAsync(
        Guid locationId, IReadOnlyList<GroupAreaItem> items, DateOnly date, IReadOnlyCollection<Guid> ignoreVisits, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var ids = items.Select(i => i.RoomId).Distinct().ToList();
        var areas = await db.Rooms.AsNoTracking().Where(r => ids.Contains(r.Id) && r.LocationId == locationId && r.IsActive && r.IsArea)
            .ToDictionaryAsync(r => r.Id, ct);
        if (areas.Count != ids.Count)
        {
            return null;
        }

        var zone = LocalTime.Zone(await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstAsync(ct));
        var (from, until) = LocalTime.Day(date, zone);
        var bookings = await db.ResourceBookings.AsNoTracking()
            .Where(b => ids.Contains(b.ResourceId) && b.From < until.AddHours(3) && from.AddHours(-3) < b.Until)
            .Where(b => b.AreaVisitId == null || !ignoreVisits.Contains(b.AreaVisitId.Value))
            .ToListAsync(ct);
        // Eine Raumsperre sperrt den Bereich wie eine Sperrzeit (ohne Termin und Eintritt); der Planer der Bereiche behandelt sie schon so.
        var closed = (await RoomClosureService.SpansAsync(db, locationId, from.AddHours(-3), until.AddHours(3), zone, ct)).Where(c => ids.Contains(c.RoomId))
            .Select(c => new ResourceBooking { Kind = ResourceKind.Room, ResourceId = c.RoomId, From = c.From, Until = c.Until, Reason = RoomClosureText.Title(c.Kind) }).ToList();
        return [.. items.Select(i => new GroupAreaPart(i, areas[i.RoomId], [.. bookings.Where(b => b.ResourceId == i.RoomId), .. closed.Where(b => b.ResourceId == i.RoomId)]))];
    }

    private Task<List<AreaVisit>> GroupVisitsAsync(Guid groupId, Guid locationId, CancellationToken ct) =>
        db.AreaVisits.Include(v => v.Bookings).Include(v => v.Customer).Where(v => v.GroupId == groupId && v.LocationId == locationId)
            .OrderBy(v => v.From).ThenBy(v => v.Id).ToListAsync(ct);

    private void ApplyArea(AreaVisit visit, PlannedArea area, DateTimeOffset now)
    {
        visit.From = area.From;
        visit.Until = area.Until;
        visit.UpdatedAt = now;
        var booking = new ResourceBooking
        {
            Kind = ResourceKind.Room,
            ResourceId = area.Area.Id,
            Slot = area.Slot,
            From = area.From,
            Until = area.Until,
            AreaVisitId = visit.Id,
        };
        db.ResourceBookings.Add(booking);
        visit.Bookings.Add(booking);
    }

    /// <summary>Planungskontext je Behandlung; gleiche Behandlungen teilen ihn. <c>null</c>, wenn eine Behandlung oder der Standort fehlt.</summary>
    private async Task<List<GroupPart>?> LoadPartsAsync(
        Guid locationId, IReadOnlyList<GroupBookingItem> items, DateOnly date, CancellationToken ct, IReadOnlyCollection<Guid>? ignore = null)
    {
        var contexts = new Dictionary<Guid, PlanningContext>();
        foreach (var treatmentId in items.Select(i => i.TreatmentId).Distinct())
        {
            if (await LoadContextIgnoringAsync(locationId, treatmentId, date, ignore ?? [], ct) is not { } context)
            {
                return null;
            }

            contexts[treatmentId] = context;
        }

        return [.. items.Select(i => new GroupPart(i, contexts[i.TreatmentId]))];
    }

    private async Task<BookingOutcome> PublishGroupAsync(
        Guid groupId, IEnumerable<(Appointment Appointment, string TreatmentName)> appointments, Func<Appointment, DateTimeOffset?>? previousStart = null,
        IReadOnlyList<AreaVisit>? visits = null)
    {
        var dtos = new List<AppointmentDto>();
        foreach (var (appointment, treatmentName) in appointments)
        {
            var dto = ToDto(appointment, treatmentName);
            await bus.PublishAsync(new AppointmentChangedMessage(dto, previousStart?.Invoke(appointment)));
            dtos.Add(dto);
        }

        return new BookingOutcome.GroupBooked(new GroupBookingResult(groupId, dtos,
            visits is { Count: > 0 } ? [.. visits.Select(AreaVisitService.ToDto)] : null));
    }
}
