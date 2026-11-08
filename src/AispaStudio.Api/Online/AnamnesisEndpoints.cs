using System.Security.Claims;
using System.Text.Json;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Online;

/// <summary>
/// Anamnesebogen im Kundenbereich: per Link aus der Bestätigungsmail oder vom Papierbogen (QR-Code), am Tablet des Empfangs
/// oder angemeldet. Das Team liest und erfasst Bögen über <see cref="AnamnesisStaffEndpoints"/>.
/// </summary>
internal static class AnamnesisEndpoints
{
    private static readonly AccessLinkPurpose[] LinkPurposes = [AccessLinkPurpose.Anamnesis, AccessLinkPurpose.AnamnesisTablet];

    public static IEndpointRouteBuilder MapAnamnesisEndpoints(this IEndpointRouteBuilder routes)
    {
        var online = routes.MapGroup("/api/online/anamnesis").WithTags("Kundenbereich: Anamnese").AllowAnonymous()
            .RequireRateLimiting(OnlineSetup.PublicRateLimit);

        online.MapGet("/form", (string? language) => AnamnesisForm.Definition(language))
            .WithName("GetAnamnesisForm")
            .WithSummary("Fragen des Anamnesebogens in der gewünschten Sprache („de“, „en“, „fr“ oder „it“; andere Sprachen bekommen Englisch).");

        online.MapGet("/{token}", async Task<Results<Ok<AnamnesisLinkInfo>, NotFound>> (
                string token, AccessLinks links, AispaDbContext db, OnlineDbContext store, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, LinkPurposes, ct);
                return link is null
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(await InfoAsync(link.CustomerId, link.AppointmentId, Channel(link), db, store, ct));
            })
            .WithName("GetAnamnesisLink")
            .WithSummary("Für wen und welchen Termin ein Anamnese-Link gilt.");

        online.MapPost("/{token}", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                string token, AnamnesisSubmission submission, AccessLinks links, AnamnesisStore anamnesis, CancellationToken ct) =>
            {
                var link = await links.FindAsync(token, LinkPurposes, ct);
                if (link is null)
                {
                    return TypedResults.NotFound();
                }

                var result = await anamnesis.SubmitAsync(submission, link.CustomerId, link.AppointmentId, Channel(link), ct);
                if (result.Result is NoContent && link.Purpose == AccessLinkPurpose.AnamnesisTablet)
                {
                    // Das Tablet geht an die nächste Person weiter: der Link darf danach nichts mehr öffnen.
                    await links.RevokeAsync(link, ct);
                }

                return result;
            })
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit)
            .WithName("SubmitAnamnesisByLink")
            .WithSummary("Anamnesebogen über den Link (Mail, Papier-QR oder Tablet) abgeben. Ein neuer Bogen ersetzt den alten.");

        var me = routes.MapGroup("/api/online/me/anamnesis").WithTags("Kundenbereich: Anamnese").RequireCustomer()
            .RequireRateLimiting(OnlineSetup.SensitiveRateLimit);

        me.MapGet("/", async Task<Results<Ok<AnamnesisLinkInfo>, NotFound>> (
                ClaimsPrincipal principal, OnlineDbContext store, AispaDbContext db, CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, store, ct) is { CustomerId: { } customerId }
                    ? TypedResults.Ok(await InfoAsync(customerId, null, AnamnesisStore.ChannelLink, db, store, ct))
                    : TypedResults.NotFound())
            .WithName("GetMyAnamnesis")
            .WithSummary("Ob und wann der eigene Anamnesebogen zuletzt abgegeben wurde.");

        me.MapPost("/", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                AnamnesisSubmission submission, ClaimsPrincipal principal, OnlineDbContext store, AnamnesisStore anamnesis,
                CancellationToken ct) =>
                await CustomerPortalEndpoints.FindAccountAsync(principal, store, ct) is { CustomerId: { } customerId }
                    ? await anamnesis.SubmitAsync(submission, customerId, null, AnamnesisStore.ChannelLink, ct)
                    : TypedResults.NotFound())
            .WithName("SubmitMyAnamnesis");

        return routes;
    }

    private static string Channel(AccessLink link) =>
        link.Purpose == AccessLinkPurpose.AnamnesisTablet ? AnamnesisStore.ChannelTablet : AnamnesisStore.ChannelLink;

    private static async Task<AnamnesisLinkInfo> InfoAsync(
        Guid customerId, Guid? appointmentId, string channel, AispaDbContext db, OnlineDbContext store, CancellationToken ct)
    {
        var firstName = await db.Customers.Where(c => c.Id == customerId).Select(c => c.FirstName).FirstOrDefaultAsync(ct) ?? "";
        var appointment = appointmentId is { } id
            ? await db.Appointments.AsNoTracking().Include(a => a.Treatment).FirstOrDefaultAsync(a => a.Id == id, ct)
            : null;
        var timeZone = appointment is null
            ? null
            : await db.Locations.Where(l => l.Id == appointment.LocationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        var last = await store.AnamnesisRecords.Where(r => r.CustomerId == customerId)
            .MaxAsync(r => (DateTimeOffset?)r.SubmittedAt, ct);
        return new AnamnesisLinkInfo(firstName, appointment?.Treatment.Name, appointment?.Start, timeZone, last, channel);
    }
}

