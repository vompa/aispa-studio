using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Roster;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>
/// Zuständigkeiten und Zertifikate (R05 Slice 117, Ä274, E14): Pflege, Übersichten und Scans nur für Inhaber und Manager (<see cref="AccessRight.ManageHrFile"/>);
/// die Erinnerungen sieht jede Person mit Kalender für sich, auch in der Mitarbeiter-App. Eine ausgeschiedene Person bleibt lesbar und bekommt nichts Neues.
/// </summary>
internal static class StaffDutyEndpoints
{
    public const int MaxScanBytes = 5 * 1024 * 1024;

    private const string FormerPerson = "Die Person ist ausgeschieden; ihre Zuständigkeiten und Zertifikate sind nur noch lesbar.";
    private const string NoKey = "Der Schlüssel für vertrauliche Daten ist nicht eingerichtet (Konfiguration HealthData). Ohne ihn lässt sich kein Scan speichern.";
    private const string NotReadable = "Der Scan lässt sich nicht lesen: Der Schlüssel für vertrauliche Daten fehlt oder passt nicht (Konfiguration HealthData).";
    private const string Tag = "Zuständigkeiten und Zertifikate";

    public static IEndpointRouteBuilder MapStaffDutyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags(Tag).RequirePermission(AccessRight.ManageHrFile);
        MapTypes(group);
        MapDuties(group);
        MapCertificates(group);
        MapScans(group);

