using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Anthropic.Models.Beta.Messages;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>Verbrauch von Claude eines Spas an einem Tag (UTC), aufsummiert je Aufgabe und Modell (T-112a). Nur Tokens, keine Inhalte.</summary>
public sealed class AiUsageDay
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateOnly Day { get; init; }

    public AiUsageTask Task { get; init; }

    public required string Model { get; init; }

    public int Calls { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CacheReadTokens { get; set; }

    public long CacheWriteTokens { get; set; }
}

internal static class AiUsageModel
{
    public const int ModelLength = 100;

    public static void Configure(ModelBuilder builder) =>
        builder.Entity<AiUsageDay>(e =>
        {
            e.Property(u => u.Id).ValueGeneratedNever();
            e.Property(u => u.Task).HasConversion<string>().HasMaxLength(40);
            e.Property(u => u.Model).HasMaxLength(ModelLength);
        });
}

/// <summary>Tokens eines Aufrufs, wie Claude sie in <c>usage</c> meldet.</summary>
internal readonly record struct AiTokens(long Input, long Output, long CacheRead, long CacheWrite)
{
    public static AiTokens From(BetaUsage usage) =>
        new(usage.InputTokens, usage.OutputTokens, usage.CacheReadInputTokens ?? 0, usage.CacheCreationInputTokens ?? 0);
}

/// <summary>
/// Der gemeinsame Punkt, an dem jeder Claude-Aufruf seinen Verbrauch meldet (T-112a): ein strukturierter Logeintrag ohne Inhalte und ohne
/// Schlüssel, dazu ein Upsert in <see cref="AiUsageDay"/> für das Spa des Scopes (HTTP-Anfrage oder Hintergrundauftrag nach
/// <see cref="TenantContext.Use"/>). Geschrieben wird über einen eigenen Scope und damit eine eigene Verbindung, damit eine offene Transaktion
/// des Aufrufers weder mitgespeichert noch abgebrochen wird. Scheitert das Schreiben, bleibt es bei einer Warnung; der KI-Aufruf läuft weiter.
/// </summary>
internal sealed partial class AiUsageRecorder(TenantContext tenant, IServiceScopeFactory scopes, TimeProvider time, ILogger<AiUsageRecorder> log)
{
    public async Task RecordAsync(AiUsageTask task, string model, AiTokens tokens, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        LogUsage(log, task, model, tokens.Input, tokens.Output, tokens.CacheRead, tokens.CacheWrite, tenantId);
        if (tenantId is not { } id)
        {
            LogNoTenant(log, task);
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(id);
            var db = scope.ServiceProvider.GetRequiredService<AispaDbContext>();
            var day = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO aispa."AiUsageDay" ("Id", "TenantId", "Day", "Task", "Model", "Calls", "InputTokens", "OutputTokens", "CacheReadTokens", "CacheWriteTokens")
                VALUES ({Guid.CreateVersion7()}, {id}, {day}, {task.ToString()}, {model}, 1, {tokens.Input}, {tokens.Output}, {tokens.CacheRead}, {tokens.CacheWrite})
                ON CONFLICT ("TenantId", "Day", "Task", "Model") DO UPDATE SET
                    "Calls" = "AiUsageDay"."Calls" + 1,
                    "InputTokens" = "AiUsageDay"."InputTokens" + EXCLUDED."InputTokens",
                    "OutputTokens" = "AiUsageDay"."OutputTokens" + EXCLUDED."OutputTokens",
                    "CacheReadTokens" = "AiUsageDay"."CacheReadTokens" + EXCLUDED."CacheReadTokens",
                    "CacheWriteTokens" = "AiUsageDay"."CacheWriteTokens" + EXCLUDED."CacheWriteTokens"
                """, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWriteFailed(log, ex, task);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Claude-Aufruf {Task} mit {Model}: {InputTokens} Tokens Eingang, {OutputTokens} Ausgang, {CacheReadTokens} Cache gelesen, {CacheWriteTokens} Cache geschrieben (Spa {TenantId}).")]
    private static partial void LogUsage(ILogger log, AiUsageTask task, string model, long inputTokens, long outputTokens, long cacheReadTokens, long cacheWriteTokens, Guid? tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Verbrauch von Claude für {Task} ohne Spa: nur protokolliert, nicht gespeichert.")]
    private static partial void LogNoTenant(ILogger log, AiUsageTask task);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Verbrauch von Claude für {Task} wurde nicht gespeichert; der KI-Aufruf läuft weiter.")]
    private static partial void LogWriteFailed(ILogger log, Exception ex, AiUsageTask task);
}
