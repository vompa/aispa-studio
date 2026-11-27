using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>Prüft einen Claude-Schlüssel mit einem kleinen Testaufruf; in Tests ersetzbar, damit kein echter Aufruf entsteht.</summary>
internal interface IClaudeKeyCheck
{
    Task<ClaudeKeyCheck> CheckAsync(string key, CancellationToken ct);
}

/// <summary>Ein Aufruf mit einem Token Antwort: kostet fast nichts und zeigt, ob Schlüssel, Guthaben und Verbindung passen.</summary>
internal sealed class AnthropicKeyCheck(ClaudeClients clients, IOptions<ClaudeOptions> options) : IClaudeKeyCheck
{
    public async Task<ClaudeKeyCheck> CheckAsync(string key, CancellationToken ct)
    {
        try
        {
            await clients.For(key).Beta.Messages.Create(new MessageCreateParams
            {
                Model = options.Value.Model,
                MaxTokens = 1,
                Messages = [new BetaUserMessageParam { Content = "Hallo" }],
            }, ct);
            return ClaudeKeyChecks.Valid;
        }
        catch (Exception e) when (e is AnthropicUnauthorizedException or AnthropicForbiddenException)
        {
            return ClaudeKeyChecks.Invalid;
        }
        catch (Exception e) when (e is AnthropicRateLimitException
            || (e is AnthropicBadRequestException && e.Message.Contains("credit", StringComparison.OrdinalIgnoreCase)))
        {
            return ClaudeKeyChecks.NoCredit;
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return ClaudeKeyChecks.Unreachable;
        }
    }
}

internal static class ClaudeKeyChecks
{
    public static readonly ClaudeKeyCheck Valid = new(ClaudeKeyCheckOutcome.Valid, "Der Schlüssel ist gültig: Claude hat geantwortet.");
    public static readonly ClaudeKeyCheck Invalid = new(ClaudeKeyCheckOutcome.Invalid, "Der Schlüssel ist ungültig. In der Claude Console einen neuen Schlüssel erstellen und eintragen.");
    public static readonly ClaudeKeyCheck NoCredit = new(ClaudeKeyCheckOutcome.NoCredit, "Der Schlüssel stimmt, aber es fehlt Guthaben oder ein Limit ist erreicht. In der Claude Console unter „Billing“ prüfen.");
    public static readonly ClaudeKeyCheck Unreachable = new(ClaudeKeyCheckOutcome.Unreachable, "Claude ist gerade nicht erreichbar. Internetverbindung prüfen und später noch einmal versuchen.");
}

/// <summary>
/// Claude-Schlüssel des Spas in der App eintragen (ADR 0103). Nur mit dem Recht für die Verwaltung des Betriebs; der Schlüssel wird nie
/// zurückgegeben und nie protokolliert. Ist <c>Ai:Claude:ApiKey</c> in der Konfiguration gesetzt, gilt er, und Speichern und Entfernen sind gesperrt.
/// </summary>
internal static class ClaudeKeyEndpoints
{
    private const int MinLength = 20;
    private const int MaxLength = 300;
    private const string ConfigLocked = "Der Schlüssel ist in der Konfiguration des Servers gesetzt und gilt vor dem Eintrag in der App. Ändern lässt er sich nur dort.";

    public static void MapClaudeKeyEndpoints(this RouteGroupBuilder group)
    {
        var key = group.MapGroup("/claude-key");

        key.MapGet("", (ClaudeKeyStore store, TenantContext tenant) => Status(store, tenant.TenantId))
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("GetClaudeKey")
            .WithSummary("Zeigt, ob ein Claude-Schlüssel gesetzt ist, woher er kommt und seine Maske; nie den Schlüssel selbst (ADR 0103).");

        key.MapPut("", async Task<Results<Ok<ClaudeKeyStatus>, ValidationProblem, ProblemHttpResult>> (
                ClaudeKeyInput input, ClaudeKeyStore store, AispaDbContext db, TenantContext tenant, CancellationToken ct) =>
            {
                if (store.ConfiguredKey is not null)
                {
                    return TypedResults.Problem(ConfigLocked, statusCode: StatusCodes.Status409Conflict);
                }

                var value = input.ApiKey?.Trim() ?? "";
                var errors = new Errors();
                errors.Check(value.StartsWith(ClaudeKeyStore.Prefix, StringComparison.Ordinal) && value.Length is >= MinLength and <= MaxLength && !value.Any(char.IsWhiteSpace),
                    nameof(input.ApiKey), "Das sieht nicht nach einem Claude-Schlüssel aus. Er beginnt mit „sk-ant-“ und steht in der Claude Console unter „API Keys“.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var tenantId = tenant.TenantId!.Value;
                await store.SaveAsync(db, tenantId, value, ct);
                return TypedResults.Ok(Status(store, tenantId));
            })
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("SaveClaudeKey")
            .WithSummary("Speichert den Claude-Schlüssel des Spas verschlüsselt; gilt sofort, ohne Neustart. 409, wenn der Schlüssel in der Konfiguration gesetzt ist.");

        key.MapPost("/check", async Task<Results<Ok<ClaudeKeyCheck>, ProblemHttpResult>> (
                ClaudeKeyStore store, TenantContext tenant, IClaudeKeyCheck check, CancellationToken ct) =>
            {
                if (store.StoredOrConfigured(tenant.TenantId).Key is not { } value)
                {
                    return TypedResults.Problem("Es ist kein Claude-Schlüssel gesetzt.", statusCode: StatusCodes.Status409Conflict);
                }

                return TypedResults.Ok(await check.CheckAsync(value, ct));
            })
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("CheckClaudeKey")
            .WithSummary("Prüft den gesetzten Claude-Schlüssel mit einem kleinen Testaufruf: gültig, ungültig, kein Guthaben oder Limit, nicht erreichbar.");

        key.MapDelete("", async Task<Results<Ok<ClaudeKeyStatus>, ProblemHttpResult>> (
                ClaudeKeyStore store, AispaDbContext db, TenantContext tenant, CancellationToken ct) =>
            {
                if (store.ConfiguredKey is not null)
                {
                    return TypedResults.Problem(ConfigLocked, statusCode: StatusCodes.Status409Conflict);
                }

                var tenantId = tenant.TenantId!.Value;
                await store.RemoveAsync(db, tenantId, ct);
                return TypedResults.Ok(Status(store, tenantId));
            })
            .RequirePermission(AccessRight.ManageOrganization)
            .WithName("RemoveClaudeKey")
            .WithSummary("Entfernt den in der App eingetragenen Claude-Schlüssel; Claude ist danach sofort aus. 409, wenn der Schlüssel in der Konfiguration gesetzt ist.");
    }

    private static ClaudeKeyStatus Status(ClaudeKeyStore store, Guid? tenantId)
    {
        var (value, source) = store.StoredOrConfigured(tenantId);
        return new ClaudeKeyStatus(value is not null, source, value is null ? null : ClaudeKeyStore.Mask(value), value is not null && !store.Off);
    }
}
