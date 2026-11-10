using AispaStudio.Api.Access;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Closures;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Operations;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Stock;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>Kalender und Terminbuchung.</summary>
internal static class CalendarEndpoints
{
    /// <summary>So viele Tage sucht „Freie Zeit“ höchstens weiter, und so viele Zeiten verlangt <c>minSlots</c> höchstens.</summary>
    private const int MaxSearchDays = 14;

    private const int MaxWantedSlots = 50;

    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Kalender").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/locations/{locationId:guid}/calendar", async Task<Results<Ok<CalendarDay>, NotFound>> (
                Guid locationId, DateOnly date, AispaDbContext db, OnlineDbContext online, RosterService roster, RoomCareService roomCare, HolidayCalendar holidays,
                AccessService access, TimeProvider time, CancellationToken ct) =>
            {
                var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours)
                    .FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location is null)
                {
                    return TypedResults.NotFound();
                }

                var zone = LocalTime.Zone(location.TimeZone);
                var (from, until) = LocalTime.Day(date, zone);

                var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
                    .Where(a => a.LocationId == locationId && a.Start < until && from < a.End)
                    .OrderBy(a => a.Start)
                    .ToListAsync(ct);

                var shifts = await roster.ShiftsAsync(locationId, date, zone, ct);
                // Kassiert: eine Kassenzeile zum Termin (auch auf offener Rechnung); dann gibt es kein „Einchecken zurücknehmen“ mehr (Ä97).
                var appointmentIds = appointments.Select(a => a.Id).ToList();
                var billed = await db.BillLines.AsNoTracking().Where(l => l.Kind == BillLineKind.Appointment && l.AppointmentId != null && appointmentIds.Contains(l.AppointmentId.Value))
                    .Select(l => l.AppointmentId!.Value).Distinct().ToListAsync(ct);

                var rooms = db.Rooms.Where(r => r.LocationId == locationId).Select(r => r.Id);
                var equipment = db.Equipment.Where(e => e.LocationId == locationId).Select(e => e.Id);
                var employees = shifts.Select(s => s.EmployeeId).Distinct().ToList();
                var blocks = await db.ResourceBookings.AsNoTracking()
                    .Where(b => b.AppointmentId == null && b.From < until && from < b.Until)
                    .Where(b => employees.Contains(b.ResourceId) || rooms.Contains(b.ResourceId) || equipment.Contains(b.ResourceId))
                    .ToListAsync(ct);

                var guests = await GuestsAsync(db, appointments, from, zone, ct);
                var absences = await AbsencesAsync(roster, locationId, date, ct);
                var rules = await roster.RulesAsync(ct);
                var breaks = CalendarBreaks.For(shifts,
                    [.. appointments.Where(a => a.Status != AppointmentStatus.Cancelled).Select(a => (a.EmployeeId, a.BlockedFrom, a.BlockedUntil)),
                        .. blocks.Where(b => b.Kind == ResourceKind.Employee).Select(b => (b.ResourceId, b.From, b.Until))],
                    [.. blocks.Where(b => b.Kind == ResourceKind.Employee && CalendarBreaks.IsBreak(b.Reason)).Select(b => (b.ResourceId, b.From, b.Until))],
                    rules);
                var notes = await db.CalendarNotes.AsNoTracking().Where(n => n.LocationId == locationId && n.Date == date)
                    .OrderBy(n => n.At).ThenBy(n => n.CreatedAt).ToListAsync(ct);
                var treatments = appointments.Select(a => a.TreatmentId).Distinct().ToList();
                var steps = await db.TreatmentPreparationSteps.AsNoTracking().Where(p => treatments.Contains(p.TreatmentId))
                    .OrderBy(p => p.Position).Select(p => new { p.TreatmentId, p.Text }).ToListAsync(ct);
                var groupAreas = await db.AreaVisits.AsNoTracking().Include(v => v.Customer)
                    .Where(v => v.LocationId == locationId && v.GroupId != null && !v.IsCancelled && v.From < until && from < v.Until)
                    .OrderBy(v => v.From).ToListAsync(ct);
                var series = await BookingService.SeriesInfosAsync(db,
                    [.. appointments.Where(a => a.SeriesId is not null).Select(a => a.SeriesId!.Value).Distinct()], ct);
                var closed = await ClosureService.InAsync(db, locationId, from, until, zone, ct);
                var openInClosures = await ClosureService.OpenCountsAsync(db, closed, zone, time.GetUtcNow(), ct);
                var waitingInClosures = await ClosureService.WaitlistCountsAsync(db, online, closed, zone, time.GetUtcNow(), ct);
                var closuresDue = await ClosureService.DueAsync(db, online, locationId, zone, time.GetUtcNow(), ct);
                var roomClosed = await RoomClosureService.InAsync(db, locationId, from, until, zone, ct);
                var openInRoomClosures = await RoomClosureService.OpenCountsAsync(db, roomClosed, zone, time.GetUtcNow(), ct);
                var roomClosureNames = await RoomClosureService.RoomNamesAsync(db, roomClosed, ct);
                var roomClosuresDue = await RoomClosureService.DueAsync(db, locationId, zone, time.GetUtcNow(), ct);

                return TypedResults.Ok(new CalendarDay(
                    locationId,
                    date,
                    location.TimeZone,
                    [.. location.OpeningHours.Where(o => o.Day == date.DayOfWeek).OrderBy(o => o.Opens)
                        .Select(o => new TimeRange(LocalTime.ToUtc(date, o.Opens, zone), LocalTime.ToUtc(date, o.Closes, zone)))],
                    shifts,
                    [.. appointments.Select(a => BookingService.ToDto(a, a.Treatment.Name))],
                    [.. blocks.Select(ToDto)],
                    guests,
                    absences,
                    breaks,
                    [.. notes.Select(ToDto)],
                    [.. steps.DistinctBy(p => p.TreatmentId).Select(p => new CalendarPreparation(p.TreatmentId, p.Text))],
                    series,
                    [.. groupAreas.Select(AreaVisitService.ToDto)],
                    await roomCare.DayAsync(locationId, date, zone, appointments, ct),
                    await ElsewhereAsync(db, roster, locationId, date, ct),
                    [.. closed.Select(c => ClosureService.ToBand(c, zone, openInClosures[c.Id], waitingInClosures[c.Id], time.GetUtcNow()))],
                    closuresDue,
                    await holidays.FactsAsync(locationId, date, (await access.CurrentAsync(ct))?.UserId, ct),
                    [.. roomClosed.Select(c => RoomClosureService.ToBand(c, roomClosureNames, zone, openInRoomClosures[c.Id], time.GetUtcNow()))],
                    roomClosuresDue,
                    billed));
            })
            .WithName("GetCalendarDay")
            .WithSummary("Ein Tag im Kalender eines Standorts: Öffnungszeit, Dienste, Termine (auch stornierte), Sperrzeiten, Abwesenheiten, "
                + "Pausen nach dem Arbeitszeitrecht, Notizen, Vorbereitungstexte, Raumpflege-Aufgaben und Zustand der Räume (Tagesstart, Abnahmen), Schließzeiten des Standorts, Raumsperren der Räume "
                + "und die Tagesfakten (Feiertag, Schulferien, Hinweise aus aktiven Kalender-Abos; ausgeblendete Abos des eigenen Zugangs fehlen).");

        group.MapGet("/locations/{locationId:guid}/availability", async Task<Results<Ok<List<AvailableSlot>>, NotFound>> (
                Guid locationId, Guid treatmentId, DateOnly date, Guid? employeeId, int? stepMinutes, Guid? ignoreAppointmentId, Guid? customerId,
                Guid? roomId, int? minSlots, int? days, BookingService booking, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var step = TimeSpan.FromMinutes(Math.Clamp(stepMinutes ?? 15, 5, 120));
                var wanted = Math.Clamp(minSlots ?? 0, 0, MaxWantedSlots);
                var lastDay = Math.Clamp(days ?? MaxSearchDays, 1, MaxSearchDays);
                var rules = await CustomerRules.LoadAsync(db, customerId, ct);
                var now = time.GetUtcNow();
                var slots = new List<AvailableSlot>();
                for (var offset = 0; offset < lastDay; offset++)
                {
                    var day = date.AddDays(offset);
                    // ignoreAppointmentId: beim Verschieben zählt die bisherige Zeit des Termins als frei.
                    var context = await booking.LoadContextAsync(locationId, treatmentId, day, ignoreAppointmentId, ct);
                    if (context is null)
                    {
                        return TypedResults.NotFound();
                    }

                    // Mit Raumwunsch plant der Planer nur in diesem Raum; sonst wählte er den ersten freien und der Wunschraum käme nie vor.
                    var planning = rules.Apply(context, employeeId);
                    if (roomId is { } wantedRoom)
                    {
                        planning = planning with { Rooms = [.. planning.Rooms.Where(r => r.Id == wantedRoom)] };
                    }

                    var found = new ResourcePlanner(planning).FreeSlots(day, step, employeeId, snug: true);
                    // Beim Weitersuchen zählen nur Zeiten ab jetzt; sonst wären die vergangenen Stunden von heute schon „freie Zeiten“.
                    slots.AddRange(wanted > 0 ? found.Where(s => s.Start >= now) : found);
                    if (slots.Count >= wanted)
                    {
                        break;
                    }
                }

                return TypedResults.Ok(slots);
            })
            .WithName("FindAvailableSlots")
            .WithSummary("Freie Startzeiten für eine Behandlung an einem Tag, je Person mit passendem Raum und Gerät. "
                + "Mit Bewertung, ob eine Zeit eine unbrauchbare Lücke lässt, und zusätzlich lückenlos anschließenden Zeiten außerhalb des Rasters. "
                + "Mit customerId gelten die Kennzeichen des Gastes (Puffer, „Nicht bei mir“). Mit roomId nur dieser Raum. "
                + "Mit minSlots sucht die Abfrage an den folgenden Tagen weiter (höchstens days, bis 14), bis mindestens so viele Zeiten ab jetzt gefunden sind; "
                + "ein Tag wird immer ganz geliefert.");

        group.MapGet("/locations/{locationId:guid}/gap-suggestions", async Task<Results<Ok<List<GapSuggestion>>, NotFound>> (
                Guid locationId, DateOnly date, BookingService booking, CancellationToken ct) =>
                await booking.GapSuggestionsAsync(locationId, date, ct) is { } suggestions ? TypedResults.Ok(suggestions) : TypedResults.NotFound())
            .WithName("GetGapSuggestions")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Vorschläge, gebuchte Termine um wenige Minuten zu verschieben, damit bei einer Person keine Lücke bleibt, "
                + "in die keine Behandlung passt. Verschoben wird nur über „MoveAppointment“, nach Absprache mit dem Gast.");

        group.MapGet("/appointments/{appointmentId:guid}", async Task<Results<Ok<AppointmentDto>, NotFound>> (
                Guid appointmentId, AispaDbContext db, CancellationToken ct) =>
                await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId, ct) is { } a
                    ? TypedResults.Ok(BookingService.ToDto(a, a.Treatment.Name))
                    : TypedResults.NotFound())
            .WithName("GetAppointment");

        group.MapGet("/appointments/{appointmentId:guid}/history", async Task<Results<Ok<List<HistoryEntry>>, NotFound>> (
                Guid appointmentId, AppointmentHistory history, CancellationToken ct) =>
                await history.ForAsync(appointmentId, ct) is { } entries ? TypedResults.Ok(entries) : TypedResults.NotFound())
            .WithName("GetAppointmentHistory")
            .WithSummary("Verlauf des Termins, neueste oben (ADR 0082): angelegt über welchen Kanal, von wem und wann, Änderungen mit altem und neuem Wert, "
                + "Storno und Nicht erschienen. Sichtbar für alle, die den Termin sehen.");

        group.MapPost("/appointments", async Task<Results<Created<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                AppointmentRequest request, BookingService booking, StockDemandService stockDemand, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PastBookings.CheckAsync(db, time, request.LocationId, request.Start, ct) is { } past)
                {
                    return TypedResults.Conflict(past);
                }

                return await booking.BookAsync(request, ct) switch
                {
                    BookingOutcome.Success s => TypedResults.Created($"/api/appointments/{s.Appointment.Id}",
                        s.Appointment with { Warnings = Merge(s.Appointment.Warnings, await StockWarningsAsync(stockDemand, s.Appointment, ct)) }),
                    BookingOutcome.NotFound n => NotFound(n),
                    BookingOutcome.Invalid i => Invalid(i),
                    BookingOutcome.Rejected r => TypedResults.Conflict(r.Reason),
                    var other => throw Unexpected(other),
                };
            })
            .WithName("BookAppointment")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Bucht einen Termin für heute oder später (ein früherer Tag ergibt 409). Fehlende Person, Raum oder Gerät wählt der Server; bei Überschneidung 409 mit Begründung. "
                + "Reißt der Termin eine Lagergrenze (Bestand reicht nicht, Mindestmenge, nur noch wenige Behandlungen), steht das in Warnings; die Buchung bleibt gültig. "
                + "An einem Schließtag oder einem Tag ohne Dienst bucht die Rezeption trotzdem; Warnings trägt dann „Der Standort hat an diesem Tag geschlossen.“. "
                + "Außerhalb der Öffnungszeit oder des Dienstes trägt die 409 OutsideHours; mit OutsideHoursConfirmed („Trotzdem buchen“) bucht die Rezeption trotzdem, Warnings trägt dann „Außerhalb der Öffnungszeit oder des Dienstes gebucht.“.");

        group.MapPost("/appointments/{appointmentId:guid}/move", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, MoveAppointmentRequest request, BookingService booking, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PastBookings.CheckMoveAsync(db, time, appointmentId, request.Start, ct) is { } past)
                {
                    return TypedResults.Conflict(past);
                }

                return Respond(await booking.MoveAsync(appointmentId, request, ct, house: true));
            })
            .WithName("MoveAppointment")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Verschiebt einen Termin auf heute oder später (ein früherer Tag ergibt 409); ohne Angabe bleiben Person, Raum und Gerät, wenn sie zur neuen Zeit frei sind. "
                + "Mit expectedVersion (Version des Stands, von dem aus geändert wird) antwortet der Server mit 409 und dem aktuellen Termin in current, "
                + "wenn der Termin inzwischen geändert wurde; das gilt ebenso für Zeiten, Notiz, Storno, Status und Zurücknehmen.");

        group.MapPut("/appointments/{appointmentId:guid}/late", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, ReportLateRequest request, BookingService booking, CancellationToken ct) =>
                Respond(await booking.ReportLateAsync(appointmentId, request.Minutes, ct)))
            .WithName("ReportLateArrival")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Der Gast kommt später: merkt die Verspätung am Termin (0 nimmt sie zurück). Der Termin bleibt an seiner Zeit.");

        group.MapPut("/appointments/{appointmentId:guid}/times", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, AppointmentTimesRequest request, BookingService booking, CancellationToken ct) =>
                Respond(await booking.ChangeTimesAsync(appointmentId, request.PreparationMinutes, request.FollowUpMinutes, ct, request.ExpectedVersion)))
            .WithName("ChangeAppointmentTimes")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Vor- und Nachbereitung dieses einen Termins ändern; der Planer prüft Person, Raum und Gerät zur selben Startzeit. Mit expectedVersion 409 und dem aktuellen Termin in current, wenn der Termin inzwischen geändert wurde.");

        group.MapPut("/appointments/{appointmentId:guid}/note", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, AppointmentNoteRequest request, BookingService booking, CancellationToken ct) =>
                Respond(await booking.ChangeNoteAsync(appointmentId, request.Note, ct, request.ExpectedVersion)))
            .WithName("ChangeAppointmentNote")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Notiz zu diesem einen Termin setzen oder (leer) löschen, höchstens 500 Zeichen. Nicht die Kundennotiz; der Termin selbst bleibt unberührt. Mit expectedVersion 409 und dem aktuellen Termin in current, wenn der Termin inzwischen geändert wurde.");

        group.MapPut("/appointments/{appointmentId:guid}/guest", async Task<Results<Ok<GroupBookingResult>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, AssignGuestRequest request, BookingService booking, CancellationToken ct) =>
                await booking.AssignGuestAsync(appointmentId, request.CustomerId, ct) switch
                {
                    BookingOutcome.GroupBooked g => TypedResults.Ok(g.Result),
                    BookingOutcome.NotFound n => NotFound(n),
                    BookingOutcome.Invalid i => Invalid(i),
                    BookingOutcome.Rejected r => TypedResults.Conflict(r.Reason),
                    var other => throw Unexpected(other),
                })
            .WithName("AssignPlaceholderGuest")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Trägt für einen Gast ohne Namen (Gruppe) den Gast ein, bei allen seinen Terminen der Buchung.");

        group.MapPost("/appointments/{appointmentId:guid}/cancel", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, CancelAppointmentRequest request, BookingService booking, CancellationToken ct) =>
                Respond(await booking.CancelAsync(appointmentId, request.Reason, ct, expectedVersion: request.ExpectedVersion)))
            .WithName("CancelAppointment")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Storniert einen Termin und gibt Person, Raum und Gerät sofort wieder frei. Mit expectedVersion 409 und dem aktuellen Termin in current, wenn der Termin inzwischen geändert wurde.");

        group.MapGet("/appointments/{appointmentId:guid}/restoration", async Task<Ok<AppointmentRestoration>> (
                Guid appointmentId, ClosureCancellations notices, BookingService booking, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var info = await notices.RestorationAsync(appointmentId, ct);
                var appointment = await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId && a.Status == AppointmentStatus.Cancelled)
                    .Select(a => new { a.LocationId, a.Start }).FirstOrDefaultAsync(ct);
                if (appointment is null)
                {
                    return TypedResults.Ok(info);
                }

                var inPast = await PastBookings.CheckAsync(db, time, appointment.LocationId, appointment.Start, ct, PastBookings.RestoreMessage) is not null;
                return TypedResults.Ok(info with { InPast = inPast, Conflict = await booking.RestoreConflictAsync(appointmentId, ct) });
            })
            .WithName("GetAppointmentRestoration")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Was beim Zurücknehmen der Stornierung zu beachten ist: wartet die Absage-Nachricht noch, ist sie schon raus, ging eine Anzahlung mit der Absage zurück, "
                + "liegt der Termin in der Vergangenheit (inPast) oder sind Zeit, Person oder Raum inzwischen belegt (conflict); dann braucht das Zurücknehmen eine neue Zeit.");

        group.MapPost("/appointments/{appointmentId:guid}/restore", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, RestoreAppointmentRequest request, BookingService booking, ClosureCancellations notices, AispaDbContext db, TimeProvider time,
                CancellationToken ct) =>
            {
                // Die Vergangenheit prüft der Endpunkt wie beim Buchen und Verschieben (PastBookings): zur alten Zeit nur, wenn der Tag nicht vorbei ist.
                if (request.Start is { } newStart)
                {
                    if (await PastBookings.CheckMoveAsync(db, time, appointmentId, newStart, ct) is { } pastMove)
                    {
                        return TypedResults.Conflict(pastMove);
                    }
                }
                else if (await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId).Select(a => new { a.LocationId, a.Start }).FirstOrDefaultAsync(ct) is { } current
                    && await PastBookings.CheckAsync(db, time, current.LocationId, current.Start, ct, PastBookings.RestoreMessage) is { } past)
                {
                    return TypedResults.Conflict(past);
                }

                var before = await notices.RestorationAsync(appointmentId, ct);
                var outcome = await booking.RestoreAsync(appointmentId, request.Start, ct, request.ExpectedVersion);
                if (outcome is BookingOutcome.Success { Appointment: var restored })
                {
                    await notices.AfterRestoreAsync(restored, before.Notice, request.SendCorrection, ct);
                }

                return Respond(outcome);
            })
            .WithName("RestoreAppointment")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Nimmt eine Stornierung zurück: der Termin ist wieder gebucht, wenn Zeit, Person und Raum frei sind (409 mit den Belegungen, sonst) und der Tag nicht vorbei ist "
                + "(409 mit der Meldung, sonst). Mit „start“ zu einem neuen Beginn, heute oder später. "
                + "Eine noch wartende Absage-Nachricht geht nie raus; mit „sendCorrection“ bekommt der Gast, der die Absage schon hat, eine Korrektur.");

        group.MapPost("/appointments/{appointmentId:guid}/platform-accepted", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, BookingService booking, CancellationToken ct) =>
                Respond(await booking.ConfirmPlatformAcceptedAsync(appointmentId, ct)))
            .WithName("ConfirmAppointmentPlatformAccepted")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Die Rezeption hat die Treatwell-Buchung in Treatwell angenommen; der Hinweis „In Treatwell noch annehmen“ am Termin verschwindet.");

        group.MapPost("/appointments/{appointmentId:guid}/status", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, AppointmentStatusChange change, BookingService booking, RoomCareService roomCare, AispaDbContext db, TimeProvider time,
                CancellationToken ct) =>
            {
                if (await StatusDays.CheckAsync(db, time, appointmentId, change.Status, ct) is { } blocked)
                {
                    return Invalid(blocked);
                }

                var outcome = await booking.ChangeStatusAsync(appointmentId, change.Status, ct, change.ExpectedVersion);
                if (outcome is BookingOutcome.Success { Appointment: var appointment } && change.Status == AppointmentStatus.CheckedIn)
                {
                    // Nur Hinweise: eine nicht abgehakte Raumpflege oder ein offener Tagesstart blockiert das Einchecken nicht.
                    var hints = await roomCare.CheckInHintsAsync(appointment, ct);
                    if (hints.Count > 0)
                    {
                        outcome = new BookingOutcome.Success(appointment with { Warnings = hints });
                    }
                }

                return Respond(outcome);
            })
            .WithName("ChangeAppointmentStatus")
            .RequirePermission(AccessRight.ChangeStatus)
            .AllowChannel(AccessChannel.StaffApp)
            .WithSummary("Eingecheckt, abgeschlossen oder nicht erschienen. Einchecken geht nur am Tag des Termins, Erledigt und Nicht erschienen erst ab dem Tag des Termins (sonst 400). "
                + "Beim Einchecken stehen in Warnings Hinweise zu Raumpflege und Tagesstart des Raums; eingecheckt wird trotzdem. "
                + "„Gebucht“ nimmt das Einchecken zurück, solange der Termin eingecheckt und noch nicht kassiert ist (sonst 400).");

        group.MapPost("/appointments/{appointmentId:guid}/begin", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, BookingService booking, CancellationToken ct) => Respond(await booking.BeginAsync(appointmentId, ct)))
            .WithName("BeginAppointment")
            .RequirePermission(AccessRight.ChangeStatus)
            .AllowChannel(AccessChannel.StaffApp)
            .WithSummary("Die Person meldet die Behandlung als begonnen; im Kalender läuft der Termin dann. Nur am Tag des Termins.");

        group.MapPost("/appointments/{appointmentId:guid}/finish", async Task<Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, BookingService booking, CancellationToken ct) => Respond(await booking.FinishAsync(appointmentId, ct)))
            .WithName("FinishAppointment")
            .RequirePermission(AccessRight.ChangeStatus)
            .AllowChannel(AccessChannel.StaffApp)
            .WithSummary("Die Person meldet die Behandlung als beendet; nur nach „begonnen“. Danach ist der Termin abgeschlossen.");

        group.MapPost("/appointments/{appointmentId:guid}/series/preview", async Task<Results<Ok<List<SeriesOccurrence>>, ValidationProblem, ProblemHttpResult>> (
                Guid appointmentId, SeriesInput rule, BookingService booking, CancellationToken ct) =>
                await booking.PreviewSeriesAsync(appointmentId, rule, ct) switch
                {
                    BookingOutcome.SeriesPreviewed p => TypedResults.Ok(p.Occurrences),
                    BookingOutcome.NotFound n => NotFound(n),
                    BookingOutcome.Invalid i => Invalid(i),
                    var other => throw Unexpected(other),
                })
            .WithName("PreviewAppointmentSeries")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Vorschau einer Serie ab diesem Termin: je Termin frei, andere Zeit, andere Person oder Konflikt, mit Grund und Feiertag. Bucht nichts.");

        group.MapPost("/appointments/{appointmentId:guid}/series", async Task<Results<Ok<SeriesResult>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, SeriesCreate input, BookingService booking, CancellationToken ct) =>
                RespondSeries(await booking.CreateSeriesAsync(appointmentId, input, ct)))
            .WithName("CreateAppointmentSeries")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Legt eine Serie ab diesem Termin an und bucht die übernommenen Termine; inzwischen belegte stehen in „failed“.");

        group.MapPost("/appointments/{appointmentId:guid}/series/cancel-following", async Task<Results<Ok<SeriesResult>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid appointmentId, CancelAppointmentRequest request, BookingService booking, CancellationToken ct) =>
                RespondSeries(await booking.CancelSeriesFromAsync(appointmentId, request.Reason, ct)))
            .WithName("CancelAppointmentSeriesFrom")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Storniert diesen und alle folgenden Termine der Serie und beendet sie.");

        group.MapPost("/locations/{locationId:guid}/appointment-groups/availability", async Task<Results<Ok<List<GroupSlot>>, ValidationProblem, ProblemHttpResult>> (
                Guid locationId, GroupAvailabilityRequest request, BookingService booking, CancellationToken ct) =>
                await booking.GroupAvailabilityAsync(locationId, request, ct) switch
                {
                    BookingOutcome.GroupSlots s => TypedResults.Ok(s.Slots),
                    BookingOutcome.NotFound n => NotFound(n),
                    BookingOutcome.Invalid i => Invalid(i),
                    var other => throw Unexpected(other),
                })
            .WithName("FindGroupSlots")
            .WithSummary("Freie Startzeiten für eine Paar- oder Spa-Tag-Buchung; je Zeit der vollständige Plan mit Person und Raum.");

        group.MapPost("/appointment-groups", async Task<Results<Created<GroupBookingResult>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                GroupBookingRequest request, BookingService booking, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PastBookings.CheckAsync(db, time, request.LocationId, request.Start, ct) is { } past)
                {
                    return TypedResults.Conflict(past);
                }

                return await booking.BookGroupAsync(request, ct) switch
                {
                    BookingOutcome.GroupBooked g => TypedResults.Created($"/api/locations/{request.LocationId}/appointment-groups/{g.Result.GroupId}", g.Result),
                    BookingOutcome.NotFound n => NotFound(n),
                    BookingOutcome.Invalid i => Invalid(i),
                    BookingOutcome.Rejected r => TypedResults.Conflict(r.Reason),
                    var other => throw Unexpected(other),
                };
            })
            .WithName("BookAppointmentGroup")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Bucht mehrere Behandlungen gemeinsam (Paar, Spa-Tag): alle oder keine. Paare kommen bevorzugt in einen Mehrplatzraum.");

        group.MapPost("/locations/{locationId:guid}/appointment-groups/{groupId:guid}/cancel", async Task<Results<Ok<GroupBookingResult>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid locationId, Guid groupId, CancelAppointmentRequest request, BookingService booking, CancellationToken ct) =>
                await booking.CancelGroupAsync(locationId, groupId, request.Reason, ct) switch
                {
                    BookingOutcome.GroupBooked g => TypedResults.Ok(g.Result),
                    BookingOutcome.NotFound n => NotFound(n),
                    BookingOutcome.Rejected r => TypedResults.Conflict(r.Reason),
                    var other => throw Unexpected(other),
                })
            .WithName("CancelAppointmentGroup")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Storniert alle offenen Termine einer gemeinsamen Buchung.");

        group.MapPost("/locations/{locationId:guid}/appointment-groups/{groupId:guid}/move", async Task<Results<Ok<GroupBookingResult>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                Guid locationId, Guid groupId, MoveGroupRequest request, BookingService booking, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PastBookings.CheckAsync(db, time, locationId, request.Start, ct, PastBookings.MoveMessage) is { } past)
                {
                    return TypedResults.Conflict(past);
                }

                return await booking.MoveGroupAsync(locationId, groupId, request.Start, ct) switch
                {
                    BookingOutcome.GroupBooked g => TypedResults.Ok(g.Result),
                    BookingOutcome.NotFound n => NotFound(n),
                    BookingOutcome.Invalid i => Invalid(i),
                    BookingOutcome.Rejected r => TypedResults.Conflict(r.Reason),
                    var other => throw Unexpected(other),
                };
            })
            .WithName("MoveAppointmentGroup")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Verschiebt eine ganze gemeinsame Buchung (Paar, Spa-Tag) auf einen neuen Beginn an heute oder später (ein früherer Tag ergibt 409): alle Termine oder keiner.");

        group.MapGet("/locations/{locationId:guid}/areas", async Task<Results<Ok<IReadOnlyList<AreaDay>>, NotFound>> (
                Guid locationId, DateOnly date, AreaVisitService areas, CancellationToken ct) =>
                await areas.DayAsync(locationId, date, ct) is { } day ? TypedResults.Ok(day) : TypedResults.NotFound())
            .WithName("GetAreaDay")
            .WithSummary("Bereiche eines Standorts (Sauna, Ruheraum) an einem Tag: Kapazität, Eintritte (auch stornierte) und Verlauf der Belegung.");

        group.MapPost("/area-visits", async Task<Results<Created<AreaVisitDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>>> (
                AreaVisitRequest request, AreaVisitService areas, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PastBookings.CheckAsync(db, time, request.LocationId, request.From, ct) is { } past)
                {
                    return TypedResults.Conflict(past);
                }

                return await areas.BookAsync(request, ct) switch
                {
                    BookingOutcome.AreaVisitBooked v => TypedResults.Created((string?)null, v.Visit),
                    BookingOutcome.NotFound n => NotFound(n),
                    BookingOutcome.Invalid i => Invalid(i),
                    BookingOutcome.Rejected r => TypedResults.Conflict(r.Reason),
                    var other => throw Unexpected(other),
                };
            })
            .WithName("BookAreaVisit")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Bucht einen Eintritt in einen Bereich; jede Person belegt einen Platz. Voll oder gesperrt: 409 mit Begründung.");

        group.MapPost("/locations/{locationId:guid}/area-visits/{visitId:guid}/cancel", async Task<Results<Ok<AreaVisitDto>, ProblemHttpResult>> (
                Guid locationId, Guid visitId, AreaVisitService areas, CancellationToken ct) =>
                await areas.CancelAsync(locationId, visitId, ct) switch
                {
                    BookingOutcome.AreaVisitBooked v => TypedResults.Ok(v.Visit),
                    BookingOutcome.NotFound n => NotFound(n),
                    var other => throw Unexpected(other),
                })
            .WithName("CancelAreaVisit")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Storniert einen Eintritt und gibt die Plätze frei.");

        group.MapPost("/blocks", async Task<Results<Created<BlockDto>, ValidationProblem, Conflict<BookingRejected>>> (
                BlockInput input, AispaDbContext db, BookingService booking, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Require(nameof(input.Reason), input.Reason);
                errors.Check(input.From < input.Until, nameof(input.Until), "Ende muss nach dem Beginn liegen.");
                var exists = input.Kind switch
                {
                    ResourceKind.Employee => await db.Employees.AnyAsync(e => e.Id == input.ResourceId, ct),
                    ResourceKind.Room => await db.Rooms.AnyAsync(r => r.Id == input.ResourceId, ct),
                    _ => await db.Equipment.AnyAsync(e => e.Id == input.ResourceId, ct),
                };
                errors.Check(exists, nameof(input.ResourceId), "Unbekannte Ressource.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var (block, rejected) = await booking.AddBlockAsync(input, ct);
                return block is null
                    ? TypedResults.Conflict(rejected)
                    : TypedResults.Created($"/api/blocks/{block.Id}", ToDto(block));
            })
            .WithName("CreateBlock")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Sperrzeit für Person, Raum oder Gerät (Urlaub, Schulung, Wartung).");

        group.MapPatch("/blocks/{blockId:guid}", async Task<Results<Ok<BlockDto>, ValidationProblem, NotFound, Conflict<BookingRejected>>> (
                Guid blockId, BlockChange change, BookingService booking, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Check(change.From < change.Until, nameof(change.Until), "Ende muss nach dem Beginn liegen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var (block, rejected) = await booking.ChangeBlockAsync(blockId, change, ct);
                return (block, rejected) switch
                {
                    ({ } changed, _) => TypedResults.Ok(ToDto(changed)),
                    (_, { } conflict) => TypedResults.Conflict(conflict),
                    _ => TypedResults.NotFound(),
                };
            })
            .WithName("ChangeBlock")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Beginn und Ende einer Sperrzeit oder Pause ändern; abgelehnt, wenn die neue Zeit schon belegt ist.");

        group.MapDelete("/blocks/{blockId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid blockId, AispaDbContext db, CancellationToken ct) =>
                await db.ResourceBookings.Where(b => b.Id == blockId && b.AppointmentId == null).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound())
            .WithName("DeleteBlock")
            .RequirePermission(AccessRight.Book);

        group.MapPost("/locations/{locationId:guid}/calendar-notes", async Task<Results<Created<CalendarNoteDto>, ValidationProblem, NotFound>> (
                Guid locationId, CalendarNoteInput input, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Require(nameof(input.Text), input.Text);
                errors.Check(input.Text is null || input.Text.Trim().Length <= 500, nameof(input.Text), "Höchstens 500 Zeichen.");
                errors.Check((input.Kind is null) == (input.ResourceId is null) && (input.ResourceId is null) == (input.At is null),
                    nameof(input.ResourceId), "Spalte und Uhrzeit nur gemeinsam angeben, für eine Tagesnotiz beide weglassen.");
                errors.Check(input.Until is null || input.At is { } at && input.Until > at, nameof(input.Until), NoteEndAfterStart);
                if (input.ResourceId is { } resourceId)
                {
                    var exists = input.Kind switch
                    {
                        ResourceKind.Employee => await db.Employees.AnyAsync(e => e.Id == resourceId, ct),
                        ResourceKind.Room => await db.Rooms.AnyAsync(r => r.Id == resourceId && r.LocationId == locationId, ct),
                        _ => await db.Equipment.AnyAsync(e => e.Id == resourceId && e.LocationId == locationId, ct),
                    };
                    errors.Check(exists, nameof(input.ResourceId), "Unbekannte Spalte.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                var now = time.GetUtcNow();
                var note = new CalendarNote
                {
                    LocationId = locationId,
                    Date = input.Date,
                    Kind = input.Kind,
                    ResourceId = input.ResourceId,
                    At = input.At,
                    Until = input.Until,
                    Text = input.Text!.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.CalendarNotes.Add(note);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/calendar-notes/{note.Id}", ToDto(note));
            })
            .WithName("CreateCalendarNote")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Tagesnotiz für den Standort oder Zettel in einer Spalte zu einer Uhrzeit; belegt nichts.");

        group.MapPatch("/calendar-notes/{noteId:guid}", async Task<Results<Ok<CalendarNoteDto>, ValidationProblem, NotFound>> (
                Guid noteId, CalendarNoteChange change, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var note = await db.CalendarNotes.FirstOrDefaultAsync(n => n.Id == noteId, ct);
                if (note is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(change.Text is null || change.Text.Trim().Length is > 0 and <= 500, nameof(change.Text), "1 bis 500 Zeichen.");
                errors.Check(change.At is null || note.At is not null, nameof(change.At), "Eine Tagesnotiz hat keine Uhrzeit.");
                var at = change.At ?? note.At;
                errors.Check(change.Until is null || at is { } start && change.Until > start, nameof(change.Until), NoteEndAfterStart);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                note.Text = change.Text?.Trim() ?? note.Text;
                note.At = at;
                // Ein bestehendes Ende vor der neuen Uhrzeit fällt weg, statt die Notiz ungültig zu machen.
                note.Until = change.Until ?? (note.Until is { } until && at is { } from && until <= from ? null : note.Until);
                note.Done = change.Done ?? note.Done;
                note.UpdatedAt = time.GetUtcNow();
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(note));
            })
            .WithName("ChangeCalendarNote")
            .RequirePermission(AccessRight.Book)
            .WithSummary("Notiz ändern oder abhaken.");

        group.MapDelete("/calendar-notes/{noteId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid noteId, AispaDbContext db, CancellationToken ct) =>
                await db.CalendarNotes.Where(n => n.Id == noteId).ExecuteDeleteAsync(ct) > 0 ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("DeleteCalendarNote")
            .RequirePermission(AccessRight.Book);

        return routes;
    }

    private const string NoteEndAfterStart = "Das Ende muss nach dem Beginn liegen.";

    private static CalendarNoteDto ToDto(CalendarNote n) => new(n.Id, n.Date, n.Text, n.Kind, n.ResourceId, n.At, n.Done, n.UpdatedAt, n.Until);

    /// <summary>
    /// Wer laut Dienstplan heute nicht da ist (ohne Dienst am Standort, mit Urlaub, Krankenstand, Schulung oder frei), mit dem letzten Tag
    /// in Folge derselben Art. Wer heute an einem anderen Standort arbeitet, ist nicht abwesend.
    /// </summary>
    private static async Task<List<CalendarAbsence>> AbsencesAsync(RosterService roster, Guid locationId, DateOnly date, CancellationToken ct)
    {
        var employees = await roster.EmployeesAt(locationId, date).AsNoTracking().Include(e => e.WorkingHours).ToListAsync(ct);
        var today = await roster.PlansAsync(employees, date, date, ct);
        var absent = employees
            .Select(e => (Employee: e, Plan: today[(e.Id, date)]))
            .Where(x => !x.Plan.Shifts.Any() && x.Plan.Entries.Any(r => r.Kind != RosterKind.Work))
            .ToList();
        if (absent.Count == 0)
        {
            return [];
        }

        const int LookAhead = 60;
        var ahead = await roster.PlansAsync([.. absent.Select(x => x.Employee)], date.AddDays(1), date.AddDays(LookAhead), ct);
        return [.. absent.Select(x =>
        {
            var kind = x.Plan.Entries.First(r => r.Kind != RosterKind.Work).Kind;
            var until = date;
            while (until < date.AddDays(LookAhead) && ahead[(x.Employee.Id, until.AddDays(1))].Entries.Any(r => r.Kind == kind))
            {
                until = until.AddDays(1);
            }

            // Beim Krankenstand bleibt die Notiz weg: sie kann einen Befund nennen, und der gehört nicht in den Kalender.
            var note = kind == RosterKind.Sick || string.IsNullOrWhiteSpace(x.Plan.Note) ? null : x.Plan.Note.Trim();
            return new CalendarAbsence(x.Employee.Id, kind, until, note);
        })];
    }

    /// <summary>
    /// Wer zum Team des Standorts gehört, heute dort keinen Dienst hat, aber an einem anderen Standort arbeitet (für „Alle Mitarbeitenden“ im Kalender).
    /// Die Zeiten rechnet die Zeitzone des anderen Standorts.
    /// </summary>
    private static async Task<List<CalendarElsewhere>> ElsewhereAsync(AispaDbContext db, RosterService roster, Guid locationId, DateOnly date, CancellationToken ct)
    {
        var employees = await roster.EmployeesAt(locationId, date).AsNoTracking().Include(e => e.WorkingHours).ToListAsync(ct);
        var plans = await roster.PlansAsync(employees, date, date, ct);
        var away = new List<(Guid Employee, RosterEntry Shift)>();
        foreach (var employee in employees)
        {
            var plan = plans[(employee.Id, date)];
            if (!plan.Shifts.Any(s => s.LocationId == locationId))
            {
                away.AddRange(plan.Shifts.Where(s => s.LocationId is not null).Select(s => (employee.Id, s)));
            }
        }

        if (away.Count == 0)
        {
            return [];
        }

        var ids = away.Select(a => a.Shift.LocationId!.Value).Distinct().ToList();
        var locations = await db.Locations.AsNoTracking().Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        return [.. away
            .Where(a => locations.ContainsKey(a.Shift.LocationId!.Value))
            .OrderBy(a => a.Shift.Start)
            .Select(a =>
            {
                var other = locations[a.Shift.LocationId!.Value];
                var zone = LocalTime.Zone(other.TimeZone);
                return new CalendarElsewhere(a.Employee, other.Name, LocalTime.ToUtc(date, a.Shift.Start!.Value, zone), LocalTime.ToUtc(date, a.Shift.End!.Value, zone));
            })];
    }

    private static BlockDto ToDto(ResourceBooking b) => new(b.Id, b.Kind, b.ResourceId, b.From, b.Until, b.Reason ?? "");

    private static Results<Ok<AppointmentDto>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>> Respond(BookingOutcome outcome) =>
        outcome switch
        {
            BookingOutcome.Success s => TypedResults.Ok(s.Appointment),
            BookingOutcome.NotFound n => NotFound(n),
            BookingOutcome.Invalid i => Invalid(i),
            BookingOutcome.Rejected r => TypedResults.Conflict(r.Reason),
            var other => throw Unexpected(other),
        };

    private static Results<Ok<SeriesResult>, ValidationProblem, ProblemHttpResult, Conflict<BookingRejected>> RespondSeries(BookingOutcome outcome) =>
        outcome switch
        {
            BookingOutcome.SeriesChanged s => TypedResults.Ok(s.Result),
            BookingOutcome.NotFound n => NotFound(n),
            BookingOutcome.Invalid i => Invalid(i),
            BookingOutcome.Rejected r => TypedResults.Conflict(r.Reason),
            var other => throw Unexpected(other),
        };

    private static async Task<IReadOnlyList<string>?> StockWarningsAsync(StockDemandService stockDemand, AppointmentDto appointment, CancellationToken ct)
    {
        var lines = await stockDemand.BookingCheckAsync(appointment.LocationId, appointment.TreatmentId, appointment.Start, appointment.Id, ct);
        return lines.Count == 0 ? null : [.. lines.Select(l => l.Message!)];
    }

    /// <summary>Der Hinweis zum Schließtag zuerst, dann die Lagerhinweise; ohne beides <c>null</c>.</summary>
    private static IReadOnlyList<string>? Merge(IReadOnlyList<string>? notices, IReadOnlyList<string>? stock) =>
        notices is null && stock is null ? null : [.. notices ?? [], .. stock ?? []];

    private static ProblemHttpResult NotFound(BookingOutcome.NotFound n) =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: $"{n.What} nicht gefunden.");

    private static ValidationProblem Invalid(BookingOutcome.Invalid i) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [i.Field] = [i.Message] });

    private static InvalidOperationException Unexpected(BookingOutcome outcome) => new($"Unerwartetes Ergebnis {outcome}.");

    /// <summary>
    /// Besuche und Nicht-Erscheinen der Gäste des Tages vor diesem Tag, über alle Standorte (gemeinsamer Kundenstamm).
    /// Gerechnet wird im Speicher, weil es je Tag nur wenige Gäste sind.
    /// </summary>
    /// <summary>Vorlieben zur Behandlung in einer Zeile für die Hover-Karte; „nicht behandeln“ steht bewusst mit dabei.</summary>
    private static string? Likes(Reporting.CustomerPreference p)
    {
        var parts = new[]
        {
            p.Pressure is { } pressure ? $"Druck: {pressure}" : null,
            p.Scent is { } scent ? $"Duft: {scent}" : null,
            p.RoomTemperature is { } temperature ? $"Raum: {temperature}" : null,
            p.Music is { } music ? $"Musik: {music}" : null,
            p.PrefersQuiet == true ? "wenig Gespräch" : null,
            p.FocusAreas is { } focus ? $"Fokus: {focus}" : null,
            p.AvoidAreas is { } avoid ? $"nicht behandeln: {avoid}" : null,
        }.OfType<string>().ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static async Task<List<CalendarGuest>> GuestsAsync(AispaDbContext db, List<Appointment> appointments, DateTimeOffset dayStart,
        TimeZoneInfo zone, CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(dayStart, zone).DateTime);
        var customers = appointments.Select(a => a.Customer).DistinctBy(c => c.Id).ToList();
        var ids = customers.Select(c => c.Id).ToList();
        var history = await db.Appointments.AsNoTracking()
            .Where(a => ids.Contains(a.CustomerId) && a.Start < dayStart
                && (a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.NoShow))
            .Select(a => new { a.Id, a.CustomerId, a.GroupId, a.Start, a.Status })
            .ToListAsync(ct);
        var yearAgo = dayStart.AddYears(-1);
        var byCustomer = history.ToLookup(a => a.CustomerId);
        var likes = (await db.CustomerPreferences.AsNoTracking().Where(p => ids.Contains(p.CustomerId)).ToListAsync(ct))
            .ToDictionary(p => p.CustomerId, Likes);
        // Gekaufte Pakete mit offenem Rest (an der Kassa verkauft, siehe Pos/PosService).
        var packages = (await db.Vouchers.AsNoTracking()
                .Where(v => v.CustomerId != null && ids.Contains(v.CustomerId.Value) && v.Units != null && v.Status == VoucherStatus.Active)
                .Join(db.Treatments, v => v.TreatmentId, t => t.Id,
                    (v, t) => new { CustomerId = v.CustomerId!.Value, Package = new GuestPackage(t.Id, t.Name, v.Units!.Value, v.UnitsLeft ?? 0, v.ValidUntil) })
                .ToListAsync(ct))
            .ToLookup(p => p.CustomerId, p => p.Package);
        var flags = (await db.CustomerFlags.AsNoTracking()
                .Where(f => ids.Contains(f.CustomerId) && f.RemovedAt == null && f.Kind != CustomerFlagKind.NotWithMe)
                .Select(f => new { f.CustomerId, f.Kind }).ToListAsync(ct))
            .ToLookup(f => f.CustomerId, f => f.Kind);

        return [.. customers.Select(c =>
        {
            var past = byCustomer[c.Id].ToList();
            var visits = past.Where(a => a.Status == AppointmentStatus.Completed).ToList();
            var last = visits.Count == 0 ? (DateTimeOffset?)null : visits.Max(a => a.Start);
            return new CalendarGuest(
                c.Id,
                visits.Select(a => a.GroupId ?? a.Id).Distinct().Count(),
                visits.Where(a => a.Start >= yearAgo).Select(a => a.GroupId ?? a.Id).Distinct().Count(),
                past.Count(a => a.Status == AppointmentStatus.NoShow),
                last is { } at ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime) : null,
                string.IsNullOrWhiteSpace(c.Note) ? null : c.Note.Trim(),
                CustomerEndpoints.BookingPreferences(c),
                likes.GetValueOrDefault(c.Id),
                packages[c.Id].Any() ? [.. packages[c.Id].OrderBy(p => p.ValidUntil ?? DateOnly.MaxValue)] : null,
                flags[c.Id].Any() ? [.. flags[c.Id].Distinct().Order()] : null,
                c.Language,
                c.Salutation,
                c.BirthDate is { } birth ? Birthdays.Near(birth, day) : null);
        })];
    }
}
