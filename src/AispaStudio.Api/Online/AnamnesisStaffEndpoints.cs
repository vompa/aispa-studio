using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace AispaStudio.Api.Online;

/// <summary>
/// Anamnese für das Team: Bogen lesen, am Tablet ausfüllen lassen, Papierbogen drucken und den ausgefüllten Bogen fotografiert
/// übernehmen. Den Stand sieht, wer den Kalender sieht; Antworten und fotografierte Bögen nur, wer die Anamnese lesen darf (jede Rolle,
/// die Kunden sieht, nicht das Backoffice; ADR 0013, Nachträge). Tablet, Papier und Hochladen nur, wer buchen darf.
/// Jeder Aufruf prüft über den Kundenstamm, dass der Kunde zum eigenen Unternehmen gehört.
/// </summary>
internal static class AnamnesisStaffEndpoints
{
    /// <summary>Claude nimmt Bilder bis 5 MB; die Oberfläche verkleinert Fotos vorher.</summary>
    public const int MaxImageBytes = 5 * 1024 * 1024;

    private static readonly TimeSpan TabletLinkValidity = TimeSpan.FromMinutes(30);

    public static IEndpointRouteBuilder MapAnamnesisStaffEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/customers/{customerId:guid}/anamnesis").WithTags("Kunden: Anamnese");
        var read = group.MapGroup("").RequirePermission(AccessRight.ViewCalendar);
        var write = group.MapGroup("").RequirePermission(AccessRight.Book);
        var health = group.MapGroup("").RequirePermission(AccessRight.ViewHealthData);

        health.MapGet("", async Task<Results<Ok<AnamnesisRecordDto>, NotFound>> (
                Guid customerId, AispaDbContext db, AnamnesisStore anamnesis, CancellationToken ct) =>
                await CustomerExistsAsync(db, customerId, ct) && await anamnesis.LatestAsync(customerId, ct) is { } record
                    ? TypedResults.Ok(record)
                    : TypedResults.NotFound())
            .WithName("GetCustomerAnamnesis")
            .WithSummary("Neuester Anamnesebogen eines Kunden, entschlüsselt.");

        read.MapGet("/overview", async Task<Results<Ok<AnamnesisOverview>, NotFound>> (
                Guid customerId, AispaDbContext db, OnlineDbContext store, IOptions<OnlineBookingOptions> options, CancellationToken ct) =>
            {
                if (!await CustomerExistsAsync(db, customerId, ct))
                {
                    return TypedResults.NotFound();
                }

                var last = await store.AnamnesisRecords.AsNoTracking().Where(r => r.CustomerId == customerId)
                    .OrderByDescending(r => r.SubmittedAt).Select(r => new { r.SubmittedAt, r.Channel }).FirstOrDefaultAsync(ct);
                var scans = await store.AnamnesisScans.AsNoTracking()
                    .Where(s => s.CustomerId == customerId && s.Status != AnamnesisScanStatus.Confirmed)
                    .OrderBy(s => s.UploadedAt)
                    .Select(s => new AnamnesisScanSummary(s.Id, s.Status, s.UploadedAt))
                    .ToListAsync(ct);
                return TypedResults.Ok(new AnamnesisOverview(
                    last?.SubmittedAt, last?.Channel, last?.SubmittedAt.AddMonths(options.Value.AnamnesisValidMonths), scans));
            })
            .WithName("GetCustomerAnamnesisOverview")
            .WithSummary("Ob ein gültiger Bogen vorliegt und welche fotografierten Bögen noch offen sind (ohne Gesundheitsdaten).");

