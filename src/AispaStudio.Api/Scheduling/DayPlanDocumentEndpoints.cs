using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Online;
using AispaStudio.Api.Roster;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Tagesplan eines Standorts als PDF (PDF-Ausgaben E, ADR 0092). Recht wie der Kalender; den Standort prüft der Mandantenfilter, ein fremder ist „nicht gefunden“.
/// Kein Versand: der Plan hängt aus oder liegt am Empfang.
/// </summary>
internal static class DayPlanDocumentEndpoints
{
    public static IEndpointRouteBuilder MapDayPlanDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/locations/{locationId:guid}").WithTags("Kalender").RequirePermission(AccessRight.ViewCalendar)
            .MapGet("/day-plan/pdf", async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly date, string? by, ClaimsPrincipal user, AispaDbContext db, RosterService roster, Waitlist waitlist, DocumentHeaders headers,
                CancellationToken ct) =>
            {
                if (!Enum.TryParse<DayPlanColumns>(by ?? nameof(DayPlanColumns.Team), ignoreCase: true, out var view) || !Enum.IsDefined(view))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["by"] = ["Bitte „team“ oder „rooms“ angeben."] });
                }

                if (await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct) is not { } location)
                {
                    return TypedResults.NotFound();
                }

                var data = await LoadAsync(location, date, view, db, roster, waitlist, ct);
                return DocumentFiles.Pdf(DayPlanDocument.Build(data, await headers.ForAsync(locationId, user, ct)), DayPlanDocument.FileName(data));
            })
            .WithName("DayPlanPdf")
            .WithSummary("Tagesplan eines Standorts als PDF (A4 quer): eine Spalte je Person (by=team, Standard) oder je Raum (by=rooms), Termine mit Zeit, Gast, Behandlung und Status, "
                + "Pausen und Sperrzeiten grau, Fußnote mit der Zahl der Wartenden. Stornierte Termine fehlen.");
        return routes;
    }

    private static async Task<DayPlanDocument.Data> LoadAsync(
        Location location, DateOnly date, DayPlanColumns view, AispaDbContext db, RosterService roster, Waitlist waitlist, CancellationToken ct)
    {
        var zone = LocalTime.Zone(location.TimeZone);
        var (from, until) = LocalTime.Day(date, zone);
        var appointments = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
            .Where(a => a.LocationId == location.Id && a.Start < until && from < a.End && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.Start).ToListAsync(ct);
        var shifts = await roster.ShiftsAsync(location.Id, date, zone, ct);
        var employeeIds = shifts.Select(s => s.EmployeeId).Concat(appointments.Select(a => a.EmployeeId)).Distinct().ToList();
        var employees = (await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToListAsync(ct)).ToDictionary(e => e.Id, e => e.InHouseName);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == location.Id && r.IsActive && !r.IsArea).OrderBy(r => r.Name).ToListAsync(ct);

        var byRooms = view == DayPlanColumns.Rooms;
        List<DayPlanDocument.Column> columns = byRooms
            ? [.. rooms.Select(r => new DayPlanDocument.Column(r.Id, r.Name))]
            : [.. employees.OrderBy(e => e.Value, StringComparer.CurrentCultureIgnoreCase).Select(e => new DayPlanDocument.Column(e.Key, e.Value))];
        var columnIds = columns.Select(c => c.Id).ToList();
        var kind = byRooms ? ResourceKind.Room : ResourceKind.Employee;
        var blocks = await db.ResourceBookings.AsNoTracking()
            .Where(b => b.Kind == kind && columnIds.Contains(b.ResourceId) && b.AppointmentId == null && b.AreaVisitId == null && b.From < until && from < b.Until)
            .OrderBy(b => b.From).ToListAsync(ct);

        var entries = appointments.Select(a => new DayPlanDocument.Entry(
                byRooms ? a.RoomId : a.EmployeeId, a.Start, a.End, false, a.PlaceholderGuest is { } number ? $"Gast {number}" : a.Customer.InHouseName, a.Treatment.Name, a.Status,
                byRooms ? employees.GetValueOrDefault(a.EmployeeId) : null))
            .Concat(blocks.Select(b => new DayPlanDocument.Entry(
                b.ResourceId, b.From < from ? from : b.From, b.Until > until ? until : b.Until, true, "", b.Reason ?? "Gesperrt", AppointmentStatus.Booked, null)))
            .Where(e => columnIds.Contains(e.ColumnId))
            .ToList();
        var waiting = await waitlist.ForLocationAsync(location.Id, ct);
        return new DayPlanDocument.Data(location.Name, date, view, columns, entries, waiting?.Count ?? 0);
    }
}
