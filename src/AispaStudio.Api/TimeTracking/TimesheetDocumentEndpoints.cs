using System.Globalization;
using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.TimeTracking;

/// <summary>
/// Stundenliste einer Person als PDF (PDF-Ausgaben F, ADR 0091). Recht wie die Zeiten (<c>ManageLocation</c> oder Personalunterlagen); die Standorte der Person prüft der Filter über die Route,
/// eine fremde Person ist „nicht gefunden“. Die Zahlen rechnet <see cref="TimeTrackingEndpoints.RowsAsync"/>, dieselbe Rechnung wie die Zeiten-Maske, mit denselben Standorten, die die
/// angemeldete Person sehen darf. Kein Versand: die Liste wird unterschrieben und abgeheftet.
/// </summary>
internal static class TimesheetDocumentEndpoints
{
    public static IEndpointRouteBuilder MapTimesheetDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/employees/{employeeId:guid}").WithTags("Zeiterfassung").RequirePermission(AccessRight.ManageLocation)
            .MapGet("/timesheet/pdf", async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> (
                Guid employeeId, string? month, ClaimsPrincipal user, AispaDbContext db, RosterService roster, TimeTrackingService times, AccessService accessService,
                DocumentHeaders headers, CancellationToken ct) =>
            {
                if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Bitte den Monat als „2026-06“ angeben."] });
                }

                if (await db.Employees.AsNoTracking().Include(e => e.WorkingHours).FirstOrDefaultAsync(e => e.Id == employeeId, ct) is not { } employee)
                {
                    return TypedResults.NotFound();
                }

                var access = (await accessService.CurrentAsync(ct))!;
                var (_, _, rows) = await TimeTrackingEndpoints.RowsAsync([employee], first, DateTime.DaysInMonth(first.Year, first.Month), access, roster, times, ct);
                var zones = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => LocalTime.Zone(l.TimeZone), ct);
                var data = new TimesheetDocument.Data(employee.InHouseName, first, rows[0], zones);
                return DocumentFiles.Pdf(TimesheetDocument.Build(data, await headers.ForAsync(null, user, ct)), TimesheetDocument.FileName(data));
            })
            .WithName("TimesheetPdf")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageLocation)
            .WithSummary("Stundenliste einer Person für einen Monat (month=2026-06) als PDF: je Tag Kommen, Gehen, Pausen, Ist, Soll und Saldo, Monatssumme und Unterschriftenzeilen.");
        return routes;
    }
}
