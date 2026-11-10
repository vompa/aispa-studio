using System.Security.Claims;
using System.Security.Cryptography;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Sync;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.HrFile;

/// <summary>
/// Personalakte (R05 Slice 110, Ä252, E8): nur Inhaber und Manager (<see cref="AccessRight.ManageHrFile"/>). Die Person bekommt ihre Einträge auf Anfrage als PDF von der Leitung.
/// Eine ausgeschiedene Person bleibt lesbar und exportierbar, neue Einträge gibt es nicht mehr, und ihre Folgetermine erinnern nicht.
/// </summary>
internal static class HrFileEndpoints
{
    private const string FormerPerson = "Die Person ist ausgeschieden; ihre Personalakte ist nur noch lesbar.";
    private const string NoKey = "Der Schlüssel für vertrauliche Daten ist nicht eingerichtet (Konfiguration HealthData). Ohne ihn lässt sich die Personalakte nicht speichern.";
    private const string NotReadable = "Die Personalakte lässt sich nicht lesen: Der Schlüssel für vertrauliche Daten fehlt oder passt nicht (Konfiguration HealthData).";

    public static IEndpointRouteBuilder MapHrFileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Personalakte").RequirePermission(AccessRight.ManageHrFile);

        group.MapGet("/employees/{employeeId:guid}/hr-file", async Task<IResult> (Guid employeeId, AispaDbContext db, HealthDataProtector protector, CancellationToken ct) =>
            {
                if (!await db.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId, ct))
                {
                    return TypedResults.NotFound();
                }

                return await ReadAsync(db, employeeId, protector, ct) is { } entries ? TypedResults.Ok(entries) : Unreadable();
            })
            .WithName("GetHrFile")
            .WithSummary("Einträge der Personalakte einer Person, neueste oben.")
            .Produces<List<HrEntryDto>>().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/employees/{employeeId:guid}/hr-file", async Task<IResult> (
                Guid employeeId, HrEntryInput input, AispaDbContext db, HealthDataProtector protector, CancellationToken ct) =>
            {
                if (await WriteProblemAsync(db, employeeId, protector, ct) is { } problem)
                {
                    return problem;
                }

                if (Validate(input.Kind, input) is { Count: > 0 } errors)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                var entry = new HrEntry { EmployeeId = employeeId, Kind = input.Kind, Payload = [] };
                entry.Apply(input, protector);
                db.Add(entry);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(entry.ToDto(protector));
            })
            .WithName("CreateHrEntry")
            .WithSummary("Legt ein Gespräch, eine Anmerkung oder eine Verwarnung an; ohne Ablaufdatum gilt eine Verwarnung 2 Jahre.")
            .Produces<HrEntryDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/employees/{employeeId:guid}/hr-file/{entryId:guid}", async Task<IResult> (
                Guid employeeId, Guid entryId, HrEntryInput input, AispaDbContext db, HealthDataProtector protector, CancellationToken ct) =>
            {
                if (await WriteProblemAsync(db, employeeId, protector, ct) is { } problem)
                {
                    return problem;
                }

                if (await db.Set<HrEntry>().FirstOrDefaultAsync(e => e.Id == entryId && e.EmployeeId == employeeId, ct) is not { } entry)
                {
                    return TypedResults.NotFound();
                }

                if (Validate(entry.Kind, input) is { Count: > 0 } errors)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                entry.Apply(input, protector);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(entry.ToDto(protector));
            })
            .WithName("UpdateHrEntry")
            .WithSummary("Ändert einen Eintrag; seine Art bleibt.")
            .Produces<HrEntryDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/employees/{employeeId:guid}/hr-file/{entryId:guid}", async Task<IResult> (
                Guid employeeId, Guid entryId, AispaDbContext db, CancellationToken ct) =>
            {
                if (await PersonProblemAsync(db, employeeId, ct) is { } problem)
                {
                    return problem;
                }

                return await db.Set<HrEntry>().Where(e => e.Id == entryId && e.EmployeeId == employeeId).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound();
            })
            .WithName("DeleteHrEntry")
            .WithSummary("Löscht einen Eintrag; im Verlauf bleibt nur, dass gelöscht wurde.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/employees/{employeeId:guid}/hr-file/export", async Task<IResult> (
                Guid employeeId, ClaimsPrincipal user, AispaDbContext db, HealthDataProtector protector, DocumentHeaders headers, TimeProvider time, CancellationToken ct) =>
            {
                if (await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct) is not { } employee)
                {
                    return TypedResults.NotFound();
                }

                var today = LocalTime.LocalDate(time.GetUtcNow(), await HrFileRules.ZoneAsync(db, ct));
                return await ReadAsync(db, employeeId, protector, ct) is { } entries
                    ? DocumentFiles.Pdf(HrFilePdf.Build(await headers.ForAsync(null, user, ct), employee.DisplayName, entries, today), DocumentFiles.PdfName("Personalakte", employee.DisplayName))
                    : Unreadable();
            })
            .WithName("ExportHrFile")
            .WithSummary("Alle Einträge einer Person als PDF für die Auskunft nach Art. 15 DSGVO.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/employees/{employeeId:guid}/hr-file/history", async Task<IResult> (Guid employeeId, AispaDbContext db, EntityHistory history, CancellationToken ct) =>
                await db.Employees.AsNoTracking().AnyAsync(e => e.Id == employeeId, ct)
                    ? TypedResults.Ok(await history.ForHrFileAsync(employeeId, ct))
                    : TypedResults.NotFound())
            .WithName("GetHrFileHistory")
            .WithSummary("Wer die Personalakte wann geändert hat, ohne Inhalt, neueste oben.")
            .Produces<List<HistoryEntry>>().ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/hr-file/follow-ups", async (AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var now = time.GetUtcNow();
                var zone = await HrFileRules.ZoneAsync(db, ct);
                var open = await db.Set<HrEntry>().AsNoTracking()
                    .Where(e => e.FollowUpOn != null && !e.FollowUpDone && e.Employee.IsActive)
                    .OrderBy(e => e.FollowUpOn).ThenBy(e => e.Id)
                    .Select(e => new { e.Id, e.EmployeeId, Name = e.Employee.PreferredName ?? e.Employee.DisplayName, FollowUpOn = e.FollowUpOn!.Value })
                    .ToListAsync(ct);
                return open.Where(e => HrFileRules.IsFollowUpDue(e.FollowUpOn, now, zone))
                    .Select(e => new HrFollowUpDto(e.Id, e.EmployeeId, $"Folgegespräch mit {e.Name}", e.FollowUpOn)).ToList();
            })
            .WithName("ListHrFollowUps")
            .WithSummary("Fällige Folgetermine aus Mitarbeitergesprächen, die noch nicht geführt sind; die Erinnerung bei der Leitung.");

        return routes;
    }

    /// <summary>null, wenn sich ein Eintrag nicht entschlüsseln lässt: Schlüssel fehlt oder passt nicht.</summary>
    private static async Task<List<HrEntryDto>?> ReadAsync(AispaDbContext db, Guid employeeId, HealthDataProtector protector, CancellationToken ct)
    {
        var entries = await db.Set<HrEntry>().AsNoTracking().Where(e => e.EmployeeId == employeeId)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).ToListAsync(ct);
        try
        {
            return [.. entries.Select(e => e.ToDto(protector))];
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            return null;
        }
    }

    private static ProblemHttpResult Unreadable() => TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Nicht möglich.", detail: NotReadable);

    private static async Task<IResult?> WriteProblemAsync(AispaDbContext db, Guid employeeId, HealthDataProtector protector, CancellationToken ct) =>
        protector.IsConfigured
            ? await PersonProblemAsync(db, employeeId, ct)
            : TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Nicht möglich.", detail: NoKey);

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

    private static Dictionary<string, string[]> Validate(HrEntryKind kind, HrEntryInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            errors[nameof(input.Text)] = [kind switch
            {
                HrEntryKind.Warning => "Grund eintragen.",
                HrEntryKind.Note => "Anmerkung eintragen.",
                _ => "Notizen eintragen.",
            }];
        }

        if (kind == HrEntryKind.Conversation && input.ConversationKind is null)
        {
            errors[nameof(input.ConversationKind)] = ["Art des Gesprächs wählen."];
        }

        if (kind == HrEntryKind.Warning && string.IsNullOrWhiteSpace(input.IssuedBy))
        {
            errors[nameof(input.IssuedBy)] = ["Eintragen, wer die Verwarnung ausgesprochen hat."];
        }

        if (kind == HrEntryKind.Warning && input.ExpiresOn < input.Date)
        {
            errors[nameof(input.ExpiresOn)] = ["Das Ablaufdatum darf nicht vor dem Datum der Verwarnung liegen."];
        }

        return errors;
    }
}
