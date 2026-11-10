using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Kennzeichen eines Gastes: feste, sachliche Angaben mit Wirkung im Kalender statt Freitext (docs/produkt/kalender-rezeption.md).
/// Aufgehobene bleiben mit Zeitpunkt und Person gespeichert, damit Änderungen nachvollziehbar sind (Art. 16 DSGVO).
/// </summary>
public sealed class CustomerFlag
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public CustomerFlagKind Kind { get; init; }

    /// <summary>Bei „Nicht bei mir“: die Person, die den Gast nicht mehr bekommt.</summary>
    public Guid? EmployeeId { get; init; }

    public string? Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? CreatedByUserId { get; init; }
    public DateTimeOffset? RemovedAt { get; set; }
    public string? RemovedByUserId { get; set; }
}

/// <summary>Was die Kennzeichen eines Gastes und sein Wunsch „Therapeutin“ oder „Therapeut“ beim Planen bewirken.</summary>
internal sealed record CustomerRules(int ExtraMinutes, IReadOnlySet<Guid> ExcludedEmployees, bool DoNotBook, TherapistGender? Gender = null)
{
    public static readonly CustomerRules None = new(0, new HashSet<Guid>(), false);

    /// <summary>Puffer bei „Braucht mehr Zeit“.</summary>
    public const int MoreTimeMinutes = 10;

    public static async Task<CustomerRules> LoadAsync(AispaDbContext db, Guid? customerId, CancellationToken ct)
    {
        if (customerId is not { } id)
        {
            return None;
        }

        var flags = await db.CustomerFlags.AsNoTracking().Where(f => f.CustomerId == id && f.RemovedAt == null)
            .Select(f => new { f.Kind, f.EmployeeId }).ToListAsync(ct);
        var gender = await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.PreferredGender).FirstOrDefaultAsync(ct);
        return From([.. flags.Select(f => (f.Kind, f.EmployeeId))], gender);
    }

    /// <summary>Die Regeln aus den gesetzten Kennzeichen und dem Wunsch „Therapeutin“ oder „Therapeut“ eines Gastes.</summary>
    public static CustomerRules From(IReadOnlyCollection<(CustomerFlagKind Kind, Guid? EmployeeId)> flags, TherapistGender? gender) =>
        flags.Count == 0 && gender is null
            ? None
            : new CustomerRules(
                flags.Any(f => f.Kind == CustomerFlagKind.MoreTime) ? MoreTimeMinutes : 0,
                flags.Where(f => f.Kind == CustomerFlagKind.NotWithMe && f.EmployeeId != null).Select(f => f.EmployeeId!.Value).ToHashSet(),
                flags.Any(f => f.Kind == CustomerFlagKind.DoNotBook),
                gender);

    /// <summary>
    /// Ohne die ausgeschlossenen Personen und mit Puffer nach der Behandlung. Beim Wunsch „Therapeutin“ oder „Therapeut“ fallen Personen
    /// mit anderer Angabe weg (ohne Angabe passen sie); <paramref name="keep"/> ist die ausdrücklich gewählte oder bisherige Person, die
    /// die Rezeption bewusst trotzdem buchen darf.
    /// </summary>
    public PlanningContext Apply(PlanningContext context, Guid? keep = null) => ReferenceEquals(this, None) ? context : context with
    {
        Employees = [.. context.Employees.Where(e => !ExcludedEmployees.Contains(e.Id) && (Gender is null || e.Gender is null || e.Gender == Gender || e.Id == keep))],
        ExtraFollowUpMinutes = context.ExtraFollowUpMinutes + ExtraMinutes,
    };
}

internal static class CustomerFlagEndpoints
{
    /// <summary>Leitung im Sinne der Kennzeichen: Inhaber und Standortleitung.</summary>
    private static bool IsManagement(UserAccess access) => access.Has(AccessRight.ManageLocation);

    public static IEndpointRouteBuilder MapCustomerFlagEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/customers/{customerId:guid}/flags").WithTags("Kunden").RequirePermission(AccessRight.ViewCalendar);

        group.MapGet("/", async (Guid customerId, AispaDbContext db, AccessService accessService, CancellationToken ct) =>
                await ListAsync(db, (await accessService.CurrentAsync(ct))!, customerId, ct))
            .WithName("ListCustomerFlags")
            .WithSummary("Gesetzte Kennzeichen eines Gastes. Den Grund bei „Nicht bei mir“ sehen nur die Leitung und die Person selbst.");

