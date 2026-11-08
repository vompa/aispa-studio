using System.Security.Cryptography;
using System.Text;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.System;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using Npgsql;
using Wolverine;

namespace AispaStudio.Api.MailIntake;

/// <summary>Abschnitt <c>Operator</c>: Zugang des Betreibers für Aufgaben über alle Spas (z. B. Nummern zuordnen). Ohne Schlüssel gibt es die Endpunkte nicht.</summary>
public sealed class OperatorOptions
{
    public const string Section = "Operator";

    public string? ApiKey { get; set; }
}

/// <summary>Nachricht in der Queue: Postfach eines Spas sofort prüfen (Knopf „Jetzt prüfen“).</summary>
public sealed record CheckSpaMailbox(Guid TenantId);

public static class CheckSpaMailboxHandler
{
    public static async Task Handle(CheckSpaMailbox message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MailboxPoller>().CheckSpaMailboxAsync(message.TenantId, ct);
    }
}

/// <summary>Eigenes Postfach und Nummern eines Spas; Zuordnung der Nummern durch den Betreiber.</summary>
internal static class IntakeEndpoints
{
    private const string UniqueViolation = "23505";
    private const int MaxSecretLength = 4000;

    public static IEndpointRouteBuilder MapIntakeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/intake").WithTags("Terminannahme").RequirePermission(AccessRight.ManageOrganization);

        group.MapGet("/mailbox", async Task<Results<Ok<SpaMailboxDto>, NoContent>> (AispaDbContext db, CancellationToken ct) =>
                await db.SpaMailboxes.AsNoTracking().OrderBy(b => b.Id).FirstOrDefaultAsync(ct) is { } box
                    ? TypedResults.Ok(ToDto(box))
                    : TypedResults.NoContent())
            .WithName("GetSpaMailbox")
            .WithSummary("Eigenes Postfach des Spas für die Terminannahme (ohne Passwort). 204, wenn keines eingerichtet ist.");