        read.MapGet("/check", async Task<Results<Ok<AnamnesisCheck>, NotFound>> (
                Guid customerId, Guid treatmentId, AispaDbContext db, AnamnesisStore anamnesis, CancellationToken ct) =>
            {
                if (!await CustomerExistsAsync(db, customerId, ct)
                    || await db.Treatments.AsNoTracking().Where(t => t.Id == treatmentId).Select(t => (bool?)t.CheckAnamnesis).FirstOrDefaultAsync(ct) is not { } check)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(new AnamnesisCheck(check && await anamnesis.LatestAsync(customerId, ct) is { } record
                    && record.Answers.Any(a => a.Yes == true || (a.Key == "other" && !string.IsNullOrWhiteSpace(a.Details)))));
            })
            .WithName("CheckCustomerAnamnesis")
            .WithSummary("Ob vor dieser Behandlung die Anamnese zu prüfen ist (nur ja oder nein, ohne Gesundheitsdaten).");

        write.MapPost("/tablet", async Task<Results<Ok<AnamnesisTabletLink>, NotFound, ValidationProblem>> (
                Guid customerId, AnamnesisLinkRequest input, AispaDbContext db, OnlineDbContext store, AccessLinks links, OnlinePortal portal,
                TimeProvider time, CancellationToken ct) =>
            {
                var check = await CheckAsync(db, customerId, input.AppointmentId, ct);
                if (check.NotFound)
                {
                    return TypedResults.NotFound();
                }

                if (check.Problem is { } problem)
                {
                    return problem;
                }

                var expires = time.GetUtcNow().Add(TabletLinkValidity);
                var token = links.Create(AccessLinkPurpose.AnamnesisTablet, customerId, input.AppointmentId, expires);
                await store.SaveChangesAsync(ct);
                return TypedResults.Ok(new AnamnesisTabletLink(portal.Url(LinkRoute(token, input.Language)), expires));
            })
            .WithName("CreateAnamnesisTabletLink")
            .WithSummary("Link für das Tablet am Empfang (als QR-Code anzeigen): 30 Minuten gültig, nach dem Abgeben verbraucht.");

