using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Roster;

/// <summary>
/// Urlaubsanspruch einer Person in einem Kalenderjahr (ADR 0097): Anspruch und Übertrag in Arbeitstagen, von der Leitung eingetragen.
/// Ohne Zeile ist für das Jahr kein Anspruch hinterlegt; die App rät keinen.
/// </summary>
public sealed class EmployeeLeaveEntitlement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; init; }
    public int Year { get; init; }
    public decimal? Days { get; set; }
    public decimal CarriedOver { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
}

/// <summary>Zählt die Urlaubstage aus dem veröffentlichten Dienstplan (ADR 0097).</summary>
internal static class LeaveAccount
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;
    public const decimal MaxDays = 366;

    private static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public static void Configure(ModelBuilder builder) =>
        builder.Entity<EmployeeLeaveEntitlement>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Days).HasPrecision(5, 1);
            e.Property(x => x.CarriedOver).HasPrecision(5, 1);
            e.Property(x => x.UpdatedByUserId).HasMaxLength(450);
            e.HasIndex(x => new { x.EmployeeId, x.Year }).IsUnique();
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

    /// <summary>
    /// Ein Urlaubstag zählt nur an einem Wochentag, an dem die Person regelmäßig arbeitet (ohne hinterlegte Arbeitszeiten: Montag bis Freitag);
    /// steht am selben Tag auch ein Dienst, zählt er halb. Tage bis einschließlich <paramref name="today"/> sind genommen, spätere geplant.
    /// </summary>
    public static async Task<LeaveAccountDto?> LoadAsync(AispaDbContext db, Guid employeeId, int year, DateOnly today, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().Include(e => e.WorkingHours).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null)
        {
            return null;
        }

        var regular = employee.WorkingHours.Select(h => h.Day).Distinct().ToArray();
        var workdays = regular.Length > 0 ? regular : Weekdays;
        var from = new DateOnly(year, 1, 1);
        var until = new DateOnly(year, 12, 31);
        var days = await db.RosterDays.AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && d.Date >= from && d.Date <= until && d.Entries.Any(x => x.Kind == RosterKind.Vacation))
            .Select(d => new { d.Date, Works = d.Entries.Any(x => x.Kind == RosterKind.Work) })
            .ToListAsync(ct);

        decimal taken = 0, planned = 0;
        var uncounted = 0;
        foreach (var day in days)
        {
            if (!workdays.Contains(day.Date.DayOfWeek))
            {
                uncounted++;
                continue;
            }

            var value = day.Works ? 0.5m : 1m;
            if (day.Date <= today)
            {
                taken += value;
            }
            else
            {
                planned += value;
            }
        }

        var entitlement = await db.EmployeeLeaveEntitlements.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Year == year, ct);
        var carried = entitlement?.CarriedOver ?? 0;
        decimal? remaining = entitlement?.Days is { } granted ? granted + carried - taken - planned : null;
        return new LeaveAccountDto(employeeId, year, entitlement?.Days, carried, taken, planned, remaining, uncounted,
            [.. workdays.OrderBy(d => ((int)d + 6) % 7)], regular.Length == 0);
    }
}

public static class LeaveAccountEndpoints
{
    public static IEndpointRouteBuilder MapLeaveAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/employees/{employeeId:guid}/leave-account").WithTags("Dienstplan").RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/{year:int}", async Task<Results<Ok<LeaveAccountDto>, NotFound>> (
                Guid employeeId, int year, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                InRange(year) && await LeaveAccount.LoadAsync(db, employeeId, year, Today(time), ct) is { } account
                    ? TypedResults.Ok(account)
                    : TypedResults.NotFound())
            .WithName("GetLeaveAccount")
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ManageLocation)
            .WithSummary("Urlaubskonto einer Person für ein Kalenderjahr: Anspruch, Übertrag, genommene und geplante Urlaubstage aus dem veröffentlichten Dienstplan, Rest.");

        group.MapPut("/{year:int}", async Task<Results<Ok<LeaveAccountDto>, NotFound, ValidationProblem>> (
                Guid employeeId, int year, LeaveEntitlementInput input, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                if (!InRange(year) || !await db.Employees.AnyAsync(e => e.Id == employeeId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(input.Entitlement is null or (>= 0 and <= LeaveAccount.MaxDays), nameof(input.Entitlement), "Der Anspruch liegt zwischen 0 und 366 Tagen.");
                errors.Check(input.CarriedOver is >= -LeaveAccount.MaxDays and <= LeaveAccount.MaxDays, nameof(input.CarriedOver), "Der Übertrag liegt zwischen −366 und 366 Tagen.");
                errors.Check(IsHalfStep(input.Entitlement ?? 0) && IsHalfStep(input.CarriedOver), nameof(input.Entitlement), "Bitte ganze oder halbe Tage eintragen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var access = (await accessService.CurrentAsync(ct))!;
                var row = await db.EmployeeLeaveEntitlements.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Year == year, ct);
                if (row is null)
                {
                    row = new EmployeeLeaveEntitlement { EmployeeId = employeeId, Year = year };
                    db.EmployeeLeaveEntitlements.Add(row);
                }

                row.Days = input.Entitlement;
                row.CarriedOver = input.CarriedOver;
                row.UpdatedAt = time.GetUtcNow();
                row.UpdatedByUserId = access.UserId;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok((await LeaveAccount.LoadAsync(db, employeeId, year, Today(time), ct))!);
            })
            .WithName("SetLeaveEntitlement")
            .WithSummary("Setzt Urlaubsanspruch und Übertrag einer Person für ein Kalenderjahr (Arbeitstage, ganze oder halbe); ohne Anspruch bleibt der Rest leer.");

        return routes;
    }

    private static bool InRange(int year) => year is >= LeaveAccount.MinYear and <= LeaveAccount.MaxYear;

    private static bool IsHalfStep(decimal value) => value * 2 == decimal.Truncate(value * 2);

    // Wie bei den Abwesenheitsanträgen: Die Person hat keinen festen Standort; der Tag nach UTC genügt für „genommen“ gegen „geplant“.
    private static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
