using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// „Betrieb einrichten“ (ADR 0099, Entscheidung 6): die Karte oben auf „Firmendaten“ zeigt, welche Schritte der Ersteinrichtung noch fehlen. Die API zählt, die App zeigt nur an
/// und springt je Schritt in die passende Maske. Ein Schritt gilt als erledigt, sobald es mindestens einen passenden Eintrag gibt; es ist keine Pflicht und sperrt nichts.
/// </summary>
internal static partial class MasterDataEndpoints
{
    private static void MapSetupProgress(RouteGroupBuilder group) =>
        group.MapGet("/setup/progress", async (AispaDbContext db, CancellationToken ct) => await SetupProgress.LoadAsync(db, ct))
            .WithName("GetSetupProgress")
            .RequirePermission(AccessRight.ManageOrganization)
            .WithSummary("Die Schritte der Ersteinrichtung (Standort, Öffnungszeiten, Räume, Behandlungen, Team, Kassa) und welche davon erledigt sind.");
}

/// <summary>Die Schritte der Ersteinrichtung in der Reihenfolge, in der man sie sinnvoll abarbeitet.</summary>
internal static class SetupProgress
{
    public static async Task<SetupProgressDto> LoadAsync(AispaDbContext db, CancellationToken ct) => new(
    [
        new("location", "Standort anlegen", "Name, Zeitzone und Anschrift des Spas.", await db.Locations.AnyAsync(ct), "masterData", "locations"),
        new("hours", "Öffnungszeiten eintragen", "Wann Gäste buchen können.", await db.Locations.AnyAsync(l => l.OpeningHours.Any(), ct), "masterData", "locations"),
        new("rooms", "Räume anlegen", "Behandlungsräume und Bereiche wie die Sauna.", await db.Rooms.AnyAsync(r => r.IsActive, ct), "masterData", "locations"),
        new("treatments", "Behandlungen anlegen", "Was das Spa anbietet, mit Dauer und Preis.", await db.Treatments.AnyAsync(t => t.IsActive, ct), "masterData", "treatments"),
        new("people", "Team anlegen", "Wer behandelt und wer an der Rezeption arbeitet.", await db.Employees.AnyAsync(e => e.IsActive, ct), "masterData", "team"),
        new("register", "Kassa einrichten", "Kassa-ID, Belegkopf und Land für Belege.", await db.CashRegisters.AnyAsync(r => r.IsActive, ct), "masterData", "kassa"),
    ]);
}
