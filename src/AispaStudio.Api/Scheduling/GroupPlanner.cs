using AispaStudio.Contracts;

namespace AispaStudio.Api.Scheduling;

/// <summary>Behandlung einer gemeinsamen Buchung mit dem Planungskontext ihrer Behandlung.</summary>
internal sealed record GroupPart(GroupBookingItem Item, PlanningContext Context);

/// <summary>Zeit in einem Bereich (Sauna, Ruheraum) als Teil eines Spa-Tags, mit den Belegungen des Bereichs an diesem Tag.</summary>
internal sealed record GroupAreaPart(GroupAreaItem Item, Room Area, IReadOnlyList<ResourceBooking> Bookings);

/// <summary>Geplante Zeit in einem Bereich: ein Platz für den Gast.</summary>
internal sealed record PlannedArea(GroupAreaItem Item, Room Area, DateTimeOffset From, DateTimeOffset Until, int Slot);

/// <summary>Geplante gemeinsame Buchung; <c>Appointments</c> und <c>Areas</c> in derselben Reihenfolge wie die Anfrage.</summary>
internal sealed record GroupPlan(IReadOnlyList<PlannedAppointment> Appointments, bool SharedRoom, IReadOnlyList<PlannedArea> Areas)
{
    public DateTimeOffset Start => Appointments.Select(a => a.Times.Start).Concat(Areas.Select(a => a.From)).Min();

    public DateTimeOffset End => Appointments.Select(a => a.Times.End).Concat(Areas.Select(a => a.Until)).Max();

    public GroupSlot ToSlot() => new(Start, End, SharedRoom,
        [.. Appointments.Select(a => new GroupSlotItem(a.Times.Start, a.Times.End, a.Employee.Id, a.Room.Id, a.Equipment?.Id))],
        Areas.Count == 0 ? null : [.. Areas.Select(a => new GroupSlotArea(a.From, a.Until, a.Area.Id, a.Item.CustomerId))]);
}

internal sealed record GroupPlanResult(GroupPlan? Plan, string? Message, IReadOnlyList<BookingConflict> Conflicts);

/// <summary>
/// Plant Paar- und Spa-Tag-Buchungen mit dem <see cref="ResourcePlanner"/>: Behandlungen eines Schritts beginnen gleichzeitig, jeder weitere
/// Schritt nach dem Ende des vorigen plus Pause. Gleichzeitige Gäste kommen bevorzugt gemeinsam in einen Mehrplatzraum (Paarsuite),
/// sonst in getrennte Räume. Zeiten in Sauna oder Ruheraum sind eigene Schritte und belegen je Gast einen Platz.
/// <c>flexible</c> (Baukasten): die Schritte dürfen in jeder Reihenfolge kommen und vor einem Schritt bis zu <see cref="MaxWait"/> warten;
/// genommen wird die Reihenfolge, mit der der Tag am frühesten endet, bei Gleichstand die angefragte. Reine Logik ohne Datenbank.
/// </summary>
internal sealed class GroupPlanner(IReadOnlyList<GroupPart> parts, TimeSpan gap, IReadOnlyList<GroupAreaPart>? areas = null, bool flexible = false)
{
    public const int MaxFlexibleSteps = 4;

    public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan WaitStep = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyList<GroupAreaPart> _areas = areas ?? [];

    public GroupPlanResult Plan(DateTimeOffset start)
    {
        var steps = parts.Select(p => p.Item.Step).Concat(_areas.Select(a => a.Item.Step)).Distinct().Order().ToList();
        if (!flexible)
        {
            return PlanOrder(start, steps, wait: false);
        }

        GroupPlanResult? best = null;
        foreach (var order in Permutations(steps))
        {
            var result = PlanOrder(start, order, wait: true);
            if (result.Plan is { } plan && (best?.Plan is not { } current || plan.End < current.End))
            {
                best = result;
            }

            best ??= result;
        }

        return best!;
    }

    /// <summary>Startzeiten eines Tages im Raster, zu denen die ganze Buchung passt.</summary>
    public IEnumerable<GroupPlan> FreeStarts(DateOnly date, TimeSpan step)
    {
        var planner = new ResourcePlanner(parts[0].Context);
        foreach (var window in planner.OpeningWindows(date))
        {
            for (var start = window.Start; start < window.End; start += step)
            {
                if (Plan(start).Plan is { } plan)
                {
                    yield return plan;
                }
            }
        }
    }

