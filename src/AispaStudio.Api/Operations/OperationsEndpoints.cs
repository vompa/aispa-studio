using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Operations;

/// <summary>Betrieb im Haus: „Zum Termin bereitstellen“ je Behandlung und Tag sowie das Rezeptionshandbuch des Spas.</summary>
internal static class OperationsEndpoints
{
    private const int MaxExtras = 20;

    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Betrieb");

        group.MapGet("/treatments/{treatmentId:guid}/extras", async (Guid treatmentId, AispaDbContext db, CancellationToken ct) =>
                await ExtrasOfAsync(db, treatmentId, ct))
            .WithName("ListTreatmentExtras")
            .WithSummary("„Zum Termin bereitstellen“ einer Behandlung: Getränke, Früchteteller und Extras mit Zeitpunkt und optionalem Lagerartikel.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("/treatments/{treatmentId:guid}/extras", async Task<Results<Ok<List<TreatmentExtraDto>>, NotFound, ValidationProblem>> (
                Guid treatmentId, List<TreatmentExtraInput> input, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Treatments.AnyAsync(t => t.Id == treatmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(input.Count <= MaxExtras, "extras", $"Höchstens {MaxExtras} Einträge.");
                var itemIds = input.Where(x => x.StockItemId != null).Select(x => x.StockItemId!.Value).Distinct().ToList();
                var known = await db.StockItems.Where(i => itemIds.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct);
                for (var i = 0; i < input.Count; i++)
                {
                    var x = input[i];
                    errors.Check(!string.IsNullOrWhiteSpace(x.Name) && x.Name.Trim().Length <= 200, $"[{i}].{nameof(x.Name)}", "Bezeichnung angeben, höchstens 200 Zeichen.");
                    errors.Check(Enum.IsDefined(x.Timing), $"[{i}].{nameof(x.Timing)}", "Zeitpunkt unbekannt.");
                    errors.Check(x.Timing != ExtraTiming.BeforeEnd || x.MinutesBeforeEnd is >= 0 and <= 240, $"[{i}].{nameof(x.MinutesBeforeEnd)}", "Minuten vor Ende von 0 bis 240.");
                    errors.Check(x.StockItemId is null || known.Contains(x.StockItemId.Value), $"[{i}].{nameof(x.StockItemId)}", "Lagerartikel nicht gefunden.");
                    errors.Check(x.StockItemId is null ? x.Quantity == 0 : x.Quantity is > 0 and < 1_000_000 && decimal.Round(x.Quantity, 3) == x.Quantity,
                        $"[{i}].{nameof(x.Quantity)}", x.StockItemId is null ? "Eine Menge gibt es nur mit Lagerartikel." : "Menge je Termin größer als 0, höchstens drei Nachkommastellen.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Gleichbleibende Zeilen behalten ihre Id, damit abgehakte Einträge heutiger Termine abgehakt bleiben.
                var existing = await db.TreatmentExtras.Where(e => e.TreatmentId == treatmentId).ToListAsync(ct);
                var saved = new List<TreatmentExtra>();
                for (var i = 0; i < input.Count; i++)
                {
                    var x = input[i];
                    var name = x.Name.Trim();
                    var minutes = x.Timing == ExtraTiming.BeforeEnd ? x.MinutesBeforeEnd : null;
                    var extra = existing.FirstOrDefault(e => !saved.Contains(e) && e.Name == name && e.Timing == x.Timing && e.MinutesBeforeEnd == minutes
                        && e.StockItemId == x.StockItemId && e.Quantity == x.Quantity);
                    if (extra is null)
                    {
                        db.TreatmentExtras.Add(extra = new TreatmentExtra
                        {
                            TreatmentId = treatmentId,
                            Name = name,
                            Timing = x.Timing,
                            MinutesBeforeEnd = minutes,
                            StockItemId = x.StockItemId,
                            Quantity = x.Quantity,
                        });
                    }

                    extra.Position = i;
                    saved.Add(extra);
                }

                db.TreatmentExtras.RemoveRange(existing.Except(saved));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await ExtrasOfAsync(db, treatmentId, ct));
            })
            .WithName("SetTreatmentExtras")
            .WithSummary("Ersetzt „Zum Termin bereitstellen“ einer Behandlung. Mit Lagerartikel bucht jede abgeschlossene Behandlung die Menge ab (zusammen mit dem Material im Rezept).")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapGet("/locations/{locationId:guid}/servings", async Task<Results<Ok<ServingDayDto>, NotFound>> (
                Guid locationId, DateOnly date, ServingService servings, CancellationToken ct) =>
                await servings.DayAsync(locationId, date, ct) is { } day ? TypedResults.Ok(day) : TypedResults.NotFound())
            .WithName("GetServings")
            .WithSummary("„Bereitstellen“ eines Tages: je Termin die Einträge mit Uhrzeit, Wünsche des Gastes und Stand des Abhakens, dazu die Summen („6 × Früchteteller“).")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("/appointments/{appointmentId:guid}/extras/{extraId:guid}/check", async Task<Results<NoContent, NotFound>> (
                Guid appointmentId, Guid extraId, ServingCheck input, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                var valid = await db.Appointments.AnyAsync(a => a.Id == appointmentId
                    && db.TreatmentExtras.Any(x => x.Id == extraId && x.TreatmentId == a.TreatmentId), ct);
                if (!valid)
                {
                    return TypedResults.NotFound();
                }

                var check = await db.AppointmentExtraChecks.FirstOrDefaultAsync(c => c.AppointmentId == appointmentId && c.ExtraId == extraId, ct);
                if (input.Done && check is null)
                {
                    db.AppointmentExtraChecks.Add(new AppointmentExtraCheck
                    {
                        AppointmentId = appointmentId,
                        ExtraId = extraId,
                        CheckedAt = time.GetUtcNow(),
                        UserId = (await accessService.CurrentAsync(ct))?.UserId,
                    });
                }
                else if (!input.Done && check is not null)
                {
                    db.AppointmentExtraChecks.Remove(check);
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("CheckServing")
            .WithSummary("Hakt ein „Bereitstellen“ eines Termins ab (mit Person und Zeit) oder nimmt den Haken zurück. Gebucht wird dadurch nichts im Lager.")
            .RequirePermission(AccessRight.ChangeStatus);

        group.MapGet("/handbook", async (AispaDbContext db, CancellationToken ct) =>
                (await db.HandbookEntries.AsNoTracking().ToListAsync(ct))
                    .OrderBy(e => e.Area, StringComparer.Ordinal).ThenBy(e => e.Title, StringComparer.Ordinal).Select(Handbook.ToDto).ToList())
            .WithName("ListHandbookEntries")
            .WithSummary("Rezeptionshandbuch des Spas: alle Einträge, auch ausgeschaltete.")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/handbook/reminders", async (DateOnly date, AispaDbContext db, CancellationToken ct) => await Handbook.RemindersAsync(db, date, ct))
            .WithName("ListHandbookReminders")
            .WithSummary("Einträge des Rezeptionshandbuchs, die an diesem Tag am Tagesstart erinnert werden (aktiv, „wichtig“, Wochentag passt).")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPost("/handbook", async Task<Results<Created<HandbookEntryDto>, ValidationProblem>> (
                HandbookEntryInput input, AispaDbContext db, AccessService accessService, TimeProvider time, IMessageBus bus, TenantContext tenants,
                CancellationToken ct) =>
            {
                if (Validate(input) is { } problem)
                {
                    return problem;
                }

                var entry = new HandbookEntry { Title = input.Title.Trim(), Text = input.Text.Trim() };
                Apply(entry, input, (await accessService.CurrentAsync(ct))?.UserId, time.GetUtcNow());
                db.HandbookEntries.Add(entry);
                await db.SaveChangesAsync(ct);
                await PublishRemindersChangedAsync(bus, tenants, db, ct);
                return TypedResults.Created($"/api/handbook/{entry.Id}", Handbook.ToDto(entry));
            })
            .WithName("CreateHandbookEntry")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/handbook/{entryId:guid}", async Task<Results<Ok<HandbookEntryDto>, NotFound, ValidationProblem>> (
                Guid entryId, HandbookEntryInput input, AispaDbContext db, AccessService accessService, TimeProvider time, IMessageBus bus, TenantContext tenants,
                CancellationToken ct) =>
            {
                if (await db.HandbookEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct) is not { } entry)
                {
                    return TypedResults.NotFound();
                }

                if (Validate(input) is { } problem)
                {
                    return problem;
                }

                entry.Title = input.Title.Trim();
                entry.Text = input.Text.Trim();
                Apply(entry, input, (await accessService.CurrentAsync(ct))?.UserId, time.GetUtcNow());
                await db.SaveChangesAsync(ct);
                await PublishRemindersChangedAsync(bus, tenants, db, ct);
                return TypedResults.Ok(Handbook.ToDto(entry));
            })
            .WithName("UpdateHandbookEntry")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("/handbook/{entryId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid entryId, AispaDbContext db, IMessageBus bus, TenantContext tenants, CancellationToken ct) =>
            {
                if (await db.HandbookEntries.Where(e => e.Id == entryId).ExecuteDeleteAsync(ct) == 0)
                {
                    return TypedResults.NotFound();
                }

                await PublishRemindersChangedAsync(bus, tenants, db, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteHandbookEntry")
            .RequirePermission(AccessRight.ManageLocation);

        return routes;
    }

    /// <summary>Das Handbuch gilt für das ganze Spa: alle Kalender an allen Standorten laden die Erinnerungen neu.</summary>
    private static async Task PublishRemindersChangedAsync(IMessageBus bus, TenantContext tenants, AispaDbContext db, CancellationToken ct)
    {
        foreach (var locationId in await db.Locations.AsNoTracking().Select(l => l.Id).ToListAsync(ct))
        {
            await bus.PublishOperationsChangedAsync(tenants, locationId, OperationsChangeKind.Reminders);
        }
    }

    private static Task<List<TreatmentExtraDto>> ExtrasOfAsync(AispaDbContext db, Guid treatmentId, CancellationToken ct) =>
        (from x in db.TreatmentExtras.AsNoTracking()
         where x.TreatmentId == treatmentId
         orderby x.Position
         join i in db.StockItems.AsNoTracking() on x.StockItemId equals i.Id into items
         from item in items.DefaultIfEmpty()
         select new TreatmentExtraDto(x.Id, x.Name, x.Timing, x.MinutesBeforeEnd, x.StockItemId, item.Name, item.Unit, x.Quantity))
        .ToListAsync(ct);

    private static ValidationProblem? Validate(HandbookEntryInput input)
    {
        var errors = new Errors();
        errors.Check(!string.IsNullOrWhiteSpace(input.Title) && input.Title.Trim().Length <= 200, nameof(input.Title), "Titel angeben, höchstens 200 Zeichen.");
        errors.Check(!string.IsNullOrWhiteSpace(input.Text) && input.Text.Trim().Length <= 4000, nameof(input.Text), "Text angeben, höchstens 4000 Zeichen.");
        errors.Check(input.Area is null || input.Area.Trim().Length <= 100, nameof(input.Area), "Bereich mit höchstens 100 Zeichen.");
        errors.Check(input.Days.All(Enum.IsDefined), nameof(input.Days), "Wochentag unbekannt.");
        return errors.Any ? errors.Problem() : null;
    }

    private static void Apply(HandbookEntry entry, HandbookEntryInput input, string? userId, DateTimeOffset now)
    {
        entry.Area = string.IsNullOrWhiteSpace(input.Area) ? null : input.Area.Trim();
        entry.RemindAtDayStart = input.RemindAtDayStart;
        entry.Days = Handbook.MaskOf(input.Days);
        entry.IsActive = input.IsActive;
        entry.UpdatedAt = now;
        entry.UpdatedByUserId = userId;
    }
}
