using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Standort schließen und löschen (ADR 0099, Punkt 10b). Ein Standort wird nicht einfach gelöscht, sondern ab einem Tag geschlossen: ab dann gilt er für Buchung, Kalender und
/// Online-Buchung wie eine dauernde Schließzeit (<c>ClosureService.SpansAsync</c>), vergangene Termine, Belege und Berichte bleiben. Gelöscht wird nur ein Standort, an dem nichts hängt.
/// Alles verlangt <see cref="AccessRight.ManageOrganization"/> wie das Anlegen.
/// </summary>
internal static partial class MasterDataEndpoints
{
    private static void MapLocationClosing(RouteGroupBuilder group)
    {
        group.MapPost("/locations/{locationId:guid}/close", async Task<Results<Ok<LocationDto>, ValidationProblem, NotFound>> (
                Guid locationId, LocationCloseInput input, AispaDbContext db, OpeningCache openingCache, TimeProvider time, CancellationToken ct) =>
            {
                var location = await db.Locations.Include(l => l.OpeningHours).FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                var zone = LocalTime.Zone(location.TimeZone);
                var firstClosed = LocalTime.ToUtc(input.ClosedFrom, TimeOnly.MinValue, zone);
                var open = await LocationDeletion.OpenAppointmentsAsync(db, locationId, firstClosed, time.GetUtcNow(), ct);
                errors.Check(open == 0, nameof(input.ClosedFrom), open == 1
                    ? $"Ab dem {input.ClosedFrom:dd.MM.yyyy} liegt noch ein Termin an diesem Standort. Bitte zuerst verschieben oder stornieren."
                    : $"Ab dem {input.ClosedFrom:dd.MM.yyyy} liegen noch {open} Termine an diesem Standort. Bitte zuerst verschieben oder stornieren.");
                errors.Check(await db.Locations.AnyAsync(l => l.Id != locationId && (l.ClosedFrom == null || l.ClosedFrom > input.ClosedFrom), ct),
                    nameof(input.ClosedFrom), "Der letzte offene Standort lässt sich nicht schließen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                location.ClosedFrom = input.ClosedFrom;
                await db.SaveChangesAsync(ct);
                openingCache.Invalidate(locationId);
                return TypedResults.Ok(ToDto(location));
            })
            .WithName("CloseLocation")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Schließt den Standort ab einem Tag: ab dann keine Buchung mehr, vergangene Termine und Belege bleiben. Abgelehnt, solange noch offene Termine ab dem Tag daran liegen oder es der letzte offene Standort wäre.");

        group.MapPost("/locations/{locationId:guid}/reopen", async Task<Results<Ok<LocationDto>, NotFound>> (
                Guid locationId, AispaDbContext db, OpeningCache openingCache, CancellationToken ct) =>
            {
                var location = await db.Locations.Include(l => l.OpeningHours).FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location is null)
                {
                    return TypedResults.NotFound();
                }

                location.ClosedFrom = null;
                await db.SaveChangesAsync(ct);
                openingCache.Invalidate(locationId);
                return TypedResults.Ok(ToDto(location));
            })
            .WithName("ReopenLocation")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Öffnet einen geschlossenen Standort wieder: die Schließung ab dem Tag entfällt.");

        group.MapGet("/locations/{locationId:guid}/deletion-check", async Task<Results<Ok<DeletionCheckDto>, NotFound>> (
                Guid locationId, AispaDbContext db, CancellationToken ct) =>
                await DeletionChecks.LocationAsync(db, locationId, ct) is { } check ? TypedResults.Ok(check) : TypedResults.NotFound())
            .WithName("CheckLocationDeletion")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Zählt, was an einem Standort hängt; gelöscht werden darf nur, wenn nichts daran hängt, sonst bleibt „Schließen ab Datum“.");

        group.MapDelete("/locations/{locationId:guid}", async Task<Results<NoContent, NotFound, Conflict<DeletionCheckDto>>> (
                Guid locationId, AispaDbContext db, OpeningCache openingCache, CancellationToken ct) =>
            {
                if (await DeletionChecks.LocationAsync(db, locationId, ct) is not { } check)
                {
                    return TypedResults.NotFound();
                }

                if (!check.CanDelete)
                {
                    return TypedResults.Conflict(check);
                }

                try
                {
                    // Das Hauptlager entsteht mit dem Standort (ADR 0068) und geht mit ihm; Lagerbewegungen hat die Prüfung ausgeschlossen. Ein SaveChanges ist eine Transaktion.
                    db.StoragePlaces.RemoveRange(await db.StoragePlaces.Where(p => p.LocationId == locationId).ToListAsync(ct));
                    db.Locations.Remove(await db.Locations.FirstAsync(l => l.Id == locationId, ct));
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Eine Verknüpfung, die die Zählung nicht kennt (Kosten, Aktionen, Kassenstand …): lieber nicht löschen als halb.
                    db.ChangeTracker.Clear();
                    return TypedResults.Conflict(LocationDeletion.StillLinked());
                }

                openingCache.Invalidate(locationId);
                return TypedResults.NoContent();
            })
            .WithName("DeleteLocation")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Löscht einen Standort, an dem nichts hängt; sonst 409 mit der Liste dessen, was daran hängt, und dem Weg „Schließen ab Datum“.");
    }
}

/// <summary>Zählungen und Texte rund um das Schließen und Löschen eines Standorts.</summary>
internal static class LocationDeletion
{
    /// <summary>Art der Alternative in <see cref="DeletionAlternative"/>: „Schließen ab Datum …“.</summary>
    internal const string Close = "close";

    /// <summary>Offene Termine (gebucht oder eingecheckt), die ab <paramref name="from"/> noch nicht zu Ende sind.</summary>
    public static Task<int> OpenAppointmentsAsync(AispaDbContext db, Guid locationId, DateTimeOffset from, DateTimeOffset now, CancellationToken ct)
    {
        var edge = from > now ? from : now;
        return db.Appointments.CountAsync(a => a.LocationId == locationId
            && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.CheckedIn) && a.End > edge, ct);
    }

    public static DeletionCheckDto StillLinked() =>
        new(false, "Am Standort hängen noch Daten, die sich nicht zählen lassen (Kosten, Kassa, Aktionen). Er lässt sich nur schließen.", [],
            new DeletionAlternative(Close, "Schließen ab Datum …"));
}
