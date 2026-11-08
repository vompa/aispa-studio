using System.Globalization;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Costs;

/// <summary>Kostensätze pflegen und Vollkosten je Behandlung auswerten. Nur Inhaber und Standortleitung (Stundensätze sind vertraulich).</summary>
internal static class CostEndpoints
{
    public static IEndpointRouteBuilder MapCostEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/costs").WithTags("Kosten").RequirePermission(AccessRight.ViewAmountReports);

        group.MapGet("/locations/{locationId:guid}/settings", async (Guid locationId, AispaDbContext db, CancellationToken ct) =>
                ToDto(await db.LocationCostSettings.AsNoTracking().FirstOrDefaultAsync(s => s.LocationId == locationId, ct)
                    ?? new LocationCostSettings { LocationId = locationId }))
            .WithName("GetLocationCostSettings");

        group.MapPut("/locations/{locationId:guid}/settings", async Task<Results<Ok<LocationCostSettingsDto>, NotFound, ValidationProblem>> (
                Guid locationId, LocationCostSettingsDto input, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                Amount(errors, nameof(input.CleaningHourlyCost), input.CleaningHourlyCost);
                Amount(errors, nameof(input.DefaultStaffHourlyCost), input.DefaultStaffHourlyCost);
                Amount(errors, nameof(input.OtherMonthlyCosts), input.OtherMonthlyCosts);
                errors.Check(input.PlannedUtilizationPercent is >= 1 and <= 100, nameof(input.PlannedUtilizationPercent), "Auslastung 1 bis 100 %.");
                errors.Check(input.StaffProductivityPercent is >= 1 and <= 100, nameof(input.StaffProductivityPercent), "Produktivität 1 bis 100 %.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var settings = await db.LocationCostSettings.FirstOrDefaultAsync(s => s.LocationId == locationId, ct);
                if (settings is null)
                {
                    db.LocationCostSettings.Add(settings = new LocationCostSettings { LocationId = locationId });
                }

                settings.CleaningHourlyCost = input.CleaningHourlyCost;
                settings.DefaultStaffHourlyCost = input.DefaultStaffHourlyCost;
                settings.OtherMonthlyCosts = input.OtherMonthlyCosts;
                settings.PlannedUtilizationPercent = input.PlannedUtilizationPercent;
                settings.StaffProductivityPercent = input.StaffProductivityPercent;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(settings));
            })
            .WithName("SetLocationCostSettings")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapGet("/locations/{locationId:guid}/rooms", async (Guid locationId, AispaDbContext db, CancellationToken ct) =>
                await (from r in db.Rooms.AsNoTracking()
                       where r.LocationId == locationId && r.IsActive
                       join c in db.RoomCosts on r.Id equals c.RoomId into costs
                       from c in costs.DefaultIfEmpty()
                       orderby r.Name
                       select new RoomCostDto(r.Id, r.Name, c == null ? 0 : c.MonthlyCost, c == null ? 0 : c.CleaningMaterialCost))
                    .ToListAsync(ct))
            .WithName("ListRoomCosts");

        group.MapPut("/rooms/{roomId:guid}", async Task<Results<Ok<RoomCostDto>, NotFound, ValidationProblem>> (
                Guid roomId, RoomCostInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
                if (room is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                Amount(errors, nameof(input.MonthlyCost), input.MonthlyCost);
                Amount(errors, nameof(input.CleaningMaterialCost), input.CleaningMaterialCost);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var cost = await db.RoomCosts.FirstOrDefaultAsync(c => c.RoomId == roomId, ct);
                if (cost is null)
                {
                    db.RoomCosts.Add(cost = new RoomCost { RoomId = roomId });
                }

                cost.MonthlyCost = input.MonthlyCost;
                cost.CleaningMaterialCost = input.CleaningMaterialCost;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new RoomCostDto(roomId, room.Name, cost.MonthlyCost, cost.CleaningMaterialCost));
            })
            .WithName("SetRoomCost")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Miet- und Nebenkostenanteil des Raums pro Monat und Pflegematerial je Reinigung.");

        group.MapGet("/locations/{locationId:guid}/employees", async (Guid locationId, AispaDbContext db, CancellationToken ct) =>
                await (from e in db.Employees.AsNoTracking()
                       where e.IsActive && e.WorkingHours.Any(w => w.LocationId == locationId)
                       join c in db.EmployeeCosts on e.Id equals c.EmployeeId into costs
                       from c in costs.DefaultIfEmpty()
                       orderby e.DisplayName
                       select new EmployeeCostDto(e.Id, e.DisplayName, c == null ? null : c.HourlyCost))
                    .ToListAsync(ct))
            .WithName("ListEmployeeCosts");

