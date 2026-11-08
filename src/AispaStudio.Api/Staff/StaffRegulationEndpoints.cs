using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.HrFile;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Staff;

/// <summary>
/// Hinweise, Sonderregelungen und Vollmachten (R05 Slice 111, Ä252, E10, E11): Pflege, Kennzeichen und „Wer darf was?“ nur für Inhaber und Manager (<see cref="AccessRight.ManageHrFile"/>);
/// die Symbole in Kalender und Dienstplan sieht, wer den Kalender oder den Dienstplan sieht. Eine ausgeschiedene Person bleibt lesbar und bekommt nichts Neues.
/// </summary>
internal static class StaffRegulationEndpoints
{
    private const string FormerPerson = "Die Person ist ausgeschieden; ihre Regelungen und Vollmachten sind nur noch lesbar.";
    private const int MaxTextLength = 500;

    public static IEndpointRouteBuilder MapStaffRegulationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Regelungen und Vollmachten").RequirePermission(AccessRight.ManageHrFile);

        group.MapGet("/employees/{employeeId:guid}/regulations", async Task<IResult> (Guid employeeId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId, ct))
                {
                    return TypedResults.NotFound();
                }

                var today = await TodayAsync(db, time, ct);
                var notes = await db.Set<StaffNote>().AsNoTracking().Where(n => n.EmployeeId == employeeId).OrderBy(n => n.Id).ToListAsync(ct);
                var rules = await db.Set<StaffRule>().AsNoTracking().Where(r => r.EmployeeId == employeeId).OrderBy(r => r.Id).ToListAsync(ct);
                var powers = await db.Set<StaffPower>().AsNoTracking().Where(p => p.EmployeeId == employeeId).OrderBy(p => p.Id).ToListAsync(ct);
                return TypedResults.Ok(new StaffRegulationsDto(
                    [.. notes.Select(n => ToDto(n, today))], [.. rules.Select(r => ToDto(r, today))], [.. powers.Select(p => ToDto(p, today))]));
            })
            .WithName("GetStaffRegulations")
            .WithSummary("Hinweise, Sonderregelungen und Vollmachten einer Person, auch abgelaufene.")
            .Produces<StaffRegulationsDto>().ProducesProblem(StatusCodes.Status404NotFound);

        MapItems<StaffNote, StaffNoteInput, StaffNoteDto>(group, "notes", "StaffNote", "den Hinweis", Validate, Apply, ToDto,
            (employeeId, _, _, _) => Task.FromResult(new StaffNote { EmployeeId = employeeId }));
        MapItems<StaffRule, StaffRuleInput, StaffRuleDto>(group, "rules", "StaffRule", "die Sonderregelung", Validate, Apply, ToDto,
            (employeeId, _, _, _) => Task.FromResult(new StaffRule { EmployeeId = employeeId }));
        MapItems<StaffPower, StaffPowerInput, StaffPowerDto>(group, "powers", "StaffPower", "die Vollmacht", Validate, Apply, ToDto, NewPowerAsync);

        group.MapGet("/employees/{employeeId:guid}/regulations/history", async Task<IResult> (Guid employeeId, AispaDbContext db, EntityHistory history, CancellationToken ct) =>
                await db.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId, ct)
                    ? TypedResults.Ok(await history.ForRegulationsAsync(employeeId, ct))
                    : TypedResults.NotFound())
            .WithName("GetStaffRegulationsHistory")
            .WithSummary("Verlauf der Regelungen und Vollmachten: nur Art, Person und Zeit, nie Inhalte.")
            .Produces<List<HistoryEntry>>().ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/staff-regulations/flags", async (AispaDbContext db, TimeProvider time, CancellationToken ct) => await FlagsAsync(db, await TodayAsync(db, time, ct), ct))
            .WithName("ListStaffFlags")
            .WithSummary("Kennzeichen für Karte und Teamliste: die heute gültigen Einträge je Person in Worten.");

        group.MapGet("/staff-regulations/powers", async (AispaDbContext db, TimeProvider time, CancellationToken ct) => await OverviewAsync(db, await TodayAsync(db, time, ct), ct))
            .WithName("ListPowerOverview")
            .WithSummary("„Wer darf was?“: je Vollmacht aus der festen Liste, wer sie hat und bis wann.");

        routes.MapGet("/api/employee-notes/symbols", async (DateOnly on, AispaDbContext db, AccessService access, CancellationToken ct) =>
            {
                var leadership = (await access.CurrentAsync(ct))!.Has(AccessRight.ManageHrFile);
                return await db.Set<StaffNote>().AsNoTracking()
                    .Where(n => n.ShowAsSymbol && (leadership || n.Visibility == StaffNoteVisibility.Team)
                        && (n.ValidFrom == null || n.ValidFrom <= on) && (n.ValidUntil == null || n.ValidUntil >= on))
                    .OrderBy(n => n.Id)
                    .Select(n => new StaffNoteSymbolDto(n.EmployeeId, n.Text))
                    .ToListAsync(ct);
            })
            .WithTags("Regelungen und Vollmachten")
            .WithName("ListStaffNoteSymbols")
            .WithSummary("Hinweise als Symbol im Dienstplan und im Kopf der Kalenderspalte; Hinweise „nur Leitung“ nur für Inhaber und Manager.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AlsoAllow(AccessRight.ViewStaffRecords, insteadOf: AccessRight.ViewCalendar);

        return routes;
    }

    /// <summary>Anlegen, Ändern und Löschen sind bei allen drei Arten gleich; im Verlauf bleibt nur, dass es geschah.</summary>
    private static void MapItems<TItem, TInput, TDto>(
        RouteGroupBuilder group,
        string segment,
        string name,
        string label,
        Func<TInput, Errors> validate,
        Action<TItem, TInput> apply,
        Func<TItem, DateOnly, TDto> toDto,
        Func<Guid, AispaDbContext, AccessService, CancellationToken, Task<TItem>> create)
        where TItem : class, IStaffRegulation
    {
        var path = $"/employees/{{employeeId:guid}}/regulations/{segment}";

        group.MapPost(path, async Task<IResult> (Guid employeeId, TInput input, AispaDbContext db, AccessService access, TimeProvider time, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                if (validate(input) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                var item = await create(employeeId, db, access, ct);
                apply(item, input);
                db.Add(item);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(toDto(item, await TodayAsync(db, time, ct)));
            })
            .WithName($"Create{name}")
            .WithSummary($"Legt {label} an.")
            .Produces<TDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut($"{path}/{{itemId:guid}}", async Task<IResult> (Guid employeeId, Guid itemId, TInput input, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                if (validate(input) is { Any: true } errors)
                {
                    return errors.Problem();
                }

                if (await db.Set<TItem>().FirstOrDefaultAsync(i => i.Id == itemId && i.EmployeeId == employeeId, ct) is not { } item)
                {
                    return TypedResults.NotFound();
                }

                apply(item, input);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(toDto(item, await TodayAsync(db, time, ct)));
            })
            .WithName($"Update{name}")
            .WithSummary($"Ändert {label}.")
            .Produces<TDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete($"{path}/{{itemId:guid}}", async Task<IResult> (Guid employeeId, Guid itemId, AispaDbContext db, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                return await db.Set<TItem>().Where(i => i.Id == itemId && i.EmployeeId == employeeId).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound();
            })
            .WithName($"Delete{name}")
            .WithSummary($"Löscht {label}; im Verlauf bleibt nur, dass gelöscht wurde.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<StaffPower> NewPowerAsync(Guid employeeId, AispaDbContext db, AccessService access, CancellationToken ct)
    {
        var userId = (await access.CurrentAsync(ct))!.UserId;
        var grantedBy = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        return new StaffPower { EmployeeId = employeeId, GrantedBy = grantedBy ?? "" };
    }

    private static async Task<List<StaffFlagsDto>> FlagsAsync(AispaDbContext db, DateOnly today, CancellationToken ct)
    {
        var notes = await db.Set<StaffNote>().AsNoTracking()
            .Where(n => (n.ValidFrom == null || n.ValidFrom <= today) && (n.ValidUntil == null || n.ValidUntil >= today)).OrderBy(n => n.Id).ToListAsync(ct);
        var rules = await db.Set<StaffRule>().AsNoTracking()
            .Where(r => r.ValidFrom <= today && (r.ValidUntil == null || r.ValidUntil >= today)).OrderBy(r => r.Id).ToListAsync(ct);
        var powers = await db.Set<StaffPower>().AsNoTracking()
            .Where(p => p.ValidFrom <= today && (p.ValidUntil == null || p.ValidUntil >= today)).OrderBy(p => p.Id).ToListAsync(ct);
        var duties = await StaffDutyEndpoints.FlagsAsync(db, today, ct);
        return
        [
            .. notes.Select(n => n.EmployeeId).Concat(rules.Select(r => r.EmployeeId)).Concat(powers.Select(p => p.EmployeeId)).Concat(duties.Keys).Distinct()
                .Select(id => new StaffFlagsDto(
                    id,
                    [.. notes.Where(n => n.EmployeeId == id).Select(n => n.Text)],
                    [.. rules.Where(r => r.EmployeeId == id).Select(StaffRuleCheck.Summary)],
                    [.. powers.Where(p => p.EmployeeId == id).Select(p => StaffPowerCheck.Label(p.Kind))],
                    duties.GetValueOrDefault(id) ?? [])),
        ];
    }

    private static async Task<List<PowerOverviewRowDto>> OverviewAsync(AispaDbContext db, DateOnly today, CancellationToken ct)
    {
        var powers = await db.Set<StaffPower>().AsNoTracking()
            .Select(p => new { Power = p, Name = p.Employee.PreferredName ?? p.Employee.DisplayName })
            .ToListAsync(ct);
        return
        [
            .. Enum.GetValues<StaffPowerKind>().Select(kind => new PowerOverviewRowDto(
                kind,
                StaffPowerCheck.Label(kind),
                StaffPowerCheck.IsChecked(kind),
                [
                    .. powers.Where(p => p.Power.Kind == kind)
                        .Select(p => new PowerHolderDto(p.Power.EmployeeId, p.Name, p.Power.ValidFrom, p.Power.ValidUntil, p.Power.GrantedBy, p.Power.ValidUntil < today))
                        .OrderBy(h => h.IsExpired).ThenBy(h => h.Name, StringComparer.Ordinal),
                ])),
        ];
    }

    /// <summary>Heute in der Zeitzone des Spas (die Personalakte gilt fürs ganze Spa).</summary>
    internal static async Task<DateOnly> TodayAsync(AispaDbContext db, TimeProvider time, CancellationToken ct) =>
        LocalTime.LocalDate(time.GetUtcNow(), await HrFileRules.ZoneAsync(db, ct));

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

    private static Errors Validate(StaffNoteInput input)
    {
        var errors = new Errors();
        errors.Check(!string.IsNullOrWhiteSpace(input.Text), nameof(input.Text), "Bitte einen Text eingeben.");
        errors.Check(input.Text is null || input.Text.Length <= MaxTextLength, nameof(input.Text), $"Höchstens {MaxTextLength} Zeichen.");
        CheckRange(errors, input.ValidFrom, input.ValidUntil);
        return errors;
    }

    private static Errors Validate(StaffRuleInput input)
    {
        var errors = new Errors();
        errors.Check(input.ValidFrom != default, nameof(input.ValidFrom), "Bitte ein Datum eingeben.");
        CheckRange(errors, input.ValidFrom, input.ValidUntil);
        errors.Check(input.Reason is null || input.Reason.Length <= MaxTextLength, nameof(input.Reason), $"Höchstens {MaxTextLength} Zeichen.");
        switch (input.Kind)
        {
            case StaffRuleKind.NoAppointmentsAfter:
                errors.Check(input.LatestEnd is not null, nameof(input.LatestEnd), "Bitte die Uhrzeit angeben.");
                break;
            case StaffRuleKind.MaxHoursPerDay:
                errors.Check(input.MaxHours is > 0 and <= 24, nameof(input.MaxHours), "Bitte Stunden zwischen 0 und 24 angeben.");
                break;
            case StaffRuleKind.FixedDaysOff:
                errors.Check(input.DaysOff is { Count: > 0 }, nameof(input.DaysOff), "Bitte mindestens einen Wochentag wählen.");
                break;
        }

        return errors;
    }

    private static Errors Validate(StaffPowerInput input)
    {
        var errors = new Errors();
        errors.Check(input.ValidFrom != default, nameof(input.ValidFrom), "Bitte ein Datum eingeben.");
        CheckRange(errors, input.ValidFrom, input.ValidUntil);
        return errors;
    }

    private static void CheckRange(Errors errors, DateOnly? from, DateOnly? until) =>
        errors.Check(from is null || until is null || from <= until, "ValidUntil", "„Gültig bis“ darf nicht vor „Gültig von“ liegen.");

    private static void Apply(StaffNote note, StaffNoteInput input)
    {
        note.Text = input.Text.Trim();
        note.Visibility = input.Visibility;
        note.ShowAsSymbol = input.ShowAsSymbol;
        note.ValidFrom = input.ValidFrom;
        note.ValidUntil = input.ValidUntil;
    }

    private static void Apply(StaffRule rule, StaffRuleInput input)
    {
        rule.Kind = input.Kind;
        rule.ValidFrom = input.ValidFrom;
        rule.ValidUntil = input.ValidUntil;
        rule.LatestEnd = input.Kind == StaffRuleKind.NoAppointmentsAfter ? input.LatestEnd : null;
        rule.MaxHours = input.Kind == StaffRuleKind.MaxHoursPerDay ? input.MaxHours : null;
        rule.DaysOff = input.Kind == StaffRuleKind.FixedDaysOff ? [.. (input.DaysOff ?? []).Distinct()] : [];
        rule.Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
    }

    private static void Apply(StaffPower power, StaffPowerInput input)
    {
        power.Kind = input.Kind;
        power.ValidFrom = input.ValidFrom;
        power.ValidUntil = input.ValidUntil;
    }

    private static StaffNoteDto ToDto(StaffNote n, DateOnly today) =>
        new(n.Id, n.EmployeeId, n.Text, n.Visibility, n.ShowAsSymbol, n.ValidFrom, n.ValidUntil, n.ValidUntil < today);

    private static StaffRuleDto ToDto(StaffRule r, DateOnly today) =>
        new(r.Id, r.EmployeeId, r.Kind, StaffRuleCheck.Summary(r), r.ValidFrom, r.ValidUntil, r.LatestEnd, r.MaxHours, r.DaysOff, r.Reason, r.ValidUntil < today);

    private static StaffPowerDto ToDto(StaffPower p, DateOnly today) =>
        new(p.Id, p.EmployeeId, p.Kind, StaffPowerCheck.Label(p.Kind), p.ValidFrom, p.ValidUntil, p.GrantedBy, p.ValidUntil < today);
}
