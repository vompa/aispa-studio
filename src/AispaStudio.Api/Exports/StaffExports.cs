using System.Globalization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.TimeTracking;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using static AispaStudio.Api.Exports.ExportRequests;

namespace AispaStudio.Api.Exports;

/// <summary>
/// Listen des Personals als Excel oder CSV (Export/Import, Aufgabe D), nur für die Leitung: Stundenliste je Person und Monat (Soll aus dem Dienstplan, Ist aus der
/// Zeiterfassung), Abwesenheiten eines Zeitraums als zusammenhängende Spannen und der Dienstplan einer Woche mit Personen als Zeilen und Tagen als Spalten.
/// Gerechnet wird wie in „Zeiterfassung“ und „Dienstplan“: Soll und Ist nur an sichtbaren Standorten, der Dienstplan mit Entwürfen.
/// </summary>
internal static class StaffExports
{
    private const string Hours = "hours";
    private const string Absences = "absences";
    private const string Roster = "roster";

    public const string MonthMissing = "Monat fehlt: month als JJJJ-MM angeben.";
    public const string WeekMissing = "Wochenbeginn fehlt: from als JJJJ-MM-TT angeben.";

    private static readonly string[] Lists = [Hours, Absences, Roster];

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public static IEndpointRouteBuilder MapStaffExportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/locations/{locationId:guid}/staff/export", async Task<Results<FileContentHttpResult, ValidationProblem, NotFound, ProblemHttpResult>> (
                Guid locationId, string? list, string? month, DateOnly? from, DateOnly? until, Guid? employeeId, string? format, AispaDbContext db,
                AccessService accessService, RosterService roster, TimeTrackingService times, ExportLog log, CancellationToken ct) =>
            {
                var (request, failure) = await LeadershipListAsync(locationId, list, Lists, format, db, accessService, ct);
                if (request is null)
                {
                    return failure!;
                }

                var access = (await accessService.CurrentAsync(ct))!;
                switch (request.List)
                {
                    case Hours:
                        if (!DateOnly.TryParseExact($"{month?.Trim()}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
                        {
                            return Problem("month", MonthMissing);
                        }

                        var last = first.AddMonths(1).AddDays(-1);
                        var staff = await StampedOrPlannedAsync(db, roster, locationId, first, employeeId, ct);
                        var hours = await HourTableAsync(roster, times, staff, first, last, access, ct);
                        return await FileAsync(hours, request.Format, "Stundenliste", first.ToString("MMMM yyyy", German), "Stundenliste", first, log, ct);
                    case Roster:
                        if (from is not { } monday)
                        {
                            return Problem("from", WeekMissing);
                        }

                        var week = await RosterTableAsync(db, roster, locationId, monday, access, ct);
                        return await FileAsync(week, request.Format, "Dienstplan", $"{Day(monday)} bis {Day(monday.AddDays(6))}", "Dienstplan", monday, log, ct);
                }

                if (RangeError(from, until) is { } rangeError)
                {
                    return Problem("from", rangeError);
                }

                var people = await StampedOrPlannedAsync(db, roster, locationId, from!.Value, employeeId, ct);
                var absences = await AbsenceTableAsync(roster, people, from.Value, until!.Value, ct);
                return await FileAsync(absences, request.Format, "Abwesenheiten", $"{Day(from.Value)} bis {Day(until.Value)}", "Abwesenheiten", from, log, ct);
            })
            .WithTags("Zeiterfassung")
            .RequirePermission(AccessRight.ManageLocation)
            .WithName("ExportStaffList")
            .WithSummary("Listen des Personals als Excel oder CSV, nur für die Leitung: list=hours (Stundenliste je Person und Tag für month=JJJJ-MM), absences "
                + "(Abwesenheiten von from bis until), roster (Dienstplan der Woche ab from, Personen als Zeilen, Tage als Spalten); wahlweise nur employeeId.");

        return routes;
    }

    /// <summary>Wie die Zeiterfassung: alle, die am Standort eingeteilt sind oder dort gestempelt haben; mit <paramref name="employeeId"/> nur diese aktive Person.</summary>
    private static async Task<List<Employee>> StampedOrPlannedAsync(AispaDbContext db, RosterService roster, Guid locationId, DateOnly since, Guid? employeeId,
        CancellationToken ct)
    {
        var stamped = db.TimeRecords.Where(r => r.LocationId == locationId && !r.IsDeleted).Select(r => r.EmployeeId);
        return await db.Employees.AsNoTracking().Include(e => e.WorkingHours)
            .Where(e => employeeId == null ? roster.EmployeesAt(locationId, since).Any(x => x.Id == e.Id) || stamped.Contains(e.Id) : e.IsActive && e.Id == employeeId)
            .OrderBy(e => e.DisplayName)
            .ToListAsync(ct);
    }

    private static async Task<ExportTable> HourTableAsync(RosterService roster, TimeTrackingService times, List<Employee> employees, DateOnly first, DateOnly last,
        UserAccess access, CancellationToken ct)
    {
        var plans = await roster.PlansAsync(employees, first, last, ct);
        var actual = await times.DaysAsync([.. employees.Select(e => e.Id)], first, last, ct);
        var columns = new ExportColumn[]
        {
            new("Person", ColumnKind.Text), new("Tag", ColumnKind.Date), new("Soll (Stunden)", ColumnKind.Number), new("Ist (Stunden)", ColumnKind.Number),
            new("Pause (Minuten)", ColumnKind.Number), new("Differenz (Stunden)", ColumnKind.Number),
        };
        var rows = new List<object?[]>();
        foreach (var employee in employees)
        {
            for (var date = first; date <= last; date = date.AddDays(1))
            {
                var spans = (actual.GetValueOrDefault((employee.Id, date)) ?? []).Where(s => access.CanSee(s.Record.LocationId)).ToList();
                var planned = Math.Round((decimal)plans[(employee.Id, date)].VisibleTo(access.CanSee, absences: false).Hours, 2);
                var worked = Math.Round((decimal)TimeTrackingService.Hours(spans), 2);
                if (planned == 0 && worked == 0)
                {
                    continue;
                }

                rows.Add([employee.DisplayName, date, planned, worked, TimeTrackingService.BreakMinutes(spans), worked - planned]);
            }
        }

        return new ExportTable("Stundenliste", columns, rows);
    }

    private static async Task<ExportTable> AbsenceTableAsync(RosterService roster, List<Employee> employees, DateOnly from, DateOnly until, CancellationToken ct)
    {
        var plans = await roster.PlansAsync(employees, from, until, ct);
        var columns = new ExportColumn[]
        {
            new("Person", ColumnKind.Text), new("Art", ColumnKind.Text), new("Von", ColumnKind.Date), new("Bis", ColumnKind.Date), new("Tage", ColumnKind.Number),
        };
        var rows = new List<object?[]>();
        foreach (var employee in employees)
        {
            // Aufeinanderfolgende Tage derselben Art werden eine Zeile: „Urlaub 2.3. bis 6.3.“.
            (RosterKind Kind, DateOnly From, DateOnly Until)? open = null;
            for (var date = from; date <= until.AddDays(1); date = date.AddDays(1))
            {
                RosterKind? kind = date <= until && plans[(employee.Id, date)].Entries.FirstOrDefault(e => e.Kind != RosterKind.Work) is { } absence ? absence.Kind : null;
                if (open is { } run && (kind != run.Kind || date > until))
                {
                    rows.Add([employee.DisplayName, KindText(run.Kind), run.From, run.Until, run.Until.DayNumber - run.From.DayNumber + 1]);
                    open = null;
                }

                if (kind is { } current)
                {
                    open = open is { } same ? same with { Until = date } : (current, date, date);
                }
            }
        }

        return new ExportTable("Abwesenheiten", columns, rows);
    }

    private static async Task<ExportTable> RosterTableAsync(AispaDbContext db, RosterService roster, Guid locationId, DateOnly monday, UserAccess access, CancellationToken ct)
    {
        var dates = Enumerable.Range(0, 7).Select(monday.AddDays).ToList();
        var employees = await roster.EmployeesAt(locationId, monday, drafts: true).AsNoTracking().Include(e => e.WorkingHours).OrderBy(e => e.DisplayName).ToListAsync(ct);
        var plans = await roster.PlansAsync(employees, monday, dates[^1], ct, drafts: true);
        var places = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var columns = new List<ExportColumn> { new("Person", ColumnKind.Text) };
        columns.AddRange(dates.Select(d => new ExportColumn(d.ToString("ddd d.M.", German), ColumnKind.Text)));
        columns.Add(new ExportColumn("Stunden", ColumnKind.Number));
        var rows = employees.Select(e =>
            {
                var days = dates.Select(d => plans[(e.Id, d)].VisibleTo(access.CanSee, absences: true)).ToList();
                var row = new List<object?> { e.DisplayName };
                row.AddRange(days.Select(d => CellText(d, locationId, places)));
                row.Add(Math.Round((decimal)days.Sum(d => d.Hours), 2));
                return row.ToArray();
            })
            .ToList();
        return new ExportTable("Dienstplan", columns, rows);
    }

    /// <summary>„9:00–17:00“, mit Standort, wenn der Dienst woanders ist, „Urlaub“; Entwürfe mit dem Zusatz „(Entwurf)“; ohne Eintrag leer.</summary>
    private static string? CellText(DayPlan day, Guid locationId, Dictionary<Guid, string> places)
    {
        var parts = day.Entries.Select(e => e.Kind == RosterKind.Work
                ? $"{e.Start!.Value:H:mm}–{e.End!.Value:H:mm}" + (e.LocationId == locationId ? "" : $" {places.GetValueOrDefault(e.LocationId!.Value)}")
                : KindText(e.Kind))
            .ToList();
        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(", ", parts) + (day.Draft ? " (Entwurf)" : "");
    }

    private static string KindText(RosterKind kind) => kind switch
    {
        RosterKind.Vacation => "Urlaub",
        RosterKind.Sick => "Krankenstand",
        RosterKind.DayOff => "Frei",
        RosterKind.Training => "Schulung",
        _ => "Dienst",
    };
}
