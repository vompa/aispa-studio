using System.Text.Json;
using AispaStudio.Contracts;

namespace AispaStudio.Api.MailIntake.Assistant;

/// <summary>
/// Ein Sprachmodell, das auf eine Anfrage ein festes JSON-Schema füllt: Claude (<see cref="ClaudeJson"/>) oder das lokale
/// Modell (<see cref="LocalAi.LocalModelJson"/>). Das Modell liefert nur Daten und löst keine Aktionen aus. Die Aufgabe dient der
/// Verbrauchsmessung von Claude (T-112a).
/// </summary>
internal interface IAiJson
{
    Task<T> AskAsync<T>(AiUsageTask task, string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct);

    /// <summary>
    /// Wie oben, mit einem gleichbleibenden Teil (<paramref name="context"/>, z. B. dem Katalog) vor der Mail oder Frage. Claude legt ihn
    /// in den Cache (T-112b); andere Modelle bekommen beides als einen Text.
    /// </summary>
    Task<T> AskAsync<T>(AiUsageTask task, string system, string context, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct) =>
        AskAsync<T>(task, system, $"{context}\n{prompt}", schema, ct);
}

/// <summary>
/// Erst Claude, und wenn Claude nicht antwortet (kein Internet, Störung), das lokale Modell (ADR 0057). Nur für Aufgaben, die
/// ein kleines Modell gut genug kann: Terminwünsche und Fragen ans Team in Bausteine zerlegen.
/// </summary>
internal sealed partial class FallbackAiJson(IAiJson primary, IAiJson fallback, ILogger<FallbackAiJson> log) : IAiJson
{
    public Task<T> AskAsync<T>(AiUsageTask task, string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct) =>
        FirstAnswerAsync(model => model.AskAsync<T>(task, system, prompt, schema, ct), ct);

    public Task<T> AskAsync<T>(AiUsageTask task, string system, string context, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct) =>
        FirstAnswerAsync(model => model.AskAsync<T>(task, system, context, prompt, schema, ct), ct);

    private async Task<T> FirstAnswerAsync<T>(Func<IAiJson, Task<T>> ask, CancellationToken ct)
    {
        try
        {
            return await ask(primary);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogFallback(log, ex);
            return await ask(fallback);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claude hat nicht geantwortet; das lokale Modell übernimmt.")]
    private static partial void LogFallback(ILogger log, Exception ex);
}
