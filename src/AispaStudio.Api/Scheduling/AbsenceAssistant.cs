using System.Globalization;
using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Ausfall-Assistent: fällt eine Person aus, schlägt er für jeden ihrer Termine Ersatz vor und erklärt ihn in einem Satz.
/// Zuerst gleiche Zeit bei einer qualifizierten Kollegin (mit dem Raum, wenn er passt), sonst die nächste freie Zeit am selben Tag.
/// Bei mehreren Kandidatinnen bekommt die mit der wenigsten Belegung an diesem Tag den Termin. Geht hier gar nichts, sucht er an den
/// anderen Standorten des Spas, die die Person am Empfang sieht (gleiche Zeit zuerst); solche Vorschläge sind nicht vorausgewählt,
/// weil der Gast zustimmen muss. Der Vorschlag ändert nichts; erst „Übernehmen“ verschiebt, über denselben Weg wie das Verschieben
/// im Kalender (Prüfung am Server, Doppelbuchungsschutz).
/// </summary>
internal sealed class AbsenceAssistant(AispaDbContext db, BookingService booking, IMessageBus bus, TenantContext tenants, TimeProvider time)
{
    private static readonly AppointmentStatus[] Open = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn];
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    public async Task<AbsenceProposal?> ProposeAsync(Guid locationId, AbsenceProposalRequest request, UserAccess? access, CancellationToken ct)
    {
        var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        if (zoneId is null || !await db.Employees.AnyAsync(e => e.Id == request.EmployeeId, ct))
        {
            return null;
        }

        var zone = LocalTime.Zone(zoneId);
        var (from, until) = LocalTime.Day(request.Date, zone);
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.LocationId == locationId && a.EmployeeId == request.EmployeeId && Open.Contains(a.Status)
                && a.Start >= from && a.Start < until)
            .OrderBy(a => a.Start)
            .ToListAsync(ct);
        var names = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var here = await db.Locations.AsNoTracking().Where(l => l.Id == locationId).Select(l => l.Name).FirstAsync(ct);

        // Andere Standorte nur, wenn die Person sie sieht und dort buchen darf; die Reihenfolge ist alphabetisch.
        var others = access is null || !access.Has(AccessRight.Book)
            ? []
            : (await db.Locations.AsNoTracking().Where(l => l.Id != locationId).OrderBy(l => l.Name)
                .Select(l => new { l.Id, l.Name, l.TimeZone }).ToListAsync(ct))
                .Where(l => access.CanSee(l.Id))
                .ToList();

        // Was schon vorgeschlagen ist, belegt Person und Raum für die folgenden Termine.
        var reserved = new List<ResourceBooking>();
        var items = new List<AbsenceProposalItem>();
        foreach (var appointment in appointments)
        {
            var dto = BookingService.ToDto(appointment, appointment.Treatment.Name);
            var loaded = await booking.LoadContextAsync(locationId, appointment.TreatmentId, request.Date, appointment.Id, ct);
            if (loaded is null)
            {
                items.Add(new(dto, AbsenceOption.None, null, null, null, null, null, "Die Behandlung wird nicht mehr angeboten. Bitte den Gast kontaktieren."));
                continue;
            }

            var context = loaded with
            {
                Employees = [.. loaded.Employees.Where(e => e.Id != request.EmployeeId)],
                Bookings = [.. loaded.Bookings, .. reserved],
            };
            var planner = new ResourcePlanner(BookingService.WithTimesOf(appointment, context));
            var load = context.Employees.ToDictionary(
                e => e.Id,
                e => context.Bookings.Where(b => b.ResourceId == e.Id && b.From < until && from < b.Until).Sum(b => (b.Until - b.From).TotalMinutes));
            var candidates = context.Employees.OrderBy(e => load[e.Id]).ThenBy(e => e.DisplayName).ToList();

            var plan = candidates.Select(e => planner.Plan(appointment.Start, e.Id, appointment.RoomId).Plan).FirstOrDefault(p => p is not null)
                ?? candidates.Select(e => planner.Plan(appointment.Start, e.Id).Plan).FirstOrDefault(p => p is not null);
            AbsenceOption option;
            string explanation;
            if (plan is not null)
            {
                option = AbsenceOption.SameTime;
                var room = plan.Room.Id == appointment.RoomId ? $"Raum {plan.Room.Name} bleibt" : $"Raum wechselt zu {plan.Room.Name}";
                explanation = $"{plan.Employee.DisplayName} hat die Qualifikation und ist um {Time(appointment.Start, zone)} frei; {room}.";
            }
            else
            {
                plan = planner.FreeSlots(request.Date, Step, null)
                    .OrderBy(s => Math.Abs((s.Start - appointment.Start).TotalMinutes))
                    .ThenBy(s => s.Fit == SlotFit.LeavesGap)
                    .ThenBy(s => load.GetValueOrDefault(s.EmployeeId))
                    .Select(s => planner.Plan(s.Start, s.EmployeeId, s.RoomId).Plan)
                    .FirstOrDefault(p => p is not null);
                option = plan is null ? AbsenceOption.None : AbsenceOption.OtherTime;
                explanation = plan is null
                    ? "An diesem Tag ist niemand mit der Qualifikation frei. Bitte den Gast kontaktieren und einen anderen Tag anbieten."
                    : $"Um {Time(appointment.Start, zone)} ist niemand frei. Nächste freie Zeit: {Time(plan.Times.Start, zone)} bei {plan.Employee.DisplayName} in {plan.Room.Name}.";
            }

            (Guid Id, string Name)? elsewhere = null;
            if (plan is null && appointment.GroupId is null && appointment.Status == AppointmentStatus.Booked)
            {
                foreach (var other in others)
                {
                    var otherZone = LocalTime.Zone(other.TimeZone);
                    var otherDate = LocalTime.LocalDate(appointment.Start, otherZone);
                    var otherLoaded = await booking.LoadContextAsync(other.Id, appointment.TreatmentId, otherDate, appointment.Id, ct);
                    if (otherLoaded is null || otherLoaded.Rooms.Count == 0)
                    {
                        continue;
                    }

                    var otherContext = otherLoaded with
                    {
                        Employees = [.. otherLoaded.Employees.Where(e => e.Id != request.EmployeeId)],
                        Bookings = [.. otherLoaded.Bookings, .. reserved],
                    };
                    var otherPlanner = new ResourcePlanner(BookingService.WithTimesOf(appointment, otherContext));
                    var sameTime = otherContext.Employees.OrderBy(e => e.DisplayName)
                        .Select(e => otherPlanner.Plan(appointment.Start, e.Id).Plan).FirstOrDefault(p => p is not null);
                    var found = sameTime ?? otherPlanner.FreeSlots(otherDate, Step, null)
                        .OrderBy(s => Math.Abs((s.Start - appointment.Start).TotalMinutes))
                        .ThenBy(s => s.Fit == SlotFit.LeavesGap)
                        .Select(s => otherPlanner.Plan(s.Start, s.EmployeeId, s.RoomId).Plan)
                        .FirstOrDefault(p => p is not null);
                    if (found is null)
                    {
                        continue;
                    }

                    plan = found;
                    elsewhere = (other.Id, other.Name);
                    option = AbsenceOption.OtherLocation;
                    var when = sameTime is not null ? $"zur gleichen Zeit um {Time(found.Times.Start, otherZone)}" : $"um {Time(found.Times.Start, otherZone)}";
                    explanation = $"In {here} ist an diesem Tag niemand frei. In {other.Name} geht es {when} bei {found.Employee.DisplayName} in {found.Room.Name}; nur nach Rücksprache mit dem Gast übernehmen.";
                    break;
                }
            }

            if (plan is not null)
            {
                reserved.AddRange(plan.Bookings().Select(b => new ResourceBooking { Kind = b.Kind, ResourceId = b.Id, From = b.From, Until = b.Until }));
            }

            items.Add(new(dto, option, plan?.Times.Start, plan?.Employee.Id, plan is null ? null : names.GetValueOrDefault(plan.Employee.Id),
                plan?.Room.Id, plan is null ? null : rooms.GetValueOrDefault(plan.Room.Id), explanation, elsewhere?.Id, elsewhere?.Name));
        }

        return new AbsenceProposal(request.EmployeeId, request.Date, items);
    }

    /// <summary>
    /// Umbuchungshilfe für eine Raumsperre (ADR 0076): für jeden offenen Termin im gesperrten Raum zuerst ein anderer Raum zur gleichen Zeit bei derselben Person, sonst die nächste
    /// freie Zeit am selben Tag (bei derselben Person, dann bei jemand anderem). Der Planer kennt die Sperre und wählt den Raum nie; übernommen wird über denselben Weg
    /// wie beim Ausfall einer Person (<see cref="ApplyAsync"/>). <c>EmployeeId</c> der Antwort ist leer, weil nicht eine Person ausfällt.
    /// </summary>
    public async Task<AbsenceProposal?> ProposeForRoomClosureAsync(Guid locationId, Guid closureId, CancellationToken ct)
    {
        var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        var rooms = db.Rooms.Where(r => r.LocationId == locationId).Select(r => r.Id);
        var closure = await db.RoomClosures.AsNoTracking().FirstOrDefaultAsync(c => c.Id == closureId && rooms.Contains(c.RoomId), ct);
        if (zoneId is null || closure is null)
        {
            return null;
        }

        var zone = LocalTime.Zone(zoneId);
        var appointments = await RoomClosureService.OpenAppointmentsAsync(db, closure, zone, time.GetUtcNow(), ct);
        var names = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        var roomNames = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId).ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        // Was schon vorgeschlagen ist, belegt Person und Raum für die folgenden Termine.
        var reserved = new List<ResourceBooking>();
        var items = new List<AbsenceProposalItem>();
        foreach (var appointment in appointments)
        {
            var dto = BookingService.ToDto(appointment, appointment.Treatment.Name);
            var date = LocalTime.LocalDate(appointment.Start, zone);
            var loaded = await booking.LoadContextAsync(locationId, appointment.TreatmentId, date, appointment.Id, ct);
            if (loaded is null)
            {
                items.Add(new(dto, AbsenceOption.None, null, null, null, null, null, "Die Behandlung wird nicht mehr angeboten. Bitte den Gast kontaktieren."));
                continue;
            }

            var context = loaded with { Bookings = [.. loaded.Bookings, .. reserved] };
            var planner = new ResourcePlanner(BookingService.WithTimesOf(appointment, context));
            var plan = planner.Plan(appointment.Start, appointment.EmployeeId).Plan;
            AbsenceOption option;
            string explanation;
            if (plan is not null)
            {
                option = AbsenceOption.SameTime;
                explanation = $"{plan.Room.Name} ist um {Time(appointment.Start, zone)} frei; {plan.Employee.DisplayName} bleibt.";
            }
            else
            {
                var nearest = (IEnumerable<AvailableSlot> slots) => slots.OrderBy(s => Math.Abs((s.Start - appointment.Start).TotalMinutes)).ThenBy(s => s.Fit == SlotFit.LeavesGap)
                    .Select(s => planner.Plan(s.Start, s.EmployeeId, s.RoomId).Plan).FirstOrDefault(p => p is not null);
                plan = nearest(planner.FreeSlots(date, Step, appointment.EmployeeId)) ?? nearest(planner.FreeSlots(date, Step, null));
                option = plan is null ? AbsenceOption.None : AbsenceOption.OtherTime;
                explanation = plan is null
                    ? "An diesem Tag ist kein anderer Raum frei. Bitte den Gast kontaktieren und einen anderen Tag anbieten."
                    : $"Um {Time(appointment.Start, zone)} ist kein anderer Raum frei. Nächste freie Zeit: {Time(plan.Times.Start, zone)} bei {plan.Employee.DisplayName} in {plan.Room.Name}.";
            }

            if (plan is not null)
            {
                reserved.AddRange(plan.Bookings().Select(b => new ResourceBooking { Kind = b.Kind, ResourceId = b.Id, From = b.From, Until = b.Until }));
            }

            items.Add(new(dto, option, plan?.Times.Start, plan?.Employee.Id, plan is null ? null : names.GetValueOrDefault(plan.Employee.Id),
                plan?.Room.Id, plan is null ? null : roomNames.GetValueOrDefault(plan.Room.Id), explanation));
        }

        return new AbsenceProposal(Guid.Empty, LocalTime.LocalDate(closure.StartsAt(zone), zone), items);
    }

    public async Task<AbsenceApplyResult> ApplyAsync(Guid locationId, AbsenceApplyRequest request, UserAccess? access, CancellationToken ct)
    {
        var locations = await db.Locations.AsNoTracking()
            .ToDictionaryAsync(l => l.Id, l => new { l.Name, ZoneId = l.TimeZone, Zone = LocalTime.Zone(l.TimeZone) }, ct);
        var moved = new List<AppointmentDto>();
        var failed = new List<AbsenceMoveFailure>();
        var notified = 0;
        foreach (var move in request.Moves)
        {
            var before = await db.Appointments.AsNoTracking().Include(a => a.Customer)
                .Where(a => a.Id == move.AppointmentId && a.LocationId == locationId)
                .Select(a => new { a.Start, a.EmployeeId, a.Customer, a.Customer.Email })
                .FirstOrDefaultAsync(ct);
            if (before is null)
            {
                failed.Add(new(move.AppointmentId, "Termin nicht gefunden."));
                continue;
            }

            var target = move.LocationId ?? locationId;
            if (target != locationId && (access is null || !access.CanSee(target) || !locations.ContainsKey(target)))
            {
                failed.Add(new(move.AppointmentId, "Für diesen Standort fehlt der Zugang."));
                continue;
            }

            var outcome = await booking.MoveAsync(move.AppointmentId, new MoveAppointmentRequest(move.Start, move.EmployeeId, move.RoomId, null), ct,
                target == locationId ? null : target);
            switch (outcome)
            {
                case BookingOutcome.Success success:
                    moved.Add(success.Appointment);
                    if (request.NotifyCustomers && before.Email is { Length: > 0 } email
                        && (success.Appointment.Start != before.Start || success.Appointment.EmployeeId != before.EmployeeId || target != locationId))
                    {
                        var employee = await db.Employees.Where(e => e.Id == success.Appointment.EmployeeId).Select(e => e.DisplayName).FirstAsync(ct);
                        var place = locations[target];
                        await bus.PublishGuestMailAsync(tenants, ChangedMail(email, GuestName.Of(before.Customer, place.ZoneId), success.Appointment, before.Start, employee,
                            place.Name, place.Zone, target == locationId ? null : locations[locationId].Name));
                        notified++;
                    }

                    break;
                case BookingOutcome.Rejected rejected:
                    failed.Add(new(move.AppointmentId, rejected.Reason.Message));
                    break;
                case BookingOutcome.Invalid invalid:
                    failed.Add(new(move.AppointmentId, invalid.Message));
                    break;
                default:
                    failed.Add(new(move.AppointmentId, "Termin nicht gefunden."));
                    break;
            }
        }

        return new AbsenceApplyResult(moved, failed, notified);
    }

    /// <param name="previousLocation">Bisheriger Standort, wenn der Termin dorthin verlegt wurde (wie mit dem Gast besprochen).</param>
    internal static OutgoingMail ChangedMail(
        string to, GuestName guest, AppointmentDto appointment, DateTimeOffset previousStart, string employee, string location, TimeZoneInfo zone,
        string? previousLocation = null)
    {
        var when = Date(appointment.Start, zone);
        var change = previousLocation is not null
            ? $"Wie besprochen findet Ihr Termin nicht in {previousLocation}, sondern in {location} statt: am {when}, bei {employee}. Der Preis bleibt wie gebucht."
            : appointment.Start == previousStart
            ? $"Ihr Termin am {when} bleibt zur gleichen Zeit. Leider ist Ihre Therapeutin bzw. Ihr Therapeut ausgefallen; Sie werden von {employee} behandelt."
            : $"Leider müssen wir Ihren Termin verschieben: von {Date(previousStart, zone)} auf {when}, bei {employee}.";
        var text = $"""
            Guten Tag {guest.GreetingName},

            {change}

            Behandlung: {appointment.TreatmentName}
            Ort: {location}

            Passt Ihnen das nicht, antworten Sie einfach auf diese Mail oder rufen Sie uns an. Wir finden gemeinsam eine andere Zeit.

            Herzliche Grüße
            Ihr Team in {location}
            """;
        return new OutgoingMail(to, $"Änderung Ihres Termins am {Date(appointment.Start, zone)}", text);
    }

    private static string Time(DateTimeOffset utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(utc, zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Date(DateTimeOffset utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(utc, zone).ToString("dd.MM.yyyy 'um' HH:mm 'Uhr'", CultureInfo.InvariantCulture);
}

internal static class AbsenceEndpoints
{
    public static IEndpointRouteBuilder MapAbsenceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/locations/{locationId:guid}/absence").WithTags("Kalender").RequirePermission(AccessRight.Book);

        group.MapPost("/proposal", async Task<Results<Ok<AbsenceProposal>, NotFound>> (
                Guid locationId, AbsenceProposalRequest request, ClaimsPrincipal principal, AccessService access, AbsenceAssistant assistant, CancellationToken ct) =>
                await assistant.ProposeAsync(locationId, request, await access.GetAsync(principal, ct), ct) is { } proposal
                    ? TypedResults.Ok(proposal)
                    : TypedResults.NotFound())
            .WithName("ProposeAbsenceCover")
            .WithSummary("Ausfall-Assistent: Ersatz für alle offenen Termine einer Person an einem Tag vorschlagen, notfalls an einem anderen Standort (ändert nichts).");

        group.MapPost("/apply", async Task<Results<Ok<AbsenceApplyResult>, ValidationProblem>> (
                Guid locationId, AbsenceApplyRequest request, ClaimsPrincipal principal, AccessService access, AbsenceAssistant assistant, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Check(request.Moves is { Count: > 0 and <= 50 }, nameof(request.Moves), "Zwischen 1 und 50 Termine übernehmen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                return TypedResults.Ok(await assistant.ApplyAsync(locationId, request, await access.GetAsync(principal, ct), ct));
            })
            .WithName("ApplyAbsenceCover")
            .WithSummary("Übernommene Vorschläge verschieben; auf Wunsch erfahren die Gäste per Mail von der Änderung.");

        return routes;
    }
}
