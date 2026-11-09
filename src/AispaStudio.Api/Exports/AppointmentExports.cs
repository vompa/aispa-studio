using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Exports;

/// <summary>
/// Termine als Datei (Export/Import, Aufgabe C): die Terminliste eines Standorts als Excel oder CSV, ein Termin als Kalenderdatei für den Gast und der Kalender einer Person
/// (Dienste und Termine ohne Namen der Gäste) für den privaten Kalender. Jede Datei steht im Export-Protokoll.
/// </summary>
internal static class AppointmentExports
{
    /// <summary>Längster Zeitraum einer Datei: ein Jahr.</summary>
    private const int MaxDays = 366;

    private const string OwnCalendarOnly = "Den Kalender einer Person exportieren nur die Leitung und die Person selbst.";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    private static readonly TimeSpan GuestReminder = TimeSpan.FromHours(24);

    public static IEndpointRouteBuilder MapAppointmentExportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Termine").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/locations/{locationId:guid}/appointments/export", async Task<Results<FileContentHttpResult, ValidationProblem, NotFound>> (
                Guid locationId, DateOnly? from, DateOnly? until, Guid? employeeId, Guid? roomId, string? status, string? format,
                AispaDbContext db, AccessService accessService, ExportLog log, CancellationToken ct) =>
            {
                if (ExportFiles.Parse(format) is not { } kind || kind is not (ExportFormat.Xlsx or ExportFormat.Csv))
                {
                    return Problem("format", "Die Terminliste gibt es als Excel (xlsx) oder CSV.");
                }

                if (RangeError(from, until ?? from) is { } rangeError)
                {
                    return Problem("from", rangeError);
                }

                AppointmentStatus? state = null;
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (ParseStatus(status) is not { } parsed)
                    {
                        return Problem("status", "Status unbekannt. Möglich sind booked, checkedin, completed, noshow und cancelled.");
                    }

                    state = parsed;
                }