        group.MapPut("/employees/{employeeId:guid}", async Task<Results<Ok<EmployeeCostDto>, NotFound, ValidationProblem>> (
                Guid employeeId, EmployeeCostInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
                if (employee is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                if (input.HourlyCost is { } value)
                {
                    Amount(errors, nameof(input.HourlyCost), value);
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                var cost = await db.EmployeeCosts.FirstOrDefaultAsync(c => c.EmployeeId == employeeId, ct);
                if (input.HourlyCost is null)
                {
                    if (cost is not null)
                    {
                        db.EmployeeCosts.Remove(cost);
                    }
                }
                else
                {
                    if (cost is null)
                    {
                        db.EmployeeCosts.Add(cost = new EmployeeCost { EmployeeId = employeeId });
                    }

                    cost.HourlyCost = input.HourlyCost.Value;
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new EmployeeCostDto(employeeId, employee.DisplayName, input.HourlyCost));
            })
            .WithName("SetEmployeeCost")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Arbeitgeberkosten pro bezahlter Stunde; leer setzt auf den Standardsatz des Standorts zurück.");

        group.MapGet("/treatments/{treatmentId:guid}/materials", async (Guid treatmentId, AispaDbContext db, CancellationToken ct) =>
                await db.TreatmentMaterials.AsNoTracking().Where(m => m.TreatmentId == treatmentId).OrderBy(m => m.Position)
                    .GroupJoin(db.StockItems, m => m.StockItemId, i => i.Id, (m, items) => new { m, items })
                    .SelectMany(x => x.items.DefaultIfEmpty(), (x, i) => i == null
                        ? new MaterialLine(x.m.Name, x.m.Quantity, x.m.Unit, x.m.UnitCost, null)
                        : new MaterialLine(i.Name, x.m.Quantity, i.Unit, i.UnitCost, i.Id))
                    .ToListAsync(ct))
            .WithName("ListTreatmentMaterials");

        group.MapPut("/treatments/{treatmentId:guid}/materials", async Task<Results<Ok<List<MaterialLine>>, NotFound, ValidationProblem>> (
                Guid treatmentId, List<MaterialLine> input, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Treatments.AnyAsync(t => t.Id == treatmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(input.Count <= 50, "lines", "Höchstens 50 Materialien.");
                var stockIds = input.Where(l => l.StockItemId != null).Select(l => l.StockItemId!.Value).ToList();
                var stock = await db.StockItems.AsNoTracking().Where(s => stockIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
                for (var i = 0; i < input.Count; i++)
                {
                    var line = input[i];
                    if (line.StockItemId is { } stockItemId)
                    {
                        errors.Check(stock.ContainsKey(stockItemId), $"[{i}].{nameof(line.StockItemId)}", "Material im Lager nicht gefunden.");
                        errors.Check(line.Quantity > 0 && decimal.Round(line.Quantity, 3) == line.Quantity, $"[{i}].{nameof(line.Quantity)}",
                            "Menge größer als 0 mit höchstens drei Nachkommastellen.");
                        continue;
                    }

                    errors.Check(!string.IsNullOrWhiteSpace(line.Name) && line.Name.Length <= 200, $"[{i}].{nameof(line.Name)}", "Bezeichnung angeben.");
                    errors.Check(!string.IsNullOrWhiteSpace(line.Unit) && line.Unit.Length <= 20, $"[{i}].{nameof(line.Unit)}", "Einheit angeben, z. B. ml oder Stück.");
                    errors.Check(line.Quantity > 0, $"[{i}].{nameof(line.Quantity)}", "Menge größer als 0.");
                    errors.Check(line.UnitCost >= 0, $"[{i}].{nameof(line.UnitCost)}", "Preis je Einheit nicht negativ.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                await db.TreatmentMaterials.Where(m => m.TreatmentId == treatmentId).ExecuteDeleteAsync(ct);
                // Aus dem Lager: Bezeichnung, Einheit und Preis des Materials (als Stand für die Liste; die Vollkosten nehmen den aktuellen Preis).
                var lines = input.Select(m => m.StockItemId is { } id
                    ? new MaterialLine(stock[id].Name, m.Quantity, stock[id].Unit, stock[id].UnitCost, id)
                    : m with { Name = m.Name.Trim(), Unit = m.Unit.Trim() }).ToList();
                db.TreatmentMaterials.AddRange(lines.Select((m, i) => new TreatmentMaterial
                {
                    TreatmentId = treatmentId,
                    Position = i,
                    Name = m.Name,
                    Quantity = m.Quantity,
                    Unit = m.Unit,
                    UnitCost = m.UnitCost,
                    StockItemId = m.StockItemId,
                }));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(lines);
            })
            .WithName("SetTreatmentMaterials")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Verbrauchsmaterial je Behandlung, frei oder aus dem Lager; ersetzt die bisherige Liste.");

        group.MapGet("/locations/{locationId:guid}/analysis", async Task<Results<Ok<CostAnalysisDto>, NotFound, ValidationProblem>> (
                Guid locationId, string? month, CostAnalysis analysis, TimeProvider clock, CancellationToken ct) =>
            {
                var date = clock.GetUtcNow().UtcDateTime;
                if (month is not null && !DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(month)] = ["Monat im Format JJJJ-MM."] });
                }

                return await analysis.AnalyzeAsync(locationId, date.Year, date.Month, ct) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound();
            })
            .WithName("AnalyzeTreatmentCosts")
            .WithSummary("Vollkosten und Deckungsbeitrag je Behandlung an einem Standort; ohne Monat der laufende Monat.");

        return routes;
    }

    private static void Amount(Errors errors, string field, decimal value) =>
        errors.Check(value >= 0 && value < 10_000_000 && decimal.Round(value, 2) == value, field, "Betrag ab 0 mit höchstens zwei Nachkommastellen.");

    private static LocationCostSettingsDto ToDto(LocationCostSettings s) =>
        new(s.CleaningHourlyCost, s.DefaultStaffHourlyCost, s.OtherMonthlyCosts, s.PlannedUtilizationPercent, s.StaffProductivityPercent);
}