        write.MapPost("/paper", async Task<Results<Ok<AnamnesisPaperSheet>, NotFound, ValidationProblem>> (
                Guid customerId, AnamnesisLinkRequest input, AispaDbContext db, OnlineDbContext store, AccessLinks links, OnlinePortal portal,
                TimeProvider time, CancellationToken ct) =>
            {
                var check = await CheckAsync(db, customerId, input.AppointmentId, ct);
                if (check.NotFound)
                {
                    return TypedResults.NotFound();
                }

                if (check.Problem is { } problem)
                {
                    return problem;
                }

                var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customerId, ct);
                var appointment = input.AppointmentId is { } appointmentId
                    ? await db.Appointments.AsNoTracking().Include(a => a.Treatment).FirstAsync(a => a.Id == appointmentId, ct)
                    : null;
                var location = appointment is null ? null : await db.Locations.AsNoTracking().FirstAsync(l => l.Id == appointment.LocationId, ct);
                var expires = appointment?.End.AddDays(1) ?? time.GetUtcNow().AddDays(30);
                var token = links.Create(AccessLinkPurpose.Anamnesis, customerId, input.AppointmentId, expires);
                await store.SaveChangesAsync(ct);
                return TypedResults.Ok(new AnamnesisPaperSheet(
                    AnamnesisForm.Definition(input.Language), $"{customer.FirstName} {customer.LastName}".Trim(), location?.Name,
                    appointment?.Treatment.Name, appointment?.Start, location?.TimeZone, portal.Url(LinkRoute(token, input.Language)), expires));
            })
            .WithName("CreateAnamnesisPaperSheet")
            .WithSummary("Papierbogen zum Drucken, mit QR-Code für alle, die lieber am Handy ausfüllen.");

        write.MapPost("/scans", async Task<Results<Accepted<AnamnesisScanDto>, NotFound, ValidationProblem>> (
                Guid customerId, IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] Guid? appointmentId,
                [Microsoft.AspNetCore.Mvc.FromForm] string? language, ClaimsPrincipal principal, AispaDbContext db, OnlineDbContext store,
                HealthDataProtector protector, IMessageBus bus, TimeProvider time, CancellationToken ct) =>
            {
                var check = await CheckAsync(db, customerId, appointmentId, ct);
                if (check.NotFound)
                {
                    return TypedResults.NotFound();
                }

                if (check.Problem is { } problem)
                {
                    return problem;
                }

                var errors = new Errors();
                errors.Check(file.Length is > 0 and <= MaxImageBytes, "File", "Bitte ein Foto bis 5 MB hochladen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, ct);
                var data = buffer.ToArray();
                var contentType = ImageType(data);
                errors.Check(contentType is not null, "File", "Bitte ein Foto als JPEG, PNG oder WebP hochladen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var id = Guid.CreateVersion7();
                var scan = new AnamnesisScan
                {
                    Id = id,
                    CustomerId = customerId,
                    AppointmentId = appointmentId,
                    UploadedAt = time.GetUtcNow(),
                    UploadedBy = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                    ContentType = contentType!,
                    Language = GuestLanguage.Normalize(language),
                    Image = protector.Protect(data, id, AnamnesisScanData.ImageContext),
                    Status = AnamnesisScanStatus.Reading,
                };
                store.AnamnesisScans.Add(scan);
                await store.SaveChangesAsync(ct);
                await bus.PublishAsync(new ReadAnamnesisScan(scan.Id));
                return TypedResults.Accepted($"/api/customers/{customerId}/anamnesis/scans/{scan.Id}", AnamnesisScanData.ToDto(scan, protector));
            })
            .DisableAntiforgery()
            .WithName("UploadAnamnesisScan")
            .WithSummary("Foto eines ausgefüllten Papierbogens hochladen; die KI liest es im Hintergrund.");

        health.MapGet("/scans/{scanId:guid}", async Task<Results<Ok<AnamnesisScanDto>, NotFound>> (
                Guid customerId, Guid scanId, AispaDbContext db, OnlineDbContext store, HealthDataProtector protector, CancellationToken ct) =>
                await FindScanAsync(db, store, customerId, scanId, ct) is { } scan
                    ? TypedResults.Ok(AnamnesisScanData.ToDto(scan, protector))
                    : TypedResults.NotFound())
            .WithName("GetAnamnesisScan")
            .WithSummary("Fotografierter Bogen mit dem Vorschlag der KI; unsichere Felder sind markiert.");

        health.MapGet("/scans/{scanId:guid}/image", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid customerId, Guid scanId, AispaDbContext db, OnlineDbContext store, HealthDataProtector protector, HttpContext http,
                CancellationToken ct) =>
            {
                if (await FindScanAsync(db, store, customerId, scanId, ct) is not { } scan)
                {
                    return TypedResults.NotFound();
                }

                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(protector.UnprotectBytes(scan.Image, scan.Id, AnamnesisScanData.ImageContext), scan.ContentType);
            })
            .WithName("GetAnamnesisScanImage")
            .WithSummary("Das Foto des Papierbogens (zum Vergleichen beim Prüfen).");

        health.MapPost("/scans/{scanId:guid}/confirm", async Task<Results<Ok<AnamnesisRecordDto>, NotFound, ProblemHttpResult, ValidationProblem>> (
                Guid customerId, Guid scanId, AnamnesisScanConfirmation input, ClaimsPrincipal principal, AispaDbContext db,
                OnlineDbContext store, AnamnesisStore anamnesis, CancellationToken ct) =>
            {
                if (await FindScanAsync(db, store, customerId, scanId, ct, tracking: true) is not { } scan)
                {
                    return TypedResults.NotFound();
                }

                if (scan.Status != AnamnesisScanStatus.ReadyForReview)
                {
                    return Conflict(scan.Status == AnamnesisScanStatus.Confirmed
                        ? "Dieser Bogen ist schon übernommen."
                        : "Die KI liest den Bogen noch. Bitte gleich noch einmal versuchen.");
                }

                var answers = input.Answers ?? [];
                var errors = AnamnesisStore.Validate(answers, input.Signature);
                errors.Check(input.ConsentOnPaper, nameof(input.ConsentOnPaper),
                    "Ohne angekreuzte Einwilligung auf dem Papier dürfen wir die Angaben nicht speichern.");
                errors.Check(input.SignedOnPaper, nameof(input.SignedOnPaper), "Der Bogen muss unterschrieben sein.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var recordId = await anamnesis.SaveAsync(answers, input.Signature, input.Language ?? scan.Language, customerId, scan.AppointmentId,
                    AnamnesisStore.ChannelPaper, ct);
                scan.Status = AnamnesisScanStatus.Confirmed;
                scan.RecordId = recordId;
                scan.ConfirmedBy = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                await store.SaveChangesAsync(ct);
                return TypedResults.Ok((await anamnesis.LatestAsync(customerId, ct))!);
            })
            .WithName("ConfirmAnamnesisScan")
            .WithSummary("Geprüften Papierbogen als Anamnese übernehmen. Das Foto bleibt als Beleg der Unterschrift verschlüsselt erhalten.");

        health.MapDelete("/scans/{scanId:guid}", async Task<Results<NoContent, NotFound, ProblemHttpResult>> (
                Guid customerId, Guid scanId, AispaDbContext db, OnlineDbContext store, CancellationToken ct) =>
            {
                if (await FindScanAsync(db, store, customerId, scanId, ct, tracking: true) is not { } scan)
                {
                    return TypedResults.NotFound();
                }

                if (scan.Status == AnamnesisScanStatus.Confirmed)
                {
                    return Conflict("Übernommene Bögen bleiben als Beleg erhalten.");
                }

                store.AnamnesisScans.Remove(scan);
                await store.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DiscardAnamnesisScan")
            .WithSummary("Falsches oder unbrauchbares Foto verwerfen (nur vor der Übernahme).");

        return routes;
    }

    /// <summary>Hash-Route im Kundenbereich; die Sprache des Bogens geht als Abfrage hinter dem Code mit.</summary>
    private static string LinkRoute(string token, string? language) => $"anamnese/{token}?lang={GuestLanguage.Normalize(language)}";

    /// <summary>Der Kundenstamm gehört zum Fachmodell; nur so ist sichergestellt, dass der Kunde zum eigenen Unternehmen gehört.</summary>
    private static Task<bool> CustomerExistsAsync(AispaDbContext db, Guid customerId, CancellationToken ct) =>
        db.Customers.AnyAsync(c => c.Id == customerId, ct);

    /// <summary>Kunde gehört zum Unternehmen, und ein angegebener Termin gehört zum Kunden.</summary>
    private static async Task<(bool NotFound, ValidationProblem? Problem)> CheckAsync(
        AispaDbContext db, Guid customerId, Guid? appointmentId, CancellationToken ct)
    {
        if (!await CustomerExistsAsync(db, customerId, ct))
        {
            return (true, null);
        }

        if (appointmentId is { } id && !await db.Appointments.AnyAsync(a => a.Id == id && a.CustomerId == customerId, ct))
        {
            var errors = new Errors();
            errors.Add("AppointmentId", "Dieser Termin gehört nicht zu diesem Kunden.");
            return (false, errors.Problem());
        }

        return (false, null);
    }

    private static ProblemHttpResult Conflict(string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Nicht möglich.", detail: detail);

    private static async Task<AnamnesisScan?> FindScanAsync(
        AispaDbContext db, OnlineDbContext store, Guid customerId, Guid scanId, CancellationToken ct, bool tracking = false)
    {
        if (!await CustomerExistsAsync(db, customerId, ct))
        {
            return null;
        }

        var scans = tracking ? store.AnamnesisScans : store.AnamnesisScans.AsNoTracking();
        return await scans.FirstOrDefaultAsync(s => s.Id == scanId && s.CustomerId == customerId, ct);
    }

    /// <summary>Erkennt das Bildformat am Inhalt, nicht am Dateinamen.</summary>
    internal static string? ImageType(ReadOnlySpan<byte> data) =>
        data switch
        {
            [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
            _ => null,
        };
}
