using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Roster;

/// <summary>Antrag einer Person auf Abwesenheit (Handy-App, Slice 106). Erst die Genehmigung schreibt in den Dienstplan.</summary>
public sealed class AbsenceRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; init; }
    public RosterKind Kind { get; init; }
    public DateOnly From { get; init; }
    public DateOnly Until { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public AbsenceRequestState State { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecidedByUserId { get; set; }
}

/// <summary>Stellen und Lesen für die Person selbst, Entscheiden für die Standortleitung (Windows-App, Slice 99).</summary>
internal static class AbsenceRequestEndpoints
{
    private const int MaxDays = 60;
    private const int MaxNoteLength = 500;
    private static readonly AppointmentStatus[] Open = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn];

    public static IEndpointRouteBuilder MapAbsenceRequestEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/absence-requests").WithTags("Dienstplan");

        group.MapPost("", async Task<Results<Ok<MyAbsenceRequestDto>, ValidationProblem>> (
                AbsenceRequestInput input, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var errors = await ValidateAsync(input, access.EmployeeId, db, time, ct);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var request = new AbsenceRequest
                {
                    EmployeeId = access.EmployeeId!.Value,
                    Kind = input.Kind,
                    From = input.From,
                    Until = input.Until,
                    Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
                    RequestedAt = time.GetUtcNow(),
                    State = AbsenceRequestState.Open,
                };
                db.AbsenceRequests.Add(request);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToMine(request));
            })
            .WithName("RequestAbsence")
            .WithSummary("Die eigene Abwesenheit beantragen; die Standortleitung entscheidet in der Windows-App.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapGet("/mine", async (AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var requests = await db.AbsenceRequests.AsNoTracking().Where(r => r.EmployeeId == access.EmployeeId)
                    .OrderByDescending(r => r.RequestedAt).ThenBy(r => r.Id).Take(50).ToListAsync(ct);
                return requests.Select(ToMine).ToList();
            })
            .WithName("ListMyAbsenceRequests")
            .WithSummary("Die eigenen Anträge mit Entscheidung, neueste zuerst.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapGet("", async (AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var open = await db.AbsenceRequests.AsNoTracking().Where(r => r.State == AbsenceRequestState.Open)
                    .OrderBy(r => r.RequestedAt).ThenBy(r => r.Id).ToListAsync(ct);
                var employeeIds = open.Select(r => r.EmployeeId).Distinct().ToList();
                var employees = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
                var rows = new List<AbsenceRequestDto>();
                foreach (var request in open.Where(r => employees.TryGetValue(r.EmployeeId, out var employee) && MayDecide(access, employee)))
                {
                    rows.Add(new(request.Id, request.EmployeeId, employees[request.EmployeeId].DisplayName, request.Kind, request.From, request.Until, request.Note,
                        request.RequestedAt, await AffectedAppointmentsAsync(db, request, ct)));
                }

                return rows;
            })
            .WithName("ListAbsenceRequests")
            .WithSummary("Offene Anträge der Personen an den eigenen Standorten; der älteste zuerst.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/{id:guid}/approve", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                Guid id, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
                await DecideAsync(id, AbsenceRequestState.Approved, db, accessService, time, ct))
            .WithName("ApproveAbsenceRequest")
            .WithSummary("Genehmigt den Antrag und trägt die Abwesenheit sofort in den Dienstplan ein. Termine im Zeitraum bleiben stehen und erscheinen als außerhalb des Dienstes.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/{id:guid}/reject", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                Guid id, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
                await DecideAsync(id, AbsenceRequestState.Rejected, db, accessService, time, ct))
            .WithName("RejectAbsenceRequest")
            .WithSummary("Lehnt den Antrag ab; der Dienstplan bleibt unverändert.")
            .RequirePermission(AccessRight.ManageLocation);

        return routes;
    }

    private static async Task<Errors> ValidateAsync(AbsenceRequestInput input, Guid? employeeId, AispaDbContext db, TimeProvider time, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(employeeId is not null, nameof(input.Kind), "Dieses Konto ist mit keiner Person im Team verknüpft.");
        errors.Check(input.Kind != RosterKind.Work && Enum.IsDefined(input.Kind), nameof(input.Kind), "Bitte eine Art der Abwesenheit wählen.");
        errors.Check(input.From <= input.Until, nameof(input.Until), "Das Ende darf nicht vor dem Beginn liegen.");
        errors.Check(input.Until.DayNumber - input.From.DayNumber < MaxDays, nameof(input.Until), $"Höchstens {MaxDays} Tage in einem Antrag.");
        errors.Check((input.Note ?? "").Length <= MaxNoteLength, nameof(input.Note), $"Höchstens {MaxNoteLength} Zeichen.");
        // Einen Tag Spielraum: Die Person hat keinen festen Standort, und die Zeitzonen der Standorte liegen nah beieinander.
        errors.Check(input.From >= DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(-1), nameof(input.From), "Der Beginn darf nicht in der Vergangenheit liegen.");
        if (!errors.Any)
        {
            var overlaps = await db.AbsenceRequests.AnyAsync(r => r.EmployeeId == employeeId && r.State != AbsenceRequestState.Rejected
                && r.From <= input.Until && input.From <= r.Until, ct);
            errors.Check(!overlaps, nameof(input.From), "Für diesen Zeitraum gibt es schon einen Antrag.");
        }

        return errors;
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> DecideAsync(
        Guid id, AbsenceRequestState decision, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct)
    {
        var access = (await accessService.CurrentAsync(ct))!;
        var request = await db.AbsenceRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        var employee = request is null ? null : await db.Employees.Include(e => e.WorkingHours).FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (request is null || employee is null || !MayDecide(access, employee))
        {
            return TypedResults.NotFound();
        }

        // Der bedingte Schreibbefehl sperrt die Zeile: Von zwei gleichzeitigen Entscheidungen gewinnt genau eine, und der Dienstplan folgt nur ihr.
        var now = time.GetUtcNow();
        var decided = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var rows = await db.AbsenceRequests.Where(r => r.Id == id && r.State == AbsenceRequestState.Open)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, decision).SetProperty(r => r.DecidedAt, now).SetProperty(r => r.DecidedByUserId, access.UserId), ct);
            if (rows == 0)
            {
                return false;
            }

            if (decision == AbsenceRequestState.Approved)
            {
                await WriteRosterAsync(db, request, employee, access, now, ct);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });
        if (!decided)
        {
            var errors = new Errors();
            errors.Check(false, "state", "Über diesen Antrag ist schon entschieden.");
            return errors.Problem();
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Wie „sofort gültig“ im Dienstplan: ersetzt auch Entwürfe; Dienste an Standorten ohne Zugang bleiben stehen.
    /// Die Notiz des Antrags kommt nicht in den Plan: Den Plan sehen alle am Standort, der Grund geht nur die Leitung an.
    /// </summary>
    private static async Task WriteRosterAsync(AispaDbContext db, AbsenceRequest request, Employee employee, UserAccess access, DateTimeOffset now, CancellationToken ct)
    {
        for (var date = request.From; date <= request.Until; date = date.AddDays(1))
        {
            var existing = await db.RosterDays.Include(d => d.Entries).FirstOrDefaultAsync(d => d.EmployeeId == employee.Id && d.Date == date, ct);
            var hidden = DayPlan.For(employee, existing, date).Shifts.Where(s => !access.CanSee(s.LocationId!.Value));
            RosterEndpoints.Publish(db, existing, employee.Id, date, [new RosterEntry(request.Kind, null, null, null), .. hidden], existing?.Note, access.UserId, now);
            if (await db.RosterDrafts.Include(d => d.Entries).FirstOrDefaultAsync(d => d.EmployeeId == employee.Id && d.Date == date, ct) is { } draft)
            {
                db.RosterDrafts.Remove(draft);
            }
        }
    }

    private static async Task<int> AffectedAppointmentsAsync(AispaDbContext db, AbsenceRequest request, CancellationToken ct)
    {
        // Grob eingrenzen (±1 Tag in UTC), genau prüfen in der Zeitzone des jeweiligen Standorts.
        var from = new DateTimeOffset(request.From.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var until = new DateTimeOffset(request.Until.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var candidates = await db.Appointments.AsNoTracking()
            .Where(a => a.EmployeeId == request.EmployeeId && Open.Contains(a.Status) && a.Start >= from && a.Start < until)
            .Join(db.Locations, a => a.LocationId, l => l.Id, (a, l) => new { a.Start, l.TimeZone })
            .ToListAsync(ct);
        return candidates.Count(c => LocalTime.LocalDate(c.Start, LocalTime.Zone(c.TimeZone)) is var day && request.From <= day && day <= request.Until);
    }

    private static bool MayDecide(UserAccess access, Employee employee) =>
        access.AllLocations || employee.WorkingHours.Any(w => access.CanSee(w.LocationId));

    private static MyAbsenceRequestDto ToMine(AbsenceRequest r) => new(r.Id, r.Kind, r.From, r.Until, r.Note, r.RequestedAt, r.State, r.DecidedAt);
}