/// <summary>Prüft und speichert Anamnesebögen, egal über welchen Weg sie kommen, und liest sie für das Team wieder.</summary>
internal sealed class AnamnesisStore(OnlineDbContext store, HealthDataProtector protector, TimeProvider time)
{
    public const string ChannelLink = "Link";
    public const string ChannelTablet = "Tablet";
    public const string ChannelPaper = "Papier";

    private const int MaxDetailsLength = 1000;

    private sealed record StoredAnswers(IReadOnlyList<AnamnesisAnswer> Answers, string Signature, string Language);

    /// <summary>Bogen, den die Kundin oder der Kunde selbst ausgefüllt hat (Link, Tablet, Konto).</summary>
    public async Task<Results<NoContent, NotFound, ValidationProblem>> SubmitAsync(
        AnamnesisSubmission submission, Guid customerId, Guid? appointmentId, string channel, CancellationToken ct)
    {
        var answers = submission.Answers ?? [];
        var errors = Validate(answers, submission.Signature);
        errors.Check(submission.ConsentHealthData, nameof(submission.ConsentHealthData), "Ohne Einwilligung können wir die Angaben nicht speichern.");
        if (errors.Any)
        {
            return errors.Problem();
        }

        await SaveAsync(answers, submission.Signature!, submission.Language, customerId, appointmentId, channel, ct);
        return TypedResults.NoContent();
    }

    public static Errors Validate(IReadOnlyList<AnamnesisAnswer> answers, string? signature)
    {
        var errors = new Errors();
        errors.Require("Signature", signature);
        errors.Check((signature?.Length ?? 0) <= 200, "Signature", "Höchstens 200 Zeichen.");
        foreach (var answer in answers)
        {
            errors.Check(AnamnesisForm.Keys.Contains(answer.Key), "Answers", $"Unbekannte Frage „{answer.Key}“.");
            errors.Check((answer.Details?.Length ?? 0) <= MaxDetailsLength, answer.Key, $"Höchstens {MaxDetailsLength} Zeichen.");
        }

        foreach (var key in AnamnesisForm.Keys.Where(AnamnesisForm.IsYesNo))
        {
            errors.Check(answers.Any(a => a.Key == key && a.Yes is not null), key, "Bitte mit Ja oder Nein beantworten.");
        }

        errors.Check(answers.Select(a => a.Key).Distinct().Count() == answers.Count, "Answers", "Jede Frage nur einmal beantworten.");
        return errors;
    }

    /// <summary>Speichert geprüfte Antworten verschlüsselt und liefert die Id des Bogens.</summary>
    public async Task<Guid> SaveAsync(
        IReadOnlyList<AnamnesisAnswer> answers, string signature, string? language, Guid customerId, Guid? appointmentId, string channel,
        CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var cleaned = answers
            .Select(a => new AnamnesisAnswer(a.Key, AnamnesisForm.IsYesNo(a.Key) ? a.Yes : null, string.IsNullOrWhiteSpace(a.Details) ? null : a.Details.Trim()))
            .ToList();
        var id = Guid.CreateVersion7();
        var payload = JsonSerializer.Serialize(new StoredAnswers(cleaned, signature.Trim(), GuestLanguage.Normalize(language)), JsonSerializerOptions.Web);
        store.AnamnesisRecords.Add(new AnamnesisRecord
        {
            Id = id,
            CustomerId = customerId,
            AppointmentId = appointmentId,
            FormVersion = AnamnesisForm.Version,
            Channel = channel,
            SubmittedAt = now,
            ConsentGivenAt = now,
            Payload = protector.Protect(payload, id),
        });
        await store.SaveChangesAsync(ct);
        return id;
    }

    /// <summary>Wann der letzte Bogen des Gastes einging, aus den Metadaten und ohne zu entschlüsseln; <c>null</c> ohne Bogen.</summary>
    public Task<DateTimeOffset?> LatestSubmittedAtAsync(Guid customerId, CancellationToken ct) =>
        store.AnamnesisRecords.AsNoTracking().Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.SubmittedAt).Select(r => (DateTimeOffset?)r.SubmittedAt).FirstOrDefaultAsync(ct);

    public async Task<AnamnesisRecordDto?> LatestAsync(Guid customerId, CancellationToken ct)
    {
        var record = await store.AnamnesisRecords.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.SubmittedAt)
            .FirstOrDefaultAsync(ct);
        if (record is null)
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<StoredAnswers>(protector.Unprotect(record.Payload, record.Id), JsonSerializerOptions.Web)!;
        return new AnamnesisRecordDto(record.Id, record.CustomerId, record.AppointmentId, record.FormVersion,
            record.Channel, record.SubmittedAt, stored.Signature, stored.Answers);
    }
}