                if (await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct) is not { } location)
                {
                    return TypedResults.NotFound();
                }

                var zone = ZoneOf(location);
                var first = from!.Value;
                var last = until ?? first;
                var begin = LocalTime.Day(first, zone).From;
                var end = LocalTime.Day(last, zone).Until;
                var query = db.Appointments.AsNoTracking().Where(a => a.LocationId == locationId && a.Start >= begin && a.Start < end);
                if (employeeId is { } person)
                {
                    query = query.Where(a => a.EmployeeId == person);
                }

                if (roomId is { } room)
                {
                    query = query.Where(a => a.RoomId == room);
                }

                if (state is { } s)
                {
                    query = query.Where(a => a.Status == s);
                }

                if (await query.CountAsync(ct) > TableExport.MaxRows)
                {
                    return Problem("from", TableExport.TooLargeMessage);
                }

                var appointments = await query.Include(a => a.Treatment).Include(a => a.Customer).OrderBy(a => a.Start).ToListAsync(ct);
                var employees = (await db.Employees.AsNoTracking().ToListAsync(ct)).ToDictionary(e => e.Id, e => e.InHouseName);
                var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
                var access = (await accessService.CurrentAsync(ct))!;
                var table = ListTable(appointments, employees, rooms, zone, access.Has(AccessRight.ManageLocation));

                var filter = string.Join(", ", new[]
                {
                    $"{Day(first)} bis {Day(last)}",
                    employeeId is { } e && employees.TryGetValue(e, out var name) ? name : null,
                    roomId is { } r && rooms.TryGetValue(r, out var roomName) ? roomName : null,
                    state is { } st ? StatusText(st) : null,
                }.Where(p => p is not null));
                await log.RecordAsync("Terminliste", kind, table.Rows.Count, filter, ct);
                return TypedResults.File(kind == ExportFormat.Xlsx ? TableExport.ToExcel(table) : TableExport.ToCsv(table), ExportFiles.ContentType(kind),
                    ExportFiles.Name("Termine", kind, first));
            })
            .WithName("ExportAppointments")
            .WithSummary("Terminliste eines Standorts als Excel oder CSV für einen Zeitraum (höchstens ein Jahr), wahlweise nur einer Person, eines Raums oder eines Status. "
                + "Die Notiz zum Termin nur für die Leitung.");

        group.MapGet("/appointments/{appointmentId:guid}/ics", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid appointmentId, AispaDbContext db, ExportLog log, TimeProvider time, CancellationToken ct) =>
            {
                if (await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct) is not { } appointment
                    || await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == appointment.LocationId, ct) is not { } location)
                {
                    return TypedResults.NotFound();
                }

                // Eine Buchung mit mehreren Teilen (Spa-Tag): alle Teile desselben Gastes, die nicht storniert sind.
                var groupId = appointment.GroupId;
                var parts = await db.Appointments.AsNoTracking().Include(a => a.Treatment)
                    .Where(a => a.Id == appointmentId
                        || (groupId != null && a.GroupId == groupId && a.CustomerId == appointment.CustomerId && a.Status != AppointmentStatus.Cancelled))
                    .OrderBy(a => a.Start).ToListAsync(ct);
                var zone = ZoneOf(location);
                var events = parts.Select(a => new ICalEvent(a.Id, a.Start, a.End, a.Treatment.Name, location.Name, null, null, GuestReminder)).ToList();
                var localStart = TimeZoneInfo.ConvertTime(parts[0].Start, zone).DateTime;
                var day = DateOnly.FromDateTime(localStart);
                await log.RecordAsync("Termin", ExportFormat.Ics, events.Count, $"{parts[0].Treatment.Name} am {Day(day)}", ct);
                return TypedResults.File(ICalWriter.Write(events, zone, time.GetUtcNow()), ExportFiles.ContentType(ExportFormat.Ics),
                    ExportFiles.Name("Termin", ExportFormat.Ics, day, TimeOnly.FromDateTime(localStart)));
            })
            .WithName("ExportAppointmentIcs")
            .WithSummary("Ein Termin als Kalenderdatei (iCal) für den Gast: Behandlung und Standort, Erinnerung 24 Stunden vorher, ohne Namen der Person; "
                + "bei einer Buchung mit mehreren Teilen alle Teile desselben Gastes.");

        group.MapGet("/employees/{employeeId:guid}/ics", async Task<Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>> (
                Guid employeeId, DateOnly? from, DateOnly? until, string? include, AispaDbContext db, AccessService accessService, RosterService roster,
                ExportLog log, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                if (!access.Has(AccessRight.ManageLocation) && access.EmployeeId != employeeId)
                {
                    return (ProblemHttpResult)PermissionExtensions.Forbidden(OwnCalendarOnly);
                }

                if (RangeError(from, until) is { } rangeError)
                {
                    return Problem("from", rangeError);
                }

                if (ParseInclude(include) is not { } parts)
                {
                    return Problem("include", "Inhalt unbekannt. Möglich sind appointments und shifts.");
                }

                if (await db.Employees.AsNoTracking().Include(e => e.WorkingHours).FirstOrDefaultAsync(e => e.Id == employeeId, ct) is not { } employee)
                {
                    return TypedResults.NotFound();
                }

                var locations = (await db.Locations.AsNoTracking().ToListAsync(ct)).Where(l => access.CanSee(l.Id)).ToDictionary(l => l.Id);
                var zone = await FirstZoneAsync(db, ct);
                var events = new List<ICalEvent>();
                if (parts.Shifts)
                {
                    events.AddRange(await ShiftsAsync(roster, employee, from!.Value, until!.Value, locations, ct));
                }

                if (parts.Appointments)
                {
                    events.AddRange(await WorkAppointmentsAsync(db, employeeId, from!.Value, until!.Value, locations, ct));
                }

                events.Sort((a, b) => a.Start.CompareTo(b.Start));
                await log.RecordAsync("Kalender einer Person", ExportFormat.Ics, events.Count, employee.InHouseName, ct);
                return TypedResults.File(ICalWriter.Write(events, zone, time.GetUtcNow()), ExportFiles.ContentType(ExportFormat.Ics),
                    ExportFiles.Name($"Kalender {employee.InHouseName}", ExportFormat.Ics));
            })
            .WithName("ExportEmployeeIcs")
            .WithSummary("Dienste und Termine einer Person als Kalenderdatei für den privaten Kalender, ohne Namen der Gäste und ohne Erinnerung; nur die Leitung "
                + "oder die Person selbst. include=appointments,shifts wählt den Inhalt (Standard beides), höchstens ein Jahr.");

        return routes;
    }

    /// <summary>Hinweis zum Zeitraum oder null: Beginn und Ende nötig, Ende nicht vor dem Beginn, höchstens <see cref="MaxDays"/> Tage.</summary>
    private static string? RangeError(DateOnly? from, DateOnly? until) => (from, until) switch
    {
        ({ } f, { } u) when u < f => "Das Ende liegt vor dem Beginn.",
        ({ } f, { } u) when u.DayNumber - f.DayNumber >= MaxDays => "Höchstens ein Jahr auf einmal.",
        ({ }, { }) => null,
        _ => "Zeitraum fehlt: from und until angeben (JJJJ-MM-TT).",
    };

    private static AppointmentStatus? ParseStatus(string text) =>
        !int.TryParse(text, out _) && Enum.TryParse<AppointmentStatus>(text.Trim(), ignoreCase: true, out var status) ? status : null;

    /// <summary>„appointments“, „shifts“ oder beides, durch Komma getrennt; ohne Angabe beides; Unbekanntes ergibt null.</summary>
    private static (bool Appointments, bool Shifts)? ParseInclude(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (true, true);
        }

        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => p.ToLowerInvariant()).ToList();
        return parts.All(p => p is "appointments" or "shifts") ? (parts.Contains("appointments"), parts.Contains("shifts")) : null;
    }

    private static ValidationProblem Problem(string field, string message) => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static TimeZoneInfo ZoneOf(Location location) => LocalTime.IsKnownZone(location.TimeZone) ? LocalTime.Zone(location.TimeZone) : TimeZoneInfo.Utc;

    /// <summary>Zeitzone des ersten Standorts für die <c>VTIMEZONE</c> der Datei; die Zeitpunkte selbst stimmen in jeder Zone.</summary>
    private static async Task<TimeZoneInfo> FirstZoneAsync(AispaDbContext db, CancellationToken ct) =>
        await db.Locations.AsNoTracking().OrderBy(l => l.Name).FirstOrDefaultAsync(ct) is { } location ? ZoneOf(location) : TimeZoneInfo.Utc;

    private static string Day(DateOnly date) => date.ToString("d.M.yyyy", German);

    private static ExportTable ListTable(List<Appointment> appointments, Dictionary<Guid, string> employees, Dictionary<Guid, string> rooms, TimeZoneInfo zone, bool leadership)
    {
        var columns = new List<ExportColumn>
        {
            new("Tag", ColumnKind.Date), new("Beginn", ColumnKind.Time), new("Ende", ColumnKind.Time), new("Gast", ColumnKind.Text), new("Behandlung", ColumnKind.Text),
            new("Person", ColumnKind.Text), new("Raum", ColumnKind.Text), new("Status", ColumnKind.Text), new("Preis", ColumnKind.Money), new("Kanal", ColumnKind.Text),
        };
        if (leadership)
        {
            columns.Add(new("Notiz", ColumnKind.Text));
        }

        var rows = appointments.Select(a =>
        {
            var start = TimeZoneInfo.ConvertTime(a.Start, zone).DateTime;
            var end = TimeZoneInfo.ConvertTime(a.End, zone).DateTime;
            var row = new List<object?>
            {
                DateOnly.FromDateTime(start), TimeOnly.FromDateTime(start), TimeOnly.FromDateTime(end),
                $"{a.Customer.FirstName} {a.Customer.LastName}", a.Treatment.Name, employees.GetValueOrDefault(a.EmployeeId), rooms.GetValueOrDefault(a.RoomId),
                StatusText(a.Status), a.Price, SourceText(a.Source),
            };
            if (leadership)
            {
                row.Add(a.Note);
            }

            return row.ToArray();
        }).ToList();
        return new("Termine", columns, rows);
    }

    /// <summary>Geltende Dienste der Person im Zeitraum (Dienstplan, sonst Wochenarbeitszeit) an Standorten, die der Aufrufer sieht: „Dienst Wien“.</summary>
    private static async Task<List<ICalEvent>> ShiftsAsync(RosterService roster, Employee employee, DateOnly from, DateOnly until,
        Dictionary<Guid, Location> locations, CancellationToken ct)
    {
        var plans = await roster.PlansAsync([employee], from, until, ct);
        return plans.Values.SelectMany(plan => plan.Shifts
            .Where(s => s.LocationId is { } l && locations.ContainsKey(l) && s.Start is not null && s.End is not null)
            .Select(s =>
            {
                var location = locations[s.LocationId!.Value];
                var zone = ZoneOf(location);
                return new ICalEvent(ShiftId(employee.Id, plan.Date, s), LocalTime.ToUtc(plan.Date, s.Start!.Value, zone), LocalTime.ToUtc(plan.Date, s.End!.Value, zone),
                    $"Dienst {location.Name}", location.Name, null, null, null);
            })).ToList();
    }

    /// <summary>Termine der Person ohne Stornierte, nach dem Tag am Standort: „Massage (Raum A)“, ohne Gast.</summary>
    private static async Task<List<ICalEvent>> WorkAppointmentsAsync(AispaDbContext db, Guid employeeId, DateOnly from, DateOnly until,
        Dictionary<Guid, Location> locations, CancellationToken ct)
    {
        // Einen Tag Rand nach beiden Seiten, damit jede Zeitzone ihren Tag ganz hat; genau gefiltert wird danach je Standort.
        var begin = LocalTime.Day(from, TimeZoneInfo.Utc).From.AddDays(-1);
        var end = LocalTime.Day(until, TimeZoneInfo.Utc).Until.AddDays(1);
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Treatment)
            .Where(a => a.EmployeeId == employeeId && a.Status != AppointmentStatus.Cancelled && a.Start >= begin && a.Start < end)
            .ToListAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        return appointments
            .Where(a => locations.ContainsKey(a.LocationId) && LocalTime.LocalDate(a.Start, ZoneOf(locations[a.LocationId])) is var day && day >= from && day <= until)
            .Select(a => new ICalEvent(a.Id, a.Start, a.End, $"{a.Treatment.Name} ({rooms.GetValueOrDefault(a.RoomId)})", locations[a.LocationId].Name, null, null, null))
            .ToList();
    }

    /// <summary>Stabile Id eines Dienstes aus Person, Tag, Standort und Zeiten: dieselbe Schicht ersetzt beim erneuten Einlesen den alten Eintrag im Kalender.</summary>
    private static Guid ShiftId(Guid employeeId, DateOnly date, RosterEntry shift)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"shift:{employeeId}:{date:yyyy-MM-dd}:{shift.LocationId}:{shift.Start:HHmm}:{shift.End:HHmm}");
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
    }

    private static string StatusText(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Booked => "Gebucht",
        AppointmentStatus.CheckedIn => "Eingecheckt",
        AppointmentStatus.Completed => "Erledigt",
        AppointmentStatus.NoShow => "Nicht erschienen",
        AppointmentStatus.Cancelled => "Storniert",
        _ => status.ToString(),
    };

    private static string SourceText(AppointmentSource source) => source switch
    {
        AppointmentSource.Online => "Online",
        AppointmentSource.Email => "E-Mail",
        AppointmentSource.Sms => "SMS",
        AppointmentSource.WhatsApp => "WhatsApp",
        AppointmentSource.Phone => "Telefon",
        AppointmentSource.Treatwell => "Treatwell",
        _ => "Rezeption",
    };
}