        group.MapPost("/", async Task<Results<Ok<List<CustomerFlagDto>>, NotFound, ValidationProblem, ProblemHttpResult>> (
                Guid customerId, CustomerFlagInput input, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                if (!await db.Customers.AnyAsync(c => c.Id == customerId && c.DeletedAt == null, ct))
                {
                    return TypedResults.NotFound();
                }

                var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
                var employeeId = input.Kind == CustomerFlagKind.NotWithMe ? input.EmployeeId ?? access.EmployeeId : null;
                var errors = new Errors();
                errors.Check((reason ?? "").Length <= 500, nameof(input.Reason), "Höchstens 500 Zeichen.");
                errors.Check(input.Kind != CustomerFlagKind.DoNotBook || reason is not null, nameof(input.Reason), "Bei „Nicht mehr buchen“ den Grund angeben.");
                errors.Check(input.Kind != CustomerFlagKind.NotWithMe || employeeId is not null, nameof(input.EmployeeId), "Für wen? Die Person wählen.");
                errors.Check(employeeId is null || await db.Employees.AnyAsync(e => e.Id == employeeId, ct), nameof(input.EmployeeId), "Unbekannte Person.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                if (MayChange(access, input.Kind, employeeId) is { } denied)
                {
                    return TypedResults.Problem(denied, statusCode: StatusCodes.Status403Forbidden);
                }

                // Dasselbe Kennzeichen zweimal ergibt keinen Sinn; bei „Nicht bei mir“ je Person einmal.
                if (!await db.CustomerFlags.AnyAsync(f => f.CustomerId == customerId && f.RemovedAt == null && f.Kind == input.Kind && f.EmployeeId == employeeId, ct))
                {
                    db.CustomerFlags.Add(new CustomerFlag
                    {
                        CustomerId = customerId,
                        Kind = input.Kind,
                        EmployeeId = employeeId,
                        Reason = reason,
                        CreatedAt = time.GetUtcNow(),
                        CreatedByUserId = access.UserId,
                    });
                    await db.SaveChangesAsync(ct);
                }

                return TypedResults.Ok(await ListAsync(db, access, customerId, ct));
            })
            .WithName("AddCustomerFlag")
            .WithSummary("Setzt ein Kennzeichen. „Braucht mehr Zeit“: wer buchen darf. „Leitung informieren“ und „Nicht mehr buchen“ (mit Grund): nur die Leitung. "
                + "„Nicht bei mir“: die Therapeut:in für sich selbst oder die Leitung.");

        group.MapDelete("/{flagId:guid}", async Task<Results<Ok<List<CustomerFlagDto>>, NotFound, ProblemHttpResult>> (
                Guid customerId, Guid flagId, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                if (await db.CustomerFlags.FirstOrDefaultAsync(f => f.Id == flagId && f.CustomerId == customerId && f.RemovedAt == null, ct) is not { } flag)
                {
                    return TypedResults.NotFound();
                }

                if (MayChange(access, flag.Kind, flag.EmployeeId) is { } denied)
                {
                    return TypedResults.Problem(denied, statusCode: StatusCodes.Status403Forbidden);
                }

                flag.RemovedAt = time.GetUtcNow();
                flag.RemovedByUserId = access.UserId;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await ListAsync(db, access, customerId, ct));
            })
            .WithName("RemoveCustomerFlag")
            .WithSummary("Hebt ein Kennzeichen auf; es bleibt mit Zeitpunkt und Person im Verlauf. Rechte wie beim Setzen.");

        return routes;
    }

    /// <summary>Null, wenn erlaubt; sonst der Grund.</summary>
    private static string? MayChange(UserAccess access, CustomerFlagKind kind, Guid? employeeId) => kind switch
    {
        CustomerFlagKind.MoreTime when !access.Has(AccessRight.Book) => "„Braucht mehr Zeit“ setzt, wer Termine buchen darf.",
        CustomerFlagKind.InformManagement or CustomerFlagKind.DoNotBook when !IsManagement(access) => "Dieses Kennzeichen setzt nur die Leitung.",
        CustomerFlagKind.NotWithMe when !IsManagement(access) && employeeId != access.EmployeeId => "„Nicht bei mir“ setzt und hebt nur die Person selbst oder die Leitung auf.",
        _ => null,
    };

    private static async Task<List<CustomerFlagDto>> ListAsync(AispaDbContext db, UserAccess access, Guid customerId, CancellationToken ct)
    {
        // Nach Art in der Reihenfolge der Aufzählung; gespeichert ist der Name, deshalb erst im Speicher sortieren.
        var flags = (await db.CustomerFlags.AsNoTracking().Where(f => f.CustomerId == customerId && f.RemovedAt == null).ToListAsync(ct))
            .OrderBy(f => f.Kind).ThenBy(f => f.CreatedAt).ToList();
        var employeeIds = flags.Where(f => f.EmployeeId != null).Select(f => f.EmployeeId!.Value).ToList();
        var names = await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DisplayName, ct);
        return [.. flags.Select(f => new CustomerFlagDto(f.Id, f.Kind, f.EmployeeId, f.EmployeeId is { } e ? names.GetValueOrDefault(e) : null,
            f.Kind != CustomerFlagKind.NotWithMe || IsManagement(access) || f.EmployeeId == access.EmployeeId ? f.Reason : null,
            DateOnly.FromDateTime(f.CreatedAt.UtcDateTime), MayChange(access, f.Kind, f.EmployeeId) is null))];
    }
}
