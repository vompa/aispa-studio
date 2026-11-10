using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Verlauf der Masken neben dem Termin (Slice 91, ADR 0082). Kunden und Behandlungen sieht, wer den Datensatz sehen darf (Kalenderrecht); Dienstplan,
/// Abwesenheiten, Benutzer und Rechte sehen nur Inhaber und Manager. Den Datensatz prüft der Mandantenfilter: ein fremder ist „nicht gefunden“.
/// </summary>
internal static class HistoryEndpoints
{
    internal const string LeadershipOnly = "Den Verlauf von Dienstplan, Benutzern und Rechten sehen nur Inhaber und Manager.";

    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Verlauf").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/customers/{customerId:guid}/history", async Task<IResult> (Guid customerId, AispaDbContext db, EntityHistory history, CancellationToken ct) =>
                await db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, ct)
                    ? TypedResults.Ok(await history.ForCustomerAsync(customerId, ct))
                    : TypedResults.NotFound())
            .WithName("GetCustomerHistory")
            .WithSummary("Wer die Kundendaten wann geändert hat, mit alten und neuen Werten; Gesundheitsdaten nur als „geändert von … am …“, neueste oben.")
            .Produces<List<HistoryEntry>>().ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/treatments/{treatmentId:guid}/history", async Task<IResult> (Guid treatmentId, AispaDbContext db, EntityHistory history, CancellationToken ct) =>
                await db.Treatments.AsNoTracking().AnyAsync(t => t.Id == treatmentId, ct)
                    ? TypedResults.Ok(await history.ForTreatmentAsync(treatmentId, ct))
                    : TypedResults.NotFound())
            .WithName("GetTreatmentHistory")
            .WithSummary("Wer eine Behandlung und ihre Preise wann geändert hat, mit alten und neuen Werten, neueste oben.")
            .Produces<List<HistoryEntry>>().ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/employees/{employeeId:guid}/roster-history", async Task<IResult> (
                Guid employeeId, AispaDbContext db, AccessService access, EntityHistory history, CancellationToken ct) =>
            {
                if (await access.CurrentAsync(ct) is not { IsOwnerLevel: true })
                {
                    return PermissionExtensions.Forbidden(LeadershipOnly);
                }

                return await db.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId, ct)
                    ? TypedResults.Ok(await history.ForRosterAsync(employeeId, ct))
                    : TypedResults.NotFound();
            })
            .WithName("GetRosterHistory")
            .WithSummary("Wer Dienste und Abwesenheiten einer Person wann eingetragen, geändert oder entfernt hat; nur Inhaber und Manager.")
            .Produces<List<HistoryEntry>>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/staff/{userId}/history", async Task<IResult> (string userId, AispaDbContext db, AccessService access, EntityHistory history, CancellationToken ct) =>
            {
                if (await access.CurrentAsync(ct) is not { IsOwnerLevel: true })
                {
                    return PermissionExtensions.Forbidden(LeadershipOnly);
                }

                return await db.StaffAccess.AsNoTracking().AnyAsync(a => a.UserId == userId, ct)
                    ? TypedResults.Ok(await history.ForStaffAsync(userId, ct))
                    : TypedResults.NotFound();
            })
            .WithName("GetStaffHistory")
            .WithSummary("Wer den Zugang, die Rolle und die Standorte eines Benutzers wann geändert hat; nur Inhaber und Manager.")
            .Produces<List<HistoryEntry>>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return routes;
    }
}
