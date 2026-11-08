using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

internal sealed partial class BookingService
{
    /// <summary>
    /// Sucht im Tag jeder Person Lücken, in die keine Behandlung des Standorts passt, und schlägt vor, den Termin direkt danach
    /// um die Lücke vorzuziehen oder den davor um die Lücke nach hinten zu schieben. Nur gebuchte, noch nicht begonnene Einzeltermine;
    /// verschoben wird nur, wenn der Planer die neue Zeit mit derselben Person zulässt und dort keine neue unbrauchbare Lücke entsteht.
    /// Es sind Vorschläge: verschoben wird erst, wenn die Rezeption es mit dem Gast abgesprochen hat.
    /// </summary>
    public async Task<List<GapSuggestion>?> GapSuggestionsAsync(Guid locationId, DateOnly date, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(location.TimeZone);
        var (dayFrom, dayUntil) = LocalTime.Day(date, zone);
        var now = time.GetUtcNow();
        var shifts = await roster.ShiftsAsync(locationId, date, zone, ct);
        if (shifts.Count == 0)
        {
            return [];
        }

        var employeeIds = shifts.Select(s => s.EmployeeId).Distinct().ToList();
        var bookings = await db.ResourceBookings.AsNoTracking()
            .Where(b => employeeIds.Contains(b.ResourceId) && b.From < dayUntil.AddHours(3) && dayFrom.AddHours(-3) < b.Until)
            .ToListAsync(ct);
        var movable = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.LocationId == locationId && a.Status == AppointmentStatus.Booked && a.GroupId == null
                && a.Start >= dayFrom && a.Start < dayUntil && a.Start > now)
            .ToDictionaryAsync(a => a.Id, ct);
        var names = await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        var useful = await db.Treatments.AsNoTracking()
            .Where(t => t.IsActive && t.Rooms.Any(r => r.LocationId == locationId && r.IsActive))
            .Select(t => (int?)(t.DurationMinutes + t.PreparationMinutes + t.FollowUpMinutes))
            .MinAsync(ct) ?? 30;

        var suggestions = new List<GapSuggestion>();
        var suggested = new HashSet<Guid>();
        foreach (var shift in shifts.OrderBy(s => s.Start))
        {
            var own = bookings.Where(b => b.ResourceId == shift.EmployeeId && b.From < shift.End && shift.Start < b.Until)
                .OrderBy(b => b.From).ToList();
            for (var i = 0; i <= own.Count; i++)
            {
                var previous = i > 0 ? own[i - 1] : null;
                var next = i < own.Count ? own[i] : null;
                var gapFrom = previous is null ? shift.Start : previous.Until > shift.Start ? previous.Until : shift.Start;
                var gapUntil = next is null ? shift.End : next.From < shift.End ? next.From : shift.End;
                var gap = (int)(gapUntil - gapFrom).TotalMinutes;
                if (gap <= 0 || gap >= useful)
                {
                    continue;
                }

                // Erst den Termin danach vorziehen (der Gast ist früher fertig), sonst den davor nach hinten schieben.
                var candidates = new[] { (Booking: next, Minutes: -gap), (Booking: previous, Minutes: gap) };
                foreach (var (booking, minutes) in candidates)
                {
                    if (booking?.AppointmentId is not { } id || !movable.TryGetValue(id, out var appointment) || suggested.Contains(id))
                    {
                        continue;
                    }

                    var start = appointment.Start.AddMinutes(minutes);
                    if (start <= now || await PlanSameEmployeeAsync(appointment, start, date, ct) is not { } reason)
                    {
                        continue;
                    }

                    var name = names.GetValueOrDefault(shift.EmployeeId, "");
                    suggestions.Add(new GapSuggestion(appointment.Id, shift.EmployeeId, name,
                        appointment.Customer.InHouseName, appointment.Treatment.Name,
                        appointment.Start, start, gap,
                        $"Bei {name} bleiben sonst {gap} Minuten frei, in die keine Behandlung passt (kürzeste {useful} Minuten). {reason}"));
                    suggested.Add(id);
                    break;
                }
            }
        }

        return [.. suggestions.OrderBy(s => s.CurrentStart)];
    }

    /// <summary>Ob der Termin zur neuen Zeit bei derselben Person geplant werden kann, ohne eine neue unbrauchbare Lücke; dann die Bewertung.</summary>
    private async Task<string?> PlanSameEmployeeAsync(Appointment appointment, DateTimeOffset start, DateOnly date, CancellationToken ct)
    {
        var context = await LoadContextAsync(appointment.LocationId, appointment.TreatmentId, date, appointment.Id, ct);
        if (context is null)
        {
            return null;
        }

        var planner = new ResourcePlanner(WithTimesOf(appointment, context));
        var plan = planner.Plan(start, appointment.EmployeeId, appointment.RoomId, appointment.EquipmentId).Plan
            ?? planner.Plan(start, appointment.EmployeeId).Plan;
        if (plan is null)
        {
            return null;
        }

        var (fit, reason) = planner.Fit(plan);
        return fit == SlotFit.LeavesGap ? null : reason;
    }
}
