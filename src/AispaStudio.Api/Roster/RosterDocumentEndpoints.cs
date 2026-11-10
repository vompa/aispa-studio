using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Roster;

/// <summary>
/// Dienstplan-Aushang eines Standorts als PDF (PDF-Ausgaben F, ADR 0092). Recht Leitung (<c>ManageLocation</c>); den Standort prüft der Mandantenfilter, ein fremder ist „nicht gefunden“. Der Aushang
/// zeigt nur den veröffentlichten Stand und nur Einträge an Standorten, die die angemeldete Person sehen darf (wie die Wochenansicht). Kein Versand: der Plan hängt aus.
/// </summary>
internal static class RosterDocumentEndpoints
{
    public static IEndpointRouteBuilder MapRosterDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/locations/{locationId:guid}").WithTags("Dienstplan").RequirePermission(AccessRight.ManageLocation)
            .MapGet("/roster/pdf", async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly? week, ClaimsPrincipal user, AispaDbContext db, RosterService roster, AccessService accessService, DocumentHeaders headers, CancellationToken ct) =>
            {
                if (week is not { } day)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["week"] = ["Bitte einen Tag der Woche angeben, z. B. 2027-03-01."] });
                }

                if (await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct) is not { } location)
                {
                    return TypedResults.NotFound();
                }

                var data = await LoadAsync(location.Id, location.Name, RosterDocument.MondayOf(day), db, roster, (await accessService.CurrentAsync(ct))!, ct);
                return DocumentFiles.Pdf(RosterDocument.Build(data, await headers.ForAsync(locationId, user, ct)), RosterDocument.FileName(data));
            })
            .WithName("RosterPdf")
            .WithSummary("Dienstplan-Aushang als PDF (A4 quer) für die Woche, in der week liegt: eine Zeile je Person, eine Spalte je Tag mit Diensten, Abwesenheiten und Notizen; nur der veröffentlichte Stand.");
        return routes;
    }

    private static async Task<RosterDocument.Data> LoadAsync(
        Guid locationId, string locationName, DateOnly from, AispaDbContext db, RosterService roster, UserAccess access, CancellationToken ct)
    {
        var until = from.AddDays(6);
        var employees = await roster.EmployeesAt(locationId, from).AsNoTracking().Include(e => e.WorkingHours).ToListAsync(ct);
        var plans = await roster.PlansAsync(employees, from, until, ct);
        var names = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var rows = employees.OrderBy(e => e.InHouseName, StringComparer.CurrentCultureIgnoreCase)
            .Select(e => new RosterDocument.Row(e.InHouseName,
                [.. Enumerable.Range(0, 7).Select(i => new RosterDocument.Cell(plans[(e.Id, from.AddDays(i))].VisibleTo(access.CanSee, absences: true), names))]))
            .ToList();
        var published = await db.RosterPublications.AsNoTracking().Where(p => p.LocationId == locationId).MaxAsync(p => (DateTimeOffset?)p.PublishedAt, ct);
        return new RosterDocument.Data(locationId, locationName, from, rows, published);
    }
}
