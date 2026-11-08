using AispaStudio.Api.Online;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Demo;

/// <summary>Buchen im Speicher: dieselben Schritte wie <c>BookingService</c>, mit dem Planer und den gemeinsamen Bausteinen, aber ohne Datenbank.</summary>
internal sealed partial class MemoryDemoOps
{
    private readonly Dictionary<(Guid LocationId, DateOnly Date), DayStaff> _staff = [];
    private readonly Dictionary<Guid, int> _shortest = [];

    /// <summary>Wer an einem Tag an einem Standort Dienst hat (ändert sich während des Verlaufs nicht).</summary>
    private sealed record DayStaff(List<EmployeeShift> Shifts, List<Employee> Employees);

    public Task<List<AvailableSlot>> FreeSlotsAsync(Guid locationId, Guid treatmentId, DateOnly date, CancellationToken ct)
    {
        var context = BuildContext(locationId, treatmentId, date);
        return Task.FromResult(context is null ? [] : new ResourcePlanner(context).FreeSlots(date, TimeSpan.FromMinutes(15), null).ToList());
    }

    public Task<BookingOutcome> BookAsync(AppointmentRequest request, CancellationToken ct)
    {
        if (request.ClientId is not null)
        {
            throw new NotSupportedException("Der Verlauf im Speicher bucht nur ohne vorgegebene Id.");
        }

        return Task.FromResult(Book(request));
    }

    private BookingOutcome Book(AppointmentRequest request)
    {
        if (!_snapshot.Customers.TryGetValue(request.CustomerId, out var customer))
        {
            return new BookingOutcome.NotFound("Kunde");
        }

        var start = request.Start.ToUniversalTime();
        if (!_snapshot.Locations.TryGetValue(request.LocationId, out var location))
        {
            return new BookingOutcome.NotFound("Standort");
        }

        var context = BuildContext(request.LocationId, request.TreatmentId, LocalTime.LocalDate(start, Zone(location)));
        if (context is null)
        {
            return new BookingOutcome.NotFound("Behandlung");
        }

        var rules = RulesOf(customer);
        if (BookingService.NotWithMe(rules, context, request.EmployeeId) is { } excluded)
        {
            return excluded;
        }

        context = rules.Apply(context, request.EmployeeId);
        var result = new ResourcePlanner(context).Plan(start, request.EmployeeId, request.RoomId, request.EquipmentId);
        if (result.Plan is not { } plan)
        {
            return new BookingOutcome.Rejected(new BookingRejected(result.Message!, result.Conflicts));
        }

        var appointment = BookingService.NewAppointment(request, customer, context, plan, _clock.GetUtcNow(), null, AppointmentSource.Reception, null);
        _world.Appointments.Add(appointment);
        _world.AppointmentsById[appointment.Id] = appointment;
        foreach (var booking in appointment.Bookings)
        {
            _world.AddBooking(booking);
        }

        return new BookingOutcome.Success(BookingService.ToDto(appointment, context.Treatment.Name));
    }

    public Task<List<GroupSlot>> FreeGroupSlotsAsync(Guid locationId, GroupAvailabilityRequest request, CancellationToken ct)
    {
        var parts = GroupParts(locationId, request.Items, request.Date);
        var planner = parts is null ? null : new GroupPlanner(parts, TimeSpan.FromMinutes(request.GapMinutes));
        return Task.FromResult(planner is null ? [] : planner.FreeStarts(request.Date, TimeSpan.FromMinutes(15)).Select(p => p.ToSlot()).ToList());
    }

    /// <summary>Wie <c>BookingService.BookGroupAsync</c> ohne Bereiche und ohne vorgegebene Id: alle Termine der Gruppe oder keiner.</summary>
    public Task<BookingOutcome> BookGroupAsync(GroupBookingRequest request, CancellationToken ct)
    {
        if (request.ClientId is not null || request.Areas is { Count: > 0 })
        {
            throw new NotSupportedException("Der Verlauf im Speicher bucht Gruppen nur ohne vorgegebene Id und ohne Bereiche.");
        }

        return Task.FromResult(BookGroup(request));
    }

