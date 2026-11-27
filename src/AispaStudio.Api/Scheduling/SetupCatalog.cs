using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Startkatalog nach Art des Spas (ADR 0104, Entscheidungen 4 und 5): feste Pakete typischer Behandlungen, die der Assistent anbietet.
/// <c>apply</c> legt Qualifikation und Behandlungen über dieselbe Logik an wie <c>POST /api/treatments</c>, ohne Preis, Material, Geräte und Ausfallgebühr.
/// </summary>
internal static partial class MasterDataEndpoints
{
    private static void MapSetupCatalog(RouteGroupBuilder group)
    {
        group.MapGet("/setup/catalog", async (AispaDbContext db, CancellationToken ct) => await SetupCatalog.ListAsync(db, ct))
            .WithName("GetSetupCatalog")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Liefert die Pakete des Startkatalogs nach Art des Spas mit ihren Behandlungen; „exists“ sagt, ob es eine Behandlung mit diesem Namen schon gibt.");

        group.MapPost("/setup/catalog/apply", async Task<Results<Ok<SetupCatalogApplyResult>, ValidationProblem>> (
                SetupCatalogApplyInput input, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var (result, errors) = await ApplyCatalogAsync(input, (await accessService.CurrentAsync(ct))!, db, ct);
                return errors.Any ? errors.Problem() : TypedResults.Ok(result);
            })
            .WithName("ApplySetupCatalog")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Legt je gewähltem Paket die fehlende Qualifikation und die fehlenden Behandlungen an, zugeordnet zu den genannten Räumen, mit Preis 0. Vorhandene Behandlungen gleichen Namens (auch inaktive) werden übersprungen; ein Fehler legt nichts an.");
    }

    private static async Task<(SetupCatalogApplyResult Result, Errors Errors)> ApplyCatalogAsync(
        SetupCatalogApplyInput input, UserAccess access, AispaDbContext db, CancellationToken ct)
    {
        var errors = new Errors();
        var choices = input.Packages ?? [];
        errors.Check(choices.Count > 0, nameof(input.Packages), SetupCatalog.NoPackage);
        var packages = new List<(SetupCatalogPackage Package, List<Guid> RoomIds)>();
        foreach (var choice in choices)
        {
            if (choice is null)
            {
                errors.Add(nameof(input.Packages), SetupCatalog.EmptyChoice);
                continue;
            }

            if (packages.Any(p => p.Package.Key == choice.Key))
            {
                errors.Add(nameof(input.Packages), SetupCatalog.DuplicatePackage);
                continue;
            }

            var package = SetupCatalog.Packages.FirstOrDefault(p => p.Key == choice.Key);
            if (package is null)
            {
                errors.Add(nameof(input.Packages), SetupCatalog.UnknownPackage(choice.Key));
                continue;
            }

            var roomIds = (choice.RoomIds ?? []).Distinct().ToList();
            if (roomIds.Count == 0)
            {
                errors.Add(nameof(input.Packages), SetupCatalog.NoRoom(package.Title));
                continue;
            }

            var locations = await db.Rooms.AsNoTracking().Where(r => roomIds.Contains(r.Id) && r.IsActive && !r.IsArea).Select(r => r.LocationId).ToListAsync(ct);
            if (locations.Count != roomIds.Count || !locations.All(access.CanSee))
            {
                errors.Add(nameof(input.Packages), SetupCatalog.BadRoom(package.Title));
                continue;
            }

            packages.Add((package, roomIds));
        }

        if (errors.Any)
        {
            return (new SetupCatalogApplyResult([]), errors);
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"setup-catalog:" + db.CurrentTenantId}, 0))", ct);
            var names = (await db.Treatments.Select(t => t.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var qualifications = await db.Qualifications.ToListAsync(ct);
            var applied = new List<SetupCatalogApplied>();
            foreach (var (package, roomIds) in packages)
            {
                var qualification = qualifications.Find(q => string.Equals(q.Name, package.Qualification, StringComparison.OrdinalIgnoreCase));
                var created = 0;
                var skipped = 0;
                foreach (var item in package.Treatments)
                {
                    if (names.Contains(item.Name))
                    {
                        skipped++;
                        continue;
                    }

                    if (qualification is null)
                    {
                        qualification = new Qualification { Name = "" };
                        var qualificationErrors = await ApplyAsync(qualification, new QualificationInput(package.Qualification), db, ct);
                        if (qualificationErrors.Any)
                        {
                            return (new SetupCatalogApplyResult([]), qualificationErrors);
                        }

                        db.Qualifications.Add(qualification);
                        qualifications.Add(qualification);
                        await db.SaveChangesAsync(ct);
                    }

                    var treatment = new Treatment { Name = "" };
                    var treatmentErrors = await ApplyAsync(treatment,
                        new TreatmentInput(item.Name, item.DurationMinutes, item.PreparationMinutes, item.FollowUpMinutes, 0m, [qualification.Id], roomIds, [], package.Category),
                        access, db, ct);
                    if (treatmentErrors.Any)
                    {
                        return (new SetupCatalogApplyResult([]), treatmentErrors);
                    }

                    db.Treatments.Add(treatment);
                    names.Add(item.Name);
                    created++;
                }

                applied.Add(new SetupCatalogApplied(package.Key, created, skipped));
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (new SetupCatalogApplyResult(applied), new Errors());
        });
    }
}

