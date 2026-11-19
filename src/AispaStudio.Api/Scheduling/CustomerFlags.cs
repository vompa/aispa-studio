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

/// <summary>Ein verworfener Vorschlag für ein Kennzeichen; der Vorschlag kommt erst nach <see cref="CustomerFlagSuggestions.DismissedDays"/> Tagen wieder.</summary>
public sealed class CustomerFlagDismissal
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; init; }
    public CustomerFlagKind Kind { get; init; }
    public DateTimeOffset DismissedAt { get; init; }
    public string? UserId { get; init; }
}

/// <summary>
/// Vorschläge für Kennzeichen aus dem Verlauf der letzten 12 Monate. Gesetzt wird nichts von selbst (ADR 0008): Die Rezeption oder die Leitung übernimmt
/// oder verwirft. „Braucht mehr Zeit“ bei wiederholter Verspätung oder Überziehen, „Leitung informieren“ bei wiederholtem Nicht-Erscheinen.
/// „Nicht bei mir“ und „Nicht mehr buchen“ bleiben persönliche Entscheidungen ohne Vorschlag.
/// </summary>
internal static class CustomerFlagSuggestions
{
    /// <summary>Ab so vielen Vorfällen im Zeitraum kommt ein Vorschlag.</summary>
    public const int MinEvents = 2;

    /// <summary>Ab so vielen Minuten zählt eine Verspätung oder ein Überziehen.</summary>
    public const int MinMinutes = 10;

    public const int Months = 12;

    /// <summary>So lange bleibt ein verworfener Vorschlag weg.</summary>
    public const int DismissedDays = 180;

    public static async Task<List<CustomerFlagSuggestion>> ForAsync(AispaDbContext db, UserAccess access, Guid customerId, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddMonths(-Months);
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.CustomerId == customerId && a.PlaceholderGuest == null && a.Start >= since && a.Start < now)
            .Select(a => new { a.Status, a.LateMinutes, a.Start, a.End, a.StartedAt, a.FinishedAt })
            .ToListAsync(ct);
        var active = await db.CustomerFlags.AsNoTracking().Where(f => f.CustomerId == customerId && f.RemovedAt == null).Select(f => f.Kind).ToListAsync(ct);
        var dismissed = await db.CustomerFlagDismissals.AsNoTracking()
            .Where(d => d.CustomerId == customerId && d.DismissedAt > now.AddDays(-DismissedDays)).Select(d => d.Kind).ToListAsync(ct);

        var suggestions = new List<CustomerFlagSuggestion>();
        var late = appointments.Count(a => a.Status != AppointmentStatus.Cancelled && a.LateMinutes >= MinMinutes);
        var overruns = appointments
            .Where(a => a.StartedAt is not null && a.FinishedAt is not null)
            .Select(a => (int)Math.Round((a.FinishedAt!.Value - a.StartedAt!.Value - (a.End - a.Start)).TotalMinutes))
            .Where(m => m >= MinMinutes).ToList();
        if (late + overruns.Count >= MinEvents)
        {
            var parts = new List<string>();
            if (late > 0)
            {
                parts.Add($"{late}-mal mindestens {MinMinutes} Minuten zu spät gekommen");
            }

            if (overruns.Count > 0)
            {
                parts.Add($"{overruns.Count}-mal länger behandelt (bis {overruns.Max()} Minuten)");
            }

            suggestions.Add(new CustomerFlagSuggestion(CustomerFlagKind.MoreTime, $"In den letzten {Months} Monaten {string.Join(" und ", parts)}."));
        }

        var noShows = appointments.Count(a => a.Status == AppointmentStatus.NoShow);
        if (noShows >= MinEvents)
        {
            suggestions.Add(new CustomerFlagSuggestion(CustomerFlagKind.InformManagement, $"In den letzten {Months} Monaten {noShows}-mal nicht erschienen."));
        }

        return [.. suggestions.Where(s => !active.Contains(s.Kind) && !dismissed.Contains(s.Kind) && CustomerFlagEndpoints.MayChange(access, s.Kind, null) is null)];
    }
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

        group.MapGet("/suggestions", async Task<Results<Ok<List<CustomerFlagSuggestion>>, NotFound>> (
                Guid customerId, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
                await db.Customers.AnyAsync(c => c.Id == customerId && c.DeletedAt == null, ct)
                    ? TypedResults.Ok(await CustomerFlagSuggestions.ForAsync(db, (await accessService.CurrentAsync(ct))!, customerId, time.GetUtcNow(), ct))
                    : TypedResults.NotFound())
            .WithName("ListCustomerFlagSuggestions")
            .WithSummary("Vorschläge für Kennzeichen aus den letzten 12 Monaten: „Braucht mehr Zeit“ ab zwei Verspätungen oder Überziehungen von mindestens 10 Minuten, "
                + "„Leitung informieren“ ab zwei Mal nicht erschienen. Nur Kennzeichen, die die angemeldete Person setzen darf, die noch nicht gesetzt "
                + "und nicht in den letzten 180 Tagen verworfen sind. Gesetzt wird nichts von selbst.");

        group.MapPost("/suggestions/{kind}/dismiss", async Task<Results<NoContent, NotFound, ProblemHttpResult>> (
                Guid customerId, CustomerFlagKind kind, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                if (!await db.Customers.AnyAsync(c => c.Id == customerId && c.DeletedAt == null, ct))
                {
                    return TypedResults.NotFound();
                }

                if (MayChange(access, kind, null) is { } denied)
                {
                    return TypedResults.Problem(denied, statusCode: StatusCodes.Status403Forbidden);
                }

                db.CustomerFlagDismissals.Add(new CustomerFlagDismissal { CustomerId = customerId, Kind = kind, DismissedAt = time.GetUtcNow(), UserId = access.UserId });
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DismissCustomerFlagSuggestion")
            .WithSummary("Verwirft einen Vorschlag; er kommt für diesen Gast erst nach 180 Tagen wieder. Rechte wie beim Setzen des Kennzeichens.");

        return routes;
    }

    /// <summary>Null, wenn erlaubt; sonst der Grund.</summary>
    internal static string? MayChange(UserAccess access, CustomerFlagKind kind, Guid? employeeId) => kind switch
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
