using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.TimeTracking;

/// <summary>
/// Zeiterfassung: die Standortleitung sieht Soll und Ist, trägt Arbeitszeiten nach und korrigiert mit Begründung.
/// Jede Korrektur steht im Änderungsprotokoll. Stempeln (Kommen, Pause, Gehen) geht nur noch am Arbeitsplatz-Kanal und hat keine
/// Oberfläche; die Mitarbeiter-App erreicht es nicht (T-015, Auftraggeber 05.11.2026: keine Zeiterfassung per Handy).
/// </summary>
internal static class TimeTrackingEndpoints
{
    /// <summary>Längster Abschnitt, der sich nachtragen lässt; länger ist fast sicher ein vergessenes Gehen.</summary>
    private static readonly TimeSpan MaxRecord = TimeSpan.FromHours(16);

    /// <summary>Ab dieser Dauer gilt ein laufender Abschnitt als vergessen.</summary>
    private static readonly TimeSpan ForgottenAfter = TimeSpan.FromHours(12);

    /// <summary>Kleine Uhrabweichungen zwischen Gerät und Server.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapTimeTrackingEndpoints(this IEndpointRouteBuilder routes)
    {
        var me = routes.MapGroup("/api/time/me").WithTags("Zeiterfassung").RequirePermission(AccessRight.ViewCalendar);

        me.MapGet("", async (AccessService accessService, AispaDbContext db, TimeTrackingService times, TimeProvider time, CancellationToken ct) =>
                await StatusAsync((await accessService.CurrentAsync(ct))!, db, times, time, ct))
            .WithName("GetMyTime")
            .WithSummary("Eigener Stand der Zeiterfassung heute: läuft gerade ein Abschnitt, Stunden und Pausen.");

        me.MapPost("/clock-in", async Task<Results<Ok<MyTimeStatus>, NotFound, ProblemHttpResult>> (
                ClockInput input, AccessService accessService, AispaDbContext db, TimeTrackingService times, TimeProvider time,
                CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                if (access.EmployeeId is not { } employeeId)
                {
                    return Conflict("Dieses Konto ist mit keiner Person im Team verknüpft. Bitte die Standortleitung fragen.");
                }

                if (!access.CanSee(input.LocationId) || !await db.Locations.AnyAsync(l => l.Id == input.LocationId, ct))
                {
                    return TypedResults.NotFound();
                }

                if (await db.TimeRecords.AnyAsync(r => r.EmployeeId == employeeId && r.End == null && !r.IsDeleted, ct))
                {
                    return Conflict("Du bist schon eingestempelt.");
                }

                var now = time.GetUtcNow();
                db.TimeRecords.Add(new TimeRecord
                {
                    EmployeeId = employeeId,
                    LocationId = input.LocationId,
                    Start = now,
                    Source = TimeRecordSource.Clock,
                    UpdatedAt = now,
                });
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Zweimal gleichzeitig gedrückt: der Index lässt nur einen laufenden Abschnitt zu.
                    return Conflict("Du bist schon eingestempelt.");
                }

                return TypedResults.Ok(await StatusAsync(access, db, times, time, ct));
            })
            .WithName("ClockIn")
            .WithSummary("Kommen oder nach der Pause weiter: beginnt einen Abschnitt am Standort.");

