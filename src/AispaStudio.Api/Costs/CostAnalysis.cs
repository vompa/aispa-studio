using System.Globalization;
using AispaStudio.Api.Data;
using AispaStudio.Api.Pos;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Costs;

/// <summary>
/// Vollkosten je Behandlung und Termin an einem Standort:
/// Material (erfasste Verbrauchsmengen), Personal (Vor-, Behandlungs- und Nachbereitungszeit zum Stundensatz, geteilt durch die Produktivität),
/// Raum (Mietanteil pro verfügbarer Raumminute bei geplanter Auslastung, einschließlich der Reinigungszeit),
/// Raumpflege (Reinigungszeit zum Stundensatz plus Pflegematerial) und Gemeinkosten (wie die Miete auf Raumminuten verteilt).
/// Verfügbare Raumminuten = Öffnungszeiten des gewählten Monats.
/// </summary>
internal sealed class CostAnalysis(AispaDbContext db)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public async Task<CostAnalysisDto?> AnalyzeAsync(Guid locationId, int year, int month, CancellationToken ct)
    {
        var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours).FirstOrDefaultAsync(l => l.Id == locationId, ct);
        if (location is null)
        {
            return null;
        }

        var settings = await db.LocationCostSettings.AsNoTracking().FirstOrDefaultAsync(s => s.LocationId == locationId, ct)
            ?? new LocationCostSettings { LocationId = locationId };
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive).ToListAsync(ct);
        var roomIds = rooms.Select(r => r.Id).ToList();
        var roomCosts = await db.RoomCosts.AsNoTracking().Where(c => roomIds.Contains(c.RoomId)).ToDictionaryAsync(c => c.RoomId, ct);
        var treatments = await db.Treatments.AsNoTracking().Include(t => t.Rooms).Include(t => t.RequiredQualifications).Include(t => t.LocationPrices)
            .Where(t => t.IsActive && t.Rooms.Any(r => r.LocationId == locationId && r.IsActive))
            .OrderBy(t => t.Name)
            .AsSplitQuery()
            .ToListAsync(ct);
        var employees = await db.Employees.AsNoTracking().Include(e => e.Qualifications)
            .Where(e => e.IsActive && e.WorkingHours.Any(w => w.LocationId == locationId))
            .ToListAsync(ct);
        var employeeIds = employees.Select(e => e.Id).ToList();
        var employeeCosts = await db.EmployeeCosts.AsNoTracking().Where(c => employeeIds.Contains(c.EmployeeId))
            .ToDictionaryAsync(c => c.EmployeeId, c => c.HourlyCost, ct);
        var treatmentIds = treatments.Select(t => t.Id).ToList();
        // Material aus dem Lager mit seinem aktuellen Einstandspreis.
        var stockItems = await db.StockItems.AsNoTracking().ToDictionaryAsync(i => i.Id, ct);
        var materials = (await db.TreatmentMaterials.AsNoTracking().Where(m => treatmentIds.Contains(m.TreatmentId)).ToListAsync(ct))
            .Select(m => m.StockItemId is { } id && stockItems.TryGetValue(id, out var item)
                ? new TreatmentMaterial
                {
                    TreatmentId = m.TreatmentId,
                    Position = m.Position,
                    Name = item.Name,
                    Quantity = m.Quantity,
                    Unit = item.Unit,
                    UnitCost = item.UnitCost,
                    StockItemId = id,
                }
                : m)
            .ToLookup(m => m.TreatmentId);
        var taxCategories = await db.TreatmentPosSettings.AsNoTracking().Where(s => treatmentIds.Contains(s.TreatmentId))
            .ToDictionaryAsync(s => s.TreatmentId, s => s.TaxCategory, ct);
        var country = await db.CashRegisters.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive)
            .OrderBy(r => r.Code).Select(r => (FiscalCountry?)r.Country).FirstOrDefaultAsync(ct) ?? FiscalCountry.AT;

        var zone = LocalTime.Zone(location.TimeZone);
        var first = new DateOnly(year, month, 1);
        var (from, _) = LocalTime.Day(first, zone);
        var (until, _) = LocalTime.Day(first.AddMonths(1), zone);
        var appointments = (await db.Appointments.AsNoTracking()
                .Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until)
                .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
                .Select(a => new { a.TreatmentId, a.Price })
                .ToListAsync(ct))
            .ToLookup(a => a.TreatmentId, a => a.Price);

        // Öffnungsminuten des Monats: je Kalendertag die Öffnungszeiten seines Wochentags.
        decimal capacity = 0;
        for (var day = first; day < first.AddMonths(1); day = day.AddDays(1))
        {
            capacity += location.OpeningHours.Where(h => h.Day == day.DayOfWeek).Sum(h => (decimal)(h.Closes - h.Opens).TotalMinutes);
        }

        var usable = capacity * settings.PlannedUtilizationPercent / 100m;
        var overheadPerMinute = usable > 0 && rooms.Count > 0 ? settings.OtherMonthlyCosts / (usable * rooms.Count) : 0;
        var productivity = Math.Max(settings.StaffProductivityPercent, 1) / 100m;

        var result = new List<TreatmentCostDto>();
        foreach (var t in treatments)
        {
            var missing = new List<string>();
            var suitableRooms = t.Rooms.Where(r => r.LocationId == locationId && r.IsActive).OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
            var boundMinutes = t.PreparationMinutes + t.DurationMinutes + t.FollowUpMinutes;
            // Einwirkzeit: der Raum bleibt belegt, die Person arbeitet in der Zeit an einem anderen Gast.
            var staffMinutes = Math.Max(boundMinutes - t.ProcessingMinutes, 0);
            var cleaningMinutes = suitableRooms.Average(r => (decimal)r.CleaningMinutes);
            var roomMinutes = boundMinutes + cleaningMinutes;

            var materialLines = materials[t.Id].OrderBy(m => m.Position).ToList();
            var material = materialLines.Sum(m => m.Quantity * m.UnitCost);
            if (materialLines.Count == 0)
            {
                missing.Add("Kein Material erfasst.");
            }

            var qualified = employees.Where(e => t.RequiredQualifications.All(q => e.Qualifications.Any(eq => eq.Id == q.Id))).ToList();
            var rates = qualified.Select(e => employeeCosts.TryGetValue(e.Id, out var rate) ? rate : settings.DefaultStaffHourlyCost).ToList();
            var hourly = rates.Count > 0 ? rates.Average() : settings.DefaultStaffHourlyCost;
            if (hourly == 0)
            {
                missing.Add("Kein Stundensatz für das Personal.");
            }

            var staff = hourly / 60m * staffMinutes / productivity;

            var roomRate = usable > 0 ? suitableRooms.Average(r => roomCosts.TryGetValue(r.Id, out var c) ? c.MonthlyCost : 0) / usable : 0;
            if (capacity == 0)
            {
                missing.Add("Keine Öffnungszeiten im Monat.");
            }
            else if (suitableRooms.Any(r => !roomCosts.TryGetValue(r.Id, out var c) || c.MonthlyCost == 0))
            {
                missing.Add("Mietanteil fehlt für: " + string.Join(", ", suitableRooms.Where(r => !roomCosts.TryGetValue(r.Id, out var c) || c.MonthlyCost == 0).Select(r => r.Name)) + ".");
            }

            var room = roomRate * roomMinutes;
            var cleaningMaterial = suitableRooms.Average(r => roomCosts.TryGetValue(r.Id, out var c) ? c.CleaningMaterialCost : 0);
            var cleaning = cleaningMinutes / 60m * settings.CleaningHourlyCost + cleaningMaterial;
            if (cleaningMinutes > 0 && settings.CleaningHourlyCost == 0)
            {
                missing.Add("Kein Stundensatz für die Raumpflege.");
            }

            var overhead = overheadPerMinute * roomMinutes;
            var total = Round(material) + Round(staff) + Round(room) + Round(cleaning) + Round(overhead);
            var percent = TaxRates.TryGet(country, taxCategories.GetValueOrDefault(t.Id, TaxCategory.Normal), out var p) ? p : 0;
            var price = t.PriceAt(locationId);
            var net = TaxRates.Net(price, percent);
            var margin = net - total;
            var booked = appointments[t.Id].ToList();

            result.Add(new(
                t.Id,
                t.Name,
                staffMinutes,
                (int)Math.Round(roomMinutes, MidpointRounding.AwayFromZero),
                price,
                net,
                [
                    new("Material", Round(material), materialLines.Count == 0 ? "nichts erfasst" : string.Join(", ", materialLines.Select(m => $"{m.Quantity.ToString("0.###", German)} {m.Unit} {m.Name}"))),
                    new("Personal", Round(staff), $"{staffMinutes} min zu {Money(hourly)}/h, {settings.StaffProductivityPercent} % produktiv"),
                    new("Raum", Round(room), $"{Format(roomMinutes)} min zu {Money(roomRate)}/min bei {settings.PlannedUtilizationPercent} % Auslastung"),
                    new("Raumpflege", Round(cleaning), $"{Format(cleaningMinutes)} min zu {Money(settings.CleaningHourlyCost)}/h + {Money(cleaningMaterial)} Material"),
                    new("Gemeinkosten", Round(overhead), $"{Format(roomMinutes)} min zu {Money(overheadPerMinute)}/min"),
                ],
                Round(material),
                Round(staff),
                Round(room),
                Round(cleaning),
                Round(overhead),
                total,
                margin,
                net == 0 ? 0 : Math.Round(margin / net * 100, 1),
                booked.Count,
                Round(booked.Sum(price => TaxRates.Net(price, percent) - total)),
                missing));
        }

        return new(locationId, $"{year:0000}-{month:00}", capacity, Math.Round(overheadPerMinute, 4), result);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) => value.ToString(value < 1 && value != 0 ? "0.0000 €" : "0.00 €", German);

    private static string Format(decimal value) => value.ToString("0.#", German);
}
