using System.Globalization;
using System.Text;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Operations;

/// <summary>Raumpflege als Aufgabe, Tagesstart-Kontrolle je Raum, Abnahme durch die Standortleitung und Hygieneprotokoll.</summary>
internal static class RoomCareEndpoints
{
    private const int MaxItems = 30;

    public static IEndpointRouteBuilder MapRoomCareEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Betrieb");

        group.MapPut("/appointments/{appointmentId:guid}/room-care", async Task<Results<NoContent, NotFound, ValidationProblem, ForbidHttpResult>> (
                Guid appointmentId, RoomCareCheckInput input, AispaDbContext db, AccessService accessService, RoomCareService care, TimeProvider time,
                IMessageBus bus, TenantContext tenants, CancellationToken ct) =>
            {
                var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
                if (appointment is null)
                {
                    return TypedResults.NotFound();
                }

                var room = await db.Rooms.AsNoTracking().FirstAsync(r => r.Id == appointment.RoomId, ct);
                var current = await accessService.CurrentAsync(ct);
                if (current is null || !current.CanSee(appointment.LocationId))
                {
                    return TypedResults.Forbid();
                }

                if (room.CleaningMinutes == 0 || appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
                {
                    return new Errors().With("appointmentId", $"Nach diesem Termin gibt es in {room.Name} keine Raumpflege.");
                }

                // Erledigt ist die Raumpflege erst, wenn jede Zeile des Raumchecks beantwortet ist (ADR 0068).
                if (input.Done && await care.TaskOfAsync(appointment, ct) is { Restock: { } lines } && lines.Count(l => l.State == RestockState.Open) is > 0 and var open)
                {
                    return new Errors().With("appointmentId", $"Erst die Ausstattung prüfen: {(open == 1 ? "1 Zeile" : $"{open} Zeilen")} offen.");
                }

                // Termine im selben Raum mit gleichem Ende der Belegung (Paarsuite) sind eine Aufgabe und werden zusammen abgehakt.
                var group = await db.Appointments.AsNoTracking().Where(a => a.RoomId == appointment.RoomId && a.BlockedUntil == appointment.BlockedUntil
                    && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow).Select(a => a.Id).ToListAsync(ct);
                var existing = await db.RoomCareChecks.Where(c => group.Contains(c.AppointmentId)).ToListAsync(ct);
                if (input.Done)
                {
                    var userId = current.UserId;
                    var now = time.GetUtcNow();
                    db.RoomCareChecks.AddRange(group.Where(id => existing.All(c => c.AppointmentId != id))
                        .Select(id => new RoomCareCheck { AppointmentId = id, RoomId = room.Id, CheckedAt = now, UserId = userId }));
                }
                else
                {
                    db.RoomCareChecks.RemoveRange(existing);
                }

                await db.SaveChangesAsync(ct);
                await bus.PublishOperationsChangedAsync(tenants, appointment.LocationId, OperationsChangeKind.RoomCare);
                return TypedResults.NoContent();
            })
            .WithName("CheckRoomCare")
            .WithSummary("Hakt die Raumpflege nach einem Termin ab (mit Person und Zeit) oder nimmt den Haken zurück. Gilt für alle Termine desselben Raums mit gleichem Ende der Belegung.")
            .RequirePermission(AccessRight.ChangeStatus);

        group.MapPut("/appointments/{appointmentId:guid}/room-care/restock/{itemId:guid}", async Task<Results<Ok<RoomCareTaskDto>, NotFound, ValidationProblem, ForbidHttpResult>> (
                Guid appointmentId, Guid itemId, RestockAnswer input, AispaDbContext db, AccessService accessService, RoomCareService care, RoomRestockService restock,
                IMessageBus bus, TenantContext tenants, CancellationToken ct) =>
            {
                var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
                if (appointment is null)
                {
                    return TypedResults.NotFound();
                }

                var current = await accessService.CurrentAsync(ct);
                if (current is null || !current.CanSee(appointment.LocationId))
                {
                    return TypedResults.Forbid();
                }

                if (await care.TaskOfAsync(appointment, ct) is not { } task)
                {
                    return new Errors().With("appointmentId", "Nach diesem Termin gibt es keine Raumpflege.");
                }

                if (await restock.AnswerAsync(task, appointment.LocationId, itemId, input, current.UserId, ct) is { } errors)
                {
                    return errors.Problem();
                }

                await bus.PublishOperationsChangedAsync(tenants, appointment.LocationId, OperationsChangeKind.RoomCare);
                if (input.State == RestockState.Refilled)
                {
                    await bus.PublishOperationsChangedAsync(tenants, appointment.LocationId, OperationsChangeKind.Stock);
                }

                return TypedResults.Ok((await care.TaskOfAsync(appointment, ct))!);
            })
            .AllowChannel(AccessChannel.StaffApp)
            .WithName("AnswerRoomRestock")
            .WithSummary("Beantwortet eine Zeile des Raumchecks nach der Behandlung: „aufgefüllt“ bucht Nachfüllen vom Hauptlager in den Raumschrank (Menge ohne Angabe die vorgeschlagene), "
                + "„reicht noch“ bucht nichts. Liefert die aktualisierte Raumpflege-Aufgabe. Wer die Raumpflege abhaken darf (Status ändern, auch Therapeut:innen), darf auch antworten.")
            .RequirePermission(AccessRight.ChangeStatus);

        group.MapGet("/rooms/{roomId:guid}/start-checklist", async Task<Results<Ok<List<RoomStartItemDto>>, NotFound>> (Guid roomId, AispaDbContext db, CancellationToken ct) =>
                await db.Rooms.AnyAsync(r => r.Id == roomId, ct)
                    ? TypedResults.Ok(await db.RoomStartItems.AsNoTracking().Where(i => i.RoomId == roomId && i.IsActive).OrderBy(i => i.Position)
                        .Select(i => new RoomStartItemDto(i.Id, i.Text)).ToListAsync(ct))
                    : TypedResults.NotFound())
            .WithName("GetRoomStartChecklist")
            .WithSummary("Checkliste der Tagesstart-Kontrolle eines Raums, z. B. „Sauna aufheizen“.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("/rooms/{roomId:guid}/start-checklist", async Task<Results<Ok<List<RoomStartItemDto>>, NotFound, ValidationProblem, ForbidHttpResult>> (
                Guid roomId, List<RoomStartItemInput> input, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId && r.IsActive, ct);
                if (room is null)
                {
                    return TypedResults.NotFound();
                }

                if (!(await accessService.CurrentAsync(ct))!.CanSee(room.LocationId))
                {
                    return TypedResults.Forbid();
                }

                var errors = new Errors();
                errors.Check(input.Count <= MaxItems, "items", $"Höchstens {MaxItems} Punkte.");
                for (var i = 0; i < input.Count; i++)
                {
                    errors.Check(!string.IsNullOrWhiteSpace(input[i].Text) && input[i].Text.Trim().Length <= 200, $"[{i}].{nameof(RoomStartItemInput.Text)}",
                        "Text angeben, höchstens 200 Zeichen.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Gleiche Texte behalten ihre Id; entfernte Punkte werden ausgeschaltet, damit das Hygieneprotokoll sie behält.
                var existing = await db.RoomStartItems.Where(i => i.RoomId == roomId).ToListAsync(ct);
                var kept = new List<RoomStartItem>();
                for (var i = 0; i < input.Count; i++)
                {
                    var text = input[i].Text.Trim();
                    var item = existing.FirstOrDefault(e => !kept.Contains(e) && e.Text == text);
                    if (item is null)
                    {
                        db.RoomStartItems.Add(item = new RoomStartItem { RoomId = roomId, Text = text });
                    }

                    item.IsActive = true;
                    item.Position = i;
                    kept.Add(item);
                }

                foreach (var removed in existing.Except(kept))
                {
                    removed.IsActive = false;
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(kept.Select(i => new RoomStartItemDto(i.Id, i.Text)).ToList());
            })
            .WithName("SetRoomStartChecklist")
            .WithSummary("Ersetzt die Checkliste der Tagesstart-Kontrolle eines Raums. Entfernte Punkte bleiben im Hygieneprotokoll, wo sie abgehakt wurden.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/locations/{locationId:guid}/room-care", async Task<Results<Ok<RoomCareDay>, NotFound>> (
                Guid locationId, DateOnly date, AispaDbContext db, RoomCareService care, CancellationToken ct) =>
            {
                if (await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is not { } timeZone)
                {
                    return TypedResults.NotFound();
                }

                var zone = LocalTime.Zone(timeZone);
                var (from, until) = LocalTime.Day(date, zone);
                var appointments = await db.Appointments.AsNoTracking().Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until).ToListAsync(ct);
                return TypedResults.Ok(await care.DayAsync(locationId, date, zone, appointments, ct));
            })
            .WithName("GetRoomCareDay")
            .WithSummary("Raumpflege-Aufgaben und Zustand der Räume (Tagesstart, Raumpflege, Abnahmen) an einem Tag; dieselben Angaben stehen auch im Kalendertag.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/locations/{locationId:guid}/room-start", async Task<Results<Ok<RoomStartDayDto>, NotFound>> (
                Guid locationId, DateOnly date, RoomCareService care, CancellationToken ct) =>
                await care.StartDayAsync(locationId, date, ct) is { } day ? TypedResults.Ok(day) : TypedResults.NotFound())
            .WithName("GetRoomStartDay")
            .WithSummary("Tagesstart-Kontrolle aller Räume mit Checkliste an einem Tag: abgehakte Punkte mit Person und Zeit, Stand je Raum und Abnahme.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("/rooms/{roomId:guid}/start-checks/{itemId:guid}", async Task<Results<NoContent, NotFound, ValidationProblem, ForbidHttpResult>> (
                Guid roomId, Guid itemId, RoomStartCheckInput input, AispaDbContext db, AccessService accessService, TimeProvider time, IMessageBus bus,
                TenantContext tenants, CancellationToken ct) =>
            {
                var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId && r.IsActive, ct);
                if (room is null || !await db.RoomStartItems.AnyAsync(i => i.Id == itemId && i.RoomId == roomId && i.IsActive, ct))
                {
                    return TypedResults.NotFound();
                }

                var current = await accessService.CurrentAsync(ct);
                if (current is null || !current.CanSee(room.LocationId))
                {
                    return TypedResults.Forbid();
                }

                var zone = LocalTime.Zone(await db.Locations.Where(l => l.Id == room.LocationId).Select(l => l.TimeZone).FirstAsync(ct));
                var now = time.GetUtcNow();
                if (input.Date > LocalTime.LocalDate(now, zone))
                {
                    return new Errors().With(nameof(input.Date), "Abgehakt wird heute oder rückwirkend, nicht im Voraus.");
                }

                if (await db.RoomApprovals.AnyAsync(a => a.RoomId == roomId && a.Date == input.Date && a.Kind == RoomApprovalKind.DayStart, ct))
                {
                    return new Errors().With(nameof(input.Date), $"Der Tagesstart von {room.Name} ist an diesem Tag schon abgenommen und lässt sich nicht mehr ändern.");
                }

                var check = await db.RoomStartChecks.FirstOrDefaultAsync(c => c.RoomId == roomId && c.Date == input.Date && c.ItemId == itemId, ct);
                if (input.Done && check is null)
                {
                    db.RoomStartChecks.Add(new RoomStartCheck
                    {
                        RoomId = roomId,
                        Date = input.Date,
                        ItemId = itemId,
                        CheckedAt = now,
                        UserId = current.UserId,
                    });
                }
                else if (!input.Done && check is not null)
                {
                    db.RoomStartChecks.Remove(check);
                }

                await db.SaveChangesAsync(ct);
                await bus.PublishOperationsChangedAsync(tenants, room.LocationId, OperationsChangeKind.RoomStart);
                return TypedResults.NoContent();
            })
            .WithName("CheckRoomStartItem")
            .WithSummary("Hakt einen Punkt der Tagesstart-Kontrolle für einen Tag ab (mit Person und Zeit) oder nimmt den Haken zurück. Nach der Abnahme nicht mehr änderbar.")
            .RequirePermission(AccessRight.ChangeStatus);

        group.MapPost("/rooms/{roomId:guid}/approvals", async Task<Results<Ok<RoomApprovalDto>, NotFound, ValidationProblem, ProblemHttpResult>> (
                Guid roomId, RoomApprovalInput input, AispaDbContext db, AccessService accessService, UserManager<AispaUser> users, RoomCareService care,
                TimeProvider time, IMessageBus bus, TenantContext tenants, CancellationToken ct) =>
            {
                var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId && r.IsActive, ct);
                if (room is null)
                {
                    return TypedResults.NotFound();
                }

                if (!Enum.IsDefined(input.Kind))
                {
                    return new Errors().With(nameof(input.Kind), "Art der Abnahme unbekannt.");
                }

                // Die Standortleitung nimmt selbst ab; sonst bestätigt sie am selben Platz mit Name und PIN.
                var access = await accessService.CurrentAsync(ct);
                string? approver;
                if (access is not null && access.Has(AccessRight.ManageLocation) && access.CanSee(room.LocationId))
                {
                    approver = access.UserId;
                }
                else if (input.Approval is null)
                {
                    return TypedResults.Problem("Die Abnahme macht die Standortleitung: mit Name und PIN bestätigen.", statusCode: StatusCodes.Status403Forbidden);
                }
                else
                {
                    string? error;
                    (approver, error) = await QuickSwitch.ApproveAsync(input.Approval, room.LocationId, db, users, time.GetUtcNow(), ct,
                        AccessRight.ManageLocation, "Räume nicht abnehmen");
                    if (approver is null)
                    {
                        return TypedResults.Problem(error, statusCode: StatusCodes.Status403Forbidden);
                    }
                }

                var (approval, rejected) = await care.ApproveAsync(roomId, input.Date, input.Kind, approver, access?.UserId, ct);
                if (approval is null)
                {
                    return new Errors().With(nameof(input.Date), rejected!);
                }

                await bus.PublishOperationsChangedAsync(tenants, room.LocationId,
                    input.Kind == RoomApprovalKind.DayStart ? OperationsChangeKind.RoomStart : OperationsChangeKind.RoomCare);
                return TypedResults.Ok(approval);
            })
            .WithName("ApproveRoom")
            .WithSummary("Nimmt den Tagesstart oder die Raumpflege eines Raums an einem Tag ab. Standortleitung und Inhaber direkt, sonst mit Name und PIN der Leitung (Approval).")
            .RequirePermission(AccessRight.ChangeStatus);

        group.MapGet("/locations/{locationId:guid}/room-approvers", async (Guid locationId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                await QuickSwitch.ApproversAsync(locationId, db, time.GetUtcNow(), ct, AccessRight.ManageLocation))
            .WithName("ListRoomApprovers")
            .WithSummary("Wer am Standort Tagesstart und Raumpflege per PIN abnehmen kann (Inhaber, Standortleitung mit PIN).")
            .RequirePermission(AccessRight.ChangeStatus);

        group.MapGet("/locations/{locationId:guid}/hygiene-log", async Task<Results<Ok<HygieneLogDto>, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly from, DateOnly until, RoomCareService care, CancellationToken ct) =>
            {
                if (LogRange(from, until) is { } problem)
                {
                    return problem;
                }

                return await care.LogAsync(locationId, from, until, ct) is { } log ? TypedResults.Ok(log) : TypedResults.NotFound();
            })
            .WithName("GetHygieneLog")
            .WithSummary("Hygieneprotokoll je Tag und Raum (Ortszeit, beide Tage eingeschlossen, bis heute): Punkte der Tagesstart-Kontrolle und Raumpflege mit Erledigung und Abnahme.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/locations/{locationId:guid}/hygiene-log.csv", async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly from, DateOnly until, RoomCareService care, CancellationToken ct) =>
            {
                if (LogRange(from, until) is { } problem)
                {
                    return problem;
                }

                if (await care.LogAsync(locationId, from, until, ct) is not { } log)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.File(Csv(log, await LocalZoneAsync(care, locationId, ct)), "text/csv; charset=utf-8", $"hygieneprotokoll-{from:yyyy-MM-dd}-{until:yyyy-MM-dd}.csv");
            })
            .WithName("GetHygieneLogCsv")
            .WithSummary("Hygieneprotokoll als CSV (Semikolon, UTF-8 mit BOM, für Excel).")
            .RequirePermission(AccessRight.ManageLocation);

        return routes;
    }

    private static ValidationProblem? LogRange(DateOnly from, DateOnly until) =>
        from > until || until.DayNumber - from.DayNumber >= RoomCareService.MaxLogDays
            ? new Errors().With("until", $"Zeitraum von höchstens {RoomCareService.MaxLogDays} Tagen, Ende nicht vor Beginn.")
            : null;

    private static Task<TimeZoneInfo> LocalZoneAsync(RoomCareService care, Guid locationId, CancellationToken ct) => care.ZoneAsync(locationId, ct);

    /// <summary>CSV mit Semikolon und BOM (Excel in AT/DE/CH); Zellen, die wie Formeln beginnen, werden entschärft.</summary>
    internal static byte[] Csv(HygieneLogDto log, TimeZoneInfo zone)
    {
        var german = CultureInfo.GetCultureInfo("de-AT");
        string Local(DateTimeOffset? at, string format) => at is { } value ? TimeZoneInfo.ConvertTime(value, zone).ToString(format, german) : "";
        var text = new StringBuilder();
        text.AppendLine("Datum;Raum;Art;Punkt;Fällig bis;Erledigt;Erledigt am;Erledigt von;Abgenommen am;Abgenommen von");
        foreach (var r in log.Rows)
        {
            text.AppendLine(string.Join(';',
                r.Date.ToString("dd.MM.yyyy", german),
                Cell(r.RoomName),
                r.Kind == RoomApprovalKind.DayStart ? "Tagesstart" : "Raumpflege",
                Cell(r.Item),
                Local(r.Due, "HH:mm"),
                r.Done ? "ja" : "offen",
                Local(r.DoneAt, "dd.MM.yyyy HH:mm"),
                Cell(r.DoneBy ?? ""),
                Local(r.ApprovedAt, "dd.MM.yyyy HH:mm"),
                Cell(r.ApprovedBy ?? "")));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Cell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    }
}

internal static class ErrorsExtensions
{
    /// <summary>Ein einzelner Fehler als fertige Antwort.</summary>
    public static ValidationProblem With(this Errors errors, string field, string message)
    {
        errors.Add(field, message);
        return errors.Problem();
    }
}