        group.MapPut("/mailbox", async Task<Results<Ok<SpaMailboxDto>, ValidationProblem>> (
                SpaMailboxInput input, AispaDbContext db, TenantContext tenants, SpaMailboxSecrets secrets, TimeProvider time,
                CancellationToken ct) =>
            {
                var box = await db.SpaMailboxes.OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
                var newSecret = string.IsNullOrEmpty(input.Secret) ? null : input.Secret;
                var errors = Validate(input, keepsSecret: box?.ProtectedSecret is not null && box.Kind == input.Kind && newSecret is null);
                if (errors.Any)
                {
                    return errors.Problem();
                }

                if (box is null)
                {
                    box = new SpaMailbox { Address = "" };
                    db.SpaMailboxes.Add(box);
                }

                box.Kind = input.Kind;
                box.Address = input.Address.Trim();
                box.Enabled = input.Enabled;
                box.Host = input.Kind == MailboxKind.Imap ? input.Host!.Trim() : null;
                box.Port = input.Kind == MailboxKind.Imap ? input.Port : 0;
                box.Security = input.Security;
                box.UserName = input.Kind == MailboxKind.Imap ? input.UserName!.Trim() : null;
                box.Folder = string.IsNullOrWhiteSpace(input.Folder) ? (input.Kind == MailboxKind.Imap ? "INBOX" : "inbox") : input.Folder.Trim();
                box.ProcessedFolder = input.Kind == MailboxKind.Imap && !string.IsNullOrWhiteSpace(input.ProcessedFolder) ? input.ProcessedFolder.Trim() : null;
                box.DirectoryTenantId = input.Kind == MailboxKind.Graph ? input.DirectoryTenantId!.Trim() : null;
                box.ClientId = input.Kind == MailboxKind.Graph ? input.ClientId!.Trim() : null;
                box.SendReplies = input.SendReplies;
                box.SmtpHost = input.Kind == MailboxKind.Imap && !string.IsNullOrWhiteSpace(input.SmtpHost) ? input.SmtpHost.Trim() : null;
                box.SmtpPort = input.Kind == MailboxKind.Imap ? input.SmtpPort : 0;
                box.SmtpSecurity = input.SmtpSecurity;
                box.SaveSentCopy = input.SaveSentCopy;
                box.SentFolder = input.Kind == MailboxKind.Imap && !string.IsNullOrWhiteSpace(input.SentFolder) ? input.SentFolder.Trim() : null;
                if (newSecret is not null)
                {
                    box.ProtectedSecret = secrets.Protect(tenants.TenantId!.Value, box.Id, newSecret);
                }

                box.UpdatedAt = time.GetUtcNow();
                box.LastError = null;
                box.LastSendError = null;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(box));
            })
            .WithName("UpdateSpaMailbox")
            .WithSummary("Eigenes Postfach einrichten oder ändern; abgerufen wird beim nächsten Durchlauf. Passwort leer lassen, um es zu behalten. "
                + "Mit SendReplies gehen Antworten und Erinnerungen aus diesem Postfach.");

        group.MapDelete("/mailbox", async (AispaDbContext db, CancellationToken ct) =>
            {
                await db.SpaMailboxes.ExecuteDeleteAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteSpaMailbox")
            .WithSummary("Eigenes Postfach entfernen. Bereits eingegangene Nachrichten bleiben im Posteingang.");

        group.MapPost("/mailbox/check", async Task<Results<Accepted, NotFound>> (
                AispaDbContext db, TenantContext tenants, IMessageBus bus, CancellationToken ct) =>
            {
                if (!await db.SpaMailboxes.AnyAsync(b => b.Enabled, ct))
                {
                    return TypedResults.NotFound();
                }

                await bus.PublishAsync(new CheckSpaMailbox(tenants.TenantId!.Value));
                return TypedResults.Accepted((string?)null);
            })
            .WithName("CheckSpaMailbox")
            .WithSummary("Postfach jetzt abrufen und, wenn es selbst versendet, die Anmeldung zum Versand prüfen. Das Ergebnis steht danach "
                + "beim Postfach (LastCheckedAt, LastError, LastSendError).");

        group.MapGet("/routes", async (AispaDbContext db, CancellationToken ct) =>
                await db.IntakeRoutes.AsNoTracking().OrderBy(r => r.Channel).ThenBy(r => r.Address)
                    .Select(r => new IntakeRouteDto(r.Id, r.Channel, r.Address, r.SenderId)).ToListAsync(ct))
            .WithName("ListIntakeRoutes")
            .WithSummary("Nummern und Adressen, über die Gäste dieses Spa erreichen. Vergeben vom Betreiber.");

        MapOperator(routes);
        return routes;
    }

    /// <summary>
    /// Betreiber: Spas ansehen und ihnen Nummern zuordnen (Oberfläche unter /betreiber/). Schlüssel in <c>X-Aispa-Operator-Key</c>;
    /// ohne <c>Operator:ApiKey</c> 404. Kein Anmelde-Cookie: der Betreiber gehört zu keinem Spa.
    /// </summary>
    private static void MapOperator(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/operator").WithTags("Betreiber").AllowAnonymous()
            .AddEndpointFilter(async (context, next) =>
            {
                var key = context.HttpContext.RequestServices.GetRequiredService<IOptions<OperatorOptions>>().Value.ApiKey;
                if (string.IsNullOrEmpty(key))
                {
                    return Results.NotFound();
                }

                var given = context.HttpContext.Request.Headers["X-Aispa-Operator-Key"].ToString();
                return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(key))
                    ? await next(context)
                    : Results.StatusCode(StatusCodes.Status403Forbidden);
            });

        group.MapGet("/spas", async (AispaDbContext db, CancellationToken ct) =>
            {
                var spas = await db.Tenants.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
                var locations = await db.Locations.IgnoreQueryFilters().AsNoTracking()
                    .GroupBy(l => EF.Property<Guid>(l, TenantModel.Property)).Select(g => new { TenantId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.TenantId, g => g.Count, ct);
                var boxes = (await db.SpaMailboxes.IgnoreQueryFilters().AsNoTracking()
                        .Select(b => new { Box = b, TenantId = EF.Property<Guid>(b, TenantModel.Property) }).ToListAsync(ct))
                    .GroupBy(b => b.TenantId).ToDictionary(g => g.Key, g => g.First().Box);
                var routesOf = (await db.IntakeRoutes.IgnoreQueryFilters().AsNoTracking()
                        .OrderBy(r => r.Channel).ThenBy(r => r.Address)
                        .Select(r => new { Route = r, TenantId = EF.Property<Guid>(r, TenantModel.Property) }).ToListAsync(ct))
                    .ToLookup(r => r.TenantId, r => new IntakeRouteDto(r.Route.Id, r.Route.Channel, r.Route.Address, r.Route.SenderId));
                return spas.Select(t =>
                {
                    var box = boxes.GetValueOrDefault(t.Id);
                    return new OperatorSpaDto(t.Id, t.Name, t.Country, t.CreatedAt, locations.GetValueOrDefault(t.Id), box?.Address,
                        box?.Enabled == true, box?.LastCheckedAt, box?.LastError ?? box?.LastSendError, [.. routesOf[t.Id]], t.LockedAt, t.LockReason, t.Plan);
                }).ToList();
            })
            .WithName("ListOperatorSpas")
            .WithSummary("Betreiber: alle Spas mit Standorten, Stand des eigenen Postfachs und zugeordneten Nummern.");

        group.MapGet("/routes", async (AispaDbContext db, CancellationToken ct) =>
                await db.IntakeRoutes.IgnoreQueryFilters().AsNoTracking().OrderBy(r => r.Address)
                    .Select(r => new OperatorIntakeRouteDto(r.Id, EF.Property<Guid>(r, TenantModel.Property), r.Channel, r.Address, r.SenderId))
                    .ToListAsync(ct))
            .WithName("ListOperatorRoutes")
            .WithSummary("Betreiber: alle zugeordneten Nummern und Adressen.");

        group.MapPost("/routes", async Task<Results<Created<OperatorIntakeRouteDto>, ValidationProblem, ProblemHttpResult>> (
            IntakeRouteInput input, AispaDbContext db, TenantContext tenants, TimeProvider time, CancellationToken ct) =>
        {
            var address = IntakeRoutes.Normalize(input.Channel, input.Address ?? "");
            var errors = new Errors();
            errors.Check(input.Channel == BookingChannel.Email ? MailboxAddress.TryParse(address, out _) && address.Contains('@') : address.Length > 5,
                nameof(input.Address), input.Channel == BookingChannel.Email ? "Gültige Mailadresse." : "Nummer im Format +43…");
            errors.Check(input.Channel != BookingChannel.Treatwell, nameof(input.Channel), "Treatwell kommt per Mail.");
            errors.Check(input.SenderId is null || input.SenderId.Length <= 100, nameof(input.SenderId), "Höchstens 100 Zeichen.");
            var plan = await db.Tenants.Where(t => t.Id == input.TenantId).Select(t => (SubscriptionPlan?)t.Plan).FirstOrDefaultAsync(ct);
            errors.Check(plan is not null, nameof(input.TenantId), "Unbekanntes Spa.");
            errors.Check(plan is not { } p || Plans.Includes(p, AppModule.BookingIntake), nameof(input.TenantId),
                "Die Terminannahme gehört zur Stufe „Spa Plus“; erst die Abo-Stufe ändern.");
            if (errors.Any)
            {
                return errors.Problem();
            }

            tenants.Use(input.TenantId);
            var route = new IntakeRoute
            {
                Channel = input.Channel,
                Address = address,
                SenderId = string.IsNullOrWhiteSpace(input.SenderId) ? null : input.SenderId.Trim(),
                CreatedAt = time.GetUtcNow(),
            };
            db.IntakeRoutes.Add(route);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
            {
                return TypedResults.Problem("Diese Nummer bzw. Adresse ist schon einem Spa zugeordnet.", statusCode: StatusCodes.Status409Conflict,
                    title: "Schon vergeben");
            }

            return TypedResults.Created($"/api/operator/routes/{route.Id}",
                new OperatorIntakeRouteDto(route.Id, input.TenantId, route.Channel, route.Address, route.SenderId));
        })
            .WithName("CreateOperatorRoute")
            .WithSummary("Betreiber: Nummer oder Adresse einem Spa zuordnen. 409, wenn sie schon einem Spa gehört.");

        group.MapDelete("/routes/{routeId:guid}", async Task<Results<NoContent, NotFound>> (Guid routeId, AispaDbContext db, CancellationToken ct) =>
                await db.IntakeRoutes.IgnoreQueryFilters().Where(r => r.Id == routeId).ExecuteDeleteAsync(ct) > 0
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound())
            .WithName("DeleteOperatorRoute")
            .WithSummary("Betreiber: Zuordnung aufheben. Nachrichten an diese Nummer finden das Spa danach nicht mehr.");

        group.MapGet("/queue-health", (Online.PaymentRecovery recovery, TimeProvider time, CancellationToken ct) => recovery.HealthAsync(time.GetUtcNow(), ct))
            .WithName("GetOperatorQueueHealth")
            .WithSummary("Betreiber: Nachrichten in der Fehlerablage und Zahlungen, deren Beleg, Gutschein oder Erstattung hängt.");

        TenantLocks.MapOperator(group);
    }

    private static Errors Validate(SpaMailboxInput input, bool keepsSecret)
    {
        var errors = new Errors();
        errors.Check(MailboxAddress.TryParse(input.Address?.Trim() ?? "", out var parsed) && parsed.Address.Contains('@'),
            nameof(input.Address), "Gültige Mailadresse des Postfachs.");
        if (input.Kind == MailboxKind.Imap)
        {
            errors.Require(nameof(input.Host), input.Host);
            errors.Check(input.Port is > 0 and <= 65535, nameof(input.Port), "Port zwischen 1 und 65535.");
            errors.Require(nameof(input.UserName), input.UserName);
            if (input.SendReplies)
            {
                errors.Require(nameof(input.SmtpHost), input.SmtpHost);
                errors.Check(input.SmtpPort is > 0 and <= 65535, nameof(input.SmtpPort), "Port zwischen 1 und 65535.");
            }

            errors.Check(input.SentFolder is not { Length: > 200 }, nameof(input.SentFolder), "Höchstens 200 Zeichen.");
        }
        else
        {
            errors.Require(nameof(input.DirectoryTenantId), input.DirectoryTenantId);
            errors.Require(nameof(input.ClientId), input.ClientId);
        }

        errors.Check(keepsSecret || !string.IsNullOrEmpty(input.Secret), nameof(input.Secret),
            input.Kind == MailboxKind.Imap ? "Passwort fehlt." : "Client-Geheimnis fehlt.");
        errors.Check(input.Secret is null || input.Secret.Length <= MaxSecretLength, nameof(input.Secret), "Zu lang.");
        return errors;
    }

    private static SpaMailboxDto ToDto(SpaMailbox b) =>
        new(b.Kind, b.Address, b.Enabled, b.Host, b.Port, b.Security, b.UserName, b.Folder, b.ProcessedFolder, b.DirectoryTenantId,
            b.ClientId, b.ProtectedSecret is not null, b.LastCheckedAt, b.LastError, b.SendReplies, b.SmtpHost, b.SmtpPort, b.SmtpSecurity,
            b.LastSendError, b.SaveSentCopy, b.SentFolder);
}