        group.MapGet("/staff-duties/overview", async (AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                await OverviewAsync(db, await StaffRegulationEndpoints.TodayAsync(db, time, ct), ct))
            .WithName("ListDutyOverview")
            .WithSummary("„Wer ist wofür zuständig?“: je Eintrag der Liste die Personen mit Vertretung und Gültigkeit, auch ohne Eintrag.");

        group.MapGet("/staff-duties/expiring", async (AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                await ExpiringAsync(db, await StaffRegulationEndpoints.TodayAsync(db, time, ct), ct))
            .WithName("ListExpiringCertificates")
            .WithSummary("„Was läuft bald ab?“: Zertifikate aktiver Personen in der Erinnerungsfrist und abgelaufene, die noch nicht erneuert sind, nach Datum.");

        routes.MapGet("/api/staff-duties/reminders", async (
                Guid? locationId, ClaimsPrincipal user, AispaDbContext db, RosterService roster, StockService stock, AccessService access, TimeProvider time, CancellationToken ct) =>
                await DutyReminders.ForAsync(db, roster, stock, (await access.CurrentAsync(ct))!, Channels.Of(user) == AccessChannel.StaffApp, locationId, await StaffRegulationEndpoints.TodayAsync(db, time, ct), ct))
            .WithTags(Tag)
            .WithName("ListDutyReminders")
            .WithSummary("Erinnerungen der angemeldeten Person: eigene Zertifikate vor dem Ablauf, bei Inhaber und Manager die aller aktiven Personen; mit locationId die Lageraufgaben, wenn sie an diese Person gehen. In der Mitarbeiter-App nur die eigenen Zertifikate.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        return routes;
    }

    private static void MapTypes(RouteGroupBuilder group)
    {
        group.MapGet("/staff-duties/types", async (AispaDbContext db, CancellationToken ct) =>
            {
                await StaffDutyModel.EnsureDefaultsAsync(db, ct);
                return (await db.Set<DutyType>().AsNoTracking().ToListAsync(ct)).InListOrder().Select(ToDto).ToList();
            })
            .WithName("ListDutyTypes")
            .WithSummary("Liste der Zuständigkeiten, auch ausgeblendete; beim ersten Aufruf mit den sieben Vorgaben.");

        group.MapPost("/staff-duties/types", async Task<IResult> (DutyTypeInput input, AispaDbContext db, CancellationToken ct) =>
            {
                if (Validate(input) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                var type = new DutyType();
                Apply(type, input);
                db.Add(type);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(type));
            })
            .WithName("CreateDutyType")
            .WithSummary("Legt einen eigenen Eintrag in der Liste der Zuständigkeiten an.")
            .Produces<DutyTypeDto>().ProducesValidationProblem();

        group.MapPut("/staff-duties/types/{typeId:guid}", async Task<IResult> (Guid typeId, DutyTypeInput input, AispaDbContext db, CancellationToken ct) =>
            {
                if (Validate(input) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                if (await db.Set<DutyType>().FirstOrDefaultAsync(t => t.Id == typeId, ct) is not { } type)
                {
                    return TypedResults.NotFound();
                }

                Apply(type, input);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(type));
            })
            .WithName("UpdateDutyType")
            .WithSummary("Benennt einen Eintrag um, blendet ihn aus oder ändert das verlangte Zertifikat; die Mindestzahl gilt nur beim Ersthelfer. Vorgaben lassen sich nicht löschen.")
            .Produces<DutyTypeDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapDuties(RouteGroupBuilder group)
    {
        const string Route = "/employees/{employeeId:guid}/duties";

        group.MapGet(Route, async Task<IResult> (Guid employeeId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId, ct))
                {
                    return TypedResults.NotFound();
                }

                var today = await StaffRegulationEndpoints.TodayAsync(db, time, ct);
                var duties = await DutiesQuery(db, d => d.EmployeeId == employeeId).ToListAsync(ct);
                return TypedResults.Ok(duties.OrderBy(d => d.Duty.ValidFrom).ThenBy(d => d.Duty.Id).Select(d => ToDto(d, today)).ToList());
            })
            .WithName("ListStaffDuties")
            .WithSummary("Zuständigkeiten einer Person, auch abgelaufene.")
            .Produces<List<StaffDutyDto>>().ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost(Route, async Task<IResult> (Guid employeeId, StaffDutyInput input, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                if (await ValidateAsync(db, employeeId, input, null, ct) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                var duty = new StaffDuty { EmployeeId = employeeId };
                Apply(duty, input);
                db.Add(duty);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await DutyDtoAsync(db, duty.Id, time, ct));
            })
            .WithName("CreateStaffDuty")
            .WithSummary("Legt eine Zuständigkeit an; sie gilt für alle Standorte.")
            .Produces<StaffDutyDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut($"{Route}/{{dutyId:guid}}", async Task<IResult> (Guid employeeId, Guid dutyId, StaffDutyInput input, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                if (await db.Set<StaffDuty>().FirstOrDefaultAsync(d => d.Id == dutyId && d.EmployeeId == employeeId, ct) is not { } duty)
                {
                    return TypedResults.NotFound();
                }

                if (await ValidateAsync(db, employeeId, input, duty.DutyTypeId, ct) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                Apply(duty, input);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await DutyDtoAsync(db, duty.Id, time, ct));
            })
            .WithName("UpdateStaffDuty")
            .WithSummary("Ändert eine Zuständigkeit.")
            .Produces<StaffDutyDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete($"{Route}/{{dutyId:guid}}", async Task<IResult> (Guid employeeId, Guid dutyId, AispaDbContext db, CancellationToken ct) =>
                await PersonProblemAsync(db, employeeId, ct) ?? (await db.Set<StaffDuty>().Where(d => d.Id == dutyId && d.EmployeeId == employeeId).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound()))
            .WithName("DeleteStaffDuty")
            .WithSummary("Löscht eine Zuständigkeit; im Verlauf bleibt nur, dass gelöscht wurde.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet($"{Route}/history", async Task<IResult> (Guid employeeId, AispaDbContext db, EntityHistory history, CancellationToken ct) =>
                await db.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId, ct)
                    ? TypedResults.Ok(await history.ForDutiesAsync(employeeId, ct))
                    : TypedResults.NotFound())
            .WithName("GetStaffDutiesHistory")
            .WithSummary("Verlauf der Zuständigkeiten, Zertifikate und Scans: nur Art, Person und Zeit, nie Inhalte.")
            .Produces<List<HistoryEntry>>().ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapCertificates(RouteGroupBuilder group)
    {
        const string Route = "/employees/{employeeId:guid}/certificates";

        group.MapGet(Route, async Task<IResult> (Guid employeeId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId, ct))
                {
                    return TypedResults.NotFound();
                }

                var today = await StaffRegulationEndpoints.TodayAsync(db, time, ct);
                var certificates = await db.Set<StaffCertificate>().AsNoTracking().Where(c => c.EmployeeId == employeeId)
                    .OrderBy(c => c.Name).ThenByDescending(c => c.ValidUntil).ThenBy(c => c.Id).ToListAsync(ct);
                var scans = await ScannedAsync(db, employeeId, ct);
                return TypedResults.Ok(certificates.Select(c => ToDto(c, certificates, scans.Contains(c.Id), today)).ToList());
            })
            .WithName("ListStaffCertificates")
            .WithSummary("Zertifikate einer Person, auch abgelaufene und erneuerte.")
            .Produces<List<StaffCertificateDto>>().ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost(Route, async Task<IResult> (Guid employeeId, StaffCertificateInput input, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                if (Validate(input) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                var certificate = new StaffCertificate { EmployeeId = employeeId };
                Apply(certificate, input);
                db.Add(certificate);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await CertificateDtoAsync(db, certificate, time, ct));
            })
            .WithName("CreateStaffCertificate")
            .WithSummary("Legt ein Zertifikat an; ohne „gültig bis“ gilt es unbefristet. Zum Erneuern ein neues anlegen, das alte bleibt als Verlauf.")
            .Produces<StaffCertificateDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut($"{Route}/{{certificateId:guid}}", async Task<IResult> (
                Guid employeeId, Guid certificateId, StaffCertificateInput input, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                if (Validate(input) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                if (await db.Set<StaffCertificate>().FirstOrDefaultAsync(c => c.Id == certificateId && c.EmployeeId == employeeId, ct) is not { } certificate)
                {
                    return TypedResults.NotFound();
                }

                Apply(certificate, input);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await CertificateDtoAsync(db, certificate, time, ct));
            })
            .WithName("UpdateStaffCertificate")
            .WithSummary("Ändert ein Zertifikat.")
            .Produces<StaffCertificateDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete($"{Route}/{{certificateId:guid}}", async Task<IResult> (Guid employeeId, Guid certificateId, AispaDbContext db, CancellationToken ct) =>
                await PersonProblemAsync(db, employeeId, ct)
                ?? (await db.Set<StaffCertificate>().Where(c => c.Id == certificateId && c.EmployeeId == employeeId).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound()))
            .WithName("DeleteStaffCertificate")
            .WithSummary("Löscht ein Zertifikat samt Scan; im Verlauf bleibt nur, dass gelöscht wurde.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapScans(RouteGroupBuilder group)
    {
        const string Route = "/employees/{employeeId:guid}/certificates/{certificateId:guid}/scan";

        group.MapPut(Route, async Task<IResult> (
                Guid employeeId, Guid certificateId, StaffCertificateScanInput input, AispaDbContext db, HealthDataProtector protector, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                if (!protector.IsConfigured)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Nicht möglich.", detail: NoKey);
                }

                if (!await db.Set<StaffCertificate>().AnyAsync(c => c.Id == certificateId && c.EmployeeId == employeeId, ct))
                {
                    return TypedResults.NotFound();
                }

                var content = Decode(input.Base64);
                var contentType = content is null ? null : ScanType(content);
                var errors = new Errors();
                errors.Check(content is { Length: > 0 and <= MaxScanBytes }, nameof(input.Base64), "Einen Scan bis 5 MB hochladen.");
                errors.Check(errors.Any || contentType is not null, nameof(input.Base64), "Den Scan als PDF, JPEG oder PNG hochladen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var scan = await db.Set<StaffCertificateScan>().FirstOrDefaultAsync(s => s.CertificateId == certificateId, ct);
                if (scan is null)
                {
                    scan = new StaffCertificateScan { CertificateId = certificateId, EmployeeId = employeeId, Content = [] };
                    db.Add(scan);
                }

                scan.FileName = FileName(input.FileName);
                scan.ContentType = contentType!;
                scan.Content = protector.Protect(content!, certificateId, StaffDutyModel.ScanContext);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("UploadStaffCertificateScan")
            .WithSummary("Scan eines Zertifikats hochladen oder ersetzen: PDF, JPEG oder PNG bis 5 MB, als Base64; gespeichert wird er verschlüsselt.")
            .Produces(StatusCodes.Status204NoContent).ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet(Route, async Task<IResult> (
                Guid employeeId, Guid certificateId, AispaDbContext db, HealthDataProtector protector, HttpContext http, CancellationToken ct) =>
            {
                if (await db.Set<StaffCertificateScan>().AsNoTracking().FirstOrDefaultAsync(s => s.CertificateId == certificateId && s.EmployeeId == employeeId, ct) is not { } scan)
                {
                    return TypedResults.NotFound();
                }

                byte[] content;
                try
                {
                    content = protector.UnprotectBytes(scan.Content, certificateId, StaffDutyModel.ScanContext);
                }
                catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Nicht möglich.", detail: NotReadable);
                }

                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(content, scan.ContentType, scan.FileName);
            })
            .WithName("GetStaffCertificateScan")
            .WithSummary("Scan eines Zertifikats als Datei.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream").ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapDelete(Route, async Task<IResult> (Guid employeeId, Guid certificateId, AispaDbContext db, CancellationToken ct) =>
                await PersonProblemAsync(db, employeeId, ct)
                ?? (await db.Set<StaffCertificateScan>().Where(s => s.CertificateId == certificateId && s.EmployeeId == employeeId).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound()))
            .WithName("DeleteStaffCertificateScan")
            .WithSummary("Löscht den Scan eines Zertifikats; das Zertifikat bleibt.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>Kennzeichen je Person (für <c>staff-regulations/flags</c>): heute gültige Zuständigkeiten mit Warnung, Zertifikate in der Frist oder abgelaufen.</summary>
    public static async Task<Dictionary<Guid, List<DutyFlagDto>>> FlagsAsync(AispaDbContext db, DateOnly today, CancellationToken ct)
    {
        var duties = await db.Set<StaffDuty>().AsNoTracking().Include(d => d.DutyType)
            .Where(d => d.DutyType.IsActive && d.ValidFrom <= today && (d.ValidUntil == null || d.ValidUntil >= today))
            .ToListAsync(ct);
        var certificates = await db.Set<StaffCertificate>().AsNoTracking().ToListAsync(ct);
        return duties.Select(d => d.EmployeeId).Concat(certificates.Select(c => c.EmployeeId)).Distinct()
            .Select(id => (Id: id, Flags: Flags(
                [.. duties.Where(d => d.EmployeeId == id).Select(d => d.DutyType).InListOrder()], [.. certificates.Where(c => c.EmployeeId == id)], today)))
            .Where(p => p.Flags.Count > 0)
            .ToDictionary(p => p.Id, p => p.Flags);
    }

    private static List<DutyFlagDto> Flags(List<DutyType> types, List<StaffCertificate> certificates, DateOnly today)
    {
        var flags = new List<DutyFlagDto>();
        var warned = new List<string>();
        foreach (var type in types)
        {
            var required = type.RequiredCertificate;
            if (string.IsNullOrWhiteSpace(required) || StaffDutyRules.IsSatisfied(required, certificates, today))
            {
                flags.Add(new(type.Name, null));
                continue;
            }

            var lastExpiry = certificates.Where(c => StaffDutyRules.Matches(c.Name, required) && c.ValidUntil < today).Max(c => c.ValidUntil);
            flags.Add(new(type.Name, lastExpiry is { } day ? $"{required} abgelaufen am {StaffDutyRules.Day(day)}" : $"{required} fehlt"));
            warned.Add(required);
        }

        foreach (var certificate in certificates.Where(c => StaffDutyRules.IsDue(c, today, certificates)).OrderBy(c => c.ValidUntil).ThenBy(c => c.Name, StringComparer.Ordinal))
        {
            var until = certificate.ValidUntil!.Value;
            if (until >= today)
            {
                flags.Add(new($"{certificate.Name} bis {StaffDutyRules.Day(until)}", null));
            }
            else if (!warned.Any(name => StaffDutyRules.Matches(certificate.Name, name)))
            {
                flags.Add(new(certificate.Name, $"abgelaufen am {StaffDutyRules.Day(until)}"));
            }
        }

        return flags;
    }

    private static async Task<List<DutyOverviewRowDto>> OverviewAsync(AispaDbContext db, DateOnly today, CancellationToken ct)
    {
        await StaffDutyModel.EnsureDefaultsAsync(db, ct);
        var types = (await db.Set<DutyType>().AsNoTracking().Where(t => t.IsActive).ToListAsync(ct)).InListOrder();
        var duties = await DutiesQuery(db, d => d.Employee.IsActive).ToListAsync(ct);
        var active = await db.Employees.Where(e => e.IsActive).Select(e => e.Id).ToListAsync(ct);
        return
        [
            .. types.Select(t => new DutyOverviewRowDto(t.Id, t.Key, t.Name, t.RequiredCertificate,
            [
                .. duties.Where(d => d.Duty.DutyTypeId == t.Id)
                    .Select(d => new DutyHolderDto(d.Duty.Id, d.Duty.EmployeeId, d.Name,
                        d.Duty.DeputyId is { } deputy && active.Contains(deputy) ? deputy : null,
                        d.Duty.DeputyId is { } shown && active.Contains(shown) ? d.DeputyName : null,
                        d.Duty.ValidFrom, d.Duty.ValidUntil, d.Duty.ValidUntil < today))
                    .OrderBy(h => h.IsExpired).ThenBy(h => h.Name, StringComparer.Ordinal).ThenBy(h => h.ValidFrom),
            ])),
        ];
    }

    private static async Task<List<ExpiringCertificateDto>> ExpiringAsync(AispaDbContext db, DateOnly today, CancellationToken ct)
    {
        var certificates = await db.Set<StaffCertificate>().AsNoTracking().Where(c => c.Employee.IsActive)
            .Select(c => new { Certificate = c, Name = c.Employee.PreferredName ?? c.Employee.DisplayName })
            .ToListAsync(ct);
        var byEmployee = certificates.Select(c => c.Certificate).ToLookup(c => c.EmployeeId);
        return
        [
            .. certificates.Where(c => StaffDutyRules.IsDue(c.Certificate, today, byEmployee[c.Certificate.EmployeeId]))
                .Select(c => new ExpiringCertificateDto(c.Certificate.Id, c.Certificate.EmployeeId, c.Name, c.Certificate.Name, c.Certificate.ValidUntil!.Value,
                    c.Certificate.ValidUntil < today))
                .OrderBy(c => c.ValidUntil).ThenBy(c => c.EmployeeName, StringComparer.Ordinal).ThenBy(c => c.Name, StringComparer.Ordinal),
        ];
    }

    private sealed record DutyRow(StaffDuty Duty, string TypeName, string Name, string? DeputyName);

    private static IQueryable<DutyRow> DutiesQuery(AispaDbContext db, Expression<Func<StaffDuty, bool>>? filter = null) =>
        db.Set<StaffDuty>().AsNoTracking().Where(filter ?? (_ => true)).Select(d => new DutyRow(
            d,
            d.DutyType.Name,
            d.Employee.PreferredName ?? d.Employee.DisplayName,
            d.Deputy == null ? null : d.Deputy.PreferredName ?? d.Deputy.DisplayName));

    private static async Task<StaffDutyDto> DutyDtoAsync(AispaDbContext db, Guid dutyId, TimeProvider time, CancellationToken ct) =>
        ToDto(await DutiesQuery(db, d => d.Id == dutyId).FirstAsync(ct), await StaffRegulationEndpoints.TodayAsync(db, time, ct));

    private static async Task<StaffCertificateDto> CertificateDtoAsync(AispaDbContext db, StaffCertificate certificate, TimeProvider time, CancellationToken ct)
    {
        var all = await db.Set<StaffCertificate>().AsNoTracking().Where(c => c.EmployeeId == certificate.EmployeeId).ToListAsync(ct);
        var scanned = await ScannedAsync(db, certificate.EmployeeId, ct);
        return ToDto(certificate, all, scanned.Contains(certificate.Id), await StaffRegulationEndpoints.TodayAsync(db, time, ct));
    }

    private static async Task<HashSet<Guid>> ScannedAsync(AispaDbContext db, Guid employeeId, CancellationToken ct) =>
        [.. await db.Set<StaffCertificateScan>().AsNoTracking().Where(s => s.EmployeeId == employeeId).Select(s => s.CertificateId).ToListAsync(ct)];

    private static async Task<IResult?> PersonProblemAsync(AispaDbContext db, Guid employeeId, CancellationToken ct)
    {
        var active = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => (bool?)e.IsActive).FirstOrDefaultAsync(ct);
        return active switch
        {
            null => TypedResults.NotFound(),
            false => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Nicht möglich.", detail: FormerPerson),
            _ => null,
        };
    }

    private static Errors Validate(DutyTypeInput input)
    {
        var errors = new Errors();
        errors.Check(!string.IsNullOrWhiteSpace(input.Name), nameof(input.Name), "Einen Namen eingeben.");
        errors.Check(input.Name is null || input.Name.Trim().Length <= 100, nameof(input.Name), "Höchstens 100 Zeichen.");
        errors.Check(input.RequiredCertificate is null || input.RequiredCertificate.Trim().Length <= 200, nameof(input.RequiredCertificate), "Höchstens 200 Zeichen.");
        errors.Check(input.MinimumOnDuty is null or >= 0 and <= 20, nameof(input.MinimumOnDuty), "Eine Zahl von 0 bis 20 eingeben.");
        return errors;
    }

    /// <param name="currentTypeId">Die Art einer bestehenden Zuständigkeit: Sie darf ausgeblendet sein, damit sich die Zuständigkeit noch beenden lässt.</param>
    private static async Task<Errors> ValidateAsync(AispaDbContext db, Guid employeeId, StaffDutyInput input, Guid? currentTypeId, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(await db.Set<DutyType>().AnyAsync(t => t.Id == input.DutyTypeId && (t.IsActive || t.Id == currentTypeId), ct), nameof(input.DutyTypeId), "Eine Zuständigkeit aus der Liste wählen.");
        errors.Check(input.ValidFrom != default, nameof(input.ValidFrom), "Ein Datum eingeben.");
        errors.Check(input.ValidUntil is null || input.ValidFrom <= input.ValidUntil, nameof(input.ValidUntil), "„Gültig bis“ darf nicht vor „Gültig von“ liegen.");
        errors.Check(input.DeputyId != employeeId, nameof(input.DeputyId), "Die Vertretung ist eine andere Person.");
        errors.Check(input.DeputyId is not { } deputy || await db.Employees.AnyAsync(e => e.Id == deputy && e.IsActive, ct),
            nameof(input.DeputyId), "Eine aktive Person als Vertretung wählen.");
        return errors;
    }

    private static Errors Validate(StaffCertificateInput input)
    {
        var errors = new Errors();
        errors.Check(!string.IsNullOrWhiteSpace(input.Name), nameof(input.Name), "Eine Bezeichnung eingeben.");
        errors.Check(input.Name is null || input.Name.Trim().Length <= 200, nameof(input.Name), "Höchstens 200 Zeichen.");
        errors.Check(input.IssuedBy is null || input.IssuedBy.Trim().Length <= 200, nameof(input.IssuedBy), "Höchstens 200 Zeichen.");
        errors.Check(input.RemindDays is >= 1 and <= 365, nameof(input.RemindDays), "Eine Frist von 1 bis 365 Tagen eingeben.");
        errors.Check(input.IssuedOn is null || input.ValidUntil is null || input.IssuedOn <= input.ValidUntil, nameof(input.ValidUntil),
            "„Gültig bis“ darf nicht vor „Ausgestellt am“ liegen.");
        return errors;
    }

    private static void Apply(DutyType type, DutyTypeInput input)
    {
        type.Name = input.Name.Trim();
        type.RequiredCertificate = Clean(input.RequiredCertificate);
        type.MinimumOnDuty = type.Key == DutyTypeKey.FirstAid ? input.MinimumOnDuty ?? type.MinimumOnDuty ?? 1 : null;
        type.IsActive = input.IsActive;
    }

    private static void Apply(StaffDuty duty, StaffDutyInput input)
    {
        duty.DutyTypeId = input.DutyTypeId;
        duty.ValidFrom = input.ValidFrom;
        duty.ValidUntil = input.ValidUntil;
        duty.DeputyId = input.DeputyId;
    }

    private static void Apply(StaffCertificate certificate, StaffCertificateInput input)
    {
        certificate.Name = input.Name.Trim();
        certificate.IssuedOn = input.IssuedOn;
        certificate.ValidUntil = input.ValidUntil;
        certificate.IssuedBy = Clean(input.IssuedBy);
        certificate.RemindDays = input.RemindDays;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static byte[]? Decode(string? base64)
    {
        try
        {
            return string.IsNullOrEmpty(base64) ? null : Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Erkennt das Format am Inhalt, nicht am Dateinamen oder an der Angabe des Aufrufers.</summary>
    internal static string? ScanType(ReadOnlySpan<byte> data) =>
        data switch
        {
            [(byte)'%', (byte)'P', (byte)'D', (byte)'F', ..] => "application/pdf",
            [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
            _ => null,
        };

    private static string FileName(string? name)
    {
        var clean = Path.GetFileName(name?.Trim() ?? "");
        return clean.Length is > 0 and <= 200 ? clean : "scan";
    }

    private static DutyTypeDto ToDto(DutyType t) => new(t.Id, t.Key, t.Name, t.RequiredCertificate, t.MinimumOnDuty, t.IsActive);

    private static StaffDutyDto ToDto(DutyRow d, DateOnly today) =>
        new(d.Duty.Id, d.Duty.EmployeeId, d.Duty.DutyTypeId, d.TypeName, d.Duty.ValidFrom, d.Duty.ValidUntil, d.Duty.DeputyId, d.DeputyName, d.Duty.ValidUntil < today);

    private static StaffCertificateDto ToDto(StaffCertificate c, IEnumerable<StaffCertificate> sameEmployee, bool hasScan, DateOnly today) =>
        new(c.Id, c.EmployeeId, c.Name, c.IssuedOn, c.ValidUntil, c.IssuedBy, c.RemindDays, hasScan, StaffDutyRules.IsDue(c, today, sameEmployee), c.ValidUntil < today);
}