internal sealed record SetupCatalogItem(string Name, int DurationMinutes, int PreparationMinutes, int FollowUpMinutes);

/// <summary>Ein Paket des Startkatalogs: <c>Qualification</c> ist der Name der Qualifikation, die alle Behandlungen des Pakets verlangen.</summary>
internal sealed record SetupCatalogPackage(string Key, string Title, string Hint, string Qualification, string Category, IReadOnlyList<SetupCatalogItem> Treatments);

internal static class SetupCatalog
{
    internal const string NoPackage = "Kein Paket gewählt. Mindestens ein Paket wählen.";

    internal const string EmptyChoice = "Eine Auswahl ist leer. Je Paket den Schlüssel und die Räume angeben.";

    internal const string DuplicatePackage = "Jedes Paket nur einmal wählen.";

    internal static string UnknownPackage(string? key) => $"Das Paket „{key}“ gibt es nicht. Ein Paket aus dem Startkatalog wählen.";

    internal static string NoRoom(string title) => $"Für „{title}“ fehlt ein Raum. Mindestens einen Behandlungsraum wählen.";

    internal static string BadRoom(string title) => $"Für „{title}“ passt ein Raum nicht. Nur aktive Behandlungsräume des Spas wählen, keine Bereiche wie die Sauna.";

    internal static readonly IReadOnlyList<SetupCatalogPackage> Packages =
    [
        new("massage", "Massage", "Klassische Massagen vom Rücken bis zum Fuß.", "Massage", "Massagen",
        [
            new("Klassische Massage", 50, 5, 10),
            new("Rückenmassage Express", 25, 5, 5),
            new("Sportmassage", 50, 5, 10),
            new("Fußreflexzonenmassage", 50, 10, 5),
            new("Aromaölmassage (60 Min.)", 60, 5, 10),
        ]),
        new("cosmetics", "Kosmetik", "Gesichtsbehandlungen von kurz bis ausführlich.", "Kosmetik", "Gesicht",
        [
            new("Gesichtsbehandlung Klassik", 60, 5, 5),
            new("Express-Gesichtsbehandlung", 30, 5, 5),
            new("Anti-Aging-Gesichtsbehandlung", 90, 5, 10),
        ]),
        new("hands-feet", "Hand und Fuß", "Maniküre und Pediküre.", "Hand- und Fußpflege", "Hände und Füße",
        [
            new("Maniküre mit Lack", 45, 5, 10),
            new("Maniküre ohne Lack", 30, 5, 5),
            new("Pediküre", 60, 10, 10),
            new("Pediküre mit Lack", 75, 10, 10),
        ]),
        new("hammam-wellness", "Hamam und Wellness", "Körperpeeling, Packung und Hamam-Ritual.", "Körperbehandlung und Hamam", "Körper und Hamam",
        [
            new("Körperpeeling", 30, 10, 10),
            new("Körperpackung", 45, 10, 10),
            new("Hamam-Ritual mit Schaumwaschung", 60, 15, 15),
        ]),
    ];

    public static async Task<List<SetupCatalogPackageDto>> ListAsync(AispaDbContext db, CancellationToken ct)
    {
        var names = (await db.Treatments.AsNoTracking().Select(t => t.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. Packages.Select(p => new SetupCatalogPackageDto(p.Key, p.Title, p.Hint, p.Qualification,
            [.. p.Treatments.Select(t => new SetupCatalogTreatmentDto(t.Name, t.DurationMinutes, t.PreparationMinutes, t.FollowUpMinutes, p.Category, names.Contains(t.Name)))]))];
    }
}
