using System.Globalization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Staff;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Roster;

/// <summary>
/// Dienstplan: die Wochenarbeitszeit ist der Normalfall, für einzelne Tage gilt stattdessen der Plan
/// (anderer Dienst, Urlaub, Krankenstand, Schulung, frei). Kalender und Buchung richten sich danach.
/// </summary>
internal static class RosterEndpoints
{
    private static readonly AppointmentStatus[] Open = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn];

    public static IEndpointRouteBuilder MapRosterEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Dienstplan").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/locations/{locationId:guid}/roster", async Task<Results<Ok<RosterWeek>, NotFound, ProblemHttpResult>> (
                Guid locationId, DateOnly from, int? days, Guid? employeeId, AispaDbContext db, RosterService roster, AccessService accessService, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                if (employeeId is not null && !access.Has(AccessRight.ManageLocation) && !access.Has(AccessRight.ViewStaffRecords))
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Keine Berechtigung.", detail: "Den Dienstplan einer einzelnen Person sehen nur Standortleitung und Personalunterlagen.");
                }

                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var count = Math.Clamp(days ?? 7, 1, 31);
                var until = from.AddDays(count - 1);
                // Mit employeeId nur diese aktive Person, auch wenn sie am Standort noch nicht eingeteilt ist (Personenkarte der Windows-App).
                var people = employeeId is { } one ? db.Employees.Where(e => e.IsActive && e.Id == one) : roster.EmployeesAt(locationId, from, drafts: true);
                var employees = await people.AsNoTracking().Include(e => e.WorkingHours).OrderBy(e => e.DisplayName).ToListAsync(ct);
                // Ab dem Vortag laden, damit auch am ersten Tag die Ruhezeit geprüft werden kann. Entwürfe zeigen, wie der Plan werden soll.
                var plans = await roster.PlansAsync(employees, from.AddDays(-1), until, ct, drafts: true);
                var rules = await roster.RulesAsync(ct);
                var dates = Enumerable.Range(0, count).Select(from.AddDays).ToList();

                var rows = employees
                    .Select(e =>
                    {
                        var days = dates.Select(d => plans[(e.Id, d)]).ToList();
                        // Wie beim Speichern: nur sichtbare Standorte. Abwesenheiten sieht, wer die Person an einem sichtbaren Standort hat; die Hinweise zum Arbeitszeitrecht rechnen über alle Dienste der Person.
                        var worksHere = employeeId is null || e.WorkingHours.Any(w => access.CanSee(w.LocationId)) || days.Any(d => d.Shifts.Any(s => access.CanSee(s.LocationId!.Value)));
                        var visible = days.Select(d => d.VisibleTo(access.CanSee, worksHere)).ToList();
                        var cells = visible.Select((d, i) => Cell(d, days[i], plans[(e.Id, d.Date.AddDays(-1))], rules)).ToList();
                        var perWeek = count >= 7 ? days.Sum(d => d.Hours) / count * 7 : 0;
                        return new RosterRow(e.Id, e.DisplayName, e.Color, cells, Math.Round(visible.Sum(d => d.Hours), 2), WorkingTime.WeekWarnings(perWeek, days, rules));
                    })
                    .ToList();

                var drafts = await db.RosterDrafts.CountAsync(d => d.LocationId == locationId && d.Date >= from && d.Date <= until, ct);
                var published = await db.RosterPublications.Where(p => p.LocationId == locationId)
                    .MaxAsync(p => (DateTimeOffset?)p.PublishedAt, ct);
                return TypedResults.Ok(new RosterWeek(locationId, from, dates, rows, drafts, published));
            })
            .WithName("GetRoster")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ViewCalendar)
            .WithSummary("Dienstplan eines Standorts (Standard: 7 Tage ab from) mit Entwürfen und Hinweisen zu Arbeits- und Ruhezeiten; mit employeeId nur für diese Person.");

        // Unter dem Standort, damit eine Standortleitung auch Personen anderer Standorte als Aushilfe einteilen kann.
        group.MapPut("/locations/{locationId:guid}/roster/{employeeId:guid}/{date}", async Task<Results<Ok<RosterDayResult>, ValidationProblem, NotFound, Conflict<BookingRejected>>> (
                Guid locationId, Guid employeeId, DateOnly date, RosterDayInput input, AispaDbContext db, RosterService roster,
                AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var employee = await db.Employees.Include(e => e.WorkingHours).FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive, ct);
                if (employee is null)
                {
                    return TypedResults.NotFound();
                }

                var existing = await db.RosterDays.Include(d => d.Entries).FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.Date == date, ct);
                var draft = await db.RosterDrafts.Include(d => d.Entries).FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.Date == date, ct);
                var current = input.Draft && draft is not null ? DayPlan.For(employee, draft) : DayPlan.For(employee, existing, date);

                // Dienste an Standorten ohne Zugang bleiben, wie sie sind (wie bei den Arbeitszeiten in den Stammdaten).
                var entries = (input.Entries ?? [])
                    .Where(e => e.Kind != RosterKind.Work || (e.LocationId is { } l && access.CanSee(l)))
                    .Concat(current.Shifts.Where(s => !access.CanSee(s.LocationId!.Value)))
                    .Select(e => e.Kind == RosterKind.Work ? e : e with { LocationId = null, Start = null, End = null })
                    .ToList();

                var errors = new Errors();
                errors.Check(entries.Count <= 6, nameof(input.Entries), "Höchstens 6 Einträge pro Tag.");
                foreach (var entry in entries.Where(e => e.Kind == RosterKind.Work))
                {
                    errors.Check(entry.Start is { } s && entry.End is { } end && s < end, nameof(input.Entries), "Beginn muss vor Ende liegen.");
                }

                var locationIds = entries.Where(e => e.LocationId is not null).Select(e => e.LocationId!.Value).Distinct().ToList();
                errors.Check(await db.Locations.CountAsync(l => locationIds.Contains(l.Id), ct) == locationIds.Count,
                    nameof(input.Entries), "Unbekannter Standort.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Sonderregelung (R05 Slice 111, E10) und Ersthelferinnen im Dienst (R05 Slice 117, E14): warnen in einem Text, sperren nie; gespeichert wird nach der Bestätigung.
                if (!input.RulesConfirmed)
                {
                    var spans = entries.Where(e => e.Kind == RosterKind.Work).Select(e => (e.Start!.Value, e.End!.Value)).ToList();
                    var hours = spans.Sum(s => (decimal)(s.Item2 - s.Item1).TotalHours);
                    var rule = await StaffRuleCheck.WarningAsync(db, employeeId, employee.PreferredName ?? employee.DisplayName, date, spans, hours, ct);
                    var firstAid = await FirstAidCheck.WarningAsync(db, roster, locationId, employee, date, entries, ct);
                    if (FirstAidCheck.Combine(rule, firstAid) is { } warning)
                    {
                        return TypedResults.Conflict(new BookingRejected(warning, [], StaffRule: true));
                    }
                }

                var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
                if (input.Draft)
                {
                    SetDraft(db, draft, employeeId, date, locationId, reset: false, entries, note, access.UserId, time.GetUtcNow());
                }
                else
                {
                    // Sofort gültig (z. B. Krankenstand): ersetzt auch einen offenen Entwurf für diesen Tag.
                    Publish(db, existing, employeeId, date, entries, note, access.UserId, time.GetUtcNow());
                    if (draft is not null)
                    {
                        db.RosterDrafts.Remove(draft);
                    }
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await ResultAsync(db, roster, employee, date, ct, drafts: input.Draft));
            })
            .WithName("PlanRosterDay")
            .WithSummary("Plant einen Tag einer Person (ersetzt für diesen Tag die Wochenarbeitszeit), sofort oder als Entwurf. Termine außerhalb des neuen Dienstes werden gemeldet, nicht verschoben.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("/locations/{locationId:guid}/roster/{employeeId:guid}/{date}", async Task<Results<Ok<RosterDayResult>, ValidationProblem, NotFound>> (
                Guid locationId, Guid employeeId, DateOnly date, bool? draft, AispaDbContext db, RosterService roster, AccessService accessService,
                TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var employee = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
                if (employee is null)
                {
                    return TypedResults.NotFound();
                }

                var foreign = await db.RosterDays.Where(d => d.EmployeeId == employeeId && d.Date == date).SelectMany(d => d.Entries)
                    .Where(x => x.LocationId != null).Select(x => x.LocationId!.Value).ToListAsync(ct);
                if (foreign.Any(l => !access.CanSee(l)))
                {
                    var errors = new Errors();
                    errors.Add("date", "An diesem Tag ist die Person auch an einem anderen Standort eingeteilt; bitte nur den eigenen Dienst ändern.");
                    return errors.Problem();
                }

                var existingDraft = await db.RosterDrafts.Include(d => d.Entries).FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.Date == date, ct);
                if (draft == true)
                {
                    SetDraft(db, existingDraft, employeeId, date, locationId, reset: true, [], null, access.UserId, time.GetUtcNow());
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(await ResultAsync(db, roster, employee, date, ct, drafts: true));
                }

                await db.RosterDays.Where(d => d.EmployeeId == employeeId && d.Date == date).ExecuteDeleteAsync(ct);
                await db.RosterDrafts.Where(d => d.EmployeeId == employeeId && d.Date == date).ExecuteDeleteAsync(ct);
                return TypedResults.Ok(await ResultAsync(db, roster, employee, date, ct));
            })
            .WithName("ResetRosterDay")
            .WithSummary("Setzt einen Tag auf die Wochenarbeitszeit zurück, sofort oder mit draft=true als Entwurf.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/locations/{locationId:guid}/roster/publish", async Task<Results<Ok<RosterPublishResult>, ValidationProblem, NotFound>> (
                Guid locationId, RosterPublishRequest request, AispaDbContext db, RosterService roster, AccessService accessService,
                TimeProvider time, CancellationToken ct) =>
            {
                var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
                if (zoneId is null)
                {
                    return TypedResults.NotFound();
                }

                if (Range(request) is { } invalid)
                {
                    return invalid.Problem();
                }

                var access = (await accessService.CurrentAsync(ct))!;
                var now = time.GetUtcNow();
                var drafts = await db.RosterDrafts.Include(d => d.Entries)
                    .Where(d => d.LocationId == locationId && d.Date >= request.From && d.Date <= request.Until)
                    .OrderBy(d => d.Date)
                    .ToListAsync(ct);
                var keys = drafts.Select(d => d.EmployeeId).Distinct().ToList();
                var existing = await db.RosterDays.Include(d => d.Entries)
                    .Where(d => keys.Contains(d.EmployeeId) && d.Date >= request.From && d.Date <= request.Until)
                    .ToDictionaryAsync(d => (d.EmployeeId, d.Date), ct);
                foreach (var draft in drafts)
                {
                    var day = existing.GetValueOrDefault((draft.EmployeeId, draft.Date));
                    if (draft.Reset)
                    {
                        if (day is not null)
                        {
                            db.RosterDays.Remove(day);
                        }
                    }
                    else
                    {
                        Publish(db, day, draft.EmployeeId, draft.Date,
                            [.. draft.Entries.Select(e => new RosterEntry(e.Kind, e.LocationId, e.Start, e.End))], draft.Note, access.UserId, now);
                    }

                    db.RosterDrafts.Remove(draft);
                }

                if (drafts.Count > 0)
                {
                    db.RosterPublications.Add(new RosterPublication
                    {
                        LocationId = locationId,
                        From = request.From,
                        Until = request.Until,
                        Days = drafts.Count,
                        PublishedAt = now,
                        PublishedByUserId = access.UserId,
                    });
                }

                await db.SaveChangesAsync(ct);

                // Kurzfristig: weniger Vorlauf als die Ankündigungsfrist des Landes, gerechnet ab heute in der Zeitzone des Standorts.
                var rules = await roster.RulesAsync(ct);
                var today = LocalTime.LocalDate(now, LocalTime.Zone(zoneId));
                var employees = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).Where(e => keys.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
                var shortNotice = drafts
                    .Where(d => d.Date.DayNumber - today.DayNumber < rules.NoticeDays && employees.ContainsKey(d.EmployeeId))
                    .Select(d => $"{employees[d.EmployeeId].DisplayName}, {d.Date.ToString("ddd d.M.", German)}: weniger als {rules.NoticeDays} Tage vorher ({rules.NoticeBasis}).")
                    .ToList();

                var outside = new List<AppointmentDto>();
                foreach (var draft in drafts.Where(d => employees.ContainsKey(d.EmployeeId)))
                {
                    outside.AddRange((await ResultAsync(db, roster, employees[draft.EmployeeId], draft.Date, ct)).AppointmentsOutsideShift);
                }

                return TypedResults.Ok(new RosterPublishResult(drafts.Count, now, shortNotice, [.. outside.DistinctBy(a => a.Id).OrderBy(a => a.Start)]));
            })
            .WithName("PublishRoster")
            .WithSummary("Veröffentlicht die Entwürfe des Standorts im Zeitraum: ab jetzt gelten sie für Kalender und Buchung. Meldet kurzfristige Änderungen und Termine außerhalb der Dienste.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/locations/{locationId:guid}/roster/discard", async Task<Results<NoContent, ValidationProblem>> (
                Guid locationId, RosterPublishRequest request, AispaDbContext db, CancellationToken ct) =>
            {
                if (Range(request) is { } invalid)
                {
                    return invalid.Problem();
                }

                await db.RosterDrafts.Where(d => d.LocationId == locationId && d.Date >= request.From && d.Date <= request.Until).ExecuteDeleteAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DiscardRosterDrafts")
            .WithSummary("Verwirft die Entwürfe des Standorts im Zeitraum; es bleibt der veröffentlichte Stand.")
            .RequirePermission(AccessRight.ManageLocation);

        return routes;
    }

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private static Errors? Range(RosterPublishRequest request)
    {
        var errors = new Errors();
        errors.Check(request.From <= request.Until && request.Until.DayNumber - request.From.DayNumber < 62, "until",
            "Ende nach dem Beginn, höchstens 62 Tage.");
        return errors.Any ? errors : null;
    }

    /// <summary>Schreibt den geltenden Plan eines Tages (neu oder ersetzt).</summary>
    internal static void Publish(AispaDbContext db, RosterDay? day, Guid employeeId, DateOnly date, IReadOnlyList<RosterEntry> entries,
        string? note, string? userId, DateTimeOffset now)
    {
        if (day is null)
        {
            day = new RosterDay { EmployeeId = employeeId, Date = date };
            db.RosterDays.Add(day);
        }
        else
        {
            db.RemoveRange(day.Entries);
            day.Entries.Clear();
        }

        day.Note = note;
        day.UpdatedAt = now;
        day.UpdatedByUserId = userId;
        foreach (var entry in entries)
        {
            var added = new RosterDayEntry { RosterDayId = day.Id, Kind = entry.Kind, LocationId = entry.LocationId, Start = entry.Start, End = entry.End };
            day.Entries.Add(added);
            db.Add(added);
        }
    }

    /// <summary>Schreibt den Entwurf eines Tages (neu oder ersetzt); <paramref name="reset"/>: zurück zur Wochenarbeitszeit.</summary>
    private static void SetDraft(AispaDbContext db, RosterDraft? draft, Guid employeeId, DateOnly date, Guid locationId, bool reset,
        IReadOnlyList<RosterEntry> entries, string? note, string? userId, DateTimeOffset now)
    {
        if (draft is null)
        {
            draft = new RosterDraft { EmployeeId = employeeId, Date = date };
            db.RosterDrafts.Add(draft);
        }
        else
        {
            db.RemoveRange(draft.Entries);
            draft.Entries.Clear();
        }

        draft.LocationId = locationId;
        draft.Reset = reset;
        draft.Note = note;
        draft.UpdatedAt = now;
        draft.UpdatedByUserId = userId;
        foreach (var entry in entries)
        {
            var added = new RosterDraftEntry { RosterDraftId = draft.Id, Kind = entry.Kind, LocationId = entry.LocationId, Start = entry.Start, End = entry.End };
            draft.Entries.Add(added);
            db.Add(added);
        }
    }

    private static RosterCell Cell(DayPlan plan, DayPlan? yesterday, WorkingTimeRules rules) => Cell(plan, plan, yesterday, rules);

    /// <summary>Die Zelle zeigt <paramref name="visible"/>; die Hinweise gelten für den ganzen Tag der Person, ohne sichtbaren Eintrag hat die Zelle keine.</summary>
    private static RosterCell Cell(DayPlan visible, DayPlan whole, DayPlan? yesterday, WorkingTimeRules rules) =>
        new(visible.Date, visible.Source, visible.Entries, visible.Note, visible.Entries.Count == 0 ? [] : WorkingTime.DayWarnings(whole, yesterday, rules), visible.Draft);

    /// <summary>Neuer Stand des Tages und offene Termine dieser Person, die jetzt außerhalb ihres Dienstes liegen.</summary>
    private static async Task<RosterDayResult> ResultAsync(
        AispaDbContext db, RosterService roster, Employee employee, DateOnly date, CancellationToken ct, bool drafts = false)
    {
        var plans = await roster.PlansAsync([employee], date.AddDays(-1), date, ct, drafts);
        var plan = plans[(employee.Id, date)];
        var rules = await roster.RulesAsync(ct);

        // Grob eingrenzen (±1 Tag in UTC), genau prüfen in der Zeitzone des jeweiligen Standorts.
        var from = new DateTimeOffset(date.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var until = from.AddDays(3);
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Join(db.Locations, a => a.LocationId, l => l.Id, (a, l) => new { Appointment = a, l.TimeZone })
            .Where(x => x.Appointment.EmployeeId == employee.Id && Open.Contains(x.Appointment.Status)
                && x.Appointment.Start < until && from < x.Appointment.End)
            .ToListAsync(ct);

        var outside = appointments
            .Where(x =>
            {
                var zone = LocalTime.Zone(x.TimeZone);
                return LocalTime.LocalDate(x.Appointment.BlockedFrom, zone) == date
                    && !plan.Shifts.Any(s => s.LocationId == x.Appointment.LocationId
                        && LocalTime.ToUtc(date, s.Start!.Value, zone) <= x.Appointment.BlockedFrom
                        && x.Appointment.BlockedUntil <= LocalTime.ToUtc(date, s.End!.Value, zone));
            })
            .OrderBy(x => x.Appointment.Start)
            .Select(x => BookingService.ToDto(x.Appointment, x.Appointment.Treatment.Name))
            .ToList();

        return new RosterDayResult(Cell(plan, plans[(employee.Id, date.AddDays(-1))], rules), outside);
    }
}