    private GroupPlanResult PlanOrder(DateTimeOffset start, IReadOnlyList<int> order, bool wait)
    {
        var planned = new PlannedAppointment[parts.Count];
        var plannedAreas = new PlannedArea[_areas.Count];
        var reserved = new List<ResourceBooking>();
        var shared = false;
        var stepStart = start;
        foreach (var (stepNumber, position) in order.Select((s, i) => (s, i)))
        {
            var members = parts.Select((part, index) => (Part: part, Index: index)).Where(x => x.Part.Item.Step == stepNumber).ToList();
            var areaMembers = _areas.Select((area, index) => (Area: area, Index: index)).Where(x => x.Area.Item.Step == stepNumber).ToList();
            GroupPlanResult? failure = null;
            var done = false;
            // Der erste Schritt beginnt genau zur gesuchten Zeit, sonst gäbe es dieselbe Buchung unter mehreren Startzeiten.
            var latest = wait && position > 0 ? stepStart + MaxWait : stepStart;
            for (var at = stepStart; at <= latest && !done; at += WaitStep)
            {
                List<PlannedAppointment>? plans = [];
                var sharedRoom = false;
                if (members.Count > 0)
                {
                    (plans, sharedRoom, failure) = PlanStep([.. members.Select(m => m.Part)], at, reserved);
                }

                if (plans is null)
                {
                    continue;
                }

                var (areaPlans, areaFailure) = PlanAreas([.. areaMembers.Select(m => m.Area)], at, reserved);
                if (areaPlans is null)
                {
                    failure = areaFailure;
                    continue;
                }

                shared |= sharedRoom;
                for (var i = 0; i < members.Count; i++)
                {
                    planned[members[i].Index] = plans[i];
                    reserved.AddRange(Reservations(plans[i]));
                }

                for (var i = 0; i < areaMembers.Count; i++)
                {
                    plannedAreas[areaMembers[i].Index] = areaPlans[i];
                    reserved.Add(new ResourceBooking
                    {
                        Kind = ResourceKind.Room,
                        ResourceId = areaPlans[i].Area.Id,
                        Slot = areaPlans[i].Slot,
                        From = areaPlans[i].From,
                        Until = areaPlans[i].Until,
                        AreaVisitId = Guid.Empty,
                    });
                }

                stepStart = plans.Select(p => p.Times.End).Concat(areaPlans.Select(a => a.Until)).Max() + gap;
                done = true;
            }

            if (!done)
            {
                return failure ?? new GroupPlanResult(null, "Zu dieser Zeit passt der Ablauf nicht.", []);
            }
        }

        return new GroupPlanResult(new GroupPlan(planned, shared, plannedAreas), null, []);
    }

    /// <summary>Je Gast ein Platz im Bereich, solange der Standort durchgehend geöffnet ist und der Bereich nicht gesperrt.</summary>
    private (List<PlannedArea>? Plans, GroupPlanResult? Failure) PlanAreas(IReadOnlyList<GroupAreaPart> members, DateTimeOffset start, List<ResourceBooking> reserved)
    {
        var plans = new List<PlannedArea>();
        var planner = new ResourcePlanner(parts[0].Context);
        foreach (var member in members)
        {
            var until = start.AddMinutes(member.Item.Minutes);
            if (!planner.OpeningWindows(LocalTime.LocalDate(start, parts[0].Context.Zone)).Any(w => w.Start <= start && until <= w.End))
            {
                return (null, new GroupPlanResult(null, $"{member.Area.Name}: Der Standort hat in dieser Zeit nicht durchgehend geöffnet.", []));
            }

            var bookings = member.Bookings.Concat(reserved.Where(b => b.ResourceId == member.Area.Id))
                .Concat(plans.Where(p => p.Area.Id == member.Area.Id).Select(p => new ResourceBooking
                {
                    Kind = ResourceKind.Room,
                    ResourceId = p.Area.Id,
                    Slot = p.Slot,
                    From = p.From,
                    Until = p.Until,
                    AreaVisitId = Guid.Empty,
                }));
            if (AreaPlanner.FreePlaces(member.Area.Places, bookings, start, until, 1) is not [var slot])
            {
                return (null, new GroupPlanResult(null, $"Im Bereich {member.Area.Name} ist nicht genug Platz.",
                    [new BookingConflict(ResourceKind.Room, member.Area.Id, member.Area.Name, "ist in dieser Zeit voll oder gesperrt")]));
            }

            plans.Add(new PlannedArea(member.Item, member.Area, start, until, slot));
        }

        return (plans, null);
    }