        me.MapPost("/clock-out", async Task<Results<Ok<MyTimeStatus>, ProblemHttpResult>> (
                AccessService accessService, AispaDbContext db, TimeTrackingService times, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var running = access.EmployeeId is { } employeeId
                    ? await db.TimeRecords.FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.End == null && !r.IsDeleted, ct)
                    : null;
                if (running is null)
                {
                    return Conflict("Du bist nicht eingestempelt.");
                }

                var now = time.GetUtcNow();
                // Sekunden nach dem Kommen wieder gedrückt: ein Abschnitt braucht ein Ende nach dem Beginn.
                running.End = now > running.Start ? now : running.Start.AddSeconds(1);
                running.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await StatusAsync(access, db, times, time, ct));
            })
            .WithName("ClockOut")
            .WithSummary("Pause oder Gehen: beendet den laufenden Abschnitt. Die Lücke bis zum nächsten Kommen zählt als Pause.");

        var location = routes.MapGroup("/api/locations/{locationId:guid}").WithTags("Zeiterfassung").RequirePermission(AccessRight.ManageLocation);

        location.MapGet("/timesheet", async Task<Results<Ok<Timesheet>, NotFound>> (
                Guid locationId, DateOnly from, int? days, Guid? employeeId, AispaDbContext db, RosterService roster, TimeTrackingService times,
                AccessService accessService, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var access = (await accessService.CurrentAsync(ct))!;

                var count = Math.Clamp(days ?? 7, 1, 31);
                var stamped = db.TimeRecords.Where(r => r.LocationId == locationId && !r.IsDeleted).Select(r => r.EmployeeId);
                var employees = await db.Employees.AsNoTracking().Include(e => e.WorkingHours)
                    .Where(e => employeeId == null ? roster.EmployeesAt(locationId, from).Any(x => x.Id == e.Id) || stamped.Contains(e.Id) : e.IsActive && e.Id == employeeId)
                    .OrderBy(e => e.DisplayName)
                    .ToListAsync(ct);
                var (rules, dates, rows) = await RowsAsync(employees, from, count, access, roster, times, ct);

                return TypedResults.Ok(new Timesheet(locationId, rules.Country, from, dates, rows));
            })
            .WithName("GetTimesheet")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageLocation)
            .WithSummary("Soll (Dienstplan) und Ist (Zeiterfassung) je Person und Tag mit Hinweisen nach dem Arbeitszeitrecht des Landes; mit employeeId nur für diese Person.");

        location.MapPost("/time-records", async Task<Results<Ok<TimeRecordDto>, ValidationProblem, NotFound>> (
                Guid locationId, TimeRecordInput input, AccessService accessService, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Employees.AnyAsync(e => e.Id == input.EmployeeId, ct))
                {
                    return TypedResults.NotFound();
                }

                if (await ValidateAsync(input, null, db, time, ct) is { } problem)
                {
                    return problem;
                }

                var now = time.GetUtcNow();
                var record = new TimeRecord
                {
                    EmployeeId = input.EmployeeId,
                    LocationId = locationId,
                    Start = input.Start,
                    End = input.End,
                    Source = TimeRecordSource.Manual,
                    UpdatedAt = now,
                };
                record.Changes.Add(await ChangeAsync(record, TimeRecordAction.Created, null, null, input.Reason, accessService, db, now, ct));
                db.TimeRecords.Add(record);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(TimeTrackingService.ToDto(record));
            })
            .WithName("AddTimeRecord")
            .WithSummary("Zeit nachtragen (z. B. Stempeln vergessen). Der Grund ist Pflicht.");

        location.MapPut("/time-records/{recordId:guid}", async Task<Results<Ok<TimeRecordDto>, ValidationProblem, NotFound>> (
                Guid locationId, Guid recordId, TimeRecordInput input, AccessService accessService, AispaDbContext db, TimeProvider time,
                CancellationToken ct) =>
            {
                var record = await db.TimeRecords.Include(r => r.Changes)
                    .FirstOrDefaultAsync(r => r.Id == recordId && r.LocationId == locationId && !r.IsDeleted, ct);
                if (record is null || record.EmployeeId != input.EmployeeId)
                {
                    return TypedResults.NotFound();
                }

                if (await ValidateAsync(input, record.Id, db, time, ct) is { } problem)
                {
                    return problem;
                }

                var now = time.GetUtcNow();
                var change = await ChangeAsync(record, TimeRecordAction.Changed, input.Start, input.End, input.Reason, accessService, db, now, ct);
                record.Start = input.Start;
                record.End = input.End;
                record.Source = TimeRecordSource.Manual;
                record.UpdatedAt = now;
                record.Changes.Add(change);
                db.Add(change);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(TimeTrackingService.ToDto(record));
            })
            .WithName("ChangeTimeRecord")
            .WithSummary("Abschnitt korrigieren. Der alte Stand bleibt im Änderungsprotokoll.");

        location.MapDelete("/time-records/{recordId:guid}", async Task<Results<NoContent, ValidationProblem, NotFound>> (
                Guid locationId, Guid recordId, [FromBody] TimeRecordDeletion input, AccessService accessService, AispaDbContext db,
                TimeProvider time, CancellationToken ct) =>
            {
                var record = await db.TimeRecords.FirstOrDefaultAsync(r => r.Id == recordId && r.LocationId == locationId && !r.IsDeleted, ct);
                if (record is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                CheckReason(errors, input.Reason);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var now = time.GetUtcNow();
                db.Add(await ChangeAsync(record, TimeRecordAction.Deleted, null, null, input.Reason, accessService, db, now, ct));
                record.IsDeleted = true;
                record.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteTimeRecord")
            .WithSummary("Abschnitt entfernen (z. B. doppelt gestempelt). Er bleibt mit Grund im Protokoll erhalten.");

        location.MapGet("/time-records/{recordId:guid}/changes", async Task<Results<Ok<List<TimeRecordChangeDto>>, NotFound>> (
                Guid locationId, Guid recordId, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.TimeRecords.AnyAsync(r => r.Id == recordId && r.LocationId == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(await db.TimeRecordChanges.AsNoTracking().Where(c => c.TimeRecordId == recordId).OrderBy(c => c.ChangedAt)
                    .Select(c => new TimeRecordChangeDto(c.ChangedAt, c.ChangedByName, c.Action, c.OldStart, c.OldEnd, c.NewStart, c.NewEnd, c.Reason))
                    .ToListAsync(ct));
            })
            .WithName("GetTimeRecordChanges")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageLocation)
            .WithSummary("Änderungsprotokoll eines Abschnitts.");

        return routes;
    }

    /// <summary>
    /// Soll und Ist der Personen für <paramref name="count"/> Tage ab <paramref name="from"/>, wie sie die Zeiten-Maske und die Stundenliste (PDF) zeigen: nur die Abschnitte an Standorten,
    /// die die angemeldete Person sehen darf, mit den Hinweisen zum Arbeitszeitrecht des Landes.
    /// </summary>
    internal static async Task<(WorkingTimeRules Rules, List<DateOnly> Dates, List<TimesheetRow> Rows)> RowsAsync(
        IReadOnlyList<Employee> employees, DateOnly from, int count, UserAccess access, RosterService roster, TimeTrackingService times, CancellationToken ct)
    {
        var until = from.AddDays(count - 1);
        var ids = employees.Select(e => e.Id).ToList();
        var plans = await roster.PlansAsync(employees, from, until, ct);
        var actual = await times.DaysAsync(ids, from.AddDays(-1), until, ct);
        var rules = await roster.RulesAsync(ct);
        var dates = Enumerable.Range(0, count).Select(from.AddDays).ToList();

        var rows = employees.Select(e =>
            {
                var cells = dates.Select(d =>
                    {
                        // Wie beim Dienstplan: nur sichtbare Standorte; die Hinweise zum Arbeitszeitrecht rechnen über alle Abschnitte der Person.
                        var whole = actual.GetValueOrDefault((e.Id, d)) ?? [];
                        var spans = whole.Where(s => access.CanSee(s.Record.LocationId)).ToList();
                        var previous = actual.GetValueOrDefault((e.Id, d.AddDays(-1)));
                        var warnings = spans.Count == 0 ? [] : WorkingTime.DayWarnings([.. whole.Select(s => (s.Start, s.End))], previous?.Max(s => s.End), rules, checkBreaks: true);
                        if (spans.Any(s => s.Running && s.End - s.Start > ForgottenAfter))
                        {
                            warnings.Insert(0, "Seit über 12 Stunden eingestempelt: Gehen vergessen?");
                        }

                        return new TimesheetDay(d, Math.Round(plans[(e.Id, d)].VisibleTo(access.CanSee, absences: false).Hours, 2), Math.Round(TimeTrackingService.Hours(spans), 2),
                            TimeTrackingService.BreakMinutes(spans), [.. spans.Select(s => TimeTrackingService.ToDto(s.Record))], warnings);
                    })
                    .ToList();
                var worked = dates.Select(d => actual.GetValueOrDefault((e.Id, d)) ?? []).ToList();
                var weekHours = worked.Sum(TimeTrackingService.Hours);
                var weekWarnings = WorkingTime.WeekWarnings(count >= 7 ? weekHours / count * 7 : 0, [.. worked.Select(w => w.Count > 0)], rules);
                return new TimesheetRow(e.Id, e.DisplayName, e.Color, cells, Math.Round(cells.Sum(c => c.PlannedHours), 2),
                    Math.Round(cells.Sum(c => c.ActualHours), 2), weekWarnings);
            })
            .ToList();
        return (rules, dates, rows);
    }

    private static async Task<MyTimeStatus> StatusAsync(UserAccess access, AispaDbContext db, TimeTrackingService times, TimeProvider time, CancellationToken ct)
    {
        if (access.EmployeeId is not { } employeeId)
        {
            return new(null, null, [], 0, 0);
        }

        var running = await db.TimeRecords.AsNoTracking().Include(r => r.Changes)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.End == null && !r.IsDeleted, ct);
        // „Heute“ in der Zeitzone des laufenden Abschnitts, sonst des ersten eigenen Standorts.
        var locations = running is null ? db.Locations.OrderBy(l => l.Name) : db.Locations.Where(l => l.Id == running.LocationId);
        var zoneId = await locations.Select(l => l.TimeZone).FirstOrDefaultAsync(ct) ?? "Europe/Vienna";
        var today = LocalTime.LocalDate(time.GetUtcNow(), LocalTime.Zone(zoneId));
        var spans = (await times.DaysAsync([employeeId], today, today, ct)).GetValueOrDefault((employeeId, today)) ?? [];
        return new(employeeId, running is null ? null : TimeTrackingService.ToDto(running), [.. spans.Select(s => TimeTrackingService.ToDto(s.Record))],
            Math.Round(TimeTrackingService.Hours(spans), 2), TimeTrackingService.BreakMinutes(spans));
    }

    private static async Task<ValidationProblem?> ValidateAsync(TimeRecordInput input, Guid? recordId, AispaDbContext db, TimeProvider time, CancellationToken ct)
    {
        var errors = new Errors();
        var now = time.GetUtcNow();
        CheckReason(errors, input.Reason);
        errors.Check(input.Start <= now + ClockSkew, nameof(input.Start), "Beginn liegt in der Zukunft.");
        if (input.End is { } end)
        {
            errors.Check(input.Start < end, nameof(input.End), "Ende muss nach dem Beginn liegen.");
            errors.Check(end <= now + ClockSkew, nameof(input.End), "Ende liegt in der Zukunft.");
            errors.Check(end - input.Start <= MaxRecord, nameof(input.End), $"Höchstens {MaxRecord.TotalHours:0} Stunden am Stück; bitte als zwei Abschnitte eintragen.");
        }

        if (!errors.Any)
        {
            var until = input.End ?? DateTimeOffset.MaxValue;
            var overlaps = await db.TimeRecords.AnyAsync(r => r.EmployeeId == input.EmployeeId && !r.IsDeleted && r.Id != recordId
                && r.Start < until && (r.End == null || input.Start < r.End), ct);
            errors.Check(!overlaps, nameof(input.Start), "Überschneidet sich mit einem anderen Abschnitt dieser Person.");
            if (input.End is null)
            {
                errors.Check(!await db.TimeRecords.AnyAsync(r => r.EmployeeId == input.EmployeeId && !r.IsDeleted && r.Id != recordId && r.End == null, ct),
                    nameof(input.End), "Es läuft schon ein Abschnitt; bitte ein Ende eintragen.");
            }
        }

        return errors.Any ? errors.Problem() : null;
    }

    private static void CheckReason(Errors errors, string? reason)
    {
        errors.Check(reason?.Trim().Length >= 3, "Reason", "Bitte kurz begründen (wird protokolliert).");
        errors.Check((reason?.Length ?? 0) <= 500, "Reason", "Höchstens 500 Zeichen.");
    }

    private static async Task<TimeRecordChange> ChangeAsync(
        TimeRecord record, TimeRecordAction action, DateTimeOffset? newStart, DateTimeOffset? newEnd, string reason,
        AccessService accessService, AispaDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var userId = (await accessService.CurrentAsync(ct))!.UserId;
        var name = await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? "";
        return new TimeRecordChange
        {
            TimeRecordId = record.Id,
            ChangedAt = now,
            ChangedByUserId = userId,
            ChangedByName = name,
            Action = action,
            OldStart = action == TimeRecordAction.Created ? null : record.Start,
            OldEnd = action == TimeRecordAction.Created ? null : record.End,
            NewStart = action == TimeRecordAction.Created ? record.Start : newStart,
            NewEnd = action == TimeRecordAction.Created ? record.End : newEnd,
            Reason = reason.Trim(),
        };
    }

    private static ProblemHttpResult Conflict(string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Nicht möglich.", detail: detail);
}
