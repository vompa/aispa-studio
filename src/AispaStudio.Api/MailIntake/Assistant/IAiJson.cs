using System.Text.Json;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>
/// Ein Sprachmodell, das auf eine Anfrage ein festes JSON-Schema füllt: Claude (<see cref="ClaudeJson"/>) oder das lokale
/// Modell (<see cref="LocalAi.LocalModelJson"/>). Das Modell liefert nur Daten und löst keine Aktionen aus.
/// </summary>
internal interface IAiJson
{
    Task<T> AskAsync<T>(string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct);
}

/// <summary>
/// Erst Claude, und wenn Claude nicht antwortet (kein Internet, Störung), das lokale Modell (ADR 0057). Nur für Aufgaben, die
/// ein kleines Modell gut genug kann: Terminwünsche und Fragen ans Team in Bausteine zerlegen.
/// </summary>
internal sealed partial class FallbackAiJson(IAiJson primary, IAiJson fallback, ILogger<FallbackAiJson> log) : IAiJson
{
    public async Task<T> AskAsync<T>(string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct)
    {
        try
        {
            return await primary.AskAsync<T>(system, prompt, schema, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogFallback(log, ex);
            return await fallback.AskAsync<T>(system, prompt, schema, ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claude hat nicht geantwortet; das lokale Modell übernimmt.")]
    private static partial void LogFallback(ILogger log, Exception ex);
}
