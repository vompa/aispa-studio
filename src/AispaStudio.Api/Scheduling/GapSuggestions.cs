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
    public Task<List<GapSuggestion>?> GapSuggestionsAsync(Guid locationId, DateOnly date, CancellationToken ct) =>
        GapSuggestionsAsync(locationId, date, null, ct);

    /// <summary>Wie <see cref="GapSuggestionsAsync(Guid, DateOnly, CancellationToken)"/> für mehrere Tage in Folge (7-Tage-Ansicht), auf Wunsch nur für eine Person.</summary>
    public async Task<List<GapSuggestion>?> GapSuggestionsAsync(Guid locationId, DateOnly from, int days, Guid? employeeId, CancellationToken ct)
    {
        var all = new List<GapSuggestion>();
        for (var i = 0; i < days; i++)
        {
            if (await GapSuggestionsAsync(locationId, from.AddDays(i), employeeId, ct) is not { } day)
            {
                return null;
            }

            all.AddRange(day);
        }

        return all;
    }

    private async Task<List<GapSuggestion>?> GapSuggestionsAsync(Guid locationId, DateOnly date, Guid? employeeId, CancellationToken ct)
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
        if (employeeId is { } only)
        {
            shifts = [.. shifts.Where(s => s.EmployeeId == only)];
        }

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
        var useful = await ShortestBindingAsync(locationId, ct) ?? 30;

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

                // Einwirkzeit: Die Pause zwischen den beiden Belegungen desselben Termins ist gewollt und keine Lücke.
                if (previous?.AppointmentId is { } sameAppointment && next?.AppointmentId == sameAppointment)
                {
                    continue;
                }

                // Erst den Termin danach vorziehen (der Gast ist früher fertig), sonst den davor nach hinten schieben.
                var candidates = new[] { (Booking: next, Minutes: -gap), (Booking: previous, Minutes: gap) };
                var handled = false;
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
                        $"Bei {name} bleiben sonst {gap} Minuten frei, in die keine Behandlung passt (kürzeste {useful} Minuten). {reason}",
                        appointment.Source));
                    suggested.Add(id);
                    handled = true;
                    break;
                }

                // Geht es allein nicht, rücken die folgenden Termine mit (Kette), sofern der Termin danach überhaupt verschiebbar ist.
                if (!handled && next?.AppointmentId is { } first && !suggested.Contains(first) && movable.ContainsKey(first)
                    && await ChainAsync(own, i, gap, useful, shift.End, movable, suggested, now, date, ct) is { } chain)
                {
                    var name = names.GetValueOrDefault(shift.EmployeeId, "");
                    var lead = chain.Moves[0];
                    suggestions.Add(new GapSuggestion(lead.Appointment.Id, shift.EmployeeId, name,
                        lead.Appointment.Customer.InHouseName, lead.Appointment.Treatment.Name,
                        lead.Appointment.Start, lead.Start, gap,
                        $"Bei {name} bleiben sonst {gap} Minuten frei, in die keine Behandlung passt (kürzeste {useful} Minuten). "
                        + $"Dafür rücken {chain.Moves.Count} Termine nacheinander um {gap} Minuten vor. {chain.Reason}",
                        lead.Appointment.Source,
                        [.. chain.Moves.Skip(1).Select(m => new GapChainMove(m.Appointment.Id, m.Appointment.Customer.InHouseName, m.Appointment.Treatment.Name,
                            m.Appointment.Start, m.Start, m.Appointment.Source))]));
                    suggested.UnionWith(chain.Moves.Select(m => m.Appointment.Id));
                }
            }
        }

        return [.. suggestions.OrderBy(s => s.CurrentStart)];
    }

    /// <summary>Höchstens so viele Termine rücken in einer Kette mit; mehr wäre für die Rezeption zu viel abzusprechen.</summary>
    internal const int MaxChain = 4;

    private sealed record ChainStep(Appointment Appointment, DateTimeOffset Start);

    private sealed record Chain(List<ChainStep> Moves, string Reason);

    /// <summary>
    /// Kette ab der Belegung <paramref name="from"/>: die Termine danach rücken so lange mit um <paramref name="gap"/> Minuten vor, bis hinter dem letzten
    /// eine brauchbare Zeit (oder das Dienstende mit brauchbarer Zeit) bleibt. Alle müssen gebucht, einzeln und noch nicht begonnen sein; jeder muss zur neuen Zeit
    /// bei derselben Person planbar sein, wobei die Belegungen der Kette selbst als frei zählen. Null, wenn es keine solche Kette mit zwei bis
    /// <see cref="MaxChain"/> Terminen gibt.
    /// </summary>
    private async Task<Chain?> ChainAsync(List<ResourceBooking> own, int from, int gap, int useful, DateTimeOffset shiftEnd, Dictionary<Guid, Appointment> movable,
        HashSet<Guid> suggested, DateTimeOffset now, DateOnly date, CancellationToken ct)
    {
        var members = new List<Appointment>();
        var closed = false;
        for (var j = from; j < own.Count && members.Count < MaxChain;)
        {
            if (own[j].AppointmentId is not { } id || !movable.TryGetValue(id, out var appointment) || suggested.Contains(id) || appointment.Start.AddMinutes(-gap) <= now)
            {
                return null;
            }

            members.Add(appointment);
            // Alle Belegungen dieses Termins (Einwirkzeit: zwei) gehören zusammen; danach kommt die nächste fremde Belegung.
            var end = own.Where(b => b.AppointmentId == id).Max(b => b.Until);
            j = own.FindIndex(j, b => b.AppointmentId != id && b.From >= end);
            var after = j < 0 ? shiftEnd : own[j].From < shiftEnd ? own[j].From : shiftEnd;
            if ((int)(after - end).TotalMinutes + gap >= useful)
            {
                closed = true;
                break;
            }

            if (j < 0)
            {
                break;
            }
        }

        if (!closed || members.Count < 2)
        {
            return null;
        }

        var ids = members.Select(m => m.Id).ToList();
        var moves = new List<ChainStep>();
        string? reason = null;
        foreach (var member in members)
        {
            var start = member.Start.AddMinutes(-gap);
            var context = await LoadContextIgnoringAsync(member.LocationId, member.TreatmentId, date, ids, ct);
            if (context is null)
            {
                return null;
            }

            var planner = new ResourcePlanner(WithTimesOf(member, context));
            var plan = planner.Plan(start, member.EmployeeId, member.RoomId, member.EquipmentId).Plan ?? planner.Plan(start, member.EmployeeId).Plan;
            if (plan is null)
            {
                return null;
            }

            if (member == members[^1])
            {
                var (fit, why) = planner.Fit(plan);
                if (fit == SlotFit.LeavesGap)
                {
                    return null;
                }

                reason = why;
            }

            moves.Add(new ChainStep(member, start));
        }

        return new Chain(moves, reason ?? "");
    }

    /// <summary>
    /// Verschiebt eine Kette aus den Lückenvorschlägen: in der Reihenfolge, in der jeder Termin in schon freie Zeit rückt (beim Vorziehen der früheste zuerst),
    /// jeweils bei derselben Person. Scheitert ein Schritt, gehen die schon verschobenen Termine an ihre alte Zeit zurück und die Antwort nennt den Grund.
    /// </summary>
    public async Task<BookingOutcome> MoveChainAsync(IReadOnlyList<GapChainStep> steps, CancellationToken ct)
    {
        if (steps.Count is < 2 or > MaxChain || steps.Select(s => s.AppointmentId).Distinct().Count() != steps.Count)
        {
            return new BookingOutcome.Invalid(nameof(GapChainRequest.Moves), $"Eine Kette hat zwei bis {MaxChain} verschiedene Termine.");
        }

        var ids = steps.Select(s => s.AppointmentId).ToList();
        var current = await db.Appointments.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Start, a.EmployeeId, a.RoomId, a.EquipmentId }).ToDictionaryAsync(a => a.Id, ct);
        if (current.Count != steps.Count)
        {
            return new BookingOutcome.NotFound("Termin");
        }

        var earlier = steps.All(s => s.Start < current[s.AppointmentId].Start);
        var later = steps.All(s => s.Start > current[s.AppointmentId].Start);
        if (!earlier && !later)
        {
            return new BookingOutcome.Invalid(nameof(GapChainRequest.Moves), "Alle Termine einer Kette rücken in dieselbe Richtung.");
        }

        var ordered = earlier ? steps.OrderBy(s => current[s.AppointmentId].Start).ToList() : steps.OrderByDescending(s => current[s.AppointmentId].Start).ToList();
        var done = new List<Guid>();
        foreach (var step in ordered)
        {
            var before = current[step.AppointmentId];
            var outcome = await MoveAsync(step.AppointmentId, new MoveAppointmentRequest(step.Start, before.EmployeeId, null, null), ct, house: true);
            if (outcome is BookingOutcome.Success)
            {
                done.Add(step.AppointmentId);
                continue;
            }

            foreach (var id in Enumerable.Reverse(done))
            {
                var old = current[id];
                await MoveAsync(id, new MoveAppointmentRequest(old.Start, old.EmployeeId, old.RoomId, old.EquipmentId), ct, house: true);
            }

            return outcome;
        }

        var moved = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment).Where(a => ids.Contains(a.Id))
            .OrderBy(a => a.Start).ToListAsync(ct);
        return new BookingOutcome.ChainMoved([.. moved.Select(a => ToDto(a, a.Treatment.Name))]);
    }

    /// <summary>Kürzeste Behandlung des Standorts mit Vor- und Nachbereitung, in Minuten: Lücken darunter bleiben ungenutzt.</summary>
    private Task<int?> ShortestBindingAsync(Guid locationId, CancellationToken ct) =>
        db.Treatments.AsNoTracking()
            .Where(t => t.IsActive && t.Rooms.Any(r => r.LocationId == locationId && r.IsActive))
            .Select(t => (int?)(t.DurationMinutes + t.PreparationMinutes + t.FollowUpMinutes))
            .MinAsync(ct);

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
