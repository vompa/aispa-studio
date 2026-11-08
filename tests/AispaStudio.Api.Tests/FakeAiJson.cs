using System.Collections.Concurrent;
using System.Text.Json;
using AispaStudio.Api.MailIntake.Assistant;

namespace AispaStudio.Api.Tests;

/// <summary>
/// KI-Attrappe für das Lesen von Treatwell-Mails: antwortet nur, wenn ein Test für eine Buchungsnummer eine Antwort vorbereitet hat
/// (die Buchungsnummer muss im Prompt stehen), sonst fällt sie aus wie ein nicht erreichbares Modell. Merkt sich die Anfragen.
/// </summary>
internal sealed class FakeAiJson : IAiJson
{
    public static readonly ConcurrentDictionary<string, object> Answers = new();
    public static readonly ConcurrentDictionary<string, (string System, string Prompt)> Requests = new();

    public Task<T> AskAsync<T>(string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken ct)
    {
        foreach (var (marker, answer) in Answers)
        {
            if (prompt.Contains(marker, StringComparison.Ordinal))
            {
                Requests[marker] = (system, prompt);
                return answer is T typed ? Task.FromResult(typed) : throw new InvalidOperationException("Falscher Antworttyp.");
            }
        }

        throw new InvalidOperationException("Die KI-Attrappe hat dafür keine Antwort.");
    }
}