    private static IEnumerable<IReadOnlyList<int>> Permutations(List<int> items)
    {
        if (items.Count <= 1)
        {
            yield return items;
            yield break;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, j) => j != i).ToList();
            foreach (var tail in Permutations(rest))
            {
                yield return [items[i], .. tail];
            }
        }
    }

    private static (List<PlannedAppointment>? Plans, bool Shared, GroupPlanResult? Failure) PlanStep(
        IReadOnlyList<GroupPart> members, DateTimeOffset start, List<ResourceBooking> reserved)
    {
        GroupPlanResult? failure = null;
        foreach (var room in SharedRoomCandidates(members))
        {
            var (plans, stepFailure) = TryStep(members, start, reserved, room);
            if (plans is not null)
            {
                return (plans, true, null);
            }

            failure ??= stepFailure;
        }

        var (separate, separateFailure) = TryStep(members, start, reserved, null);
        return separate is not null ? (separate, false, null) : (null, false, separateFailure ?? failure);
    }

    /// <summary>
    /// Mehrplatzräume, in die alle gleichzeitigen Gäste passen und die für alle ihre Behandlungen geeignet sind, kleinste zuerst.
    /// Haben alle denselben Raum gewünscht, nur dieser; bei unterschiedlichen Wünschen keiner.
    /// </summary>
    private static IEnumerable<Room> SharedRoomCandidates(IReadOnlyList<GroupPart> members)
    {
        if (members.Count < 2)
        {
            return [];
        }

        var wanted = members.Select(m => m.Item.RoomId).Distinct().ToList();
        if (wanted is not [var only] || (only is null && members.Any(m => m.Item.RoomId is not null)))
        {
            return [];
        }

        return members[0].Context.Rooms
            .Where(r => r.Places >= members.Count && (only is null || r.Id == only))
            .Where(r => members.All(m => m.Context.Rooms.Any(x => x.Id == r.Id)))
            .OrderBy(r => r.Places);
    }

    private static (List<PlannedAppointment>? Plans, GroupPlanResult? Failure) TryStep(
        IReadOnlyList<GroupPart> members, DateTimeOffset start, List<ResourceBooking> reserved, Room? shared)
    {
        var plans = new List<PlannedAppointment>();
        var local = new List<ResourceBooking>();
        foreach (var member in members)
        {
            // Im gemeinsamen Raum stören sich die Gäste des Schritts nicht; alles andere ist schon vergeben.
            var context = member.Context with
            {
                Bookings = [.. member.Context.Bookings, .. reserved, .. local.Where(b => b.ResourceId != shared?.Id)],
            };
            var result = new ResourcePlanner(context).Plan(start, member.Item.EmployeeId, shared?.Id ?? member.Item.RoomId);
            if (result.Plan is not { } plan)
            {
                return (null, new GroupPlanResult(null, $"{member.Context.Treatment.Name}: {result.Message}", result.Conflicts));
            }

            plans.Add(plan);
            local.AddRange(Reservations(plan));
        }

        if (shared is null)
        {
            return (plans, null);
        }

        // Jeder Gast belegt einen eigenen Platz, alle für die ganze Zeit, in der die Gruppe den Raum braucht (inklusive Raumpflege).
        var roomFrom = plans.Min(p => p.Times.BlockedFrom);
        var roomUntil = plans.Max(p => p.Times.RoomUntil);
        return ([.. plans.Select((p, i) => p with { RoomSlot = i, RoomFrom = roomFrom, Times = p.Times with { RoomUntil = roomUntil } })], null);
    }

    private static IEnumerable<ResourceBooking> Reservations(PlannedAppointment plan) =>
        plan.Bookings().Select(b => new ResourceBooking { Kind = b.Kind, ResourceId = b.Id, Slot = b.Slot, From = b.From, Until = b.Until });
}