    private BookingOutcome BookGroup(GroupBookingRequest request)
    {
        var customers = new Dictionary<Guid, Customer>();
        foreach (var id in request.Items.Select(i => i.CustomerId).Distinct())
        {
            if (!_snapshot.Customers.TryGetValue(id, out var customer))
            {
                return new BookingOutcome.NotFound("Kunde");
            }

            customers[id] = customer;
        }

        if (!_snapshot.Locations.TryGetValue(request.LocationId, out var location))
        {
            return new BookingOutcome.NotFound("Standort");
        }

        var start = request.Start.ToUniversalTime();
        if (GroupParts(request.LocationId, request.Items, LocalTime.LocalDate(start, Zone(location))) is not { } parts)
        {
            return new BookingOutcome.NotFound("Behandlung");
        }

        var result = new GroupPlanner(parts, TimeSpan.FromMinutes(request.GapMinutes)).Plan(start);
        if (result.Plan is not { } plan)
        {
            return new BookingOutcome.Rejected(new BookingRejected(result.Message!, result.Conflicts));
        }

        var now = _clock.GetUtcNow();
        var groupId = Guid.CreateVersion7();
        var appointments = new List<AppointmentDto>();
        for (var i = 0; i < parts.Count; i++)
        {
            var treatment = parts[i].Context.Treatment;
            var appointment = new Appointment
            {
                LocationId = request.LocationId,
                TreatmentId = treatment.Id,
                CustomerId = parts[i].Item.CustomerId,
                Customer = customers[parts[i].Item.CustomerId],
                GroupId = groupId,
                PlaceholderGuest = parts[i].Item.PlaceholderGuest,
                Price = treatment.PriceAt(request.LocationId, plan.Appointments[i].Times.Start, parts[i].Context.Zone),
                Note = request.Note,
                CreatedAt = now,
            };
            BookingService.ApplyPlan(appointment, plan.Appointments[i], now);
            _world.Appointments.Add(appointment);
            _world.AppointmentsById[appointment.Id] = appointment;
            foreach (var booking in appointment.Bookings)
            {
                _world.AddBooking(booking);
            }

            appointments.Add(BookingService.ToDto(appointment, treatment.Name));
        }

        return new BookingOutcome.GroupBooked(new GroupBookingResult(groupId, appointments));
    }

    /// <summary>Planungskontext je Behandlung; gleiche Behandlungen teilen ihn (wie <c>BookingService.LoadPartsAsync</c>).</summary>
    private List<GroupPart>? GroupParts(Guid locationId, IReadOnlyList<GroupBookingItem> items, DateOnly date)
    {
        var contexts = new Dictionary<Guid, PlanningContext>();
        foreach (var treatmentId in items.Select(i => i.TreatmentId).Distinct())
        {
            if (BuildContext(locationId, treatmentId, date) is not { } context)
            {
                return null;
            }

            contexts[treatmentId] = context;
        }

        return [.. items.Select(i => new GroupPart(i, contexts[i.TreatmentId]))];
    }

    public Task<BookingOutcome> CancelAsync(Guid appointmentId, string? reason, CancellationToken ct)
    {
        if (!_world.AppointmentsById.TryGetValue(appointmentId, out var appointment))
        {
            return Task.FromResult<BookingOutcome>(new BookingOutcome.NotFound("Termin"));
        }

        if (appointment.Status != AppointmentStatus.Cancelled)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelReason = reason;
            appointment.UpdatedAt = _clock.GetUtcNow();
            _world.RemoveBookings(appointment.Bookings);
            appointment.Bookings.Clear();
        }

