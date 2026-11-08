using System.Net.Mail;
using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Payroll;
using AispaStudio.Api.Roster;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Stammdaten für die Planung: Standorte, Räume, Geräte, Qualifikationen, Team und Behandlungen.
/// Löschen heißt deaktivieren: bestehende Termine behalten ihre Bezüge, Neues wird nicht mehr darauf gebucht.
/// Lesen darf jede Rolle; Standortdaten pflegt die Standortleitung, Team und Behandlungen ebenso, Standorte anlegen nur Inhaber.
/// </summary>
internal static partial class MasterDataEndpoints
{
    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Stammdaten").RequirePermission(AccessRight.ViewCalendar);

        MapLocations(group);
        MapRoomsAndEquipment(group);
        MapQualifications(group);
        MapEmployees(group);
        MapTreatments(group);
        return routes;
    }

    private static void MapLocations(RouteGroupBuilder group)
    {
        group.MapGet("/locations", async (AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                return (await db.Locations.AsNoTracking().Include(l => l.OpeningHours).OrderBy(l => l.Name).ToListAsync(ct))
                    .Where(l => access.CanSee(l.Id)).Select(ToDto).ToList();
            })
            .WithName("ListLocations")
            .AllowChannel(AccessChannel.StaffApp)
            .AlsoAllow(AccessRight.ViewFinance, insteadOf: AccessRight.ViewCalendar)
            .WithSummary("Die Standorte, zu denen die angemeldete Person Zugang hat.");

        group.MapPost("/locations", async Task<Results<Created<LocationDto>, ValidationProblem>> (
                LocationInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var location = new Location { Name = "", TimeZone = "", Currency = "" };
                var errors = Apply(location, input);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                db.Locations.Add(location);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/locations/{location.Id}", ToDto(location));
            })
            .WithName("CreateLocation")
            .RequirePermission(AccessRight.ManageOrganization);

        group.MapPut("/locations/{locationId:guid}", async Task<Results<Ok<LocationDto>, ValidationProblem, NotFound>> (
                Guid locationId, LocationInput input, AispaDbContext db, OpeningCache openingCache, CancellationToken ct) =>
            {
                var location = await db.Locations.Include(l => l.OpeningHours).FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = Apply(location, input);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                await db.SaveChangesAsync(ct);
                openingCache.Invalidate(locationId);
                return TypedResults.Ok(ToDto(location));
            })
            .WithName("UpdateLocation")
            .RequirePermission(AccessRight.ManageLocation)
            .WithSummary("Ändert Name, Zeitzone, Währung, Öffnungszeiten, Anschrift und Kontakt. Bestehende Termine bleiben unverändert.");

        group.MapGet("/holidays/regions", () => PublicHolidays.Regions.Select(r => new HolidayRegionDto(r.Code, r.Country, r.Name)).ToList())
            .WithName("ListHolidayRegions")
            .WithSummary("Bundesländer und Kantone, deren Feiertage die Software kennt.");

        group.MapGet("/locations/{locationId:guid}/holidays", async Task<Results<Ok<List<PublicHolidayDto>>, NotFound, ValidationProblem>> (
                Guid locationId, int year, AispaDbContext db, HolidayCalendar holidays, CancellationToken ct) =>
            {
                if (year is < 2000 or > 2100)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["year"] = ["Jahr 2000 bis 2100."] });
                }

                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok((await holidays.ForLocationAsync(locationId, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), ct))
                    .Select(h => new PublicHolidayDto(h.Date, h.Name)).ToList());
            })
            .WithName("ListLocationHolidays")
            .WithSummary("Gesetzliche Feiertage am Standort für ein Jahr: die Regeln nach Bundesland oder Kanton, ergänzt um die Tage aktiver Kalender-Abos.");

        group.MapGet("/locations/{locationId:guid}/resources", async Task<Results<Ok<LocationResources>, NotFound>> (
                Guid locationId, AispaDbContext db, RosterService roster, TimeProvider time, CancellationToken ct) =>
            {
                var location = await db.Locations.AsNoTracking().Include(l => l.OpeningHours)
                    .FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location is null)
                {
                    return TypedResults.NotFound();
                }

                var rooms = await db.Rooms.AsNoTracking().Where(r => r.LocationId == locationId && r.IsActive)
                    .OrderBy(r => r.Name).ToListAsync(ct);
                var equipment = await db.Equipment.AsNoTracking().Where(e => e.LocationId == locationId && e.IsActive)
                    .OrderBy(e => e.Name).ToListAsync(ct);
                // Auch wer nur laut Dienstplan hier aushilft (ab vorletzter Woche), damit der Kalender eine Spalte für sie hat.
                var since = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(-14);
                var here = roster.EmployeesAt(locationId, since).Select(e => e.Id);
                var employees = await EmployeesQuery(db)
                    .Where(e => here.Contains(e.Id))
                    .ToListAsync(ct);
                var treatments = await TreatmentsQuery(db)
                    .Where(t => t.Rooms.Any(r => r.LocationId == locationId && r.IsActive))
                    .ToListAsync(ct);
                var qualifications = await db.Qualifications.AsNoTracking().OrderBy(q => q.Name).ToListAsync(ct);

                return TypedResults.Ok(new LocationResources(
                    ToDto(location),
                    [.. rooms.Select(ToDto)],
                    [.. equipment.Select(ToDto)],
                    [.. employees.Select(ToDto)],
                    [.. treatments.Select(t => ToDto(t, locationId))],
                    [.. qualifications.Select(q => new QualificationDto(q.Id, q.Name))]));
            })
            .WithName("GetLocationResources")
            .WithSummary("Räume, Geräte, Team und Behandlungen eines Standorts – alles, was der Kalender zum Planen braucht.");
    }

    private static void MapRoomsAndEquipment(RouteGroupBuilder group)
    {
        group.MapPost("/locations/{locationId:guid}/rooms", async Task<Results<Created<RoomDto>, ValidationProblem, NotFound>> (
                Guid locationId, RoomInput input, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var room = new Room { LocationId = locationId, Name = "" };
                var errors = Apply(room, input);
                if (room.AdmissionArticleId is { } articleId && !await db.Articles.AnyAsync(a => a.Id == articleId && a.IsActive, ct))
                {
                    errors.Add(nameof(input.AdmissionArticleId), "Artikel für den Eintritt nicht gefunden.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                db.Rooms.Add(room);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/rooms/{room.Id}", ToDto(room));
            })
            .WithName("CreateRoom")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/rooms/{roomId:guid}", async Task<Results<Ok<RoomDto>, ValidationProblem, NotFound>> (
                Guid roomId, RoomInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && r.IsActive, ct);
                if (room is null)
                {
                    return TypedResults.NotFound();
                }

                var wasArea = room.IsArea;
                var errors = Apply(room, input);
                if (room.IsArea != wasArea && (room.IsArea
                        ? await db.Treatments.AnyAsync(t => t.IsActive && t.Rooms.Any(r => r.Id == roomId), ct)
                        : await db.AreaVisits.AnyAsync(v => v.RoomId == roomId && !v.IsCancelled, ct)))
                {
                    errors.Add(nameof(input.IsArea), room.IsArea
                        ? "Der Raum ist noch Behandlungen zugeordnet. Bitte zuerst dort entfernen."
                        : "Für den Bereich gibt es Eintritte. Bitte einen neuen Raum anlegen.");
                }

                if (room.AdmissionArticleId is { } articleId && !await db.Articles.AnyAsync(a => a.Id == articleId && a.IsActive, ct))
                {
                    errors.Add(nameof(input.AdmissionArticleId), "Artikel für den Eintritt nicht gefunden.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(room));
            })
            .WithName("UpdateRoom")
            .RequirePermission(AccessRight.ManageLocation)
            .WithSummary("Ändert Name und Raumpflege; die neue Raumpflege gilt für künftige Buchungen.");

        group.MapDelete("/rooms/{roomId:guid}", (Guid roomId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                DeactivateAsync(db.Rooms, roomId, db, time, ct))
            .WithName("DeactivateRoom")
            .RequirePermission(AccessRight.ManageLocation)
            .WithSummary("Nimmt einen Raum aus der Planung. Abgelehnt, solange noch künftige Termine darin liegen.");

        group.MapPost("/locations/{locationId:guid}/equipment", async Task<Results<Created<EquipmentDto>, ValidationProblem, NotFound>> (
                Guid locationId, EquipmentInput input, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Require(nameof(input.Name), input.Name);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var equipment = new Equipment { LocationId = locationId, Name = input.Name.Trim() };
                db.Equipment.Add(equipment);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/equipment/{equipment.Id}", ToDto(equipment));
            })
            .WithName("CreateEquipment")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/equipment/{equipmentId:guid}", async Task<Results<Ok<EquipmentDto>, ValidationProblem, NotFound>> (
                Guid equipmentId, EquipmentInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var equipment = await db.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId && e.IsActive, ct);
                if (equipment is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Require(nameof(input.Name), input.Name);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                equipment.Name = input.Name.Trim();
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(equipment));
            })
            .WithName("UpdateEquipment")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("/equipment/{equipmentId:guid}", (Guid equipmentId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                DeactivateAsync(db.Equipment, equipmentId, db, time, ct))
            .WithName("DeactivateEquipment")
            .RequirePermission(AccessRight.ManageLocation)
            .WithSummary("Nimmt ein Gerät aus der Planung. Abgelehnt, solange es noch für künftige Termine gebraucht wird.");
    }

    private static void MapQualifications(RouteGroupBuilder group)
    {
        group.MapGet("/qualifications", async (AispaDbContext db, CancellationToken ct) =>
                await db.Qualifications.AsNoTracking().OrderBy(q => q.Name)
                    .Select(q => new QualificationDto(q.Id, q.Name)).ToListAsync(ct))
            .WithName("ListQualifications");

        group.MapPost("/qualifications", async Task<Results<Created<QualificationDto>, ValidationProblem>> (
                QualificationInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var qualification = new Qualification { Name = "" };
                var errors = await ApplyAsync(qualification, input, db, ct);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                db.Qualifications.Add(qualification);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/qualifications/{qualification.Id}", new QualificationDto(qualification.Id, qualification.Name));
            })
            .WithName("CreateQualification")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPut("/qualifications/{qualificationId:guid}", async Task<Results<Ok<QualificationDto>, ValidationProblem, NotFound>> (
                Guid qualificationId, QualificationInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var qualification = await db.Qualifications.FirstOrDefaultAsync(q => q.Id == qualificationId, ct);
                if (qualification is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = await ApplyAsync(qualification, input, db, ct);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new QualificationDto(qualification.Id, qualification.Name));
            })
            .WithName("RenameQualification")
            .RequirePermission(AccessRight.ManageCatalog);
    }

    private static void MapEmployees(RouteGroupBuilder group)
    {
        group.MapGet("/employees", async (AispaDbContext db, CancellationToken ct) =>
                (await EmployeesQuery(db).ToListAsync(ct)).Select(ToDto).ToList())
            .WithName("ListEmployees")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ViewCalendar)
            .WithSummary("Alle aktiven Personen über alle Standorte.");

        group.MapGet("/employees/former", async (AispaDbContext db, CancellationToken ct) =>
                (await db.Employees.AsNoTracking().Include(e => e.Qualifications).Include(e => e.WorkingHours)
                    .AsSplitQuery()
                    .Where(e => !e.IsActive)
                    .OrderBy(e => e.PreferredName ?? e.DisplayName)
                    .ToListAsync(ct)).Select(ToDto).ToList())
            .WithName("ListFormerEmployees")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Alle ausgeschiedenen Personen (aus dem Team genommen), nur für Verwaltung der Stammdaten.");

        group.MapPost("/employees", async Task<Results<Created<EmployeeDto>, ValidationProblem>> (
                EmployeeInput input, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var employee = new Employee { DisplayName = "", Color = "" };
                var errors = await ApplyAsync(employee, input, (await accessService.CurrentAsync(ct))!, db, ct);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                db.Employees.Add(employee);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/employees/{employee.Id}", ToDto(employee));
            })
            .WithName("CreateEmployee")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPut("/employees/{employeeId:guid}", async Task<Results<Ok<EmployeeDto>, ValidationProblem, NotFound>> (
                Guid employeeId, EmployeeInput input, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var employee = await db.Employees.Include(e => e.Qualifications).Include(e => e.WorkingHours)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive, ct);
                if (employee is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = await ApplyAsync(employee, input, (await accessService.CurrentAsync(ct))!, db, ct);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(employee));
            })
            .WithName("UpdateEmployee")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Ändert Name, Farbe, Qualifikationen, Sprachen und Wochenarbeitszeiten. Bestehende Termine bleiben unverändert.");

        group.MapDelete("/employees/{employeeId:guid}", (Guid employeeId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                DeactivateAsync(db.Employees, employeeId, db, time, ct))
            .WithName("DeactivateEmployee")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Nimmt eine Person aus der Planung (z. B. Austritt). Abgelehnt, solange sie noch künftige Termine hat.");
    }

    private static void MapTreatments(RouteGroupBuilder group)
    {
        group.MapGet("/treatments", async (AispaDbContext db, CancellationToken ct) =>
                (await TreatmentsQuery(db).ToListAsync(ct)).Select(t => ToDto(t)).ToList())
            .WithName("ListTreatments")
            .WithSummary("Alle aktiven Behandlungen mit ihren geeigneten Räumen und Geräten an allen Standorten.");

        group.MapPost("/treatments", async Task<Results<Created<TreatmentDto>, ValidationProblem>> (
                TreatmentInput input, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var treatment = new Treatment { Name = "" };
                var errors = await ApplyAsync(treatment, input, (await accessService.CurrentAsync(ct))!, db, ct);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                db.Treatments.Add(treatment);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/treatments/{treatment.Id}", ToDto(treatment));
            })
            .WithName("CreateTreatment")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPut("/treatments/{treatmentId:guid}", async Task<Results<Ok<TreatmentDto>, ValidationProblem, NotFound>> (
                Guid treatmentId, TreatmentInput input, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
            {
                var treatment = await db.Treatments
                    .Include(t => t.RequiredQualifications).Include(t => t.Rooms).Include(t => t.Equipment).Include(t => t.LocationPrices).Include(t => t.PriceRules)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(t => t.Id == treatmentId && t.IsActive, ct);
                if (treatment is null)
                {
                    return TypedResults.NotFound();
                }

                var errors = await ApplyAsync(treatment, input, (await accessService.CurrentAsync(ct))!, db, ct);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(treatment));
            })
            .WithName("UpdateTreatment")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Ändert eine Behandlung für künftige Buchungen; gebuchte Termine behalten Zeiten und Preis.");

        group.MapDelete("/treatments/{treatmentId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid treatmentId, AispaDbContext db, CancellationToken ct) =>
                await db.Treatments.Where(t => t.Id == treatmentId && t.IsActive)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound())
            .WithName("DeactivateTreatment")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Bietet eine Behandlung nicht mehr an. Bereits gebuchte Termine bleiben bestehen.");
    }

    /// <summary>Deaktiviert Person, Raum oder Gerät, sofern keine künftigen Termine mehr daran hängen.</summary>
    private static async Task<Results<NoContent, NotFound, Conflict<BookingRejected>>> DeactivateAsync<T>(
        DbSet<T> set, Guid id, AispaDbContext db, TimeProvider time, CancellationToken ct)
        where T : class
    {
        var entity = await set.FindAsync([id], ct);
        if (entity is null || !db.Entry(entity).Property<bool>("IsActive").CurrentValue)
        {
            return TypedResults.NotFound();
        }

        var now = time.GetUtcNow();
        var upcoming = await db.ResourceBookings.CountAsync(b => b.ResourceId == id && b.AppointmentId != null && b.Until > now, ct);
        if (upcoming > 0)
        {
            return TypedResults.Conflict(new BookingRejected(
                upcoming == 1
                    ? "Daran hängt noch ein künftiger Termin. Bitte zuerst verschieben oder stornieren."
                    : $"Daran hängen noch {upcoming} künftige Termine. Bitte zuerst verschieben oder stornieren.",
                []));
        }

        db.Entry(entity).Property<bool>("IsActive").CurrentValue = false;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static IQueryable<Employee> EmployeesQuery(AispaDbContext db) =>
        db.Employees.AsNoTracking().Include(e => e.Qualifications).Include(e => e.WorkingHours)
            .AsSplitQuery()
            .Where(e => e.IsActive)
            .OrderBy(e => e.PreferredName ?? e.DisplayName);

    private static IQueryable<Treatment> TreatmentsQuery(AispaDbContext db) =>
        db.Treatments.AsNoTracking()
            .Include(t => t.RequiredQualifications).Include(t => t.Rooms).Include(t => t.Equipment).Include(t => t.LocationPrices).Include(t => t.PriceRules)
            .AsSplitQuery()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name);

    private static Errors Apply(Location location, LocationInput input)
    {
        var errors = new Errors();
        errors.Require(nameof(input.Name), input.Name);
        errors.Check(LocalTime.IsKnownZone(input.TimeZone ?? ""), nameof(input.TimeZone), "Unbekannte Zeitzone, z. B. „Europe/Vienna“ verwenden.");
        errors.Check(input.Currency is not null && CurrencyCode().IsMatch(input.Currency), nameof(input.Currency),
            "Währung als ISO-Code, z. B. „EUR“ oder „CHF“.");
        errors.Check(input.Region is null || PublicHolidays.Region(input.Region) is not null, nameof(input.Region),
            "Unbekanntes Bundesland oder unbekannter Kanton, z. B. „AT-9“ oder „DE-BY“.");
        foreach (var o in input.OpeningHours ?? [])
        {
            errors.Check(o.Opens < o.Closes, nameof(input.OpeningHours), $"{o.Day}: Öffnung muss vor Schließung liegen.");
        }

        if (input.Contact is { } contact)
        {
            errors.Check((contact.Street?.Trim().Length ?? 0) <= 200, nameof(input.Contact), "Straße höchstens 200 Zeichen.");
            errors.Check((contact.PostalCode?.Trim().Length ?? 0) <= 10, nameof(input.Contact), "Postleitzahl höchstens 10 Zeichen.");
            errors.Check((contact.City?.Trim().Length ?? 0) <= 100, nameof(input.Contact), "Ort höchstens 100 Zeichen.");
            errors.Check((contact.Phone?.Trim().Length ?? 0) <= 40, nameof(input.Contact), "Telefon höchstens 40 Zeichen.");
            errors.Check((contact.Directions?.Trim().Length ?? 0) <= SchedulingModel.LocationDirectionsLength, nameof(input.Contact),
                $"Anreise höchstens {SchedulingModel.LocationDirectionsLength} Zeichen.");
            errors.Check(string.IsNullOrWhiteSpace(contact.Email) || MailAddress.TryCreate(contact.Email.Trim(), out _) && contact.Email.Trim().Length <= 254,
                nameof(input.Contact), "Keine gültige E-Mail-Adresse.");
        }

        if (input.Company is { } company)
        {
            errors.Check((company.LegalName?.Trim().Length ?? 0) <= 200, nameof(input.Company), "Firmenwortlaut höchstens 200 Zeichen.");
            errors.Check((company.VatId?.Trim().Length ?? 0) <= 20, nameof(input.Company), "UID-Nummer höchstens 20 Zeichen.");
            errors.Check((company.CompanyRegisterNumber?.Trim().Length ?? 0) <= 30, nameof(input.Company), "Firmenbuchnummer höchstens 30 Zeichen.");
            errors.Check((company.CompanyRegisterCourt?.Trim().Length ?? 0) <= 100, nameof(input.Company), "Gericht höchstens 100 Zeichen.");
        }

        if (errors.Any)
        {
            return errors;
        }

        location.Name = input.Name.Trim();
        location.TimeZone = input.TimeZone!;
        location.Currency = input.Currency!;
        location.Region = input.Region;
        location.OpeningHours.Clear();
        location.OpeningHours.AddRange((input.OpeningHours ?? []).Select(o =>
            new LocationOpeningHours { LocationId = location.Id, Day = o.Day, Opens = o.Opens, Closes = o.Closes }));
        if (input.Contact is { } c)
        {
            location.Street = Clean(c.Street);
            location.PostalCode = Clean(c.PostalCode);
            location.City = Clean(c.City);
            location.Phone = Clean(c.Phone);
            location.Email = Clean(c.Email);
            location.Directions = Clean(c.Directions);
        }

        if (input.Company is { } k)
        {
            location.LegalName = Clean(k.LegalName);
            location.VatId = Clean(k.VatId);
            location.CompanyRegisterNumber = Clean(k.CompanyRegisterNumber);
            location.CompanyRegisterCourt = Clean(k.CompanyRegisterCourt);
        }

        return errors;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Errors Apply(Room room, RoomInput input)
    {
        var errors = new Errors();
        errors.Require(nameof(input.Name), input.Name);
        errors.Check(input.CleaningMinutes is >= 0 and <= 240, nameof(input.CleaningMinutes), "Zwischen 0 und 240 Minuten.");
        var isArea = input.IsArea ?? room.IsArea;
        var maxPlaces = isArea ? 200 : 8;
        errors.Check(input.Places is null || (input.Places >= 1 && input.Places <= maxPlaces), nameof(input.Places), $"Zwischen 1 und {maxPlaces} Plätzen.");
        if (!errors.Any)
        {
            room.Name = input.Name.Trim();
            room.CleaningMinutes = input.CleaningMinutes;
            room.IsArea = isArea;
            room.Places = Math.Min(input.Places ?? room.Places, maxPlaces);
            room.AdmissionArticleId = isArea ? input.AdmissionArticleId : null;
        }

        return errors;
    }

    private static async Task<Errors> ApplyAsync(Qualification qualification, QualificationInput input, AispaDbContext db, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Require(nameof(input.Name), input.Name);
        var name = input.Name?.Trim() ?? "";
        errors.Check(!await db.Qualifications.AnyAsync(q => q.Name == name && q.Id != qualification.Id, ct), nameof(input.Name), "Gibt es schon.");
        if (!errors.Any)
        {
            qualification.Name = name;
        }

        return errors;
    }

    /// <summary>
    /// Arbeitszeiten an Standorten ohne Zugang (z. B. die Standortleitung Wien bearbeitet eine Person, die auch in Salzburg arbeitet)
    /// bleiben, wie sie sind; nur die eigenen Standorte werden aus der Eingabe übernommen.
    /// </summary>
    private static async Task<Errors> ApplyAsync(Employee employee, EmployeeInput input, UserAccess access, AispaDbContext db, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Require(nameof(input.DisplayName), input.DisplayName);
        errors.Check((input.PreferredName?.Trim().Length ?? 0) <= EmployeeNames.MaxPreferredNameLength, nameof(input.PreferredName),
            $"Höchstens {EmployeeNames.MaxPreferredNameLength} Zeichen.");
        errors.Check(input.Color is not null && HexColor().IsMatch(input.Color), nameof(input.Color), "Farbe als #rrggbb.");

        var qualificationIds = input.QualificationIds ?? [];
        var qualifications = await db.Qualifications.Where(q => qualificationIds.Contains(q.Id)).ToListAsync(ct);
        errors.Check(qualifications.Count == qualificationIds.Distinct().Count(), nameof(input.QualificationIds), "Unbekannte Qualifikation.");
        errors.Check((input.Languages ?? []).All(CustomerEndpoints.KnownLanguage), nameof(input.Languages), "Unbekannte Sprache.");

        var hours = (input.WorkingHours ?? []).Where(h => access.CanSee(h.LocationId))
            .Concat(employee.WorkingHours.Where(w => !access.CanSee(w.LocationId)).Select(w => new WorkingHours(w.LocationId, w.Day, w.Start, w.End)))
            .ToList();
        var locationIds = hours.Select(h => h.LocationId).Distinct().ToList();
        var knownLocations = await db.Locations.CountAsync(l => locationIds.Contains(l.Id), ct);
        errors.Check(knownLocations == locationIds.Count, nameof(input.WorkingHours), "Unbekannter Standort.");
        foreach (var h in hours)
        {
            errors.Check(h.Start < h.End, nameof(input.WorkingHours), $"{h.Day}: Beginn muss vor Ende liegen.");
        }

        // Niemand kann gleichzeitig an zwei Standorten arbeiten.
        foreach (var day in hours.GroupBy(h => h.Day))
        {
            var sorted = day.OrderBy(h => h.Start).ToList();
            errors.Check(sorted.Zip(sorted.Skip(1)).All(p => p.First.End <= p.Second.Start), nameof(input.WorkingHours),
                $"{day.Key}: Arbeitszeiten überschneiden sich.");
        }

        if (errors.Any)
        {
            return errors;
        }

        employee.DisplayName = input.DisplayName.Trim();
        employee.PreferredName = string.IsNullOrWhiteSpace(input.PreferredName) ? null : input.PreferredName.Trim();
        employee.Color = input.Color!;
        employee.Languages = [.. (input.Languages ?? []).Select(GuestLanguages.Normalize).OfType<string>().Distinct().Order(StringComparer.Ordinal)];
        employee.Gender = input.Gender;
        employee.Qualifications.Clear();
        employee.Qualifications.AddRange(qualifications);
        employee.WorkingHours.Clear();
        employee.WorkingHours.AddRange(hours.Select(h => new EmployeeWorkingHours
        {
            EmployeeId = employee.Id,
            LocationId = h.LocationId,
            Day = h.Day,
            Start = h.Start,
            End = h.End,
        }));
        return errors;
    }

    /// <summary>Wie bei Personen: Räume und Geräte an Standorten ohne Zugang bleiben unverändert.</summary>
    private static async Task<Errors> ApplyAsync(Treatment treatment, TreatmentInput input, UserAccess access, AispaDbContext db, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Require(nameof(input.Name), input.Name);
        errors.Check(input.DurationMinutes is > 0 and <= 600, nameof(input.DurationMinutes), "Zwischen 1 und 600 Minuten.");
        errors.Check(input.PreparationMinutes is >= 0 and <= 120, nameof(input.PreparationMinutes), "Zwischen 0 und 120 Minuten.");
        errors.Check(input.FollowUpMinutes is >= 0 and <= 120, nameof(input.FollowUpMinutes), "Zwischen 0 und 120 Minuten.");
        errors.Check(input.HandsOnMinutes is >= 0 and <= 120, nameof(input.HandsOnMinutes), "Zwischen 0 und 120 Minuten.");
        errors.Check(input.ProcessingMinutes is >= 0 and <= 120, nameof(input.ProcessingMinutes), "Zwischen 0 und 120 Minuten.");
        errors.Check(input.HandsOnMinutes + input.ProcessingMinutes <= input.DurationMinutes, nameof(input.ProcessingMinutes),
            "Behandlungszeit bis zur Einwirkzeit und Einwirkzeit dürfen zusammen die Dauer nicht übersteigen.");
        errors.Check(input.ProcessingMinutes == 0 || input.HandsOnMinutes > 0, nameof(input.HandsOnMinutes),
            "Für eine Einwirkzeit die Minuten am Gast davor angeben.");
        errors.Check(input.Price >= 0, nameof(input.Price), "Darf nicht negativ sein.");
        errors.Check((input.Category?.Trim().Length ?? 0) <= 60, nameof(input.Category), "Höchstens 60 Zeichen.");
        errors.Check(input.NoShowFeePercent is null or (>= 0 and <= 100), nameof(input.NoShowFeePercent), "Ausfallgebühr von 0 bis 100 Prozent.");
        errors.Check(input.DepositPercent is null or (>= 0 and <= 100), nameof(input.DepositPercent), "Anzahlung von 0 bis 100 Prozent.");
        errors.Check((input.GuestHint?.Trim().Length ?? 0) <= 300, nameof(input.GuestHint), "Höchstens 300 Zeichen.");
        var prices = input.LocationPrices ?? [];
        errors.Check(prices.All(p => p.Price >= 0 && decimal.Round(p.Price, 2) == p.Price), nameof(input.LocationPrices),
            "Preise nicht negativ, höchstens zwei Nachkommastellen.");
        errors.Check(prices.Select(p => p.LocationId).Distinct().Count() == prices.Count, nameof(input.LocationPrices), "Je Standort nur ein Preis.");
        var priceLocations = prices.Select(p => p.LocationId).ToList();
        errors.Check(await db.Locations.CountAsync(l => priceLocations.Contains(l.Id), ct) == priceLocations.Count && priceLocations.All(access.CanSee),
            nameof(input.LocationPrices), "Unbekannter Standort.");
        errors.Check(input.RoomIds is { Count: > 0 }, nameof(input.RoomIds), "Mindestens einen geeigneten Raum angeben.");

        var qualificationIds = input.RequiredQualificationIds ?? [];
        var roomIds = input.RoomIds ?? [];
        var equipmentIds = input.EquipmentIds ?? [];
        var qualifications = await db.Qualifications.Where(q => qualificationIds.Contains(q.Id)).ToListAsync(ct);
        var rooms = await db.Rooms.Where(r => roomIds.Contains(r.Id) && r.IsActive).ToListAsync(ct);
        var equipment = await db.Equipment.Where(e => equipmentIds.Contains(e.Id) && e.IsActive).ToListAsync(ct);
        errors.Check(qualifications.Count == qualificationIds.Distinct().Count(), nameof(input.RequiredQualificationIds), "Unbekannte Qualifikation.");
        errors.Check(rooms.Count == roomIds.Distinct().Count(), nameof(input.RoomIds), "Unbekannter Raum.");
        errors.Check(!rooms.Any(r => r.IsArea), nameof(input.RoomIds), "Bereiche wie Sauna oder Ruheraum werden als Eintritt gebucht, nicht als Behandlungsraum.");
        errors.Check(equipment.Count == equipmentIds.Distinct().Count(), nameof(input.EquipmentIds), "Unbekanntes Gerät.");
        if (errors.Any)
        {
            return errors;
        }

        treatment.Name = input.Name.Trim();
        treatment.DurationMinutes = input.DurationMinutes;
        treatment.PreparationMinutes = input.PreparationMinutes;
        treatment.FollowUpMinutes = input.FollowUpMinutes;
        treatment.HandsOnMinutes = input.HandsOnMinutes;
        treatment.ProcessingMinutes = input.ProcessingMinutes;
        treatment.Price = input.Price;
        treatment.Category = Clean(input.Category);
        treatment.NoShowFeePercent = input.NoShowFeePercent;
        treatment.DepositPercent = input.DepositPercent;
        treatment.GuestHint = Clean(input.GuestHint);
        treatment.CheckAnamnesis = input.CheckAnamnesis;
        if (input.LocationPrices is not null)
        {
            // Wie bei Räumen: Preise an Standorten ohne Zugang bleiben unverändert. Gleich dem Grundpreis heißt keine Abweichung.
            var wanted = prices.Where(p => p.Price != input.Price).ToDictionary(p => p.LocationId, p => p.Price);
            treatment.LocationPrices.RemoveAll(p => access.CanSee(p.LocationId) && !wanted.ContainsKey(p.LocationId));
            foreach (var (locationId, price) in wanted)
            {
                if (treatment.LocationPrices.Find(p => p.LocationId == locationId) is { } existing)
                {
                    existing.Price = price;
                }
                else
                {
                    treatment.LocationPrices.Add(new TreatmentLocationPrice { TreatmentId = treatment.Id, LocationId = locationId, Price = price });
                }
            }
        }
        treatment.RequiredQualifications.Clear();
        treatment.RequiredQualifications.AddRange(qualifications);
        var keptRooms = treatment.Rooms.Where(r => !access.CanSee(r.LocationId)).ToList();
        var keptEquipment = treatment.Equipment.Where(e => !access.CanSee(e.LocationId)).ToList();
        treatment.Rooms.Clear();
        treatment.Rooms.AddRange(rooms.Where(r => access.CanSee(r.LocationId)).Concat(keptRooms).DistinctBy(r => r.Id));
        treatment.Equipment.Clear();
        treatment.Equipment.AddRange(equipment.Where(e => access.CanSee(e.LocationId)).Concat(keptEquipment).DistinctBy(e => e.Id));
        return errors;
    }

    public static LocationDto ToDto(Location l) => new(l.Id, l.Name, l.TimeZone, l.Currency,
        [.. l.OpeningHours.OrderBy(o => o.Day).ThenBy(o => o.Opens).Select(o => new OpeningHours(o.Day, o.Opens, o.Closes))], l.Region,
        ContactOf(l), CompanyOf(l));

    public static LocationContact ContactOf(Location l) => new(l.Street, l.PostalCode, l.City, l.Phone, l.Email, l.Directions);

    public static LocationCompany CompanyOf(Location l) => new(l.LegalName, l.VatId, l.CompanyRegisterNumber, l.CompanyRegisterCourt);

    public static RoomDto ToDto(Room r) => new(r.Id, r.LocationId, r.Name, r.CleaningMinutes, r.Places, r.IsArea, r.AdmissionArticleId);

    public static EquipmentDto ToDto(Equipment e) => new(e.Id, e.LocationId, e.Name);

    public static EmployeeDto ToDto(Employee e) => new(e.Id, e.DisplayName, e.Color,
        [.. e.Qualifications.Select(q => q.Id)],
        [.. e.WorkingHours.OrderBy(w => w.Day).ThenBy(w => w.Start).Select(w => new WorkingHours(w.LocationId, w.Day, w.Start, w.End))],
        e.Languages.Count > 0 ? e.Languages : null, e.Gender, e.PreferredName);

    /// <summary>Räume und Geräte nur aktive; mit <paramref name="locationId"/> nur die dieses Standorts.</summary>
    public static TreatmentDto ToDto(Treatment t, Guid? locationId = null) => new(t.Id, t.Name, t.DurationMinutes, t.PreparationMinutes,
        t.FollowUpMinutes, locationId is { } at ? t.PriceAt(at) : t.Price,
        [.. t.RequiredQualifications.Select(q => q.Id)],
        [.. t.Rooms.Where(r => r.IsActive && (locationId == null || r.LocationId == locationId)).Select(r => r.Id)],
        [.. t.Equipment.Where(e => e.IsActive && (locationId == null || e.LocationId == locationId)).Select(e => e.Id)],
        t.Category,
        t.Price,
        [.. t.LocationPrices.OrderBy(p => p.LocationId).Select(p => new TreatmentLocationPriceDto(p.LocationId, p.Price))],
        t.NoShowFeePercent, t.DepositPercent, t.GuestHint, t.CheckAnamnesis, t.HandsOnMinutes, t.ProcessingMinutes);

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyCode();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