        return Task.FromResult<BookingOutcome>(new BookingOutcome.Success(BookingService.ToDto(appointment, TreatmentName(appointment))));
    }

    public Task<BookingOutcome> ChangeStatusAsync(Guid appointmentId, AppointmentStatus status, CancellationToken ct)
    {
        if (status == AppointmentStatus.Cancelled)
        {
            return Task.FromResult<BookingOutcome>(new BookingOutcome.Invalid(nameof(status), "Zum Stornieren den Endpunkt „cancel“ verwenden."));
        }

        if (!_world.AppointmentsById.TryGetValue(appointmentId, out var appointment))
        {
            return Task.FromResult<BookingOutcome>(new BookingOutcome.NotFound("Termin"));
        }

        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            return Task.FromResult<BookingOutcome>(new BookingOutcome.Invalid(nameof(status), "Der Termin ist storniert."));
        }

        appointment.Status = status;
        appointment.UpdatedAt = _clock.GetUtcNow();
        return Task.FromResult<BookingOutcome>(new BookingOutcome.Success(BookingService.ToDto(appointment, TreatmentName(appointment))));
    }

    public Task AddOnlineBookingsAsync(IReadOnlyCollection<OnlineBooking> bookings, CancellationToken ct)
    {
        _world.OnlineBookings.AddRange(bookings);
        return Task.CompletedTask;
    }

    public Task AddFollowUpsAsync(IReadOnlyCollection<AppointmentFollowUp> followUps, CancellationToken ct)
    {
        _world.FollowUps.AddRange(followUps);
        return Task.CompletedTask;
    }

    private string TreatmentName(Appointment appointment) => _snapshot.Treatments[appointment.TreatmentId].Name;

    private CustomerRules RulesOf(Customer customer) =>
        CustomerRules.From(_snapshot.Flags.TryGetValue(customer.Id, out var flags) ? flags : [], customer.PreferredGender);

    private TimeZoneInfo Zone(Location location) => Zone(location.TimeZone);

    private TimeZoneInfo Zone(string id)
    {
        if (!_zones.TryGetValue(id, out var zone))
        {
            _zones[id] = zone = LocalTime.Zone(id);
        }

        return zone;
    }

    /// <summary>Wie <c>BookingService.LoadContextAsync</c>, aber aus dem Speicher.</summary>
    private PlanningContext? BuildContext(Guid locationId, Guid treatmentId, DateOnly date)
    {
        if (!_snapshot.Locations.TryGetValue(locationId, out var location)
            || !_snapshot.Treatments.TryGetValue(treatmentId, out var treatment) || !treatment.IsActive)
        {
            return null;
        }

        var zone = Zone(location);
        var (rooms, equipment) = BookingService.ResourcesAt(treatment, locationId);
        var staff = StaffOn(locationId, date, zone);
        var (from, until) = BookingService.BookingWindow(date, zone);
        var bookings = new List<ResourceBooking>();
        foreach (var id in staff.Employees.Select(e => e.Id).Concat(rooms.Select(r => r.Id)).Concat(equipment.Select(e => e.Id)))
        {
            if (_world.BookingsByResource.TryGetValue(id, out var list))
            {
                bookings.AddRange(list.Where(b => b.From < until && from < b.Until));
            }
        }

        return new PlanningContext(zone, location.OpeningHours, treatment, staff.Employees, rooms, equipment, bookings, locationId, staff.Shifts,
            ShortestAt(locationId));
    }

    private DayStaff StaffOn(Guid locationId, DateOnly date, TimeZoneInfo zone)
    {
        if (_staff.TryGetValue((locationId, date), out var cached))
        {
            return cached;
        }

        // Aktive Personen, die dort regelmäßig arbeiten oder im Dienstplan ab diesem Tag dort eingeteilt sind (RosterService.EmployeesAt).
        var at = _snapshot.Employees.Where(e => e.IsActive
            && (e.WorkingHours.Any(w => w.LocationId == locationId)
                || (_snapshot.RosterDaysByEmployee.TryGetValue(e.Id, out var days)
                    && days.Any(d => d.Date >= date && d.Entries.Any(x => x.LocationId == locationId))))).ToList();
        var plans = at.ToDictionary(e => (e.Id, date), e => DayPlan.For(e, _snapshot.RosterDays.GetValueOrDefault((e.Id, date)), date));
        var shifts = RosterService.ShiftsOf(at, plans, locationId, date, zone);
        var working = shifts.Select(s => s.EmployeeId).ToHashSet();
        return _staff[(locationId, date)] = new DayStaff(shifts, [.. _snapshot.Employees.Where(e => working.Contains(e.Id))]);
    }

    /// <summary>Kürzeste Behandlung des Standorts mit Vor- und Nachbereitung: Lücken darunter bleiben ungenutzt.</summary>
    private int ShortestAt(Guid locationId)
    {
        if (!_shortest.TryGetValue(locationId, out var shortest))
        {
            _shortest[locationId] = shortest = _snapshot.Treatments.Values
                .Where(t => t.IsActive && t.Rooms.Any(r => r.LocationId == locationId && r.IsActive))
                .Select(t => (int?)(t.DurationMinutes + t.PreparationMinutes + t.FollowUpMinutes)).Min() ?? 30;
        }

        return shortest;
    }
}
